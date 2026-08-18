using System;
using System.Collections.Generic;
using GameShared.Random;

namespace DragonBound.Recruitment
{
    [Serializable]
    public sealed class RecruitDeckState
    {
        public int RunSeed;
        public string RuntimePrefix;
        public int CompletedRecruitments;
        public LimitedComponentBagState ComponentBag;
    }

    public sealed class RecruitDeck
    {
        private readonly RecruitmentCatalog catalog;
        private readonly IRunRandom random;
        private readonly string runtimePrefix;
        private readonly bool enableHeroComponents;
        private readonly bool heroSliceMode;
        private readonly IReadOnlyList<ComponentToken> componentSequence;
        private readonly LimitedComponentBag finiteComponentBag;
        private readonly int finiteRunSeed;
        private int componentIndex;

        public RecruitDeck(
            RecruitmentCatalog catalog,
            IRunRandom random,
            string runtimePrefix,
            bool enableHeroComponents = false,
            bool heroSliceMode = false)
            : this(
                catalog,
                random,
                runtimePrefix,
                enableHeroComponents,
                heroSliceMode,
                null,
                0)
        {
        }

        public RecruitDeck(
            RecruitmentCatalog catalog,
            int runSeed,
            string runtimePrefix,
            LimitedComponentBag componentBag,
            bool enableHeroComponents = true,
            bool heroSliceMode = false)
            : this(
                catalog,
                new RunRandom(DeriveSeed(runSeed, runtimePrefix, "legacy")),
                runtimePrefix,
                enableHeroComponents,
                heroSliceMode,
                componentBag,
                runSeed)
        {
        }

        private RecruitDeck(
            RecruitmentCatalog catalog,
            IRunRandom random,
            string runtimePrefix,
            bool enableHeroComponents,
            bool heroSliceMode,
            LimitedComponentBag componentBag,
            int runSeed)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (string.IsNullOrWhiteSpace(runtimePrefix))
            {
                throw new ArgumentException("A stable runtime prefix is required.", nameof(runtimePrefix));
            }

            this.runtimePrefix = runtimePrefix;
            this.enableHeroComponents = enableHeroComponents;
            if (heroSliceMode && !enableHeroComponents)
            {
                throw new ArgumentException(
                    "Hero slice mode requires hero components to be enabled.",
                    nameof(heroSliceMode));
            }

            if (componentBag != null && (!enableHeroComponents || heroSliceMode))
            {
                throw new ArgumentException(
                    "A finite component bag requires normal hero components to be enabled.",
                    nameof(componentBag));
            }

            this.heroSliceMode = heroSliceMode;
            finiteComponentBag = componentBag;
            finiteRunSeed = runSeed;
            componentSequence = componentBag != null
                ? new ComponentToken[0]
                : !enableHeroComponents
                ? new ComponentToken[0]
                : heroSliceMode
                    ? BuildHeroSliceComponentSequence()
                    : BuildComponentSequence(catalog, random);
        }

        public int CompletedRecruitments { get; private set; }
        public int InitialHeroComponents => finiteComponentBag != null
            ? finiteComponentBag.InitialCount
            : componentSequence.Count;
        public int RemainingHeroComponents => finiteComponentBag != null
            ? finiteComponentBag.RemainingCount
            : componentSequence.Count - componentIndex;
        public int DrawnHeroComponents => finiteComponentBag != null
            ? finiteComponentBag.DrawnCount
            : componentIndex;
        public int DiscardedHeroComponents => finiteComponentBag != null
            ? finiteComponentBag.DiscardedCount
            : 0;
        public bool EnableHeroComponents => enableHeroComponents;
        public bool HeroSliceMode => heroSliceMode;
        public bool UsesFiniteComponentBag => finiteComponentBag != null;
        public LimitedComponentBag ComponentBag => finiteComponentBag;

        public int GetRemainingHeroComponentCount(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("A component config id is required.", nameof(configId));
            }

            if (finiteComponentBag != null)
            {
                return finiteComponentBag.GetRemainingCount(configId);
            }

