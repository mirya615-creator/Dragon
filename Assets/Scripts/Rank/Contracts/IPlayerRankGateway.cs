using System;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
public sealed class PlayerRankState
{
    public int Level;
    public string RankName;
    public int Division;
    public int CurrentStars;
    public int RequiredStars;
    public long TotalRankStars;
}

public sealed class RankProgressResult
{
    public PlayerRankState State;
    public PlayerRankState PromotionFromState;
    public bool Promoted;
}

/// <summary>
/// Player rank boundary. A server-backed unary-call implementation can replace the local gateway.
/// </summary>
public interface IPlayerRankGateway
{
    Task<PlayerRankState> GetRankAsync(string playerId, CancellationToken cancellationToken);

    Task<RankProgressResult> RecordVictoryAsync(
        string playerId,
        string matchId,
        CancellationToken cancellationToken);

    Task<RankProgressResult> RecordDefeatAsync(
        string playerId,
        string matchId,
        CancellationToken cancellationToken);
}
