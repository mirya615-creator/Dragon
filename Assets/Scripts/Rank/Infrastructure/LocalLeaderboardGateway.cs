using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Development leaderboard. Production can replace this class with a unary Go-server gateway.
/// </summary>
public sealed class LocalLeaderboardGateway : ILeaderboardGateway
{
    private readonly IPlayerRankGateway rankGateway = new LocalPlayerRankGateway();

    public async Task<IReadOnlyList<LeaderboardPlayer>> GetLeaderboardAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var players = new List<LeaderboardPlayer>
        {
            CreatePlayer("training-aria", "Aria", 137, 1000),
            CreatePlayer("training-borin", "Borin", 119, 2000),
            CreatePlayer("training-cyra", "Cyra", 104, 3000),
            CreatePlayer("training-doran", "Doran", 91, 4000),
            CreatePlayer("training-elin", "Elin", 67, 5000),
            CreatePlayer("training-fenn", "Fenn", 28, 6000),
            CreatePlayer("training-gale", "Gale", 112, 7000),
            CreatePlayer("training-hara", "Hara", 109, 8000),
            CreatePlayer("training-ivo", "Ivo", 102, 9000),
            CreatePlayer("training-juna", "Juna", 83, 10000),
            CreatePlayer("training-kael", "Kael", 51, 11000),
            CreatePlayer("training-lyra", "Lyra", 12, 12000)
        };

        if (!string.IsNullOrWhiteSpace(playerId))
        {
            PlayerRankState currentRank = await rankGateway.GetRankAsync(playerId, cancellationToken);
            players.RemoveAt(players.Count - 1);
            players.Add(new LeaderboardPlayer
            {
                PlayerId = playerId,
                DisplayName = "You",
                RankLevel = currentRank.Level,
                Division = currentRank.Division,
                CurrentStars = currentRank.CurrentStars,
                TotalRankStars = currentRank.TotalRankStars,
                ReachedRankAtUnixMilliseconds = 13000
            });
        }

        return players;
    }

    private static LeaderboardPlayer CreatePlayer(
        string playerId,
        string displayName,
        long totalStars,
        long reachedAt)
    {
        PlayerRankState rank = RankProgressionRules.Calculate(totalStars);
        return new LeaderboardPlayer
        {
            PlayerId = playerId,
            DisplayName = displayName,
            RankLevel = rank.Level,
            Division = rank.Division,
            CurrentStars = rank.CurrentStars,
            TotalRankStars = rank.TotalRankStars,
            ReachedRankAtUnixMilliseconds = reachedAt
        };
    }
}
