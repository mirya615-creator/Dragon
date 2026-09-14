public static class LocalServiceModule
{
    public static IClientServices Build(ClientServiceConfig config)
    {
        var authSession = new PersistentAuthSessionStore();
        var guestIdentity = new GuestIdentityService();
        var auth = new LocalAuthGateway();
        var googleOAuth = new MockGoogleOAuthProvider();
        var energy = new LocalPlayerEnergyGateway();
        var gold = new LocalPlayerGoldGateway();
        var leaderboardStore = new LocalLeaderboardPeriodStore();
        var rank = new LocalPlayerRankGateway(leaderboardStore);
        var leaderboard = new LocalLeaderboardGateway(rank, leaderboardStore);
        var merchant = new LocalMerchantGateway(gold);
        var runes = new LocalRuneRewardService();
        var rewardedAds = new AnalyticsRewardedAdService(new MockRewardedAdService());
        var runForgePick = new LocalRunForgePickGateway();
        var forgekeepersGift = new LocalForgekeepersGiftGateway();
        var share = new MockShareService();
        var signIn = new LocalSignInGateway(
            share,
            new LocalSignInRewardSink(gold, runes));
        var gameplay = new DragonBound.Services.LocalGameplayRunGateway();
        var serverFeatures = new LocalServerFeatureGateway();
        DragonBound.Services.GameplayRunGatewayRegistry.Install(gameplay);

        return new ClientServices(
            auth,
            authSession,
            guestIdentity,
            googleOAuth,
            energy,
            gold,
            rank,
            leaderboard,
            merchant,
            runes,
            rewardedAds,
            runForgePick,
            forgekeepersGift,
            share,
            signIn,
            gameplay,
            serverFeatures,
            serverFeatures,
            serverFeatures,
            serverFeatures);
    }
}

internal sealed class LocalServerFeatureGateway :
    IGameplayRunSnapshotGateway,
    ICloudSaveGateway,
    IPushGateway,
    ISocialGateway
{
    public System.Threading.Tasks.Task<GameplayRunSnapshotResult> SaveSnapshotAsync(
        GameplayRunSnapshotRequest request,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(new GameplayRunSnapshotResult
        {
            RunId = request?.RunId,
            EventSequence = request?.EventSequence ?? 0,
            PayloadSize = request?.Payload?.Length ?? 0
        });
    }

    public System.Threading.Tasks.Task<CloudSaveSnapshot> GetAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(CreateSave());
    }

    public System.Threading.Tasks.Task<CloudSaveSnapshot> UpdateAsync(
        CloudSaveSnapshot snapshot,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(snapshot ?? CreateSave());
    }

    public System.Threading.Tasks.Task<PushDeviceState> RegisterDeviceAsync(
        PushDeviceRegistration registration,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(new PushDeviceState
        {
            DeviceId = string.IsNullOrWhiteSpace(registration?.DeviceId)
                ? System.Guid.NewGuid().ToString()
                : registration.DeviceId,
            Platform = registration?.Platform,
            Locale = registration?.Locale,
            IanaTimezone = registration?.IanaTimezone,
            Authorized = true
        });
    }

    public System.Threading.Tasks.Task UnregisterDeviceAsync(
        string deviceId,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<ClientPushPreferences> GetPreferencesAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(new ClientPushPreferences());
    }

    public System.Threading.Tasks.Task<ClientPushPreferences> UpdatePreferencesAsync(
        ClientPushPreferences preferences,
        string idempotencyKey,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(preferences ?? new ClientPushPreferences());
    }

    public System.Threading.Tasks.Task<SocialLinkState> LinkAsync(
        string provider,
        string authorizationCode,
        string idempotencyKey,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(new SocialLinkState
        {
            LinkId = System.Guid.NewGuid().ToString(),
            Provider = provider
        });
    }

    public System.Threading.Tasks.Task UnlinkAsync(
        string provider,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<SocialSyncState> SyncFriendsAsync(
        string provider,
        string idempotencyKey,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return System.Threading.Tasks.Task.FromResult(new SocialSyncState { Provider = provider });
    }

    public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<SocialFriendState>> GetFriendsAsync(
        string provider,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        System.Collections.Generic.IReadOnlyList<SocialFriendState> result =
            new System.Collections.Generic.List<SocialFriendState>();
        return System.Threading.Tasks.Task.FromResult(result);
    }

    private static CloudSaveSnapshot CreateSave()
    {
        return new CloudSaveSnapshot
        {
            SchemaVersion = 1,
            ServerRevision = 1,
            Settings = new CloudSaveSettings { MusicVolume = 1f, SfxVolume = 1f, Language = "en" },
            PushPreferences = new CloudSavePushPreferences()
        };
    }
}
