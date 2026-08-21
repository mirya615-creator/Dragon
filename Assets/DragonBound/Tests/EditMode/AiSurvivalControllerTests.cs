using System.Collections.Generic;
using DragonBound.AI;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class AiSurvivalControllerTests
    {
        [Test]
        public void AiTick_RecruitsAndDeploysUsingOnlyAiSideState()
        {
            var context = CreateFiniteAiContext(100, 101);
            var player = new TeamState(TeamSide.Player);
            player.AddResources(100);

            context.Controller.Tick();

            Assert.AreEqual(1, context.Recruitment.CompletedRecruitments);
            Assert.Less(context.Team.Resources, 100);
            Assert.AreEqual(100, player.Resources);
            Assert.Greater(context.Destination.DeployedCount, 0);
            Assert.Greater(context.Destination.GetDeployedUnits().Count, 0);
            Assert.AreEqual(TeamSide.AI, context.Controller.Side);
            foreach (var unit in context.Destination.GetDeployedUnits())
            {
                Assert.AreEqual(CellType.Battle, GetCellType(context.Board, unit.GridPosition));
            }
        }

        [Test]
        public void AiTick_DoesNotDeadlockRecruitmentWhenCampHasUnplaceableComponents()
        {
            var context = CreateFiniteAiContext(100, 202);
            FillBattleWithMaxLevelBasics(context.Board, context.Destination);
            context.Destination.Commit(
                context.Destination.Plan(RecruitBatch.CardsPerRecruitment),
                new RecruitBatch(1, new[]
                {
                    Component("blocked.component", HeroSliceCatalog.DragonSigilComponentId),
                    Basic("bench.basic.1"),
                    Basic("bench.basic.2"),
                    Basic("bench.basic.3"),
                    Basic("bench.basic.4")
                }));

            context.Controller.Tick();

            Assert.AreEqual(1, context.Recruitment.CompletedRecruitments);
            Assert.IsTrue(
                context.Destination.TryGetCard("blocked.component", out _),
                "A finite hero component must remain owned when the AI can legally stage it by swapping board contents.");
            Assert.IsFalse(
                context.Recruitment.WasHeroComponentDiscarded(HeroSliceCatalog.DragonSigilComponentId),
                "Recipe components must not be permanently discarded merely because the board was initially full.");
            Assert.IsFalse(context.Controller.LastRecruitTelemetry.LegacyCampPolicyWouldBlock);
            Assert.AreEqual(0, context.Controller.LegacyCampPolicyBlockCount);
            Assert.IsFalse(context.Controller.LastRecruitTelemetry.RecruitBlockedReason ==
                           AiRecruitBlockedReason.CampPolicyBlocked);
        }

        [Test]
        public void AiTick_UsesBenchShovelToUnlockOnlyItsOwnBoardWhenFull()
        {
            var context = CreateFiniteAiContext(0, 303);
            var playerBoard = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            FillBattleWithMaxLevelBasics(context.Board, context.Destination);
            context.Destination.Commit(
                context.Destination.Plan(RecruitBatch.CardsPerRecruitment),
                new RecruitBatch(1, new[]
                {
                    new RecruitCard(
                        "bench.shovel",
                        RecruitItemKind.Shovel,
                        ShovelRecruitmentConfig.ShovelConfigId,
                        string.Empty),
                    Basic("bench.basic.1"),
                    Basic("bench.basic.2"),
                    Basic("bench.basic.3"),
                    Basic("bench.basic.4")
                }));

            var aiOpenBefore = context.Board.UnlockedBattleCellCount;
            var playerOpenBefore = playerBoard.UnlockedBattleCellCount;
            context.Controller.Tick();

            Assert.AreEqual(aiOpenBefore + 1, context.Board.UnlockedBattleCellCount);
            Assert.AreEqual(playerOpenBefore, playerBoard.UnlockedBattleCellCount);
            Assert.AreEqual(0, context.Destination.GetBenchShovelCount());
            Assert.AreEqual(0, context.Shovels.AvailableShovelCount);
        }

        [Test]
        public void AiTick_UsesBenchShovelImmediatelyEvenWhenBattleCellsRemain()
        {
            var context = CreateFiniteAiContext(0, 304);
            context.Destination.Commit(
                context.Destination.Plan(RecruitBatch.CardsPerRecruitment),
                new RecruitBatch(1, new[]
                {
                    new RecruitCard(
                        "bench.shovel.immediate",
                        RecruitItemKind.Shovel,
                        ShovelRecruitmentConfig.ShovelConfigId,
                        string.Empty),
                    Basic("bench.basic.1"),
                    Basic("bench.basic.2"),
                    Basic("bench.basic.3"),
                    Basic("bench.basic.4")
                }));

            var openBefore = context.Board.UnlockedBattleCellCount;
            context.Controller.Tick();

            Assert.AreEqual(openBefore + 1, context.Board.UnlockedBattleCellCount);
            Assert.AreEqual(0, context.Destination.GetBenchShovelCount());
        }

        [Test]
        public void AiTick_FormsAnOwnedRecipeBeforeRefreshingComponentsFromAFullBench()
        {
            var context = CreateFiniteAiContext(0, 305);
            FillBattleWithMaxLevelBasics(context.Board, context.Destination);
            context.Destination.Commit(
                context.Destination.Plan(RecruitBatch.CardsPerRecruitment),
                new RecruitBatch(1, new[]
                {
                    Component("recipe.sigil", HeroSliceCatalog.DragonSigilComponentId),
                    Component("recipe.ranger", HeroSliceCatalog.SkyRangerComponentId),
                    Basic("recipe.basic.1", BasicUnitCatalog.MaxLevel),
                    Basic("recipe.basic.2", BasicUnitCatalog.MaxLevel),
                    Basic("recipe.basic.3", BasicUnitCatalog.MaxLevel)
                }));

            context.Controller.Tick();

            Assert.AreEqual(1, context.Destination.ActivePairLinkCount);
            Assert.AreEqual(1, context.Controller.RecipeFormationSucceeded);
            Assert.AreEqual(0, context.Recruitment.CompletedRecruitments);

            context.Controller.Tick();

            Assert.AreEqual(1, context.Destination.ActivePairLinkCount,
                "A stable PairLink must not be rearranged for ordinary basic-unit deployment.");
        }

        [Test]
        public void ComponentLifecycleSnapshot_ConservesTheFiniteBagBeforeAndAfterAiRecruitment()
        {
            var context = CreateFiniteAiContext(100, 306);
            var initial = ComponentLifecycleSnapshot.Capture(
                context.Recruitment,
                context.Destination,
                context.Board);

            Assert.AreEqual(24, initial.RemainingInBag);
            Assert.IsTrue(initial.IsConserved);

            context.Controller.Tick();
            var afterRecruit = ComponentLifecycleSnapshot.Capture(
                context.Recruitment,
                context.Destination,
                context.Board);

            Assert.IsTrue(afterRecruit.IsConserved);
            Assert.AreEqual(24, afterRecruit.ConservedTotal);
            Assert.AreEqual(afterRecruit.TotalDeliveredComponents,
                24 - afterRecruit.RemainingInBag);
        }

        [Test]
        public void AiDiagnostics_RecordWaveEndAndDeathSummary()
        {
            var context = CreateFiniteAiContext(50, 404);
            context.Controller.Tick();
            context.Controller.RecordWaveEnd(1, 2, 0);
            context.Controller.RecordWaveEnd(2, 4, 1);
            context.Team.ApplyHatchlingDamage(context.Team.HatchlingMaxHealth);
            context.Controller.RecordRunEnd(2, 4, 3);

            var diagnostics = context.Controller.Diagnostics;
            Assert.AreEqual(2, diagnostics.WaveRecords.Count);
            Assert.AreEqual(2, diagnostics.FirstLeakWave);
            Assert.AreEqual(2, diagnostics.DeathWave);
            Assert.AreEqual(context.Recruitment.CompletedRecruitments, diagnostics.DeathRecruitCount);
            StringAssert.Contains("AI_SURVIVAL_SUMMARY", diagnostics.CreateSummary());
        }

        [Test]
        public void AiRecruitTelemetry_ReportsAnUnaffordableDecisionWithoutAStall()
        {
            var context = CreateFiniteAiContext(0, 405);

            context.Controller.Tick(2);

            var telemetry = context.Controller.LastRecruitTelemetry;
            Assert.AreEqual(0, telemetry.CurrentResources);
            Assert.AreEqual(10, telemetry.NextRecruitCost);
            Assert.IsFalse(telemetry.CanAffordRecruit);
            Assert.IsFalse(telemetry.AIRecruitAttempted);
            Assert.IsFalse(telemetry.AIRecruitSucceeded);
            Assert.AreEqual(AiRecruitBlockedReason.InsufficientResources, telemetry.RecruitBlockedReason);
            Assert.AreEqual(0, context.Controller.RecruitStallCount);
        }

        [Test]
        [Category("Diagnostics")]
        public void AiSurvivalSimulation_RunsOneHundredSeedsAndReportsRawRates()
        {
            var report = AiSurvivalSimulation.Run(1, 100);

            TestContext.WriteLine(report.CreateReport());
            Assert.AreEqual(100, report.SampleCount);
            Assert.AreEqual(
                100,
                report.DeathCount + report.ReachedWaveTwentyCount);
            Assert.Less(report.WaveOneDeaths, 100);
            Assert.Less(report.WaveTwoDeaths, 100);
        }

        private static AiContext CreateFiniteAiContext(int resources, int seed)
        {
            var board = DragonBoundBoardLayout.CreateDefault(TeamSide.AI);
            var destination = new BoardRecruitDestination(board);
            var team = new TeamState(TeamSide.AI);
            team.AddResources(resources);
            var catalog = GreyboxRecruitmentCatalog.Create();
            var bag = LimitedComponentBag.CreateBag(
                seed,
                LimitedComponentBag.DefaultContentVersion,
                catalog);
            var shovelState = new ShovelRecruitmentState(
                () => board.GetPositions(CellType.Locked).Count);
            var deck = new RecruitDeck(catalog, seed, "ai.test", bag, shovelState: shovelState);
            var recruitment = new RecruitmentService(team, deck, destination);
            var shovels = new ShovelUnlockService(board, destination);
            var controller = new BasicUnitAiController(board, destination, recruitment, shovels, team);
            controller.Diagnostics.EmitLogs = false;
            return new AiContext(board, destination, team, recruitment, shovels, controller);
        }

        private static void FillBattleWithMaxLevelBasics(
            BoardGrid board,
            BoardRecruitDestination destination)
        {
            var counter = 0;
            foreach (var battle in board.GetPositions(CellType.Battle))
            {
                var runtimeId = "battle.max." + counter++;
                destination.Commit(
                    destination.Plan(RecruitBatch.CardsPerRecruitment),
                    new RecruitBatch(counter, new[]
                    {
                        Basic(runtimeId, BasicUnitCatalog.MaxLevel),
                        Basic(runtimeId + ".filler.1", BasicUnitCatalog.MaxLevel),
                        Basic(runtimeId + ".filler.2", BasicUnitCatalog.MaxLevel),
                        Basic(runtimeId + ".filler.3", BasicUnitCatalog.MaxLevel),
                        Basic(runtimeId + ".filler.4", BasicUnitCatalog.MaxLevel)
                    }));
                Assert.IsTrue(board.TryGetPosition(runtimeId, out var bench));
                Assert.IsTrue(board.TryMove(bench, battle));
            }
        }

        private static RecruitCard Basic(string runtimeId, int level = 1)
        {
            return new RecruitCard(
                runtimeId,
                RecruitItemKind.BasicUnit,
                "basic.axe_raider",
                string.Empty,
                level);
        }

        private static RecruitCard Component(string runtimeId, string componentId)
        {
            return new RecruitCard(
                runtimeId,
                RecruitItemKind.HeroComponent,
                componentId,
                runtimeId + ".source");
        }

        private static CellType GetCellType(BoardGrid board, GridPosition position)
        {
            Assert.IsTrue(board.TryGetCellType(position, out var type));
            return type;
        }

        private readonly struct AiContext
        {
            public AiContext(
                BoardGrid board,
                BoardRecruitDestination destination,
                TeamState team,
                RecruitmentService recruitment,
                ShovelUnlockService shovels,
                BasicUnitAiController controller)
            {
                Board = board;
                Destination = destination;
                Team = team;
                Recruitment = recruitment;
                Shovels = shovels;
                Controller = controller;
            }

            public BoardGrid Board { get; }
            public BoardRecruitDestination Destination { get; }
            public TeamState Team { get; }
            public RecruitmentService Recruitment { get; }
            public ShovelUnlockService Shovels { get; }
            public BasicUnitAiController Controller { get; }
        }
    }
}
