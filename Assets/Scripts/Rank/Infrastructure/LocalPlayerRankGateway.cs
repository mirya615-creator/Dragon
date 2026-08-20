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
    private const string ReachedStateAtSegment = ".reached-state-at";

    private readonly LocalLeaderboardPeriodStore leaderboardStore;

    public LocalPlayerRankGateway(LocalLeaderboardPeriodStore leaderboardStore)
    {
        this.leaderboardStore = leaderboardStore ??
            throw new ArgumentNullException(nameof(leaderboardStore));
    }

    public Task<PlayerRankState> GetRankAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = GetRankKey(playerId);
        return Task.FromResult(LoadState(key));
    }

    public Task<RankProgressResult> RecordVictoryAsync(
        string playerId,
        string matchId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMatchId(matchId);

        string key = GetRankKey(playerId);
        string matchKey = key + MatchKeySegment + HashKey(matchId);
        PlayerRankState previousState = LoadState(key);
        if (PlayerPrefs.HasKey(matchKey))
        {
            return Task.FromResult(CreateUnchangedResult(previousState));
        }

        long updatedTotal = previousState.TotalRankStars < long.MaxValue
            ? previousState.TotalRankStars + 1
            : previousState.TotalRankStars;
        PlayerRankState updatedState = RankProgressionRules.Calculate(updatedTotal);
        long reachedAt = HasRankingStateChanged(previousState, updatedState)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            : previousState.ReachedStateAtUnixMilliseconds;
        updatedState.ReachedStateAtUnixMilliseconds = reachedAt;

        bool promoted = previousState.Level != updatedState.Level ||
                        previousState.Division != updatedState.Division;
        SaveState(key, updatedState);
        PlayerPrefs.SetInt(matchKey, 1);
        PlayerPrefs.Save();
        RecordLeaderboardState(playerId, updatedState);

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
        ValidateMatchId(matchId);

        string key = GetRankKey(playerId);
        string matchKey = key + MatchKeySegment + HashKey(matchId);
        PlayerRankState previousState = LoadState(key);
        if (PlayerPrefs.HasKey(matchKey))
        {
            return Task.FromResult(CreateUnchangedResult(previousState));
        }

        long updatedTotal = RankProgressionRules.CalculateTotalAfterDefeat(
            previousState.TotalRankStars);
        PlayerRankState updatedState = RankProgressionRules.Calculate(updatedTotal);
        long reachedAt = HasRankingStateChanged(previousState, updatedState)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            : previousState.ReachedStateAtUnixMilliseconds;
        updatedState.ReachedStateAtUnixMilliseconds = reachedAt;

        SaveState(key, updatedState);
        PlayerPrefs.SetInt(matchKey, 1);
        PlayerPrefs.Save();
        RecordLeaderboardState(playerId, updatedState);
        return Task.FromResult(CreateUnchangedResult(updatedState));
    }

    private void RecordLeaderboardState(string playerId, PlayerRankState state)
    {
        leaderboardStore.RecordCurrentPeriods(
            playerId,
            "You",
            state,
            state.ReachedStateAtUnixMilliseconds,
            DateTimeOffset.UtcNow);
    }

    private static PlayerRankState LoadState(string key)
    {
        long totalStars = LoadTotalStars(key);
        PlayerRankState state = RankProgressionRules.Calculate(totalStars);
        state.ReachedStateAtUnixMilliseconds = LoadReachedStateAt(key);
        return state;
    }

    private static long LoadReachedStateAt(string key)
    {
        string reachedKey = key + ReachedStateAtSegment;
        string value = PlayerPrefs.GetString(reachedKey, string.Empty);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long reachedAt) &&
            reachedAt > 0)
        {
            return reachedAt;
        }

        reachedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        PlayerPrefs.SetString(reachedKey, reachedAt.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        return reachedAt;
    }

    private static void SaveState(string key, PlayerRankState state)
    {
        PlayerPrefs.SetString(
            key,
            state.TotalRankStars.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(
            key + ReachedStateAtSegment,
            state.ReachedStateAtUnixMilliseconds.ToString(CultureInfo.InvariantCulture));
    }

    private static bool HasRankingStateChanged(PlayerRankState previous, PlayerRankState current)
    {
        if (previous.Level != current.Level) return true;
        if (current.Level >= 10)
            return previous.TotalRankStars != current.TotalRankStars;
        return previous.Division != current.Division ||
               previous.CurrentStars != current.CurrentStars;
    }

    private static RankProgressResult CreateUnchangedResult(PlayerRankState state)
    {
        return new RankProgressResult
        {
            State = state,
            PromotionFromState = null,
            Promoted = false
        };
    }

    private static void ValidateMatchId(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            throw new ArgumentException("Match ID is required.", nameof(matchId));
    }

    private static string GetRankKey(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        return RankKeyPrefix + HashKey(playerId);
    }

    private static long LoadTotalStars(string key)
    {
        string value = PlayerPrefs.GetString(key, "0");
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long stars) ||
            stars < 0)
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
