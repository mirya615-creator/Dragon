#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RuneAccountDayDevelopmentMenu
{
    [MenuItem("Tools/DragonBound/Runes/Set Test Account Day/Day 1")]
    private static void SetDayOne() => SetDay(1);

    [MenuItem("Tools/DragonBound/Runes/Set Test Account Day/Day 2")]
    private static void SetDayTwo() => SetDay(2);

    [MenuItem("Tools/DragonBound/Runes/Set Test Account Day/Day 3")]
    private static void SetDayThree() => SetDay(3);

    [MenuItem("Tools/DragonBound/Runes/Set Test Account Day/Clear Override")]
    private static void ClearOverride()
    {
        LocalRuneProgressionSettings.ClearDevelopmentOverride();
        Debug.Log("Rune test AccountDay override cleared.");
    }

    private static void SetDay(int accountDay)
    {
        LocalRuneProgressionSettings.SetDevelopmentAccountDay(accountDay);
        Debug.Log($"Rune test AccountDay set to Day {accountDay}.");
    }
}
#endif
