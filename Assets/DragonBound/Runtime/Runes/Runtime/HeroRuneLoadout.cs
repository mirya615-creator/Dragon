using System;
using System.Collections.Generic;

namespace DragonBound.Runes
{
    public sealed class RuneLoadoutSnapshot
    {
        private readonly Dictionary<string, string> assignments;
        internal RuneLoadoutSnapshot(Dictionary<string, string> source) { assignments = new Dictionary<string, string>(source, StringComparer.Ordinal); }
        public static RuneLoadoutSnapshot Empty { get; } = new RuneLoadoutSnapshot(new Dictionary<string, string>(StringComparer.Ordinal));
        public string GetRune(string heroId) { string runeId; return assignments.TryGetValue(heroId, out runeId) ? runeId : string.Empty; }
        public IReadOnlyDictionary<string, string> Assignments => assignments;
    }
    [Serializable]
    public sealed class HeroRuneLoadout
    {
        private readonly Dictionary<string, string> assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        public bool IsLocked { get; private set; }
        public RuneLoadoutSnapshot RunStartSnapshot { get; private set; }
        public IReadOnlyDictionary<string, string> Assignments => assignments;
        public string GetRune(string heroId) { string runeId; return assignments.TryGetValue(heroId, out runeId) ? runeId : string.Empty; }
        public bool Assign(string heroId, string runeId, RuneInventory inventory)
        {
            if (IsLocked || string.IsNullOrWhiteSpace(heroId) || RuneCatalog.Get(runeId) == null || inventory == null) return false;
            string previous; if (assignments.TryGetValue(heroId, out previous) && previous == runeId) return true;
            if (inventory.OwnedCount(runeId) <= AssignedCopies(runeId, heroId)) return false;
            assignments[heroId] = runeId; return true;
        }
        public bool Unassign(string heroId) { return !IsLocked && assignments.Remove(heroId); }
        public int AssignedCopies(string runeId, string excludingHeroId = null)
        { var count = 0; foreach (var pair in assignments) if (pair.Value == runeId && pair.Key != excludingHeroId) count++; return count; }
        public bool Validate(RuneInventory inventory, out string error)
        {
            foreach (var pair in assignments)
            {
                if (RuneCatalog.Get(pair.Value) == null) { error = "UnknownRune:" + pair.Value; return false; }
                if (inventory == null || AssignedCopies(pair.Value) > inventory.OwnedCount(pair.Value)) { error = "AssignedCopies:" + pair.Value; return false; }
            }
            error = string.Empty; return true;
        }
        public bool LockAtRunStart(RuneInventory inventory) { string error; if (!Validate(inventory, out error)) return false; IsLocked = true; RunStartSnapshot = new RuneLoadoutSnapshot(assignments); return true; }
        public void LockEmptyAtRunStart()
        {
            IsLocked = true;
            RunStartSnapshot = RuneLoadoutSnapshot.Empty;
        }

        public void UnlockForLoadoutEditing()
        {
            IsLocked = false;
            RunStartSnapshot = null;
        }

        public bool TryRestorePersistentAssignments(
            IEnumerable<RuneLoadoutAssignment> source,
            RuneInventory inventory,
            out string error)
        {
            assignments.Clear();
            IsLocked = false;
            RunStartSnapshot = null;
            if (source != null)
            {
                foreach (var assignment in source)
                {
                    if (assignment == null || string.IsNullOrWhiteSpace(assignment.HeroId) ||
                        RuneCatalog.Get(assignment.RuneId) == null || assignments.ContainsKey(assignment.HeroId))
                    {
                        error = "InvalidLoadoutAssignment";
                        assignments.Clear();
                        return false;
                    }

                    assignments.Add(assignment.HeroId, assignment.RuneId);
                }
            }

            return Validate(inventory, out error);
        }

        public List<RuneLoadoutAssignment> CreatePersistentCopy()
        {
            var result = new List<RuneLoadoutAssignment>(assignments.Count);
            foreach (var pair in assignments)
            {
                result.Add(new RuneLoadoutAssignment { HeroId = pair.Key, RuneId = pair.Value });
            }

            result.Sort((left, right) => string.CompareOrdinal(left.HeroId, right.HeroId));
            return result;
        }
    }
}
