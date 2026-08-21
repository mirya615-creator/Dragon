using System;
using System.IO;
using GameShared.Telemetry;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class AnalyticsEventSchemaV1Tests
    {
        [Test]
        public void EveryFrozenEventNameIsRegistered()
        {
            var names = new[]
            {
                AnalyticsEventNames.RunStart, AnalyticsEventNames.RunFinish, AnalyticsEventNames.WaveReached,
                AnalyticsEventNames.DeathWave, AnalyticsEventNames.BossSpawned, AnalyticsEventNames.BossKilled,
                AnalyticsEventNames.BossTtk, AnalyticsEventNames.BossSkillCast, AnalyticsEventNames.BossSummonSpawned,
                AnalyticsEventNames.Recruit, AnalyticsEventNames.HeroFormed, AnalyticsEventNames.HeroLevelUp,
                AnalyticsEventNames.ItemEquipped, AnalyticsEventNames.ItemUsed, AnalyticsEventNames.RuneEquipped,
                AnalyticsEventNames.RuneDrop, AnalyticsEventNames.HeartLost
            };

            foreach (var name in names)
            {
                Assert.IsTrue(AnalyticsEventNames.IsKnown(name), name);
            }
        }

        [Test]
        public void ValidEventSerializesAllRequiredCommonFields()
        {
            var value = CreateEvent(AnalyticsEventNames.RunStart, 1);
            string error;
            Assert.IsTrue(AnalyticsSchemaV1.TryValidate(value, out error), error);

            var json = JsonUtility.ToJson(value);
            StringAssert.Contains("\"event_name\":\"run_start\"", json);
            StringAssert.Contains("\"event_version\":1", json);
            StringAssert.Contains("\"run_seed\":7401", json);
            StringAssert.Contains("\"config_version\":\"config.v1\"", json);
        }

        [Test]
        public void SchemaRejectsMissingCommonFieldsAndUnsupportedVersion()
        {
            var missing = CreateEvent(AnalyticsEventNames.RunStart, 1);
            missing.event_id = string.Empty;
            string error;
            Assert.IsFalse(AnalyticsSchemaV1.TryValidate(missing, out error));
            StringAssert.Contains("event_id", error);

            var unsupported = CreateEvent(AnalyticsEventNames.RunStart, 1);
            unsupported.event_version = 2;
            Assert.IsFalse(AnalyticsSchemaV1.TryValidate(unsupported, out error));
            StringAssert.Contains("event_version", error);
        }

        [Test]
        public void SchemaRequiresStableIdsForTypedEvents()
        {
            var boss = CreateEvent(AnalyticsEventNames.BossSkillCast, 1);
            boss.skill_id = "SOULCHAIN";
            string error;
            Assert.IsFalse(AnalyticsSchemaV1.TryValidate(boss, out error));
            StringAssert.Contains("boss_id", error);

            boss.boss_id = "BOSS_SOULCHAIN_BINDER";
            Assert.IsTrue(AnalyticsSchemaV1.TryValidate(boss, out error), error);
        }

        [Test]
        public void SchemaRequiresLossReasonAndPositiveHeartLoss()
        {
            var death = CreateEvent(AnalyticsEventNames.DeathWave, 1);
            string error;
            Assert.IsFalse(AnalyticsSchemaV1.TryValidate(death, out error));
            StringAssert.Contains("reason", error);

            death.reason = "normal_goal";
            Assert.IsTrue(AnalyticsSchemaV1.TryValidate(death, out error), error);

            var heartLoss = CreateEvent(AnalyticsEventNames.HeartLost, 1);
            heartLoss.reason = "normal_goal";
            Assert.IsFalse(AnalyticsSchemaV1.TryValidate(heartLoss, out error));
            StringAssert.Contains("positive count", error);

            heartLoss.count = 1;
            Assert.IsTrue(AnalyticsSchemaV1.TryValidate(heartLoss, out error), error);
        }

        [Test]
        public void RecorderPreservesRunSeedAndSequenceAndDropsDuplicateIds()
        {
            var sink = new InMemoryAnalyticsSink();
            var recorder = new AnalyticsRecorder(sink);
            string error;

            Assert.AreEqual(AnalyticsRecordResult.Accepted, recorder.Record(CreateEvent(AnalyticsEventNames.RunStart, 1), out error), error);
            Assert.AreEqual(AnalyticsRecordResult.Accepted, recorder.Record(CreateEvent(AnalyticsEventNames.WaveReached, 2), out error), error);

            var duplicate = CreateEvent(AnalyticsEventNames.WaveReached, 3);
            duplicate.event_id = "event-2";
            Assert.AreEqual(AnalyticsRecordResult.Duplicate, recorder.Record(duplicate, out error));
            Assert.AreEqual(2, sink.Events.Count);

            var wrongSeed = CreateEvent(AnalyticsEventNames.WaveReached, 3);
            wrongSeed.event_id = "event-3";
            wrongSeed.run_seed = 7402;
            Assert.AreEqual(AnalyticsRecordResult.RunSeedMismatch, recorder.Record(wrongSeed, out error));

            var skipped = CreateEvent(AnalyticsEventNames.WaveReached, 4);
            skipped.event_id = "event-4";
            Assert.AreEqual(AnalyticsRecordResult.OutOfOrder, recorder.Record(skipped, out error));
        }

        [Test]
        public void InMemorySinkCopiesRecordedEvents()
        {
            var sink = new InMemoryAnalyticsSink();
            var value = CreateEvent(AnalyticsEventNames.Recruit, 1);
            value.unit_id = "BASIC_FIRE";
            Assert.IsTrue(sink.Record(value));
            value.unit_id = "MUTATED_AFTER_RECORD";

            Assert.AreEqual("BASIC_FIRE", sink.Events[0].unit_id);
            Assert.AreEqual(0, sink.WriteErrorCount);
        }

        [Test]
        public void JsonlSinkWritesTopLevelV1Events()
        {
            var directory = Path.Combine(Path.GetTempPath(), "DragonBoundTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "analytics.jsonl");
            try
            {
                using (var sink = new JsonlTelemetry(path))
                {
                    Assert.IsTrue(sink.Record(CreateEvent(AnalyticsEventNames.RunStart, 1)));
                    sink.Flush();
                }

                var line = File.ReadAllText(path);
                StringAssert.Contains("\"event_name\":\"run_start\"", line);
                StringAssert.Contains("\"run_id\":\"run-7401\"", line);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static AnalyticsEvent CreateEvent(string name, long sequence)
        {
            return new AnalyticsEvent
            {
                event_name = name,
                event_id = "event-" + sequence,
                client_timestamp = "2026-08-17T12:00:00.0000000Z",
                run_id = "run-7401",
                run_seed = 7401,
                config_version = "config.v1",
                build_version = "build.v1",
                side = "player",
                wave = 1,
                sequence = sequence
            };
        }
    }
}
