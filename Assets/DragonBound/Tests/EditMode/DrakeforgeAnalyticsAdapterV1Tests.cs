using System;
using DragonBound.Analytics;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Grid;
using DragonBound.Recruitment;
using DragonBound.Runes;
using GameShared.Random;
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
        public void ResolvedItemUseRecordsCommandResultAndCooldownOnce()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var observation = new ItemUseResolvedEvent(
                ItemIds.WinterveilRune,
                1,
                "activate",
                true,
                ItemOperationFailure.None,
                30f,
                30f);

            Assert.IsTrue(adapter.RecordItemUseResolved(TeamSide.Player, 6, observation), adapter.LastError);
            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual("item_command", sink.Events[0].result);
            Assert.AreEqual("activate", sink.Events[0].reason);
            Assert.AreEqual("item_result", sink.Events[1].result);
            Assert.AreEqual("accepted", sink.Events[1].reason);
            Assert.AreEqual("cooldown", sink.Events[2].result);
            Assert.AreEqual(30f, sink.Events[2].duration_seconds, 0.001f);

            Assert.IsFalse(adapter.RecordItemUseResolved(TeamSide.Player, 6, observation));
            Assert.AreEqual(3, sink.Events.Count, "The same item attempt must not be duplicated.");
        }

        [Test]
        public void RuneLoadoutSnapshotRecordsStableAssignmentsOnce()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            Assert.IsTrue(RuneLoadoutSnapshot.TryCreate(
                new[]
                {
                    new RuneLoadoutAssignment
                    {
                        HeroId = HeroDefinitionCatalog.Definitions[0].Id,
                        RuneId = "Power"
                    }
                },
                out var snapshot,
                out var snapshotError), snapshotError);

            Assert.IsTrue(adapter.RecordRuneLoadoutSnapshotLocked(
                TeamSide.Player, 0, snapshot), adapter.LastError);
            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RuneEquip, sink.Events[0].event_name);
            Assert.AreEqual(HeroDefinitionCatalog.Definitions[0].Id, sink.Events[0].hero_id);
            Assert.AreEqual("Power", sink.Events[0].rune_id);
            Assert.AreEqual("run_start", sink.Events[0].snapshot_reason);
            Assert.AreEqual("loadout_snapshot_locked", sink.Events[0].result);

            Assert.IsFalse(adapter.RecordRuneLoadoutSnapshotLocked(TeamSide.Player, 0, snapshot));
            Assert.AreEqual(1, sink.Events.Count);
        }

        [Test]
        public void DetachedAdapterDoesNotObserveLaterItemCommands()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var runtime = new TwentyWavePressureRuntime(
                new MatchController(821), null, null, 821,
                itemSnapshotProvider: new EmptyItemRunSnapshotProvider());
            adapter.Attach(runtime);

            Assert.IsTrue(runtime.StartRun());
            var eventCountAtDetach = sink.Events.Count;
            adapter.Detach();
            Assert.IsFalse(runtime.TryUseItem(
                TeamSide.Player,
                ItemIds.WinterveilRune,
                out var reason));
            Assert.AreEqual("NotActiveInSnapshot", reason);
            Assert.AreEqual(eventCountAtDetach, sink.Events.Count);
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

        [Test]
        public void CoreLifecycleUsesStableIdsAndRecordsTerminalReason()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);

            Assert.IsTrue(adapter.RecordRunStart(), adapter.LastError);
            Assert.IsFalse(adapter.RecordRunStart(), "A resumed Running state must not duplicate run_start.");
            Assert.IsTrue(adapter.RecordWaveStarted(1), adapter.LastError);
            Assert.IsTrue(adapter.RecordWaveFinished(1, 24f), adapter.LastError);
            Assert.IsTrue(adapter.RecordMatchFinished(1, "defeat", "player_defeated", 31f), adapter.LastError);

            Assert.AreEqual(4, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RunStart, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.WaveStart, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.WaveFinish, sink.Events[2].event_name);
            Assert.AreEqual(24f, sink.Events[2].elapsed_seconds);
            Assert.AreEqual(AnalyticsEventNamesV2.MatchFinish, sink.Events[3].event_name);
            Assert.AreEqual("defeat", sink.Events[3].match_result);
            Assert.AreEqual("player_defeated", sink.Events[3].reason);
        }

        [Test]
        public void HeroFormationRecordsSideHeroAndPairIdentity()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var heroId = HeroSliceCatalog.WindclawRangerHeroId;
            var pair = new HeroPairLink(
                "pair-1",
                "component-1",
                "component-2",
                "recipe-1",
                heroId,
                HeroRecipeRarity.Purple,
                new HeroPairCombatProxy(heroId, new HeroProgressionState(heroId)));

            Assert.IsTrue(
                adapter.RecordHeroFormed(TeamSide.AI, 2, new HeroPairLinkedEvent(pair)),
                adapter.LastError);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.HeroFormed, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsSides.Ai, sink.Events[0].side);
            Assert.AreEqual(heroId, sink.Events[0].hero_id);
            Assert.AreEqual("pair-1", sink.Events[0].source_unit_id);
        }

        [Test]
        public void SuccessfulRecruitAutomaticallyRecordsCompositionAndFormationSnapshot()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var match = new MatchController(17);
            var playerDestination = new BoardRecruitDestination(DragonBoundBoardLayout.CreateInitial());
            var aiDestination = new BoardRecruitDestination(DragonBoundBoardLayout.CreateInitial());
            var playerRecruitment = new RecruitmentService(
                match.Player,
                new RecruitDeck(
                    GreyboxRecruitmentCatalog.Create(),
                    new RunSeed(17).Random,
                    "analytics.player",
                    enableHeroComponents: false),
                playerDestination);
            var runtime = new ThreeWaveSliceRuntime(match, playerDestination, aiDestination);
            adapter.Attach(runtime, playerDestination, aiDestination, playerRecruitment, null);

            var attempt = playerRecruitment.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.Success, attempt.Status);
            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.RecruitResult, sink.Events[0].event_name);
            Assert.AreEqual(1, sink.Events[0].recruitment_number);
            Assert.AreEqual(5, sink.Events[0].basic_count);
            Assert.AreEqual(0, sink.Events[0].component_count);
            Assert.AreEqual(0, sink.Events[0].forge_pick_count);
            Assert.AreEqual(
                AnalyticsComponentPolicies.RecruitComponentPolicyV2,
                sink.Events[0].component_policy);
            Assert.AreEqual(AnalyticsEventNamesV2.FormationSnapshot, sink.Events[1].event_name);
            Assert.AreEqual("post_recruit", sink.Events[1].snapshot_reason);
            Assert.AreEqual(5, sink.Events[1].basic_unit_count);
            Assert.AreEqual(0, sink.Events[1].board_occupied);
            Assert.AreEqual(5, sink.Events[1].bench_occupied);
            Assert.AreEqual(0, sink.Events[1].hittable_unit_count);

            adapter.Detach();
            match.Player.AddResources(20);
            playerRecruitment.TryRecruit();
            Assert.AreEqual(2, sink.Events.Count, "Detached adapter must not observe later recruits.");
        }

        [Test]
        public void NormalGoalRecordsGoalHeartLossAndDeathWaveInOrder()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var enemy = new EnemyLifecycleEvent(
                EnemyLifecycleEventKind.Spawned,
                3,
                "player.wave3.enemy01",
                EnemyArchetype.Normal,
                30f,
                0f,
                moveSpeedCellsPerSecond: 0.6f);

            Assert.IsTrue(adapter.RecordEnemyLifecycle(TeamSide.Player, enemy, 10f), adapter.LastError);
            Assert.IsTrue(adapter.RecordEnemyGoalResolved(
                TeamSide.Player,
                new EnemyGoalResolvedEvent(
                    new EnemyLifecycleEvent(
                        EnemyLifecycleEventKind.Leaked,
                        3,
                        enemy.RuntimeId,
                        enemy.Archetype,
                        enemy.MaxHitPoints,
                        1f),
                    1,
                    0,
                    false)), adapter.LastError);

            Assert.AreEqual(4, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.EnemySpawn, sink.Events[0].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.EnemyGoal, sink.Events[1].event_name);
            Assert.AreEqual(AnalyticsEventNamesV2.HeartLost, sink.Events[2].event_name);
            Assert.AreEqual(1, sink.Events[2].heart_before);
            Assert.AreEqual(0, sink.Events[2].heart_after);
            Assert.AreEqual(AnalyticsEventNamesV2.DeathWave, sink.Events[3].event_name);
            Assert.AreEqual("hatchling_health_zero", sink.Events[3].reason);
        }

        [Test]
        public void BossGoalRecordsInstantDefeatWithoutNormalHeartLoss()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var boss = new EnemyLifecycleEvent(
                EnemyLifecycleEventKind.Leaked,
                6,
                "player.wave6.boss",
                EnemyArchetype.Boss,
                800f,
                1f,
                "BOSS_SOULCHAIN_BINDER");

            Assert.IsTrue(adapter.RecordEnemyGoalResolved(
                TeamSide.Player,
                new EnemyGoalResolvedEvent(boss, 3, 0, true)), adapter.LastError);

            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.BossGoal, sink.Events[0].event_name);
            Assert.AreEqual("BOSS_SOULCHAIN_BINDER", sink.Events[0].boss_id);
            Assert.AreEqual(AnalyticsEventNamesV2.DeathWave, sink.Events[1].event_name);
        }

        [Test]
        public void HeroLastHitRecordsXpAndLevelUpInSettlementOrder()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var enemy = new EnemyLifecycleEvent(
                EnemyLifecycleEventKind.Killed,
                4,
                "player.wave4.enemy02",
                EnemyArchetype.Elite,
                60f,
                0.5f);
            var owner = new CombatDamageOwner(
                CombatDamageOwnerKind.Hero,
                TeamSide.Player,
                "pair-hero-1",
                HeroSliceCatalog.WindclawRangerHeroId);

            Assert.IsTrue(adapter.RecordEnemyKillResolved(
                TeamSide.Player,
                4,
                new EnemyKillResolvedEvent(enemy, owner)), adapter.LastError);
            Assert.IsTrue(adapter.RecordHeroExperienceResolved(
                TeamSide.Player,
                4,
                new HeroExperienceResolvedEvent(
                    enemy.RuntimeId,
                    owner.SourceRuntimeId,
                    owner.HeroId,
                    3,
                    1,
                    2)), adapter.LastError);

            Assert.AreEqual(3, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.LastHit, sink.Events[0].event_name);
            Assert.AreEqual(enemy.RuntimeId, sink.Events[0].enemy_id);
            Assert.AreEqual(owner.SourceRuntimeId, sink.Events[0].source_unit_id);
            Assert.AreEqual(owner.HeroId, sink.Events[0].hero_id);
            Assert.AreEqual(AnalyticsEventNamesV2.HeroXp, sink.Events[1].event_name);
            Assert.AreEqual(3, sink.Events[1].xp_amount);
            Assert.AreEqual(AnalyticsEventNamesV2.HeroLevelUp, sink.Events[2].event_name);
            Assert.AreEqual(2, sink.Events[2].hero_level);
        }

        [Test]
        public void BasicUnitLastHitDoesNotCreateHeroProgressionEvents()
        {
            var sink = new InMemoryAnalyticsSinkV2();
            var adapter = CreateAdapter(sink);
            var enemy = new EnemyLifecycleEvent(
                EnemyLifecycleEventKind.Killed,
                2,
                "ai.wave2.enemy03",
                EnemyArchetype.Normal,
                30f,
                0.4f);
            var owner = new CombatDamageOwner(
                CombatDamageOwnerKind.BasicUnit,
                TeamSide.AI,
                "ai.basic.1");

            Assert.IsTrue(adapter.RecordEnemyKillResolved(
                TeamSide.AI,
                2,
                new EnemyKillResolvedEvent(enemy, owner)), adapter.LastError);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(AnalyticsEventNamesV2.LastHit, sink.Events[0].event_name);
            Assert.AreEqual(string.Empty, sink.Events[0].hero_id);
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
