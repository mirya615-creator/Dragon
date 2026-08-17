using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Offline gold balance used during client development.
/// Replace this implementation with the Go match-settlement unary call.
/// </summary>
public sealed class LocalPlayerGoldGateway : IPlayerGoldGateway
{
    public const long VictoryReward = 20;
    public const long DefeatReward = 10;

    private const string GoldKeyPrefix = "dragonbound.player-gold.";
    private const string MatchKeySegment = ".match.";

    public Task<PlayerGoldState> GetGoldAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long balance = LoadBalance(GetGoldKey(playerId));
        return Task.FromResult(new PlayerGoldState { Balance = balance });
    }

    public Task<GoldSettlementResult> SettleMatchAsync(
        string playerId,
        string matchId,
        MatchOutcome outcome,
        GoldClaimType claimType,
        string adVerificationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException("Match ID is required.", nameof(matchId));
        }

        long reward = GetReward(outcome, claimType, adVerificationId);
        string goldKey = GetGoldKey(playerId);
        string matchKey = goldKey + MatchKeySegment + HashKey(matchId);
        long currentBalance = LoadBalance(goldKey);

        if (PlayerPrefs.HasKey(matchKey))
        {
            return Task.FromResult(new GoldSettlementResult
            {
                Reward = PlayerPrefs.GetInt(matchKey, 0),
                Balance = currentBalance,
                Applied = false
            });
        }

        long updatedBalance = currentBalance <= long.MaxValue - reward
            ? currentBalance + reward
            : long.MaxValue;
        long appliedReward = updatedBalance - currentBalance;

        PlayerPrefs.SetString(goldKey, updatedBalance.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetInt(matchKey, (int)appliedReward);
        PlayerPrefs.Save();

        return Task.FromResult(new GoldSettlementResult
        {
            Reward = appliedReward,
            Balance = updatedBalance,
            Applied = true
        });
    }

    private static long GetReward(
        MatchOutcome outcome,
        GoldClaimType claimType,
        string adVerificationId)
    {
        long baseReward;
        switch (outcome)
        {
            case MatchOutcome.Victory:
                baseReward = VictoryReward;
                break;
            case MatchOutcome.Defeat:
                baseReward = DefeatReward;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown match outcome.");
        }

        switch (claimType)
        {
            case GoldClaimType.Standard:
                return baseReward;
            case GoldClaimType.RewardedAd:
                if (string.IsNullOrWhiteSpace(adVerificationId))
                {
                    throw new ArgumentException(
                        "Ad verification ID is required for a rewarded-ad claim.",
                        nameof(adVerificationId));
                }
                return baseReward * 2;
            default:
                throw new ArgumentOutOfRangeException(nameof(claimType), claimType, "Unknown gold claim type.");
        }
    }

    private static string GetGoldKey(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        }
        return GoldKeyPrefix + HashKey(playerId);
    }

    private static long LoadBalance(string key)
    {
        string storedValue = PlayerPrefs.GetString(key, "0");
        if (!long.TryParse(storedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long balance) ||
            balance < 0)
        {
            balance = 0;
            PlayerPrefs.SetString(key, "0");
            PlayerPrefs.Save();
        }
        return balance;
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
