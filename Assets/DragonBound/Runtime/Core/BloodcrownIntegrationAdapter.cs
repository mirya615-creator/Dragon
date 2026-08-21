using System;
using DragonBound.Bosses.Contracts;
using DragonBound.Bosses.Runtime;
using DragonBound.Combat;

namespace DragonBound.Core
{
    /// <summary>Adapter owned by the pressure composition root; the Boss module stays engine-agnostic.</summary>
    internal sealed class BloodcrownIntegrationAdapter :
        IBloodcrownBossTarget,
        IBloodcrownBasicPolicyPort,
        IBloodcrownSpellbreaker
    {
        private readonly EnemyRuntime boss;
        private readonly TeamSide side;
        private readonly ISoulChainSpellbreakerResolver spellbreaker;

        public BloodcrownIntegrationAdapter(
            EnemyRuntime boss,
            TeamSide side,
            ISoulChainSpellbreakerResolver spellbreaker)
        {
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.side = side;
            this.spellbreaker = spellbreaker;
        }

        public float MaxHitPoints => boss.MaxHitPoints;
        public bool IsAlive => boss.IsAlive;
        public EnemyRuntime Boss => boss;
        public bool IsDecreeActive { get; private set; }
        public int EffectiveCombatLevel { get; private set; }
        public bool IsMergeBlocked { get; private set; }

        public void ApplyReflectedDamage(float damage)
        {
            if (damage <= 0f || !boss.IsAlive)
            {
                return;
            }

            // Reflected damage is a boss self-effect. It is intentionally not assigned to a
            // Hero/Basic owner and is not settled as a kill reward by this adapter.
            boss.ApplyDamage(damage);
        }

        public void EnableDecree(int effectiveCombatLevel)
        {
            IsDecreeActive = true;
            EffectiveCombatLevel = effectiveCombatLevel;
        }

        public void DisableDecree()
        {
            IsDecreeActive = false;
            EffectiveCombatLevel = 0;
        }

        public void SetMergeBlocked(bool blocked)
        {
            IsMergeBlocked = blocked;
        }

        public SpellbreakerOutcome Evaluate(BossCastAttempt attempt)
        {
            if (spellbreaker == null)
            {
                return SpellbreakerOutcome.NotEvaluated;
            }

            return spellbreaker.ShouldBlockCast(new SoulChainBossCastContext(
                attempt.BossId.Value,
                side,
                attempt.AttemptNumber,
                boss.MaxHitPoints))
                ? SpellbreakerOutcome.Blocked
                : SpellbreakerOutcome.Passed;
        }
    }

    internal sealed class BloodcrownBasicModifierPipeline : IBloodcrownBasicModifierPipeline
    {
        public float ApplyAttack(float levelOneBaseAttack) => levelOneBaseAttack;
        public float ApplyAttackSpeed(float levelOneBaseAttackSpeed) => levelOneBaseAttackSpeed;
    }
}
