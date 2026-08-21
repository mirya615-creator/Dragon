using System;
using System.Collections.Generic;

namespace DragonBound.Core
{
    /// <summary>One editable greybox pressure wave. All quantities are per combat side.</summary>
    public sealed class PressureRaceWaveDefinition
    {
        public PressureRaceWaveDefinition(
            int waveIndex,
            int enemyCountPerSide,
            float waveDurationSeconds,
            float normalWeight,
            float fastWeight,
            float eliteWeight,
            float healthMultiplier,
            float moveSpeedMultiplier,
            bool hasBossSlot,
            float spawnIntervalSeconds = 0f,
            float firstSpawnDelaySeconds = -1f,
            float interWaveSpawnGapSeconds = 0f)
        {
            if (waveIndex < 1 || enemyCountPerSide < 1 || waveDurationSeconds <= 0f ||
                normalWeight < 0f || fastWeight < 0f || eliteWeight < 0f ||
                normalWeight + fastWeight + eliteWeight <= 0f ||
                healthMultiplier <= 0f || moveSpeedMultiplier <= 0f ||
                spawnIntervalSeconds < 0f || firstSpawnDelaySeconds < -1.0001f ||
                interWaveSpawnGapSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            WaveIndex = waveIndex;
            EnemyCountPerSide = enemyCountPerSide;
            WaveDurationSeconds = waveDurationSeconds;
            NormalWeight = normalWeight;
            FastWeight = fastWeight;
            EliteWeight = eliteWeight;
            HealthMultiplier = healthMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            HasBossSlot = hasBossSlot;
            SpawnIntervalSeconds = spawnIntervalSeconds > 0f
                ? spawnIntervalSeconds
                : waveDurationSeconds / (enemyCountPerSide + 1f);
            FirstSpawnDelaySeconds = firstSpawnDelaySeconds >= 0f
                ? firstSpawnDelaySeconds
                : SpawnIntervalSeconds;
            InterWaveSpawnGapSeconds = interWaveSpawnGapSeconds;
        }

        public int WaveIndex { get; }
        public int EnemyCountPerSide { get; }
        public float WaveDurationSeconds { get; }
        public float NormalWeight { get; }
        public float FastWeight { get; }
        public float EliteWeight { get; }
        public float HealthMultiplier { get; }
        public float MoveSpeedMultiplier { get; }
        public bool HasBossSlot { get; }
        public float SpawnIntervalSeconds { get; }
        public float FirstSpawnDelaySeconds { get; }
        public float InterWaveSpawnGapSeconds { get; }
        public float LastSpawnTimeSeconds => FirstSpawnDelaySeconds +
                                             ((EnemyCountPerSide - 1) * SpawnIntervalSeconds);
        public float TotalWeight => NormalWeight + FastWeight + EliteWeight;
    }

    /// <summary>
    /// Single source for the pressure-race greybox. It deliberately owns the first-pass
    /// pacing only; enemy base HP remains the normal EnemyRuntime baseline.
    /// </summary>
    public sealed class TwentyWavePressureConfiguration
    {
        public const string ConfigurationId = "PressureRaceGreyboxV2";
        public const int WaveCount = BattleSettlementDefinition.MaxScheduledWave;

        // Core-loop V2 test pacing. These are isolated twenty-wave greybox parameters,
        // not the three-wave slice and not final live-balance values.
        public const float StartPreparationSeconds = 4.0f;
        public const float RegularSpawnIntervalSeconds = 1.50f;
        public const float InterWaveSpawnGapSeconds = 6.50f;
        public const float NormalMoveSpeedCellsPerSecond = 0.60f;
        public const float FastMoveSpeedCellsPerSecond = 0.80f;
        public const float EliteMoveSpeedCellsPerSecond = 0.58f;

        // R1 is the promoted production HP curve. Values are effective enemy max HP,
        // converted to the formal EnemyRuntime baseline only while building the schedule.
        private static readonly float[] ProductionMaxHitPoints =
        {
            25.5f, 26.1f, 26.7f, 35f, 45f, 63f, 95f, 120f, 145f, 175f,
            205f, 240f, 275f, 315f, 360f, 410f, 465f, 525f, 590f, 660f
        };

