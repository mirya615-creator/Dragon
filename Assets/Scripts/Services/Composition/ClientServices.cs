using System;

public sealed class ClientServices : IClientServices
{
    public ClientServices(
        IAuthGateway auth,
        IAuthSessionStore authSession,
        IGuestIdentityProvider guestIdentity,
        IGoogleOAuthProvider googleOAuth,
        IPlayerEnergyGateway energy,
        IPlayerGoldGateway gold,
        IPlayerRankGateway rank,
        ILeaderboardGateway leaderboard,
        IMerchantGateway merchant,
        IRuneProfileGateway runes,
        IRewardedAdService rewardedAds,
        IShareService share,
        DragonBound.Services.IGameplayRunGateway gameplay)
    {
        Auth = auth ?? throw new ArgumentNullException(nameof(auth));
        AuthSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
        GuestIdentity = guestIdentity ?? throw new ArgumentNullException(nameof(guestIdentity));
        GoogleOAuth = googleOAuth ?? throw new ArgumentNullException(nameof(googleOAuth));
        Energy = energy ?? throw new ArgumentNullException(nameof(energy));
        Gold = gold ?? throw new ArgumentNullException(nameof(gold));
        Rank = rank ?? throw new ArgumentNullException(nameof(rank));
        Leaderboard = leaderboard ?? throw new ArgumentNullException(nameof(leaderboard));
        Merchant = merchant ?? throw new ArgumentNullException(nameof(merchant));
        Runes = runes ?? throw new ArgumentNullException(nameof(runes));
        RewardedAds = rewardedAds ?? throw new ArgumentNullException(nameof(rewardedAds));
        Share = share ?? throw new ArgumentNullException(nameof(share));
        Gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
    }

    public IAuthGateway Auth { get; }
    public IAuthSessionStore AuthSession { get; }
    public IGuestIdentityProvider GuestIdentity { get; }
    public IGoogleOAuthProvider GoogleOAuth { get; }
    public IPlayerEnergyGateway Energy { get; }
    public IPlayerGoldGateway Gold { get; }
    public IPlayerRankGateway Rank { get; }
    public ILeaderboardGateway Leaderboard { get; }
    public IMerchantGateway Merchant { get; }
    public IRuneProfileGateway Runes { get; }
    public IRewardedAdService RewardedAds { get; }
    public IShareService Share { get; }
    public DragonBound.Services.IGameplayRunGateway Gameplay { get; }
}
