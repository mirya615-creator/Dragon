using DragonBound.Core;
using DragonBound.Recruitment;
using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class GlobalPressureDiagnosticsTests
    {
        [Test]
        [Category("Diagnostics")]
        public void RunsCoreLoopRhythmPoliciesPairedOneThousandSeeds()
        {
            var progressPath = Path.Combine(Application.dataPath, "..", "Logs", "codex-core-loop-rhythm-policy-ab-progress.txt");
            File.WriteAllText(progressPath, "CORE_LOOP_RHYTHM_POLICY_AB_PROGRESS\n");
            var progressLock = new object();
            var wasEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            CoreLoopRhythmReport v2;
            CoreLoopRhythmReport v3;
            try
            {
                v2 = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V2, completed =>
                {
                    if (completed % 10 == 0)
                    {
                        lock (progressLock)
                        {
                            File.AppendAllText(progressPath, $"V2 CompletedSeeds={completed}\n");
                        }
                    }
                });
                v3 = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V3, completed =>
                {
                    if (completed % 10 == 0)
                    {
                        lock (progressLock)
                        {
                            File.AppendAllText(progressPath, $"V3 CompletedSeeds={completed}\n");
                        }
                    }
                });
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasEnabled;
            }

            Assert.AreEqual(1000, v2.SampleCount);
            Assert.AreEqual(1000, v3.SampleCount);
            Assert.AreEqual(RecruitComponentPolicy.V2, v2.ComponentPolicy);
            Assert.AreEqual(RecruitComponentPolicy.V3, v3.ComponentPolicy);
            Assert.AreEqual(0, v2.Player.ComponentConservationFailures);
            Assert.AreEqual(0, v2.AI.ComponentConservationFailures);
            Assert.AreEqual(0, v3.Player.ComponentConservationFailures);
            Assert.AreEqual(0, v3.AI.ComponentConservationFailures);
            Assert.AreEqual(0, v2.Player.RecruitStallTotal);
            Assert.AreEqual(0, v2.AI.RecruitStallTotal);
            Assert.AreEqual(0, v3.Player.RecruitStallTotal);
            Assert.AreEqual(0, v3.AI.RecruitStallTotal);
            Assert.AreEqual(10, v2.Timing.GetWave(1).SpawnCount);
            Assert.AreEqual(4f, v2.Timing.GetWave(1).FirstSpawnTimeSeconds, 0.051f);
            Assert.AreEqual(6.5f, v2.Timing.GetWave(1).ActualInterWaveGapSeconds, 0.051f);
            TestContext.WriteLine("V2\n" + v2.FormatReport());
            TestContext.WriteLine("V3\n" + v3.FormatReport());
        }

        [Test]
        public void BoardBenchCapacityAuditUsesFormalV3AndIsDeterministic()
        {
            var first = CoreLoopRhythmDiagnostics.RunBoardBenchCapacityAudit(1, 2);
            var second = CoreLoopRhythmDiagnostics.RunBoardBenchCapacityAudit(1, 2);

            Assert.AreEqual(2, first.SampleCount);
            Assert.AreEqual(2, first.Player.RunCount);
            Assert.AreEqual(2, first.AI.RunCount);
            Assert.AreEqual(first.Player.AverageOccupancyRatio, second.Player.AverageOccupancyRatio, 0.000001d);
            Assert.AreEqual(first.AI.AverageOccupancyRatio, second.AI.AverageOccupancyRatio, 0.000001d);
            Assert.GreaterOrEqual(first.Player.ComponentDiscards, 0);
            Assert.GreaterOrEqual(first.AI.ComponentDiscards, 0);
            TestContext.WriteLine(first.FormatReport());
        }

        [Test]
        [Category("Diagnostics")]
        public void RunsFormalV3BoardBenchCapacityAuditOneThousandSeeds()
        {
            var progressPath = Path.Combine(Application.dataPath, "..", "Logs", "codex-board-bench-capacity-audit-progress.txt");
            File.WriteAllText(progressPath, "BOARD_BENCH_CAPACITY_AUDIT_V1_PROGRESS\n");
            var progressLock = new object();
            var wasEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            BoardBenchCapacityAuditReport report;
            try
            {
                report = CoreLoopRhythmDiagnostics.RunBoardBenchCapacityAudit(1, 1000, completed =>
                {
                    if (completed % 10 != 0) return;
                    lock (progressLock)
                    {
                        File.AppendAllText(progressPath, $"CompletedSeeds={completed}\n");
                    }
                });
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasEnabled;
            }

            Assert.AreEqual(1000, report.SampleCount);
            Assert.AreEqual(1000, report.Player.RunCount);
            Assert.AreEqual(1000, report.AI.RunCount);
            TestContext.WriteLine(report.FormatReport());
        }
    }
}
