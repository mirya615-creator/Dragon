using System;
using System.Globalization;
using System.IO;
using System.Text;
using DragonBound.Core;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    /// <summary>
    /// Bounded, production-schedule W6 HP calibration. This batch never writes production HP.
    /// </summary>
    public static class W6SoulchainFormalHpCalibrationBatch
    {
        private const int FirstSeed = 1;
        private const int SampleCount = 1000;
        private static readonly float[] CandidateHitPoints = { 700f, 800f, 900f };

        [MenuItem("DragonBound/Diagnostics/Run W6 Soulchain Formal HP Calibration 1000 Seed")]
        public static void RunFromMenu()
        {
            Run();
        }

        // Invoked with -executeMethod DragonBound.Editor.W6SoulchainFormalHpCalibrationBatch.Run.
        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsDirectory = Path.Combine(root, "Logs");
            var docsDirectory = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var markdown = new StringBuilder();
            var csv = new StringBuilder();
            markdown.AppendLine("# W6 Soulchain Binder Formal HP Calibration V1");
            markdown.AppendLine();
            markdown.AppendLine("- Real Production W1-W6 schedule, Recruit V3, normal-only enemies at 0.60 cells/s, current AI, Item/Rune disabled.");
            markdown.AppendLine("- Same RunSeed set `1..1000` for every candidate; Player and AI are reported independently.");
            markdown.AppendLine("- Soulchain mechanics are unchanged. HP is an analysis input; this batch never writes Production HP.");
            markdown.AppendLine("- Greybox reference: `500` post-fix BossSpawn `76.90%` both sides, TTK P50 `21.75s` Player / `23.10s` AI.");
            markdown.AppendLine("- Candidate bracket rationale: existing 500/650 evidence places the post-fix 28-32s target above 650; bounded candidates are 700/800/900.");
            markdown.AppendLine();
            markdown.AppendLine("## Candidate results");
            markdown.AppendLine();

            var recommendation = 0f;
            var recommendationScore = double.MaxValue;
            var previousLogging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                foreach (var candidate in CandidateHitPoints)
                {
                    var report = CoreLoopRhythmDiagnostics.RunW6BareCalibration(FirstSeed, SampleCount, candidate);
                    if (csv.Length == 0)
                    {
                        csv.AppendLine("hp,side,samples,bossSpawned,bossKilled,bossLeaked,bossAliveAtW7,bossNotGenerated,earlyMatchEndBeforeBoss,bossSpawnedUnresolved,killWindow28To32,ttkKillSampleCount,ttkP25,ttkP50,ttkP75,damage0To3Spawned,damage0To5Spawned,avgHittableBasicSpawned,avgHittableHeroSpawned,avgPredictedDpsSpawned,avgResidualW1ToW6");
                    }

                    AppendCandidate(markdown, candidate, "Player", report.Player, csv);
                    AppendCandidate(markdown, candidate, "AI", report.AI, csv);

                    var playerScore = Score(report.Player);
                    var aiScore = Score(report.AI);
                    var score = playerScore + aiScore;
                    if (score < recommendationScore && MeetsWindow(report.Player) && MeetsWindow(report.AI))
                    {
                        recommendation = candidate;
                        recommendationScore = score;
                    }
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
            }

            markdown.AppendLine();
            markdown.AppendLine("## Decision");
            markdown.AppendLine();
            if (recommendation > 0f)
            {
                markdown.AppendLine("Candidate `" + Format(recommendation) + " HP` satisfies the Player and AI kill-sample P50 window. This batch still does not promote Production; review full pressure outcomes before a separate promote task.");
            }
            else
            {
                markdown.AppendLine("No candidate satisfies the Player and AI kill-sample P50 28-32s window simultaneously. Production W6 Boss HP remains **PENDING**; do not change Boss speed, Soulchain mechanics, enemies, Hero/Basic values, AI, or wave schedule to force a fit.");
            }

            markdown.AppendLine();
            markdown.AppendLine("## Outcome definitions");
            markdown.AppendLine();
            markdown.AppendLine("`BossNotGenerated` means the shared run ended before the W6 Boss spawn node. `EarlyMatchEndBeforeBoss` is that subset with MatchEndWave <= 5. `BossSpawnedUnresolved` means the Boss spawned but was neither killed nor leaked in the recorded W6/W7 resolution window. TTK percentiles and 28-32s hit rate use killed Boss samples only.");
            markdown.AppendLine();
            markdown.AppendLine("Raw metrics: `Logs/W6SoulchainFormalHpCalibration.csv`.");

            var reportPath = Path.Combine(docsDirectory, "W6SoulchainFormalHpCalibrationV1.md");
            var csvPath = Path.Combine(logsDirectory, "W6SoulchainFormalHpCalibration.csv");
            File.WriteAllText(reportPath, markdown.ToString());
            File.WriteAllText(csvPath, csv.ToString());
            Debug.Log("W6 Soulchain formal HP calibration complete. Report=" + reportPath + " Csv=" + csvPath);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void AppendCandidate(
            StringBuilder markdown,
            float hp,
            string side,
            W6BareCalibrationAggregate aggregate,
            StringBuilder csv)
        {
            var samples = aggregate.Samples;
            var spawned = Count(samples, sample => sample.BossSpawned);
            var killed = Count(samples, sample => sample.BossKilled);
            var leaked = Count(samples, sample => sample.BossLeaked);
            var aliveAtW7 = Count(samples, sample => sample.BossAliveAtW7Start);
            var notGenerated = Count(samples, sample => !sample.BossSpawned);
            var earlyEnd = Count(samples, sample => !sample.BossSpawned && sample.MatchEndWave < 6);
            var unresolved = Count(samples, sample => sample.BossSpawned && !sample.BossKilled && !sample.BossLeaked);
            var killWindow = Count(samples, sample => sample.BossKilled && sample.BossTtkSeconds >= 28f && sample.BossTtkSeconds <= 32f);
            var killSampleCount = killed;
            var avgBasic = Average(samples, sample => sample.BossSpawned ? sample.HittableBasicCount : 0f, true);
            var avgHero = Average(samples, sample => sample.BossSpawned ? sample.HittableHeroCount : 0f, true);
            var avgDps = Average(samples, sample => sample.BossSpawned ? sample.EstimatedSingleTargetDps : 0f, true);
            var avgResidual = Average(samples, sample =>
            {
                var total = 0;
                for (var wave = 1; wave <= 6; wave++) total += sample.ResidualAtNextWaveStart[wave];
                return total;
            }, false);

            markdown.AppendLine("### " + Format(hp) + " HP / " + side);
            markdown.AppendLine();
            markdown.AppendLine("- BossSpawned=" + spawned + "/" + aggregate.SampleCount + " (" + Rate(spawned, aggregate.SampleCount) + ")");
            markdown.AppendLine("- BossKilled=" + killed + ", BossToGoal=" + leaked + ", BossAliveAtW7=" + aliveAtW7 + ", BossNotGenerated=" + notGenerated + ", EarlyMatchEndBeforeBoss=" + earlyEnd + ", BossSpawnedUnresolved=" + unresolved);
            markdown.AppendLine("- TTK kill samples=" + killSampleCount + ", P25/P50/P75=" + Format(aggregate.PercentileBossTtk(0.25)) + "/" + Format(aggregate.PercentileBossTtk(0.50)) + "/" + Format(aggregate.PercentileBossTtk(0.75)) + "s, Window28To32=" + killWindow + "/" + killSampleCount + " (" + Rate(killWindow, killSampleCount) + ")");
            markdown.AppendLine("- Spawned-sample Damage0-3/Damage0-5=" + Format(aggregate.AverageBossDamageFirst3SecondsSpawned) + "/" + Format(aggregate.AverageBossDamageFirst5SecondsSpawned) + ", HittableBasic/Hero=" + Format(avgBasic) + "/" + Format(avgHero) + ", PredictedDPS=" + Format(avgDps));
            markdown.AppendLine("- Residual W1-W6 total average=" + Format(avgResidual) + ", BossAliveAtW7=" + Rate(aliveAtW7, aggregate.SampleCount));
            markdown.AppendLine();

            csv.Append(Format(hp)).Append(',').Append(side).Append(',').Append(aggregate.SampleCount).Append(',')
                .Append(spawned).Append(',').Append(killed).Append(',').Append(leaked).Append(',').Append(aliveAtW7).Append(',')
                .Append(notGenerated).Append(',').Append(earlyEnd).Append(',').Append(unresolved).Append(',').Append(killWindow).Append(',')
                .Append(killSampleCount).Append(',').Append(Format(aggregate.PercentileBossTtk(0.25))).Append(',')
                .Append(Format(aggregate.PercentileBossTtk(0.50))).Append(',').Append(Format(aggregate.PercentileBossTtk(0.75))).Append(',')
                .Append(Format(aggregate.AverageBossDamageFirst3SecondsSpawned)).Append(',')
                .Append(Format(aggregate.AverageBossDamageFirst5SecondsSpawned)).Append(',').Append(Format(avgBasic)).Append(',')
                .Append(Format(avgHero)).Append(',').Append(Format(avgDps)).Append(',').Append(Format(avgResidual)).AppendLine();
        }

        private static bool MeetsWindow(W6BareCalibrationAggregate aggregate)
        {
            return aggregate.BossKillCount > 0 && aggregate.PercentileBossTtk(0.50) >= 28d && aggregate.PercentileBossTtk(0.50) <= 32d;
        }

        private static double Score(W6BareCalibrationAggregate aggregate)
        {
            return Math.Abs(aggregate.PercentileBossTtk(0.25) - 25d) + Math.Abs(aggregate.PercentileBossTtk(0.50) - 30d) + Math.Abs(aggregate.PercentileBossTtk(0.75) - 35d);
        }

        private static int Count(System.Collections.Generic.IReadOnlyList<W6BareCalibrationSideRun> samples, Func<W6BareCalibrationSideRun, bool> predicate)
        {
            var count = 0;
            foreach (var sample in samples) if (predicate(sample)) count++;
            return count;
        }

        private static double Average(System.Collections.Generic.IReadOnlyList<W6BareCalibrationSideRun> samples, Func<W6BareCalibrationSideRun, float> selector, bool spawnedOnly)
        {
            double total = 0d;
            var count = 0;
            foreach (var sample in samples)
            {
                if (spawnedOnly && !sample.BossSpawned) continue;
                total += selector(sample);
                count++;
            }

            return count == 0 ? 0d : total / count;
        }

        private static string Rate(int numerator, int denominator)
        {
            return (denominator == 0 ? 0d : numerator / (double)denominator).ToString("P2", CultureInfo.InvariantCulture);
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
