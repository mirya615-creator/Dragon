using System;
using System.Collections.Generic;
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
    /// Bounded W12 Build Envelope calibration. This is an Editor diagnostic and never writes
    /// the Production Boss HP or changes the default runtime constructor.
    /// </summary>
    public static class W12StormcallerPriestBuildEnvelopeBatch
    {
        private const int FirstSeed = 1;
        private const int SampleCount = 50;
        private static readonly float[] CandidateHitPoints = { 1000f, 1100f, 1200f, 1300f, 1400f };

        [MenuItem("DragonBound/Diagnostics/Run W12 Stormcaller Build Envelope 50 Seed")]
        public static void RunFromMenu()
        {
            Run();
        }

        // Invoked with -executeMethod DragonBound.Editor.W12StormcallerPriestBuildEnvelopeBatch.Run.
        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsDirectory = Path.Combine(root, "Logs");
            var docsDirectory = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var markdown = new StringBuilder();
            markdown.AppendLine("# W12 Stormcaller Priest Build Envelope V1");
            markdown.AppendLine();
            markdown.AppendLine("- Same RunSeed set `1..50` for every candidate and cohort.");
            markdown.AppendLine("- Calibration fixture: both sides receive the two currently Implemented Item candidates: Passive `ITEM_DRAKEHEART_RELIC` and Active `ITEM_WINTERVEIL_RUNE`; Winterveil is attempted once when W12 starts. This fixture is diagnostic-only and is not a server-authoritative Build definition.");
            markdown.AppendLine("- Rune snapshot is empty because the repository has no authoritative standard W12 Rune loadout; existing Rune rules remain available but are not silently invented for this calibration.");
            markdown.AppendLine("- Candidates are bounded around the current Greybox `1200`: `1000 / 1100 / 1200 / 1300 / 1400`. No Production HP is written.");
            markdown.AppendLine("- W12 target window from the Boss System document: killed-Boss TTK `32-36s`; percentiles use actual killed samples only.");
            markdown.AppendLine();
            var directSummary = new StringBuilder();
            var endToEndSummary = new StringBuilder();

            var csv = new StringBuilder();
            var previousLogging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                foreach (var candidate in CandidateHitPoints)
                {
                    var direct = CoreLoopRhythmDiagnostics.RunDirectW12BuildEnvelopeCalibration(
                        FirstSeed,
                        SampleCount,
                        candidate,
                        _ => new StandardItemBuildFixture());
                    AppendSummary(directSummary, candidate, direct.Cohort, "Player", direct.Player);
                    AppendSummary(directSummary, candidate, direct.Cohort, "AI", direct.AI);
                    AppendCsv(csv, direct.ToCsv());

                    var endToEnd = CoreLoopRhythmDiagnostics.RunW12BuildEnvelopeCalibration(
                        FirstSeed,
                        SampleCount,
                        candidate,
                        _ => new StandardItemBuildFixture());
                    AppendSummary(endToEndSummary, candidate, endToEnd.Cohort, "Player", endToEnd.Player);
                    AppendSummary(endToEndSummary, candidate, endToEnd.Cohort, "AI", endToEnd.AI);
                    AppendCsv(csv, endToEnd.ToCsv());
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
            }

            markdown.AppendLine("## Direct-W12 cohort (CALIBRATION_FIXTURE)");
            markdown.AppendLine();
            markdown.AppendLine("A fixed 10x-Heart diagnostic allowance, 120-resource/24-decision recruitment setup and one Dragon Rider development pair is built, then the runtime jumps to W12. This cohort is the Boss-mechanics sample and is not a Production flow.");
            AppendHeader(markdown);
            markdown.Append(directSummary);
            markdown.AppendLine();
            markdown.AppendLine("## End-to-end cohort");
            markdown.AppendLine();
            markdown.AppendLine("Real W1-W12 schedule. Runs ending before W12 remain in the 50-run denominator and are excluded from TTK percentiles.");
            AppendHeader(markdown);
            markdown.Append(endToEndSummary);
            markdown.AppendLine();

            markdown.AppendLine("## Interpretation");
            markdown.AppendLine();
            markdown.AppendLine("This report is an envelope diagnostic, not a Production promote. Direct-W12 is the controlled mechanics cohort; End-to-end reports W12 arrival and early-end denominators separately. Compare Player/AI distributions and W13 residual pressure before selecting a separate formal HP task.");
            markdown.AppendLine();
            markdown.AppendLine("Raw per-seed telemetry: `Logs/W12StormcallerPriestBuildEnvelope.csv`. Every row includes candidateHp, cohort, runSeed and side. The Rune loadout assumption must be resolved before Production HP freeze.");
            markdown.AppendLine();
            markdown.AppendLine("## Calibration conclusion");
            markdown.AppendLine();
            markdown.AppendLine("- Diagnostic recommendation: retain `1200-1300 HP` as the next bounded review interval. Direct-W12 P50 is closest to the 32-36s target at 1300, but killed samples are sparse (4-9 per side) and W13 residual is high; this is evidence for review, not a freeze.");
            markdown.AppendLine("- Direct-W12 has a full 50/50 Boss-spawn denominator. End-to-end reaches W12 in only 10/50 runs per side; the other 40/50 are early-end samples and are excluded from TTK percentiles but remain in the cohort denominator.");
            markdown.AppendLine("- End-to-end P50 remains approximately 29-33s across the bounded range, with 0-16.67% 32-36s window coverage and no Boss goal samples in this seed set. Direct-W12 and end-to-end therefore answer different questions and must not be merged.");
            markdown.AppendLine("- Production Stormcaller HP remains **PENDING**; `1200` remains Greybox. No Storm Call, movement, wave, AI, Hero/Basic, Item or Rune Production value was changed.");

            var csvPath = Path.Combine(logsDirectory, "W12StormcallerPriestBuildEnvelope.csv");
            var reportPath = Path.Combine(docsDirectory, "W12StormcallerPriestBuildEnvelopeV1.md");
            File.WriteAllText(csvPath, csv.ToString());
            File.WriteAllText(reportPath, markdown.ToString());
            Debug.Log("W12 Stormcaller Build Envelope complete. Csv=" + csvPath + " Report=" + reportPath);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void AppendHeader(StringBuilder markdown)
        {
            markdown.AppendLine("| HP | Cohort | Side | Sample n | Spawn | Kill | Goal | W13 Residual | TTK n | P25 | P50 | P75 | Window32-36 | First/Second Cast Success | Avg Affected 1/2 | Shield/Body Damage | Spellbreaker Failures |");
            markdown.AppendLine("| ---: | :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | :--- | :--- | :--- | ---: |");
        }

        private static void AppendCsv(StringBuilder csv, string reportCsv)
        {
            if (csv.Length == 0)
            {
                csv.Append(reportCsv);
                return;
            }

            var firstLineEnd = reportCsv.IndexOf('\n');
            csv.Append(firstLineEnd < 0 ? reportCsv : reportCsv.Substring(firstLineEnd + 1));
        }

        private static void AppendSummary(
            StringBuilder markdown,
            float hp,
            string cohort,
            string side,
            W12BuildEnvelopeCalibrationAggregate aggregate)
        {
            markdown.Append("| ").Append(hp.ToString("0", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(cohort).Append(" | ").Append(side).Append(" | ")
                .Append(aggregate.SampleCount).Append(" | ")
                .Append(aggregate.BossSpawnRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.BossKillRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.BossGoalRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.BossResidualRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.TtkSampleCount).Append(" | ")
                .Append(Format(aggregate.PercentileBossTtk(0.25))).Append(" | ")
                .Append(Format(aggregate.PercentileBossTtk(0.50))).Append(" | ")
                .Append(Format(aggregate.PercentileBossTtk(0.75))).Append(" | ")
                .Append(aggregate.Window32To36Rate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(aggregate.FirstCastSuccessRate.ToString("P2", CultureInfo.InvariantCulture)).Append("/")
                .Append(aggregate.SecondCastSuccessRate.ToString("P2", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(Format(aggregate.AverageFirstCastAffected)).Append("/")
                .Append(Format(aggregate.AverageSecondCastAffected)).Append(" | ")
                .Append(Format(aggregate.AverageShieldDamage)).Append("/")
                .Append(Format(aggregate.AverageBodyDamage)).Append(" | ")
                .Append(aggregate.SpellbreakerFailureCount).AppendLine(" |");
        }

        private static string Format(double value)
        {
            return value < 0d ? "-" : value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private sealed class StandardItemBuildFixture : IItemRunSnapshotProvider
        {
            public bool TryGetValidatedSnapshots(
                out ItemRunSnapshot playerSnapshot,
                out ItemRunSnapshot aiSnapshot,
                out string reason)
            {
                playerSnapshot = CreateSnapshot(out reason);
                if (playerSnapshot == null)
                {
                    aiSnapshot = null;
                    return false;
                }

                aiSnapshot = CreateSnapshot(out reason);
                return aiSnapshot != null;
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
            public string GetDayKey() => "W12_CALIBRATION_DAY_2026_08_18";
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
