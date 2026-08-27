using System;
using UnityEngine;

/// <summary>
/// Local development substitute for the future server-owned AccountDay value.
/// It never derives progression from device time.
/// </summary>
public static class LocalRuneProgressionSettings
{
    private const string AccountDayOverrideKey = "dragonbound.runes.dev-account-day-v1";
    private const int DevelopmentDefaultAccountDay = 3;

    public static int ResolveAccountDay(int persistedAccountDay)
    {
        int fallback = Math.Max(1, persistedAccountDay);
        if (PlayerPrefs.HasKey(AccountDayOverrideKey))
        {
            return Math.Max(1, PlayerPrefs.GetInt(AccountDayOverrideKey, fallback));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // During development the rune feature is tested as an account that has
        // already reached day three. An explicit PlayerPrefs override still wins,
        // so locked-day behaviour can be tested from the development menu.
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
    }

    public static void ClearDevelopmentOverride()
    {
        PlayerPrefs.DeleteKey(AccountDayOverrideKey);
        PlayerPrefs.Save();
    }
}
