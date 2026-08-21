using System;

namespace DragonBound.Runes
{
    public sealed class RuneModifierInput
    {
        public float BaseAttackDamage;
        public float BaseRange;
        public float HeroLevelMultiplier = 1f;
        public float HeroSkillMultiplier = 1f;
        public float TemporaryBuffMultiplier = 1f;
    }
    public readonly struct RuneModifierResult
    {
        public RuneModifierResult(float attackDamage, float range) { AttackDamage = attackDamage; Range = range; }
        public float AttackDamage { get; }
        public float Range { get; }
    }
    public static class RuneModifierPipeline
    {
        public static RuneModifierResult Evaluate(RuneModifierInput input, RuneDefinition rune)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var damage = input.BaseAttackDamage * input.HeroLevelMultiplier * input.HeroSkillMultiplier * input.TemporaryBuffMultiplier;
            var range = input.BaseRange;
            if (rune != null)
            {
                if (rune.EffectType == RuneEffectType.AttackDamagePercent) damage *= 1f + rune.Parameter;
                if (rune.EffectType == RuneEffectType.AttackRangeFlat) range += rune.Parameter;
            }
            return new RuneModifierResult(damage, range);
        }
    }
}
