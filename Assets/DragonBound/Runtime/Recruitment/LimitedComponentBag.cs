using System;
using System.Collections.Generic;
using GameShared.Random;

namespace DragonBound.Recruitment
{
    /// <summary>
    /// Serializable run state for the finite 24-instance component bag. Lists are kept as
    /// concrete fields so the state can be persisted by Unity or an external save adapter.
    /// </summary>
    [Serializable]
    public sealed class LimitedComponentBagState
    {
        public int RunSeed;
        public string ContentVersion;
        public string RngVersion;
        public List<string> OrderedComponentInstanceIds = new List<string>();
        public int CurrentCursor;
        public int RemainingCount;
        public List<string> DrawnInstanceIds = new List<string>();
        public List<string> DiscardedInstanceIds = new List<string>();
    }

    /// <summary>
    /// A finite, deterministic component sequence. It is deliberately independent of
    /// RecruitDeck so the current five-card recruitment cadence remains unchanged.
    /// </summary>
    public sealed class LimitedComponentBag
    {
        public const string DefaultContentVersion = "DragonBound.HeroComponents.v1";
        public const string RngAlgorithmVersion = "RecruitComponentBag.v1";

        private readonly Dictionary<string, HeroComponentInstanceDefinition> definitionsByInstanceId;
        private readonly List<string> orderedInstanceIds;
        private readonly HashSet<string> drawnInstanceIds;
        private readonly HashSet<string> discardedInstanceIds;

        private LimitedComponentBag(
            RecruitmentCatalog catalog,
            int runSeed,
            string contentVersion,
            IReadOnlyList<string> orderedIds,
            int cursor,
            IEnumerable<string> drawnIds,
            IEnumerable<string> discardedIds)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException("A component bag content version is required.", nameof(contentVersion));
            }

            definitionsByInstanceId = new Dictionary<string, HeroComponentInstanceDefinition>(StringComparer.Ordinal);
            foreach (var instance in catalog.ComponentBagTemplate)
            {
                if (instance == null || !definitionsByInstanceId.TryAdd(instance.InstanceId, instance))
                {
                    throw new ArgumentException("The catalog contains invalid or duplicate component instances.", nameof(catalog));
                }
            }

            if (definitionsByInstanceId.Count != 24)
            {
                throw new ArgumentException("A finite component bag requires exactly 24 instances.", nameof(catalog));
            }

