using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
internal sealed class GoRunForgePickIntentRequest
{
    public string ad_point_id;
    public string client_event_id;
    public string platform;
}

[Serializable]
internal sealed class GoRunForgePickIntentResponse
{
    public string claim_id;
    public string run_id;
    public string ad_point_id;
    public string placement_id;
    public string ad_custom_data;
    public string expires_at;
    public bool already_claimed;
}

[Serializable]
internal sealed class GoRunForgePickClaimRequest
{
    public string claim_id;
}

[Serializable]
internal sealed class GoRunForgePickStatusResponse
{
    public string claim_id;
    public string run_id;
    public string status;
    public bool can_claim;
    public string reward_type;
    public int reward_count;
    public List<string> unit_runtime_ids;
    public bool applied;
    public bool replayed;
    public string ledger_reference;
}

public sealed class GoRunForgePickGateway : IRunForgePickGateway
{
    private const string AdPointId = "run_forge_pick";
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;

    internal GoRunForgePickGateway(IUnaryTransport transport, GoApiContextFactory contexts)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    public async Task<RunForgePickStatus> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        Validate(runId, nameof(runId));
        GoRunForgePickStatusResponse response =
            await transport.SendAsync<object, GoRunForgePickStatusResponse>(
                "GET",
                BuildPath(runId),
                null,
                contexts.Create(),
                cancellationToken);
        ValidateStatusResponse(response, runId);
        return ToStatus(response);
    }

    public async Task<RunForgePickIntent> CreateIntentAsync(
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
        var request = new GoRunForgePickIntentRequest
        {
            ad_point_id = AdPointId,
            client_event_id = clientEventId,
            platform = platform
        };
        GoRunForgePickIntentResponse response =
            await transport.SendAsync<GoRunForgePickIntentRequest, GoRunForgePickIntentResponse>(
                "POST",
                BuildPath(runId) + "/intents",
                request,
                contexts.Create(idempotencyKey),
                cancellationToken);
        if (response == null || string.IsNullOrWhiteSpace(response.claim_id) ||
            !string.Equals(response.run_id, runId, StringComparison.Ordinal) ||
            (!response.already_claimed &&
             (string.IsNullOrWhiteSpace(response.placement_id) ||
              string.IsNullOrWhiteSpace(response.ad_custom_data))))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Run Forge Pick intent response is incomplete.");
        }
        return new RunForgePickIntent
        {
            ClaimId = response.claim_id,
            RunId = response.run_id,
            AdPointId = response.ad_point_id,
            PlacementId = response.placement_id,
            AdCustomData = response.ad_custom_data,
            ExpiresAt = response.expires_at,
            AlreadyClaimed = response.already_claimed
        };
    }

    public async Task<RunForgePickClaimResult> ClaimAsync(
        string runId,
        string claimId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Validate(runId, nameof(runId));
        Validate(claimId, nameof(claimId));
        Validate(idempotencyKey, nameof(idempotencyKey));
        GoRunForgePickStatusResponse response =
            await transport.SendAsync<GoRunForgePickClaimRequest, GoRunForgePickStatusResponse>(
                "POST",
                BuildPath(runId) + "/claim",
                new GoRunForgePickClaimRequest { claim_id = claimId },
                contexts.Create(idempotencyKey),
                cancellationToken);
        ValidateStatusResponse(response, runId);
        return new RunForgePickClaimResult
        {
            ClaimId = response.claim_id,
            RunId = response.run_id,
            State = ParseState(response.status, response.can_claim),
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
               "/ad-rewards/forge-pick";
    }

    private static RunForgePickStatus ToStatus(GoRunForgePickStatusResponse response)
    {
        return new RunForgePickStatus
        {
            RunId = response.run_id,
            State = ParseState(response.status, response.can_claim),
            CanClaim = response.can_claim,
            RewardCount = response.reward_count,
            UnitRuntimeIds = response.unit_runtime_ids ?? new List<string>()
        };
    }

    private static RunForgePickState ParseState(string status, bool canClaim)
    {
        switch ((status ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "granted": return RunForgePickState.Granted;
            case "pending":
            case "verified":
            case "verification_pending": return RunForgePickState.VerificationPending;
            case "available": return RunForgePickState.Available;
            default: return canClaim ? RunForgePickState.Available : RunForgePickState.Unavailable;
        }
    }

    private static void ValidateStatusResponse(
        GoRunForgePickStatusResponse response,
        string expectedRunId)
    {
        if (response == null ||
            !string.Equals(response.run_id, expectedRunId, StringComparison.Ordinal))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Run Forge Pick status response is incomplete.");
        }
        if (ParseState(response.status, response.can_claim) == RunForgePickState.Granted &&
            (response.reward_count != 2 || response.unit_runtime_ids == null ||
             response.unit_runtime_ids.Count != 2 ||
             string.IsNullOrWhiteSpace(response.unit_runtime_ids[0]) ||
             string.IsNullOrWhiteSpace(response.unit_runtime_ids[1])))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Granted Run Forge Pick response requires exactly two stable unit IDs.");
        }
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);
    }
}
