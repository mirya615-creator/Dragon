using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

internal sealed class GoApiContextFactory
{
    private readonly IAuthSessionStore sessions;
    private readonly ClientServiceConfig config;

    public GoApiContextFactory(IAuthSessionStore sessions, ClientServiceConfig config)
    {
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public UnaryRequestContext Create(string idempotencyKey = "")
    {
        return new UnaryRequestContext
        {
            AccessToken = sessions.Current?.AccessToken ?? string.Empty,
            IdempotencyKey = idempotencyKey ?? string.Empty,
            RequestId = Guid.NewGuid().ToString("N"),
            ClientVersion = config.ClientVersion,
            ContentVersion = config.ContentVersion
        };
    }
}

internal sealed class GoBootstrapClient
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;
    private readonly GoServerState state;

    public GoBootstrapClient(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        GoServerState state)
    {
        this.transport = transport;
        this.contexts = contexts;
        this.state = state;
    }

    public async Task<GoBootstrapResponse> GetAsync(CancellationToken cancellationToken)
    {
        GoBootstrapResponse response = await transport.SendAsync<object, GoBootstrapResponse>(
            "GET", "/v1/bootstrap", null, contexts.Create(), cancellationToken);
        state.UpdateBootstrap(response);
        return response;
    }
}

public sealed class GoPlayerEnergyGateway : IPlayerEnergyGateway
{
    internal const string AdClaimPath = "/v1/ads/claim";
    internal const string EnergyAdPlacement = "energy_restore";
    internal const string ShareCreatePath = "/v1/share/create";
    internal const string ShareClaimPath = "/v1/share/claim";
    internal const string EnergySharePlacement = "energy_share";

    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;
    private readonly GoBootstrapClient bootstrap;

    internal GoPlayerEnergyGateway(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        GoBootstrapClient bootstrap)
    {
        this.transport = transport;
        this.contexts = contexts;
        this.bootstrap = bootstrap;
    }

