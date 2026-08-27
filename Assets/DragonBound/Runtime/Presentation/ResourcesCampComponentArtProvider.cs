using System;
using System.Collections.Generic;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Default frontend component-art source. Formal component ids stay stable while
    /// the numeric filenames remain an implementation detail of Resources/ComponentUI.
    /// </summary>
    public sealed class ResourcesCampComponentArtProvider : ICampArtProvider
    {
        private static readonly IReadOnlyDictionary<string, string> ComponentResourcePaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { DragonBoundComponentIds.RuneStaff, "ComponentUI/01" },
                { DragonBoundComponentIds.StoneScholar, "ComponentUI/001" },
                { DragonBoundComponentIds.RuneApprentice, "ComponentUI/002" },
                { DragonBoundComponentIds.AstralMage, "ComponentUI/003" },
                { DragonBoundComponentIds.ContractHatchling, "ComponentUI/02" },
                { DragonBoundComponentIds.SkyRanger, "ComponentUI/004" },
                { DragonBoundComponentIds.FlameShaman, "ComponentUI/005" },
                { DragonBoundComponentIds.DragonKnight, "ComponentUI/006" },
                { DragonBoundComponentIds.AncestralWarCrown, "ComponentUI/03" },
                { DragonBoundComponentIds.NorthlandScout, "ComponentUI/007" },
                { DragonBoundComponentIds.WanderingSwordsman, "ComponentUI/008" },
                { DragonBoundComponentIds.StormWarrior, "ComponentUI/009" },
                { DragonBoundComponentIds.RuneDagger, "ComponentUI/010" },
                { DragonBoundComponentIds.ShadowWalker, "ComponentUI/011" },
                { DragonBoundComponentIds.ValkyrieAcolyte, "ComponentUI/012" },
                { DragonBoundComponentIds.DragonboneLongbow, "ComponentUI/043" },
                { DragonBoundComponentIds.AncientHarpoon, "ComponentUI/014" },
                { DragonBoundComponentIds.DeepseaHarpooner, "ComponentUI/015" }
            };

        private readonly Dictionary<string, Sprite> loadedSprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> reportedMissingPaths =
            new HashSet<string>(StringComparer.Ordinal);

        public static ResourcesCampComponentArtProvider Shared { get; } =
            new ResourcesCampComponentArtProvider();

        public int ComponentMappingCount => ComponentResourcePaths.Count;

        public bool TryGetBasicUnitSprite(string unitId, out Sprite sprite)
        {
            sprite = null;
            return false;
        }

        public bool TryGetHeroComponentSprite(string componentId, out Sprite sprite)
        {
            componentId = DragonBoundLegacyAliases.ResolveComponentId(componentId);
            if (string.IsNullOrWhiteSpace(componentId) ||
                !ComponentResourcePaths.TryGetValue(componentId, out var resourcePath))
            {
                sprite = null;
                return false;
            }

            if (loadedSprites.TryGetValue(componentId, out sprite) && sprite != null)
            {
                return true;
            }

            sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                if (reportedMissingPaths.Add(resourcePath))
                {
                    Debug.LogWarning(
                        "Camp component art is missing at Resources/" + resourcePath +
                        ". The authored placeholder will remain visible.");
                }
                return false;
            }

            loadedSprites[componentId] = sprite;
            return true;
        }

        public bool TryGetHeroSprite(string heroId, out Sprite sprite)
        {
            sprite = null;
            return false;
        }
    }
}
