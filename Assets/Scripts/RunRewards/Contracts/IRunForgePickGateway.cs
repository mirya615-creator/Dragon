using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public enum RunForgePickState
{
    Unavailable,
    Available,
    VerificationPending,
    Granted
}

public sealed class RunForgePickStatus
{
    public string RunId;
    public RunForgePickState State;
    public bool CanClaim;
    public int RewardCount;
    public List<string> UnitRuntimeIds = new List<string>();
}

public sealed class RunForgePickIntent
{
    public string ClaimId;
    public string RunId;
    public string AdPointId;
    public string PlacementId;
    public string AdCustomData;
    public string ExpiresAt;
    public bool AlreadyClaimed;
}

public sealed class RunForgePickClaimResult
{
    public string ClaimId;
    public string RunId;
    public RunForgePickState State;
    public string RewardType;
    public int RewardCount;
    public List<string> UnitRuntimeIds = new List<string>();
    public bool Applied;
    public bool Replayed;
    public string LedgerReference;
}

public interface IRunForgePickGateway
{
    Task<RunForgePickStatus> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken);

    Task<RunForgePickIntent> CreateIntentAsync(
        string runId,
        string clientEventId,
        string platform,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<RunForgePickClaimResult> ClaimAsync(
        string runId,
        string claimId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
