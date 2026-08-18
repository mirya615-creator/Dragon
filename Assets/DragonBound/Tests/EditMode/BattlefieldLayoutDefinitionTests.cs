using DragonBound.Core;
using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class BattlefieldLayoutDefinitionTests
    {
        [Test]
        public void DefaultFixedLayoutStartsWithSixConnectedBattleCellsAndLockedCapacity()
        {
            var layout = BattlefieldLayoutDefinitions.Default;
            var board = DragonBoundBoardLayout.CreateDefault();

            Assert.AreEqual(BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id, layout.LayoutId);
            Assert.AreEqual(8, layout.Width);
            Assert.AreEqual(10, layout.Height);
            Assert.AreEqual(6, board.GetPositions(CellType.Battle).Count);
            Assert.AreEqual(18, board.GetPositions(CellType.Locked).Count);
            Assert.AreEqual(5, board.GetPositions(CellType.Bench).Count);
            Assert.AreEqual(29, board.CellCount);
            Assert.IsTrue(BoardGrid.AreOrthogonallyAdjacent(
                new GridPosition(2, 2),
                new GridPosition(3, 2)));
            Assert.IsTrue(BoardGrid.AreOrthogonallyAdjacent(
                new GridPosition(4, 1),
                new GridPosition(4, 2)));
        }

        [Test]
        public void LockedReferenceMapSlotRejectsPlacementUntilDebugUnlocked()
        {
            var board = DragonBoundBoardLayout.CreateDefault();
            var locked = new GridPosition(1, 0);

            Assert.IsFalse(board.TryPlace("unit.a", locked));
            Assert.IsTrue(board.TryDebugUnlockCell(locked));
            Assert.IsTrue(board.TryPlace("unit.a", locked));
            Assert.AreEqual(7, board.UnlockedBattleCellCount);
        }

        [Test]
        public void ReferenceMapAllowsDebugUnlockForEveryExplicitLockedCell()
        {
            var board = DragonBoundBoardLayout.CreateDefault();

            // Map01 has deliberate locked islands. The strict authored mask, rather than
            // an inferred adjacency rule, determines valid debug expansion targets.
            Assert.IsTrue(board.TryDebugUnlockCell(new GridPosition(0, 4)));
            Assert.IsTrue(board.TryDebugUnlockCell(new GridPosition(1, 4)));
        }

        [Test]
        public void InitialSixCellsMirrorAcrossThePlayerAndAiBoards()
        {
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ZhaoYunReference;
            var player = DragonBoundBoardLayout.Create(layout, TeamSide.Player);
            var ai = DragonBoundBoardLayout.Create(layout, TeamSide.AI);

            Assert.AreEqual(6, player.UnlockedBattleCellCount);
            Assert.AreEqual(6, ai.UnlockedBattleCellCount);
            foreach (var position in player.GetPositions(CellType.Battle))
            {
                Assert.IsTrue(ai.TryGetCellType(
                    layout.GetFairCounterpart(position, TeamSide.Player),
                    out var counterpartType));
                Assert.AreEqual(CellType.Battle, counterpartType);
            }
        }

        [Test]
        public void SpaciousLayoutExposesFiveByFiveFormationCapacity()
        {
            var board = DragonBoundBoardLayout.Create(
                BattlefieldLayoutDefinitions.Spacious5x5,
                TeamSide.Player);

            Assert.AreEqual(5, board.Layout.Width);
            Assert.AreEqual(5, board.Layout.Height);
            Assert.AreEqual(6, board.GetPositions(CellType.Battle).Count);
            Assert.AreEqual(19, board.GetPositions(CellType.Locked).Count);
            Assert.AreEqual(5, board.GetPositions(CellType.Bench).Count);
            Assert.AreEqual(30, board.CellCount);
        }

        [Test]
        public void FixedRangeBandsResolveForEveryOwnedDeploymentCell()
        {
            var player = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            var ai = DragonBoundBoardLayout.CreateDefault(TeamSide.AI);

            foreach (var position in player.GetPositions(CellType.Battle))
            {
                Assert.IsTrue(System.Enum.IsDefined(typeof(BattlefieldRangeBand), player.GetRangeBand(position)));
            }

            foreach (var position in ai.GetPositions(CellType.Battle))
            {
                Assert.IsTrue(System.Enum.IsDefined(typeof(BattlefieldRangeBand), ai.GetRangeBand(position)));
            }
        }

        [Test]
        public void SpaciousRangeBandsUseOneNearTwoMiddleAndTwoFarColumns()
        {
            var player = DragonBoundBoardLayout.Create(
                BattlefieldLayoutDefinitions.Spacious5x5,
                TeamSide.Player);

            Assert.AreEqual(BattlefieldRangeBand.Near, player.GetRangeBand(new GridPosition(0, 1)));
            Assert.AreEqual(BattlefieldRangeBand.Middle, player.GetRangeBand(new GridPosition(1, 1)));
            Assert.AreEqual(BattlefieldRangeBand.Middle, player.GetRangeBand(new GridPosition(2, 1)));
            Assert.AreEqual(BattlefieldRangeBand.Far, player.GetRangeBand(new GridPosition(3, 1)));
            Assert.AreEqual(BattlefieldRangeBand.Far, player.GetRangeBand(new GridPosition(4, 1)));
        }

        [Test]
        public void SymmetricLanesStayOutsideEveryFormationCell()
        {
            var layout = BattlefieldLayoutDefinitions.Default;
            var playerTransform = layout.GetTransform(TeamSide.Player);
            var aiTransform = layout.GetTransform(TeamSide.AI);

            Assert.IsFalse(playerTransform.IsHorizontallyMirrored);
            Assert.IsTrue(aiTransform.IsHorizontallyMirrored);
            foreach (var cell in layout.FormationCells)
            {
                Assert.Greater(layout.GetLaneDistance(cell.Position, TeamSide.Player), 0.5f);
                Assert.Greater(layout.GetLaneDistance(cell.Position, TeamSide.AI), 0.5f);
            }
        }
    }
}
