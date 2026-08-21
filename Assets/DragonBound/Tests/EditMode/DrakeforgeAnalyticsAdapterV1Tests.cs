using System;
using DragonBound.Analytics;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Runes;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class DrakeforgeAnalyticsAdapterV1Tests
    {
        [Test]
        public void W12LifecycleAndStormcallerCastMapToTypedEvents()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var spawned = new EnemyLifecycleEvent(
                EnemyLifecycleEventKind.Spawned,
                12,
                "enemy.boss.player.12",
                EnemyArchetype.Boss,
                1200f,
                0f);

            Assert.IsTrue(adapter.RecordRunStart(), adapter.LastError);
            Assert.IsTrue(adapter.RecordW12Lifecycle(TeamSide.Player, spawned, 10f), adapter.LastError);
            Assert.IsTrue(adapter.RecordStormcallerCast(
                TeamSide.Player,
                12,
                new StormcallerCastEvent(StormcallerCastEventKind.CastStarted, 1, 7f, 0, 0f)), adapter.LastError);
            Assert.IsTrue(adapter.RecordStormcallerCast(
                TeamSide.Player,
                12,
                new StormcallerCastEvent(StormcallerCastEventKind.CastFailed, 1, 7.75f, 0, 12f)), adapter.LastError);

            Assert.AreEqual(4, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.BossSpawn, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.BossSkill, sink.Events[2].event_name);
            Assert.AreEqual("started", sink.Events[2].result);
            Assert.AreEqual(AnalyticsEventNamesV2.BossSkill, sink.Events[3].event_name);
            Assert.AreEqual("blocked", sink.Events[3].result);
            Assert.AreEqual(AnalyticsExecutionContexts.DiagnosticAiVsAi, sink.Events[0].execution_context);
            Assert.AreEqual(AnalyticsSides.Player, sink.Events[1].side);
        }

        [Test]
        public void DamageIsAggregatedUntilWindowFlushAndNeverEmitsPerAttack()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            adapter.RecordRunStart();
            adapter.AccumulateCombatDamage(TeamSide.AI, new CombatEvent(
                TeamSide.AI,
                AttackKind.Single,
                "hero.ai",
                "boss.ai",
                8f,
                false,
                false,
                20,
                shieldDamage: 5f,
                healthDamage: 3f));
            adapter.AccumulateCombatDamage(TeamSide.AI, new CombatEvent(
                TeamSide.AI,
                AttackKind.Single,
                "hero.ai",
                "boss.ai",
                4f,
                false,
                false,
                20,
                shieldDamage: 2f,
                healthDamage: 2f));

            Assert.AreEqual(1, sink.Events.Count);
            Assert.IsTrue(adapter.FlushDamageWindow(TeamSide.AI, 12, "spawn_to_3s", 3f), adapter.LastError);
            Assert.AreEqual(4, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.BossDamageWindow, sink.Events[1].event_name);
            Assert.AreEqual(12f, sink.Events[1].damage, 0.0001f);
            Assert.AreEqual(AnalyticsEventNamesV2.BossDamageWindow, sink.Events[2].event_name);
            Assert.AreEqual(7f, sink.Events[2].damage, 0.0001f);
            Assert.AreEqual("shield_damage", sink.Events[2].result);
            Assert.AreEqual(AnalyticsEventNamesV2.BossDamageWindow, sink.Events[3].event_name);
            Assert.AreEqual(5f, sink.Events[3].damage, 0.0001f);
            Assert.AreEqual("health_damage", sink.Events[3].result);
        }

        [Test]
        public void ItemAndRuneAdapterRecordsSnapshotCommandResultCooldownAndReward()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            adapter.RecordRunStart();
            Assert.IsTrue(adapter.RecordItemSnapshotLocked(TeamSide.Player, 0, ItemRunSnapshot.Empty), adapter.LastError);
            Assert.IsTrue(adapter.RecordItemCommand(TeamSide.Player, 12, ItemIds.WinterveilRune, "activate"), adapter.LastError);
            Assert.IsTrue(adapter.RecordItemResult(TeamSide.Player, 12, ItemIds.WinterveilRune, false, "Cooldown"), adapter.LastError);
            Assert.IsTrue(adapter.RecordItemCooldown(TeamSide.Player, 12, ItemIds.WinterveilRune, 29f), adapter.LastError);
            Assert.IsTrue(adapter.RecordRuneLoadoutSnapshotLocked(TeamSide.Player, 0, RuneLoadoutSnapshot.Empty), adapter.LastError);
            Assert.IsTrue(adapter.RecordRuneReward(TeamSide.Player, new RuneReward(3, RuneRarity.Common, "RUNE_TEST", true, false)), adapter.LastError);
            Assert.IsTrue(adapter.RecordRuneGateRejected(TeamSide.Player, 2, "RuneSystemLockedUntilDay3"), adapter.LastError);

            Assert.AreEqual(8, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.ItemEquip, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.ItemUse, sink.Events[4].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGrant, sink.Events[6].event_name);
            Assert.AreEqual("rune_reward", sink.Events[6].result);
            Assert.AreEqual("complete", sink.Events[6].reason);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneGrant, sink.Events[7].event_name);
            Assert.AreEqual("rune_gate_rejected", sink.Events[7].result);
        }

        [Test]
        public void CalibrationCannotBeRecordedInLiveContext()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = new DrakeforgeAnalyticsAdapterV1(
                new AnalyticsRecorderV2(sink),
                new DrakeforgeAnalyticsRunContext(
                    "live-run",
                    7,
                    AnalyticsExecutionContexts.LivePlayerVsAi,
                    "config.v2",
                    "build.v2",
                    AnalyticsRankTiers.Unranked,
                    AnalyticsAiDifficulties.Standard));

            Assert.IsFalse(adapter.RecordCalibrationSample(TeamSide.AI, 12, "stormcaller", 1200f, "boss_killed"));
            StringAssert.Contains("diagnostic_ai_vs_ai", adapter.LastError);
        }

        private static DrakeforgeAnalyticsAdapterV1 CreateAdapter(InMemoryAnalyticsSinkV2 sink)
        {
            return new DrakeforgeAnalyticsAdapterV1(
                new AnalyticsRecorderV2(sink),
                new DrakeforgeAnalyticsRunContext(
                    "diag-run-1",
                    7401,
                    AnalyticsExecutionContexts.DiagnosticAiVsAi,
                    "config.v2",
                    "build.v2",
                    AnalyticsRankTiers.Unranked,
                    AnalyticsAiDifficulties.Standard),
                () => new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc));
        }
    }
}
