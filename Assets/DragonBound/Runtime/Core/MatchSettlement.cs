namespace DragonBound.Core
{
    public enum SettlementDecision
    {
        Undecided,
        PlayerVictory,
        PlayerDefeat
    }

    public readonly struct SettlementContext
    {
        public SettlementContext(
            int playerHatchlingHealth,
            int aiHatchlingHealth,
            bool playerDefeatedFinalBoss,
            bool aiDefeatedFinalBoss,
            float playerFinalBossHealth,
            float aiFinalBossHealth,
            float playerWave15ClearTime,
            float aiWave15ClearTime)
        {
            PlayerHatchlingHealth = playerHatchlingHealth;
            AIHatchlingHealth = aiHatchlingHealth;
            PlayerDefeatedFinalBoss = playerDefeatedFinalBoss;
            AIDefeatedFinalBoss = aiDefeatedFinalBoss;
            PlayerFinalBossHealth = playerFinalBossHealth;
            AIFinalBossHealth = aiFinalBossHealth;
            PlayerWave15ClearTime = playerWave15ClearTime;
            AIWave15ClearTime = aiWave15ClearTime;
        }

        public int PlayerHatchlingHealth { get; }
        public int AIHatchlingHealth { get; }
        public bool PlayerDefeatedFinalBoss { get; }
        public bool AIDefeatedFinalBoss { get; }
        public float PlayerFinalBossHealth { get; }
        public float AIFinalBossHealth { get; }
        public float PlayerWave15ClearTime { get; }
        public float AIWave15ClearTime { get; }
    }

    public interface IMatchSettlementRule
    {
        SettlementDecision Evaluate(SettlementContext context);
    }

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
