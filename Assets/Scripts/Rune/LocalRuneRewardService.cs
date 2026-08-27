using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Local development implementation. Replace this boundary with the Go run-settlement unary call.
/// </summary>
public sealed class LocalRuneRewardService : IRuneProfileGateway
{
    private const string ProfileKeyPrefix = "dragonbound.runes.";
    private const string SettledRunSegment = ".settled-run.";
    private const string GuestPaginationTestSegment = ".guest-pagination-test-v1";

    private RuneProfile RemoveLegacyGuestPaginationTestInventory(string playerId, int itemCount)
    {
        if (string.IsNullOrWhiteSpace(playerId) || itemCount <= 0)
        {
            return string.IsNullOrWhiteSpace(playerId)
                ? new RuneProfile()
                : LoadProfileByKey(GetProfileKey(playerId));
        }

        string profileKey = GetProfileKey(playerId);
        string testKey = profileKey + GuestPaginationTestSegment;
        RuneProfile profile = LoadProfileByKey(profileKey);
        if (!PlayerPrefs.HasKey(testKey)) return profile;

        IReadOnlyList<RuneDefinition> definitions = RuneCatalog.All;
        if (definitions == null || definitions.Count == 0) return profile;

        var random = new System.Random(StableSeed(playerId));
        for (int index = 0; index < itemCount; index++)
        {
            RuneDefinition definition = definitions[random.Next(definitions.Count)];
            RuneInventoryEntry entry = FindInventoryEntry(profile, definition.RuneId);
            if (entry == null) continue;

            int equippedCount = CountAssignedRunes(profile, definition.RuneId, null);
            entry.OwnedCount = Math.Max(equippedCount, entry.OwnedCount - 1);
        }

        for (int index = profile.Inventory.Count - 1; index >= 0; index--)
        {
            RuneInventoryEntry entry = profile.Inventory[index];
            if (entry.OwnedCount <= 0 && entry.FragmentCount <= 0)
            {
                profile.Inventory.RemoveAt(index);
            }
        }

        SaveProfile(profileKey, profile);
        PlayerPrefs.DeleteKey(testKey);
        PlayerPrefs.Save();
        return profile;
    }

    public Task<RuneProfile> SettleRunAsync(
        string playerId,
        string runId,
        IReadOnlyList<RuneReward> rewards,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateIdentity(playerId, runId);
        string profileKey = GetProfileKey(playerId);
        string settledKey = profileKey + SettledRunSegment + HashKey(runId);
        RuneProfile profile = LoadProfileByKey(profileKey);

        if (PlayerPrefs.HasKey(settledKey)) return Task.FromResult(profile);

        profile.LastRunRewards.Clear();
        if (profile.AccountDay >= 3 && rewards != null)
        {
            for (int index = 0; index < rewards.Count && index < 4; index++)
            {
                RuneReward reward = rewards[index];
                RuneDefinition definition = reward != null
                    ? RuneCatalog.Find(reward.RuneId)
                    : null;
                if (reward == null || definition == null) continue;

                RuneInventoryEntry entry = FindOrCreateEntry(profile, reward.RuneId);
                int safeAmount = Math.Max(1, reward.Amount);
                if (reward.RewardKind == RuneRewardKind.CompleteRune)
                {
                    entry.OwnedCount = AddWithoutOverflow(entry.OwnedCount, safeAmount);
                }
                else
                {
                    AddFragments(entry, definition, safeAmount);
                }

                profile.LastRunRewards.Add(CloneReward(reward));
            }
        }

        SaveProfile(profileKey, profile);
        PlayerPrefs.SetInt(settledKey, 1);
        PlayerPrefs.Save();
        return Task.FromResult(profile);
    }

