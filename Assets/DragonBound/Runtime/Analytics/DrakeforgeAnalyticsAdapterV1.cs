using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Recruitment;
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
            int playerRankLevel = 0,
            string buildLane = AnalyticsBuildLanes.Development)
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
            BuildLane = buildLane ?? string.Empty;
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
        public string BuildLane { get; }
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

        private readonly AnalyticsRunSessionV2 session;
        private readonly DrakeforgeAnalyticsRunContext context;
        private readonly Dictionary<string, float> w12SpawnTimeBySide =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, DamageAggregate> damageBySide =
            new Dictionary<string, DamageAggregate>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> bossSpawnTimeByRuntime =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private TwentyWavePressureRuntime attachedRuntime;
        private ThreeWaveSliceRuntime attachedThreeWaveRuntime;
        private BoardRecruitDestination attachedPlayerDestination;
        private BoardRecruitDestination attachedAiDestination;
        private RecruitmentService attachedPlayerRecruitment;
        private RecruitmentService attachedAiRecruitment;
        private Action<EnemyLifecycleEvent> playerLifecycleHandler;
        private Action<EnemyLifecycleEvent> aiLifecycleHandler;
        private Action<EnemyGoalResolvedEvent> playerGoalResolvedHandler;
        private Action<EnemyGoalResolvedEvent> aiGoalResolvedHandler;
        private Action<EnemyKillResolvedEvent> playerKillResolvedHandler;
        private Action<EnemyKillResolvedEvent> aiKillResolvedHandler;
        private Action<HeroExperienceResolvedEvent> playerHeroExperienceHandler;
        private Action<HeroExperienceResolvedEvent> aiHeroExperienceHandler;
        private Action<TeamSide, ItemRunSnapshot> itemSnapshotHandler;
        private Action<TeamSide, ItemUseResolvedEvent> itemUseHandler;
        private Action<TeamSide, StormcallerCastEvent> stormcallerHandler;
        private Action<CombatEvent> combatHandler;
        private Action<RuneReward> runeRewardHandler;
        private Action<int> waveStartedHandler;
        private Action<int, float> waveFinishedHandler;
        private Action<HeroPairLinkedEvent> playerHeroFormedHandler;
        private Action<HeroPairLinkedEvent> aiHeroFormedHandler;
        private Action<RecruitmentAttempt> playerRecruitmentHandler;
        private Action<RecruitmentAttempt> aiRecruitmentHandler;

        public DrakeforgeAnalyticsAdapterV1(
            AnalyticsRecorderV2 recorder,
            DrakeforgeAnalyticsRunContext context,
            Func<DateTime> utcNow = null)
            : this(new AnalyticsRunSessionV2(recorder, context, utcNow))
        {
        }

        public DrakeforgeAnalyticsAdapterV1(AnalyticsRunSessionV2 session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            context = session.Context;
        }

        public string LastError { get; private set; } = string.Empty;
        public long NextSequence => session.NextSequence;

        public bool RecordRunStart()
        {
            return Record(AnalyticsEventNamesV2.RunStart, AnalyticsSides.System, 0, "run:start", null);
        }

        public bool RecordWaveStarted(int wave)
        {
            return Record(
                AnalyticsEventNamesV2.WaveStart,
                AnalyticsSides.System,
                wave,
                "wave:" + wave + ":start",
                null);
        }

        public bool RecordEnemyLifecycle(TeamSide side, EnemyLifecycleEvent observation, float elapsedSeconds)
        {
            var analyticsSide = ToAnalyticsSide(side);
            var wave = Math.Max(0, observation.SpawnWave);
            var bossLike = observation.Archetype == EnemyArchetype.Boss;
            var runtimeKey = analyticsSide + ":" + observation.RuntimeId;
            if (observation.Kind == EnemyLifecycleEventKind.Spawned)
            {
                if (bossLike)
                {
                    bossSpawnTimeByRuntime[runtimeKey] = Math.Max(0f, elapsedSeconds);
                    return Record(
                        AnalyticsEventNamesV2.BossSpawn,
                        analyticsSide,
                        wave,
                        "boss:" + runtimeKey + ":spawn",
                        value =>
                        {
                            value.boss_id = StableBossId(observation);
                            value.enemy_type = AnalyticsEnemyTypes.Boss;
                            value.move_speed_cells_per_second = Math.Max(0.0001f, observation.MoveSpeedCellsPerSecond);
                            value.max_hit_points = observation.MaxHitPoints;
                        });
                }

                return Record(
                    AnalyticsEventNamesV2.EnemySpawn,
                    analyticsSide,
                    wave,
                    "enemy:" + runtimeKey + ":spawn",
                    value =>
                    {
                        value.enemy_type = observation.Archetype == EnemyArchetype.Swarm
                            ? AnalyticsEnemyTypes.BossSummon
                            : AnalyticsEnemyTypes.Normal;
                        value.enemy_id = observation.RuntimeId;
                        value.move_speed_cells_per_second = Math.Max(0f, observation.MoveSpeedCellsPerSecond);
                        value.max_hit_points = observation.MaxHitPoints;
                    });
            }

            if (observation.Kind == EnemyLifecycleEventKind.Killed && bossLike)
            {
                bossSpawnTimeByRuntime.TryGetValue(runtimeKey, out var spawnedAt);
                bossSpawnTimeByRuntime.Remove(runtimeKey);
                return Record(
                    AnalyticsEventNamesV2.BossKill,
                    analyticsSide,
                    wave,
                    "boss:" + runtimeKey + ":kill",
                    value =>
                    {
                        value.boss_id = StableBossId(observation);
                        value.duration_seconds = Math.Max(0f, elapsedSeconds - spawnedAt);
                    });
            }

            return true;
        }

        public bool RecordEnemyGoalResolved(TeamSide side, EnemyGoalResolvedEvent observation)
        {
            var enemy = observation.Enemy;
            var analyticsSide = ToAnalyticsSide(side);
            var wave = Math.Max(1, enemy.SpawnWave);
            var runtimeKey = analyticsSide + ":" + enemy.RuntimeId;
            bool bossLike = enemy.Archetype == EnemyArchetype.Boss || enemy.Archetype == EnemyArchetype.Swarm;
            bool recordedGoal;
            if (bossLike)
            {
                recordedGoal = Record(
                    AnalyticsEventNamesV2.BossGoal,
                    analyticsSide,
                    wave,
                    "boss:" + runtimeKey + ":goal",
                    value =>
                    {
                        value.boss_id = StableBossId(enemy);
                        value.heart_after = observation.HeartAfter;
                    });
            }
            else
            {
                recordedGoal = Record(
                    AnalyticsEventNamesV2.EnemyGoal,
                    analyticsSide,
                    wave,
                    "enemy:" + runtimeKey + ":goal",
                    value =>
                    {
                        value.enemy_type = AnalyticsEnemyTypes.Normal;
                        value.enemy_id = enemy.RuntimeId;
                        value.reason = "dragon_goal";
                    });

                var lost = Math.Max(0, observation.HeartBefore - observation.HeartAfter);
                if (lost > 0)
                {
                    recordedGoal &= Record(
                        AnalyticsEventNamesV2.HeartLost,
                        analyticsSide,
                        wave,
                        "heart:" + runtimeKey,
                        value =>
                        {
                            value.count = lost;
                            value.reason = "enemy_goal";
                            value.heart_before = observation.HeartBefore;
                            value.heart_after = observation.HeartAfter;
                        });
                }
            }

            if (observation.InstantDefeat || observation.HeartAfter <= 0)
            {
                recordedGoal &= Record(
                    AnalyticsEventNamesV2.DeathWave,
                    analyticsSide,
                    wave,
                    "death_wave:" + analyticsSide,
                    value => value.reason = bossLike ? "boss_goal" : "hatchling_health_zero");
            }

            return recordedGoal;
        }

        public bool RecordEnemyKillResolved(TeamSide side, int wave, EnemyKillResolvedEvent observation)
        {
            var owner = observation.LastHitOwner;
            if (!owner.IsValid || owner.Side != side || string.IsNullOrWhiteSpace(observation.Enemy.RuntimeId))
            {
                return true;
            }

            var analyticsSide = ToAnalyticsSide(side);
            return Record(
                AnalyticsEventNamesV2.LastHit,
                analyticsSide,
                Math.Max(1, wave),
                "last_hit:" + analyticsSide + ":" + observation.Enemy.RuntimeId,
                value =>
                {
                    value.enemy_id = observation.Enemy.RuntimeId;
                    value.source_unit_id = owner.SourceRuntimeId;
                    value.hero_id = owner.HeroId;
                });
        }

        public bool RecordHeroExperienceResolved(
            TeamSide side,
            int wave,
            HeroExperienceResolvedEvent observation)
        {
            if (observation.Amount <= 0 || string.IsNullOrWhiteSpace(observation.HeroId) ||
                string.IsNullOrWhiteSpace(observation.HeroRuntimeId))
            {
                return true;
            }

            var analyticsSide = ToAnalyticsSide(side);
            var identity = analyticsSide + ":" + observation.HeroRuntimeId + ":" + observation.EnemyRuntimeId;
            var recorded = Record(
                AnalyticsEventNamesV2.HeroXp,
                analyticsSide,
                Math.Max(1, wave),
                "hero_xp:" + identity,
                value =>
                {
                    value.hero_id = observation.HeroId;
                    value.source_unit_id = observation.HeroRuntimeId;
                    value.enemy_id = observation.EnemyRuntimeId;
                    value.xp_amount = observation.Amount;
                });
            if (observation.CurrentLevel > observation.PreviousLevel)
            {
                recorded &= Record(
                    AnalyticsEventNamesV2.HeroLevelUp,
                    analyticsSide,
                    Math.Max(1, wave),
                    "hero_level:" + analyticsSide + ":" + observation.HeroRuntimeId + ":" + observation.CurrentLevel,
                    value =>
                    {
                        value.hero_id = observation.HeroId;
                        value.source_unit_id = observation.HeroRuntimeId;
                        value.hero_level = observation.CurrentLevel;
                    });
            }

            return recorded;
        }

        public bool RecordWaveFinished(int wave, float elapsedSeconds)
        {
            return Record(
                AnalyticsEventNamesV2.WaveFinish,
                AnalyticsSides.System,
                wave,
                "wave:" + wave + ":finish",
                value => value.elapsed_seconds = Math.Max(0f, elapsedSeconds));
        }

        public bool RecordHeroFormed(TeamSide side, int wave, HeroPairLinkedEvent observation)
        {
            var pair = observation.PairLink;
            if (pair == null)
            {
                LastError = "Hero pair link is required.";
                return false;
            }

            return Record(
                AnalyticsEventNamesV2.HeroFormed,
                ToAnalyticsSide(side),
                Math.Max(0, wave),
                "hero_formed:" + pair.PairLinkId,
                value =>
                {
                    value.hero_id = pair.HeroId;
                    value.source_unit_id = pair.PairLinkId;
                });
        }

        public bool RecordMatchFinished(int wave, string matchResult, string reason, float elapsedSeconds)
        {
            return Record(
                AnalyticsEventNamesV2.MatchFinish,
                AnalyticsSides.System,
                Math.Max(0, wave),
                "match:finish",
                value =>
                {
                    value.match_result = matchResult ?? string.Empty;
                    value.reason = reason ?? string.Empty;
                    value.elapsed_seconds = Math.Max(0f, elapsedSeconds);
                });
        }

        public bool RecordRecruitResult(
            TeamSide side,
            int wave,
            RecruitmentAttempt attempt,
            RecruitmentService recruitment)
        {
            if (attempt.Status != RecruitmentStatus.Success || attempt.Batch == null || recruitment == null)
            {
                return true;
            }

            var componentCount = 0;
            var basicCount = 0;
            var forgePickCount = 0;
            foreach (var card in attempt.Batch.Cards)
            {
                switch (card.Kind)
                {
                    case RecruitItemKind.HeroComponent: componentCount++; break;
                    case RecruitItemKind.BasicUnit: basicCount++; break;
                    case RecruitItemKind.Shovel: forgePickCount++; break;
                }
            }

            var policy = recruitment.ComponentPolicy == RecruitComponentPolicy.V3
                ? AnalyticsComponentPolicies.RecruitComponentPolicyV3
                : AnalyticsComponentPolicies.RecruitComponentPolicyV2;
            return Record(
                AnalyticsEventNamesV2.RecruitResult,
                ToAnalyticsSide(side),
                Math.Max(0, wave),
                "recruit:" + ToAnalyticsSide(side) + ":" + attempt.Batch.RecruitmentNumber,
                value =>
                {
                    value.recruitment_number = attempt.Batch.RecruitmentNumber;
                    value.component_count = componentCount;
                    value.basic_count = basicCount;
                    value.forge_pick_count = forgePickCount;
                    value.component_policy = policy;
                    value.remaining_component_bag = Math.Max(0, recruitment.RemainingHeroComponents);
                });
        }

        public bool RecordFormationSnapshot(
            TeamSide side,
            int wave,
            string reason,
            string bookmarkKey,
            BoardRecruitDestination destination)
        {
            if (destination == null || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(bookmarkKey))
            {
                LastError = "Formation snapshot requires destination, reason and bookmark key.";
                return false;
            }

            var basicCount = 0;
            var componentCount = 0;
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind == RecruitItemKind.BasicUnit) basicCount++;
                else if (card.Kind == RecruitItemKind.HeroComponent) componentCount++;
            }

            var heroCount = destination.GetActiveHeroPairs().Count;
            var hittableCount = destination.GetDeployedUnits().Count + heroCount;
            return Record(
                AnalyticsEventNamesV2.FormationSnapshot,
                ToAnalyticsSide(side),
                Math.Max(0, wave),
                "formation:" + ToAnalyticsSide(side) + ":" + bookmarkKey,
                value =>
                {
                    value.snapshot_reason = reason;
                    value.basic_unit_count = basicCount;
                    value.hero_count = heroCount;
                    value.component_unit_count = componentCount;
                    value.board_occupied = destination.DeployedCount;
                    value.bench_occupied = destination.CampCount;
                    value.hittable_unit_count = hittableCount;
                });
        }

        public void RecordFormationSnapshots(int wave, string reason, string bookmarkKey)
        {
            if (attachedPlayerDestination != null)
            {
                RecordFormationSnapshot(TeamSide.Player, wave, reason, bookmarkKey, attachedPlayerDestination);
            }

            if (attachedAiDestination != null)
            {
                RecordFormationSnapshot(TeamSide.AI, wave, reason, bookmarkKey, attachedAiDestination);
            }
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
                recorded &= Record(
                    AnalyticsEventNamesV2.ItemEquip,
                    ToAnalyticsSide(side),
                    Math.Max(0, wave),
                    "item_snapshot:" + ToAnalyticsSide(side) + ":" + index + ":" + itemId,
                    valueToRecord =>
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

        public bool RecordItemUseResolved(TeamSide side, int wave, ItemUseResolvedEvent observation)
        {
            var analyticsSide = ToAnalyticsSide(side);
            var attemptKey = "item:" + analyticsSide + ":" + observation.AttemptNumber;
            var recorded = Record(
                AnalyticsEventNamesV2.ItemUse,
                analyticsSide,
                Math.Max(0, wave),
                attemptKey + ":command",
                valueToRecord =>
                {
                    valueToRecord.item_id = observation.ItemId;
                    valueToRecord.result = "item_command";
                    valueToRecord.reason = observation.Command;
                });
            recorded &= Record(
                AnalyticsEventNamesV2.ItemUse,
                analyticsSide,
                Math.Max(0, wave),
                attemptKey + ":result",
                valueToRecord =>
                {
                    valueToRecord.item_id = observation.ItemId;
                    valueToRecord.result = "item_result";
                    valueToRecord.reason = observation.Accepted
                        ? "accepted"
                        : "rejected:" + (string.IsNullOrWhiteSpace(observation.Reason)
                            ? "Unknown"
                            : observation.Reason);
                });

            if (observation.CooldownRemainingSeconds > 0.0001f)
            {
                recorded &= Record(
                    AnalyticsEventNamesV2.ItemUse,
                    analyticsSide,
                    Math.Max(0, wave),
                    attemptKey + ":cooldown",
                    valueToRecord =>
                    {
                        valueToRecord.item_id = observation.ItemId;
                        valueToRecord.result = "cooldown";
                        valueToRecord.duration_seconds = observation.CooldownRemainingSeconds;
                    });
            }

            return recorded;
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
                return Record(
                    AnalyticsEventNamesV2.RuneEquip,
                    ToAnalyticsSide(side),
                    Math.Max(0, wave),
                    "rune_snapshot:" + ToAnalyticsSide(side) + ":empty",
                    valueToRecord =>
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
                recorded &= Record(
                    AnalyticsEventNamesV2.RuneEquip,
                    ToAnalyticsSide(side),
                    Math.Max(0, wave),
                    "rune_snapshot:" + ToAnalyticsSide(side) + ":" + assignment.Key + ":" + runeId,
                    valueToRecord =>
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

        public void Attach(
            TwentyWavePressureRuntime runtime,
            BoardRecruitDestination playerDestination = null,
            BoardRecruitDestination aiDestination = null,
            RecruitmentService playerRecruitment = null,
            RecruitmentService aiRecruitment = null)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            Detach();
            attachedRuntime = runtime;
            playerLifecycleHandler = value => RecordEnemyLifecycle(TeamSide.Player, value, runtime.ElapsedRunTime);
            aiLifecycleHandler = value => RecordEnemyLifecycle(TeamSide.AI, value, runtime.ElapsedRunTime);
            playerGoalResolvedHandler = value => RecordEnemyGoalResolved(TeamSide.Player, value);
            aiGoalResolvedHandler = value => RecordEnemyGoalResolved(TeamSide.AI, value);
            playerKillResolvedHandler = value => RecordEnemyKillResolved(
                TeamSide.Player, runtime.CurrentWaveIndex, value);
            aiKillResolvedHandler = value => RecordEnemyKillResolved(
                TeamSide.AI, runtime.CurrentWaveIndex, value);
            playerHeroExperienceHandler = value => RecordHeroExperienceResolved(
                TeamSide.Player, runtime.CurrentWaveIndex, value);
            aiHeroExperienceHandler = value => RecordHeroExperienceResolved(
                TeamSide.AI, runtime.CurrentWaveIndex, value);
            itemSnapshotHandler = (side, snapshot) =>
                RecordItemSnapshotLocked(side, runtime.CurrentWaveIndex, snapshot);
            itemUseHandler = (side, value) =>
                RecordItemUseResolved(side, runtime.CurrentWaveIndex, value);
            stormcallerHandler = (side, value) => RecordStormcallerCast(side, runtime.CurrentWaveIndex, value);
            combatHandler = value => AccumulateCombatDamage(value.Team, value);
            runeRewardHandler = value => RecordRuneReward(TeamSide.Player, value);
            waveStartedHandler = wave =>
            {
                RecordWaveStarted(wave);
                RecordFormationSnapshots(wave, "wave_start", "wave:" + wave + ":start");
            };
            waveFinishedHandler = (wave, elapsed) => RecordWaveFinished(wave, elapsed);
            runtime.PlayerEnemyLifecycleEmitted += playerLifecycleHandler;
            runtime.AiEnemyLifecycleEmitted += aiLifecycleHandler;
            runtime.PlayerEnemyGoalResolved += playerGoalResolvedHandler;
            runtime.AiEnemyGoalResolved += aiGoalResolvedHandler;
            runtime.PlayerEnemyKillResolved += playerKillResolvedHandler;
            runtime.AiEnemyKillResolved += aiKillResolvedHandler;
            runtime.PlayerHeroExperienceResolved += playerHeroExperienceHandler;
            runtime.AiHeroExperienceResolved += aiHeroExperienceHandler;
            runtime.ItemSnapshotLocked += itemSnapshotHandler;
            runtime.ItemUseResolved += itemUseHandler;
            runtime.StormcallerCastEmitted += stormcallerHandler;
            runtime.CombatEmitted += combatHandler;
            runtime.PlayerRuneRewardGranted += runeRewardHandler;
            runtime.WaveStarted += waveStartedHandler;
            runtime.WaveFinished += waveFinishedHandler;
            AttachDestinations(playerDestination, aiDestination, () => runtime.CurrentWaveIndex);
            AttachRecruitments(playerRecruitment, aiRecruitment, () => runtime.CurrentWaveIndex);
        }

        public void Attach(
            ThreeWaveSliceRuntime runtime,
            BoardRecruitDestination playerDestination = null,
            BoardRecruitDestination aiDestination = null,
            RecruitmentService playerRecruitment = null,
            RecruitmentService aiRecruitment = null)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            Detach();
            attachedThreeWaveRuntime = runtime;
            playerLifecycleHandler = value => RecordEnemyLifecycle(TeamSide.Player, value, runtime.ElapsedRunTime);
            aiLifecycleHandler = value => RecordEnemyLifecycle(TeamSide.AI, value, runtime.ElapsedRunTime);
            playerGoalResolvedHandler = value => RecordEnemyGoalResolved(TeamSide.Player, value);
            aiGoalResolvedHandler = value => RecordEnemyGoalResolved(TeamSide.AI, value);
            playerKillResolvedHandler = value => RecordEnemyKillResolved(TeamSide.Player, runtime.CurrentWave, value);
            aiKillResolvedHandler = value => RecordEnemyKillResolved(TeamSide.AI, runtime.CurrentWave, value);
            playerHeroExperienceHandler = value => RecordHeroExperienceResolved(
                TeamSide.Player, runtime.CurrentWave, value);
            aiHeroExperienceHandler = value => RecordHeroExperienceResolved(
                TeamSide.AI, runtime.CurrentWave, value);
            waveStartedHandler = wave =>
            {
                RecordWaveStarted(wave);
                RecordFormationSnapshots(wave, "wave_start", "wave:" + wave + ":start");
            };
            waveFinishedHandler = (wave, elapsed) => RecordWaveFinished(wave, elapsed);
            runtime.WaveStarted += waveStartedHandler;
            runtime.WaveFinished += waveFinishedHandler;
            runtime.PlayerEnemyLifecycleEmitted += playerLifecycleHandler;
            runtime.AiEnemyLifecycleEmitted += aiLifecycleHandler;
            runtime.PlayerEnemyGoalResolved += playerGoalResolvedHandler;
            runtime.AiEnemyGoalResolved += aiGoalResolvedHandler;
            runtime.PlayerEnemyKillResolved += playerKillResolvedHandler;
            runtime.AiEnemyKillResolved += aiKillResolvedHandler;
            runtime.PlayerHeroExperienceResolved += playerHeroExperienceHandler;
            runtime.AiHeroExperienceResolved += aiHeroExperienceHandler;
            AttachDestinations(playerDestination, aiDestination, () => runtime.CurrentWave);
            AttachRecruitments(playerRecruitment, aiRecruitment, () => runtime.CurrentWave);
        }

        public void Detach()
        {
            if (attachedRuntime != null)
            {
                attachedRuntime.PlayerEnemyLifecycleEmitted -= playerLifecycleHandler;
                attachedRuntime.AiEnemyLifecycleEmitted -= aiLifecycleHandler;
                attachedRuntime.PlayerEnemyGoalResolved -= playerGoalResolvedHandler;
                attachedRuntime.AiEnemyGoalResolved -= aiGoalResolvedHandler;
                attachedRuntime.PlayerEnemyKillResolved -= playerKillResolvedHandler;
                attachedRuntime.AiEnemyKillResolved -= aiKillResolvedHandler;
                attachedRuntime.PlayerHeroExperienceResolved -= playerHeroExperienceHandler;
                attachedRuntime.AiHeroExperienceResolved -= aiHeroExperienceHandler;
                attachedRuntime.ItemSnapshotLocked -= itemSnapshotHandler;
                attachedRuntime.ItemUseResolved -= itemUseHandler;
                attachedRuntime.StormcallerCastEmitted -= stormcallerHandler;
                attachedRuntime.CombatEmitted -= combatHandler;
                attachedRuntime.PlayerRuneRewardGranted -= runeRewardHandler;
                attachedRuntime.WaveStarted -= waveStartedHandler;
                attachedRuntime.WaveFinished -= waveFinishedHandler;
            }

            if (attachedThreeWaveRuntime != null)
            {
                attachedThreeWaveRuntime.WaveStarted -= waveStartedHandler;
                attachedThreeWaveRuntime.WaveFinished -= waveFinishedHandler;
                attachedThreeWaveRuntime.PlayerEnemyLifecycleEmitted -= playerLifecycleHandler;
                attachedThreeWaveRuntime.AiEnemyLifecycleEmitted -= aiLifecycleHandler;
                attachedThreeWaveRuntime.PlayerEnemyGoalResolved -= playerGoalResolvedHandler;
                attachedThreeWaveRuntime.AiEnemyGoalResolved -= aiGoalResolvedHandler;
                attachedThreeWaveRuntime.PlayerEnemyKillResolved -= playerKillResolvedHandler;
                attachedThreeWaveRuntime.AiEnemyKillResolved -= aiKillResolvedHandler;
                attachedThreeWaveRuntime.PlayerHeroExperienceResolved -= playerHeroExperienceHandler;
                attachedThreeWaveRuntime.AiHeroExperienceResolved -= aiHeroExperienceHandler;
            }

            if (attachedPlayerDestination != null)
            {
                attachedPlayerDestination.HeroPairLinked -= playerHeroFormedHandler;
            }

            if (attachedAiDestination != null)
            {
                attachedAiDestination.HeroPairLinked -= aiHeroFormedHandler;
            }

            if (attachedPlayerRecruitment != null)
            {
                attachedPlayerRecruitment.Attempted -= playerRecruitmentHandler;
            }

            if (attachedAiRecruitment != null)
            {
                attachedAiRecruitment.Attempted -= aiRecruitmentHandler;
            }

            attachedRuntime = null;
            attachedThreeWaveRuntime = null;
            attachedPlayerDestination = null;
            attachedAiDestination = null;
            attachedPlayerRecruitment = null;
            attachedAiRecruitment = null;
            playerLifecycleHandler = null;
            aiLifecycleHandler = null;
            playerGoalResolvedHandler = null;
            aiGoalResolvedHandler = null;
            playerKillResolvedHandler = null;
            aiKillResolvedHandler = null;
            playerHeroExperienceHandler = null;
            aiHeroExperienceHandler = null;
            itemSnapshotHandler = null;
            itemUseHandler = null;
            stormcallerHandler = null;
            combatHandler = null;
            runeRewardHandler = null;
            waveStartedHandler = null;
            waveFinishedHandler = null;
            playerHeroFormedHandler = null;
            aiHeroFormedHandler = null;
            playerRecruitmentHandler = null;
            aiRecruitmentHandler = null;
        }

        private void AttachDestinations(
            BoardRecruitDestination playerDestination,
            BoardRecruitDestination aiDestination,
            Func<int> currentWave)
        {
            attachedPlayerDestination = playerDestination;
            attachedAiDestination = aiDestination;
            if (playerDestination != null)
            {
                playerHeroFormedHandler = value =>
                {
                    RecordHeroFormed(TeamSide.Player, currentWave(), value);
                    RecordFormationSnapshot(
                        TeamSide.Player,
                        currentWave(),
                        "post_hero_formed",
                        "hero_formed:" + value.PairLink.PairLinkId,
                        playerDestination);
                };
                playerDestination.HeroPairLinked += playerHeroFormedHandler;
            }

            if (aiDestination != null)
            {
                aiHeroFormedHandler = value =>
                {
                    RecordHeroFormed(TeamSide.AI, currentWave(), value);
                    RecordFormationSnapshot(
                        TeamSide.AI,
                        currentWave(),
                        "post_hero_formed",
                        "hero_formed:" + value.PairLink.PairLinkId,
                        aiDestination);
                };
                aiDestination.HeroPairLinked += aiHeroFormedHandler;
            }
        }

        private void AttachRecruitments(
            RecruitmentService playerRecruitment,
            RecruitmentService aiRecruitment,
            Func<int> currentWave)
        {
            attachedPlayerRecruitment = playerRecruitment;
            attachedAiRecruitment = aiRecruitment;
            if (playerRecruitment != null)
            {
                playerRecruitmentHandler = attempt =>
                {
                    if (attempt.Status != RecruitmentStatus.Success) return;
                    RecordRecruitResult(TeamSide.Player, currentWave(), attempt, playerRecruitment);
                    RecordFormationSnapshot(
                        TeamSide.Player,
                        currentWave(),
                        "post_recruit",
                        "recruit:" + attempt.Batch.RecruitmentNumber,
                        attachedPlayerDestination);
                };
                playerRecruitment.Attempted += playerRecruitmentHandler;
            }

            if (aiRecruitment != null)
            {
                aiRecruitmentHandler = attempt =>
                {
                    if (attempt.Status != RecruitmentStatus.Success) return;
                    RecordRecruitResult(TeamSide.AI, currentWave(), attempt, aiRecruitment);
                    RecordFormationSnapshot(
                        TeamSide.AI,
                        currentWave(),
                        "post_recruit",
                        "recruit:" + attempt.Batch.RecruitmentNumber,
                        attachedAiDestination);
                };
                aiRecruitment.Attempted += aiRecruitmentHandler;
            }
        }

        private bool Record(string eventName, string side, int wave, Action<AnalyticsEventV2> configure)
        {
            return Record(eventName, side, wave, null, configure);
        }

        private bool Record(
            string eventName,
            string side,
            int wave,
            string dedupeKey,
            Action<AnalyticsEventV2> configure)
        {
            var result = session.Record(
                eventName,
                side,
                wave,
                dedupeKey,
                configure,
                out var error);
            LastError = error;
            return result == AnalyticsRecordResultV2.Accepted;
        }

        private static string ToAnalyticsSide(TeamSide side)
        {
            return side == TeamSide.Player ? AnalyticsSides.Player : AnalyticsSides.Ai;
        }

        private static string StableBossId(EnemyLifecycleEvent value)
        {
            return string.IsNullOrWhiteSpace(value.BossId) ? value.RuntimeId : value.BossId;
        }
    }
}
