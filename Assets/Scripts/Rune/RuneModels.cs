using System;
using System.Collections.Generic;

public enum RuneRarity
{
    Common,
    Excellent,
    Epic,
    Legendary
}

public enum RuneRewardKind
{
    CompleteRune,
    Fragment
}

[Serializable]
public sealed class RuneReward
{
    public string RuneId;
    public string DisplayName;
    public RuneRarity Rarity;
    public RuneRewardKind RewardKind;
    public int Amount;
}

[Serializable]
public sealed class RuneInventoryEntry
{
    public string RuneId;
    public int OwnedCount;
    public int FragmentCount;
}

[Serializable]
public sealed class HeroRuneLoadoutEntry
{
    public string HeroId;
    public string RuneId;
}

[Serializable]
public sealed class RuneProfile
{
    public List<RuneInventoryEntry> Inventory = new List<RuneInventoryEntry>();
    public List<RuneReward> LastRunRewards = new List<RuneReward>();
    public List<HeroRuneLoadoutEntry> Loadouts = new List<HeroRuneLoadoutEntry>();
}

public sealed class RuneDefinition
{
    public readonly string RuneId;
    public readonly string DisplayName;
    public readonly RuneRarity Rarity;

    public RuneDefinition(string runeId, string displayName, RuneRarity rarity)
    {
        RuneId = runeId;
        DisplayName = displayName;
        Rarity = rarity;
    }

    public int RequiredFragments
    {
        get
        {
            if (Rarity == RuneRarity.Epic) return 3;
            if (Rarity == RuneRarity.Legendary) return 5;
            return 0;
        }
    }
}

public static class RuneCatalog
{
    private static readonly RuneDefinition[] Definitions =
    {
        new RuneDefinition("RUNE_MIGHT", "Rune of Might", RuneRarity.Common),
        new RuneDefinition("RUNE_FARREACH", "Farreach Rune", RuneRarity.Excellent),
        new RuneDefinition("RUNE_POWER", "Power Rune", RuneRarity.Excellent),
        new RuneDefinition("RUNE_LONGSHOT", "Longshot Rune", RuneRarity.Excellent),
        new RuneDefinition("RUNE_FROSTBITE", "Frostbite Rune", RuneRarity.Excellent),
        new RuneDefinition("RUNE_RICOCHET", "Ricochet Rune", RuneRarity.Epic),
        new RuneDefinition("RUNE_VOLLEY", "Volley Rune", RuneRarity.Epic),
        new RuneDefinition("RUNE_BLADE_TEMPEST", "Blade Tempest Rune", RuneRarity.Epic),
        new RuneDefinition("RUNE_AMBUSH", "Ambush Rune", RuneRarity.Epic),
        new RuneDefinition("RUNE_WINDHAWK", "Windhawk Rune", RuneRarity.Epic),
        new RuneDefinition("RUNE_SKYBREAKER", "Skybreaker Rune", RuneRarity.Legendary),
        new RuneDefinition("RUNE_WYRMGUARD", "Wyrmguard Rune", RuneRarity.Legendary),
        new RuneDefinition("RUNE_DRAGONBLOOM", "Dragonbloom Rune", RuneRarity.Legendary),
        new RuneDefinition("RUNE_WARCRY", "Warcry Rune", RuneRarity.Legendary)
    };

    public static IReadOnlyList<RuneDefinition> All => Definitions;

    public static RuneDefinition Find(string runeId)
    {
        for (int index = 0; index < Definitions.Length; index++)
        {
            if (Definitions[index].RuneId == runeId) return Definitions[index];
        }
        return null;
    }
}
