using System.IO;
using DragonBound.Core;
using DragonBound.Recruitment;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class BareRunMatchEndAuditBatch
    {
        public static void Run()
        {
            var wasLoggingEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            var candidates = new[]
            {
                EnemyHpCurveCandidate.LargeScaleModerate,
                EnemyHpCurveCandidate.W5W6MildRelief,
                EnemyHpCurveCandidate.W5W6StrongRelief
            };
            var builder = new System.Text.StringBuilder();
            foreach (var candidate in candidates)
            {
                builder.AppendLine("=== " + DisplayName(candidate) + " ===");
                var report = CoreLoopRhythmDiagnostics.Run(1, 1000, RecruitComponentPolicy.V3, candidate);
                builder.AppendLine(report.FormatReport());
            }

            var path = Path.GetFullPath(Path.Combine("Logs", "codex-small-enemy-pressure-v2-2-w5-w6-relief-sweep-1000.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, builder.ToString());
            File.WriteAllText(Path.ChangeExtension(path, ".xml"),
                "<SmallEnemyPressureV22W5W6ReliefSweep SampleCount=\"1000\" Candidates=\"CONTROL,R1,R2\" />");
            Debug.unityLogger.logEnabled = wasLoggingEnabled;
            EditorApplication.Exit(0);
        }

        private static string DisplayName(EnemyHpCurveCandidate candidate)
        {
            switch (candidate)
            {
                case EnemyHpCurveCandidate.LargeScaleModerate: return "CONTROL";
                case EnemyHpCurveCandidate.W5W6MildRelief: return "R1";
                case EnemyHpCurveCandidate.W5W6StrongRelief: return "R2";
                default: return candidate.ToString();
            }
        }
    }
}
