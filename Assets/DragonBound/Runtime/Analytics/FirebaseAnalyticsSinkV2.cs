using System;
using System.Collections.Generic;

namespace DragonBound.Analytics
{
    public enum AnalyticsConsentStateV2
    {
        Unknown,
        Granted,
        Denied
    }

    public interface IAnalyticsPrivacyControlV2
    {
        void ClearPending();
    }

    public interface IAnalyticsSinkAvailabilityV2
    {
        bool IsAvailable { get; }
    }

    public static class FirebaseAnalyticsRuntimeState
    {
        private static volatile bool isReady;
        private static volatile bool collectionEnabled;
        private static volatile AnalyticsConsentStateV2 consentState;

        public static bool IsReady => isReady;
        public static bool CollectionEnabled => collectionEnabled;
        public static AnalyticsConsentStateV2 ConsentState => consentState;
        public static bool CanCollect => collectionEnabled && consentState == AnalyticsConsentStateV2.Granted;
        public static bool CanRecord => isReady && CanCollect;

        public static void SetReady(bool value)
        {
            isReady = value;
        }

        public static void SetCollectionEnabled(bool value)
        {
            collectionEnabled = value;
        }

        public static void SetConsentState(AnalyticsConsentStateV2 value)
        {
            consentState = value;
        }
    }

    /// <summary>Accepts valid events without storing or transmitting them.</summary>
    public sealed class NoOpAnalyticsSinkV2 : IAnalyticsSinkV2, IAnalyticsSinkAvailabilityV2
    {
        public int WriteErrorCount { get; private set; }
        public bool IsAvailable => true;

        public bool Record(AnalyticsEventV2 value)
        {
            if (value != null)
            {
                return true;
            }

            WriteErrorCount++;
            return false;
        }

        public void Flush()
        {
        }
    }

    /// <summary>
    /// Bounded queue that treats an event as accepted once it is safely buffered. It preserves
    /// event order while Firebase is initializing and never blocks gameplay on transport state.
    /// </summary>
    public sealed class BufferedAnalyticsSinkV2 : IAnalyticsSinkV2, IAnalyticsPrivacyControlV2
    {
        public const int DefaultCapacity = 256;
        public const int DefaultMaxAttempts = 3;

        private sealed class PendingEvent
        {
            public PendingEvent(AnalyticsEventV2 value)
            {
                Value = value;
            }

            public AnalyticsEventV2 Value { get; }
            public int FailedAttempts { get; set; }
        }

        private readonly object gate = new object();
        private readonly IAnalyticsSinkV2 downstream;
        private readonly IAnalyticsSinkAvailabilityV2 availability;
        private readonly Func<bool> canBuffer;
        private readonly Queue<PendingEvent> pending = new Queue<PendingEvent>();
        private readonly int capacity;
        private readonly int maxAttempts;
        private int writeErrorCount;
        private int failedAttemptCount;
        private int droppedCount;
        private int privacyDiscardedCount;

        public BufferedAnalyticsSinkV2(
            IAnalyticsSinkV2 downstream,
            int capacity = DefaultCapacity,
            Func<bool> canBuffer = null,
            int maxAttempts = DefaultMaxAttempts)
        {
            this.downstream = downstream ?? throw new ArgumentNullException(nameof(downstream));
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            }

            this.capacity = capacity;
            this.maxAttempts = maxAttempts;
            availability = downstream as IAnalyticsSinkAvailabilityV2;
            this.canBuffer = canBuffer ?? (() => true);
        }

        public int WriteErrorCount => writeErrorCount + downstream.WriteErrorCount;
        public int FailedAttemptCount => failedAttemptCount;
        public int DroppedCount => droppedCount;
        public int PrivacyDiscardedCount => privacyDiscardedCount;

        public int PendingCount
        {
            get
            {
                lock (gate)
                {
                    return pending.Count;
                }
            }
        }

        public bool Record(AnalyticsEventV2 value)
        {
            if (value == null)
            {
                writeErrorCount++;
                return false;
            }

            if (!canBuffer())
            {
                return false;
            }

            lock (gate)
            {
                if (pending.Count >= capacity)
                {
                    writeErrorCount++;
                    droppedCount++;
                    return false;
                }

                pending.Enqueue(new PendingEvent(value.Clone()));
                DrainAvailableEvents();
                return true;
            }
        }

        public void Flush()
        {
            lock (gate)
            {
                DrainAvailableEvents();
                try
                {
                    downstream.Flush();
                }
                catch (Exception)
                {
                    writeErrorCount++;
                }
            }
        }

