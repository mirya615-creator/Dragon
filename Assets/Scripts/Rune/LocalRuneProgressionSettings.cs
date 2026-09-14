using System;
using UnityEngine;

/// <summary>
/// Local development substitute for the future server-owned AccountDay value.
/// It never derives progression from device time.
/// </summary>
public static class LocalRuneProgressionSettings
{
    private const string AccountDayOverrideKey = "dragonbound.runes.dev-account-day-v1";
    private const int DevelopmentDefaultAccountDay = 1;

    /// <summary>
    /// Raised after the local development override changes so active rune UI can
    /// reload the profile without requiring a scene reload.
    /// </summary>
    public static event Action AccountDayChanged;

    public static bool IsDevelopmentOverrideActive
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return PlayerPrefs.HasKey(AccountDayOverrideKey);
#else
            return false;
#endif
        }
    }

    public static int ResolveAccountDay(int persistedAccountDay)
    {
        int fallback = Math.Max(1, persistedAccountDay);
        if (PlayerPrefs.HasKey(AccountDayOverrideKey))
        {
            return Math.Max(1, PlayerPrefs.GetInt(AccountDayOverrideKey, fallback));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development starts at day one so the three-day restriction is active by
        // default. An explicit PlayerPrefs override still wins, allowing each day
        // to be tested from the development menu without changing profile data.
        return Math.Max(DevelopmentDefaultAccountDay, fallback);
#else
        // Release builds keep using the persisted/server-owned account day.
        return fallback;
#endif
    }

    public static void SetDevelopmentAccountDay(int accountDay)
    {
        PlayerPrefs.SetInt(AccountDayOverrideKey, Math.Max(1, accountDay));
        PlayerPrefs.Save();
        AccountDayChanged?.Invoke();
    }

    public static void ClearDevelopmentOverride()
    {
        PlayerPrefs.DeleteKey(AccountDayOverrideKey);
        PlayerPrefs.Save();
        AccountDayChanged?.Invoke();
    }
}
