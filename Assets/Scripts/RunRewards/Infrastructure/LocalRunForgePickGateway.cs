using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class LocalRunForgePickGateway : IRunForgePickGateway
{
    private const string AdPointId = "run_forge_pick";
    private readonly Dictionary<string, RunForgePickClaimResult> grantsByRun =
        new Dictionary<string, RunForgePickClaimResult>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> claimRunIds =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, RunForgePickIntent> intentsByIdempotencyKey =
        new Dictionary<string, RunForgePickIntent>(StringComparer.Ordinal);

    public Task<RunForgePickStatus> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        ValidateRunId(runId);
        cancellationToken.ThrowIfCancellationRequested();
        if (grantsByRun.TryGetValue(runId, out RunForgePickClaimResult granted))
        {
            return Task.FromResult(new RunForgePickStatus
            {
                RunId = runId,
                State = RunForgePickState.Granted,
                CanClaim = false,
                RewardCount = granted.RewardCount,
                UnitRuntimeIds = new List<string>(granted.UnitRuntimeIds)
            });
        }

        return Task.FromResult(new RunForgePickStatus
        {
            RunId = runId,
            State = RunForgePickState.Available,
            CanClaim = true,
            RewardCount = 2
        });
    }

    public Task<RunForgePickIntent> CreateIntentAsync(
        string runId,
        string clientEventId,
        string platform,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateRunId(runId);
        Validate(clientEventId, nameof(clientEventId));
        Validate(platform, nameof(platform));
        Validate(idempotencyKey, nameof(idempotencyKey));
        cancellationToken.ThrowIfCancellationRequested();

        if (intentsByIdempotencyKey.TryGetValue(
                runId + "|" + idempotencyKey,
                out RunForgePickIntent existingIntent))
        {
            return Task.FromResult(Clone(existingIntent));
        }

        bool alreadyClaimed = grantsByRun.ContainsKey(runId);
        string claimId = Guid.NewGuid().ToString("D");
        if (!alreadyClaimed) claimRunIds[claimId] = runId;
        var intent = new RunForgePickIntent
        {
            ClaimId = claimId,
            RunId = runId,
            AdPointId = AdPointId,
            PlacementId = AdPointId,
            AdCustomData = claimId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10).ToString("O"),
            AlreadyClaimed = alreadyClaimed
        };
        intentsByIdempotencyKey[runId + "|" + idempotencyKey] = intent;
        return Task.FromResult(Clone(intent));
    }

    public Task<RunForgePickClaimResult> ClaimAsync(
        string runId,
        string claimId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateRunId(runId);
        Validate(claimId, nameof(claimId));
        Validate(idempotencyKey, nameof(idempotencyKey));
        cancellationToken.ThrowIfCancellationRequested();

        if (grantsByRun.TryGetValue(runId, out RunForgePickClaimResult existing))
        {
            return Task.FromResult(Clone(existing, false, true));
        }
        if (!claimRunIds.TryGetValue(claimId, out string intentRunId) ||
            !string.Equals(intentRunId, runId, StringComparison.Ordinal))
        {
            throw new ClientServiceException("AD_VERIFICATION_FAILED", "Unknown local ad intent.");
        }

        var result = new RunForgePickClaimResult
        {
            ClaimId = claimId,
            RunId = runId,
            State = RunForgePickState.Granted,
            RewardType = "forge_pick",
            RewardCount = 2,
            Applied = true,
            Replayed = false,
            LedgerReference = "local:" + claimId,
            UnitRuntimeIds = new List<string>
            {
                "run-reward:" + claimId + ":1",
                "run-reward:" + claimId + ":2"
            }
        };
        grantsByRun[runId] = result;
        return Task.FromResult(Clone(result, true, false));
    }

    private static RunForgePickClaimResult Clone(
        RunForgePickClaimResult source,
        bool applied,
        bool replayed)
    {
        return new RunForgePickClaimResult
        {
            ClaimId = source.ClaimId,
            RunId = source.RunId,
            State = source.State,
            RewardType = source.RewardType,
            RewardCount = source.RewardCount,
            UnitRuntimeIds = new List<string>(source.UnitRuntimeIds),
            Applied = applied,
            Replayed = replayed,
            LedgerReference = source.LedgerReference
        };
    }

    private static RunForgePickIntent Clone(RunForgePickIntent source)
    {
        return new RunForgePickIntent
        {
            ClaimId = source.ClaimId,
            RunId = source.RunId,
            AdPointId = source.AdPointId,
            PlacementId = source.PlacementId,
            AdCustomData = source.AdCustomData,
            ExpiresAt = source.ExpiresAt,
            AlreadyClaimed = source.AlreadyClaimed
        };
    }

    private static void ValidateRunId(string runId)
    {
        Validate(runId, nameof(runId));
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);
    }
}
