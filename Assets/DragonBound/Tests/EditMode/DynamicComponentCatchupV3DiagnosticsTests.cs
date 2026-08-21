using System.Collections.Generic;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class DynamicComponentCatchupV3DiagnosticsTests
    {
        [Test]
        public void CandidateV3DefinesTheSpecifiedTiersAndNeverErasesPureBasic()
        {
            Assert.AreEqual(3, DynamicComponentCatchupV3Config.MaxComponentsPerRecruit);
            Assert.AreEqual(
                DynamicComponentCatchupV3Tier.Normal,
                DynamicComponentCatchupV3Config.GetTier(3, 13, 0));
            Assert.AreEqual(
                DynamicComponentCatchupV3Tier.Normal,
                DynamicComponentCatchupV3Config.GetTier(4, 4, 4));
            Assert.AreEqual(
                DynamicComponentCatchupV3Tier.Light,
                DynamicComponentCatchupV3Config.GetTier(4, 4, 3));
            Assert.AreEqual(
                DynamicComponentCatchupV3Tier.Medium,
                DynamicComponentCatchupV3Config.GetTier(4, 4, 2));
            Assert.AreEqual(
                DynamicComponentCatchupV3Tier.Strong,
                DynamicComponentCatchupV3Config.GetTier(4, 8, 10));
            Assert.AreEqual(
                DynamicComponentCatchupV3Tier.Severe,
                DynamicComponentCatchupV3Config.GetTier(4, 8, 8));

            foreach (DynamicComponentCatchupV3Tier tier in System.Enum.GetValues(typeof(DynamicComponentCatchupV3Tier)))
            {
                Assert.GreaterOrEqual(DynamicComponentCatchupV3Config.GetPureBasicWeight(tier), 0.10f);
            }
        }

        [Test]
        public void CandidateV3NeverDrawsFourComponentsAndConservesTheFormalBag()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            for (var seed = 1; seed <= 100; seed++)
            {
                var deck = new DynamicComponentCatchupV3Deck(catalog, seed);
                for (var recruit = 1; recruit <= 13; recruit++)
                {
                    var batch = deck.DrawNext(recruit);
                    Assert.LessOrEqual(batch.PlannedComponentCount, 3);
                    Assert.LessOrEqual(batch.DeliveredComponentCount, 3);
                    Assert.GreaterOrEqual(batch.BasicUnitCount, 1);
                    Assert.AreEqual(5, batch.ResultCount);
                    Assert.AreEqual(24, deck.ComponentBag.DrawnCount + deck.ComponentBag.RemainingCount);
                }
            }
        }

        [Test]
        [Category("Diagnostics")]
        public void CandidateV3ForgePickDoesNotReduceAThreeComponentBatch()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            for (var seed = 1; seed <= 10000; seed++)
            {
                var deck = new DynamicComponentCatchupV3Deck(catalog, seed);
                for (var recruit = 1; recruit <= 13; recruit++)
                {
                    var batch = deck.DrawNext(recruit);
                    if (!batch.ForgePickGenerated || batch.PlannedComponentCount != 3 ||
                        batch.RemainingComponentCount > 21)
                    {
                        continue;
                    }

                    Assert.AreEqual(3, batch.DeliveredComponentCount);
                    Assert.AreEqual(1, batch.BasicUnitCount);
                    Assert.AreEqual(5, batch.ResultCount);
                    return;
                }
            }

            Assert.Fail("No deterministic V3 sample contained a three-component Forge Pick batch.");
        }

        [Test]
        public void CandidateV3NoLockedCellsDoesNotRollOrAdvanceForgePickPity()
        {
            var deck = new DynamicComponentCatchupV3Deck(GreyboxRecruitmentCatalog.Create(), 171, 0);
            for (var recruit = 1; recruit <= 13; recruit++)
            {
                var batch = deck.DrawNext(recruit);
                Assert.IsFalse(batch.ForgePickDecision.IsEligible);
                Assert.IsFalse(batch.ForgePickDecision.RollAttempted);
                Assert.IsFalse(batch.ForgePickGenerated);
            }

            Assert.AreEqual(0, deck.ShovelState.ConsecutiveEligibleBatchesWithoutShovel);
        }

        [Test]
        public void CandidateV3IsDeterministicAndCannotMutateBaselineV2()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var first = new DynamicComponentCatchupV3Deck(catalog, 818);
            var second = new DynamicComponentCatchupV3Deck(catalog, 818);
            var beforeCandidate = CreateBaseline(catalog, 818);
            var baselineFirst = beforeCandidate.DrawNext();

            for (var recruit = 1; recruit <= 13; recruit++)
            {
                var firstBatch = first.DrawNext(recruit);
                var secondBatch = second.DrawNext(recruit);
                Assert.AreEqual(firstBatch.PlannedComponentCount, secondBatch.PlannedComponentCount);
                Assert.AreEqual(firstBatch.DeliveredComponentCount, secondBatch.DeliveredComponentCount);
                Assert.AreEqual(firstBatch.ForgePickGenerated, secondBatch.ForgePickGenerated);
                Assert.AreEqual(firstBatch.RemainingComponentCount, secondBatch.RemainingComponentCount);
            }

            var afterCandidate = CreateBaseline(catalog, 818);
            AssertBatchesEqual(baselineFirst, afterCandidate.DrawNext());
        }

        [Test]
        [Category("Diagnostics")]
        public void CandidateV3RunsPairedOneHundredThousandSeedAudit()
        {
            var report = DynamicComponentCatchupV3Diagnostics.SamplePaired(
                GreyboxRecruitmentCatalog.Create(),
                1,
                100000);
            TestContext.WriteLine(report.FormatReport());

            Assert.AreEqual(100000, report.SampleCount);
            Assert.AreEqual(0, report.Candidate.ConservationFailures);
            Assert.AreEqual(0, report.Baseline.ConservationFailures);
            for (var recruit = 1; recruit <= 13; recruit++)
            {
                Assert.AreEqual(0, report.Candidate.ComponentCounts[recruit, 4]);
                Assert.GreaterOrEqual(report.Candidate.ComponentCounts[recruit, 0], 0);
            }

            Assert.Greater(report.Baseline.ComponentCounts[4, 4], 0);
            Assert.AreEqual(100d, report.Candidate.ForgePickEligibleRate(6), 0.0001d);
        }

        private static RecruitDeck CreateBaseline(RecruitmentCatalog catalog, int seed)
        {
            var bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
            return new RecruitDeck(
                catalog,
                seed,
                "dynamic-v3-ab-baseline",
                bag,
                shovelState: new ShovelRecruitmentState(() => 18));
        }

        private static void AssertBatchesEqual(RecruitBatch first, RecruitBatch second)
        {
            Assert.AreEqual(first.RecruitmentNumber, second.RecruitmentNumber);
            Assert.AreEqual(first.Cards.Count, second.Cards.Count);
            for (var index = 0; index < first.Cards.Count; index++)
            {
                Assert.AreEqual(first.Cards[index].Kind, second.Cards[index].Kind);
                Assert.AreEqual(first.Cards[index].ConfigId, second.Cards[index].ConfigId);
                Assert.AreEqual(first.Cards[index].SourceInstanceId, second.Cards[index].SourceInstanceId);
            }
        }
    }
}
