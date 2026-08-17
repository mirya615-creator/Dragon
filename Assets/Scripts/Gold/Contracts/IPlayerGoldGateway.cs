using System;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
public sealed class PlayerGoldState
{
    public long Balance;
}

public enum MatchOutcome
{
    Victory,
    Defeat
}

public enum GoldClaimType
{
    Standard,
    RewardedAd
}

public sealed class GoldSettlementResult
{
    public long Reward;
    public long Balance;
    public bool Applied;
}

/// <summary>
/// Player gold boundary. A server-backed unary-call implementation can replace the local gateway.
/// </summary>
public interface IPlayerGoldGateway
{
    Task<PlayerGoldState> GetGoldAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task<GoldSettlementResult> SettleMatchAsync(
        string playerId,
        string matchId,
        MatchOutcome outcome,
        GoldClaimType claimType,
        string adVerificationId,
        CancellationToken cancellationToken);
}
