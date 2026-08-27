using System;
using System.Collections.Generic;

namespace DragonBound.Items
{
    public enum ItemCategory
    {
        Active,
        Passive
    }

    public enum ItemRarity
    {
        Rare,
        Excellent,
        Epic,
        Legendary
    }

    public enum ItemImplementationStatus
    {
        Implemented,
        Pending
    }

    public enum ItemEffectKind
    {
        None,
        DrakeheartRelic,
        WinterveilRune,
        WyrmfangSnare,
        RuneburstMine,
        FrenzyRune,
        RuneOfTempering,
        WarforgeSigil,
        DragonfallJudgment,
        PactOfEndurance,
        FarwatchCrest,
        FrostMire,
        WarTempo,
        VeteransMark,
        QuartermastersSatchel,
        SpellbreakerSeal,
        RivalryOath,
        DraconicPresence,
        ForgeTreasury,
        BattlefieldCommand,
        ForgekeepersGift
    }

    public static class ItemIds
    {
        public const string WyrmfangSnare = "ITEM_WYRMFANG_SNARE";
        public const string WinterveilRune = "ITEM_WINTERVEIL_RUNE";
        public const string RuneburstMine = "ITEM_RUNEBURST_MINE";
        public const string FrenzyRune = "ITEM_FRENZY_RUNE";
        public const string RuneOfTempering = "ITEM_RUNE_OF_TEMPERING";
        public const string WarforgeSigil = "ITEM_WARFORGE_SIGIL";
        public const string DrakeheartRelic = "ITEM_DRAKEHEART_RELIC";
        public const string PactOfEndurance = "ITEM_PACT_OF_ENDURANCE";
        public const string FarwatchCrest = "ITEM_FARWATCH_CREST";
        public const string FrostMire = "ITEM_FROST_MIRE";
        public const string WarTempo = "ITEM_WAR_TEMPO";
        public const string VeteransMark = "ITEM_VETERANS_MARK";
        public const string QuartermastersSatchel = "ITEM_QUARTERMASTERS_SATCHEL";
        public const string SpellbreakerSeal = "ITEM_SPELLBREAKER_SEAL";
        public const string RivalryOath = "ITEM_RIVALRY_OATH";
        public const string ForgeTreasury = "ITEM_FORGE_TREASURY";
        public const string BattlefieldCommand = "ITEM_BATTLEFIELD_COMMAND";
        public const string ForgekeepersGift = "ITEM_FORGEKEEPERS_GIFT";
        public const string DragonfallJudgment = "ITEM_DRAGONFALL_JUDGMENT";
        public const string DraconicPresence = "ITEM_DRACONIC_PRESENCE";
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public ItemDefinition(
            string itemId,
            string displayNameKey,
            ItemCategory category,
            ItemRarity rarity,
            ItemImplementationStatus status,
            ItemEffectKind effectKind = ItemEffectKind.None,
            string iconKey = "",
            string artAssetKey = "")
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("ItemId is required.", nameof(itemId));
            }

