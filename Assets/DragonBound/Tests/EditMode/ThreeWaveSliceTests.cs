using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ThreeWaveSliceTests
    {
        [Test]
        public void ThreeWavesGenerateAndSettleWhenBothSidesHaveDeployedUnits()
        {
            var match = CreateMatch(out var playerBoard, out var aiBoard, out var playerDestination, out var aiDestination);
            RecruitAndDeploy(match.Player, playerBoard, playerDestination, "player", new RunSeed(1).Random);
            RecruitAndDeploy(match.AI, aiBoard, aiDestination, "ai", new RunSeed(2).Random);

            var runtime = new ThreeWaveSliceRuntime(match, playerDestination, aiDestination);
            Tick(runtime, 150f);

            Assert.IsTrue(runtime.IsComplete);
            Assert.AreEqual(MatchState.Victory, match.State);
            Assert.AreEqual(30, runtime.TotalGenerated);
            Assert.Greater(runtime.TotalAttacks, 0);
            Assert.AreEqual(0, runtime.TotalLeaked);
            Assert.AreEqual(3, match.CurrentWave);
        }

        [Test]
        public void ThreeWavesLeakIntoDefeatWithoutDeployedUnits()
        {
            var match = CreateMatch(out _, out _, out var playerDestination, out var aiDestination);
            var runtime = new ThreeWaveSliceRuntime(match, playerDestination, aiDestination);
            Tick(runtime, 150f);

            Assert.IsTrue(runtime.IsComplete);
            Assert.AreEqual(MatchState.Defeat, match.State);
            Assert.AreEqual(runtime.TotalGenerated, runtime.TotalLeaked);
            Assert.AreEqual(0, runtime.PlayerEnemyRegistry.Count);
            Assert.AreEqual(0, runtime.AiEnemyRegistry.Count);
            Assert.AreEqual("DragonGoal", runtime.PlayerPath.GoalNode);
            Assert.AreEqual("DragonGoal", runtime.AiPath.GoalNode);
            CollectionAssert.AllItemsAreUnique(runtime.PlayerPath.Nodes);
            CollectionAssert.AllItemsAreUnique(runtime.AiPath.Nodes);
            Assert.AreEqual(0, match.Player.HatchlingHealth);
        }

        [Test]
        public void FourBasicArchetypesEmitAllRequiredCombatKinds()
        {
            var match = CreateMatch(out var playerBoard, out var aiBoard, out var playerDestination, out var aiDestination);
            CommitAndDeployFourBasics(playerBoard, playerDestination, "player");
            CommitAndDeployFourBasics(aiBoard, aiDestination, "ai");
            var runtime = new ThreeWaveSliceRuntime(match, playerDestination, aiDestination);
            var kinds = new HashSet<AttackKind>();
            runtime.CombatEmitted += combatEvent =>
            {
                if (combatEvent.Damage > 0)
                {
                    kinds.Add(combatEvent.Kind);
                }
            };

            Tick(runtime, 20f);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    AttackKind.Single,
                    AttackKind.BowProjectile,
                    AttackKind.SpearPierce,
                    AttackKind.RiderSweep
                },
                kinds);
            Assert.Greater(runtime.TotalKills, 0);
        }

        [Test]
        public void DraggingDeployedUnit_PausesAndResumesAttackCooldown()
        {
            var match = CreateMatch(out var playerBoard, out _, out var playerDestination, out var aiDestination);
            var cards = new[]
            {
                new RecruitCard("player.bow", RecruitItemKind.BasicUnit, "basic.longbow_hunter", string.Empty),
                new RecruitCard("player.axe", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty),
                new RecruitCard("player.spear", RecruitItemKind.BasicUnit, "basic.spear_raider", string.Empty),
                new RecruitCard("player.berserker", RecruitItemKind.BasicUnit, "basic.twinaxe_berserker", string.Empty),
                new RecruitCard("player.axe2", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty)
            };
            playerDestination.Commit(RecruitDestinationPlan.AddToEmptySlots, new RecruitBatch(1, cards));
            Assert.IsTrue(playerBoard.TryMove(
                playerBoard.GetPositions(CellType.Bench)[0],
                playerBoard.GetPositions(CellType.Battle)[0]));
            var runtime = new ThreeWaveSliceRuntime(match, playerDestination, aiDestination);
            var bowAttacks = 0;
            runtime.CombatEmitted += combatEvent =>
            {
                if (combatEvent.AttackerRuntimeId == "player.bow" && combatEvent.Damage > 0f)
                {
                    bowAttacks++;
                }
            };

            Tick(runtime, 0.4f);
            Assert.AreEqual(0, bowAttacks);
            Assert.IsTrue(playerDestination.SetCombatSuspended("player.bow", true));
            Tick(runtime, 2f);
            Assert.AreEqual(0, bowAttacks);
            Assert.IsTrue(playerDestination.IsCombatSuspended("player.bow"));
            Assert.IsTrue(playerDestination.SetCombatSuspended("player.bow", false));
            Tick(runtime, 0.4f);

            Assert.AreEqual(1, bowAttacks);
            Assert.IsFalse(playerDestination.IsCombatSuspended("player.bow"));
        }

        private static MatchController CreateMatch(
            out BoardGrid playerBoard,
            out BoardGrid aiBoard,
            out BoardRecruitDestination playerDestination,
            out BoardRecruitDestination aiDestination)
        {
            var match = new MatchController(73);
            match.SetCurrentWave(1);
            Assert.IsTrue(match.TryTransition(MatchState.Ready));
            Assert.IsTrue(match.TryTransition(MatchState.Running));
            playerBoard = DragonBoundBoardLayout.CreateInitial();
            aiBoard = DragonBoundBoardLayout.CreateInitial();
            playerDestination = new BoardRecruitDestination(playerBoard);
            aiDestination = new BoardRecruitDestination(aiBoard);
            return match;
        }

        private static void RecruitAndDeploy(
            TeamState team,
            BoardGrid board,
            BoardRecruitDestination destination,
            string prefix,
            IRunRandom random)
        {
            var service = new RecruitmentService(
                team,
                new RecruitDeck(GreyboxRecruitmentCatalog.Create(), random, prefix),
                destination);
            Assert.AreEqual(RecruitmentStatus.Success, service.TryRecruit().Status);
            var bench = board.GetPositions(CellType.Bench);
            var battle = board.GetPositions(CellType.Battle);
            for (var index = 0; index < bench.Count; index++)
            {
                Assert.IsTrue(board.TryMove(bench[index], battle[index]));
            }
        }

        private static void CommitAndDeployFourBasics(
            BoardGrid board,
            BoardRecruitDestination destination,
            string prefix)
        {
            var cards = new[]
            {
                new RecruitCard(prefix + ".berserker", RecruitItemKind.BasicUnit, "basic.twinaxe_berserker", string.Empty),
                new RecruitCard(prefix + ".axe", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty),
                new RecruitCard(prefix + ".bow", RecruitItemKind.BasicUnit, "basic.longbow_hunter", string.Empty),
                new RecruitCard(prefix + ".spear", RecruitItemKind.BasicUnit, "basic.spear_raider", string.Empty),
                new RecruitCard(prefix + ".axe2", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty)
            };
            destination.Commit(RecruitDestinationPlan.AddToEmptySlots, new RecruitBatch(1, cards));
            var bench = board.GetPositions(CellType.Bench);
            var battle = board.GetPositions(CellType.Battle);
            for (var index = 0; index < 4; index++)
            {
                Assert.IsTrue(board.TryMove(bench[index], battle[index]));
            }
        }

        private static void Tick(ThreeWaveSliceRuntime runtime, float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += 0.1f)
            {
                runtime.Tick(0.1f);
                if (runtime.IsComplete)
                {
                    return;
                }
            }
        }
    }
}
