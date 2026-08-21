using System;
using System.Globalization;
using System.IO;
using System.Text;
using DragonBound.Core;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    /// <summary>Direct Boss mechanics envelope. It never changes the Production constructor.</summary>
    public static class BossDirectCalibrationBatch
    {
        private const int FirstSeed = 1;
        private const int SampleCount = 50;
        private static readonly float[] W16Candidates = { 2000f, 2400f, 2800f, 3200f };
        private static readonly float[] W20Candidates = { 4000f, 5000f, 6000f, 7000f };

        [MenuItem("DragonBound/Diagnostics/Run Direct W16 W20 Boss Envelope 50 Seed")]
        public static void RunFromMenu()
        {
            Run();
        }

        // Invoked with -executeMethod DragonBound.Editor.BossDirectCalibrationBatch.Run.
        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsDirectory = Path.Combine(root, "Logs");
            var docsDirectory = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var csv = new StringBuilder();
            var markdown = new StringBuilder();
            markdown.AppendLine("# Direct W16 / W20 Boss Envelope Calibration V1");
            markdown.AppendLine();
            markdown.AppendLine("- Seed set: `1..50` for every candidate.");
            markdown.AppendLine("- Cohort: `DIRECT_BOSS + AI_V0`, fixed development pair and diagnostic resources; this is not a Production run entry.");
            markdown.AppendLine("- Item/Rune: disabled. Rune standard build is not authoritative in the current client diagnostic API.");
            markdown.AppendLine("- TTK percentiles use killed samples only; early failure is retained in the spawn denominator.");
            markdown.AppendLine();

            var previousLogging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                markdown.AppendLine("## W16 Bloodcrown Tyrant");
                markdown.AppendLine();
                markdown.AppendLine("| HP | Side | Spawn | Kill | Goal | TTK P25 | TTK P50 | TTK P75 | Damage P50 |");
                markdown.AppendLine("| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
                foreach (var candidate in W16Candidates)
                {
                    var report = CoreLoopRhythmDiagnostics.RunDirectBossCalibration(
                        FirstSeed, SampleCount, 16, "DIRECT_W16_" + candidate.ToString("0", CultureInfo.InvariantCulture),
                        bloodcrownBossMaxHitPoints: candidate);
                    AppendSummary(markdown, candidate, report, 16);
                    AppendCsv(csv, report.ToCsv());
                }

                markdown.AppendLine();
                markdown.AppendLine("## W20 Worldeater Wyrm");
                markdown.AppendLine();
                markdown.AppendLine("| HP | Side | Spawn | Kill | Goal | TTK P25 | TTK P50 | TTK P75 | Damage P50 | Summons P50 |");
                markdown.AppendLine("| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
                foreach (var candidate in W20Candidates)
                {
                    var report = CoreLoopRhythmDiagnostics.RunDirectBossCalibration(
                        FirstSeed, SampleCount, 20, "DIRECT_W20_" + candidate.ToString("0", CultureInfo.InvariantCulture),
                        worldeaterBossMaxHitPoints: candidate);
                    AppendSummary(markdown, candidate, report, 20);
                    AppendCsv(csv, report.ToCsv());
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
            }

            markdown.AppendLine();
            markdown.AppendLine("## Decision boundary");
            markdown.AppendLine();
            markdown.AppendLine("These are mechanics-envelope measurements only. No HP is promoted until Item + Rune standard/full cohorts, Spellbreaker cohorts, and end-to-end pressure results are available.");
            File.WriteAllText(Path.Combine(logsDirectory, "BossDirectCalibrationV1.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(docsDirectory, "BossDirectCalibrationV1.md"), markdown.ToString());
            Debug.Log("Direct Boss calibration complete.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void AppendSummary(
            StringBuilder markdown,
            float hp,
            JointBalanceCalibrationReport report,
            int wave)
        {
            foreach (var sideName in new[] { "Player", "AI" })
            {
                var bosses = new System.Collections.Generic.List<JointBossCalibrationSample>();
                foreach (var run in report.Runs)
                {
                    var side = sideName == "Player" ? run.Player : run.AI;
                    foreach (var boss in side.Bosses) if (boss.Wave == wave) bosses.Add(boss);
                }

                markdown.Append("| ").Append(hp.ToString("0", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(sideName).Append(" | ").Append(Rate(Count(bosses, boss => boss.Spawned), bosses.Count).ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(Rate(Count(bosses, boss => boss.Killed), bosses.Count).ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(Rate(Count(bosses, boss => boss.ReachedGoal), bosses.Count).ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(PercentileTtk(bosses, .25).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(PercentileTtk(bosses, .50).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(PercentileTtk(bosses, .75).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(PercentileDamage(bosses, .50).ToString("0.00", CultureInfo.InvariantCulture));
                if (wave == 20) markdown.Append(" | ").Append(PercentileSummons(bosses, .50).ToString("0.00", CultureInfo.InvariantCulture));
                markdown.AppendLine(" |");
            }
        }

        private static int Count(System.Collections.Generic.IReadOnlyList<JointBossCalibrationSample> values, Func<JointBossCalibrationSample, bool> predicate)
        {
            var count = 0;
            foreach (var value in values) if (predicate(value)) count++;
            return count;
        }

        private static double PercentileTtk(System.Collections.Generic.IReadOnlyList<JointBossCalibrationSample> values, double percentile)
        {
            var ordered = new System.Collections.Generic.List<float>();
            foreach (var value in values) if (value.Killed) ordered.Add(value.TtkSeconds);
            return Percentile(ordered, percentile);
        }

        private static double PercentileDamage(System.Collections.Generic.IReadOnlyList<JointBossCalibrationSample> values, double percentile)
        {
            var ordered = new System.Collections.Generic.List<float>();
            foreach (var value in values) if (value.Killed) ordered.Add(value.Damage);
            return Percentile(ordered, percentile);
        }

        private static double PercentileSummons(System.Collections.Generic.IReadOnlyList<JointBossCalibrationSample> values, double percentile)
        {
            var ordered = new System.Collections.Generic.List<float>();
            foreach (var value in values) if (value.Spawned) ordered.Add(value.SummonCount);
            return Percentile(ordered, percentile);
        }

        private static double Percentile(System.Collections.Generic.List<float> values, double percentile)
        {
            if (values.Count == 0) return -1d;
            values.Sort();
            var index = (int)Math.Round((values.Count - 1) * percentile, MidpointRounding.AwayFromZero);
            return values[Math.Max(0, Math.Min(values.Count - 1, index))];
        }

        private static double Rate(int numerator, int denominator)
        {
            return denominator == 0 ? 0d : numerator / (double)denominator;
        }

        private static void AppendCsv(StringBuilder destination, string source)
        {
            if (destination.Length == 0)
            {
                destination.Append(source);
                return;
            }

            var firstLineEnd = source.IndexOf('\n');
            destination.Append(firstLineEnd < 0 ? source : source.Substring(firstLineEnd + 1));
        }
    }
}
