using System;
using System.Collections.Generic;
using System.Linq;
using DragonBound.Combat;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class HeroRecipeValidationTests
    {
        [Test]
        public void FrozenCatalogContainsExactlyTwelveUniqueImplementedHeroRecipes()
        {
            var expectedHeroIds = new[]
            {
                DragonBoundHeroIds.WindclawRanger,
                DragonBoundHeroIds.EmberShaman,
                DragonBoundHeroIds.RuneboltMage,
                DragonBoundHeroIds.Stonebinder,
                DragonBoundHeroIds.CrownSwordLeader,
                DragonBoundHeroIds.CrownHunterLeader,
                DragonBoundHeroIds.DragonRider,
                DragonBoundHeroIds.StarfallArchmage,
                DragonBoundHeroIds.ThunderJarl,
                DragonBoundHeroIds.NightfangAssassin,
                DragonBoundHeroIds.LeviathanHunter,
                DragonBoundHeroIds.SkyhunterValkyrie
            };

            Assert.AreEqual(12, HeroRecipeCatalog.Definitions.Count);
            CollectionAssert.AreEquivalent(
                expectedHeroIds,
                HeroRecipeCatalog.Definitions.Select(recipe => recipe.HeroId));
            Assert.AreEqual(
                12,
                HeroRecipeCatalog.Definitions.Select(recipe => recipe.HeroId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.AreEqual(
                12,
                HeroRecipeCatalog.Definitions.Select(recipe => recipe.RecipeId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.IsTrue(HeroRecipeCatalog.Definitions.All(recipe =>
                HeroDefinitionCatalog.GetMetadata(recipe.HeroId).RuntimeCombatState ==
                HeroRuntimeCombatState.Implemented));
        }

        [Test]
        public void FormalTwentyFourInstanceBagCoversEveryFormalRecipe()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var componentIds = new HashSet<string>(
                catalog.ComponentBagTemplate.Select(instance => instance.ComponentId),
                StringComparer.Ordinal);

            Assert.AreEqual(18, catalog.Components.Count);
            Assert.AreEqual(24, catalog.ComponentBagTemplate.Count);
            Assert.AreEqual(
                3,
                catalog.ComponentBagTemplate.Count(instance =>
                    instance.ComponentId == DragonBoundComponentIds.ContractHatchling));
            Assert.AreEqual(
                3,
                catalog.ComponentBagTemplate.Count(instance =>
                    instance.ComponentId == DragonBoundComponentIds.RuneStaff));
            Assert.AreEqual(
                3,
                catalog.ComponentBagTemplate.Count(instance =>
                    instance.ComponentId == DragonBoundComponentIds.AncestralWarCrown));
            Assert.AreEqual(
                24,
                catalog.ComponentBagTemplate.Select(instance => instance.InstanceId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.IsTrue(catalog.Recipes.All(recipe =>
                componentIds.Contains(recipe.ComponentAId) && componentIds.Contains(recipe.ComponentBId)));
        }

        [Test]
        public void EveryFormalHeroRecipeFormsRejectsInvalidInputAndReforms()
        {
            var results = HeroRecipeValidation.ValidateAll();

            Assert.AreEqual(12, results.Count);
            foreach (var result in results)
            {
                Assert.IsTrue(result.Registered, result.HeroId + " is not registered in the formal catalog.");
                Assert.IsTrue(result.PairLinkTest, result.HeroId + " did not form the expected PairLink.");
                Assert.AreEqual(nameof(HeroPairCombatProxy), result.Executor);
                Assert.IsTrue(result.WrongDirectionRejected, result.HeroId + " accepted an invalid direction.");
                Assert.IsTrue(result.MissingComponentRejected, result.HeroId + " accepted a missing component.");
                Assert.IsTrue(result.PairBreaksAndReforms, result.HeroId + " did not break and reform.");
            }
        }

        [Test]
        [Category("Diagnostics")]
        public void NormalFiniteRecruitmentRetainsBroadRecipeComponentCoverage()
        {
            var report = HeroRecipeValidation.AuditNormalRunSeeds(1, 1000);

            Assert.AreEqual(1000, report.SampleCount);
            // This follows the current V2 finite-bag baseline after Forge Pick reservations
            // defer some fourth components instead of silently drawing or discarding them.
            Assert.GreaterOrEqual(report.FullyCoveredCount, 900);
            Assert.LessOrEqual(report.IncompleteCount, 100);
        }
    }
}
