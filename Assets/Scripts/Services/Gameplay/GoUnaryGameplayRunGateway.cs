using System;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Core;
using DragonBound.Services;
using GameShared.Random;

public sealed class GoUnaryGameplayRunGateway : IGameplayRunGateway, IActiveRunRecoveryGateway
{
    public const string StartPath = "/v1/runs/start";
    internal const string FinishPathSuffix = "/finish";
    internal const string QuitPathSuffix = "/quit";
    internal const long VictoryGoldReward = 20;
    internal const long DefeatGoldReward = 10;
    internal const int MinimumRunDurationSeconds = 30;

    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contextFactory;
    private readonly GoServerState state;
    private readonly ClientServiceConfig config;
    private readonly LocalGameplayRunGateway localRecruitment = new LocalGameplayRunGateway();
    private readonly SemaphoreSlim settlementGate = new SemaphoreSlim(1, 1);

    internal GoUnaryGameplayRunGateway(
        IUnaryTransport transport,
        GoApiContextFactory contextFactory,
        GoServerState state,
        ClientServiceConfig config)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<StartGameplayRunResult> StartRunAsync(
        StartGameplayRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        string playerId = string.IsNullOrWhiteSpace(request.PlayerId)
            ? state.Bootstrap?.player?.player_id
            : request.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ClientServiceException(
                "PLAYER_SESSION_REQUIRED",
                "An authenticated player is required before starting an online Run.");
        string idempotencyKey = string.IsNullOrWhiteSpace(request.ClientRunNonce)
            ? Guid.NewGuid().ToString("N")
            : request.ClientRunNonce;
        var wireRequest = new GoRunStartRequest
        {
            stage_id = string.IsNullOrWhiteSpace(config.DefaultStageId)
                ? request.GameMode
                : config.DefaultStageId,
            client_version = string.IsNullOrWhiteSpace(request.ClientVersion)
                ? config.ClientVersion
                : request.ClientVersion,
            content_version = string.IsNullOrWhiteSpace(request.ContentVersion)
                ? config.ContentVersion
                : request.ContentVersion,
            requested_config_version = config.ConfigVersion,
            device_session_id = idempotencyKey
        };
        GoRunStartResponse response = await transport.SendAsync<GoRunStartRequest, GoRunStartResponse>(
            "POST",
            StartPath,
            wireRequest,
            contextFactory.Create(idempotencyKey),
            cancellationToken);
        if (response == null || string.IsNullOrWhiteSpace(response.run_id))
            throw new ClientServiceException("INVALID_RESPONSE", "Run start response is incomplete.");

        // The server has already committed the Run at this point. Persist its identity
        // before validating the payload so a rejected response can still be quit/recovered.
        ActiveRunStore.Save(
            playerId,
            response.run_id,
            response.started_at,
            response.expires_at);
        state.StoreTicket(
            response.run_id,
            response.ticket_signature,
            response.run_nonce,
            response.started_at);
        int runSeed;
        int playerRecruitSeed;
        int aiRecruitSeed;
        int waveRandomSeed;
        try
        {
            runSeed = ParseSeed(response.run_seed);
            ParseServerTimestamp(response.started_at, "started_at");
            string initialEventHash = InitialHash(response.run_nonce);
            state.InitializeEventChain(response.run_id, initialEventHash);
            ActiveRunStore.InitializeEventChain(response.run_id, initialEventHash);
            ResolveRandomStreams(
                response,
                runSeed,
                out playerRecruitSeed,
                out aiRecruitSeed,
                out waveRandomSeed);
            ValidateStageSnapshot(response);
        }
        catch
        {
            await TryReleaseRejectedStartAsync(response.run_id, CancellationToken.None);
            throw;
        }
        return new StartGameplayRunResult
        {
            RunId = response.run_id,
            RunSeed = runSeed,
            PlayerRecruitSeed = playerRecruitSeed,
            AiRecruitSeed = aiRecruitSeed,
            CombatSeed = SharedRandomProtocolV1.DeriveSeed(runSeed, "combat"),
            RulesVersion = response.event_version,
            StartingResources = 20,
            ReconnectGraceSeconds = 90,
            AfkTimeoutSeconds = 180,
            PlayerRankLevel = Math.Max(1, Math.Min(10, request.PlayerRankLevel)),
            AiProfile = string.Empty,
            AiDecisionSeed = SharedRandomProtocolV1.DeriveSeed(runSeed, "ai.decision"),
            IsRecoveryMatch = false,
            AiAlgorithmVersion = response.rng_version,
            RandomProtocolVersion = SharedRandomProtocolV1.Version,
            WaveRandomSeed = waveRandomSeed
        };
    }

    private async Task TryReleaseRejectedStartAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendQuitAsync(runId, runId + ":quit", cancellationToken);
        }
        catch (Exception exception)
        {
            // Keep ActiveRunStore intact so the lifecycle recovery path can retry.
            UnityEngine.Debug.LogWarning(
                "Run start response was rejected and immediate server quit failed: " +
                exception.Message);
        }
    }

    public Task<RecruitGameplayResult> RecruitAsync(
        RecruitGameplayRequest request,
        CancellationToken cancellationToken)
    {
        // OpenAPI deliberately has no Recruit endpoint. Keep the existing deterministic
        // client algorithm until the shared random-stream protocol is unfrozen.
        return localRecruitment.RecruitAsync(request, cancellationToken);
    }

    public async Task<FinishGameplayRunResult> FinishRunAsync(
        FinishGameplayRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        await settlementGate.WaitAsync(cancellationToken);
        try
        {
            return await FinishRunLockedAsync(request, cancellationToken);
        }
        finally
        {
            settlementGate.Release();
        }
    }

    internal bool HasPendingFinishRequest(string runId)
    {
        return state.TryGetTicket(runId, out GoServerState.RunTicket ticket) &&
               ticket.PendingFinishRequest != null;
    }

    private async Task<FinishGameplayRunResult> FinishRunLockedAsync(
        FinishGameplayRunRequest request,
        CancellationToken cancellationToken)
    {
        if (!state.TryGetTicket(request.RunId, out GoServerState.RunTicket ticket) ||
            string.IsNullOrWhiteSpace(ticket.TicketSignature) ||
            string.IsNullOrWhiteSpace(ticket.RunNonce))
        {
            throw new ClientServiceException(
                "RUN_TICKET_NOT_FOUND",
                "The server run ticket is unavailable; start a new online run.");
        }

        if (request.ProposedResult == ServerMatchResult.Defeat)
        {
            return await QuitRunForSettlementAsync(request, cancellationToken);
        }

        await state.EventChainGate.WaitAsync(cancellationToken);
        try
        {
            ValidateVictoryFinish(request);
            bool reusedSnapshot = ticket.PendingFinishRequest != null;
            string idempotencyKey;
            GoRunFinishRequest wireRequest;
            if (reusedSnapshot)
            {
                idempotencyKey = ticket.PendingFinishIdempotencyKey;
                wireRequest = ticket.PendingFinishRequest;
            }
            else
            {
                idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? request.RunId + ":finish"
                    : request.IdempotencyKey;
                await WaitForMinimumDurationAsync(ticket.StartedAt, cancellationToken);
                int remainingHealth = Math.Max(0, request.RemainingHealth);
                GoRunChainEvent runEnd = CreateRunEndEvent(ticket, request, remainingHealth);
                wireRequest = new GoRunFinishRequest
                {
                    ticket_signature = ticket.TicketSignature,
                    finished_at = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    result = request.ProposedResult == ServerMatchResult.Victory ? "win" : "lose",
                    reached_wave = Math.Max(0, request.ReachedWave),
                    remaining_health = remainingHealth,
                    final_event_sequence = runEnd.sequence,
                    final_event_hash = runEnd.current_hash,
                    critical_random_sequence = ToCanonicalBase64(
                        SharedRandomProtocolV1.EncodeCriticalRandomSequence(
                            request.CriticalRandomSamples))
                };
                wireRequest.event_summary.Add(runEnd);
                ticket.PendingFinishRequest = wireRequest;
                ticket.PendingFinishIdempotencyKey = idempotencyKey;
                ticket.PendingFinishFingerprint = FinishRequestFingerprint(wireRequest);
            }

            UnityEngine.Debug.Log(
                "[RunFinish] Snapshot=" + (reusedSnapshot ? "Reused" : "Created") +
                " RunId=" + request.RunId +
                " ReachedWave=" + wireRequest.reached_wave +
                " IdempotencyHash=" + StableDiagnosticHash(idempotencyKey) +
                " BodyHash=" + ticket.PendingFinishFingerprint);
            GoRunFinishResponse response =
                await transport.SendAsync<GoRunFinishRequest, GoRunFinishResponse>(
                    "POST",
                    RunPath(request.RunId, FinishPathSuffix),
                    wireRequest,
                    contextFactory.Create(idempotencyKey),
                    cancellationToken);
            ValidateVictorySettlement(response, request.RunId);
            state.StoreFinish(response);

            bool resolved = response != null &&
                (response.settlement_status == "Settled" ||
                 response.settlement_status == "ExpiredNoReward" ||
                 response.settlement_status == "QuitNoReward" ||
                 response.settlement_status == "RejectedNoReward");
            bool grantRewards = response?.settlement_status == "Settled";
            if (resolved) ActiveRunStore.ClearIfMatches(request.RunId);
            GameplayRankSettlement rankSettlement = CreateRankSettlement(response?.rank_settlement);
            return new FinishGameplayRunResult
            {
                Accepted = resolved,
                RunId = response?.run_id ?? request.RunId,
                Result = response?.result == "win" ? ServerMatchResult.Victory : ServerMatchResult.Defeat,
                SettlementType = GameplaySettlementType.Normal,
                TerminationReason = request.TerminationReason,
                FaultAttribution = request.FaultAttribution,
                ApplyRank = false,
                ApplySeasonProgress = false,
                CountCompletedRun = false,
                GrantRewards = grantRewards,
                ServerAuthoritativeSettlement = true,
                SettlementStatus = response?.settlement_status ?? string.Empty,
                RejectionCode = response?.rejection_code ?? string.Empty,
                GoldReward = response?.settlement?.total_gold ?? 0,
                RuneFragmentReward = response?.settlement?.rune_fragments ?? 0,
                MerchantEventAvailable = HasMerchantEvent(response),
                MerchantEventId = response?.merchant_event?.event_id ?? string.Empty,
                HasAuthoritativeRankSettlement = rankSettlement?.After != null,
                RankSettlement = rankSettlement
            };
        }
        finally
        {
            state.EventChainGate.Release();
        }
    }

    public async Task QuitActiveRunAsync(
        string playerId,
        string runId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateRecoveryRequest(playerId, runId, idempotencyKey);
        await settlementGate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await SendQuitAsync(runId, idempotencyKey, cancellationToken);
            }
            catch (ClientServiceException exception) when (IsTerminalRecoveryResponse(exception))
            {
                // A previous finish/quit may have committed while its response was lost.
                // These stable responses prove this Run no longer blocks a new start.
                ActiveRunStore.ClearIfMatches(runId);
            }
        }
        finally
        {
            settlementGate.Release();
        }
    }

    public bool TryQuitActiveRunBlocking(
        string playerId,
        string runId,
        string idempotencyKey,
        int timeoutMilliseconds)
    {
        ValidateRecoveryRequest(playerId, runId, idempotencyKey);
        if (!settlementGate.Wait(0)) return false;
        try
        {
            UnaryRequestContext context = contextFactory.Create(idempotencyKey);
            string url = config.ApiBaseUrl.TrimEnd('/') +
                         "/v1/runs/" + Uri.EscapeDataString(runId) + "/quit";
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Accept = "application/json";
            request.ContentLength = 0;
            request.Timeout = Math.Max(250, timeoutMilliseconds);
            request.ReadWriteTimeout = Math.Max(250, timeoutMilliseconds);
            if (!string.IsNullOrWhiteSpace(context.AccessToken))
                request.Headers[HttpRequestHeader.Authorization] =
                    "Bearer " + context.AccessToken.Trim();
            request.Headers["Idempotency-Key"] = idempotencyKey;
            if (!string.IsNullOrWhiteSpace(context.RequestId))
                request.Headers["X-Trace-Id"] = context.RequestId;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                int status = (int)response.StatusCode;
                if (status < 200 || status >= 300) return false;
            }
            ActiveRunStore.ClearIfMatches(runId);
            return true;
        }
        catch (WebException exception)
        {
            UnityEngine.Debug.LogWarning("Play Mode force-quit request failed: " + exception.Message);
            return false;
        }
        finally
        {
            settlementGate.Release();
        }
    }

    private async Task SendQuitAsync(
        string runId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await transport.SendAsync<object, EmptyResponse>(
            "POST",
            RunPath(runId, QuitPathSuffix),
            null,
            contextFactory.Create(idempotencyKey),
            cancellationToken);
        ActiveRunStore.ClearIfMatches(runId);
    }

    private async Task<FinishGameplayRunResult> QuitRunForSettlementAsync(
        FinishGameplayRunRequest request,
        CancellationToken cancellationToken)
    {
        GoRunFinishResponse response = await transport.SendAsync<object, GoRunFinishResponse>(
            "POST",
            RunPath(request.RunId, QuitPathSuffix),
            null,
            contextFactory.Create(request.RunId + ":quit"),
            cancellationToken);
        ValidateQuitSettlement(response, request.RunId);
        state.StoreFinish(response);
        ActiveRunStore.ClearIfMatches(request.RunId);
        GameplayRankSettlement rankSettlement = CreateRankSettlement(response.rank_settlement);
        return new FinishGameplayRunResult
        {
            Accepted = true,
            RunId = response.run_id,
            Result = ServerMatchResult.Defeat,
            SettlementType = GameplaySettlementType.Normal,
            TerminationReason = request.TerminationReason,
            FaultAttribution = request.FaultAttribution,
            ApplyRank = false,
            ApplySeasonProgress = false,
            CountCompletedRun = false,
            GrantRewards = true,
            ServerAuthoritativeSettlement = true,
            SettlementStatus = response.settlement_status,
            RejectionCode = response.rejection_code ?? string.Empty,
            GoldReward = response.settlement.total_gold,
            RuneFragmentReward = response.settlement.rune_fragments,
            MerchantEventAvailable = HasMerchantEvent(response),
            MerchantEventId = response.merchant_event?.event_id ?? string.Empty,
            HasAuthoritativeRankSettlement = rankSettlement?.After != null,
            RankSettlement = rankSettlement
        };
    }

    private static GameplayRankSettlement CreateRankSettlement(GoRunRankSettlement settlement)
    {
        if (settlement == null || !settlement.applied) return null;
        GameplayRankState after = ToGameplayRankState(settlement.after);
        if (after == null) return null;
        after.Version = Math.Max(after.Version, settlement.version);
        return new GameplayRankSettlement
        {
            Applied = true,
            Replayed = settlement.replayed,
            StarDelta = settlement.star_delta,
            Before = ToGameplayRankState(settlement.before),
            After = after,
            Promoted = settlement.promoted,
            Demoted = settlement.demoted,
            Version = Math.Max(0, settlement.version)
        };
    }

    private static GameplayRankState ToGameplayRankState(GoRankSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.rank_id)) return null;
        return new GameplayRankState
        {
            RankId = snapshot.rank_id,
            Segment = snapshot.segment,
            Stars = snapshot.stars,
            TotalRankStars = snapshot.total_rank_stars,
            Version = snapshot.version,
            UpdatedAt = snapshot.updated_at ?? string.Empty
        };
    }

    private static bool HasMerchantEvent(GoRunFinishResponse response)
    {
        return response?.merchant_event != null &&
               !string.IsNullOrWhiteSpace(response.merchant_event.event_id) &&
               response.merchant_event.offers != null &&
               response.merchant_event.offers.Count > 0;
    }

    private static void ValidateQuitSettlement(GoRunFinishResponse response, string runId)
    {
        if (response == null ||
            !string.Equals(response.run_id, runId, StringComparison.Ordinal) ||
            !string.Equals(response.result, "lose", StringComparison.Ordinal) ||
            !string.Equals(response.settlement_status, "Settled", StringComparison.Ordinal) ||
            response.settlement == null ||
            response.settlement.total_gold != DefeatGoldReward)
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Server quit response must contain a settled 10-gold defeat reward.");
    }

    private static void ValidateVictorySettlement(
        GoRunFinishResponse response,
        string runId,
        string endpoint = "finish")
    {
        if (response == null ||
            !string.Equals(response.run_id, runId, StringComparison.Ordinal) ||
            !string.Equals(response.result, "win", StringComparison.Ordinal) ||
            !string.Equals(response.settlement_status, "Settled", StringComparison.Ordinal) ||
            response.settlement == null ||
            response.settlement.total_gold != VictoryGoldReward)
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Server " + endpoint +
                " response must contain a settled 20-gold victory reward.");
    }

    private static string RunPath(string runId, string suffix)
    {
        return "/v1/runs/" + Uri.EscapeDataString(runId) + suffix;
    }

    private static void ValidateRecoveryRequest(
        string playerId,
        string runId,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
    }

    private static bool IsTerminalRecoveryResponse(ClientServiceException exception)
    {
        if (exception == null) return false;
        return string.Equals(exception.Code, "SETTLEMENT_CONFLICT", StringComparison.Ordinal) ||
               string.Equals(exception.Code, "RUN_NORMAL_LOSS_SETTLED", StringComparison.Ordinal) ||
               string.Equals(exception.Code, "RUN_QUIT", StringComparison.Ordinal) ||
               string.Equals(exception.Code, "RUN_NOT_FOUND", StringComparison.Ordinal);
    }

    private static GoRunChainEvent CreateRunEndEvent(
        GoServerState.RunTicket ticket,
        FinishGameplayRunRequest request,
        int remainingHealth)
    {
        string payload = string.Join("|", new[]
        {
            request.ProposedResult.ToString(),
            request.ReachedWave.ToString(CultureInfo.InvariantCulture),
            remainingHealth.ToString(CultureInfo.InvariantCulture),
            request.RecruitmentCount.ToString(CultureInfo.InvariantCulture)
        });
        string payloadDigest;
        using (SHA256 sha = SHA256.Create())
            payloadDigest = Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        if (ticket == null || !IsEventHash(ticket.CurrentEventHash) || ticket.EventSequence < 0)
            throw new ClientServiceException(
                "RUN_EVENT_CHAIN_UNAVAILABLE",
                "Run event-chain state is unavailable.");
        long sequence = checked(ticket.EventSequence + 1);
        string previousHash = ticket.CurrentEventHash;
        return new GoRunChainEvent
        {
            sequence = sequence,
            event_type = "run_end",
            payload_digest = payloadDigest,
            previous_hash = previousHash,
            current_hash = EventHash(
                ticket.RunNonce,
                checked((ulong)sequence),
                "run_end",
                payloadDigest,
                previousHash)
        };
    }

    private static void ValidateVictoryFinish(FinishGameplayRunRequest request)
    {
        if (request.ProposedResult != ServerMatchResult.Victory)
            throw new ClientServiceException(
                "RUN_RESULT_INVALID",
                "Only a victory can be submitted to the Run finish endpoint.");
        if (request.ReachedWave < 1 ||
            request.ReachedWave > BattleSettlementDefinition.MaxScheduledWave)
            throw new ClientServiceException(
                "RUN_VICTORY_WAVE_INVALID",
                "The finish endpoint accepts victories from waves 1 through 20 only.");
    }

    private static async Task WaitForMinimumDurationAsync(
        string startedAt,
        CancellationToken cancellationToken)
    {
        DateTimeOffset serverStartedAt = ParseServerTimestamp(startedAt, "started_at");
        DateTimeOffset earliestFinishAt = serverStartedAt.AddSeconds(MinimumRunDurationSeconds);
        while (DateTimeOffset.UtcNow < earliestFinishAt)
        {
            TimeSpan remaining = earliestFinishAt - DateTimeOffset.UtcNow;
            await Task.Delay(
                remaining > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : remaining,
                cancellationToken);
        }
    }

    private static DateTimeOffset ParseServerTimestamp(string value, string fieldName)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Server Run " + fieldName + " is invalid.");
        return parsed;
    }

    private static bool IsEventHash(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f')))
                return false;
        }
        return true;
    }

    private static string ToCanonicalBase64(string base64Url)
    {
        string normalized = (base64Url ?? string.Empty).Replace('-', '+').Replace('_', '/');
        return normalized.PadRight(
            normalized.Length + ((4 - normalized.Length % 4) % 4),
            '=');
    }

    private static string FinishRequestFingerprint(GoRunFinishRequest request)
    {
        return StableDiagnosticHash(UnityEngine.JsonUtility.ToJson(request));
    }

    private static string StableDiagnosticHash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(16);
            for (int index = 0; index < 8; index++)
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static string InitialHash(string runNonce)
    {
        byte[] nonce = DecodeBase64Url(runNonce);
        byte[] domain = Encoding.UTF8.GetBytes("DragonBound/EventChain/v1\0");
        byte[] material = new byte[domain.Length + nonce.Length];
        Buffer.BlockCopy(domain, 0, material, 0, domain.Length);
        Buffer.BlockCopy(nonce, 0, material, domain.Length, nonce.Length);
        using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(material));
    }

    private static string EventHash(
        string runNonce,
        ulong sequence,
        string eventType,
        string payloadDigest,
        string previousHash)
    {
        byte[] domain = Encoding.UTF8.GetBytes("DragonBound/Event/v1\0");
        byte[] nonce = Encoding.UTF8.GetBytes(runNonce);
        byte[] type = Encoding.UTF8.GetBytes(eventType);
        byte[] payload = FromHex(payloadDigest);
        byte[] previous = FromHex(previousHash);
        byte[] material = new byte[domain.Length + 2 + nonce.Length + 8 + 2 + type.Length + 64];
        int offset = 0;
        Buffer.BlockCopy(domain, 0, material, offset, domain.Length); offset += domain.Length;
        WriteUInt16(material, ref offset, (ushort)nonce.Length);
        Buffer.BlockCopy(nonce, 0, material, offset, nonce.Length); offset += nonce.Length;
        WriteUInt64(material, ref offset, sequence);
        WriteUInt16(material, ref offset, (ushort)type.Length);
        Buffer.BlockCopy(type, 0, material, offset, type.Length); offset += type.Length;
        Buffer.BlockCopy(payload, 0, material, offset, payload.Length); offset += payload.Length;
        Buffer.BlockCopy(previous, 0, material, offset, previous.Length);
        using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(material));
    }

    private static void WriteUInt16(byte[] target, ref int offset, ushort value)
    {
        target[offset++] = (byte)(value >> 8);
        target[offset++] = (byte)value;
    }

    private static void WriteUInt64(byte[] target, ref int offset, ulong value)
    {
        for (int shift = 56; shift >= 0; shift -= 8)
            target[offset++] = (byte)(value >> shift);
    }

    private static byte[] DecodeBase64Url(string value)
    {
        string normalized = (value ?? string.Empty).Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        byte[] decoded = Convert.FromBase64String(normalized);
        if (decoded.Length != 16)
            throw new ClientServiceException("INVALID_RUN_NONCE", "Server run nonce is invalid.");
        return decoded;
    }

    private static byte[] FromHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            throw new ClientServiceException("INVALID_RUN_HASH", "Run hash is invalid.");
        var result = new byte[value.Length / 2];
        for (int index = 0; index < result.Length; index++)
            result[index] = byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return result;
    }

    private static string Hex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static int ParseSeed(string value)
    {
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed))
            throw new ClientServiceException("INVALID_RESPONSE", "Server run seed is invalid.");
        return unchecked((int)(parsed ^ (parsed >> 32)));
    }

    private void ResolveRandomStreams(
        GoRunStartResponse response,
        int runSeed,
        out int playerRecruitSeed,
        out int aiRecruitSeed,
        out int waveRandomSeed)
    {
        bool hasRecruit = !string.IsNullOrWhiteSpace(response.recruit_random);
        bool hasWave = !string.IsNullOrWhiteSpace(response.wave_rng);
        if (config.RequireAuthoritativeRunContract && (!hasRecruit || !hasWave))
            throw new ClientServiceException(
                "RUN_RANDOM_CONTRACT_REQUIRED",
                "Run start response is missing recruit_random or wave_rng.");
        if (config.RequireAuthoritativeRunContract &&
            !string.Equals(response.rng_version, SharedRandomProtocolV1.Version, StringComparison.Ordinal))
            throw new ClientServiceException(
                "RUN_RANDOM_VERSION_MISMATCH",
                "Server random protocol version is not supported by this client.");

        try
        {
            if (hasRecruit)
                SharedRandomProtocolV1.DecodeRecruitRandom(
                    response.recruit_random,
                    out playerRecruitSeed,
                    out aiRecruitSeed);
            else
            {
                playerRecruitSeed = SharedRandomProtocolV1.DeriveSeed(runSeed, "player.recruit");
                aiRecruitSeed = SharedRandomProtocolV1.DeriveSeed(runSeed, "ai.recruit");
            }
            waveRandomSeed = hasWave
                ? SharedRandomProtocolV1.DecodeWaveRandom(response.wave_rng)
                : runSeed;
            if (config.RequireAuthoritativeRunContract &&
                (playerRecruitSeed != SharedRandomProtocolV1.DeriveSeed(runSeed, "player.recruit") ||
                 aiRecruitSeed != SharedRandomProtocolV1.DeriveSeed(runSeed, "ai.recruit") ||
                 waveRandomSeed != runSeed))
                throw new ClientServiceException(
                    "RUN_RANDOM_SEED_MISMATCH",
                    "Server random stream payload does not match run_seed.");
        }
        catch (FormatException exception)
        {
            throw new ClientServiceException(
                "RUN_RANDOM_PAYLOAD_INVALID",
                "Server random stream payload is invalid: " + exception.Message);
        }
    }

    private void ValidateStageSnapshot(GoRunStartResponse response)
    {
        if (!config.RequireAuthoritativeRunContract) return;
        if (string.IsNullOrWhiteSpace(response.stage_snapshot_digest) ||
            !string.Equals(
                response.stage_snapshot_digest,
                config.ExpectedStageSnapshotDigest,
                StringComparison.OrdinalIgnoreCase))
            throw new ClientServiceException(
                "STAGE_SNAPSHOT_MISMATCH",
                "Server Stage snapshot is missing or does not match the published client manifest.");
    }
}
