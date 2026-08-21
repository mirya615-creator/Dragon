using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.Core
{
    public enum ThreeWaveEnemyDurabilityProfile
    {
        BasicUnitBaseline,
        HeroSkillShowcase
    }

    /// <summary>Retained compact verification slice. It now schedules the shared pressure-race side runtime.</summary>
    public sealed class ThreeWaveSliceRuntime : IWaveRuntime
    {
        public const int WaveCount = 3;
        public const float Wave1DurationSeconds = 24f;
        public const float Wave2DurationSeconds = 26f;
        public const float Wave3DurationSeconds = 28f;

        private static readonly WaveDefinition[] BasicUnitWaves =
        {
            new WaveDefinition(4, Wave1DurationSeconds, 30f, 30f),
            new WaveDefinition(5, Wave2DurationSeconds, 30f, 30f),
            new WaveDefinition(6, Wave3DurationSeconds, 30f, 30f)
        };

        // Hero skills need enemies that survive across their first meaningful cadence.
        private static readonly WaveDefinition[] HeroSkillShowcaseWaves =
        {
            new WaveDefinition(4, Wave1DurationSeconds, 300f, 300f),
            new WaveDefinition(5, Wave2DurationSeconds, 340f, 340f),
            new WaveDefinition(6, Wave3DurationSeconds, 380f, 480f)
        };

        private readonly MatchController match;
        private readonly PressureRaceSideRuntime player;
        private readonly PressureRaceSideRuntime ai;
        private readonly WaveDefinition[] waves;
        private float waveElapsed;
        private int waveIndex = -1;
        private int lastElapsedLogSecond;
        private bool finalWaveEnded;

        public ThreeWaveSliceRuntime(
            MatchController match,
            BoardRecruitDestination playerDestination,
            BoardRecruitDestination aiDestination,
            ThreeWaveEnemyDurabilityProfile durabilityProfile = ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            DurabilityProfile = durabilityProfile;
            waves = GetWaves(durabilityProfile);
            player = new PressureRaceSideRuntime(
                "Player", "ThreeWave", TeamSide.Player, match.Player, playerDestination, Emit, RaiseCombatEvent);
            ai = new PressureRaceSideRuntime(
                "AI", "ThreeWave", TeamSide.AI, match.AI, aiDestination, Emit, RaiseCombatEvent);
        }

        public bool IsComplete { get; private set; }
        public bool IsGameplayRunning => match.State == MatchState.Running;
        public ThreeWaveEnemyDurabilityProfile DurabilityProfile { get; }
        public int CurrentWave => waveIndex < 0 ? 0 : waveIndex + 1;
        public float WaveElapsedSeconds => waveElapsed;
        public float WaveDurationSeconds => waveIndex < 0 ? 0f : waves[waveIndex].DurationSeconds;
        public float WaveRemainingSeconds => Mathf.Max(0f, WaveDurationSeconds - waveElapsed);
        public string LastEvent { get; private set; } = "NONE";
        public int TotalGenerated => player.TotalGenerated + ai.TotalGenerated;
        public int TotalAttacks => player.TotalAttacks + ai.TotalAttacks;
        public int TotalKills => player.TotalKills + ai.TotalKills;
        public int TotalLeaked => player.TotalLeaked + ai.TotalLeaked;
        public int TotalResidual => player.TotalResidual + ai.TotalResidual;
        public EnemyRegistry PlayerEnemyRegistry => player.Registry;
        public EnemyRegistry AiEnemyRegistry => ai.Registry;
        public EnemyPath PlayerPath => player.Path;
        public EnemyPath AiPath => ai.Path;

        public static float GetEnemyMaxHitPoints(int waveNumber, EnemyArchetype archetype)
        {
            return GetEnemyMaxHitPoints(ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline, waveNumber, archetype);
        }

        public static float GetEnemyMaxHitPoints(
            ThreeWaveEnemyDurabilityProfile durabilityProfile,
            int waveNumber,
            EnemyArchetype archetype)
        {
            var profileWaves = GetWaves(durabilityProfile);
            if (waveNumber < 1 || waveNumber > profileWaves.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber));
            }

            return profileWaves[waveNumber - 1].GetMaxHitPoints(archetype);
        }

        public event Action<CombatEvent> CombatEmitted;

        public void Tick(float deltaSeconds)
        {
            if (IsComplete || deltaSeconds <= 0f || match.State != MatchState.Running)
            {
                return;
            }

            if (waveIndex < 0)
            {
                StartWave(0);
            }

            waveElapsed += deltaSeconds;
            LogElapsedSeconds();
            player.Tick(deltaSeconds, CurrentWave);
            ai.Tick(deltaSeconds, CurrentWave);

            if (!finalWaveEnded && waveElapsed >= waves[waveIndex].DurationSeconds)
            {
                EndWave();
            }

            if (finalWaveEnded && player.Remaining == 0 && ai.Remaining == 0)
            {
                TrySettle();
            }
        }

        private void StartWave(int index)
        {
            waveIndex = index;
            waveElapsed = 0f;
            lastElapsedLogSecond = 0;
            var definition = waves[index];
            var waveNumber = CurrentWave;
            player.QueueWave(
                waveNumber,
                definition.DurationSeconds,
                BuildSpawns(definition, waveNumber, player.TotalGenerated));
            ai.QueueWave(
                waveNumber,
                definition.DurationSeconds,
                BuildSpawns(definition, waveNumber, ai.TotalGenerated));
            match.SetCurrentWave(waveNumber);
            Emit(
                $"WaveStarted Wave={waveNumber} DurationSeconds={definition.DurationSeconds:0} " +
                $"RemainingSeconds={definition.DurationSeconds:0}");
        }

        private static IReadOnlyList<PressureRaceEnemySpawn> BuildSpawns(
            WaveDefinition definition,
            int waveNumber,
            int firstEnemyNumber)
        {
            var spawns = new PressureRaceEnemySpawn[definition.EnemyCount];
            for (var index = 0; index < spawns.Length; index++)
            {
                var enemyNumber = firstEnemyNumber + index;
                var archetype = waveNumber == 3 && enemyNumber % 2 == 0
                    ? EnemyArchetype.Elite
                    : waveNumber == 2 ? EnemyArchetype.Fast : EnemyArchetype.Normal;
                spawns[index] = new PressureRaceEnemySpawn(
                    archetype,
                    definition.GetMaxHitPoints(archetype),
                    1f);
            }

            return spawns;
        }

        private void LogElapsedSeconds()
        {
            var elapsedSecond = Mathf.Min(Mathf.FloorToInt(waveElapsed), Mathf.CeilToInt(WaveDurationSeconds));
            while (lastElapsedLogSecond < elapsedSecond)
            {
                lastElapsedLogSecond++;
                Emit(
                    $"WaveElapsed Wave={CurrentWave} ElapsedSeconds={lastElapsedLogSecond} " +
                    $"RemainingSeconds={Mathf.Max(0, Mathf.CeilToInt(WaveDurationSeconds - lastElapsedLogSecond))}");
            }
        }

        private void EndWave()
        {
            player.RecordResidual(CurrentWave);
            ai.RecordResidual(CurrentWave);
            Emit(
                $"WaveFinished Wave={CurrentWave} ElapsedSeconds={Mathf.RoundToInt(waveElapsed)} " +
                $"RemainingSeconds=0 Residual={player.Remaining + ai.Remaining}");

            if (waveIndex >= waves.Length - 1)
            {
                finalWaveEnded = true;
                Emit("ThreeWave Wave=3 Event=FinalWaveEnded");
                return;
            }

            StartWave(waveIndex + 1);
        }

        private void TrySettle()
        {
            if (match.Player.HatchlingHealth <= 0 || match.AI.HatchlingHealth <= 0)
            {
                match.TryTransition(MatchState.Defeat);
                Emit("ThreeWave Result=Defeat");
            }
            else
            {
                match.TryTransition(MatchState.Victory);
                Emit("ThreeWave Result=Victory");
            }

            IsComplete = true;
        }

        private void RaiseCombatEvent(CombatEvent combatEvent)
        {
            CombatEmitted?.Invoke(combatEvent);
        }

        private void Emit(string message)
        {
            LastEvent = message;
            Debug.Log(message);
        }

        private static WaveDefinition[] GetWaves(ThreeWaveEnemyDurabilityProfile durabilityProfile)
        {
            switch (durabilityProfile)
            {
                case ThreeWaveEnemyDurabilityProfile.BasicUnitBaseline:
                    return BasicUnitWaves;
                case ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase:
                    return HeroSkillShowcaseWaves;
                default:
                    throw new ArgumentOutOfRangeException(nameof(durabilityProfile));
            }
        }

        private readonly struct WaveDefinition
        {
            public WaveDefinition(int enemyCount, float durationSeconds, float normalAndFastHitPoints, float eliteHitPoints)
            {
                if (normalAndFastHitPoints <= 0f || eliteHitPoints <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(normalAndFastHitPoints));
                }

                EnemyCount = enemyCount;
                DurationSeconds = durationSeconds;
                NormalAndFastHitPoints = normalAndFastHitPoints;
                EliteHitPoints = eliteHitPoints;
            }

            public int EnemyCount { get; }
            public float DurationSeconds { get; }
            public float NormalAndFastHitPoints { get; }
            public float EliteHitPoints { get; }

            public float GetMaxHitPoints(EnemyArchetype archetype)
            {
                return archetype == EnemyArchetype.Elite ? EliteHitPoints : NormalAndFastHitPoints;
            }
        }
    }
}
