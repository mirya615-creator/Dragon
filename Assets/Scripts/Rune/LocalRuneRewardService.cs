using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Local development implementation. Replace this boundary with the Go run-settlement unary call.
/// </summary>
public sealed class LocalRuneRewardService
{
    private const string ProfileKeyPrefix = "dragonbound.runes.";
    private const string SettledRunSegment = ".settled-run.";

    public RuneProfile SettleRun(string playerId, string runId, IReadOnlyList<RuneReward> rewards)
    {
        ValidateIdentity(playerId, runId);
        string profileKey = GetProfileKey(playerId);
        string settledKey = profileKey + SettledRunSegment + HashKey(runId);
        RuneProfile profile = LoadProfileByKey(profileKey);

        if (PlayerPrefs.HasKey(settledKey)) return profile;

        profile.LastRunRewards.Clear();
        if (rewards != null)
        {
            for (int index = 0; index < rewards.Count && index < 4; index++)
            {
                RuneReward reward = rewards[index];
                if (reward == null || RuneCatalog.Find(reward.RuneId) == null) continue;

                RuneInventoryEntry entry = FindOrCreateEntry(profile, reward.RuneId);
                int safeAmount = Math.Max(1, reward.Amount);
                if (reward.RewardKind == RuneRewardKind.CompleteRune)
                {
                    entry.OwnedCount += safeAmount;
                }
                else
                {
                    entry.FragmentCount += safeAmount;
                }

                profile.LastRunRewards.Add(CloneReward(reward));
            }
        }

        SaveProfile(profileKey, profile);
        PlayerPrefs.SetInt(settledKey, 1);
        PlayerPrefs.Save();
        return profile;
    }

    public RuneProfile GetProfile(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return new RuneProfile();
        return LoadProfileByKey(GetProfileKey(playerId));
    }

    public bool TryEquipRune(
        string playerId,
        string heroId,
        string runeId,
        out RuneProfile updatedProfile)
    {
        if (string.IsNullOrWhiteSpace(playerId) ||
            string.IsNullOrWhiteSpace(heroId) ||
            RuneCatalog.Find(runeId) == null)
        {
            updatedProfile = new RuneProfile();
            return false;
        }

        string profileKey = GetProfileKey(playerId);
        RuneProfile profile = LoadProfileByKey(profileKey);
        RuneInventoryEntry inventory = FindInventoryEntry(profile, runeId);
        if (inventory == null)
        {
            updatedProfile = profile;
            return false;
        }

        HeroRuneLoadoutEntry heroLoadout = FindHeroLoadout(profile, heroId);
        int assignedToOtherHeroes = CountAssignedRunes(
            profile,
            runeId,
            heroLoadout != null ? heroId : null);
        if (inventory.OwnedCount <= assignedToOtherHeroes)
        {
            updatedProfile = profile;
            return false;
        }

        if (heroLoadout == null)
        {
            heroLoadout = new HeroRuneLoadoutEntry { HeroId = heroId };
            profile.Loadouts.Add(heroLoadout);
        }
        heroLoadout.RuneId = runeId;

        SaveProfile(profileKey, profile);
        updatedProfile = profile;
        return true;
    }

    private static RuneProfile LoadProfileByKey(string profileKey)
    {
        string json = PlayerPrefs.GetString(profileKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return new RuneProfile();

        try
        {
            RuneProfile profile = JsonUtility.FromJson<RuneProfile>(json);
            if (profile == null) return new RuneProfile();
            if (profile.Inventory == null) profile.Inventory = new List<RuneInventoryEntry>();
            if (profile.LastRunRewards == null) profile.LastRunRewards = new List<RuneReward>();
            if (profile.Loadouts == null) profile.Loadouts = new List<HeroRuneLoadoutEntry>();
            return profile;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Rune profile is invalid and will be recreated: {exception.Message}");
            return new RuneProfile();
        }
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
}
