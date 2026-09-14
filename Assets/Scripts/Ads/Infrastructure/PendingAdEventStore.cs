using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Persists one unsettled ad success event per player, placement and reward scope.
/// This makes a retry reuse the same server idempotency identity after an ambiguous
/// network failure or application restart.
/// </summary>
public static class PendingAdEventStore
{
    private const string KeyPrefix = "dragonbound.ad.pending.v1.";

    public static string GetOrCreate(string playerId, string placementId, string rewardScope = "")
    {
        Validate(playerId, nameof(playerId));
        Validate(placementId, nameof(placementId));

        string key = GetStorageKey(playerId, placementId, rewardScope);
        string existing = PlayerPrefs.GetString(key, string.Empty);
        if (Guid.TryParseExact(existing, "N", out _)) return existing;

        string created = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, created);
        PlayerPrefs.Save();
        return created;
    }

    public static void Complete(
        string playerId,
        string placementId,
        string rewardScope,
        string eventId)
    {
        Validate(playerId, nameof(playerId));
        Validate(placementId, nameof(placementId));
        if (string.IsNullOrWhiteSpace(eventId)) return;

        string key = GetStorageKey(playerId, placementId, rewardScope);
        if (!string.Equals(PlayerPrefs.GetString(key, string.Empty), eventId, StringComparison.Ordinal))
            return;

        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    private static string GetStorageKey(string playerId, string placementId, string rewardScope)
    {
        using (SHA256 sha = SHA256.Create())
        {
            string source = playerId + "|" + placementId + "|" + (rewardScope ?? string.Empty);
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
            var builder = new StringBuilder(KeyPrefix, KeyPrefix.Length + 32);
            for (int index = 0; index < 16; index++)
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);
    }
}
