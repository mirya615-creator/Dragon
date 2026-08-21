using System.IO;
using DragonBound.Core;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class W6SoulChainTelemetryBatch
    {
        [MenuItem("DragonBound/Diagnostics/Run W6 SoulChain 1000 Seed")]
        public static void Run1000()
        {
            var comparison = W6SoulChainTelemetryRunner.RunSeedRange(1, 1000);
            var logsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(logsDirectory);

            var wrapper = new W6SoulChainTelemetryOutput
            {
                FixtureId = W6SoulChainTelemetryRunner.FixtureId,
                TestFixture = true,
                W6NormalCount = W6SoulChainTelemetryRunner.W6NormalCount,
                W6NormalMaxHitPoints = W6SoulChainTelemetryRunner.W6NormalMaxHitPoints,
                W7StartSeconds = W6SoulChainTelemetryRunner.W7StartSeconds,
                BossGreyboxHitPoints = SoulchainBinderConfiguration.GreyboxMaxHitPoints,
                FormalBossHitPointsPending = SoulchainBinderConfiguration.FormalHitPointsPending,
                Comparison = comparison
            };

            File.WriteAllText(
                Path.Combine(logsDirectory, "W6SoulChainTelemetry.json"),
                JsonUtility.ToJson(wrapper, true));
            File.WriteAllText(
                Path.Combine(logsDirectory, "W6SoulChainTelemetry.csv"),
                comparison.ToCsv());
            Debug.Log(
                $"W6SoulChainTelemetry Complete Seeds=1000 Fixture={wrapper.FixtureId} " +
                $"Json={Path.Combine(logsDirectory, "W6SoulChainTelemetry.json")} " +
                $"Csv={Path.Combine(logsDirectory, "W6SoulChainTelemetry.csv")}");
        }

        [System.Serializable]
        private sealed class W6SoulChainTelemetryOutput
        {
            public string FixtureId;
            public bool TestFixture;
            public int W6NormalCount;
            public float W6NormalMaxHitPoints;
            public float W7StartSeconds;
            public float BossGreyboxHitPoints;
            public bool FormalBossHitPointsPending;
            public W6SoulChainTelemetryComparison Comparison;
        }
    }
}
