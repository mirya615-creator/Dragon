using System.IO;
using System.Text;
using DragonBound.AI;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class AiDeploymentFormationSymmetryBatch
    {
        [MenuItem("DragonBound/Diagnostics/Run AI Deployment Formation Symmetry Replay")]
        public static void RunFromMenu()
        {
            Run();
        }

        // Invoked with -executeMethod DragonBound.Editor.AiDeploymentFormationSymmetryBatch.Run.
        public static void Run()
        {
            var trace = new AiDeploymentFormationSymmetryTrace().Run(1701, 8);
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var logs = Path.Combine(root, "Logs");
            var docs = Path.Combine(root, "Docs");
            Directory.CreateDirectory(logs);
            Directory.CreateDirectory(docs);
            var csvPath = Path.Combine(logs, "AiDeploymentFormationSymmetryReplay.csv");
            var reportPath = Path.Combine(docs, "AiDeploymentFormationSideSymmetryAuditV1.md");
            File.WriteAllText(csvPath, trace.ToCsv());

            var report = new StringBuilder();
            report.AppendLine("# AI Deployment / Formation Side Symmetry Audit V1");
            report.AppendLine();
            report.AppendLine("- Same-input replay uses the same RunSeed, runtime prefix, finite component bag, RecruitBatch sequence, resources, unlock state, and decision cycles on both sides.");
            report.AppendLine("- Both sides are driven by `BasicUnitAiController`; AI-side positions are converted to the Player-local rotational counterpart before comparison.");
            report.AppendLine("- " + trace.FormatReport());
            report.AppendLine();
            report.AppendLine("## Root cause and fix");
            report.AppendLine();
            report.AppendLine("- The original divergence was caused by world-coordinate enumeration in `BasicUnitAiController`: board cells, occupants, merge candidates, parking targets, and recipe candidates were ordered in global coordinates, so the rotationally mirrored side made different tie-break choices.");
            report.AppendLine("- After side-local ordering, `HeroRecipeDefinition` formation rules still ran against AI world coordinates and reversed vertical component orientation. Formation checks and target positions now convert through the side-local rotational transform in `BasicUnitAiController` and `BoardRecruitDestination`.");
            report.AppendLine("- Fixed-board conversion for intermediate recipe coordinates is total (8x10 rotation); normal board validation rejects candidates outside the deployment mask.");
            report.AppendLine("- No attack, range, HP, speed, count, recruit probability, component probability, or hero rule changed.");
            report.AppendLine();
            report.AppendLine("## Production input boundary");
            report.AppendLine();
            report.AppendLine("- Offline `CoreLoopRhythmDiagnostics` is AI-versus-AI, but its production-style streams are intentionally side-specific: runtime prefixes are `player` and `ai`, deck salts differ, and bag seeds differ.");
            report.AppendLine("- `RecruitDeck.DeriveSeed` hashes `runtimePrefix` into each finite-batch stream, so swapping salts does not make inputs identical while the prefixes remain different. Residual composition differences therefore follow the input-stream boundary, not a remaining side-local controller branch.");
            report.AppendLine("- Live `Greybox_Main`/`HeroSlice_Main` remains manual Player versus automatic/preset AI and is not an offline AI-versus-AI fairness sample.");
            report.AppendLine();
            report.AppendLine("## Post-fix fixed-500 verification");
            report.AppendLine();
            report.AppendLine("- One post-fix real W1-W6 run was executed for seeds `1..1000` with Soulchain Binder `500` Greybox HP; this was not an HP sweep.");
            report.AppendLine("- BossSpawn was `76.90%` for both Player and AI.");
            report.AppendLine("- Boss-spawned first-5-second Boss damage was Player `77.58` and AI `58.10` (pre-fix `77.93` / `16.72`).");
            report.AppendLine("- Boss TTK P50 was Player `21.75s` and AI `23.10s` (pre-fix `21.60s` / `29.55s`).");
            report.AppendLine("- Full per-seed telemetry: `Logs/W6CombatReachSideSymmetry-500.csv`; formal W6 Boss HP remains **PENDING**.");
            report.AppendLine();
            report.AppendLine("## Cycle detail");
            report.AppendLine();
            foreach (var cycle in trace.Cycles)
            {
                report.AppendLine(
                    "- Cycle " + cycle.Cycle +
                    ": Symmetric=" + cycle.IsSymmetric +
                    ", PlayerRecipeAttempt/Success=" + cycle.PlayerRecipeAttempts + "/" + cycle.PlayerRecipeSuccesses +
                    ", AIRecipeAttempt/Success=" + cycle.AiRecipeAttempts + "/" + cycle.AiRecipeSuccesses +
                    ", Difference=" + (string.IsNullOrEmpty(cycle.Difference) ? "None" : cycle.Difference));
            }
            report.AppendLine();
            report.AppendLine("## Live scene boundary");
            report.AppendLine();
            report.AppendLine("Live `Greybox_Main`/`HeroSlice_Main` remains manual Player versus automatic AI. This replay is intentionally AI-versus-AI and must not be interpreted as a human-player fairness sample.");
            report.AppendLine();
            report.AppendLine("Raw trace: `Logs/AiDeploymentFormationSymmetryReplay.csv`.");
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log("AI deployment formation symmetry replay complete. Csv=" + csvPath + " Report=" + reportPath);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