    public async Task<PlayerEnergyState> GetEnergyAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        GoEnergyResponse response = await transport.SendAsync<object, GoEnergyResponse>(
            "GET", "/v1/energy", null, contexts.Create(), cancellationToken);
        return ToState(response.current, response.max, response.next_recover_at);
    }

    public async Task<EnergyConsumeResult> ConsumeEnergyAsync(
        string playerId,
        int amount,
        string requestId,
        CancellationToken cancellationToken)
    {
        // The server consumes energy atomically in POST /v1/runs/start. This method is
        // therefore a read-only preflight; it must never create a second debit.
        PlayerEnergyState current = await GetEnergyAsync(playerId, cancellationToken);
        bool sufficient = amount > 0 && current.Current >= amount;
        return new EnergyConsumeResult
        {
            Succeeded = sufficient,
            State = new PlayerEnergyState
            {
                Current = sufficient ? current.Current - amount : current.Current,
                Maximum = current.Maximum,
                NextRecoveryUnixTime = current.NextRecoveryUnixTime
            }
        };
    }

    public Task<PlayerEnergyState> GrantEnergyAsync(
        string playerId,
        int amount,
        string rewardTransactionId,
        CancellationToken cancellationToken)
    {
        return Unsupported<PlayerEnergyState>(
            "ENERGY_GRANT_REQUIRES_VERIFIED_SOURCE",
            "Energy can only be granted by a verified server reward flow.");
    }

    public async Task<DailyRewardStatus> GetRewardedAdStatusAsync(
        string playerId,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        GoEnergyResponse response = await transport.SendAsync<object, GoEnergyResponse>(
            "GET", "/v1/energy", null, contexts.Create(), cancellationToken);
        GoRestoreStatus status = response.ad_restore ?? new GoRestoreStatus();
        return new DailyRewardStatus
        {
            ClaimsUsed = status.daily_used,
            DailyLimit = status.daily_limit,
            CanClaim = status.enabled && status.daily_used < status.daily_limit
        };
    }

    public async Task<RewardedAdEnergyClaimResult> ClaimRewardedAdEnergyAsync(
        string playerId,
        int amount,
        int dailyLimit,
        string rewardTransactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (dailyLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(dailyLimit));
        if (string.IsNullOrWhiteSpace(rewardTransactionId))
            throw new ArgumentException("Ad event ID is required.", nameof(rewardTransactionId));

        await SendClientAdSuccessAsync(
            transport,
            contexts,
            playerId,
            EnergyAdPlacement,
            rewardTransactionId,
            string.Empty,
            EnergyAdPlacement,
            cancellationToken);

        GoEnergyResponse response = await transport.SendAsync<object, GoEnergyResponse>(
            "GET", "/v1/energy", null, contexts.Create(), cancellationToken);
        if (response == null)
            throw new ClientServiceException("INVALID_RESPONSE", "Energy response was empty.");
        GoRestoreStatus restore = response.ad_restore ?? new GoRestoreStatus();
        return new RewardedAdEnergyClaimResult
        {
            Succeeded = true,
            LimitReached = !restore.enabled || restore.daily_used >= restore.daily_limit,
            ClaimsUsed = restore.daily_used,
            DailyLimit = restore.daily_limit,
            State = ToState(response.current, response.max, response.next_recover_at)
        };
    }

    public Task<DailyRewardStatus> AcknowledgeRewardedAdLimitAsync(
        string playerId,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        return GetRewardedAdStatusAsync(playerId, dailyLimit, cancellationToken);
    }

    public async Task<DailyShareStatus> GetShareStatusAsync(
        string playerId,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        GoShareStatusResponse response = await transport.SendAsync<object, GoShareStatusResponse>(
            "GET", "/v1/share/status", null, contexts.Create(), cancellationToken);
        if (response == null)
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Share status response was empty.");
        int used = Math.Max(0, response.claimed_count);
        int limit = used + Math.Max(0, response.remaining);
        return new DailyShareStatus
        {
            SharesUsed = used,
            DailyLimit = limit,
            CanShare = response.remaining > 0
        };
    }

    public async Task<ShareEnergyClaimResult> ClaimShareEnergyAsync(
        string playerId,
        int amount,
        int dailyLimit,
        string shareTransactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (dailyLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(dailyLimit));
        if (string.IsNullOrWhiteSpace(shareTransactionId))
            throw new ArgumentException("Share event ID is required.", nameof(shareTransactionId));

        string platform = ResolvePlatform();
        var createRequest = new GoShareCreateRequest
        {
            platform = platform,
            placement_id = EnergySharePlacement,
            client_share_succeeded = true,
            client_share_event_id = shareTransactionId
        };
        GoShareCreateResponse created = await transport.SendAsync<GoShareCreateRequest, GoShareCreateResponse>(
            "POST",
            ShareCreatePath,
            createRequest,
            contexts.Create("share.create:" + shareTransactionId),
            cancellationToken);
        if (created == null || string.IsNullOrWhiteSpace(created.share_id))
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Share create response did not return a share ID.");

        var claimRequest = new GoShareClaimRequest
        {
            share_id = created.share_id,
            share_token = created.share_token ?? string.Empty,
            platform = platform,
            placement_id = EnergySharePlacement,
            client_share_succeeded = true,
            client_share_event_id = shareTransactionId
        };
        GoShareClaimResponse claimed = await transport.SendAsync<GoShareClaimRequest, GoShareClaimResponse>(
            "POST",
            ShareClaimPath,
            claimRequest,
            contexts.Create("share.claim:" + shareTransactionId),
            cancellationToken);
        if (claimed?.energy == null)
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Share claim response did not return an energy snapshot.");

        DailyShareStatus status = await GetShareStatusAsync(
            playerId,
            dailyLimit,
            cancellationToken);
        return new ShareEnergyClaimResult
        {
            Succeeded = true,
            LimitReached = !status.CanShare,
            SharesUsed = status.SharesUsed,
            DailyLimit = status.DailyLimit,
            State = ToState(
                claimed.energy.current,
                claimed.energy.max,
                claimed.energy.next_recover_at)
        };
    }

    public async Task<DailyShareStatus> AcknowledgeShareLimitAsync(
        string playerId,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        return await GetShareStatusAsync(playerId, dailyLimit, cancellationToken);
    }

    private static PlayerEnergyState ToState(int current, int maximum, string nextRecoverAt)
    {
        long next = 0;
        if (DateTimeOffset.TryParse(nextRecoverAt, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
        {
            next = parsed.ToUnixTimeSeconds();
        }
        return new PlayerEnergyState
        {
            Current = current,
            Maximum = maximum,
            NextRecoveryUnixTime = next
        };
    }

    private static string ResolvePlatform()
    {
        switch (UnityEngine.Application.platform)
        {
            case UnityEngine.RuntimePlatform.Android: return "android";
            case UnityEngine.RuntimePlatform.IPhonePlayer: return "ios";
            default: return "editor";
        }
    }

    internal static async Task SendClientAdSuccessAsync(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        string playerId,
        string placement,
        string eventId,
        string runId,
        string rewardScope,
        CancellationToken cancellationToken)
    {
        var request = new GoAdClaimRequest
        {
            transaction_id = eventId,
            player_id = playerId,
            placement = placement,
            provider = "client_trusted",
            payload = new GoClientReportedAdPayload
            {
                reward_scope = rewardScope ?? string.Empty
            },
            signature = string.Empty,
            run_id = runId ?? string.Empty,
            client_ad_succeeded = true,
            client_ad_event_id = eventId,
            ad_platform = ResolvePlatform()
        };
        GoAdClaimResponse response = await transport.SendAsync<GoAdClaimRequest, GoAdClaimResponse>(
            "POST",
            AdClaimPath,
            request,
            contexts.Create("ad.claim:" + eventId),
            cancellationToken);
        if (response == null)
            throw new ClientServiceException("INVALID_RESPONSE", "Ad claim response was empty.");
    }

    private static Task<T> Unsupported<T>(string code, string message)
    {
        return Task.FromException<T>(new ClientServiceException(code, message));
    }
}

public sealed class GoPlayerGoldGateway : IPlayerGoldGateway
{
    private const string SettlementDoublePlacement = "settle_double";

    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;
    private readonly GoBootstrapClient bootstrap;
    private readonly GoServerState state;

    internal GoPlayerGoldGateway(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        GoBootstrapClient bootstrap,
        GoServerState state)
    {
        this.transport = transport;
        this.contexts = contexts;
        this.bootstrap = bootstrap;
        this.state = state;
    }

    public async Task<PlayerGoldState> GetGoldAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        GoBootstrapResponse response = await bootstrap.GetAsync(cancellationToken);
        return new PlayerGoldState { Balance = response?.resources?.gold ?? 0 };
    }

    public async Task<GoldSettlementResult> SettleMatchAsync(
        string playerId,
        string matchId,
        MatchOutcome outcome,
        GoldClaimType claimType,
        string adVerificationId,
        CancellationToken cancellationToken)
    {
        if (!state.TryGetTicket(matchId, out GoServerState.RunTicket ticket) || ticket.Finish == null)
        {
            throw new ClientServiceException(
                "RUN_SETTLEMENT_NOT_FOUND", "The server run has not been settled.");
        }
        if (claimType == GoldClaimType.RewardedAd)
        {
            if (string.IsNullOrWhiteSpace(adVerificationId))
                throw new ArgumentException("Ad event ID is required.", nameof(adVerificationId));
            await GoPlayerEnergyGateway.SendClientAdSuccessAsync(
                transport,
                contexts,
                playerId,
                SettlementDoublePlacement,
                adVerificationId,
                matchId,
                matchId,
                cancellationToken);
            GoBootstrapResponse refreshed = await bootstrap.GetAsync(cancellationToken);
            return new GoldSettlementResult
            {
                Reward = ticket.GoldReward,
                Balance = refreshed?.resources?.gold ?? 0,
                Applied = true
            };
        }

        long balance = state.Bootstrap?.resources?.gold ?? 0;
        return new GoldSettlementResult
        {
            Reward = ticket.GoldReward,
            Balance = balance,
            Applied = false
        };
    }

    public Task<GoldSpendResult> TrySpendAsync(
        string playerId,
        long amount,
        string transactionId,
        CancellationToken cancellationToken)
    {
        return Task.FromException<GoldSpendResult>(new ClientServiceException(
            "DIRECT_GOLD_SPEND_UNSUPPORTED",
            "Gold spending must use a server-owned purchase endpoint."));
    }
}

