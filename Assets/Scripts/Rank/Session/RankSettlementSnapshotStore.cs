public static class RankSettlementSnapshotStore
{
    private static string pendingPlayerId;
    private static RankProgressResult pendingResult;

    public static void Set(string playerId, RankProgressResult result)
    {
        if (string.IsNullOrWhiteSpace(playerId) || result?.State == null) return;
        pendingPlayerId = playerId;
        pendingResult = result;
    }

    public static RankProgressResult Peek(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || playerId != pendingPlayerId) return null;
        return pendingResult;
    }

    public static void Clear(string playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId) && playerId != pendingPlayerId) return;
        pendingPlayerId = null;
        pendingResult = null;
    }

    public static void Clear()
    {
        pendingPlayerId = null;
        pendingResult = null;
    }
}
