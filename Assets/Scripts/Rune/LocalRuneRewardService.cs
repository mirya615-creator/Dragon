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

        PlayerPrefs.SetString(profileKey, JsonUtility.ToJson(profile));
        PlayerPrefs.SetInt(settledKey, 1);
        PlayerPrefs.Save();
        return profile;
    }

    public RuneProfile GetProfile(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return new RuneProfile();
        return LoadProfileByKey(GetProfileKey(playerId));
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
