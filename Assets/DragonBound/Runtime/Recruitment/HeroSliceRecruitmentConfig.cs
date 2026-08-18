using System;
using System.Collections.Generic;

namespace DragonBound.Recruitment
{
    public static class HeroSliceRecruitmentConfig
    {
        public const string DragonSigilId = DragonBoundComponentIds.DragonSigil;
        public const string SkyRangerId = DragonBoundComponentIds.SkyRanger;
        public const string FlameShamanId = DragonBoundComponentIds.FlameShaman;
        public const string DragonKnightId = DragonBoundComponentIds.DragonKnight;
        public const string WindclawRangerId = DragonBoundHeroIds.WindclawRanger;
        public const string EmberShamanId = DragonBoundHeroIds.EmberShaman;
        public const string DragonRiderId = DragonBoundHeroIds.DragonRider;

        private static readonly IReadOnlyList<HeroComponentDefinition> components =
            Array.AsReadOnly(new[]
            {
                HeroComponentCatalog.Get(DragonSigilId),
                HeroComponentCatalog.Get(SkyRangerId),
                HeroComponentCatalog.Get(FlameShamanId),
                HeroComponentCatalog.Get(DragonKnightId)
            });

        private static readonly IReadOnlyList<HeroRecipeDefinition> recipes =
            Array.AsReadOnly(new[]
            {
                HeroRecipeCatalog.Get(WindclawRangerId),
                HeroRecipeCatalog.Get(EmberShamanId),
                HeroRecipeCatalog.Get(DragonRiderId)
            });

        public static IReadOnlyList<HeroComponentDefinition> Components => components;
        public static IReadOnlyList<HeroRecipeDefinition> Recipes => recipes;

        public static bool TryGetComponent(string configId, out HeroComponentDefinition component)
        {
            foreach (var candidate in components)
            {
                if (string.Equals(candidate.Id, configId, StringComparison.Ordinal))
                {
                    component = candidate;
                    return true;
                }
            }

            component = null;
            return false;
        }

        public static HeroComponentDefinition GetComponent(string configId)
        {
            if (!TryGetComponent(configId, out var component))
            {
                throw new KeyNotFoundException($"Unknown hero slice component {configId}.");
            }

            return component;
        }
    }
}
