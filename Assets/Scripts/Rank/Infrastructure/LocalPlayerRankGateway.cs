using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Offline rank progress used during client development.
/// </summary>
public sealed class LocalPlayerRankGateway : IPlayerRankGateway
{
    private const string RankKeyPrefix = "dragonbound.player-rank.";
    private const string MatchKeySegment = ".match.";

    public Task<PlayerRankState> GetRankAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = GetRankKey(playerId);
        long totalStars = LoadTotalStars(key);
        return Task.FromResult(RankProgressionRules.Calculate(totalStars));
    }

    public Task<RankProgressResult> RecordVictoryAsync(
        string playerId,
        string matchId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException("Match ID is required.", nameof(matchId));
        }

        string key = GetRankKey(playerId);
        string matchKey = key + MatchKeySegment + HashKey(matchId);
        long currentTotal = LoadTotalStars(key);

        if (PlayerPrefs.HasKey(matchKey))
        {
            PlayerRankState currentState = RankProgressionRules.Calculate(currentTotal);
            return Task.FromResult(new RankProgressResult
            {
                State = currentState,
                PromotionFromState = null,
                Promoted = false
            });
        }

        PlayerRankState previousState = RankProgressionRules.Calculate(currentTotal);
        long updatedTotal = currentTotal < long.MaxValue ? currentTotal + 1 : currentTotal;
        PlayerRankState updatedState = RankProgressionRules.Calculate(updatedTotal);
        bool promoted = previousState.Level != updatedState.Level ||
                        previousState.Division != updatedState.Division;

        PlayerPrefs.SetString(key, updatedTotal.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetInt(matchKey, 1);
        PlayerPrefs.Save();

        return Task.FromResult(new RankProgressResult
        {
            State = updatedState,
            PromotionFromState = promoted
                ? RankProgressionRules.CreateFullPromotionState(previousState)
                : null,
            Promoted = promoted
        });
    }

    public Task<RankProgressResult> RecordDefeatAsync(
        string playerId,
        string matchId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException("Match ID is required.", nameof(matchId));
        }

        string key = GetRankKey(playerId);
        string matchKey = key + MatchKeySegment + HashKey(matchId);
        long currentTotal = LoadTotalStars(key);

        if (PlayerPrefs.HasKey(matchKey))
        {
            return Task.FromResult(CreateUnchangedResult(currentTotal));
        }

        long updatedTotal = RankProgressionRules.CalculateTotalAfterDefeat(currentTotal);
        PlayerPrefs.SetString(key, updatedTotal.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetInt(matchKey, 1);
        PlayerPrefs.Save();

        return Task.FromResult(CreateUnchangedResult(updatedTotal));
    }

    private static RankProgressResult CreateUnchangedResult(long totalStars)
    {
        return new RankProgressResult
        {
            State = RankProgressionRules.Calculate(totalStars),
            PromotionFromState = null,
            Promoted = false
        };
    }

    private static string GetRankKey(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        }
        return RankKeyPrefix + HashKey(playerId);
    }

    private static long LoadTotalStars(string key)
    {
        string value = PlayerPrefs.GetString(key, "0");
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long stars) || stars < 0)
        {
            stars = 0;
            PlayerPrefs.SetString(key, "0");
            PlayerPrefs.Save();
        }
        return stars;
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
