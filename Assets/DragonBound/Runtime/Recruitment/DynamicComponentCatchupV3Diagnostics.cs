using System;
using System.Collections.Generic;
using System.Text;
using GameShared.Random;

namespace DragonBound.Recruitment
{
    public enum DynamicComponentCatchupV3Tier
    {
        Normal,
        Light,
        Medium,
        Strong,
        Severe
    }

    public readonly struct DynamicComponentCatchupV3Weights
    {
        public DynamicComponentCatchupV3Weights(
            float pureBasic,
            float oneComponent,
            float twoComponents,
            float threeComponents)
        {
            PureBasic = pureBasic;
            OneComponent = oneComponent;
            TwoComponents = twoComponents;
            ThreeComponents = threeComponents;
        }

        public float PureBasic { get; }
        public float OneComponent { get; }
        public float TwoComponents { get; }
        public float ThreeComponents { get; }
        public float ExpectedComponents => OneComponent + TwoComponents * 2f + ThreeComponents * 3f;
    }

    /// <summary>
    /// Versioned V3 policy data. Production V3 and the paired diagnostic share this source;
    /// production V2 remains a separate selectable policy in RecruitDeck.
    /// </summary>
    public static class DynamicComponentCatchupV3Config
    {
        public const string AlgorithmVersion = "DynamicComponentCatchupV3.diagnostic.1";
        public const string ComponentStreamId = "RecruitComponentCatchupV3";
        public const string ComponentContext = "RecruitComponentCatchupV3.ComponentCount";
        public const int MaxComponentsPerRecruit = 3;

        private static readonly float[] TargetDeliveredByWave =
        {
            0f,
            0.8f, 1.6f, 2.4f, 4.0f, 6.0f, 8.5f, 11.0f,
            13.5f, 16.0f, 18.5f, 21.0f, 22.5f, 24.0f
        };

        public static float GetTargetDelivered(int wave)
        {
            if (wave <= 0)
            {
                return 0f;
            }

            return wave < TargetDeliveredByWave.Length
                ? TargetDeliveredByWave[wave]
                : 24f;
        }

        public static DynamicComponentCatchupV3Tier GetTier(
            int recruitmentNumber,
            int wave,
            int actualDeliveredComponents)
        {
            if (recruitmentNumber <= 3)
            {
                return DynamicComponentCatchupV3Tier.Normal;
            }

            var deficit = GetTargetDelivered(wave) - actualDeliveredComponents;
            if (deficit <= 0f)
            {
                return DynamicComponentCatchupV3Tier.Normal;
            }

            if (deficit <= 1.5f)
            {
                return DynamicComponentCatchupV3Tier.Light;
            }

            if (deficit <= 3.0f)
            {
                return DynamicComponentCatchupV3Tier.Medium;
            }

            if (deficit <= 5.0f)
            {
                return DynamicComponentCatchupV3Tier.Strong;
            }

            return DynamicComponentCatchupV3Tier.Severe;
        }

        public static DynamicComponentCatchupV3Weights GetWeights(DynamicComponentCatchupV3Tier tier)
        {
            switch (tier)
            {
                case DynamicComponentCatchupV3Tier.Light:
                    return new DynamicComponentCatchupV3Weights(0.40f, 0.15f, 0.25f, 0.20f);
                case DynamicComponentCatchupV3Tier.Medium:
                    return new DynamicComponentCatchupV3Weights(0.30f, 0.10f, 0.25f, 0.35f);
                case DynamicComponentCatchupV3Tier.Strong:
                    return new DynamicComponentCatchupV3Weights(0.20f, 0.05f, 0.20f, 0.55f);
                case DynamicComponentCatchupV3Tier.Severe:
                    return new DynamicComponentCatchupV3Weights(0.10f, 0.00f, 0.15f, 0.75f);
                default:
                    return new DynamicComponentCatchupV3Weights(0.50f, 0.20f, 0.20f, 0.10f);
            }
        }

