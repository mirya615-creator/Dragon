using System;
using System.Collections.Generic;
using DragonBound.Recruitment;
using DragonBound.Grid;

namespace DragonBound.Combat
{
    public enum HeroAttackType
    {
        RangedSingleTarget,
        Area,
        AreaDamageOverTime,
        PiercingLine,
        SingleTargetStun,
        LockedSingleTargetRamp,
        MarkedSingleTarget,
        LargeArea,
        Chain,
        ExecuteSingleTarget,
        PiercingFalloff,
        LockedAttackSpeedRamp
    }

    public enum HeroTargetPriority
    {
        Frontmost,
        EliteFirst,
        HighestHealth,
        HighestDensity,
        BossFirst
    }

    public enum HeroSkillTriggerType
    {
        EveryNthAttack,
        OnHit,
        NormalAttack,
        OnSameTargetAttack,
        OnFirstAttack,
        Cooldown,
        Passive
    }

    public readonly struct HeroLevelStats
    {
        public HeroLevelStats(
            int level,
            int requiredExperience,
            float attackMultiplier,
            float attackSpeedMultiplier)
            : this(level, requiredExperience, attackMultiplier, attackSpeedMultiplier, 1f)
        {
        }

        public HeroLevelStats(
            int level,
            int requiredExperience,
            float attackMultiplier,
            float attackSpeedMultiplier,
            float skillMultiplier)
        {
            Level = level;
            RequiredExperience = requiredExperience;
            AttackMultiplier = attackMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            SkillMultiplier = skillMultiplier;
        }

        public int Level { get; }
        public int RequiredExperience { get; }
        public float AttackMultiplier { get; }
        public float AttackSpeedMultiplier { get; }
        public float SkillMultiplier { get; }
    }

    public sealed class SkillDefinition
    {
        public SkillDefinition(
            string skillId,
            string displayNameZh,
            string displayNameEn,
            HeroSkillTriggerType triggerType,
            int triggerCount = 0,
            float cooldown = 0f,
            float damageMultiplier = 0f,
            float radius = 0f,
            float width = 0f,
            float length = 0f,
            int maxTargets = 0,
            float baseStunDuration = 0f,
            float duration = 0f,
            float tickInterval = 0f,
            HeroTargetPriority? targetPriorityOverride = null,
            IReadOnlyDictionary<string, float> scalarParameters = null,
            IReadOnlyDictionary<string, float[]> seriesParameters = null)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                throw new ArgumentException("A skill id is required.", nameof(skillId));
            }

