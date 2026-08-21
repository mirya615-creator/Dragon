using System.Collections.Generic;

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
                throw new System.ArgumentException("A skill id is required.", nameof(skillId));
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

    public interface IHeroIdentity
    {
        string HeroId { get; }
        string RecipeId { get; }
    }

    public interface IHeroProgression
    {
        string HeroId { get; }
        int Experience { get; }
        int Level { get; }
    }

    public interface IHeroXpAwarder
    {
        int AwardExperience(string heroRuntimeId, int amount);
    }
}
