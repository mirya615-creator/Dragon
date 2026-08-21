using System;
using System.Collections.Generic;

namespace DragonBound.Core
{
    public static class StormcallerPriestConfiguration
    {
        public const string BossId = BossExperienceRewards.StormcallerPriestBossId;
        public const float GreyboxMaxHitPoints = 1200f;
        public const float BossMoveSpeedCellsPerSecond = 0.20f;
        public const float FirstCastDelaySeconds = 7f;
        public const float CastWindupSeconds = 0.75f;
        public const float EffectDurationSeconds = 8f;
        public const float CooldownSeconds = 12f;
        public const float EffectRadiusCells = 2.5f;
        public const float ShieldValue = 60f;
        public const float MoveSpeedMultiplier = 1.15f;
        public const bool FormalHitPointsPending = true;
    }

    public enum StormcallerCastEventKind
    {
        CastStarted,
        WindupResolved,
        EffectApplied,
        EffectEnded,
        CastFailed,
        CooldownStarted
    }

    public readonly struct StormcallerCastEvent
    {
        public StormcallerCastEvent(
            StormcallerCastEventKind kind,
            int castNumber,
            float elapsedSeconds,
            int affectedCount,
            float reflectionDamage)
        {
            Kind = kind;
            CastNumber = castNumber;
            ElapsedSeconds = elapsedSeconds;
            AffectedCount = affectedCount;
            ReflectionDamage = reflectionDamage;
        }

        public StormcallerCastEventKind Kind { get; }
        public int CastNumber { get; }
        public float ElapsedSeconds { get; }
        public int AffectedCount { get; }
        public float ReflectionDamage { get; }
    }

    public sealed class StormcallerPriestRuntime
    {
        private readonly EnemyRuntime boss;
        private readonly TeamSide side;
        private readonly EnemyRegistry enemies;
        private readonly ISoulChainSpellbreakerResolver spellbreaker;
        private float elapsedSeconds;
        private float nextCastStart = StormcallerPriestConfiguration.FirstCastDelaySeconds;
        private float castStart;
        private float effectEnd;
        private float cooldownEnd;
        private bool castActive;
        private bool windupResolved;
        private bool effectActive;
        private bool bossDeathObserved;

        public StormcallerPriestRuntime(
            EnemyRuntime boss,
            TeamSide side,
            EnemyRegistry enemies,
            ISoulChainSpellbreakerResolver spellbreaker = null)
        {
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.side = side;
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.spellbreaker = spellbreaker;
        }

        public EnemyRuntime Boss => boss;
        public float ElapsedSeconds => elapsedSeconds;
        public bool IsCasting => castActive;
        public bool IsEffectActive => effectActive;
        public float CooldownRemainingSeconds => Math.Max(0f, cooldownEnd - elapsedSeconds);
        public int CastsStarted { get; private set; }
        public int CastsSucceeded { get; private set; }
        public int CastsFailed { get; private set; }
        public int LastAffectedCount { get; private set; }
        public event Action<StormcallerCastEvent> CastEvent;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || bossDeathObserved)
            {
                return;
            }

            if (!boss.IsAlive)
            {
                bossDeathObserved = true;
                castActive = false;
                effectActive = false;
                return;
            }

            var targetTime = elapsedSeconds + deltaSeconds;
            while (elapsedSeconds < targetTime - 0.0001f)
            {
                var nextBoundary = targetTime;
                if (!castActive)
                {
                    nextBoundary = Math.Min(nextBoundary, nextCastStart);
                }
                else if (!windupResolved)
                {
                    nextBoundary = Math.Min(nextBoundary, castStart + StormcallerPriestConfiguration.CastWindupSeconds);
                }
                else if (effectActive)
                {
                    nextBoundary = Math.Min(nextBoundary, effectEnd);
                }

                elapsedSeconds = nextBoundary;
                ProcessBoundary();
            }

            if (elapsedSeconds < targetTime - 0.0001f)
            {
                elapsedSeconds = targetTime;
                ProcessBoundary();
            }
        }

        private void ProcessBoundary()
        {
            if (!castActive && elapsedSeconds >= nextCastStart - 0.0001f)
            {
                BeginCast();
            }

            if (castActive && !windupResolved &&
                elapsedSeconds >= castStart + StormcallerPriestConfiguration.CastWindupSeconds - 0.0001f)
            {
                ResolveWindup();
            }

            if (castActive && effectActive && elapsedSeconds >= effectEnd - 0.0001f)
            {
                EndEffect();
            }
        }

        private void BeginCast()
        {
            castActive = true;
            windupResolved = false;
            effectActive = false;
            castStart = elapsedSeconds;
            CastsStarted++;
            Emit(StormcallerCastEventKind.CastStarted, 0f);
        }

        private void ResolveWindup()
        {
            windupResolved = true;
            Emit(StormcallerCastEventKind.WindupResolved, 0f);
            var context = new SoulChainBossCastContext(
                StormcallerPriestConfiguration.BossId,
                side,
                CastsStarted,
                boss.MaxHitPoints);
            if (spellbreaker != null && spellbreaker.ShouldBlockCast(context))
            {
                var reflectionDamage = boss.MaxHitPoints * 0.10f;
                boss.ApplyDamage(reflectionDamage);
                CastsFailed++;
                LastAffectedCount = 0;
                Emit(StormcallerCastEventKind.CastFailed, reflectionDamage);
                StartCooldown();
                return;
            }

            var affected = 0;
            foreach (var enemy in SnapshotEligibleTargets())
            {
                enemy.ApplyStormcallerShield(StormcallerPriestConfiguration.ShieldValue);
                enemy.ApplyStormcallerSpeedBuff(
                    StormcallerPriestConfiguration.MoveSpeedMultiplier,
                    StormcallerPriestConfiguration.EffectDurationSeconds);
                affected++;
            }

            LastAffectedCount = affected;
            CastsSucceeded++;
            effectActive = true;
            effectEnd = elapsedSeconds + StormcallerPriestConfiguration.EffectDurationSeconds;
            Emit(StormcallerCastEventKind.EffectApplied, 0f);
        }

        private List<EnemyRuntime> SnapshotEligibleTargets()
        {
            var result = new List<EnemyRuntime>();
            var radiusSquared = StormcallerPriestConfiguration.EffectRadiusCells *
                                StormcallerPriestConfiguration.EffectRadiusCells;
            foreach (var enemy in enemies.Snapshot())
            {
                if (enemy == null || !enemy.IsAlive || enemy.Team != side ||
                    enemy.Archetype != EnemyArchetype.Normal ||
                    boss.CombatPosition.DistanceSquared(enemy.CombatPosition) > radiusSquared + 0.0001f)
                {
                    continue;
                }

                result.Add(enemy);
            }

            return result;
        }

        private void EndEffect()
        {
            effectActive = false;
            castActive = false;
            Emit(StormcallerCastEventKind.EffectEnded, 0f);
            StartCooldown();
        }

        private void StartCooldown()
        {
            cooldownEnd = elapsedSeconds + StormcallerPriestConfiguration.CooldownSeconds;
            nextCastStart = cooldownEnd;
            Emit(StormcallerCastEventKind.CooldownStarted, 0f);
        }

        private void Emit(StormcallerCastEventKind kind, float reflectionDamage)
        {
            CastEvent?.Invoke(new StormcallerCastEvent(
                kind,
                CastsStarted,
                elapsedSeconds,
                LastAffectedCount,
                reflectionDamage));
        }
    }
}
