using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
public sealed class LeaderboardPlayer
{
    public string PlayerId;
    public string DisplayName;
    public int RankLevel;
    public int Division;
    public int CurrentStars;
    public long TotalRankStars;
    public long ReachedRankAtUnixMilliseconds;
}

/// <summary>
/// Leaderboard unary-call boundary. Replace the local implementation with the Go server adapter.
/// </summary>
public interface ILeaderboardGateway
{
    Task<IReadOnlyList<LeaderboardPlayer>> GetLeaderboardAsync(
        string playerId,
        CancellationToken cancellationToken);
}
