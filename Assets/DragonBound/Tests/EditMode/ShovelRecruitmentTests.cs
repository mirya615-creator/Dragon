using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ShovelRecruitmentTests
    {
        [Test]
        public void NewFixedBoardStartsWithSixUnlockedCellsPerSide()
        {
            var layout = BattlefieldLayoutDefinitions.Default;
            var player = DragonBoundBoardLayout.Create(layout, TeamSide.Player);
            var ai = DragonBoundBoardLayout.Create(layout, TeamSide.AI);

            Assert.AreEqual(6, player.GetPositions(CellType.Battle).Count);
            Assert.AreEqual(18, player.GetPositions(CellType.Locked).Count);
            Assert.AreEqual(6, ai.GetPositions(CellType.Battle).Count);
            Assert.AreEqual(18, ai.GetPositions(CellType.Locked).Count);
        }

        [Test]
        public void GrantedShovelUnlocksExactlyOneArbitraryOwnLockedCell()
        {
            var playerBoard = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            var aiBoard = DragonBoundBoardLayout.CreateDefault(TeamSide.AI);
            var unlocks = new ShovelUnlockService(playerBoard, new BoardRecruitDestination(playerBoard));
            var target = playerBoard.GetPositions(CellType.Locked)[playerBoard.GetPositions(CellType.Locked).Count - 1];
            var opponentTarget = aiBoard.GetPositions(CellType.Locked)[0];

            unlocks.GrantShovel(1);
            Assert.IsTrue(unlocks.BeginSelection());
            Assert.IsFalse(unlocks.TryUnlockCell(opponentTarget));
            Assert.AreEqual(1, unlocks.AvailableShovelCount);
            Assert.IsTrue(unlocks.TryUnlockCell(target));
            Assert.AreEqual(CellType.Battle, GetCellType(playerBoard, target));
            Assert.AreEqual(0, unlocks.AvailableShovelCount);
            Assert.AreEqual(7, playerBoard.GetPositions(CellType.Battle).Count);

            unlocks.GrantShovel(1);
            Assert.IsTrue(unlocks.BeginSelection());
            Assert.IsFalse(unlocks.TryUnlockCell(target));
            Assert.AreEqual(1, unlocks.AvailableShovelCount);
            unlocks.CancelSelection();
            Assert.AreEqual(1, unlocks.AvailableShovelCount);
        }

        [Test]
        public void BenchShovelUsesTheSameUnlockPathAndCannotBeDragged()
        {
            var board = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            var destination = new BoardRecruitDestination(board);
            var cards = new List<RecruitCard>
            {
                new RecruitCard("shovel", RecruitItemKind.Shovel, ShovelRecruitmentConfig.ShovelConfigId, string.Empty),
                new RecruitCard("basic1", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("basic2", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("basic3", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("basic4", RecruitItemKind.BasicUnit, "axe", string.Empty)
            };
            destination.Commit(destination.Plan(5), new RecruitBatch(1, cards));
            var unlocks = new ShovelUnlockService(board, destination);
            var target = board.GetPositions(CellType.Locked)[0];

            Assert.AreEqual(1, destination.GetBenchShovelCount());
            Assert.IsFalse(destination.CanBeginDrag("shovel"));
            Assert.IsFalse(new DragPlacementController(board, destination).BeginDrag("shovel"));
            Assert.IsTrue(unlocks.BeginSelection("shovel"));
            Assert.IsTrue(unlocks.TryUnlockCell(target));
            Assert.AreEqual(0, destination.GetBenchShovelCount());
            Assert.IsFalse(destination.TryGetCard("shovel", out _));
        }

        [Test]
        public void ShovelOnlyReplacesBasicsAndAlwaysLeavesOneBasicUnit()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var shovelBag = LimitedComponentBag.CreateBag(72, LimitedComponentBag.DefaultContentVersion, catalog);
            var withShovels = new RecruitDeck(
                catalog, 72, "components-only", shovelBag,
                shovelState: new ShovelRecruitmentState(() => 18));

            for (var batch = 1; batch <= 12; batch++)
            {
                var drawnBefore = shovelBag.DrawnCount;
                var actual = withShovels.DrawNext();
                var actualComponents = Count(actual, RecruitItemKind.HeroComponent);
                Assert.LessOrEqual(Count(actual, RecruitItemKind.Shovel), 1);
                Assert.GreaterOrEqual(Count(actual, RecruitItemKind.BasicUnit), 1);
                Assert.AreEqual(5, actual.Cards.Count);
                Assert.AreEqual(actualComponents, shovelBag.DrawnCount - drawnBefore);
                if (Count(actual, RecruitItemKind.Shovel) > 0)
                {
                    Assert.LessOrEqual(actualComponents, 3);
                }
            }
        }

        [Test]
        [Category("Diagnostics")]
        public void FourComponentPlanWithForgePickReservesOneComponentInTheFiniteBag()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            for (var seed = 1; seed <= 10000; seed++)
            {
                var plannedBag = LimitedComponentBag.CreateBag(
                    seed,
                    LimitedComponentBag.DefaultContentVersion,
                    catalog);
                var planned = new RecruitDeck(
                    catalog,
                    seed,
                    "forge-pick-reservation",
                    plannedBag,
                    shovelState: new ShovelRecruitmentState(() => 0));
                if (Count(planned.DrawNext(), RecruitItemKind.HeroComponent) != 4)
                {
                    continue;
                }

                var bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
                var deck = new RecruitDeck(
                    catalog,
                    seed,
                    "forge-pick-reservation",
                    bag,
                    shovelState: new ShovelRecruitmentState(() => 18));
                var batch = deck.DrawNext();
                if (Count(batch, RecruitItemKind.Shovel) == 0)
                {
                    continue;
                }

                Assert.AreEqual(4, deck.LastFiniteBatchTelemetry.PlannedComponentCount);
                Assert.AreEqual(3, deck.LastFiniteBatchTelemetry.DeliveredComponentCount);
                Assert.AreEqual(3, Count(batch, RecruitItemKind.HeroComponent));
                Assert.AreEqual(1, Count(batch, RecruitItemKind.Shovel));
                Assert.AreEqual(1, Count(batch, RecruitItemKind.BasicUnit));
                Assert.AreEqual(3, bag.DrawnCount);
                Assert.AreEqual(21, bag.RemainingCount);
                Assert.AreEqual(0, bag.DiscardedInstanceIds.Count);
                return;
            }

            Assert.Fail("No deterministic run produced a four-component Forge Pick reservation case.");
        }

        [Test]
        [Category("Diagnostics")]
        public void DynamicMissChanceAndEligiblePityFollowV2Rules()
        {
            Assert.AreEqual(0.20f, ShovelRecruitmentConfig.GetChance(0), 0.0001f);
            Assert.AreEqual(0.35f, ShovelRecruitmentConfig.GetChance(1), 0.0001f);
            Assert.AreEqual(0.50f, ShovelRecruitmentConfig.GetChance(2), 0.0001f);
            Assert.AreEqual(0.50f, ShovelRecruitmentConfig.GetChance(3), 0.0001f);

            var state = new ShovelRecruitmentState(() => 18);
            var misses = new FixedRandom(0.99f);
            for (var batch = 1; batch <= 3; batch++)
            {
                var decision = state.PreviewDecision(batch, misses);
                Assert.IsTrue(decision.IsEligible);
                Assert.IsFalse(decision.ShouldSpawn);
                Assert.AreEqual(batch - 1, decision.ConsecutiveEligibleMisses);
                state.Commit(decision);
            }

            var guaranteed = state.PreviewDecision(4, misses);
            Assert.IsTrue(guaranteed.ShouldSpawn);
            Assert.IsTrue(guaranteed.IsGuaranteed);
            Assert.AreEqual(3, guaranteed.ConsecutiveEligibleMisses);
            state.Commit(guaranteed);
            Assert.AreEqual(0, state.ConsecutiveEligibleBatchesWithoutShovel);

            var afterReset = state.PreviewDecision(5, misses);
            Assert.AreEqual(0.20f, afterReset.Chance, 0.0001f);

            var noLockedState = new ShovelRecruitmentState(() => 0);
            var ineligible = noLockedState.PreviewDecision(5, misses);
            Assert.IsFalse(ineligible.IsEligible);
            Assert.AreEqual(0, ineligible.Chance, 0.0001f);
            Assert.AreEqual(ShovelNotEligibleReason.NoLockedCells, ineligible.NotEligibleReason);
            noLockedState.Commit(ineligible);
            Assert.AreEqual(0, noLockedState.ConsecutiveEligibleBatchesWithoutShovel);
        }

        [Test]
        [Category("Diagnostics")]
        public void SameSeedReplaysShovelsWithoutChangingOtherDeckStreams()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var first = CreateDeck(catalog, 827, 18);
            var second = CreateDeck(catalog, 827, 18);
            for (var batch = 1; batch <= 12; batch++)
            {
                AssertBatchesEqual(first.DrawNext(), second.DrawNext());
            }

            var diagnostic = ShovelRecruitmentDiagnostics.SampleRecruitment(catalog, 1, 100000);
            TestContext.WriteLine("ShovelRecruitmentDistribution " + diagnostic.FormatReport());
            Assert.AreEqual(100000, diagnostic.SampleCount);
            Assert.Greater(diagnostic.RecruitOneToThreeEligible, 0);
            Assert.Greater(diagnostic.RecruitFourToSevenEligible, 0);
            Assert.Greater(diagnostic.RecruitEightToElevenEligible, 0);
            Assert.Greater(diagnostic.AverageShovelsAfterRecruit6, 1.8d);
            Assert.Less(diagnostic.AverageShovelsAfterRecruit6, 2.5d);
            Assert.AreEqual(0, diagnostic.P0ShovelsByRecruit6);
            Assert.GreaterOrEqual(
                diagnostic.P2ShovelsByRecruit6 + diagnostic.P3PlusShovelsByRecruit6,
                70000);
            Assert.AreEqual(
                diagnostic.SampleCount,
                diagnostic.P0ShovelsByRecruit6 + diagnostic.P1ShovelsByRecruit6 +
                diagnostic.P2ShovelsByRecruit6 + diagnostic.P3PlusShovelsByRecruit6);
            Assert.GreaterOrEqual(diagnostic.LongestEligibleNoShovelInterval, 0);
            for (var recruit = 1; recruit <= ShovelRecruitmentDistribution.DiagnosticRecruitCount; recruit++)
            {
                Assert.AreEqual(100d, diagnostic.GetForgePickEligibleRate(recruit), 0.0001d);
                Assert.GreaterOrEqual(diagnostic.GetAverageBasicUnitCount(recruit), 1d);
            }
            Assert.Greater(diagnostic.GetLegacyFourComponentBlockedRate(4), 70d);
        }

        [Test]
        public void FailedRecruitDoesNotCommitForgePickDecision()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var board = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            var destination = new BoardRecruitDestination(board);
            var team = new TeamState(TeamSide.Player);
            var state = new ShovelRecruitmentState(() => board.GetPositions(CellType.Locked).Count);
            var deck = new RecruitDeck(
                catalog,
                901,
                "failed-forge-pick",
                LimitedComponentBag.CreateBag(901, LimitedComponentBag.DefaultContentVersion, catalog),
                shovelState: state);
            var recruitment = new RecruitmentService(team, deck, destination);

            var failed = recruitment.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.InsufficientResources, failed.Status);
            Assert.AreEqual(0, deck.CompletedRecruitments);
            Assert.IsFalse(state.HasCommittedDecision);
            Assert.AreEqual(0, state.ConsecutiveEligibleBatchesWithoutShovel);
            Assert.AreEqual(0, deck.ComponentBag.DrawnCount);
        }

        [Test]
        public void NoLockedCellsSuppressesShovelsWithoutAdvancingPity()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var state = new ShovelRecruitmentState(() => 0);
            var deck = new RecruitDeck(
                catalog,
                411,
                "no-locked-cells",
                LimitedComponentBag.CreateBag(411, LimitedComponentBag.DefaultContentVersion, catalog),
                shovelState: state);

            for (var batch = 1; batch <= 11; batch++)
            {
                Assert.AreEqual(0, Count(deck.DrawNext(), RecruitItemKind.Shovel));
            }

            Assert.AreEqual(0, state.ConsecutiveEligibleBatchesWithoutShovel);
            Assert.AreEqual(0, state.GuaranteedShovelCount);
        }

        [Test]
        public void RefreshingBenchDiscardsUnusedShovelAndSidesRemainIndependent()
        {
            var playerBoard = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            var aiBoard = DragonBoundBoardLayout.CreateDefault(TeamSide.AI);
            var playerDestination = new BoardRecruitDestination(playerBoard);
            var aiDestination = new BoardRecruitDestination(aiBoard);
            var playerUnlocks = new ShovelUnlockService(playerBoard, playerDestination);
            var aiUnlocks = new ShovelUnlockService(aiBoard, aiDestination);

            playerUnlocks.GrantShovel(1);
            Assert.AreEqual(1, playerUnlocks.AvailableShovelCount);
            Assert.AreEqual(0, aiUnlocks.AvailableShovelCount);
            aiUnlocks.GrantShovel(2);
            Assert.AreEqual(1, playerUnlocks.AvailableShovelCount);
            Assert.AreEqual(2, aiUnlocks.AvailableShovelCount);

            var initial = new RecruitBatch(1, new List<RecruitCard>
            {
                new RecruitCard("unused-shovel", RecruitItemKind.Shovel, ShovelRecruitmentConfig.ShovelConfigId, string.Empty),
                new RecruitCard("a", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("b", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("c", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("d", RecruitItemKind.BasicUnit, "axe", string.Empty)
            });
            playerDestination.Commit(playerDestination.Plan(5), initial);
            var replacement = new RecruitBatch(2, new List<RecruitCard>
            {
                new RecruitCard("e", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("f", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("g", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("h", RecruitItemKind.BasicUnit, "axe", string.Empty),
                new RecruitCard("i", RecruitItemKind.BasicUnit, "axe", string.Empty)
            });
            var receipt = playerDestination.Commit(playerDestination.Plan(5), replacement);

            Assert.IsFalse(playerDestination.TryGetCard("unused-shovel", out _));
            Assert.AreEqual(0, playerDestination.GetBenchShovelCount());
            Assert.AreEqual(1, Count(receipt.RemovedCards, RecruitItemKind.Shovel));
        }

        private static RecruitDeck CreateDeck(RecruitmentCatalog catalog, int seed, int lockedCellCount)
        {
            return new RecruitDeck(
                catalog,
                seed,
                "shovel-test",
                LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog),
                shovelState: new ShovelRecruitmentState(() => lockedCellCount));
        }

        private static CellType GetCellType(BoardGrid board, GridPosition position)
        {
            Assert.IsTrue(board.TryGetCellType(position, out var type));
            return type;
        }

        private static int Count(RecruitBatch batch, RecruitItemKind kind)
        {
            return Count(batch.Cards, kind);
        }

        private static int Count(IReadOnlyList<RecruitCard> cards, RecruitItemKind kind)
        {
            var count = 0;
            foreach (var card in cards)
            {
                if (card.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertBatchesEqual(RecruitBatch first, RecruitBatch second)
        {
            Assert.AreEqual(first.RecruitmentNumber, second.RecruitmentNumber);
            Assert.AreEqual(first.Cards.Count, second.Cards.Count);
            for (var index = 0; index < first.Cards.Count; index++)
            {
                Assert.AreEqual(first.Cards[index].Kind, second.Cards[index].Kind);
                Assert.AreEqual(first.Cards[index].RuntimeId, second.Cards[index].RuntimeId);
                Assert.AreEqual(first.Cards[index].ConfigId, second.Cards[index].ConfigId);
            }
        }

        private sealed class FixedRandom : IRunRandom
        {
            private readonly float value;

            public FixedRandom(float value)
            {
                this.value = value;
            }

            public int Seed => 1;
            public long CallIndex { get; private set; }

            public int NextInt(string context, int minInclusive, int maxExclusive)
            {
                CallIndex++;
                return minInclusive;
            }

            public float NextUnit(string context)
            {
                CallIndex++;
                return value;
            }
        }
    }
}
