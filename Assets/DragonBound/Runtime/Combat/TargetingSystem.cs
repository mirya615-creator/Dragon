using System;
using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;

namespace DragonBound.Combat
{
    public sealed class TargetingSystem
    {
        private const float RangeEpsilon = 0.0001f;

        public static CombatPoint FromBoardPosition(GridPosition position)
        {
            return new CombatPoint(position.X, 3f - position.Y);
        }

        public static CombatPoint FromBoardPosition(BoardGrid board, GridPosition position)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            return board.GetCombatPosition(position);
        }

        public bool IsWithinRange(CombatPoint attacker, CombatPoint target, float rangeCells)
        {
            if (rangeCells < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rangeCells));
            }

            return attacker.DistanceSquared(target) <= (rangeCells * rangeCells) + RangeEpsilon;
        }

        public bool IsWithinRange(CombatPoint attacker, EnemyRuntime target, float rangeCells)
        {
            return target != null && target.IsAlive && IsWithinRange(attacker, target.CombatPosition, rangeCells);
        }

        public EnemyRuntime SelectFrontmostInRange(
            CombatPoint attacker,
            float rangeCells,
            IEnumerable<EnemyRuntime> enemies)
        {
            var targets = SelectFrontmostInRange(attacker, rangeCells, enemies, 1);
            return targets.Count == 0 ? null : targets[0];
        }

        public List<EnemyRuntime> SelectFrontmostInRange(
            CombatPoint attacker,
            float rangeCells,
            IEnumerable<EnemyRuntime> enemies,
            int maximumTargets)
        {
            if (enemies == null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }

            if (maximumTargets < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTargets));
            }

            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in enemies)
            {
                if (IsWithinRange(attacker, enemy, rangeCells))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort(CompareFrontmost);
            if (candidates.Count > maximumTargets)
            {
                candidates.RemoveRange(maximumTargets, candidates.Count - maximumTargets);
            }

            return candidates;
        }

        public EnemyRuntime SelectEliteFirstInRange(
            CombatPoint attacker,
            float rangeCells,
            IEnumerable<EnemyRuntime> enemies)
        {
            if (enemies == null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }

            var candidates = new List<EnemyRuntime>();
            foreach (var enemy in enemies)
            {
                if (IsWithinRange(attacker, enemy, rangeCells))
                {
                    candidates.Add(enemy);
                }
            }

            candidates.Sort((first, second) =>
            {
                var elite = (second.Archetype == EnemyArchetype.Elite).CompareTo(
                    first.Archetype == EnemyArchetype.Elite);
                return elite != 0 ? elite : CompareFrontmost(first, second);
            });
            return candidates.Count == 0 ? null : candidates[0];
        }

        private static int CompareFrontmost(EnemyRuntime first, EnemyRuntime second)
        {
            var pathProgress = second.PathProgress.CompareTo(first.PathProgress);
            if (pathProgress != 0)
            {
                return pathProgress;
            }

            var spawnSequence = first.SpawnSequence.CompareTo(second.SpawnSequence);
            return spawnSequence != 0
                ? spawnSequence
                : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
        }
    }
}
