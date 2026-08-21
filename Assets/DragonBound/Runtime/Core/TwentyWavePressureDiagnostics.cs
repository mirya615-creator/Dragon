using System;
using System.Collections.Generic;
using System.Text;

namespace DragonBound.Core
{
    public readonly struct PressureRaceWaveDiagnostic
    {
        public PressureRaceWaveDiagnostic(
            int waveIndex,
            int spawnedPerSide,
            int cumulativePerSide,
            float durationSeconds,
            float spawnIntervalSeconds)
        {
            WaveIndex = waveIndex;
            SpawnedPerSide = spawnedPerSide;
            CumulativePerSide = cumulativePerSide;
            DurationSeconds = durationSeconds;
            SpawnIntervalSeconds = spawnIntervalSeconds;
        }

        public int WaveIndex { get; }
        public int SpawnedPerSide { get; }
        public int CumulativePerSide { get; }
        public float DurationSeconds { get; }
        public float SpawnIntervalSeconds { get; }
    }

    /// <summary>Configuration-backed diagnostic; it does not own a second spawn algorithm.</summary>
    public static class TwentyWavePressureDiagnostics
    {
        public static IReadOnlyList<PressureRaceWaveDiagnostic> Build(TwentyWavePressureConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var rows = new PressureRaceWaveDiagnostic[configuration.Waves.Count];
            for (var index = 0; index < rows.Length; index++)
            {
                var definition = configuration.Waves[index];
                rows[index] = new PressureRaceWaveDiagnostic(
                    definition.WaveIndex,
                    definition.EnemyCountPerSide,
                    configuration.GetCumulativeEnemyCountPerSide(definition.WaveIndex),
                    definition.WaveDurationSeconds,
                    definition.SpawnIntervalSeconds);
            }

            return rows;
        }

        public static string CreateReport(TwentyWavePressureConfiguration configuration)
        {
            var rows = Build(configuration);
            var builder = new StringBuilder("Wave | SpawnedPerSide | CumulativePerSide | Duration | SpawnInterval");
            foreach (var row in rows)
            {
                builder.Append('\n');
                builder.Append(row.WaveIndex);
                builder.Append(" | ");
                builder.Append(row.SpawnedPerSide);
                builder.Append(" | ");
                builder.Append(row.CumulativePerSide);
                builder.Append(" | ");
                builder.Append(row.DurationSeconds.ToString("0.00"));
                builder.Append(" | ");
                builder.Append(row.SpawnIntervalSeconds.ToString("0.000"));
            }

            builder.Append('\n');
            builder.Append("W15 CumulativePerSide=");
            builder.Append(configuration.GetCumulativeEnemyCountPerSide(15));
            builder.Append('\n');
            builder.Append("W20 CumulativePerSide=");
            builder.Append(configuration.GetCumulativeEnemyCountPerSide(20));
            builder.Append('\n');
            builder.Append("TheoreticalW15KillResources=");
            builder.Append(configuration.GetCumulativeEnemyCountPerSide(15));
            return builder.ToString();
        }
    }
}
