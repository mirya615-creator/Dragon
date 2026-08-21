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
            float pathProgress)
        {
            Kind = kind;
            SpawnWave = spawnWave;
            RuntimeId = runtimeId ?? string.Empty;
            Archetype = archetype;
            MaxHitPoints = maxHitPoints;
            PathProgress = pathProgress;
        }

        public EnemyLifecycleEventKind Kind { get; }
        public int SpawnWave { get; }
        public string RuntimeId { get; }
        public EnemyArchetype Archetype { get; }
        public float MaxHitPoints { get; }
        public float PathProgress { get; }
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
