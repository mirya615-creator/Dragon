using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Runes;

namespace DragonBound.Analytics
{
    /// <summary>Immutable V2 envelope allocated once by the run/bootstrap owner.</summary>
    public sealed class DrakeforgeAnalyticsRunContext
    {
        public DrakeforgeAnalyticsRunContext(
            string runId,
            int runSeed,
            string executionContext,
            string configVersion,
            string buildVersion,
            string rankTier,
            string aiDifficulty,
            string aiProfile = "",
            string aiAlgorithmVersion = "",
            int aiDecisionSeed = 0,
            bool aiRecoveryMatch = false,
            int playerRankLevel = 0)
        {
            RunId = runId ?? string.Empty;
            RunSeed = runSeed;
            ExecutionContext = executionContext ?? string.Empty;
            ConfigVersion = configVersion ?? string.Empty;
            BuildVersion = buildVersion ?? string.Empty;
            RankTier = rankTier ?? string.Empty;
            AiDifficulty = aiDifficulty ?? string.Empty;
            AiProfile = aiProfile ?? string.Empty;
            AiAlgorithmVersion = aiAlgorithmVersion ?? string.Empty;
            AiDecisionSeed = aiDecisionSeed;
            AiRecoveryMatch = aiRecoveryMatch;
            PlayerRankLevel = playerRankLevel;
        }

        public string RunId { get; }
        public int RunSeed { get; }
        public string ExecutionContext { get; }
        public string ConfigVersion { get; }
        public string BuildVersion { get; }
        public string RankTier { get; }
        public string AiDifficulty { get; }
        public string AiProfile { get; }
        public string AiAlgorithmVersion { get; }
        public int AiDecisionSeed { get; }
        public bool AiRecoveryMatch { get; }
        public int PlayerRankLevel { get; }
    }

    /// <summary>
    /// Observes public gameplay seams and converts them to V2 evidence. It never owns gameplay
    /// state and only emits combat damage when an explicit aggregate window is closed.
    /// </summary>
    public sealed class DrakeforgeAnalyticsAdapterV1
    {
        public const string StormcallerSkillId = "stormcaller_priest_stormcall";
        public const string EmptySnapshotId = "none";

        private sealed class DamageAggregate
        {
            public float Shield;
            public float Health;
        }

        private readonly AnalyticsRecorderV2 recorder;
        private readonly DrakeforgeAnalyticsRunContext context;
        private readonly Func<DateTime> utcNow;
        private readonly Dictionary<string, float> w12SpawnTimeBySide =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, DamageAggregate> damageBySide =
            new Dictionary<string, DamageAggregate>(StringComparer.Ordinal);
        private long nextSequence = 1;
        private TwentyWavePressureRuntime attachedRuntime;
        private Action<EnemyLifecycleEvent> playerLifecycleHandler;
        private Action<EnemyLifecycleEvent> aiLifecycleHandler;
        private Action<TeamSide, StormcallerCastEvent> stormcallerHandler;
        private Action<CombatEvent> combatHandler;
        private Action<RuneReward> runeRewardHandler;

