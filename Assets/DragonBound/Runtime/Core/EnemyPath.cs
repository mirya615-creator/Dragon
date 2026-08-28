using System;
using System.Collections.Generic;
using DragonBound.Combat;

namespace DragonBound.Core
{
    public sealed class EnemyPath
    {
        private readonly string[] nodes;
        private readonly CombatPoint[] combatPositions;
        private readonly float[] cumulativeDistances;
        private readonly float[] segmentDistances;
        private readonly float totalDistance;

        public EnemyPath(IReadOnlyList<string> nodeNames, IReadOnlyList<CombatPoint> positions = null)
        {
            if (nodeNames == null || nodeNames.Count < 2)
            {
                throw new ArgumentException("An enemy path requires at least a spawn and a goal node.", nameof(nodeNames));
            }

            if (positions != null && positions.Count != nodeNames.Count)
            {
                throw new ArgumentException("Path names and combat positions must have equal counts.", nameof(positions));
            }

            nodes = new string[nodeNames.Count];
            combatPositions = new CombatPoint[nodeNames.Count];
            cumulativeDistances = new float[nodeNames.Count];
            segmentDistances = new float[nodeNames.Count - 1];
            for (var index = 0; index < nodeNames.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(nodeNames[index]))
                {
                    throw new ArgumentException("Enemy path node names cannot be empty.", nameof(nodeNames));
                }

                nodes[index] = nodeNames[index];
                combatPositions[index] = positions == null
                    ? new CombatPoint(index, 0f)
                    : positions[index];
            }

            if (!string.Equals(nodes[nodes.Length - 1], "DragonGoal", StringComparison.Ordinal))
            {
                throw new ArgumentException("The final enemy path node must be DragonGoal.", nameof(nodeNames));
            }

            var distance = 0f;
            for (var index = 0; index < segmentDistances.Length; index++)
            {
                var deltaX = combatPositions[index + 1].X - combatPositions[index].X;
                var deltaY = combatPositions[index + 1].Y - combatPositions[index].Y;
                var length = (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                if (length <= 0.0001f)
                {
                    throw new ArgumentException("Enemy path segments cannot have zero length.", nameof(positions));
                }

                segmentDistances[index] = length;
                distance += length;
                cumulativeDistances[index + 1] = distance;
            }

            totalDistance = distance;
        }

        public int NodeCount => nodes.Length;
        public int GoalIndex => nodes.Length - 1;
        public string GoalNode => nodes[GoalIndex];
        public IReadOnlyList<string> Nodes => nodes;
        public float TotalDistance => totalDistance;

        public CombatPoint GetNodeCombatPosition(int index)
        {
            if (index < 0 || index >= combatPositions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return combatPositions[index];
        }

        public void PlaceAtSpawn(EnemyRuntime enemy)
        {
            if (enemy == null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            enemy.PathIndex = 0;
            enemy.SetPathState(0, 0f, 0f, GoalIndex, combatPositions[0]);
        }

        public CombatPoint GetCombatPosition(EnemyRuntime enemy)
        {
            if (enemy == null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            if (enemy.PathIndex >= GoalIndex)
            {
                return combatPositions[GoalIndex];
            }

            return CombatPoint.Lerp(
                combatPositions[enemy.PathIndex],
                combatPositions[enemy.PathIndex + 1],
                enemy.SegmentProgress);
        }

        public bool Advance(EnemyRuntime enemy, float deltaSeconds, float travelSeconds)
        {
            if (enemy == null || enemy.HasResolved || deltaSeconds <= 0f)
            {
                return false;
            }

            if (travelSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(travelSeconds));
            }

            var pathProgress = Math.Max(0f, Math.Min(1f,
                enemy.PathProgress + (deltaSeconds / travelSeconds)));
            if (pathProgress >= 1f)
            {
                enemy.SetPathState(GoalIndex, 0f, 1f, GoalIndex, combatPositions[GoalIndex]);
                return true;
            }

            var traveledDistance = pathProgress * totalDistance;
            var segmentIndex = ResolveSegmentIndex(traveledDistance);
            var segmentProgress = Math.Max(0f, Math.Min(1f,
                (traveledDistance - cumulativeDistances[segmentIndex]) /
                segmentDistances[segmentIndex]));
            var position = CombatPoint.Lerp(
                combatPositions[segmentIndex],
                combatPositions[segmentIndex + 1],
                segmentProgress);
            enemy.SetPathState(
                segmentIndex,
                segmentProgress,
                pathProgress,
                GoalIndex,
                position);
            enemy.State = EnemyRuntimeState.Moving;
            return false;
        }

        public bool MoveBackwardByPathDistance(EnemyRuntime enemy, float distance)
        {
            if (enemy == null || enemy.HasResolved || distance <= 0f)
            {
                return false;
            }

            var targetProgress = Math.Max(0f, enemy.PathProgress - (distance / totalDistance));
            if (targetProgress >= enemy.PathProgress - 0.0001f)
            {
                return false;
            }

            SetPathProgress(enemy, targetProgress);
            return true;
        }

        private void SetPathProgress(EnemyRuntime enemy, float pathProgress)
        {
            if (pathProgress <= 0f)
            {
                enemy.SetPathState(0, 0f, 0f, GoalIndex, combatPositions[0]);
                return;
            }

            var traveledDistance = pathProgress * totalDistance;
            var segmentIndex = ResolveSegmentIndex(traveledDistance);
            var segmentProgress = Math.Max(0f, Math.Min(1f,
                (traveledDistance - cumulativeDistances[segmentIndex]) /
                segmentDistances[segmentIndex]));
            enemy.SetPathState(
                segmentIndex,
                segmentProgress,
                pathProgress,
                GoalIndex,
                CombatPoint.Lerp(
                    combatPositions[segmentIndex],
                    combatPositions[segmentIndex + 1],
                    segmentProgress));
            enemy.State = EnemyRuntimeState.Moving;
        }

        private int ResolveSegmentIndex(float traveledDistance)
        {
            for (var index = 0; index < segmentDistances.Length - 1; index++)
            {
                if (traveledDistance < cumulativeDistances[index + 1] - 0.0001f)
                {
                    return index;
                }
            }

            return segmentDistances.Length - 1;
        }
    }
}