            var count = 0;
            for (var index = componentIndex; index < componentSequence.Count; index++)
            {
                if (string.Equals(componentSequence[index].ConfigId, configId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public int GetInitialHeroComponentCount(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("A component config id is required.", nameof(configId));
            }

            if (finiteComponentBag != null)
            {
                return finiteComponentBag.GetInitialCount(configId);
            }

            var count = 0;
            for (var index = 0; index < componentSequence.Count; index++)
            {
                if (string.Equals(componentSequence[index].ConfigId, configId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsUniqueHeroComponent(string configId)
        {
            if (!enableHeroComponents || string.IsNullOrWhiteSpace(configId))
            {
                return false;
            }

            if (heroSliceMode)
            {
                return HeroSliceRecruitmentConfig.TryGetComponent(configId, out var sliceComponent) &&
                       sliceComponent.IsUnique;
            }

            try
            {
                return catalog.GetComponent(configId).IsUnique;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public RecruitBatch DrawNext()
        {
            if (finiteComponentBag != null)
            {
                return BuildFiniteComponentBatch(true);
            }

            var recruitmentNumber = CompletedRecruitments + 1;
            var cards = new List<RecruitCard>(5);

            if (heroSliceMode)
            {
                var componentCount = GetHeroSliceComponentCount(recruitmentNumber);
                for (var index = 0; index < componentCount; index++)
                {
                    var token = componentSequence[componentIndex++];
                    cards.Add(CreateCard(
                        recruitmentNumber,
                        cards.Count,
                        RecruitItemKind.HeroComponent,
                        token.ConfigId,
                        token.InstanceId));
                }

                while (cards.Count < RecruitBatch.CardsPerRecruitment)
                {
                    cards.Add(DrawBasicUnit(recruitmentNumber, cards.Count));
                }
            }
            else if (enableHeroComponents && recruitmentNumber <= 8)
            {
                for (var index = 0; index < 3; index++)
                {
                    var token = componentSequence[componentIndex++];
                    cards.Add(CreateCard(recruitmentNumber, cards.Count, RecruitItemKind.HeroComponent, token.ConfigId, token.InstanceId));
                }

                for (var index = 0; index < 2; index++)
                {
                    cards.Add(DrawBasicUnit(recruitmentNumber, cards.Count));
                }
            }
            else
            {
                for (var index = 0; index < 5; index++)
                {
                    cards.Add(DrawBasicUnit(recruitmentNumber, cards.Count));
                }
            }

            Shuffle(cards, random, "recruit.batch.order");
            CompletedRecruitments = recruitmentNumber;
            return new RecruitBatch(recruitmentNumber, cards);
        }

        public RecruitBatch PeekNext()
        {
            if (finiteComponentBag == null)
            {
                throw new InvalidOperationException("Only finite component recruitment decks support non-mutating previews.");
            }

            return BuildFiniteComponentBatch(false);
        }

        public void CommitPreviewedBatch(RecruitBatch preview)
        {
            if (finiteComponentBag == null)
            {
                throw new InvalidOperationException("Only finite component recruitment decks support preview commits.");
            }

            if (!BatchesMatch(preview, BuildFiniteComponentBatch(false)))
            {
                throw new InvalidOperationException("The recruitment preview no longer matches the finite deck state.");
            }

            BuildFiniteComponentBatch(true);
        }

        public bool MarkComponentDiscarded(string componentInstanceId)
        {
            return finiteComponentBag != null && finiteComponentBag.MarkDiscarded(componentInstanceId);
        }

        public bool WasComponentDiscarded(string componentInstanceId)
        {
            return finiteComponentBag != null && finiteComponentBag.WasDiscarded(componentInstanceId);
        }

        public RecruitDeckState CaptureState()
        {
            if (finiteComponentBag == null)
            {
                throw new InvalidOperationException("Only finite component recruitment decks support state capture.");
            }

            return new RecruitDeckState
            {
                RunSeed = finiteRunSeed,
                RuntimePrefix = runtimePrefix,
                CompletedRecruitments = CompletedRecruitments,
                ComponentBag = finiteComponentBag.CaptureState()
            };
        }

        public static RecruitDeck RestoreFinite(
            RecruitmentCatalog catalog,
            RecruitDeckState state)
        {
            if (state == null || state.ComponentBag == null || string.IsNullOrWhiteSpace(state.RuntimePrefix))
            {
                throw new ArgumentException("A complete finite recruitment deck state is required.", nameof(state));
            }

            var deck = new RecruitDeck(
                catalog,
                state.RunSeed,
                state.RuntimePrefix,
                LimitedComponentBag.Restore(catalog, state.ComponentBag));
            if (state.CompletedRecruitments < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            deck.CompletedRecruitments = state.CompletedRecruitments;
            return deck;
        }

        private RecruitBatch BuildFiniteComponentBatch(bool consume)
        {
            var recruitmentNumber = CompletedRecruitments + 1;
            var componentCount = GetFiniteComponentCount(recruitmentNumber);
            var cards = new List<RecruitCard>(RecruitBatch.CardsPerRecruitment);
            var componentInstances = componentCount > 0
                ? consume
                    ? finiteComponentBag.Draw(componentCount)
                    : finiteComponentBag.Peek(componentCount)
                : new HeroComponentInstanceDefinition[0];
            foreach (var instance in componentInstances)
            {
                cards.Add(CreateCard(
                    recruitmentNumber,
                    cards.Count,
                    RecruitItemKind.HeroComponent,
                    instance.ComponentId,
                    instance.ComponentInstanceId));
            }

            var basicRandom = CreateFiniteBatchRandom("basic-unit", recruitmentNumber);
            while (cards.Count < RecruitBatch.CardsPerRecruitment)
            {
                cards.Add(DrawBasicUnit(recruitmentNumber, cards.Count, basicRandom));
            }

            Shuffle(cards, CreateFiniteBatchRandom("slot-order", recruitmentNumber), "RecruitSlotOrder.v1");
            if (consume)
            {
                CompletedRecruitments = recruitmentNumber;
            }

            return new RecruitBatch(recruitmentNumber, cards);
        }

        private static bool BatchesMatch(RecruitBatch expected, RecruitBatch actual)
        {
            if (expected == null || actual == null ||
                expected.RecruitmentNumber != actual.RecruitmentNumber ||
                expected.Cards.Count != actual.Cards.Count)
            {
                return false;
            }

            for (var index = 0; index < expected.Cards.Count; index++)
            {
                var left = expected.Cards[index];
                var right = actual.Cards[index];
                if (left.Kind != right.Kind ||
                    !string.Equals(left.RuntimeId, right.RuntimeId, StringComparison.Ordinal) ||
                    !string.Equals(left.ConfigId, right.ConfigId, StringComparison.Ordinal) ||
                    !string.Equals(left.SourceInstanceId, right.SourceInstanceId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetFiniteComponentCount(int recruitmentNumber)
        {
            if (finiteComponentBag.IsExhausted ||
                recruitmentNumber > FiniteComponentRecruitmentConfig.GuaranteedCompletionBatch)
            {
                return 0;
            }

            if (recruitmentNumber == FiniteComponentRecruitmentConfig.GuaranteedCompletionBatch)
            {
                return finiteComponentBag.RemainingCount;
            }

            if (recruitmentNumber > FiniteComponentRecruitmentConfig.NormalProbabilityBatchCount)
            {
                return 0;
            }

            var probabilityRandom = CreateFiniteBatchRandom("component-count", recruitmentNumber);
            var requested = probabilityRandom.NextUnit("RecruitComponentCount.v1") <
                            FiniteComponentRecruitmentConfig.ThreeComponentBatchChance
                ? FiniteComponentRecruitmentConfig.NormalMaxComponentsPerBatch
                : FiniteComponentRecruitmentConfig.NormalMinComponentsPerBatch;
            return ResolveFiniteComponentCount(requested, finiteComponentBag.RemainingCount);
        }

        private static int ResolveFiniteComponentCount(int requested, int remainingCount)
        {
            if (remainingCount <= 0)
            {
                return 0;
            }

            var count = Math.Min(requested, remainingCount);
            // Keep normal batches at two or three components whenever possible. This prevents
            // a previous three-component draw from stranding a single component before batch 11.
            if (remainingCount - count == 1)
            {
                count = count == FiniteComponentRecruitmentConfig.NormalMaxComponentsPerBatch
                    ? FiniteComponentRecruitmentConfig.NormalMinComponentsPerBatch
                    : FiniteComponentRecruitmentConfig.NormalMaxComponentsPerBatch;
                count = Math.Min(count, remainingCount);
            }

            return count;
        }

        private RecruitCard DrawBasicUnit(int recruitmentNumber, int slot)
        {
            return DrawBasicUnit(recruitmentNumber, slot, random);
        }

        private RecruitCard DrawBasicUnit(int recruitmentNumber, int slot, IRunRandom sourceRandom)
        {
            var index = sourceRandom.NextInt("recruit.basic.config", 0, catalog.BasicUnitIds.Count);
            return CreateCard(recruitmentNumber, slot, RecruitItemKind.BasicUnit, catalog.BasicUnitIds[index], string.Empty);
        }

        private RecruitCard CreateCard(
            int recruitmentNumber,
            int slot,
            RecruitItemKind kind,
            string configId,
            string sourceInstanceId)
        {
            var runtimeId = $"{runtimePrefix}.r{recruitmentNumber:00}.s{slot:00}";
            return new RecruitCard(
                runtimeId,
                kind,
                configId,
                sourceInstanceId,
                isUnique: kind == RecruitItemKind.HeroComponent && IsUniqueHeroComponent(configId));
        }

        private static int GetHeroSliceComponentCount(int recruitmentNumber)
        {
            switch (recruitmentNumber)
            {
                case 1:
                case 3:
                    return 1;
                case 2:
                    return 2;
                default:
                    return 0;
            }
        }

        private static IReadOnlyList<ComponentToken> BuildHeroSliceComponentSequence()
        {
            return new[]
            {
                new ComponentToken(
                    HeroSliceRecruitmentConfig.DragonSigilId,
                    $"{HeroSliceRecruitmentConfig.DragonSigilId}.copy01"),
                new ComponentToken(
                    HeroSliceRecruitmentConfig.SkyRangerId,
                    $"{HeroSliceRecruitmentConfig.SkyRangerId}.copy01"),
                new ComponentToken(
                    HeroSliceRecruitmentConfig.DragonSigilId,
                    $"{HeroSliceRecruitmentConfig.DragonSigilId}.copy02"),
                new ComponentToken(
                    HeroSliceRecruitmentConfig.DragonKnightId,
                    $"{HeroSliceRecruitmentConfig.DragonKnightId}.copy01")
            };
        }

        private static IReadOnlyList<ComponentToken> BuildComponentSequence(RecruitmentCatalog catalog, IRunRandom random)
        {
            var remaining = ExpandComponentTokens(catalog);
            var sequence = new ComponentToken[24];

            var purpleRecipes = GetRecipes(catalog, HeroRecipeRarity.Purple, false);
            var purpleRecipe = purpleRecipes[random.NextInt("recruit.guarantee.purple", 0, purpleRecipes.Count)];
            var purpleFirst = TakeToken(remaining, purpleRecipe.ComponentAId);
            var purpleSecond = TakeToken(remaining, purpleRecipe.ComponentBId);
            var firstSixSlots = CreateRange(0, 6);
            sequence[TakeRandomSlot(firstSixSlots, random, "recruit.guarantee.purple.slot")] = purpleFirst;
            sequence[TakeRandomSlot(firstSixSlots, random, "recruit.guarantee.purple.slot")] = purpleSecond;

            var stagedGoldRecipes = GetRecipes(catalog, HeroRecipeRarity.Gold, true);
            var goldRecipe = stagedGoldRecipes[random.NextInt("recruit.guarantee.gold", 0, stagedGoldRecipes.Count)];
            var goldFirst = TakeToken(remaining, goldRecipe.ComponentAId);
            var goldSecond = TakeToken(remaining, goldRecipe.ComponentBId);
            sequence[random.NextInt("recruit.guarantee.gold.exposure", 9, 12)] = goldFirst;
            sequence[random.NextInt("recruit.guarantee.gold.completion", 12, 18)] = goldSecond;

            var earlyEligible = new List<ComponentToken>();
            foreach (var token in remaining)
            {
                if (catalog.GetComponent(token.ConfigId).Pool != HeroComponentPool.Gold)
                {
                    earlyEligible.Add(token);
                }
            }

            Shuffle(earlyEligible, random, "recruit.components.early");
            for (var index = 0; index < 12; index++)
            {
                if (sequence[index] != null)
                {
                    continue;
                }

                if (earlyEligible.Count == 0)
                {
                    throw new InvalidOperationException("Not enough non-gold components to satisfy the early deck constraints.");
                }

                var token = earlyEligible[earlyEligible.Count - 1];
                earlyEligible.RemoveAt(earlyEligible.Count - 1);
                remaining.Remove(token);
                sequence[index] = token;
            }

            Shuffle(remaining, random, "recruit.components.late");
            var remainingIndex = 0;
            for (var index = 12; index < sequence.Length; index++)
            {
                if (sequence[index] == null)
                {
                    sequence[index] = remaining[remainingIndex++];
                }
            }

            if (remainingIndex != remaining.Count)
            {
                throw new InvalidOperationException("The constrained component deck did not consume exactly 24 cards.");
            }

            return sequence;
        }

        private static List<ComponentToken> ExpandComponentTokens(RecruitmentCatalog catalog)
        {
            var tokens = new List<ComponentToken>(24);
            foreach (var instance in catalog.ComponentBagTemplate)
            {
                tokens.Add(new ComponentToken(instance.ComponentId, instance.InstanceId));
            }

            return tokens;
        }

        private static List<HeroRecipeDefinition> GetRecipes(
            RecruitmentCatalog catalog,
            HeroRecipeRarity rarity,
            bool requireTwoGoldComponents)
        {
            var recipes = new List<HeroRecipeDefinition>();
            foreach (var recipe in catalog.Recipes)
            {
                if (recipe.Rarity != rarity)
                {
                    continue;
                }

                if (requireTwoGoldComponents &&
                    (catalog.GetComponent(recipe.ComponentAId).Pool != HeroComponentPool.Gold ||
                     catalog.GetComponent(recipe.ComponentBId).Pool != HeroComponentPool.Gold))
                {
                    continue;
                }

                recipes.Add(recipe);
            }

            if (recipes.Count == 0)
            {
                throw new InvalidOperationException($"No valid {rarity} recruitment guarantee recipe exists.");
            }

            return recipes;
        }

        private static ComponentToken TakeToken(List<ComponentToken> tokens, string configId)
        {
            for (var index = 0; index < tokens.Count; index++)
            {
                if (!string.Equals(tokens[index].ConfigId, configId, StringComparison.Ordinal))
                {
                    continue;
                }

                var token = tokens[index];
                tokens.RemoveAt(index);
                return token;
            }

            throw new InvalidOperationException($"Component {configId} is unavailable while building the deck.");
        }

        private static List<int> CreateRange(int start, int count)
        {
            var values = new List<int>(count);
            for (var value = start; value < start + count; value++)
            {
                values.Add(value);
            }

            return values;
        }

        private static int TakeRandomSlot(List<int> slots, IRunRandom random, string context)
        {
            var index = random.NextInt(context, 0, slots.Count);
            var slot = slots[index];
            slots.RemoveAt(index);
            return slot;
        }

        private static void Shuffle<T>(List<T> values, IRunRandom random, string context)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var swapIndex = random.NextInt(context, 0, index + 1);
                var value = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = value;
            }
        }

        private IRunRandom CreateFiniteBatchRandom(string streamId, int recruitmentNumber)
        {
            return new RunRandom(DeriveSeed(finiteRunSeed, runtimePrefix, streamId, recruitmentNumber));
        }

        private static int DeriveSeed(int runSeed, string runtimePrefix, string streamId, int batch = 0)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashText(hash, runtimePrefix);
                hash = HashText(hash, streamId);
                hash ^= (uint)runSeed;
                hash *= 16777619u;
                hash ^= (uint)batch;
                hash *= 16777619u;
                return (int)hash;
            }
        }

        private static uint HashText(uint hash, string value)
        {
            unchecked
            {
                foreach (var character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private sealed class ComponentToken
        {
            public ComponentToken(string configId, string instanceId)
            {
                ConfigId = configId;
                InstanceId = instanceId;
            }

            public string ConfigId { get; }
            public string InstanceId { get; }
        }
    }
}
