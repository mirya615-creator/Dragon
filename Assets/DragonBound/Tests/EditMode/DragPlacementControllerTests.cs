using System.Collections.Generic;
using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class DragPlacementControllerTests
    {
        [Test]
        public void TapOnlySelectsUnit()
        {
            var gesture = new FixedSlotDragGesture();
            var selectionCount = 0;
            gesture.PointerDown(100f, 200f);
            if (gesture.PointerUp())
            {
                selectionCount++;
            }

            Assert.AreEqual(1, selectionCount);
            Assert.IsFalse(gesture.IsDragging);
        }

        [Test]
        public void DragStartsOnlyAfterThreshold()
        {
            var gesture = new FixedSlotDragGesture();
            gesture.PointerDown(10f, 10f);

            Assert.IsFalse(gesture.TryBeginDrag(15f, 18f, 10f));
            Assert.IsFalse(gesture.IsDragging);
            Assert.IsTrue(gesture.TryBeginDrag(20f, 10f, 10f));
            Assert.IsTrue(gesture.IsDragging);
            Assert.IsFalse(gesture.PointerUp());
        }

        [Test]
        public void ActivePointerOwnsGestureUntilItCompletes()
        {
            var gesture = new FixedSlotDragGesture();
            Assert.IsTrue(gesture.PointerDown(11, 10f, 10f));
            Assert.IsTrue(gesture.TryBeginDrag(11, 20f, 10f, 10f));

            Assert.IsFalse(gesture.PointerDown(22, 100f, 100f));
            Assert.IsFalse(gesture.PointerUp(22));
            Assert.IsTrue(gesture.IsDragging);
            Assert.IsTrue(gesture.OwnsPointer(11));

            Assert.IsFalse(gesture.PointerUp(11));
            Assert.IsFalse(gesture.IsDragging);
            Assert.IsFalse(gesture.HasActivePointer);
        }

        [Test]
        public void DragDoesNotContinuouslyMoveRuntimeUnit()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Bench)[0];
            var target = board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var mutations = new List<GridMutation>();
            board.Changed += mutations.Add;
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.a"));
            Assert.IsTrue(board.TryGetPosition("unit.a", out var duringDrag));
            Assert.AreEqual(origin, duringDrag);
            Assert.AreEqual(0, mutations.Count);

            Assert.AreEqual(DragDropStatus.Moved, drag.Drop(target));
            Assert.AreEqual(1, mutations.Count);
            Assert.IsTrue(board.TryGetPosition("unit.a", out var afterDrop));
            Assert.AreEqual(target, afterDrop);
        }

        [Test]
        public void DragPreviewAcceptsFixedSlotsWithoutCellHighlights()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Bench)[0];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.a"));

            Assert.AreEqual(0, drag.HighlightedPositions.Count);
            Assert.IsFalse(drag.IsHighlighted(board.GetPositions(CellType.Battle)[0]));
            Assert.IsTrue(drag.CanPreviewTarget(board.GetPositions(CellType.Battle)[0]));
            Assert.IsTrue(drag.CanPreviewTarget(board.GetPositions(CellType.Bench)[1]));
            Assert.IsFalse(drag.IsHighlighted(board.GetPositions(CellType.Locked)[0]));
            Assert.IsFalse(drag.CanPreviewTarget(board.GetPositions(CellType.Locked)[0]));
        }

        [Test]
        public void LockedCellNeverHighlights()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Bench)[0];
            var locked = board.GetPositions(CellType.Locked)[0];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.a"));
            Assert.IsFalse(drag.IsHighlighted(locked));
            Assert.IsFalse(drag.CanPreviewTarget(locked));
        }

        [Test]
        public void ValidCrossZoneDropMovesUnitAndPublishesCompletion()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Bench)[0];
            var target = board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var drag = new DragPlacementController(board);
            var completions = new List<DragCompletion>();
            drag.Completed += completions.Add;

            Assert.IsTrue(drag.BeginDrag("unit.a"));
            Assert.AreEqual(DragDropStatus.Moved, drag.Drop(target));

            Assert.IsTrue(board.TryGetPosition("unit.a", out var finalPosition));
            Assert.AreEqual(target, finalPosition);
            Assert.AreEqual(1, completions.Count);
            Assert.AreEqual(1, completions[0].Sequence);
            Assert.AreEqual(DragDropStatus.Moved, completions[0].Status);
        }

        [Test]
        public void InvalidDropRevertsWithoutMutatingBoard()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Bench)[0];
            var locked = board.GetPositions(CellType.Locked)[0];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var mutations = new List<GridMutation>();
            board.Changed += mutations.Add;
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.a"));
            Assert.AreEqual(DragDropStatus.Reverted, drag.Drop(locked));

            Assert.IsTrue(board.TryGetPosition("unit.a", out var finalPosition));
            Assert.AreEqual(origin, finalPosition);
            Assert.AreEqual(0, mutations.Count);
        }

        [Test]
        public void LockedCellRejectsDrop()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Battle)[0];
            var locked = board.GetPositions(CellType.Locked)[0];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.a"));
            Assert.AreEqual(DragDropStatus.Reverted, drag.Drop(locked));
            Assert.IsTrue(board.TryGetPosition("unit.a", out var restored));
            Assert.AreEqual(origin, restored);
        }

        [Test]
        public void InvalidDropRestoresSourceAndTarget()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var source = board.GetPositions(CellType.Battle)[0];
            var target = board.GetPositions(CellType.Battle)[1];
            var locked = board.GetPositions(CellType.Locked)[0];
            Assert.IsTrue(board.TryPlace("unit.source", source));
            Assert.IsTrue(board.TryPlace("unit.target", target));
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.source"));
            Assert.AreEqual(DragDropStatus.Reverted, drag.Drop(locked));
            Assert.IsTrue(board.TryGetPosition("unit.source", out var restoredSource));
            Assert.IsTrue(board.TryGetPosition("unit.target", out var restoredTarget));
            Assert.AreEqual(source, restoredSource);
            Assert.AreEqual(target, restoredTarget);
            Assert.AreEqual(2, board.GetOccupants().Count);
        }

        [Test]
        public void BattleRepositionIsEnabledForEmptyBattleCells()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Battle)[0];
            var target = board.GetPositions(CellType.Battle)[1];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.a"));
            Assert.IsTrue(drag.CanPreviewTarget(target));
            Assert.IsFalse(drag.IsHighlighted(target));
            Assert.AreEqual(DragDropStatus.Moved, drag.Drop(target));
        }

        [Test]
        public void BattleUnitPreviewsAndMovesToEmptyBenchCell()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var origin = board.GetPositions(CellType.Battle)[0];
            var target = board.GetPositions(CellType.Bench)[0];
            Assert.IsTrue(board.TryPlace("unit.a", origin));
            var drag = new DragPlacementController(board);

            Assert.IsTrue(drag.BeginDrag("unit.a"));
            Assert.IsTrue(drag.CanPreviewTarget(target));
            Assert.IsFalse(drag.IsHighlighted(target));
            Assert.AreEqual(DragDropStatus.Moved, drag.Drop(target));
            Assert.IsTrue(board.TryGetPosition("unit.a", out var finalPosition));
            Assert.AreEqual(target, finalPosition);
        }
    }
}
