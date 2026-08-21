using System;
using System.Collections.Generic;
using System.Text;

namespace DragonBound.Recruitment
{
    /// <summary>
    /// Development-only sampling utility for checking when the finite component bag is exhausted.
    /// It has no UI dependency and does not modify a live run.
    /// </summary>
    public sealed class FiniteComponentCompletionDistribution
    {
        public int SampleCount { get; internal set; }
        public int Batch8 { get; internal set; }
        public int Batch9 { get; internal set; }
        public int Batch10 { get; internal set; }
        public int Batch11 { get; internal set; }
        public int LateOrIncomplete { get; internal set; }

        public int CompletedCount => Batch8 + Batch9 + Batch10 + Batch11;

        public double Batch8Percentage => GetPercentage(Batch8);
        public double Batch9Percentage => GetPercentage(Batch9);
        public double Batch10Percentage => GetPercentage(Batch10);
        public double Batch11Percentage => GetPercentage(Batch11);
        public double LateOrIncompletePercentage => GetPercentage(LateOrIncomplete);

        public string FormatReport()
        {
            return $"SampleCount={SampleCount}; " +
                   $"Batch8={Batch8} ({Batch8Percentage:F2}%); " +
                   $"Batch9={Batch9} ({Batch9Percentage:F2}%); " +
                   $"Batch10={Batch10} ({Batch10Percentage:F2}%); " +
                   $"Batch11={Batch11} ({Batch11Percentage:F2}%); " +
                   $"LateOrIncomplete={LateOrIncomplete} ({LateOrIncompletePercentage:F2}%); " +
                   $"CompletedCount={CompletedCount}";
        }

        private double GetPercentage(int count)
        {
            return SampleCount == 0 ? 0d : count * 100d / SampleCount;
        }
    }

    public sealed class DynamicFiniteRecruitmentDistribution
    {
        internal DynamicFiniteRecruitmentDistribution(int sampleCount)
        {
            SampleCount = sampleCount;
            ComponentCountByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            BasicUnitCountByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            PureBasicByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            OneComponentByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            MultiComponentByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            ShovelByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            MultiComponentCountTwoByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            MultiComponentCountThreeByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            MultiComponentCountFourByRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            BagEmptyAtRecruit = new int[FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount + 1];
            RemainingComponentsAfterRecruit11 = new SortedDictionary<int, int>();
        }

        public int SampleCount { get; }
        public int[] ComponentCountByRecruit { get; }
        public int[] BasicUnitCountByRecruit { get; }
        public int[] PureBasicByRecruit { get; }
        public int[] OneComponentByRecruit { get; }
        public int[] MultiComponentByRecruit { get; }
        public int[] ShovelByRecruit { get; }
        public int[] MultiComponentCountTwoByRecruit { get; }
        public int[] MultiComponentCountThreeByRecruit { get; }
        public int[] MultiComponentCountFourByRecruit { get; }
        public int[] BagEmptyAtRecruit { get; }
        public SortedDictionary<int, int> RemainingComponentsAfterRecruit11 { get; }
        public int BagEmptyByRecruit11 { get; internal set; }
        public int TotalBasicUnits { get; internal set; }
        public int TotalShovels { get; internal set; }

        public double BagEmptyByRecruit11Rate => SampleCount == 0 ? 0d : BagEmptyByRecruit11 * 100d / SampleCount;
        public double AverageBasicUnitsPerBatch => SampleCount == 0
            ? 0d
            : TotalBasicUnits / (double)(SampleCount * FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount);
        public double AverageShovelsPerRun => SampleCount == 0 ? 0d : TotalShovels / (double)SampleCount;

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"SampleCount={SampleCount}");
            builder.AppendLine($"BagEmptyByRecruit11={BagEmptyByRecruit11} ({BagEmptyByRecruit11Rate:F2}%)");
            builder.AppendLine($"AverageBasicUnitsPerBatch={AverageBasicUnitsPerBatch:F3}");
            builder.AppendLine($"AverageShovelsPerRun={AverageShovelsPerRun:F3}");
            builder.AppendLine("Recruit | AvgComponents | AvgBasicUnits | CumulativeAvg | PureBasic% | OneComponent% | MultiComponent% | Shovel% | Multi2% | Multi3% | Multi4%");

            var cumulative = 0;
            for (var recruit = 1; recruit <= FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount; recruit++)
            {
                cumulative += ComponentCountByRecruit[recruit];
                var multiCount = MultiComponentByRecruit[recruit];
                builder.AppendLine(
                    $"{recruit} | " +
                    $"{ComponentCountByRecruit[recruit] / (double)SampleCount:F3} | " +
                    $"{BasicUnitCountByRecruit[recruit] / (double)SampleCount:F3} | " +
                    $"{cumulative / (double)SampleCount:F3} | " +
                    $"{GetRate(PureBasicByRecruit[recruit], SampleCount):F2}% | " +
                    $"{GetRate(OneComponentByRecruit[recruit], SampleCount):F2}% | " +
                    $"{GetRate(MultiComponentByRecruit[recruit], SampleCount):F2}% | " +
                    $"{GetRate(ShovelByRecruit[recruit], SampleCount):F2}% | " +
                    $"{GetRate(MultiComponentCountTwoByRecruit[recruit], multiCount):F2}% | " +
                    $"{GetRate(MultiComponentCountThreeByRecruit[recruit], multiCount):F2}% | " +
                    $"{GetRate(MultiComponentCountFourByRecruit[recruit], multiCount):F2}%");
            }

