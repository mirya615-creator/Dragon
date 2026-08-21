using System;
using NUnit.Framework;
using DragonBound.Foundation.Contracts;
using DragonBound.Runes.Contracts;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneContractsTests
    {
        [Test]
        public void StableIdsUseOrdinalValueEquality()
        {
            var first = new RuneId("Power");
            var second = new RuneId("Power");

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
            Assert.AreNotEqual(first, new RuneId("power"));
        }

        [Test]
        public void ProfileSnapshotCopiesCollectionsAndCarriesTypedState()
        {
            var inventory = new[]
            {
                new RuneInventoryEntrySnapshot(new RuneId("Power"), RuneRarity.Excellent, 1, 0)
            };
            var assignments = new[]
            {
                new LoadoutAssignment("HERO_TEST", new RuneId("Power"))
            };
            var profile = new RuneProfileSnapshot(
                RuneContractStatus.Ready,
                new AccountDay(3),
                "RuneContent.V1",
                inventory,
                assignments);

            Assert.IsTrue(profile.IsReady);
            Assert.AreEqual(1, profile.Inventory.Count);
            Assert.AreEqual("HERO_TEST", profile.LoadoutAssignments[0].HeroId);
            Assert.AreEqual("RuneContent.V1", profile.ContentVersion);
            Assert.AreEqual(RuneContractStatus.Pending, RuneProfileSnapshot.Pending.Status);
            Assert.AreEqual(RuneContractStatus.NotConfigured, RuneProfileSnapshot.NotConfigured.Status);
        }

        [Test]
        public void RunSnapshotIsolatedFromProfileAssignments()
        {
            var source = new[] { new LoadoutAssignment("HERO_TEST", new RuneId("Power")) };
            var snapshot = new RunSnapshot(
                RuneContractStatus.Ready,
                new RunId(42),
                new AccountDay(3),
                new DayKey("server-supplied-day"),
                source);

            source[0] = new LoadoutAssignment("HERO_TEST", new RuneId("Might"));

            Assert.IsTrue(snapshot.IsReady);
            Assert.AreEqual(42, snapshot.RunId.Seed);
            Assert.AreEqual("Power", snapshot.LoadoutAssignments[0].RuneId.Value);
            Assert.AreEqual("server-supplied-day", snapshot.DayKey.Value);
        }

        [Test]
        public void PendingAndNotConfiguredStatesRemainTyped()
        {
            Assert.AreEqual(FeatureGateState.Pending, FeatureGateResult.Pending.State);
            Assert.AreEqual(FeatureGateState.NotConfigured, FeatureGateResult.NotConfigured.State);
            Assert.AreEqual(RewardGrantState.Pending, RewardGrant.Pending.State);
            Assert.AreEqual(RewardGrantState.NotConfigured, RewardGrant.NotConfigured.State);
        }

        [Test]
        public void ProvidersAreReplaceablePortsWithNoConcreteImplementation()
        {
            IRuneProfileProvider profileProvider = new FixedProfileProvider();
            IRuneSnapshotProvider snapshotProvider = new FixedSnapshotProvider();
            IRuneRewardProvider rewardProvider = new FixedRewardProvider();

            Assert.AreEqual(RuneContractStatus.NotConfigured, profileProvider.GetProfile().Status);
            Assert.AreEqual(RuneContractStatus.Pending, snapshotProvider.CreateSnapshot(new RunId(1), RuneProfileSnapshot.NotConfigured).Status);
            Assert.AreEqual(RewardGrantState.NotConfigured, rewardProvider.RequestReward(new RuneRewardRequest(new RunId(1), new WaveNumber(3))).State);
        }

        [Test]
        public void ValueObjectsRejectMissingIdentity()
        {
            Assert.Throws<ArgumentException>(() => new RuneId(""));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AccountDay(0));
            Assert.Throws<ArgumentException>(() => new DayKey(""));
            Assert.Throws<ArgumentException>(() => new LoadoutAssignment("", new RuneId("Power")));
        }

        private sealed class FixedProfileProvider : IRuneProfileProvider
        {
            public RuneProfileSnapshot GetProfile() => RuneProfileSnapshot.NotConfigured;
        }

        private sealed class FixedSnapshotProvider : IRuneSnapshotProvider
        {
            public RunSnapshot CreateSnapshot(RunId runId, RuneProfileSnapshot profile) => RunSnapshot.Pending;
        }

        private sealed class FixedRewardProvider : IRuneRewardProvider
        {
            public RewardGrant RequestReward(RuneRewardRequest request) => RewardGrant.NotConfigured;
        }
    }
}
