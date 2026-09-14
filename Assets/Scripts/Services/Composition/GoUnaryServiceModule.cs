using System;

public static class GoUnaryServiceModule
{
    public static IClientServices Build(ClientServiceConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        Validate(config);

        var authSession = new PersistentAuthSessionStore();
        var rawTransport = new UnityWebRequestUnaryTransport(
            config.ApiBaseUrl,
            config.TimeoutSeconds,
            config.EnableNetworkLogging);
        var transport = new RefreshingUnaryTransport(rawTransport, authSession);
        var contexts = new GoApiContextFactory(authSession, config);
        var state = new GoServerState();
        var bootstrap = new GoBootstrapClient(transport, contexts, state);

        var auth = new GoAuthGateway(rawTransport, transport, contexts);
        var guestIdentity = new GuestIdentityService();
        var googleOAuth = new UnavailableGoogleOAuthProvider();
        var energy = new GoPlayerEnergyGateway(transport, contexts, bootstrap);
        var gold = new GoPlayerGoldGateway(transport, contexts, bootstrap, state);
        var leaderboard = new GoLeaderboardGateway(transport, contexts);
        var rank = new GoPlayerRankGateway(transport, contexts);
        var merchant = new GoMerchantGateway(transport, contexts, gold);
        var runes = new GoRuneProfileGateway(transport, contexts, bootstrap);
        // Temporary pre-SDK bridge: the client reports the interaction as completed,
        // while each reward gateway still submits the authoritative success event.
        var rewardedAds = new ClientReportedRewardedAdService();
        var runForgePick = new GoRunForgePickGateway(transport, contexts);
        var forgekeepersGift = new GoForgekeepersGiftGateway(transport, contexts);
        var share = new ClientReportedShareService();
        var signIn = new GoSignInGateway(transport, contexts, bootstrap, runes);
        var gameplay = new GoUnaryGameplayRunGateway(transport, contexts, state, config);
        var gameplaySnapshots = new GoGameplayRunSnapshotGateway(transport, contexts, state);
        var cloudSave = new GoCloudSaveGateway(transport, contexts);
        var push = new GoPushGateway(transport, contexts);
        var social = new GoSocialGateway(transport, contexts);
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
            gameplaySnapshots,
            cloudSave,
            push,
            social);
    }

    private static void Validate(ClientServiceConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiBaseUrl))
            throw new InvalidOperationException("Go backend mode requires an API base URL.");
        if (string.IsNullOrWhiteSpace(config.ClientVersion))
            throw new InvalidOperationException("Go backend mode requires a client version.");
        if (string.IsNullOrWhiteSpace(config.ContentVersion))
            throw new InvalidOperationException("Go backend mode requires a content version.");
        if (string.IsNullOrWhiteSpace(config.ConfigVersion))
            throw new InvalidOperationException("Go backend mode requires a run config version.");
        if (string.IsNullOrWhiteSpace(config.DefaultStageId))
            throw new InvalidOperationException("Go backend mode requires a server stage ID.");
    }
}
