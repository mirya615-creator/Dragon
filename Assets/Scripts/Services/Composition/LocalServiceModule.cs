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
        var rewardedAds = new MockRewardedAdService();
        var share = new MockShareService();

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
            share);
    }
}
