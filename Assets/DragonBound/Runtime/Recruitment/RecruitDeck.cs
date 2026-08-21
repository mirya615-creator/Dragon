using System;
using System.Collections.Generic;
using GameShared.Random;

namespace DragonBound.Recruitment
{
    public enum RecruitComponentPolicy
    {
        V2 = 0,
        V3 = 1
    }

    [Serializable]
    public sealed class RecruitDeckState
    {
        public int RunSeed;
        public string RuntimePrefix;
        public int CompletedRecruitments;
        public RecruitComponentPolicy ComponentPolicy;
        public LimitedComponentBagState ComponentBag;
        public ShovelRecruitmentStateData ShovelState;
    }

    public readonly struct FiniteRecruitBatchTelemetry
    {
        public FiniteRecruitBatchTelemetry(
            int recruitmentNumber,
            int plannedComponentCount,
            int deliveredComponentCount,
            int basicUnitCount,
            bool generatedShovel,
            ShovelSpawnDecision shovelDecision)
        {
            RecruitmentNumber = recruitmentNumber;
            PlannedComponentCount = plannedComponentCount;
            DeliveredComponentCount = deliveredComponentCount;
            BasicUnitCount = basicUnitCount;
            GeneratedShovel = generatedShovel;
            ShovelDecision = shovelDecision;
        }

        public int RecruitmentNumber { get; }
        public int PlannedComponentCount { get; }
        public int DeliveredComponentCount { get; }
        public int BasicUnitCount { get; }
        public bool GeneratedShovel { get; }
        public ShovelSpawnDecision ShovelDecision { get; }
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
        private readonly ShovelRecruitmentState shovelState;
        private readonly int finiteRunSeed;
        private readonly RecruitComponentPolicy componentPolicy;
        private readonly Func<int> currentWaveProvider;
        private int componentIndex;

        private enum DynamicFiniteBatchKind
        {
            PureBasic,
            OneComponent,
            MultiComponent,
            Shovel
        }

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
                0,
                null,
                RecruitComponentPolicy.V2,
                null)
        {
        }

        public RecruitDeck(
            RecruitmentCatalog catalog,
            int runSeed,
            string runtimePrefix,
            LimitedComponentBag componentBag,
            bool enableHeroComponents = true,
            bool heroSliceMode = false,
            ShovelRecruitmentState shovelState = null,
            RecruitComponentPolicy componentPolicy = RecruitComponentPolicy.V2,
            Func<int> currentWaveProvider = null)
            : this(
                catalog,
                new RunRandom(DeriveSeed(runSeed, runtimePrefix, "legacy")),
                runtimePrefix,
                enableHeroComponents,
                heroSliceMode,
                componentBag,
                runSeed,
                shovelState,
                componentPolicy,
                currentWaveProvider)
        {
        }

