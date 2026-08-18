using System;
using System.Collections.Generic;
using System.Linq;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class HeroCatalogSingleSourceTests
    {
        [Test]
        public void HeroComponentCatalogHasExactlyEighteenTypesAndTwentyFourRunInstances()
        {
            var components = HeroComponentCatalog.Definitions;
            Assert.AreEqual(18, components.Count);
            Assert.AreEqual(18, HeroComponentCatalog.RunManifest.Count);
            Assert.AreEqual(24, HeroComponentCatalog.RunManifest.Sum(entry => entry.CopiesPerRun));
            Assert.AreEqual(24, components.Sum(component => component.CopiesPerRun));
            Assert.AreEqual(18, components.Select(component => component.CanonicalId).Distinct().Count());

            AssertCopies(DragonBoundComponentIds.ContractHatchling, 3);
            AssertCopies(DragonBoundComponentIds.RuneStaff, 3);
            AssertCopies(DragonBoundComponentIds.AncestralWarCrown, 3);
            Assert.IsTrue(components
                .Where(component => !component.IsPublicCore)
                .All(component => component.CopiesPerRun == 1 && component.IsUnique));
        }

        [Test]
        public void RecruitComponentLabelsUseConfiguredChineseNames()
        {
            foreach (var component in HeroComponentCatalog.Definitions)
            {
                Assert.AreEqual(
                    component.DisplayNameZh,
                    HeroSliceCatalog.GetComponentDisplayName(component.Id),
                    component.Id);
            }
        }

        [Test]
        public void HeroDefinitionCatalogHasTwelveHeroesWithSixPurpleAndSixGold()
        {
            var heroes = HeroDefinitionCatalog.Definitions;
            Assert.AreEqual(12, heroes.Count);
            Assert.AreEqual(6, heroes.Count(hero => hero.Rarity == HeroRecipeRarity.Purple));
            Assert.AreEqual(6, heroes.Count(hero => hero.Rarity == HeroRecipeRarity.Gold));
            Assert.AreEqual(12, heroes.Select(hero => hero.Id).Distinct().Count());
            Assert.IsTrue(FrozenHeroConfigurationCatalog.Configuration.HeroMetadata.All(metadata => metadata.GalleryVisible));
        }

        [Test]
        public void RecipeCatalogHasTwelveUniqueEntriesAndEveryHeroHasExactlyOneRecipe()
        {
            var recipes = HeroRecipeCatalog.Definitions;
            Assert.AreEqual(12, recipes.Count);
            Assert.AreEqual(12, recipes.Select(recipe => recipe.RecipeId).Distinct().Count());
            Assert.AreEqual(12, recipes.Select(recipe => recipe.HeroId).Distinct().Count());

            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                var metadata = HeroDefinitionCatalog.GetMetadata(hero.Id);
                var recipe = HeroRecipeCatalog.Get(metadata.RecipeId);
                Assert.AreEqual(hero.Id, recipe.HeroId);
                CollectionAssert.AreEquivalent(
                    new[] { hero.ComponentAId, hero.ComponentBId },
                    recipe.RequiredComponentIds);
            }
        }

        [TestCase(DragonBoundRecipeIds.WindclawRanger, DragonBoundHeroIds.WindclawRanger,
            HeroFormationOrientation.Vertical, DragonBoundComponentIds.SkyRanger, DragonBoundComponentIds.ContractHatchling)]
        [TestCase(DragonBoundRecipeIds.EmberShaman, DragonBoundHeroIds.EmberShaman,
            HeroFormationOrientation.Vertical, DragonBoundComponentIds.FlameShaman, DragonBoundComponentIds.ContractHatchling)]
        [TestCase(DragonBoundRecipeIds.DragonRider, DragonBoundHeroIds.DragonRider,
            HeroFormationOrientation.Vertical, DragonBoundComponentIds.DragonKnight, DragonBoundComponentIds.ContractHatchling)]
        [TestCase(DragonBoundRecipeIds.RuneboltMage, DragonBoundHeroIds.RuneboltMage,
            HeroFormationOrientation.Horizontal, DragonBoundComponentIds.RuneStaff, DragonBoundComponentIds.RuneApprentice)]
        [TestCase(DragonBoundRecipeIds.Stonebinder, DragonBoundHeroIds.Stonebinder,
            HeroFormationOrientation.Horizontal, DragonBoundComponentIds.RuneStaff, DragonBoundComponentIds.StoneScholar)]
        [TestCase(DragonBoundRecipeIds.StarfallArchmage, DragonBoundHeroIds.StarfallArchmage,
            HeroFormationOrientation.Horizontal, DragonBoundComponentIds.RuneStaff, DragonBoundComponentIds.AstralMage)]
        [TestCase(DragonBoundRecipeIds.CrownSwordLeader, DragonBoundHeroIds.CrownSwordLeader,
            HeroFormationOrientation.Vertical, DragonBoundComponentIds.AncestralWarCrown, DragonBoundComponentIds.WanderingSwordsman)]
        [TestCase(DragonBoundRecipeIds.CrownHunterLeader, DragonBoundHeroIds.CrownHunterLeader,
            HeroFormationOrientation.Vertical, DragonBoundComponentIds.AncestralWarCrown, DragonBoundComponentIds.NorthlandScout)]
        [TestCase(DragonBoundRecipeIds.ThunderJarl, DragonBoundHeroIds.ThunderJarl,
            HeroFormationOrientation.Vertical, DragonBoundComponentIds.AncestralWarCrown, DragonBoundComponentIds.StormWarrior)]
        [TestCase(DragonBoundRecipeIds.NightfangAssassin, DragonBoundHeroIds.NightfangAssassin,
            HeroFormationOrientation.Horizontal, DragonBoundComponentIds.RuneDagger, DragonBoundComponentIds.ShadowWalker)]
        [TestCase(DragonBoundRecipeIds.LeviathanHunter, DragonBoundHeroIds.LeviathanHunter,
            HeroFormationOrientation.Horizontal, DragonBoundComponentIds.AncientHarpoon, DragonBoundComponentIds.DeepseaHarpooner)]
        [TestCase(DragonBoundRecipeIds.SkyhunterValkyrie, DragonBoundHeroIds.SkyhunterValkyrie,
            HeroFormationOrientation.Horizontal, DragonBoundComponentIds.DragonboneLongbow, DragonBoundComponentIds.ValkyrieAcolyte)]
        public void AllRecipesUseTheFrozenComponentsAndDirections(
            string recipeId,
            string heroId,
            HeroFormationOrientation orientation,
            string firstRequiredComponent,
            string secondRequiredComponent)
        {
            var recipe = HeroRecipeCatalog.Get(recipeId);
            Assert.AreEqual(heroId, recipe.HeroId);
            Assert.AreEqual(orientation, recipe.FormationOrientation);
            if (orientation == HeroFormationOrientation.Vertical)
            {
                Assert.AreEqual(firstRequiredComponent, recipe.TopComponentId);
                Assert.AreEqual(secondRequiredComponent, recipe.BottomComponentId);
                Assert.IsTrue(recipe.MatchesFormation(
                    firstRequiredComponent, new GridPosition(3, 4),
                    secondRequiredComponent, new GridPosition(3, 3)));
                Assert.IsFalse(recipe.MatchesFormation(
                    secondRequiredComponent, new GridPosition(3, 4),
                    firstRequiredComponent, new GridPosition(3, 3)));
                return;
            }

            Assert.AreEqual(firstRequiredComponent, recipe.LeftComponentId);
            Assert.AreEqual(secondRequiredComponent, recipe.RightComponentId);
            Assert.IsTrue(recipe.MatchesFormation(
                firstRequiredComponent, new GridPosition(3, 3),
                secondRequiredComponent, new GridPosition(4, 3)));
            Assert.IsFalse(recipe.MatchesFormation(
                secondRequiredComponent, new GridPosition(3, 3),
                firstRequiredComponent, new GridPosition(4, 3)));
        }

        [Test]
        public void LegacyAliasesResolveToCanonicalIdsWithoutLeakingDeprecatedDisplayNames()
        {
            Assert.AreEqual(
                DragonBoundComponentIds.ContractHatchling,
                HeroComponentCatalog.Get("CORE_DRAGON_SIGIL").CanonicalId);
            Assert.AreEqual(
                DragonBoundComponentIds.AstralMage,
                HeroComponentCatalog.Get("PART_METEOR_CORE").CanonicalId);
            Assert.AreEqual(
                DragonBoundHeroIds.CrownSwordLeader,
                HeroDefinitionCatalog.Get("HERO_HORNBLADE_DUELIST").Id);
            Assert.AreEqual(
                DragonBoundHeroIds.CrownHunterLeader,
                HeroDefinitionCatalog.Get("HERO_NORTHWATCH_HUNTER").Id);

            Assert.IsFalse(HeroComponentCatalog.Definitions.Any(component =>
                component.DisplayNameZh == "龙纹印记" ||
                component.DisplayNameZh == "符文魔典" ||
                component.DisplayNameZh == "战争号角" ||
                component.DisplayNameZh == "陨星核心" ||
                component.DisplayNameZh == "风暴王冠" ||
                component.DisplayNameZh == "暗影斗篷" ||
                component.DisplayNameZh == "海兽之眼" ||
                component.DisplayNameZh == "女武神羽翼"));
        }

        [Test]
        public void FrozenCrownNamesAndCombatStatesRemainExplicit()
        {
            Assert.AreEqual(
                HeroNameFreezeState.Frozen,
                HeroDefinitionCatalog.GetMetadata(DragonBoundHeroIds.CrownSwordLeader).NameFreezeState);
            Assert.AreEqual(
                HeroNameFreezeState.Frozen,
                HeroDefinitionCatalog.GetMetadata(DragonBoundHeroIds.CrownHunterLeader).NameFreezeState);
            var sword = HeroDefinitionCatalog.Get(DragonBoundHeroIds.CrownSwordLeader);
            Assert.AreEqual("冠誓剑士", sword.DisplayNameZh);
            Assert.AreEqual("Oathcrown Swordsman", sword.DisplayNameEn);
            Assert.AreEqual("霜冠猎手", HeroDefinitionCatalog.Get(DragonBoundHeroIds.CrownHunterLeader).DisplayNameZh);
            Assert.AreEqual("Frostcrown Hunter", HeroDefinitionCatalog.Get(DragonBoundHeroIds.CrownHunterLeader).DisplayNameEn);
            Assert.AreEqual(DragonBoundHeroIds.CrownSwordLeader, HeroRecipeCatalog.Get(DragonBoundRecipeIds.CrownSwordLeader).HeroId);
            Assert.AreEqual(DragonBoundHeroIds.CrownHunterLeader, HeroRecipeCatalog.Get(DragonBoundRecipeIds.CrownHunterLeader).HeroId);

            var implemented = HeroDefinitionCatalog.Definitions
                .Where(hero => HeroDefinitionCatalog.GetMetadata(hero.Id).RuntimeCombatState == HeroRuntimeCombatState.Implemented)
                .Select(hero => hero.Id)
                .OrderBy(id => id)
                .ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    DragonBoundHeroIds.WindclawRanger,
                    DragonBoundHeroIds.EmberShaman,
                    DragonBoundHeroIds.DragonRider,
                    DragonBoundHeroIds.RuneboltMage,
                    DragonBoundHeroIds.Stonebinder,
                    DragonBoundHeroIds.StarfallArchmage,
                    DragonBoundHeroIds.CrownSwordLeader,
                    DragonBoundHeroIds.CrownHunterLeader,
                    DragonBoundHeroIds.ThunderJarl,
                    DragonBoundHeroIds.NightfangAssassin,
                    DragonBoundHeroIds.LeviathanHunter,
                    DragonBoundHeroIds.SkyhunterValkyrie
                },
                implemented);
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.RuneboltMage));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.Stonebinder));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.StarfallArchmage));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.CrownSwordLeader));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.CrownHunterLeader));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.ThunderJarl));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.NightfangAssassin));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.LeviathanHunter));
            Assert.DoesNotThrow(() => HeroSliceCatalog.Get(DragonBoundHeroIds.SkyhunterValkyrie));
        }

        [Test]
        public void ComponentCatalogProvidesRecipeAndArtHandoffMetadata()
        {
            foreach (var component in HeroComponentCatalog.Definitions)
            {
                Assert.AreEqual(component.Id, component.CanonicalId);
                Assert.IsTrue(component.ArtSlotId.StartsWith("ART_Component_", StringComparison.Ordinal));
                Assert.AreEqual(component.CompatibleHeroIds.Count, component.CompatibleRecipeIds.Count);
                Assert.IsTrue(component.CompatibleRecipeIds.All(id => id.StartsWith("RECIPE_", StringComparison.Ordinal)));
            }

            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                var metadata = HeroDefinitionCatalog.GetMetadata(hero.Id);
                Assert.IsTrue(metadata.ArtSlotId.StartsWith("ART_Hero_", StringComparison.Ordinal));
                Assert.IsTrue(metadata.GalleryVisible);
            }
        }

        private static void AssertCopies(string componentId, int expected)
        {
            Assert.AreEqual(expected, HeroComponentCatalog.Get(componentId).CopiesPerRun);
        }
    }
}