public sealed class GoPlayerRankGateway : IPlayerRankGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;

    internal GoPlayerRankGateway(IUnaryTransport transport, GoApiContextFactory contexts)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    public async Task<PlayerRankState> GetRankAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        GoRankProfileResponse response = await transport.SendAsync<object, GoRankProfileResponse>(
            "GET", "/v1/rank", null, contexts.Create(), cancellationToken);
        if (response == null || string.IsNullOrWhiteSpace(response.rank_id))
            throw new ClientServiceException("INVALID_RESPONSE", "Rank response is incomplete.");
        return GoRankContractMapper.ToState(response);
    }

    public Task<RankProgressResult> RecordVictoryAsync(
        string playerId,
        string matchId,
        CancellationToken cancellationToken)
    {
        return AlreadySettledAsync(playerId, cancellationToken);
    }

    public Task<RankProgressResult> RecordDefeatAsync(
        string playerId,
        string matchId,
        CancellationToken cancellationToken)
    {
        return AlreadySettledAsync(playerId, cancellationToken);
    }

    private async Task<RankProgressResult> AlreadySettledAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        PlayerRankState current = await GetRankAsync(playerId, cancellationToken);
        return new RankProgressResult { State = current, Promoted = false };
    }
}

internal static class GoRankContractMapper
{
    public static PlayerRankState ToState(GoRankProfileResponse response)
    {
        if (response == null) return null;
        PlayerRankState state = RankProgressionRules.FromServerState(
            response.rank_id,
            response.segment,
            response.stars);
        ApplyMetadata(
            state,
            response.total_rank_stars,
            response.version,
            response.updated_at);
        return state;
    }

