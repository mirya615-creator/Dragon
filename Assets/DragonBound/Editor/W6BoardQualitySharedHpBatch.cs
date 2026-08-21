using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DragonBound.Core;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    /// <summary>
    /// Diagnostic comparison for BoardQuality-driven shared W6 Boss HP. It never changes the
    /// Production default and runs one fixed baseline plus two bounded dynamic schemes.
    /// </summary>
    public static class W6BoardQualitySharedHpBatch
    {
        private const int FirstSeed = 1;
        private const int SampleCount = 1000;

        [MenuItem("DragonBound/Diagnostics/Run W6 Board-Quality Shared HP Comparison 1000 Seed")]
        public static void RunFromMenu()
        {
            Run();
        }

        // Invoked with -executeMethod DragonBound.Editor.W6BoardQualitySharedHpBatch.Run.
        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsDirectory = Path.Combine(root, "Logs");
            var docsDirectory = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var previousLogging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            W6BareFullScheduleCalibrationReport baseline;
            try
            {
                baseline = CoreLoopRhythmDiagnostics.RunW6BareCalibration(FirstSeed, SampleCount, 500f);
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
            }

            var referenceQuality = new float[SampleCount];
            for (var index = 0; index < SampleCount; index++)
            {
                referenceQuality[index] = Math.Max(
                    baseline.Player.Samples[index].BoardQualityAtBossSpawn,
                    baseline.AI.Samples[index].BoardQualityAtBossSpawn);
            }

            var qualifiedReferenceQuality = new List<float>();
            foreach (var value in referenceQuality)
            {
                if (value > 0f) qualifiedReferenceQuality.Add(value);
            }

            qualifiedReferenceQuality.Sort();
            var thresholds = new[]
            {
                (float)Quantile(qualifiedReferenceQuality, 0.25),
                (float)Quantile(qualifiedReferenceQuality, 0.50),
                (float)Quantile(qualifiedReferenceQuality, 0.75)
            };

            var schemeA = new Scheme("Tight", new[] { 450f, 500f, 550f, 600f });
            var schemeB = new Scheme("Broad", new[] { 400f, 500f, 600f, 700f });
            W6BareFullScheduleCalibrationReport dynamicA;
            W6BareFullScheduleCalibrationReport dynamicB;
            try
            {
                dynamicA = CoreLoopRhythmDiagnostics.RunW6BareCalibrationBySeed(
                    FirstSeed,
                    SampleCount,
                    seed => schemeA.GetHp(referenceQuality[seed - FirstSeed], thresholds));
                dynamicB = CoreLoopRhythmDiagnostics.RunW6BareCalibrationBySeed(
                    FirstSeed,
                    SampleCount,
                    seed => schemeB.GetHp(referenceQuality[seed - FirstSeed], thresholds));
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
            }

            var markdown = new StringBuilder();
            var csv = new StringBuilder();
            markdown.AppendLine("# W6 Board-Quality Shared HP V1 Diagnostic");
            markdown.AppendLine();
            markdown.AppendLine("- Fixed 500 baseline plus exactly two dynamic schemes, each using the same Seed set `1..1000`.");
            markdown.AppendLine("- BoardQuality is snapshotted once immediately before W6 Boss generation and uses only deployed Basic units and completed Hero pairs.");
            markdown.AppendLine("- Formula: `sum(Basic Attack*AttackSpeed) + sum(Hero Attack*AttackSpeed)` at current configured level. No range, position, Bench, unpaired Component, temporary effect, resource, Item, or Rune state is included.");
            markdown.AppendLine("- ReferenceQuality is `max(PlayerQuality, AIQuality)`. Equal values deterministically select Player as ReferenceSide for paired diagnostics; both sides always receive the same shared HP.");
            markdown.AppendLine("- Quality thresholds are derived from the fixed-500 Seed distribution, not hand-authored.");
            markdown.AppendLine();
            markdown.AppendLine("## Derived quality distribution");
            markdown.AppendLine();
            markdown.AppendLine("- Qualified ReferenceQuality samples=" + qualifiedReferenceQuality.Count + "/" + SampleCount);
            markdown.AppendLine("- Q25/Q50/Q75=" + Format(thresholds[0]) + "/" + Format(thresholds[1]) + "/" + Format(thresholds[2]));
            markdown.AppendLine("- Tier mapping: T1 < Q25, T2 < Q50, T3 < Q75, T4 >= Q75. HP mapping is monotonic in every scheme.");
            markdown.AppendLine();
            markdown.AppendLine("## Overall comparison");
            markdown.AppendLine();
            markdown.AppendLine("| Scheme | Shared HP rule | Reference TTK P25/P50/P75 | Weak TTK P25/P50/P75 | Reference 20-25 | Weak 20-25 | Reference quality delta | TTK delta |");
            markdown.AppendLine("| :--- | :--- | :--- | :--- | ---: | ---: | ---: | ---: |");
            csv.AppendLine("scheme,tier,side,hp,samples,bossSpawned,bossKilled,bossLeaked,bossAliveAtW7,earlyEndBeforeBoss,ttkKillSamples,ttkP25,ttkP50,ttkP75,window20To25,avgBoardQuality,avgReferenceQualityDelta,avgTtkDelta");

            AppendComparison(markdown, csv, "Fixed500", baseline, baseline, null, referenceQuality, thresholds, "500/500/500/500");
            AppendComparison(markdown, csv, schemeA.Name, dynamicA, baseline, schemeA, referenceQuality, thresholds, "450/500/550/600");
            AppendComparison(markdown, csv, schemeB.Name, dynamicB, baseline, schemeB, referenceQuality, thresholds, "400/500/600/700");

            markdown.AppendLine();
            markdown.AppendLine("## Decision");
            markdown.AppendLine();
            markdown.AppendLine("Dynamic schemes are diagnostic only. Production promotion requires a clear improvement over Fixed500 in ReferenceSide P50 concentration and 20-25s hit rate without collapsing weak-side variance.");
            markdown.AppendLine("Formal Production W6 HP remains **PENDING** until this comparison is reviewed.");
            markdown.AppendLine();
            markdown.AppendLine("Raw metrics: `Logs/W6BoardQualitySharedHpComparison.csv`.");

            File.WriteAllText(Path.Combine(docsDirectory, "W6BoardQualitySharedHpV1.md"), markdown.ToString());
            File.WriteAllText(Path.Combine(logsDirectory, "W6BoardQualitySharedHpComparison.csv"), csv.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void AppendComparison(
            StringBuilder markdown,
            StringBuilder csv,
            string name,
            W6BareFullScheduleCalibrationReport report,
            W6BareFullScheduleCalibrationReport baseline,
            Scheme scheme,
            float[] referenceQuality,
            float[] thresholds,
            string hpRule)
        {
            var reference = new List<W6BareCalibrationSideRun>();
            var weak = new List<W6BareCalibrationSideRun>();
            var referenceQualityDelta = new List<float>();
            var ttkDelta = new List<float>();
            var referenceWindow = 0;
            var weakWindow = 0;
            for (var index = 0; index < SampleCount; index++)
            {
                var playerQuality = baseline.Player.Samples[index].BoardQualityAtBossSpawn;
                var aiQuality = baseline.AI.Samples[index].BoardQualityAtBossSpawn;
                var playerIsReference = playerQuality >= aiQuality;
                var referenceSample = playerIsReference ? report.Player.Samples[index] : report.AI.Samples[index];
                var weakSample = playerIsReference ? report.AI.Samples[index] : report.Player.Samples[index];
                reference.Add(referenceSample);
                weak.Add(weakSample);
                referenceQualityDelta.Add(Math.Abs(playerQuality - aiQuality));
                if (referenceSample.BossKilled && referenceSample.BossTtkSeconds >= 20f && referenceSample.BossTtkSeconds <= 25f) referenceWindow++;
                if (weakSample.BossKilled && weakSample.BossTtkSeconds >= 20f && weakSample.BossTtkSeconds <= 25f) weakWindow++;
                if (referenceSample.BossKilled && weakSample.BossKilled)
                {
                    ttkDelta.Add(weakSample.BossTtkSeconds - referenceSample.BossTtkSeconds);
                }
            }

            var referenceMetrics = BuildMetrics(reference);
            var weakMetrics = BuildMetrics(weak);
            markdown.AppendLine("| " + name + " | " + hpRule + " | " + referenceMetrics.TtkSummary + " | " + weakMetrics.TtkSummary + " | " + Rate(referenceWindow, referenceMetrics.KillSamples) + " | " + Rate(weakWindow, weakMetrics.KillSamples) + " | " + Format(Average(referenceQualityDelta)) + " | " + Format(Average(ttkDelta)) + " |");
            markdown.AppendLine();
            markdown.AppendLine("### " + name + " per tier");
            markdown.AppendLine();
            markdown.AppendLine("| Tier | HP | Samples | Reference Kill | Weak Kill | Reference P25/P50/P75 | Weak P25/P50/P75 | Reference 20-25 | Weak 20-25 | Early End |");
            markdown.AppendLine("| :--- | ---: | ---: | ---: | ---: | :--- | :--- | ---: | ---: | ---: |");
            for (var tier = 0; tier < 4; tier++)
            {
                var refTier = new List<W6BareCalibrationSideRun>();
                var weakTier = new List<W6BareCalibrationSideRun>();
                var refWindow = 0;
                var weakTierWindow = 0;
                var earlyEnd = 0;
                for (var index = 0; index < SampleCount; index++)
                {
                    if (GetTier(referenceQuality[index], thresholds) != tier) continue;
                    var playerQuality = baseline.Player.Samples[index].BoardQualityAtBossSpawn;
                    var playerIsReference = playerQuality >= baseline.AI.Samples[index].BoardQualityAtBossSpawn;
                    var refSample = playerIsReference ? report.Player.Samples[index] : report.AI.Samples[index];
                    var weakSample = playerIsReference ? report.AI.Samples[index] : report.Player.Samples[index];
                    refTier.Add(refSample);
                    weakTier.Add(weakSample);
                    if (refSample.BossKilled && refSample.BossTtkSeconds >= 20f && refSample.BossTtkSeconds <= 25f) refWindow++;
                    if (weakSample.BossKilled && weakSample.BossTtkSeconds >= 20f && weakSample.BossTtkSeconds <= 25f) weakTierWindow++;
                    if (!refSample.BossSpawned) earlyEnd++;
                }

                var refTierMetrics = BuildMetrics(refTier);
                var weakTierMetrics = BuildMetrics(weakTier);
                var hp = scheme == null ? 500f : scheme.HitPoints[tier];
                markdown.AppendLine("| T" + (tier + 1) + " | " + Format(hp) + " | " + refTier.Count + " | " + Rate(refTierMetrics.Kills, refTier.Count) + " | " + Rate(weakTierMetrics.Kills, weakTier.Count) + " | " + refTierMetrics.TtkSummary + " | " + weakTierMetrics.TtkSummary + " | " + Rate(refWindow, refTierMetrics.KillSamples) + " | " + Rate(weakTierWindow, weakTierMetrics.KillSamples) + " | " + earlyEnd + " |");
                AppendCsv(csv, name, tier, hp, refTier, referenceQuality, thresholds, baseline, report);
                AppendCsv(csv, name, tier, hp, weakTier, referenceQuality, thresholds, baseline, report, false);
            }
        }

        private static void AppendCsv(StringBuilder csv, string scheme, int tier, float hp, List<W6BareCalibrationSideRun> tierSamples, float[] referenceQuality, float[] thresholds, W6BareFullScheduleCalibrationReport baseline, W6BareFullScheduleCalibrationReport report, bool referenceSide = true)
        {
            var metrics = BuildMetrics(tierSamples);
            var side = referenceSide ? "Reference" : "Weak";
            var quality = tierSamples.Count == 0 ? 0d : Average(tierSamples, sample => sample.BoardQualityAtBossSpawn);
            csv.Append(scheme).Append(',').Append("T").Append(tier + 1).Append(',').Append(side).Append(',').Append(Format(hp)).Append(',').Append(tierSamples.Count).Append(',')
                .Append(metrics.Spawned).Append(',').Append(metrics.Kills).Append(',').Append(metrics.Leaked).Append(',').Append(metrics.AliveAtW7).Append(',').Append(metrics.EarlyEnd).Append(',')
                .Append(metrics.KillSamples).Append(',').Append(Format(metrics.P25)).Append(',').Append(Format(metrics.P50)).Append(',').Append(Format(metrics.P75)).Append(',')
                .Append(Rate(metrics.Window20To25, metrics.KillSamples)).Append(',').Append(Format(quality)).AppendLine(",0.00,0.00");
        }

        private static Metrics BuildMetrics(IReadOnlyList<W6BareCalibrationSideRun> samples)
        {
            var kills = 0;
            var spawned = 0;
            var leaked = 0;
            var alive = 0;
            var early = 0;
            var window = 0;
            var ttks = new List<double>();
            foreach (var sample in samples)
            {
                if (sample.BossSpawned) spawned++;
                else early++;
                if (sample.BossKilled)
                {
                    kills++;
                    ttks.Add(sample.BossTtkSeconds);
                    if (sample.BossTtkSeconds >= 20f && sample.BossTtkSeconds <= 25f) window++;
                }
                if (sample.BossLeaked) leaked++;
                if (sample.BossAliveAtW7Start) alive++;
            }

            return new Metrics(kills, spawned, leaked, alive, early, window, ttks);
        }

        private static int GetTier(float quality, float[] thresholds)
        {
            if (quality < thresholds[0]) return 0;
            if (quality < thresholds[1]) return 1;
            if (quality < thresholds[2]) return 2;
            return 3;
        }

        private static double Quantile(List<float> values, double percentile)
        {
            if (values.Count == 0) return 0d;
            var position = (values.Count - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            return lower == upper ? values[lower] : values[lower] + (values[upper] - values[lower]) * (position - lower);
        }

        private static double Average(IReadOnlyList<float> values)
        {
            if (values.Count == 0) return 0d;
            double total = 0d;
            foreach (var value in values) total += value;
            return total / values.Count;
        }

        private static double Average(IReadOnlyList<W6BareCalibrationSideRun> values, Func<W6BareCalibrationSideRun, float> selector)
        {
            if (values.Count == 0) return 0d;
            double total = 0d;
            foreach (var value in values) total += selector(value);
            return total / values.Count;
        }

        private static string Rate(int numerator, int denominator)
        {
            return (denominator == 0 ? 0d : numerator / (double)denominator).ToString("P2", CultureInfo.InvariantCulture);
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private sealed class Scheme
        {
            public Scheme(string name, float[] hitPoints)
            {
                Name = name;
                HitPoints = hitPoints;
            }

            public string Name { get; }
            public float[] HitPoints { get; }

            public float GetHp(float quality, float[] thresholds)
            {
                return HitPoints[GetTier(quality, thresholds)];
            }
        }

        private sealed class Metrics
        {
            public Metrics(int kills, int spawned, int leaked, int aliveAtW7, int earlyEnd, int window20To25, List<double> ttks)
            {
                Kills = kills;
                Spawned = spawned;
                Leaked = leaked;
                AliveAtW7 = aliveAtW7;
                EarlyEnd = earlyEnd;
                Window20To25 = window20To25;
                KillSamples = ttks.Count;
                P25 = Percentile(ttks, 0.25);
                P50 = Percentile(ttks, 0.50);
                P75 = Percentile(ttks, 0.75);
                TtkSummary = Format(P25) + "/" + Format(P50) + "/" + Format(P75) + "s";
            }

            public int Kills { get; }
            public int Spawned { get; }
            public int Leaked { get; }
            public int AliveAtW7 { get; }
            public int EarlyEnd { get; }
            public int Window20To25 { get; }
            public int KillSamples { get; }
            public double P25 { get; }
            public double P50 { get; }
            public double P75 { get; }
            public string TtkSummary { get; }

            private static double Percentile(List<double> values, double percentile)
            {
                if (values.Count == 0) return 0d;
                values.Sort();
                var position = (values.Count - 1) * percentile;
                var lower = (int)Math.Floor(position);
                var upper = (int)Math.Ceiling(position);
                return lower == upper ? values[lower] : values[lower] + (values[upper] - values[lower]) * (position - lower);
            }
        }
    }
}
