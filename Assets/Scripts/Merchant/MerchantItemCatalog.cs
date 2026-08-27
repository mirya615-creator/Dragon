using System.Collections.Generic;

public static class MerchantItemCatalog
{
    private static readonly Dictionary<string, string> ItemUiIconKeys =
        new Dictionary<string, string>
        {
            { "ITEM_WYRMFANG_SNARE", "ItemUI/1" },
            { "ITEM_WINTERVEIL_RUNE", "ItemUI/2" },
            { "ITEM_MANABURST_MINE", "ItemUI/3" },
            { "ITEM_FRENZY_RUNE", "ItemUI/4" },
            { "ITEM_RUNE_TEMPERING", "ItemUI/5" },
            { "ITEM_WARFORGE_SIGIL", "ItemUI/6" },
            { "ITEM_DRAKEHEART_RELIC", "ItemUI/7" },
            { "ITEM_PACT_ENDURANCE", "ItemUI/8" },
            { "ITEM_FARWATCH_CREST", "ItemUI/9" },
            { "ITEM_FROST_MIRE", "ItemUI/10" },
            { "ITEM_WAR_TEMPO", "ItemUI/11" },
            { "ITEM_VETERAN_MARK", "ItemUI/12" },
            { "ITEM_QUARTERMASTER_SATCHEL", "ItemUI/13" },
            { "ITEM_SPELLBREAKER_SEAL", "ItemUI/14" },
            { "ITEM_RIVALRY_OATH", "ItemUI/15" },
            { "ITEM_FORGE_TREASURY", "ItemUI/16" },
            { "ITEM_BATTLEFIELD_COMMAND", "ItemUI/17" },
            { "ITEM_FORGEGIFTERS_GIFT", "ItemUI/18" },
            { "ITEM_DRAGONFALL_JUDGMENT", "ItemUI/19" },
            { "ITEM_DRACONIC_PRESENCE", "ItemUI/20" }
        };

    private static readonly Dictionary<string, string> EnglishIntroductions =
        new Dictionary<string, string>
        {
            { "ITEM_WYRMFANG_SNARE", "Place a trap: 40% HP to normal enemies, Boss capped at 5%" },
            { "ITEM_WINTERVEIL_RUNE", "Slow all enemies by 10% for 5s; effective against Bosses" },
            { "ITEM_MANABURST_MINE", "One-time AoE: 80 damage to normal enemies, Boss capped at 3%" },
            { "ITEM_FRENZY_RUNE", "Target unit Attack Speed x1.4; stacks up to 2 times" },
            { "ITEM_RUNE_TEMPERING", "50% chance to gain 1 level; 50% chance to lose 1 level" },
            { "ITEM_WARFORGE_SIGIL", "Target unit instantly gains 1 level" },
            { "ITEM_DRAKEHEART_RELIC", "Maximum and current Heart +3" },
            { "ITEM_PACT_ENDURANCE", "You gain 5 Heart; your opponent gains 3 Heart" },
            { "ITEM_FARWATCH_CREST", "Ranged and flying units gain double attack range" },
            { "ITEM_FROST_MIRE", "Enemy Move Speed -10% for the entire match" },
            { "ITEM_WAR_TEMPO", "Both sides gain 10% Attack Speed" },
            { "ITEM_VETERAN_MARK", "Recruited units have a 5% chance to start at Lv.2" },
            { "ITEM_QUARTERMASTER_SATCHEL", "Reserve slots +1" },
            { "ITEM_SPELLBREAKER_SEAL", "Boss casts have a 50% chance to fail and deal 10% backlash" },
            { "ITEM_RIVALRY_OATH", "Your Attack Speed +50%; opponent Attack Speed +30%" },
            { "ITEM_FORGE_TREASURY", "Gain 3 resources after every 10 kills" },
            { "ITEM_BATTLEFIELD_COMMAND", "The first Hero recruitment is free" },
            { "ITEM_FORGEGIFTERS_GIFT", "Generate one Forge Pick every 90 seconds" },
            { "ITEM_DRAGONFALL_JUDGMENT", "First enemy near the finish is judged: normal 80%, Boss capped at 8%" },
            { "ITEM_DRACONIC_PRESENCE", "Each Hero slows enemies by 2%, up to 10%" }
        };

