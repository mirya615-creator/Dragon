using System;
using System.Globalization;
using System.IO;
using System.Text;
using DragonBound.Core;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    /// <summary>Batch-only real W1-W6 calibration sweep. It never writes the production Boss HP.</summary>
    public static class W6BareFullScheduleCalibrationBatch
    {
        private static readonly float[] CandidateHitPoints = { 350f, 400f, 450f, 500f, 550f, 600f, 650f };

        [MenuItem("DragonBound/Diagnostics/Run W6 Bare Full-Schedule Calibration 1000 Seed")]
        public static void RunFromMenu()
        {
            Run();
        }

        // Invoked with -executeMethod DragonBound.Editor.W6BareFullScheduleCalibrationBatch.Run.
        public static void Run()
        {
            const int firstSeed = 1;
            const int sampleCount = 1000;
            var logsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            var docsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs"));
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var csv = new StringBuilder();
            var markdown = new StringBuilder();
            markdown.AppendLine("# W6 Bare Full-Schedule Calibration V1");
            markdown.AppendLine();
            markdown.AppendLine("- Schedule: real W1-W6 `CoreLoopRhythmDiagnostics` run from seeds 1-1000.");
            markdown.AppendLine("- Item/Rune: disabled by the normal bare diagnostics constructor.");
            markdown.AppendLine("- Boss: fixed Soulchain Binder mechanics; HP is an analysis input only.");
            markdown.AppendLine("- `500` remains the Greybox value. Formal W6 Boss HP remains **PENDING**.");
            markdown.AppendLine("- Qualified baseline sample: Boss spawned, the run remained active through its spawn, and at least one deployed Basic or active Hero existed at that instant.");
            markdown.AppendLine("- Power proxy: deployed Basic count plus active Hero level sum. It is not a combat rating.");
            markdown.AppendLine("- Quality strata are fixed before analysis from that spawn-time proxy: lower, middle, and upper thirds of qualified samples per side.");
            markdown.AppendLine();
            markdown.AppendLine("| HP | Side | Qualified | Kill | Leak | TTK Mean | P10 | P25 | P50 | P75 | P90 |");
            markdown.AppendLine("| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");

            var recommendation = 0f;
            var recommendationDistance = double.MaxValue;
            var hasHeader = false;
            var wasLoggingEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                foreach (var candidate in CandidateHitPoints)
                {
                    var report = CoreLoopRhythmDiagnostics.RunW6BareCalibration(firstSeed, sampleCount, candidate);
                    AppendCandidate(markdown, candidate, "Player", report.Player);
                    AppendCandidate(markdown, candidate, "AI", report.AI);
                    AppendDetail(markdown, candidate, "Player", report.Player);
                    AppendDetail(markdown, candidate, "AI", report.AI);
                    AppendCsv(csv, report, ref hasHeader);

                    var playerMedian = report.Player.PercentileBossTtk(0.50);
                    var aiMedian = report.AI.PercentileBossTtk(0.50);
                    var distance = Math.Abs(playerMedian - 30d) + Math.Abs(aiMedian - 30d);
                    if (report.Player.QualifiedBaselineCount > 0 && report.AI.QualifiedBaselineCount > 0 &&
                        playerMedian >= 28d && playerMedian <= 32d && aiMedian >= 28d && aiMedian <= 32d &&
                        distance < recommendationDistance)
                    {
                        recommendation = candidate;
                        recommendationDistance = distance;
                    }
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasLoggingEnabled;
            }

            markdown.AppendLine();
            markdown.AppendLine("## Recommendation");
            markdown.AppendLine();
            markdown.AppendLine(recommendation > 0f
                ? "Analysis recommendation: `" + recommendation.ToString("0", CultureInfo.InvariantCulture) + " HP` is the nearest candidate whose Player and AI qualified-sample medians both fall in the 28-32s target window. This is not a production promotion; review leak rate, residual pressure, and low/normal/high-quality strata before approval."
                : "No candidate satisfied the 28-32s qualified-sample median target on both Player and AI. Production remains unchanged; the side bias and low W6 qualification rate must be reviewed before formal HP selection.");
            markdown.AppendLine();
            markdown.AppendLine("Raw per-seed telemetry is written to `Logs/W6BareFullScheduleCalibration.csv`.");

            var csvPath = Path.Combine(logsDirectory, "W6BareFullScheduleCalibration.csv");
            var reportPath = Path.Combine(docsDirectory, "W6BareFullScheduleCalibrationV1.md");
            File.WriteAllText(csvPath, csv.ToString());
            File.WriteAllText(reportPath, markdown.ToString());
            Debug.Log("W6 Bare Full-Schedule Calibration complete. Csv=" + csvPath + " Report=" + reportPath);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void AppendCandidate(StringBuilder builder, float hp, string side, W6BareCalibrationAggregate aggregate)
        {
            builder.Append("| ").Append(hp.ToString("0", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(side).Append(" | ")
                .Append(aggregate.QualifiedBaselineRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.BossKillRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.BossLeakRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.AverageBossTtkSeconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.PercentileBossTtk(0.10).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.PercentileBossTtk(0.25).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.PercentileBossTtk(0.50).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.PercentileBossTtk(0.75).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.PercentileBossTtk(0.90).ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" |");
        }

        private static void AppendCsv(StringBuilder destination, W6BareFullScheduleCalibrationReport report, ref bool hasHeader)
        {
            var text = report.ToCsv();
            if (!hasHeader)
            {
                destination.Append(text);
                hasHeader = true;
                return;
            }

            var firstLine = text.IndexOf('\n');
            if (firstLine >= 0) destination.Append(text.Substring(firstLine + 1));
        }

        private static void AppendDetail(StringBuilder builder, float hp, string side, W6BareCalibrationAggregate aggregate)
        {
            var damageTotal = aggregate.AverageBossDamage;
            var basicShare = damageTotal <= 0d ? 0d : aggregate.AverageBasicDamage / damageTotal;
            var heroShare = damageTotal <= 0d ? 0d : aggregate.AverageHeroDamage / damageTotal;
            builder.AppendLine();
            builder.AppendLine("- HP " + hp.ToString("0", CultureInfo.InvariantCulture) + " / " + side +
                ": SpawnBasic=" + aggregate.AverageBasicAtBossSpawn.ToString("0.00", CultureInfo.InvariantCulture) +
                ", SpawnHero=" + aggregate.AverageHeroAtBossSpawn.ToString("0.00", CultureInfo.InvariantCulture) +
                ", Damage Basic/Hero=" + basicShare.ToString("P2", CultureInfo.InvariantCulture) + "/" + heroShare.ToString("P2", CultureInfo.InvariantCulture) +
                ", Cast S/F=" + aggregate.AverageSoulChainCastsStarted.ToString("0.00", CultureInfo.InvariantCulture) + "/" + aggregate.AverageSoulChainCastsSucceeded.ToString("0.00", CultureInfo.InvariantCulture) + "/" + aggregate.AverageSoulChainCastsFailed.ToString("0.00", CultureInfo.InvariantCulture) +
                ", ControlUnitSeconds=" + aggregate.AverageControlUnitSeconds.ToString("0.00", CultureInfo.InvariantCulture) +
                ", BossAliveAtW7=" + aggregate.BossAliveAtW7StartRate.ToString("P2", CultureInfo.InvariantCulture) +
                ", W6Heart=" + aggregate.AverageHeartAtW6End.ToString("0.00", CultureInfo.InvariantCulture) +
                ", ResidualW1-W6=" + FormatResiduals(aggregate) +
                ", Strata=" + aggregate.FormatQualityStrata() + ".");
        }

        private static string FormatResiduals(W6BareCalibrationAggregate aggregate)
        {
            var builder = new StringBuilder();
            for (var wave = 1; wave <= 6; wave++)
            {
                if (wave > 1) builder.Append('/');
                builder.Append(aggregate.AverageResidualAtNextWaveStart(wave).ToString("0.00", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
