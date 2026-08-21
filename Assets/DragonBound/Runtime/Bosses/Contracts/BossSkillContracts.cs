using System;
using DragonBound.Combat;
using DragonBound.Core;

namespace DragonBound.Bosses.Contracts
{
    public readonly struct BossSkillId : IEquatable<BossSkillId>
    {
        public BossSkillId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A Boss skill id is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool Equals(BossSkillId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BossSkillId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(BossSkillId left, BossSkillId right) => left.Equals(right);
        public static bool operator !=(BossSkillId left, BossSkillId right) => !left.Equals(right);
    }

    public enum BossSkillLifecycle
    {
        Start,
        Windup,
        Resolve,
        Blocked,
        Cooldown
    }

    public enum BossCastOutcome
    {
        Resolved,
        Blocked,
        Skipped,
        Invalid
    }

    public enum SpellbreakerOutcome
    {
        NotEvaluated,
        Passed,
        Blocked
    }

    public readonly struct BossSkillLifecycleEvent
    {
        public BossSkillLifecycleEvent(
            BossId bossId,
            BossSkillId skillId,
            int attemptNumber,
            BossSkillLifecycle lifecycle,
            float elapsedSeconds)
        {
            if (attemptNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber));
            }

            if (elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            BossId = bossId;
            SkillId = skillId;
            AttemptNumber = attemptNumber;
            Lifecycle = lifecycle;
            ElapsedSeconds = elapsedSeconds;
        }

        public BossId BossId { get; }
        public BossSkillId SkillId { get; }
        public int AttemptNumber { get; }
        public BossSkillLifecycle Lifecycle { get; }
        public float ElapsedSeconds { get; }
    }

    public readonly struct BossCastAttempt
    {
        public BossCastAttempt(
            BossId bossId,
            BossSkillId skillId,
            int attemptNumber,
            float elapsedSeconds,
            bool targetLocked,
            bool spellbreakerEligible)
        {
            if (attemptNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber));
            }

            BossId = bossId;
            SkillId = skillId;
            AttemptNumber = attemptNumber;
            ElapsedSeconds = elapsedSeconds;
            TargetLocked = targetLocked;
            SpellbreakerEligible = spellbreakerEligible;
        }

        public BossId BossId { get; }
        public BossSkillId SkillId { get; }
        public int AttemptNumber { get; }
        public float ElapsedSeconds { get; }
        public bool TargetLocked { get; }
        public bool SpellbreakerEligible { get; }
    }

    public readonly struct BossCastResult
    {
        public BossCastResult(
            BossCastAttempt attempt,
            BossCastOutcome outcome,
            SpellbreakerOutcome spellbreakerOutcome,
            float reflectedDamage,
            BossGoalEffect goalEffect,
            bool rewardGranted)
        {
            if (reflectedDamage < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(reflectedDamage));
            }

            Attempt = attempt;
            Outcome = outcome;
            SpellbreakerOutcome = spellbreakerOutcome;
            ReflectedDamage = reflectedDamage;
            GoalEffect = goalEffect;
            RewardGranted = rewardGranted;
        }

        public BossCastAttempt Attempt { get; }
        public BossCastOutcome Outcome { get; }
        public SpellbreakerOutcome SpellbreakerOutcome { get; }
        public float ReflectedDamage { get; }
        public BossGoalEffect GoalEffect { get; }
        public bool RewardGranted { get; }
        public bool WasBlocked => Outcome == BossCastOutcome.Blocked || SpellbreakerOutcome == SpellbreakerOutcome.Blocked;
    }

    public interface IBossSkillLifecycleSink
    {
        void Publish(BossSkillLifecycleEvent value);
    }

    public readonly struct BossLastHitXpAward
    {
        public BossLastHitXpAward(BossId bossId, int xpAmount, CombatDamageOwner lastHitOwner, bool formalLastHit)
        {
            if (xpAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(xpAmount));
            }

            BossId = bossId;
            XpAmount = xpAmount;
            LastHitOwner = lastHitOwner;
            FormalLastHit = formalLastHit;
        }

        public BossId BossId { get; }
        public int XpAmount { get; }
        public CombatDamageOwner LastHitOwner { get; }
        public bool FormalLastHit { get; }
        public bool GrantedToHero => FormalLastHit && XpAmount > 0 &&
            LastHitOwner.Kind == CombatDamageOwnerKind.Hero &&
            !string.IsNullOrEmpty(LastHitOwner.HeroId);
    }
}
