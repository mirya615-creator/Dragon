using System.Collections.Generic;
using System.Linq;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RecruitDeckTests
    {
        [Test]
        public void PriceStartsAtTenAndIncreasesByTwo()
        {
            Assert.AreEqual(10, RecruitmentPrice.GetCost(1));
            Assert.AreEqual(12, RecruitmentPrice.GetCost(2));
            Assert.AreEqual(24, RecruitmentPrice.GetCost(8));
            Assert.AreEqual(26, RecruitmentPrice.GetCost(9));
        }

        [Test]
        public void FirstEightRecruitmentsContainThreeComponentsAndTwoBasics()
        {
            var deck = CreateDeck(73, true);
            var componentInstances = new HashSet<string>();

            for (var recruitment = 1; recruitment <= 8; recruitment++)
            {
                var batch = deck.DrawNext();
                Assert.AreEqual(5, batch.Cards.Count);
                Assert.AreEqual(3, Count(batch, RecruitItemKind.HeroComponent));
                Assert.AreEqual(2, Count(batch, RecruitItemKind.BasicUnit));
                foreach (var card in batch.Cards)
                {
                    if (card.Kind == RecruitItemKind.HeroComponent)
                    {
                        Assert.IsTrue(componentInstances.Add(card.SourceInstanceId));
                    }
                }
            }

            Assert.AreEqual(24, componentInstances.Count);
            Assert.AreEqual(0, deck.RemainingHeroComponents);
        }

        [Test]
        public void NinthAndLaterRecruitmentsUseInfiniteBasicPoolOnly()
        {
            var deck = CreateDeck(73, false);
            RecruitBatch batch = null;
            for (var recruitment = 1; recruitment <= 40; recruitment++)
            {
                batch = deck.DrawNext();
            }

            Assert.IsNotNull(batch);
            Assert.AreEqual(5, Count(batch, RecruitItemKind.BasicUnit));
            Assert.AreEqual(0, Count(batch, RecruitItemKind.HeroComponent));
            Assert.AreEqual(40, deck.CompletedRecruitments);
        }

        [Test]
        public void SameSeedReplaysCardOrderExactly()
        {
            var first = CreateDeck(2081, false);
            var second = CreateDeck(2081, false);

            for (var recruitment = 0; recruitment < 12; recruitment++)
            {
                var firstBatch = first.DrawNext();
                var secondBatch = second.DrawNext();
                for (var slot = 0; slot < 5; slot++)
                {
                    Assert.AreEqual(firstBatch.Cards[slot].RuntimeId, secondBatch.Cards[slot].RuntimeId);
                    Assert.AreEqual(firstBatch.Cards[slot].Kind, secondBatch.Cards[slot].Kind);
                    Assert.AreEqual(firstBatch.Cards[slot].ConfigId, secondBatch.Cards[slot].ConfigId);
                    Assert.AreEqual(firstBatch.Cards[slot].SourceInstanceId, secondBatch.Cards[slot].SourceInstanceId);
                }
            }
        }

        [Test]
        public void ConstrainedDeckMeetsPurpleAndGoldTimingGuarantees()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var deck = new RecruitDeck(catalog, new RunSeed(73).Random, "player", true);
            var throughTwo = new HashSet<string>();
            var throughFour = new HashSet<string>();
            var throughSix = new HashSet<string>();

            for (var recruitment = 1; recruitment <= 6; recruitment++)
            {
                var batch = deck.DrawNext();
                foreach (var card in batch.Cards)
                {
                    if (card.Kind != RecruitItemKind.HeroComponent)
                    {
                        continue;
                    }

                    throughSix.Add(card.ConfigId);
                    if (recruitment <= 4)
                    {
                        throughFour.Add(card.ConfigId);
                    }

                    if (recruitment <= 2)
                    {
                        throughTwo.Add(card.ConfigId);
                    }
                }
            }

            Assert.IsTrue(HasCompleteRecipe(catalog, HeroRecipeRarity.Purple, throughTwo));
            Assert.IsTrue(ContainsPoolCard(catalog, HeroComponentPool.Gold, throughFour));
            Assert.IsFalse(HasCompleteRecipe(catalog, HeroRecipeRarity.Gold, throughFour));
            Assert.IsTrue(HasCompleteRecipe(catalog, HeroRecipeRarity.Gold, throughSix));
        }

        [Test]
        public void HeroSliceFirstThreeRecruitmentsUseFiniteFourComponentSequence()
        {
            var deck = CreateHeroSliceDeck(73);

            var first = deck.DrawNext();
            Assert.AreEqual(1, Count(first, RecruitItemKind.HeroComponent));
            Assert.AreEqual(4, Count(first, RecruitItemKind.BasicUnit));
            CollectionAssert.AreEquivalent(
                new[] { HeroSliceRecruitmentConfig.DragonSigilId },
                ComponentIds(first));
            Assert.AreEqual(3, deck.RemainingHeroComponents);
            Assert.AreEqual(1, deck.GetRemainingHeroComponentCount(HeroSliceRecruitmentConfig.DragonSigilId));

            var second = deck.DrawNext();
            Assert.AreEqual(2, Count(second, RecruitItemKind.HeroComponent));
            Assert.AreEqual(3, Count(second, RecruitItemKind.BasicUnit));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    HeroSliceRecruitmentConfig.SkyRangerId,
                    HeroSliceRecruitmentConfig.DragonSigilId
                },
                ComponentIds(second));
            Assert.AreEqual(1, deck.RemainingHeroComponents);

            var third = deck.DrawNext();
            Assert.AreEqual(1, Count(third, RecruitItemKind.HeroComponent));
            Assert.AreEqual(4, Count(third, RecruitItemKind.BasicUnit));
            CollectionAssert.AreEquivalent(
                new[] { HeroSliceRecruitmentConfig.DragonKnightId },
                ComponentIds(third));
            Assert.AreEqual(0, deck.RemainingHeroComponents);

            var fourth = deck.DrawNext();
            Assert.AreEqual(0, Count(fourth, RecruitItemKind.HeroComponent));
            Assert.AreEqual(5, Count(fourth, RecruitItemKind.BasicUnit));
            Assert.IsTrue(deck.EnableHeroComponents);
            Assert.IsTrue(deck.HeroSliceMode);
        }

        [Test]
        public void HeroComponent_IsRemovedFromFiniteBag()
        {
            var deck = CreateHeroSliceDeck(2081);

            var first = deck.DrawNext();
            var firstSigil = first.Cards.Single(card => card.Kind == RecruitItemKind.HeroComponent);
            Assert.AreEqual(HeroSliceRecruitmentConfig.DragonSigilId, firstSigil.ConfigId);
            Assert.IsFalse(firstSigil.IsUnique);
            Assert.AreEqual(3, deck.RemainingHeroComponents);

            var second = deck.DrawNext();
            Assert.AreEqual(1, deck.RemainingHeroComponents);
            Assert.AreEqual(0, deck.GetRemainingHeroComponentCount(HeroSliceRecruitmentConfig.DragonSigilId));
            Assert.AreEqual(0, deck.GetRemainingHeroComponentCount(HeroSliceRecruitmentConfig.SkyRangerId));
            Assert.IsTrue(second.Cards.Single(card =>
                card.ConfigId == HeroSliceRecruitmentConfig.SkyRangerId).IsUnique);

            var third = deck.DrawNext();
            Assert.AreEqual(0, deck.RemainingHeroComponents);
            Assert.IsTrue(third.Cards.Single(card =>
                card.ConfigId == HeroSliceRecruitmentConfig.DragonKnightId).IsUnique);
        }

        [Test]
        public void HeroSliceSameSeedReplaysComponentAndBasicOrderExactly()
        {
            var first = CreateHeroSliceDeck(20260801);
            var second = CreateHeroSliceDeck(20260801);

            for (var recruitment = 0; recruitment < 6; recruitment++)
            {
                var firstBatch = first.DrawNext();
                var secondBatch = second.DrawNext();
                for (var slot = 0; slot < RecruitBatch.CardsPerRecruitment; slot++)
                {
                    Assert.AreEqual(firstBatch.Cards[slot].RuntimeId, secondBatch.Cards[slot].RuntimeId);
                    Assert.AreEqual(firstBatch.Cards[slot].Kind, secondBatch.Cards[slot].Kind);
                    Assert.AreEqual(firstBatch.Cards[slot].ConfigId, secondBatch.Cards[slot].ConfigId);
                    Assert.AreEqual(firstBatch.Cards[slot].SourceInstanceId, secondBatch.Cards[slot].SourceInstanceId);
                }
            }
        }

        [Test]
        public void HeroComponentsDisabledProducesOnlyAllowedBasicUnits()
        {
            var deck = CreateDeck(73, false);
            for (var recruitment = 1; recruitment <= 8; recruitment++)
            {
                var batch = deck.DrawNext();
                Assert.AreEqual(5, batch.Cards.Count);
                Assert.IsTrue(batch.Cards.All(card => card.Kind == RecruitItemKind.BasicUnit));
                Assert.IsTrue(batch.Cards.All(card =>
                    card.ConfigId == "basic.axe_raider" ||
                    card.ConfigId == "basic.longbow_hunter" ||
                    card.ConfigId == "basic.spear_raider" ||
                    card.ConfigId == "basic.twinaxe_berserker"));
            }

            Assert.AreEqual(0, deck.RemainingHeroComponents);
            Assert.IsFalse(deck.EnableHeroComponents);
        }

        [Test]
        public void OneHundredDisabledRecruitmentsNeverExposeHeroComponents()
        {
            var deck = CreateDeck(20260801, false);
            for (var recruitment = 0; recruitment < 100; recruitment++)
            {
                var batch = deck.DrawNext();
                Assert.AreEqual(RecruitmentService.CardsPerRecruitment, batch.Cards.Count);
                Assert.IsTrue(batch.Cards.All(card => card.Kind == RecruitItemKind.BasicUnit));
                Assert.IsTrue(batch.Cards.All(card =>
                    card.ConfigId == "basic.axe_raider" ||
                    card.ConfigId == "basic.longbow_hunter" ||
                    card.ConfigId == "basic.spear_raider" ||
                    card.ConfigId == "basic.twinaxe_berserker"));
            }

            Assert.AreEqual(100, deck.CompletedRecruitments);
            Assert.AreEqual(0, deck.RemainingHeroComponents);
        }

        [Test]
        public void ComponentRuntimeIsDataOnlyAndPairLinkOwnsCombatProxy()
        {
            var componentType = typeof(ComponentRuntime);
            Assert.IsNull(componentType.GetField("CombatController"));
            Assert.IsNull(componentType.GetProperty("CombatController"));
            Assert.IsNull(componentType.GetProperty("CombatProxy"));
            Assert.IsNull(componentType.GetMethod("Attack"));
            Assert.IsNull(componentType.GetMethod("AcquireTarget"));
            Assert.IsNull(componentType.GetMethod("AddExperience"));
            Assert.IsNull(componentType.GetMethod("GetRange"));
            Assert.IsNotNull(typeof(HeroPairLink).GetProperty("CombatProxy"));
        }

        private static RecruitDeck CreateDeck(int seed, bool enableHeroComponents)
        {
            return new RecruitDeck(
                GreyboxRecruitmentCatalog.Create(),
                new RunSeed(seed).Random,
                "player",
                enableHeroComponents);
        }

        private static RecruitDeck CreateHeroSliceDeck(int seed)
        {
            return new RecruitDeck(
                GreyboxRecruitmentCatalog.Create(),
                new RunSeed(seed).Random,
                "player",
                true,
                true);
        }

        private static string[] ComponentIds(RecruitBatch batch)
        {
            return batch.Cards
                .Where(card => card.Kind == RecruitItemKind.HeroComponent)
                .Select(card => card.ConfigId)
                .ToArray();
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

        private static bool HasCompleteRecipe(
            RecruitmentCatalog catalog,
            HeroRecipeRarity rarity,
            HashSet<string> configIds)
        {
            foreach (var recipe in catalog.Recipes)
            {
                if (recipe.Rarity == rarity &&
                    configIds.Contains(recipe.ComponentAId) &&
                    configIds.Contains(recipe.ComponentBId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPoolCard(
            RecruitmentCatalog catalog,
            HeroComponentPool pool,
            HashSet<string> configIds)
        {
            foreach (var configId in configIds)
            {
                if (catalog.GetComponent(configId).Pool == pool)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
