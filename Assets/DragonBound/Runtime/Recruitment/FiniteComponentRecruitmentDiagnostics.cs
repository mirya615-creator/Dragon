using System;

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
                for (var batch = 1; batch <= FiniteComponentRecruitmentConfig.GuaranteedCompletionBatch; batch++)
                {
                    deck.DrawNext();
                    if (bag.IsExhausted)
                    {
                        completionBatch = batch;
                        break;
                    }
                }

                switch (completionBatch)
                {
                    case 8:
                        distribution.Batch8++;
                        break;
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
    }
}
