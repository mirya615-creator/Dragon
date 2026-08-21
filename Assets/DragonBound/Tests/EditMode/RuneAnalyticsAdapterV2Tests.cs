using DragonBound.Analytics;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneAnalyticsAdapterV2Tests
    {
        [Test]
        public void V2RegistryIncludesRuneLifecycleEvents()
        {
            Assert.AreEqual(45, AnalyticsEventNamesV2.All.Length);
            Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(AnalyticsEventNamesV2.RuneLoadoutAssign));
            Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(AnalyticsEventNamesV2.RuneLoadoutUnequip));
            Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(AnalyticsEventNamesV2.RuneCraft));
            Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(AnalyticsEventNamesV2.RuneGateRejection));
            Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(AnalyticsEventNamesV2.RuneRewardPending));
            Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(AnalyticsEventNamesV2.RuneRewardGranted));
            Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(AnalyticsEventNamesV2.RuneRewardRejected));
        }

        [Test]
        public void LoadoutAssignUnequipAndCraftUseTypedOperationResults()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            string error;

            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                adapter.RecordLoadoutAssign(
                    new RuneLoadoutOperationObservationV2("assign-1", 0, "hero-1", "Power", true, string.Empty),
                    out error),
                error);
            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                adapter.RecordLoadoutUnequip(
                    new RuneLoadoutOperationObservationV2("unequip-1", 0, "hero-1", string.Empty, true, string.Empty),
                    out error),
                error);
            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                adapter.RecordCraft(
                    new RuneLoadoutOperationObservationV2("craft-1", 1, string.Empty, "Ricochet", false, "InsufficientFragments"),
                    out error),
                error);

            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneLoadoutAssign, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsRuneOperationResults.Accepted, sink.Events[0].operation_result);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneLoadoutUnequip, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneCraft, sink.Events[2].event_name);
            Assert.AreEqual(AnalyticsRuneOperationResults.Rejected, sink.Events[2].operation_result);
            Assert.AreEqual("InsufficientFragments", sink.Events[2].reason);
            Assert.AreEqual(1, sink.Events[0].sequence);
            Assert.AreEqual(3, sink.Events[2].sequence);
        }

        [Test]
        public void Day3GateRejectionRecordsLockedAndAccountDay()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            string error;

            var result = adapter.RecordGateRejection(
                new RuneGateRejectionObservationV2(
                    "gate-equip-day2",
                    0,
                    "loadout_assign",
                    2,
                    "RuneSystemLockedUntilDay3"),
                out error);

            Assert.AreEqual(AnalyticsRecordResultV2.Accepted, result, error);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGateRejection, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsRuneGateStates.Locked, sink.Events[0].gate_state);
            Assert.AreEqual(2, sink.Events[0].account_day);
            Assert.AreEqual("RuneSystemLockedUntilDay3", sink.Events[0].reason);
        }

        [Test]
        public void RewardLifecycleRecordsPendingGrantedAndRejectedSeparately()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            string error;

            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                adapter.RecordRewardPending(new RuneRewardPendingObservationV2("reward-3-pending", 3), out error),
                error);
            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                adapter.RecordRewardResult(
                    new RuneRewardResultObservationV2("reward-3-granted", 3, "Power", "complete", true, string.Empty),
                    out error),
                error);
            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                adapter.RecordRewardResult(
                    new RuneRewardResultObservationV2("reward-4-rejected", 4, string.Empty, string.Empty, false, "RuneSystemLockedUntilDay3"),
                    out error),
                error);

            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneRewardPending, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsRuneRewardStates.Pending, sink.Events[0].reward_state);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneRewardGranted, sink.Events[1].event_name);
            Assert.AreEqual("Power", sink.Events[1].rune_id);
            Assert.AreEqual(AnalyticsRuneRewardStates.Granted, sink.Events[1].reward_state);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneRewardRejected, sink.Events[2].event_name);
            Assert.AreEqual(AnalyticsRuneRewardStates.Rejected, sink.Events[2].reward_state);
            Assert.AreEqual("RuneSystemLockedUntilDay3", sink.Events[2].reason);
        }

        [Test]
        public void AdapterUsesStableDedupeKeyAndDoesNotAdvanceSequenceOnDuplicate()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var observation = new RuneRewardPendingObservationV2("reward-3-pending", 3);
            string error;

            Assert.AreEqual(AnalyticsRecordResultV2.Accepted, adapter.RecordRewardPending(observation, out error), error);
            Assert.AreEqual(AnalyticsRecordResultV2.Duplicate, adapter.RecordRewardPending(observation, out error));
            Assert.AreEqual(1, sink.Events.Count);

            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                adapter.RecordRewardPending(new RuneRewardPendingObservationV2("reward-4-pending", 4), out error),
                error);
            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(2, sink.Events[1].sequence);
        }

        [Test]
        public void SchemaRequiresCorrectRuneLifecycleStates()
        {
            var value = AnalyticsEventV2Factory.Create(
                AnalyticsEventNamesV2.RuneRewardPending,
                "rune-state-1",
                "run-rune",
                99,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                AnalyticsSides.Player,
                3,
                AnalyticsRankTiers.Unranked,
                AnalyticsAiDifficulties.Standard,
                1,
                "config.v2",
                "build.v2",
                new System.DateTime(2026, 8, 18, 0, 0, 0, System.DateTimeKind.Utc));
            value.reward_wave = 3;
            value.reward_state = AnalyticsRuneRewardStates.Granted;

            string error;
            Assert.IsFalse(AnalyticsSchemaV2.TryValidate(value, out error));
            StringAssert.Contains("pending reward_state", error);
        }

        private static RuneAnalyticsAdapterV2 CreateAdapter(InMemoryAnalyticsSinkV2 sink)
        {
            return new RuneAnalyticsAdapterV2(
                new AnalyticsRecorderV2(sink),
                "run-rune-1",
                7401,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                AnalyticsSides.Player,
                AnalyticsRankTiers.Unranked,
                AnalyticsAiDifficulties.Standard,
                "config.v2",
                "build.v2");
        }
    }
}
