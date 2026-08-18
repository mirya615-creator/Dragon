using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class FixedBoardLayoutDefinitionTests
    {
        [Test]
        public void FormalLayoutsUseOneEightByTenBoardAndCellSize()
        {
            foreach (var layout in BattlefieldLayoutDefinitions.FormalLayouts)
            {
                Assert.AreEqual(8, layout.Columns);
                Assert.AreEqual(10, layout.Rows);
                Assert.AreEqual(80, layout.CellDefinitions.Count);
                Assert.AreEqual(FixedBoardLayoutDefinition.LogicalCellSize, layout.CellSize);
                Assert.AreEqual(layout.Columns, layout.Width);
                Assert.AreEqual(layout.Rows, layout.Height);
            }
        }

        [Test]
        public void ReferenceMap01UsesTheFrozenRolesAndExplicitPaths()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;

            Assert.AreEqual(BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id, layout.LayoutId);
            AssertReferenceRole(layout, 0, 0, FixedBoardCellRole.Goal, FixedBoardCellOwner.AI);
            AssertReferenceRole(layout, 0, 7, FixedBoardCellRole.Spawn, FixedBoardCellOwner.AI);
            AssertReferenceRole(layout, 1, 3, FixedBoardCellRole.Deployment, FixedBoardCellOwner.AI, FixedBoardDeployState.Unlocked);
            AssertReferenceRole(layout, 4, 4, FixedBoardCellRole.Deployment, FixedBoardCellOwner.AI, FixedBoardDeployState.LockedUnlockable);
            AssertReferenceRole(layout, 5, 0, FixedBoardCellRole.Deployment, FixedBoardCellOwner.Player, FixedBoardDeployState.LockedUnlockable);
            AssertReferenceRole(layout, 9, 0, FixedBoardCellRole.Spawn, FixedBoardCellOwner.Player);
            AssertReferenceRole(layout, 9, 7, FixedBoardCellRole.Goal, FixedBoardCellOwner.Player);

            CollectionAssert.AreEqual(
                new[]
                {
                    new GridPosition(0, 0), new GridPosition(0, 1), new GridPosition(0, 2), new GridPosition(0, 3),
                    new GridPosition(1, 3), new GridPosition(2, 3), new GridPosition(3, 3), new GridPosition(4, 3),
                    new GridPosition(4, 4), new GridPosition(5, 4), new GridPosition(6, 4), new GridPosition(7, 4),
                    new GridPosition(7, 3), new GridPosition(7, 2), new GridPosition(7, 1), new GridPosition(7, 0)
                },
                layout.PlayerLaneWaypoints);
            CollectionAssert.AreEqual(
                new[]
                {
                    new GridPosition(7, 9), new GridPosition(7, 8), new GridPosition(7, 7), new GridPosition(7, 6),
                    new GridPosition(6, 6), new GridPosition(5, 6), new GridPosition(4, 6), new GridPosition(3, 6),
                    new GridPosition(3, 5), new GridPosition(2, 5), new GridPosition(1, 5), new GridPosition(0, 5),
                    new GridPosition(0, 6), new GridPosition(0, 7), new GridPosition(0, 8), new GridPosition(0, 9)
                },
                layout.AiLaneWaypoints);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new GridPosition(2, 2), new GridPosition(3, 2), new GridPosition(4, 2),
                    new GridPosition(2, 1), new GridPosition(3, 1), new GridPosition(4, 1)
                },
                layout.PlayerInitialUnlockedCells);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new GridPosition(3, 8), new GridPosition(4, 8), new GridPosition(5, 8),
                    new GridPosition(3, 7), new GridPosition(4, 7), new GridPosition(5, 7)
                },
                layout.AiInitialUnlockedCells);

            foreach (var playerCell in layout.GetPotentialDeploymentCells(TeamSide.Player))
            {
                Assert.IsTrue(layout.IsOwnedDeploymentCell(
                    layout.GetFairCounterpart(playerCell, TeamSide.Player),
                    TeamSide.AI));
            }
        }

        [Test]
        public void ReferenceMap01AssignsExactlyTheFrozenEightyCellRoleCounts()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
            var unlocked = 0;
            var locked = 0;
            var lane = 0;
            var spawn = 0;
            var goal = 0;
            foreach (var cell in layout.CellDefinitions)
            {
                Assert.IsTrue(cell.ArtSlotId.StartsWith("ART_"));
                switch (cell.Role)
                {
                    case FixedBoardCellRole.Deployment:
                        if (cell.DeployState == FixedBoardDeployState.Unlocked) unlocked++;
                        else if (cell.DeployState == FixedBoardDeployState.LockedUnlockable) locked++;
                        break;
                    case FixedBoardCellRole.Lane: lane++; break;
                    case FixedBoardCellRole.Spawn: spawn++; break;
                    case FixedBoardCellRole.Goal: goal++; break;
                    default: Assert.Fail($"Unexpected ReferenceMap01 role {cell.Role} at {cell.Coordinate}."); break;
                }

                var configRow = FixedBoardLayoutDefinition.ToConfigRow(cell.Coordinate);
                var counterpart = new GridPosition((layout.Columns - 1) - cell.Coordinate.X, (layout.Rows - 1) - cell.Coordinate.Y);
                Assert.IsTrue(layout.TryGetCellDefinition(counterpart, out var opposite));
                Assert.AreEqual(cell.Role, opposite.Role);
                Assert.AreEqual(cell.DeployState, opposite.DeployState);
                Assert.AreEqual(configRow < 5 ? FixedBoardCellOwner.AI : FixedBoardCellOwner.Player, cell.Owner);
            }

            Assert.AreEqual(12, unlocked);
            Assert.AreEqual(36, locked);
            Assert.AreEqual(28, lane);
            Assert.AreEqual(2, spawn);
            Assert.AreEqual(2, goal);
        }

        [Test]
        public void ReferenceMap01MatchesTheFrozenMaskCellByCell()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
            var rows = new[]
            {
                "GLLLLLLS",
                "RLLUUULR",
                "RLLUUULR",
                "RLLRRRRR",
                "RRRRLLLL",
                "LLLLRRRR",
                "RRRRRLLR",
                "RLUUULLR",
                "RLUUULLR",
                "SLLLLLLG"
            };

            for (var configRow = 0; configRow < rows.Length; configRow++)
            {
                for (var configColumn = 0; configColumn < rows[configRow].Length; configColumn++)
                {
                    var coordinate = FixedBoardLayoutDefinition.FromConfigCoordinate(configRow, configColumn);
                    Assert.IsTrue(layout.TryGetCellDefinition(coordinate, out var cell));
                    Assert.AreEqual(
                        configRow < 5 ? FixedBoardCellOwner.AI : FixedBoardCellOwner.Player,
                        cell.Owner,
                        $"R{configRow}C{configColumn}");
                    AssertFrozenRole(rows[configRow][configColumn], cell, configRow, configColumn);
                }
            }
        }

        [Test]
        public void ExistingFixedLayoutsRemainAvailableForRegressionCoverage()
        {
            Assert.IsTrue(BattlefieldLayoutDefinitions.TryGetFixed(
                BattlefieldLayoutDefinitions.Fixed8x10HorizontalStartId,
                out _));
            Assert.IsTrue(BattlefieldLayoutDefinitions.TryGetFixed(
                BattlefieldLayoutDefinitions.Fixed8x10VerticalStartId,
                out _));
            Assert.IsTrue(BattlefieldLayoutDefinitions.TryGetFixed(
                BattlefieldLayoutDefinitions.Fixed8x10BalancedStartId,
                out _));
        }

        [Test]
        public void FormalLayoutsPartitionConfiguredDeploymentCapacityForBothSides()
        {
            foreach (var layout in BattlefieldLayoutDefinitions.FormalLayouts)
            {
                AssertSideCapacity(layout, TeamSide.Player);
                AssertSideCapacity(layout, TeamSide.AI);
            }
        }

        [Test]
        public void FormalDeploymentMasksAreConnectedAndRotationallyEquivalent()
        {
            foreach (var layout in BattlefieldLayoutDefinitions.FormalLayouts)
            {
                if (layout.RequiresOrthogonalUnlockAdjacency)
                {
                    AssertConnected(layout.GetPotentialDeploymentCells(TeamSide.Player));
                    AssertConnected(layout.GetPotentialDeploymentCells(TeamSide.AI));
                }
                AssertConnected(layout.PlayerInitialUnlockedCells);
                AssertConnected(layout.AiInitialUnlockedCells);

                foreach (var position in layout.GetPotentialDeploymentCells(TeamSide.Player))
                {
                    var counterpart = layout.GetFairCounterpart(position, TeamSide.Player);
                    Assert.IsTrue(layout.IsOwnedDeploymentCell(counterpart, TeamSide.AI));
                }
            }
        }

        [Test]
        public void FormalLanesAreSeparateFromDeploymentAndHaveEqualLength()
        {
            foreach (var layout in BattlefieldLayoutDefinitions.FormalLayouts)
            {
                AssertLaneDoesNotOverlapDeployments(layout, layout.PlayerLaneWaypoints);
                AssertLaneDoesNotOverlapDeployments(layout, layout.AiLaneWaypoints);
                Assert.AreEqual(GetLength(layout.PlayerLaneWaypoints), GetLength(layout.AiLaneWaypoints), 0.0001f);
            }
        }

        [Test]
        public void HorizontalAndVerticalRecipesFitEveryInitialMask()
        {
            foreach (var layout in BattlefieldLayoutDefinitions.FormalLayouts)
            {
                Assert.IsTrue(HasAdjacentPair(layout.PlayerInitialUnlockedCells, 1, 0));
                Assert.IsTrue(HasAdjacentPair(layout.PlayerInitialUnlockedCells, 0, 1));
            }
        }

        [Test]
        public void BoardGridExposesOnlyOwnedDeploymentCellsAndRejectsLockedPlacement()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10HorizontalStart;
            var player = DragonBoundBoardLayout.Create(layout, TeamSide.Player);
            var ai = DragonBoundBoardLayout.Create(layout, TeamSide.AI);
            var locked = new GridPosition(3, 1);

            Assert.AreEqual(16, player.GetPositions(CellType.Battle).Count + player.GetPositions(CellType.Locked).Count);
            Assert.AreEqual(16, ai.GetPositions(CellType.Battle).Count + ai.GetPositions(CellType.Locked).Count);
            Assert.IsFalse(ai.TryGetCellType(new GridPosition(0, 1), out _));
            Assert.IsFalse(player.TryPlace("player.unit", locked));
            Assert.IsTrue(player.TryDebugUnlockCell(locked));
            Assert.IsTrue(player.TryPlace("player.unit", locked));
        }

        [Test]
        public void LegacyLayoutsRemainAvailableForRegressionCoverage()
        {
            Assert.IsTrue(BattlefieldLayoutDefinitions.TryGet(BattlefieldLayoutDefinitions.Legacy3x3Id, out var legacy));
            var board = DragonBoundBoardLayout.Create(legacy);
            Assert.AreEqual(3, legacy.Width);
            Assert.AreEqual(3, legacy.Height);
            Assert.AreEqual(6, board.UnlockedBattleCellCount);
        }

        private static void AssertSideCapacity(FixedBoardLayoutDefinition layout, TeamSide side)
        {
            var isReferenceMap = layout.LayoutId == BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id;
            Assert.AreEqual(isReferenceMap ? 24 : 16, layout.GetPotentialDeploymentCells(side).Count);
            Assert.AreEqual(6, layout.GetInitialUnlockedCells(side).Count);
            Assert.AreEqual(isReferenceMap ? 18 : 10, layout.GetUnlockableCells(side).Count);
        }

        private static void AssertReferenceRole(
            FixedBoardLayoutDefinition layout,
            int configRow,
            int configColumn,
            FixedBoardCellRole role,
            FixedBoardCellOwner owner,
            FixedBoardDeployState deployState = FixedBoardDeployState.NotApplicable)
        {
            var coordinate = FixedBoardLayoutDefinition.FromConfigCoordinate(configRow, configColumn);
            Assert.IsTrue(layout.TryGetCellDefinition(coordinate, out var cell));
            Assert.AreEqual(role, cell.Role);
            Assert.AreEqual(owner, cell.Owner);
            Assert.AreEqual(deployState, cell.DeployState);
        }

        private static void AssertFrozenRole(
            char expected,
            FixedBoardCellDefinition actual,
            int configRow,
            int configColumn)
        {
            var coordinate = $"R{configRow}C{configColumn}";
            switch (expected)
            {
                case 'U':
                    Assert.AreEqual(FixedBoardCellRole.Deployment, actual.Role, coordinate);
                    Assert.AreEqual(FixedBoardDeployState.Unlocked, actual.DeployState, coordinate);
                    break;
                case 'L':
                    Assert.AreEqual(FixedBoardCellRole.Deployment, actual.Role, coordinate);
                    Assert.AreEqual(FixedBoardDeployState.LockedUnlockable, actual.DeployState, coordinate);
                    break;
                case 'R':
                    Assert.AreEqual(FixedBoardCellRole.Lane, actual.Role, coordinate);
                    break;
                case 'S':
                    Assert.AreEqual(FixedBoardCellRole.Spawn, actual.Role, coordinate);
                    break;
                case 'G':
                    Assert.AreEqual(FixedBoardCellRole.Goal, actual.Role, coordinate);
                    break;
                default:
                    Assert.Fail($"Unexpected frozen-map role '{expected}' at {coordinate}.");
                    break;
            }
        }

        private static void AssertConnected(IReadOnlyList<GridPosition> positions)
        {
            var remaining = new HashSet<GridPosition>(positions);
            var queue = new Queue<GridPosition>();
            queue.Enqueue(positions[0]);
            remaining.Remove(positions[0]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var candidate in positions)
                {
                    if (remaining.Contains(candidate) && BoardGrid.AreOrthogonallyAdjacent(current, candidate))
                    {
                        remaining.Remove(candidate);
                        queue.Enqueue(candidate);
                    }
                }
            }

            Assert.IsEmpty(remaining);
        }

        private static void AssertLaneDoesNotOverlapDeployments(
            FixedBoardLayoutDefinition layout,
            IReadOnlyList<GridPosition> lane)
        {
            foreach (var position in lane)
            {
                Assert.IsTrue(layout.TryGetCellDefinition(position, out var definition));
                Assert.AreNotEqual(FixedBoardCellRole.Deployment, definition.Role);
            }
        }

        private static float GetLength(IReadOnlyList<GridPosition> points)
        {
            var length = 0f;
            for (var index = 0; index < points.Count - 1; index++)
            {
                var x = points[index + 1].X - points[index].X;
                var y = points[index + 1].Y - points[index].Y;
                length += UnityEngine.Mathf.Sqrt((x * x) + (y * y));
            }

            return length;
        }

        private static bool HasAdjacentPair(IReadOnlyList<GridPosition> positions, int xDelta, int yDelta)
        {
            foreach (var first in positions)
            {
                foreach (var second in positions)
                {
                    if (second.X - first.X == xDelta && second.Y - first.Y == yDelta)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

}