        public static float GetPureBasicWeight(DynamicComponentCatchupV3Tier tier)
        {
            return GetWeights(tier).PureBasic;
        }
    }

    public readonly struct DynamicComponentCatchupV3Batch
    {
        public DynamicComponentCatchupV3Batch(
            int recruitmentNumber,
            int wave,
            DynamicComponentCatchupV3Tier tier,
            float deficit,
            int plannedComponentCount,
            int deliveredComponentCount,
            int basicUnitCount,
            bool forgePickGenerated,
            ShovelSpawnDecision forgePickDecision,
            int remainingComponentCount)
        {
            RecruitmentNumber = recruitmentNumber;
            Wave = wave;
            Tier = tier;
            Deficit = deficit;
            PlannedComponentCount = plannedComponentCount;
            DeliveredComponentCount = deliveredComponentCount;
            BasicUnitCount = basicUnitCount;
            ForgePickGenerated = forgePickGenerated;
            ForgePickDecision = forgePickDecision;
            RemainingComponentCount = remainingComponentCount;
        }

        public int RecruitmentNumber { get; }
        public int Wave { get; }
        public DynamicComponentCatchupV3Tier Tier { get; }
        public float Deficit { get; }
        public int PlannedComponentCount { get; }
        public int DeliveredComponentCount { get; }
        public int BasicUnitCount { get; }
        public bool ForgePickGenerated { get; }
        public ShovelSpawnDecision ForgePickDecision { get; }
        public int RemainingComponentCount { get; }
        public int ResultCount => DeliveredComponentCount + BasicUnitCount + (ForgePickGenerated ? 1 : 0);
    }

    /// <summary>
    /// Candidate-only Recruit simulator. It uses the formal 24-instance bag and shovel state,
    /// but never changes the production RecruitDeck or any live run object.
    /// </summary>
    public sealed class DynamicComponentCatchupV3Deck
    {
        private const string RandomContentVersion = "DragonBound.HeroComponents.v1";
        private readonly LimitedComponentBag bag;
        private readonly ShovelRecruitmentState shovelState;
        private readonly int runSeed;

        public DynamicComponentCatchupV3Deck(
            RecruitmentCatalog catalog,
            int runSeed,
            int lockedCellCount = 18)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            this.runSeed = runSeed;
            bag = LimitedComponentBag.CreateBag(
                runSeed,
                RandomContentVersion,
                catalog);
            shovelState = new ShovelRecruitmentState(() => Math.Max(0, lockedCellCount));
        }

        public int CompletedRecruitments { get; private set; }
        public LimitedComponentBag ComponentBag => bag;
        public ShovelRecruitmentState ShovelState => shovelState;

        public DynamicComponentCatchupV3Batch DrawNext(int wave)
        {
            var recruitmentNumber = CompletedRecruitments + 1;
            var deliveredBefore = bag.DrawnCount;
            var target = DynamicComponentCatchupV3Config.GetTargetDelivered(wave);
            var deficit = target - deliveredBefore;
            var tier = DynamicComponentCatchupV3Config.GetTier(
                recruitmentNumber,
                wave,
                deliveredBefore);
            var weights = DynamicComponentCatchupV3Config.GetWeights(tier);
            var componentRoll = CreateRandom("ComponentCount", recruitmentNumber).NextUnit(
                DynamicComponentCatchupV3Config.ComponentContext);
            var plannedComponentCount = DrawComponentCount(componentRoll, weights);
            var deliveredComponentCount = Math.Min(
                plannedComponentCount,
                Math.Min(DynamicComponentCatchupV3Config.MaxComponentsPerRecruit, bag.RemainingCount));
            if (deliveredComponentCount > 0)
            {
                bag.Draw(deliveredComponentCount);
            }

            var forgePickDecision = shovelState.PreviewDecision(
                recruitmentNumber,
                CreateRandom("ForgePick", recruitmentNumber));
            var forgePickGenerated = forgePickDecision.ShouldSpawn;
            var basicUnitCount = RecruitBatch.CardsPerRecruitment -
                                 deliveredComponentCount -
                                 (forgePickGenerated ? 1 : 0);
            if (basicUnitCount < 1)
            {
                throw new InvalidOperationException("Candidate V3 must always retain one basic unit.");
            }

            shovelState.Commit(forgePickDecision);
            CompletedRecruitments = recruitmentNumber;
            if (bag.DrawnCount + bag.RemainingCount != bag.InitialCount)
            {
                throw new InvalidOperationException("Candidate V3 component conservation failed.");
            }

            return new DynamicComponentCatchupV3Batch(
                recruitmentNumber,
                wave,
                tier,
                deficit,
                plannedComponentCount,
                deliveredComponentCount,
                basicUnitCount,
                forgePickGenerated,
                forgePickDecision,
                bag.RemainingCount);
        }

