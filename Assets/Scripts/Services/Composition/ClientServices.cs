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
        IRunForgePickGateway runForgePick,
        IForgekeepersGiftGateway forgekeepersGift,
        IShareService share,
        ISignInGateway signIn,
        DragonBound.Services.IGameplayRunGateway gameplay,
        IGameplayRunSnapshotGateway gameplaySnapshots,
        ICloudSaveGateway cloudSave,
        IPushGateway push,
        ISocialGateway social)
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
        RunForgePick = runForgePick ?? throw new ArgumentNullException(nameof(runForgePick));
        ForgekeepersGift = forgekeepersGift ??
                           throw new ArgumentNullException(nameof(forgekeepersGift));
        Share = share ?? throw new ArgumentNullException(nameof(share));
        SignIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        Gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        GameplaySnapshots = gameplaySnapshots ?? throw new ArgumentNullException(nameof(gameplaySnapshots));
        CloudSave = cloudSave ?? throw new ArgumentNullException(nameof(cloudSave));
        Push = push ?? throw new ArgumentNullException(nameof(push));
        Social = social ?? throw new ArgumentNullException(nameof(social));
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
    public IRunForgePickGateway RunForgePick { get; }
    public IForgekeepersGiftGateway ForgekeepersGift { get; }
    public IShareService Share { get; }
    public ISignInGateway SignIn { get; }
    public DragonBound.Services.IGameplayRunGateway Gameplay { get; }
    public IGameplayRunSnapshotGateway GameplaySnapshots { get; }
    public ICloudSaveGateway CloudSave { get; }
    public IPushGateway Push { get; }
    public ISocialGateway Social { get; }
}
