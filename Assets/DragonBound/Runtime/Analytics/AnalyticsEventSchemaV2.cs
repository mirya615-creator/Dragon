using System;
using System.Collections.Generic;
using System.Globalization;

namespace DragonBound.Analytics
{
    public static class AnalyticsExecutionContexts
    {
        public const string LivePlayerVsAi = "live_player_vs_ai";
        public const string DiagnosticAiVsAi = "diagnostic_ai_vs_ai";
        public const string HeroSliceShowcase = "hero_slice_showcase";

        public static bool IsKnown(string value)
        {
            return value == LivePlayerVsAi ||
                   value == DiagnosticAiVsAi ||
                   value == HeroSliceShowcase;
        }
    }

    public static class AnalyticsSides
    {
        public const string Player = "player";
        public const string Ai = "ai";
        public const string System = "system";

        public static bool IsKnown(string value)
        {
            return value == Player || value == Ai || value == System;
        }
    }

    public static class AnalyticsEnemyTypes
    {
        public const string Normal = "normal";
        public const string Boss = "boss";
        public const string BossSummon = "boss_summon";

        public static bool IsKnown(string value)
        {
            return value == Normal || value == Boss || value == BossSummon;
        }
    }

    public static class AnalyticsComponentPolicies
    {
        public const string RecruitComponentPolicyV3 = "recruit_component_policy_v3";

        public static bool IsKnown(string value)
        {
            return value == RecruitComponentPolicyV3;
        }
    }

    public static class AnalyticsRankTiers
    {
        public const string Unranked = "unranked";
        public const string Bronze = "bronze";
        public const string Silver = "silver";
        public const string Gold = "gold";
        public const string Platinum = "platinum";
        public const string Diamond = "diamond";
        public const string Master = "master";
        public const string Legend = "legend";

        public static bool IsKnown(string value)
        {
            switch (value)
            {
                case Unranked:
                case Bronze:
                case Silver:
                case Gold:
                case Platinum:
                case Diamond:
                case Master:
                case Legend:
                    return true;
                default:
                    return false;
            }
        }
    }

    public static class AnalyticsAiDifficulties
    {
        public const string None = "none";
        public const string Easy = "easy";
        public const string Standard = "standard";
        public const string Hard = "hard";
        public const string Elite = "elite";

        public static bool IsKnown(string value)
        {
            switch (value)
            {
                case None:
                case Easy:
                case Standard:
                case Hard:
                case Elite:
                    return true;
                default:
                    return false;
            }
        }
    }

    public static class AnalyticsLedgerStatuses
    {
        public const string Accepted = "accepted";
        public const string Duplicate = "duplicate";
        public const string Rejected = "rejected";
        public const string Timeout = "timeout";
        public const string OfflineQueued = "offline_queued";
        public const string Restored = "restored";

        public static bool IsKnown(string value)
        {
            switch (value)
            {
                case Accepted:
                case Duplicate:
                case Rejected:
                case Timeout:
                case OfflineQueued:
                case Restored:
                    return true;
                default:
                    return false;
            }
        }
    }

    public static class AnalyticsRuneOperationResults
    {
        public const string Accepted = "accepted";
        public const string Rejected = "rejected";

        public static bool IsKnown(string value)
        {
            return value == Accepted || value == Rejected;
        }
    }

    public static class AnalyticsRuneGateStates
    {
        public const string Locked = "locked";

        public static bool IsKnown(string value)
        {
            return value == Locked;
        }
    }

    public static class AnalyticsRuneRewardStates
    {
        public const string Pending = "pending";
        public const string Granted = "granted";
        public const string Rejected = "rejected";

        public static bool IsKnown(string value)
        {
            return value == Pending || value == Granted || value == Rejected;
        }
    }

