using System;
using DragonBound.Bosses.Contracts;
using DragonBound.Core;

namespace DragonBound.Bosses.Runtime
{
    public interface IBloodcrownBossTarget
    {
        float MaxHitPoints { get; }
        bool IsAlive { get; }
        void ApplyReflectedDamage(float damage);
    }

    /// <summary>
    /// Host port for the W16 global Basic policy. Implementations must apply the override to
    /// current units and retain it for Basic units registered during the Boss lifetime.
    /// </summary>
    public interface IBloodcrownBasicPolicyPort
    {
        bool IsDecreeActive { get; }
        int EffectiveCombatLevel { get; }
        bool IsMergeBlocked { get; }
        void EnableDecree(int effectiveCombatLevel);
        void DisableDecree();
        void SetMergeBlocked(bool blocked);
    }

    public interface IBloodcrownSpellbreaker
    {
        SpellbreakerOutcome Evaluate(BossCastAttempt attempt);
    }

    public sealed class BloodcrownTyrantRuntime
    {
        private readonly BossDefinition definition;
        private readonly IBloodcrownBossTarget boss;
        private readonly IBloodcrownBasicPolicyPort basicPolicy;
        private readonly IBloodcrownSpellbreaker spellbreaker;
        private readonly BossSkillId skillId = new BossSkillId(BloodcrownTyrantConfiguration.SkillId);
        private float elapsedSeconds;
        private float nextCastStart = BloodcrownTyrantConfiguration.FirstCastDelaySeconds;
        private float windupEnd;
        private int castAttemptNumber;
        private bool castActive;
        private bool decreeApplied;
        private bool deathHandled;

        public BloodcrownTyrantRuntime(
            BossDefinition definition,
            IBloodcrownBossTarget boss,
            IBloodcrownBasicPolicyPort basicPolicy,
            IBloodcrownSpellbreaker spellbreaker = null)
        {
            if (definition.BossId != FixedBossIds.W16BloodcrownTyrant || definition.Wave.Value != 16)
            {
                throw new ArgumentException("Bloodcrown runtime requires the fixed W16 Boss identity.", nameof(definition));
            }

            if (definition.GoalEffect != BossGoalEffect.InstantDefeat)
            {
                throw new ArgumentException("Bloodcrown runtime requires InstantDefeat as its GoalEffect.", nameof(definition));
            }

            this.definition = definition;
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.basicPolicy = basicPolicy ?? throw new ArgumentNullException(nameof(basicPolicy));
            this.spellbreaker = spellbreaker;
        }

        public event Action<BossSkillLifecycleEvent> LifecycleEmitted;
        public event Action<BossCastResult> CastResultEmitted;

        public BossDefinition Definition => definition;
        public float ElapsedSeconds => elapsedSeconds;
        public int CastAttemptCount => castAttemptNumber;
        public bool IsDecreeApplied => decreeApplied;
        public bool IsDead => deathHandled;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (deathHandled)
            {
                return;
            }

            if (!boss.IsAlive)
            {
                HandleBossDeath();
                return;
            }

            var targetTime = elapsedSeconds + deltaSeconds;
            while (elapsedSeconds < targetTime - 0.0001f && !deathHandled)
            {
                var nextBoundary = targetTime;
                if (!decreeApplied)
                {
                    nextBoundary = castActive
                        ? Math.Min(nextBoundary, windupEnd)
                        : Math.Min(nextBoundary, nextCastStart);
                }

                elapsedSeconds = nextBoundary;
                ProcessBoundary();
            }

            if (!deathHandled && elapsedSeconds < targetTime - 0.0001f)
            {
                elapsedSeconds = targetTime;
                ProcessBoundary();
            }
        }

        public BossLastHitXpAward CreateLastHitXpAward(CombatDamageOwner lastHitOwner, bool formalLastHit)
        {
            return new BossLastHitXpAward(
                definition.BossId,
                definition.HeroXpReward,
                lastHitOwner,
                formalLastHit);
        }

        private void BeginCast()
        {
            castAttemptNumber++;
            castActive = true;
            var castStart = nextCastStart;
            windupEnd = castStart + BloodcrownTyrantConfiguration.CastWindupSeconds;
            var attempt = new BossCastAttempt(
                definition.BossId,
                skillId,
                castAttemptNumber,
                castStart,
                true,
                true);
            EmitLifecycle(attempt, BossSkillLifecycle.Start);
            EmitLifecycle(attempt, BossSkillLifecycle.Windup);
        }

        private void ProcessBoundary()
        {
            if (!boss.IsAlive)
            {
                HandleBossDeath();
                return;
            }

            if (decreeApplied)
            {
                return;
            }

            if (!castActive && elapsedSeconds + 0.0001f >= nextCastStart)
            {
                BeginCast();
            }

            if (castActive && elapsedSeconds + 0.0001f >= windupEnd)
            {
                ResolveCast();
            }
        }

        private void ResolveCast()
        {
            castActive = false;
            var resolvedAt = windupEnd;
            var attempt = new BossCastAttempt(
                definition.BossId,
                skillId,
                castAttemptNumber,
                resolvedAt,
                true,
                true);

            if (!boss.IsAlive)
            {
                EmitResult(new BossCastResult(
                    attempt,
                    BossCastOutcome.Skipped,
                    SpellbreakerOutcome.NotEvaluated,
                    0f,
                    BossGoalEffect.None,
                    false));
                return;
            }

            var breakerOutcome = spellbreaker == null
                ? SpellbreakerOutcome.NotEvaluated
                : spellbreaker.Evaluate(attempt);
            if (breakerOutcome == SpellbreakerOutcome.Blocked)
            {
                var reflectedDamage = boss.MaxHitPoints * BloodcrownTyrantConfiguration.SpellbreakerReflectionFraction;
                boss.ApplyReflectedDamage(reflectedDamage);
                EmitLifecycle(attempt, BossSkillLifecycle.Blocked);
                EmitResult(new BossCastResult(
                    attempt,
                    BossCastOutcome.Blocked,
                    breakerOutcome,
                    reflectedDamage,
                    BossGoalEffect.None,
                    false));
                EmitLifecycle(attempt, BossSkillLifecycle.Cooldown);
                nextCastStart = resolvedAt + BloodcrownTyrantConfiguration.RetryCooldownSeconds;
                return;
            }

            basicPolicy.EnableDecree(BloodcrownTyrantConfiguration.EffectiveCombatLevel);
            basicPolicy.SetMergeBlocked(true);
            decreeApplied = true;
            EmitLifecycle(attempt, BossSkillLifecycle.Resolve);
            EmitResult(new BossCastResult(
                attempt,
                BossCastOutcome.Resolved,
                breakerOutcome,
                0f,
                BossGoalEffect.None,
                false));
        }

        private void HandleBossDeath()
        {
            deathHandled = true;
            castActive = false;
            if (decreeApplied || basicPolicy.IsDecreeActive || basicPolicy.IsMergeBlocked)
            {
                basicPolicy.DisableDecree();
                basicPolicy.SetMergeBlocked(false);
            }

            decreeApplied = false;
        }

        private void EmitLifecycle(BossCastAttempt attempt, BossSkillLifecycle lifecycle)
        {
            LifecycleEmitted?.Invoke(new BossSkillLifecycleEvent(
                attempt.BossId,
                attempt.SkillId,
                attempt.AttemptNumber,
                lifecycle,
                attempt.ElapsedSeconds));
        }

        private void EmitResult(BossCastResult result)
        {
            CastResultEmitted?.Invoke(result);
        }
    }
}
