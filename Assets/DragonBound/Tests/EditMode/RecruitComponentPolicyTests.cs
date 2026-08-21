using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RecruitComponentPolicyTests
    {
        [Test]
        public void V2AndV3PoliciesAreExplicitDeterministicAndKeepTheFiniteBagConserved()
        {
            const int seed = 23871;
            var firstV2 = CreateDeck(seed, RecruitComponentPolicy.V2);
            var secondV2 = CreateDeck(seed, RecruitComponentPolicy.V2);
            var firstV3 = CreateDeck(seed, RecruitComponentPolicy.V3);
            var secondV3 = CreateDeck(seed, RecruitComponentPolicy.V3);

            for (var recruit = 1; recruit <= 16; recruit++)
            {
                AssertBatchesEqual(firstV2.DrawNext(), secondV2.DrawNext());
                var firstV3Batch = firstV3.DrawNext();
                AssertBatchesEqual(firstV3Batch, secondV3.DrawNext());
                Assert.LessOrEqual(Count(firstV3Batch, RecruitItemKind.HeroComponent), 3);
                Assert.GreaterOrEqual(Count(firstV3Batch, RecruitItemKind.BasicUnit), 1);
                Assert.AreEqual(5, firstV3Batch.Cards.Count);
            }

            Assert.AreEqual(RecruitComponentPolicy.V2, firstV2.ComponentPolicy);
            Assert.AreEqual(RecruitComponentPolicy.V3, firstV3.ComponentPolicy);
            Assert.AreEqual(24, firstV2.ComponentBag.DrawnCount + firstV2.ComponentBag.RemainingCount);
            Assert.AreEqual(24, firstV3.ComponentBag.DrawnCount + firstV3.ComponentBag.RemainingCount);
        }

        [Test]
        [Category("Diagnostics")]
        public void V3ThreeComponentForgePickBatchKeepsAllThreeComponentsAndOneBasic()
        {
            for (var seed = 1; seed <= 10000; seed++)
            {
                var deck = CreateDeck(seed, RecruitComponentPolicy.V3);
                for (var recruit = 1; recruit <= 13; recruit++)
                {
                    var batch = deck.DrawNext();
                    if (Count(batch, RecruitItemKind.HeroComponent) != 3 ||
                        Count(batch, RecruitItemKind.Shovel) != 1)
                    {
                        continue;
                    }

                    Assert.AreEqual(1, Count(batch, RecruitItemKind.BasicUnit));
                    Assert.IsTrue(deck.HasLastFiniteBatchTelemetry);
                    Assert.AreEqual(3, deck.LastFiniteBatchTelemetry.PlannedComponentCount);
                    Assert.AreEqual(3, deck.LastFiniteBatchTelemetry.DeliveredComponentCount);
                    Assert.IsTrue(deck.LastFiniteBatchTelemetry.GeneratedShovel);
                    return;
                }
            }

            Assert.Fail("No deterministic V3 seed produced a three-component Forge Pick batch.");
        }

        [Test]
        public void V3StateRestorePreservesPolicyAndFutureSequence()
        {
            var original = CreateDeck(92618, RecruitComponentPolicy.V3);
            for (var recruit = 1; recruit <= 5; recruit++)
            {
                original.DrawNext();
            }

            var restored = RecruitDeck.RestoreFinite(
                GreyboxRecruitmentCatalog.Create(),
                original.CaptureState(),
                () => 18);
            Assert.AreEqual(RecruitComponentPolicy.V3, restored.ComponentPolicy);

            for (var recruit = 6; recruit <= 13; recruit++)
            {
                AssertBatchesEqual(original.DrawNext(), restored.DrawNext());
            }
        }

        private static RecruitDeck CreateDeck(int seed, RecruitComponentPolicy policy)
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var bag = LimitedComponentBag.CreateBag(seed, LimitedComponentBag.DefaultContentVersion, catalog);
            return new RecruitDeck(
                catalog,
                seed,
                "policy-test",
                bag,
                shovelState: new ShovelRecruitmentState(() => 18),
                componentPolicy: policy,
                currentWaveProvider: null);
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

        private static void AssertBatchesEqual(RecruitBatch expected, RecruitBatch actual)
        {
            Assert.AreEqual(expected.RecruitmentNumber, actual.RecruitmentNumber);
            Assert.AreEqual(expected.Cards.Count, actual.Cards.Count);
            for (var index = 0; index < expected.Cards.Count; index++)
            {
                Assert.AreEqual(expected.Cards[index].RuntimeId, actual.Cards[index].RuntimeId);
                Assert.AreEqual(expected.Cards[index].Kind, actual.Cards[index].Kind);
                Assert.AreEqual(expected.Cards[index].ConfigId, actual.Cards[index].ConfigId);
                Assert.AreEqual(expected.Cards[index].SourceInstanceId, actual.Cards[index].SourceInstanceId);
            }
        }
    }
}
