using DragonBound.AI;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class BasicUnitOperationsTests
    {
        [Test]
        public void SameTypeSameLevel_Merges()
        {
            var context = CreateContext(Axe("source"), Axe("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[0]));
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[1]));

            Assert.AreEqual(DragDropStatus.Merged, Drag(context, "source", battle[1]));
            Assert.IsFalse(context.Board.TryGetPosition("source", out _));
            Assert.IsTrue(context.Board.TryGetPosition("target", out var targetPosition));
            Assert.AreEqual(battle[1], targetPosition);
            Assert.IsTrue(context.Destination.TryGetCard("target", out var merged));
            Assert.AreEqual(2, merged.Level);
        }

        [Test]
        public void SameTypeSameLevelBoardUnitsMerge()
        {
            SameTypeSameLevel_Merges();
        }

        [Test]
        public void DifferentType_DoesNotMerge()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[0]));
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[1]));

            Assert.AreEqual(DragDropStatus.Swapped, Drag(context, "source", battle[1]));
            Assert.AreEqual(5, context.Destination.TotalObjectCount);
            Assert.AreEqual(1, GetCard(context, "source").Level);
            Assert.AreEqual(1, GetCard(context, "target").Level);
        }

        [Test]
        public void DifferentLevel_DoesNotMerge()
        {
            var context = CreateContext(Axe("source", 1), Axe("target", 2));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[0]));
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[1]));

            Assert.AreEqual(DragDropStatus.Swapped, Drag(context, "source", battle[1]));
            Assert.AreEqual(1, GetCard(context, "source").Level);
            Assert.AreEqual(2, GetCard(context, "target").Level);
        }

        [Test]
        public void LevelFive_CannotMerge()
        {
            var context = CreateContext(Axe("source", 5), Axe("target", 5));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[0]));
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[1]));

            Assert.AreEqual(DragDropStatus.Reverted, Drag(context, "source", battle[1]));
            Assert.AreEqual(5, context.Destination.TotalObjectCount);
            Assert.AreEqual(5, GetCard(context, "target").Level);
        }

        [Test]
        public void MergeUsesConfiguredStats()
        {
            var context = CreateContext(Axe("source"), Axe("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[0]));
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[1]));
            Assert.AreEqual(DragDropStatus.Merged, Drag(context, "source", battle[1]));

            var merged = GetCard(context, "target");
            var stats = BasicUnitCatalog.GetStats(merged.ConfigId, merged.Level);
            Assert.AreEqual(4.50f, stats.Attack, 0.0001f);
            Assert.AreEqual(1.88f, stats.AttackSpeed, 0.0001f);
            Assert.AreEqual(1.5f, stats.RangeCells, 0.0001f);
        }

        [Test]
        public void BattleUnit_CanMoveToEmptyCell()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var bench = context.Board.GetPositions(CellType.Bench)[0];
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench, battle[0]));

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "source", battle[1]));
            Assert.IsTrue(context.Board.TryGetPosition("source", out var finalPosition));
            Assert.AreEqual(battle[1], finalPosition);
        }

        [Test]
        public void BoardUnitMovesToEmptyUnlockedCell()
        {
            BattleUnit_CanMoveToEmptyCell();
        }

        [Test]
        public void DifferentUnits_CanSwap()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[0]));
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[1]));

            Assert.AreEqual(DragDropStatus.Swapped, Drag(context, "source", battle[1]));
            Assert.IsTrue(context.Board.TryGetPosition("source", out var sourcePosition));
            Assert.IsTrue(context.Board.TryGetPosition("target", out var targetPosition));
            Assert.AreEqual(battle[1], sourcePosition);
            Assert.AreEqual(battle[0], targetPosition);
        }

        [Test]
        public void DifferentBoardUnitsSwap()
        {
            DifferentUnits_CanSwap();
        }

        [Test]
        public void BenchUnitDeploysToEmptyBoardCell()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var battle = context.Board.GetPositions(CellType.Battle)[0];

            Assert.IsFalse(context.Destination.IsCombatRegistered("source"));
            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "source", battle));
            Assert.IsTrue(context.Board.TryGetPosition("source", out var deployed));
            Assert.AreEqual(battle, deployed);
            Assert.IsTrue(context.Destination.IsCombatRegistered("source"));
        }

        [Test]
        public void BattleUnit_CanReturnToEmptyBench()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var bench = context.Board.GetPositions(CellType.Bench)[0];
            var battle = context.Board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(context.Board.TryMove(bench, battle));

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "source", bench));
            Assert.IsTrue(context.Board.TryGetPosition("source", out var finalPosition));
            Assert.AreEqual(bench, finalPosition);
        }

        [Test]
        public void BoardUnitMovesToEmptyBenchSlot()
        {
            BattleUnit_CanReturnToEmptyBench();
        }

        [Test]
        public void BenchAndBoardUnitsSwap()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(context.Board.TryMove(bench[0], battle));

            Assert.AreEqual(DragDropStatus.Swapped, Drag(context, "source", bench[1]));
            Assert.IsTrue(context.Board.TryGetPosition("source", out var sourcePosition));
            Assert.IsTrue(context.Board.TryGetPosition("target", out var targetPosition));
            Assert.AreEqual(bench[1], sourcePosition);
            Assert.AreEqual(battle, targetPosition);
            Assert.IsFalse(context.Destination.IsCombatRegistered("source"));
            Assert.IsTrue(context.Destination.IsCombatRegistered("target"));
        }

        [Test]
        public void CrossZoneSameTypeSameLevelMergesBenchToBattle()
        {
            var context = CreateContext(Axe("source"), Axe("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(context.Board.TryMove(bench[1], battle));

            Assert.AreEqual(DragDropStatus.Merged, Drag(context, "source", battle));
            Assert.AreEqual(4, context.Destination.TotalObjectCount);
            Assert.IsFalse(context.Board.TryGetPosition("source", out _));
            Assert.AreEqual(2, GetCard(context, "target").Level);
            Assert.IsTrue(context.Board.TryGetPosition("target", out var targetPosition));
            Assert.AreEqual(battle, targetPosition);
        }

        [Test]
        public void CrossZoneSameTypeSameLevelMergesBattleToBench()
        {
            var context = CreateContext(Axe("source"), Axe("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(context.Board.TryMove(bench[0], battle));

            Assert.AreEqual(DragDropStatus.Merged, Drag(context, "source", bench[1]));
            Assert.AreEqual(4, context.Destination.TotalObjectCount);
            Assert.IsFalse(context.Board.TryGetPosition("source", out _));
            Assert.AreEqual(2, GetCard(context, "target").Level);
            Assert.IsTrue(context.Board.TryGetPosition("target", out var targetPosition));
            Assert.AreEqual(bench[1], targetPosition);
        }

        [Test]
        public void BenchSlotsCanSwap()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var bench = context.Board.GetPositions(CellType.Bench);

            Assert.AreEqual(DragDropStatus.Swapped, Drag(context, "source", bench[1]));
            Assert.IsTrue(context.Board.TryGetPosition("source", out var sourcePosition));
            Assert.IsTrue(context.Board.TryGetPosition("target", out var targetPosition));
            Assert.AreEqual(bench[1], sourcePosition);
            Assert.AreEqual(bench[0], targetPosition);
        }

        [Test]
        public void MovingToBenchStopsCombat()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var bench = context.Board.GetPositions(CellType.Bench)[0];
            var battle = context.Board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(context.Board.TryMove(bench, battle));
            Assert.IsTrue(context.Destination.IsCombatRegistered("source"));

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "source", bench));
            Assert.IsFalse(context.Destination.IsCombatRegistered("source"));
            Assert.IsFalse(context.Destination.IsCombatSuspended("source"));
        }

        [Test]
        public void MovingFromBenchStartsCombat()
        {
            var context = CreateContext(Axe("source"), Bow("target"));
            var battle = context.Board.GetPositions(CellType.Battle)[0];
            Assert.IsFalse(context.Destination.IsCombatRegistered("source"));

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "source", battle));
            Assert.IsTrue(context.Destination.IsCombatRegistered("source"));
            Assert.IsFalse(context.Destination.IsCombatSuspended("source"));
        }

        [Test]
        public void AIUsesSameMergeRules()
        {
            var context = CreateContext(Axe("source"), Axe("target"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[0]));
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[1]));
            var team = new TeamState(TeamSide.AI);
            team.AddResources(20);
            var recruitment = new RecruitmentService(
                team,
                new RecruitDeck(GreyboxRecruitmentCatalog.Create(), new RunSeed(91).Random, "ai.test"),
                context.Destination);
            var ai = new BasicUnitAiController(context.Board, context.Destination, recruitment);
            Assert.AreEqual(1, ai.MergeAllAvailable());
            Assert.AreEqual(2, GetCard(context, "source").Level);
            Assert.IsFalse(context.Board.TryGetPosition("target", out _));
        }

        [Test]
        public void AIMerge_RequiresBothUnitsDeployed()
        {
            var context = CreateContext(Axe("bench.axe"), Axe("battle.axe"));
            var bench = context.Board.GetPositions(CellType.Bench);
            var battle = context.Board.GetPositions(CellType.Battle);
            Assert.IsTrue(context.Board.TryMove(bench[1], battle[0]));
            var ai = CreateAi(context);

            Assert.AreEqual(0, ai.MergeAllAvailable());
            Assert.IsTrue(context.Board.TryMove(bench[0], battle[1]));
            Assert.AreEqual(1, ai.MergeAllAvailable());
            Assert.IsTrue(context.Board.TryGetPosition("battle.axe", out var finalPosition));
            Assert.AreEqual(battle[0], finalPosition);
            Assert.AreEqual(2, GetCard(context, "battle.axe").Level);
            Assert.IsFalse(context.Board.TryGetPosition("bench.axe", out _));
        }

        private static BasicUnitAiController CreateAi(Context context)
        {
            var team = new TeamState(TeamSide.AI);
            team.AddResources(20);
            var recruitment = new RecruitmentService(
                team,
                new RecruitDeck(GreyboxRecruitmentCatalog.Create(), new RunSeed(91).Random, "ai.test"),
                context.Destination);
            return new BasicUnitAiController(context.Board, context.Destination, recruitment);
        }

        private static DragDropStatus Drag(Context context, string runtimeId, GridPosition target)
        {
            var drag = new DragPlacementController(context.Board, context.Destination, true);
            Assert.IsTrue(drag.BeginDrag(runtimeId));
            return drag.Drop(target);
        }

        private static Context CreateContext(RecruitCard first, RecruitCard second)
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var destination = new BoardRecruitDestination(board);
            destination.Commit(
                RecruitDestinationPlan.AddToEmptySlots,
                new RecruitBatch(1, new[]
                {
                    first,
                    second,
                    Spear("filler.spear"),
                    Bow("filler.bow"),
                    Berserker("filler.berserker")
                }));
            return new Context(board, destination);
        }

        private static RecruitCard GetCard(Context context, string runtimeId)
        {
            Assert.IsTrue(context.Destination.TryGetCard(runtimeId, out var card));
            return card;
        }

        private static RecruitCard Axe(string id, int level = 1)
        {
            return Card(id, "basic.axe_raider", level);
        }

        private static RecruitCard Bow(string id, int level = 1)
        {
            return Card(id, "basic.longbow_hunter", level);
        }

        private static RecruitCard Spear(string id, int level = 1)
        {
            return Card(id, "basic.spear_raider", level);
        }

        private static RecruitCard Berserker(string id, int level = 1)
        {
            return Card(id, "basic.twinaxe_berserker", level);
        }

        private static RecruitCard Card(string id, string configId, int level)
        {
            return new RecruitCard(id, RecruitItemKind.BasicUnit, configId, string.Empty, level);
        }

        private readonly struct Context
        {
            public Context(BoardGrid board, BoardRecruitDestination destination)
            {
                Board = board;
                Destination = destination;
            }

            public BoardGrid Board { get; }
            public BoardRecruitDestination Destination { get; }
        }
    }
}
