using System.Linq;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class HeroWorkshopCatalogTests
    {
        [Test]
        public void HeroGalleryContainsTwelveRecipes()
        {
            Assert.AreEqual(12, FrozenHeroConfigurationCatalog.Configuration.Recipes.Count);
            Assert.AreEqual(12, FrozenHeroConfigurationCatalog.Configuration.Heroes.Count);
        }

        [Test]
        public void HeroGalleryUsesConfiguredDirections()
        {
            var recipes = FrozenHeroConfigurationCatalog.Configuration.Recipes;
            Assert.AreEqual(6, recipes.Count(recipe =>
                recipe.FormationOrientation == HeroFormationOrientation.Vertical));
            Assert.AreEqual(6, recipes.Count(recipe =>
                recipe.FormationOrientation == HeroFormationOrientation.Horizontal));
            Assert.IsTrue(recipes.All(recipe => !string.IsNullOrWhiteSpace(recipe.FormationPrefabId)));
        }

        [Test]
        public void HeroGalleryReadsFromRecipeCatalog()
        {
            foreach (var hero in FrozenHeroConfigurationCatalog.Configuration.Heroes)
            {
                var recipe = FrozenHeroConfigurationCatalog.GetRecipe(hero.Id);
                Assert.AreEqual(hero.Id, recipe.HeroId);
                Assert.IsTrue(recipe.Matches(hero.ComponentAId, hero.ComponentBId));
            }
        }

        [Test]
        public void DragonRecipeRequiresPersonAboveDragon()
        {
            Assert.IsTrue(HeroSliceCatalog.TryGetRecipeDefinitionAtFormation(
                DragonBoundComponentIds.SkyRanger,
                new GridPosition(0, 2),
                DragonBoundComponentIds.DragonSigil,
                new GridPosition(0, 1),
                out var recipe));
            Assert.AreEqual(DragonBoundHeroIds.WindclawRanger, recipe.HeroId);
        }

        [Test]
        public void DragonRecipeRejectsDragonAbovePerson()
        {
            Assert.IsFalse(HeroSliceCatalog.TryGetRecipeDefinitionAtFormation(
                DragonBoundComponentIds.DragonSigil,
                new GridPosition(0, 2),
                DragonBoundComponentIds.SkyRanger,
                new GridPosition(0, 1),
                out _));
        }

        [Test]
        public void DragonRecipeRejectsHorizontalLayout()
        {
            Assert.IsFalse(HeroSliceCatalog.TryGetRecipeDefinitionAtFormation(
                DragonBoundComponentIds.SkyRanger,
                new GridPosition(0, 2),
                DragonBoundComponentIds.DragonSigil,
                new GridPosition(1, 2),
                out _));
        }

        [Test]
        public void CrownRecipeRequiresCrownAbovePerson()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.HornbladeDuelist);
            Assert.IsTrue(recipe.MatchesFormation(
                DragonBoundComponentIds.WarHorn,
                new GridPosition(1, 2),
                DragonBoundComponentIds.WanderingSword,
                new GridPosition(1, 1)));
            Assert.IsFalse(recipe.MatchesFormation(
                DragonBoundComponentIds.WanderingSword,
                new GridPosition(1, 2),
                DragonBoundComponentIds.WarHorn,
                new GridPosition(1, 1)));
        }

        [Test]
        public void WeaponRecipeRequiresWeaponLeftPersonRight()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.RuneboltMage);
            Assert.IsTrue(recipe.MatchesFormation(
                DragonBoundComponentIds.RuneGrimoire,
                new GridPosition(0, 1),
                DragonBoundComponentIds.RuneApprentice,
                new GridPosition(1, 1)));
            Assert.IsFalse(recipe.MatchesFormation(
                DragonBoundComponentIds.RuneApprentice,
                new GridPosition(0, 1),
                DragonBoundComponentIds.RuneGrimoire,
                new GridPosition(1, 1)));
        }

        [Test]
        public void RecipeResultDoesNotDependOnPlacementOrder()
        {
            Assert.IsTrue(HeroSliceCatalog.TryGetRecipeDefinitionAtFormation(
                DragonBoundComponentIds.SkyRanger,
                new GridPosition(0, 2),
                DragonBoundComponentIds.DragonSigil,
                new GridPosition(0, 1),
                out var first));
            Assert.IsTrue(HeroSliceCatalog.TryGetRecipeDefinitionAtFormation(
                DragonBoundComponentIds.DragonSigil,
                new GridPosition(0, 1),
                DragonBoundComponentIds.SkyRanger,
                new GridPosition(0, 2),
                out var second));
            Assert.AreEqual(first.HeroId, second.HeroId);
        }
    }
}
