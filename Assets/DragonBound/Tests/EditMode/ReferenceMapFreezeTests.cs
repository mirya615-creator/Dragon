using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ReferenceMapFreezeTests
    {
        [Test]
        public void CoordinateConverterFlipsConfigRowExactlyOnce()
        {
            var runtime = LayoutCoordinateConverter.ToRuntime(7, 2);

            Assert.AreEqual(new GridPosition(2, 2), runtime);
            Assert.AreEqual(7, LayoutCoordinateConverter.ToConfigRow(runtime));
            Assert.AreEqual(2, LayoutCoordinateConverter.ToConfigColumn(runtime));
        }

        [Test]
        public void ConfigRowNineMapsToRuntimeYZero()
        {
            Assert.AreEqual(0, LayoutCoordinateConverter.ToRuntime(9, 0).Y);
        }

        [Test]
        public void ConfigRowZeroMapsToRuntimeYNine()
        {
            Assert.AreEqual(9, LayoutCoordinateConverter.ToRuntime(0, 0).Y);
        }

        [Test]
        public void FreezeInfoMatchesReferenceMapCandidate()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;

            var freezeInfo = Fixed8x10ReferenceMapFreezeInfo.Current;
            Assert.AreEqual(freezeInfo.LayoutId, layout.LayoutId);
            Assert.AreEqual(freezeInfo.BoardColumns, layout.Columns);
            Assert.AreEqual(freezeInfo.BoardRows, layout.Rows);
            Assert.AreEqual(freezeInfo.PlayerPathNodeCount, layout.PlayerLaneWaypoints.Count);
            Assert.AreEqual(freezeInfo.AiPathNodeCount, layout.AiLaneWaypoints.Count);
        }

        [Test]
        public void PlayerAndAiPathsAreRotationallyEquivalent()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
            for (var index = 0; index < layout.PlayerLaneWaypoints.Count; index++)
            {
                var player = layout.PlayerLaneWaypoints[index];
                var ai = layout.AiLaneWaypoints[index];
                Assert.AreEqual(
                    new GridPosition(7 - player.X, 9 - player.Y),
                    ai,
                    $"Path index {index}");
            }
        }

        [Test]
        public void RoadTileTypeMatchesOrderedPathNeighbors()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
            AssertRoadType(layout, 9, 0, FixedBoardRoadTileType.Spawn);
            AssertRoadType(layout, 6, 1, FixedBoardRoadTileType.StraightHorizontal);
            AssertRoadType(layout, 6, 4, FixedBoardRoadTileType.CornerLeftUp);
            AssertRoadType(layout, 5, 4, FixedBoardRoadTileType.CornerRightDown);
            AssertRoadType(layout, 9, 7, FixedBoardRoadTileType.Goal);
        }

        [Test]
        public void PlayerEnemyUsesNormalizedCumulativePathProgressAtCorners()
        {
            var path = new EnemyPath(
                new[] { "Spawn", "PathPoint_1", "PathPoint_2", "DragonGoal" },
                new[]
                {
                    new CombatPoint(0f, 0f),
                    new CombatPoint(0f, 1f),
                    new CombatPoint(1f, 1f),
                    new CombatPoint(1f, 2f)
                });
            var enemy = new EnemyRuntime("path.enemy", TeamSide.Player);
            path.PlaceAtSpawn(enemy);

            Assert.IsFalse(path.Advance(enemy, 1f, 4f));
            Assert.AreEqual(0, enemy.PathIndex);
            Assert.AreEqual(0.25f, enemy.PathProgress, 0.0001f);
            Assert.AreEqual(0.75f, enemy.SegmentProgress, 0.0001f);
            Assert.IsTrue(enemy.CombatPosition.Equals(new CombatPoint(0f, 0.75f)));

            Assert.IsFalse(path.Advance(enemy, 0.5f, 4f));
            Assert.AreEqual(0.375f, enemy.PathProgress, 0.0001f);
            Assert.AreEqual(1, enemy.PathIndex);
            Assert.IsTrue(enemy.CombatPosition.Equals(new CombatPoint(0.125f, 1f)));
            Assert.LessOrEqual(enemy.PathProgress, 1f);

            Assert.IsTrue(path.Advance(enemy, 2.5f, 4f));
            Assert.AreEqual(path.GoalIndex, enemy.PathIndex);
            Assert.AreEqual(1f, enemy.PathProgress, 0.0001f);
            Assert.IsTrue(enemy.CombatPosition.Equals(new CombatPoint(1f, 2f)));
        }

        [Test]
        public void FrontmostTargetUsesNormalizedPathProgress()
        {
            var targeting = new TargetingSystem();
            var rear = new EnemyRuntime("rear", TeamSide.Player);
            rear.SetTargetingState(12, 0.70f, new CombatPoint(1f, 0f));
            var front = new EnemyRuntime("front", TeamSide.Player);
            front.SetTargetingState(5, 0.75f, new CombatPoint(1f, 0f));

            Assert.AreSame(front, targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                2f,
                new[] { rear, front }));
        }

        [Test]
        public void LockedRoadSpawnAndGoalCellsRejectPlacementWhileUnlockedCellsAccept()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
            var board = DragonBoundBoardLayout.Create(layout, TeamSide.Player);

            var unlocked = LayoutCoordinateConverter.ToRuntime(7, 2);
            var locked = LayoutCoordinateConverter.ToRuntime(7, 1);
            var lane = LayoutCoordinateConverter.ToRuntime(8, 0);
            var spawn = LayoutCoordinateConverter.ToRuntime(9, 0);
            var goal = LayoutCoordinateConverter.ToRuntime(9, 7);

            Assert.IsTrue(board.TryPlace("unlocked", unlocked));
            Assert.IsFalse(board.TryPlace("locked", locked));
            Assert.IsFalse(board.TryPlace("lane", lane));
            Assert.IsFalse(board.TryPlace("spawn", spawn));
            Assert.IsFalse(board.TryPlace("goal", goal));
        }

        private static void AssertRoadType(
            FixedBoardLayoutDefinition layout,
            int configRow,
            int configColumn,
            FixedBoardRoadTileType expected)
        {
            var position = LayoutCoordinateConverter.ToRuntime(configRow, configColumn);
            Assert.IsTrue(layout.TryGetRoadTileType(position, out var actual));
            Assert.AreEqual(expected, actual);
        }
    }
}
