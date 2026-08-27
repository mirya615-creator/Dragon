using System;
using System.Collections.Generic;
using DragonBound.Bootstrap;

/// <summary>
/// Converts the account/profile contract used by Main into the immutable combat contract used by
/// Greybox_Main. This is also the seam used by the future Go unary Rune gateway.
/// </summary>
public static class RuneGameplayLoadoutAdapter
{
    private static readonly IReadOnlyDictionary<string, string> RuntimeRuneIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RUNE_MIGHT"] = "Might",
            ["RUNE_FARREACH"] = "Farreach",
            ["RUNE_POWER"] = "Power",
            ["RUNE_LONGSHOT"] = "Longshot",
            ["RUNE_FROSTBITE"] = "Frostbite",
            ["RUNE_RICOCHET"] = "Ricochet",
            ["RUNE_VOLLEY"] = "Volley",
            ["RUNE_BLADE_TEMPEST"] = "BladeTempest",
            ["RUNE_AMBUSH"] = "Ambush",
            ["RUNE_WINDHAWK"] = "Windhawk",
            ["RUNE_SKYBREAKER"] = "Skybreaker",
            ["RUNE_WYRMGUARD"] = "Wyrmguard",
            ["RUNE_DRAGONBLOOM"] = "Dragonbloom",
            ["RUNE_WARCRY"] = "Warcry"
        };

    private static readonly ISet<string> KnownRuntimeRuneIds =
        new HashSet<string>(RuntimeRuneIds.Values, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> ProfileRuneIds =
        CreateProfileRuneIds();

    public static bool TryCreateAssignments(
        global::RuneProfile profile,
        out IReadOnlyList<ExternalRuneLoadoutAssignment> assignments,
        out string error)
    {
        var values = new List<ExternalRuneLoadoutAssignment>();
        var assignedHeroes = new HashSet<string>(StringComparer.Ordinal);
        if (profile?.Loadouts != null)
        {
            for (int index = 0; index < profile.Loadouts.Count; index++)
            {
                global::HeroRuneLoadoutEntry source = profile.Loadouts[index];
                string heroId = source == null
                    ? string.Empty
                    : HeroRuneIdentityCatalog.ResolvePersisted(source.HeroId);
                string runeId = source == null ? string.Empty : ResolveRuntimeRuneId(source.RuneId);
                if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(runeId))
                {
                    assignments = Array.Empty<ExternalRuneLoadoutAssignment>();
                    error = "InvalidAssignment:" + index;
                    return false;
                }

                if (!assignedHeroes.Add(heroId))
                {
                    assignments = Array.Empty<ExternalRuneLoadoutAssignment>();
                    error = "DuplicateHeroId:" + heroId;
                    return false;
                }

                values.Add(new ExternalRuneLoadoutAssignment { HeroId = heroId, RuneId = runeId });
            }
        }

        assignments = values.AsReadOnly();
        error = string.Empty;
        return true;
    }

    public static string ResolveRuntimeRuneId(string profileRuneId)
    {
        if (string.IsNullOrWhiteSpace(profileRuneId)) return string.Empty;
        if (RuntimeRuneIds.TryGetValue(profileRuneId.Trim(), out string runtimeId)) return runtimeId;
        string normalized = profileRuneId.Trim();
        return KnownRuntimeRuneIds.Contains(normalized) ? normalized : string.Empty;
    }

    public static string ResolveProfileRuneId(string runtimeRuneId)
    {
        if (string.IsNullOrWhiteSpace(runtimeRuneId)) return string.Empty;
        string normalized = runtimeRuneId.Trim();
        if (ProfileRuneIds.TryGetValue(normalized, out string profileRuneId)) return profileRuneId;
        return RuntimeRuneIds.ContainsKey(normalized) ? normalized : string.Empty;
    }

    private static IReadOnlyDictionary<string, string> CreateProfileRuneIds()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in RuntimeRuneIds)
        {
            values[pair.Value] = pair.Key;
        }
        return values;
    }
}
