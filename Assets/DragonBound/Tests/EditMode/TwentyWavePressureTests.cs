using System.Collections.Generic;
using DragonBound.Core;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class TwentyWavePressureTests
    {
        [Test]
        public void GreyboxConfigurationContainsExactlyTwentyConfiguredWaves()
        {
            var configuration = TwentyWavePressureConfiguration.CreateGreyboxV1();

            Assert.AreEqual(20, configuration.Waves.Count);
            Assert.AreEqual(10, configuration.GetWave(1).EnemyCountPerSide);
            Assert.AreEqual(43, configuration.GetWave(20).EnemyCountPerSide);
            Assert.AreEqual(303, configuration.GetCumulativeEnemyCountPerSide(15));
            Assert.AreEqual(498, configuration.GetCumulativeEnemyCountPerSide(20));
            Assert.IsTrue(configuration.GetWave(6).HasBossSlot);
            Assert.IsTrue(configuration.GetWave(12).HasBossSlot);
            Assert.IsTrue(configuration.GetWave(16).HasBossSlot);
            Assert.IsTrue(configuration.GetWave(20).HasBossSlot);
            Assert.IsFalse(configuration.GetWave(5).HasBossSlot);
        }

        [Test]
        public void CompositionIsSymmetricAndIndependentFromOtherRandomConsumers()
        {
            var first = CreateRuntime(443);
            var unrelated = new RunRandom(9981);
            for (var index = 0; index < 100; index++)
            {
                unrelated.NextUnit("Unrelated.System." + index);
            }

            var second = CreateRuntime(443);
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                var playerComposition = first.GetWaveComposition(wave, TeamSide.Player);
                var aiComposition = first.GetWaveComposition(wave, TeamSide.AI);
                var repeatedComposition = second.GetWaveComposition(wave, TeamSide.Player);
                CollectionAssert.AreEqual(playerComposition, aiComposition);
                CollectionAssert.AreEqual(playerComposition, repeatedComposition);
                Assert.AreEqual(first.Configuration.GetWave(wave).EnemyCountPerSide, playerComposition.Count);
            }

            Assert.AreEqual("PressureComposition.v1", first.RngVersion);
        }

        [Test]
        public void WaveSpawnPlansUseConfiguredHealthAndSpeedMultipliers()
        {
            var runtime = CreateRuntime(19);
            var wave = runtime.Configuration.GetWave(10);
            var spawns = runtime.GetWaveSpawnPlan(10, TeamSide.Player);

            Assert.AreEqual(wave.EnemyCountPerSide, spawns.Count);
            Assert.AreEqual(
                EnemyRuntime.DefaultMaxHitPoints * wave.HealthMultiplier,
                spawns[0].MaxHitPoints,
                0.0001f);
            Assert.AreEqual(wave.MoveSpeedMultiplier, spawns[0].MoveSpeedMultiplier, 0.0001f);
        }

        [Test]
        public void CoreLoopV2UsesTheConfiguredPreparationAndConstantTempo()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            var first = configuration.GetWave(1);
            var last = configuration.GetWave(20);

            Assert.AreEqual(4f, first.FirstSpawnDelaySeconds, 0.0001f);
            Assert.AreEqual(1.5f, first.SpawnIntervalSeconds, 0.0001f);
            Assert.AreEqual(6.5f, first.InterWaveSpawnGapSeconds, 0.0001f);
            Assert.AreEqual(25.5f, EnemyRuntime.DefaultMaxHitPoints * first.HealthMultiplier, 0.0001f);
            Assert.AreEqual(45f, EnemyRuntime.DefaultMaxHitPoints * configuration.GetWave(5).HealthMultiplier, 0.0001f);
            Assert.AreEqual(63f, EnemyRuntime.DefaultMaxHitPoints * configuration.GetWave(6).HealthMultiplier, 0.0001f);
            Assert.AreEqual(1f, first.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(660f, EnemyRuntime.DefaultMaxHitPoints * last.HealthMultiplier, 0.0001f);
            Assert.AreEqual(1f, last.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0.60f, configuration.GetMoveSpeedCellsPerSecond(EnemyArchetype.Normal), 0.0001f);
            Assert.AreEqual(0.80f, configuration.GetMoveSpeedCellsPerSecond(EnemyArchetype.Fast), 0.0001f);
            Assert.AreEqual(0.58f, configuration.GetMoveSpeedCellsPerSecond(EnemyArchetype.Elite), 0.0001f);
        }

        [Test]
        public void ProductionRegularWavesUseOnlyNormalCompositionWeights()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();

            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                var definition = configuration.GetWave(wave);
                Assert.AreEqual(1f, definition.NormalWeight, 0.0001f, "W" + wave);
                Assert.AreEqual(0f, definition.FastWeight, 0.0001f, "W" + wave);
                Assert.AreEqual(0f, definition.EliteWeight, 0.0001f, "W" + wave);
                Assert.AreEqual(1f, definition.TotalWeight, 0.0001f, "W" + wave);
            }
        }

        [Test]
        public void ProductionSpawnPlansContainOnlyNormalRegularEnemiesAtConfiguredSpeed()
        {
            var runtime = CreateRuntime(9017);

            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                foreach (var side in new[] { TeamSide.Player, TeamSide.AI })
                {
                    var definition = runtime.Configuration.GetWave(wave);
                    var spawns = runtime.GetWaveSpawnPlan(wave, side);
                    Assert.AreEqual(definition.EnemyCountPerSide, spawns.Count, "W" + wave + " " + side);
                    foreach (var spawn in spawns)
                    {
                        Assert.AreEqual(EnemyArchetype.Normal, spawn.Archetype, "W" + wave + " " + side);
                        Assert.AreEqual(0.60f, spawn.MoveSpeedCellsPerSecond, 0.0001f,
                            "W" + wave + " " + side);
                    }
                }
            }
        }

        [Test]
        public void ProductionRuntimeSpawnsNormalForEveryRegularWave()
        {
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                var runtime = CreateRuntime(11000 + wave);
                Assert.IsTrue(runtime.StartRun());
                if (wave > 1)
                {
                    Assert.IsTrue(runtime.JumpToWave(wave));
                }

                runtime.Tick(runtime.Configuration.GetWave(wave).FirstSpawnDelaySeconds + 0.01f);
                foreach (var enemy in runtime.PlayerEnemyRegistry.Snapshot())
                {
                    if (enemy.Archetype != EnemyArchetype.Boss)
                    {
                        Assert.AreEqual(EnemyArchetype.Normal, enemy.Archetype, "W" + wave);
                    }
                }
            }
        }

        [Test]
        public void ProductionBossSlotsAndBossSpeedRemainIndependent()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                var expectedBossSlot = TwentyWavePressureConfiguration.IsBossWave(wave);
                Assert.AreEqual(expectedBossSlot, configuration.GetWave(wave).HasBossSlot, "W" + wave);
            }

            Assert.AreEqual(0.20f, SoulchainBinderConfiguration.BossMoveSpeedCellsPerSecond, 0.0001f);
        }

        [Test]
        public void DefaultRuntimeLoadsThePromotedR1ProductionCurve()
        {
            var runtime = CreateRuntime(7401);
            var expectedMaxHitPoints = new[]
            {
                25.5f, 26.1f, 26.7f, 35f, 45f, 63f, 95f, 120f, 145f, 175f,
                205f, 240f, 275f, 315f, 360f, 410f, 465f, 525f, 590f, 660f
            };

            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                Assert.AreEqual(expectedMaxHitPoints[wave - 1],
                    TwentyWavePressureConfiguration.GetProductionMaxHitPoints(wave), 0.0001f);
                Assert.AreEqual(expectedMaxHitPoints[wave - 1],
                    EnemyRuntime.DefaultMaxHitPoints * runtime.Configuration.GetWave(wave).HealthMultiplier, 0.0001f);
            }

            Assert.AreEqual(10, runtime.Configuration.GetWave(1).EnemyCountPerSide);
            Assert.AreEqual(43, runtime.Configuration.GetWave(20).EnemyCountPerSide);
            Assert.AreEqual(1.5f, runtime.Configuration.GetWave(8).SpawnIntervalSeconds, 0.0001f);
            Assert.AreEqual(6.5f, runtime.Configuration.GetWave(8).InterWaveSpawnGapSeconds, 0.0001f);
        }

        [Test]
        public void PlayerAndAiCreateIndependentEnemyInstances()
        {
            var runtime = CreateRuntime(55);

            Assert.IsTrue(runtime.StartRun());
            runtime.Tick(3.99f);
            Assert.AreEqual(0, runtime.PlayerEnemyRegistry.Count);
            runtime.Tick(0.01f);

            Assert.AreEqual(1, runtime.PlayerEnemyRegistry.Count);
            Assert.AreEqual(1, runtime.AiEnemyRegistry.Count);
            var playerEnemy = runtime.PlayerEnemyRegistry.Snapshot()[0];
            var aiEnemy = runtime.AiEnemyRegistry.Snapshot()[0];
            Assert.AreNotSame(playerEnemy, aiEnemy);
            Assert.AreNotEqual(playerEnemy.RuntimeId, aiEnemy.RuntimeId);
            Assert.AreEqual(TeamSide.Player, playerEnemy.Team);
            Assert.AreEqual(TeamSide.AI, aiEnemy.Team);
        }

        [Test]
        public void EndingWaveRetainsResidualEnemiesAndStartsNextWave()
        {
            var runtime = CreateRuntime(91, CreateShortDurationConfiguration());
            Assert.IsTrue(runtime.StartRun());

            runtime.Tick(0.11f);
            Assert.AreEqual(2, runtime.CurrentWaveIndex);
            Assert.AreEqual(10, runtime.PlayerEnemyRegistry.Count);
            Assert.AreEqual(10, runtime.AiEnemyRegistry.Count);

            runtime.Tick(0.01f);
            Assert.AreEqual(11, runtime.PlayerEnemyRegistry.Count);
            Assert.AreEqual(11, runtime.AiEnemyRegistry.Count);
            Assert.AreEqual(11, runtime.PlayerTotalSpawned);
            Assert.AreEqual(11, runtime.AiTotalSpawned);
        }

        [Test]
        public void PauseResumeAndJumpAreDeveloperControlsOnlyOnTheScheduler()
        {
            var runtime = CreateRuntime(121);
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.PauseWave());
            runtime.Tick(10f);
            Assert.AreEqual(0, runtime.PlayerTotalSpawned);
            Assert.IsTrue(runtime.ResumeWave());
            Assert.IsTrue(runtime.JumpToWave(12));

            Assert.AreEqual(12, runtime.CurrentWaveIndex);
            Assert.AreEqual(12, runtime.Configuration.GetWave(12).WaveIndex);
        }

        [Test]
        public void SubscribedBossWarningDefersBossWaveUntilConfirmed()
        {
            var match = new MatchController(612);
            var runtime = new TwentyWavePressureRuntime(match, null, null, 612);
            var requestedWave = 0;
            runtime.BossWarningRequested += wave => requestedWave = wave;

            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(TwentyWavePressureConfiguration.SoulChainBossWave));

            Assert.AreEqual(TwentyWavePressureConfiguration.SoulChainBossWave, requestedWave);
            Assert.AreEqual(MatchState.BossPrompt, match.State);
            Assert.IsTrue(runtime.IsBossWarningPending);
            Assert.AreEqual(TwentyWavePressureConfiguration.SoulChainBossWave, runtime.PendingBossWave);
            Assert.AreEqual(1, runtime.CurrentWaveIndex);
            Assert.IsNull(runtime.PlayerW6Boss);
            Assert.IsNull(runtime.AiW6Boss);

            runtime.Tick(10f);
            Assert.AreEqual(0f, runtime.WaveElapsedTime, 0.0001f);
            Assert.IsTrue(runtime.ConfirmBossWarning());
            Assert.AreEqual(MatchState.Running, match.State);
            Assert.AreEqual(TwentyWavePressureConfiguration.SoulChainBossWave, runtime.CurrentWaveIndex);
            Assert.IsNotNull(runtime.PlayerW6Boss);
            Assert.IsNotNull(runtime.AiW6Boss);
            Assert.IsFalse(runtime.IsBossWarningPending);
            Assert.IsFalse(runtime.ConfirmBossWarning(), "Repeated confirmation must not duplicate the boss wave.");
        }

        [Test]
        public void RuntimeWithoutBossWarningSubscriberKeepsLegacyImmediateWaveStart()
        {
            var runtime = CreateRuntime(613);

            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(TwentyWavePressureConfiguration.SoulChainBossWave));

            Assert.AreEqual(TwentyWavePressureConfiguration.SoulChainBossWave, runtime.CurrentWaveIndex);
            Assert.IsNotNull(runtime.PlayerW6Boss);
            Assert.IsFalse(runtime.IsBossWarningPending);
        }

        [Test]
        public void DiagnosticsAreReadFromTheFormalConfiguration()
        {
            var configuration = TwentyWavePressureConfiguration.CreateGreyboxV1();
            var rows = TwentyWavePressureDiagnostics.Build(configuration);
            var report = TwentyWavePressureDiagnostics.CreateReport(configuration);

            Assert.AreEqual(20, rows.Count);
            Assert.AreEqual(303, rows[14].CumulativePerSide);
            Assert.AreEqual(498, rows[19].CumulativePerSide);
            StringAssert.Contains("W15 CumulativePerSide=303", report);
            StringAssert.Contains("TheoreticalW15KillResources=303", report);
        }

        [Test]
        public void CompletingWaveTwentyOnlyCompletesTheRegularSchedule()
        {
            var match = new MatchController(341);
            var runtime = new TwentyWavePressureRuntime(
                match,
                null,
                null,
                341,
                CreateShortDurationConfiguration());

            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(20));
            runtime.Tick(0.11f);

            Assert.IsTrue(runtime.RegularWaveScheduleCompleted);
            Assert.IsFalse(runtime.IsComplete);
            Assert.AreEqual(MatchState.Running, match.State);
        }

        [Test]
        public void BaseDeathRemainsTheOnlyAutomaticPressureRunSettlement()
        {
            var match = new MatchController(342);
            var runtime = new TwentyWavePressureRuntime(match, null, null, 342);

            Assert.IsTrue(runtime.StartRun());
            match.Player.ApplyHatchlingDamage(match.Player.HatchlingHealth);
            runtime.Tick(0.01f);

            Assert.IsTrue(runtime.IsComplete);
            Assert.AreEqual(MatchState.Defeat, match.State);
        }

        private static TwentyWavePressureRuntime CreateRuntime(
            int seed,
            TwentyWavePressureConfiguration configuration = null)
        {
            return new TwentyWavePressureRuntime(
                new MatchController(seed),
                null,
                null,
                seed,
                configuration);
        }

        private static TwentyWavePressureConfiguration CreateShortDurationConfiguration()
        {
            var source = TwentyWavePressureConfiguration.CreateGreyboxV1();
            var definitions = new List<PressureRaceWaveDefinition>();
            foreach (var wave in source.Waves)
            {
                definitions.Add(new PressureRaceWaveDefinition(
                    wave.WaveIndex,
                    wave.EnemyCountPerSide,
                    0.1f,
                    wave.NormalWeight,
                    wave.FastWeight,
                    wave.EliteWeight,
                    wave.HealthMultiplier,
                    wave.MoveSpeedMultiplier,
                    wave.HasBossSlot));
            }

            return new TwentyWavePressureConfiguration(definitions);
        }
    }
}
