using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class W6BareFullScheduleCalibrationTests
    {
        [Test]
        public void CalibrationOverrideUsesCandidateHitPointsWithoutChangingProductionDefault()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            var defaultRuntime = new TwentyWavePressureRuntime(new MatchController(810), null, null, 810, configuration);
            var calibratedRuntime = new TwentyWavePressureRuntime(
                new MatchController(810),
                null,
                null,
                810,
                configuration,
                soulChainBossMaxHitPoints: 420f);

            Assert.AreEqual(600f, SoulchainBinderConfiguration.GreyboxMaxHitPoints, 0.0001f);
            Assert.AreEqual(600f, defaultRuntime.SoulChainBossMaxHitPoints, 0.0001f);
            Assert.AreEqual(420f, calibratedRuntime.SoulChainBossMaxHitPoints, 0.0001f);
            Assert.IsTrue(calibratedRuntime.StartRun());
            Assert.IsTrue(calibratedRuntime.JumpToWave(TwentyWavePressureConfiguration.SoulChainBossWave));
            Assert.AreEqual(420f, calibratedRuntime.PlayerW6Boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(600f, SoulchainBinderConfiguration.GreyboxMaxHitPoints, 0.0001f);
        }

        [Test]
        public void FullScheduleCalibrationIsDeterministicAndUsesTheRealW6BossLifecycle()
        {
            var first = CoreLoopRhythmDiagnostics.RunW6BareCalibration(1, 1, 500f);
            var second = CoreLoopRhythmDiagnostics.RunW6BareCalibration(1, 1, 500f);

            Assert.AreEqual(first.ToCsv(), second.ToCsv());
            Assert.AreEqual(1, first.Player.SampleCount);
            Assert.AreEqual(1, first.AI.SampleCount);
            Assert.AreEqual(first.Player.Samples[0].BossSpawned, first.AI.Samples[0].BossSpawned);
            Assert.That(first.Player.Samples[0].BossDamageTotal,
                Is.EqualTo(first.Player.Samples[0].BasicDamageToBoss +
                            first.Player.Samples[0].HeroDamageToBoss +
                            first.Player.Samples[0].OtherDamageToBoss).Within(0.001f));
        }
    }
}