        private RecruitDeck(
            RecruitmentCatalog catalog,
            IRunRandom random,
            string runtimePrefix,
            bool enableHeroComponents,
            bool heroSliceMode,
            LimitedComponentBag componentBag,
            int runSeed,
            ShovelRecruitmentState suppliedShovelState,
            RecruitComponentPolicy componentPolicy,
            Func<int> currentWaveProvider)
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
            shovelState = componentBag != null
                ? suppliedShovelState ?? new ShovelRecruitmentState()
                : null;
            finiteRunSeed = runSeed;
            this.componentPolicy = componentPolicy;
            this.currentWaveProvider = currentWaveProvider;
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
        public ShovelRecruitmentState ShovelState => shovelState;
        public RecruitComponentPolicy ComponentPolicy => componentPolicy;
        public bool HasLastFiniteBatchTelemetry { get; private set; }
        public FiniteRecruitBatchTelemetry LastFiniteBatchTelemetry { get; private set; }

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
                ComponentPolicy = componentPolicy,
                ComponentBag = finiteComponentBag.CaptureState(),
                ShovelState = shovelState?.CaptureState()
            };
        }

        public static RecruitDeck RestoreFinite(
            RecruitmentCatalog catalog,
            RecruitDeckState state,
            Func<int> lockedCellCountProvider = null,
            Func<int> currentWaveProvider = null)
        {
            if (state == null || state.ComponentBag == null || string.IsNullOrWhiteSpace(state.RuntimePrefix))
            {
                throw new ArgumentException("A complete finite recruitment deck state is required.", nameof(state));
            }

            var restoredShovelState = new ShovelRecruitmentState(
                lockedCellCountProvider ?? (() => state.ShovelState?.LockedCellCountAtCapture ?? int.MaxValue));
            var deck = new RecruitDeck(
                catalog,
                state.RunSeed,
                state.RuntimePrefix,
                LimitedComponentBag.Restore(catalog, state.ComponentBag),
                shovelState: restoredShovelState,
                componentPolicy: state.ComponentPolicy,
                currentWaveProvider: currentWaveProvider);
            if (state.CompletedRecruitments < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            deck.CompletedRecruitments = state.CompletedRecruitments;
            if (state.ShovelState != null)
            {
                deck.shovelState.RestoreState(state.ShovelState);
            }
            return deck;
        }

        private RecruitBatch BuildFiniteComponentBatch(bool consume)
        {
            return componentPolicy == RecruitComponentPolicy.V3
                ? BuildFiniteComponentBatchV3(consume)
                : BuildFiniteComponentBatchV2(consume);
        }

        // This method is the preserved V2 transaction path. Keep its ordering and streams stable.
        private RecruitBatch BuildFiniteComponentBatchV2(bool consume)
        {
            var recruitmentNumber = CompletedRecruitments + 1;
            var batchKind = DrawDynamicFiniteBatchKind(recruitmentNumber);
            var plannedComponentCount = GetDynamicFiniteComponentCount(
                recruitmentNumber,
                batchKind,
                FiniteComponentRecruitmentConfig.MaxComponentsPerBatch);
            // Resolve the independent shovel decision before drawing from the finite bag. A
            // successful shovel reserves one of the five positions and leaves another for a
            // basic unit, so a planned four-component batch delivers three components instead.
            // The deferred instance remains in the bag for later dynamic catch-up.
            var shovelDecision = shovelState?.PreviewDecision(
                recruitmentNumber,
                CreateFiniteBatchRandom(ShovelRecruitmentConfig.RandomStreamId, recruitmentNumber));
            var componentCapacity = shovelDecision.HasValue && shovelDecision.Value.ShouldSpawn
                ? FiniteComponentRecruitmentConfig.MaxComponentsPerBatch - 1
                : FiniteComponentRecruitmentConfig.MaxComponentsPerBatch;
            var componentCount = Math.Min(plannedComponentCount, componentCapacity);
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

            if (shovelDecision.HasValue && shovelDecision.Value.ShouldSpawn)
            {
                cards.Add(CreateCard(
                    recruitmentNumber,
                    cards.Count,
                    RecruitItemKind.Shovel,
                    ShovelRecruitmentConfig.ShovelConfigId,
                    string.Empty));
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
                if (shovelDecision.HasValue)
                {
                    shovelState.Commit(shovelDecision.Value);
                }

                HasLastFiniteBatchTelemetry = true;
                LastFiniteBatchTelemetry = new FiniteRecruitBatchTelemetry(
                    recruitmentNumber,
                    plannedComponentCount,
                    componentCount,
                    CountCards(cards, RecruitItemKind.BasicUnit),
                    shovelDecision.HasValue && shovelDecision.Value.ShouldSpawn,
                    shovelDecision ?? default);
            }

            return new RecruitBatch(recruitmentNumber, cards);
        }

        private RecruitBatch BuildFiniteComponentBatchV3(bool consume)
        {
            var recruitmentNumber = CompletedRecruitments + 1;
            var wave = ResolveV3Wave(recruitmentNumber);
            var tier = DynamicComponentCatchupV3Config.GetTier(
                recruitmentNumber,
                wave,
                finiteComponentBag.DrawnCount);
            var weights = DynamicComponentCatchupV3Config.GetWeights(tier);
            var componentRoll = CreateFiniteBatchRandom(
                DynamicComponentCatchupV3Config.ComponentStreamId,
                recruitmentNumber).NextUnit(DynamicComponentCatchupV3Config.ComponentContext);
            var plannedComponentCount = DrawV3ComponentCount(componentRoll, weights);
            var componentCount = Math.Min(
                plannedComponentCount,
                Math.Min(DynamicComponentCatchupV3Config.MaxComponentsPerRecruit, finiteComponentBag.RemainingCount));

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

            // V3 treats Forge Pick as an independent utility result. It occupies a basic slot
            // only after the component count is fixed, so it cannot defer or discard a component.
            var shovelDecision = shovelState?.PreviewDecision(
                recruitmentNumber,
                CreateFiniteBatchRandom(ShovelRecruitmentConfig.RandomStreamId, recruitmentNumber));
            if (shovelDecision.HasValue && shovelDecision.Value.ShouldSpawn)
            {
                cards.Add(CreateCard(
                    recruitmentNumber,
                    cards.Count,
                    RecruitItemKind.Shovel,
                    ShovelRecruitmentConfig.ShovelConfigId,
                    string.Empty));
            }

            var basicRandom = CreateFiniteBatchRandom("basic-unit", recruitmentNumber);
            while (cards.Count < RecruitBatch.CardsPerRecruitment)
            {
                cards.Add(DrawBasicUnit(recruitmentNumber, cards.Count, basicRandom));
            }

            if (CountCards(cards, RecruitItemKind.BasicUnit) < FiniteComponentRecruitmentConfig.MinBasicUnitsPerBatch)
            {
                throw new InvalidOperationException("V3 finite recruitment must retain at least one basic unit.");
            }

            Shuffle(cards, CreateFiniteBatchRandom("slot-order", recruitmentNumber), "RecruitSlotOrder.v1");
            if (consume)
            {
                CompletedRecruitments = recruitmentNumber;
                if (shovelDecision.HasValue)
                {
                    shovelState.Commit(shovelDecision.Value);
                }

                HasLastFiniteBatchTelemetry = true;
                LastFiniteBatchTelemetry = new FiniteRecruitBatchTelemetry(
                    recruitmentNumber,
                    plannedComponentCount,
                    componentCount,
                    CountCards(cards, RecruitItemKind.BasicUnit),
                    shovelDecision.HasValue && shovelDecision.Value.ShouldSpawn,
                    shovelDecision ?? default);
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

        private DynamicFiniteBatchKind DrawDynamicFiniteBatchKind(int recruitmentNumber)
        {
            if (finiteComponentBag.IsExhausted)
            {
                var emptyRoll = CreateFiniteBatchRandom(
                    FiniteComponentRecruitmentConfig.DynamicKindStreamId,
                    recruitmentNumber).NextUnit(FiniteComponentRecruitmentConfig.DynamicKindContext);
                return emptyRoll < FiniteComponentRecruitmentConfig.BaseShovelWeight
                    ? DynamicFiniteBatchKind.Shovel
                    : DynamicFiniteBatchKind.PureBasic;
            }

            GetDynamicFiniteWeights(
                out var pureBasicWeight,
                out var oneComponentWeight,
                out var multiComponentWeight,
                out var shovelWeight);

            var roll = CreateFiniteBatchRandom(
                FiniteComponentRecruitmentConfig.DynamicKindStreamId,
                recruitmentNumber).NextUnit(FiniteComponentRecruitmentConfig.DynamicKindContext);
            if (roll < pureBasicWeight)
            {
                return DynamicFiniteBatchKind.PureBasic;
            }

            roll -= pureBasicWeight;
            if (roll < oneComponentWeight)
            {
                return DynamicFiniteBatchKind.OneComponent;
            }

            roll -= oneComponentWeight;
            return roll < multiComponentWeight
                ? DynamicFiniteBatchKind.MultiComponent
                : DynamicFiniteBatchKind.Shovel;
        }

        private int ResolveV3Wave(int recruitmentNumber)
        {
            if (currentWaveProvider != null)
            {
                return Math.Max(1, currentWaveProvider());
            }

            // Standalone decks and diagnostics use the documented Rn -> Wn mapping.
            return Math.Max(1, recruitmentNumber);
        }

        private static int DrawV3ComponentCount(
            float roll,
            DynamicComponentCatchupV3Weights weights)
        {
            if (roll < weights.PureBasic)
            {
                return 0;
            }

            roll -= weights.PureBasic;
            if (roll < weights.OneComponent)
            {
                return 1;
            }

            roll -= weights.OneComponent;
            return roll < weights.TwoComponents
                ? 2
                : DynamicComponentCatchupV3Config.MaxComponentsPerRecruit;
        }

        private int GetDynamicFiniteComponentCount(
            int recruitmentNumber,
            DynamicFiniteBatchKind batchKind,
            int maximumComponents)
        {
            if (finiteComponentBag.RemainingCount <= 0)
            {
                return 0;
            }

            switch (batchKind)
            {
                case DynamicFiniteBatchKind.OneComponent:
                    return Math.Min(1, Math.Min(maximumComponents, finiteComponentBag.RemainingCount));
                case DynamicFiniteBatchKind.MultiComponent:
                    return Math.Min(
                        DrawDynamicMultiComponentCount(recruitmentNumber),
                        Math.Min(maximumComponents, finiteComponentBag.RemainingCount));
                default:
                    return 0;
            }
        }

        private void GetDynamicFiniteWeights(
            out float pureBasicWeight,
            out float oneComponentWeight,
            out float multiComponentWeight,
            out float shovelWeight)
        {
            shovelWeight = FiniteComponentRecruitmentConfig.BaseShovelWeight;
            pureBasicWeight = FiniteComponentRecruitmentConfig.BasePureBasicWeight;
            oneComponentWeight = FiniteComponentRecruitmentConfig.BaseOneComponentWeight;
            multiComponentWeight = FiniteComponentRecruitmentConfig.BaseMultiComponentWeight;

            var catchupPressure = GetDynamicCatchupPressure();
            if (catchupPressure <= 0f)
            {
                return;
            }

            var pureTransfer = pureBasicWeight * catchupPressure;
            pureBasicWeight -= pureTransfer;
            multiComponentWeight += pureTransfer;

            var oneTransferPressure = Clamp01(
                (catchupPressure - FiniteComponentRecruitmentConfig.OneComponentTransferPressureFloor) /
                (1f - FiniteComponentRecruitmentConfig.OneComponentTransferPressureFloor));
            var oneTransfer = oneComponentWeight * oneTransferPressure;
            oneComponentWeight -= oneTransfer;
            multiComponentWeight += oneTransfer;

            NormalizeDynamicWeights(
                ref pureBasicWeight,
                ref oneComponentWeight,
                ref multiComponentWeight,
                ref shovelWeight);
        }

        private int DrawDynamicMultiComponentCount(int recruitmentNumber)
        {
            var pressure = GetDynamicCatchupPressure();
            var weightTwo = 0.34f * (1f - pressure);
            var weightThree = 0.33f * (1f - 0.75f * pressure);
            var weightFour = 0.33f + 0.5875f * pressure;
            var total = weightTwo + weightThree + weightFour;

            var roll = CreateFiniteBatchRandom(
                FiniteComponentRecruitmentConfig.DynamicMultiCountStreamId,
                recruitmentNumber).NextUnit(FiniteComponentRecruitmentConfig.DynamicMultiCountContext) * total;
            if (roll < weightTwo)
            {
                return 2;
            }

            return roll < weightTwo + weightThree ? 3 : 4;
        }

        private float GetDynamicCatchupPressure()
        {
            if (CompletedRecruitments < FiniteComponentRecruitmentConfig.OpeningProtectedRecruitCount)
            {
                return 0f;
            }

            var deliveredComponents = finiteComponentBag.DrawnCount;
            var protectedExpected = FiniteComponentRecruitmentConfig.BaseExpectedComponentsPerRecruit *
                                    FiniteComponentRecruitmentConfig.OpeningProtectedRecruitCount;
            var catchupRecruitOrdinal = CompletedRecruitments -
                                        FiniteComponentRecruitmentConfig.OpeningProtectedRecruitCount + 1;
            var allowedAfterCurrentRecruit = protectedExpected +
                                             FiniteComponentRecruitmentConfig.CatchupAllowedComponentsPerRecruit *
                                             catchupRecruitOrdinal;
            var projectedAfterNormalBaseRecruit = deliveredComponents +
                                                  FiniteComponentRecruitmentConfig.BaseExpectedComponentsPerRecruit;
            var lag = allowedAfterCurrentRecruit - projectedAfterNormalBaseRecruit;
            return Clamp01(lag / FiniteComponentRecruitmentConfig.CatchupFullPressureLag);
        }

        private static void NormalizeDynamicWeights(
            ref float pureBasicWeight,
            ref float oneComponentWeight,
            ref float multiComponentWeight,
            ref float shovelWeight)
        {
            pureBasicWeight = Math.Max(0f, pureBasicWeight);
            oneComponentWeight = Math.Max(0f, oneComponentWeight);
            multiComponentWeight = Math.Max(0f, multiComponentWeight);
            shovelWeight = Math.Max(0f, shovelWeight);
            var total = pureBasicWeight + oneComponentWeight + multiComponentWeight + shovelWeight;
            if (total <= 0f)
            {
                pureBasicWeight = 1f;
                return;
            }

            pureBasicWeight /= total;
            oneComponentWeight /= total;
            multiComponentWeight /= total;
            shovelWeight /= total;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }

        private static int CountCards(IReadOnlyList<RecruitCard> cards, RecruitItemKind kind)
        {
            var count = 0;
            foreach (var card in cards)
            {
                if (card.Kind == kind)
                {
                    count++;
                }
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
