using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface ILocalSignInRewardSink
{
    Task<IReadOnlyCollection<string>> GetOwnedRuneIdsAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task ApplyAsync(
        string playerId,
        string claimId,
        IReadOnlyList<SignInReward> rewards,
        CancellationToken cancellationToken);
}

/// <summary>Applies preview sign-in rewards to the existing local gold and rune stores.</summary>
public sealed class LocalSignInRewardSink : ILocalSignInRewardSink
{
    private readonly LocalPlayerGoldGateway gold;
    private readonly LocalRuneRewardService runes;

    public LocalSignInRewardSink(
        LocalPlayerGoldGateway gold,
        LocalRuneRewardService runes)
    {
        this.gold = gold ?? throw new ArgumentNullException(nameof(gold));
        this.runes = runes ?? throw new ArgumentNullException(nameof(runes));
    }

    public async Task<IReadOnlyCollection<string>> GetOwnedRuneIdsAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        RuneProfile profile = await runes.GetProfileAsync(playerId, cancellationToken);
        var owned = new HashSet<string>(StringComparer.Ordinal);
        foreach (RuneInventoryEntry entry in profile.Inventory)
        {
            if (entry != null && entry.OwnedCount > 0 && !string.IsNullOrWhiteSpace(entry.RuneId))
                owned.Add(entry.RuneId);
        }
        return owned;
    }

    public async Task ApplyAsync(
        string playerId,
        string claimId,
        IReadOnlyList<SignInReward> rewards,
        CancellationToken cancellationToken)
    {
        long goldAmount = 0;
        var runeRewards = new List<RuneReward>();
        foreach (SignInReward reward in rewards)
        {
            if (reward == null || reward.Amount <= 0) continue;
            if (reward.Type == SignInRewardType.Gold)
            {
                goldAmount = checked(goldAmount + reward.Amount);
                continue;
            }

            runeRewards.Add(new RuneReward
            {
                RuneId = reward.RuneId,
                DisplayName = reward.DisplayName,
                Rarity = reward.RuneRarity,
                RewardKind = RuneRewardKind.CompleteRune,
                Amount = reward.Amount
            });
        }

        if (goldAmount > 0)
            await gold.GrantDevelopmentRewardAsync(
                playerId, goldAmount, claimId + ":gold", cancellationToken);
        if (runeRewards.Count > 0)
            await runes.GrantDevelopmentRewardsAsync(
                playerId, claimId + ":runes", runeRewards, cancellationToken);
    }
}

/// <summary>
/// Seven-day local sign-in preview. UTC DayKey is used until the server becomes
/// authoritative. Missing a day never resets TotalClaims.
/// </summary>
public sealed class LocalSignInGateway : ISignInGateway
{
    private const string ProfileKeyPrefix = "dragonbound.signin.v1.";
    private readonly IShareService shareService;
    private readonly ILocalSignInRewardSink rewardSink;
    private readonly Func<DateTimeOffset> utcNow;

    [Serializable]
    private sealed class LocalProfile
    {
        public int TotalClaims;
        public string LastClaimDayKey;
        public List<SignInReward> LastRewards = new List<SignInReward>();
    }

    public LocalSignInGateway(
        IShareService shareService,
        ILocalSignInRewardSink rewardSink)
        : this(shareService, rewardSink, () => DateTimeOffset.UtcNow)
    {
    }

    public LocalSignInGateway(
        IShareService shareService,
        ILocalSignInRewardSink rewardSink,
        Func<DateTimeOffset> utcNow)
    {
        this.shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
        this.rewardSink = rewardSink ?? throw new ArgumentNullException(nameof(rewardSink));
        this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public Task<SignInStatus> GetStatusAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        LocalProfile profile = Load(playerId);
        string dayKey = ResolveDayKey(utcNow());
        bool canClaim = !string.Equals(profile.LastClaimDayKey, dayKey, StringComparison.Ordinal);
        int cycleDay = ResolveCycleDay(profile.TotalClaims, canClaim);
        return Task.FromResult(new SignInStatus
        {
            DayKey = dayKey,
            CycleDay = cycleDay,
            TotalClaims = profile.TotalClaims,
            CanClaim = canClaim,
            DoubleAvailable = true,
            PreviewReward = CreatePreview(cycleDay)
        });
    }

