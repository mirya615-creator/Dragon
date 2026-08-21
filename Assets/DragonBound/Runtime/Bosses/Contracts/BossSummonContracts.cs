using System;
using DragonBound.Core;

namespace DragonBound.Bosses.Contracts
{
    public enum BossSummonSpawnSource
    {
        BossSkill,
        BossPhase,
        BossPassive
    }

    public readonly struct BossSummonPolicy
    {
        public BossSummonPolicy(
            BossSummonSpawnSource spawnSource,
            int heroXpReward,
            int runResourceReward,
            bool despawnOnBossDeath,
            bool blocksWaveScheduleCompletion,
            bool persistsAcrossWave)
        {
            if (heroXpReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(heroXpReward));
            }

            if (runResourceReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runResourceReward));
            }

            SpawnSource = spawnSource;
            HeroXpReward = heroXpReward;
            RunResourceReward = runResourceReward;
            DespawnOnBossDeath = despawnOnBossDeath;
            BlocksWaveScheduleCompletion = blocksWaveScheduleCompletion;
            PersistsAcrossWave = persistsAcrossWave;
        }

        public BossSummonSpawnSource SpawnSource { get; }
        public int HeroXpReward { get; }
        public int RunResourceReward { get; }
        public bool DespawnOnBossDeath { get; }
        public bool BlocksWaveScheduleCompletion { get; }
        public bool PersistsAcrossWave { get; }
    }

    public readonly struct BossSummonDefinition
    {
        public BossSummonDefinition(
            BossId ownerBossId,
            string summonId,
            EnemyArchetype archetype,
            int count,
            float maxHitPoints,
            float moveSpeed,
            BossGoalEffect goalEffect,
            BossSummonPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(summonId))
            {
                throw new ArgumentException("A summon id is required.", nameof(summonId));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (maxHitPoints <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            }

            if (moveSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            }

            OwnerBossId = ownerBossId;
            SummonId = summonId;
            Archetype = archetype;
            Count = count;
            MaxHitPoints = maxHitPoints;
            MoveSpeed = moveSpeed;
            GoalEffect = goalEffect;
            Policy = policy;
        }

        public BossId OwnerBossId { get; }
        public string SummonId { get; }
        public EnemyArchetype Archetype { get; }
        public int Count { get; }
        public float MaxHitPoints { get; }
        public float MoveSpeed { get; }
        public BossGoalEffect GoalEffect { get; }
        public BossSummonPolicy Policy { get; }
    }
}
