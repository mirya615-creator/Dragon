using System;
using System.Collections.Generic;
using DragonBound.Recruitment;
using UnityEngine;

/// <summary>
/// Stable identity for a Main/WeaponPanel hero slot. UI labels are presentation only and must
/// never be sent to persistence or used as a gameplay key.
/// </summary>
[DisallowMultipleComponent]
public sealed class HeroRuneSlotIdentity : MonoBehaviour
{
    [SerializeField] private string heroId;

    public string HeroId => heroId;

    public void InitializeIfEmpty(string value)
    {
        if (string.IsNullOrWhiteSpace(heroId)) heroId = value ?? string.Empty;
    }
}

public static class HeroRuneIdentityCatalog
{
    private static readonly string[] WeaponPanelOrder =
    {
        DragonBoundHeroIds.WindclawRanger,
        DragonBoundHeroIds.EmberShaman,
        DragonBoundHeroIds.RuneboltMage,
        DragonBoundHeroIds.Stonebinder,
        DragonBoundHeroIds.CrownSwordLeader,
        DragonBoundHeroIds.CrownHunterLeader,
        DragonBoundHeroIds.DragonRider,
        DragonBoundHeroIds.StarfallArchmage,
        DragonBoundHeroIds.ThunderJarl,
        DragonBoundHeroIds.NightfangAssassin,
        DragonBoundHeroIds.LeviathanHunter,
        DragonBoundHeroIds.SkyhunterValkyrie
    };

    private static readonly IReadOnlyDictionary<string, string> LegacyNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Windclaw Ranger"] = DragonBoundHeroIds.WindclawRanger,
            ["Ember Shaman"] = DragonBoundHeroIds.EmberShaman,
            ["Runebolt Mage"] = DragonBoundHeroIds.RuneboltMage,
            ["Stonebinder"] = DragonBoundHeroIds.Stonebinder,
            ["Stonebound Warlock"] = DragonBoundHeroIds.Stonebinder,
            ["Oathcrown Swordsman"] = DragonBoundHeroIds.CrownSwordLeader,
            ["Oathcrown Blademaster"] = DragonBoundHeroIds.CrownSwordLeader,
            ["Frostcrown Hunter"] = DragonBoundHeroIds.CrownHunterLeader,
            ["Dragon Rider"] = DragonBoundHeroIds.DragonRider,
            ["Flame Drake Rider"] = DragonBoundHeroIds.DragonRider,
            ["Starfall Archmage"] = DragonBoundHeroIds.StarfallArchmage,
            ["Thunder Jarl"] = DragonBoundHeroIds.ThunderJarl,
            ["Thunderlord"] = DragonBoundHeroIds.ThunderJarl,
            ["Nightfang Assassin"] = DragonBoundHeroIds.NightfangAssassin,
            ["Leviathan Hunter"] = DragonBoundHeroIds.LeviathanHunter,
            ["Abyssal Harpooner"] = DragonBoundHeroIds.LeviathanHunter,
            ["Skyhunter Valkyrie"] = DragonBoundHeroIds.SkyhunterValkyrie,
            ["Skyborne Valkyrie"] = DragonBoundHeroIds.SkyhunterValkyrie
        };

    public static string ResolveSlot(int siblingIndex, string authoredHeroId, string legacyName)
    {
        if (IsCanonical(authoredHeroId)) return authoredHeroId;
        if (!string.IsNullOrWhiteSpace(legacyName) &&
            LegacyNames.TryGetValue(legacyName.Trim(), out string resolved))
        {
            return resolved;
        }

        return siblingIndex >= 0 && siblingIndex < WeaponPanelOrder.Length
            ? WeaponPanelOrder[siblingIndex]
            : string.Empty;
    }

    public static string ResolvePersisted(string value)
    {
        if (IsCanonical(value)) return value;
        return !string.IsNullOrWhiteSpace(value) && LegacyNames.TryGetValue(value.Trim(), out string resolved)
            ? resolved
            : string.Empty;
    }

    private static bool IsCanonical(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        for (int index = 0; index < WeaponPanelOrder.Length; index++)
        {
            if (string.Equals(WeaponPanelOrder[index], value, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