        public DrakeforgeAnalyticsAdapterV1(
            AnalyticsRecorderV2 recorder,
            DrakeforgeAnalyticsRunContext context,
            Func<DateTime> utcNow = null)
        {
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public string LastError { get; private set; } = string.Empty;
        public long NextSequence => nextSequence;

        public bool RecordRunStart()
        {
            return Record(AnalyticsEventNamesV2.RunStart, AnalyticsSides.System, 0, null);
        }

        public bool RecordW12Lifecycle(TeamSide side, EnemyLifecycleEvent value, float elapsedSeconds)
        {
            if (value.Archetype != EnemyArchetype.Boss || value.SpawnWave != 12)
            {
                LastError = "Only W12 Boss lifecycle events are accepted by this adapter.";
                return false;
            }

            var analyticsSide = ToAnalyticsSide(side);
            var wave = value.SpawnWave;
            if (value.Kind == EnemyLifecycleEventKind.Spawned)
            {
                w12SpawnTimeBySide[analyticsSide] = Math.Max(0f, elapsedSeconds);
                return Record(AnalyticsEventNamesV2.BossSpawn, analyticsSide, wave, valueToRecord =>
                {
                    valueToRecord.boss_id = StormcallerPriestConfiguration.BossId;
                    valueToRecord.enemy_type = AnalyticsEnemyTypes.Boss;
                    valueToRecord.move_speed_cells_per_second = StormcallerPriestConfiguration.BossMoveSpeedCellsPerSecond;
                    valueToRecord.max_hit_points = value.MaxHitPoints;
                });
            }

            float spawnTime;
            w12SpawnTimeBySide.TryGetValue(analyticsSide, out spawnTime);
            var duration = Math.Max(0f, elapsedSeconds - spawnTime);
            if (value.Kind == EnemyLifecycleEventKind.Killed)
            {
                var flushed = FlushDamageWindow(side, wave, "spawn_to_kill", duration);
                var recorded = Record(AnalyticsEventNamesV2.BossKill, analyticsSide, wave, valueToRecord =>
                {
                    valueToRecord.boss_id = StormcallerPriestConfiguration.BossId;
                    valueToRecord.duration_seconds = duration;
                });
                return flushed && recorded;
            }

            if (value.Kind == EnemyLifecycleEventKind.Leaked)
            {
                var flushed = FlushDamageWindow(side, wave, "spawn_to_goal", duration);
                var recorded = Record(AnalyticsEventNamesV2.BossGoal, analyticsSide, wave, valueToRecord =>
                {
                    valueToRecord.boss_id = StormcallerPriestConfiguration.BossId;
                    valueToRecord.heart_after = 0;
                });
                return flushed && recorded;
            }

            LastError = "Unknown W12 lifecycle event.";
            return false;
        }

        public bool RecordStormcallerCast(TeamSide side, int wave, StormcallerCastEvent value)
        {
            string eventName;
            string result;
            switch (value.Kind)
            {
                case StormcallerCastEventKind.CastStarted:
                    eventName = AnalyticsEventNamesV2.BossSkill;
                    result = "started";
                    break;
                case StormcallerCastEventKind.EffectApplied:
                    eventName = AnalyticsEventNamesV2.BossSkill;
                    result = "resolved";
                    break;
                case StormcallerCastEventKind.CastFailed:
                    eventName = AnalyticsEventNamesV2.BossSkill;
                    result = "blocked";
                    break;
                default:
                    return true;
            }

            var analyticsSide = ToAnalyticsSide(side);
            var cast = Record(eventName, analyticsSide, wave, valueToRecord =>
            {
                valueToRecord.boss_id = StormcallerPriestConfiguration.BossId;
                valueToRecord.skill_id = StormcallerSkillId;
                valueToRecord.count = value.CastNumber;
                valueToRecord.result = result;
                valueToRecord.elapsed_seconds = value.ElapsedSeconds;
                valueToRecord.damage = value.ReflectionDamage;
                valueToRecord.reason = value.Kind == StormcallerCastEventKind.CastFailed
                    ? "spellbreaker_reflection"
                    : string.Empty;
            });
            return cast;
        }

        public void AccumulateCombatDamage(TeamSide side, CombatEvent value)
        {
            if (value.ShieldDamage <= 0f && value.HealthDamage <= 0f)
            {
                return;
            }

            var analyticsSide = ToAnalyticsSide(side);
            DamageAggregate aggregate;
            if (!damageBySide.TryGetValue(analyticsSide, out aggregate))
            {
                aggregate = new DamageAggregate();
                damageBySide.Add(analyticsSide, aggregate);
            }

            aggregate.Shield += Math.Max(0f, value.ShieldDamage);
            aggregate.Health += Math.Max(0f, value.HealthDamage);
        }

        public bool FlushDamageWindow(TeamSide side, int wave, string windowId, float durationSeconds)
        {
            var analyticsSide = ToAnalyticsSide(side);
            DamageAggregate aggregate;
            if (!damageBySide.TryGetValue(analyticsSide, out aggregate))
            {
                return true;
            }

            var recorded = true;
            var totalDamage = aggregate.Shield + aggregate.Health;
            recorded &= Record(AnalyticsEventNamesV2.BossDamageWindow, analyticsSide, wave, valueToRecord =>
            {
                valueToRecord.boss_id = StormcallerPriestConfiguration.BossId;
                valueToRecord.damage_window_id = windowId + ":total";
                valueToRecord.duration_seconds = Math.Max(0.0001f, durationSeconds);
                valueToRecord.damage = totalDamage;
                valueToRecord.result = "total_damage";
            });
            if (aggregate.Shield > 0f)
            {
                recorded &= Record(AnalyticsEventNamesV2.BossDamageWindow, analyticsSide, wave, valueToRecord =>
                {
                    valueToRecord.boss_id = StormcallerPriestConfiguration.BossId;
                    valueToRecord.damage_window_id = windowId + ":shield";
                    valueToRecord.duration_seconds = Math.Max(0f, durationSeconds);
                    valueToRecord.damage = aggregate.Shield;
                    valueToRecord.result = "shield_damage";
                });
            }

            if (aggregate.Health > 0f)
            {
                recorded &= Record(AnalyticsEventNamesV2.BossDamageWindow, analyticsSide, wave, valueToRecord =>
                {
                    valueToRecord.boss_id = StormcallerPriestConfiguration.BossId;
                    valueToRecord.damage_window_id = windowId + ":health";
                    valueToRecord.duration_seconds = Math.Max(0f, durationSeconds);
                    valueToRecord.damage = aggregate.Health;
                    valueToRecord.result = "health_damage";
                });
            }

            aggregate.Shield = 0f;
            aggregate.Health = 0f;
            return recorded;
        }

        public bool RecordItemSnapshotLocked(TeamSide side, int wave, ItemRunSnapshot snapshot)
        {
            if (snapshot == null)
            {
                LastError = "Item snapshot is required.";
                return false;
            }

            var itemIds = new List<string>();
            itemIds.AddRange(snapshot.ActiveItems);
            itemIds.AddRange(snapshot.PassiveItems);
            if (itemIds.Count == 0)
            {
                itemIds.Add(EmptySnapshotId);
            }

            var recorded = true;
            for (var index = 0; index < itemIds.Count; index++)
            {
                var itemId = itemIds[index];
                recorded &= Record(AnalyticsEventNamesV2.ItemEquip, ToAnalyticsSide(side), wave, valueToRecord =>
                {
                    valueToRecord.item_id = itemId;
                    valueToRecord.snapshot_reason = "run_start";
                    valueToRecord.result = "snapshot_locked";
                });
            }
            return recorded;
        }

        public bool RecordItemCommand(TeamSide side, int wave, string itemId, string command)
        {
            return Record(AnalyticsEventNamesV2.ItemUse, ToAnalyticsSide(side), wave, valueToRecord =>
            {
                valueToRecord.item_id = itemId;
                valueToRecord.result = "item_command";
                valueToRecord.reason = command;
            });
        }

        public bool RecordItemResult(TeamSide side, int wave, string itemId, bool accepted, string reason)
        {
            return Record(AnalyticsEventNamesV2.ItemUse, ToAnalyticsSide(side), wave, valueToRecord =>
            {
                valueToRecord.item_id = itemId;
                valueToRecord.result = "item_result";
                valueToRecord.reason = accepted ? "accepted" : "rejected:" + (reason ?? "unknown");
            });
        }

        public bool RecordItemCooldown(TeamSide side, int wave, string itemId, float remainingSeconds)
        {
            return Record(AnalyticsEventNamesV2.ItemUse, ToAnalyticsSide(side), wave, valueToRecord =>
            {
                valueToRecord.item_id = itemId;
                valueToRecord.result = "cooldown";
                valueToRecord.duration_seconds = Math.Max(0f, remainingSeconds);
            });
        }

        public bool RecordRuneLoadoutSnapshotLocked(TeamSide side, int wave, RuneLoadoutSnapshot snapshot)
        {
            if (snapshot == null)
            {
                LastError = "Rune snapshot is required.";
                return false;
            }

            if (snapshot.Assignments.Count == 0)
            {
                return Record(AnalyticsEventNamesV2.RuneEquip, ToAnalyticsSide(side), wave, valueToRecord =>
                {
                    valueToRecord.snapshot_reason = "run_start_empty";
                    valueToRecord.count = 0;
                    valueToRecord.rune_id = EmptySnapshotId;
                    valueToRecord.result = "loadout_snapshot_locked";
                });
            }

            var recorded = true;
            foreach (var assignment in snapshot.Assignments)
            {
                var runeId = assignment.Value;
                recorded &= Record(AnalyticsEventNamesV2.RuneEquip, ToAnalyticsSide(side), wave, valueToRecord =>
                {
                    valueToRecord.rune_id = runeId;
                    valueToRecord.hero_id = assignment.Key;
                    valueToRecord.snapshot_reason = "run_start";
                    valueToRecord.count = 1;
                    valueToRecord.result = "loadout_snapshot_locked";
                });
            }
            return recorded;
        }

        public bool RecordRuneReward(TeamSide side, RuneReward reward)
        {
            if (reward == null)
            {
                LastError = "Rune reward is required.";
                return false;
            }

            return Record(AnalyticsEventNamesV2.RuneGrant, ToAnalyticsSide(side), reward.Wave, valueToRecord =>
            {
                valueToRecord.rune_id = reward.RuneId;
                valueToRecord.count = 1;
                valueToRecord.result = "rune_reward";
                valueToRecord.reason = reward.IsComplete ? "complete" : "fragment";
            });
        }

        public bool RecordRuneGateRejected(TeamSide side, int wave, string reason)
        {
            return Record(AnalyticsEventNamesV2.RuneGrant, ToAnalyticsSide(side), wave, valueToRecord =>
            {
                valueToRecord.rune_id = EmptySnapshotId;
                valueToRecord.result = "rune_gate_rejected";
                valueToRecord.reason = reason;
            });
        }

        public bool RecordCalibrationSample(
            TeamSide side,
            int wave,
            string cohort,
            float candidateHitPoints,
            string earlyEndReason)
        {
            if (context.ExecutionContext != AnalyticsExecutionContexts.DiagnosticAiVsAi)
            {
                LastError = "Calibration samples require diagnostic_ai_vs_ai execution_context.";
                return false;
            }

            return Record(AnalyticsEventNamesV2.BossDamageWindow, ToAnalyticsSide(side), wave, valueToRecord =>
            {
                valueToRecord.boss_id = "CALIBRATION";
                valueToRecord.damage_window_id = "calibration_sample";
                valueToRecord.duration_seconds = 0.0001f;
                valueToRecord.max_hit_points = candidateHitPoints;
                valueToRecord.result = cohort;
                valueToRecord.reason = earlyEndReason ?? string.Empty;
            });
        }

        public void Attach(TwentyWavePressureRuntime runtime)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            Detach();
            attachedRuntime = runtime;
            playerLifecycleHandler = value => RecordW12Lifecycle(TeamSide.Player, value, runtime.ElapsedRunTime);
            aiLifecycleHandler = value => RecordW12Lifecycle(TeamSide.AI, value, runtime.ElapsedRunTime);
            stormcallerHandler = (side, value) => RecordStormcallerCast(side, runtime.CurrentWaveIndex, value);
            combatHandler = value => AccumulateCombatDamage(value.Team, value);
            runeRewardHandler = value => RecordRuneReward(TeamSide.Player, value);
            runtime.PlayerEnemyLifecycleEmitted += playerLifecycleHandler;
            runtime.AiEnemyLifecycleEmitted += aiLifecycleHandler;
            runtime.StormcallerCastEmitted += stormcallerHandler;
            runtime.CombatEmitted += combatHandler;
            runtime.PlayerRuneRewardGranted += runeRewardHandler;
        }

