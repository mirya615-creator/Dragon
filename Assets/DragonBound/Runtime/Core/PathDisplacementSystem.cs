using System;

namespace DragonBound.Core
{
    /// <summary>
    /// Applies movement effects through an ordered EnemyPath, so units stay on their lane.
    /// </summary>
    public sealed class PathDisplacementSystem
    {
        private readonly EnemyPath path;

        public PathDisplacementSystem(EnemyPath path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public bool IsDisplacementImmune(EnemyRuntime enemy)
        {
            return enemy == null || enemy.Archetype == EnemyArchetype.Boss;
        }

        public bool MoveBackwardByPathDistance(EnemyRuntime enemy, float distance)
        {
            return !IsDisplacementImmune(enemy) && path.MoveBackwardByPathDistance(enemy, distance);
        }

        public bool ApplyMovementSlow(EnemyRuntime enemy, float slowFraction, float durationSeconds)
        {
            return enemy != null && enemy.ApplyMovementSlow(slowFraction, durationSeconds);
        }
    }
}
