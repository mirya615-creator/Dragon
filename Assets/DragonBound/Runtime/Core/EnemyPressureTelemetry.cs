using System;
using System.Collections.Generic;
using System.Text;

namespace DragonBound.Core
{
    /// <summary>
    /// Per-run observer for enemy lifetimes. It receives only lifecycle events from the real
    /// PressureRaceSideRuntime; it owns no spawn, combat, targeting, or damage behavior.
    /// </summary>
    internal sealed class EnemyPressureSideRun
    {
        private readonly Dictionary<string, ActiveEnemy> active =
            new Dictionary<string, ActiveEnemy>(StringComparer.Ordinal);

        public readonly int[] Spawned = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] Killed = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] Leaked = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] ReachedFinalQuarter = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] ResidualAtNextWaveStart = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] PeakAlive = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly float[] ActualMaxHitPoints = new float[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly List<float>[] ResolvedLifetimes = CreateLists();
        public readonly List<float>[] DeathLifetimes = CreateLists();

        public void RecordLifecycle(EnemyLifecycleEvent value, float elapsedTime)
        {
            if (value.SpawnWave < 1 || value.SpawnWave > TwentyWavePressureConfiguration.WaveCount)
            {
                return;
            }

            if (value.Kind == EnemyLifecycleEventKind.Spawned)
            {
                active[value.RuntimeId] = new ActiveEnemy(value.SpawnWave, elapsedTime, value.PathProgress >= 0.75f);
                Spawned[value.SpawnWave]++;
                ActualMaxHitPoints[value.SpawnWave] = value.MaxHitPoints;
                return;
            }

            if (!active.TryGetValue(value.RuntimeId, out var entry))
            {
                return;
            }

            active.Remove(value.RuntimeId);
            entry.ReachedFinalQuarter |= value.PathProgress >= 0.75f;
            if (entry.ReachedFinalQuarter)
            {
                ReachedFinalQuarter[entry.SpawnWave]++;
            }

            var lifetime = Math.Max(0f, elapsedTime - entry.SpawnTime);
            ResolvedLifetimes[entry.SpawnWave].Add(lifetime);
            if (value.Kind == EnemyLifecycleEventKind.Killed)
            {
                Killed[entry.SpawnWave]++;
                DeathLifetimes[entry.SpawnWave].Add(lifetime);
            }
            else if (value.Kind == EnemyLifecycleEventKind.Leaked)
            {
                Leaked[entry.SpawnWave]++;
            }
        }

        public void TrackProgress(EnemyRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            foreach (var enemy in registry.Enemies)
            {
                if (enemy != null && active.TryGetValue(enemy.RuntimeId, out var entry) && enemy.PathProgress >= 0.75f)
                {
                    entry.ReachedFinalQuarter = true;
                    active[enemy.RuntimeId] = entry;
                }
            }
        }

        public void RecordResidual(int completedWave, int residual)
        {
            if (completedWave >= 1 && completedWave <= TwentyWavePressureConfiguration.WaveCount)
            {
                ResidualAtNextWaveStart[completedWave] = Math.Max(0, residual);
            }
        }

        public void RecordAlive(int wave, int alive)
        {
            if (wave >= 1 && wave <= TwentyWavePressureConfiguration.WaveCount)
            {
                PeakAlive[wave] = Math.Max(PeakAlive[wave], Math.Max(0, alive));
            }
        }

        private static List<float>[] CreateLists()
        {
            var lists = new List<float>[TwentyWavePressureConfiguration.WaveCount + 1];
            for (var index = 0; index < lists.Length; index++)
            {
                lists[index] = new List<float>();
            }

            return lists;
        }

        private struct ActiveEnemy
        {
            public ActiveEnemy(int spawnWave, float spawnTime, bool reachedFinalQuarter)
            {
                SpawnWave = spawnWave;
                SpawnTime = spawnTime;
                ReachedFinalQuarter = reachedFinalQuarter;
            }

            public int SpawnWave;
            public float SpawnTime;
            public bool ReachedFinalQuarter;
        }
    }

    internal sealed class EnemyPressureSideAggregate
    {
        private readonly int sampleCount;
        private readonly long[] spawned = new long[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly long[] killed = new long[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly long[] leaked = new long[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly long[] reachedFinalQuarter = new long[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly long[] residualAtNextWaveStart = new long[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly List<int>[] residualSamples = CreateIntLists();
        private readonly List<int>[] peakAliveSamples = CreateIntLists();
        private readonly double[] actualHpTotals = new double[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] actualHpSamples = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly List<float>[] resolvedLifetimes = CreateLists();
        private readonly List<float>[] deathLifetimes = CreateLists();

        public EnemyPressureSideAggregate(int sampleCount)
        {
            this.sampleCount = sampleCount;
        }

        public void Add(EnemyPressureSideRun run)
        {
            if (run == null)
            {
                return;
            }

            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                spawned[wave] += run.Spawned[wave];
                killed[wave] += run.Killed[wave];
                leaked[wave] += run.Leaked[wave];
                reachedFinalQuarter[wave] += run.ReachedFinalQuarter[wave];
                residualAtNextWaveStart[wave] += run.ResidualAtNextWaveStart[wave];
                residualSamples[wave].Add(run.ResidualAtNextWaveStart[wave]);
                peakAliveSamples[wave].Add(run.PeakAlive[wave]);
                if (run.Spawned[wave] > 0)
                {
                    actualHpTotals[wave] += run.ActualMaxHitPoints[wave];
                    actualHpSamples[wave]++;
                }

                resolvedLifetimes[wave].AddRange(run.ResolvedLifetimes[wave]);
                deathLifetimes[wave].AddRange(run.DeathLifetimes[wave]);
            }
        }

        public string Format(string label)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[{label}] EnemyHPActual {FormatHpTable()}");
            foreach (var wave in new[] { 3, 6, 7, 8, 10, 12, 16, 20 })
            {
                var lifetime = resolvedLifetimes[wave];
                var deaths = deathLifetimes[wave];
                builder.AppendLine(
                    $"[{label}] TTK W{wave} EnemyLifetimeSeconds Mean={Average(lifetime):0.00} P50={Percentile(lifetime, 0.50):0.00} " +
                    $"P75={Percentile(lifetime, 0.75):0.00} P90={Percentile(lifetime, 0.90):0.00} " +
                    $"SpawnToDeathSeconds Mean={Average(deaths):0.00} P50={Percentile(deaths, 0.50):0.00} " +
                    $"P75={Percentile(deaths, 0.75):0.00} P90={Percentile(deaths, 0.90):0.00} " +
                    $"PDeathLt1={Rate(CountLessThan(deaths, 1f), spawned[wave]):P2} " +
                    $"PDeathLt2={Rate(CountLessThan(deaths, 2f), spawned[wave]):P2} " +
                    $"PLivesGt5={Rate(CountGreaterThan(lifetime, 5f), lifetime.Count):P2} " +
                    $"PReachesFinal25={Rate(reachedFinalQuarter[wave], spawned[wave]):P2} " +
                    $"Spawned={spawned[wave]} Killed={killed[wave]} Leaked={leaked[wave]}");
            }

            foreach (var wave in new[] { 6, 7, 8, 10, 12, 13, 16 })
            {
                builder.AppendLine(
                    $"[{label}] EnemyBacklog W{wave} EnemiesRemainingAtNextWaveStart=" +
                    $"Mean={AveragePerRun(residualAtNextWaveStart[wave]):0.00} " +
                    $"P90={Percentile(residualSamples[wave], 0.90):0.00} " +
                    $"Max={Maximum(residualSamples[wave])} " +
                    $"MeanPeakAlive={Average(peakAliveSamples[wave]):0.00} " +
                    $"P90PeakAlive={Percentile(peakAliveSamples[wave], 0.90):0.00}");
            }

            return builder.ToString();
        }

        private string FormatHpTable()
        {
            var builder = new StringBuilder();
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                if (wave > 1) builder.Append(' ');
                builder.Append("W").Append(wave).Append('=').Append(
                    actualHpSamples[wave] == 0
                        ? "NA"
                        : (actualHpTotals[wave] / actualHpSamples[wave]).ToString("0.0"));
            }

            return builder.ToString();
        }

        private static List<float>[] CreateLists()
        {
            var lists = new List<float>[TwentyWavePressureConfiguration.WaveCount + 1];
            for (var index = 0; index < lists.Length; index++) lists[index] = new List<float>();
            return lists;
        }

        private static List<int>[] CreateIntLists()
        {
            var lists = new List<int>[TwentyWavePressureConfiguration.WaveCount + 1];
            for (var index = 0; index < lists.Length; index++) lists[index] = new List<int>();
            return lists;
        }

        private static int CountLessThan(IReadOnlyList<float> values, float threshold)
        {
            var count = 0;
            foreach (var value in values) if (value < threshold) count++;
            return count;
        }

        private static int CountGreaterThan(IReadOnlyList<float> values, float threshold)
        {
            var count = 0;
            foreach (var value in values) if (value > threshold) count++;
            return count;
        }

        private double AveragePerRun(long total) => sampleCount == 0 ? 0d : total / (double)sampleCount;
        private static double Rate(long numerator, long denominator) => denominator <= 0 ? 0d : numerator / (double)denominator;
        private static double Average(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0) return 0d;
            double total = 0d;
            foreach (var value in values) total += value;
            return total / values.Count;
        }

        private static double Average(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0) return 0d;
            long total = 0;
            foreach (var value in values) total += value;
            return total / (double)values.Count;
        }

        private static double Percentile(IReadOnlyList<float> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0d;
            var ordered = new List<float>(values);
            ordered.Sort();
            var position = (ordered.Count - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            if (lower == upper) return ordered[lower];
            return ordered[lower] + ((ordered[upper] - ordered[lower]) * (position - lower));
        }

        private static double Percentile(IReadOnlyList<int> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0d;
            var ordered = new List<int>(values);
            ordered.Sort();
            var position = (ordered.Count - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            if (lower == upper) return ordered[lower];
            return ordered[lower] + ((ordered[upper] - ordered[lower]) * (position - lower));
        }

        private static int Maximum(IReadOnlyList<int> values)
        {
            var maximum = 0;
            if (values == null) return maximum;
            foreach (var value in values) maximum = Math.Max(maximum, value);
            return maximum;
        }
    }
}


