using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.AI;
using DragonBound.Grid;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    public sealed class GlobalPressureDiagnosticsReport
    {
        internal GlobalPressureDiagnosticsReport(int sampleCount)
        {
            SampleCount = sampleCount;
            Player = new GlobalPressureSideAggregate("Player", sampleCount);
            AI = new GlobalPressureSideAggregate("AI", sampleCount);
        }

        public int SampleCount { get; }
        public GlobalPressureSideAggregate Player { get; }
        public GlobalPressureSideAggregate AI { get; }

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"GLOBAL_PRESSURE_V1 SampleCount={SampleCount}");
            builder.Append(Player.FormatReport());
            builder.Append(AI.FormatReport());
            return builder.ToString();
        }
    }

    public sealed class GlobalPressureSideAggregate
    {
        private readonly int sampleCount;
        private readonly Dictionary<int, int> deathWaveDistribution = new Dictionary<int, int>();
        private readonly Dictionary<int, int> bagEmptyWaveDistribution = new Dictionary<int, int>();
        private readonly Dictionary<int, int> bagEmptyRecruitDistribution = new Dictionary<int, int>();
        private readonly Dictionary<string, int> heroFormedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly int[] waveKillTotals = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] waveLeakTotals = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private readonly int[] waveSampleCounts = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        private int deathWaveTotal;
        private int deathCount;

        internal GlobalPressureSideAggregate(string label, int sampleCount)
        {
            Label = label;
            this.sampleCount = sampleCount;
        }

        public string Label { get; }
        public int DeathWaveOneOrEarlier { get; private set; }
        public int DeathBeforeWaveThree { get; private set; }
        public int DeathBeforeWaveSix { get; private set; }
        public int DeathBeforeWaveTwelve { get; private set; }
        public int DeathBeforeWaveSixteen { get; private set; }
        public int ReachedWaveTwenty { get; private set; }
        public float AverageDeathWave => deathCount == 0 ? 0f : (float)deathWaveTotal / deathCount;

        public RunningAverage RecruitW3 = new RunningAverage();
        public RunningAverage RecruitW6 = new RunningAverage();
        public RunningAverage RecruitW12 = new RunningAverage();
        public RunningAverage RecruitW15 = new RunningAverage();
        public RunningAverage RecruitW16Start = new RunningAverage();
        public RunningAverage RecruitW20 = new RunningAverage();
        public int W16RecruitAtLeast8 { get; private set; }
        public int W16RecruitAtLeast9 { get; private set; }
        public int W16RecruitAtLeast10 { get; private set; }
        public int W16RecruitAtLeast11 { get; private set; }

        public RunningAverage DeliveredW6 = new RunningAverage();
        public RunningAverage DeliveredW12 = new RunningAverage();
        public RunningAverage DeliveredW16 = new RunningAverage();
        public RunningAverage RemainingW6 = new RunningAverage();
        public RunningAverage RemainingW12 = new RunningAverage();
        public RunningAverage RemainingW16 = new RunningAverage();
        public int BagEmptyBeforeWaveSixteen { get; private set; }

        public RunningAverage PairLinksPerRun = new RunningAverage();
        public RunningAverage HeroCountW6 = new RunningAverage();
        public RunningAverage HeroCountW12 = new RunningAverage();
        public RunningAverage HeroCountW16 = new RunningAverage();
        public RunningAverage DistinctHeroIdsPerRun = new RunningAverage();
        public RunningAverage UnpairedComponents = new RunningAverage();

        public RunningAverage OpenCellsW6 = new RunningAverage();
        public RunningAverage OpenCellsW12 = new RunningAverage();
        public RunningAverage OpenCellsW16 = new RunningAverage();
        public RunningAverage ShovelsGenerated = new RunningAverage();
        public RunningAverage ShovelsUsed = new RunningAverage();
        public RunningAverage ShovelsDiscarded = new RunningAverage();
        public RunningAverage BoardPressureFirstWave = new RunningAverage();
        public int BenchFullRuns { get; private set; }
        public RunningAverage CampOverwriteCount = new RunningAverage();
        public RunningAverage ComponentDiscardedCount = new RunningAverage();

        public RunningAverage FirstLeakWave = new RunningAverage();
        public RunningAverage BaseHpW6 = new RunningAverage();
        public RunningAverage BaseHpW12 = new RunningAverage();
        public RunningAverage BaseHpW16 = new RunningAverage();
        public RunningAverage DeathKills = new RunningAverage();
        public RunningAverage DeathLeaks = new RunningAverage();
        public RunningAverage DeathRecruitCount = new RunningAverage();
        public RunningAverage DeathHeroCount = new RunningAverage();

        internal void AddRun(GlobalPressureSideRun run)
        {
            if (run.DeathWave > 0)
            {
                Increment(deathWaveDistribution, run.DeathWave);
                deathCount++;
                deathWaveTotal += run.DeathWave;
                if (run.DeathWave <= 1) DeathWaveOneOrEarlier++;
                if (run.DeathWave < 3) DeathBeforeWaveThree++;
                if (run.DeathWave < 6) DeathBeforeWaveSix++;
                if (run.DeathWave < 12) DeathBeforeWaveTwelve++;
                if (run.DeathWave < 16) DeathBeforeWaveSixteen++;
                DeathKills.Add(run.DeathKills);
                DeathLeaks.Add(run.DeathLeaks);
                DeathRecruitCount.Add(run.DeathRecruitCount);
                DeathHeroCount.Add(run.DeathHeroCount);
            }

            if (run.ReachedWaveTwenty)
            {
                ReachedWaveTwenty++;
            }

            if (run.HasW3Snapshot) RecruitW3.Add(run.RecruitW3);
            if (run.HasW6Snapshot) RecruitW6.Add(run.RecruitW6);
            if (run.HasW12Snapshot) RecruitW12.Add(run.RecruitW12);
            if (run.HasW15Snapshot) RecruitW15.Add(run.RecruitW15);
            if (run.HasW16StartSnapshot)
            {
                RecruitW16Start.Add(run.RecruitW16Start);
                if (run.RecruitW16Start >= 8) W16RecruitAtLeast8++;
                if (run.RecruitW16Start >= 9) W16RecruitAtLeast9++;
                if (run.RecruitW16Start >= 10) W16RecruitAtLeast10++;
                if (run.RecruitW16Start >= 11) W16RecruitAtLeast11++;
            }
            if (run.HasW20Snapshot) RecruitW20.Add(run.RecruitW20);

            if (run.HasW6Snapshot)
            {
                DeliveredW6.Add(run.DeliveredW6);
                RemainingW6.Add(run.RemainingW6);
                HeroCountW6.Add(run.HeroCountW6);
                OpenCellsW6.Add(run.OpenCellsW6);
                BaseHpW6.Add(run.BaseHpW6);
            }

            if (run.HasW12Snapshot)
            {
                DeliveredW12.Add(run.DeliveredW12);
                RemainingW12.Add(run.RemainingW12);
                HeroCountW12.Add(run.HeroCountW12);
                OpenCellsW12.Add(run.OpenCellsW12);
                BaseHpW12.Add(run.BaseHpW12);
            }

            if (run.HasW16Snapshot)
            {
                DeliveredW16.Add(run.DeliveredW16);
                RemainingW16.Add(run.RemainingW16);
                HeroCountW16.Add(run.HeroCountW16);
                OpenCellsW16.Add(run.OpenCellsW16);
                BaseHpW16.Add(run.BaseHpW16);
            }
            if (run.BagEmptyWave > 0)
            {
                Increment(bagEmptyWaveDistribution, run.BagEmptyWave);
                Increment(bagEmptyRecruitDistribution, run.BagEmptyRecruit);
                if (run.BagEmptyWave < 16) BagEmptyBeforeWaveSixteen++;
            }

            PairLinksPerRun.Add(run.PairLinksFormed);
            DistinctHeroIdsPerRun.Add(run.DistinctHeroIdsFormed);
            UnpairedComponents.Add(run.UnpairedComponents);
            foreach (var pair in run.HeroFormedCounts)
            {
                heroFormedCounts.TryGetValue(pair.Key, out var count);
                heroFormedCounts[pair.Key] = count + pair.Value;
            }

            ShovelsGenerated.Add(run.ShovelsGenerated);
            ShovelsUsed.Add(run.ShovelsUsed);
            ShovelsDiscarded.Add(run.ShovelsDiscarded);
            if (run.FirstBoardPressureWave > 0)
            {
                BoardPressureFirstWave.Add(run.FirstBoardPressureWave);
            }

            if (run.BenchFull)
            {
                BenchFullRuns++;
            }

            CampOverwriteCount.Add(run.CampOverwriteCount);
            ComponentDiscardedCount.Add(run.ComponentDiscardedCount);
            if (run.FirstLeakWave > 0)
            {
                FirstLeakWave.Add(run.FirstLeakWave);
            }

            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                if (!run.WaveRecorded[wave])
                {
                    continue;
                }

                waveKillTotals[wave] += run.WaveKills[wave];
                waveLeakTotals[wave] += run.WaveLeaks[wave];
                waveSampleCounts[wave]++;
            }
        }

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[{Label}] Death W1OrEarlier={DeathWaveOneOrEarlier} ({Rate(DeathWaveOneOrEarlier):P2}) " +
                               $"BeforeW3={DeathBeforeWaveThree} ({Rate(DeathBeforeWaveThree):P2}) " +
                               $"BeforeW6={DeathBeforeWaveSix} ({Rate(DeathBeforeWaveSix):P2}) " +
                               $"BeforeW12={DeathBeforeWaveTwelve} ({Rate(DeathBeforeWaveTwelve):P2}) " +
                               $"BeforeW16={DeathBeforeWaveSixteen} ({Rate(DeathBeforeWaveSixteen):P2}) " +
                               $"ReachedW20={ReachedWaveTwenty} ({Rate(ReachedWaveTwenty):P2}) AvgDeathWave={AverageDeathWave:0.00} " +
                               $"DeathWaveDistribution={FormatMap(deathWaveDistribution)}");
            builder.AppendLine($"[{Label}] Recruit W3={RecruitW3.Average:0.00} W6={RecruitW6.Average:0.00} " +
                               $"W12={RecruitW12.Average:0.00} W15={RecruitW15.Average:0.00} " +
                               $"W16Start={RecruitW16Start.Average:0.00} W20={RecruitW20.Average:0.00} " +
                               $"W16ReachedSamples={RecruitW16Start.Count} " +
                               $"W16>=8={RateOf(W16RecruitAtLeast8, RecruitW16Start.Count):P2} " +
                               $"W16>=9={RateOf(W16RecruitAtLeast9, RecruitW16Start.Count):P2} " +
                               $"W16>=10={RateOf(W16RecruitAtLeast10, RecruitW16Start.Count):P2} " +
                               $"W16>=11={RateOf(W16RecruitAtLeast11, RecruitW16Start.Count):P2}");
            builder.AppendLine($"[{Label}] Components Delivered W6={DeliveredW6.Average:0.00} W12={DeliveredW12.Average:0.00} W16={DeliveredW16.Average:0.00}; " +
                               $"Remaining W6={RemainingW6.Average:0.00} W12={RemainingW12.Average:0.00} W16={RemainingW16.Average:0.00}; " +
                               $"BagEmptyBeforeW16={BagEmptyBeforeWaveSixteen} ({Rate(BagEmptyBeforeWaveSixteen):P2}) " +
                               $"BagEmptyWave={FormatMap(bagEmptyWaveDistribution)} BagEmptyRecruit={FormatMap(bagEmptyRecruitDistribution)}");
            builder.AppendLine($"[{Label}] Heroes PairLinksPerRun={PairLinksPerRun.Average:0.00} " +
                               $"HeroCount W6={HeroCountW6.Average:0.00} W12={HeroCountW12.Average:0.00} W16={HeroCountW16.Average:0.00} " +
                               $"DistinctHeroIdsPerRun={DistinctHeroIdsPerRun.Average:0.00} UnpairedComponents={UnpairedComponents.Average:0.00} " +
                               $"HeroFormedCounts={FormatMap(heroFormedCounts)}");
            builder.AppendLine($"[{Label}] ShovelBoard OpenCells W6={OpenCellsW6.Average:0.00} W12={OpenCellsW12.Average:0.00} W16={OpenCellsW16.Average:0.00}; " +
                               $"ShovelsGenerated={ShovelsGenerated.Average:0.00} Used={ShovelsUsed.Average:0.00} Discarded={ShovelsDiscarded.Average:0.00}; " +
                               $"BoardPressureFirstWave={BoardPressureFirstWave.Average:0.00} BenchFullRate={Rate(BenchFullRuns):P2} " +
                               $"CampOverwrite={CampOverwriteCount.Average:0.00} ComponentDiscarded={ComponentDiscardedCount.Average:0.00}");
            builder.AppendLine($"[{Label}] Combat FirstLeakWave={FirstLeakWave.Average:0.00} " +
                               $"BaseHP W6={BaseHpW6.Average:0.00} W12={BaseHpW12.Average:0.00} W16={BaseHpW16.Average:0.00}; " +
                               $"Death Kills={DeathKills.Average:0.00} Leaks={DeathLeaks.Average:0.00} Recruit={DeathRecruitCount.Average:0.00} Hero={DeathHeroCount.Average:0.00}");
            builder.AppendLine($"[{Label}] WaveKills={FormatWaveAverages(waveKillTotals, waveSampleCounts)}");
            builder.AppendLine($"[{Label}] WaveLeaks={FormatWaveAverages(waveLeakTotals, waveSampleCounts)}");
            return builder.ToString();
        }

        private double Rate(int count)
        {
            return sampleCount == 0 ? 0d : count / (double)sampleCount;
        }

        private static double RateOf(int count, int denominator)
        {
            return denominator == 0 ? 0d : count / (double)denominator;
        }

        private static void Increment<TKey>(IDictionary<TKey, int> map, TKey key)
        {
            map.TryGetValue(key, out var count);
            map[key] = count + 1;
        }

        private static string FormatMap<TKey>(IDictionary<TKey, int> map)
        {
            if (map.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder("{");
            var first = true;
            foreach (var pair in map)
            {
                if (!first) builder.Append(", ");
                first = false;
                builder.Append(pair.Key).Append(":").Append(pair.Value);
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatWaveAverages(int[] totals, int[] counts)
        {
            var builder = new StringBuilder();
            for (var wave = 1; wave <= TwentyWavePressureConfiguration.WaveCount; wave++)
            {
                if (wave > 1) builder.Append(", ");
                builder.Append("W").Append(wave).Append(":");
                builder.Append(counts[wave] == 0 ? "NA" : (totals[wave] / (double)counts[wave]).ToString("0.00"));
            }

            return builder.ToString();
        }
    }

    public struct RunningAverage
    {
        private double total;
        private int count;
        public int Count => count;
        public double Average => count == 0 ? 0d : total / count;
        public void Add(double value)
        {
            total += value;
            count++;
        }
    }

    internal sealed class GlobalPressureSideRun
    {
        public int DeathWave = -1;
        public bool ReachedWaveTwenty;
        public int RecruitW3;
        public int RecruitW6;
        public int RecruitW12;
        public int RecruitW15;
        public int RecruitW16Start;
        public int RecruitW20;
        public bool HasW3Snapshot;
        public bool HasW6Snapshot;
        public bool HasW12Snapshot;
        public bool HasW15Snapshot;
        public bool HasW16StartSnapshot;
        public bool HasW16Snapshot;
        public bool HasW20Snapshot;
        public int DeliveredW6;
        public int DeliveredW12;
        public int DeliveredW16;
        public int RemainingW6;
        public int RemainingW12;
        public int RemainingW16;
        public int BagEmptyWave = -1;
        public int BagEmptyRecruit = -1;
        public int PairLinksFormed;
        public int HeroCountW6;
        public int HeroCountW12;
        public int HeroCountW16;
        public int DistinctHeroIdsFormed;
        public int UnpairedComponents;
        public int OpenCellsW6;
        public int OpenCellsW12;
        public int OpenCellsW16;
        public int ShovelsGenerated;
        public int ShovelsUsed;
        public int ShovelsDiscarded;
        public int FirstBoardPressureWave = -1;
        public bool BenchFull;
        public int CampOverwriteCount;
        public int ComponentDiscardedCount;
        public int FirstLeakWave = -1;
        public int BaseHpW6;
        public int BaseHpW12;
        public int BaseHpW16;
        public int DeathKills;
        public int DeathLeaks;
        public int DeathRecruitCount;
        public int DeathHeroCount;
        public readonly int[] WaveKills = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly int[] WaveLeaks = new int[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly bool[] WaveRecorded = new bool[TwentyWavePressureConfiguration.WaveCount + 1];
        public readonly Dictionary<string, int> HeroFormedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly HashSet<string> DistinctHeroIds = new HashSet<string>(StringComparer.Ordinal);
    }

    public static class GlobalPressureDiagnostics
    {
        private const int PlayerDeckSalt = 0x13579BDF;
        private const int AiDeckSalt = 0x2468ACE0;
        private const float TickSeconds = 0.1f;
        private const float MaxPostScheduleSeconds = 120f;
        private const float MaxDiagnosticRunSeconds = 720f;

        public static GlobalPressureDiagnosticsReport Run(int firstRunSeed, int sampleCount)
        {
            return Run(firstRunSeed, sampleCount, null);
        }

        public static GlobalPressureDiagnosticsReport Run(int firstRunSeed, int sampleCount, Action<int> progress)
        {
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var report = new GlobalPressureDiagnosticsReport(sampleCount);
            var runs = new GlobalPressureRunResult[sampleCount];
            var completed = 0;
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };

            Parallel.For(0, sampleCount, options, offset =>
            {
                runs[offset] = RunOne(unchecked(firstRunSeed + offset));
                var current = Interlocked.Increment(ref completed);
                progress?.Invoke(current);
            });

            for (var offset = 0; offset < sampleCount; offset++)
            {
                var result = runs[offset];
                report.Player.AddRun(result.Player);
                report.AI.AddRun(result.AI);
            }

            return report;
        }

        private static GlobalPressureRunResult RunOne(int runSeed)
        {
            var catalog = GreyboxRecruitmentCatalog.Create();
            var match = new MatchController(runSeed);
            var layout = BattlefieldLayoutDefinitions.Default;
            var playerBoard = DragonBoundBoardLayout.Create(layout, TeamSide.Player);
            var aiBoard = DragonBoundBoardLayout.Create(layout, TeamSide.AI);
            var playerDestination = new BoardRecruitDestination(playerBoard);
            var aiDestination = new BoardRecruitDestination(aiBoard);
            var playerBag = LimitedComponentBag.CreateBag(runSeed, LimitedComponentBag.DefaultContentVersion, catalog);
            var aiBagSeed = unchecked(runSeed ^ AiDeckSalt);
            var aiBag = LimitedComponentBag.CreateBag(aiBagSeed, LimitedComponentBag.DefaultContentVersion, catalog);
            var playerDeck = new RecruitDeck(
                catalog,
                unchecked(runSeed ^ PlayerDeckSalt),
                "player",
                playerBag,
                shovelState: new ShovelRecruitmentState(() => playerBoard.GetPositions(CellType.Locked).Count));
            var aiDeck = new RecruitDeck(
                catalog,
                aiBagSeed,
                "ai",
                aiBag,
                shovelState: new ShovelRecruitmentState(() => aiBoard.GetPositions(CellType.Locked).Count));
            var playerRecruitment = new RecruitmentService(match.Player, playerDeck, playerDestination);
            var aiRecruitment = new RecruitmentService(match.AI, aiDeck, aiDestination);
            var playerShovels = new ShovelUnlockService(playerBoard, playerDestination);
            var aiShovels = new ShovelUnlockService(aiBoard, aiDestination);
            var playerController = new BasicUnitAiController(
                playerBoard,
                playerDestination,
                playerRecruitment,
                playerShovels,
                match.Player);
            var aiController = new BasicUnitAiController(
                aiBoard,
                aiDestination,
                aiRecruitment,
                aiShovels,
                match.AI);
            playerController.Diagnostics.EmitLogs = false;
            aiController.Diagnostics.EmitLogs = false;

            var run = new GlobalPressureRunResult();
            HookSide(playerRecruitment, playerDestination, playerShovels, run.Player);
            HookSide(aiRecruitment, aiDestination, aiShovels, run.AI);

            playerController.Tick();
            aiController.Tick();
            var runtime = new TwentyWavePressureRuntime(match, playerDestination, aiDestination, runSeed);
            runtime.EmitLogs = false;
            runtime.StartRun();

            var previousWave = runtime.CurrentWaveIndex;
            var previousPlayerKills = 0;
            var previousAiKills = 0;
            var previousPlayerLeaks = 0;
            var previousAiLeaks = 0;
            var postScheduleSeconds = 0f;
            var elapsedRunSeconds = 0f;
            while (!runtime.IsComplete)
            {
                playerController.Tick();
                aiController.Tick();
                runtime.Tick(TickSeconds);
                elapsedRunSeconds += TickSeconds;

                if (runtime.CurrentWaveIndex != previousWave)
                {
                    RecordWaveEnd(run.Player, previousWave, runtime.PlayerTotalKilled - previousPlayerKills, runtime.PlayerTotalReachedGoal - previousPlayerLeaks);
                    RecordWaveEnd(run.AI, previousWave, runtime.AiTotalKilled - previousAiKills, runtime.AiTotalReachedGoal - previousAiLeaks);
                    previousPlayerKills = runtime.PlayerTotalKilled;
                    previousAiKills = runtime.AiTotalKilled;
                    previousPlayerLeaks = runtime.PlayerTotalReachedGoal;
                    previousAiLeaks = runtime.AiTotalReachedGoal;
                    CaptureWaveSnapshot(run.Player, previousWave, match.Player, playerRecruitment, playerDestination, playerBoard);
                    CaptureWaveSnapshot(run.AI, previousWave, match.AI, aiRecruitment, aiDestination, aiBoard);
                    if (runtime.CurrentWaveIndex == 16)
                    {
                        run.Player.RecruitW16Start = playerRecruitment.CompletedRecruitments;
                        run.AI.RecruitW16Start = aiRecruitment.CompletedRecruitments;
                        run.Player.HasW16StartSnapshot = true;
                        run.AI.HasW16StartSnapshot = true;
                    }

                    previousWave = runtime.CurrentWaveIndex;
                }

                SyncLive(run.Player, runtime.CurrentWaveIndex, match.Player, playerRecruitment, playerDestination, playerBoard);
                SyncLive(run.AI, runtime.CurrentWaveIndex, match.AI, aiRecruitment, aiDestination, aiBoard);

                if (runtime.RegularWaveScheduleCompleted)
                {
                    if (!run.Player.ReachedWaveTwenty)
                    {
                        run.Player.ReachedWaveTwenty = true;
                        run.AI.ReachedWaveTwenty = true;
                        RecordWaveEnd(run.Player, 20, runtime.PlayerTotalKilled - previousPlayerKills, runtime.PlayerTotalReachedGoal - previousPlayerLeaks);
                        RecordWaveEnd(run.AI, 20, runtime.AiTotalKilled - previousAiKills, runtime.AiTotalReachedGoal - previousAiLeaks);
                        CaptureWaveSnapshot(run.Player, 20, match.Player, playerRecruitment, playerDestination, playerBoard);
                        CaptureWaveSnapshot(run.AI, 20, match.AI, aiRecruitment, aiDestination, aiBoard);
                    }

                    postScheduleSeconds += TickSeconds;
                    if ((runtime.PlayerAliveEnemyCount + runtime.AiAliveEnemyCount == 0) ||
                        postScheduleSeconds >= MaxPostScheduleSeconds)
                    {
                        break;
                    }
                }

                if (elapsedRunSeconds >= MaxDiagnosticRunSeconds)
                {
                    break;
                }
            }

            FinalizeSide(run.Player, match.Player, playerRecruitment, playerDestination, runtime.PlayerTotalKilled, runtime.PlayerTotalReachedGoal, runtime.CurrentWaveIndex);
            FinalizeSide(run.AI, match.AI, aiRecruitment, aiDestination, runtime.AiTotalKilled, runtime.AiTotalReachedGoal, runtime.CurrentWaveIndex);
            return run;
        }

        private static void HookSide(
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            ShovelUnlockService shovels,
            GlobalPressureSideRun run)
        {
            recruitment.Attempted += attempt =>
            {
                if (attempt.Status != RecruitmentStatus.Success)
                {
                    return;
                }

                foreach (var card in attempt.Batch.Cards)
                {
                    if (card.Kind == RecruitItemKind.Shovel)
                    {
                        run.ShovelsGenerated++;
                    }
                }

                if (attempt.RefreshedBench)
                {
                    run.CampOverwriteCount++;
                    foreach (var card in attempt.RefreshedCards)
                    {
                        if (card.Kind == RecruitItemKind.HeroComponent)
                        {
                            run.ComponentDiscardedCount++;
                        }
                        else if (card.Kind == RecruitItemKind.Shovel)
                        {
                            run.ShovelsDiscarded++;
                        }
                    }
                }
            };
            destination.HeroPairLinked += linked =>
            {
                run.PairLinksFormed++;
                run.DistinctHeroIds.Add(linked.PairLink.HeroId);
                run.HeroFormedCounts.TryGetValue(linked.PairLink.HeroId, out var count);
                run.HeroFormedCounts[linked.PairLink.HeroId] = count + 1;
            };
            shovels.ShovelUsed += _ => run.ShovelsUsed++;
        }

        private static void SyncLive(
            GlobalPressureSideRun run,
            int wave,
            TeamState team,
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            BoardGrid board)
        {
            if (run.BagEmptyWave < 0 && recruitment.RemainingHeroComponents == 0)
            {
                run.BagEmptyWave = Math.Max(1, wave);
                run.BagEmptyRecruit = recruitment.CompletedRecruitments;
            }

            if (run.FirstBoardPressureWave < 0 &&
                board.UnlockedBattleCellCount > 0 &&
                destination.DeployedCount >= board.UnlockedBattleCellCount)
            {
                run.FirstBoardPressureWave = Math.Max(1, wave);
            }

            if (destination.CampCount >= board.GetPositions(CellType.Bench).Count)
            {
                run.BenchFull = true;
            }

            if (run.FirstLeakWave < 0 && team.HatchlingHealth < team.HatchlingMaxHealth)
            {
                run.FirstLeakWave = Math.Max(1, wave);
            }
        }

        private static void CaptureWaveSnapshot(
            GlobalPressureSideRun run,
            int wave,
            TeamState team,
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            BoardGrid board)
        {
            switch (wave)
            {
                case 3:
                    run.RecruitW3 = recruitment.CompletedRecruitments;
                    run.HasW3Snapshot = true;
                    break;
                case 6:
                    run.HasW6Snapshot = true;
                    run.RecruitW6 = recruitment.CompletedRecruitments;
                    run.DeliveredW6 = recruitment.DrawnHeroComponents;
                    run.RemainingW6 = recruitment.RemainingHeroComponents;
                    run.HeroCountW6 = destination.ActivePairLinkCount;
                    run.OpenCellsW6 = board.UnlockedBattleCellCount;
                    run.BaseHpW6 = team.HatchlingHealth;
                    break;
                case 12:
                    run.HasW12Snapshot = true;
                    run.RecruitW12 = recruitment.CompletedRecruitments;
                    run.DeliveredW12 = recruitment.DrawnHeroComponents;
                    run.RemainingW12 = recruitment.RemainingHeroComponents;
                    run.HeroCountW12 = destination.ActivePairLinkCount;
                    run.OpenCellsW12 = board.UnlockedBattleCellCount;
                    run.BaseHpW12 = team.HatchlingHealth;
                    break;
                case 15:
                    run.HasW15Snapshot = true;
                    run.RecruitW15 = recruitment.CompletedRecruitments;
                    break;
                case 16:
                    run.HasW16Snapshot = true;
                    run.DeliveredW16 = recruitment.DrawnHeroComponents;
                    run.RemainingW16 = recruitment.RemainingHeroComponents;
                    run.HeroCountW16 = destination.ActivePairLinkCount;
                    run.OpenCellsW16 = board.UnlockedBattleCellCount;
                    run.BaseHpW16 = team.HatchlingHealth;
                    break;
                case 20:
                    run.HasW20Snapshot = true;
                    run.RecruitW20 = recruitment.CompletedRecruitments;
                    break;
            }
        }

        private static void RecordWaveEnd(GlobalPressureSideRun run, int wave, int kills, int leaks)
        {
            if (wave < 1 || wave > TwentyWavePressureConfiguration.WaveCount || run.WaveRecorded[wave])
            {
                return;
            }

            run.WaveKills[wave] = kills;
            run.WaveLeaks[wave] = leaks;
            run.WaveRecorded[wave] = true;
        }

        private static void FinalizeSide(
            GlobalPressureSideRun run,
            TeamState team,
            RecruitmentService recruitment,
            BoardRecruitDestination destination,
            int totalKills,
            int totalLeaks,
            int currentWave)
        {
            run.DistinctHeroIdsFormed = run.DistinctHeroIds.Count;
            run.UnpairedComponents = Math.Max(0, CountCards(destination, RecruitItemKind.HeroComponent) - destination.ActivePairLinkCount * 2);
            if (team.HatchlingHealth <= 0)
            {
                run.DeathWave = Math.Max(1, currentWave);
                run.DeathKills = totalKills;
                run.DeathLeaks = totalLeaks;
                run.DeathRecruitCount = recruitment.CompletedRecruitments;
                run.DeathHeroCount = destination.ActivePairLinkCount;
            }
        }

        private static int CountCards(BoardRecruitDestination destination, RecruitItemKind kind)
        {
            var count = 0;
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class GlobalPressureRunResult
        {
            public readonly GlobalPressureSideRun Player = new GlobalPressureSideRun();
            public readonly GlobalPressureSideRun AI = new GlobalPressureSideRun();
        }
    }
}