            builder.Append("BagEmptyAtRecruit=");
            for (var recruit = 1; recruit <= FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount; recruit++)
            {
                if (recruit > 1)
                {
                    builder.Append(", ");
                }

                builder.Append(recruit).Append(":").Append(BagEmptyAtRecruit[recruit]);
            }

            builder.AppendLine();
            builder.Append("RemainingComponentsAfterRecruit11=");
            var first = true;
            foreach (var pair in RemainingComponentsAfterRecruit11)
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                first = false;
                builder.Append(pair.Key).Append(":").Append(pair.Value);
            }

            return builder.ToString();
        }

        private static double GetRate(int count, int denominator)
        {
            return denominator == 0 ? 0d : count * 100d / denominator;
        }
    }

    public static class FiniteComponentRecruitmentDiagnostics
    {
        public static FiniteComponentCompletionDistribution SampleCompletionBatches(
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

            var distribution = new FiniteComponentCompletionDistribution
            {
                SampleCount = sampleCount
            };
            for (var index = 0; index < sampleCount; index++)
            {
                var seed = unchecked(firstRunSeed + index);
                var bag = LimitedComponentBag.CreateBag(
                    seed,
                    LimitedComponentBag.DefaultContentVersion,
                    catalog);
                var deck = new RecruitDeck(catalog, seed, "diagnostic", bag);
                var completionBatch = 0;
                for (var batch = 1; batch <= FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount; batch++)
                {
                    deck.DrawNext();
                    if (bag.IsExhausted)
                    {
                        completionBatch = batch;
                        break;
                    }
                }

                if (completionBatch > 0 && completionBatch <= 8)
                {
                    distribution.Batch8++;
                    continue;
                }

                switch (completionBatch)
                {
                    case 9:
                        distribution.Batch9++;
                        break;
                    case 10:
                        distribution.Batch10++;
                        break;
                    case 11:
                        distribution.Batch11++;
                        break;
                    default:
                        distribution.LateOrIncomplete++;
                        break;
                }
            }

            return distribution;
        }

        public static DynamicFiniteRecruitmentDistribution SampleDynamicCatchup(
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

            var distribution = new DynamicFiniteRecruitmentDistribution(sampleCount);
            for (var index = 0; index < sampleCount; index++)
            {
                var seed = unchecked(firstRunSeed + index);
                var bag = LimitedComponentBag.CreateBag(
                    seed,
                    LimitedComponentBag.DefaultContentVersion,
                    catalog);
                var deck = new RecruitDeck(catalog, seed, "diagnostic", bag);
                var emptyAtRecruit = 0;
                for (var recruit = 1; recruit <= FiniteComponentRecruitmentConfig.TargetCompletionRecruitCount; recruit++)
                {
                    var batch = deck.DrawNext();
                    var componentCount = Count(batch, RecruitItemKind.HeroComponent);
                    var shovelCount = Count(batch, RecruitItemKind.Shovel);
                    var basicCount = Count(batch, RecruitItemKind.BasicUnit);
                    distribution.ComponentCountByRecruit[recruit] += componentCount;
                    distribution.BasicUnitCountByRecruit[recruit] += basicCount;
                    distribution.TotalBasicUnits += basicCount;
                    distribution.TotalShovels += shovelCount;

                    if (shovelCount > 0)
                    {
                        distribution.ShovelByRecruit[recruit]++;
                    }
                    else if (componentCount == 0)
                    {
                        distribution.PureBasicByRecruit[recruit]++;
                    }
                    else if (componentCount == 1)
                    {
                        distribution.OneComponentByRecruit[recruit]++;
                    }
                    else
                    {
                        distribution.MultiComponentByRecruit[recruit]++;
                        if (componentCount == 2)
                        {
                            distribution.MultiComponentCountTwoByRecruit[recruit]++;
                        }
                        else if (componentCount == 3)
                        {
                            distribution.MultiComponentCountThreeByRecruit[recruit]++;
                        }
                        else if (componentCount == 4)
                        {
                            distribution.MultiComponentCountFourByRecruit[recruit]++;
                        }
                    }

                    if (emptyAtRecruit == 0 && bag.IsExhausted)
                    {
                        emptyAtRecruit = recruit;
                        distribution.BagEmptyAtRecruit[recruit]++;
                    }
                }

                if (emptyAtRecruit > 0)
                {
                    distribution.BagEmptyByRecruit11++;
                }

                Increment(distribution.RemainingComponentsAfterRecruit11, bag.RemainingCount);
            }

            return distribution;
        }

        private static int Count(RecruitBatch batch, RecruitItemKind kind)
        {
            var count = 0;
            foreach (var card in batch.Cards)
            {
                if (card.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Increment(IDictionary<int, int> values, int key)
        {
            values.TryGetValue(key, out var count);
            values[key] = count + 1;
        }
    }
}
