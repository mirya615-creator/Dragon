using System;
using System.Text;

namespace DragonBound.Recruitment
{
    /// <summary>Development-only shovel sampling driven by the same RecruitDeck used in a run.</summary>
    public sealed class ShovelRecruitmentDistribution
    {
        public const int DiagnosticRecruitCount = 12;

        private readonly int[] plannedComponentTotals = new int[DiagnosticRecruitCount + 1];
        private readonly int[] deliveredComponentTotals = new int[DiagnosticRecruitCount + 1];
        private readonly int[] basicUnitTotals = new int[DiagnosticRecruitCount + 1];
        private readonly int[] eligibleCounts = new int[DiagnosticRecruitCount + 1];
        private readonly int[] rollAttemptedCounts = new int[DiagnosticRecruitCount + 1];
        private readonly int[] generatedCounts = new int[DiagnosticRecruitCount + 1];
        private readonly int[] cumulativeShovelTotals = new int[DiagnosticRecruitCount + 1];
        private readonly int[] deliveredTotals = new int[DiagnosticRecruitCount + 1];
        private readonly int[] remainingTotals = new int[DiagnosticRecruitCount + 1];
        private readonly int[] legacyCapacityBlockedCounts = new int[DiagnosticRecruitCount + 1];
        private readonly int[,] rawComponentCounts = new int[DiagnosticRecruitCount + 1, 5];
        private readonly int[,] rawComponentEligibleCounts = new int[DiagnosticRecruitCount + 1, 5];
        private readonly int[,] rawComponentRollCounts = new int[DiagnosticRecruitCount + 1, 5];
        private readonly int[,] rawComponentGeneratedCounts = new int[DiagnosticRecruitCount + 1, 5];
        private readonly int[] notEligibleReasonCounts =
            new int[Enum.GetValues(typeof(ShovelNotEligibleReason)).Length];

        public int SampleCount { get; internal set; }
        public int EligibleBatchCount { get; internal set; }
        public int ShovelBatchCount { get; internal set; }
        public int RecruitOneToThreeEligible { get; internal set; }
        public int RecruitOneToThreeShovels { get; internal set; }
        public int RecruitFourToSevenEligible { get; internal set; }
        public int RecruitFourToSevenShovels { get; internal set; }
        public int RecruitEightToElevenEligible { get; internal set; }
        public int RecruitEightToElevenShovels { get; internal set; }
        public int GuaranteedShovelCount { get; internal set; }
        public int LongestEligibleNoShovelInterval { get; internal set; }
        public int ShovelsAfterRecruit4Total { get; internal set; }
        public int ShovelsAfterRecruit6Total { get; internal set; }
        public int ShovelsAfterRecruit8Total { get; internal set; }
        public int P0ShovelsByRecruit6 { get; internal set; }
        public int P1ShovelsByRecruit6 { get; internal set; }
        public int P2ShovelsByRecruit6 { get; internal set; }
        public int P3PlusShovelsByRecruit6 { get; internal set; }

        public double ShovelRatePerEligibleBatch => GetRate(ShovelBatchCount, EligibleBatchCount);
        public double AverageShovelsAfterRecruit4 => Average(ShovelsAfterRecruit4Total);
        public double AverageShovelsAfterRecruit6 => Average(ShovelsAfterRecruit6Total);
        public double AverageShovelsAfterRecruit8 => Average(ShovelsAfterRecruit8Total);
        public double RecruitOneToThreeRate => GetRate(RecruitOneToThreeShovels, RecruitOneToThreeEligible);
        public double RecruitFourToSevenRate => GetRate(RecruitFourToSevenShovels, RecruitFourToSevenEligible);
        public double RecruitEightToElevenRate => GetRate(RecruitEightToElevenShovels, RecruitEightToElevenEligible);

        public double GetAveragePlannedComponentCount(int recruitmentNumber) =>
            Average(GetAt(plannedComponentTotals, recruitmentNumber));

