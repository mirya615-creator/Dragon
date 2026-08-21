using System;

namespace DragonBound.Core
{
    /// <summary>Presentation-only contract shared by the compact verification slice and the formal pressure race.</summary>
    public interface IWaveRuntime : IWaveRuntimeStatus
    {
        EnemyRegistry PlayerEnemyRegistry { get; }
        EnemyRegistry AiEnemyRegistry { get; }

        event Action<CombatEvent> CombatEmitted;
    }
}
