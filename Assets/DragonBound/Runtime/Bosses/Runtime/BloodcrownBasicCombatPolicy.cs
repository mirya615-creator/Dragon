using System;

namespace DragonBound.Bosses.Runtime
{
    public enum BloodcrownMergeEntry
    {
        DragDrop,
        Automatic,
        Recruitment,
        Item
    }

    public static class BloodcrownMergePolicy
    {
        public static bool CanMerge(BloodcrownMergeEntry entry, bool decreeActive)
        {
            return !decreeActive;
        }

        public static bool KeepsDuplicateRecruitIndependent(bool decreeActive)
        {
            return !CanMerge(BloodcrownMergeEntry.Recruitment, decreeActive);
        }
    }

    public interface IBloodcrownBasicModifierPipeline
    {
        float ApplyAttack(float levelOneBaseAttack);
        float ApplyAttackSpeed(float levelOneBaseAttackSpeed);
    }

    public readonly struct BloodcrownBasicCombatInput
    {
        public BloodcrownBasicCombatInput(
            int storedLevel,
            float levelOneBaseAttack,
            float levelOneBaseAttackSpeed,
            float storedLevelRange)
        {
            if (storedLevel < 1 || levelOneBaseAttack < 0f || levelOneBaseAttackSpeed <= 0f || storedLevelRange < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(storedLevel));
            }

            StoredLevel = storedLevel;
            LevelOneBaseAttack = levelOneBaseAttack;
            LevelOneBaseAttackSpeed = levelOneBaseAttackSpeed;
            StoredLevelRange = storedLevelRange;
        }

        public int StoredLevel { get; }
        public float LevelOneBaseAttack { get; }
        public float LevelOneBaseAttackSpeed { get; }
        public float StoredLevelRange { get; }
    }

    public readonly struct BloodcrownBasicCombatStats
    {
        public BloodcrownBasicCombatStats(
            int storedLevel,
            int effectiveCombatLevel,
            float attack,
            float attackSpeed,
            float range)
        {
            StoredLevel = storedLevel;
            EffectiveCombatLevel = effectiveCombatLevel;
            Attack = attack;
            AttackSpeed = attackSpeed;
            Range = range;
        }

        public int StoredLevel { get; }
        public int EffectiveCombatLevel { get; }
        public float Attack { get; }
        public float AttackSpeed { get; }
        public float Range { get; }
    }

    public static class BloodcrownBasicCombatPolicy
    {
        public static BloodcrownBasicCombatStats Apply(
            BloodcrownBasicCombatInput input,
            IBloodcrownBasicModifierPipeline modifierPipeline)
        {
            if (modifierPipeline == null)
            {
                throw new ArgumentNullException(nameof(modifierPipeline));
            }

            return new BloodcrownBasicCombatStats(
                input.StoredLevel,
                BloodcrownTyrantConfiguration.EffectiveCombatLevel,
                modifierPipeline.ApplyAttack(input.LevelOneBaseAttack),
                modifierPipeline.ApplyAttackSpeed(input.LevelOneBaseAttackSpeed),
                input.StoredLevelRange);
        }
    }
}
