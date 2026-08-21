using System;
using System.Collections.Generic;

namespace DragonBound.Runes
{
    [Serializable]
    public sealed class RuneInventoryEntry
    {
        public string RuneId;
        public RuneRarity Rarity;
        public int OwnedCount;
        public int FragmentCount;
    }
    [Serializable]
    public sealed class RuneInventory
    {
        private readonly Dictionary<string, RuneInventoryEntry> entries = new Dictionary<string, RuneInventoryEntry>(StringComparer.Ordinal);
        public const int EpicFragmentsPerRune = 3;
        public const int LegendaryFragmentsPerRune = 5;
        public IEnumerable<RuneInventoryEntry> Entries => entries.Values;
        public RuneInventoryEntry GetOrCreate(string runeId)
        {
            var definition = RuneCatalog.Get(runeId);
            if (definition == null) throw new ArgumentException("Unknown RuneId", nameof(runeId));
            RuneInventoryEntry entry;
            if (!entries.TryGetValue(runeId, out entry))
            {
                entry = new RuneInventoryEntry { RuneId = runeId, Rarity = definition.Rarity };
                entries.Add(runeId, entry);
            }
            return entry;
        }
        public int OwnedCount(string runeId) { return GetOrCreate(runeId).OwnedCount; }
        public int FragmentCount(string runeId) { return GetOrCreate(runeId).FragmentCount; }
        public void AddComplete(string runeId, int count = 1) { if (count < 0) throw new ArgumentOutOfRangeException(nameof(count)); GetOrCreate(runeId).OwnedCount += count; }
        public void AddFragment(string runeId, int count = 1)
        {
            var entry = GetOrCreate(runeId);
            if (entry.Rarity == RuneRarity.Common || entry.Rarity == RuneRarity.Excellent) throw new InvalidOperationException("Common and Excellent Runes do not have fragments.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count)); entry.FragmentCount += count;
        }
        public bool CanCraftRune(string runeId)
        {
            var entry = GetOrCreate(runeId);
            return entry.Rarity == RuneRarity.Epic ? entry.FragmentCount >= EpicFragmentsPerRune : entry.Rarity == RuneRarity.Legendary && entry.FragmentCount >= LegendaryFragmentsPerRune;
        }
        public bool CraftRune(string runeId)
        {
            if (!CanCraftRune(runeId)) return false;
            var entry = GetOrCreate(runeId); entry.FragmentCount -= entry.Rarity == RuneRarity.Epic ? EpicFragmentsPerRune : LegendaryFragmentsPerRune; entry.OwnedCount++; return true;
        }
        public bool TryGet(string runeId, out RuneInventoryEntry entry) { return entries.TryGetValue(runeId, out entry); }

        public List<RuneInventoryEntry> CreatePersistentCopy()
        {
            var result = new List<RuneInventoryEntry>(entries.Count);
            foreach (var entry in entries.Values)
            {
                result.Add(new RuneInventoryEntry
                {
                    RuneId = entry.RuneId,
                    Rarity = entry.Rarity,
                    OwnedCount = entry.OwnedCount,
                    FragmentCount = entry.FragmentCount
                });
            }

            result.Sort((left, right) => string.CompareOrdinal(left.RuneId, right.RuneId));
            return result;
        }

        public static bool TryCreateFromPersistentEntries(
            IEnumerable<RuneInventoryEntry> source,
            out RuneInventory inventory,
            out string error)
        {
            inventory = new RuneInventory();
            error = string.Empty;
            if (source == null)
            {
                return true;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in source)
            {
                var definition = entry == null ? null : RuneCatalog.Get(entry.RuneId);
                if (definition == null || !seen.Add(entry.RuneId))
                {
                    error = "InvalidInventoryEntry";
                    return false;
                }

                if (entry.Rarity != definition.Rarity || entry.OwnedCount < 0 || entry.FragmentCount < 0)
                {
                    error = "InvalidInventoryCount:" + entry.RuneId;
                    return false;
                }

                if ((definition.Rarity == RuneRarity.Common || definition.Rarity == RuneRarity.Excellent) &&
                    entry.FragmentCount != 0)
                {
                    error = "UnexpectedFragments:" + entry.RuneId;
                    return false;
                }

                inventory.entries.Add(entry.RuneId, new RuneInventoryEntry
                {
                    RuneId = entry.RuneId,
                    Rarity = entry.Rarity,
                    OwnedCount = entry.OwnedCount,
                    FragmentCount = entry.FragmentCount
                });
            }

            return true;
        }
    }
}