        public double GetAverageDeliveredComponentCount(int recruitmentNumber) =>
            Average(GetAt(deliveredComponentTotals, recruitmentNumber));

        public double GetAverageBasicUnitCount(int recruitmentNumber) =>
            Average(GetAt(basicUnitTotals, recruitmentNumber));

        public double GetForgePickEligibleRate(int recruitmentNumber) =>
            GetRate(GetAt(eligibleCounts, recruitmentNumber), SampleCount);

        public double GetForgePickRollAttemptedRate(int recruitmentNumber) =>
            GetRate(GetAt(rollAttemptedCounts, recruitmentNumber), SampleCount);

        public double GetForgePickGeneratedRate(int recruitmentNumber) =>
            GetRate(GetAt(generatedCounts, recruitmentNumber), SampleCount);

        public double GetAverageForgePicks(int recruitmentNumber) =>
            Average(GetAt(cumulativeShovelTotals, recruitmentNumber));

        public double GetAverageComponentsDelivered(int recruitmentNumber) =>
            Average(GetAt(deliveredTotals, recruitmentNumber));

        public double GetAverageComponentsRemaining(int recruitmentNumber) =>
            Average(GetAt(remainingTotals, recruitmentNumber));

        /// <summary>
        /// Counterfactual audit for the superseded V2 ordering: it rejected a batch when a
        /// four-component plan left only one basic slot before the Forge Pick was considered.
        /// It is diagnostic-only and never participates in the live eligibility decision.
        /// </summary>
        public double GetLegacyFourComponentBlockedRate(int recruitmentNumber) =>
            GetRate(GetAt(legacyCapacityBlockedCounts, recruitmentNumber), SampleCount);

        public int GetRawComponentCount(int recruitmentNumber, int componentCount) =>
            rawComponentCounts[ValidateRecruitment(recruitmentNumber), ValidateComponentCount(componentCount)];

        public double GetRawComponentEligibleRate(int recruitmentNumber, int componentCount) =>
            GetRate(
                rawComponentEligibleCounts[ValidateRecruitment(recruitmentNumber), ValidateComponentCount(componentCount)],
                GetRawComponentCount(recruitmentNumber, componentCount));

        public double GetRawComponentRollAttemptedRate(int recruitmentNumber, int componentCount) =>
            GetRate(
                rawComponentRollCounts[ValidateRecruitment(recruitmentNumber), ValidateComponentCount(componentCount)],
                GetRawComponentCount(recruitmentNumber, componentCount));

        public double GetRawComponentGeneratedRate(int recruitmentNumber, int componentCount) =>
            GetRate(
                rawComponentGeneratedCounts[ValidateRecruitment(recruitmentNumber), ValidateComponentCount(componentCount)],
                GetRawComponentCount(recruitmentNumber, componentCount));

        public int GetNotEligibleCount(ShovelNotEligibleReason reason) =>
            notEligibleReasonCounts[(int)reason];

        internal void RecordBatch(
            int recruitmentNumber,
            FiniteRecruitBatchTelemetry telemetry,
            int drawnComponentCount,
            int remainingComponentCount,
            int cumulativeShovelCount)
        {
            var recruit = ValidateRecruitment(recruitmentNumber);
            var rawComponents = ValidateComponentCount(telemetry.PlannedComponentCount);
            plannedComponentTotals[recruit] += telemetry.PlannedComponentCount;
            deliveredComponentTotals[recruit] += telemetry.DeliveredComponentCount;
            basicUnitTotals[recruit] += telemetry.BasicUnitCount;
            cumulativeShovelTotals[recruit] += cumulativeShovelCount;
            deliveredTotals[recruit] += drawnComponentCount;
            remainingTotals[recruit] += remainingComponentCount;
            rawComponentCounts[recruit, rawComponents]++;
            if (rawComponents == FiniteComponentRecruitmentConfig.MaxComponentsPerBatch)
            {
                legacyCapacityBlockedCounts[recruit]++;
            }

            var decision = telemetry.ShovelDecision;
            if (decision.IsEligible)
            {
                eligibleCounts[recruit]++;
                rawComponentEligibleCounts[recruit, rawComponents]++;
                EligibleBatchCount++;
                if (decision.RollAttempted)
                {
                    rollAttemptedCounts[recruit]++;
                    rawComponentRollCounts[recruit, rawComponents]++;
                }

                if (telemetry.GeneratedShovel)
                {
                    generatedCounts[recruit]++;
                    rawComponentGeneratedCounts[recruit, rawComponents]++;
                    ShovelBatchCount++;
                }
            }
            else
            {
                notEligibleReasonCounts[(int)decision.NotEligibleReason]++;
            }

            RecordRange(recruitmentNumber, decision.IsEligible, telemetry.GeneratedShovel);
        }

