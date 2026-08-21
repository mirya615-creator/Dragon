using System;
using System.Collections.Generic;

namespace DragonBound.Runes
{
    public enum RuneRarity { Common, Excellent, Epic, Legendary }
    public enum RuneEffectType
    {
        AttackDamagePercent, AttackRangeFlat, LongshotDistanceDamage, FrostbiteSlow,
        Ricochet, Volley, BladeTempest, Ambush, Windhawk, Skybreaker, Wyrmguard,
        Dragonbloom, Warcry
    }

    [Serializable]
    public sealed class RuneDefinition
    {
        public RuneDefinition(string runeId, string displayNameKey, RuneRarity rarity,
            RuneEffectType effectType, float parameter = 0f, float secondaryParameter = 0f,
            bool dropPoolEnabled = true, string iconKey = "", string artAssetKey = "", string rarityThemeKey = "",
            IReadOnlyDictionary<string, float> parameters = null)
        {
            RuneId = runeId; DisplayNameKey = displayNameKey; Rarity = rarity;
            EffectType = effectType; Parameter = parameter; SecondaryParameter = secondaryParameter;
            DropPoolEnabled = dropPoolEnabled;
            IconKey = string.IsNullOrEmpty(iconKey) ? "RuneIcon." + runeId : iconKey;
            ArtAssetKey = string.IsNullOrEmpty(artAssetKey) ? "RuneArt." + runeId : artAssetKey;
            RarityThemeKey = string.IsNullOrEmpty(rarityThemeKey) ? "RuneTheme." + rarity : rarityThemeKey;
            Parameters = parameters == null
                ? new Dictionary<string, float>(StringComparer.Ordinal)
                : new Dictionary<string, float>(parameters, StringComparer.Ordinal);
        }
        public string RuneId { get; }
        public string DisplayNameKey { get; }
        public RuneRarity Rarity { get; }
        public RuneEffectType EffectType { get; }
        public float Parameter { get; }
        public float SecondaryParameter { get; }
        public bool DropPoolEnabled { get; }
        public string IconKey { get; }
        public string ArtAssetKey { get; }
        public string RarityThemeKey { get; }
        public IReadOnlyDictionary<string, float> Parameters { get; }

        public float GetParameter(string key, float fallback = 0f)
        {
            return Parameters.TryGetValue(key, out var value) ? value : fallback;
        }
    }

    public static class RuneCatalog
    {
        private static readonly IReadOnlyList<RuneDefinition> definitions = new List<RuneDefinition>
        {
            Define("Might", "Rune.OfMight", RuneRarity.Common, RuneEffectType.AttackDamagePercent,
                .08f, new Dictionary<string, float> { ["DamagePercent"] = .08f }),
            Define("Farreach", "Rune.Farreach", RuneRarity.Excellent, RuneEffectType.AttackRangeFlat,
                .75f, new Dictionary<string, float> { ["RangeCells"] = .75f }),
            Define("Power", "Rune.Power", RuneRarity.Excellent, RuneEffectType.AttackDamagePercent,
                .15f, new Dictionary<string, float> { ["DamagePercent"] = .15f }),
            Define("Longshot", "Rune.Longshot", RuneRarity.Excellent, RuneEffectType.LongshotDistanceDamage,
                .20f, new Dictionary<string, float> { ["MaxDamagePercent"] = .20f }),
            Define("Frostbite", "Rune.Frostbite", RuneRarity.Excellent, RuneEffectType.FrostbiteSlow,
                .10f, new Dictionary<string, float>
                {
                    ["NormalSlow"] = .10f, ["NormalDuration"] = 1.5f,
                    ["BossSlow"] = .05f, ["BossDuration"] = 1f
                }),
            Define("Ricochet", "Rune.Ricochet", RuneRarity.Epic, RuneEffectType.Ricochet,
                .30f, new Dictionary<string, float> { ["Chance"] = .30f, ["DamageMultiplier"] = .55f, ["TargetCount"] = 1f }),
            Define("Volley", "Rune.Volley", RuneRarity.Epic, RuneEffectType.Volley,
                .35f, new Dictionary<string, float> { ["AttackThreshold"] = 10f, ["BoltCount"] = 5f, ["DamageMultiplier"] = .35f }),
            Define("BladeTempest", "Rune.BladeTempest", RuneRarity.Epic, RuneEffectType.BladeTempest,
                .40f, new Dictionary<string, float> { ["Chance"] = .40f, ["TargetCount"] = 3f, ["DamageMultiplier"] = .60f }),
            Define("Ambush", "Rune.Ambush", RuneRarity.Epic, RuneEffectType.Ambush,
                .30f, new Dictionary<string, float> { ["Chance"] = .30f, ["Radius"] = .75f, ["DamageMultiplier"] = .80f }),
            Define("Windhawk", "Rune.Windhawk", RuneRarity.Epic, RuneEffectType.Windhawk,
                .15f, new Dictionary<string, float>
                {
                    ["Chance"] = .15f, ["IcdSeconds"] = 2f,
                    ["InterceptDamageMultiplier"] = .90f, ["FallbackDamageMultiplier"] = .60f
                }),
            Define("Skybreaker", "Rune.Skybreaker", RuneRarity.Legendary, RuneEffectType.Skybreaker,
                .10f, new Dictionary<string, float>
                {
                    ["Chance"] = .10f, ["Radius"] = .90f,
                    ["PrimaryDamageMultiplier"] = 1.80f, ["SecondaryDamageMultiplier"] = .80f
                }),
            Define("Wyrmguard", "Rune.Wyrmguard", RuneRarity.Legendary, RuneEffectType.Wyrmguard,
                12f, new Dictionary<string, float>
                {
                    ["DurationSeconds"] = 12f, ["AttackRate"] = 1.5f, ["DamageMultiplier"] = .35f
                }),
            Define("Dragonbloom", "Rune.Dragonbloom", RuneRarity.Legendary, RuneEffectType.Dragonbloom,
                .30f, new Dictionary<string, float>
                {
                    ["Chance"] = .30f, ["DurationSeconds"] = 4f, ["AttackRate"] = 1f, ["DamageMultiplier"] = .40f
                }),
            Define("Warcry", "Rune.Warcry", RuneRarity.Legendary, RuneEffectType.Warcry,
                .12f, new Dictionary<string, float>
                {
                    ["Chance"] = .12f, ["IcdSeconds"] = 10f, ["Radius"] = 2.5f,
                    ["AttackSpeedMultiplier"] = 1.20f, ["DurationSeconds"] = 6f
                })
        };

        private static RuneDefinition Define(
            string runeId,
            string displayNameKey,
            RuneRarity rarity,
            RuneEffectType effectType,
            float parameter,
            IReadOnlyDictionary<string, float> parameters)
        {
            return new RuneDefinition(
                runeId,
                displayNameKey,
                rarity,
                effectType,
                parameter,
                0f,
                true,
                parameters: parameters);
        }
        public static IReadOnlyList<RuneDefinition> All => definitions;
        public static RuneDefinition Get(string runeId)
        {
            for (var i = 0; i < definitions.Count; i++)
                if (string.Equals(definitions[i].RuneId, runeId, StringComparison.Ordinal)) return definitions[i];
            return null;
        }
        public static IReadOnlyList<RuneDefinition> Pool(RuneRarity rarity)
        {
            var result = new List<RuneDefinition>();
            for (var i = 0; i < definitions.Count; i++)
                if (definitions[i].Rarity == rarity && definitions[i].DropPoolEnabled) result.Add(definitions[i]);
            return result;
        }
    }
}
