using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Stores the local player's completed player-vs-AI results for loading-screen presentation.
/// Server-owned profile statistics can replace this snapshot without changing the UI binder.
/// </summary>
public static class GameplayWinRateProfile
{
    private const string PreferenceKeyPrefix = "dragonbound.gameplay-win-rate.v1.";

    public readonly struct Snapshot
    {
        public Snapshot(int playerWins, int aiWins)
        {
            PlayerWins = Mathf.Max(0, playerWins);
            AiWins = Mathf.Max(0, aiWins);
        }

        public int PlayerWins { get; }
        public int AiWins { get; }
        public int CompletedMatches => PlayerWins + AiWins;
        public int PlayerRatePercent => CalculateRate(PlayerWins, CompletedMatches);
        public int AiRatePercent => CalculateRate(AiWins, CompletedMatches);
    }

    [Serializable]
    private sealed class SaveData
    {
        public int PlayerWins;
        public int AiWins;
    }

    public static Snapshot Load(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return new Snapshot(0, 0);
        }

        string json = PlayerPrefs.GetString(GetPreferenceKey(playerId), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Snapshot(0, 0);
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            return data == null
                ? new Snapshot(0, 0)
                : new Snapshot(data.PlayerWins, data.AiWins);
        }
        catch (Exception)
        {
            return new Snapshot(0, 0);
        }
    }

    public static Snapshot RecordCompletedMatch(string playerId, bool playerWon)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return new Snapshot(0, 0);
        }

        Snapshot current = Load(playerId);
        int playerWins = current.PlayerWins;
        int aiWins = current.AiWins;
        if (playerWon)
        {
            if (playerWins < int.MaxValue) playerWins++;
        }
        else if (aiWins < int.MaxValue)
        {
            aiWins++;
        }

        var data = new SaveData { PlayerWins = playerWins, AiWins = aiWins };
        PlayerPrefs.SetString(GetPreferenceKey(playerId), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        return new Snapshot(playerWins, aiWins);
    }

    public static string FormatRate(int percent)
    {
        return Mathf.Clamp(percent, 0, 100) + "%";
    }

    private static int CalculateRate(int wins, int completedMatches)
    {
        return completedMatches <= 0
            ? 0
            : Mathf.Clamp(Mathf.RoundToInt(wins * 100f / completedMatches), 0, 100);
    }

    private static string GetPreferenceKey(string playerId)
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(playerId.Trim()))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return PreferenceKeyPrefix + encoded;
    }
}
