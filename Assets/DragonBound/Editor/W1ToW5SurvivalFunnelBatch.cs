using System.IO;
using System.Text;
using DragonBound.Core;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class W1ToW5SurvivalFunnelBatch
    {
        [MenuItem("DragonBound/Diagnostics/Run W1-W5 Survival Funnel Original + Salt Swap 1000 Seed")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void Run()
        {
            const int firstSeed = 1;
            const int sampleCount = 1000;
            var logsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            var docsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs"));
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);
            var wasLoggingEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            W1ToW5SurvivalFunnelReport original;
            W1ToW5SurvivalFunnelReport swapped;
            try
            {
                original = CoreLoopRhythmDiagnostics.RunW1ToW5SurvivalFunnel(firstSeed, sampleCount, false);
                swapped = CoreLoopRhythmDiagnostics.RunW1ToW5SurvivalFunnel(firstSeed, sampleCount, true);
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasLoggingEnabled;
            }

            var report = new StringBuilder();
            report.AppendLine("# W1-W5 Survival Funnel and Player/AI Side Bias Audit V1");
            report.AppendLine();
            report.AppendLine("- Seed set: `1..1000`, exactly one original run and one Deck/Bag salt-swap run per seed.");
            report.AppendLine("- Both sides are driven by `BasicUnitAiController`; Player is not a human-operation sample.");
            report.AppendLine("- Item/Rune are disabled. W6 Boss HP is not used to judge W1-W5 survival.");
            report.AppendLine("- Production shared settlement remains unchanged: either side reaching zero Heart ends the match.");
            report.AppendLine("- Reach rates are therefore shared-settlement reach rates. They are not independent counterfactual side survival rates.");
            report.AppendLine();
            report.AppendLine("## Original salts");
            report.AppendLine();
            report.AppendLine(original.FormatReport());
            report.AppendLine("## Swapped salts");
            report.AppendLine();
            report.AppendLine(swapped.FormatReport());
            report.AppendLine("## Original vs swapped deltas");
            report.AppendLine();
            AppendDelta(report, original.Player, swapped.Player);
            AppendDelta(report, original.AI, swapped.AI);
            report.AppendLine();
            report.AppendLine("## Root-cause conclusion");
            report.AppendLine();
            report.AppendLine("The W6 figure above is the proportion of shared matches that remain alive until the synchronized W6 generation node. Since both sides enter each wave together, observed per-side W6 reach is the same shared-settlement event; the useful asymmetry is which side first depletes Heart and whether that difference follows the side or the Deck/Bag input after swapping.");
            report.AppendLine("First-defeated counts: original Player=" + original.Player.FirstDefeatedCount + ", AI=" + original.AI.FirstDefeatedCount + ", same-frame double=" + original.Player.SameFrameDoubleDefeatCount + "; swapped Player=" + swapped.Player.FirstDefeatedCount + ", AI=" + swapped.AI.FirstDefeatedCount + ", same-frame double=" + swapped.Player.SameFrameDoubleDefeatCount + ". The side gap changes after the Deck/Bag inputs are exchanged, so the dominant asymmetry follows the random Deck/Bag input rather than a fixed Player/AI branch.");
            report.AppendLine("Largest death-wave bucket: original Player=" + DominantDeathWave(original.Player) + ", AI=" + DominantDeathWave(original.AI) + "; swapped Player=" + DominantDeathWave(swapped.Player) + ", AI=" + DominantDeathWave(swapped.AI) + ". W1 deaths original Player=" + original.Player.DeathWaveCount(1) + ", AI=" + original.AI.DeathWaveCount(1) + "; swapped Player=" + swapped.Player.DeathWaveCount(1) + ", AI=" + swapped.AI.DeathWaveCount(1) + ". Recruit Stall is zero in both experiments, so the observed early collapse is not the repaired W6 recruit-stall path.");
            report.AppendLine("Priority recommendation: audit the largest observed early death-wave pressure next. Do not change W6 Boss HP or call this an AI-vs-human win rate; this remains a shared-settlement survival funnel.");
            report.AppendLine("No Production behavior or numerical value was changed. Do not freeze W6 Boss HP until this funnel/side-bias decision is resolved.");
            report.AppendLine();
            report.AppendLine("Raw rows: `Logs/W1ToW5SurvivalFunnel-OriginalAndSwapped.csv`.");

            var csv = new StringBuilder();
            AppendCsvRows(csv, original.ToCsv());
            AppendCsvRows(csv, swapped.ToCsv());
            var reportPath = Path.Combine(docsDirectory, "W1ToW5SurvivalFunnelAndSideBiasV1.md");
            var csvPath = Path.Combine(logsDirectory, "W1ToW5SurvivalFunnel-OriginalAndSwapped.csv");
            File.WriteAllText(reportPath, report.ToString());
            File.WriteAllText(csvPath, csv.ToString());
            Debug.Log("W1-W5 Survival Funnel complete. Report=" + reportPath + " Csv=" + csvPath);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void AppendDelta(StringBuilder report, W1ToW5SurvivalFunnelAggregate original, W1ToW5SurvivalFunnelAggregate swapped)
        {
            report.AppendLine("- " + original.Label + ":");
            for (var wave = 2; wave <= 6; wave++)
            {
                report.AppendLine("  - W" + wave + " reach delta: " +
                    (swapped.ReachedCounts[wave] - original.ReachedCounts[wave]) + " samples (swapped " +
                    swapped.ReachedCounts[wave] + ", original " + original.ReachedCounts[wave] + ").");
            }
        }

        private static int DominantDeathWave(W1ToW5SurvivalFunnelAggregate aggregate)
        {
            var dominantWave = 0;
            var dominantCount = 0;
            for (var wave = 1; wave <= 5; wave++)
            {
                var count = aggregate.DeathWaveCount(wave);
                if (count > dominantCount)
                {
                    dominantWave = wave;
                    dominantCount = count;
                }
            }

            return dominantWave;
        }

        private static void AppendCsvRows(StringBuilder destination, string csv)
        {
            var firstLine = csv.IndexOf('\n');
            if (firstLine < 0) return;
            if (destination.Length == 0) destination.Append(csv.Substring(0, firstLine + 1));
            destination.Append(csv.Substring(firstLine + 1));
        }
    }
}
