using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerPrefs-backed weekly/monthly leaderboard snapshots for offline development.
/// Periods are global UTC periods and never use a player DayKey or local timezone.
/// </summary>
public sealed class LocalLeaderboardPeriodStore
{
    private const string KeyPrefix = "dragonbound.leaderboard.";

    [Serializable]
    private sealed class BoardData
    {
        public List<LeaderboardPlayer> Players = new List<LeaderboardPlayer>();
    }

    public void RecordCurrentPeriods(
        string playerId,
        string displayName,
        PlayerRankState state,
        long reachedStateAtUnixMilliseconds,
        DateTimeOffset utcNow)
    {
        if (state == null || string.IsNullOrWhiteSpace(playerId)) return;
        Upsert(
            LeaderboardPeriodResolver.Resolve(LeaderboardPeriodType.Weekly, utcNow),
            CreatePlayer(playerId, displayName, state, reachedStateAtUnixMilliseconds));
        Upsert(
            LeaderboardPeriodResolver.Resolve(LeaderboardPeriodType.Monthly, utcNow),
            CreatePlayer(playerId, displayName, state, reachedStateAtUnixMilliseconds));
    }

    public IReadOnlyList<LeaderboardPlayer> GetPlayers(LeaderboardPeriod period)
    {
        return Load(period).Players;
    }

    public void Upsert(LeaderboardPeriod period, LeaderboardPlayer player)
    {
        if (period == null || player == null || string.IsNullOrWhiteSpace(player.PlayerId)) return;

        BoardData data = Load(period);
        int existingIndex = data.Players.FindIndex(candidate =>
            candidate != null && string.Equals(
                candidate.PlayerId,
                player.PlayerId,
                StringComparison.Ordinal));
        LeaderboardPlayer copy = Clone(player);
        if (existingIndex >= 0)
        {
            data.Players[existingIndex] = copy;
        }
        else
        {
            data.Players.Add(copy);
        }
        Save(period, data);
    }

    private static LeaderboardPlayer CreatePlayer(
        string playerId,
        string displayName,
        PlayerRankState state,
        long reachedStateAtUnixMilliseconds)
    {
        return new LeaderboardPlayer
        {
            PlayerId = playerId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName,
            RankLevel = state.Level,
            Division = state.Division,
            CurrentStars = state.CurrentStars,
            TotalRankStars = state.TotalRankStars,
            ReachedStateAtUnixMilliseconds = reachedStateAtUnixMilliseconds
        };
    }

    private static LeaderboardPlayer Clone(LeaderboardPlayer source)
    {
        return new LeaderboardPlayer
        {
            PlayerId = source.PlayerId,
            DisplayName = source.DisplayName,
            RankLevel = source.RankLevel,
            Division = source.Division,
            CurrentStars = source.CurrentStars,
            TotalRankStars = source.TotalRankStars,
            ReachedStateAtUnixMilliseconds = source.ReachedStateAtUnixMilliseconds
        };
    }

    private static BoardData Load(LeaderboardPeriod period)
    {
        string key = GetKey(period);
        if (!PlayerPrefs.HasKey(key)) return CreateSeedData(period);

        try
        {
            BoardData data = JsonUtility.FromJson<BoardData>(PlayerPrefs.GetString(key, string.Empty));
            if (data == null) return CreateSeedData(period);
            if (data.Players == null) data.Players = new List<LeaderboardPlayer>();
            return data;
        }
        catch (ArgumentException)
        {
            return CreateSeedData(period);
        }
    }

    private static void Save(LeaderboardPeriod period, BoardData data)
    {
        PlayerPrefs.SetString(GetKey(period), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private static string GetKey(LeaderboardPeriod period)
    {
        if (period == null || string.IsNullOrWhiteSpace(period.PeriodKey))
            throw new ArgumentException("Leaderboard period is required.", nameof(period));
        return KeyPrefix + period.PeriodKey;
    }

    private static BoardData CreateSeedData(LeaderboardPeriod period)
    {
        string[] ids =
        {
            "training-aria", "training-borin", "training-cyra", "training-doran",
            "training-elin", "training-fenn", "training-gale", "training-hara",
            "training-ivo", "training-juna", "training-kael", "training-lyra"
        };
        string[] names =
        {
            "Aria", "Borin", "Cyra", "Doran", "Elin", "Fenn",
            "Gale", "Hara", "Ivo", "Juna", "Kael", "Lyra"
        };
        long[] weeklyStars = { 137, 119, 104, 91, 67, 28, 112, 109, 102, 83, 51, 12 };
        long[] monthlyStars = { 116, 141, 98, 107, 73, 35, 121, 88, 110, 79, 56, 18 };
        long[] selectedStars = period.Type == LeaderboardPeriodType.Weekly
            ? weeklyStars
            : monthlyStars;

        var data = new BoardData();
        for (int index = 0; index < ids.Length; index++)
        {
            PlayerRankState rank = RankProgressionRules.Calculate(selectedStars[index]);
            data.Players.Add(CreatePlayer(
                ids[index],
                names[index],
                rank,
                period.StartsAtUnixMilliseconds + (index + 1) * 1000L));
        }
        return data;
    }
}
