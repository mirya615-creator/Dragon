using DragonBound.Analytics;
using DragonBound.Recruitment;
using DragonBound.Runes;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneAnalyticsGameplayIntegrationTests
    {
        [Test]
        public void LoadoutBridgeRecordsTypedAssignUnequipCraftAndGateResults()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var bridge = new RuneLoadoutAnalyticsBridge(CreateAdapter(sink));

            bridge.RecordAssign("HERO_TEST", "Power", true, string.Empty);
            bridge.RecordUnequip("HERO_TEST", false, "HeroHasNoEquippedRune");
            bridge.RecordCraft("Power", false, "InsufficientFragments");
            bridge.RecordGate("loadout_assign", 2, "RuneSystemLockedUntilDay3");

            Assert.AreEqual(4, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneLoadoutAssign, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneLoadoutUnequip, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneCraft, sink.Events[2].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGateRejection, sink.Events[3].event_name);
            Assert.AreEqual("RuneSystemLockedUntilDay3", sink.Events[3].reason);
        }

        [Test]
        public void LoadoutServiceRecordsAssignUnequipAndCraftSuccesses()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var profile = new RuneSaveData { AccountDay = 3 };
            profile.EnsureRuntimeState(out _);
            profile.Inventory.AddComplete("Power");
            profile.Inventory.AddFragment("Ricochet", RuneInventory.EpicFragmentsPerRune);
            var service = new RuneLoadoutService(
                profile,
                new RuneFeatureGate(new FixedProgression(3)),
                null,
                CreateAdapter(sink));

            Assert.IsTrue(service.TryEquip(HeroDefinitionCatalog.Definitions[0].Id, "Power", out var equipError), equipError);
            Assert.IsTrue(service.TryUnequip(HeroDefinitionCatalog.Definitions[0].Id, out var unequipError), unequipError);
            Assert.IsTrue(service.TryCraft("Ricochet", out var craftError), craftError);

            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneLoadoutAssign, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneLoadoutUnequip, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneCraft, sink.Events[2].event_name);
            Assert.AreEqual(AnalyticsRuneOperationResults.Accepted, sink.Events[0].operation_result);
            Assert.AreEqual(AnalyticsRuneOperationResults.Accepted, sink.Events[1].operation_result);
            Assert.AreEqual(AnalyticsRuneOperationResults.Accepted, sink.Events[2].operation_result);
        }

        [Test]
        public void LoadoutServiceRecordsFailuresAndDay3GateRejectionsAtTypedReturns()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var profile = new RuneSaveData { AccountDay = 2 };
            profile.EnsureRuntimeState(out _);
            profile.Inventory.AddComplete("Power");
            var service = new RuneLoadoutService(
                profile,
                new RuneFeatureGate(new FixedProgression(2)),
                null,
                CreateAdapter(sink));

            Assert.IsFalse(service.TryEquip(HeroDefinitionCatalog.Definitions[0].Id, "Power", out var equipError));
            Assert.AreEqual("RuneSystemLockedUntilDay3", equipError);
            Assert.IsFalse(service.TryUnequip(HeroDefinitionCatalog.Definitions[0].Id, out var unequipError));
            Assert.AreEqual("RuneSystemLockedUntilDay3", unequipError);
            Assert.IsFalse(service.TryCraft("Ricochet", out var craftError));
            Assert.AreEqual("RuneSystemLockedUntilDay3", craftError);

            Assert.AreEqual(6, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGateRejection, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGateRejection, sink.Events[3].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGateRejection, sink.Events[5].event_name);
            Assert.AreEqual(2, sink.Events[1].account_day);
            Assert.AreEqual("RuneSystemLockedUntilDay3", sink.Events[5].reason);
        }

        [Test]
        public void LoadoutServiceRecordsOrdinaryTypedFailuresWithoutChangingState()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var profile = new RuneSaveData { AccountDay = 3 };
            profile.EnsureRuntimeState(out _);
            var service = new RuneLoadoutService(
                profile,
                new RuneFeatureGate(new FixedProgression(3)),
                null,
                CreateAdapter(sink));

            Assert.IsFalse(service.TryUnequip(HeroDefinitionCatalog.Definitions[0].Id, out var unequipError));
            Assert.AreEqual("HeroHasNoEquippedRune", unequipError);
            Assert.IsFalse(service.TryCraft("Ricochet", out var craftError));
            Assert.AreEqual("InsufficientFragments", craftError);
            Assert.IsFalse(service.TryEquip("UNKNOWN_HERO", "Power", out var equipError));
            Assert.AreEqual("UnknownHeroId", equipError);

            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual(AnalyticsRuneOperationResults.Rejected, sink.Events[0].operation_result);
            Assert.AreEqual("HeroHasNoEquippedRune", sink.Events[0].reason);
            Assert.AreEqual("InsufficientFragments", sink.Events[1].reason);
            Assert.AreEqual("UnknownHeroId", sink.Events[2].reason);
            Assert.AreEqual(0, profile.Loadout.Assignments.Count);
        }

        [Test]
        public void RewardGateEmitsPendingGateAndRejectedAtTypedBoundary()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var profile = new RuneSaveData();
            profile.EnsureRuntimeState(out _);
            var service = new RuneRunRewardService(
                7401,
                profile.Inventory,
                new RuneFeatureGate(new FixedProgression(2)),
                null,
                adapter);

            var result = service.CompleteWaveResult(3);

            Assert.IsFalse(result.Granted);
            Assert.AreEqual("RuneSystemLockedUntilDay3", result.Reason);
            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneRewardPending, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGateRejection, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneRewardRejected, sink.Events[2].event_name);
        }

        [Test]
        public void RewardSuccessEmitsPendingAndGrantedWithoutChangingLegacyReturn()
        {
            var seed = FindRewardSeed();
            var sink = new InMemoryAnalyticsSinkV2();
            var profile = new RuneSaveData();
            profile.EnsureRuntimeState(out _);
            var service = new RuneRunRewardService(
                seed,
                profile.Inventory,
                new RuneFeatureGate(new FixedProgression(3)),
                null,
                CreateAdapter(sink));

            var result = service.CompleteWaveResult(3);

            Assert.IsTrue(result.Granted, result.Reason);
            Assert.IsNotNull(result.Reward);
            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneRewardPending, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneRewardGranted, sink.Events[1].event_name);
            Assert.AreEqual(result.Reward.RuneId, sink.Events[1].rune_id);
        }

        private static int FindRewardSeed()
        {
            for (var seed = 1; seed < 10000; seed++)
            {
                var state = new RuneDropState();
                if (RuneDropRules.TryRollCompletedWave(seed, 3, state) != null)
                {
                    return seed;
                }
            }

            Assert.Fail("Expected a deterministic reward seed in the bounded test range.");
            return 1;
        }

        private static RuneAnalyticsAdapterV2 CreateAdapter(InMemoryAnalyticsSinkV2 sink)
        {
            return new RuneAnalyticsAdapterV2(
                new AnalyticsRecorderV2(sink),
                "run-rune-gameplay",
                7401,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                AnalyticsSides.Player,
                AnalyticsRankTiers.Unranked,
                AnalyticsAiDifficulties.Standard,
                "config.v2",
                "build.v2");
        }

        private sealed class FixedProgression : IRuneProgressionProvider
        {
            public FixedProgression(int accountDay)
            {
                AccountDay = accountDay;
            }

            public int AccountDay { get; }
        }
    }
}
