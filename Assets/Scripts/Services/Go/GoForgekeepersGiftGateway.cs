using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
internal sealed class GoForgekeepersGiftIntentRequest
{
    public string ad_point_id;
    public string client_event_id;
    public string platform;
}

[Serializable]
internal sealed class GoForgekeepersGiftIntentResponse
{
    public string claim_id;
    public string run_id;
    public int opportunity_index;
    public string placement_id;
    public string ad_custom_data;
    public string expires_at;
    public string next_available_at;
}

[Serializable]
internal sealed class GoForgekeepersGiftClaimRequest
{
    public string claim_id;
}

[Serializable]
internal sealed class GoForgekeepersGiftStatusResponse
{
    public string claim_id;
    public string run_id;
    public bool equipped;
    public string status;
    public bool can_open;
    public int opportunity_index;
    public string next_available_at;
    public int remaining_seconds;
    public string reward_type;
    public int reward_count;
    public List<string> unit_runtime_ids;
    public bool applied;
    public bool replayed;
    public string ledger_reference;
}

public sealed class GoForgekeepersGiftGateway : IForgekeepersGiftGateway
{
    private const string AdPointId = "item_forgekeepers_gift";
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;

    internal GoForgekeepersGiftGateway(
        IUnaryTransport transport,
        GoApiContextFactory contexts)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    public async Task<ForgekeepersGiftStatus> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        Validate(runId, nameof(runId));
        GoForgekeepersGiftStatusResponse response =
            await transport.SendAsync<object, GoForgekeepersGiftStatusResponse>(
                "GET",
                BuildPath(runId),
                null,
                contexts.Create(),
                cancellationToken);
        ValidateStatusResponse(response, runId);
        return new ForgekeepersGiftStatus
        {
            RunId = response.run_id,
            ClaimId = response.claim_id,
            Equipped = response.equipped,
            State = ParseState(response.status, response.can_open),
            CanOpen = response.can_open,
            OpportunityIndex = response.opportunity_index,
            NextAvailableAt = response.next_available_at,
            RemainingSeconds = Math.Max(0, response.remaining_seconds),
            RewardCount = response.reward_count,
            UnitRuntimeIds = response.unit_runtime_ids ?? new List<string>()
        };
    }

    public async Task<ForgekeepersGiftIntent> CreateIntentAsync(
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
        var request = new GoForgekeepersGiftIntentRequest
        {
            ad_point_id = AdPointId,
            client_event_id = clientEventId,
            platform = platform
        };
        GoForgekeepersGiftIntentResponse response =
            await transport.SendAsync<GoForgekeepersGiftIntentRequest,
                GoForgekeepersGiftIntentResponse>(
                "POST",
                BuildPath(runId) + "/intents",
                request,
                contexts.Create(idempotencyKey),
                cancellationToken);
        if (response == null ||
            string.IsNullOrWhiteSpace(response.claim_id) ||
            !string.Equals(response.run_id, runId, StringComparison.Ordinal) ||
            response.opportunity_index <= 0 ||
            string.IsNullOrWhiteSpace(response.placement_id) ||
            string.IsNullOrWhiteSpace(response.ad_custom_data) ||
            string.IsNullOrWhiteSpace(response.next_available_at))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Forgekeeper's Gift intent response is incomplete.");
        }
        return new ForgekeepersGiftIntent
        {
            ClaimId = response.claim_id,
            RunId = response.run_id,
            OpportunityIndex = response.opportunity_index,
            PlacementId = response.placement_id,
            AdCustomData = response.ad_custom_data,
            ExpiresAt = response.expires_at,
            NextAvailableAt = response.next_available_at
        };
    }

    public async Task<ForgekeepersGiftClaimResult> ClaimAsync(
        string runId,
        string claimId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Validate(runId, nameof(runId));
        Validate(claimId, nameof(claimId));
        Validate(idempotencyKey, nameof(idempotencyKey));
        GoForgekeepersGiftStatusResponse response =
            await transport.SendAsync<GoForgekeepersGiftClaimRequest,
                GoForgekeepersGiftStatusResponse>(
                "POST",
                BuildPath(runId) + "/claim",
                new GoForgekeepersGiftClaimRequest { claim_id = claimId },
                contexts.Create(idempotencyKey),
                cancellationToken);
        ValidateStatusResponse(response, runId);
        return new ForgekeepersGiftClaimResult
        {
            ClaimId = response.claim_id,
            RunId = response.run_id,
            OpportunityIndex = response.opportunity_index,
            State = ParseState(response.status, response.can_open),
            RewardType = response.reward_type,
            RewardCount = response.reward_count,
            UnitRuntimeIds = response.unit_runtime_ids ?? new List<string>(),
            Applied = response.applied,
            Replayed = response.replayed,
            LedgerReference = response.ledger_reference
        };
    }

    private static string BuildPath(string runId)
    {
        return "/v1/runs/" + Uri.EscapeDataString(runId) +
               "/item-rewards/forgekeepers-gift";
    }

    private static ForgekeepersGiftState ParseState(string status, bool canOpen)
    {
        switch ((status ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "granted": return ForgekeepersGiftState.Granted;
            case "pending":
            case "verified":
            case "verification_pending": return ForgekeepersGiftState.VerificationPending;
            case "available": return ForgekeepersGiftState.Available;
            case "cooldown": return ForgekeepersGiftState.Cooldown;
            default: return canOpen
                ? ForgekeepersGiftState.Available
                : ForgekeepersGiftState.Unavailable;
        }
    }

    private static void ValidateStatusResponse(
        GoForgekeepersGiftStatusResponse response,
        string expectedRunId)
    {
        if (response == null ||
            !string.Equals(response.run_id, expectedRunId, StringComparison.Ordinal))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Forgekeeper's Gift status response is incomplete.");
        }
        if (ParseState(response.status, response.can_open) == ForgekeepersGiftState.Granted &&
            (string.IsNullOrWhiteSpace(response.claim_id) ||
             response.reward_count != 1 ||
             response.unit_runtime_ids == null ||
             response.unit_runtime_ids.Count != 1 ||
             string.IsNullOrWhiteSpace(response.unit_runtime_ids[0])))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Granted Forgekeeper's Gift requires exactly one stable unit ID.");
        }
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);
    }
}