    private static readonly MerchantProduct[] Products =
    {
        Item("ITEM_WYRMFANG_SNARE", "Wyrmfang Snare", "Wyrmfang Snare", "Rare", "Active", 40, "Place a trap: 40% HP to normal enemies, Boss capped at 5%"),
        Item("ITEM_WINTERVEIL_RUNE", "Winterveil Scroll", "Winterveil Scroll", "Rare", "Active", 40, "Slow all enemies by 10% for 5s; effective against Bosses"),
        Item("ITEM_MANABURST_MINE", "Arcane Thunderburst", "Arcane Thunderburst", "Excellent", "Active", 60, "One-time AoE: 80 damage to normal enemies, Boss capped at 3%"),
        Item("ITEM_FRENZY_RUNE", "Berserker War Drum", "Berserker War Drum", "Epic", "Active", 80, "Target unit Attack Speed x1.4; stacks up to 2 times"),
        Item("ITEM_RUNE_TEMPERING", "Tempering Hammer", "Tempering Hammer", "Epic", "Active", 80, "50% chance to gain 1 level; 50% chance to lose 1 level"),
        Item("ITEM_WARFORGE_SIGIL", "Warforge Sigil", "Warforge Sigil", "Legendary", "Active", 120, "Target unit instantly gains 1 level"),
        Item("ITEM_DRAKEHEART_RELIC", "Drakeheart Relic", "Drakeheart Relic", "Rare", "Passive", 40, "Maximum and current Heart +3"),
        Item("ITEM_PACT_ENDURANCE", "Pact of Endurance", "Pact of Endurance", "Rare", "Passive", 40, "You gain 5 Heart; your opponent gains 3 Heart"),
        Item("ITEM_FARWATCH_CREST", "Farwatch Crest", "Farwatch Crest", "Rare", "Passive", 40, "Ranged and flying units gain double attack range"),
        Item("ITEM_FROST_MIRE", "Frost Mire", "Frost Mire", "Rare", "Passive", 40, "Enemy Move Speed -10% for the entire match"),
        Item("ITEM_WAR_TEMPO", "War Tempo", "War Tempo", "Excellent", "Passive", 60, "Both sides gain 10% Attack Speed"),
        Item("ITEM_VETERAN_MARK", "Veteran's Mark", "Veteran's Mark", "Excellent", "Passive", 60, "Recruited units have a 5% chance to start at Lv.2"),
        Item("ITEM_QUARTERMASTER_SATCHEL", "Quartermaster's Satchel", "Quartermaster's Satchel", "Excellent", "Passive", 60, "Reserve slots +1"),
        Item("ITEM_SPELLBREAKER_SEAL", "Spellbreaker Seal", "Spellbreaker Seal", "Epic", "Passive", 80, "Boss casts have a 50% chance to fail and deal 10% backlash"),
        Item("ITEM_RIVALRY_OATH", "Rivalry Oath", "Rivalry Oath", "Epic", "Passive", 80, "Your Attack Speed +50%; opponent Attack Speed +30%"),
        Item("ITEM_FORGE_TREASURY", "Forge Treasury", "Forge Treasury", "Epic", "Passive", 80, "Gain 3 resources after every 10 kills"),
        Item("ITEM_BATTLEFIELD_COMMAND", "Battlefield Command", "Battlefield Command", "Epic", "Passive", 80, "The first Hero recruitment is free"),
        Item("ITEM_FORGEGIFTERS_GIFT", "Forgekeeper's Gift", "Forgekeeper's Gift", "Legendary", "Passive", 120, "Generate one Forge Pick every 90 seconds", false),
        Item("ITEM_DRAGONFALL_JUDGMENT", "Dragonfall Judgment", "Dragonfall Judgment", "Legendary", "Passive", 120, "First enemy near the finish is judged: normal 80%, Boss capped at 8%"),
        Item("ITEM_DRACONIC_PRESENCE", "Draconic Presence", "Draconic Presence", "Legendary", "Passive", 120, "Each Hero slows enemies by 2%, up to 10%")
    };

    public static IReadOnlyList<MerchantProduct> All => Products;

    public static MerchantProduct Find(string productId)
    {
        foreach (MerchantProduct product in Products)
        {
            if (product.ProductId == productId) return Clone(product);
        }
        return null;
    }

    public static string GetIconKey(string productId)
    {
        return productId != null && ItemUiIconKeys.TryGetValue(productId, out string iconKey)
            ? iconKey
            : string.Empty;
    }

    public static string GetEnglishRarity(string rarity)
    {
        switch (rarity)
        {
            case "Rare": return "Rare";
            case "Excellent": return "Excellent";
            case "Epic": return "Epic";
            case "Legendary": return "Legendary";
            default: return rarity;
        }
    }

    public static string GetEnglishIntroduction(string productId)
    {
        return productId != null && EnglishIntroductions.TryGetValue(productId, out string introduction)
            ? introduction
            : string.Empty;
    }

    public static List<MerchantProduct> GetGoldCandidates()
    {
        var candidates = new List<MerchantProduct>();
        foreach (MerchantProduct product in Products)
        {
            if (product.GoldPurchasable) candidates.Add(Clone(product));
        }
        return candidates;
    }

    private static MerchantProduct Item(
        string id,
        string chineseName,
        string englishName,
        string rarity,
        string itemType,
        int price,
        string introduction,
        bool goldPurchasable = true)
    {
        return new MerchantProduct
        {
            ProductId = id,
            ChineseName = chineseName,
            EnglishName = englishName,
            Rarity = rarity,
            ItemType = itemType,
            GoldPrice = price,
            Introduction = introduction,
            IconKey = GetIconKey(id),
            GoldPurchasable = goldPurchasable
        };
    }

    private static MerchantProduct Clone(MerchantProduct product)
    {
        return new MerchantProduct
        {
            ProductId = product.ProductId,
            ChineseName = product.ChineseName,
            EnglishName = product.EnglishName,
            Rarity = product.Rarity,
            ItemType = product.ItemType,
            GoldPrice = product.GoldPrice,
            Introduction = product.Introduction,
            IconKey = product.IconKey,
            GoldPurchasable = product.GoldPurchasable,
            PaymentType = product.PaymentType,
            AdPlacementId = product.AdPlacementId
        };
    }
}