        public void ClearPending()
        {
            lock (gate)
            {
                privacyDiscardedCount += pending.Count;
                pending.Clear();
            }
        }

        private void DrainAvailableEvents()
        {
            if (availability != null && !availability.IsAvailable)
            {
                return;
            }

            while (pending.Count > 0)
            {
                var current = pending.Peek();
                bool recorded;
                try
                {
                    recorded = downstream.Record(current.Value);
                }
                catch (Exception)
                {
                    recorded = false;
                    writeErrorCount++;
                }

                if (recorded)
                {
                    pending.Dequeue();
                    continue;
                }

                current.FailedAttempts++;
                failedAttemptCount++;
                if (current.FailedAttempts >= maxAttempts)
                {
                    pending.Dequeue();
                    droppedCount++;
                    continue;
                }

                return;
            }
        }
    }

    public enum FirebaseAnalyticsParameterKindV2
    {
        String,
        Integer,
        Double
    }

    public readonly struct FirebaseAnalyticsParameterV2
    {
        public FirebaseAnalyticsParameterV2(string name, string value)
        {
            Name = name;
            StringValue = value;
            IntegerValue = 0;
            DoubleValue = 0d;
            Kind = FirebaseAnalyticsParameterKindV2.String;
        }

        public FirebaseAnalyticsParameterV2(string name, long value)
        {
            Name = name;
            StringValue = string.Empty;
            IntegerValue = value;
            DoubleValue = 0d;
            Kind = FirebaseAnalyticsParameterKindV2.Integer;
        }

        public FirebaseAnalyticsParameterV2(string name, double value)
        {
            Name = name;
            StringValue = string.Empty;
            IntegerValue = 0;
            DoubleValue = value;
            Kind = FirebaseAnalyticsParameterKindV2.Double;
        }

        public string Name { get; }
        public string StringValue { get; }
        public long IntegerValue { get; }
        public double DoubleValue { get; }
        public FirebaseAnalyticsParameterKindV2 Kind { get; }
    }

    public interface IFirebaseAnalyticsGatewayV2
    {
        void LogEvent(string eventName, IReadOnlyList<FirebaseAnalyticsParameterV2> parameters);
    }

    public static class FirebaseAnalyticsGatewayRegistryV2
    {
        public static IFirebaseAnalyticsGatewayV2 Current { get; set; }
    }

    public sealed class FirebaseAnalyticsSinkV2 : IAnalyticsSinkV2, IAnalyticsSinkAvailabilityV2
    {
        private readonly IFirebaseAnalyticsGatewayV2 gateway;
        private readonly Func<bool> isAvailable;

        public FirebaseAnalyticsSinkV2(
            IFirebaseAnalyticsGatewayV2 gateway = null,
            Func<bool> isAvailable = null)
        {
            this.gateway = gateway;
            this.isAvailable = isAvailable ?? (() => FirebaseAnalyticsRuntimeState.CanRecord);
        }

        public int WriteErrorCount { get; private set; }
        public bool IsAvailable => isAvailable();

        public bool Record(AnalyticsEventV2 value)
        {
            var targetGateway = gateway ?? FirebaseAnalyticsGatewayRegistryV2.Current;
            if (value == null || !IsAvailable || targetGateway == null)
            {
                WriteErrorCount++;
                return false;
            }

            try
            {
                targetGateway.LogEvent(value.event_name, FirebaseAnalyticsParameterBuilderV2.Build(value));
                return true;
            }
            catch (Exception)
            {
                WriteErrorCount++;
                return false;
            }
        }

        public void Flush()
        {
            // Firebase Analytics owns its local upload queue and exposes no synchronous flush API.
        }
    }

    public static class FirebaseAnalyticsParameterBuilderV2
    {
        public static IReadOnlyList<FirebaseAnalyticsParameterV2> Build(AnalyticsEventV2 value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var result = new List<FirebaseAnalyticsParameterV2>(24);
            Add(result, "event_version", value.event_version);
            Add(result, "event_id", value.event_id);
            Add(result, "run_id", value.run_id);
            Add(result, "run_seed", value.run_seed);
            Add(result, "execution_context", value.execution_context);
            Add(result, "config_version", value.config_version);
            Add(result, "build_version", value.build_version);
            Add(result, "build_lane", value.build_lane);
            Add(result, "side", value.side);
            Add(result, "wave", value.wave);
            Add(result, "rank_tier", value.rank_tier);
            Add(result, "ai_difficulty", value.ai_difficulty);
            Add(result, "ai_profile", value.ai_profile);
            Add(result, "ai_algorithm_version", value.ai_algorithm_version);
            if (value.ai_decision_seed != 0)
            {
                Add(result, "ai_decision_seed", value.ai_decision_seed);
            }
            Add(result, "ai_recovery_match", value.ai_recovery_match ? 1 : 0);
            if (value.player_rank_level > 0)
            {
                Add(result, "player_rank_level", value.player_rank_level);
            }
            Add(result, "sequence", value.sequence);

            switch (value.event_name)
            {
                case AnalyticsEventNamesV2.WaveStart:
                    break;
                case AnalyticsEventNamesV2.WaveFinish:
                    AddPositive(result, "elapsed_seconds", value.elapsed_seconds);
                    break;
                case AnalyticsEventNamesV2.EnemySpawn:
                    Add(result, "enemy_type", value.enemy_type);
                    Add(result, "enemy_id", value.enemy_id);
                    Add(result, "move_speed", value.move_speed_cells_per_second);
                    AddPositive(result, "max_hit_points", value.max_hit_points);
                    break;
                case AnalyticsEventNamesV2.EnemyGoal:
                    Add(result, "enemy_type", value.enemy_type);
                    Add(result, "enemy_id", value.enemy_id);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.RecruitResult:
                    Add(result, "recruitment_number", value.recruitment_number);
                    Add(result, "component_count", value.component_count);
                    Add(result, "basic_count", value.basic_count);
                    Add(result, "forge_pick_count", value.forge_pick_count);
                    Add(result, "component_policy", value.component_policy);
                    Add(result, "remaining_component_bag", value.remaining_component_bag);
                    break;
                case AnalyticsEventNamesV2.FormationSnapshot:
                    Add(result, "snapshot_reason", value.snapshot_reason);
                    Add(result, "basic_unit_count", value.basic_unit_count);
                    Add(result, "hero_count", value.hero_count);
                    Add(result, "component_unit_count", value.component_unit_count);
                    Add(result, "board_occupied", value.board_occupied);
                    Add(result, "bench_occupied", value.bench_occupied);
                    Add(result, "hittable_unit_count", value.hittable_unit_count);
                    break;
                case AnalyticsEventNamesV2.HeroFormed:
                    Add(result, "hero_id", value.hero_id);
                    Add(result, "source_unit_id", value.source_unit_id);
                    break;
                case AnalyticsEventNamesV2.HeroXp:
                    Add(result, "hero_id", value.hero_id);
                    Add(result, "xp_amount", value.xp_amount);
                    break;
                case AnalyticsEventNamesV2.HeroLevelUp:
                    Add(result, "hero_id", value.hero_id);
                    Add(result, "hero_level", value.hero_level);
                    break;
                case AnalyticsEventNamesV2.LastHit:
                    Add(result, "enemy_id", value.enemy_id);
                    Add(result, "source_unit_id", value.source_unit_id);
                    Add(result, "hero_id", value.hero_id);
                    break;
                case AnalyticsEventNamesV2.BossSpawn:
                    Add(result, "boss_id", value.boss_id);
                    Add(result, "enemy_type", value.enemy_type);
                    Add(result, "move_speed", value.move_speed_cells_per_second);
                    Add(result, "max_hit_points", value.max_hit_points);
                    break;
                case AnalyticsEventNamesV2.BossSkill:
                    Add(result, "boss_id", value.boss_id);
                    Add(result, "skill_id", value.skill_id);
                    Add(result, "count", value.count);
                    Add(result, "result", value.result);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.BossSummon:
                    Add(result, "boss_id", value.boss_id);
                    Add(result, "summon_id", value.summon_id);
                    Add(result, "enemy_type", value.enemy_type);
                    Add(result, "count", value.count);
                    break;
                case AnalyticsEventNamesV2.BossDamageWindow:
                    Add(result, "boss_id", value.boss_id);
                    Add(result, "damage_window_id", value.damage_window_id);
                    Add(result, "duration_seconds", value.duration_seconds);
                    Add(result, "damage", value.damage);
                    Add(result, "result", value.result);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.BossKill:
                    Add(result, "boss_id", value.boss_id);
                    Add(result, "duration_seconds", value.duration_seconds);
                    break;
                case AnalyticsEventNamesV2.BossGoal:
                    Add(result, "boss_id", value.boss_id);
                    Add(result, "heart_after", value.heart_after);
                    break;
                case AnalyticsEventNamesV2.HeartLost:
                    Add(result, "count", value.count);
                    Add(result, "reason", value.reason);
                    Add(result, "heart_before", value.heart_before);
                    Add(result, "heart_after", value.heart_after);
                    break;
                case AnalyticsEventNamesV2.DeathWave:
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.MatchFinish:
                    Add(result, "match_result", value.match_result);
                    Add(result, "reason", value.reason);
                    AddPositive(result, "elapsed_seconds", value.elapsed_seconds);
                    break;
                case AnalyticsEventNamesV2.ItemGrant:
                case AnalyticsEventNamesV2.ItemEquip:
                case AnalyticsEventNamesV2.ItemUse:
                    Add(result, "item_id", value.item_id);
                    Add(result, "result", value.result);
                    Add(result, "reason", value.reason);
                    AddPositive(result, "duration_seconds", value.duration_seconds);
                    break;
                case AnalyticsEventNamesV2.RuneGrant:
                case AnalyticsEventNamesV2.RuneEquip:
                    Add(result, "rune_id", value.rune_id);
                    Add(result, "hero_id", value.hero_id);
                    Add(result, "count", value.count);
                    Add(result, "result", value.result);
                    Add(result, "reason", value.reason);
                    Add(result, "snapshot_reason", value.snapshot_reason);
                    break;
                case AnalyticsEventNamesV2.RuneLoadoutAssign:
                case AnalyticsEventNamesV2.RuneLoadoutUnequip:
                case AnalyticsEventNamesV2.RuneCraft:
                    Add(result, "hero_id", value.hero_id);
                    Add(result, "rune_id", value.rune_id);
                    Add(result, "operation_result", value.operation_result);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.RuneGateRejection:
                    Add(result, "rune_operation", value.rune_operation);
                    Add(result, "gate_state", value.gate_state);
                    Add(result, "account_day", value.account_day);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.RuneRewardPending:
                case AnalyticsEventNamesV2.RuneRewardGranted:
                case AnalyticsEventNamesV2.RuneRewardRejected:
                    Add(result, "reward_wave", value.reward_wave);
                    Add(result, "reward_state", value.reward_state);
                    Add(result, "rune_id", value.rune_id);
                    Add(result, "reward_form", value.reward_form);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.EnergySpend:
                case AnalyticsEventNamesV2.EnergyGrant:
                    Add(result, "energy_amount", value.energy_amount);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.AdRequest:
                case AnalyticsEventNamesV2.AdResult:
                    Add(result, "ad_point_id", value.ad_point_id);
                    Add(result, "result", value.result);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.MerchantOpen:
                case AnalyticsEventNamesV2.MerchantOffer:
                case AnalyticsEventNamesV2.MerchantPurchase:
                    Add(result, "merchant_id", value.merchant_id);
                    Add(result, "offer_id", value.offer_id);
                    Add(result, "currency_type", value.currency_type);
                    Add(result, "result", value.result);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.LedgerResult:
                    Add(result, "ledger_operation", value.ledger_operation);
                    Add(result, "ledger_status", value.ledger_status);
                    Add(result, "transaction_ref_hash", value.transaction_ref_hash);
                    Add(result, "idempotency_key_hash", value.idempotency_key_hash);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.RankSnapshot:
                case AnalyticsEventNamesV2.RankChange:
                    Add(result, "rank_value", value.rank_value);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.LeaderboardSnapshot:
                    Add(result, "leaderboard_period", value.leaderboard_period);
                    Add(result, "rank_value", value.rank_value);
                    break;
                case AnalyticsEventNamesV2.SettlementGold:
                    Add(result, "gold_amount", value.gold_amount);
                    Add(result, "reason", value.reason);
                    break;
                case AnalyticsEventNamesV2.EmergencySave:
                    Add(result, "result", value.result);
                    Add(result, "reason", value.reason);
                    break;
            }

            return result;
        }

        private static void Add(List<FirebaseAnalyticsParameterV2> result, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(new FirebaseAnalyticsParameterV2(name, value));
            }
        }

        private static void Add(List<FirebaseAnalyticsParameterV2> result, string name, int value)
        {
            result.Add(new FirebaseAnalyticsParameterV2(name, (long)value));
        }

        private static void Add(List<FirebaseAnalyticsParameterV2> result, string name, long value)
        {
            result.Add(new FirebaseAnalyticsParameterV2(name, value));
        }

        private static void Add(List<FirebaseAnalyticsParameterV2> result, string name, float value)
        {
            result.Add(new FirebaseAnalyticsParameterV2(name, (double)value));
        }

        private static void AddPositive(List<FirebaseAnalyticsParameterV2> result, string name, float value)
        {
            if (value > 0f)
            {
                Add(result, name, value);
            }
        }
    }
}