    public static PlayerRankState ToState(GoRankSnapshot response)
    {
        if (response == null || string.IsNullOrWhiteSpace(response.rank_id)) return null;
        PlayerRankState state = RankProgressionRules.FromServerState(
            response.rank_id,
            response.segment,
            response.stars);
        ApplyMetadata(
            state,
            response.total_rank_stars,
            response.version,
            response.updated_at);
        return state;
    }

    private static void ApplyMetadata(
        PlayerRankState state,
        long totalRankStars,
        long version,
        string updatedAt)
    {
        if (state == null) return;
        if (totalRankStars > 0 || state.TotalRankStars == 0)
            state.TotalRankStars = Math.Max(0, totalRankStars);
        state.Version = Math.Max(0, version);
        if (DateTimeOffset.TryParse(
                updatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            state.ReachedStateAtUnixMilliseconds = parsed.ToUnixTimeMilliseconds();
        }
    }
}

public sealed class GoLeaderboardGateway : ILeaderboardGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;

    internal GoLeaderboardGateway(IUnaryTransport transport, GoApiContextFactory contexts)
    {
        this.transport = transport;
        this.contexts = contexts;
    }

    public async Task<LeaderboardResult> GetLeaderboardAsync(
        string playerId,
        LeaderboardPeriodType periodType,
        CancellationToken cancellationToken)
    {
        string board = periodType == LeaderboardPeriodType.Monthly ? "monthly" : "weekly";
        GoLeaderboardResponse response = await transport.SendAsync<object, GoLeaderboardResponse>(
            "GET",
            "/v1/leaderboards/" + board + "?page=1&page_size=100",
            null,
            contexts.Create(),
            cancellationToken);

        var result = new LeaderboardResult
        {
            Period = new LeaderboardPeriod
            {
                Type = periodType,
                PeriodKey = response.period_key,
                StartsAtUnixMilliseconds = ParseMilliseconds(response.start_at),
                EndsAtUnixMilliseconds = ParseMilliseconds(response.end_at)
            }
        };
        var players = new List<LeaderboardPlayer>();
        foreach (GoLeaderboardEntry entry in response.entries ?? new List<GoLeaderboardEntry>())
        {
            PlayerRankState rankState = RankProgressionRules.FromServerState(
                entry.rank_id,
                entry.segment,
                entry.stars);
            var player = new LeaderboardPlayer
            {
                PlayerId = entry.player_id,
                AvatarId = entry.avatar_id,
                DisplayName = ShortPlayerName(entry.player_id),
                RankLevel = ParseRankLevel(entry.rank_id),
                Division = entry.segment <= 0 ? 0 : entry.segment,
                CurrentStars = rankState.CurrentStars,
                TotalRankStars = rankState.TotalRankStars,
                ReachedStateAtUnixMilliseconds = ParseMilliseconds(entry.tie_breaker_at)
            };
            players.Add(player);
            if (string.Equals(entry.player_id, playerId, StringComparison.Ordinal))
            {
                result.LocalPlayer = player;
                result.LocalPlayerPosition = entry.rank_position > 0
                    ? (int)Math.Min(int.MaxValue, entry.rank_position)
                    : players.Count;
            }
        }
        result.Players = players;
        return result;
    }

