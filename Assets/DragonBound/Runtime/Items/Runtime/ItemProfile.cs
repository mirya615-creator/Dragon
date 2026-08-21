using System;
using System.Collections.Generic;

namespace DragonBound.Items
{
    public enum ItemMatchCompletionOutcome
    {
        Victory,
        Defeat,
        AbnormalExit
    }

    public static class ItemAccountProgressRules
    {
        public static bool CountsAsNormalCompletedMatch(ItemMatchCompletionOutcome outcome)
        {
            return outcome == ItemMatchCompletionOutcome.Victory ||
                   outcome == ItemMatchCompletionOutcome.Defeat;
        }
    }

    public static class ItemOperationFailure
    {
        public const string None = "None";
        public const string Locked = "Locked";
        public const string UnknownItem = "UnknownItem";
        public const string PendingImplementation = "PendingImplementation";
        public const string NotOwned = "NotOwned";
        public const string DuplicateItem = "DuplicateItem";
        public const string ActiveSlotsFull = "ActiveSlotsFull";
        public const string PassiveSlotsFull = "PassiveSlotsFull";
        public const string InvalidLoadout = "InvalidLoadout";
        public const string InvalidDayKey = "InvalidDayKey";
        public const string InvalidAccountProgress = "InvalidAccountProgress";
        public const string IncompatibleSchema = "IncompatibleSchema";
    }

    public interface IItemDayKeyProvider
    {
        string GetDayKey();
    }

    public interface IItemProfileRepository
    {
        bool TryLoad(out ItemProfileData data, out string error);
        bool Save(ItemProfileData data, out string error);
    }

    public interface IItemServerLedger
    {
        bool TryGetAuthoritativeDayKey(out string dayKey);
    }

    /// <summary>Authoritative account progress supplied by matchmaking/meta services.
    /// Item code never increments this value and never derives it from a device clock.</summary>
    public interface IItemAccountProgressProvider
    {
        bool TryGetNormalCompletedMatchCount(out int completedMatchCount);
    }

    [Serializable]
    public sealed class ItemInventoryEntry
    {
        public string ItemId;
        public ItemRarity Rarity;
        public int OwnedCount;
        public int FragmentCount;

        public ItemInventoryEntry Clone()
        {
            return new ItemInventoryEntry
            {
                ItemId = ItemId,
                Rarity = Rarity,
                OwnedCount = OwnedCount,
                FragmentCount = FragmentCount
            };
        }
    }

    public sealed class ItemDailyInventory
    {
        private readonly Dictionary<string, ItemInventoryEntry> entries =
            new Dictionary<string, ItemInventoryEntry>(StringComparer.Ordinal);

        public IEnumerable<ItemInventoryEntry> Entries => entries.Values;

        public int OwnedCount(string itemId)
        {
            ItemInventoryEntry entry;
            return entries.TryGetValue(itemId, out entry) ? entry.OwnedCount : 0;
        }

        public int FragmentCount(string itemId)
        {
            ItemInventoryEntry entry;
            return entries.TryGetValue(itemId, out entry) ? entry.FragmentCount : 0;
        }

        public bool Owns(string itemId)
        {
            return OwnedCount(itemId) > 0;
        }

        public bool TryGrantOwned(string itemId)
        {
            var definition = ItemCatalog.Get(itemId);
            if (definition == null)
            {
                return false;
            }

            ItemInventoryEntry entry;
            if (entries.TryGetValue(itemId, out entry))
            {
                return false;
            }

            entries.Add(itemId, new ItemInventoryEntry
            {
                ItemId = itemId,
                Rarity = definition.Rarity,
                OwnedCount = 1,
                FragmentCount = 0
            });
            return true;
        }

        public bool TryAddFragments(string itemId, int count)
        {
            var definition = ItemCatalog.Get(itemId);
            if (definition == null || count < 0)
            {
                return false;
            }

            ItemInventoryEntry entry;
            if (!entries.TryGetValue(itemId, out entry))
            {
                entry = new ItemInventoryEntry
                {
                    ItemId = itemId,
                    Rarity = definition.Rarity
                };
                entries.Add(itemId, entry);
            }

            entry.FragmentCount = checked(entry.FragmentCount + count);
            return true;
        }

        public bool TryGet(string itemId, out ItemInventoryEntry entry)
        {
            return entries.TryGetValue(itemId, out entry);
        }

        public List<ItemInventoryEntry> CreatePersistentCopy()
        {
            var copy = new List<ItemInventoryEntry>(entries.Count);
            foreach (var entry in entries.Values)
            {
                copy.Add(entry.Clone());
            }

            copy.Sort((left, right) => string.CompareOrdinal(left.ItemId, right.ItemId));
            return copy;
        }

        internal bool TryRestore(IEnumerable<ItemInventoryEntry> source, out string error)
        {
            entries.Clear();
            error = string.Empty;
            if (source == null)
            {
                return true;
            }

            foreach (var entry in source)
            {
                var definition = entry == null ? null : ItemCatalog.Get(entry.ItemId);
                if (definition == null || entry.OwnedCount < 0 || entry.OwnedCount > 1 || entry.FragmentCount < 0 ||
                    definition.Rarity != entry.Rarity || entries.ContainsKey(entry.ItemId))
                {
                    entries.Clear();
                    error = ItemOperationFailure.InvalidLoadout;
                    return false;
                }

                entries.Add(entry.ItemId, entry.Clone());
            }

            return true;
        }
    }

