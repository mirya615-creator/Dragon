using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class W1ToW5SurvivalFunnelTests
    {
        [Test]
        public void SameSeedFunnelIsDeterministicForOriginalAndSaltSwap()
        {
            var originalFirst = CoreLoopRhythmDiagnostics.RunW1ToW5SurvivalFunnel(1, 1, false);
            var originalSecond = CoreLoopRhythmDiagnostics.RunW1ToW5SurvivalFunnel(1, 1, false);
            var swappedFirst = CoreLoopRhythmDiagnostics.RunW1ToW5SurvivalFunnel(1, 1, true);
            var swappedSecond = CoreLoopRhythmDiagnostics.RunW1ToW5SurvivalFunnel(1, 1, true);

            Assert.AreEqual(originalFirst.ToCsv(), originalSecond.ToCsv());
            Assert.AreEqual(swappedFirst.ToCsv(), swappedSecond.ToCsv());
            Assert.AreEqual(originalFirst.SampleCount, swappedFirst.SampleCount);
        }

        [Test]
        public void FunnelDoesNotChangeProductionW6BossDefault()
        {
            var runtime = new TwentyWavePressureRuntime(new MatchController(811), null, null, 811);
            Assert.AreEqual(600f, runtime.SoulChainBossMaxHitPoints, 0.0001f);
        }
    }
}
