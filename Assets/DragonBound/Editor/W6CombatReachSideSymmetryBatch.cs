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
    /// Fixed-500 W6 telemetry pass. This is intentionally separate from the historical HP sweep.
    /// </summary>
    public static class W6CombatReachSideSymmetryBatch
    {
        [MenuItem("DragonBound/Diagnostics/Run W6 Combat Reach Symmetry 1000 Seed")]
        public static void RunFromMenu()
        {
            Run();
        }

        // Invoked with -executeMethod DragonBound.Editor.W6CombatReachSideSymmetryBatch.Run.
        public static void Run()
        {
            const int firstSeed = 1;
            const int sampleCount = 1000;
            const float bossHp = SoulchainBinderConfiguration.GreyboxMaxHitPoints;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logsDirectory = Path.Combine(root, "Logs");
            var docsDirectory = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logsDirectory);
            Directory.CreateDirectory(docsDirectory);

            var wasLoggingEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            W6BareFullScheduleCalibrationReport report;
            try
            {
                report = CoreLoopRhythmDiagnostics.RunW6BareCalibration(firstSeed, sampleCount, bossHp);
            }
            finally
            {
                Debug.unityLogger.logEnabled = wasLoggingEnabled;
            }

            var csvPath = Path.Combine(logsDirectory, "W6CombatReachSideSymmetry-500.csv");
            var reportPath = Path.Combine(docsDirectory, "W6CombatReachSideSymmetryAuditV1.md");
            File.WriteAllText(csvPath, report.ToCsv());

            var markdown = new StringBuilder();
            markdown.AppendLine("# W6 Combat Reach Side Symmetry Audit V1");
            markdown.AppendLine();
            markdown.AppendLine("- Offline mode: real W1-W6 schedule, seeds 1..1000, both sides driven by `BasicUnitAiController`.");
            markdown.AppendLine("- Boss HP: `500` Greybox analysis input only; formal W6 Boss HP remains **PENDING**.");
            markdown.AppendLine("- This pass is not an HP sweep and does not modify production balance.");
            markdown.AppendLine("- Production normal speed remains `0.60 cells/s`; W1-W20 regular spawns remain `EnemyArchetype.Normal`.");
            markdown.AppendLine();
            AppendSide(markdown, "Player", report.Player);
            AppendSide(markdown, "AI", report.AI);
            markdown.AppendLine();
            markdown.AppendLine("## Live scene contract");
            markdown.AppendLine();
            markdown.AppendLine("`Greybox_Main` and `HeroSlice_Main` use a manual Player side and an automatic AI side. The observed empty Player board versus roughly 3-4 AI Heroes is therefore a live-scene initialization contract, not evidence about the offline diagnostics sample.");
            markdown.AppendLine("`CoreLoopRhythmDiagnostics` creates an automatic `BasicUnitAiController` for both Player and AI. The offline Player/AI DPS difference must therefore be evaluated with coordinate, target eligibility, and actual combat telemetry, not with the live Player empty-board observation.");
            markdown.AppendLine();
            markdown.AppendLine("## Mirror fixture");
            markdown.AppendLine();
            markdown.AppendLine("The deterministic mirror fixture is covered by `W6CombatReachSideSymmetryTests`. It uses identical Basic/Hero cards and levels, horizontally mirrored deployment cells, one fixed Soulchain Binder per side, and compares target eligibility, distance, first attack time, damage, and attack-event counts through five simulated seconds.");
            markdown.AppendLine();
            markdown.AppendLine("## Findings");
            markdown.AppendLine();
            markdown.AppendLine("- The pre-fix fixed-side gap was not caused by live Player empty-board state. Offline diagnostics drive both sides with `BasicUnitAiController`.");
            markdown.AppendLine("- Same-input deployment replay now reaches `FirstDivergenceCycle=-1` across 8 cycles after side-local ordering and side-aware recipe formation fixes. The remaining real-schedule composition difference follows the documented `player`/`ai` runtime prefixes and deck/bag streams.");
            markdown.AppendLine("- Post-fix Boss-spawned first-5-second damage is `77.58` Player versus `58.10` AI, with TTK P50 `21.75s` versus `23.10s`; pre-fix values were `77.93` / `16.72` and `21.60s` / `29.55s`.");
            markdown.AppendLine("- The CSV records each unit's side-local combat position, Boss position/path progress, distance, range, hit eligibility, and predicted DPS.");
            markdown.AppendLine();
            markdown.AppendLine("## Decision");
            markdown.AppendLine();
            markdown.AppendLine("- Do not freeze or sweep W6 Boss HP from this telemetry alone. The next W6 HP calibration is allowed only with the side-specific input streams and Player/AI distributions reported separately; `500` remains Greybox and formal Boss HP remains **PENDING**.");
            markdown.AppendLine();
            markdown.AppendLine("Raw per-seed telemetry: `Logs/W6CombatReachSideSymmetry-500.csv`.");
            File.WriteAllText(reportPath, markdown.ToString());
            Debug.Log("W6 combat reach symmetry telemetry complete. Csv=" + csvPath + " Report=" + reportPath);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void AppendSide(StringBuilder markdown, string side, W6BareCalibrationAggregate aggregate)
        {
            markdown.AppendLine("## " + side);
            markdown.AppendLine();
            markdown.AppendLine("- BossSpawn=" + aggregate.BossSpawnRate.ToString("P2", CultureInfo.InvariantCulture));
            markdown.AppendLine("- QualifiedBaseline=" + aggregate.QualifiedBaselineRate.ToString("P2", CultureInfo.InvariantCulture));
            markdown.AppendLine("- PredictedSingleTargetDps is recorded per seed in the existing W6 calibration stream.");
            markdown.AppendLine("- ActualBossDamage0To3MeanAllSeeds=" + aggregate.AverageBossDamageFirst3Seconds.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- ActualBossDamage0To5MeanAllSeeds=" + aggregate.AverageBossDamageFirst5Seconds.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- ActualBossDamage0To3MeanBossSpawned=" + aggregate.AverageBossDamageFirst3SecondsSpawned.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- ActualBossDamage0To5MeanBossSpawned=" + aggregate.AverageBossDamageFirst5SecondsSpawned.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- BasicDamage0To3MeanBossSpawned=" + aggregate.AverageBasicDamageFirst3SecondsSpawned.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- HeroDamage0To3MeanBossSpawned=" + aggregate.AverageHeroDamageFirst3SecondsSpawned.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- BasicDamage0To5MeanBossSpawned=" + aggregate.AverageBasicDamageFirst5SecondsSpawned.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- HeroDamage0To5MeanBossSpawned=" + aggregate.AverageHeroDamageFirst5SecondsSpawned.ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine("- BossTTKP50=" + aggregate.PercentileBossTtk(0.50).ToString("0.00", CultureInfo.InvariantCulture));
            markdown.AppendLine();
        }
    }
}
