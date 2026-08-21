using System;
using System.Globalization;
using System.IO;
using System.Text;
using DragonBound.Core;
using DragonBound.Items;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    /// <summary>
    /// First executable slice of the joint balance matrix. It combines the existing W6 bare
    /// and W12 item-envelope diagnostics without changing any Production value.
    /// </summary>
    public static class JointBalanceCalibrationBatch
    {
        private const int FirstSeed = 1;
        private const int SampleCount = 50;
        private static readonly float[] W12Candidates = { 1100f, 1200f, 1300f };

        [MenuItem("DragonBound/Diagnostics/Run Joint Balance Calibration Smoke")]
        public static void RunFromMenu()
        {
            Run();
        }

        [MenuItem("DragonBound/Diagnostics/Run Joint Balance Calibration Formal 1000 Seed")]
        public static void RunFormalFromMenu()
        {
            RunFormal();
        }

        // Invoked with -executeMethod DragonBound.Editor.JointBalanceCalibrationBatch.Run.
        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsDirectory = Path.Combine(root, "Logs");
            var docsDirectory = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var csv = new StringBuilder();
            var report = new StringBuilder();
            report.AppendLine("# Joint Item + Rune + Boss Balance Calibration Smoke V1");
            report.AppendLine();
            report.AppendLine("- Seed set: `1..50`.");
            report.AppendLine("- Full pressure: real W1-W20 `BARE + AI_V0` diagnostic with W6/W12/W16/W20 candidate HP `600/1200/2400/5000`.");
            report.AppendLine("- W12 Item fixture: existing two-item diagnostic; Rune snapshot remains empty because no authoritative standard Rune build exists.");
            report.AppendLine("- No Production HP or gameplay rule is written by this batch.");
            report.AppendLine();

            var previousLogging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                var full = CoreLoopRhythmDiagnostics.RunJointBalanceCalibration(
                    FirstSeed,
                    SampleCount,
                    "BARE",
                    null,
                    600f,
                    1200f,
                    2400f,
                    5000f);
                report.AppendLine("## Full W1-W20 BARE / AI_V0");
                report.AppendLine();
                report.AppendLine(full.FormatSummary());
                File.WriteAllText(Path.Combine(logsDirectory, "JointBalanceCalibrationSmoke-Full.csv"), full.ToCsv());

                var w6 = CoreLoopRhythmDiagnostics.RunW6BareCalibration(FirstSeed, SampleCount, 600f);
                report.AppendLine("## W6 BARE / AI_V0");
                report.AppendLine();
                AppendW6(report, "Player", w6.Player);
                AppendW6(report, "AI", w6.AI);
                report.AppendLine();
                report.AppendLine("## W12 STANDARD fixture / AI_V0");
                report.AppendLine();
                report.AppendLine("| HP | Side | Spawn | Kill | Goal | TTK n | P50 | Window32-36 | Item activations | W13 residual |");
                report.AppendLine("| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");

                foreach (var candidate in W12Candidates)
                {
                    var w12 = CoreLoopRhythmDiagnostics.RunW12BuildEnvelopeCalibration(
                        FirstSeed,
                        SampleCount,
                        candidate,
                        _ => new ExistingItemFixture());
                    AppendW12(report, candidate, "Player", w12.Player);
                    AppendW12(report, candidate, "AI", w12.AI);
                    AppendCsv(csv, w12.ToCsv());
                }

                report.AppendLine();
                report.AppendLine("## Scope boundary");
                report.AppendLine();
                report.AppendLine("This smoke result is a baseline artifact, not a Production promote. STANDARD/FULL Rune builds, AI levels 1-3 and loss-streak downgrades remain separate implementation work.");

                var w6Csv = w6.ToCsv();
                File.WriteAllText(Path.Combine(logsDirectory, "JointBalanceCalibrationSmoke-W6.csv"), w6Csv);
                File.WriteAllText(Path.Combine(logsDirectory, "JointBalanceCalibrationSmoke-W12.csv"), csv.ToString());
                File.WriteAllText(Path.Combine(docsDirectory, "JointBalanceCalibrationSmokeV1.md"), report.ToString());
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
            }

            Debug.Log("Joint balance calibration smoke complete.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // Invoked with -executeMethod DragonBound.Editor.JointBalanceCalibrationBatch.RunFormal.
        public static void RunFormal()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsDirectory = Path.Combine(root, "Logs");
            var docsDirectory = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var previousLogging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            JointBalanceCalibrationReport calibration;
            try
            {
                calibration = CoreLoopRhythmDiagnostics.RunJointBalanceCalibration(
                    FirstSeed,
                    1000,
                    "BARE_FORMAL",
                    null,
                    600f,
                    1200f,
                    2400f,
                    5000f,
                    completed =>
                    {
                        if (completed % 100 == 0) Debug.Log("Joint balance formal completed seeds=" + completed);
                    });
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
            }

            var markdown = new StringBuilder();
            markdown.AppendLine("# Joint Item + Rune + Boss Balance Calibration Formal V1");
            markdown.AppendLine();
            markdown.AppendLine("- Seed set: `1..1000`; every original early failure remains in the denominator.");
            markdown.AppendLine("- Build: `BARE_FORMAL + AI_V0`; Item and Rune are disabled because no authoritative standard Rune build exists in the client diagnostic API.");
            markdown.AppendLine("- Candidate Boss HP: W6/W12/W16/W20 = `600/1200/2400/5000`.");
            markdown.AppendLine("- This report is a pressure baseline, not a Production HP promote.");
            markdown.AppendLine();
            markdown.AppendLine(calibration.FormatSummary());
            markdown.AppendLine();
            markdown.AppendLine("## Interpretation");
            markdown.AppendLine();
            markdown.AppendLine("W16/W20 Boss TTK percentiles use only actually spawned and killed samples. A low spawn rate is retained as pressure evidence and is not replaced by direct-Boss rerolls.");
            markdown.AppendLine("Item/Rune STANDARD and FULL cohorts remain pending until an authoritative Rune build fixture is provided by the owning configuration boundary.");

            File.WriteAllText(Path.Combine(logsDirectory, "JointBalanceCalibrationFormalV1.csv"), calibration.ToCsv());
            File.WriteAllText(Path.Combine(docsDirectory, "JointBalanceCalibrationFormalV1.md"), markdown.ToString());
            Debug.Log("Joint balance formal calibration complete.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void AppendW6(StringBuilder report, string side, W6BareCalibrationAggregate aggregate)
        {
            report.Append("- ").Append(side).Append(": spawn=")
                .Append(aggregate.QualifiedBaselineRate.ToString("P2", CultureInfo.InvariantCulture))
                .Append(", kill=").Append(aggregate.BossKillRate.ToString("P2", CultureInfo.InvariantCulture))
                .Append(", leak=").Append(aggregate.BossLeakRate.ToString("P2", CultureInfo.InvariantCulture))
                .Append(", TTK P50=").Append(aggregate.PercentileBossTtk(0.50).ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine("s.");
        }

        private static void AppendW12(
            StringBuilder report,
            float hp,
            string side,
            W12BuildEnvelopeCalibrationAggregate aggregate)
        {
            report.Append("| ").Append(hp.ToString("0", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(side).Append(" | ").Append(aggregate.BossSpawnRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.BossKillRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.BossGoalRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.TtkSampleCount).Append(" | ")
                .Append(aggregate.PercentileBossTtk(0.50).ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.Window32To36Rate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.AverageItemActivations.ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.AverageW13Residual.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine(" |");
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

        private sealed class ExistingItemFixture : IItemRunSnapshotProvider
        {
            public bool TryGetValidatedSnapshots(
                out ItemRunSnapshot playerSnapshot,
                out ItemRunSnapshot aiSnapshot,
                out string reason)
            {
                playerSnapshot = CreateSnapshot(out reason);
                aiSnapshot = playerSnapshot == null ? null : CreateSnapshot(out reason);
                return playerSnapshot != null && aiSnapshot != null;
            }

            private static ItemRunSnapshot CreateSnapshot(out string reason)
            {
                var profile = new ItemProfile();
                profile.RefreshDay(new FixedDayKeyProvider(), out reason);
                profile.RefreshAuthoritativeAccountProgress(new FixedProgressProvider(), out reason);
                if (!profile.Inventory.TryGrantOwned(ItemIds.DrakeheartRelic) ||
                    !profile.Inventory.TryGrantOwned(ItemIds.WinterveilRune) ||
                    !profile.Loadout.TryEquip(ItemIds.DrakeheartRelic, profile.Inventory, out reason) ||
                    !profile.Loadout.TryEquip(ItemIds.WinterveilRune, profile.Inventory, out reason) ||
                    !profile.TryCreateRunSnapshot(out var snapshot, out reason))
                {
                    return null;
                }

                return snapshot;
            }
        }

        private sealed class FixedDayKeyProvider : IItemDayKeyProvider
        {
            public string GetDayKey() => "JOINT_CALIBRATION_DAY_2026_08_18";
        }

        private sealed class FixedProgressProvider : IItemAccountProgressProvider
        {
            public bool TryGetNormalCompletedMatchCount(out int completedMatchCount)
            {
                completedMatchCount = ItemProfile.UnlockCompletedMatchCount;
                return true;
            }
        }
    }
}
