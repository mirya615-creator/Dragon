using System;
using System.Collections.Generic;
using System.Text;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.AI
{
    [Serializable]
    public sealed class AiSurvivalWaveRecord
    {
        public int Wave { get; internal set; }
        public int Resources { get; internal set; }
        public int RecruitCount { get; internal set; }
        public int BasicUnits { get; internal set; }
        public int Components { get; internal set; }
        public int PairLinks { get; internal set; }
        public int OpenCells { get; internal set; }
        public int Kills { get; internal set; }
        public int Leaks { get; internal set; }
        public int BaseHP { get; internal set; }
    }

    /// <summary>Read-only per-wave view of one AI side's real gameplay state.</summary>
    public sealed class AiSurvivalDiagnostics
    {
        private readonly TeamSide side;
        private readonly BoardGrid board;
        private readonly BoardRecruitDestination destination;
        private readonly RecruitmentService recruitment;
        private readonly TeamState team;
        private readonly List<AiSurvivalWaveRecord> records = new List<AiSurvivalWaveRecord>();

        public AiSurvivalDiagnostics(
            TeamSide side,
            BoardGrid board,
            BoardRecruitDestination destination,
            RecruitmentService recruitment,
            TeamState team)
        {
            this.side = side;
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.recruitment = recruitment ?? throw new ArgumentNullException(nameof(recruitment));
            this.team = team ?? throw new ArgumentNullException(nameof(team));
            if (board.Side != side || team.Side != side)
            {
                throw new ArgumentException("Diagnostics must observe one matching combat side.");
            }
        }

        public bool EmitLogs { get; set; } = true;
        public IReadOnlyList<AiSurvivalWaveRecord> WaveRecords => records;
        public int FirstLeakWave { get; private set; } = -1;
        public int DeathWave { get; private set; } = -1;
        public int DeathRecruitCount { get; private set; }
        public int DeathKills { get; private set; }
        public int DeathLeaks { get; private set; }

        public void RecordWaveEnd(int wave, int kills, int leaks)
        {
            if (wave < 1 || HasRecord(wave))
            {
                return;
            }

            if (FirstLeakWave < 0 && leaks > 0)
            {
                FirstLeakWave = wave;
            }

            var record = Capture(wave, kills, leaks);
            records.Add(record);
            if (EmitLogs)
            {
                Debug.Log(FormatRecord(record));
            }
        }

        public void RecordRunEnd(int wave, int kills, int leaks)
        {
            if (FirstLeakWave < 0 && leaks > 0)
            {
                FirstLeakWave = Math.Max(1, wave);
            }

            if (team.HatchlingHealth > 0 || DeathWave >= 0)
            {
                return;
            }

            DeathWave = Math.Max(1, wave);
            DeathRecruitCount = recruitment.CompletedRecruitments;
            DeathKills = kills;
            DeathLeaks = leaks;
            if (EmitLogs)
            {
                Debug.Log(
                    $"AI_SURVIVAL_DEFEAT Side={side} Wave={DeathWave} RecruitCount={DeathRecruitCount} " +
                    $"Kills={DeathKills} Leaks={DeathLeaks}");
            }
        }

        public AiSurvivalWaveRecord GetWaveRecord(int wave)
        {
            foreach (var record in records)
            {
                if (record.Wave == wave)
                {
                    return record;
                }
            }

            return null;
        }

        public string CreateSummary()
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                $"AI_SURVIVAL_SUMMARY Side={side} FirstLeakWave={FirstLeakWave} DeathWave={DeathWave} " +
                $"DeathRecruitCount={DeathRecruitCount} DeathKills={DeathKills} DeathLeaks={DeathLeaks}");
            foreach (var record in records)
            {
                builder.AppendLine(FormatRecord(record));
            }

            return builder.ToString();
        }

        private bool HasRecord(int wave)
        {
            foreach (var record in records)
            {
                if (record.Wave == wave)
                {
                    return true;
                }
            }

            return false;
        }

        private AiSurvivalWaveRecord Capture(int wave, int kills, int leaks)
        {
            var basicUnits = 0;
            var components = 0;
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind == RecruitItemKind.BasicUnit)
                {
                    basicUnits++;
                }
                else if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    components++;
                }
            }

            return new AiSurvivalWaveRecord
            {
                Wave = wave,
                Resources = team.Resources,
                RecruitCount = recruitment.CompletedRecruitments,
                BasicUnits = basicUnits,
                Components = components,
                PairLinks = destination.ActivePairLinkCount,
                OpenCells = board.UnlockedBattleCellCount,
                Kills = kills,
                Leaks = leaks,
                BaseHP = team.HatchlingHealth
            };
        }

        private static string FormatRecord(AiSurvivalWaveRecord record)
        {
            return $"AI_SURVIVAL_WAVE Wave={record.Wave} Resources={record.Resources} " +
                   $"RecruitCount={record.RecruitCount} BasicUnits={record.BasicUnits} " +
                   $"Components={record.Components} PairLinks={record.PairLinks} " +
                   $"OpenCells={record.OpenCells} Kills={record.Kills} " +
                   $"Leaks={record.Leaks} BaseHP={record.BaseHP}";
        }
    }

    public sealed class AiSurvivalSeedResult
    {
        internal AiSurvivalSeedResult(int runSeed, AiSurvivalDiagnostics diagnostics, bool reachedWaveTwenty)
        {
            RunSeed = runSeed;
            Diagnostics = diagnostics;
            ReachedWaveTwenty = reachedWaveTwenty;
        }

        public int RunSeed { get; }
        public AiSurvivalDiagnostics Diagnostics { get; }
        public bool ReachedWaveTwenty { get; }
    }

    public sealed class AiSurvivalSampleReport
    {
        internal AiSurvivalSampleReport(IReadOnlyList<AiSurvivalSeedResult> results)
        {
            Results = results;
            SampleCount = results.Count;
            foreach (var result in results)
            {
                var deathWave = result.Diagnostics.DeathWave;
                if (deathWave == 1)
                {
                    WaveOneDeaths++;
                }
                else if (deathWave == 2)
                {
                    WaveTwoDeaths++;
                }
                else if (deathWave == 3)
                {
                    WaveThreeDeaths++;
                }

                if (deathWave > 0)
                {
                    DeathCount++;
                    deathWaveTotal += deathWave;
                    if (deathWave < 3)
                    {
                        DeathsBeforeWaveThree++;
                    }

                    if (deathWave < 6)
                    {
                        DeathsBeforeWaveSix++;
                    }

                    if (deathWave <= 6)
                    {
                        DeathsBeforeWaveSeven++;
                    }
                }

                var waveOne = result.Diagnostics.GetWaveRecord(1);
                if (waveOne != null)
                {
                    waveOneBasicUnitTotal += waveOne.BasicUnits;
                    waveOneRecordCount++;
                }

                if (result.ReachedWaveTwenty)
                {
                    ReachedWaveTwentyCount++;
                }
            }
        }

        private int deathWaveTotal;
        public IReadOnlyList<AiSurvivalSeedResult> Results { get; }
        public int SampleCount { get; }
        public int WaveOneDeaths { get; private set; }
        public int WaveTwoDeaths { get; private set; }
        public int WaveThreeDeaths { get; private set; }
        public int DeathsBeforeWaveThree { get; private set; }
        public int DeathsBeforeWaveSix { get; private set; }
        public int DeathsBeforeWaveSeven { get; private set; }
        public int DeathCount { get; private set; }
        public int ReachedWaveTwentyCount { get; private set; }
        private int waveOneBasicUnitTotal;
        private int waveOneRecordCount;
        public float WaveOneDeathRate => GetRate(WaveOneDeaths);
        public float WaveTwoDeathRate => GetRate(WaveTwoDeaths);
        public float WaveThreeDeathRate => GetRate(WaveThreeDeaths);
        public float DeathsBeforeWaveThreeRate => GetRate(DeathsBeforeWaveThree);
        public float DeathsBeforeWaveSixRate => GetRate(DeathsBeforeWaveSix);
        public float DeathsBeforeWaveSevenRate => GetRate(DeathsBeforeWaveSeven);
        public float AverageDeathWave => DeathCount == 0 ? 0f : (float)deathWaveTotal / DeathCount;
        public float WaveOneAverageBoardBasicUnitCount => waveOneRecordCount == 0
            ? 0f
            : (float)waveOneBasicUnitTotal / waveOneRecordCount;

        public string CreateReport()
        {
            return $"AI_SURVIVAL_SAMPLE SampleCount={SampleCount} " +
                   $"W1Deaths={WaveOneDeaths} ({WaveOneDeathRate:P2}) " +
                   $"W2Deaths={WaveTwoDeaths} ({WaveTwoDeathRate:P2}) " +
                   $"W3Deaths={WaveThreeDeaths} ({WaveThreeDeathRate:P2}) " +
                   $"DeathsBeforeW3={DeathsBeforeWaveThree} ({DeathsBeforeWaveThreeRate:P2}) " +
                   $"DeathsBeforeW6={DeathsBeforeWaveSix} ({DeathsBeforeWaveSixRate:P2}) " +
                   $"DeathsBeforeW7={DeathsBeforeWaveSeven} ({DeathsBeforeWaveSevenRate:P2}) " +
                   $"W1AverageBoardBasicUnitCount={WaveOneAverageBoardBasicUnitCount:0.00} " +
                   $"DeathCount={DeathCount} AverageDeathWave={AverageDeathWave:0.00} " +
                   $"ReachedW20={ReachedWaveTwentyCount}";
        }

        private float GetRate(int count)
        {
            return SampleCount == 0 ? 0f : (float)count / SampleCount;
        }
    }

    /// <summary>
    /// Development-only deterministic pressure simulation. It uses the same RecruitmentService,
    /// RecruitDeck, LimitedComponentBag, ShovelUnlockService, and PressureRaceSideRuntime as a
    /// live AI side; the player side is intentionally not simulated so its idle base cannot end
    /// the diagnostic before the AI result is observed.
    /// </summary>
    public static class AiSurvivalSimulation
    {
        private const int AiSeedSalt = 0x2468ACE0;
        private const float TickSeconds = 0.1f;

        public static AiSurvivalSampleReport Run(int firstRunSeed, int sampleCount)
        {
            return Run(firstRunSeed, sampleCount, TwentyWavePressureConfiguration.WaveCount);
        }

        public static AiSurvivalSampleReport Run(int firstRunSeed, int sampleCount, int maxWave)
        {
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            if (maxWave < 1 || maxWave > TwentyWavePressureConfiguration.WaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWave));
            }

            var results = new List<AiSurvivalSeedResult>(sampleCount);
            for (var offset = 0; offset < sampleCount; offset++)
            {
                results.Add(RunOne(unchecked(firstRunSeed + offset), maxWave));
            }

            return new AiSurvivalSampleReport(results.AsReadOnly());
        }

        private static AiSurvivalSeedResult RunOne(int runSeed, int maxWave)
        {
            var match = new MatchController(runSeed);
            var board = DragonBoundBoardLayout.Create(BattlefieldLayoutDefinitions.Default, TeamSide.AI);
            var destination = new BoardRecruitDestination(board);
            var catalog = GreyboxRecruitmentCatalog.Create();
            var bagSeed = unchecked(runSeed ^ AiSeedSalt);
            var bag = LimitedComponentBag.CreateBag(
                bagSeed,
                LimitedComponentBag.DefaultContentVersion,
                catalog);
            var shovelState = new ShovelRecruitmentState(
                () => board.GetPositions(CellType.Locked).Count);
            var deck = new RecruitDeck(catalog, bagSeed, "ai", bag, shovelState: shovelState);
            var recruitment = new RecruitmentService(match.AI, deck, destination);
            var shovels = new ShovelUnlockService(board, destination);
            var controller = new BasicUnitAiController(
                board,
                destination,
                recruitment,
                shovels,
                match.AI);
            controller.Diagnostics.EmitLogs = false;
            controller.Tick();

            var configuration = TwentyWavePressureConfiguration.CreateGreyboxV1();
            var composition = new TwentyWavePressureRuntime(match, null, destination, runSeed, configuration);
            var sideRuntime = new PressureRaceSideRuntime(
                "AI",
                "AiSurvival",
                TeamSide.AI,
                match.AI,
                destination,
                _ => { },
                _ => { });
            var wave = 1;
            var elapsed = 0f;
            sideRuntime.QueueWave(
                wave,
                configuration.GetWave(wave).WaveDurationSeconds,
                composition.GetWaveSpawnPlan(wave, TeamSide.AI));

            while (wave <= maxWave && match.AI.HatchlingHealth > 0)
            {
                controller.Tick();
                sideRuntime.Tick(TickSeconds, wave);
                elapsed += TickSeconds;
                if (match.AI.HatchlingHealth <= 0)
                {
                    controller.RecordRunEnd(wave, sideRuntime.TotalKills, sideRuntime.TotalLeaked);
                    break;
                }

                if (elapsed + 0.0001f < configuration.GetWave(wave).WaveDurationSeconds)
                {
                    continue;
                }

                controller.RecordWaveEnd(wave, sideRuntime.TotalKills, sideRuntime.TotalLeaked);
                if (wave == maxWave)
                {
                    break;
                }

                wave++;
                elapsed = 0f;
                sideRuntime.QueueWave(
                    wave,
                    configuration.GetWave(wave).WaveDurationSeconds,
                    composition.GetWaveSpawnPlan(wave, TeamSide.AI));
            }

            return new AiSurvivalSeedResult(
                runSeed,
                controller.Diagnostics,
                maxWave >= TwentyWavePressureConfiguration.WaveCount &&
                wave >= TwentyWavePressureConfiguration.WaveCount &&
                match.AI.HatchlingHealth > 0);
        }
    }
}
