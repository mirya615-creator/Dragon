namespace DragonBound.Core
{
    public enum EnemyRuntimeState
    {
        Spawned,
        Moving,
        Dead,
        Leaked
    }

    public enum EnemyArchetype
    {
        Normal,
        Fast,
        Swarm,
        Elite,
        Boss
    }

    public enum EnemyLifecycleEventKind
    {
        Spawned,
        Killed,
        Leaked
    }

    public readonly struct EnemyLifecycleEvent
    {
        public EnemyLifecycleEvent(
            EnemyLifecycleEventKind kind,
            int spawnWave,
            string runtimeId,
            EnemyArchetype archetype,
            float maxHitPoints,
            float pathProgress,
            string bossId = "",
            float moveSpeedCellsPerSecond = 0f)
        {
            Kind = kind;
            SpawnWave = spawnWave;
            RuntimeId = runtimeId ?? string.Empty;
            Archetype = archetype;
            MaxHitPoints = maxHitPoints;
            PathProgress = pathProgress;
            BossId = bossId ?? string.Empty;
            MoveSpeedCellsPerSecond = moveSpeedCellsPerSecond;
        }

        public EnemyLifecycleEventKind Kind { get; }
        public int SpawnWave { get; }
        public string RuntimeId { get; }
        public EnemyArchetype Archetype { get; }
        public float MaxHitPoints { get; }
        public float PathProgress { get; }
        public string BossId { get; }
        public float MoveSpeedCellsPerSecond { get; }
    }

    public readonly struct EnemyGoalResolvedEvent
    {
        public EnemyGoalResolvedEvent(
            EnemyLifecycleEvent enemy,
            int heartBefore,
            int heartAfter,
            bool instantDefeat)
        {
            Enemy = enemy;
            HeartBefore = heartBefore;
            HeartAfter = heartAfter;
            InstantDefeat = instantDefeat;
        }

        public EnemyLifecycleEvent Enemy { get; }
        public int HeartBefore { get; }
        public int HeartAfter { get; }
        public bool InstantDefeat { get; }
    }

    public interface IPathProgress
    {
        int PathIndex { get; }
        float PathProgress { get; }
        float SegmentProgress { get; }
    }

    public interface IEnemyLifecycle
    {
        string RuntimeId { get; }
        EnemyRuntimeState State { get; }
        bool IsAlive { get; }
        bool HasResolved { get; }
    }

    public interface IEnemyGoalSettlement
    {
        void ResolveGoal(string enemyRuntimeId, EnemyArchetype archetype);
    }
}
