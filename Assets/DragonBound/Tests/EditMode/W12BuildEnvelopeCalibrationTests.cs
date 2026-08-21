using DragonBound.Items;
using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class W12BuildEnvelopeCalibrationTests
    {
        [Test]
        public void CalibrationUsesAnalysisHpAndIsDeterministicWithoutChangingProductionDefault()
        {
            var first = CoreLoopRhythmDiagnostics.RunW12BuildEnvelopeCalibration(
                1,
                1,
                1200f,
                _ => new EmptyItemRunSnapshotProvider());
            var second = CoreLoopRhythmDiagnostics.RunW12BuildEnvelopeCalibration(
                1,
                1,
                1200f,
                _ => new EmptyItemRunSnapshotProvider());

            Assert.AreEqual(first.ToCsv(), second.ToCsv());
            Assert.AreEqual(1200f, first.BossMaxHitPoints, 0.0001f);
            Assert.AreEqual(600f, SoulchainBinderConfiguration.GreyboxMaxHitPoints, 0.0001f);
            Assert.AreEqual(1200f, StormcallerPriestConfiguration.GreyboxMaxHitPoints, 0.0001f);
            Assert.AreEqual(1, first.Player.SampleCount);
            Assert.AreEqual(1, first.AI.SampleCount);
        }

        [Test]
        public void DirectW12CalibrationUsesCandidateHpAndRecordsItsNonEmptyFixture()
        {
            var report = CoreLoopRhythmDiagnostics.RunDirectW12BuildEnvelopeCalibration(
                1,
                1,
                1300f,
                _ => new EmptyItemRunSnapshotProvider());

            Assert.AreEqual("Direct-W12", report.Cohort);
            Assert.AreEqual(1300f, report.Player.Samples[0].BossMaxHitPoints, 0.0001f);
            Assert.AreEqual(1300f, report.AI.Samples[0].BossMaxHitPoints, 0.0001f);
            Assert.Greater(report.Player.Samples[0].DirectSetupBoardUnits + report.Player.Samples[0].DirectSetupBenchUnits, 0);
            Assert.Greater(report.AI.Samples[0].DirectSetupBoardUnits + report.AI.Samples[0].DirectSetupBenchUnits, 0);
        }

        [Test]
        public void CalibrationCsvIdentifiesCandidateCohortSeedAndSide()
        {
            var report = CoreLoopRhythmDiagnostics.RunDirectW12BuildEnvelopeCalibration(
                1,
                1,
                1100f,
                _ => new EmptyItemRunSnapshotProvider());

            StringAssert.StartsWith("candidateHp,cohort,runSeed,side,", report.ToCsv());
            StringAssert.Contains("1100.00,Direct-W12,1,Player,1100.00", report.ToCsv());
        }
    }
}