        public string FormatReport()
        {
            var report = new StringBuilder();
            report.Append($"SampleCount={SampleCount}; ");
            report.Append($"ShovelRatePerEligibleBatch={ShovelRatePerEligibleBatch:F2}%; ");
            report.Append($"AverageShovelsAfterRecruit4={AverageShovelsAfterRecruit4:F3}; ");
            report.Append($"AverageShovelsAfterRecruit6={AverageShovelsAfterRecruit6:F3}; ");
            report.Append($"AverageShovelsAfterRecruit8={AverageShovelsAfterRecruit8:F3}; ");
            report.Append($"P0/P1/P2/P3PlusByRecruit6={P0ShovelsByRecruit6}/{P1ShovelsByRecruit6}/");
            report.Append($"{P2ShovelsByRecruit6}/{P3PlusShovelsByRecruit6}; ");
            report.Append($"GuaranteedShovels={GuaranteedShovelCount}; ");
            report.Append($"LongestEligibleNoShovelInterval={LongestEligibleNoShovelInterval}; ");
            report.Append($"NotEligibleReasons=[NoLockedCells={GetNotEligibleCount(ShovelNotEligibleReason.NoLockedCells)}, ");
            report.Append($"InvalidRecruit={GetNotEligibleCount(ShovelNotEligibleReason.InvalidRecruit)}]");

            for (var recruit = 1; recruit <= DiagnosticRecruitCount; recruit++)
            {
                report.AppendLine();
                report.Append(
                    $"R{recruit}: AvgPlannedComponents={GetAveragePlannedComponentCount(recruit):F3}; " +
                    $"AvgDeliveredComponents={GetAverageDeliveredComponentCount(recruit):F3}; " +
                    $"AvgBasicUnits={GetAverageBasicUnitCount(recruit):F3}; " +
                    $"ForgePickEligibleRate={GetForgePickEligibleRate(recruit):F2}%; " +
                    $"ForgePickRollAttemptedRate={GetForgePickRollAttemptedRate(recruit):F2}%; " +
                    $"ForgePickGeneratedRate={GetForgePickGeneratedRate(recruit):F2}%; " +
                    $"AverageForgePicks={GetAverageForgePicks(recruit):F3}; " +
                    $"AverageComponentsDelivered={GetAverageComponentsDelivered(recruit):F3}; " +
                    $"AverageComponentsRemaining={GetAverageComponentsRemaining(recruit):F3}; " +
                    $"LegacyFourComponentBlockedRate={GetLegacyFourComponentBlockedRate(recruit):F2}%; " +
                    $"Raw0/1/2/3/4={GetRawComponentCount(recruit, 0)}/" +
                    $"{GetRawComponentCount(recruit, 1)}/{GetRawComponentCount(recruit, 2)}/" +
                    $"{GetRawComponentCount(recruit, 3)}/{GetRawComponentCount(recruit, 4)}");
            }

            return report.ToString();
        }

        internal void RecordRecruitSixDistribution(int shovelCount)
        {
            switch (shovelCount)
            {
                case 0:
                    P0ShovelsByRecruit6++;
                    break;
                case 1:
                    P1ShovelsByRecruit6++;
                    break;
                case 2:
                    P2ShovelsByRecruit6++;
                    break;
                default:
                    P3PlusShovelsByRecruit6++;
                    break;
            }
        }