            SkillId = skillId;
            DisplayNameZh = displayNameZh ?? string.Empty;
            DisplayNameEn = displayNameEn ?? string.Empty;
            TriggerType = triggerType;
            TriggerCount = triggerCount;
            Cooldown = cooldown;
            DamageMultiplier = damageMultiplier;
            Radius = radius;
            Width = width;
            Length = length;
            MaxTargets = maxTargets;
            BaseStunDuration = baseStunDuration;
            Duration = duration;
            TickInterval = tickInterval;
            TargetPriorityOverride = targetPriorityOverride;
            ScalarParameters = scalarParameters ?? new Dictionary<string, float>();
            SeriesParameters = seriesParameters ?? new Dictionary<string, float[]>();
        }

        public string SkillId { get; }
        public string DisplayNameZh { get; }
        public string DisplayNameEn { get; }
        public HeroSkillTriggerType TriggerType { get; }
        public int TriggerCount { get; }
        public float Cooldown { get; }
        public float DamageMultiplier { get; }
        public float Radius { get; }
        public float Width { get; }
        public float Length { get; }
        public int MaxTargets { get; }
        public float BaseStunDuration { get; }
        public float Duration { get; }
        public float TickInterval { get; }
        public HeroTargetPriority? TargetPriorityOverride { get; }
        public IReadOnlyDictionary<string, float> ScalarParameters { get; }
        public IReadOnlyDictionary<string, float[]> SeriesParameters { get; }
    }

    public sealed class HeroDefinition
    {
        private readonly HeroLevelStats[] levels;

        public HeroDefinition(
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
            IReadOnlyList<HeroTargetPriority> targetPriority,
            string skillId,
            string weaponArchetype,
            IReadOnlyDictionary<string, float> attackParameters = null,
            IReadOnlyDictionary<string, float[]> attackSeriesParameters = null)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(displayNameZh) ||
                string.IsNullOrWhiteSpace(displayNameEn) ||
                string.IsNullOrWhiteSpace(componentAId) ||
                string.IsNullOrWhiteSpace(componentBId) ||
                string.IsNullOrWhiteSpace(skillId))
            {
                throw new ArgumentException("A hero requires formal ids, names, components, and a skill id.");
            }

            if (baseAttack <= 0f || baseAttackSpeed <= 0f || rangeCells <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(baseAttack), "Hero base combat values must be positive.");
            }

            Id = id;
            DisplayNameZh = displayNameZh;
            DisplayNameEn = displayNameEn;
            Rarity = rarity;
            ComponentAId = componentAId;
            ComponentBId = componentBId;
            BaseAttack = baseAttack;
            BaseAttackSpeed = baseAttackSpeed;
            RangeCells = rangeCells;
            AttackPattern = attackPattern;
            this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
            TargetPriority = targetPriority ?? throw new ArgumentNullException(nameof(targetPriority));
            SkillId = skillId;
            WeaponArchetype = weaponArchetype ?? string.Empty;
            AttackParameters = attackParameters ?? new Dictionary<string, float>();
            AttackSeriesParameters = attackSeriesParameters ?? new Dictionary<string, float[]>();
            if (levels.Length == 0 || TargetPriority.Count == 0)
            {
                throw new ArgumentException("A hero requires growth levels and at least one target priority.");
            }
        }

        public string Id { get; }
        public string DisplayName => DisplayNameEn;
        public string DisplayNameZh { get; }
        public string DisplayNameEn { get; }
        public HeroRecipeRarity Rarity { get; }
        public string ComponentAId { get; }
        public string ComponentBId { get; }
        public float BaseAttack { get; }
        public float BaseAttackSpeed { get; }
        public float RangeCells { get; }
        public HeroAttackType AttackPattern { get; }
        public HeroAttackType AttackType => AttackPattern;
        public IReadOnlyList<HeroTargetPriority> TargetPriority { get; }
        public string SkillId { get; }
        public string WeaponArchetype { get; }
        public IReadOnlyDictionary<string, float> AttackParameters { get; }
        public IReadOnlyDictionary<string, float[]> AttackSeriesParameters { get; }
        public int MaxLevel => levels.Length;

        public HeroLevelStats GetLevelStats(int level)
        {
            if (level < 1 || level > levels.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            return levels[level - 1];
        }

        public int GetLevelForExperience(int experience)
        {
            if (experience < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(experience));
            }

            var level = 1;
            for (var index = 1; index < levels.Length; index++)
            {
                if (experience < levels[index].RequiredExperience)
                {
                    break;
                }

                level = index + 1;
            }

            return level;
        }
    }

    public static class HeroSliceCatalog
    {
        public const string DragonSigilComponentId = HeroSliceRecruitmentConfig.DragonSigilId;
        public const string SkyRangerComponentId = HeroSliceRecruitmentConfig.SkyRangerId;
        public const string FlameShamanComponentId = HeroSliceRecruitmentConfig.FlameShamanId;
        public const string DragonKnightComponentId = HeroSliceRecruitmentConfig.DragonKnightId;
        public const string WindclawRangerHeroId = HeroSliceRecruitmentConfig.WindclawRangerId;
        public const string EmberShamanHeroId = HeroSliceRecruitmentConfig.EmberShamanId;
        public const string DragonRiderHeroId = HeroSliceRecruitmentConfig.DragonRiderId;
        public const string RuneboltMageHeroId = DragonBoundHeroIds.RuneboltMage;
        public const string StonebinderHeroId = DragonBoundHeroIds.Stonebinder;
        public const string StarfallArchmageHeroId = DragonBoundHeroIds.StarfallArchmage;
        public const string CrownSwordLeaderHeroId = DragonBoundHeroIds.CrownSwordLeader;
        public const string CrownHunterLeaderHeroId = DragonBoundHeroIds.CrownHunterLeader;
        public const string ThunderJarlHeroId = DragonBoundHeroIds.ThunderJarl;
        public const string NightfangAssassinHeroId = DragonBoundHeroIds.NightfangAssassin;
        public const string LeviathanHunterHeroId = DragonBoundHeroIds.LeviathanHunter;
        public const string SkyhunterValkyrieHeroId = DragonBoundHeroIds.SkyhunterValkyrie;
        public const string WindclawRangerRecipeId = DragonBoundRecipeIds.WindclawRanger;
        public const string EmberShamanRecipeId = DragonBoundRecipeIds.EmberShaman;
        public const string DragonRiderRecipeId = DragonBoundRecipeIds.DragonRider;
        public const string RuneboltMageRecipeId = DragonBoundRecipeIds.RuneboltMage;
        public const string StonebinderRecipeId = DragonBoundRecipeIds.Stonebinder;
        public const string StarfallArchmageRecipeId = DragonBoundRecipeIds.StarfallArchmage;

        // HeroSlice_Main is an adapter over the formal catalog, never a second definition source.
        private static readonly Dictionary<string, HeroDefinition> Definitions = BuildDefinitions();

        public static HeroDefinition Get(string heroId)
        {
            if (!Definitions.TryGetValue(heroId, out var definition))
            {
                throw new KeyNotFoundException($"Hero {heroId} is not enabled in HeroSlice_Main.");
            }

            return definition;
        }

        public static bool TryGetRecipe(
            string firstComponentId,
            string secondComponentId,
            out string heroId)
        {
            if (TryGetRecipeDefinition(firstComponentId, secondComponentId, out var recipe))
            {
                heroId = recipe.HeroId;
                return true;
            }

            heroId = string.Empty;
            return false;
        }

        public static bool TryGetRecipeDefinition(
            string firstComponentId,
            string secondComponentId,
            out HeroRecipeDefinition recipe)
        {
            foreach (var candidate in GetEnabledRecipes())
            {
                if (candidate.Matches(firstComponentId, secondComponentId))
                {
                    recipe = candidate;
                    return true;
                }
            }

            recipe = null;
            return false;
        }

        public static bool TryGetRecipeDefinitionAtFormation(
            string firstComponentId,
            GridPosition firstPosition,
            string secondComponentId,
            GridPosition secondPosition,
            out HeroRecipeDefinition recipe)
        {
            foreach (var candidate in GetEnabledRecipes())
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

        public static string GetComponentDisplayName(string componentId)
        {
            if (HeroSliceRecruitmentConfig.TryGetComponent(componentId, out var sliceComponent))
            {
                return sliceComponent.DisplayNameZh;
            }

            return HeroComponentCatalog.TryGet(componentId, out var formalComponent)
                ? formalComponent.DisplayNameZh
                : "英雄组件";
        }

        public static bool IsUniqueComponent(string componentId)
        {
            return (HeroSliceRecruitmentConfig.TryGetComponent(componentId, out var sliceComponent) &&
                    sliceComponent.IsUnique) ||
                   (HeroComponentCatalog.TryGet(componentId, out var formalComponent) &&
                    formalComponent.IsUnique);
        }

        private static IEnumerable<HeroRecipeDefinition> GetEnabledRecipes()
        {
            foreach (var recipe in FrozenHeroConfigurationCatalog.Configuration.Recipes)
            {
                if (HeroDefinitionCatalog.GetMetadata(recipe.HeroId).RuntimeCombatState ==
                    HeroRuntimeCombatState.Implemented)
                {
                    yield return recipe;
                }
            }
        }

        private static Dictionary<string, HeroDefinition> BuildDefinitions()
        {
            var result = new Dictionary<string, HeroDefinition>(StringComparer.Ordinal);
            foreach (var recipe in FrozenHeroConfigurationCatalog.Configuration.Recipes)
            {
                var metadata = HeroDefinitionCatalog.GetMetadata(recipe.HeroId);
                if (metadata.RuntimeCombatState == HeroRuntimeCombatState.Implemented)
                {
                    result.Add(recipe.HeroId, HeroDefinitionCatalog.Get(recipe.HeroId));
                }
            }

            return result;
        }
    }
}
