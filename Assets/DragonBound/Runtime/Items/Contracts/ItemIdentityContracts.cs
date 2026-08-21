using System;
using System.Collections.Generic;

namespace DragonBound.Items.Contracts
{
    public enum ItemCategory
    {
        Active,
        Passive
    }

    public enum ItemConfigurationState
    {
        Configured,
        Pending,
        NotConfigured
    }

    public readonly struct ItemId : IEquatable<ItemId>, IComparable<ItemId>
    {
        public ItemId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An ItemId is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public bool Equals(ItemId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ItemId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public int CompareTo(ItemId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ItemId left, ItemId right) => left.Equals(right);
        public static bool operator !=(ItemId left, ItemId right) => !left.Equals(right);
    }

    public static class ItemIds
    {
        public static readonly ItemId WyrmfangSnare = new ItemId("ITEM_WYRMFANG_SNARE");
        public static readonly ItemId WinterveilRune = new ItemId("ITEM_WINTERVEIL_RUNE");
        public static readonly ItemId RuneburstMine = new ItemId("ITEM_RUNEBURST_MINE");
        public static readonly ItemId FrenzyRune = new ItemId("ITEM_FRENZY_RUNE");
        public static readonly ItemId RuneOfTempering = new ItemId("ITEM_RUNE_OF_TEMPERING");
        public static readonly ItemId WarforgeSigil = new ItemId("ITEM_WARFORGE_SIGIL");
        public static readonly ItemId DrakeheartRelic = new ItemId("ITEM_DRAKEHEART_RELIC");
        public static readonly ItemId PactOfEndurance = new ItemId("ITEM_PACT_OF_ENDURANCE");
        public static readonly ItemId FarwatchCrest = new ItemId("ITEM_FARWATCH_CREST");
        public static readonly ItemId FrostMire = new ItemId("ITEM_FROST_MIRE");
        public static readonly ItemId WarTempo = new ItemId("ITEM_WAR_TEMPO");
        public static readonly ItemId VeteransMark = new ItemId("ITEM_VETERANS_MARK");
        public static readonly ItemId QuartermastersSatchel = new ItemId("ITEM_QUARTERMASTERS_SATCHEL");
        public static readonly ItemId SpellbreakerSeal = new ItemId("ITEM_SPELLBREAKER_SEAL");
        public static readonly ItemId RivalryOath = new ItemId("ITEM_RIVALRY_OATH");
        public static readonly ItemId ForgeTreasury = new ItemId("ITEM_FORGE_TREASURY");
        public static readonly ItemId BattlefieldCommand = new ItemId("ITEM_BATTLEFIELD_COMMAND");
        public static readonly ItemId ForgekeepersGift = new ItemId("ITEM_FORGEKEEPERS_GIFT");
        public static readonly ItemId DragonfallJudgment = new ItemId("ITEM_DRAGONFALL_JUDGMENT");
        public static readonly ItemId DraconicPresence = new ItemId("ITEM_DRACONIC_PRESENCE");

        public static IReadOnlyList<ItemId> All { get; } = new[]
        {
            WyrmfangSnare, WinterveilRune, RuneburstMine, FrenzyRune, RuneOfTempering,
            WarforgeSigil, DrakeheartRelic, PactOfEndurance, FarwatchCrest, FrostMire,
            WarTempo, VeteransMark, QuartermastersSatchel, SpellbreakerSeal, RivalryOath,
            ForgeTreasury, BattlefieldCommand, ForgekeepersGift, DragonfallJudgment,
            DraconicPresence
        };
    }

    public readonly struct ItemCatalogEntry
    {
        public ItemCatalogEntry(ItemId id, ItemCategory category, ItemConfigurationState state)
        {
            Id = id;
            Category = category;
            State = state;
        }

        public ItemId Id { get; }
        public ItemCategory Category { get; }
        public ItemConfigurationState State { get; }
        public bool IsUsable => State == ItemConfigurationState.Configured;
    }

    public static class ItemCatalog
    {
        private static readonly IReadOnlyList<ItemCatalogEntry> entries = new[]
        {
            Active(ItemIds.WyrmfangSnare, ItemConfigurationState.Configured),
            Active(ItemIds.WinterveilRune, ItemConfigurationState.Configured),
            Active(ItemIds.RuneburstMine, ItemConfigurationState.Configured),
            Active(ItemIds.FrenzyRune, ItemConfigurationState.Configured),
            Active(ItemIds.RuneOfTempering, ItemConfigurationState.Configured),
            Active(ItemIds.WarforgeSigil, ItemConfigurationState.Configured),
            Passive(ItemIds.DrakeheartRelic, ItemConfigurationState.Configured),
            Passive(ItemIds.PactOfEndurance, ItemConfigurationState.Configured),
            Passive(ItemIds.FarwatchCrest, ItemConfigurationState.Configured),
            Passive(ItemIds.FrostMire, ItemConfigurationState.Configured),
            Passive(ItemIds.WarTempo, ItemConfigurationState.Configured),
            Passive(ItemIds.VeteransMark, ItemConfigurationState.Configured),
            Passive(ItemIds.QuartermastersSatchel, ItemConfigurationState.Configured),
            Passive(ItemIds.SpellbreakerSeal, ItemConfigurationState.Configured),
            Passive(ItemIds.RivalryOath, ItemConfigurationState.Configured),
            Passive(ItemIds.ForgeTreasury, ItemConfigurationState.Configured),
            Passive(ItemIds.BattlefieldCommand, ItemConfigurationState.Configured),
            Passive(ItemIds.ForgekeepersGift, ItemConfigurationState.Configured),
            Passive(ItemIds.DragonfallJudgment, ItemConfigurationState.Configured),
            Passive(ItemIds.DraconicPresence, ItemConfigurationState.Configured)
        };

        public static IReadOnlyList<ItemCatalogEntry> All => entries;

        public static bool TryGet(ItemId id, out ItemCatalogEntry entry)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == id)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default(ItemCatalogEntry);
            return false;
        }

        private static ItemCatalogEntry Active(ItemId id, ItemConfigurationState state)
        {
            return new ItemCatalogEntry(id, ItemCategory.Active, state);
        }

        private static ItemCatalogEntry Passive(ItemId id, ItemConfigurationState state)
        {
            return new ItemCatalogEntry(id, ItemCategory.Passive, state);
        }
    }
}
