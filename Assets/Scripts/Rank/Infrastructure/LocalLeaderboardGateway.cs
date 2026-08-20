using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Development weekly/monthly leaderboard. Production can replace it with a Go unary gateway.
/// </summary>
public sealed class LocalLeaderboardGateway : ILeaderboardGateway
{
    private readonly IPlayerRankGateway rankGateway;
    private readonly LocalLeaderboardPeriodStore periodStore;

    public LocalLeaderboardGateway(
        IPlayerRankGateway rankGateway,
        LocalLeaderboardPeriodStore periodStore)
    {
        this.rankGateway = rankGateway ?? throw new ArgumentNullException(nameof(rankGateway));
        this.periodStore = periodStore ?? throw new ArgumentNullException(nameof(periodStore));
    }

    public async Task<LeaderboardResult> GetLeaderboardAsync(
        string playerId,
        LeaderboardPeriodType periodType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LeaderboardPeriod period = LeaderboardPeriodResolver.Resolve(
            periodType,
            DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(playerId))
        {
            PlayerRankState currentRank = await rankGateway.GetRankAsync(
                playerId,
                cancellationToken);
            periodStore.Upsert(period, new LeaderboardPlayer
            {
                PlayerId = playerId,
                DisplayName = "You",
                RankLevel = currentRank.Level,
                Division = currentRank.Division,
                CurrentStars = currentRank.CurrentStars,
                TotalRankStars = currentRank.TotalRankStars,
                ReachedStateAtUnixMilliseconds = currentRank.ReachedStateAtUnixMilliseconds
            });
        }

        List<LeaderboardPlayer> sorted = LeaderboardRankingRules.Sort(
            periodStore.GetPlayers(period));
        LeaderboardPlayer localPlayer = null;
        int localPosition = 0;
        for (int index = 0; index < sorted.Count; index++)
        {
            if (!string.Equals(sorted[index].PlayerId, playerId, StringComparison.Ordinal)) continue;
            localPlayer = sorted[index];
            localPosition = index + 1;
            break;
        }

        return new LeaderboardResult
        {
            Period = period,
            Players = sorted,
            LocalPlayer = localPlayer,
            LocalPlayerPosition = localPosition
        };
    }
}
