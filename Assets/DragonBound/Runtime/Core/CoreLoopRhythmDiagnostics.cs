using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.AI;
using DragonBound.Bosses.Runtime;
using DragonBound.Grid;
using DragonBound.Items;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    /// <summary>
    /// Runs the real twenty-wave runtime with both sides controlled by AI V0. It exists only
    /// for greybox analysis and owns no combat, recruitment, or spawn rules of its own.
    /// </summary>
    public static class CoreLoopRhythmDiagnostics
    {
        private const int PlayerDeckSalt = 0x13579BDF;
        private const int AiDeckSalt = 0x2468ACE0;
        private const float TickSeconds = 0.10f;
        private const float MaxPostScheduleSeconds = 120f;
        private const float MaxRunSeconds = 1800f;

        public static CoreLoopRhythmReport Run(int firstRunSeed, int sampleCount, Action<int> progress = null)
        {
            return Run(firstRunSeed, sampleCount, RecruitComponentPolicy.V2, progress);
        }

        /// <summary>
        /// Runs the production Core Loop with an explicit finite-component policy. Both sides
        /// receive independent bags and streams as in a live match; the supplied policy is the
        /// only A/B variable.
        /// </summary>
        public static CoreLoopRhythmReport Run(
            int firstRunSeed,
            int sampleCount,
            RecruitComponentPolicy componentPolicy,
            Action<int> progress = null)
        {
            return Run(firstRunSeed, sampleCount, componentPolicy, EnemyHpCurveCandidate.CurrentProduction, progress);
        }

        public static CoreLoopRhythmReport Run(
            int firstRunSeed,
            int sampleCount,
            RecruitComponentPolicy componentPolicy,
            EnemyHpCurveCandidate hpCandidate,
            Action<int> progress = null)
        {
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            Parallel.For(0, sampleCount, options, offset =>
            {
                results[offset] = RunOne(unchecked(firstRunSeed + offset), componentPolicy, false, hpCandidate);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new CoreLoopRhythmReport(
                sampleCount,
                CoreLoopTimingVerification.RunFormalScheduleProof(),
                componentPolicy,
                hpCandidate);
            for (var index = 0; index < results.Length; index++)
            {
                report.Player.Add(results[index].Player);
                report.AI.Add(results[index].AI);
                report.PlayerEnemyPressure.Add(results[index].PlayerEnemyPressure);
                report.AiEnemyPressure.Add(results[index].AiEnemyPressure);
                report.MatchEnd.Add(results[index].MatchEnd);
            }

            return report;
        }

        /// <summary>
        /// Runs the complete W1-W20 pressure runtime and exposes per-seed Boss lifecycle and
        /// damage observations. This is diagnostic-only and never writes Production values.
        /// </summary>
        public static JointBalanceCalibrationReport RunJointBalanceCalibration(
            int firstRunSeed,
            int sampleCount,
            string buildId = "BARE",
            IItemRunSnapshotProvider itemSnapshotProvider = null,
            float soulChainBossMaxHitPoints = SoulchainBinderConfiguration.GreyboxMaxHitPoints,
            float stormcallerBossMaxHitPoints = StormcallerPriestConfiguration.GreyboxMaxHitPoints,
            float bloodcrownBossMaxHitPoints = BloodcrownTyrantConfiguration.GreyboxMaxHitPoints,
            float worldeaterBossMaxHitPoints = WorldeaterWyrmConfiguration.GreyboxMaxHitPoints,
            Action<int> progress = null)
        {
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            Parallel.For(0, sampleCount, options, offset =>
            {
                results[offset] = RunOne(
                    unchecked(firstRunSeed + offset),
                    RecruitComponentPolicy.V3,
                    false,
                    EnemyHpCurveCandidate.CurrentProduction,
                    soulChainBossMaxHitPoints,
                    itemSnapshotProvider: itemSnapshotProvider,
                    stormcallerBossMaxHitPoints: stormcallerBossMaxHitPoints,
                    bloodcrownBossMaxHitPoints: bloodcrownBossMaxHitPoints,
                    worldeaterBossMaxHitPoints: worldeaterBossMaxHitPoints);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new JointBalanceCalibrationReport(firstRunSeed, sampleCount, buildId);
            for (var index = 0; index < results.Length; index++)
            {
                report.Add(results[index].CreateJointCalibrationSample(unchecked(firstRunSeed + index)));
            }

            return report;
        }

        /// <summary>
        /// Runs a deterministic full-build diagnostic fixture and jumps directly to one Boss
        /// wave. This is for Boss-mechanics envelopes only; it is not a Production entry path.
        /// </summary>
        public static JointBalanceCalibrationReport RunDirectBossCalibration(
            int firstRunSeed,
            int sampleCount,
            int bossWave,
            string buildId,
            IItemRunSnapshotProvider itemSnapshotProvider = null,
            float soulChainBossMaxHitPoints = SoulchainBinderConfiguration.GreyboxMaxHitPoints,
            float stormcallerBossMaxHitPoints = StormcallerPriestConfiguration.GreyboxMaxHitPoints,
            float bloodcrownBossMaxHitPoints = BloodcrownTyrantConfiguration.GreyboxMaxHitPoints,
            float worldeaterBossMaxHitPoints = WorldeaterWyrmConfiguration.GreyboxMaxHitPoints,
            Action<int> progress = null)
        {
            if (sampleCount < 1 || bossWave < 6 || bossWave > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            Parallel.For(0, sampleCount, options, offset =>
            {
                results[offset] = RunOne(
                    unchecked(firstRunSeed + offset),
                    RecruitComponentPolicy.V3,
                    false,
                    EnemyHpCurveCandidate.CurrentProduction,
                    soulChainBossMaxHitPoints,
                    itemSnapshotProvider: itemSnapshotProvider,
                    stormcallerBossMaxHitPoints: stormcallerBossMaxHitPoints,
                    bloodcrownBossMaxHitPoints: bloodcrownBossMaxHitPoints,
                    worldeaterBossMaxHitPoints: worldeaterBossMaxHitPoints,
                    directCalibrationWave: bossWave);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new JointBalanceCalibrationReport(firstRunSeed, sampleCount, buildId);
            for (var index = 0; index < results.Length; index++)
            {
                report.Add(results[index].CreateJointCalibrationSample(unchecked(firstRunSeed + index)));
            }

            return report;
        }

        /// <summary>Runs the same formal V3 Core Loop while collecting capacity telemetry only.</summary>
        public static BoardBenchCapacityAuditReport RunBoardBenchCapacityAudit(
            int firstRunSeed,
            int sampleCount,
            Action<int> progress = null)
        {
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.For(0, sampleCount, options, offset =>
            {
                results[offset] = RunOne(unchecked(firstRunSeed + offset), RecruitComponentPolicy.V3, true, EnemyHpCurveCandidate.CurrentProduction);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new BoardBenchCapacityAuditReport(sampleCount);
            foreach (var result in results)
            {
                report.Player.Add(result.PlayerCapacityAudit);
                report.AI.Add(result.AiCapacityAudit);
            }

            return report;
        }

        /// <summary>
        /// Runs the real W1-W12 schedule for bounded W12 HP calibration. Item snapshots and
        /// Winterveil activation are diagnostic inputs only; Production defaults are unchanged.
        /// </summary>
        public static W12BuildEnvelopeCalibrationReport RunW12BuildEnvelopeCalibration(
            int firstRunSeed,
            int sampleCount,
            float bossMaxHitPoints,
            Func<int, IItemRunSnapshotProvider> itemProviderFactory,
            Action<int> progress = null)
        {
            return RunW12BuildEnvelopeCalibration(
                firstRunSeed,
                sampleCount,
                bossMaxHitPoints,
                itemProviderFactory,
                false,
                progress);
        }

        /// <summary>
        /// Runs a deterministic diagnostic-only pre-W12 setup and jumps directly to W12.
        /// This is a CALIBRATION_FIXTURE, never a Production entry point.
        /// </summary>
        public static W12BuildEnvelopeCalibrationReport RunDirectW12BuildEnvelopeCalibration(
            int firstRunSeed,
            int sampleCount,
            float bossMaxHitPoints,
            Func<int, IItemRunSnapshotProvider> itemProviderFactory,
            Action<int> progress = null)
        {
            return RunW12BuildEnvelopeCalibration(
                firstRunSeed,
                sampleCount,
                bossMaxHitPoints,
                itemProviderFactory,
                true,
                progress);
        }

        private static W12BuildEnvelopeCalibrationReport RunW12BuildEnvelopeCalibration(
            int firstRunSeed,
            int sampleCount,
            float bossMaxHitPoints,
            Func<int, IItemRunSnapshotProvider> itemProviderFactory,
            bool directW12,
            Action<int> progress)
        {
            if (sampleCount < 1 || bossMaxHitPoints <= 0f || itemProviderFactory == null)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            Parallel.For(0, sampleCount, options, offset =>
            {
                var runSeed = unchecked(firstRunSeed + offset);
                results[offset] = RunOne(
                    runSeed,
                    RecruitComponentPolicy.V3,
                    false,
                    EnemyHpCurveCandidate.CurrentProduction,
                    SoulchainBinderConfiguration.GreyboxMaxHitPoints,
                    stopAfterW12BossResolution: true,
                    stormcallerBossMaxHitPoints: bossMaxHitPoints,
                    itemSnapshotProvider: itemProviderFactory(runSeed),
                    collectW12Calibration: true,
                    activateDiagnosticWinterveilAtW12: true,
                    directW12Calibration: directW12);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new W12BuildEnvelopeCalibrationReport(
                firstRunSeed,
                sampleCount,
                bossMaxHitPoints,
                directW12 ? "Direct-W12" : "End-to-end");
            for (var index = 0; index < results.Length; index++)
            {
                report.Add(results[index].W12Calibration);
            }

            return report;
        }

        /// <summary>
        /// Runs the real W1-W6 portion of the pressure schedule with the normal AI V0 and
        /// production recruitment/combat services. The Boss HP is an analysis input only;
        /// the production configuration remains unchanged.
        /// </summary>
        public static W6BareFullScheduleCalibrationReport RunW6BareCalibration(
            int firstRunSeed,
            int sampleCount,
            float bossMaxHitPoints,
            Action<int> progress = null)
        {
            if (sampleCount < 1 || bossMaxHitPoints <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            Parallel.For(0, sampleCount, options, offset =>
            {
                results[offset] = RunOne(
                    unchecked(firstRunSeed + offset),
                    RecruitComponentPolicy.V3,
                    false,
                    EnemyHpCurveCandidate.CurrentProduction,
                    bossMaxHitPoints,
                    stopAfterW6BossResolution: true);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new W6BareFullScheduleCalibrationReport(firstRunSeed, sampleCount, bossMaxHitPoints);
            for (var index = 0; index < results.Length; index++)
            {
                report.Add(unchecked(firstRunSeed + index), results[index]);
            }

            return report;
        }

        /// <summary>
        /// Runs the real W1-W6 schedule with a deterministic Boss HP selected per RunSeed.
        /// This is diagnostic-only; the normal Production constructor still uses its default.
        /// </summary>
        public static W6BareFullScheduleCalibrationReport RunW6BareCalibrationBySeed(
            int firstRunSeed,
            int sampleCount,
            Func<int, float> bossMaxHitPointsProvider,
            Action<int> progress = null)
        {
            if (sampleCount < 1 || bossMaxHitPointsProvider == null)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            Parallel.For(0, sampleCount, options, offset =>
            {
                var runSeed = unchecked(firstRunSeed + offset);
                var bossHp = bossMaxHitPointsProvider(runSeed);
                if (bossHp <= 0f) throw new ArgumentOutOfRangeException(nameof(bossMaxHitPointsProvider));
                results[offset] = RunOne(
                    runSeed,
                    RecruitComponentPolicy.V3,
                    false,
                    EnemyHpCurveCandidate.CurrentProduction,
                    bossHp,
                    stopAfterW6BossResolution: true);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new W6BareFullScheduleCalibrationReport(firstRunSeed, sampleCount, 0f);
            for (var index = 0; index < results.Length; index++)
            {
                report.Add(unchecked(firstRunSeed + index), results[index]);
            }

            return report;
        }

        /// <summary>
        /// Runs the real shared-settlement schedule through the W6 Boss generation node. The
        /// optional salt swap is diagnostic-only and does not alter either production salt.
        /// </summary>
        public static W1ToW5SurvivalFunnelReport RunW1ToW5SurvivalFunnel(
            int firstRunSeed,
            int sampleCount,
            bool swapDeckInputs,
            Action<int> progress = null)
        {
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var completed = 0;
            var results = new CoreLoopRunResult[sampleCount];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            Parallel.For(0, sampleCount, options, offset =>
            {
                results[offset] = RunOne(
                    unchecked(firstRunSeed + offset),
                    RecruitComponentPolicy.V3,
                    false,
                    EnemyHpCurveCandidate.CurrentProduction,
                    SoulchainBinderConfiguration.GreyboxMaxHitPoints,
                    stopAtW6Start: true,
                    swapDeckInputs: swapDeckInputs);
                progress?.Invoke(Interlocked.Increment(ref completed));
            });

            var report = new W1ToW5SurvivalFunnelReport(firstRunSeed, sampleCount, swapDeckInputs);
            for (var index = 0; index < results.Length; index++)
            {
                report.Add(unchecked(firstRunSeed + index), results[index]);
            }

            return report;
        }

        internal static CoreLoopRunResult RunOne(
            int runSeed,
            RecruitComponentPolicy componentPolicy,
            bool collectCapacityAudit = false,
            EnemyHpCurveCandidate hpCandidate = EnemyHpCurveCandidate.CurrentProduction,
            float soulChainBossMaxHitPoints = SoulchainBinderConfiguration.GreyboxMaxHitPoints,
            bool stopAfterW6BossResolution = false,
            bool stopAtW6Start = false,
            bool swapDeckInputs = false,
            IItemRunSnapshotProvider itemSnapshotProvider = null,
            float stormcallerBossMaxHitPoints = StormcallerPriestConfiguration.GreyboxMaxHitPoints,
            float bloodcrownBossMaxHitPoints = BloodcrownTyrantConfiguration.GreyboxMaxHitPoints,
            float worldeaterBossMaxHitPoints = WorldeaterWyrmConfiguration.GreyboxMaxHitPoints,
            bool collectW12Calibration = false,
            bool stopAfterW12BossResolution = false,
            bool activateDiagnosticWinterveilAtW12 = false,
            bool directW12Calibration = false,
            int directCalibrationWave = 0)
        {
            if (directW12Calibration)
            {
                directCalibrationWave = 12;
            }

            if (directCalibrationWave != 0 && (directCalibrationWave < 6 || directCalibrationWave > 20))
            {
                throw new ArgumentOutOfRangeException(nameof(directCalibrationWave));
            }

            var catalog = GreyboxRecruitmentCatalog.Create();
            var match = new MatchController(
                runSeed,
                directCalibrationWave > 0
                    ? BattleSettlementDefinition.InitialMaxHeart * 10
                    : BattleSettlementDefinition.InitialMaxHeart);
            var layout = BattlefieldLayoutDefinitions.Default;
            var playerBoard = DragonBoundBoardLayout.Create(layout, TeamSide.Player);
            var aiBoard = DragonBoundBoardLayout.Create(layout, TeamSide.AI);
            var playerDestination = new BoardRecruitDestination(playerBoard);
            var aiDestination = new BoardRecruitDestination(aiBoard);
            var playerDeckSalt = swapDeckInputs ? AiDeckSalt : PlayerDeckSalt;
            var aiDeckSalt = swapDeckInputs ? PlayerDeckSalt : AiDeckSalt;
            var playerBagSeed = swapDeckInputs ? unchecked(runSeed ^ AiDeckSalt) : runSeed;
            var aiBagSeed = swapDeckInputs ? runSeed : unchecked(runSeed ^ AiDeckSalt);
            var playerBag = LimitedComponentBag.CreateBag(playerBagSeed, LimitedComponentBag.DefaultContentVersion, catalog);
            var aiSeed = aiBagSeed;
            var aiBag = LimitedComponentBag.CreateBag(aiSeed, LimitedComponentBag.DefaultContentVersion, catalog);
            var playerRecruitment = new RecruitmentService(
                match.Player,
                new RecruitDeck(
                    catalog,
                    unchecked(runSeed ^ playerDeckSalt),
                    "player",
                    playerBag,
                    shovelState: new ShovelRecruitmentState(() => playerBoard.GetPositions(CellType.Locked).Count),
                    componentPolicy: componentPolicy,
                    currentWaveProvider: () => match.CurrentWave),
                playerDestination);
            var aiRecruitment = new RecruitmentService(
                match.AI,
                new RecruitDeck(
                    catalog,
                    aiSeed,
                    "ai",
                    aiBag,
                    shovelState: new ShovelRecruitmentState(() => aiBoard.GetPositions(CellType.Locked).Count),
                    componentPolicy: componentPolicy,
                    currentWaveProvider: () => match.CurrentWave),
                aiDestination);
            var playerShovels = new ShovelUnlockService(playerBoard, playerDestination);
            var aiShovels = new ShovelUnlockService(aiBoard, aiDestination);
            var playerController = new BasicUnitAiController(
                playerBoard,
                playerDestination,
                playerRecruitment,
                playerShovels,
                match.Player);
            var aiController = new BasicUnitAiController(
                aiBoard,
                aiDestination,
                aiRecruitment,
                aiShovels,
                match.AI);
            playerController.Diagnostics.EmitLogs = false;
            aiController.Diagnostics.EmitLogs = false;

            var result = new CoreLoopRunResult();
            result.PlayerEnemyPressure = new EnemyPressureSideRun();
            result.AiEnemyPressure = new EnemyPressureSideRun();
            result.W6Calibration = new W6BareCalibrationRun();
            result.PlayerJointCalibration = new JointBalanceCalibrationSideRun();
            result.AiJointCalibration = new JointBalanceCalibrationSideRun();
            if (collectW12Calibration)
            {
                result.W12Calibration = new W12BuildEnvelopeCalibrationRun();
            }
            if (collectCapacityAudit)
            {
                result.PlayerCapacityAudit = new BoardBenchCapacitySideRun();
                result.AiCapacityAudit = new BoardBenchCapacitySideRun();
            }
            var elapsed = 0f;
            playerRecruitment.Attempted += attempt =>
            {
                result.Player.RecordRecruitment(attempt);
                result.PlayerCapacityAudit?.RecordRecruitment(attempt, playerDestination, playerBoard, playerController);
            };
            aiRecruitment.Attempted += attempt =>
            {
                result.AI.RecordRecruitment(attempt);
                result.AiCapacityAudit?.RecordRecruitment(attempt, aiDestination, aiBoard, aiController);
            };
            playerShovels.ShovelUsed += result.Player.RecordShovelUsed;
            aiShovels.ShovelUsed += result.AI.RecordShovelUsed;
            playerDestination.HeroPairLinked += linked => result.Player.RecordPairLinkCreated(linked.PairLink, elapsed);
            aiDestination.HeroPairLinked += linked => result.AI.RecordPairLinkCreated(linked.PairLink, elapsed);
            playerDestination.HeroPairUnlinked += unlinked => result.Player.RecordPairLinkBroken(unlinked.PairLink, unlinked.Reason, elapsed);
            aiDestination.HeroPairUnlinked += unlinked => result.AI.RecordPairLinkBroken(unlinked.PairLink, unlinked.Reason, elapsed);
            playerDestination.BasicUnitMerged += _ =>
            {
                result.Player.MergesPerformed++;
                result.PlayerCapacityAudit?.RecordMergePerformed();
            };
            aiDestination.BasicUnitMerged += _ =>
            {
                result.AI.MergesPerformed++;
                result.AiCapacityAudit?.RecordMergePerformed();
            };
            if (directCalibrationWave <= 0)
            {
                playerController.Tick();
                aiController.Tick();
            }

            TwentyWavePressureRuntime runtime = null;
            runtime = new TwentyWavePressureRuntime(
                match,
                playerDestination,
                aiDestination,
                runSeed,
                EnemyHpCurveCandidates.Create(hpCandidate),
                soulChainBossMaxHitPoints: soulChainBossMaxHitPoints,
                stormcallerBossMaxHitPoints: stormcallerBossMaxHitPoints,
                bloodcrownBossMaxHitPoints: bloodcrownBossMaxHitPoints,
                worldeaterBossMaxHitPoints: worldeaterBossMaxHitPoints,
                itemSnapshotProvider: itemSnapshotProvider)
            {
                EmitLogs = false
            };
            runtime.SoulChainCastEmitted += (side, value) =>
                result.W6Calibration.RecordCast(side, value, runtime);
            runtime.StormcallerCastEmitted += (side, value) =>
                result.W12Calibration?.GetSide(side).RecordCast(value);
            EnemyArchetype? playerLeakThisTick = null;
            EnemyArchetype? aiLeakThisTick = null;
            runtime.PlayerEnemyLifecycleEmitted += value =>
            {
                result.PlayerEnemyPressure.RecordLifecycle(value, elapsed);
                result.PlayerJointCalibration.RecordLifecycle(value, elapsed);
                result.W6Calibration.RecordLifecycle(TeamSide.Player, value, elapsed, playerDestination);
                result.W12Calibration?.GetSide(TeamSide.Player).RecordLifecycle(value, elapsed);
                if (value.Kind == EnemyLifecycleEventKind.Leaked)
                {
                    playerLeakThisTick = value.Archetype;
                }
            };
            runtime.AiEnemyLifecycleEmitted += value =>
            {
                result.AiEnemyPressure.RecordLifecycle(value, elapsed);
                result.AiJointCalibration.RecordLifecycle(value, elapsed);
                result.W6Calibration.RecordLifecycle(TeamSide.AI, value, elapsed, aiDestination);
                result.W12Calibration?.GetSide(TeamSide.AI).RecordLifecycle(value, elapsed);
                if (value.Kind == EnemyLifecycleEventKind.Leaked)
                {
                    aiLeakThisTick = value.Archetype;
                }
            };
            runtime.CombatEmitted += value =>
                RecordCombatXp(result.Player, value, TeamSide.Player);
            runtime.CombatEmitted += value =>
                RecordCombatXp(result.AI, value, TeamSide.AI);
            runtime.CombatEmitted += value =>
                result.W6Calibration.RecordCombat(value.Team, value, runtime.ElapsedRunTime);
            runtime.CombatEmitted += value =>
                result.W12Calibration?.GetSide(value.Team).RecordCombat(value, runtime.ElapsedRunTime);
            runtime.CombatEmitted += value =>
            {
                if (value.Team == TeamSide.Player)
                {
                    result.PlayerJointCalibration.RecordCombat(value, runtime.ElapsedRunTime);
                }
                else
                {
                    result.AiJointCalibration.RecordCombat(value, runtime.ElapsedRunTime);
                }
            };
            runtime.StartRun();
            CaptureWaveStart(result.Player, 1, playerRecruitment, playerDestination);
            CaptureWaveStart(result.AI, 1, aiRecruitment, aiDestination);

            if (directCalibrationWave > 0)
            {
                // CALIBRATION_FIXTURE: build a deterministic, non-empty board using the
                // existing controllers before jumping directly to W12. No Production path
                // calls this mode.
                DragonRouteHeroDevelopmentFactory.TrySpawnPair(
                    playerDestination,
                    DragonBoundHeroIds.DragonRider,
                    "direct." + directCalibrationWave + ".player",
                    out _);
                DragonRouteHeroDevelopmentFactory.TrySpawnPair(
                    aiDestination,
                    DragonBoundHeroIds.DragonRider,
                    "direct." + directCalibrationWave + ".ai",
                    out _);
                match.Player.AddResources(120);
                match.AI.AddResources(120);
                for (var decision = 0; decision < 24; decision++)
                {
                    playerController.Tick(0);
                    aiController.Tick(0);
                }

                if (!runtime.JumpToWave(directCalibrationWave))
                {
                    throw new InvalidOperationException("Direct Boss calibration fixture could not enter W" + directCalibrationWave + ".");
                }

                if (result.W12Calibration != null)
                {
                    result.W12Calibration.RecordDirectSetup(playerDestination, aiDestination);
                }
            }

            var previousWave = directCalibrationWave > 0 ? directCalibrationWave - 1 : runtime.CurrentWaveIndex;
            var previousPlayerSpawned = 0;
            var previousAiSpawned = 0;
            var previousPlayerLeaks = 0;
            var previousAiLeaks = 0;
            var postSchedule = 0f;
            var diagnosticWinterveilActivated = false;
            while (!runtime.IsComplete && elapsed < MaxRunSeconds)
            {
                playerController.Tick(runtime.CurrentWaveIndex);
                aiController.Tick(runtime.CurrentWaveIndex);
                playerLeakThisTick = null;
                aiLeakThisTick = null;
                runtime.Tick(TickSeconds);
                elapsed += TickSeconds;

                if (runtime.IsComplete && !result.MatchEnd.IsRecorded)
                {
                    result.MatchEnd.RecordGameplaySettlement(
                        runtime.CurrentWaveIndex,
                        runtime.ElapsedRunTime,
                        match.Player.HatchlingHealth,
                        match.AI.HatchlingHealth,
                        runtime.WavesExhausted,
                        playerLeakThisTick,
                        aiLeakThisTick);
                }

                result.PlayerCapacityAudit?.RecordTick(elapsed, TickSeconds, playerBoard, playerDestination, playerController);
                result.AiCapacityAudit?.RecordTick(elapsed, TickSeconds, aiBoard, aiDestination, aiController);

                RecordAlive(result.Player, runtime.CurrentWaveIndex, runtime.PlayerAliveEnemyCount);
                RecordAlive(result.AI, runtime.CurrentWaveIndex, runtime.AiAliveEnemyCount);
                result.PlayerEnemyPressure.RecordAlive(runtime.CurrentWaveIndex, runtime.PlayerAliveEnemyCount);
                result.AiEnemyPressure.RecordAlive(runtime.CurrentWaveIndex, runtime.AiAliveEnemyCount);
                result.PlayerEnemyPressure.TrackProgress(runtime.PlayerEnemyRegistry);
                result.AiEnemyPressure.TrackProgress(runtime.AiEnemyRegistry);
                RecordSpawns(
                    result.Player,
                    runtime.CurrentWaveIndex,
                    runtime.PlayerTotalSpawned - previousPlayerSpawned,
                    runtime.ElapsedRunTime);
                RecordSpawns(
                    result.AI,
                    runtime.CurrentWaveIndex,
                    runtime.AiTotalSpawned - previousAiSpawned,
                    runtime.ElapsedRunTime);
                previousPlayerSpawned = runtime.PlayerTotalSpawned;
                previousAiSpawned = runtime.AiTotalSpawned;
                if (result.Player.FirstLeakWave < 0 &&
                    runtime.PlayerTotalReachedGoal > previousPlayerLeaks)
                {
                    result.Player.FirstLeakWave = runtime.CurrentWaveIndex;
                }
                if (result.AI.FirstLeakWave < 0 &&
                    runtime.AiTotalReachedGoal > previousAiLeaks)
                {
                    result.AI.FirstLeakWave = runtime.CurrentWaveIndex;
                }
                previousPlayerLeaks = runtime.PlayerTotalReachedGoal;
                previousAiLeaks = runtime.AiTotalReachedGoal;

                if (runtime.CurrentWaveIndex != previousWave)
                {
                    result.PlayerEnemyPressure.RecordResidual(previousWave, runtime.PlayerLastEndedWaveResidual);
                    result.AiEnemyPressure.RecordResidual(previousWave, runtime.AiLastEndedWaveResidual);
                    if (previousWave == 6)
                    {
                        result.W6Calibration.Player.RecordW6End(
                            match.Player.HatchlingHealth,
                            match.Player.IsInstantDefeated,
                            runtime.CurrentWaveIndex);
                        result.W6Calibration.AI.RecordW6End(
                            match.AI.HatchlingHealth,
                            match.AI.IsInstantDefeated,
                            runtime.CurrentWaveIndex);
                        result.W6Calibration.Player.RecordW7Start(runtime.PlayerW6Boss != null && runtime.PlayerW6Boss.IsAlive);
                        result.W6Calibration.AI.RecordW7Start(runtime.AiW6Boss != null && runtime.AiW6Boss.IsAlive);
                    }
                    CaptureWaveEnd(result.Player, previousWave, playerRecruitment, playerDestination, playerBoard, playerController);
                    CaptureWaveEnd(result.AI, previousWave, aiRecruitment, aiDestination, aiBoard, aiController);
                    result.PlayerCapacityAudit?.CaptureWave(previousWave, playerBoard, playerDestination);
                    result.AiCapacityAudit?.CaptureWave(previousWave, aiBoard, aiDestination);
                    CaptureWaveStart(result.Player, runtime.CurrentWaveIndex, playerRecruitment, playerDestination);
                    CaptureWaveStart(result.AI, runtime.CurrentWaveIndex, aiRecruitment, aiDestination);
                    CaptureFunnelWaveEnd(result.Player, previousWave, match.Player, playerDestination, playerBoard);
                    CaptureFunnelWaveEnd(result.AI, previousWave, match.AI, aiDestination, aiBoard);
                    if (runtime.CurrentWaveIndex == 6)
                    {
                        result.W6Calibration.Player.RecordHittableSnapshot(runtime.PlayerW6Boss, TeamSide.Player, playerDestination);
                        result.W6Calibration.AI.RecordHittableSnapshot(runtime.AiW6Boss, TeamSide.AI, aiDestination);
                    }
                    if (runtime.CurrentWaveIndex == 12 && activateDiagnosticWinterveilAtW12 && !diagnosticWinterveilActivated)
                    {
                        var playerActivated = runtime.TryUseItem(TeamSide.Player, ItemIds.WinterveilRune, out var playerReason);
                        var aiActivated = runtime.TryUseItem(TeamSide.AI, ItemIds.WinterveilRune, out var aiReason);
                        result.W12Calibration?.Player.RecordItemActivation(playerActivated && string.Equals(playerReason, ItemOperationFailure.None, StringComparison.Ordinal));
                        result.W12Calibration?.AI.RecordItemActivation(aiActivated && string.Equals(aiReason, ItemOperationFailure.None, StringComparison.Ordinal));
                        diagnosticWinterveilActivated = true;
                    }
                    if (runtime.CurrentWaveIndex == 13)
                    {
                        result.W12Calibration?.Player.RecordW13Residual(runtime.PlayerLastEndedWaveResidual);
                        result.W12Calibration?.AI.RecordW13Residual(runtime.AiLastEndedWaveResidual);
                    }
                    previousWave = runtime.CurrentWaveIndex;
                    if (stopAtW6Start && runtime.CurrentWaveIndex == 6)
                    {
                        result.MatchEnd.RecordDeveloperStop(
                            runtime.CurrentWaveIndex,
                            runtime.ElapsedRunTime,
                            match.Player.HatchlingHealth,
                            match.AI.HatchlingHealth,
                            runtime.WavesExhausted);
                        runtime.StopRun();
                    }
                }

                if (stopAfterW6BossResolution && runtime.CurrentWaveIndex >= 7 &&
                    (result.W6Calibration.Player.BossLeaked || result.W6Calibration.AI.BossLeaked ||
                     (result.W6Calibration.Player.BossKilled && result.W6Calibration.AI.BossKilled) ||
                     (result.W6Calibration.Player.BossSpawned &&
                      runtime.ElapsedRunTime >= result.W6Calibration.Player.BossSpawnTimeSeconds + 60f)))
                {
                    result.MatchEnd.RecordDeveloperStop(
                        runtime.CurrentWaveIndex,
                        runtime.ElapsedRunTime,
                        match.Player.HatchlingHealth,
                        match.AI.HatchlingHealth,
                        runtime.WavesExhausted);
                    runtime.StopRun();
                }

                if (stopAfterW12BossResolution && runtime.CurrentWaveIndex >= 13 &&
                    (((result.W12Calibration?.Player.BossKilled ?? false) || (result.W12Calibration?.Player.BossReachedGoal ?? false)) &&
                     ((result.W12Calibration?.AI.BossKilled ?? false) || (result.W12Calibration?.AI.BossReachedGoal ?? false)) ||
                     (result.W12Calibration?.Player.BossSpawned ?? false) &&
                     runtime.ElapsedRunTime >= result.W12Calibration.Player.BossSpawnTimeSeconds + 60f))
                {
                    result.MatchEnd.RecordDeveloperStop(
                        runtime.CurrentWaveIndex,
                        runtime.ElapsedRunTime,
                        match.Player.HatchlingHealth,
                        match.AI.HatchlingHealth,
                        runtime.WavesExhausted);
                    runtime.StopRun();
                }

                if (runtime.WavesExhausted)
                {
                    postSchedule += TickSeconds;
                    if (postSchedule >= MaxPostScheduleSeconds)
                    {
                        result.MatchEnd.RecordDeveloperStop(
                            runtime.CurrentWaveIndex,
                            runtime.ElapsedRunTime,
                            match.Player.HatchlingHealth,
                            match.AI.HatchlingHealth,
                            runtime.WavesExhausted);
                        runtime.StopRun();
                    }
                }
            }

            if (!result.MatchEnd.IsRecorded)
            {
                result.MatchEnd.RecordTimeout(
                    runtime.CurrentWaveIndex,
                    runtime.ElapsedRunTime,
                    match.Player.HatchlingHealth,
                    match.AI.HatchlingHealth,
                    runtime.WavesExhausted);
            }

            if (runtime.CurrentWaveIndex >= 6)
            {
                result.W6Calibration.Player.RecordW6End(
                    match.Player.HatchlingHealth,
                    match.Player.IsInstantDefeated,
                    runtime.CurrentWaveIndex);
                result.W6Calibration.AI.RecordW6End(
                    match.AI.HatchlingHealth,
                    match.AI.IsInstantDefeated,
                    runtime.CurrentWaveIndex);
            }

            CaptureWaveEnd(result.Player, runtime.CurrentWaveIndex, playerRecruitment, playerDestination, playerBoard, playerController);
            CaptureWaveEnd(result.AI, runtime.CurrentWaveIndex, aiRecruitment, aiDestination, aiBoard, aiController);
            CaptureFunnelWaveEnd(result.Player, runtime.CurrentWaveIndex, match.Player, playerDestination, playerBoard);
            CaptureFunnelWaveEnd(result.AI, runtime.CurrentWaveIndex, match.AI, aiDestination, aiBoard);
            result.PlayerCapacityAudit?.CaptureWave(runtime.CurrentWaveIndex, playerBoard, playerDestination);
            result.AiCapacityAudit?.CaptureWave(runtime.CurrentWaveIndex, aiBoard, aiDestination);
            FinalizeSide(
                result.Player,
                runtime,
                match.Player,
                playerRecruitment,
                playerDestination,
                playerBoard,
                playerController,
                TeamSide.Player);
            FinalizeSide(
                result.AI,
                runtime,
                match.AI,
                aiRecruitment,
                aiDestination,
                aiBoard,
                aiController,
                TeamSide.AI);
            result.PlayerCapacityAudit?.FinalizeRecipeFailures(playerController);
            result.AiCapacityAudit?.FinalizeRecipeFailures(aiController);
            result.PlayerCapacityAudit?.Complete(elapsed, playerDestination, playerBoard);
            result.AiCapacityAudit?.Complete(elapsed, aiDestination, aiBoard);
            result.W6Calibration.SetResiduals(TeamSide.Player, result.PlayerEnemyPressure.ResidualAtNextWaveStart);
            result.W6Calibration.SetResiduals(TeamSide.AI, result.AiEnemyPressure.ResidualAtNextWaveStart);
            if (result.W12Calibration != null && result.MatchEnd.IsRecorded)
            {
                result.W12Calibration.Player.RecordMatchEnd(result.MatchEnd.Wave, result.MatchEnd.EndReason);
                result.W12Calibration.AI.RecordMatchEnd(result.MatchEnd.Wave, result.MatchEnd.EndReason);
            }
            return result;
        }

        private static void CaptureWaveStart(
            CoreLoopSideRun side,
            int wave,
            RecruitmentService recruitment,
            BoardRecruitDestination destination)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount || side.StartRecorded[wave])
            {
                return;
            }

            side.StartRecorded[wave] = true;
            side.RecruitAtStart[wave] = recruitment.CompletedRecruitments;
            side.HeroAtStart[wave] = destination.ActivePairLinkCount;
        }

        private static void CaptureFunnelWaveEnd(
            CoreLoopSideRun side,
            int wave,
            TeamState team,
            BoardRecruitDestination destination,
            BoardGrid board)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount || side.FunnelEndRecorded[wave])
            {
                return;
            }

            side.FunnelEndRecorded[wave] = true;
            side.HeartAtEnd[wave] = team == null ? -1 : team.HatchlingHealth;
            side.ResourcesAtEnd[wave] = team == null ? -1 : team.Resources;
            side.BoardOccupiedAtEnd[wave] = destination == null ? 0 : destination.DeployedCount;
            side.BenchOccupiedAtEnd[wave] = destination == null ? 0 : destination.CampCount;
            side.BasicUnitCountAtEnd[wave] = 0;
            if (destination != null)
            {
                foreach (var card in destination.GetBoardCards())
                {
                    if (card.Kind == RecruitItemKind.BasicUnit) side.BasicUnitCountAtEnd[wave]++;
                }
            }
        }

        private static void CaptureWaveEnd(
            CoreLoopSideRun side,
            int wave,
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            BoardGrid board,
            BasicUnitAiController controller)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount || side.EndRecorded[wave])
            {
                return;
            }

            side.EndRecorded[wave] = true;
            side.RecruitAtEnd[wave] = recruitment.CompletedRecruitments;
            side.HeroAtEnd[wave] = destination.ActivePairLinkCount;
            side.CaptureHeroXpSnapshot(wave, destination);
            side.LifecycleAtEnd[wave] = ComponentLifecycleSnapshot.Capture(recruitment, destination, board);
            side.AvailableRecipePairsAtEnd[wave] = controller.AvailableRecipePairCount;
            side.BlockedRecipePairsAtEnd[wave] = controller.BlockedRecipeCount;
            side.OpenCellsAtEnd[wave] = board.GetPositions(CellType.Battle).Count;
            side.ShovelsGeneratedAtEnd[wave] = side.ShovelsGenerated;
            side.ShovelsUsedAtEnd[wave] = side.ShovelsUsed;
            side.ShovelsDiscardedAtEnd[wave] = side.ShovelsDiscarded;
            side.BenchFullAtEnd[wave] = destination.CampCount >= board.GetPositions(CellType.Bench).Count;
            side.BoardPressureAtEnd[wave] = destination.DeployedCount >= side.OpenCellsAtEnd[wave];
        }

        private static void RecordSpawns(CoreLoopSideRun side, int wave, int spawnCount, float time)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount || spawnCount <= 0)
            {
                return;
            }

            side.SpawnCount[wave] += spawnCount;
            if (side.FirstSpawnTime[wave] < 0f)
            {
                side.FirstSpawnTime[wave] = time;
            }

            side.LastSpawnTime[wave] = time;
        }

        private static void RecordAlive(CoreLoopSideRun side, int wave, int alive)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount)
            {
                return;
            }

            side.AliveTotal[wave] += alive;
            side.AliveSamples[wave]++;
            if (alive > side.PeakAlive[wave])
            {
                side.PeakAlive[wave] = alive;
            }
        }

        private static void FinalizeSide(
            CoreLoopSideRun side,
            TwentyWavePressureRuntime runtime,
            TeamState team,
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            BoardGrid board,
            BasicUnitAiController controller,
            TeamSide teamSide)
        {
            side.ReachedWaveTwenty = runtime.CurrentWaveIndex >= 20;
            side.UnpairedComponentCount = CountUnpairedComponents(destination);
            side.RecruitStallCount = controller.RecruitStallCount;
            side.FirstRecruitStallWave = controller.FirstRecruitStallWave;
            side.LegacyCampPolicyBlockCount = controller.LegacyCampPolicyBlockCount;
            side.RecipeOpportunityCreated = controller.RecipeOpportunityCreated;
            side.RecipeFormationAttempted = controller.RecipeFormationAttempted;
            side.RecipeFormationSucceeded = controller.RecipeFormationSucceeded;
            side.RecipeFormationFailed = controller.RecipeFormationFailed;
            side.RecipeRetryCount = controller.RecipeRetryCount;
            side.RunEndWave = runtime.CurrentWaveIndex;
            side.RunEndLifecycle = ComponentLifecycleSnapshot.Capture(recruitment, destination, board);
            side.CaptureHeroXpSnapshot(Math.Min(TwentyWavePressureConfiguration.WaveCount, runtime.CurrentWaveIndex), destination);
            side.RunEndHeroCount = destination.ActivePairLinkCount;
            side.RunEndOpenCellCount = board.GetPositions(CellType.Battle).Count;
            side.BaseLeaks = teamSide == TeamSide.Player
                ? runtime.PlayerTotalReachedGoal
                : runtime.AiTotalReachedGoal;
            foreach (var pair in controller.RecruitStallCounts)
            {
                side.RecruitStallsByReason[pair.Key] = pair.Value;
            }
            foreach (var pair in controller.RecipeFailureCounts)
            {
                side.RecipeFailuresByReason[pair.Key] = pair.Value;
            }

            if (team.HatchlingHealth <= 0)
            {
                side.DeathWave = Math.Max(1, runtime.CurrentWaveIndex);
                side.DeathSnapshot = CaptureStructuralSnapshot(side, recruitment, destination, board);
            }
        }

        private static void RecordCombatXp(CoreLoopSideRun side, CombatEvent value, TeamSide expectedSide)
        {
            if (value.Team == expectedSide)
            {
                side.RecordCombat(value);
            }
        }

        private static CoreLoopStructuralSnapshot CaptureStructuralSnapshot(
            CoreLoopSideRun side,
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            BoardGrid board)
        {
            var basicUnitCount = 0;
            var highestBasicLevel = 0;
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind != RecruitItemKind.BasicUnit)
                {
                    continue;
                }

                basicUnitCount++;
                highestBasicLevel = Math.Max(highestBasicLevel, card.Level);
            }

            var lifecycle = side.RunEndLifecycle ?? ComponentLifecycleSnapshot.Capture(recruitment, destination, board);
            return new CoreLoopStructuralSnapshot(
                destination.ActivePairLinkCount,
                side.DistinctHeroIds.Count,
                board.GetPositions(CellType.Battle).Count,
                lifecycle.TotalDeliveredComponents,
                lifecycle.ComponentsDiscarded,
                recruitment.CompletedRecruitments,
                basicUnitCount,
                highestBasicLevel);
        }

        private static int CountUnpairedComponents(BoardRecruitDestination destination)
        {
            var count = 0;
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind == RecruitItemKind.HeroComponent &&
                    !destination.TryGetPairLinkForComponent(card.RuntimeId, out _))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public sealed class CoreLoopRhythmReport
    {
        internal CoreLoopRhythmReport(
            int sampleCount,
            CoreLoopTimingVerification timing,
            RecruitComponentPolicy componentPolicy,
            EnemyHpCurveCandidate hpCandidate = EnemyHpCurveCandidate.CurrentProduction)
        {
            SampleCount = sampleCount;
            Timing = timing;
            ComponentPolicy = componentPolicy;
            HpCandidate = hpCandidate;
            Player = new CoreLoopSideAggregate("Player", sampleCount);
            AI = new CoreLoopSideAggregate("AI", sampleCount);
            PlayerEnemyPressure = new EnemyPressureSideAggregate(sampleCount);
            AiEnemyPressure = new EnemyPressureSideAggregate(sampleCount);
            MatchEnd = new CoreLoopMatchEndAggregate(sampleCount);
        }

        public int SampleCount { get; }
        public CoreLoopTimingVerification Timing { get; }
        public RecruitComponentPolicy ComponentPolicy { get; }
        public EnemyHpCurveCandidate HpCandidate { get; }
        public CoreLoopSideAggregate Player { get; }
        public CoreLoopSideAggregate AI { get; }
        public CoreLoopMatchEndAggregate MatchEnd { get; }
        internal EnemyPressureSideAggregate PlayerEnemyPressure { get; }
        internal EnemyPressureSideAggregate AiEnemyPressure { get; }

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"CORE_LOOP_RHYTHM_{ComponentPolicy}_{HpCandidate} SampleCount={SampleCount}");
            builder.AppendLine(Timing.FormatReport());
            builder.Append(Player.FormatReport(Timing));
            builder.Append(PlayerEnemyPressure.Format("Player"));
            builder.Append(AI.FormatReport(Timing));
            builder.Append(MatchEnd.FormatReport());
            builder.Append(AiEnemyPressure.Format("AI"));
            return builder.ToString();
        }
    }

    public sealed class CoreLoopSideAggregate
    {
        private readonly int sampleCount;
        private readonly int[] endSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] startSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] recruitEndTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] recruitStartTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] heroEndTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] aliveTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] aliveSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] peakAlive = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] lifecycleRemainingTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] lifecycleBenchTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] lifecycleBoardUnpairedTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] lifecyclePairTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] lifecycleDiscardedTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] lifecycleDeliveredTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] lifecycleSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] availableRecipeTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] blockedRecipeTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] openCellTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] shovelGeneratedTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] shovelUsedTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly double[] shovelDiscardedTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] benchFullSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] boardPressureSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private double runEndWaveTotal;
        private double runEndDeliveredTotal;
        private double runEndRemainingTotal;
        private double runEndBenchTotal;
        private double runEndBoardUnpairedTotal;
        private double runEndPairLinkedTotal;
        private double runEndDiscardedTotal;
        private double runEndHeroTotal;
        private double runEndOpenCellTotal;
        private int runEndSamples;
        private readonly Dictionary<AiRecruitBlockedReason, int> stallReasons =
            new Dictionary<AiRecruitBlockedReason, int>();
        private readonly Dictionary<AiRecipeBlockedReason, int> recipeFailureReasons =
            new Dictionary<AiRecipeBlockedReason, int>();
        private readonly Dictionary<string, int> heroFormedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroKillTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroXpTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroFormationTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> heroFormationKillTotals = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> heroFormationXpTotals = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> heroFormationLevelTotals = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> heroFormationXpSamples = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> heroFormationLevelSamples = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> heroFormationDeathWaveTotals = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroFormationDeathWaveSamples = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroFormationReachedW12 = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroFormationReachedW16 = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroFormationReachedW20 = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double>[] heroXpWaveTotals = CreateHeroWaveMaps();
        private readonly int[] heroXpWaveSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly Dictionary<string, int> pairBreakReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<int> deathWaves = new List<int>();
        private readonly List<int> firstLeakWaves = new List<int>();
        private readonly List<int> baseLeaksPerRun = new List<int>();
        private readonly int[] deathWaveCounts = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly StructuralBucketAggregate[] deathStructure =
        {
            new StructuralBucketAggregate("W1-W6"),
            new StructuralBucketAggregate("W7-W12"),
            new StructuralBucketAggregate("W13-W16"),
            new StructuralBucketAggregate("W17+")
        };
        private readonly HeroDepthBucketAggregate[] heroDepth =
        {
            new HeroDepthBucketAggregate("0-1"),
            new HeroDepthBucketAggregate("2"),
            new HeroDepthBucketAggregate("3"),
            new HeroDepthBucketAggregate("4+")
        };
        private int runsWithStalls;
        private int totalStalls;
        private int legacyCampPolicyBlockTotal;
        private int firstStallWaveTotal;
        private int firstStallWaveSamples;
        private int reachedWaveTwenty;
        private int firstLeakWaveTotal;
        private int firstLeakWaveSamples;
        private int pairLinkTotal;
        private int pairLinksBrokenTotal;
        private double pairLinkLifetimeTotal;
        private int pairLinkLifetimeSamples;
        private int distinctHeroIdsTotal;
        private int lifecycleConservationFailures;
        private int recipeOpportunityTotal;
        private int recipeAttemptTotal;
        private int recipeSucceededTotal;
        private int recipeFailedTotal;
        private int recipeRetryTotal;
        private int unpairedComponentTotal;
        private int beforeW6AtLeast4;
        private int beforeW8AtLeast5;
        private int beforeW10AtLeast6;
        private int beforeW10AtLeast7;
        private int beforeW10AtLeast8;
        private int beforeW12AtLeast8;
        private int beforeW12AtLeast9;
        private int beforeW12AtLeast10;
        private int beforeW12AtLeast11;
        private int beforeW6Samples;
        private int beforeW8Samples;
        private int beforeW10Samples;
        private int beforeW12Samples;
        private int w6OneHero;
        private int w8OneHero;
        private int w10TwoHeroes;
        private int w12TwoHeroes;
        private int w12ThreeHeroes;
        private int w6EndOneHero;
        private int w8EndOneHero;
        private int w8EndTwoHeroes;
        private int w10EndTwoHeroes;
        private int w10EndThreeHeroes;
        private int w12EndTwoHeroes;
        private int w12EndThreeHeroes;
        private int w12EndFourHeroes;
        private int basicUnitKillsTotal;
        private int heroKillsTotal;
        private int unattributedKillsTotal;
        private int totalHeroXpFromKills;
        private double highestHeroXpTotal;
        private double lowestActiveHeroXpTotal;
        private double xpGiniTotal;
        private int xpDistributionSamples;

        internal CoreLoopSideAggregate(string label, int sampleCount)
        {
            Label = label;
            this.sampleCount = sampleCount;
        }

        public string Label { get; }
        public int ComponentConservationFailures => lifecycleConservationFailures;
        public int RecruitStallTotal => totalStalls;

        internal void Add(CoreLoopSideRun run)
        {
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                if (run.EndRecorded[wave])
                {
                    endSamples[wave]++;
                    recruitEndTotals[wave] += run.RecruitAtEnd[wave];
                    heroEndTotals[wave] += run.HeroAtEnd[wave];
                    var lifecycle = run.LifecycleAtEnd[wave];
                    if (lifecycle != null)
                    {
                        lifecycleSamples[wave]++;
                        lifecycleRemainingTotals[wave] += lifecycle.RemainingInBag;
                        lifecycleBenchTotals[wave] += lifecycle.ComponentsInBench;
                        lifecycleBoardUnpairedTotals[wave] += lifecycle.ComponentsOnBoardUnpaired;
                        lifecyclePairTotals[wave] += lifecycle.ComponentsInPairLinks;
                        lifecycleDiscardedTotals[wave] += lifecycle.ComponentsDiscarded;
                        lifecycleDeliveredTotals[wave] += lifecycle.TotalDeliveredComponents;
                        availableRecipeTotals[wave] += run.AvailableRecipePairsAtEnd[wave];
                        blockedRecipeTotals[wave] += run.BlockedRecipePairsAtEnd[wave];
                        openCellTotals[wave] += run.OpenCellsAtEnd[wave];
                        shovelGeneratedTotals[wave] += run.ShovelsGeneratedAtEnd[wave];
                        shovelUsedTotals[wave] += run.ShovelsUsedAtEnd[wave];
                        shovelDiscardedTotals[wave] += run.ShovelsDiscardedAtEnd[wave];
                        if (run.BenchFullAtEnd[wave]) benchFullSamples[wave]++;
                        if (run.BoardPressureAtEnd[wave]) boardPressureSamples[wave]++;
                        if (!lifecycle.IsConserved) lifecycleConservationFailures++;
                    }

                    var heroSnapshot = run.HeroXpAtEndByWave[wave];
                    if (heroSnapshot != null)
                    {
                        heroXpWaveSamples[wave]++;
                        foreach (var hero in heroSnapshot)
                        {
                            heroXpWaveTotals[wave].TryGetValue(hero.Key, out var totalXp);
                            heroXpWaveTotals[wave][hero.Key] = totalXp + hero.Value;
                        }
                    }

                    if (wave == 6 && run.HeroAtEnd[wave] >= 1) w6EndOneHero++;
                    if (wave == 8)
                    {
                        if (run.HeroAtEnd[wave] >= 1) w8EndOneHero++;
                        if (run.HeroAtEnd[wave] >= 2) w8EndTwoHeroes++;
                    }
                    if (wave == 10)
                    {
                        if (run.HeroAtEnd[wave] >= 2) w10EndTwoHeroes++;
                        if (run.HeroAtEnd[wave] >= 3) w10EndThreeHeroes++;
                    }
                    if (wave == 12)
                    {
                        if (run.HeroAtEnd[wave] >= 2) w12EndTwoHeroes++;
                        if (run.HeroAtEnd[wave] >= 3) w12EndThreeHeroes++;
                        if (run.HeroAtEnd[wave] >= 4) w12EndFourHeroes++;
                    }
                }

                if (run.StartRecorded[wave])
                {
                    startSamples[wave]++;
                    recruitStartTotals[wave] += run.RecruitAtStart[wave];
                }

                aliveTotals[wave] += run.AliveTotal[wave];
                aliveSamples[wave] += run.AliveSamples[wave];
                peakAlive[wave] = Math.Max(peakAlive[wave], run.PeakAlive[wave]);
            }

            if (run.RunEndLifecycle != null)
            {
                var lifecycle = run.RunEndLifecycle;
                runEndSamples++;
                runEndWaveTotal += run.RunEndWave;
                runEndDeliveredTotal += lifecycle.TotalDeliveredComponents;
                runEndRemainingTotal += lifecycle.RemainingInBag;
                runEndBenchTotal += lifecycle.ComponentsInBench;
                runEndBoardUnpairedTotal += lifecycle.ComponentsOnBoardUnpaired;
                runEndPairLinkedTotal += lifecycle.ComponentsInPairLinks;
                runEndDiscardedTotal += lifecycle.ComponentsDiscarded;
                runEndHeroTotal += run.RunEndHeroCount;
                runEndOpenCellTotal += run.RunEndOpenCellCount;
                if (!lifecycle.IsConserved) lifecycleConservationFailures++;
            }

            if (run.StartRecorded[6])
            {
                beforeW6Samples++;
                if (run.RecruitAtStart[6] >= 4) beforeW6AtLeast4++;
                if (run.HeroAtStart[6] >= 1) w6OneHero++;
            }
            if (run.StartRecorded[8])
            {
                beforeW8Samples++;
                if (run.RecruitAtStart[8] >= 5) beforeW8AtLeast5++;
                if (run.HeroAtStart[8] >= 1) w8OneHero++;
            }
            if (run.StartRecorded[10])
            {
                beforeW10Samples++;
                if (run.RecruitAtStart[10] >= 6) beforeW10AtLeast6++;
                if (run.RecruitAtStart[10] >= 7) beforeW10AtLeast7++;
                if (run.RecruitAtStart[10] >= 8) beforeW10AtLeast8++;
                if (run.HeroAtStart[10] >= 2) w10TwoHeroes++;
            }
            if (run.StartRecorded[12])
            {
                beforeW12Samples++;
                if (run.RecruitAtStart[12] >= 8) beforeW12AtLeast8++;
                if (run.RecruitAtStart[12] >= 9) beforeW12AtLeast9++;
                if (run.RecruitAtStart[12] >= 10) beforeW12AtLeast10++;
                if (run.RecruitAtStart[12] >= 11) beforeW12AtLeast11++;
                if (run.HeroAtStart[12] >= 2) w12TwoHeroes++;
                if (run.HeroAtStart[12] >= 3) w12ThreeHeroes++;
            }

            if (run.DeathWave > 0)
            {
                deathWaves.Add(run.DeathWave);
                deathWaveCounts[run.DeathWave]++;
                if (run.DeathSnapshot != null)
                {
                    deathStructure[GetDeathStructureBucket(run.DeathWave)].Add(run.DeathSnapshot);
                }
            }
            if (run.ReachedWaveTwenty)
            {
                reachedWaveTwenty++;
            }
            if (run.FirstLeakWave > 0)
            {
                firstLeakWaveTotal += run.FirstLeakWave;
                firstLeakWaveSamples++;
                firstLeakWaves.Add(run.FirstLeakWave);
            }
            baseLeaksPerRun.Add(run.BaseLeaks);
            heroDepth[GetHeroDepthBucket(run.RunEndHeroCount)].Add(run);

            pairLinkTotal += run.PairLinksFormed;
            pairLinksBrokenTotal += run.PairLinksBroken;
            pairLinkLifetimeTotal += run.PairLinkLifetimeTotal;
            pairLinkLifetimeSamples += run.PairLinksBroken;
            distinctHeroIdsTotal += run.DistinctHeroIds.Count;
            recipeOpportunityTotal += run.RecipeOpportunityCreated;
            recipeAttemptTotal += run.RecipeFormationAttempted;
            recipeSucceededTotal += run.RecipeFormationSucceeded;
            recipeFailedTotal += run.RecipeFormationFailed;
            recipeRetryTotal += run.RecipeRetryCount;
            unpairedComponentTotal += run.UnpairedComponentCount;
            totalStalls += run.RecruitStallCount;
            legacyCampPolicyBlockTotal += run.LegacyCampPolicyBlockCount;
            if (run.RecruitStallCount > 0)
            {
                runsWithStalls++;
            }
            if (run.FirstRecruitStallWave > 0)
            {
                firstStallWaveTotal += run.FirstRecruitStallWave;
                firstStallWaveSamples++;
            }
            basicUnitKillsTotal += run.BasicUnitKills;
            heroKillsTotal += run.HeroKills;
            unattributedKillsTotal += run.UnattributedKills;
            totalHeroXpFromKills += run.TotalHeroXpFromKills;
            if (run.HeroXpTotals.Count > 0)
            {
                var activeValues = new List<int>(run.HeroXpTotals.Values);
                var highest = 0;
                var lowest = int.MaxValue;
                foreach (var xp in activeValues)
                {
                    highest = Math.Max(highest, xp);
                    lowest = Math.Min(lowest, xp);
                }
                highestHeroXpTotal += highest;
                lowestActiveHeroXpTotal += lowest == int.MaxValue ? 0 : lowest;
                xpGiniTotal += CalculateGini(activeValues);
                xpDistributionSamples++;
            }
            foreach (var pair in run.RecruitStallsByReason)
            {
                stallReasons.TryGetValue(pair.Key, out var count);
                stallReasons[pair.Key] = count + pair.Value;
            }
            foreach (var pair in run.HeroFormedCounts)
            {
                heroFormedCounts.TryGetValue(pair.Key, out var count);
                heroFormedCounts[pair.Key] = count + pair.Value;
            }
            foreach (var pair in run.HeroKillCounts)
            {
                heroKillTotals.TryGetValue(pair.Key, out var count);
                heroKillTotals[pair.Key] = count + pair.Value;
            }
            foreach (var pair in run.HeroXpTotals)
            {
                heroXpTotals.TryGetValue(pair.Key, out var totalXp);
                heroXpTotals[pair.Key] = totalXp + pair.Value;
            }
            foreach (var formation in run.HeroFormations)
            {
                heroFormationTotals.TryGetValue(formation.HeroId, out var formed);
                heroFormationTotals[formation.HeroId] = formed + 1;
                heroFormationKillTotals.TryGetValue(formation.HeroId, out var formationKills);
                heroFormationKillTotals[formation.HeroId] = formationKills + formation.Kills;
                heroFormationXpTotals.TryGetValue(formation.HeroId, out var formationXp);
                heroFormationXpTotals[formation.HeroId] = formationXp + formation.XP;
                heroFormationLevelTotals.TryGetValue(formation.HeroId, out var formationLevels);
                heroFormationLevelTotals[formation.HeroId] = formationLevels + formation.Level;
                if (run.DeathWave > 0)
                {
                    heroFormationDeathWaveTotals.TryGetValue(formation.HeroId, out var deathWaveTotal);
                    heroFormationDeathWaveTotals[formation.HeroId] = deathWaveTotal + run.DeathWave;
                    heroFormationDeathWaveSamples.TryGetValue(formation.HeroId, out var deathWaveSamples);
                    heroFormationDeathWaveSamples[formation.HeroId] = deathWaveSamples + 1;
                }

                if (run.EndRecorded[12] || run.RunEndWave >= 12)
                {
                    heroFormationReachedW12.TryGetValue(formation.HeroId, out var reached);
                    heroFormationReachedW12[formation.HeroId] = reached + 1;
                }

                if (run.EndRecorded[16] || run.RunEndWave >= 16)
                {
                    heroFormationReachedW16.TryGetValue(formation.HeroId, out var reached);
                    heroFormationReachedW16[formation.HeroId] = reached + 1;
                }

                if (run.ReachedWaveTwenty)
                {
                    heroFormationReachedW20.TryGetValue(formation.HeroId, out var reached);
                    heroFormationReachedW20[formation.HeroId] = reached + 1;
                }
                if (!heroFormationXpSamples.TryGetValue(formation.HeroId, out var xpSamples))
                {
                    xpSamples = new List<int>();
                    heroFormationXpSamples.Add(formation.HeroId, xpSamples);
                }
                xpSamples.Add(formation.XP);
                if (!heroFormationLevelSamples.TryGetValue(formation.HeroId, out var levelSamples))
                {
                    levelSamples = new List<int>();
                    heroFormationLevelSamples.Add(formation.HeroId, levelSamples);
                }
                levelSamples.Add(formation.Level);
            }
            foreach (var pair in run.PairBreakReasons)
            {
                pairBreakReasons.TryGetValue(pair.Key, out var count);
                pairBreakReasons[pair.Key] = count + pair.Value;
            }
            foreach (var pair in run.RecipeFailuresByReason)
            {
                recipeFailureReasons.TryGetValue(pair.Key, out var count);
                recipeFailureReasons[pair.Key] = count + pair.Value;
            }
        }

        public string FormatReport(CoreLoopTimingVerification timing)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                $"[{Label}] Recruit W1End={EndRecruit(1):0.00} W3End={EndRecruit(3):0.00} " +
                $"W6End={EndRecruit(6):0.00} W8End={EndRecruit(8):0.00} W10End={EndRecruit(10):0.00} " +
                $"W12End={EndRecruit(12):0.00} W16Start={StartRecruit(16):0.00}");
            builder.AppendLine(
                $"[{Label}] RecruitThresholds BeforeW6>=4={Rate(beforeW6AtLeast4, beforeW6Samples):P2} " +
                $"BeforeW8>=5={Rate(beforeW8AtLeast5, beforeW8Samples):P2} " +
                $"BeforeW10>=6/7/8={Rate(beforeW10AtLeast6, beforeW10Samples):P2}/" +
                $"{Rate(beforeW10AtLeast7, beforeW10Samples):P2}/{Rate(beforeW10AtLeast8, beforeW10Samples):P2} " +
                $"BeforeW12>=8/9/10/11={Rate(beforeW12AtLeast8, beforeW12Samples):P2}/" +
                $"{Rate(beforeW12AtLeast9, beforeW12Samples):P2}/" +
                $"{Rate(beforeW12AtLeast10, beforeW12Samples):P2}/" +
                $"{Rate(beforeW12AtLeast11, beforeW12Samples):P2}");
            builder.AppendLine(
                $"[{Label}] RecruitStall Runs={runsWithStalls} ({Rate(runsWithStalls, sampleCount):P2}) " +
                $"Total={totalStalls} FirstWave={Average(firstStallWaveTotal, firstStallWaveSamples):0.00} " +
                $"Reasons={FormatMap(stallReasons)} LegacyCampPolicyWouldBlock=" +
                $"{Average(legacyCampPolicyBlockTotal, sampleCount):0.00}/run");
            builder.AppendLine(
                $"[{Label}] Heroes W3={EndHero(3):0.00} W6={EndHero(6):0.00} W8={EndHero(8):0.00} " +
                $"W10={EndHero(10):0.00} W12={EndHero(12):0.00} PairLinksPerRun=" +
                $"{Average(pairLinkTotal, sampleCount):0.00} UnpairedComponents={Average(unpairedComponentTotal, sampleCount):0.00} " +
                $"BeforeW6>=1={Rate(w6OneHero, beforeW6Samples):P2} BeforeW8>=1={Rate(w8OneHero, beforeW8Samples):P2} " +
                $"BeforeW10>=2={Rate(w10TwoHeroes, beforeW10Samples):P2} " +
                $"BeforeW12>=2/3={Rate(w12TwoHeroes, beforeW12Samples):P2}/{Rate(w12ThreeHeroes, beforeW12Samples):P2}");
            builder.AppendLine(
                $"[{Label}] HeroThresholds W6>=1={Rate(w6EndOneHero, endSamples[6]):P2} " +
                $"W8>=1/2={Rate(w8EndOneHero, endSamples[8]):P2}/{Rate(w8EndTwoHeroes, endSamples[8]):P2} " +
                $"W10>=2/3={Rate(w10EndTwoHeroes, endSamples[10]):P2}/{Rate(w10EndThreeHeroes, endSamples[10]):P2} " +
                $"W12>=2/3/4={Rate(w12EndTwoHeroes, endSamples[12]):P2}/{Rate(w12EndThreeHeroes, endSamples[12]):P2}/{Rate(w12EndFourHeroes, endSamples[12]):P2}");
            builder.AppendLine(
                $"[{Label}] HeroRecipeLifecycle DistinctHeroIdsPerRun={Average(distinctHeroIdsTotal, sampleCount):0.00} " +
                $"HeroFormedCounts={FormatMap(heroFormedCounts)} PairLinksCreated={pairLinkTotal} " +
                $"PairLinksBroken={pairLinksBrokenTotal} AverageBrokenLifetime={Average(pairLinkLifetimeTotal, pairLinkLifetimeSamples):0.00}s " +
                $"BreakReasons={FormatMap(pairBreakReasons)}");
            builder.AppendLine(
                $"[{Label}] HeroXPCombat BasicUnitKills={basicUnitKillsTotal} HeroKills={heroKillsTotal} " +
                $"UnattributedKills={unattributedKillsTotal} XPFromKills={totalHeroXpFromKills} " +
                $"HighestHeroXP={Average(highestHeroXpTotal, xpDistributionSamples):0.00} " +
                $"LowestActiveHeroXP={Average(lowestActiveHeroXpTotal, xpDistributionSamples):0.00} " +
                $"XPGini={Average(xpGiniTotal, xpDistributionSamples):0.000} " +
                $"XPShareByHero={FormatHeroXpShares()} HeroKillsByHero={FormatMap(heroKillTotals)}");
            foreach (var wave in new[] { 3, 6, 8, 10, 12, 16 })
            {
                builder.AppendLine($"[{Label}] HeroXP W{wave} {FormatHeroXpAtWave(wave)}");
            }
            builder.AppendLine($"[{Label}] HeroFormationXP {FormatHeroFormationReport()}");
            builder.AppendLine(
                $"[{Label}] RecipeAttempts Opportunities={recipeOpportunityTotal} Attempts={recipeAttemptTotal} " +
                $"Succeeded={recipeSucceededTotal} Failed={recipeFailedTotal} Retries={recipeRetryTotal} " +
                $"FailureReasons={FormatMap(recipeFailureReasons)}");
            foreach (var wave in new[] { 3, 6, 8, 10, 12, 16 })
            {
                builder.AppendLine(
                    $"[{Label}] ComponentLifecycle W{wave} Delivered={LifecycleAverage(lifecycleDeliveredTotals, wave):0.00} " +
                    $"Remaining={LifecycleAverage(lifecycleRemainingTotals, wave):0.00} Camp=0.00 " +
                    $"Bench={LifecycleAverage(lifecycleBenchTotals, wave):0.00} " +
                    $"BoardUnpaired={LifecycleAverage(lifecycleBoardUnpairedTotals, wave):0.00} " +
                    $"PairLinkComponents={LifecycleAverage(lifecyclePairTotals, wave):0.00} " +
                    $"Discarded={LifecycleAverage(lifecycleDiscardedTotals, wave):0.00} " +
                    $"ComponentDiscardRate={LifecycleDiscardRate(wave):P2} " +
                    $"AvailableRecipes={LifecycleAverage(availableRecipeTotals, wave):0.00} " +
                    $"BlockedRecipes={LifecycleAverage(blockedRecipeTotals, wave):0.00}");
            }
            foreach (var wave in new[] { 3, 6, 8, 10, 12 })
            {
                builder.AppendLine(
                    $"[{Label}] ShovelBoard W{wave} OpenCells={LifecycleAverage(openCellTotals, wave):0.00} " +
                    $"ShovelsGenerated={LifecycleAverage(shovelGeneratedTotals, wave):0.00} " +
                    $"ShovelsUsed={LifecycleAverage(shovelUsedTotals, wave):0.00} " +
                    $"ShovelsDiscarded={LifecycleAverage(shovelDiscardedTotals, wave):0.00} " +
                    $"Heroes={EndHero(wave):0.00} " +
                    $"BenchFullRate={Rate(benchFullSamples[wave], endSamples[wave]):P2} " +
                    $"BoardPressureRate={Rate(boardPressureSamples[wave], endSamples[wave]):P2}");
            }
            builder.AppendLine(
                $"[{Label}] RunEnd Wave={Average(runEndWaveTotal, runEndSamples):0.00} " +
                $"Delivered={Average(runEndDeliveredTotal, runEndSamples):0.00} " +
                $"Remaining={Average(runEndRemainingTotal, runEndSamples):0.00} " +
                $"Bench={Average(runEndBenchTotal, runEndSamples):0.00} " +
                $"BoardUnpaired={Average(runEndBoardUnpairedTotal, runEndSamples):0.00} " +
                $"PairLinkComponents={Average(runEndPairLinkedTotal, runEndSamples):0.00} " +
                $"Discarded={Average(runEndDiscardedTotal, runEndSamples):0.00} " +
                $"ComponentDiscardRate={Rate(runEndDiscardedTotal, runEndDeliveredTotal):P2} " +
                $"Heroes={Average(runEndHeroTotal, runEndSamples):0.00} " +
                $"OpenCells={Average(runEndOpenCellTotal, runEndSamples):0.00}");
            builder.AppendLine($"[{Label}] ComponentConservationFailures={lifecycleConservationFailures}");
            builder.AppendLine(
                $"[{Label}] Combat FirstLeakWave={Average(firstLeakWaveTotal, firstLeakWaveSamples):0.00} " +
                $"AverageDeathWave={AverageDeathWave():0.00} MedianDeathWave={MedianDeathWave():0.00} " +
                $"Death<=W1={DeathRate(1):P2} <=W3={DeathRate(3):P2} <=W6={DeathRate(6):P2} " +
                $"<=W8={DeathRate(8):P2} <=W10={DeathRate(10):P2} <=W12={DeathRate(12):P2} " +
                $"<=W16={DeathRate(16):P2} ReachedW20={Rate(reachedWaveTwenty, sampleCount):P2}");
            builder.AppendLine(
                $"[{Label}] Survival ReachedW3={Rate(endSamples[3], sampleCount):P2} " +
                $"ReachedW6={Rate(endSamples[6], sampleCount):P2} ReachedW7={Rate(endSamples[7], sampleCount):P2} " +
                $"ReachedW8={Rate(endSamples[8], sampleCount):P2} ReachedW10={Rate(endSamples[10], sampleCount):P2} " +
                $"ReachedW12={Rate(endSamples[12], sampleCount):P2} ReachedW13={Rate(endSamples[13], sampleCount):P2} " +
                $"ReachedW15={Rate(endSamples[15], sampleCount):P2} ReachedW16={Rate(endSamples[16], sampleCount):P2} " +
                $"ReachedW20={Rate(reachedWaveTwenty, sampleCount):P2}");
            builder.AppendLine(
                $"[{Label}] CombatPercentiles FirstLeakWave(LeakingRuns) Mean={Average(firstLeakWaveTotal, firstLeakWaveSamples):0.00} " +
                $"Median={Percentile(firstLeakWaves, 0.50):0.00} P25={Percentile(firstLeakWaves, 0.25):0.00} " +
                $"P75={Percentile(firstLeakWaves, 0.75):0.00} BaseLeaksPerRun Mean={Average(baseLeaksPerRun):0.00} " +
                $"Median={Percentile(baseLeaksPerRun, 0.50):0.00} P90={Percentile(baseLeaksPerRun, 0.90):0.00}");
            builder.AppendLine($"[{Label}] DeathWaveDistribution {FormatDeathWaveDistribution()}");
            builder.AppendLine(
                $"[{Label}] DeathWindows W1-W6={Rate(CountDeathsInRange(1, 6), sampleCount):P2} " +
                $"W7-W12={Rate(CountDeathsInRange(7, 12), sampleCount):P2} " +
                $"W13-W16={Rate(CountDeathsInRange(13, 16), sampleCount):P2} " +
                $"W17-W20={Rate(CountDeathsInRange(17, 20), sampleCount):P2} " +
                $"ReachedW20={Rate(reachedWaveTwenty, sampleCount):P2}");
            foreach (var bucket in deathStructure)
            {
                builder.AppendLine($"[{Label}] DeathStructure {bucket.Format()}");
            }
            foreach (var bucket in heroDepth)
            {
                builder.AppendLine($"[{Label}] HeroRunDepth {bucket.Format()}");
            }
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                var schedule = timing.GetWave(wave);
                builder.AppendLine(
                    $"[{Label}] Timing W{wave} SpawnCount={schedule.SpawnCount} SpawnInterval={schedule.SpawnIntervalSeconds:0.00} " +
                    $"FirstSpawn={schedule.FirstSpawnTimeSeconds:0.00} LastSpawn={schedule.LastSpawnTimeSeconds:0.00} " +
                    $"NextWaveFirstSpawn={schedule.NextWaveFirstSpawnTimeSeconds:0.00} " +
                    $"ActualInterWaveGap={schedule.ActualInterWaveGapSeconds:0.00} " +
                    $"AverageAlive={Average(aliveTotals[wave], aliveSamples[wave]):0.00} PeakAlive={peakAlive[wave]}");
            }

            return builder.ToString();
        }

        private double EndRecruit(int wave) => Average(recruitEndTotals[wave], endSamples[wave]);
        private double StartRecruit(int wave) => Average(recruitStartTotals[wave], startSamples[wave]);
        private double EndHero(int wave) => Average(heroEndTotals[wave], endSamples[wave]);
        private double LifecycleAverage(double[] values, int wave) => Average(values[wave], lifecycleSamples[wave]);
        private double LifecycleDiscardRate(int wave) => Rate(
            LifecycleAverage(lifecycleDiscardedTotals, wave),
            LifecycleAverage(lifecycleDeliveredTotals, wave));
        private string FormatHeroXpAtWave(int wave)
        {
            if (heroXpWaveSamples[wave] == 0)
            {
                return "NoSnapshot";
            }

            var builder = new StringBuilder();
            var first = true;
            foreach (var pair in heroXpWaveTotals[wave])
            {
                if (!first) builder.Append(' ');
                first = false;
                builder.Append(pair.Key).Append('=').Append(
                    (pair.Value / heroXpWaveSamples[wave]).ToString("0.00"));
            }
            return builder.Length == 0 ? "None" : builder.ToString();
        }

        private string FormatHeroFormationReport()
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var recipe in FrozenHeroConfigurationCatalog.Configuration.Recipes)
            {
                if (!first) builder.Append("; ");
                first = false;
                var heroId = recipe.HeroId;
                heroFormationTotals.TryGetValue(heroId, out var formationCount);
                heroFormationKillTotals.TryGetValue(heroId, out var kills);
                heroFormationXpTotals.TryGetValue(heroId, out var xp);
                heroFormationLevelTotals.TryGetValue(heroId, out var levels);
                heroFormationXpSamples.TryGetValue(heroId, out var samples);
                heroFormationLevelSamples.TryGetValue(heroId, out var levelSamples);
                heroFormationDeathWaveTotals.TryGetValue(heroId, out var deathWaves);
                heroFormationDeathWaveSamples.TryGetValue(heroId, out var deathWaveSamples);
                heroFormationReachedW12.TryGetValue(heroId, out var reachedW12);
                heroFormationReachedW16.TryGetValue(heroId, out var reachedW16);
                heroFormationReachedW20.TryGetValue(heroId, out var reachedW20);
                heroKillTotals.TryGetValue(heroId, out var killCount);
                heroXpTotals.TryGetValue(heroId, out var totalXp);
                builder.Append(heroId).Append(" FormationCount=").Append(formationCount)
                    .Append(" KillCount=").Append(killCount)
                    .Append(" KillShare=").Append(HeroKillShare(heroId).ToString("P2"))
                    .Append(" AvgKillsWhenFormed=").Append(Average(kills, formationCount).ToString("0.00"))
                    .Append(" AvgXPWhenFormed=").Append(Average(xp, formationCount).ToString("0.00"))
                    .Append(" AvgLevelWhenFormed=").Append(Average(levels, formationCount).ToString("0.00"))
                    .Append(" P50XP=").Append(Percentile(samples, 0.50).ToString("0.00"))
                    .Append(" P90XP=").Append(Percentile(samples, 0.90).ToString("0.00"))
                    .Append(" P50Level=").Append(Percentile(levelSamples, 0.50).ToString("0.00"))
                    .Append(" P90Level=").Append(Percentile(levelSamples, 0.90).ToString("0.00"))
                    .Append(" XPShare=").Append(HeroXpShare(heroId).ToString("P2"))
                    .Append(" Lv1Stagnation=").Append(Rate(CountAtMost(levelSamples, 1), formationCount).ToString("P2"))
                    .Append(" AvgDeathWaveWhenFormed=").Append(
                        deathWaveSamples == 0 ? "NA" : Average(deathWaves, deathWaveSamples).ToString("0.00"))
                    .Append(" DeathSamples=").Append(deathWaveSamples)
                    .Append(" ReachedW12=").Append(Rate(reachedW12, formationCount).ToString("P2"))
                    .Append(" ReachedW16=").Append(Rate(reachedW16, formationCount).ToString("P2"))
                    .Append(" ReachedW20=").Append(Rate(reachedW20, formationCount).ToString("P2"));
            }
            return builder.Length == 0 ? "None" : builder.ToString();
        }

        private string FormatHeroXpShares()
        {
            long total = 0;
            foreach (var pair in heroXpTotals)
            {
                total += pair.Value;
            }

            if (total <= 0)
            {
                return "None";
            }

            var builder = new StringBuilder();
            var first = true;
            foreach (var pair in heroXpTotals)
            {
                if (!first) builder.Append(' ');
                first = false;
                builder.Append(pair.Key).Append('=').Append(
                    (pair.Value / (double)total).ToString("P2"));
            }
            return builder.ToString();
        }
        private double HeroKillShare(string heroId)
        {
            long total = 0;
            foreach (var pair in heroKillTotals) total += pair.Value;
            return total == 0 || !heroKillTotals.TryGetValue(heroId, out var kills) ? 0d : kills / (double)total;
        }
        private double HeroXpShare(string heroId)
        {
            long total = 0;
            foreach (var pair in heroXpTotals) total += pair.Value;
            return total == 0 || !heroXpTotals.TryGetValue(heroId, out var xp) ? 0d : xp / (double)total;
        }
        private double DeathRate(int throughWave) => Rate(CountDeaths(throughWave), sampleCount);
        private int CountDeaths(int throughWave)
        {
            var count = 0;
            foreach (var wave in deathWaves)
            {
                if (wave <= throughWave) count++;
            }
            return count;
        }
        private int CountDeathsInRange(int firstWave, int lastWave)
        {
            var count = 0;
            foreach (var wave in deathWaves)
            {
                if (wave >= firstWave && wave <= lastWave) count++;
            }
            return count;
        }
        private double AverageDeathWave()
        {
            if (deathWaves.Count == 0) return 0d;
            var total = 0;
            foreach (var wave in deathWaves) total += wave;
            return (double)total / deathWaves.Count;
        }
        private double MedianDeathWave()
        {
            return Percentile(deathWaves, 0.50);
        }
        private static double Average(double total, int count) => count == 0 ? 0d : total / count;
        private static Dictionary<string, double>[] CreateHeroWaveMaps()
        {
            var maps = new Dictionary<string, double>[TwentyWavePressureConfiguration.WaveCount + 1];
            for (var index = 0; index < maps.Length; index++)
            {
                maps[index] = new Dictionary<string, double>(StringComparer.Ordinal);
            }
            return maps;
        }

        private static double CalculateGini(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0d;
            }

            var ordered = new List<int>(values);
            ordered.Sort();
            long weighted = 0;
            long total = 0;
            for (var index = 0; index < ordered.Count; index++)
            {
                total += ordered[index];
                weighted += (long)(index + 1) * ordered[index];
            }

            if (total == 0)
            {
                return 0d;
            }

            return ((2d * weighted) / (ordered.Count * (double)total)) -
                   ((ordered.Count + 1d) / ordered.Count);
        }
        private static double Average(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0) return 0d;
            long total = 0;
            foreach (var value in values) total += value;
            return total / (double)values.Count;
        }
        private static double Percentile(IReadOnlyList<int> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0d;
            var ordered = new List<int>(values);
            ordered.Sort();
            var position = (ordered.Count - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            if (lower == upper) return ordered[lower];
            return ordered[lower] + ((ordered[upper] - ordered[lower]) * (position - lower));
        }
        private static int CountAtMost(IReadOnlyList<int> values, int maximum)
        {
            if (values == null) return 0;
            var count = 0;
            foreach (var value in values)
            {
                if (value <= maximum) count++;
            }
            return count;
        }
        private string FormatDeathWaveDistribution()
        {
            var builder = new StringBuilder();
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                if (wave > 1) builder.Append(' ');
                builder.Append('W').Append(wave).Append('=').Append(Rate(deathWaveCounts[wave], sampleCount).ToString("P2"));
            }
            return builder.ToString();
        }
        private static int GetDeathStructureBucket(int deathWave)
        {
            if (deathWave <= 6) return 0;
            if (deathWave <= 12) return 1;
            return deathWave <= 16 ? 2 : 3;
        }
        private static int GetHeroDepthBucket(int heroCount)
        {
            if (heroCount <= 1) return 0;
            if (heroCount == 2) return 1;
            return heroCount == 3 ? 2 : 3;
        }
        private static double Rate(int numerator, int denominator) => denominator == 0 ? 0d : numerator / (double)denominator;
        private static double Rate(double numerator, double denominator) => denominator <= 0d ? 0d : numerator / denominator;
        private static string FormatMap<TKey>(IDictionary<TKey, int> map)
        {
            var builder = new StringBuilder("{");
            var first = true;
            foreach (var pair in map)
            {
                if (!first) builder.Append(", ");
                first = false;
                builder.Append(pair.Key).Append(":").Append(pair.Value);
            }
            return builder.Append("}").ToString();
        }

        private sealed class StructuralBucketAggregate
        {
            private readonly string label;
            private int samples;
            private double heroCount;
            private double distinctHeroIds;
            private double openCells;
            private double delivered;
            private double discarded;
            private double recruitCount;
            private double basicUnitCount;
            private double highestBasicLevel;

            public StructuralBucketAggregate(string label)
            {
                this.label = label;
            }

            public void Add(CoreLoopStructuralSnapshot snapshot)
            {
                samples++;
                heroCount += snapshot.HeroCount;
                distinctHeroIds += snapshot.DistinctHeroIds;
                openCells += snapshot.OpenCells;
                delivered += snapshot.ComponentsDelivered;
                discarded += snapshot.ComponentsDiscarded;
                recruitCount += snapshot.RecruitCount;
                basicUnitCount += snapshot.BasicUnitCount;
                highestBasicLevel += snapshot.HighestBasicLevel;
            }

            public string Format()
            {
                return $"Window={label} Deaths={samples} HeroCount={Average(heroCount, samples):0.00} DistinctHeroIds={Average(distinctHeroIds, samples):0.00} " +
                       $"OpenCells={Average(openCells, samples):0.00} Delivered={Average(delivered, samples):0.00} " +
                       $"Discarded={Average(discarded, samples):0.00} RecruitCount={Average(recruitCount, samples):0.00} " +
                       $"BasicUnitCount={Average(basicUnitCount, samples):0.00} HighestBasicLevel={Average(highestBasicLevel, samples):0.00}";
            }
        }

        private sealed class HeroDepthBucketAggregate
        {
            private readonly string label;
            private int samples;
            private int reachedW7;
            private int reachedW10;
            private int reachedW12;
            private int reachedW13;
            private int reachedW16;
            private int reachedW20;
            private readonly List<int> deathWaves = new List<int>();

            public HeroDepthBucketAggregate(string label)
            {
                this.label = label;
            }

            public void Add(CoreLoopSideRun run)
            {
                samples++;
                if (run.EndRecorded[7] || run.RunEndWave >= 7) reachedW7++;
                if (run.EndRecorded[10] || run.RunEndWave >= 10) reachedW10++;
                if (run.EndRecorded[12] || run.RunEndWave >= 12) reachedW12++;
                if (run.EndRecorded[13] || run.RunEndWave >= 13) reachedW13++;
                if (run.EndRecorded[16] || run.RunEndWave >= 16) reachedW16++;
                if (run.ReachedWaveTwenty) reachedW20++;
                if (run.DeathWave > 0) deathWaves.Add(run.DeathWave);
            }

            public string Format()
            {
                return $"HeroCount={label} Samples={samples} AvgDeathWave={Average(deathWaves):0.00} " +
                       $"ReachedW7={Rate(reachedW7, samples):P2} ReachedW10={Rate(reachedW10, samples):P2} " +
                       $"ReachedW12={Rate(reachedW12, samples):P2} ReachedW13={Rate(reachedW13, samples):P2} " +
                       $"ReachedW16={Rate(reachedW16, samples):P2} " +
                       $"ReachedW20={Rate(reachedW20, samples):P2}";
            }
        }
    }

    public sealed class CoreLoopTimingVerification
    {
        private readonly CoreLoopTimingWave[] waves;

        private CoreLoopTimingVerification(CoreLoopTimingWave[] waves, float normalSpeed, float fastSpeed, float eliteSpeed)
        {
            this.waves = waves;
            NormalMoveSpeedCellsPerSecond = normalSpeed;
            FastMoveSpeedCellsPerSecond = fastSpeed;
            EliteMoveSpeedCellsPerSecond = eliteSpeed;
        }

        public float NormalMoveSpeedCellsPerSecond { get; }
        public float FastMoveSpeedCellsPerSecond { get; }
        public float EliteMoveSpeedCellsPerSecond { get; }
        public CoreLoopTimingWave GetWave(int wave) => waves[wave - 1];

        internal static CoreLoopTimingVerification RunFormalScheduleProof()
        {
            var match = new MatchController(7301, hatchlingMaxHealth: 100000);
            var runtime = new TwentyWavePressureRuntime(match, null, null, 7301)
            {
                EmitLogs = false
            };
            runtime.StartRun();
            var first = new float[TwentyWavePressureConfiguration.WaveCount + 1];
            var last = new float[TwentyWavePressureConfiguration.WaveCount + 1];
            var spawned = new int[TwentyWavePressureConfiguration.WaveCount + 1];
            for (var index = 0; index < first.Length; index++)
            {
                first[index] = -1f;
                last[index] = -1f;
            }

            var previousSpawned = 0;
            while (!runtime.WavesExhausted && !runtime.IsComplete)
            {
                runtime.Tick(0.05f);
                var wave = runtime.CurrentWaveIndex;
                var delta = runtime.PlayerTotalSpawned - previousSpawned;
                if (delta > 0)
                {
                    spawned[wave] += delta;
                    if (first[wave] < 0f) first[wave] = runtime.ElapsedRunTime;
                    last[wave] = runtime.ElapsedRunTime;
                }
                previousSpawned = runtime.PlayerTotalSpawned;
            }

            var configuration = runtime.Configuration;
            var rows = new CoreLoopTimingWave[TwentyWavePressureConfiguration.WaveCount];
            for (var wave = 1; wave <= rows.Length; wave++)
            {
                var definition = configuration.GetWave(wave);
                var nextFirst = wave == rows.Length ? -1f : first[wave + 1];
                rows[wave - 1] = new CoreLoopTimingWave(
                    wave,
                    spawned[wave],
                    definition.SpawnIntervalSeconds,
                    first[wave],
                    last[wave],
                    nextFirst,
                    wave == rows.Length ? -1f : nextFirst - last[wave]);
            }

            var pathDistance = runtime.PlayerPath.TotalDistance;
            var samplePlan = runtime.GetWaveSpawnPlan(1, TeamSide.Player);
            return new CoreLoopTimingVerification(
                rows,
                ResolveCellSpeed(samplePlan, EnemyArchetype.Normal, pathDistance),
                ResolveCellSpeed(samplePlan, EnemyArchetype.Fast, pathDistance),
                ResolveCellSpeed(samplePlan, EnemyArchetype.Elite, pathDistance));
        }

        public string FormatReport()
        {
            return $"TimingProof NormalMoveSpeed={NormalMoveSpeedCellsPerSecond:0.00} " +
                   $"FastMoveSpeed={FastMoveSpeedCellsPerSecond:0.00} EliteMoveSpeed={EliteMoveSpeedCellsPerSecond:0.00}";
        }

        private static float ResolveCellSpeed(
            IReadOnlyList<PressureRaceEnemySpawn> plan,
            EnemyArchetype archetype,
            float pathDistance)
        {
            foreach (var spawn in plan)
            {
                if (spawn.Archetype == archetype)
                {
                    return spawn.MoveSpeedCellsPerSecond;
                }
            }

            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            return configuration.GetMoveSpeedCellsPerSecond(archetype);
        }
    }

    public readonly struct CoreLoopTimingWave
    {
        public CoreLoopTimingWave(
            int wave,
            int spawnCount,
            float spawnIntervalSeconds,
            float firstSpawnTimeSeconds,
            float lastSpawnTimeSeconds,
            float nextWaveFirstSpawnTimeSeconds,
            float actualInterWaveGapSeconds)
        {
            Wave = wave;
            SpawnCount = spawnCount;
            SpawnIntervalSeconds = spawnIntervalSeconds;
            FirstSpawnTimeSeconds = firstSpawnTimeSeconds;
            LastSpawnTimeSeconds = lastSpawnTimeSeconds;
            NextWaveFirstSpawnTimeSeconds = nextWaveFirstSpawnTimeSeconds;
            ActualInterWaveGapSeconds = actualInterWaveGapSeconds;
        }

        public int Wave { get; }
        public int SpawnCount { get; }
        public float SpawnIntervalSeconds { get; }
        public float FirstSpawnTimeSeconds { get; }
        public float LastSpawnTimeSeconds { get; }
        public float NextWaveFirstSpawnTimeSeconds { get; }
        public float ActualInterWaveGapSeconds { get; }
    }

    internal sealed class CoreLoopRunResult
    {
        public readonly CoreLoopSideRun Player = new CoreLoopSideRun();
        public readonly CoreLoopSideRun AI = new CoreLoopSideRun();
        public JointBalanceCalibrationSideRun PlayerJointCalibration;
        public JointBalanceCalibrationSideRun AiJointCalibration;
        public readonly CoreLoopMatchEndRun MatchEnd = new CoreLoopMatchEndRun();
        public EnemyPressureSideRun PlayerEnemyPressure;
        public EnemyPressureSideRun AiEnemyPressure;
        public W6BareCalibrationRun W6Calibration;
        public W12BuildEnvelopeCalibrationRun W12Calibration;
        public BoardBenchCapacitySideRun PlayerCapacityAudit;
        public BoardBenchCapacitySideRun AiCapacityAudit;

        public JointBalanceCalibrationRunSample CreateJointCalibrationSample(int runSeed)
        {
            return new JointBalanceCalibrationRunSample(
                runSeed,
                PlayerJointCalibration.CreateSample("Player", Player),
                AiJointCalibration.CreateSample("AI", AI));
        }
    }

    internal sealed class CoreLoopMatchEndRun
    {
        public bool IsRecorded { get; private set; }
        public bool IsGameplaySettlement { get; private set; }
        public int Wave { get; private set; }
        public float DurationSeconds { get; private set; }
        public int PlayerHealth { get; private set; }
        public int AiHealth { get; private set; }
        public string Winner { get; private set; } = "Invalid";
        public string EndReason { get; private set; } = "Invalid/System Destroy";
        public bool ScheduleCompletedAtEnd { get; private set; }
        public bool PlayerDefeated => PlayerHealth <= 0;
        public bool AiDefeated => AiHealth <= 0;

        public void RecordGameplaySettlement(
            int wave,
            float durationSeconds,
            int playerHealth,
            int aiHealth,
            bool scheduleCompleted,
            EnemyArchetype? playerLeak,
            EnemyArchetype? aiLeak)
        {
            if (IsRecorded) return;
            IsRecorded = true;
            IsGameplaySettlement = true;
            Wave = Math.Max(1, wave);
            DurationSeconds = Math.Max(0f, durationSeconds);
            PlayerHealth = playerHealth;
            AiHealth = aiHealth;
            ScheduleCompletedAtEnd = scheduleCompleted;
            Winner = PlayerDefeated && AiDefeated ? "Draw" : PlayerDefeated ? "AI" : "Player";
            EndReason = ResolveReason(PlayerDefeated ? playerLeak : aiLeak);
        }

        public void RecordDeveloperStop(int wave, float durationSeconds, int playerHealth, int aiHealth, bool scheduleCompleted)
        {
            if (IsRecorded) return;
            IsRecorded = true;
            Wave = Math.Max(1, wave);
            DurationSeconds = Math.Max(0f, durationSeconds);
            PlayerHealth = playerHealth;
            AiHealth = aiHealth;
            ScheduleCompletedAtEnd = scheduleCompleted;
            Winner = "Invalid";
            EndReason = "DeveloperStop";
        }

        public void RecordTimeout(int wave, float durationSeconds, int playerHealth, int aiHealth, bool scheduleCompleted)
        {
            if (IsRecorded) return;
            IsRecorded = true;
            Wave = Math.Max(1, wave);
            DurationSeconds = Math.Max(0f, durationSeconds);
            PlayerHealth = playerHealth;
            AiHealth = aiHealth;
            ScheduleCompletedAtEnd = scheduleCompleted;
            Winner = "Invalid";
            EndReason = "Timeout";
        }

        private static string ResolveReason(EnemyArchetype? archetype)
        {
            if (!archetype.HasValue) return "OtherLegalHeart";
            return "Heart depleted by leaked " + archetype.Value;
        }
    }

    public sealed class CoreLoopMatchEndAggregate
    {
        private readonly int sampleCount;
        private readonly int[] endWaveCounts = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly Dictionary<string, int> causes = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<float> durations = new List<float>();
        private readonly List<float> earlyDurations = new List<float>();
        private readonly List<float> targetDurations = new List<float>();
        private readonly List<float> strongTailDurations = new List<float>();
        private readonly List<float> deepTailDurations = new List<float>();
        private int gameplayEnds;
        private int invalidEnds;
        private int playerWins;
        private int aiWins;
        private int draws;
        private int playerFirstDeaths;
        private int aiFirstDeaths;
        private int gameplayEndsAfterSchedule;
        private int developerStops;
        private int timeouts;

        internal CoreLoopMatchEndAggregate(int sampleCount)
        {
            this.sampleCount = sampleCount;
        }

        public int GameplayEndCount => gameplayEnds;
        public int InvalidOrDeveloperEndCount => invalidEnds;
        public int CompletedDistributionCount => CountRange(1, TwentyWavePressureConfiguration.WaveCount);
        public int GameplayEndsAfterScheduleCount => gameplayEndsAfterSchedule;
        public int GetEndWaveCount(int wave)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(wave));
            }

            return endWaveCounts[wave];
        }

        internal void Add(CoreLoopMatchEndRun run)
        {
            if (run == null || !run.IsRecorded)
            {
                invalidEnds++;
                return;
            }

            if (!run.IsGameplaySettlement)
            {
                invalidEnds++;
                if (run.EndReason == "DeveloperStop") developerStops++;
                if (run.EndReason == "Timeout") timeouts++;
            }
            else
            {
                gameplayEnds++;
                if (run.ScheduleCompletedAtEnd) gameplayEndsAfterSchedule++;
                if (run.Winner == "Player") playerWins++;
                else if (run.Winner == "AI") aiWins++;
                else draws++;
                if (run.PlayerDefeated && !run.AiDefeated) playerFirstDeaths++;
                else if (run.AiDefeated && !run.PlayerDefeated) aiFirstDeaths++;
                if (run.Wave >= 1 && run.Wave <= TwentyWavePressureConfiguration.WaveCount)
                {
                    endWaveCounts[run.Wave]++;
                }
                durations.Add(run.DurationSeconds);
                var bucket = run.Wave <= 6 ? earlyDurations :
                    run.Wave <= 12 ? targetDurations :
                    run.Wave <= 16 ? strongTailDurations : deepTailDurations;
                bucket.Add(run.DurationSeconds);
            }

            causes.TryGetValue(run.EndReason, out var count);
            causes[run.EndReason] = count + 1;
        }

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[MatchEnd] GameplayEnds={gameplayEnds} InvalidOrDeveloperEnds={invalidEnds} " +
                               $"PlayerWin={Rate(playerWins, sampleCount):P2} AIWin={Rate(aiWins, sampleCount):P2} " +
                               $"Draw={Rate(draws, sampleCount):P2} PlayerFirstDeath={Rate(playerFirstDeaths, sampleCount):P2} " +
                               $"AIFirstDeath={Rate(aiFirstDeaths, sampleCount):P2}");
            builder.AppendLine($"[MatchEnd] EndWaveDistribution {FormatWaveCounts()} SurviveBeyondW20={Rate(CountBeyondW20(), sampleCount):P2} " +
                               $"GameplayEndsAfterSchedule={gameplayEndsAfterSchedule} DeveloperStops={developerStops} Timeouts={timeouts}");
            builder.AppendLine($"[MatchEnd] Windows W1-W6={Rate(CountRange(1, 6), sampleCount):P2} " +
                               $"W7-W12={Rate(CountRange(7, 12), sampleCount):P2} " +
                               $"W13-W16={Rate(CountRange(13, 16), sampleCount):P2} " +
                               $"W17-W20={Rate(CountRange(17, 20), sampleCount):P2}");
            builder.AppendLine($"[MatchEnd] Duration Mean={Average(durations):0.00}s P25={Percentile(durations, .25):0.00}s " +
                               $"P50={Percentile(durations, .50):0.00}s P75={Percentile(durations, .75):0.00}s P90={Percentile(durations, .90):0.00}s " +
                               $"W1-W6={Average(earlyDurations):0.00}s W7-W12={Average(targetDurations):0.00}s " +
                               $"W13-W16={Average(strongTailDurations):0.00}s W17-W20={Average(deepTailDurations):0.00}s");
            builder.AppendLine($"[MatchEnd] EndCauses={FormatMap(causes)}");
            return builder.ToString();
        }

        private int CountBeyondW20() => gameplayEndsAfterSchedule;
        private int CountRange(int first, int last)
        {
            var count = 0;
            for (var wave = first; wave <= last; wave++) count += endWaveCounts[wave];
            return count;
        }
        private string FormatWaveCounts()
        {
            var builder = new StringBuilder();
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                if (wave > 1) builder.Append(' ');
                builder.Append('W').Append(wave).Append('=').Append(endWaveCounts[wave]).Append('/').Append(Rate(endWaveCounts[wave], sampleCount).ToString("P2"));
            }
            return builder.ToString();
        }
        private static double Average(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0) return 0d;
            var total = 0d;
            foreach (var value in values) total += value;
            return total / values.Count;
        }
        private static double Percentile(IReadOnlyList<float> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0d;
            var ordered = new List<float>(values);
            ordered.Sort();
            var position = (ordered.Count - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            return lower == upper ? ordered[lower] : ordered[lower] + (ordered[upper] - ordered[lower]) * (float)(position - lower);
        }
        private static double Rate(int numerator, int denominator) => denominator == 0 ? 0d : numerator / (double)denominator;
        private static string FormatMap<TKey>(IDictionary<TKey, int> map)
        {
            var builder = new StringBuilder("{");
            var first = true;
            foreach (var pair in map)
            {
                if (!first) builder.Append(", ");
                first = false;
                builder.Append(pair.Key).Append(':').Append(pair.Value);
            }
            return builder.Append('}').ToString();
        }
    }

    internal sealed class CoreLoopSideRun
    {
        public readonly bool[] StartRecorded = new bool[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly bool[] EndRecorded = new bool[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly bool[] FunnelEndRecorded = new bool[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] RecruitAtStart = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] RecruitAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] HeroAtStart = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] HeroAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] HeartAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] ResourcesAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] BoardOccupiedAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] BenchOccupiedAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] BasicUnitCountAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly ComponentLifecycleSnapshot[] LifecycleAtEnd = new ComponentLifecycleSnapshot[TwentyWavePressureConfiguration.WaveCount + 1];
        public ComponentLifecycleSnapshot RunEndLifecycle;
        public int RunEndWave;
        public int RunEndHeroCount;
        public int RunEndOpenCellCount;
        public readonly int[] AvailableRecipePairsAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] BlockedRecipePairsAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] SpawnCount = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] OpenCellsAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] ShovelsGeneratedAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] ShovelsUsedAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] ShovelsDiscardedAtEnd = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly bool[] BenchFullAtEnd = new bool[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly bool[] BoardPressureAtEnd = new bool[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly float[] FirstSpawnTime = CreateFloatArray();
        public readonly float[] LastSpawnTime = CreateFloatArray();
        public readonly double[] AliveTotal = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] AliveSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] PeakAlive = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly Dictionary<AiRecruitBlockedReason, int> RecruitStallsByReason =
            new Dictionary<AiRecruitBlockedReason, int>();
        public readonly Dictionary<string, int> HeroFormedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> HeroKillCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> HeroXpTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> HeroLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, HeroXpFormationRun> HeroFormationsByPairLink =
            new Dictionary<string, HeroXpFormationRun>(StringComparer.Ordinal);
        public readonly List<HeroXpFormationRun> HeroFormations = new List<HeroXpFormationRun>();
        public readonly Dictionary<string, int>[] HeroXpAtEndByWave =
            new Dictionary<string, int>[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly HashSet<string> DistinctHeroIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> PairBreakReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<AiRecipeBlockedReason, int> RecipeFailuresByReason =
            new Dictionary<AiRecipeBlockedReason, int>();
        private readonly Dictionary<string, float> pairLinkCreatedAt = new Dictionary<string, float>(StringComparer.Ordinal);
        public int PairLinksFormed;
        public int PairLinksBroken;
        public double PairLinkLifetimeTotal;
        public int RecipeOpportunityCreated;
        public int RecipeFormationAttempted;
        public int RecipeFormationSucceeded;
        public int RecipeFormationFailed;
        public int RecipeRetryCount;
        public int UnpairedComponentCount;
        public int RecruitStallCount;
        public int FirstRecruitStallWave = -1;
        public int LegacyCampPolicyBlockCount;
        public int FirstLeakWave = -1;
        public int DeathWave = -1;
        public bool ReachedWaveTwenty;
        public int ShovelsGenerated;
        public int ShovelsUsed;
        public int ShovelsDiscarded;
        public int BaseLeaks;
        public int MergesPerformed;
        public int BasicUnitKills;
        public int HeroKills;
        public int UnattributedKills;
        public int TotalHeroXpFromKills;
        public CoreLoopStructuralSnapshot DeathSnapshot;

        public void RecordRecruitment(RecruitmentAttempt attempt)
        {
            if (attempt.Status != RecruitmentStatus.Success || attempt.Batch == null)
            {
                return;
            }

            foreach (var card in attempt.Batch.Cards)
            {
                if (card.Kind == RecruitItemKind.Shovel)
                {
                    ShovelsGenerated++;
                }
            }

            if (!attempt.RefreshedBench)
            {
                return;
            }

            foreach (var card in attempt.RefreshedCards)
            {
                if (card.Kind == RecruitItemKind.Shovel)
                {
                    ShovelsDiscarded++;
                }
            }
        }

        public void RecordShovelUsed(GridPosition position)
        {
            ShovelsUsed++;
        }

        public void RecordCombat(CombatEvent value)
        {
            if (!value.Killed)
            {
                return;
            }

            switch (value.DamageOwnerKind)
            {
                case CombatDamageOwnerKind.BasicUnit:
                    BasicUnitKills++;
                    return;
                case CombatDamageOwnerKind.Hero:
                    HeroKills++;
                    if (string.IsNullOrEmpty(value.DamageOwnerHeroId))
                    {
                        UnattributedKills++;
                        return;
                    }

                    HeroKillCounts.TryGetValue(value.DamageOwnerHeroId, out var kills);
                    HeroKillCounts[value.DamageOwnerHeroId] = kills + 1;
                    HeroXpTotals.TryGetValue(value.DamageOwnerHeroId, out var xp);
                    HeroXpTotals[value.DamageOwnerHeroId] = xp + value.HeroXpAwarded;
                    HeroLevels[value.DamageOwnerHeroId] = Math.Max(
                        HeroLevels.TryGetValue(value.DamageOwnerHeroId, out var currentLevel) ? currentLevel : 1,
                        value.DamageOwnerHeroLevel);
                    TotalHeroXpFromKills += value.HeroXpAwarded;
                    if (HeroFormationsByPairLink.TryGetValue(value.DamageOwnerRuntimeId, out var formation))
                    {
                        formation.Kills++;
                        formation.XP += value.HeroXpAwarded;
                        formation.Level = Math.Max(formation.Level, value.DamageOwnerHeroLevel);
                    }
                    return;
                default:
                    UnattributedKills++;
                    return;
            }
        }

        public void CaptureHeroXpSnapshot(int wave, BoardRecruitDestination destination)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount || destination == null)
            {
                return;
            }

            var snapshot = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var activePair in destination.GetActiveHeroPairs())
            {
                var heroId = activePair.PairLink.HeroId;
                snapshot.TryGetValue(heroId, out var xp);
                snapshot[heroId] = xp + activePair.PairLink.CombatProxy.Experience;
            }

            HeroXpAtEndByWave[wave] = snapshot;
        }

        public void RecordPairLinkCreated(HeroPairLink pairLink, float time)
        {
            PairLinksFormed++;
            DistinctHeroIds.Add(pairLink.HeroId);
            HeroFormedCounts.TryGetValue(pairLink.HeroId, out var formed);
            HeroFormedCounts[pairLink.HeroId] = formed + 1;
            if (!HeroKillCounts.ContainsKey(pairLink.HeroId)) HeroKillCounts.Add(pairLink.HeroId, 0);
            if (!HeroXpTotals.ContainsKey(pairLink.HeroId)) HeroXpTotals.Add(pairLink.HeroId, 0);
            if (!HeroLevels.ContainsKey(pairLink.HeroId)) HeroLevels.Add(pairLink.HeroId, pairLink.CombatProxy.Level);
            pairLinkCreatedAt[pairLink.PairLinkId] = time;
            var formation = new HeroXpFormationRun(pairLink.PairLinkId, pairLink.HeroId);
            HeroFormations.Add(formation);
            HeroFormationsByPairLink[pairLink.PairLinkId] = formation;
        }

        public void RecordPairLinkBroken(HeroPairLink pairLink, string reason, float time)
        {
            PairLinksBroken++;
            PairBreakReasons.TryGetValue(reason ?? "Other", out var broken);
            PairBreakReasons[reason ?? "Other"] = broken + 1;
            if (pairLinkCreatedAt.TryGetValue(pairLink.PairLinkId, out var createdAt))
            {
                PairLinkLifetimeTotal += Math.Max(0f, time - createdAt);
                pairLinkCreatedAt.Remove(pairLink.PairLinkId);
            }
        }

        private static float[] CreateFloatArray()
        {
            var values = new float[TwentyWavePressureConfiguration.WaveCount + 1];
            for (var index = 0; index < values.Length; index++) values[index] = -1f;
            return values;
        }
    }

    internal sealed class HeroXpFormationRun
    {
        public HeroXpFormationRun(string pairLinkId, string heroId)
        {
            PairLinkId = pairLinkId ?? string.Empty;
            HeroId = heroId ?? string.Empty;
            Level = 1;
        }

        public string PairLinkId { get; }
        public string HeroId { get; }
        public int Kills { get; set; }
        public int XP { get; set; }
        public int Level { get; set; }
    }

    internal sealed class CoreLoopStructuralSnapshot
    {
        public CoreLoopStructuralSnapshot(
            int heroCount,
            int distinctHeroIds,
            int openCells,
            int componentsDelivered,
            int componentsDiscarded,
            int recruitCount,
            int basicUnitCount,
            int highestBasicLevel)
        {
            HeroCount = heroCount;
            DistinctHeroIds = distinctHeroIds;
            OpenCells = openCells;
            ComponentsDelivered = componentsDelivered;
            ComponentsDiscarded = componentsDiscarded;
            RecruitCount = recruitCount;
            BasicUnitCount = basicUnitCount;
            HighestBasicLevel = highestBasicLevel;
        }

        public int HeroCount { get; }
        public int DistinctHeroIds { get; }
        public int OpenCells { get; }
        public int ComponentsDelivered { get; }
        public int ComponentsDiscarded { get; }
        public int RecruitCount { get; }
        public int BasicUnitCount { get; }
        public int HighestBasicLevel { get; }
    }
}