    public static class AnalyticsEventNamesV2
    {
        public const string RunStart = "run_start";
        public const string WaveStart = "wave_start";
        public const string WaveFinish = "wave_finish";
        public const string EnemySpawn = "enemy_spawn";
        public const string EnemyGoal = "enemy_goal";
        public const string RecruitResult = "recruit_result";
        public const string FormationSnapshot = "formation_snapshot";
        public const string HeroFormed = "hero_formed";
        public const string HeroXp = "hero_xp";
        public const string HeroLevelUp = "hero_level_up";
        public const string LastHit = "last_hit";
        public const string BossSpawn = "boss_spawn";
        public const string BossSkill = "boss_skill";
        public const string BossSummon = "boss_summon";
        public const string BossDamageWindow = "boss_damage_window";
        public const string BossKill = "boss_kill";
        public const string BossGoal = "boss_goal";
        public const string HeartLost = "heart_lost";
        public const string DeathWave = "death_wave";
        public const string MatchFinish = "match_finish";
        public const string ItemGrant = "item_grant";
        public const string ItemEquip = "item_equip";
        public const string ItemUse = "item_use";
        public const string RuneGrant = "rune_grant";
        public const string RuneEquip = "rune_equip";
        public const string RuneLoadoutAssign = "rune_loadout_assign";
        public const string RuneLoadoutUnequip = "rune_loadout_unequip";
        public const string RuneCraft = "rune_craft";
        public const string RuneGateRejection = "rune_gate_rejection";
        public const string RuneRewardPending = "rune_reward_pending";
        public const string RuneRewardGranted = "rune_reward_granted";
        public const string RuneRewardRejected = "rune_reward_rejected";
        public const string EnergySpend = "energy_spend";
        public const string EnergyGrant = "energy_grant";
        public const string AdRequest = "ad_request";
        public const string AdResult = "ad_result";
        public const string MerchantOpen = "merchant_open";
        public const string MerchantOffer = "merchant_offer";
        public const string MerchantPurchase = "merchant_purchase";
        public const string LedgerResult = "ledger_result";
        public const string RankSnapshot = "rank_snapshot";
        public const string RankChange = "rank_change";
        public const string LeaderboardSnapshot = "leaderboard_snapshot";
        public const string SettlementGold = "settlement_gold";
        public const string EmergencySave = "emergency_save";

        public static readonly string[] All =
        {
            RunStart,
            WaveStart,
            WaveFinish,
            EnemySpawn,
            EnemyGoal,
            RecruitResult,
            FormationSnapshot,
            HeroFormed,
            HeroXp,
            HeroLevelUp,
            LastHit,
            BossSpawn,
            BossSkill,
            BossSummon,
            BossDamageWindow,
            BossKill,
            BossGoal,
            HeartLost,
            DeathWave,
            MatchFinish,
            ItemGrant,
            ItemEquip,
            ItemUse,
            RuneGrant,
            RuneEquip,
            RuneLoadoutAssign,
            RuneLoadoutUnequip,
            RuneCraft,
            RuneGateRejection,
            RuneRewardPending,
            RuneRewardGranted,
            RuneRewardRejected,
            EnergySpend,
            EnergyGrant,
            AdRequest,
            AdResult,
            MerchantOpen,
            MerchantOffer,
            MerchantPurchase,
            LedgerResult,
            RankSnapshot,
            RankChange,
            LeaderboardSnapshot,
            SettlementGold,
            EmergencySave
        };