    public sealed class ItemLoadout
    {
        public const int MaxActiveItems = 2;
        public const int MaxPassiveItems = 6;

        private readonly List<string> activeItems = new List<string>();
        private readonly List<string> passiveItems = new List<string>();

        public IReadOnlyList<string> ActiveItems => activeItems;
        public IReadOnlyList<string> PassiveItems => passiveItems;

        public bool TryEquip(string itemId, ItemDailyInventory inventory, out string reason)
        {
            reason = ItemOperationFailure.None;
            var definition = ItemCatalog.Get(itemId);
            if (definition == null)
            {
                reason = ItemOperationFailure.UnknownItem;
                return false;
            }

            if (!definition.IsFormalCandidate)
            {
                reason = ItemOperationFailure.PendingImplementation;
                return false;
            }

            if (inventory == null || !inventory.Owns(itemId))
            {
                reason = ItemOperationFailure.NotOwned;
                return false;
            }

            if (Contains(itemId))
            {
                reason = ItemOperationFailure.DuplicateItem;
                return false;
            }

            if (definition.Category == ItemCategory.Active)
            {
                if (activeItems.Count >= MaxActiveItems)
                {
                    reason = ItemOperationFailure.ActiveSlotsFull;
                    return false;
                }

                activeItems.Add(itemId);
            }
            else
            {
                if (passiveItems.Count >= MaxPassiveItems)
                {
                    reason = ItemOperationFailure.PassiveSlotsFull;
                    return false;
                }

                passiveItems.Add(itemId);
            }

            return true;
        }

        public bool TryUnequip(string itemId)
        {
            return activeItems.Remove(itemId) || passiveItems.Remove(itemId);
        }

        public bool Contains(string itemId)
        {
            return activeItems.Contains(itemId) || passiveItems.Contains(itemId);
        }

