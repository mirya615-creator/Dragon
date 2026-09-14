using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

[Serializable]
public sealed class CloudSaveSettings
{
    public float MusicVolume;
    public float SfxVolume;
    public string Language;
}

[Serializable]
public sealed class CloudSavePushPreferences
{
    public bool EnergyFull;
    public bool Events;
    public bool LeaderboardEnd;
    public bool Comeback;
}

public sealed class CloudSaveSnapshot
{
    public int SchemaVersion;
    public string ContentVersion;
    public long ServerRevision;
    public CloudSaveSettings Settings;
    public CloudSavePushPreferences PushPreferences;
}

public interface ICloudSaveGateway
{
    Task<CloudSaveSnapshot> GetAsync(CancellationToken cancellationToken);
    Task<CloudSaveSnapshot> UpdateAsync(
        CloudSaveSnapshot snapshot,
        CancellationToken cancellationToken);
}

public sealed class PushDeviceRegistration
{
    public string DeviceId;
    public string Platform;
    public string PushToken;
    public string Locale;
    public string IanaTimezone;
}

public sealed class PushDeviceState
{
    public string DeviceId;
    public string Platform;
    public string Locale;
    public string IanaTimezone;
    public bool Authorized;
    public string UpdatedAt;
}

public sealed class ClientPushPreferences
{
    public bool EnergyFull;
    public bool Events;
    public bool LeaderboardEnd;
    public bool Comeback;
    public int? QuietStartHour;
    public int? QuietEndHour;
}

public interface IPushGateway
{
    Task<PushDeviceState> RegisterDeviceAsync(
        PushDeviceRegistration registration,
        CancellationToken cancellationToken);
    Task UnregisterDeviceAsync(string deviceId, CancellationToken cancellationToken);
    Task<ClientPushPreferences> GetPreferencesAsync(CancellationToken cancellationToken);
    Task<ClientPushPreferences> UpdatePreferencesAsync(
        ClientPushPreferences preferences,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed class SocialLinkState
{
    public string LinkId;
    public string Provider;
    public string LinkedAt;
}

public sealed class SocialFriendState
{
    public string PlayerId;
    public string Provider;
}

public sealed class SocialSyncState
{
    public string Provider;
    public int SyncedCount;
    public List<SocialFriendState> Friends = new List<SocialFriendState>();
}

public interface ISocialGateway
{
    Task<SocialLinkState> LinkAsync(
        string provider,
        string authorizationCode,
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task UnlinkAsync(string provider, CancellationToken cancellationToken);
    Task<SocialSyncState> SyncFriendsAsync(
        string provider,
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SocialFriendState>> GetFriendsAsync(
        string provider,
        CancellationToken cancellationToken);
}

public sealed class GameplayRunSnapshotRequest
{
    public string RunId;
    public string SnapshotType;
    public int? WaveNumber;
    public int? CurrentHealth;
    public long EventSequence;
    public byte[] Payload;
    public string PreviousHash;
    public string CurrentHash;
}

public sealed class GameplayRunSnapshotResult
{
    public long SnapshotId;
    public string RunId;
    public long EventSequence;
    public int PayloadSize;
    public string PayloadDigest;
    public string PayloadReference;
    public string CurrentHash;
    public string CreatedAt;
    public bool Replayed;
}

public interface IGameplayRunSnapshotGateway
{
    Task<GameplayRunSnapshotResult> SaveSnapshotAsync(
        GameplayRunSnapshotRequest request,
        CancellationToken cancellationToken);
}

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
    IRunForgePickGateway RunForgePick { get; }
    IForgekeepersGiftGateway ForgekeepersGift { get; }
    IShareService Share { get; }
    ISignInGateway SignIn { get; }
    DragonBound.Services.IGameplayRunGateway Gameplay { get; }
    IGameplayRunSnapshotGateway GameplaySnapshots { get; }
    ICloudSaveGateway CloudSave { get; }
    IPushGateway Push { get; }
    ISocialGateway Social { get; }
}