            orderedInstanceIds = new List<string>(orderedIds ?? throw new ArgumentNullException(nameof(orderedIds)));
            ValidateOrderedIds(orderedInstanceIds);
            if (cursor < 0 || cursor > orderedInstanceIds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(cursor));
            }

            drawnInstanceIds = new HashSet<string>(drawnIds ?? throw new ArgumentNullException(nameof(drawnIds)), StringComparer.Ordinal);
            discardedInstanceIds = new HashSet<string>(discardedIds ?? throw new ArgumentNullException(nameof(discardedIds)), StringComparer.Ordinal);
            ValidateRuntimeState(cursor);

            RunSeed = runSeed;
            ContentVersion = contentVersion;
            CurrentCursor = cursor;
        }

        public static LimitedComponentBag CreateBag(
            int runSeed,
            string contentVersion,
            RecruitmentCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var ids = new List<string>(catalog.ComponentBagTemplate.Count);
            foreach (var instance in catalog.ComponentBagTemplate)
            {
                ids.Add(instance.InstanceId);
            }

            var random = new RunRandom(DeriveSeed(runSeed, contentVersion));
            for (var index = ids.Count - 1; index > 0; index--)
            {
                var swapIndex = random.NextInt("RecruitComponentBag.shuffle", 0, index + 1);
                var value = ids[index];
                ids[index] = ids[swapIndex];
                ids[swapIndex] = value;
            }

            return new LimitedComponentBag(
                catalog,
                runSeed,
                contentVersion,
                ids,
                0,
                new string[0],
                new string[0]);
        }

        public static LimitedComponentBag Restore(
            RecruitmentCatalog catalog,
            LimitedComponentBagState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!string.Equals(state.RngVersion, RngAlgorithmVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unsupported component bag RNG version '{state.RngVersion}'. Expected '{RngAlgorithmVersion}'.");
            }

            var bag = new LimitedComponentBag(
                catalog,
                state.RunSeed,
                state.ContentVersion,
                state.OrderedComponentInstanceIds,
                state.CurrentCursor,
                state.DrawnInstanceIds,
                state.DiscardedInstanceIds);
            if (state.RemainingCount != bag.RemainingCount)
            {
                throw new InvalidOperationException("Component bag remaining count does not match its cursor.");
            }

            return bag;
        }

        public int RunSeed { get; }
        public string ContentVersion { get; }
        public string RngVersion => RngAlgorithmVersion;
        public IReadOnlyList<string> OrderedComponentInstanceIds => orderedInstanceIds.AsReadOnly();
        public IReadOnlyCollection<string> DrawnInstanceIds => drawnInstanceIds;
        public IReadOnlyCollection<string> DiscardedInstanceIds => discardedInstanceIds;
        public int CurrentCursor { get; private set; }
        public int InitialCount => orderedInstanceIds.Count;
        public int RemainingCount => orderedInstanceIds.Count - CurrentCursor;
        public int DrawnCount => drawnInstanceIds.Count;
        public int DiscardedCount => discardedInstanceIds.Count;
        public bool IsExhausted => RemainingCount == 0;

        public int GetInitialCount(string componentDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(componentDefinitionId))
            {
                throw new ArgumentException("A component definition id is required.", nameof(componentDefinitionId));
            }

            var count = 0;
            foreach (var instanceId in orderedInstanceIds)
            {
                if (string.Equals(GetInstance(instanceId).ComponentId, componentDefinitionId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public int GetRemainingCount(string componentDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(componentDefinitionId))
            {
                throw new ArgumentException("A component definition id is required.", nameof(componentDefinitionId));
            }

            var count = 0;
            for (var index = CurrentCursor; index < orderedInstanceIds.Count; index++)
            {
                if (string.Equals(GetInstance(orderedInstanceIds[index]).ComponentId, componentDefinitionId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public HeroComponentInstanceDefinition GetInstance(string componentInstanceId)
        {
            if (string.IsNullOrWhiteSpace(componentInstanceId) ||
                !definitionsByInstanceId.TryGetValue(componentInstanceId, out var definition))
            {
                throw new KeyNotFoundException($"Unknown component instance '{componentInstanceId}'.");
            }

            return definition;
        }

        public IReadOnlyList<HeroComponentInstanceDefinition> Peek(int count)
        {
            ValidateDrawCount(count);
            var result = new List<HeroComponentInstanceDefinition>(count);
            for (var index = 0; index < count; index++)
            {
                result.Add(GetInstance(orderedInstanceIds[CurrentCursor + index]));
            }

            return result.AsReadOnly();
        }

        public HeroComponentInstanceDefinition DrawOne()
        {
            return Draw(1)[0];
        }

        public IReadOnlyList<HeroComponentInstanceDefinition> Draw(int count)
        {
            ValidateDrawCount(count);
            var result = new List<HeroComponentInstanceDefinition>(count);
            for (var index = 0; index < count; index++)
            {
                var definition = GetInstance(orderedInstanceIds[CurrentCursor++]);
                drawnInstanceIds.Add(definition.InstanceId);
                result.Add(definition);
            }

            return result.AsReadOnly();
        }

        public bool MarkDiscarded(string componentInstanceId)
        {
            if (string.IsNullOrWhiteSpace(componentInstanceId) ||
                !drawnInstanceIds.Contains(componentInstanceId))
            {
                return false;
            }

            return discardedInstanceIds.Add(componentInstanceId);
        }

        public bool WasDiscarded(string componentInstanceId)
        {
            return !string.IsNullOrWhiteSpace(componentInstanceId) &&
                   discardedInstanceIds.Contains(componentInstanceId);
        }

        public LimitedComponentBagState CaptureState()
        {
            return new LimitedComponentBagState
            {
                RunSeed = RunSeed,
                ContentVersion = ContentVersion,
                RngVersion = RngVersion,
                OrderedComponentInstanceIds = new List<string>(orderedInstanceIds),
                CurrentCursor = CurrentCursor,
                RemainingCount = RemainingCount,
                DrawnInstanceIds = orderedInstanceIds.GetRange(0, CurrentCursor),
                DiscardedInstanceIds = orderedInstanceIds.FindAll(discardedInstanceIds.Contains)
            };
        }

        private void ValidateDrawCount(int count)
        {
            if (count < 1 || count > RemainingCount)
            {
                throw new InvalidOperationException(
                    $"Cannot draw {count} component instances with {RemainingCount} remaining.");
            }
        }

        private void ValidateOrderedIds(IReadOnlyList<string> ids)
        {
            if (ids.Count != definitionsByInstanceId.Count)
            {
                throw new ArgumentException("The ordered component bag must contain all 24 instances.", nameof(ids));
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (!definitionsByInstanceId.ContainsKey(id) || !unique.Add(id))
                {
                    throw new ArgumentException("The ordered component bag contains an unknown or duplicate instance.", nameof(ids));
                }
            }
        }

        private void ValidateRuntimeState(int cursor)
        {
            if (drawnInstanceIds.Count != cursor ||
                discardedInstanceIds.Count > drawnInstanceIds.Count)
            {
                throw new ArgumentException("Component bag cursor and drawn state are inconsistent.", nameof(cursor));
            }

            for (var index = 0; index < orderedInstanceIds.Count; index++)
            {
                var id = orderedInstanceIds[index];
                var shouldBeDrawn = index < cursor;
                if (drawnInstanceIds.Contains(id) != shouldBeDrawn ||
                    (discardedInstanceIds.Contains(id) && !shouldBeDrawn))
                {
                    throw new ArgumentException("Component bag drawn state does not match its cursor.", nameof(cursor));
                }
            }

            foreach (var id in discardedInstanceIds)
            {
                if (!drawnInstanceIds.Contains(id))
                {
                    throw new ArgumentException("Discarded instances must have been drawn first.", nameof(cursor));
                }
            }
        }

        private static int DeriveSeed(int runSeed, string contentVersion)
        {
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException("A component bag content version is required.", nameof(contentVersion));
            }

            unchecked
            {
                uint hash = 2166136261U;
                hash ^= (uint)runSeed;
                hash *= 16777619U;
                foreach (var character in contentVersion)
                {
                    hash ^= character;
                    hash *= 16777619U;
                }

                foreach (var character in RngAlgorithmVersion)
                {
                    hash ^= character;
                    hash *= 16777619U;
                }

                return (int)hash;
            }
        }
    }
}