    internal static PlayerRankState ToRankState(LeaderboardPlayer player)
    {
        PlayerRankState state = RankProgressionRules.Calculate(player?.TotalRankStars ?? 0);
        if (player != null)
        {
            state.Level = Math.Max(1, player.RankLevel);
            state.Division = player.Division;
            state.ReachedStateAtUnixMilliseconds = player.ReachedStateAtUnixMilliseconds;
        }
        return state;
    }

    private static int ParseRankLevel(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("RANK_", StringComparison.Ordinal) &&
            int.TryParse(value.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int level))
            return Math.Max(1, Math.Min(10, level));
        return 1;
    }

    private static long ParseMilliseconds(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? parsed.ToUnixTimeMilliseconds()
            : 0;
    }

    private static string ShortPlayerName(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return "Player";
        return playerId.Length <= 8 ? playerId : "Player " + playerId.Substring(0, 8);
    }
}

public sealed class GoCloudSaveGateway : ICloudSaveGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;

    internal GoCloudSaveGateway(IUnaryTransport transport, GoApiContextFactory contexts)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    public async Task<CloudSaveSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        GoSaveSnapshot response = await transport.SendAsync<object, GoSaveSnapshot>(
            "GET", "/v1/player/save", null, contexts.Create(), cancellationToken);
        return Map(response);
    }

    public async Task<CloudSaveSnapshot> UpdateAsync(
        CloudSaveSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot == null || snapshot.ServerRevision < 1)
            throw new ArgumentException("A cloud save with a valid server revision is required.", nameof(snapshot));
        ValidateSettings(snapshot.Settings);
        var request = new GoSaveUpdate
        {
            expected_revision = snapshot.ServerRevision,
            settings = new GoSettings
            {
                music_volume = snapshot.Settings.MusicVolume,
                sfx_volume = snapshot.Settings.SfxVolume,
                language = snapshot.Settings.Language
            },
            push_preferences = ToWire(snapshot.PushPreferences)
        };
        GoSaveSnapshot response = await transport.SendAsync<GoSaveUpdate, GoSaveSnapshot>(
            "PUT", "/v1/player/save", request, contexts.Create(), cancellationToken);
        return Map(response);
    }

    private static CloudSaveSnapshot Map(GoSaveSnapshot value)
    {
        if (value == null || value.server_revision < 1 || value.settings == null ||
            value.push_preferences == null)
            throw new ClientServiceException("INVALID_RESPONSE", "Cloud save response is incomplete.");
        return new CloudSaveSnapshot
        {
            SchemaVersion = value.save_schema_version,
            ContentVersion = value.content_version,
            ServerRevision = value.server_revision,
            Settings = new CloudSaveSettings
            {
                MusicVolume = value.settings.music_volume,
                SfxVolume = value.settings.sfx_volume,
                Language = value.settings.language
            },
            PushPreferences = new CloudSavePushPreferences
            {
                EnergyFull = value.push_preferences.energy_full,
                Events = value.push_preferences.events,
                LeaderboardEnd = value.push_preferences.leaderboard_end,
                Comeback = value.push_preferences.comeback
            }
        };
    }

    private static GoSavePushPreferences ToWire(CloudSavePushPreferences value)
    {
        value = value ?? new CloudSavePushPreferences();
        return new GoSavePushPreferences
        {
            energy_full = value.EnergyFull,
            events = value.Events,
            leaderboard_end = value.LeaderboardEnd,
            comeback = value.Comeback
        };
    }

    private static void ValidateSettings(CloudSaveSettings value)
    {
        if (value == null || value.MusicVolume < 0f || value.MusicVolume > 1f ||
            value.SfxVolume < 0f || value.SfxVolume > 1f ||
            string.IsNullOrWhiteSpace(value.Language) || value.Language.Length > 35)
            throw new ArgumentException("Cloud save settings are invalid.", nameof(value));
    }
}

