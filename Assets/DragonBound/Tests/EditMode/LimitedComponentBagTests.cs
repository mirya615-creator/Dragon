using System;
using System.Linq;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class LimitedComponentBagTests
    {
        [Test]
        public void BagUsesAll18DefinitionsAndExactly24StableInstances()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var bag = CreateBag(73);

            Assert.AreEqual(18, catalog.Components.Count);
            Assert.AreEqual(24, bag.OrderedComponentInstanceIds.Count);
            Assert.AreEqual(24, bag.OrderedComponentInstanceIds.Distinct().Count());
            Assert.AreEqual(24, catalog.ComponentBagTemplate.Select(instance => instance.ComponentInstanceId).Distinct().Count());
        }

        [Test]
        public void PublicCoreComponentsHaveThreeDistinctInstancesAndOthersHaveOne()
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var bag = CreateBag(73);
            foreach (var component in catalog.Components)
            {
                var count = bag.OrderedComponentInstanceIds
                    .Select(bag.GetInstance)
                    .Count(instance => instance.ComponentDefinitionId == component.Id);
                Assert.AreEqual(component.CopiesPerRun, count, component.Id);
            }
        }

        [Test]
        public void DrawIsWithoutReplacementAndExhaustsAfter24Instances()
        {
            var bag = CreateBag(73);
            var drawn = bag.Draw(24).Select(instance => instance.ComponentInstanceId).ToArray();

            Assert.AreEqual(24, drawn.Distinct().Count());
            Assert.AreEqual(0, bag.RemainingCount);
            Assert.IsTrue(bag.IsExhausted);
            Assert.Throws<InvalidOperationException>(() => bag.DrawOne());
        }

        [Test]
        public void RuntimeCountsExposeInitialRemainingDrawnAndDiscardedInstances()
        {
            var bag = CreateBag(73);
            var first = bag.DrawOne();

            Assert.AreEqual(24, bag.InitialCount);
            Assert.AreEqual(1, bag.DrawnCount);
            Assert.AreEqual(0, bag.DiscardedCount);
            Assert.AreEqual(
                bag.GetInitialCount(first.ComponentId) - 1,
                bag.GetRemainingCount(first.ComponentId));

            Assert.IsTrue(bag.MarkDiscarded(first.ComponentInstanceId));
            Assert.AreEqual(1, bag.DiscardedCount);
            Assert.AreEqual(23, bag.RemainingCount);
        }

        [Test]
        public void PeekDoesNotAdvanceCursor()
        {
            var bag = CreateBag(73);
            var expected = bag.OrderedComponentInstanceIds[0];
            Assert.AreEqual(expected, bag.Peek(1)[0].ComponentInstanceId);
            Assert.AreEqual(24, bag.RemainingCount);
            Assert.AreEqual(expected, bag.DrawOne().ComponentInstanceId);
        }

        [Test]
        public void SameRunSeedContentAndAlgorithmReproduceOrderExactly()
        {
            var first = CreateBag(2081);
            var second = CreateBag(2081);
            CollectionAssert.AreEqual(first.OrderedComponentInstanceIds, second.OrderedComponentInstanceIds);
            Assert.AreEqual(first.RngVersion, second.RngVersion);
        }

        [Test]
        public void DifferentRunSeedsNormallyProduceDifferentOrder()
        {
            var first = CreateBag(2081);
            var second = CreateBag(2082);
            Assert.IsFalse(first.OrderedComponentInstanceIds.SequenceEqual(second.OrderedComponentInstanceIds));
        }

        [Test]
        public void OtherRandomStreamsDoNotChangeComponentBagOrder()
        {
            var first = CreateBag(2081);
            var unrelated = new RunRandom(2081);
            for (var index = 0; index < 100; index++)
            {
                unrelated.NextInt("enemy.spawn", 0, 1000);
            }

            var second = CreateBag(2081);
            CollectionAssert.AreEqual(first.OrderedComponentInstanceIds, second.OrderedComponentInstanceIds);
        }

        [Test]
        public void DiscardedDrawnComponentNeverReturnsToBag()
        {
            var bag = CreateBag(73);
            var discarded = bag.DrawOne();
            Assert.IsTrue(bag.MarkDiscarded(discarded.ComponentInstanceId));
            Assert.IsTrue(bag.WasDiscarded(discarded.ComponentInstanceId));
            Assert.IsFalse(bag.MarkDiscarded(discarded.ComponentInstanceId));
            Assert.AreEqual(23, bag.RemainingCount);
            Assert.IsFalse(bag.Peek(23).Any(instance => instance.ComponentInstanceId == discarded.ComponentInstanceId));
        }

        [Test]
        public void UndrawnComponentCannotBeDiscardedOrReturned()
        {
            var bag = CreateBag(73);
            var next = bag.Peek(1)[0];
            Assert.IsFalse(bag.MarkDiscarded(next.ComponentInstanceId));
            Assert.AreEqual(24, bag.RemainingCount);
        }

        [Test]
        public void SerializedStateRestoresCursorAndFutureOrder()
        {
            var source = CreateBag(20260806);
            source.Draw(7);
            var discarded = source.DrawOne();
            source.MarkDiscarded(discarded.ComponentInstanceId);
            var state = source.CaptureState();
            var json = JsonUtility.ToJson(state);
            var restored = LimitedComponentBag.Restore(
                GreyboxRecruitmentCatalog.Create(),
                JsonUtility.FromJson<LimitedComponentBagState>(json));

            Assert.AreEqual(source.CurrentCursor, restored.CurrentCursor);
            Assert.AreEqual(source.RemainingCount, restored.RemainingCount);
            CollectionAssert.AreEqual(
                source.Draw(8).Select(instance => instance.ComponentInstanceId),
                restored.Draw(8).Select(instance => instance.ComponentInstanceId));
        }

        [Test]
        public void RestoreRejectsUnknownAlgorithmVersion()
        {
            var state = CreateBag(73).CaptureState();
            state.RngVersion = "RecruitComponentBag.future";
            Assert.Throws<InvalidOperationException>(() => LimitedComponentBag.Restore(
                GreyboxRecruitmentCatalog.Create(),
                state));
        }

        private static LimitedComponentBag CreateBag(int seed)
        {
            return LimitedComponentBag.CreateBag(
                seed,
                LimitedComponentBag.DefaultContentVersion,
                GreyboxRecruitmentCatalog.Create());
        }
    }
}
