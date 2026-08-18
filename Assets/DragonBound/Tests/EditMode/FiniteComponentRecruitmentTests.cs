using System.Collections.Generic;
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
            Assert.AreEqual(0.50f, FiniteComponentRecruitmentConfig.ThreeComponentBatchChance, 0.0001f);
            Assert.AreEqual(10, FiniteComponentRecruitmentConfig.NormalProbabilityBatchCount);
            Assert.AreEqual(11, FiniteComponentRecruitmentConfig.GuaranteedCompletionBatch);
            Assert.AreEqual(2, FiniteComponentRecruitmentConfig.NormalMinComponentsPerBatch);
            Assert.AreEqual(3, FiniteComponentRecruitmentConfig.NormalMaxComponentsPerBatch);
        }

        [Test]
        public void FirstTenBatchesUseTwoOrThreeComponentsUntilBagIsEmpty()
        {
            var deck = CreateFiniteDeck(73, out var bag);
            for (var batch = 1; batch <= 10; batch++)
            {
                var hadComponents = bag.RemainingCount > 0;
                var result = deck.DrawNext();
                Assert.AreEqual(5, result.Cards.Count);
                var componentCount = Count(result, RecruitItemKind.HeroComponent);
                if (hadComponents)
                {
                    Assert.IsTrue(componentCount == 2 || componentCount == 3);
                    Assert.AreEqual(5 - componentCount, Count(result, RecruitItemKind.BasicUnit));
                }
                else
                {
                    Assert.AreEqual(0, componentCount);
                    Assert.AreEqual(5, Count(result, RecruitItemKind.BasicUnit));
                }
            }
        }

        [Test]
        public void EleventhBatchGuaranteesAllTwentyFourInstancesHaveBeenDistributed()
        {
            var deck = CreateFiniteDeck(20260801, out var bag);
            var instanceIds = new HashSet<string>();
            for (var batch = 1; batch <= 11; batch++)
            {
                foreach (var card in deck.DrawNext().Cards)
                {
                    if (card.Kind == RecruitItemKind.HeroComponent)
                    {
                        Assert.IsTrue(instanceIds.Add(card.SourceInstanceId));
                    }
                }
            }

            Assert.AreEqual(24, instanceIds.Count);
            Assert.AreEqual(0, bag.RemainingCount);
            Assert.IsTrue(bag.IsExhausted);
        }

        [Test]
        public void EarliestPossibleCompletionIsBatchEightAndNoRunCompletesAfterBatchEleven()
        {
            var distribution = FiniteComponentRecruitmentDiagnostics.SampleCompletionBatches(
                GreyboxRecruitmentCatalog.Create(),
                1,
                10000);

            TestContext.WriteLine(
                $"FiniteComponentCompletionDistribution Samples={distribution.SampleCount} " +
                $"B8={distribution.Batch8} B9={distribution.Batch9} " +
                $"B10={distribution.Batch10} B11={distribution.Batch11} " +
                $"Late={distribution.LateOrIncomplete}");
            Assert.Greater(distribution.Batch8, 0);
            Assert.AreEqual(0, distribution.LateOrIncomplete);
            Assert.AreEqual(distribution.SampleCount, distribution.CompletedCount);
        }

        [Test]
        public void FiniteBagFillsAllLaterBatchesWithBasicsAfterEarlyExhaustion()
        {
            var seed = FindSeedCompletingAtBatchEight();
            var deck = CreateFiniteDeck(seed, out var bag);
            for (var batch = 1; batch <= 8; batch++)
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
            var deck = CreateFiniteDeck(222, out var bag);
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
            Assert.GreaterOrEqual(bag.DiscardedInstanceIds.Count, 2);
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

        private static RecruitDeck CreateFiniteDeck(int seed, out LimitedComponentBag bag)
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
            return new RecruitDeck(catalog, seed, "player", bag);
        }

        private static int FindSeedCompletingAtBatchEight()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            for (var seed = 1; seed <= 10000; seed++)
            {
                var bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
                var deck = new RecruitDeck(catalog, seed, "player", bag);
                for (var batch = 1; batch <= 8; batch++)
                {
                    deck.DrawNext();
                }

                if (bag.IsExhausted)
                {
                    return seed;
                }
            }

            Assert.Fail("No deterministic seed completed the component bag by batch eight.");
            return 0;
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
