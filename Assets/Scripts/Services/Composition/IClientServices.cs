public interface IClientServices
{
    IAuthGateway Auth { get; }
    IAuthSessionStore AuthSession { get; }
    IGuestIdentityProvider GuestIdentity { get; }
    IGoogleOAuthProvider GoogleOAuth { get; }
    IPlayerEnergyGateway Energy { get; }
    IPlayerGoldGateway Gold { get; }
    IPlayerRankGateway Rank { get; }
    ILeaderboardGateway Leaderboard { get; }
    IMerchantGateway Merchant { get; }
    IRuneProfileGateway Runes { get; }
    IRewardedAdService RewardedAds { get; }
    IShareService Share { get; }
}
