namespace DragonBound.Core
{
    public sealed class FinalBossSettlementRule : IMatchSettlementRule
    {
        public SettlementDecision Evaluate(SettlementContext context)
        {
            if (context.PlayerHatchlingHealth <= 0 && context.AIHatchlingHealth > 0)
            {
                return SettlementDecision.PlayerDefeat;
            }

            if (context.AIHatchlingHealth <= 0 && context.PlayerHatchlingHealth > 0)
            {
                return SettlementDecision.PlayerVictory;
            }

            if (context.PlayerHatchlingHealth <= 0 && context.AIHatchlingHealth <= 0)
            {
                return SettlementDecision.Undecided;
            }

            if (context.PlayerDefeatedFinalBoss != context.AIDefeatedFinalBoss)
            {
                return context.PlayerDefeatedFinalBoss
                    ? SettlementDecision.PlayerVictory
                    : SettlementDecision.PlayerDefeat;
            }

            var bossHealthComparison = context.PlayerFinalBossHealth.CompareTo(context.AIFinalBossHealth);
            if (bossHealthComparison != 0)
            {
                return bossHealthComparison < 0
                    ? SettlementDecision.PlayerVictory
                    : SettlementDecision.PlayerDefeat;
            }

            var hatchlingHealthComparison = context.PlayerHatchlingHealth.CompareTo(context.AIHatchlingHealth);
            if (hatchlingHealthComparison != 0)
            {
                return hatchlingHealthComparison > 0
                    ? SettlementDecision.PlayerVictory
                    : SettlementDecision.PlayerDefeat;
            }

            var clearTimeComparison = context.PlayerWave15ClearTime.CompareTo(context.AIWave15ClearTime);
            if (clearTimeComparison == 0)
            {
                return SettlementDecision.Undecided;
            }

            return clearTimeComparison < 0
                ? SettlementDecision.PlayerVictory
                : SettlementDecision.PlayerDefeat;
        }
    }
}
