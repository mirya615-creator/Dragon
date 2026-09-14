using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class LocalForgekeepersGiftGateway : IForgekeepersGiftGateway
{
    private const int CooldownSeconds = 90;

    private sealed class RunState
    {
        public DateTime NextAvailableAtUtc;
        public int NextOpportunityIndex = 1;
        public readonly Dictionary<string, ForgekeepersGiftIntent> Intents =
            new Dictionary<string, ForgekeepersGiftIntent>(StringComparer.Ordinal);
        public readonly Dictionary<string, ForgekeepersGiftClaimResult> Grants =
            new Dictionary<string, ForgekeepersGiftClaimResult>(StringComparer.Ordinal);
    }

    private readonly Dictionary<string, RunState> runs =
        new Dictionary<string, RunState>(StringComparer.Ordinal);

    public Task<ForgekeepersGiftStatus> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        Validate(runId, nameof(runId));
        cancellationToken.ThrowIfCancellationRequested();
        RunState state = GetRun(runId);
        int remaining = Math.Max(
            0,
            (int)Math.Ceiling((state.NextAvailableAtUtc - DateTime.UtcNow).TotalSeconds));
        return Task.FromResult(new ForgekeepersGiftStatus
        {
            RunId = runId,
            Equipped = true,
            State = remaining > 0
                ? ForgekeepersGiftState.Cooldown
                : ForgekeepersGiftState.Available,
            CanOpen = remaining == 0,
            OpportunityIndex = state.NextOpportunityIndex,
            NextAvailableAt = state.NextAvailableAtUtc.ToString("O"),
            RemainingSeconds = remaining,
            RewardCount = 1
        });
    }

    public Task<ForgekeepersGiftIntent> CreateIntentAsync(
        string runId,
        string clientEventId,
        string platform,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Validate(runId, nameof(runId));
        Validate(clientEventId, nameof(clientEventId));
        Validate(platform, nameof(platform));
        Validate(idempotencyKey, nameof(idempotencyKey));
        cancellationToken.ThrowIfCancellationRequested();
        RunState state = GetRun(runId);
        if (state.Intents.TryGetValue(idempotencyKey, out ForgekeepersGiftIntent replay))
            return Task.FromResult(Clone(replay));
        if (DateTime.UtcNow < state.NextAvailableAtUtc)
            throw new ClientServiceException("REWARD_NOT_READY", "Local reward is cooling down.");

        string claimId = Guid.NewGuid().ToString("D");
        int opportunity = state.NextOpportunityIndex++;
        state.NextAvailableAtUtc = DateTime.UtcNow.AddSeconds(CooldownSeconds);
        var intent = new ForgekeepersGiftIntent
        {
            ClaimId = claimId,
            RunId = runId,
            OpportunityIndex = opportunity,
            PlacementId = "item_forgekeepers_gift",
            AdCustomData = claimId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10).ToString("O"),
            NextAvailableAt = state.NextAvailableAtUtc.ToString("O")
        };
        state.Intents[idempotencyKey] = intent;
        return Task.FromResult(Clone(intent));
    }

    public Task<ForgekeepersGiftClaimResult> ClaimAsync(
        string runId,
        string claimId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Validate(runId, nameof(runId));
        Validate(claimId, nameof(claimId));
        Validate(idempotencyKey, nameof(idempotencyKey));
        cancellationToken.ThrowIfCancellationRequested();
        RunState state = GetRun(runId);
        if (state.Grants.TryGetValue(claimId, out ForgekeepersGiftClaimResult replay))
            return Task.FromResult(Clone(replay, false, true));

        ForgekeepersGiftIntent intent = null;
        foreach (ForgekeepersGiftIntent candidate in state.Intents.Values)
        {
            if (candidate.ClaimId != claimId) continue;
            intent = candidate;
            break;
        }
        if (intent == null)
            throw new ClientServiceException("AD_VERIFICATION_FAILED", "Unknown local ad intent.");

        var grant = new ForgekeepersGiftClaimResult
        {
            ClaimId = claimId,
            RunId = runId,
            OpportunityIndex = intent.OpportunityIndex,
            State = ForgekeepersGiftState.Granted,
            RewardType = "forge_pick",
            RewardCount = 1,
            UnitRuntimeIds = new List<string> { "run-reward:" + claimId + ":1" },
            Applied = true,
            LedgerReference = "local:" + claimId
        };
        state.Grants[claimId] = grant;
        return Task.FromResult(Clone(grant, true, false));
    }

    private RunState GetRun(string runId)
    {
        if (runs.TryGetValue(runId, out RunState state)) return state;
        state = new RunState { NextAvailableAtUtc = DateTime.UtcNow.AddSeconds(CooldownSeconds) };
        runs[runId] = state;
        return state;
    }

    private static ForgekeepersGiftIntent Clone(ForgekeepersGiftIntent value)
    {
        return new ForgekeepersGiftIntent
        {
            ClaimId = value.ClaimId,
            RunId = value.RunId,
            OpportunityIndex = value.OpportunityIndex,
            PlacementId = value.PlacementId,
            AdCustomData = value.AdCustomData,
            ExpiresAt = value.ExpiresAt,
            NextAvailableAt = value.NextAvailableAt
        };
    }

    private static ForgekeepersGiftClaimResult Clone(
        ForgekeepersGiftClaimResult value,
        bool applied,
        bool replayed)
    {
        return new ForgekeepersGiftClaimResult
        {
            ClaimId = value.ClaimId,
            RunId = value.RunId,
            OpportunityIndex = value.OpportunityIndex,
            State = value.State,
            RewardType = value.RewardType,
            RewardCount = value.RewardCount,
            UnitRuntimeIds = new List<string>(value.UnitRuntimeIds),
            Applied = applied,
            Replayed = replayed,
            LedgerReference = value.LedgerReference
        };
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);
    }
}