    public async Task<SignInClaimResult> ClaimAsync(
        string playerId,
        string expectedDayKey,
        SignInClaimMode mode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        string dayKey = ResolveDayKey(utcNow());
        if (!string.Equals(expectedDayKey, dayKey, StringComparison.Ordinal))
            throw new InvalidOperationException("The sign-in DayKey changed. Refresh status and try again.");

        LocalProfile profile = Load(playerId);
        if (string.Equals(profile.LastClaimDayKey, dayKey, StringComparison.Ordinal))
            return Replay(profile, dayKey);

        if (mode == SignInClaimMode.SharedDouble)
        {
            ShareResult share = await shareService.ShareAsync(
                new ShareRequest
                {
                    PlacementId = "signin_double",
                    Message = "Join me in DragonBound!"
                },
                cancellationToken);
            if (share != ShareResult.Completed)
                throw new InvalidOperationException("Sharing was not completed.");
        }

        int cycleDay = ResolveCycleDay(profile.TotalClaims, true);
        IReadOnlyCollection<string> ownedRunes = await rewardSink.GetOwnedRuneIdsAsync(
            playerId, cancellationToken);
        List<SignInReward> rewards = BuildRewards(
            playerId, dayKey, cycleDay, mode == SignInClaimMode.SharedDouble ? 2 : 1, ownedRunes);
        string claimId = "signin:" + HashKey(playerId + "|" + dayKey);

        // The sink is independently idempotent by claimId. If saving the sign-in profile
        // is interrupted, retrying does not duplicate currency or runes.
        await rewardSink.ApplyAsync(playerId, claimId, rewards, cancellationToken);
        profile.TotalClaims = checked(profile.TotalClaims + 1);
        profile.LastClaimDayKey = dayKey;
        profile.LastRewards = CloneRewards(rewards);
        Save(playerId, profile);

        return new SignInClaimResult
        {
            Applied = true,
            Replayed = false,
            DayKey = dayKey,
            CycleDay = cycleDay,
            TotalClaims = profile.TotalClaims,
            ClaimMode = mode,
            Rewards = CloneRewards(rewards)
        };
    }

    public static int ResolveCycleDay(int totalClaims, bool canClaim)
    {
        if (totalClaims < 0) throw new ArgumentOutOfRangeException(nameof(totalClaims));
        int ordinal = canClaim ? totalClaims : Math.Max(0, totalClaims - 1);
        return (ordinal % 7) + 1;
    }

    public static SignInReward CreatePreview(int cycleDay)
    {
        switch (cycleDay)
        {
            case 1: return Gold(40);
            case 2: return Gold(60);
            case 3: return Rune(RuneRarity.Epic, 1, string.Empty, "Random Complete Epic Rune");
            case 4: return Gold(80);
            case 5: return Gold(100);
            case 6: return Gold(160);
            case 7: return Rune(RuneRarity.Legendary, 1, string.Empty, "Random Complete Legendary Rune");
            default: throw new ArgumentOutOfRangeException(nameof(cycleDay));
        }
    }