public sealed class GoPushGateway : IPushGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;

    internal GoPushGateway(IUnaryTransport transport, GoApiContextFactory contexts)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    public async Task<PushDeviceState> RegisterDeviceAsync(
        PushDeviceRegistration registration,
        CancellationToken cancellationToken)
    {
        if (registration == null || string.IsNullOrWhiteSpace(registration.PushToken) ||
            (registration.Platform != "fcm" && registration.Platform != "apns"))
            throw new ArgumentException("A valid FCM or APNS device registration is required.", nameof(registration));
        var request = new GoPushDeviceRequest
        {
            device_id = registration.DeviceId,
            platform = registration.Platform,
            push_token = registration.PushToken,
            locale = registration.Locale,
            iana_timezone = registration.IanaTimezone
        };
        GoPushDeviceResponse response = await transport.SendAsync<GoPushDeviceRequest, GoPushDeviceResponse>(
            "POST", "/v1/push/devices", request, contexts.Create(), cancellationToken);
        if (response == null || string.IsNullOrWhiteSpace(response.device_id))
            throw new ClientServiceException("INVALID_RESPONSE", "Push device response is incomplete.");
        return new PushDeviceState
        {
            DeviceId = response.device_id,
            Platform = response.platform,
            Locale = response.locale,
            IanaTimezone = response.iana_timezone,
            Authorized = response.authorized,
            UpdatedAt = response.updated_at
        };
    }

    public Task UnregisterDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(deviceId, out _))
            throw new ArgumentException("A valid push device ID is required.", nameof(deviceId));
        return transport.SendAsync<object, EmptyResponse>(
            "DELETE", "/v1/push/devices/" + Uri.EscapeDataString(deviceId),
            null, contexts.Create(), cancellationToken);
    }

    public async Task<ClientPushPreferences> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        GoPushPreferencesResponse response = await transport.SendAsync<object, GoPushPreferencesResponse>(
            "GET", "/v1/push/preferences", null, contexts.Create(), cancellationToken);
        return Map(response);
    }

    public async Task<ClientPushPreferences> UpdatePreferencesAsync(
        ClientPushPreferences preferences,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidatePreferences(preferences);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        var request = new GoPushPreferencesRequest
        {
            energy_full = preferences.EnergyFull,
            events = preferences.Events,
            leaderboard_end = preferences.LeaderboardEnd,
            comeback = preferences.Comeback,
            quiet_start_hour = preferences.QuietStartHour,
            quiet_end_hour = preferences.QuietEndHour
        };
        GoPushPreferencesResponse response = await transport.SendAsync<GoPushPreferencesRequest, GoPushPreferencesResponse>(
            "PUT", "/v1/push/preferences", request, contexts.Create(idempotencyKey), cancellationToken);
        return Map(response);
    }

    private static ClientPushPreferences Map(GoPushPreferencesResponse value)
    {
        if (value == null)
            throw new ClientServiceException("INVALID_RESPONSE", "Push preferences response is empty.");
        return new ClientPushPreferences
        {
            EnergyFull = value.energy_full,
            Events = value.events,
            LeaderboardEnd = value.leaderboard_end,
            Comeback = value.comeback,
            QuietStartHour = value.quiet_start_hour,
            QuietEndHour = value.quiet_end_hour
        };
    }

    private static void ValidatePreferences(ClientPushPreferences value)
    {
        if (value == null || !ValidHour(value.QuietStartHour) || !ValidHour(value.QuietEndHour) ||
            value.QuietStartHour.HasValue != value.QuietEndHour.HasValue)
            throw new ArgumentException("Push quiet hours must both be null or between 0 and 23.", nameof(value));
    }

    private static bool ValidHour(int? value) => !value.HasValue || (value.Value >= 0 && value.Value <= 23);
}