        public static bool IsKnown(string value)
        {
            for (var index = 0; index < All.Length; index++)
            {
                if (All[index] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class AnalyticsSchemaV2
    {
        public const int EventVersion = 2;
        public const int MaxWave = 20;
        public const int CardsPerRecruitment = 5;
        public const int MaxRecruitComponents = 3;

        public static bool TryValidate(AnalyticsEventV2 value, out string error)
        {
            if (value == null)
            {
                error = "event is required";
                return false;
            }

            if (!AnalyticsEventNamesV2.IsKnown(value.event_name))
            {
                error = "event_name is not registered in Analytics Event Schema V2";
                return false;
            }

            if (value.event_version != EventVersion)
            {
                error = "event_version is not supported";
                return false;
            }

            if (string.IsNullOrWhiteSpace(value.event_id) ||
                string.IsNullOrWhiteSpace(value.run_id) ||
                string.IsNullOrWhiteSpace(value.config_version) ||
                string.IsNullOrWhiteSpace(value.build_version))
            {
                error = "event_id, run_id, config_version and build_version are required";
                return false;
            }

            if (value.sequence < 1)
            {
                error = "sequence must be positive";
                return false;
            }

            if (value.wave < 0 || value.wave > MaxWave)
            {
                error = "wave must be between 0 and 20";
                return false;
            }

            if (!AnalyticsExecutionContexts.IsKnown(value.execution_context))
            {
                error = "execution_context is invalid";
                return false;
            }

            if (!AnalyticsSides.IsKnown(value.side))
            {
                error = "side is invalid";
                return false;
            }

            if (!AnalyticsRankTiers.IsKnown(value.rank_tier))
            {
                error = "rank_tier is invalid";
                return false;
            }

            if (!AnalyticsAiDifficulties.IsKnown(value.ai_difficulty))
            {
                error = "ai_difficulty is invalid";
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

            return HasRequiredPayload(value, out error);
        }

        private static bool HasRequiredPayload(AnalyticsEventV2 value, out string error)
        {
            switch (value.event_name)
            {
                case AnalyticsEventNamesV2.EnemySpawn:
                case AnalyticsEventNamesV2.EnemyGoal:
                    if (!AnalyticsEnemyTypes.IsKnown(value.enemy_type) || string.IsNullOrWhiteSpace(value.enemy_id))
                    {
                        return Missing("enemy_type and enemy_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RecruitResult:
                    if (value.recruitment_number < 1 ||
                        value.component_count < 0 ||
                        value.component_count > MaxRecruitComponents ||
                        value.basic_count < 1 ||
                        value.forge_pick_count < 0 ||
                        value.component_count + value.basic_count + value.forge_pick_count != CardsPerRecruitment ||
                        !AnalyticsComponentPolicies.IsKnown(value.component_policy) ||
                        value.remaining_component_bag < 0)
                    {
                        return Missing("valid recruitment_number, counts, component_policy and remaining_component_bag", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.FormationSnapshot:
                    if (string.IsNullOrWhiteSpace(value.snapshot_reason) ||
                        value.basic_unit_count < 0 ||
                        value.hero_count < 0 ||
                        value.component_unit_count < 0 ||
                        value.board_occupied < 0 ||
                        value.bench_occupied < 0 ||
                        value.hittable_unit_count < 0)
                    {
                        return Missing("snapshot_reason and non-negative formation counts", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.HeroFormed:
                    if (string.IsNullOrWhiteSpace(value.hero_id))
                    {
                        return Missing("hero_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.HeroXp:
                    if (string.IsNullOrWhiteSpace(value.hero_id) || value.xp_amount <= 0)
                    {
                        return Missing("hero_id and positive xp_amount", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.HeroLevelUp:
                    if (string.IsNullOrWhiteSpace(value.hero_id) || value.hero_level < 1)
                    {
                        return Missing("hero_id and positive hero_level", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.LastHit:
                    if (string.IsNullOrWhiteSpace(value.enemy_id) || string.IsNullOrWhiteSpace(value.source_unit_id))
                    {
                        return Missing("enemy_id and source_unit_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.BossSpawn:
                    if (string.IsNullOrWhiteSpace(value.boss_id) ||
                        value.enemy_type != AnalyticsEnemyTypes.Boss ||
                        value.move_speed_cells_per_second <= 0f ||
                        value.max_hit_points <= 0f)
                    {
                        return Missing("boss_id, boss enemy_type, move speed and max_hit_points", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.BossSkill:
                    if (string.IsNullOrWhiteSpace(value.boss_id) ||
                        string.IsNullOrWhiteSpace(value.skill_id) ||
                        value.count < 1)
                    {
                        return Missing("boss_id, skill_id and cast count", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.BossSummon:
                    if (string.IsNullOrWhiteSpace(value.boss_id) ||
                        string.IsNullOrWhiteSpace(value.summon_id) ||
                        value.count < 1 ||
                        value.enemy_type != AnalyticsEnemyTypes.BossSummon)
                    {
                        return Missing("boss_id, summon_id, count and boss_summon enemy_type", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.BossDamageWindow:
                    if (string.IsNullOrWhiteSpace(value.boss_id) ||
                        string.IsNullOrWhiteSpace(value.damage_window_id) ||
                        value.duration_seconds <= 0f)
                    {
                        return Missing("boss_id, damage_window_id and duration_seconds", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.BossKill:
                    if (string.IsNullOrWhiteSpace(value.boss_id) || value.duration_seconds < 0f)
                    {
                        return Missing("boss_id and non-negative duration_seconds", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.BossGoal:
                    if (string.IsNullOrWhiteSpace(value.boss_id) || value.heart_after != 0)
                    {
                        return Missing("boss_id and heart_after=0 instant defeat", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.HeartLost:
                    if (value.count <= 0 || string.IsNullOrWhiteSpace(value.reason) || value.heart_after < 0)
                    {
                        return Missing("positive count, reason and heart_after", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.DeathWave:
                    if (value.wave < 1 || string.IsNullOrWhiteSpace(value.reason))
                    {
                        return Missing("death wave and reason", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.MatchFinish:
                    if (string.IsNullOrWhiteSpace(value.match_result) || string.IsNullOrWhiteSpace(value.reason))
                    {
                        return Missing("match_result and reason", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.ItemGrant:
                case AnalyticsEventNamesV2.ItemEquip:
                case AnalyticsEventNamesV2.ItemUse:
                    if (string.IsNullOrWhiteSpace(value.item_id))
                    {
                        return Missing("item_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneGrant:
                case AnalyticsEventNamesV2.RuneEquip:
                    if (string.IsNullOrWhiteSpace(value.rune_id))
                    {
                        return Missing("rune_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneLoadoutAssign:
                    if (string.IsNullOrWhiteSpace(value.hero_id) ||
                        string.IsNullOrWhiteSpace(value.rune_id) ||
                        !AnalyticsRuneOperationResults.IsKnown(value.operation_result))
                    {
                        return Missing("hero_id, rune_id and operation_result", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneLoadoutUnequip:
                    if (string.IsNullOrWhiteSpace(value.hero_id) ||
                        !AnalyticsRuneOperationResults.IsKnown(value.operation_result))
                    {
                        return Missing("hero_id and operation_result", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneCraft:
                    if (string.IsNullOrWhiteSpace(value.rune_id) ||
                        !AnalyticsRuneOperationResults.IsKnown(value.operation_result))
                    {
                        return Missing("rune_id and operation_result", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneGateRejection:
                    if (!AnalyticsRuneGateStates.IsKnown(value.gate_state) ||
                        string.IsNullOrWhiteSpace(value.rune_operation) ||
                        value.account_day < 1 ||
                        string.IsNullOrWhiteSpace(value.reason))
                    {
                        return Missing("rune_operation, locked gate_state, account_day and reason", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneRewardPending:
                    if (value.reward_wave < 1 ||
                        value.reward_state != AnalyticsRuneRewardStates.Pending)
                    {
                        return Missing("positive reward_wave and pending reward_state", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneRewardGranted:
                    if (value.reward_wave < 1 ||
                        string.IsNullOrWhiteSpace(value.rune_id) ||
                        !AnalyticsRuneRewardStates.IsKnown(value.reward_state) ||
                        value.reward_state != AnalyticsRuneRewardStates.Granted)
                    {
                        return Missing("positive reward_wave, rune_id and granted reward_state", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RuneRewardRejected:
                    if (value.reward_wave < 1 ||
                        !AnalyticsRuneRewardStates.IsKnown(value.reward_state) ||
                        value.reward_state != AnalyticsRuneRewardStates.Rejected ||
                        string.IsNullOrWhiteSpace(value.reason))
                    {
                        return Missing("positive reward_wave, rejected reward_state and reason", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.EnergySpend:
                case AnalyticsEventNamesV2.EnergyGrant:
                    if (value.energy_amount <= 0 || string.IsNullOrWhiteSpace(value.reason))
                    {
                        return Missing("positive energy_amount and reason", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.AdRequest:
                    if (string.IsNullOrWhiteSpace(value.ad_point_id))
                    {
                        return Missing("ad_point_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.AdResult:
                    if (string.IsNullOrWhiteSpace(value.ad_point_id) || string.IsNullOrWhiteSpace(value.result))
                    {
                        return Missing("ad_point_id and result", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.MerchantOpen:
                    if (string.IsNullOrWhiteSpace(value.merchant_id))
                    {
                        return Missing("merchant_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.MerchantOffer:
                case AnalyticsEventNamesV2.MerchantPurchase:
                    if (string.IsNullOrWhiteSpace(value.merchant_id) || string.IsNullOrWhiteSpace(value.offer_id))
                    {
                        return Missing("merchant_id and offer_id", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.LedgerResult:
                    if (string.IsNullOrWhiteSpace(value.ledger_operation) ||
                        !AnalyticsLedgerStatuses.IsKnown(value.ledger_status) ||
                        !HasNonSensitiveLedgerReference(value))
                    {
                        return Missing("ledger_operation, ledger_status and hashed ledger reference", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.RankSnapshot:
                case AnalyticsEventNamesV2.RankChange:
                    if (value.rank_value < 0)
                    {
                        return Missing("non-negative rank_value", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.LeaderboardSnapshot:
                    if (string.IsNullOrWhiteSpace(value.leaderboard_period) || value.rank_value < 0)
                    {
                        return Missing("leaderboard_period and non-negative rank_value", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.SettlementGold:
                    if (value.gold_amount < 0 || string.IsNullOrWhiteSpace(value.reason))
                    {
                        return Missing("gold_amount and reason", out error);
                    }
                    break;
                case AnalyticsEventNamesV2.EmergencySave:
                    if (string.IsNullOrWhiteSpace(value.result) || string.IsNullOrWhiteSpace(value.reason))
                    {
                        return Missing("result and reason", out error);
                    }
                    break;
            }

            error = string.Empty;
            return true;
        }

        private static bool HasNonSensitiveLedgerReference(AnalyticsEventV2 value)
        {
            return IsHashedReference(value.transaction_ref_hash) ||
                   IsHashedReference(value.idempotency_key_hash);
        }

        private static bool IsHashedReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 12)
            {
                return false;
            }

            var lowered = value.ToLowerInvariant();
            return lowered.IndexOf("token", StringComparison.Ordinal) < 0 &&
                   lowered.IndexOf("secret", StringComparison.Ordinal) < 0 &&
                   lowered.IndexOf("bearer", StringComparison.Ordinal) < 0;
        }

        private static bool Missing(string fields, out string error)
        {
            error = fields + " is required for this event";
            return false;
        }
    }

    [Serializable]
    public sealed class AnalyticsEventV2
    {
        public string event_name;
        public int event_version = AnalyticsSchemaV2.EventVersion;
        public string event_id;
        public string client_timestamp;
        public string run_id;
        public int run_seed;
        public string execution_context;
        public string config_version;
        public string build_version;
        public string side;
        public int wave;
        public string rank_tier;
        public string ai_difficulty;
        public long sequence;

        public string result;
        public string reason;
        public string match_result;
        public float elapsed_seconds;
        public float duration_seconds;
        public int count;

        public string enemy_type;
        public string enemy_id;
        public float move_speed_cells_per_second;
        public float max_hit_points;

        public string boss_id;
        public string skill_id;
        public string summon_id;
        public string damage_window_id;
        public float damage;

        public int recruitment_number;
        public int component_count;
        public int basic_count;
        public int forge_pick_count;
        public string component_policy;
        public int remaining_component_bag;

        public string snapshot_reason;
        public int basic_unit_count;
        public int hero_count;
        public int component_unit_count;
        public int board_occupied;
        public int bench_occupied;
        public int hittable_unit_count;

        public string hero_id;
        public int hero_level;
        public int xp_amount;
        public string source_unit_id;

        public string item_id;
        public string rune_id;
        public string rune_operation;
        public string operation_result;
        public string gate_state;
        public string reward_state;
        public int account_day;
        public int reward_wave;
        public string rune_rarity;
        public string reward_form;
        public int energy_amount;
        public string ad_point_id;
        public string merchant_id;
        public string offer_id;
        public string currency_type;
        public int gold_amount;

        public string ledger_operation;
        public string ledger_status;
        public string transaction_ref_hash;
        public string idempotency_key_hash;

        public int heart_before;
        public int heart_after;
        public int rank_value;
        public string leaderboard_period;

        public AnalyticsEventV2 Clone()
        {
            return (AnalyticsEventV2)MemberwiseClone();
        }
    }

    public static class AnalyticsEventV2Factory
    {
        public static AnalyticsEventV2 Create(
            string eventName,
            string eventId,
            string runId,
            int runSeed,
            string executionContext,
            string side,
            int wave,
            string rankTier,
            string aiDifficulty,
            long sequence,
            string configVersion,
            string buildVersion,
            DateTime utcTimestamp)
        {
            return new AnalyticsEventV2
            {
                event_name = eventName,
                event_id = eventId,
                client_timestamp = utcTimestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                run_id = runId,
                run_seed = runSeed,
                execution_context = executionContext,
                config_version = configVersion,
                build_version = buildVersion,
                side = side,
                wave = wave,
                rank_tier = rankTier,
                ai_difficulty = aiDifficulty,
                sequence = sequence
            };
        }
    }

    public interface IAnalyticsSinkV2
    {
        int WriteErrorCount { get; }
        bool Record(AnalyticsEventV2 value);
        void Flush();
    }

    public sealed class InMemoryAnalyticsSinkV2 : IAnalyticsSinkV2
    {
        private readonly List<AnalyticsEventV2> events = new List<AnalyticsEventV2>();

        public int WriteErrorCount { get; private set; }
        public IReadOnlyList<AnalyticsEventV2> Events
        {
            get { return events; }
        }

        public bool Record(AnalyticsEventV2 value)
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

    public enum AnalyticsRecordResultV2
    {
        Accepted,
        Duplicate,
        Invalid,
        OutOfOrder,
        RunSeedMismatch,
        ExecutionContextMismatch,
        SinkFailure
    }

    public sealed class AnalyticsRecorderV2
    {
        private readonly IAnalyticsSinkV2 sink;
        private readonly HashSet<string> recordedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> nextSequenceByRun = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> seedByRun = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> contextByRun = new Dictionary<string, string>(StringComparer.Ordinal);

        public AnalyticsRecorderV2(IAnalyticsSinkV2 sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            this.sink = sink;
        }

        public AnalyticsRecordResultV2 Record(AnalyticsEventV2 value, out string error)
        {
            if (!AnalyticsSchemaV2.TryValidate(value, out error))
            {
                return AnalyticsRecordResultV2.Invalid;
            }

            if (recordedIds.Contains(value.event_id))
            {
                error = string.Empty;
                return AnalyticsRecordResultV2.Duplicate;
            }

            int registeredSeed;
            if (seedByRun.TryGetValue(value.run_id, out registeredSeed) && registeredSeed != value.run_seed)
            {
                error = "run_seed changed within the same run_id";
                return AnalyticsRecordResultV2.RunSeedMismatch;
            }

            string registeredContext;
            if (contextByRun.TryGetValue(value.run_id, out registeredContext) &&
                registeredContext != value.execution_context)
            {
                error = "execution_context changed within the same run_id";
                return AnalyticsRecordResultV2.ExecutionContextMismatch;
            }

            long expectedSequence;
            if (!nextSequenceByRun.TryGetValue(value.run_id, out expectedSequence))
            {
                expectedSequence = 1;
            }

            if (value.sequence != expectedSequence)
            {
                error = "sequence must be the next value for its run_id";
                return AnalyticsRecordResultV2.OutOfOrder;
            }

            if (!sink.Record(value))
            {
                error = "analytics sink rejected the event";
                return AnalyticsRecordResultV2.SinkFailure;
            }

            recordedIds.Add(value.event_id);
            seedByRun[value.run_id] = value.run_seed;
            contextByRun[value.run_id] = value.execution_context;
            nextSequenceByRun[value.run_id] = expectedSequence + 1;
            error = string.Empty;
            return AnalyticsRecordResultV2.Accepted;
        }

        public void Flush()
        {
            sink.Flush();
        }
    }
}