    private static List<SignInReward> BuildRewards(
        string playerId,
        string dayKey,
        int cycleDay,
        int multiplier,
        IReadOnlyCollection<string> ownedRuneIds)
    {
        SignInReward preview = CreatePreview(cycleDay);
        if (preview.Type == SignInRewardType.Gold)
            return new List<SignInReward> { Gold(checked(preview.Amount * multiplier)) };

        var rewards = new List<SignInReward>();
        var owned = new HashSet<string>(ownedRuneIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        for (int ordinal = 0; ordinal < multiplier; ordinal++)
        {
            RuneDefinition selected = SelectRune(
                preview.RuneRarity, owned, playerId, dayKey, cycleDay, ordinal);
            rewards.Add(Rune(selected.Rarity, 1, selected.RuneId, selected.DisplayName));
            owned.Add(selected.RuneId);
        }
        return rewards;
    }

    private static RuneDefinition SelectRune(
        RuneRarity rarity,
        HashSet<string> owned,
        string playerId,
        string dayKey,
        int cycleDay,
        int ordinal)
    {
        var fullPool = new List<RuneDefinition>();
        var unowned = new List<RuneDefinition>();
        foreach (RuneDefinition definition in RuneCatalog.All)
        {
            if (definition.Rarity != rarity) continue;
            fullPool.Add(definition);
            if (!owned.Contains(definition.RuneId)) unowned.Add(definition);
        }
        List<RuneDefinition> candidates = unowned.Count > 0 ? unowned : fullPool;
        if (candidates.Count == 0)
            throw new InvalidOperationException("The requested sign-in rune pool is empty.");

        int index = StableIndex(
            playerId + "|" + dayKey + "|" + cycleDay.ToString(CultureInfo.InvariantCulture) +
            "|" + ordinal.ToString(CultureInfo.InvariantCulture),
            candidates.Count);
        return candidates[index];
    }

    private static int StableIndex(string value, int count)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            uint number = ((uint)digest[0] << 24) | ((uint)digest[1] << 16) |
                          ((uint)digest[2] << 8) | digest[3];
            return (int)(number % (uint)count);
        }
    }

    private static SignInReward Gold(int amount)
    {
        return new SignInReward
        {
            Type = SignInRewardType.Gold,
            Amount = amount,
            DisplayName = "Gold"
        };
    }

    private static SignInReward Rune(
        RuneRarity rarity,
        int amount,
        string runeId,
        string displayName)
    {
        return new SignInReward
        {
            Type = SignInRewardType.CompleteRune,
            Amount = amount,
            RuneId = runeId,
            DisplayName = displayName,
            RuneRarity = rarity
        };
    }

    private static SignInClaimResult Replay(LocalProfile profile, string dayKey)
    {
        return new SignInClaimResult
        {
            Applied = false,
            Replayed = true,
            DayKey = dayKey,
            CycleDay = ResolveCycleDay(profile.TotalClaims, false),
            TotalClaims = profile.TotalClaims,
            Rewards = CloneRewards(profile.LastRewards)
        };
    }

    private static List<SignInReward> CloneRewards(IReadOnlyList<SignInReward> source)
    {
        var result = new List<SignInReward>();
        if (source == null) return result;
        foreach (SignInReward reward in source)
        {
            if (reward == null) continue;
            result.Add(new SignInReward
            {
                Type = reward.Type,
                Amount = reward.Amount,
                RuneId = reward.RuneId,
                DisplayName = reward.DisplayName,
                RuneRarity = reward.RuneRarity
            });
        }
        return result;
    }

    private static LocalProfile Load(string playerId)
    {
        string json = PlayerPrefs.GetString(ProfileKey(playerId), string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new LocalProfile();
        try
        {
            LocalProfile profile = JsonUtility.FromJson<LocalProfile>(json) ?? new LocalProfile();
            if (profile.TotalClaims < 0) profile.TotalClaims = 0;
            if (profile.LastRewards == null) profile.LastRewards = new List<SignInReward>();
            return profile;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Invalid local sign-in profile was reset: " + exception.Message);
            return new LocalProfile();
        }
    }

    private static void Save(string playerId, LocalProfile profile)
    {
        PlayerPrefs.SetString(ProfileKey(playerId), JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    private static string ProfileKey(string playerId)
    {
        return ProfileKeyPrefix + HashKey(playerId);
    }

    private static string HashKey(string value)
    {
        using (SHA256 sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace('/', '_').Replace('+', '-').TrimEnd('=');
    }

    private static string ResolveDayKey(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void ValidatePlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
    }
}
