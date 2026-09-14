using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class PendingRunForgePickClaim
{
    public string RunId;
    public string ClaimId;
    public string ClientEventId;
    public string IntentIdempotencyKey;
    public string ClaimIdempotencyKey;
}

public static class PendingRunForgePickClaimStore
{
    private const string KeyPrefix = "dragonbound.run-forge-pick.pending.v1.";

    public static bool TryGet(string runId, out PendingRunForgePickClaim pending)
    {
        Validate(runId);
        pending = null;
        string json = PlayerPrefs.GetString(GetKey(runId), string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            PendingRunForgePickClaim value = JsonUtility.FromJson<PendingRunForgePickClaim>(json);
            if (value == null || !string.Equals(value.RunId, runId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(value.ClaimId) ||
                string.IsNullOrWhiteSpace(value.ClaimIdempotencyKey))
            {
                Clear(runId);
                return false;
            }
            pending = value;
            return true;
        }
        catch (ArgumentException)
        {
            Clear(runId);
            return false;
        }
    }

    public static void Save(PendingRunForgePickClaim pending)
    {
        if (pending == null) throw new ArgumentNullException(nameof(pending));
        Validate(pending.RunId);
        Validate(pending.ClaimId);
        Validate(pending.ClaimIdempotencyKey);
        PlayerPrefs.SetString(GetKey(pending.RunId), JsonUtility.ToJson(pending));
        PlayerPrefs.Save();
    }

    public static void Clear(string runId)
    {
        Validate(runId);
        PlayerPrefs.DeleteKey(GetKey(runId));
        PlayerPrefs.Save();
    }

    private static string GetKey(string runId)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(runId));
            var builder = new StringBuilder(KeyPrefix, KeyPrefix.Length + 32);
            for (int index = 0; index < 16; index++)
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", nameof(value));
    }
}
