using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public enum LeaderboardPeriodType
{
    Weekly,
    Monthly
}

[Serializable]
public sealed class LeaderboardPeriod
{
    public LeaderboardPeriodType Type;
    public string PeriodKey;
    public long StartsAtUnixMilliseconds;
    public long EndsAtUnixMilliseconds;
}

[Serializable]
public sealed class LeaderboardPlayer
{
    public string PlayerId;
    public string DisplayName;
    public int RankLevel;
    public int Division;
    public int CurrentStars;
    public long TotalRankStars;
    public long ReachedStateAtUnixMilliseconds;
}

public sealed class LeaderboardResult
{
    public LeaderboardPeriod Period;
    public IReadOnlyList<LeaderboardPlayer> Players;
    public LeaderboardPlayer LocalPlayer;
    public int LocalPlayerPosition;
}

/// <summary>
/// Leaderboard unary-call boundary. Replace the local implementation with the Go server adapter.
/// </summary>
public interface ILeaderboardGateway
{
    Task<LeaderboardResult> GetLeaderboardAsync(
        string playerId,
        LeaderboardPeriodType periodType,
        CancellationToken cancellationToken);
}
