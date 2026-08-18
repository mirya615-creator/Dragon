using System;
using System.Collections.Generic;
using System.Linq;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class FrozenHeroConfigurationTests
    {
        [Test]
        public void FrozenConfigurationHas18FormalComponentsAnd24Instances()
        {
            var configuration = FrozenHeroConfigurationCatalog.Configuration;
            Assert.AreEqual(18, configuration.Components.Count);
            Assert.AreEqual(24, configuration.BagTemplate.Count);
            Assert.AreEqual(24, configuration.BagTemplate.Select(value => value.InstanceId).Distinct().Count());

            var expectedIds = new[]
            {
                DragonBoundComponentIds.DragonSigil,
                DragonBoundComponentIds.RuneGrimoire,
                DragonBoundComponentIds.WarHorn,
                DragonBoundComponentIds.SkyRanger,
                DragonBoundComponentIds.FlameShaman,
                DragonBoundComponentIds.RuneApprentice,
                DragonBoundComponentIds.StoneScholar,
                DragonBoundComponentIds.WanderingSword,
                DragonBoundComponentIds.NorthwatchScout,
                DragonBoundComponentIds.DragonKnight,
                DragonBoundComponentIds.MeteorCore,
                DragonBoundComponentIds.StormCrown,
                DragonBoundComponentIds.ShadowCloak,
                DragonBoundComponentIds.RuneDagger,
                DragonBoundComponentIds.LeviathanEye,
                DragonBoundComponentIds.AncientHarpoon,
                DragonBoundComponentIds.ValkyrieWings,
                DragonBoundComponentIds.DragonboneBow
            };
            CollectionAssert.AreEquivalent(expectedIds, configuration.Components.Select(value => value.Id));

            foreach (var component in configuration.Components)
            {
                var expectedCopies = component.Category == HeroComponentCategory.PublicCore ? 3 : 1;
                Assert.AreEqual(expectedCopies, component.CopiesPerRun, component.Id);
                Assert.AreEqual(
                    expectedCopies,
                    configuration.BagTemplate.Count(value => value.ComponentId == component.Id),
                    component.Id);
            }
        }

        [Test]
        public void FrozenConfigurationHas12FormalRecipesHeroesAndSkills()
        {
            var configuration = FrozenHeroConfigurationCatalog.Configuration;
            Assert.AreEqual(12, configuration.Recipes.Count);
            Assert.AreEqual(12, configuration.Heroes.Count);
            Assert.AreEqual(12, configuration.Skills.Count);

            var expectedHeroIds = new[]
            {
                DragonBoundHeroIds.WindclawRanger,
                DragonBoundHeroIds.EmberShaman,
                DragonBoundHeroIds.DragonRider,
                DragonBoundHeroIds.RuneboltMage,
                DragonBoundHeroIds.Stonebinder,
                DragonBoundHeroIds.StarfallArchmage,
                DragonBoundHeroIds.HornbladeDuelist,
                DragonBoundHeroIds.NorthwatchHunter,
                DragonBoundHeroIds.ThunderJarl,
                DragonBoundHeroIds.NightfangAssassin,
                DragonBoundHeroIds.LeviathanHunter,
                DragonBoundHeroIds.SkyhunterValkyrie
            };
            CollectionAssert.AreEquivalent(expectedHeroIds, configuration.Recipes.Select(value => value.HeroId));
            CollectionAssert.AreEquivalent(expectedHeroIds, configuration.Heroes.Select(value => value.Id));
            Assert.IsTrue(configuration.Heroes.All(hero =>
                configuration.Skills.Any(skill => skill.SkillId == hero.SkillId)));
        }

        [Test]
        public void FrozenRecipesUseAllTwelveConfiguredDirections()
        {
            AssertFormation(DragonBoundHeroIds.WindclawRanger, HeroFormationOrientation.Vertical,
                DragonBoundComponentIds.SkyRanger, DragonBoundComponentIds.DragonSigil);
            AssertFormation(DragonBoundHeroIds.EmberShaman, HeroFormationOrientation.Vertical,
                DragonBoundComponentIds.FlameShaman, DragonBoundComponentIds.DragonSigil);
            AssertFormation(DragonBoundHeroIds.DragonRider, HeroFormationOrientation.Vertical,
                DragonBoundComponentIds.DragonKnight, DragonBoundComponentIds.DragonSigil);
            AssertFormation(DragonBoundHeroIds.HornbladeDuelist, HeroFormationOrientation.Vertical,
                DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.WanderingSword);
            AssertFormation(DragonBoundHeroIds.NorthwatchHunter, HeroFormationOrientation.Vertical,
                DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.NorthwatchScout);
            AssertFormation(DragonBoundHeroIds.ThunderJarl, HeroFormationOrientation.Vertical,
                DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.StormCrown);
            AssertFormation(DragonBoundHeroIds.RuneboltMage, HeroFormationOrientation.Horizontal,
                DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.RuneApprentice);
            AssertFormation(DragonBoundHeroIds.Stonebinder, HeroFormationOrientation.Horizontal,
                DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.StoneScholar);
            AssertFormation(DragonBoundHeroIds.StarfallArchmage, HeroFormationOrientation.Horizontal,
                DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.MeteorCore);
            AssertFormation(DragonBoundHeroIds.NightfangAssassin, HeroFormationOrientation.Horizontal,
                DragonBoundComponentIds.RuneDagger, DragonBoundComponentIds.ShadowCloak);
            AssertFormation(DragonBoundHeroIds.LeviathanHunter, HeroFormationOrientation.Horizontal,
                DragonBoundComponentIds.AncientHarpoon, DragonBoundComponentIds.LeviathanEye);
            AssertFormation(DragonBoundHeroIds.SkyhunterValkyrie, HeroFormationOrientation.Horizontal,
                DragonBoundComponentIds.DragonboneBow, DragonBoundComponentIds.ValkyrieWings);
        }

        [Test]
        public void DragonRecipeRequiresPersonAboveDragon()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.WindclawRanger);
            Assert.IsTrue(recipe.MatchesFormation(
                DragonBoundComponentIds.SkyRanger, new GridPosition(1, 2),
                DragonBoundComponentIds.DragonSigil, new GridPosition(1, 1)));
        }

        [Test]
        public void DragonRecipeRejectsDragonAbovePerson()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.WindclawRanger);
            Assert.IsFalse(recipe.MatchesFormation(
                DragonBoundComponentIds.DragonSigil, new GridPosition(1, 2),
                DragonBoundComponentIds.SkyRanger, new GridPosition(1, 1)));
        }

        [Test]
        public void DragonRecipeRejectsHorizontalLayout()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.WindclawRanger);
            Assert.IsFalse(recipe.MatchesFormation(
                DragonBoundComponentIds.SkyRanger, new GridPosition(1, 1),
                DragonBoundComponentIds.DragonSigil, new GridPosition(2, 1)));
        }

        [Test]
        public void CrownRecipeRequiresCrownAbovePerson()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.HornbladeDuelist);
            Assert.IsTrue(recipe.MatchesFormation(
                DragonBoundComponentIds.WarHorn, new GridPosition(1, 2),
                DragonBoundComponentIds.WanderingSword, new GridPosition(1, 1)));
        }

        [Test]
        public void CrownRecipeRejectsPersonAboveCrown()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.HornbladeDuelist);
            Assert.IsFalse(recipe.MatchesFormation(
                DragonBoundComponentIds.WanderingSword, new GridPosition(1, 2),
                DragonBoundComponentIds.WarHorn, new GridPosition(1, 1)));
        }

        [Test]
        public void WeaponRecipeRequiresWeaponLeftPersonRight()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.RuneboltMage);
            Assert.IsTrue(recipe.MatchesFormation(
                DragonBoundComponentIds.RuneGrimoire, new GridPosition(1, 1),
                DragonBoundComponentIds.RuneApprentice, new GridPosition(2, 1)));
        }

        [Test]
        public void WeaponRecipeRejectsPersonLeftWeaponRight()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.RuneboltMage);
            Assert.IsFalse(recipe.MatchesFormation(
                DragonBoundComponentIds.RuneApprentice, new GridPosition(1, 1),
                DragonBoundComponentIds.RuneGrimoire, new GridPosition(2, 1)));
        }

        [Test]
        public void WeaponRecipeRejectsVerticalLayout()
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(DragonBoundHeroIds.RuneboltMage);
            Assert.IsFalse(recipe.MatchesFormation(
                DragonBoundComponentIds.RuneGrimoire, new GridPosition(1, 1),
                DragonBoundComponentIds.RuneApprentice, new GridPosition(1, 2)));
        }

        [Test]
        public void PublicCoreRecipesUseConfiguredUniquePartnerAsProgressOwner()
        {
            var configuration = FrozenHeroConfigurationCatalog.Configuration;
            var components = configuration.Components.ToDictionary(value => value.Id);
            foreach (var recipe in configuration.Recipes.Take(9))
            {
                var componentA = components[recipe.ComponentAId];
                var componentB = components[recipe.ComponentBId];
                var expectedOwner = componentA.Category == HeroComponentCategory.PublicCore
                    ? componentB.Id
                    : componentA.Id;
                Assert.AreEqual(expectedOwner, recipe.ProgressOwnerComponentId, recipe.HeroId);
            }

            Assert.IsTrue(configuration.Recipes.Skip(9).All(recipe =>
                !string.IsNullOrEmpty(recipe.ProgressOwnerComponentId)));
        }

        [Test]
        public void ValidatorAcceptsFrozenFrameworkWithDedicatedGoldProgressOwners()
        {
            var normal = FrozenHeroConfigurationValidator.Validate(
                FrozenHeroConfigurationCatalog.Configuration,
                false);
            Assert.AreEqual(0, normal.Count(value => value.Severity == ConfigurationValidationSeverity.Error));
            Assert.AreEqual(
                0,
                normal.Count(value => value.Code == "DedicatedGoldProgressOwnerPending" &&
                                      value.Severity == ConfigurationValidationSeverity.Warning));

            var runtimeReady = FrozenHeroConfigurationValidator.Validate(
                FrozenHeroConfigurationCatalog.Configuration,
                true);
            Assert.AreEqual(
                0,
                runtimeReady.Count(value => value.Severity == ConfigurationValidationSeverity.Error));
        }

        [Test]
        public void HeroSliceMainKeepsOnlyCurrentEnabledRange()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                     DragonBoundComponentIds.DragonSigil,
                     DragonBoundComponentIds.SkyRanger,
                     DragonBoundComponentIds.FlameShaman,
                     DragonBoundComponentIds.DragonKnight
                },
                HeroSliceRecruitmentConfig.Components.Select(value => value.Id));
            CollectionAssert.AreEquivalent(
                new[]
                {
                     DragonBoundHeroIds.WindclawRanger,
                     DragonBoundHeroIds.EmberShaman,
                     DragonBoundHeroIds.DragonRider
                },
                HeroSliceRecruitmentConfig.Recipes.Select(value => value.HeroId));

            var deck = new RecruitDeck(
                GreyboxRecruitmentCatalog.Create(),
                new RunRandom(20260804),
                "slice.scope",
                true,
                true);
            var componentIds = new List<string>();
            for (var recruit = 0; recruit < 3; recruit++)
            {
                componentIds.AddRange(deck.DrawNext().Cards
                    .Where(card => card.Kind == RecruitItemKind.HeroComponent)
                    .Select(card => card.ConfigId));
            }

            CollectionAssert.AreEquivalent(
                new[]
                {
                    DragonBoundComponentIds.DragonSigil,
                    DragonBoundComponentIds.DragonSigil,
                    DragonBoundComponentIds.SkyRanger,
                    DragonBoundComponentIds.DragonKnight
                },
                componentIds);
            Assert.AreEqual(0, deck.RemainingHeroComponents);
        }

        [Test]
        public void FrozenGrowthAndControlRulesMatchVersionOne()
        {
            var purple = FrozenHeroConfigurationCatalog.GetHero(DragonBoundHeroIds.WindclawRanger);
            AssertGrowth(
                purple,
                new[] { 0, 20, 60 },
                new[] { 1f, 1.05f, 1.10f },
                new[] { 1f, 1.25f, 1.56f },
                new[] { 1f, 1.10f, 1.25f });

            var gold = FrozenHeroConfigurationCatalog.GetHero(DragonBoundHeroIds.DragonRider);
            AssertGrowth(
                gold,
                new[] { 0, 20, 55, 105, 175 },
                new[] { 1f, 1.12f, 1.25f, 1.40f, 1.57f },
                new[] { 1f, 1.10f, 1.21f, 1.33f, 1.46f },
                new[] { 1f, 1.10f, 1.25f, 1.45f, 1.70f });

            var rules = FrozenHeroConfigurationCatalog.Configuration.ControlRules;
            Assert.AreEqual(1f, rules.NormalStunMultiplier);
            Assert.AreEqual(0.60f, rules.EliteStunMultiplier);
            Assert.AreEqual(0.20f, rules.BossStunMultiplier);
            Assert.AreEqual(2f, rules.BossPostStunImmunitySeconds);
        }

        [Test]
        public void FullDeckMeetsFrozenFirstEightRecruitmentTargets()
        {
            for (var seed = 1; seed <= 24; seed++)
            {
                var catalog = GreyboxRecruitmentCatalog.Create();
                var deck = new RecruitDeck(catalog, new RunRandom(seed), $"full.{seed}", true, false);
                var throughTwo = new HashSet<string>(StringComparer.Ordinal);
                var throughFour = new HashSet<string>(StringComparer.Ordinal);
                var throughSix = new HashSet<string>(StringComparer.Ordinal);
                var throughEight = new List<RecruitCard>();

                for (var recruitment = 1; recruitment <= 8; recruitment++)
                {
                    var components = deck.DrawNext().Cards
                        .Where(card => card.Kind == RecruitItemKind.HeroComponent)
                        .ToArray();
                    throughEight.AddRange(components);
                    foreach (var card in components)
                    {
                        if (recruitment <= 2) throughTwo.Add(card.ConfigId);
                        if (recruitment <= 4) throughFour.Add(card.ConfigId);
                        if (recruitment <= 6) throughSix.Add(card.ConfigId);
                    }
                }

                Assert.IsTrue(HasCompleteRecipe(catalog, HeroRecipeRarity.Purple, throughTwo), $"seed={seed}");
                Assert.IsTrue(throughFour.Any(id => catalog.GetComponent(id).Pool == HeroComponentPool.Gold), $"seed={seed}");
                Assert.IsTrue(HasCompleteRecipe(catalog, HeroRecipeRarity.Gold, throughSix), $"seed={seed}");
                Assert.AreEqual(24, throughEight.Count, $"seed={seed}");
                Assert.AreEqual(24, throughEight.Select(card => card.SourceInstanceId).Distinct().Count(), $"seed={seed}");
                Assert.AreEqual(0, deck.RemainingHeroComponents, $"seed={seed}");
            }
        }

        [Test]
        public void FormalDefinitionsContainNoTemporaryIds()
        {
            var configuration = FrozenHeroConfigurationCatalog.Configuration;
            Assert.IsTrue(configuration.Components.All(value => value.Id.StartsWith("CMP_", StringComparison.Ordinal)));
            Assert.IsTrue(configuration.Heroes.All(value => value.Id.StartsWith("HERO_", StringComparison.Ordinal)));
            Assert.IsFalse(configuration.Components.Any(value => value.Id.Contains("PLACEHOLDER")));
            Assert.IsFalse(configuration.Heroes.Any(value => value.Id.Contains("PLACEHOLDER")));
        }

        private static void AssertGrowth(
            HeroDefinition hero,
            int[] experience,
            float[] attack,
            float[] attackSpeed,
            float[] skill)
        {
            Assert.AreEqual(experience.Length, hero.MaxLevel);
            for (var index = 0; index < experience.Length; index++)
            {
                var stats = hero.GetLevelStats(index + 1);
                Assert.AreEqual(experience[index], stats.RequiredExperience);
                Assert.AreEqual(attack[index], stats.AttackMultiplier, 0.0001f);
                Assert.AreEqual(attackSpeed[index], stats.AttackSpeedMultiplier, 0.0001f);
                Assert.AreEqual(skill[index], stats.SkillMultiplier, 0.0001f);
            }
        }

        private static void AssertFormation(
            string heroId,
            HeroFormationOrientation orientation,
            string firstComponentId,
            string secondComponentId)
        {
            var recipe = FrozenHeroConfigurationCatalog.GetRecipe(heroId);
            Assert.AreEqual(orientation, recipe.FormationOrientation, heroId);
            if (orientation == HeroFormationOrientation.Vertical)
            {
                Assert.AreEqual(firstComponentId, recipe.TopComponentId, heroId);
                Assert.AreEqual(secondComponentId, recipe.BottomComponentId, heroId);
                Assert.IsTrue(recipe.MatchesFormation(
                    secondComponentId, new GridPosition(2, 1),
                    firstComponentId, new GridPosition(2, 2)), heroId);
                return;
            }

            Assert.AreEqual(firstComponentId, recipe.LeftComponentId, heroId);
            Assert.AreEqual(secondComponentId, recipe.RightComponentId, heroId);
            Assert.IsTrue(recipe.MatchesFormation(
                secondComponentId, new GridPosition(2, 1),
                firstComponentId, new GridPosition(1, 1)), heroId);
        }

        private static bool HasCompleteRecipe(
            RecruitmentCatalog catalog,
            HeroRecipeRarity rarity,
            ISet<string> componentIds)
        {
            return catalog.Recipes.Any(recipe =>
                recipe.Rarity == rarity &&
                componentIds.Contains(recipe.ComponentAId) &&
                componentIds.Contains(recipe.ComponentBId));
        }
    }
}
