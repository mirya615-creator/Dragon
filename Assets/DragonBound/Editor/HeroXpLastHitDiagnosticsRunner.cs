using System.IO;
using DragonBound.Core;
using DragonBound.Recruitment;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    /// <summary>Development-only runner for the formal V3 Core Loop XP report.</summary>
    public static class HeroXpLastHitDiagnosticsRunner
    {
        public static void Run1000()
        {
            var logDirectory = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logDirectory);
            var progressPath = Path.Combine(logDirectory, "codex-hero-xp-last-hit-1000-progress.txt");
            var reportPath = Path.Combine(logDirectory, "codex-hero-xp-last-hit-1000.txt");
            File.WriteAllText(progressPath, "HERO_XP_LAST_HIT_V1_PROGRESS\n");

            var report = CoreLoopRhythmDiagnostics.Run(
                1,
                1000,
                RecruitComponentPolicy.V3,
                completed =>
                {
                    if (completed % 10 == 0)
                    {
                        File.AppendAllText(progressPath, $"CompletedSeeds={completed}\n");
                    }
                });
            File.WriteAllText(reportPath, report.FormatReport());
            Debug.Log($"Hero XP Last Hit 1000 Seed report written: {reportPath}");
        }
    }
}