        private void RecordRange(int recruitmentNumber, bool eligible, bool generatedShovel)
        {
            if (!eligible)
            {
                return;
            }

            if (recruitmentNumber <= 3)
            {
                RecruitOneToThreeEligible++;
                if (generatedShovel) RecruitOneToThreeShovels++;
                return;
            }

            if (recruitmentNumber <= 7)
            {
                RecruitFourToSevenEligible++;
                if (generatedShovel) RecruitFourToSevenShovels++;
                return;
            }

            if (recruitmentNumber <= 11)
            {
                RecruitEightToElevenEligible++;
                if (generatedShovel) RecruitEightToElevenShovels++;
            }
        }

        private double Average(int total)
        {
            return SampleCount == 0 ? 0d : total / (double)SampleCount;
        }

        private static double GetRate(int count, int denominator)
        {
            return denominator == 0 ? 0d : count * 100d / denominator;
        }

        private static int ValidateRecruitment(int recruitmentNumber)
        {
            if (recruitmentNumber < 1 || recruitmentNumber > DiagnosticRecruitCount)
            {
                throw new ArgumentOutOfRangeException(nameof(recruitmentNumber));
            }

            return recruitmentNumber;
        }

        private static int ValidateComponentCount(int componentCount)
        {
            if (componentCount < 0 || componentCount > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(componentCount));
            }

            return componentCount;
        }

        private static int GetAt(int[] values, int recruitmentNumber)
        {
            return values[ValidateRecruitment(recruitmentNumber)];
        }
    }

    public static class ShovelRecruitmentDiagnostics
    {
        public static ShovelRecruitmentDistribution SampleRecruitment(
            RecruitmentCatalog catalog,
            int firstRunSeed,
            int sampleCount)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var distribution = new ShovelRecruitmentDistribution { SampleCount = sampleCount };
            for (var index = 0; index < sampleCount; index++)
            {
                var seed = unchecked(firstRunSeed + index);
                var bag = LimitedComponentBag.CreateBag(
                    seed,
                    LimitedComponentBag.DefaultContentVersion,
                    catalog);
                // Locked cells remain available so this measures formal RecruitDeck generation,
                // not a player's later decision to spend a Forge Pick.
                var state = new ShovelRecruitmentState(() => 18);
                var deck = new RecruitDeck(catalog, seed, "shovel-diagnostic", bag, shovelState: state);
                var cumulativeShovels = 0;
                for (var batchNumber = 1;
                     batchNumber <= ShovelRecruitmentDistribution.DiagnosticRecruitCount;
                     batchNumber++)
                {
                    deck.DrawNext();
                    var telemetry = deck.LastFiniteBatchTelemetry;
                    if (telemetry.GeneratedShovel)
                    {
                        cumulativeShovels++;
                    }

                    distribution.RecordBatch(
                        batchNumber,
                        telemetry,
                        bag.DrawnCount,
                        bag.RemainingCount,
                        cumulativeShovels);
                    if (batchNumber == 4)
                    {
                        distribution.ShovelsAfterRecruit4Total += cumulativeShovels;
                    }

                    if (batchNumber == 6)
                    {
                        distribution.ShovelsAfterRecruit6Total += cumulativeShovels;
                        distribution.RecordRecruitSixDistribution(cumulativeShovels);
                    }

                    if (batchNumber == 8)
                    {
                        distribution.ShovelsAfterRecruit8Total += cumulativeShovels;
                    }
                }

                distribution.GuaranteedShovelCount += state.GuaranteedShovelCount;
                if (state.LongestEligibleNoShovelInterval > distribution.LongestEligibleNoShovelInterval)
                {
                    distribution.LongestEligibleNoShovelInterval = state.LongestEligibleNoShovelInterval;
                }
            }

            return distribution;
        }
    }
}
