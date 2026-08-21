using System;
using System.IO;
using System.Reflection;
using DragonBound.Core;
using DragonBound.Recruitment;
using NUnit.Framework;
using UnityEngine;

namespace DragonBound.Tests.EditMode
{
    public sealed class BareRunTargetValidationTests
    {
        [Test]
        public void ModerateCandidatePreservesProductionAndExposesBareRunDiagnostics()
        {
            var production = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.CurrentProduction);
            var moderate = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.LargeScaleModerate);
            Assert.AreEqual(25.5f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.LargeScaleModerate, 1));
            Assert.AreEqual(95f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.LargeScaleModerate, 7));
            Assert.AreEqual(660f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.LargeScaleModerate, 20));
            Assert.AreEqual(95f, EnemyRuntime.DefaultMaxHitPoints * production.GetWave(7).HealthMultiplier, 0.0001f);
            Assert.AreEqual(95f, EnemyRuntime.DefaultMaxHitPoints * moderate.GetWave(7).HealthMultiplier, 0.0001f);

            var report = CoreLoopRhythmDiagnostics.Run(1, 1, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.LargeScaleModerate);
            var formatted = report.FormatReport();
            StringAssert.Contains("DeathWindows", formatted);
            StringAssert.Contains("P90PeakAlive", formatted);
            StringAssert.Contains("HeroCount=0-1", formatted);
            StringAssert.Contains("P50Level", formatted);
            StringAssert.Contains("Lv1Stagnation", formatted);
        }

        [Test]
        public void LastHitXpProductionSettlementIsPresentForBareRun()
        {
            var heroXpSettlement = typeof(HeroXpSettlement).GetMethod(
                nameof(HeroXpSettlement.GetAwardedExperience),
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(heroXpSettlement);

            var heroKill = new EnemyRuntime("hero", TeamSide.Player, archetype: EnemyArchetype.Elite);
            heroKill.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Hero,
                TeamSide.Player,
                "pair-link",
                "HERO_DRAGON_RIDER"));
            Assert.AreEqual(3, HeroXpSettlement.GetAwardedExperience(heroKill));

            var basicKill = new EnemyRuntime("basic", TeamSide.Player, archetype: EnemyArchetype.Normal);
            basicKill.RecordDamageOwner(new CombatDamageOwner(CombatDamageOwnerKind.BasicUnit, TeamSide.Player, "basic-unit"));
            Assert.AreEqual(0, HeroXpSettlement.GetAwardedExperience(basicKill));
        }

        [Test]
        [Category("Diagnostics")]
        public void RunsLargeScaleModerateBareRunOneThousandSeeds()
        {
            var logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "codex-small-enemy-pressure-v2-1-bare-run.txt"));
            var wasLoggingEnabled = Debug.unityLogger.logEnabled;
            CoreLoopRhythmReport report;
            try
            {
                Debug.unityLogger.logEnabled = false;
                report = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.LargeScaleModerate);
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasLoggingEnabled;
            }
            var formatted = report.FormatReport();
            File.WriteAllText(logPath, formatted);

            Assert.AreEqual(1000, report.SampleCount);
            Assert.AreEqual(EnemyHpCurveCandidate.LargeScaleModerate, report.HpCandidate);
            StringAssert.Contains("DeathWindows", formatted);
            StringAssert.Contains("HeroFormationXP", formatted);
            TestContext.WriteLine(formatted);
        }

        [Test]
        [Category("Diagnostics")]
        public void MatchEndClosureAuditUsesOnlyGameplaySettlement()
        {
            var wasLoggingEnabled = Debug.unityLogger.logEnabled;
            CoreLoopRhythmReport report;
            try
            {
                Debug.unityLogger.logEnabled = false;
                report = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.LargeScaleModerate);
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasLoggingEnabled;
            }

            var formatted = report.FormatReport();
            var logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "codex-bare-run-match-end-closure-1000.txt"));
            File.WriteAllText(logPath, formatted);
            Assert.AreEqual(1000, report.SampleCount);
            Assert.AreEqual(EnemyHpCurveCandidate.LargeScaleModerate, report.HpCandidate);
            Assert.AreEqual(1000, report.MatchEnd.GameplayEndCount);
            Assert.AreEqual(0, report.MatchEnd.InvalidOrDeveloperEndCount);
            Assert.AreEqual(1000, report.MatchEnd.CompletedDistributionCount);
            Assert.AreEqual(0, report.MatchEnd.GameplayEndsAfterScheduleCount);
            StringAssert.Contains("[MatchEnd] EndWaveDistribution", formatted);
            StringAssert.Contains("GameplayEndsAfterSchedule=", formatted);
            StringAssert.Contains("EndCauses=", formatted);
            TestContext.WriteLine(formatted);
        }

        [Test]
        [Category("Diagnostics")]
        public void W5W6ReliefCandidatesOnlyChangeTheRequestedWaves()
        {
            var control = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.LargeScaleModerate);
            var mild = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.W5W6MildRelief);
            var strong = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.W5W6StrongRelief);
            Assert.AreEqual(50f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.LargeScaleModerate, 5));
            Assert.AreEqual(70f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.LargeScaleModerate, 6));
            Assert.AreEqual(45f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.W5W6MildRelief, 5));
            Assert.AreEqual(63f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.W5W6MildRelief, 6));
            Assert.AreEqual(42.5f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.W5W6StrongRelief, 5));
            Assert.AreEqual(59.5f, EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.W5W6StrongRelief, 6));
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                if (wave == 5 || wave == 6) continue;
                Assert.AreEqual(
                    EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.LargeScaleModerate, wave),
                    EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.W5W6MildRelief, wave));
                Assert.AreEqual(
                    EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.LargeScaleModerate, wave),
                    EnemyHpCurveCandidates.GetExpectedMaxHitPoints(EnemyHpCurveCandidate.W5W6StrongRelief, wave));
            }
            Assert.AreEqual(50f, EnemyRuntime.DefaultMaxHitPoints * control.GetWave(5).HealthMultiplier, 0.0001f);
            Assert.AreEqual(45f, EnemyRuntime.DefaultMaxHitPoints * mild.GetWave(5).HealthMultiplier, 0.0001f);
            Assert.AreEqual(42.5f, EnemyRuntime.DefaultMaxHitPoints * strong.GetWave(5).HealthMultiplier, 0.0001f);
        }

        [Test]
        public void PromotedProductionMatchesR1CandidateForTheSameRunSeed()
        {
            var production = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.CurrentProduction);
            var r1 = EnemyHpCurveCandidates.Create(EnemyHpCurveCandidate.W5W6MildRelief);
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                Assert.AreEqual(production.GetWave(wave).HealthMultiplier, r1.GetWave(wave).HealthMultiplier, 0.000001f);
            }

            var productionReport = CoreLoopRhythmDiagnostics.Run(
                9191, 1, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.CurrentProduction);
            var r1Report = CoreLoopRhythmDiagnostics.Run(
                9191, 1, RecruitComponentPolicy.V3, EnemyHpCurveCandidate.W5W6MildRelief);
            Assert.AreEqual(
                NormalizeCandidateLabel(productionReport.FormatReport()),
                NormalizeCandidateLabel(r1Report.FormatReport()));
        }

        [Test]
        [Category("Diagnostics")]
        public void ReliefSweepCandidatesExposeDistinctLabels()
        {
            Assert.AreEqual("W5W6MildRelief", EnemyHpCurveCandidate.W5W6MildRelief.ToString());
            Assert.AreEqual("W5W6StrongRelief", EnemyHpCurveCandidate.W5W6StrongRelief.ToString());
        }

        private static string NormalizeCandidateLabel(string report)
        {
            return report
                .Replace("CurrentProduction", "R1")
                .Replace("W5W6MildRelief", "R1");
        }
    }
}