    public Task<RuneProfile> GetProfileAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Task.FromResult(new RuneProfile());
        }

        // Removes the one-time inventory injected by an older guest pagination test.
        // The marker makes this a no-op for every normal local profile.
        return Task.FromResult(RemoveLegacyGuestPaginationTestInventory(playerId, 30));
    }

    public Task<RuneProfileMutationResult> EquipRuneAsync(
        string playerId,
        string heroId,
        string runeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(playerId) ||
            string.IsNullOrWhiteSpace(heroId) ||
            RuneCatalog.Find(runeId) == null)
        {
            return MutationResult(false, new RuneProfile());
        }

        string profileKey = GetProfileKey(playerId);
        RuneProfile profile = LoadProfileByKey(profileKey);
        if (profile.AccountDay < 3)
        {
            return MutationResult(false, profile);
        }
        RuneInventoryEntry inventory = FindInventoryEntry(profile, runeId);
        if (inventory == null)
        {
            return MutationResult(false, profile);
        }

        HeroRuneLoadoutEntry heroLoadout = FindHeroLoadout(profile, heroId);
        int assignedToOtherHeroes = CountAssignedRunes(
            profile,
            runeId,
            heroLoadout != null ? heroId : null);
        if (inventory.OwnedCount <= assignedToOtherHeroes)
        {
            return MutationResult(false, profile);
        }

        if (heroLoadout == null)
        {
            heroLoadout = new HeroRuneLoadoutEntry { HeroId = heroId };
            profile.Loadouts.Add(heroLoadout);
        }
        heroLoadout.RuneId = runeId;

        SaveProfile(profileKey, profile);
        return MutationResult(true, profile);
    }

    public Task<RuneProfileMutationResult> UnequipRuneAsync(
        string playerId,
        string heroId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(playerId) ||
            string.IsNullOrWhiteSpace(heroId))
        {
            return MutationResult(false, new RuneProfile());
        }

        string profileKey = GetProfileKey(playerId);
        RuneProfile profile = LoadProfileByKey(profileKey);
        if (profile.AccountDay < 3)
        {
            return MutationResult(false, profile);
        }
        HeroRuneLoadoutEntry heroLoadout = FindHeroLoadout(profile, heroId);
        if (heroLoadout == null || string.IsNullOrEmpty(heroLoadout.RuneId))
        {
            return MutationResult(false, profile);
        }

        profile.Loadouts.Remove(heroLoadout);
        SaveProfile(profileKey, profile);
        return MutationResult(true, profile);
    }

    private static Task<RuneProfileMutationResult> MutationResult(
        bool succeeded,
        RuneProfile profile)
    {
        return Task.FromResult(new RuneProfileMutationResult
        {
            Succeeded = succeeded,
            Profile = profile
        });
    }

    private static RuneProfile LoadProfileByKey(string profileKey)
    {
        string json = PlayerPrefs.GetString(profileKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return ApplyAccountDay(new RuneProfile());

        try
        {
            RuneProfile profile = JsonUtility.FromJson<RuneProfile>(json);
            if (profile == null) return ApplyAccountDay(new RuneProfile());
            profile.AccountDay = LocalRuneProgressionSettings.ResolveAccountDay(profile.AccountDay);
            if (profile.Inventory == null) profile.Inventory = new List<RuneInventoryEntry>();
            if (profile.LastRunRewards == null) profile.LastRunRewards = new List<RuneReward>();
            if (profile.Loadouts == null) profile.Loadouts = new List<HeroRuneLoadoutEntry>();
            bool normalizedFragments = NormalizeLegacyFragmentOverflow(profile);
            bool normalizedHeroes = NormalizeLegacyHeroIds(profile);
            if (normalizedFragments || normalizedHeroes)
            {
                SaveProfile(profileKey, profile);
            }
            return profile;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Rune profile is invalid and will be recreated: {exception.Message}");
            return ApplyAccountDay(new RuneProfile());
        }
    }

    private static RuneProfile ApplyAccountDay(RuneProfile profile)
    {
        profile.AccountDay = LocalRuneProgressionSettings.ResolveAccountDay(profile.AccountDay);
        return profile;
    }

    private static RuneInventoryEntry FindOrCreateEntry(RuneProfile profile, string runeId)
    {
        for (int index = 0; index < profile.Inventory.Count; index++)
        {
            if (profile.Inventory[index].RuneId == runeId) return profile.Inventory[index];
        }

        var entry = new RuneInventoryEntry { RuneId = runeId };
        profile.Inventory.Add(entry);
        return entry;
    }

    private static void AddFragments(
        RuneInventoryEntry entry,
        RuneDefinition definition,
        int amount)
    {
        int required = definition.RequiredFragments;
        if (required <= 0)
        {
            Debug.LogWarning($"Rune '{definition.RuneId}' does not support fragment rewards.");
            return;
        }

        long totalFragments = Math.Max(0, entry.FragmentCount) + (long)Math.Max(0, amount);
        long completedRunes = totalFragments / required;
        entry.OwnedCount = AddWithoutOverflow(entry.OwnedCount, completedRunes);
        entry.FragmentCount = (int)(totalFragments % required);
    }

    private static bool NormalizeLegacyFragmentOverflow(RuneProfile profile)
    {
        bool changed = false;
        for (int index = 0; index < profile.Inventory.Count; index++)
        {
            RuneInventoryEntry entry = profile.Inventory[index];
            if (entry == null) continue;

            if (entry.OwnedCount < 0)
            {
                entry.OwnedCount = 0;
                changed = true;
            }
            if (entry.FragmentCount < 0)
            {
                entry.FragmentCount = 0;
                changed = true;
            }

            RuneDefinition definition = RuneCatalog.Find(entry.RuneId);
            int required = definition != null ? definition.RequiredFragments : 0;
            if (required <= 0 || entry.FragmentCount < required) continue;

            int previousOwned = entry.OwnedCount;
            int previousFragments = entry.FragmentCount;
            AddFragments(entry, definition, 0);
            changed |= previousOwned != entry.OwnedCount ||
                       previousFragments != entry.FragmentCount;
        }
        return changed;
    }

    private static bool NormalizeLegacyHeroIds(RuneProfile profile)
    {
        bool changed = false;
        var assignedHeroes = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < profile.Loadouts.Count; index++)
        {
            HeroRuneLoadoutEntry loadout = profile.Loadouts[index];
            if (loadout == null) continue;

            string canonicalHeroId = HeroRuneIdentityCatalog.ResolvePersisted(loadout.HeroId);
            if (string.IsNullOrEmpty(canonicalHeroId)) continue;
            if (!string.Equals(loadout.HeroId, canonicalHeroId, StringComparison.Ordinal))
            {
                loadout.HeroId = canonicalHeroId;
                changed = true;
            }

            if (assignedHeroes.Add(canonicalHeroId)) continue;
            profile.Loadouts.RemoveAt(index--);
            changed = true;
        }

        return changed;
    }

    private static int AddWithoutOverflow(int current, long amount)
    {
        long safeCurrent = Math.Max(0, current);
        long safeAmount = Math.Max(0L, amount);
        return (int)Math.Min(int.MaxValue, safeCurrent + safeAmount);
    }

    private static RuneInventoryEntry FindInventoryEntry(RuneProfile profile, string runeId)
    {
        for (int index = 0; index < profile.Inventory.Count; index++)
        {
            if (profile.Inventory[index].RuneId == runeId) return profile.Inventory[index];
        }
        return null;
    }

    private static HeroRuneLoadoutEntry FindHeroLoadout(RuneProfile profile, string heroId)
    {
        for (int index = 0; index < profile.Loadouts.Count; index++)
        {
            if (profile.Loadouts[index].HeroId == heroId) return profile.Loadouts[index];
        }
        return null;
    }

    private static int CountAssignedRunes(
        RuneProfile profile,
        string runeId,
        string excludedHeroId)
    {
        int count = 0;
        for (int index = 0; index < profile.Loadouts.Count; index++)
        {
            HeroRuneLoadoutEntry loadout = profile.Loadouts[index];
            if (loadout.RuneId == runeId && loadout.HeroId != excludedHeroId) count++;
        }
        return count;
    }

    private static void SaveProfile(string profileKey, RuneProfile profile)
    {
        PlayerPrefs.SetString(profileKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    private static RuneReward CloneReward(RuneReward reward)
    {
        return new RuneReward
        {
            RuneId = reward.RuneId,
            DisplayName = reward.DisplayName,
            Rarity = reward.Rarity,
            RewardKind = reward.RewardKind,
            Amount = reward.Amount
        };
    }

    private static void ValidateIdentity(string playerId, string runId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));
    }

    private static string GetProfileKey(string playerId)
    {
        return ProfileKeyPrefix + HashKey(playerId);
    }

    private static string HashKey(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(digest).Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int index = 0; index < value.Length; index++)
            {
                hash = hash * 31 + value[index];
            }
            return hash;
        }
    }
}
