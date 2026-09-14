using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public enum ForgekeepersGiftState
{
    Unavailable,
    Cooldown,
    Available,
    VerificationPending,
    Granted
}

public sealed class ForgekeepersGiftStatus
{
    public string RunId;
    public string ClaimId;
    public bool Equipped;
    public ForgekeepersGiftState State;
    public bool CanOpen;
    public int OpportunityIndex;
    public string NextAvailableAt;
    public int RemainingSeconds;
    public int RewardCount;
    public IList<string> UnitRuntimeIds = new List<string>();
}

public sealed class ForgekeepersGiftIntent
{
    public string ClaimId;
    public string RunId;
    public int OpportunityIndex;
    public string PlacementId;
    public string AdCustomData;
    public string ExpiresAt;
    public string NextAvailableAt;
}

public sealed class ForgekeepersGiftClaimResult
{
    public string ClaimId;
    public string RunId;
    public int OpportunityIndex;
    public ForgekeepersGiftState State;
    public string RewardType;
    public int RewardCount;
    public IList<string> UnitRuntimeIds = new List<string>();
    public bool Applied;
    public bool Replayed;
    public string LedgerReference;
}

public interface IForgekeepersGiftGateway
{
    Task<ForgekeepersGiftStatus> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken);

    Task<ForgekeepersGiftIntent> CreateIntentAsync(
        string runId,
        string clientEventId,
        string platform,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<ForgekeepersGiftClaimResult> ClaimAsync(
        string runId,
        string claimId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
