using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class PendingForgekeepersGiftClaim
{
    public string RunId;
    public string ClaimId;
    public string ClientEventId;
    public string IntentIdempotencyKey;
    public string ClaimIdempotencyKey;
    public int OpportunityIndex;
    public bool AdCompleted;
    public string GrantedUnitRuntimeId;
}

public static class ForgekeepersGiftClaimStore
{
    private const string PendingPrefix = "dragonbound.forgekeepers-gift.pending.v1.";
    private const string HandledPrefix = "dragonbound.forgekeepers-gift.handled.v1.";

    public static bool TryGetPending(string runId, out PendingForgekeepersGiftClaim pending)
    {
        Validate(runId);
        pending = null;
        string json = PlayerPrefs.GetString(PendingPrefix + Hash(runId), string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            PendingForgekeepersGiftClaim value =
                JsonUtility.FromJson<PendingForgekeepersGiftClaim>(json);
            if (value == null ||
                !string.Equals(value.RunId, runId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(value.ClaimId) ||
                string.IsNullOrWhiteSpace(value.ClaimIdempotencyKey))
            {
                ClearPending(runId);
                return false;
            }
            pending = value;
            return true;
        }
        catch (ArgumentException)
        {
            ClearPending(runId);
            return false;
        }
    }

    public static void SavePending(PendingForgekeepersGiftClaim pending)
    {
        if (pending == null) throw new ArgumentNullException(nameof(pending));
        Validate(pending.RunId);
        Validate(pending.ClaimId);
        Validate(pending.ClaimIdempotencyKey);
        PlayerPrefs.SetString(
            PendingPrefix + Hash(pending.RunId),
            JsonUtility.ToJson(pending));
        PlayerPrefs.Save();
    }

    public static void ClearPending(string runId)
    {
        Validate(runId);
        PlayerPrefs.DeleteKey(PendingPrefix + Hash(runId));
        PlayerPrefs.Save();
    }

    public static bool IsHandled(string runId, string claimId)
    {
        Validate(runId);
        Validate(claimId);
        return PlayerPrefs.GetInt(HandledPrefix + Hash(runId + "|" + claimId), 0) == 1;
    }

    public static void MarkHandled(string runId, string claimId)
    {
        Validate(runId);
        Validate(claimId);
        PlayerPrefs.SetInt(HandledPrefix + Hash(runId + "|" + claimId), 1);
        PlayerPrefs.Save();
    }

    private static string Hash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(32);
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
