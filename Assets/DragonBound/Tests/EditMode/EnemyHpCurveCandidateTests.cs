using System;
using System.IO;
using DragonBound.Core;
using DragonBound.Recruitment;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class EnemyHpCurveCandidateTests
    {
        private static readonly float[] R1ProductionHp =
        {
            25.5f, 26.1f, 26.7f, 35f, 45f, 63f, 95f, 120f, 145f, 175f,
            205f, 240f, 275f, 315f, 360f, 410f, 465f, 525f, 590f, 660f
        };

        private static readonly float[] LargeScaleModerateHp =
        {
            25.5f, 26.1f, 26.7f, 35f, 50f, 70f, 95f, 120f, 145f, 175f,
            205f, 240f, 275f, 315f, 360f, 410f, 465f, 525f, 590f, 660f
        };

        private static readonly float[] LargeScaleStrongHp =
        {
            25.5f, 26.1f, 26.7f, 40f, 60f, 85f, 120f, 150f, 185f, 225f,
            270f, 320f, 375f, 435f, 500f, 570f, 645f, 725f, 810f, 900f
        };

        [Test]
        public void LargeScaleCandidatesFreezeAllNonHpFieldsAndMatchTheTwentyWaveTables()
        {
            Assert.AreEqual(30f, EnemyRuntime.DefaultMaxHitPoints, 0.0001f,
                "The production EnemyRuntime base HP must not be silently changed by a candidate.");

            var current = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.CurrentProduction);
            var moderate = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.LargeScaleModerate);
            var strong = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.LargeScaleStrong);
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                var a = current.GetWave(wave);
                var b = moderate.GetWave(wave);
                var c = strong.GetWave(wave);
                AssertNonHpFieldsEqual(a, b);
                AssertNonHpFieldsEqual(a, c);
                Assert.AreEqual(R1ProductionHp[wave - 1], EnemyRuntime.DefaultMaxHitPoints * a.HealthMultiplier, 0.0001f);
                Assert.AreEqual(LargeScaleModerateHp[wave - 1], EnemyRuntime.DefaultMaxHitPoints * b.HealthMultiplier, 0.0001f);
                Assert.AreEqual(LargeScaleStrongHp[wave - 1], EnemyRuntime.DefaultMaxHitPoints * c.HealthMultiplier, 0.0001f);
                Assert.Greater(a.HealthMultiplier, 0f);
                Assert.Greater(b.HealthMultiplier, 0f);
                Assert.Greater(c.HealthMultiplier, 0f);

                var moderateSpawn = new PressureRaceEnemySpawn(
                    EnemyArchetype.Normal,
                    EnemyRuntime.DefaultMaxHitPoints * b.HealthMultiplier,
                    b.MoveSpeedMultiplier,
                    moderate.GetMoveSpeedCellsPerSecond(EnemyArchetype.Normal));
                var strongSpawn = new PressureRaceEnemySpawn(
                    EnemyArchetype.Elite,
                    EnemyRuntime.DefaultMaxHitPoints * c.HealthMultiplier,
                    c.MoveSpeedMultiplier,
                    strong.GetMoveSpeedCellsPerSecond(EnemyArchetype.Elite));
                Assert.AreEqual(LargeScaleModerateHp[wave - 1], moderateSpawn.MaxHitPoints, 0.0001f);
                Assert.AreEqual(LargeScaleStrongHp[wave - 1], strongSpawn.MaxHitPoints, 0.0001f);
            }

            // W1-W3 are deliberately bit-identical in effective HP across A/B/C.
            for (var wave = 1; wave <= 3; wave++)
            {
                Assert.AreEqual(current.GetWave(wave).HealthMultiplier, moderate.GetWave(wave).HealthMultiplier, 0.000001f);
                Assert.AreEqual(current.GetWave(wave).HealthMultiplier, strong.GetWave(wave).HealthMultiplier, 0.000001f);
            }
        }

        [Test]
        public void CandidatesAreDeterministicAndDoNotChangeLastHitXp()
        {
            var first = CoreLoopRhythmDiagnostics.Run(1, 1, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.LargeScaleModerate);
            var second = CoreLoopRhythmDiagnostics.Run(1, 1, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.LargeScaleModerate);
            Assert.AreEqual(first.FormatReport(), second.FormatReport());
            StringAssert.Contains("TTK W7", first.FormatReport());
            StringAssert.Contains("EnemyBacklog W12", first.FormatReport());
            StringAssert.Contains("HeroXPCombat", first.FormatReport());

            // Formal reward rule remains production Last-Hit: Normal/Fast=1, Elite=3.
            Assert.AreEqual(1, new EnemyRuntime("normal", TeamSide.Player, archetype: EnemyArchetype.Normal).ExperienceReward);
            Assert.AreEqual(1, new EnemyRuntime("fast", TeamSide.Player, archetype: EnemyArchetype.Fast).ExperienceReward);
            Assert.AreEqual(3, new EnemyRuntime("elite", TeamSide.Player, archetype: EnemyArchetype.Elite).ExperienceReward);
        }

        [Test]
        [Category("Diagnostics")]
        public void RunsLargeScaleAbcOneThousandSeedComparison()
        {
            var path = Path.Combine(Application.dataPath, "..", "Logs", "codex-small-enemy-pressure-v2-progress.txt");
            File.WriteAllText(path, "SMALL_ENEMY_PRESSURE_BALANCE_V2_PROGRESS\\n");
            var wasEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            CoreLoopRhythmReport current;
            CoreLoopRhythmReport moderate;
            CoreLoopRhythmReport strong;
            try
            {
                current = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.CurrentProduction, completed => WriteProgress(path, "A_Current", completed));
                moderate = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.LargeScaleModerate, completed => WriteProgress(path, "B_LargeScaleModerate", completed));
                strong = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.LargeScaleStrong, completed => WriteProgress(path, "C_LargeScaleStrong", completed));
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasEnabled;
            }

            Assert.AreEqual(1000, current.SampleCount);
            Assert.AreEqual(1000, moderate.SampleCount);
            Assert.AreEqual(1000, strong.SampleCount);
            TestContext.WriteLine("A_CURRENT\\n" + current.FormatReport());
            TestContext.WriteLine("B_LARGE_SCALE_MODERATE\\n" + moderate.FormatReport());
            TestContext.WriteLine("C_LARGE_SCALE_STRONG\\n" + strong.FormatReport());
        }

        private static void AssertNonHpFieldsEqual(PressureRaceWaveDefinition expected, PressureRaceWaveDefinition candidate)
        {
            Assert.AreEqual(expected.WaveIndex, candidate.WaveIndex);
            Assert.AreEqual(expected.EnemyCountPerSide, candidate.EnemyCountPerSide);
            Assert.AreEqual(expected.WaveDurationSeconds, candidate.WaveDurationSeconds, 0.000001f);
            Assert.AreEqual(expected.NormalWeight, candidate.NormalWeight, 0.000001f);
            Assert.AreEqual(expected.FastWeight, candidate.FastWeight, 0.000001f);
            Assert.AreEqual(expected.EliteWeight, candidate.EliteWeight, 0.000001f);
            Assert.AreEqual(expected.MoveSpeedMultiplier, candidate.MoveSpeedMultiplier, 0.000001f);
            Assert.AreEqual(expected.HasBossSlot, candidate.HasBossSlot);
            Assert.AreEqual(expected.SpawnIntervalSeconds, candidate.SpawnIntervalSeconds, 0.000001f);
            Assert.AreEqual(expected.FirstSpawnDelaySeconds, candidate.FirstSpawnDelaySeconds, 0.000001f);
            Assert.AreEqual(expected.InterWaveSpawnGapSeconds, candidate.InterWaveSpawnGapSeconds, 0.000001f);
        }

        private static void WriteProgress(string path, string candidate, int completed)
        {
            if (completed % 10 != 0) return;
            File.AppendAllText(path, candidate + " CompletedSeeds=" + completed + "\\n");
        }
    }
}