        public bool Validate(ItemDailyInventory inventory, out string reason)
        {
            reason = ItemOperationFailure.None;
            if (inventory == null)
            {
                reason = ItemOperationFailure.NotOwned;
                return false;
            }

            if (activeItems.Count > MaxActiveItems || passiveItems.Count > MaxPassiveItems)
            {
                reason = ItemOperationFailure.InvalidLoadout;
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < activeItems.Count; i++)
            {
                if (!ValidateItem(activeItems[i], ItemCategory.Active, inventory, seen, out reason))
                {
                    return false;
                }
            }

            for (var i = 0; i < passiveItems.Count; i++)
            {
                if (!ValidateItem(passiveItems[i], ItemCategory.Passive, inventory, seen, out reason))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsSameAs(ItemLoadout other)
        {
            if (other == null || activeItems.Count != other.activeItems.Count ||
                passiveItems.Count != other.passiveItems.Count)
            {
                return false;
            }

            for (var i = 0; i < activeItems.Count; i++)
            {
                if (!string.Equals(activeItems[i], other.activeItems[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (var i = 0; i < passiveItems.Count; i++)
            {
                if (!string.Equals(passiveItems[i], other.passiveItems[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        internal void Restore(IEnumerable<string> active, IEnumerable<string> passive)
        {
            activeItems.Clear();
            passiveItems.Clear();
            if (active != null)
            {
                activeItems.AddRange(active);
            }

            if (passive != null)
            {
                passiveItems.AddRange(passive);
            }
        }

        private static bool ValidateItem(
            string itemId,
            ItemCategory expectedCategory,
            ItemDailyInventory inventory,
            HashSet<string> seen,
            out string reason)
        {
            var definition = ItemCatalog.Get(itemId);
            if (definition == null)
            {
                reason = ItemOperationFailure.UnknownItem;
                return false;
            }

            if (definition.Category != expectedCategory)
            {
                reason = ItemOperationFailure.InvalidLoadout;
                return false;
            }

            if (!definition.IsFormalCandidate)
            {
                reason = ItemOperationFailure.PendingImplementation;
                return false;
            }

            if (!inventory.Owns(itemId))
            {
                reason = ItemOperationFailure.NotOwned;
                return false;
            }

            if (!seen.Add(itemId))
            {
                reason = ItemOperationFailure.DuplicateItem;
                return false;
            }

            reason = ItemOperationFailure.None;
            return true;
        }
    }

    [Serializable]
    public sealed class ItemProfileData
    {
        public int SchemaVersion = ItemProfile.CurrentSchemaVersion;
        public string DayKey = string.Empty;
        public int NormalCompletedMatchCount;
        public List<ItemInventoryEntry> Inventory = new List<ItemInventoryEntry>();
        public List<string> ActiveLoadout = new List<string>();
        public List<string> PassiveLoadout = new List<string>();
    }

    public sealed class ItemProfile
    {
        public const int CurrentSchemaVersion = 2;
        public const int UnlockCompletedMatchCount = 5;

        public ItemProfile()
        {
            Inventory = new ItemDailyInventory();
            Loadout = new ItemLoadout();
        }

        public string DayKey { get; private set; } = string.Empty;
        public int NormalCompletedMatchCount { get; private set; }
        public bool IsUnlocked => NormalCompletedMatchCount >= UnlockCompletedMatchCount;
        public ItemDailyInventory Inventory { get; }
        public ItemLoadout Loadout { get; }

        public bool RefreshDay(IItemDayKeyProvider dayKeyProvider, out string reason)
        {
            reason = ItemOperationFailure.None;
            if (dayKeyProvider == null)
            {
                reason = ItemOperationFailure.InvalidDayKey;
                return false;
            }

            var key = dayKeyProvider.GetDayKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                reason = ItemOperationFailure.InvalidDayKey;
                return false;
            }

            if (!string.Equals(DayKey, key, StringComparison.Ordinal))
            {
                string resetError;
                Inventory.TryRestore(null, out resetError);
                Loadout.Restore(null, null);
            }

            DayKey = key;
            return true;
        }

        public bool RefreshAuthoritativeAccountProgress(
            IItemAccountProgressProvider progressProvider,
            out string reason)
        {
            reason = ItemOperationFailure.None;
            if (progressProvider == null ||
                !progressProvider.TryGetNormalCompletedMatchCount(out var completedMatchCount) ||
                completedMatchCount < 0)
            {
                reason = ItemOperationFailure.InvalidAccountProgress;
                return false;
            }

            NormalCompletedMatchCount = completedMatchCount;
            return true;
        }

        public bool TryCreateRunSnapshot(out ItemRunSnapshot snapshot, out string reason)
        {
            snapshot = null;
            reason = ItemOperationFailure.None;
            if (!IsUnlocked)
            {
                reason = ItemOperationFailure.Locked;
                return false;
            }

            if (!Loadout.Validate(Inventory, out reason))
            {
                return false;
            }

            snapshot = new ItemRunSnapshot(Loadout.ActiveItems, Loadout.PassiveItems, DayKey);
            return true;
        }

        public ItemProfileData CreatePersistentData()
        {
            var data = new ItemProfileData
            {
                SchemaVersion = CurrentSchemaVersion,
                DayKey = DayKey,
                NormalCompletedMatchCount = NormalCompletedMatchCount,
                Inventory = Inventory.CreatePersistentCopy(),
                ActiveLoadout = new List<string>(Loadout.ActiveItems),
                PassiveLoadout = new List<string>(Loadout.PassiveItems)
            };
            return data;
        }

        public bool TryRestorePersistentData(ItemProfileData data, out string reason)
        {
            reason = ItemOperationFailure.None;
            // V1 used DayNumber for the former Day 2 gate. It has no trustworthy mapping to
            // completed-match progress, so migration deliberately requires a server refresh.
            if (data == null || data.SchemaVersion != CurrentSchemaVersion)
            {
                reason = ItemOperationFailure.IncompatibleSchema;
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.DayKey) ||
                data.NormalCompletedMatchCount < 0)
            {
                reason = ItemOperationFailure.InvalidDayKey;
                return false;
            }

            var restoredInventory = new ItemDailyInventory();
            if (!restoredInventory.TryRestore(data.Inventory, out reason))
            {
                return false;
            }

            var restoredLoadout = new ItemLoadout();
            restoredLoadout.Restore(data.ActiveLoadout, data.PassiveLoadout);
            if (!restoredLoadout.Validate(restoredInventory, out reason))
            {
                return false;
            }

            DayKey = data.DayKey;
            NormalCompletedMatchCount = data.NormalCompletedMatchCount;
            if (!Inventory.TryRestore(restoredInventory.CreatePersistentCopy(), out reason))
            {
                return false;
            }

            Loadout.Restore(restoredLoadout.ActiveItems, restoredLoadout.PassiveItems);
            return true;
        }
    }

    public sealed class ItemRunSnapshot
    {
        private readonly List<string> activeItems;
        private readonly List<string> passiveItems;

        public static ItemRunSnapshot Empty { get; } = new ItemRunSnapshot(null, null, string.Empty);

        internal ItemRunSnapshot(IEnumerable<string> active, IEnumerable<string> passive, string dayKey)
        {
            activeItems = active == null ? new List<string>() : new List<string>(active);
            passiveItems = passive == null ? new List<string>() : new List<string>(passive);
            DayKey = dayKey ?? string.Empty;
        }

        public string DayKey { get; }
        public IReadOnlyList<string> ActiveItems => activeItems.AsReadOnly();
        public IReadOnlyList<string> PassiveItems => passiveItems.AsReadOnly();

        public bool Contains(string itemId)
        {
            return activeItems.Contains(itemId) || passiveItems.Contains(itemId);
        }

        public bool IsActive(string itemId)
        {
            return activeItems.Contains(itemId);
        }

        public bool IsPassive(string itemId)
        {
            return passiveItems.Contains(itemId);
        }
    }
}
