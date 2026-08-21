using System.Collections.Generic;
using DragonBound.Combat;

namespace DragonBound.Core
{
    /// <summary>Core-owned adapter that keeps EnemyRuntime out of Runes.Runtime.</summary>
    public sealed class EnemyRuntimeRuneCombatTarget : IRuneCombatTarget
    {
        private readonly EnemyRuntime enemy;

        public EnemyRuntimeRuneCombatTarget(EnemyRuntime enemy)
        {
            this.enemy = enemy;
        }

        public string RuntimeId => enemy?.RuntimeId ?? string.Empty;
        public bool IsAlive => enemy != null && enemy.IsAlive;
        public bool IsBoss => enemy != null && enemy.Archetype == EnemyArchetype.Boss;
        public float PathProgress => enemy?.PathProgress ?? 0f;
        public CombatPoint CombatPosition => enemy == null ? default(CombatPoint) : enemy.CombatPosition;

        public RuneDamageApplication ApplyRuneDamage(float damage)
        {
            if (enemy == null)
            {
                return new RuneDamageApplication(damage, 0f, 0f, false);
            }

            var application = enemy.ApplyDamage(damage);
            return new RuneDamageApplication(
                application.Requested,
                application.ShieldDamage,
                application.HealthDamage,
                application.Killed);
        }

        public bool TryApplyRuneSlow(float slowFraction, float durationSeconds)
        {
            return enemy != null && enemy.ApplyMovementSlow(slowFraction, durationSeconds);
        }
    }

    /// <summary>Snapshot adapter for future RuneEffectExecutor injection.</summary>
    public sealed class EnemyRegistryRuneCombatTargetRegistry : IRuneCombatTargetRegistry
    {
        private readonly EnemyRegistry registry;

        public EnemyRegistryRuneCombatTargetRegistry(EnemyRegistry registry)
        {
            this.registry = registry;
        }

        public IReadOnlyList<IRuneCombatTarget> Snapshot()
        {
            var result = new List<IRuneCombatTarget>();
            if (registry == null) return result;
            foreach (var enemy in registry.Snapshot())
            {
                result.Add(new EnemyRuntimeRuneCombatTarget(enemy));
            }

            return result;
        }
    }
}
