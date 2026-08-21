using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GameShared.Telemetry
{
    public static class AnalyticsEventNames
    {
        public const string RunStart = "run_start";
        public const string RunFinish = "run_finish";
        public const string WaveReached = "wave_reached";
        public const string DeathWave = "death_wave";
        public const string BossSpawned = "boss_spawned";
        public const string BossKilled = "boss_killed";
        public const string BossTtk = "boss_ttk";
        public const string BossSkillCast = "boss_skill_cast";
        public const string BossSummonSpawned = "boss_summon_spawned";
        public const string Recruit = "recruit";
        public const string HeroFormed = "hero_formed";
        public const string HeroLevelUp = "hero_level_up";
        public const string ItemEquipped = "item_equipped";
        public const string ItemUsed = "item_used";
        public const string RuneEquipped = "rune_equipped";
        public const string RuneDrop = "rune_drop";
        public const string HeartLost = "heart_lost";

        public static bool IsKnown(string value)
        {
            switch (value)
            {
                case RunStart:
                case RunFinish:
                case WaveReached:
                case DeathWave:
                case BossSpawned:
                case BossKilled:
                case BossTtk:
                case BossSkillCast:
                case BossSummonSpawned:
                case Recruit:
                case HeroFormed:
                case HeroLevelUp:
                case ItemEquipped:
                case ItemUsed:
                case RuneEquipped:
                case RuneDrop:
                case HeartLost:
                    return true;
                default:
                    return false;
            }
        }
    }

    public static class AnalyticsSchemaV1
    {
        public const int EventVersion = 1;

        public static bool TryValidate(AnalyticsEvent value, out string error)
        {
            if (value == null)
            {
                error = "event is required";
                return false;
            }

            if (!AnalyticsEventNames.IsKnown(value.event_name))
            {
                error = "event_name is not registered in Analytics Event Schema V1";
                return false;
            }

            if (value.event_version != EventVersion)
            {
                error = "event_version is not supported";
                return false;
            }

            if (string.IsNullOrWhiteSpace(value.event_id) || string.IsNullOrWhiteSpace(value.run_id) ||
                string.IsNullOrWhiteSpace(value.config_version) || string.IsNullOrWhiteSpace(value.build_version))
            {
                error = "event_id, run_id, config_version and build_version are required";
                return false;
            }

            if (value.sequence < 1)
            {
                error = "sequence must be positive";
                return false;
            }

            if (value.wave < 0 || !IsKnownSide(value.side))
            {
                error = "side or wave is invalid";
                return false;
            }

            DateTime parsedTimestamp;
            if (!DateTime.TryParse(
                    value.client_timestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out parsedTimestamp))
            {
                error = "client_timestamp must be an ISO-8601 timestamp";
                return false;
            }

            if (!HasRequiredPayload(value, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool HasRequiredPayload(AnalyticsEvent value, out string error)
        {
            switch (value.event_name)
            {
                case AnalyticsEventNames.RunFinish:
                    if (string.IsNullOrWhiteSpace(value.result)) return Missing("result", out error);
                    break;
                case AnalyticsEventNames.DeathWave:
                    if (string.IsNullOrWhiteSpace(value.reason)) return Missing("reason", out error);
                    break;
                case AnalyticsEventNames.BossSpawned:
                case AnalyticsEventNames.BossKilled:
                case AnalyticsEventNames.BossTtk:
                    if (string.IsNullOrWhiteSpace(value.boss_id)) return Missing("boss_id", out error);
                    break;
                case AnalyticsEventNames.BossSkillCast:
                    if (string.IsNullOrWhiteSpace(value.boss_id) || string.IsNullOrWhiteSpace(value.skill_id))
                        return Missing("boss_id and skill_id", out error);
                    break;
                case AnalyticsEventNames.BossSummonSpawned:
                    if (string.IsNullOrWhiteSpace(value.boss_id) || string.IsNullOrWhiteSpace(value.summon_id))
                        return Missing("boss_id and summon_id", out error);
                    break;
                case AnalyticsEventNames.Recruit:
                    if (string.IsNullOrWhiteSpace(value.unit_id)) return Missing("unit_id", out error);
                    break;
                case AnalyticsEventNames.HeroFormed:
                case AnalyticsEventNames.HeroLevelUp:
                    if (string.IsNullOrWhiteSpace(value.hero_id)) return Missing("hero_id", out error);
                    break;
                case AnalyticsEventNames.ItemEquipped:
                case AnalyticsEventNames.ItemUsed:
                    if (string.IsNullOrWhiteSpace(value.item_id)) return Missing("item_id", out error);
                    break;
                case AnalyticsEventNames.RuneEquipped:
                case AnalyticsEventNames.RuneDrop:
                    if (string.IsNullOrWhiteSpace(value.rune_id)) return Missing("rune_id", out error);
                    break;
                case AnalyticsEventNames.HeartLost:
                    if (value.count <= 0 || string.IsNullOrWhiteSpace(value.reason))
                        return Missing("positive count and reason", out error);
                    break;
            }

            error = string.Empty;
            return true;
        }

        private static bool Missing(string fields, out string error)
        {
            error = fields + " is required for this event";
            return false;
        }

        private static bool IsKnownSide(string value)
        {
            return value == "player" || value == "ai" || value == "system";
        }
    }

    /// <summary>
    /// Stable V1 event wire model. IDs are machine identifiers only; display text and account data
    /// are deliberately excluded. Optional event-specific fields remain empty when not applicable.
    /// </summary>
    [Serializable]
    public sealed class AnalyticsEvent
    {
        public string event_name;
        public int event_version = AnalyticsSchemaV1.EventVersion;
        public string event_id;
        public string client_timestamp;
        public string run_id;
        public int run_seed;
        public string config_version;
        public string build_version;
        public string side;
        public int wave;
        public long sequence;

        public string result;
        public float elapsed_seconds;
        public string boss_id;
        public string skill_id;
        public string summon_id;
        public string unit_id;
        public string hero_id;
        public int hero_level;
        public string item_id;
        public string rune_id;
        public int count;
        public float value;
        public string reason;

        public AnalyticsEvent Clone()
        {
            return (AnalyticsEvent)MemberwiseClone();
        }
    }

    public interface IAnalyticsSink
    {
        int WriteErrorCount { get; }
        bool Record(AnalyticsEvent value);
        void Flush();
    }

    public sealed class InMemoryAnalyticsSink : IAnalyticsSink
    {
        private readonly List<AnalyticsEvent> events = new List<AnalyticsEvent>();

        public int WriteErrorCount { get; private set; }
        public IReadOnlyList<AnalyticsEvent> Events => events;

        public bool Record(AnalyticsEvent value)
        {
            if (value == null)
            {
                WriteErrorCount++;
                return false;
            }

            events.Add(value.Clone());
            return true;
        }

        public void Flush()
        {
        }
    }

    public enum AnalyticsRecordResult
    {
        Accepted,
        Duplicate,
        Invalid,
        OutOfOrder,
        RunSeedMismatch,
        SinkFailure
    }

    /// <summary>
    /// Per-process ordering and duplicate guard. Transport retries are identified by event_id;
    /// production upload can replace the sink without changing callers or event semantics.
    /// </summary>
    public sealed class AnalyticsRecorder
    {
        private readonly IAnalyticsSink sink;
        private readonly HashSet<string> recordedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> nextSequenceByRun = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> seedByRun = new Dictionary<string, int>(StringComparer.Ordinal);

        public AnalyticsRecorder(IAnalyticsSink sink)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public AnalyticsRecordResult Record(AnalyticsEvent value, out string error)
        {
            if (!AnalyticsSchemaV1.TryValidate(value, out error))
            {
                return AnalyticsRecordResult.Invalid;
            }

            if (recordedIds.Contains(value.event_id))
            {
                error = string.Empty;
                return AnalyticsRecordResult.Duplicate;
            }

            int registeredSeed;
            if (seedByRun.TryGetValue(value.run_id, out registeredSeed) && registeredSeed != value.run_seed)
            {
                error = "run_seed changed within the same run_id";
                return AnalyticsRecordResult.RunSeedMismatch;
            }

            long expectedSequence;
            if (!nextSequenceByRun.TryGetValue(value.run_id, out expectedSequence))
            {
                expectedSequence = 1;
            }

            if (value.sequence != expectedSequence)
            {
                error = "sequence must be the next value for its run_id";
                return AnalyticsRecordResult.OutOfOrder;
            }

            if (!sink.Record(value))
            {
                error = "analytics sink rejected the event";
                return AnalyticsRecordResult.SinkFailure;
            }

            recordedIds.Add(value.event_id);
            seedByRun[value.run_id] = value.run_seed;
            nextSequenceByRun[value.run_id] = expectedSequence + 1;
            error = string.Empty;
            return AnalyticsRecordResult.Accepted;
        }

        public void Flush()
        {
            sink.Flush();
        }
    }
}