        private IRunRandom CreateRandom(string stream, int recruitmentNumber)
        {
            return new RunRandom(DeriveSeed(runSeed, stream, recruitmentNumber));
        }

        private static int DrawComponentCount(
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
            if (roll < weights.TwoComponents)
            {
                return 2;
            }

            return DynamicComponentCatchupV3Config.MaxComponentsPerRecruit;
        }

        private static int DeriveSeed(int seed, string stream, int recruitmentNumber)
        {
            unchecked
            {
                uint hash = 2166136261U;
                hash = Mix(hash, (uint)seed);
                hash = Mix(hash, RandomContentVersion);
                hash = Mix(hash, DynamicComponentCatchupV3Config.AlgorithmVersion);
                hash = Mix(hash, stream);
                hash = Mix(hash, (uint)recruitmentNumber);
                return (int)hash;
            }
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619U;
        }

        private static uint Mix(uint hash, string value)
        {
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619U;
            }

            return hash;
        }
    }

    public sealed class DynamicComponentCatchupV3RecruitStats
    {
        internal DynamicComponentCatchupV3RecruitStats(int sampleCount)
        {
            SampleCount = sampleCount;
            ComponentCounts = new int[14, 5];
            EligibleCounts = new int[14];
            RollAttemptedCounts = new int[14];
            GeneratedCounts = new int[14];
            CumulativeForgePickCounts = new int[14, 4];
            TierCounts = new int[14, 5];
            V2PressureBands = new int[14, 4];
            PlannedComponentTotals = new int[14];
            DeliveredComponentTotals = new int[14];
            BasicUnitTotals = new int[14];
            CumulativeDeliveredTotals = new int[14];
            RemainingTotals = new int[14];
            BagEmptyCounts = new int[14];
            RemainingLE1Counts = new int[14];
            RemainingLE2Counts = new int[14];
            RemainingExactCounts = new int[14, 25];
        }

        public int SampleCount { get; }
        public int[,] ComponentCounts { get; }
        public int[] EligibleCounts { get; }
        public int[] RollAttemptedCounts { get; }
        public int[] GeneratedCounts { get; }
        public int[,] CumulativeForgePickCounts { get; }
        public int[,] TierCounts { get; }
        public int[,] V2PressureBands { get; }
        public int[] PlannedComponentTotals { get; }
        public int[] DeliveredComponentTotals { get; }
        public int[] BasicUnitTotals { get; }
        public int[] CumulativeDeliveredTotals { get; }
        public int[] RemainingTotals { get; }
        public int[] BagEmptyCounts { get; }
        public int[] RemainingLE1Counts { get; }
        public int[] RemainingLE2Counts { get; }
        public int[,] RemainingExactCounts { get; }
        public int HardPityTriggerCount { get; internal set; }
        public int LongestEligibleMissStreak { get; internal set; }
        public int ConservationFailures { get; internal set; }

        public double AverageComponentCount(int recruit) => Average(PlannedComponentTotals[recruit]);
        public double AverageDeliveredComponentCount(int recruit) => Average(DeliveredComponentTotals[recruit]);
        public double AverageBasicUnitCount(int recruit) => Average(BasicUnitTotals[recruit]);
        public double AverageCumulativeDelivered(int recruit) => Average(CumulativeDeliveredTotals[recruit]);
        public double AverageRemaining(int recruit) => Average(RemainingTotals[recruit]);
        public double ForgePickEligibleRate(int recruit) => Rate(EligibleCounts[recruit], SampleCount);
        public double ForgePickGeneratedRate(int recruit) => Rate(GeneratedCounts[recruit], SampleCount);
        public double BagEmptyRate(int recruit) => Rate(BagEmptyCounts[recruit], SampleCount);
        public double RemainingLE1Rate(int recruit) => Rate(RemainingLE1Counts[recruit], SampleCount);
        public double RemainingLE2Rate(int recruit) => Rate(RemainingLE2Counts[recruit], SampleCount);
        public double RemainingExactRate(int recruit, int remaining) => Rate(RemainingExactCounts[recruit, remaining], SampleCount);

        public double AverageCumulativeForgePicks(int recruit)
        {
            var total = 0;
            for (var i = 0; i < 4; i++)
            {
                total += CumulativeForgePickCounts[recruit, i] * i;
            }

            return Average(total);
        }

        public string FormatRow(int recruit, bool candidate)
        {
            var tierText = candidate
                ? FormatTierDistribution(recruit)
                : FormatV2PressureDistribution(recruit);
            return $"R{recruit} P0/P1/P2/P3/P4={ComponentCounts[recruit, 0]}/" +
                   $"{ComponentCounts[recruit, 1]}/{ComponentCounts[recruit, 2]}/" +
                   $"{ComponentCounts[recruit, 3]}/{ComponentCounts[recruit, 4]} " +
                   $"AvgComponentsThisRecruit={AverageDeliveredComponentCount(recruit):F3} " +
                   $"AvgComponentsDeliveredCumulative={AverageCumulativeDelivered(recruit):F3} " +
                   $"AvgRemainingInBag={AverageRemaining(recruit):F3} " +
                   $"BagEmptyRate={BagEmptyRate(recruit):F2}% RemainingLE1Rate={RemainingLE1Rate(recruit):F2}% " +
                   $"RemainingLE2Rate={RemainingLE2Rate(recruit):F2}% " +
                   $"ForgePickRollEligibleRate={ForgePickEligibleRate(recruit):F2}% " +
                   $"ForgePickGeneratedRate={ForgePickGeneratedRate(recruit):F2}% " +
                   $"AvgForgePicksCumulative={AverageCumulativeForgePicks(recruit):F3} " +
                   $"ForgePickP0/P1/P2/P3Plus={Rate(CumulativeForgePickCounts[recruit, 0], SampleCount):F2}%/" +
                   $"{Rate(CumulativeForgePickCounts[recruit, 1], SampleCount):F2}%/" +
                   $"{Rate(CumulativeForgePickCounts[recruit, 2], SampleCount):F2}%/" +
                   $"{Rate(CumulativeForgePickCounts[recruit, 3], SampleCount):F2}% " +
                   $"PureBasicRate={Rate(ComponentCounts[recruit, 0], SampleCount):F2}% " +
                   $"CatchupTier={tierText}";
        }

        public string FormatTierDistribution(int recruit)
        {
            return $"N/L/M/S/Se={TierCounts[recruit, 0]}/{TierCounts[recruit, 1]}/" +
                   $"{TierCounts[recruit, 2]}/{TierCounts[recruit, 3]}/{TierCounts[recruit, 4]}";
        }

        public string FormatV2PressureDistribution(int recruit)
        {
            return $"Opening/Normal/Partial/Full={V2PressureBands[recruit, 0]}/" +
                   $"{V2PressureBands[recruit, 1]}/{V2PressureBands[recruit, 2]}/{V2PressureBands[recruit, 3]}";
        }

        private double Average(int total) => SampleCount == 0 ? 0d : total / (double)SampleCount;
        private static double Rate(int count, int denominator) => denominator == 0 ? 0d : count * 100d / denominator;
    }

    public sealed class DynamicComponentCatchupV3Comparison
    {
        internal DynamicComponentCatchupV3Comparison(int sampleCount)
        {
            SampleCount = sampleCount;
            Baseline = new DynamicComponentCatchupV3RecruitStats(sampleCount);
            Candidate = new DynamicComponentCatchupV3RecruitStats(sampleCount);
            CandidateW13Delivered = new int[sampleCount];
            BaselineW13Delivered = new int[sampleCount];
        }

        public int SampleCount { get; }
        public DynamicComponentCatchupV3RecruitStats Baseline { get; }
        public DynamicComponentCatchupV3RecruitStats Candidate { get; }
        public int[] CandidateW13Delivered { get; }
        public int[] BaselineW13Delivered { get; }

        public double CandidateW13AverageDelivered => Average(CandidateW13Delivered);
        public double BaselineW13AverageDelivered => Average(BaselineW13Delivered);
        public double CandidateW13AverageRemaining => Candidate.RemainingTotals[13] / (double)SampleCount;
        public double BaselineW13AverageRemaining => Baseline.RemainingTotals[13] / (double)SampleCount;
        public double CandidateW13BagEmptyRate => Candidate.BagEmptyRate(13);
        public double CandidateW13RemainingLE2Rate => Candidate.RemainingLE2Rate(13);
        public double CandidateW13RemainingGE4Rate =>
            100d - Candidate.BagEmptyRate(13) - Candidate.RemainingExactRate(13, 1) -
            Candidate.RemainingExactRate(13, 2) - Candidate.RemainingExactRate(13, 3);
        public double CandidateW13P10Delivered => Percentile(CandidateW13Delivered, 0.10);
        public double CandidateW13P25Delivered => Percentile(CandidateW13Delivered, 0.25);
        public double CandidateW13P50Delivered => Percentile(CandidateW13Delivered, 0.50);
        public double CandidateW13P75Delivered => Percentile(CandidateW13Delivered, 0.75);
        public double CandidateW13P90Delivered => Percentile(CandidateW13Delivered, 0.90);

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                $"DynamicComponentCatchupV3 A/B SampleCount={SampleCount} " +
                "RecruitWaveMap=R1:W1,R2:W2,R3:W3,R4:W4,R5:W5,R6:W6,R7:W7,R8:W8,R9:W9,R10:W10,R11:W11,R12:W12,R13:W13");
            builder.AppendLine(
                $"CandidateW13 AvgDelivered={CandidateW13AverageDelivered:F3} " +
                $"Median={CandidateW13P50Delivered:F3} P10/P25/P75/P90={CandidateW13P10Delivered:F3}/" +
                $"{CandidateW13P25Delivered:F3}/{CandidateW13P75Delivered:F3}/{CandidateW13P90Delivered:F3} " +
                $"AvgRemaining={CandidateW13AverageRemaining:F3} BagEmpty={CandidateW13BagEmptyRate:F2}% " +
                $"Remaining1={Candidate.RemainingExactRate(13, 1):F2}% " +
                $"Remaining2={Candidate.RemainingExactRate(13, 2):F2}% " +
                $"Remaining3={Candidate.RemainingExactRate(13, 3):F2}% " +
                $"RemainingGE4={CandidateW13RemainingGE4Rate:F2}% " +
                $"RemainingLE2={CandidateW13RemainingLE2Rate:F2}%");
            builder.AppendLine(
                $"BaselineV2W13 AvgDelivered={BaselineW13AverageDelivered:F3} " +
                $"AvgRemaining={BaselineW13AverageRemaining:F3} BagEmpty={Baseline.BagEmptyRate(13):F2}% " +
                $"RemainingLE2={Baseline.RemainingLE2Rate(13):F2}%");
            builder.AppendLine(
                $"CandidateForgePick HardPityTriggerCount={Candidate.HardPityTriggerCount} " +
                $"LongestEligibleMissStreak={Candidate.LongestEligibleMissStreak}");
            builder.AppendLine(
                $"BaselineConservationFailures={Baseline.ConservationFailures} " +
                $"CandidateConservationFailures={Candidate.ConservationFailures}");
            for (var recruit = 1; recruit <= 13; recruit++)
            {
                builder.AppendLine($"[BaselineV2] {Baseline.FormatRow(recruit, false)}");
                builder.AppendLine($"[CandidateV3] {Candidate.FormatRow(recruit, true)}");
            }

            return builder.ToString();
        }

        private static double Average(IReadOnlyList<int> values)
        {
            if (values.Count == 0)
            {
                return 0d;
            }

            var total = 0d;
            foreach (var value in values) total += value;
            return total / values.Count;
        }

        private static double Percentile(IReadOnlyList<int> source, double percentile)
        {
            var values = new List<int>(source);
            values.Sort();
            if (values.Count == 0) return 0d;
            var position = percentile * (values.Count - 1);
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            if (lower == upper) return values[lower];
            var fraction = position - lower;
            return values[lower] + (values[upper] - values[lower]) * fraction;
        }
    }

    public static class DynamicComponentCatchupV3Diagnostics
    {
        public static DynamicComponentCatchupV3Comparison SamplePaired(
            RecruitmentCatalog catalog,
            int firstRunSeed,
            int sampleCount)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (sampleCount < 1) throw new ArgumentOutOfRangeException(nameof(sampleCount));

            var comparison = new DynamicComponentCatchupV3Comparison(sampleCount);
            for (var index = 0; index < sampleCount; index++)
            {
                var seed = unchecked(firstRunSeed + index);
                SampleBaseline(catalog, seed, comparison.Baseline, comparison.BaselineW13Delivered, index);
                SampleCandidate(catalog, seed, comparison.Candidate, comparison.CandidateW13Delivered, index);
            }

            return comparison;
        }

        private static void SampleBaseline(
            RecruitmentCatalog catalog,
            int seed,
            DynamicComponentCatchupV3RecruitStats stats,
            int[] w13Delivered,
            int sampleIndex)
        {
            var bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
            var deck = new RecruitDeck(
                catalog,
                seed,
                "dynamic-v3-ab-baseline",
                bag,
                shovelState: new ShovelRecruitmentState(() => 18));
            var cumulativeShovels = 0;
            for (var recruit = 1; recruit <= 13; recruit++)
            {
                var deliveredBefore = bag.DrawnCount;
                var batch = deck.DrawNext();
                var components = Count(batch, RecruitItemKind.HeroComponent);
                var basics = Count(batch, RecruitItemKind.BasicUnit);
                var shovels = Count(batch, RecruitItemKind.Shovel);
                cumulativeShovels += shovels;
                RecordBatch(
                    stats,
                    recruit,
                    components,
                    components,
                    basics,
                    shovels > 0,
                    deck.ShovelState?.LastCommittedDecision ?? default,
                    cumulativeShovels,
                    bag,
                    GetV2PressureBand(recruit, deliveredBefore));
            }

            w13Delivered[sampleIndex] = bag.DrawnCount;
        }

        private static void SampleCandidate(
            RecruitmentCatalog catalog,
            int seed,
            DynamicComponentCatchupV3RecruitStats stats,
            int[] w13Delivered,
            int sampleIndex)
        {
            var deck = new DynamicComponentCatchupV3Deck(catalog, seed, 18);
            var cumulativeShovels = 0;
            for (var recruit = 1; recruit <= 13; recruit++)
            {
                var batch = deck.DrawNext(recruit);
                cumulativeShovels += batch.ForgePickGenerated ? 1 : 0;
                var tier = (int)batch.Tier;
                stats.TierCounts[recruit, tier]++;
                RecordBatch(
                    stats,
                    recruit,
                    batch.PlannedComponentCount,
                    batch.DeliveredComponentCount,
                    batch.BasicUnitCount,
                    batch.ForgePickGenerated,
                    batch.ForgePickDecision,
                    cumulativeShovels,
                    deck.ComponentBag,
                    -1);
            }

            stats.LongestEligibleMissStreak = Math.Max(
                stats.LongestEligibleMissStreak,
                deck.ShovelState.LongestEligibleNoShovelInterval);
            w13Delivered[sampleIndex] = deck.ComponentBag.DrawnCount;
        }

        private static void RecordBatch(
            DynamicComponentCatchupV3RecruitStats stats,
            int recruit,
            int plannedComponents,
            int deliveredComponents,
            int basicUnits,
            bool generatedShovel,
            ShovelSpawnDecision decision,
            int cumulativeShovels,
            LimitedComponentBag bag,
            int v2PressureBand)
        {
            stats.PlannedComponentTotals[recruit] += plannedComponents;
            stats.DeliveredComponentTotals[recruit] += deliveredComponents;
            stats.BasicUnitTotals[recruit] += basicUnits;
            stats.CumulativeDeliveredTotals[recruit] += bag.DrawnCount;
            stats.RemainingTotals[recruit] += bag.RemainingCount;
            stats.ComponentCounts[recruit, Math.Min(4, deliveredComponents)]++;
            stats.CumulativeForgePickCounts[recruit, Math.Min(3, cumulativeShovels)]++;
            if (decision.IsEligible) stats.EligibleCounts[recruit]++;
            if (decision.RollAttempted) stats.RollAttemptedCounts[recruit]++;
            if (generatedShovel) stats.GeneratedCounts[recruit]++;
            if (bag.DrawnCount + bag.RemainingCount != bag.InitialCount) stats.ConservationFailures++;
            if (bag.IsExhausted) stats.BagEmptyCounts[recruit]++;
            if (bag.RemainingCount <= 1) stats.RemainingLE1Counts[recruit]++;
            if (bag.RemainingCount <= 2) stats.RemainingLE2Counts[recruit]++;
            stats.RemainingExactCounts[recruit, bag.RemainingCount]++;
            if (v2PressureBand >= 0) stats.V2PressureBands[recruit, v2PressureBand]++;
            if (decision.IsEligible && decision.IsGuaranteed) stats.HardPityTriggerCount++;
        }

        private static int GetV2PressureBand(int recruit, int deliveredBefore)
        {
            if (recruit <= FiniteComponentRecruitmentConfig.OpeningProtectedRecruitCount) return 0;
            var protectedExpected = FiniteComponentRecruitmentConfig.BaseExpectedComponentsPerRecruit *
                                    FiniteComponentRecruitmentConfig.OpeningProtectedRecruitCount;
            var ordinal = recruit - FiniteComponentRecruitmentConfig.OpeningProtectedRecruitCount;
            var allowed = protectedExpected + FiniteComponentRecruitmentConfig.CatchupAllowedComponentsPerRecruit * ordinal;
            var projected = deliveredBefore + FiniteComponentRecruitmentConfig.BaseExpectedComponentsPerRecruit;
            var lag = allowed - projected;
            var pressure = lag <= 0f ? 0f : Math.Min(1f, lag / FiniteComponentRecruitmentConfig.CatchupFullPressureLag);
            if (pressure <= 0f) return 1;
            return pressure >= 1f ? 3 : 2;
        }

        private static int Count(RecruitBatch batch, RecruitItemKind kind)
        {
            var count = 0;
            foreach (var card in batch.Cards) if (card.Kind == kind) count++;
            return count;
        }
    }
}
