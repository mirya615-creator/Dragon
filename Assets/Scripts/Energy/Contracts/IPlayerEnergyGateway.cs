using System;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
public sealed class PlayerEnergyState
{
    public int Current;
    public int Maximum;
    public long NextRecoveryUnixTime;
}

public sealed class EnergyConsumeResult
{
    public bool Succeeded;
    public PlayerEnergyState State;
}

public sealed class DailyRewardStatus
{
    public int ClaimsUsed;
    public int DailyLimit;
    public bool CanClaim;
}

public sealed class RewardedAdEnergyClaimResult
{
    public bool Succeeded;
    public bool LimitReached;
    public int ClaimsUsed;
    public int DailyLimit;
    public PlayerEnergyState State;
}

/// <summary>
/// Player energy boundary. A server-backed unary-call implementation can replace the local gateway.
/// </summary>
public interface IPlayerEnergyGateway
{
    Task<PlayerEnergyState> GetEnergyAsync(string playerId, CancellationToken cancellationToken);

    Task<EnergyConsumeResult> ConsumeEnergyAsync(
        string playerId,
        int amount,
        string requestId,
        CancellationToken cancellationToken);

    Task<PlayerEnergyState> GrantEnergyAsync(
        string playerId,
        int amount,
        string rewardTransactionId,
        CancellationToken cancellationToken);

    Task<DailyRewardStatus> GetRewardedAdStatusAsync(
        string playerId,
        int dailyLimit,
        CancellationToken cancellationToken);

    Task<RewardedAdEnergyClaimResult> ClaimRewardedAdEnergyAsync(
        string playerId,
        int amount,
        int dailyLimit,
        string rewardTransactionId,
        CancellationToken cancellationToken);
}
