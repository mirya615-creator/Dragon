public static class RankPromotionStore
{
    private static string pendingPlayerId;
    private static RankProgressResult pendingResult;

    public static void Set(string playerId, RankProgressResult result)
    {
        if (string.IsNullOrEmpty(playerId) || result == null || !result.Promoted ||
            result.PromotionFromState == null)
        {
            return;
        }

        pendingPlayerId = playerId;
        pendingResult = result;
    }

    public static RankProgressResult Consume(string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || playerId != pendingPlayerId) return null;
        RankProgressResult result = pendingResult;
        pendingPlayerId = null;
        pendingResult = null;
        return result;
    }

    public static void Clear()
    {
        pendingPlayerId = null;
        pendingResult = null;
    }
}
