using System;
using System.Collections.Generic;
using DragonBound.Combat;

namespace DragonBound.Recruitment
{
    public static class DragonBoundComponentIds
    {
        public const string ContractHatchling = "CMP_CONTRACT_HATCHLING";
        public const string RuneStaff = "CMP_RUNE_STAFF";
        public const string AncestralWarCrown = "CMP_ANCESTRAL_WAR_CROWN";
        public const string SkyRanger = "CMP_SKY_RANGER";
        public const string FlameShaman = "CMP_FLAME_SHAMAN";
        public const string DragonKnight = "CMP_DRAGON_KNIGHT";
        public const string RuneApprentice = "CMP_RUNE_APPRENTICE";
        public const string StoneScholar = "CMP_STONE_SCHOLAR";
        public const string AstralMage = "CMP_ASTRAL_MAGE";
        public const string WanderingSwordsman = "CMP_WANDERING_SWORDSMAN";
        public const string NorthlandScout = "CMP_NORTHLAND_SCOUT";
        public const string StormWarrior = "CMP_STORM_WARRIOR";
        public const string ShadowWalker = "CMP_SHADOW_WALKER";
        public const string RuneDagger = "CMP_RUNE_DAGGER";
        public const string DeepseaHarpooner = "CMP_DEEPSEA_HARPOONER";
        public const string AncientHarpoon = "CMP_ANCIENT_HARPOON";
        public const string ValkyrieAcolyte = "CMP_VALKYRIE_ACOLYTE";
        public const string DragonboneLongbow = "CMP_DRAGONBONE_LONGBOW";

        // Source compatibility only. New data and runtime matching must use canonical names above.
        public const string DragonSigil = ContractHatchling;
        public const string RuneGrimoire = RuneStaff;
        public const string WarHorn = AncestralWarCrown;
        public const string MeteorCore = AstralMage;
        public const string StormCrown = StormWarrior;
        public const string ShadowCloak = ShadowWalker;
        public const string LeviathanEye = DeepseaHarpooner;
        public const string ValkyrieWings = ValkyrieAcolyte;
        public const string WanderingSword = WanderingSwordsman;
        public const string NorthwatchScout = NorthlandScout;
        public const string DragonboneBow = DragonboneLongbow;
    }

    public static class DragonBoundHeroIds
    {
        public const string WindclawRanger = "HERO_WINDCLAW_RANGER";
        public const string EmberShaman = "HERO_EMBER_SHAMAN";
        public const string DragonRider = "HERO_DRAGON_RIDER";
        public const string RuneboltMage = "HERO_RUNEBOLT_MAGE";
        public const string Stonebinder = "HERO_STONEBINDER";
        public const string StarfallArchmage = "HERO_STARFALL_ARCHMAGE";
        public const string CrownSwordLeader = "HERO_CROWN_SWORD_LEADER";
        public const string CrownHunterLeader = "HERO_CROWN_HUNTER_LEADER";
        public const string ThunderJarl = "HERO_THUNDER_JARL";
        public const string NightfangAssassin = "HERO_NIGHTFANG_ASSASSIN";
        public const string LeviathanHunter = "HERO_LEVIATHAN_HUNTER";
        public const string SkyhunterValkyrie = "HERO_SKYHUNTER_VALKYRIE";

        // Legacy source symbols resolve to the neutral frozen hero identities.
        public const string HornbladeDuelist = CrownSwordLeader;
        public const string NorthwatchHunter = CrownHunterLeader;
    }

    public static class DragonBoundRecipeIds
    {
        public const string WindclawRanger = "RECIPE_WINDCLAW_RANGER";
        public const string EmberShaman = "RECIPE_EMBER_SHAMAN";
        public const string DragonRider = "RECIPE_DRAGON_RIDER";
        public const string RuneboltMage = "RECIPE_RUNEBOLT_MAGE";
        public const string Stonebinder = "RECIPE_STONEBINDER";
        public const string StarfallArchmage = "RECIPE_STARFALL_ARCHMAGE";
        public const string CrownSwordLeader = "RECIPE_CROWN_SWORD_LEADER";
        public const string CrownHunterLeader = "RECIPE_CROWN_HUNTER_LEADER";
        public const string ThunderJarl = "RECIPE_THUNDER_JARL";
        public const string NightfangAssassin = "RECIPE_NIGHTFANG_ASSASSIN";
        public const string LeviathanHunter = "RECIPE_LEVIATHAN_HUNTER";
        public const string SkyhunterValkyrie = "RECIPE_SKYHUNTER_VALKYRIE";
    }