public sealed class GoSocialGateway : ISocialGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;

    internal GoSocialGateway(IUnaryTransport transport, GoApiContextFactory contexts)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    public async Task<SocialLinkState> LinkAsync(
        string provider,
        string authorizationCode,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateProvider(provider);
        if (string.IsNullOrWhiteSpace(authorizationCode) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Authorization code and idempotency key are required.");
        GoSocialLinkResponse response = await transport.SendAsync<GoSocialLinkRequest, GoSocialLinkResponse>(
            "POST", SocialPath(provider),
            new GoSocialLinkRequest { authorization_code = authorizationCode },
            contexts.Create(idempotencyKey), cancellationToken);
        if (response == null || string.IsNullOrWhiteSpace(response.link_id))
            throw new ClientServiceException("INVALID_RESPONSE", "Social link response is incomplete.");
        return new SocialLinkState
        {
            LinkId = response.link_id,
            Provider = response.provider,
            LinkedAt = response.linked_at
        };
    }

    public Task UnlinkAsync(string provider, CancellationToken cancellationToken)
    {
        ValidateProvider(provider);
        return transport.SendAsync<object, EmptyResponse>(
            "DELETE", SocialPath(provider), null, contexts.Create(), cancellationToken);
    }

    public async Task<SocialSyncState> SyncFriendsAsync(
        string provider,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateProvider(provider);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        GoSocialSyncResponse response = await transport.SendAsync<GoSocialSyncRequest, GoSocialSyncResponse>(
            "POST", "/v1/social/friends/sync", new GoSocialSyncRequest { provider = provider },
            contexts.Create(idempotencyKey), cancellationToken);
        return MapSync(response);
    }

    public async Task<IReadOnlyList<SocialFriendState>> GetFriendsAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(provider)) ValidateProvider(provider);
        string path = "/v1/social/friends" + (string.IsNullOrWhiteSpace(provider)
            ? string.Empty
            : "?provider=" + Uri.EscapeDataString(provider));
        GoSocialFriendsResponse response = await transport.SendAsync<object, GoSocialFriendsResponse>(
            "GET", path, null, contexts.Create(), cancellationToken);
        return MapFriends(response?.friends);
    }

    private static SocialSyncState MapSync(GoSocialSyncResponse value)
    {
        if (value == null)
            throw new ClientServiceException("INVALID_RESPONSE", "Social sync response is empty.");
        return new SocialSyncState
        {
            Provider = value.provider,
            SyncedCount = value.synced_count,
            Friends = new List<SocialFriendState>(MapFriends(value.friends))
        };
    }

    private static IReadOnlyList<SocialFriendState> MapFriends(IReadOnlyList<GoSocialFriend> values)
    {
        var result = new List<SocialFriendState>();
        if (values == null) return result;
        for (int index = 0; index < values.Count; index++)
        {
            GoSocialFriend value = values[index];
            if (value == null || string.IsNullOrWhiteSpace(value.player_id)) continue;
            result.Add(new SocialFriendState { PlayerId = value.player_id, Provider = value.provider });
        }
        return result;
    }

    private static string SocialPath(string provider) =>
        "/v1/social/links/" + Uri.EscapeDataString(provider);

    private static void ValidateProvider(string provider)
    {
        if (provider != "facebook" && provider != "discord")
            throw new ArgumentException("Social provider must be facebook or discord.", nameof(provider));
    }
}

