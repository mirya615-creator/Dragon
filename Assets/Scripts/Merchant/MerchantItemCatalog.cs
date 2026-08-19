using System.Collections.Generic;

public static class MerchantItemCatalog
{
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
        Item("ITEM_WYRMFANG_SNARE", "龙牙陷阱", "Wyrmfang Snare", "稀有", "Active", 40, "路放陷阱，秒普通怪40%血，Boss限5%"),
        Item("ITEM_WINTERVEIL_RUNE", "冬幕符文", "Winterveil Rune", "稀有", "Active", 40, "全体敌人减速10%，5秒，Boss有效"),
        Item("ITEM_MANABURST_MINE", "符爆地雷", "Manaburst Mine", "卓越", "Active", 60, "一次性AoE，小怪80伤，Boss限3%"),
        Item("ITEM_FRENZY_RUNE", "狂战符文", "Frenzy Rune", "史诗", "Active", 80, "指定单位攻速×1.4，可叠2次"),
        Item("ITEM_RUNE_TEMPERING", "淬炼符文", "Rune of Tempering", "史诗", "Active", 80, "50%升1级，50%降1级"),
        Item("ITEM_WARFORGE_SIGIL", "战炉符印", "Warforge Sigil", "传说", "Active", 120, "指定单位直接升1级"),
        Item("ITEM_DRAKEHEART_RELIC", "龙心圣物", "Drakeheart Relic", "稀有", "Passive", 40, "生命上限与当前各+3"),
        Item("ITEM_PACT_ENDURANCE", "坚生契约", "Pact of Endurance", "稀有", "Passive", 40, "你+5血，对手+3血"),
        Item("ITEM_FARWATCH_CREST", "远望徽记", "Farwatch Crest", "稀有", "Passive", 40, "弓手与空中单位射程翻倍"),
        Item("ITEM_FROST_MIRE", "寒霜泥沼", "Frost Mire", "稀有", "Passive", 40, "敌方全程移速-10%"),
        Item("ITEM_WAR_TEMPO", "战争律动", "War Tempo", "卓越", "Passive", 60, "双方攻速+10%"),
        Item("ITEM_VETERAN_MARK", "老兵印记", "Veteran's Mark", "卓越", "Passive", 60, "招募的兵5%直接2级"),
        Item("ITEM_QUARTERMASTER_SATCHEL", "军需行囊", "Quartermaster's Satchel", "卓越", "Passive", 60, "备战区格子+1"),
        Item("ITEM_SPELLBREAKER_SEAL", "破法封印", "Spellbreaker Seal", "史诗", "Passive", 80, "Boss施法50%失败，反噬10%"),
        Item("ITEM_RIVALRY_OATH", "竞战誓约", "Rivalry Oath", "史诗", "Passive", 80, "你攻速+50%，对手+30%"),
        Item("ITEM_FORGE_TREASURY", "炉心宝库", "Forge Treasury", "史诗", "Passive", 80, "每10次击杀+3资源"),
        Item("ITEM_BATTLEFIELD_COMMAND", "战场指挥", "Battlefield Command", "史诗", "Passive", 80, "首个英雄免费招募一次"),
        Item("ITEM_FORGEGIFTERS_GIFT", "炉匠馈赠", "Forgekeeper's Gift", "传说", "Passive", 120, "每90秒生成一个锻造镐", false),
        Item("ITEM_DRAGONFALL_JUDGMENT", "龙陨裁决", "Dragonfall Judgment", "传说", "Passive", 120, "首敌近终点前3格天罚：小怪80%，Boss限8%"),
        Item("ITEM_DRACONIC_PRESENCE", "龙威震慑", "Draconic Presence", "传说", "Passive", 120, "每英雄令敌人减速2%，上限10%")
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

    public static string GetEnglishRarity(string rarity)
    {
        switch (rarity)
        {
            case "稀有": return "Rare";
            case "卓越": return "Excellent";
            case "史诗": return "Epic";
            case "传说": return "Legendary";
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
            IconKey = id,
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
