using System;
using DragonBound.Analytics;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class AnalyticsEventSchemaV2Tests
    {
        [Test]
        public void EveryV2EventNameIsRegistered()
        {
            Assert.AreEqual(45, AnalyticsEventNamesV2.All.Length);

            foreach (var name in AnalyticsEventNamesV2.All)
            {
                Assert.IsTrue(AnalyticsEventNamesV2.IsKnown(name), name);
            }
        }

        [Test]
        public void CommonEnvelopeRequiresExecutionContextRankAndAiDifficulty()
        {
            var value = CreateEvent(AnalyticsEventNamesV2.RunStart, 1);
            string error;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(value, out error), error);

            value.execution_context = "live_and_diagnostic_mixed";
            Assert.IsFalse(AnalyticsSchemaV2.TryValidate(value, out error));
            StringAssert.Contains("execution_context", error);

            value = CreateEvent(AnalyticsEventNamesV2.RunStart, 1);
            value.rank_tier = string.Empty;
            Assert.IsFalse(AnalyticsSchemaV2.TryValidate(value, out error));
            StringAssert.Contains("rank_tier", error);

            value = CreateEvent(AnalyticsEventNamesV2.RunStart, 1);
            value.ai_difficulty = "omniscient";
            Assert.IsFalse(AnalyticsSchemaV2.TryValidate(value, out error));
            StringAssert.Contains("ai_difficulty", error);
        }

        [Test]
        public void RecruitResultRequiresV3CountsAndRemainingBag()
        {
            var value = CreateEvent(AnalyticsEventNamesV2.RecruitResult, 1);
            value.recruitment_number = 4;
            value.component_count = 3;
            value.basic_count = 1;
            value.forge_pick_count = 1;
            value.component_policy = AnalyticsComponentPolicies.RecruitComponentPolicyV3;
            value.remaining_component_bag = 17;

            string error;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(value, out error), error);

            value.component_count = 4;
            value.basic_count = 0;
            value.forge_pick_count = 1;
            Assert.IsFalse(AnalyticsSchemaV2.TryValidate(value, out error));
            StringAssert.Contains("component_policy", error);
        }

        [Test]
        public void FormationSnapshotIsBoundedToKeySnapshotCounts()
        {
            var value = CreateEvent(AnalyticsEventNamesV2.FormationSnapshot, 1);
            value.snapshot_reason = "wave_start";
            value.basic_unit_count = 5;
            value.hero_count = 2;
            value.component_unit_count = 3;
            value.board_occupied = 8;
            value.bench_occupied = 2;
            value.hittable_unit_count = 6;

            string error;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(value, out error), error);

            value.hittable_unit_count = -1;
            Assert.IsFalse(AnalyticsSchemaV2.TryValidate(value, out error));
            StringAssert.Contains("formation counts", error);
        }

        [Test]
        public void BossEventsCoverSpawnSkillSummonDamageKillAndGoal()
        {
            string error;

            var spawn = CreateEvent(AnalyticsEventNamesV2.BossSpawn, 1);
            spawn.enemy_type = AnalyticsEnemyTypes.Boss;
            spawn.boss_id = "BOSS_SOULCHAIN_BINDER";
            spawn.move_speed_cells_per_second = 0.20f;
            spawn.max_hit_points = 500f;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(spawn, out error), error);

            var skill = CreateEvent(AnalyticsEventNamesV2.BossSkill, 1);
            skill.boss_id = "BOSS_SOULCHAIN_BINDER";
            skill.skill_id = "SOULCHAIN";
            skill.count = 1;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(skill, out error), error);

            var summon = CreateEvent(AnalyticsEventNamesV2.BossSummon, 1);
            summon.boss_id = "BOSS_SUMMONER";
            summon.summon_id = "SUMMON_WHELP";
            summon.enemy_type = AnalyticsEnemyTypes.BossSummon;
            summon.count = 2;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(summon, out error), error);

            var window = CreateEvent(AnalyticsEventNamesV2.BossDamageWindow, 1);
            window.boss_id = "BOSS_SOULCHAIN_BINDER";
            window.damage_window_id = "spawn_to_3s";
            window.duration_seconds = 3f;
            window.damage = 38f;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(window, out error), error);

            var kill = CreateEvent(AnalyticsEventNamesV2.BossKill, 1);
            kill.boss_id = "BOSS_SOULCHAIN_BINDER";
            kill.duration_seconds = 21.5f;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(kill, out error), error);

            var goal = CreateEvent(AnalyticsEventNamesV2.BossGoal, 1);
            goal.boss_id = "BOSS_SOULCHAIN_BINDER";
            goal.heart_before = 3;
            goal.heart_after = 0;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(goal, out error), error);
        }

        [Test]
        public void LedgerResultRequiresHashedReferencesAndRejectsTokenLikeValues()
        {
            var value = CreateEvent(AnalyticsEventNamesV2.LedgerResult, 1);
            value.ledger_operation = "energy_spend";
            value.ledger_status = AnalyticsLedgerStatuses.Accepted;
            value.idempotency_key_hash = "sha256:abcdef1234567890";

            string error;
            Assert.IsTrue(AnalyticsSchemaV2.TryValidate(value, out error), error);

            value.idempotency_key_hash = "bearer-token-raw";
            Assert.IsFalse(AnalyticsSchemaV2.TryValidate(value, out error));
            StringAssert.Contains("hashed ledger reference", error);
        }

        [Test]
        public void RecorderPreservesRunSeedContextSequenceAndDropsDuplicateIds()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var recorder = new AnalyticsRecorderV2(sink);
            string error;

            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                recorder.Record(CreateEvent(AnalyticsEventNamesV2.RunStart, 1), out error),
                error);
            Assert.AreEqual(
                AnalyticsRecordResultV2.Accepted,
                recorder.Record(CreateEvent(AnalyticsEventNamesV2.WaveStart, 2), out error),
                error);

            var duplicate = CreateEvent(AnalyticsEventNamesV2.WaveFinish, 3);
            duplicate.event_id = "event-2";
            Assert.AreEqual(AnalyticsRecordResultV2.Duplicate, recorder.Record(duplicate, out error));
            Assert.AreEqual(2, sink.Events.Count);

            var wrongSeed = CreateEvent(AnalyticsEventNamesV2.WaveFinish, 3);
            wrongSeed.event_id = "event-3";
            wrongSeed.run_seed = 7402;
            Assert.AreEqual(AnalyticsRecordResultV2.RunSeedMismatch, recorder.Record(wrongSeed, out error));

            var mixedContext = CreateEvent(AnalyticsEventNamesV2.WaveFinish, 3);
            mixedContext.event_id = "event-4";
            mixedContext.execution_context = AnalyticsExecutionContexts.DiagnosticAiVsAi;
            Assert.AreEqual(AnalyticsRecordResultV2.ExecutionContextMismatch, recorder.Record(mixedContext, out error));

            var skipped = CreateEvent(AnalyticsEventNamesV2.WaveFinish, 4);
            skipped.event_id = "event-5";
            Assert.AreEqual(AnalyticsRecordResultV2.OutOfOrder, recorder.Record(skipped, out error));
        }

        [Test]
        public void InMemorySinkCopiesEventsAndJsonContainsV2Envelope()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var value = CreateEvent(AnalyticsEventNamesV2.MatchFinish, 1);
            value.match_result = "victory";
            value.reason = "ai_defeated";
            Assert.IsTrue(sink.Record(value));
            value.match_result = "mutated_after_record";

            Assert.AreEqual("victory", sink.Events[0].match_result);

            var json = JsonUtility.ToJson(sink.Events[0]);
            StringAssert.Contains("\"event_version\":2", json);
            StringAssert.Contains("\"execution_context\":\"live_player_vs_ai\"", json);
            StringAssert.Contains("\"run_seed\":7401", json);
        }

        private static AnalyticsEventV2 CreateEvent(string name, long sequence)
        {
            return AnalyticsEventV2Factory.Create(
                name,
                "event-" + sequence,
                "run-7401",
                7401,
                AnalyticsExecutionContexts.LivePlayerVsAi,
                AnalyticsSides.Player,
                name == AnalyticsEventNamesV2.RunStart ? 0 : 1,
                AnalyticsRankTiers.Unranked,
                AnalyticsAiDifficulties.Standard,
                sequence,
                "config.v2",
                "build.v2",
                new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc));
        }
    }
}
