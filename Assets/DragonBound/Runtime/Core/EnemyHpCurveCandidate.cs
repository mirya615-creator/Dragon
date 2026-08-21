using System;
using System.Collections.Generic;

namespace DragonBound.Core
{
    /// <summary>
    /// Development-only HP sweep candidates. CurrentProduction is the live configuration;
    /// the two LargeScale candidates are never selected by production bootstrap code.
    /// </summary>
    public enum EnemyHpCurveCandidate
    {
        CurrentProduction,
        LargeScaleModerate,
        LargeScaleStrong,
        W5W6MildRelief,
        W5W6StrongRelief
    }

    public static class EnemyHpCurveCandidates
    {
        private static readonly float[] LargeScaleModerateMaxHp =
        {
            25.5f, 26.1f, 26.7f, 35f, 50f, 70f, 95f, 120f, 145f, 175f,
            205f, 240f, 275f, 315f, 360f, 410f, 465f, 525f, 590f, 660f
        };

        private static readonly float[] LargeScaleStrongMaxHp =
        {
            25.5f, 26.1f, 26.7f, 40f, 60f, 85f, 120f, 150f, 185f, 225f,
            270f, 320f, 375f, 435f, 500f, 570f, 645f, 725f, 810f, 900f
        };

        private static readonly float[] W5W6MildReliefMaxHp = CreateReliefCurve(45f, 63f);
        private static readonly float[] W5W6StrongReliefMaxHp = CreateReliefCurve(42.5f, 59.5f);

        public static TwentyWavePressureConfiguration Create(EnemyHpCurveCandidate candidate)
        {
            var current = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            if (candidate == EnemyHpCurveCandidate.CurrentProduction)
            {
                return current;
            }

            var targetMaxHp = candidate == EnemyHpCurveCandidate.LargeScaleModerate
                ? LargeScaleModerateMaxHp
                : candidate == EnemyHpCurveCandidate.LargeScaleStrong
                    ? LargeScaleStrongMaxHp
                    : candidate == EnemyHpCurveCandidate.W5W6MildRelief
                        ? W5W6MildReliefMaxHp
                        : W5W6StrongReliefMaxHp;
            var definitions = new List<PressureRaceWaveDefinition>(TwentyWavePressureConfiguration.WaveCount);
            foreach (var wave in current.Waves)
            {
                // EnemyRuntime.DefaultMaxHitPoints remains the production base. Candidates
                // replace only the per-wave HP multiplier, so all count/composition/timing/
                // movement fields stay byte-for-byte inherited from the production schedule.
                var targetHp = targetMaxHp[wave.WaveIndex - 1];
                definitions.Add(new PressureRaceWaveDefinition(
                    wave.WaveIndex,
                    wave.EnemyCountPerSide,
                    wave.WaveDurationSeconds,
                    wave.NormalWeight,
                    wave.FastWeight,
                    wave.EliteWeight,
                    targetHp / EnemyRuntime.DefaultMaxHitPoints,
                    wave.MoveSpeedMultiplier,
                    wave.HasBossSlot,
                    wave.SpawnIntervalSeconds,
                    wave.FirstSpawnDelaySeconds,
                    wave.InterWaveSpawnGapSeconds));
            }

            return new TwentyWavePressureConfiguration(definitions);
        }

        public static float GetExpectedMaxHitPoints(EnemyHpCurveCandidate candidate, int waveIndex)
        {
            if (waveIndex < 1 || waveIndex > TwentyWavePressureConfiguration.WaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            if (candidate == EnemyHpCurveCandidate.LargeScaleModerate)
            {
                return LargeScaleModerateMaxHp[waveIndex - 1];
            }

            if (candidate == EnemyHpCurveCandidate.LargeScaleStrong)
            {
                return LargeScaleStrongMaxHp[waveIndex - 1];
            }

            if (candidate == EnemyHpCurveCandidate.W5W6MildRelief)
            {
                return W5W6MildReliefMaxHp[waveIndex - 1];
            }

            if (candidate == EnemyHpCurveCandidate.W5W6StrongRelief)
            {
                return W5W6StrongReliefMaxHp[waveIndex - 1];
            }

            var production = TwentyWavePressureConfiguration.CreateCoreLoopV2().GetWave(waveIndex);
            return EnemyRuntime.DefaultMaxHitPoints * production.HealthMultiplier;
        }

        private static float[] CreateReliefCurve(float wave5, float wave6)
        {
            var values = (float[])LargeScaleModerateMaxHp.Clone();
            values[4] = wave5;
            values[5] = wave6;
            return values;
        }
    }
}
