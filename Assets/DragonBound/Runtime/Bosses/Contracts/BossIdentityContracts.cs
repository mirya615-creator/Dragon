using System;
using DragonBound.Foundation.Contracts;

namespace DragonBound.Bosses.Contracts
{
    public readonly struct BossId : IEquatable<BossId>
    {
        public BossId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A Boss id is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool Equals(BossId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BossId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(BossId left, BossId right) => left.Equals(right);
        public static bool operator !=(BossId left, BossId right) => !left.Equals(right);
    }

    public static class FixedBossIds
    {
        public const string W6SoulchainBinderValue = "BOSS_SOULCHAIN_BINDER";
        public const string W12StormcallerPriestValue = "BOSS_STORMCALLER_PRIEST";
        public const string W16BloodcrownTyrantValue = "BOSS_BLOODCROWN_TYRANT";
        public const string W20WorldeaterWyrmValue = "BOSS_WORLDEATER_WYRM";

        public static BossId W6SoulchainBinder => new BossId(W6SoulchainBinderValue);
        public static BossId W12StormcallerPriest => new BossId(W12StormcallerPriestValue);
        public static BossId W16BloodcrownTyrant => new BossId(W16BloodcrownTyrantValue);
        public static BossId W20WorldeaterWyrm => new BossId(W20WorldeaterWyrmValue);
    }

    public readonly struct FixedBossIdentity
    {
        public FixedBossIdentity(BossId bossId, WaveNumber wave)
        {
            BossId = bossId;
            Wave = wave;
        }

        public BossId BossId { get; }
        public WaveNumber Wave { get; }
    }

    public static class FixedBosses
    {
        public static FixedBossIdentity W6 => new FixedBossIdentity(FixedBossIds.W6SoulchainBinder, new WaveNumber(6));
        public static FixedBossIdentity W12 => new FixedBossIdentity(FixedBossIds.W12StormcallerPriest, new WaveNumber(12));
        public static FixedBossIdentity W16 => new FixedBossIdentity(FixedBossIds.W16BloodcrownTyrant, new WaveNumber(16));
        public static FixedBossIdentity W20 => new FixedBossIdentity(FixedBossIds.W20WorldeaterWyrm, new WaveNumber(20));
    }

    public enum BossGoalEffect
    {
        None,
        HeartDamage,
        InstantDefeat
    }

    public readonly struct BossDefinition
    {
        public BossDefinition(
            BossId bossId,
            WaveNumber wave,
            float maxHitPoints,
            float moveSpeed,
            BossGoalEffect goalEffect,
            int heroXpReward)
        {
            if (maxHitPoints <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            }

            if (moveSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            }

            if (heroXpReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(heroXpReward));
            }

            BossId = bossId;
            Wave = wave;
            MaxHitPoints = maxHitPoints;
            MoveSpeed = moveSpeed;
            GoalEffect = goalEffect;
            HeroXpReward = heroXpReward;
        }

        public BossId BossId { get; }
        public WaveNumber Wave { get; }
        public float MaxHitPoints { get; }
        public float MoveSpeed { get; }
        public BossGoalEffect GoalEffect { get; }
        public int HeroXpReward { get; }
    }
}
