using System;
using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class PressureRunDiagnosticsTests
    {
        [Test]
        public void DiagnosticsRecordRecruitmentsAndOverwriteBreakdownFromTheLiveServices()
        {
            using (var context = new DiagnosticsContext(601))
            {
                Assert.IsTrue(context.Runtime.StartRun());
                var first = context.PlayerRecruitment.TryRecruit();
                context.Diagnostics.Tick(0.01f);
                var second = context.PlayerRecruitment.TryRecruit();
                context.Diagnostics.Tick(0.01f);

                Assert.AreEqual(RecruitmentStatus.Success, first.Status);
                Assert.AreEqual(RecruitmentStatus.Success, second.Status);
                Assert.AreEqual(2, context.Diagnostics.Player.SuccessfulRecruitCount);
                Assert.AreEqual(2, context.Diagnostics.Player.RecruitmentRecords.Count);
                Assert.AreEqual(1, context.Diagnostics.Player.RecruitmentRecords[0].Wave);
                Assert.GreaterOrEqual(context.Diagnostics.Player.RecruitmentRecords[0].RunTime, 0f);
                Assert.AreEqual(first.Cost + second.Cost, context.Diagnostics.Player.SpentResources);
                Assert.AreEqual(1, context.Diagnostics.Player.RecruitOverwriteCount);
                Assert.AreEqual(
                    second.RefreshedCards.Count,
                    context.Diagnostics.Player.OverwrittenBasicUnitCount +
                    context.Diagnostics.Player.OverwrittenComponentCount +
                    context.Diagnostics.Player.OverwrittenShovelCount);
                Assert.AreEqual(
                    context.PlayerRecruitment.DrawnHeroComponents,
                    context.Diagnostics.Player.DeliveredComponentCount);
                Assert.AreEqual(
                    context.PlayerRecruitment.RemainingHeroComponents,
                    context.Diagnostics.Player.RemainingComponentCount);
                Assert.AreEqual(0, context.Diagnostics.AI.SuccessfulRecruitCount);
            }
        }

        [Test]
        public void DiagnosticsTrackComponentExhaustionAndShovelUnlockOnlyOnce()
        {
            using (var context = new DiagnosticsContext(602))
            {
                Assert.IsTrue(context.Runtime.StartRun());
                context.PlayerShovels.GrantShovel(1);
                var target = context.PlayerBoard.GetPositions(CellType.Locked)[0];
                Assert.IsTrue(context.PlayerShovels.BeginSelection());
                Assert.IsTrue(context.PlayerShovels.TryUnlockCell(target));
                context.Diagnostics.Tick(0.01f);

                Assert.AreEqual(1, context.Diagnostics.Player.ShovelsGrantedExternally);
                Assert.AreEqual(1, context.Diagnostics.Player.ShovelsUsed);
                Assert.AreEqual(7, context.Diagnostics.Player.OpenCellCount);
                Assert.AreEqual(17, context.Diagnostics.Player.LockedCellCount);

                for (var index = 0; index < 11; index++)
                {
                    Assert.AreEqual(RecruitmentStatus.Success, context.PlayerRecruitment.TryRecruit().Status);
                    context.Diagnostics.Tick(0.01f);
                }

                Assert.AreEqual(0, context.Diagnostics.Player.RemainingComponentCount);
                Assert.GreaterOrEqual(context.Diagnostics.Player.ComponentBagExhaustedAtRecruit, 1);
                Assert.AreEqual(1, context.Diagnostics.Player.ComponentBagExhaustedAtWave);
                var exhaustedAtTime = context.Diagnostics.Player.ComponentBagExhaustedAtTime;
                context.Diagnostics.Tick(0.01f);
                Assert.AreEqual(exhaustedAtTime, context.Diagnostics.Player.ComponentBagExhaustedAtTime);
            }
        }

        [Test]
        public void DiagnosticsCaptureBothSidesAtEveryRequiredWaveWithoutChangingTheRun()
        {
            using (var context = new DiagnosticsContext(603, CreateShortDurationConfiguration()))
            {
                Assert.IsTrue(context.Runtime.StartRun());
                var expectedWaves = new[] { 1, 3, 6, 8, 11, 12, 15, 16, 20 };
                foreach (var wave in expectedWaves)
                {
                    if (wave != 1)
                    {
                        Assert.IsTrue(context.Runtime.JumpToWave(wave));
                    }

                    context.Diagnostics.Tick(0.001f);
                }

                Assert.AreEqual(expectedWaves.Length * 2, context.Diagnostics.Snapshots.Count);
                foreach (var wave in expectedWaves)
                {
                    Assert.AreEqual(2, CountSnapshots(context.Diagnostics.Snapshots, wave));
                }

                Assert.AreEqual(MatchState.Running, context.Match.State);
                Assert.AreEqual(20, context.Runtime.CurrentWave);
                Assert.AreEqual(0, context.PlayerRecruitment.CompletedRecruitments);
                Assert.AreEqual(0, context.AiRecruitment.CompletedRecruitments);
            }
        }

        [Test]
        public void DeveloperStopProducesSummaryWithoutChangingMatchOutcome()
        {
            using (var context = new DiagnosticsContext(604))
            {
                Assert.IsTrue(context.Runtime.StartRun());
                context.Diagnostics.Tick(0.01f);
                Assert.IsTrue(context.Runtime.StopRun());
                var summary = context.Diagnostics.StopAndReport();

                StringAssert.Contains("RunSeed=604", summary);
                StringAssert.Contains("Result=DeveloperStopped", summary);
                StringAssert.Contains("Player:", summary);
                StringAssert.Contains("AI:", summary);
                Assert.AreEqual(MatchState.Running, context.Match.State);
            }
        }

        private static int CountSnapshots(IReadOnlyList<PressureWaveSnapshot> snapshots, int wave)
        {
            var count = 0;
            foreach (var snapshot in snapshots)
            {
                if (snapshot.Wave == wave)
                {
                    count++;
                }
            }

            return count;
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

        private sealed class DiagnosticsContext : IDisposable
        {
            public DiagnosticsContext(int seed, TwentyWavePressureConfiguration configuration = null)
            {
                Match = new MatchController(seed);
                Match.Player.AddResources(1000);
                Match.AI.AddResources(1000);
                PlayerBoard = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
                var aiBoard = DragonBoundBoardLayout.CreateDefault(TeamSide.AI);
                var playerDestination = new BoardRecruitDestination(PlayerBoard);
                var aiDestination = new BoardRecruitDestination(aiBoard);
                PlayerShovels = new ShovelUnlockService(PlayerBoard, playerDestination);
                var aiShovels = new ShovelUnlockService(aiBoard, aiDestination);
                var catalog = GreyboxRecruitmentCatalog.Create();
                PlayerRecruitment = CreateRecruitment(
                    Match.Player,
                    catalog,
                    seed,
                    "diagnostics.player",
                    playerDestination,
                    () => PlayerBoard.GetPositions(CellType.Locked).Count);
                AiRecruitment = CreateRecruitment(
                    Match.AI,
                    catalog,
                    seed + 1,
                    "diagnostics.ai",
                    aiDestination,
                    () => aiBoard.GetPositions(CellType.Locked).Count);
                Runtime = new TwentyWavePressureRuntime(
                    Match,
                    playerDestination,
                    aiDestination,
                    seed,
                    configuration);
                Diagnostics = new PressureRunDiagnostics(
                    Match,
                    Runtime,
                    PlayerRecruitment,
                    AiRecruitment,
                    playerDestination,
                    aiDestination,
                    PlayerShovels,
                    aiShovels);
            }

            public MatchController Match { get; }
            public BoardGrid PlayerBoard { get; }
            public RecruitmentService PlayerRecruitment { get; }
            public RecruitmentService AiRecruitment { get; }
            public ShovelUnlockService PlayerShovels { get; }
            public TwentyWavePressureRuntime Runtime { get; }
            public PressureRunDiagnostics Diagnostics { get; }

            public void Dispose()
            {
                Diagnostics.Dispose();
            }

            private static RecruitmentService CreateRecruitment(
                TeamState team,
                RecruitmentCatalog catalog,
                int seed,
                string prefix,
                BoardRecruitDestination destination,
                Func<int> lockedCellCount)
            {
                var bag = LimitedComponentBag.CreateBag(
                    seed,
                    LimitedComponentBag.DefaultContentVersion,
                    catalog);
                var deck = new RecruitDeck(
                    catalog,
                    seed,
                    prefix,
                    bag,
                    shovelState: new ShovelRecruitmentState(lockedCellCount));
                return new RecruitmentService(team, deck, destination);
            }
        }
    }
}
