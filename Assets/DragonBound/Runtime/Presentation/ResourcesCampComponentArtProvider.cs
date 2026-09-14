using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Grid;
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
        private static readonly IReadOnlyDictionary<Grid.BasicUnitArchetype, string> BasicUnitResourcePaths =
            new Dictionary<Grid.BasicUnitArchetype, string>
            {
                { Grid.BasicUnitArchetype.Axe, "Hero/UnitUI/AXE00" },
                { Grid.BasicUnitArchetype .Berserker , "Hero/UnitUI/BERSERKER00" },
                { Grid.BasicUnitArchetype.Bow, "Hero/UnitUI/BOW00" },
                { Grid.BasicUnitArchetype.Spear, "Hero/UnitUI/SPEAR00" }
            };

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

        private static readonly IReadOnlyDictionary<string, string> HeroResourcePaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { DragonBoundHeroIds.WindclawRanger, "Hero/Windclaw Ranger/Normal/风爪游侠普攻_00000" },
                { DragonBoundHeroIds.EmberShaman, "Hero/Ember Shaman/余烬萨满普攻_00000" },
                { DragonBoundHeroIds.DragonRider, "Hero/Flame Drake Rider/Normal/烈焰龍騎普攻_00000" },
                { DragonBoundHeroIds.RuneboltMage, "Hero/Runebolt Mage/符文雷矢法师普攻_00000" },
                { DragonBoundHeroIds.Stonebinder, "Hero/Stonebound Warlock/岩缚法师普攻_00000" },
                { DragonBoundHeroIds.StarfallArchmage, "Hero/Starfall Archmage/星陨法师普攻_00000" },
                { DragonBoundHeroIds.CrownSwordLeader, "Hero/Oathcrown Blademaster/誓冠剑士_00000" },
                { DragonBoundHeroIds.CrownHunterLeader, "Hero/Frostcrown Hunter/霜冠猎手普攻_00000" },
                { DragonBoundHeroIds.ThunderJarl, "Hero/Thunderlord/雷霆领主普攻_00000" },
                { DragonBoundHeroIds.NightfangAssassin, "Hero/Nightfang Assassin/夜牙刺客普攻_00000" },
                { DragonBoundHeroIds.LeviathanHunter, "Hero/Abyssal Harpooner/深渊鱼叉手普攻_00000" },
                { DragonBoundHeroIds.SkyhunterValkyrie, "Hero/Skyborne Valkyrie/天穹女武神普攻_00000" }
            };

        private readonly Dictionary<string, Sprite> loadedSprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> reportedMissingPaths =
            new HashSet<string>(StringComparer.Ordinal);

        public static ResourcesCampComponentArtProvider Shared { get; } =
            new ResourcesCampComponentArtProvider();

        public int ComponentMappingCount => ComponentResourcePaths.Count;
        public int HeroMappingCount => HeroResourcePaths.Count;

        public bool TryGetBasicUnitSprite(string unitId, out Sprite sprite)
        {
            Grid.BasicUnitArchetype archetype;
            try
            {
                archetype = BasicUnitCatalog.GetArchetype(unitId);
            }
            catch (ArgumentException)
            {
                sprite = null;
                return false;
            }

            if (!BasicUnitResourcePaths.TryGetValue(archetype, out var resourcePath))
            {
                sprite = null;
                return false;
            }

            return TryLoadSprite("basic:" + archetype, resourcePath, "basic-unit", out sprite);
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

            return TryLoadSprite(componentId, resourcePath, "component", out sprite);
        }

        public bool TryGetHeroSprite(string heroId, out Sprite sprite)
        {
            heroId = DragonBoundLegacyAliases.ResolveHeroId(heroId);
            if (string.IsNullOrWhiteSpace(heroId) ||
                !HeroResourcePaths.TryGetValue(heroId, out var resourcePath))
            {
                sprite = null;
                return false;
            }

            return TryLoadSprite("hero:" + heroId, resourcePath, "hero", out sprite);
        }

        private bool TryLoadSprite(
            string cacheKey,
            string resourcePath,
            string artType,
            out Sprite sprite)
        {
            if (loadedSprites.TryGetValue(cacheKey, out sprite) && sprite != null)
            {
                return true;
            }

            sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                if (reportedMissingPaths.Add(resourcePath))
                {
                    Debug.LogWarning(
                        "Camp " + artType + " art is missing at Resources/" + resourcePath +
                        ". The authored placeholder will remain visible.");
                }
                return false;
            }

            loadedSprites[cacheKey] = sprite;
            return true;
        }
    }
}