            ItemId = itemId;
            DisplayNameKey = string.IsNullOrWhiteSpace(displayNameKey) ? itemId : displayNameKey;
            Category = category;
            Rarity = rarity;
            Status = status;
            EffectKind = effectKind;
            IconKey = string.IsNullOrWhiteSpace(iconKey) ? "ItemIcon." + itemId : iconKey;
            ArtAssetKey = string.IsNullOrWhiteSpace(artAssetKey) ? "ItemArt." + itemId : artAssetKey;
        }

        public string ItemId { get; }
        public string DisplayNameKey { get; }
        public ItemCategory Category { get; }
        public ItemRarity Rarity { get; }
        public ItemImplementationStatus Status { get; }
        public ItemEffectKind EffectKind { get; }
        public string IconKey { get; }
        public string ArtAssetKey { get; }
        public bool IsFormalCandidate => Status == ItemImplementationStatus.Implemented;
    }

    public static class ItemCatalog
    {
        private static readonly IReadOnlyDictionary<string, string> englishDisplayNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { ItemIds.WyrmfangSnare, "Wyrmfang Snare" },
                { ItemIds.WinterveilRune, "Winterveil Scroll" },
                { ItemIds.RuneburstMine, "Arcane Thunderburst" },
                { ItemIds.FrenzyRune, "Berserker War Drum" },
                { ItemIds.RuneOfTempering, "Tempering Hammer" },
                { ItemIds.WarforgeSigil, "Warforge Sigil" },
                { ItemIds.DrakeheartRelic, "Drakeheart Relic" },
                { ItemIds.PactOfEndurance, "Pact of Endurance" },
                { ItemIds.FarwatchCrest, "Farwatch Crest" },
                { ItemIds.FrostMire, "Frost Mire" },
                { ItemIds.WarTempo, "War Tempo" },
                { ItemIds.VeteransMark, "Veteran's Mark" },
                { ItemIds.QuartermastersSatchel, "Quartermaster's Satchel" },
                { ItemIds.SpellbreakerSeal, "Spellbreaker Seal" },
                { ItemIds.RivalryOath, "Rivalry Oath" },
                { ItemIds.ForgeTreasury, "Forge Treasury" },
                { ItemIds.BattlefieldCommand, "Battlefield Command" },
                { ItemIds.ForgekeepersGift, "Forgekeeper's Gift" },
                { ItemIds.DragonfallJudgment, "Dragonfall Judgment" },
                { ItemIds.DraconicPresence, "Draconic Presence" }
            };

        private static readonly IReadOnlyList<ItemDefinition> definitions = new List<ItemDefinition>
        {
            Define(ItemIds.WyrmfangSnare, ItemCategory.Active, ItemRarity.Rare,
                ItemImplementationStatus.Implemented, ItemEffectKind.WyrmfangSnare),
            Define(ItemIds.WinterveilRune, ItemCategory.Active, ItemRarity.Rare,
                ItemImplementationStatus.Implemented, ItemEffectKind.WinterveilRune),
            Define(ItemIds.RuneburstMine, ItemCategory.Active, ItemRarity.Excellent,
                ItemImplementationStatus.Implemented, ItemEffectKind.RuneburstMine),
            Define(ItemIds.FrenzyRune, ItemCategory.Active, ItemRarity.Epic,
                ItemImplementationStatus.Implemented, ItemEffectKind.FrenzyRune),
            Define(ItemIds.RuneOfTempering, ItemCategory.Active, ItemRarity.Epic,
                ItemImplementationStatus.Implemented, ItemEffectKind.RuneOfTempering),
            Define(ItemIds.WarforgeSigil, ItemCategory.Active, ItemRarity.Legendary,
                ItemImplementationStatus.Implemented, ItemEffectKind.WarforgeSigil),
            Define(ItemIds.DrakeheartRelic, ItemCategory.Passive, ItemRarity.Rare,
                ItemImplementationStatus.Implemented, ItemEffectKind.DrakeheartRelic),
            Define(ItemIds.PactOfEndurance, ItemCategory.Passive, ItemRarity.Rare,
                ItemImplementationStatus.Implemented, ItemEffectKind.PactOfEndurance),
            Define(ItemIds.FarwatchCrest, ItemCategory.Passive, ItemRarity.Rare,
                ItemImplementationStatus.Implemented, ItemEffectKind.FarwatchCrest),
            Define(ItemIds.FrostMire, ItemCategory.Passive, ItemRarity.Rare,
                ItemImplementationStatus.Implemented, ItemEffectKind.FrostMire),
            Define(ItemIds.WarTempo, ItemCategory.Passive, ItemRarity.Excellent,
                ItemImplementationStatus.Implemented, ItemEffectKind.WarTempo),
            Define(ItemIds.VeteransMark, ItemCategory.Passive, ItemRarity.Excellent,
                ItemImplementationStatus.Implemented, ItemEffectKind.VeteransMark),
            Define(ItemIds.QuartermastersSatchel, ItemCategory.Passive, ItemRarity.Excellent,
                ItemImplementationStatus.Implemented, ItemEffectKind.QuartermastersSatchel),
            Define(ItemIds.SpellbreakerSeal, ItemCategory.Passive, ItemRarity.Epic,
                ItemImplementationStatus.Implemented, ItemEffectKind.SpellbreakerSeal),
            Define(ItemIds.RivalryOath, ItemCategory.Passive, ItemRarity.Epic,
                ItemImplementationStatus.Implemented, ItemEffectKind.RivalryOath),
            Define(ItemIds.ForgeTreasury, ItemCategory.Passive, ItemRarity.Epic,
                ItemImplementationStatus.Implemented, ItemEffectKind.ForgeTreasury),
            Define(ItemIds.BattlefieldCommand, ItemCategory.Passive, ItemRarity.Epic,
                ItemImplementationStatus.Implemented, ItemEffectKind.BattlefieldCommand),
            Define(ItemIds.ForgekeepersGift, ItemCategory.Passive, ItemRarity.Legendary,
                ItemImplementationStatus.Implemented, ItemEffectKind.ForgekeepersGift),
            Define(ItemIds.DragonfallJudgment, ItemCategory.Passive, ItemRarity.Legendary,
                ItemImplementationStatus.Implemented, ItemEffectKind.DragonfallJudgment),
            Define(ItemIds.DraconicPresence, ItemCategory.Passive, ItemRarity.Legendary,
                ItemImplementationStatus.Implemented, ItemEffectKind.DraconicPresence)
        };

        public static IReadOnlyList<ItemDefinition> All => definitions;

        public static ItemDefinition Get(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return definitions[i];
                }
            }

            return null;
        }

        public static string GetEnglishDisplayName(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return string.Empty;
            return englishDisplayNames.TryGetValue(itemId, out string displayName)
                ? displayName
                : itemId;
        }

        public static IReadOnlyList<ItemDefinition> FormalCandidates
        {
            get
            {
                var result = new List<ItemDefinition>();
                for (var i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i].IsFormalCandidate)
                    {
                        result.Add(definitions[i]);
                    }
                }

                return result;
            }
        }

        private static ItemDefinition Define(
            string itemId,
            ItemCategory category,
            ItemRarity rarity,
            ItemImplementationStatus status = ItemImplementationStatus.Pending,
            ItemEffectKind effectKind = ItemEffectKind.None)
        {
            return new ItemDefinition(itemId, "Item." + itemId, category, rarity, status, effectKind);
        }
    }
}
