using DragonBound.Bosses.Contracts;
using DragonBound.Foundation.Contracts;

namespace DragonBound.Bosses.Runtime
{
    public static class WorldeaterWyrmConfiguration
    {
        public const string BossId = FixedBossIds.W20WorldeaterWyrmValue;
        public const string SkillId = "WORLDEATER_DEVOUR";
        public const string SummonSkillId = "WORLDEATER_MINION_SUMMON";
        public const string SubBossSummonSkillId = "WORLDEATER_SUB_BOSS_SUMMON";
        public const string MinionId = "WORLDEATER_MINION";
        public const string SubBossId = "WORLDEATER_SUB_BOSS";
        public const float GreyboxMaxHitPoints = 5000f;
        public const float BossMoveSpeedCellsPerSecond = 0.20f;
        public const float FirstDevourDelaySeconds = 10f;
        public const float DevourWindupSeconds = 1f;
        public const float DevourCooldownSeconds = 15f;
        public const float FirstSummonDelaySeconds = 12f;
        public const float SummonWindupSeconds = 0.75f;
        public const float SummonCooldownSeconds = 18f;
        public const int SummonCount = 4;
        public const float MinionMaxHitPoints = 330f;
        public const float MinionMoveSpeedCellsPerSecond = 0.75f;
        // Test-stage values; the server configuration may replace them later.
        public const float SubBossMaxHitPoints = 900f;
        public const float SubBossMoveSpeedCellsPerSecond = 0.45f;
        public const float BasicGrowthFraction = 0.05f;
        public const float MinionGrowthFraction = 0.03f;
        public const float SubBossGrowthFraction = 0.10f;
        public const float SpellbreakerReflectionFraction = 0.10f;
        public const int HeroXpReward = 20;

        public static BossDefinition CreateGreyboxDefinition()
        {
            return new BossDefinition(
                FixedBossIds.W20WorldeaterWyrm,
                new WaveNumber(20),
                GreyboxMaxHitPoints,
                BossMoveSpeedCellsPerSecond,
                BossGoalEffect.InstantDefeat,
                HeroXpReward);
        }

        public static BossSummonDefinition CreateMinionDefinition()
        {
            return new BossSummonDefinition(
                FixedBossIds.W20WorldeaterWyrm,
                MinionId,
                DragonBound.Core.EnemyArchetype.Swarm,
                SummonCount,
                MinionMaxHitPoints,
                MinionMoveSpeedCellsPerSecond,
                BossGoalEffect.InstantDefeat,
                new BossSummonPolicy(
                    BossSummonSpawnSource.BossSkill,
                    0,
                    0,
                    false,
                    false,
                    true));
        }

        public static BossSummonDefinition CreateSubBossDefinition()
        {
            return new BossSummonDefinition(
                FixedBossIds.W20WorldeaterWyrm,
                SubBossId,
                DragonBound.Core.EnemyArchetype.Boss,
                1,
                SubBossMaxHitPoints,
                SubBossMoveSpeedCellsPerSecond,
                BossGoalEffect.InstantDefeat,
                new BossSummonPolicy(
                    BossSummonSpawnSource.BossSkill,
                    0,
                    0,
                    false,
                    false,
                    true));
        }
    }
}
