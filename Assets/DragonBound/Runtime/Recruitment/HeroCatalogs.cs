using System;
using System.Collections.Generic;
using DragonBound.Grid;

namespace DragonBound.Recruitment
{
    // These facades expose the single frozen configuration without creating parallel UI or matcher tables.
    public static class HeroComponentCatalog
    {
        private static readonly IReadOnlyList<HeroComponentRunManifest> runManifest = BuildRunManifest();

        public static IReadOnlyList<HeroComponentDefinition> Definitions =>
            FrozenHeroConfigurationCatalog.Configuration.Components;

        public static IReadOnlyList<HeroComponentRunManifest> RunManifest => runManifest;

        public static HeroComponentDefinition Get(string componentId) =>
            FrozenHeroConfigurationCatalog.GetComponent(componentId);

        public static bool TryGet(string componentId, out HeroComponentDefinition component)
        {
            try
            {
                component = Get(componentId);
                return true;
            }
            catch (KeyNotFoundException)
            {
                component = null;
                return false;
            }
        }

        private static IReadOnlyList<HeroComponentRunManifest> BuildRunManifest()
        {
            var entries = new List<HeroComponentRunManifest>();
            foreach (var component in Definitions)
            {
                entries.Add(new HeroComponentRunManifest(component.Id, component.CopiesPerRun));
            }

            return entries.AsReadOnly();
        }
    }

    public static class HeroDefinitionCatalog
    {
        public static IReadOnlyList<DragonBound.Combat.HeroDefinition> Definitions =>
            FrozenHeroConfigurationCatalog.Configuration.Heroes;

        public static DragonBound.Combat.HeroDefinition Get(string heroId) =>
            FrozenHeroConfigurationCatalog.GetHero(heroId);

        public static HeroCatalogMetadata GetMetadata(string heroId) =>
            FrozenHeroConfigurationCatalog.GetHeroMetadata(heroId);
    }

    public static class HeroRecipeCatalog
    {
        public static IReadOnlyList<HeroRecipeDefinition> Definitions =>
            FrozenHeroConfigurationCatalog.Configuration.Recipes;

        public static HeroRecipeDefinition Get(string recipeOrHeroId) =>
            FrozenHeroConfigurationCatalog.GetRecipe(recipeOrHeroId);

        public static bool TryGetAtFormation(
            string firstComponentId,
            GridPosition firstPosition,
            string secondComponentId,
            GridPosition secondPosition,
            out HeroRecipeDefinition recipe)
        {
            firstComponentId = DragonBoundLegacyAliases.ResolveComponentId(firstComponentId);
            secondComponentId = DragonBoundLegacyAliases.ResolveComponentId(secondComponentId);
            foreach (var candidate in Definitions)
            {
                if (candidate.MatchesFormation(
                        firstComponentId,
                        firstPosition,
                        secondComponentId,
                        secondPosition))
                {
                    recipe = candidate;
                    return true;
                }
            }

            recipe = null;
            return false;
        }
    }

    public sealed class HeroComponentRunManifest
    {
        public HeroComponentRunManifest(string componentId, int copiesPerRun)
        {
            if (string.IsNullOrWhiteSpace(componentId) || copiesPerRun < 1)
            {
                throw new ArgumentException("A component run manifest requires an id and positive copy count.");
            }

            ComponentId = componentId;
            CopiesPerRun = copiesPerRun;
        }

        public string ComponentId { get; }
        public int CopiesPerRun { get; }
    }
}