        public void Detach()
        {
            if (attachedRuntime == null)
            {
                return;
            }

            attachedRuntime.PlayerEnemyLifecycleEmitted -= playerLifecycleHandler;
            attachedRuntime.AiEnemyLifecycleEmitted -= aiLifecycleHandler;
            attachedRuntime.StormcallerCastEmitted -= stormcallerHandler;
            attachedRuntime.CombatEmitted -= combatHandler;
            attachedRuntime.PlayerRuneRewardGranted -= runeRewardHandler;
            attachedRuntime = null;
            playerLifecycleHandler = null;
            aiLifecycleHandler = null;
            stormcallerHandler = null;
            combatHandler = null;
            runeRewardHandler = null;
        }

        private bool Record(string eventName, string side, int wave, Action<AnalyticsEventV2> configure)
        {
            var sequence = nextSequence;
            var value = AnalyticsEventV2Factory.Create(
                eventName,
                context.RunId + ":analytics:" + sequence,
                context.RunId,
                context.RunSeed,
                context.ExecutionContext,
                side,
                wave,
                context.RankTier,
                context.AiDifficulty,
                sequence,
                context.ConfigVersion,
                context.BuildVersion,
                utcNow());
            value.ai_profile = context.AiProfile;
            value.ai_algorithm_version = context.AiAlgorithmVersion;
            value.ai_decision_seed = context.AiDecisionSeed;
            value.ai_recovery_match = context.AiRecoveryMatch;
            value.player_rank_level = context.PlayerRankLevel;
            configure?.Invoke(value);
            var result = recorder.Record(value, out var error);
            LastError = error;
            if (result != AnalyticsRecordResultV2.Accepted)
            {
                return false;
            }

            nextSequence++;
            return true;
        }

        private static string ToAnalyticsSide(TeamSide side)
        {
            return side == TeamSide.Player ? AnalyticsSides.Player : AnalyticsSides.Ai;
        }
    }
}
