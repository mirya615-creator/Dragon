using System.Collections.Generic;
using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class BoardGridTests
    {
        [Test]
        public void InitialLayoutContainsThreeByThreeFormationAndFiveBenchCells()
        {
            var grid = DragonBoundBoardLayout.CreateInitial();

            Assert.AreEqual(6, grid.GetPositions(CellType.Battle).Count);
            Assert.AreEqual(5, grid.GetPositions(CellType.Bench).Count);
            Assert.AreEqual(3, grid.GetPositions(CellType.Locked).Count);
            Assert.AreEqual(14, grid.CellCount);
            foreach (var locked in grid.GetPositions(CellType.Locked))
            {
                Assert.AreEqual(3, locked.Y);
            }
        }

        [Test]
        public void AiFormationDoesNotContainInvisibleBenchCells()
        {
            var grid = DragonBoundBoardLayout.CreateFormationOnly();

            Assert.AreEqual(6, grid.GetPositions(CellType.Battle).Count);
            Assert.AreEqual(3, grid.GetPositions(CellType.Locked).Count);
            Assert.AreEqual(0, grid.GetPositions(CellType.Bench).Count);
            Assert.AreEqual(9, grid.CellCount);
        }

        [Test]
        public void PlacementRejectsLockedOccupiedAndDuplicateUnitCells()
        {
            var battle = new GridPosition(0, 0);
            var locked = new GridPosition(1, 0);
            var otherBattle = new GridPosition(2, 0);
            var grid = new BoardGrid(new[]
            {
                Cell(battle, CellType.Battle),
                Cell(locked, CellType.Locked),
                Cell(otherBattle, CellType.Battle)
            });

            Assert.IsTrue(grid.TryPlace("unit.a", battle));
            Assert.IsFalse(grid.TryPlace("unit.b", battle));
            Assert.IsFalse(grid.TryPlace("unit.b", locked));
            Assert.IsFalse(grid.TryPlace("unit.a", otherBattle));
        }

        [Test]
        public void UnlockMakesReservedCellPlaceable()
        {
            var grid = DragonBoundBoardLayout.CreateInitial();
            var locked = grid.GetPositions(CellType.Locked)[0];

            Assert.IsFalse(grid.TryPlace("unit.a", locked));
            Assert.IsTrue(grid.TryUnlock(locked));
            Assert.IsTrue(grid.TryPlace("unit.a", locked));
            Assert.AreEqual(7, grid.GetPositions(CellType.Battle).Count);
        }

        [Test]
        public void SuccessfulMutationsPublishStableSequence()
        {
            var grid = DragonBoundBoardLayout.CreateInitial();
            var events = new List<GridMutation>();
            grid.Changed += events.Add;
            var bench = grid.GetPositions(CellType.Bench)[0];
            var battle = grid.GetPositions(CellType.Battle)[0];
            var locked = grid.GetPositions(CellType.Locked)[0];

            Assert.IsTrue(grid.TryPlace("unit.a", bench));
            Assert.IsFalse(grid.TryPlace("unit.b", bench));
            Assert.IsTrue(grid.TryMove(bench, battle));
            Assert.IsTrue(grid.TryUnlock(locked));

            Assert.AreEqual(3, events.Count);
            Assert.AreEqual(1, events[0].Sequence);
            Assert.AreEqual(GridMutationKind.Placed, events[0].Kind);
            Assert.AreEqual(2, events[1].Sequence);
            Assert.AreEqual(GridMutationKind.Moved, events[1].Kind);
            Assert.AreEqual(3, events[2].Sequence);
            Assert.AreEqual(GridMutationKind.CellUnlocked, events[2].Kind);
        }

        [Test]
        public void OccupiedBattleCellsCanSwapAtomically()
        {
            var grid = DragonBoundBoardLayout.CreateInitial();
            var battle = grid.GetPositions(CellType.Battle);
            Assert.IsTrue(grid.TryPlace("unit.a", battle[0]));
            Assert.IsTrue(grid.TryPlace("unit.b", battle[1]));

            Assert.IsTrue(grid.TrySwap(battle[0], battle[1]));
            Assert.IsTrue(grid.TryGetPosition("unit.a", out var first));
            Assert.IsTrue(grid.TryGetPosition("unit.b", out var second));
            Assert.AreEqual(battle[1], first);
            Assert.AreEqual(battle[0], second);
        }

        [Test]
        public void OrthogonalAdjacencyExcludesDiagonalAndSeparatedCells()
        {
            var origin = new GridPosition(1, 1);

            Assert.IsTrue(BoardGrid.AreOrthogonallyAdjacent(origin, new GridPosition(2, 1)));
            Assert.IsFalse(BoardGrid.AreOrthogonallyAdjacent(origin, new GridPosition(2, 2)));
            Assert.IsFalse(BoardGrid.AreOrthogonallyAdjacent(origin, new GridPosition(3, 1)));
        }

        [Test]
        public void ComponentsOccupySeparateCellsAndAppearSeparately()
        {
            var grid = DragonBoundBoardLayout.CreateInitial();
            var battle = grid.GetPositions(CellType.Battle);

            Assert.IsTrue(grid.TryPlace("sigil", battle[0]));
            Assert.IsTrue(grid.TryPlace("ranger", battle[1]));
            Assert.IsTrue(grid.TryGetOccupant(battle[0], out var first));
            Assert.IsTrue(grid.TryGetOccupant(battle[1], out var second));
            Assert.AreEqual("sigil", first);
            Assert.AreEqual("ranger", second);
            Assert.AreEqual(2, grid.GetOccupants().Count);
        }

        [Test]
        public void MovingOneComponentLeavesPartnerInOriginalCell()
        {
            var grid = DragonBoundBoardLayout.CreateInitial();
            var battle = grid.GetPositions(CellType.Battle);
            Assert.IsTrue(grid.TryPlace("sigil", battle[0]));
            Assert.IsTrue(grid.TryPlace("ranger", battle[1]));

            Assert.IsTrue(grid.TryMove(battle[0], battle[2]));
            Assert.IsFalse(grid.TryGetOccupant(battle[0], out _));
            Assert.IsTrue(grid.TryGetOccupant(battle[1], out var partner));
            Assert.IsTrue(grid.TryGetOccupant(battle[2], out var moved));
            Assert.AreEqual("ranger", partner);
            Assert.AreEqual("sigil", moved);
        }

        [Test]
        public void RemovingOneComponentDoesNotRemovePartner()
        {
            var grid = DragonBoundBoardLayout.CreateInitial();
            var battle = grid.GetPositions(CellType.Battle);
            Assert.IsTrue(grid.TryPlace("sigil", battle[0]));
            Assert.IsTrue(grid.TryPlace("ranger", battle[1]));

            Assert.IsTrue(grid.TryRemoveAt(battle[0]));

            Assert.IsFalse(grid.TryGetOccupant(battle[0], out _));
            Assert.IsTrue(grid.TryGetOccupant(battle[1], out var partner));
            Assert.AreEqual("ranger", partner);
            Assert.AreEqual(1, grid.GetOccupants().Count);
        }

        private static KeyValuePair<GridPosition, CellType> Cell(GridPosition position, CellType type)
        {
            return new KeyValuePair<GridPosition, CellType>(position, type);
        }
    }
}
