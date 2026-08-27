using System.Collections.Generic;
using System.Linq;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class FiniteComponentRecruitmentTests
    {
        [Test]
        public void FiniteRecruitmentConfigurationMatchesFrozenBalanceValues()
        {
            Assert.AreEqual(11, FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount);
            Assert.AreEqual(4, FiniteComponentRecruitmentConfig.MaxComponentsPerBatch);
            Assert.AreEqual(2, FiniteComponentRecruitmentConfig.MultiMinComponentsPerBatch);
            Assert.AreEqual(4, FiniteComponentRecruitmentConfig.MultiMaxComponentsPerBatch);
            Assert.AreEqual(1, FiniteComponentRecruitmentConfig.MinBasicUnitsPerBatch);
            Assert.AreEqual(0.50f, FiniteComponentRecruitmentConfig.BasePureBasicWeight, 0.0001f);
            Assert.AreEqual(0.20f, FiniteComponentRecruitmentConfig.BaseOneComponentWeight, 0.0001f);
            Assert.AreEqual(0.20f, FiniteComponentRecruitmentConfig.BaseMultiComponentWeight, 0.0001f);
            Assert.AreEqual(0.10f, FiniteComponentRecruitmentConfig.BaseShovelWeight, 0.0001f);
            Assert.AreEqual(3, FiniteComponentRecruitmentConfig.OpeningProtectedRecruitCount);
            Assert.AreEqual(0.80f, FiniteComponentRecruitmentConfig.BaseExpectedComponentsPerRecruit, 0.0001f);
            Assert.AreEqual(5.00f, FiniteComponentRecruitmentConfig.CatchupAllowedComponentsPerRecruit, 0.0001f);
        }

        [Test]
        public void DynamicCatchupBatchesKeepFiveResultsAndNeverReplaceTheLastBasicUnit()
        {
            var deck = CreateFiniteDeck(73, out var bag);
            for (var batch = 1; batch <= 14; batch++)
            {
                var result = deck.DrawNext();
                Assert.AreEqual(5, result.Cards.Count);
                var componentCount = Count(result, RecruitItemKind.HeroComponent);
                Assert.LessOrEqual(componentCount, FiniteComponentRecruitmentConfig.MaxComponentsPerBatch);
                Assert.GreaterOrEqual(Count(result, RecruitItemKind.BasicUnit), 1);
                Assert.AreEqual(0, Count(result, RecruitItemKind.Shovel));
                Assert.AreEqual(5 - componentCount, Count(result, RecruitItemKind.BasicUnit));
            }
        }

        [Test]
        public void DynamicCatchupDistributesUniqueComponentInstancesWithoutReturningCards()
        {
            var deck = CreateFiniteDeck(20260801, out var bag);
            var instanceIds = new HashSet<string>();
            for (var batch = 1; batch <= 16; batch++)
            {
                foreach (var card in deck.DrawNext().Cards)
                {
                    if (card.Kind == RecruitItemKind.HeroComponent)
                    {
                        Assert.IsTrue(instanceIds.Add(card.SourceInstanceId));
                    }
                }
            }

            Assert.AreEqual(bag.DrawnCount, instanceIds.Count);
            Assert.LessOrEqual(instanceIds.Count, 24);
        }

        [Test]
        [Category("Diagnostics")]
        public void DynamicCatchupMonteCarloKeepsFormalV2SupplyWithoutHardRecruitElevenCompletion()
        {
            var distribution = FiniteComponentRecruitmentDiagnostics.SampleDynamicCatchup(
                GreyboxRecruitmentCatalog.Create(),
                1,
                10000);

            TestContext.WriteLine("DynamicFiniteRecruitmentDistribution\n" + distribution.FormatReport());
            Assert.AreEqual(10000, distribution.SampleCount);
            // Forge Pick V2 reserves a basic-result slot before drawing the finite bag. A planned
            // fourth component correctly remains in the bag, so V2 no longer promises the old
            // 95% Recruit-11 completion target. This protects the current formal baseline
            // without changing its probability or catch-up implementation.
            Assert.GreaterOrEqual(distribution.BagEmptyByRecruit11Rate, 85d);
            Assert.Less(distribution.BagEmptyByRecruit11Rate, 99.9d);
            AssertRateBetween(distribution.PureBasicByRecruit[1], distribution.SampleCount, 0.45d, 0.55d);
            AssertRateBetween(distribution.MultiComponentByRecruit[1], distribution.SampleCount, 0.15d, 0.25d);
            AssertRateBetween(distribution.ShovelByRecruit[1], distribution.SampleCount, 0.14d, 0.22d);
            Assert.GreaterOrEqual(distribution.BasicUnitCountByRecruit[1] / (double)distribution.SampleCount, 3.8d);
            Assert.Less(distribution.MultiComponentByRecruit[2] / (double)distribution.SampleCount, 0.60d);
            Assert.Less(distribution.MultiComponentByRecruit[3] / (double)distribution.SampleCount, 0.60d);
            Assert.AreEqual(
                distribution.SampleCount,
                Sum(distribution.BagEmptyAtRecruit) +
                GetRemainingAfterRecruit11Count(distribution));
        }

        [Test]
        [Category("Diagnostics")]
        public void CompletionDistributionStillReportsOnlyOfficialRecruitDeckResults()
        {
            var distribution = FiniteComponentRecruitmentDiagnostics.SampleCompletionBatches(
                GreyboxRecruitmentCatalog.Create(),
                1,
                10000);

            TestContext.WriteLine("FiniteComponentCompletionDistribution " + distribution.FormatReport());
            Assert.AreEqual(10000, distribution.SampleCount);
            Assert.GreaterOrEqual(distribution.CompletedCount, 8500);
            Assert.Less(distribution.LateOrIncomplete, 1500);
            Assert.AreEqual(
                distribution.SampleCount,
                distribution.Batch8 + distribution.Batch9 + distribution.Batch10 + distribution.Batch11 +
                distribution.LateOrIncomplete);
        }

        [Test]
        [Category("Diagnostics")]
        public void FiniteBagFillsAllLaterBatchesWithBasicsAfterEarlyExhaustion()
        {
            var seed = FindSeedCompletingByRecruitEleven();
            var deck = CreateFiniteDeck(seed, out var bag);
            for (var batch = 1; batch <= FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount; batch++)
            {
                deck.DrawNext();
            }

            Assert.IsTrue(bag.IsExhausted);
            var later = deck.DrawNext();
            Assert.AreEqual(0, Count(later, RecruitItemKind.HeroComponent));
            Assert.AreEqual(5, Count(later, RecruitItemKind.BasicUnit));
        }

        [Test]
        public void SameRunSeedReplaysComponentInstancesBasicUnitsAndSlotOrderExactly()
        {
            var first = CreateFiniteDeck(90210, out _);
            var second = CreateFiniteDeck(90210, out _);
            for (var batch = 1; batch <= 12; batch++)
            {
                AssertBatchesEqual(first.DrawNext(), second.DrawNext());
            }
        }

        [Test]
        public void IndependentRandomCallsDoNotPolluteFiniteRecruitmentStreams()
        {
            var expected = CreateFiniteDeck(12345, out _);
            var actual = CreateFiniteDeck(12345, out _);
            var unrelated = new RunRandom(99999);
            for (var index = 0; index < 1000; index++)
            {
                unrelated.NextInt("unrelated", 0, 1000);
            }

            for (var batch = 1; batch <= 12; batch++)
            {
                AssertBatchesEqual(expected.DrawNext(), actual.DrawNext());
            }
        }

        [Test]
        public void InsufficientResourcesAndPreviewDoNotAdvanceFiniteDeckState()
        {
            var deck = CreateFiniteDeck(73, out var bag);
            var preview = deck.PeekNext();
            Assert.AreEqual(0, deck.CompletedRecruitments);
            Assert.AreEqual(24, bag.RemainingCount);
            Assert.AreEqual(5, preview.Cards.Count);

            var team = new TeamState(TeamSide.Player);
            team.AddResources(9);
            var service = new RecruitmentService(
                team,
                deck,
                new BoardRecruitDestination(DragonBoundBoardLayout.CreateInitial()));
            var attempt = service.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.InsufficientResources, attempt.Status);
            Assert.AreEqual(9, team.Resources);
            Assert.AreEqual(0, deck.CompletedRecruitments);
            Assert.AreEqual(24, bag.RemainingCount);
        }

        [Test]
        public void RefreshedFiniteComponentsArePermanentlyDiscardedAndDoNotReturnToBag()
        {
            var deck = CreateFiniteDeck(FindSeedWhoseFirstBatchContainsComponents(), out var bag);
            var team = new TeamState(TeamSide.Player);
            team.AddResources(100);
            var service = new RecruitmentService(
                team,
                deck,
                new BoardRecruitDestination(DragonBoundBoardLayout.CreateInitial()));

            var first = service.TryRecruit();
            var second = service.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.Success, first.Status);
            Assert.IsTrue(second.RefreshedBench);
            var drawnCount = Count(first.Batch, RecruitItemKind.HeroComponent) +
                             Count(second.Batch, RecruitItemKind.HeroComponent);
            foreach (var card in second.RefreshedCards)
            {
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    Assert.IsTrue(bag.WasDiscarded(card.SourceInstanceId));
                }
            }

            Assert.AreEqual(drawnCount, bag.DrawnInstanceIds.Count);
            Assert.GreaterOrEqual(bag.DiscardedInstanceIds.Count, 1);
        }

        [Test]
        public void SavedFiniteDeckResumesWithTheSameFutureRecruitmentSequence()
        {
            var original = CreateFiniteDeck(661, out _);
            original.DrawNext();
            original.DrawNext();
            original.DrawNext();

            var json = JsonUtility.ToJson(original.CaptureState());
            var restored = RecruitDeck.RestoreFinite(
                GreyboxRecruitmentCatalog.Create(),
                JsonUtility.FromJson<RecruitDeckState>(json));
            for (var batch = 4; batch <= 12; batch++)
            {
                AssertBatchesEqual(original.DrawNext(), restored.DrawNext());
            }
        }

        [Test]
        public void PlayerAndAiFiniteRecruitmentStatesAreIndependent()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var playerBag = LimitedComponentBag.CreateBag(77, LimitedComponentBag.DefaultContentVersion, catalog);
            var aiBag = LimitedComponentBag.CreateBag(88, LimitedComponentBag.DefaultContentVersion, catalog);
            var player = new RecruitDeck(catalog, 77, "player", playerBag);
            var ai = new RecruitDeck(catalog, 88, "ai", aiBag);

            player.DrawNext();
            player.DrawNext();

            Assert.AreEqual(2, player.CompletedRecruitments);
            Assert.Less(playerBag.RemainingCount, 24);
            Assert.AreEqual(0, ai.CompletedRecruitments);
            Assert.AreEqual(24, aiBag.RemainingCount);
            Assert.IsFalse(aiBag.WasDiscarded(playerBag.OrderedComponentInstanceIds[0]));
        }

        [Test]
        public void OpeningRecruitmentsDoNotGuaranteeAPurplePairOrTwoDeliveredComponents()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var foundUnmatchedBagOpening = false;
            var foundFewerThanTwoComponentsByRecruitThree = false;
            for (var seed = 1; seed <= 512; seed++)
            {
                var bag = LimitedComponentBag.CreateBag(
                    seed,
                    LimitedComponentBag.DefaultContentVersion,
                    catalog);
                var first = bag.GetInstance(bag.OrderedComponentInstanceIds[0]).ComponentId;
                var second = bag.GetInstance(bag.OrderedComponentInstanceIds[1]).ComponentId;
                if (!catalog.Recipes.Any(recipe =>
                        recipe.Rarity == HeroRecipeRarity.Purple && recipe.Matches(first, second)))
                {
                    foundUnmatchedBagOpening = true;
                }

                var deck = new RecruitDeck(
                    catalog,
                    seed,
                    "player",
                    bag,
                    componentPolicy: RecruitComponentPolicy.V3,
                    currentWaveProvider: () => 1);
                deck.DrawNext();
                deck.DrawNext();
                deck.DrawNext();
                if (bag.DrawnCount < 2)
                {
                    foundFewerThanTwoComponentsByRecruitThree = true;
                }

                if (foundUnmatchedBagOpening && foundFewerThanTwoComponentsByRecruitThree)
                {
                    break;
                }
            }

            Assert.IsTrue(foundUnmatchedBagOpening,
                "The shuffled bag must not force a purple recipe into its first two positions.");
            Assert.IsTrue(foundFewerThanTwoComponentsByRecruitThree,
                "Normal V3 rolls must allow fewer than two components across the first three recruits.");
        }

        [Test]
        public void FiniteBasicUnitsUseEveryCatalogEntryBeforeRepeating()
        {
            var deck = CreateFiniteDeck(20260824, out _);
            for (var batchIndex = 0; batchIndex < 8; batchIndex++)
            {
                var basicIds = deck.DrawNext().Cards
                    .Where(card => card.Kind == RecruitItemKind.BasicUnit)
                    .Select(card => card.ConfigId)
                    .ToList();
                Assert.AreEqual(
                    System.Math.Min(basicIds.Count, 4),
                    basicIds.Distinct().Count());
            }
        }

        private static RecruitDeck CreateFiniteDeck(int seed, out LimitedComponentBag bag)
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
            return new RecruitDeck(
                catalog,
                seed,
                "player",
                bag,
                shovelState: new ShovelRecruitmentState(() => 0));
        }

        private static int FindSeedCompletingByRecruitEleven()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            for (var seed = 1; seed <= 100000; seed++)
            {
                var bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
                var deck = new RecruitDeck(catalog, seed, "player", bag);
                for (var batch = 1; batch <= FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount; batch++)
                {
                    deck.DrawNext();
                }

                if (bag.IsExhausted)
                {
                    return seed;
                }
            }

            Assert.Fail("No deterministic seed completed the component bag by recruit eleven.");
            return 0;
        }

        private static int FindSeedWhoseFirstBatchContainsComponents()
        {
            for (var seed = 1; seed <= 10000; seed++)
            {
                var deck = CreateFiniteDeck(seed, out _);
                if (Count(deck.PeekNext(), RecruitItemKind.HeroComponent) > 0)
                {
                    return seed;
                }
            }

            Assert.Fail("No deterministic seed produced a first-batch component.");
            return 0;
        }

        private static int Sum(int[] values)
        {
            var sum = 0;
            for (var index = 0; index < values.Length; index++)
            {
                sum += values[index];
            }

            return sum;
        }

        private static int GetRemainingAfterRecruit11Count(DynamicFiniteRecruitmentDistribution distribution)
        {
            var count = 0;
            foreach (var pair in distribution.RemainingComponentsAfterRecruit11)
            {
                if (pair.Key > 0)
                {
                    count += pair.Value;
                }
            }

            return count;
        }

        private static void AssertRateBetween(int count, int denominator, double minInclusive, double maxInclusive)
        {
            var rate = count / (double)denominator;
            Assert.GreaterOrEqual(rate, minInclusive);
            Assert.LessOrEqual(rate, maxInclusive);
        }

        private static int Count(RecruitBatch batch, RecruitItemKind kind)
        {
            var count = 0;
            foreach (var card in batch.Cards)
            {
                if (card.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertBatchesEqual(RecruitBatch expected, RecruitBatch actual)
        {
            Assert.AreEqual(expected.RecruitmentNumber, actual.RecruitmentNumber);
            Assert.AreEqual(expected.Cards.Count, actual.Cards.Count);
            for (var index = 0; index < expected.Cards.Count; index++)
            {
                Assert.AreEqual(expected.Cards[index].RuntimeId, actual.Cards[index].RuntimeId);
                Assert.AreEqual(expected.Cards[index].Kind, actual.Cards[index].Kind);
                Assert.AreEqual(expected.Cards[index].ConfigId, actual.Cards[index].ConfigId);
                Assert.AreEqual(expected.Cards[index].SourceInstanceId, actual.Cards[index].SourceInstanceId);
            }
        }
    }
}
