using System;

namespace DragonBound.AI
{
    /// <summary>
    /// Pure recovery-match rules. Persistence/server authority is intentionally kept at the
    /// gameplay gateway boundary so the AI controller cannot change matchmaking state.
    /// </summary>
    public static class AiRecoveryPolicy
    {
        public const int RequiredNormalDefeats = 2;

        public static bool ShouldStartRecovery(int rankLevel, int consecutiveNormalDefeats)
        {
            return rankLevel < 10 && consecutiveNormalDefeats >= RequiredNormalDefeats;
        }

        public static AiStrategyProfileId ResolveEffectiveProfile(
            AiStrategyProfileId normalProfile,
            bool recoveryMatch)
        {
            return recoveryMatch
                ? AiRankProfileMapping.OneStepEasier(normalProfile)
                : normalProfile;
        }

        public static int UpdateDefeatStreak(int current, bool isNormalResult, bool playerWon)
        {
            if (!isNormalResult) return Math.Max(0, current);
            return playerWon ? 0 : Math.Max(0, current) + 1;
        }
    }
}
