using DragonBound.Bosses.Contracts;
using DragonBound.Foundation.Contracts;

namespace DragonBound.Bosses.Runtime
{
    public readonly struct BloodcrownTyrantSlot
    {
        public BloodcrownTyrantSlot(BossDefinition definition)
        {
            if (definition.BossId != FixedBossIds.W16BloodcrownTyrant || definition.Wave.Value != 16)
            {
                throw new System.ArgumentException("The Bloodcrown slot requires the fixed W16 definition.", nameof(definition));
            }

            Definition = definition;
        }

        public BossDefinition Definition { get; }
        public bool IsIndependentBossSlot => true;
        public int RegularEnemyCountContribution => 0;
    }

    public static class BloodcrownTyrantConfiguration
    {
        public const string BossId = FixedBossIds.W16BloodcrownTyrantValue;
        public const string SkillId = "BLOODCROWN_DECREE";

        // Greybox input only. Production HP remains pending and is never inferred here.
        public const float GreyboxMaxHitPoints = 2400f;
        public const float BossMoveSpeedCellsPerSecond = 0.20f;
        public const float FirstCastDelaySeconds = 8f;
        public const float CastWindupSeconds = 1f;
        public const float RetryCooldownSeconds = 12f;
        public const float SpellbreakerReflectionFraction = 0.10f;
        public const int EffectiveCombatLevel = 1;
        public const int HeroXpReward = 15;

        public static BossDefinition CreateGreyboxDefinition()
        {
            return new BossDefinition(
                FixedBossIds.W16BloodcrownTyrant,
                new WaveNumber(16),
                GreyboxMaxHitPoints,
                BossMoveSpeedCellsPerSecond,
                BossGoalEffect.InstantDefeat,
                HeroXpReward);
        }

        public static BloodcrownTyrantSlot CreateGreyboxSlot()
        {
            return new BloodcrownTyrantSlot(CreateGreyboxDefinition());
        }
    }
}