        private readonly PressureRaceWaveDefinition[] waves;

        public TwentyWavePressureConfiguration(IReadOnlyList<PressureRaceWaveDefinition> waveDefinitions)
        {
            if (waveDefinitions == null || waveDefinitions.Count != WaveCount)
            {
                throw new ArgumentException("The pressure-race configuration requires exactly twenty waves.", nameof(waveDefinitions));
            }

            waves = new PressureRaceWaveDefinition[waveDefinitions.Count];
            for (var index = 0; index < waveDefinitions.Count; index++)
            {
                var definition = waveDefinitions[index] ?? throw new ArgumentException("Wave definitions cannot be null.", nameof(waveDefinitions));
                if (definition.WaveIndex != index + 1)
                {
                    throw new ArgumentException("Wave indices must be contiguous and one-based.", nameof(waveDefinitions));
                }

                waves[index] = definition;
            }
        }

        public IReadOnlyList<PressureRaceWaveDefinition> Waves => waves;

        public PressureRaceWaveDefinition GetWave(int waveIndex)
        {
            if (waveIndex < 1 || waveIndex > waves.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            return waves[waveIndex - 1];
        }

        public int GetCumulativeEnemyCountPerSide(int throughWaveIndex)
        {
            if (throughWaveIndex < 0 || throughWaveIndex > waves.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(throughWaveIndex));
            }

            var total = 0;
            for (var index = 0; index < throughWaveIndex; index++)
            {
                total += waves[index].EnemyCountPerSide;
            }

            return total;
        }

        public static float GetProductionMaxHitPoints(int waveIndex)
        {
            if (waveIndex < 1 || waveIndex > WaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            return ProductionMaxHitPoints[waveIndex - 1];
        }

        public static TwentyWavePressureConfiguration CreateGreyboxV1()
        {
            return CreateCoreLoopV2();
        }

        public static TwentyWavePressureConfiguration CreateCoreLoopV2()
        {
            // R1 promotes the validated LargeScaleModerate curve with only W5/W6 relieved
            // to 45/63. Timing and archetype-specific, wave-invariant travel speeds remain
            // the existing Core Loop V2 values below.
            var counts = new[]
            {
                10, 11, 12, 13, 15, 16, 18, 19, 21, 23,
                25, 27, 29, 31, 33, 35, 37, 39, 41, 43
            };
            var definitions = new PressureRaceWaveDefinition[WaveCount];
            for (var index = 0; index < WaveCount; index++)
            {
                var wave = index + 1;
                GetProductionWeights(out var normalWeight, out var fastWeight, out var eliteWeight);
                var firstSpawnDelay = wave == 1 ? StartPreparationSeconds : 0f;
                var duration = firstSpawnDelay +
                               ((counts[index] - 1) * RegularSpawnIntervalSeconds) +
                               InterWaveSpawnGapSeconds;
                definitions[index] = new PressureRaceWaveDefinition(
                    wave,
                    counts[index],
                    duration,
                    normalWeight,
                    fastWeight,
                    eliteWeight,
                    ProductionMaxHitPoints[index] / EnemyRuntime.DefaultMaxHitPoints,
                    moveSpeedMultiplier: 1f,
                    hasBossSlot: wave == 6 || wave == 12 || wave == 16 || wave == 20,
                    spawnIntervalSeconds: RegularSpawnIntervalSeconds,
                    firstSpawnDelaySeconds: firstSpawnDelay,
                    interWaveSpawnGapSeconds: InterWaveSpawnGapSeconds);
            }

            return new TwentyWavePressureConfiguration(definitions);
        }

        public float GetMoveSpeedCellsPerSecond(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Fast:
                    return FastMoveSpeedCellsPerSecond;
                case EnemyArchetype.Elite:
                    return EliteMoveSpeedCellsPerSecond;
                default:
                    return NormalMoveSpeedCellsPerSecond;
            }
        }

        private static void GetProductionWeights(out float normal, out float fast, out float elite)
        {
            // Production has only three formal enemy entities: Normal, Boss, and
            // BossSummon. Bosses use their independent schedule; regular waves are
            // therefore always composed of Normal enemies.
            normal = 1f;
            fast = 0f;
            elite = 0f;
        }
    }
}