    public static class DragonBoundLegacyAliases
    {
        private static readonly IReadOnlyDictionary<string, string> componentIds =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "CORE_DRAGON_SIGIL", DragonBoundComponentIds.ContractHatchling },
                { "CORE_RUNE_GRIMOIRE", DragonBoundComponentIds.RuneStaff },
                { "CORE_WAR_HORN", DragonBoundComponentIds.AncestralWarCrown },
                { "PART_SKY_RANGER", DragonBoundComponentIds.SkyRanger },
                { "PART_FLAME_SHAMAN", DragonBoundComponentIds.FlameShaman },
                { "PART_RUNE_APPRENTICE", DragonBoundComponentIds.RuneApprentice },
                { "PART_STONE_SCHOLAR", DragonBoundComponentIds.StoneScholar },
                { "PART_METEOR_CORE", DragonBoundComponentIds.AstralMage },
                { "PART_STORM_CROWN", DragonBoundComponentIds.StormWarrior },
                { "PART_SHADOW_CLOAK", DragonBoundComponentIds.ShadowWalker },
                { "PART_LEVIATHAN_EYE", DragonBoundComponentIds.DeepseaHarpooner },
                { "PART_VALKYRIE_WINGS", DragonBoundComponentIds.ValkyrieAcolyte },
                { "PART_WANDERING_SWORD", DragonBoundComponentIds.WanderingSwordsman },
                { "PART_NORTHWATCH_SCOUT", DragonBoundComponentIds.NorthlandScout },
                { "PART_DRAGON_KNIGHT", DragonBoundComponentIds.DragonKnight },
                { "PART_RUNE_DAGGER", DragonBoundComponentIds.RuneDagger },
                { "PART_ANCIENT_HARPOON", DragonBoundComponentIds.AncientHarpoon },
                { "PART_DRAGONBONE_BOW", DragonBoundComponentIds.DragonboneLongbow }
            };

        private static readonly IReadOnlyDictionary<string, string> heroIds =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "HERO_HORNBLADE_DUELIST", DragonBoundHeroIds.CrownSwordLeader },
                { "HERO_NORTHWATCH_HUNTER", DragonBoundHeroIds.CrownHunterLeader }
            };

        public static string ResolveComponentId(string id) => Resolve(id, componentIds);
        public static string ResolveHeroId(string id) => Resolve(id, heroIds);

        public static IReadOnlyList<string> GetComponentAliases(string canonicalId)
        {
            return GetAliases(canonicalId, componentIds);
        }

        public static IReadOnlyList<string> GetHeroAliases(string canonicalId)
        {
            return GetAliases(canonicalId, heroIds);
        }

        private static string Resolve(string id, IReadOnlyDictionary<string, string> aliases)
        {
            return !string.IsNullOrWhiteSpace(id) && aliases.TryGetValue(id, out var canonical)
                ? canonical
                : id;
        }

        private static IReadOnlyList<string> GetAliases(
            string canonicalId,
            IReadOnlyDictionary<string, string> aliases)
        {
            var result = new List<string>();
            foreach (var alias in aliases)
            {
                if (string.Equals(alias.Value, canonicalId, StringComparison.Ordinal))
                {
                    result.Add(alias.Key);
                }
            }

            return result.AsReadOnly();
        }
    }

    public static class DragonBoundSkillIds
    {
        public const string PowerShot = "SKILL_POWER_SHOT";
        public const string ExplosiveFireball = "SKILL_EXPLOSIVE_FIREBALL";
        public const string RuneboltMage = "SKILL_RUNEBOLT_MAGE";
        public const string StoneBind = "SKILL_STONE_BIND";
        public const string DuelMomentum = "SKILL_DUEL_MOMENTUM";
        public const string HuntMark = "SKILL_HUNT_MARK";
        public const string FlameDive = "SKILL_FLAME_DIVE";
        public const string Starfall = "SKILL_STARFALL";
        public const string ThunderDominion = "SKILL_THUNDER_DOMINION";
        public const string NightfangExecution = "SKILL_NIGHTFANG_EXECUTION";
        public const string AbyssHarpoon = "SKILL_ABYSS_HARPOON";
        public const string SkyHunt = "SKILL_SKY_HUNT";
    }

    public sealed class HeroControlRulesDefinition
    {
        public HeroControlRulesDefinition(
            float normalStunMultiplier,
            float eliteStunMultiplier,
            float bossStunMultiplier,
            float bossPostStunImmunitySeconds)
        {
            NormalStunMultiplier = normalStunMultiplier;
            EliteStunMultiplier = eliteStunMultiplier;
            BossStunMultiplier = bossStunMultiplier;
            BossPostStunImmunitySeconds = bossPostStunImmunitySeconds;
        }

        public float NormalStunMultiplier { get; }
        public float EliteStunMultiplier { get; }
        public float BossStunMultiplier { get; }
        public float BossPostStunImmunitySeconds { get; }
    }

    public sealed class HeroRecruitmentMilestones
    {
        public const int PurpleRecipeAvailableByRecruitment = 2;
        public const int FirstPurpleFormableByRecruitment = 3;
        public const int GoldDirectionVisibleByRecruitment = 4;
        public const int GoldRecipeFirstRecruitment = 5;
        public const int GoldRecipeLastRecruitment = 6;
        public const int AllComponentsDeliveredByRecruitment = 8;
    }

    public sealed class FrozenHeroConfiguration
    {
        public FrozenHeroConfiguration(
            IReadOnlyList<HeroComponentDefinition> components,
            IReadOnlyList<HeroRecipeDefinition> recipes,
            IReadOnlyList<HeroDefinition> heroes,
            IReadOnlyList<HeroCatalogMetadata> heroMetadata,
            IReadOnlyList<SkillDefinition> skills,
            IReadOnlyList<HeroComponentInstanceDefinition> bagTemplate,
            HeroControlRulesDefinition controlRules)
        {
            Components = components ?? throw new ArgumentNullException(nameof(components));
            Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            Heroes = heroes ?? throw new ArgumentNullException(nameof(heroes));
            HeroMetadata = heroMetadata ?? throw new ArgumentNullException(nameof(heroMetadata));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            BagTemplate = bagTemplate ?? throw new ArgumentNullException(nameof(bagTemplate));
            ControlRules = controlRules ?? throw new ArgumentNullException(nameof(controlRules));
        }

        public IReadOnlyList<HeroComponentDefinition> Components { get; }
        public IReadOnlyList<HeroRecipeDefinition> Recipes { get; }
        public IReadOnlyList<HeroDefinition> Heroes { get; }
        public IReadOnlyList<HeroCatalogMetadata> HeroMetadata { get; }
        public IReadOnlyList<SkillDefinition> Skills { get; }
        public IReadOnlyList<HeroComponentInstanceDefinition> BagTemplate { get; }
        public HeroControlRulesDefinition ControlRules { get; }
    }

    public enum ConfigurationValidationSeverity
    {
        Warning,
        Error
    }

    public readonly struct ConfigurationValidationIssue
    {
        public ConfigurationValidationIssue(
            ConfigurationValidationSeverity severity,
            string code,
            string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        public ConfigurationValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
    }

    public static class FrozenHeroConfigurationCatalog
    {
        private static readonly FrozenHeroConfiguration configuration = Build();
        private static readonly IReadOnlyList<ConfigurationValidationIssue> validationIssues =
            FrozenHeroConfigurationValidator.Validate(configuration, false);

        static FrozenHeroConfigurationCatalog()
        {
            foreach (var issue in validationIssues)
            {
                if (issue.Severity == ConfigurationValidationSeverity.Error)
                {
                    throw new InvalidOperationException(
                        $"Frozen hero configuration is invalid: {issue.Code} {issue.Message}");
                }
            }
        }

        public static FrozenHeroConfiguration Configuration => configuration;
        public static IReadOnlyList<ConfigurationValidationIssue> ValidationIssues => validationIssues;

        public static HeroComponentDefinition GetComponent(string componentId)
        {
            componentId = DragonBoundLegacyAliases.ResolveComponentId(componentId);
            foreach (var component in configuration.Components)
            {
                if (string.Equals(component.Id, componentId, StringComparison.Ordinal))
                {
                    return component;
                }
            }

            throw new KeyNotFoundException($"Unknown formal component {componentId}.");
        }

        public static HeroRecipeDefinition GetRecipe(string recipeOrHeroId)
        {
            foreach (var recipe in configuration.Recipes)
            {
                if (string.Equals(recipe.RecipeId, recipeOrHeroId, StringComparison.Ordinal) ||
                    string.Equals(recipe.HeroId, DragonBoundLegacyAliases.ResolveHeroId(recipeOrHeroId), StringComparison.Ordinal))
                {
                    return recipe;
                }
            }

            throw new KeyNotFoundException($"Unknown formal recipe {recipeOrHeroId}.");
        }

        public static HeroDefinition GetHero(string heroId)
        {
            heroId = DragonBoundLegacyAliases.ResolveHeroId(heroId);
            foreach (var hero in configuration.Heroes)
            {
                if (string.Equals(hero.Id, heroId, StringComparison.Ordinal))
                {
                    return hero;
                }
            }

            throw new KeyNotFoundException($"Unknown formal hero {heroId}.");
        }

        public static HeroCatalogMetadata GetHeroMetadata(string heroId)
        {
            heroId = DragonBoundLegacyAliases.ResolveHeroId(heroId);
            foreach (var metadata in configuration.HeroMetadata)
            {
                if (string.Equals(metadata.HeroId, heroId, StringComparison.Ordinal))
                {
                    return metadata;
                }
            }

            throw new KeyNotFoundException($"Unknown formal hero metadata {heroId}.");
        }

        public static SkillDefinition GetSkill(string skillId)
        {
            foreach (var skill in configuration.Skills)
            {
                if (string.Equals(skill.SkillId, skillId, StringComparison.Ordinal))
                {
                    return skill;
                }
            }

            throw new KeyNotFoundException($"Unknown formal skill {skillId}.");
        }

        private static FrozenHeroConfiguration Build()
        {
            var components = BuildComponents();
            return new FrozenHeroConfiguration(
                components,
                BuildRecipes(),
                BuildHeroes(),
                BuildHeroMetadata(),
                BuildSkills(),
                BuildBagTemplate(components),
                new HeroControlRulesDefinition(1f, 0.60f, 0.20f, 2f));
        }

        private static IReadOnlyList<HeroComponentDefinition> BuildComponents()
        {
            return Array.AsReadOnly(new[]
            {
                Component(DragonBoundComponentIds.DragonSigil, "契约幼龙", "Contract Dragonling", HeroComponentCategory.PublicCore, 3,
                    DragonBoundHeroIds.WindclawRanger, DragonBoundHeroIds.EmberShaman, DragonBoundHeroIds.DragonRider),
                Component(DragonBoundComponentIds.RuneGrimoire, "符文法杖", "Rune Staff", HeroComponentCategory.PublicCore, 3,
                    DragonBoundHeroIds.RuneboltMage, DragonBoundHeroIds.Stonebinder, DragonBoundHeroIds.StarfallArchmage),
                Component(DragonBoundComponentIds.WarHorn, "先祖战冠", "Ancestral War Crown", HeroComponentCategory.PublicCore, 3,
                    DragonBoundHeroIds.HornbladeDuelist, DragonBoundHeroIds.NorthwatchHunter, DragonBoundHeroIds.ThunderJarl),
                Component(DragonBoundComponentIds.SkyRanger, "天空游侠", "Sky Ranger", HeroComponentCategory.PurplePartner, 1,
                    DragonBoundHeroIds.WindclawRanger),
                Component(DragonBoundComponentIds.FlameShaman, "火焰萨满", "Flame Shaman", HeroComponentCategory.PurplePartner, 1,
                    DragonBoundHeroIds.EmberShaman),
                Component(DragonBoundComponentIds.RuneApprentice, "符文学徒", "Rune Apprentice", HeroComponentCategory.PurplePartner, 1,
                    DragonBoundHeroIds.RuneboltMage),
                Component(DragonBoundComponentIds.StoneScholar, "石像学者", "Stone Scholar", HeroComponentCategory.PurplePartner, 1,
                    DragonBoundHeroIds.Stonebinder),
                Component(DragonBoundComponentIds.WanderingSword, "流浪剑士", "Wandering Sword", HeroComponentCategory.PurplePartner, 1,
                    DragonBoundHeroIds.HornbladeDuelist),
                Component(DragonBoundComponentIds.NorthwatchScout, "北境斥候", "Northwatch Scout", HeroComponentCategory.PurplePartner, 1,
                    DragonBoundHeroIds.NorthwatchHunter),
                Component(DragonBoundComponentIds.DragonKnight, "龙骑士", "Dragon Knight", HeroComponentCategory.SharedRouteGoldPartner, 1,
                    DragonBoundHeroIds.DragonRider),
                Component(DragonBoundComponentIds.MeteorCore, "星界术师", "Astral Arcanist", HeroComponentCategory.SharedRouteGoldPartner, 1,
                    DragonBoundHeroIds.StarfallArchmage),
                Component(DragonBoundComponentIds.StormCrown, "风暴勇士", "Storm Warrior", HeroComponentCategory.SharedRouteGoldPartner, 1,
                    DragonBoundHeroIds.ThunderJarl),
                Component(DragonBoundComponentIds.ShadowCloak, "暗影行者", "Shadow Walker", HeroComponentCategory.DedicatedGold, 1,
                    DragonBoundHeroIds.NightfangAssassin),
                Component(DragonBoundComponentIds.RuneDagger, "符文匕首", "Rune Dagger", HeroComponentCategory.DedicatedGold, 1,
                    DragonBoundHeroIds.NightfangAssassin),
                Component(DragonBoundComponentIds.LeviathanEye, "深海鱼叉手", "Deepsea Harpooner", HeroComponentCategory.DedicatedGold, 1,
                    DragonBoundHeroIds.LeviathanHunter),
                Component(DragonBoundComponentIds.AncientHarpoon, "远古鱼叉", "Ancient Harpoon", HeroComponentCategory.DedicatedGold, 1,
                    DragonBoundHeroIds.LeviathanHunter),
                Component(DragonBoundComponentIds.ValkyrieWings, "女武神侍从", "Valkyrie Attendant", HeroComponentCategory.DedicatedGold, 1,
                    DragonBoundHeroIds.SkyhunterValkyrie),
                Component(DragonBoundComponentIds.DragonboneBow, "龙骨长弓", "Dragonbone Bow", HeroComponentCategory.DedicatedGold, 1,
                    DragonBoundHeroIds.SkyhunterValkyrie)
            });
        }

        private static IReadOnlyList<HeroRecipeDefinition> BuildRecipes()
        {
            return Array.AsReadOnly(new[]
            {
                Recipe(DragonBoundRecipeIds.WindclawRanger, DragonBoundHeroIds.WindclawRanger, HeroRecipeRarity.Purple,
                    HeroFormationOrientation.Vertical, DragonBoundComponentIds.SkyRanger, DragonBoundComponentIds.DragonSigil,
                    null, null, "FORM_WINDCLAW_RANGER", DragonBoundComponentIds.SkyRanger),
                Recipe(DragonBoundRecipeIds.EmberShaman, DragonBoundHeroIds.EmberShaman, HeroRecipeRarity.Purple,
                    HeroFormationOrientation.Vertical, DragonBoundComponentIds.FlameShaman, DragonBoundComponentIds.DragonSigil,
                    null, null, "FORM_EMBER_SHAMAN", DragonBoundComponentIds.FlameShaman),
                Recipe(DragonBoundRecipeIds.DragonRider, DragonBoundHeroIds.DragonRider, HeroRecipeRarity.Gold,
                    HeroFormationOrientation.Vertical, DragonBoundComponentIds.DragonKnight, DragonBoundComponentIds.DragonSigil,
                    null, null, "FORM_DRAGON_RIDER", DragonBoundComponentIds.DragonKnight),
                Recipe(DragonBoundRecipeIds.CrownSwordLeader, DragonBoundHeroIds.CrownSwordLeader, HeroRecipeRarity.Purple,
                    HeroFormationOrientation.Vertical, DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.WanderingSword,
                    null, null, "FORM_HORNBLADE_DUELIST", DragonBoundComponentIds.WanderingSword),
                Recipe(DragonBoundRecipeIds.CrownHunterLeader, DragonBoundHeroIds.CrownHunterLeader, HeroRecipeRarity.Purple,
                    HeroFormationOrientation.Vertical, DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.NorthwatchScout,
                    null, null, "FORM_NORTHWATCH_HUNTER", DragonBoundComponentIds.NorthwatchScout),
                Recipe(DragonBoundRecipeIds.ThunderJarl, DragonBoundHeroIds.ThunderJarl, HeroRecipeRarity.Gold,
                    HeroFormationOrientation.Vertical, DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.StormCrown,
                    null, null, "FORM_THUNDER_JARL", DragonBoundComponentIds.StormCrown),
                Recipe(DragonBoundRecipeIds.RuneboltMage, DragonBoundHeroIds.RuneboltMage, HeroRecipeRarity.Purple,
                    HeroFormationOrientation.Horizontal, null, null, DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.RuneApprentice,
                    "FORM_RUNEBOLT_MAGE", DragonBoundComponentIds.RuneApprentice),
                Recipe(DragonBoundRecipeIds.Stonebinder, DragonBoundHeroIds.Stonebinder, HeroRecipeRarity.Purple,
                    HeroFormationOrientation.Horizontal, null, null, DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.StoneScholar,
                    "FORM_STONEBINDER", DragonBoundComponentIds.StoneScholar),
                Recipe(DragonBoundRecipeIds.StarfallArchmage, DragonBoundHeroIds.StarfallArchmage, HeroRecipeRarity.Gold,
                    HeroFormationOrientation.Horizontal, null, null, DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.MeteorCore,
                    "FORM_STARFALL_ARCHMAGE", DragonBoundComponentIds.MeteorCore),
                Recipe(DragonBoundRecipeIds.NightfangAssassin, DragonBoundHeroIds.NightfangAssassin, HeroRecipeRarity.Gold,
                    HeroFormationOrientation.Horizontal, null, null, DragonBoundComponentIds.RuneDagger, DragonBoundComponentIds.ShadowCloak,
                    "FORM_NIGHTFANG_ASSASSIN", DragonBoundComponentIds.ShadowCloak),
                Recipe(DragonBoundRecipeIds.LeviathanHunter, DragonBoundHeroIds.LeviathanHunter, HeroRecipeRarity.Gold,
                    HeroFormationOrientation.Horizontal, null, null, DragonBoundComponentIds.AncientHarpoon, DragonBoundComponentIds.LeviathanEye,
                    "FORM_LEVIATHAN_HUNTER", DragonBoundComponentIds.LeviathanEye),
                Recipe(DragonBoundRecipeIds.SkyhunterValkyrie, DragonBoundHeroIds.SkyhunterValkyrie, HeroRecipeRarity.Gold,
                    HeroFormationOrientation.Horizontal, null, null, DragonBoundComponentIds.DragonboneBow, DragonBoundComponentIds.ValkyrieWings,
                    "FORM_SKYHUNTER_VALKYRIE", DragonBoundComponentIds.ValkyrieWings)
            });
        }

        private static IReadOnlyList<SkillDefinition> BuildSkills()
        {
            return Array.AsReadOnly(new[]
            {
                new SkillDefinition(
                    DragonBoundSkillIds.PowerShot, "风爪强袭", "Power Shot", HeroSkillTriggerType.EveryNthAttack,
                    triggerCount: 5, damageMultiplier: 1.80f,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "EliteFinalDamageMultiplier", 1.25f },
                        { "BossReceivesEliteBonus", 0f },
                        { "AttackCountResetsOnRetarget", 0f }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.ExplosiveFireball, "爆裂火球", "Explosive Fireball", HeroSkillTriggerType.NormalAttack,
                    radius: 0.90f, maxTargets: 5,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "PrimaryDamageMultiplier", 1f },
                        { "SecondaryDamageMultiplier", 0.75f }
                    }),
                // The frozen table gives this normal attack no separate player-facing skill name.
                new SkillDefinition(
                    DragonBoundSkillIds.RuneboltMage, string.Empty, string.Empty, HeroSkillTriggerType.NormalAttack,
                    width: 0.35f, length: 5f,
                    seriesParameters: new Dictionary<string, float[]>
                    {
                        { "MaxTargetsByLevel", new[] { 4f, 5f, 6f } }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.StoneBind, "岩缚", "Stone Bind", HeroSkillTriggerType.EveryNthAttack,
                    triggerCount: 4, baseStunDuration: 1.20f,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "AttackCountResetsOnRetarget", 0f }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.DuelMomentum, "决斗气势", "Duel Momentum", HeroSkillTriggerType.OnSameTargetAttack,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "FinalDamageBonusPerStack", 0.08f }
                    },
                    seriesParameters: new Dictionary<string, float[]>
                    {
                        { "MaxStacksByLevel", new[] { 5f, 6f, 7f } }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.HuntMark, "狩猎标记", "Hunt Mark", HeroSkillTriggerType.OnFirstAttack,
                    damageMultiplier: 1.25f,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "MaximumActiveMarks", 1f },
                        { "MarkedAttackDamageMultiplier", 1.25f },
                        { "ProvidesTeamDamageBonus", 0f }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.FlameDive, "烈焰俯冲", "Flame Dive", HeroSkillTriggerType.Cooldown,
                    cooldown: 6f, damageMultiplier: 2f, width: 0.70f, length: 6f,
                    duration: 3f, tickInterval: 1f,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "FlameTickBaseAttackMultiplier", 0.25f },
                        { "ConsumesCooldownWithoutTarget", 0f },
                        { "AppliesSlow", 0f },
                        { "AppliesStun", 0f }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.Starfall, "星陨", "Starfall", HeroSkillTriggerType.Cooldown,
                    cooldown: 8f, damageMultiplier: 2.80f, radius: 1.50f, maxTargets: 12,
                    targetPriorityOverride: HeroTargetPriority.HighestDensity,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "TelegraphDurationSeconds", 1f },
                        { "AppliesStun", 0f },
                        { "AppliesKnockback", 0f }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.ThunderDominion, "雷霆领域", "Thunder Dominion", HeroSkillTriggerType.Cooldown,
                    cooldown: 8f, damageMultiplier: 0.60f, baseStunDuration: 0.90f,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "AffectsAllEnemiesInAttackRange", 1f }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.NightfangExecution, "暗影处决", "Shadow Execution", HeroSkillTriggerType.Cooldown,
                    cooldown: 8f, duration: 0.60f,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "EliteAndBossFinalDamageBonus", 0.60f },
                        { "ExecuteHealthThreshold", 0.20f },
                        { "ExecuteFinalDamageBonus", 0.30f },
                        { "ExecutionHealthThreshold", 0.10f },
                        { "BossLowHealthFinalDamageMultiplier", 1.50f },
                        { "SkillTargetRange", 3.50f },
                        { "MaximumRetargets", 1f }
                    },
                    seriesParameters: new Dictionary<string, float[]>
                    {
                        { "DamageMultiplierByHit", new[] { 0.70f, 0.70f, 1.60f } }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.AbyssHarpoon, "深渊回钩", "Abyss Reeling Harpoon", HeroSkillTriggerType.Cooldown,
                    cooldown: 9f, duration: 0.45f, width: 0.40f, length: 6f, maxTargets: 6,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "NormalPullDistance", 1f },
                        { "ElitePullDistance", 0.50f },
                        { "BossSlowFraction", 0.25f },
                        { "BossSlowDurationSeconds", 1.50f },
                        { "SkillTargetRange", 3.50f }
                    },
                    seriesParameters: new Dictionary<string, float[]>
                    {
                        { "DamageMultiplierByHit", new[] { 1.50f, 1.38f, 1.26f, 1.14f, 1.02f, 0.90f } }
                    }),
                new SkillDefinition(
                    DragonBoundSkillIds.SkyHunt, "天穹圣辉", "Skyward Radiance", HeroSkillTriggerType.Cooldown,
                    cooldown: 10f, duration: 3f, maxTargets: 3,
                    scalarParameters: new Dictionary<string, float>
                    {
                        { "AttackSpeedBonusPerStack", 0.06f },
                        { "SecondaryDamageMultiplier", 0.40f },
                        { "SkillTargetRange", 3.50f }
                    },
                    seriesParameters: new Dictionary<string, float[]>
                    {
                        { "MaxStacksByLevel", new[] { 5f, 6f, 7f, 8f, 10f } }
                    })
            });
        }

        private static IReadOnlyList<HeroDefinition> BuildHeroes()
        {
            return Array.AsReadOnly(new[]
            {
                Hero(DragonBoundHeroIds.WindclawRanger, "风爪游侠", "Windclaw Ranger", HeroRecipeRarity.Purple,
                    DragonBoundComponentIds.DragonSigil, DragonBoundComponentIds.SkyRanger, 14f, 1.80f, 3.25f,
                    HeroAttackType.RangedSingleTarget, PurpleLevels(), DragonBoundSkillIds.PowerShot,
                    Targets(HeroTargetPriority.EliteFirst, HeroTargetPriority.Frontmost)),
                Hero(DragonBoundHeroIds.EmberShaman, "余烬萨满", "Ember Shaman", HeroRecipeRarity.Purple,
                    DragonBoundComponentIds.DragonSigil, DragonBoundComponentIds.FlameShaman, 8f, 1.70f, 3.00f,
                    HeroAttackType.Area, PurpleLevels(), DragonBoundSkillIds.ExplosiveFireball,
                    Targets(HeroTargetPriority.Frontmost),
                    new Dictionary<string, float> { { "AttackRadius", 0.90f }, { "AttackMaxTargets", 5f } }),
                Hero(DragonBoundHeroIds.RuneboltMage, "符文雷矢法师", "Runebolt Mage", HeroRecipeRarity.Purple,
                    DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.RuneApprentice, 8f, 1.75f, 3.00f,
                    HeroAttackType.PiercingLine, PurpleLevels(), DragonBoundSkillIds.RuneboltMage,
                    Targets(HeroTargetPriority.Frontmost),
                    new Dictionary<string, float> { { "PierceLength", 5f }, { "PierceWidth", 0.35f } },
                    new Dictionary<string, float[]> { { "MaxTargetsByLevel", new[] { 4f, 5f, 6f } } }),
                Hero(DragonBoundHeroIds.Stonebinder, "岩缚术士", "Stonebinder", HeroRecipeRarity.Purple,
                    DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.StoneScholar, 10f, 1.45f, 2.75f,
                    HeroAttackType.SingleTargetStun, PurpleLevels(), DragonBoundSkillIds.StoneBind,
                    Targets(HeroTargetPriority.Frontmost)),
                Hero(DragonBoundHeroIds.CrownSwordLeader, "冠誓剑士", "Oathcrown Swordsman", HeroRecipeRarity.Purple,
                    DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.WanderingSword, 18f, 1.50f, 1.75f,
                    HeroAttackType.LockedSingleTargetRamp, PurpleLevels(), DragonBoundSkillIds.DuelMomentum,
                    Targets(HeroTargetPriority.Frontmost)),
                Hero(DragonBoundHeroIds.CrownHunterLeader, "霜冠猎手", "Frostcrown Hunter", HeroRecipeRarity.Purple,
                    DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.NorthwatchScout, 16f, 1.45f, 3.25f,
                    HeroAttackType.MarkedSingleTarget, PurpleLevels(), DragonBoundSkillIds.HuntMark,
                    Targets(HeroTargetPriority.HighestHealth, HeroTargetPriority.Frontmost)),
                Hero(DragonBoundHeroIds.DragonRider, "烈焰龙骑", "Dragon Rider", HeroRecipeRarity.Gold,
                    DragonBoundComponentIds.DragonSigil, DragonBoundComponentIds.DragonKnight, 13f, 1.70f, 3.00f,
                    HeroAttackType.Area, GoldLevels(), DragonBoundSkillIds.FlameDive,
                    Targets(HeroTargetPriority.Frontmost),
                    new Dictionary<string, float> { { "AttackRadius", 0.65f }, { "AttackMaxTargets", 4f } }),
                Hero(DragonBoundHeroIds.StarfallArchmage, "星陨大法师", "Starfall Archmage", HeroRecipeRarity.Gold,
                    DragonBoundComponentIds.RuneGrimoire, DragonBoundComponentIds.MeteorCore, 12f, 1.75f, 3.25f,
                    HeroAttackType.LargeArea, GoldLevels(), DragonBoundSkillIds.Starfall,
                    Targets(HeroTargetPriority.HighestDensity, HeroTargetPriority.Frontmost),
                    new Dictionary<string, float> { { "AttackRadius", 0.80f }, { "AttackMaxTargets", 5f } }),
                Hero(DragonBoundHeroIds.ThunderJarl, "雷霆领主", "Thunder Jarl", HeroRecipeRarity.Gold,
                    DragonBoundComponentIds.WarHorn, DragonBoundComponentIds.StormCrown, 11f, 1.55f, 3.00f,
                    HeroAttackType.Chain, GoldLevels(), DragonBoundSkillIds.ThunderDominion,
                    Targets(HeroTargetPriority.Frontmost),
                    new Dictionary<string, float> { { "AttackMaxTargets", 3f }, { "ChainJumpRange", 1f } },
                    new Dictionary<string, float[]> { { "ChainDamageMultipliers", new[] { 1f, 0.75f, 0.55f } } }),
                Hero(DragonBoundHeroIds.NightfangAssassin, "夜牙刺客", "Nightfang Assassin", HeroRecipeRarity.Gold,
                    DragonBoundComponentIds.ShadowCloak, DragonBoundComponentIds.RuneDagger, 30f, 1.50f, 2.25f,
                    HeroAttackType.ExecuteSingleTarget, GoldLevels(), DragonBoundSkillIds.NightfangExecution,
                    Targets(HeroTargetPriority.BossFirst, HeroTargetPriority.EliteFirst, HeroTargetPriority.Frontmost)),
                Hero(DragonBoundHeroIds.LeviathanHunter, "海兽猎手", "Leviathan Hunter", HeroRecipeRarity.Gold,
                    DragonBoundComponentIds.LeviathanEye, DragonBoundComponentIds.AncientHarpoon, 15f, 1.85f, 3.50f,
                    HeroAttackType.PiercingFalloff, GoldLevels(), DragonBoundSkillIds.AbyssHarpoon,
                    Targets(HeroTargetPriority.Frontmost),
                    new Dictionary<string, float> { { "PierceLength", 6f }, { "PierceWidth", 0.40f }, { "AttackMaxTargets", 6f } },
                    new Dictionary<string, float[]> { { "DamageMultiplierByHit", new[] { 1f, 0.92f, 0.84f, 0.76f, 0.68f, 0.60f } } }),
                Hero(DragonBoundHeroIds.SkyhunterValkyrie, "天穹女武神", "Skyhunter Valkyrie", HeroRecipeRarity.Gold,
                    DragonBoundComponentIds.ValkyrieWings, DragonBoundComponentIds.DragonboneBow, 24f, 1.80f, 3.50f,
                    HeroAttackType.LockedAttackSpeedRamp, GoldLevels(), DragonBoundSkillIds.SkyHunt,
                    Targets(HeroTargetPriority.Frontmost))
            });
        }

        private static HeroComponentDefinition Component(
            string id,
            string displayNameZh,
            string displayNameEn,
            HeroComponentCategory category,
            int copiesPerRun,
            params string[] compatibleHeroIds)
        {
            return new HeroComponentDefinition(
                id,
                displayNameZh,
                displayNameEn,
                category,
                copiesPerRun,
                category != HeroComponentCategory.PublicCore,
                Array.AsReadOnly(compatibleHeroIds),
                id,
                "ART_Component_" + id);
        }

        private static HeroRecipeDefinition Recipe(
            string recipeId,
            string heroId,
            HeroRecipeRarity rarity,
            HeroFormationOrientation formationOrientation,
            string topComponentId,
            string bottomComponentId,
            string leftComponentId,
            string rightComponentId,
            string formationPrefabId,
            string progressOwnerComponentId)
        {
            return new HeroRecipeDefinition(
                recipeId,
                heroId,
                rarity,
                formationOrientation,
                topComponentId,
                bottomComponentId,
                leftComponentId,
                rightComponentId,
                formationPrefabId,
                progressOwnerComponentId);
        }

        private static IReadOnlyList<HeroCatalogMetadata> BuildHeroMetadata()
        {
            return Array.AsReadOnly(new[]
            {
                Metadata(DragonBoundHeroIds.WindclawRanger, DragonBoundRecipeIds.WindclawRanger,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.EmberShaman, DragonBoundRecipeIds.EmberShaman,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.DragonRider, DragonBoundRecipeIds.DragonRider,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.RuneboltMage, DragonBoundRecipeIds.RuneboltMage,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.Stonebinder, DragonBoundRecipeIds.Stonebinder,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.StarfallArchmage, DragonBoundRecipeIds.StarfallArchmage,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.CrownSwordLeader, DragonBoundRecipeIds.CrownSwordLeader,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.CrownHunterLeader, DragonBoundRecipeIds.CrownHunterLeader,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.ThunderJarl, DragonBoundRecipeIds.ThunderJarl,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.NightfangAssassin, DragonBoundRecipeIds.NightfangAssassin,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.LeviathanHunter, DragonBoundRecipeIds.LeviathanHunter,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented),
                Metadata(DragonBoundHeroIds.SkyhunterValkyrie, DragonBoundRecipeIds.SkyhunterValkyrie,
                    HeroNameFreezeState.Frozen, HeroRuntimeCombatState.Implemented)
            });
        }

        private static HeroCatalogMetadata Metadata(
            string heroId,
            string recipeId,
            HeroNameFreezeState nameFreezeState,
            HeroRuntimeCombatState runtimeCombatState)
        {
            return new HeroCatalogMetadata(
                heroId,
                recipeId,
                nameFreezeState,
                runtimeCombatState,
                galleryVisible: true,
                artSlotId: "ART_Hero_" + heroId);
        }

        private static HeroDefinition Hero(
            string id,
            string displayNameZh,
            string displayNameEn,
            HeroRecipeRarity rarity,
            string componentAId,
            string componentBId,
            float baseAttack,
            float baseAttackSpeed,
            float rangeCells,
            HeroAttackType attackPattern,
            HeroLevelStats[] levels,
            string skillId,
            IReadOnlyList<HeroTargetPriority> targetPriority,
            IReadOnlyDictionary<string, float> attackParameters = null,
            IReadOnlyDictionary<string, float[]> attackSeriesParameters = null)
        {
            return new HeroDefinition(
                id,
                displayNameZh,
                displayNameEn,
                rarity,
                componentAId,
                componentBId,
                baseAttack,
                baseAttackSpeed,
                rangeCells,
                attackPattern,
                levels,
                targetPriority,
                skillId,
                string.Empty,
                attackParameters,
                attackSeriesParameters);
        }

        private static HeroLevelStats[] PurpleLevels()
        {
            return new[]
            {
                new HeroLevelStats(1, 0, 1f, 1f, 1f),
                new HeroLevelStats(2, 20, 1.05f, 1.25f, 1.10f),
                new HeroLevelStats(3, 60, 1.10f, 1.56f, 1.25f)
            };
        }

        private static HeroLevelStats[] GoldLevels()
        {
            return new[]
            {
                new HeroLevelStats(1, 0, 1f, 1f, 1f),
                new HeroLevelStats(2, 20, 1.12f, 1.10f, 1.10f),
                new HeroLevelStats(3, 55, 1.25f, 1.21f, 1.25f),
                new HeroLevelStats(4, 105, 1.40f, 1.33f, 1.45f),
                new HeroLevelStats(5, 175, 1.57f, 1.46f, 1.70f)
            };
        }

        private static IReadOnlyList<HeroTargetPriority> Targets(params HeroTargetPriority[] priorities)
        {
            return Array.AsReadOnly(priorities);
        }

        private static IReadOnlyList<HeroComponentInstanceDefinition> BuildBagTemplate(
            IReadOnlyList<HeroComponentDefinition> components)
        {
            var instances = new List<HeroComponentInstanceDefinition>(24);
            foreach (var component in components)
            {
                for (var copy = 1; copy <= component.CopiesPerRun; copy++)
                {
                    instances.Add(new HeroComponentInstanceDefinition(
                        $"{component.Id}_{copy:00}",
                        component.Id,
                        copy));
                }
            }

            return instances.AsReadOnly();
        }
    }

    public static class FrozenHeroConfigurationValidator
    {
        public static IReadOnlyList<ConfigurationValidationIssue> Validate(
            FrozenHeroConfiguration configuration,
            bool requireDedicatedGoldProgressOwners)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var issues = new List<ConfigurationValidationIssue>();
            var componentById = IndexComponents(configuration.Components, issues);
            var recipeByHeroId = IndexRecipes(configuration.Recipes, issues);
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            var skillIds = IndexSkills(configuration.Skills, issues);
            var heroIds = new HashSet<string>(StringComparer.Ordinal);

            RequireCount(issues, "ComponentTypeCount", 18, configuration.Components.Count);
            RequireCount(issues, "RecipeCount", 12, configuration.Recipes.Count);
            RequireCount(issues, "HeroCount", 12, configuration.Heroes.Count);
            RequireCount(issues, "HeroMetadataCount", 12, configuration.HeroMetadata.Count);
            RequireCount(issues, "SkillCount", 12, configuration.Skills.Count);
            RequireCount(issues, "BagInstanceCount", 24, configuration.BagTemplate.Count);

            var purpleRecipeCount = 0;
            var goldRecipeCount = 0;
            foreach (var recipe in configuration.Recipes)
            {
                if (recipe != null && recipe.Rarity == HeroRecipeRarity.Purple)
                {
                    purpleRecipeCount++;
                }
                else if (recipe != null && recipe.Rarity == HeroRecipeRarity.Gold)
                {
                    goldRecipeCount++;
                }
            }

            RequireCount(issues, "PurpleRecipeCount", 6, purpleRecipeCount);
            RequireCount(issues, "GoldRecipeCount", 6, goldRecipeCount);

            var totalCopies = 0;
            foreach (var component in configuration.Components)
            {
                totalCopies += component.CopiesPerRun;
                var expectedCopies = component.Category == HeroComponentCategory.PublicCore ? 3 : 1;
                if (component.CopiesPerRun != expectedCopies)
                {
                    Error(issues, "ComponentCopies", $"{component.Id} must have {expectedCopies} copies.");
                }

                if ((component.Category == HeroComponentCategory.PublicCore) == component.IsUnique)
                {
                    Error(issues, "ComponentUniqueFlag", $"{component.Id} has an invalid UNIQUE flag.");
                }

                foreach (var compatibleHeroId in component.CompatibleHeroIds)
                {
                    if (!recipeByHeroId.ContainsKey(compatibleHeroId))
                    {
                        Error(issues, "UnknownCompatibleHero", $"{component.Id} references {compatibleHeroId}.");
                    }
                }
            }

            RequireCount(issues, "ComponentCopyTotal", 24, totalCopies);

            foreach (var recipe in configuration.Recipes)
            {
                if (string.IsNullOrWhiteSpace(recipe.RecipeId) || !recipeIds.Add(recipe.RecipeId))
                {
                    Error(issues, "RecipeIdRequiredOrDuplicate", recipe.HeroId);
                }
                if (!componentById.TryGetValue(recipe.ComponentAId, out var componentA) ||
                    !componentById.TryGetValue(recipe.ComponentBId, out var componentB))
                {
                    Error(issues, "RecipeComponentReference", $"{recipe.HeroId} references an unknown component.");
                    continue;
                }

                if (!Contains(componentA.CompatibleHeroIds, recipe.HeroId) ||
                    !Contains(componentB.CompatibleHeroIds, recipe.HeroId))
                {
                    Error(issues, "RecipeCompatibility", $"{recipe.HeroId} is missing from a component compatibility list.");
                }

                if (!HasValidFormationDefinition(recipe))
                {
                    Error(issues, "RecipeFormation", $"{recipe.HeroId} has an invalid fixed formation definition.");
                }

                var hasPublicCore = componentA.Category == HeroComponentCategory.PublicCore ||
                                    componentB.Category == HeroComponentCategory.PublicCore;
                if (hasPublicCore)
                {
                    var expectedOwner = componentA.Category == HeroComponentCategory.PublicCore
                        ? componentB.Id
                        : componentA.Id;
                    if (!string.Equals(recipe.ProgressOwnerComponentId, expectedOwner, StringComparison.Ordinal))
                    {
                        Error(issues, "PublicRouteProgressOwner", $"{recipe.HeroId} progress owner must be {expectedOwner}.");
                    }
                }
                else if (string.IsNullOrWhiteSpace(recipe.ProgressOwnerComponentId))
                {
                    AddIssue(
                        issues,
                        requireDedicatedGoldProgressOwners
                            ? ConfigurationValidationSeverity.Error
                            : ConfigurationValidationSeverity.Warning,
                        "DedicatedGoldProgressOwnerPending",
                        $"{recipe.HeroId} requires an explicit owner before its combat Runtime is enabled.");
                }
                else if (!string.Equals(recipe.ProgressOwnerComponentId, componentA.Id, StringComparison.Ordinal) &&
                         !string.Equals(recipe.ProgressOwnerComponentId, componentB.Id, StringComparison.Ordinal))
                {
                    Error(issues, "RecipeProgressOwner", $"{recipe.HeroId} progress owner is not part of its recipe.");
                }
            }

            foreach (var hero in configuration.Heroes)
            {
                if (!heroIds.Add(hero.Id))
                {
                    Error(issues, "DuplicateHeroId", hero.Id);
                    continue;
                }

                if (!recipeByHeroId.TryGetValue(hero.Id, out var recipe) ||
                    !recipe.Matches(hero.ComponentAId, hero.ComponentBId) ||
                    recipe.Rarity != hero.Rarity)
                {
                    Error(issues, "HeroRecipeMismatch", hero.Id);
                }

                if (!skillIds.Contains(hero.SkillId))
                {
                    Error(issues, "HeroSkillReference", $"{hero.Id} references {hero.SkillId}.");
                }

                var expectedMaxLevel = hero.Rarity == HeroRecipeRarity.Purple ? 3 : 5;
                if (hero.MaxLevel != expectedMaxLevel)
                {
                    Error(issues, "HeroMaxLevel", $"{hero.Id} must stop at level {expectedMaxLevel}.");
                }

                var previousExperience = -1;
                for (var level = 1; level <= hero.MaxLevel; level++)
                {
                    var stats = hero.GetLevelStats(level);
                    if (stats.Level != level ||
                        stats.RequiredExperience <= previousExperience ||
                        stats.AttackMultiplier <= 0f ||
                        stats.AttackSpeedMultiplier <= 0f ||
                        stats.SkillMultiplier <= 0f)
                    {
                        Error(issues, "HeroGrowth", $"{hero.Id} has invalid level {level} growth.");
                    }

                    previousExperience = stats.RequiredExperience;
                }
            }

            var metadataHeroIds = new HashSet<string>(StringComparer.Ordinal);
            var metadataRecipeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var metadata in configuration.HeroMetadata)
            {
                if (metadata == null ||
                    !metadataHeroIds.Add(metadata.HeroId) ||
                    !metadataRecipeIds.Add(metadata.RecipeId))
                {
                    Error(issues, "DuplicateHeroMetadata", metadata == null ? "null" : metadata.HeroId);
                    continue;
                }

                if (!heroIds.Contains(metadata.HeroId) ||
                    !recipeByHeroId.TryGetValue(metadata.HeroId, out var recipe) ||
                    !string.Equals(recipe.RecipeId, metadata.RecipeId, StringComparison.Ordinal))
                {
                    Error(issues, "HeroMetadataMismatch", metadata.HeroId);
                }

                if (metadata.RuntimeCombatState == HeroRuntimeCombatState.Implemented &&
                    metadata.NameFreezeState != HeroNameFreezeState.Frozen)
                {
                    // Runtime combat can be implemented before the final localized
                    // display name is frozen. Keep this as an explicit handoff warning.
                    AddIssue(
                        issues,
                        ConfigurationValidationSeverity.Warning,
                        "ImplementedHeroNamePending",
                        metadata.HeroId);
                }
            }

            ValidateBag(configuration.BagTemplate, componentById, issues);
            if (configuration.ControlRules.NormalStunMultiplier != 1f ||
                configuration.ControlRules.EliteStunMultiplier != 0.60f ||
                configuration.ControlRules.BossStunMultiplier != 0.20f ||
                configuration.ControlRules.BossPostStunImmunitySeconds != 2f)
            {
                Error(issues, "ControlRules", "Frozen stun multipliers or boss immunity do not match v1.0.");
            }

            return issues.AsReadOnly();
        }

        private static bool HasValidFormationDefinition(HeroRecipeDefinition recipe)
        {
            if (string.IsNullOrWhiteSpace(recipe.FormationPrefabId))
            {
                return false;
            }

            switch (recipe.FormationOrientation)
            {
                case HeroFormationOrientation.Vertical:
                    return !string.IsNullOrWhiteSpace(recipe.TopComponentId) &&
                           !string.IsNullOrWhiteSpace(recipe.BottomComponentId) &&
                           string.IsNullOrWhiteSpace(recipe.LeftComponentId) &&
                           string.IsNullOrWhiteSpace(recipe.RightComponentId) &&
                           string.Equals(recipe.ComponentAId, recipe.TopComponentId, StringComparison.Ordinal) &&
                           string.Equals(recipe.ComponentBId, recipe.BottomComponentId, StringComparison.Ordinal);
                case HeroFormationOrientation.Horizontal:
                    return string.IsNullOrWhiteSpace(recipe.TopComponentId) &&
                           string.IsNullOrWhiteSpace(recipe.BottomComponentId) &&
                           !string.IsNullOrWhiteSpace(recipe.LeftComponentId) &&
                           !string.IsNullOrWhiteSpace(recipe.RightComponentId) &&
                           string.Equals(recipe.ComponentAId, recipe.LeftComponentId, StringComparison.Ordinal) &&
                           string.Equals(recipe.ComponentBId, recipe.RightComponentId, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        private static Dictionary<string, HeroComponentDefinition> IndexComponents(
            IReadOnlyList<HeroComponentDefinition> components,
            ICollection<ConfigurationValidationIssue> issues)
        {
            var result = new Dictionary<string, HeroComponentDefinition>(StringComparer.Ordinal);
            foreach (var component in components)
            {
                if (component == null || result.ContainsKey(component.Id))
                {
                    Error(issues, "DuplicateComponentId", component == null ? "null" : component.Id);
                    continue;
                }

                result.Add(component.Id, component);
            }

            return result;
        }

        private static Dictionary<string, HeroRecipeDefinition> IndexRecipes(
            IReadOnlyList<HeroRecipeDefinition> recipes,
            ICollection<ConfigurationValidationIssue> issues)
        {
            var result = new Dictionary<string, HeroRecipeDefinition>(StringComparer.Ordinal);
            foreach (var recipe in recipes)
            {
                if (recipe == null || result.ContainsKey(recipe.HeroId))
                {
                    Error(issues, "DuplicateRecipeId", recipe == null ? "null" : recipe.HeroId);
                    continue;
                }

                result.Add(recipe.HeroId, recipe);
            }

            return result;
        }

        private static HashSet<string> IndexSkills(
            IReadOnlyList<SkillDefinition> skills,
            ICollection<ConfigurationValidationIssue> issues)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skill in skills)
            {
                if (skill == null || !result.Add(skill.SkillId))
                {
                    Error(issues, "DuplicateSkillId", skill == null ? "null" : skill.SkillId);
                }
            }

            return result;
        }

        private static void ValidateBag(
            IReadOnlyList<HeroComponentInstanceDefinition> bag,
            IReadOnlyDictionary<string, HeroComponentDefinition> componentById,
            ICollection<ConfigurationValidationIssue> issues)
        {
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            var countByComponent = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var instance in bag)
            {
                if (instance == null || !instanceIds.Add(instance.InstanceId))
                {
                    Error(issues, "BagInstanceId", instance == null ? "null" : instance.InstanceId);
                    continue;
                }

                if (!componentById.ContainsKey(instance.ComponentId))
                {
                    Error(issues, "BagComponentReference", instance.ComponentId);
                    continue;
                }

                countByComponent.TryGetValue(instance.ComponentId, out var count);
                countByComponent[instance.ComponentId] = count + 1;
            }

            foreach (var component in componentById.Values)
            {
                countByComponent.TryGetValue(component.Id, out var count);
                if (count != component.CopiesPerRun)
                {
                    Error(issues, "BagComponentCount", $"{component.Id} has {count} instances.");
                }
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            foreach (var value in values)
            {
                if (string.Equals(value, expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RequireCount(
            ICollection<ConfigurationValidationIssue> issues,
            string code,
            int expected,
            int actual)
        {
            if (expected != actual)
            {
                Error(issues, code, $"Expected {expected}, actual {actual}.");
            }
        }

        private static void Error(
            ICollection<ConfigurationValidationIssue> issues,
            string code,
            string message)
        {
            AddIssue(issues, ConfigurationValidationSeverity.Error, code, message);
        }

        private static void AddIssue(
            ICollection<ConfigurationValidationIssue> issues,
            ConfigurationValidationSeverity severity,
            string code,
            string message)
        {
            issues.Add(new ConfigurationValidationIssue(severity, code, message));
        }
    }
}
