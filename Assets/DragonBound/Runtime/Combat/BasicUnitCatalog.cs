using System;
using DragonBound.Core;
using DragonBound.Grid;

namespace DragonBound.Combat
{
    public readonly struct BasicUnitStats
    {
        public BasicUnitStats(
            BasicUnitArchetype archetype,
            int level,
            float attack,
            float attackSpeed,
            float rangeCells,
            AttackKind attackKind)
        {
            Archetype = archetype;
            Level = level;
            Attack = attack;
            AttackSpeed = attackSpeed;
            RangeCells = rangeCells;
            AttackKind = attackKind;
        }

        public BasicUnitArchetype Archetype { get; }
        public int Level { get; }
        public float Attack { get; }
        public float AttackSpeed { get; }
        public float RangeCells { get; }
        public AttackKind AttackKind { get; }
        public float AttackIntervalSeconds => 1f / AttackSpeed;
    }

    public static class BasicUnitCatalog
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        private static readonly float[] AxeAttack = { 3.00f, 4.50f, 6.30f, 8.19f, 10.24f };
        private static readonly float[] StandardAttack = { 2.00f, 3.00f, 4.20f, 5.46f, 6.82f };
        private static readonly float[] StandardSpeed = { 1.25f, 1.88f, 2.62f, 3.41f, 4.27f };
        private static readonly float[] SpearSpeed = { 1.38f, 2.06f, 2.89f, 3.75f, 4.69f };

        public static BasicUnitStats GetStats(string configId, int level)
        {
            if (level < MinLevel || level > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            var index = level - 1;
            switch (GetArchetype(configId))
            {
                case BasicUnitArchetype.Axe:
                    return new BasicUnitStats(
                        BasicUnitArchetype.Axe,
                        level,
                        AxeAttack[index],
                        StandardSpeed[index],
                        1.5f,
                        AttackKind.Single);
                case BasicUnitArchetype.Bow:
                    return new BasicUnitStats(
                        BasicUnitArchetype.Bow,
                        level,
                        StandardAttack[index],
                        StandardSpeed[index],
                        3.5f,
                        AttackKind.BowProjectile);
                case BasicUnitArchetype.Spear:
                    return new BasicUnitStats(
                        BasicUnitArchetype.Spear,
                        level,
                        StandardAttack[index],
                        SpearSpeed[index],
                        2.5f,
                        AttackKind.SpearPierce);
                case BasicUnitArchetype.Rider:
                    return new BasicUnitStats(
                        BasicUnitArchetype.Rider,
                        level,
                        StandardAttack[index],
                        StandardSpeed[index],
                        2f,
                        AttackKind.RiderSweep);
                default:
                    throw new ArgumentOutOfRangeException(nameof(configId));
            }
        }

        public static BasicUnitArchetype GetArchetype(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("A basic unit config id is required.", nameof(configId));
            }

            if (configId.IndexOf("longbow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BasicUnitArchetype.Bow;
            }

            if (configId.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BasicUnitArchetype.Spear;
            }

            if (configId.IndexOf("twinaxe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                configId.IndexOf("berserker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                configId.IndexOf("rider", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BasicUnitArchetype.Rider;
            }

            if (configId.IndexOf("axe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BasicUnitArchetype.Axe;
            }

            throw new ArgumentException($"Unknown basic unit config id: {configId}", nameof(configId));
        }

        public static string GetDisplayName(string configId)
        {
            switch (GetArchetype(configId))
            {
                case BasicUnitArchetype.Bow:
                    return "BOW";
                case BasicUnitArchetype.Spear:
                    return "SPEAR";
                case BasicUnitArchetype.Rider:
                    return "BERSERKER";
                default:
                    return "AXE";
            }
        }
    }
}