public sealed class GoGameplayRunSnapshotGateway : IGameplayRunSnapshotGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;
    private readonly GoServerState state;

    internal GoGameplayRunSnapshotGateway(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        GoServerState state)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public async Task<GameplayRunSnapshotResult> SaveSnapshotAsync(
        GameplayRunSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || !Guid.TryParse(request.RunId, out _) || request.EventSequence < 1 ||
            request.Payload == null || request.Payload.Length == 0 ||
            !ValidSnapshotType(request.SnapshotType) ||
            (request.WaveNumber.HasValue && request.WaveNumber.Value < 0) ||
            (request.CurrentHealth.HasValue && request.CurrentHealth.Value < 0) ||
            !ValidEventHash(request.PreviousHash) || !ValidEventHash(request.CurrentHash))
            throw new ArgumentException("Run snapshot request is incomplete.", nameof(request));
        await state.EventChainGate.WaitAsync(cancellationToken);
        try
        {
            if (!state.TryGetTicket(request.RunId, out GoServerState.RunTicket ticket) ||
                !ActiveRunStore.TryGet(out ActiveRunRecord activeRun) ||
                !string.Equals(activeRun.runId, request.RunId, StringComparison.Ordinal) ||
                request.EventSequence != ticket.EventSequence + 1 ||
                activeRun.eventSequence != ticket.EventSequence ||
                !string.Equals(request.PreviousHash, ticket.CurrentEventHash, StringComparison.Ordinal) ||
                !string.Equals(activeRun.currentEventHash, ticket.CurrentEventHash, StringComparison.Ordinal))
                throw new ClientServiceException(
                    "RUN_SNAPSHOT_CHAIN_CONFLICT",
                    "Snapshot does not continue the current Run event chain.");

            var wire = new GoRunSnapshotRequest
            {
                snapshot_type = request.SnapshotType,
                wave_number = request.WaveNumber,
                current_health = request.CurrentHealth,
                event_sequence = request.EventSequence,
                payload = Convert.ToBase64String(request.Payload),
                previous_hash = request.PreviousHash,
                current_hash = request.CurrentHash
            };
            GoRunSnapshotResponse response = await transport.SendAsync<GoRunSnapshotRequest, GoRunSnapshotResponse>(
                "POST", "/v1/runs/" + Uri.EscapeDataString(request.RunId) + "/snapshot",
                wire,
                contexts.Create(request.RunId + ":snapshot:" + request.EventSequence),
                cancellationToken);
            if (response == null || response.snapshot_id < 1 ||
                response.event_sequence != request.EventSequence ||
                !string.Equals(response.current_hash, request.CurrentHash, StringComparison.Ordinal))
                throw new ClientServiceException("INVALID_RESPONSE", "Run snapshot response is incomplete.");

            long previousSequence = ticket.EventSequence;
            string previousHash = ticket.CurrentEventHash;
            if (!ActiveRunStore.TryAdvanceEventChain(
                    request.RunId,
                    previousSequence,
                    previousHash,
                    request.EventSequence,
                    request.CurrentHash) ||
                !state.TryAdvanceEventChain(
                    request.RunId,
                    previousSequence,
                    previousHash,
                    request.EventSequence,
                    request.CurrentHash))
                throw new ClientServiceException(
                    "RUN_SNAPSHOT_CHAIN_CONFLICT",
                    "Snapshot was accepted but the local Run event chain could not advance.");

            return new GameplayRunSnapshotResult
            {
                SnapshotId = response.snapshot_id,
                RunId = response.run_id,
                EventSequence = response.event_sequence,
                PayloadSize = response.payload_size,
                PayloadDigest = response.payload_digest,
                PayloadReference = response.payload_ref,
                CurrentHash = response.current_hash,
                CreatedAt = response.created_at,
                Replayed = response.replayed
            };
        }
        finally
        {
            state.EventChainGate.Release();
        }
    }

    private static bool ValidSnapshotType(string value)
    {
        switch (value)
        {
            case "wave_end":
            case "boss_start":
            case "boss_end":
            case "health_change":
            case "background":
            case "disconnect":
            case "run_end":
                return true;
            default:
                return false;
        }
    }

    private static bool ValidEventHash(string value)
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
}
