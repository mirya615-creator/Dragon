using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class JointBalanceCalibrationTests
    {
        [Test]
        public void FullCalibrationIsDeterministicAndEmitsAllFourBossRows()
        {
            var first = CoreLoopRhythmDiagnostics.RunJointBalanceCalibration(41, 1, "TEST");
            var second = CoreLoopRhythmDiagnostics.RunJointBalanceCalibration(41, 1, "TEST");

            Assert.AreEqual(first.ToCsv(), second.ToCsv());
            Assert.AreEqual(1, first.Runs.Count);
            Assert.AreEqual(4, first.Runs[0].Player.Bosses.Count);
            Assert.AreEqual(4, first.Runs[0].AI.Bosses.Count);
            Assert.AreEqual(6, first.Runs[0].Player.Bosses[0].Wave);
            Assert.AreEqual(20, first.Runs[0].Player.Bosses[3].Wave);
        }

        [Test]
        public void DirectCalibrationUsesW16AndW20CandidateHpWithoutChangingDefaults()
        {
            var w16 = CoreLoopRhythmDiagnostics.RunDirectBossCalibration(
                7,
                1,
                16,
                "W16_TEST",
                bloodcrownBossMaxHitPoints: 2800f);
            var w20 = CoreLoopRhythmDiagnostics.RunDirectBossCalibration(
                7,
                1,
                20,
                "W20_TEST",
                worldeaterBossMaxHitPoints: 4000f);

            Assert.IsTrue(w16.Runs[0].Player.Bosses[2].Spawned);
            Assert.AreEqual(2800f, w16.Runs[0].Player.Bosses[2].MaxHitPoints, 0.0001f);
            Assert.IsTrue(w20.Runs[0].Player.Bosses[3].Spawned);
            Assert.AreEqual(4000f, w20.Runs[0].Player.Bosses[3].MaxHitPoints, 0.0001f);

            var production = new TwentyWavePressureRuntime(new MatchController(7), null, null, 7);
            Assert.AreEqual(600f, production.SoulChainBossMaxHitPoints, 0.0001f);
            Assert.AreEqual(1200f, production.StormcallerBossMaxHitPoints, 0.0001f);
        }
    }
}
