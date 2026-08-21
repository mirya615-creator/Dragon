using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DragonBound.Core
{
    public sealed class W1ToW5SurvivalFunnelReport
    {
        private readonly W1ToW5SurvivalFunnelAggregate player = new W1ToW5SurvivalFunnelAggregate("Player");
        private readonly W1ToW5SurvivalFunnelAggregate ai = new W1ToW5SurvivalFunnelAggregate("AI");
        private readonly List<W1ToW5FunnelSample> samples = new List<W1ToW5FunnelSample>();

        public W1ToW5SurvivalFunnelReport(int firstRunSeed, int sampleCount, bool swapDeckInputs)
        {
            FirstRunSeed = firstRunSeed;
            SampleCount = sampleCount;
            SwapDeckInputs = swapDeckInputs;
        }

        public int FirstRunSeed { get; }
        public int SampleCount { get; }
        public bool SwapDeckInputs { get; }
        public W1ToW5SurvivalFunnelAggregate Player => player;
        public W1ToW5SurvivalFunnelAggregate AI => ai;
        public IReadOnlyList<W1ToW5FunnelSample> Samples => samples;

        internal void Add(int runSeed, CoreLoopRunResult result)
        {
            if (result == null) return;
            player.Add(runSeed, result.Player, result.PlayerEnemyPressure, result.MatchEnd, result.W6Calibration.Player);
            ai.Add(runSeed, result.AI, result.AiEnemyPressure, result.MatchEnd, result.W6Calibration.AI);
            samples.Add(W1ToW5FunnelSample.Create(runSeed, "Player", result.Player, result.PlayerEnemyPressure, result.MatchEnd, result.W6Calibration.Player));
            samples.Add(W1ToW5FunnelSample.Create(runSeed, "AI", result.AI, result.AiEnemyPressure, result.MatchEnd, result.W6Calibration.AI));
        }

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine("W1-W5 Survival Funnel and Side Bias Audit");
            builder.AppendLine("SeedSet=" + FirstRunSeed + ".." + (FirstRunSeed + SampleCount - 1) +
                " SwapDeckInputs=" + SwapDeckInputs +
                " SharedSettlement=Production Match ends when either side is defeated");
            builder.AppendLine("W6BossHP=NotUsed; Items=Disabled; Runes=Disabled; PlayerAndAIControllers=BasicUnitAiController");
            builder.AppendLine(player.Format());
            builder.AppendLine(ai.Format());
            return builder.ToString();
        }

        public string ToCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("runSeed,side,swapDeckInputs,reachedW2,reachedW3,reachedW4,reachedW5,reachedW6,deathWave,firstLeakWave,deathReason,mergeCount,recruitStallCount,w1Heart,w1Resources,w1Recruit,w1Basic,w1Hero,w1Board,w1Bench,w1Residual,w2Heart,w2Resources,w2Recruit,w2Basic,w2Hero,w2Board,w2Bench,w2Residual,w3Heart,w3Resources,w3Recruit,w3Basic,w3Hero,w3Board,w3Bench,w3Residual,w4Heart,w4Resources,w4Recruit,w4Basic,w4Hero,w4Board,w4Bench,w4Residual,w5Heart,w5Resources,w5Recruit,w5Basic,w5Hero,w5Board,w5Bench,w5Residual,w6BossSpawned,w6HittableBasic,w6HittableHero,w6SingleTargetDps,w6HeroDescriptors");
            foreach (var sample in samples)
            {
                builder.Append(sample.ToCsv(SwapDeckInputs)).AppendLine();
            }

            return builder.ToString();
        }
    }

    public sealed class W1ToW5SurvivalFunnelAggregate
    {
        private readonly string label;
        private readonly int[] reached = new int[7];
        private readonly int[] deathWaveCounts = new int[21];
        private readonly int[] firstLeakWaveCounts = new int[21];
        private readonly double[] heartTotals = new double[6];
        private readonly double[] resourceTotals = new double[6];
        private readonly double[] recruitTotals = new double[6];
        private readonly double[] basicTotals = new double[6];
        private readonly double[] heroTotals = new double[6];
        private readonly double[] boardTotals = new double[6];
        private readonly double[] benchTotals = new double[6];
        private readonly double[] residualTotals = new double[6];
        private readonly int[] waveSamples = new int[6];
        private readonly List<double> w6Dps = new List<double>();
        private readonly Dictionary<string, int> deathReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> recruitStallReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroIds = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroLevelTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> heroLevelSamples = new Dictionary<string, int>(StringComparer.Ordinal);
        private int deathCount;
        private int firstDefeatedCount;
        private int sameFrameDoubleDefeatCount;
        private int firstLeakCount;
        private int totalMerges;
        private int totalRecruitStalls;
        private int w6BossSpawnCount;
        private int w6HittableBasic;
        private int w6HittableHero;
        private double w6DpsTotal;

        internal W1ToW5SurvivalFunnelAggregate(string label)
        {
            this.label = label;
        }

        public string Label => label;
        public int SampleCount { get; private set; }
        public int[] ReachedCounts => reached;
        public int DeathCount => deathCount;
        public int FirstDefeatedCount => firstDefeatedCount;
        public int SameFrameDoubleDefeatCount => sameFrameDoubleDefeatCount;
        public int FirstLeakCount => firstLeakCount;
        public int W6BossSpawnCount => w6BossSpawnCount;
        public int TotalMerges => totalMerges;
        public int TotalRecruitStalls => totalRecruitStalls;
        public double W6AverageSingleTargetDps => SampleCount == 0 ? 0d : w6DpsTotal / SampleCount;
        public double W6QualifiedAverageSingleTargetDps => w6Dps.Count == 0 ? 0d : Average(w6Dps);
        public int DeathWaveCount(int wave)
        {
            if (wave < 1 || wave >= deathWaveCounts.Length) throw new ArgumentOutOfRangeException(nameof(wave));
            return deathWaveCounts[wave];
        }

        internal void Add(
            int runSeed,
            CoreLoopSideRun side,
            EnemyPressureSideRun pressure,
            CoreLoopMatchEndRun matchEnd,
            W6BareCalibrationSideRun w6)
        {
            SampleCount++;
            if (matchEnd != null)
            {
                if (matchEnd.PlayerDefeated && matchEnd.AiDefeated)
                {
                    sameFrameDoubleDefeatCount++;
                }
                else if ((label == "Player" && matchEnd.PlayerDefeated) ||
                         (label == "AI" && matchEnd.AiDefeated))
                {
                    firstDefeatedCount++;
                }
            }
            for (var wave = 2; wave <= 6; wave++) if (side.StartRecorded[wave]) reached[wave]++;
            if (side.DeathWave > 0)
            {
                deathCount++;
                deathWaveCounts[Math.Min(20, side.DeathWave)]++;
                Increment(deathReasons, ResolveDeathReason(matchEnd));
            }

            if (side.FirstLeakWave > 0)
            {
                firstLeakCount++;
                firstLeakWaveCounts[Math.Min(20, side.FirstLeakWave)]++;
            }

            totalMerges += side.MergesPerformed;
            totalRecruitStalls += side.RecruitStallCount;
            foreach (var pair in side.RecruitStallsByReason) Increment(recruitStallReasons, pair.Key.ToString(), pair.Value);
            foreach (var pair in side.HeroFormedCounts) Increment(heroIds, pair.Key, pair.Value);
            foreach (var pair in side.HeroLevels)
            {
                Increment(heroLevelTotals, pair.Key, pair.Value);
                Increment(heroLevelSamples, pair.Key);
            }

            for (var wave = 1; wave <= 5; wave++)
            {
                if (!side.FunnelEndRecorded[wave]) continue;
                var index = wave - 1;
                waveSamples[index]++;
                heartTotals[index] += side.HeartAtEnd[wave];
                resourceTotals[index] += side.ResourcesAtEnd[wave];
                recruitTotals[index] += side.RecruitAtEnd[wave];
                basicTotals[index] += side.BasicUnitCountAtEnd[wave];
                heroTotals[index] += side.HeroAtEnd[wave];
                boardTotals[index] += side.BoardOccupiedAtEnd[wave];
                benchTotals[index] += side.BenchOccupiedAtEnd[wave];
                residualTotals[index] += pressure == null ? 0 : pressure.ResidualAtNextWaveStart[wave];
            }

            if (w6 != null && w6.BossSpawned)
            {
                w6BossSpawnCount++;
                w6HittableBasic += w6.HittableBasicCount;
                w6HittableHero += w6.HittableHeroCount;
                w6DpsTotal += w6.EstimatedSingleTargetDps;
                if (w6.QualifiedBaseline) w6Dps.Add(w6.EstimatedSingleTargetDps);
            }
        }

        public string Format()
        {
            var builder = new StringBuilder();
            builder.AppendLine("[" + label + "] IndependentReach " + FormatReach());
            builder.AppendLine("[" + label + "] SharedSettlementDeathCount=" + deathCount + "/" + SampleCount +
                " (" + Rate(deathCount, SampleCount).ToString("P2", CultureInfo.InvariantCulture) + ") " +
                "FirstDefeated=" + firstDefeatedCount + " SameFrameDoubleDefeat=" + sameFrameDoubleDefeatCount + " " +
                "DeathWaves=" + FormatCounts(deathWaveCounts) + " FirstLeakWaves=" + FormatCounts(firstLeakWaveCounts) +
                " FailureReasons=" + FormatMap(deathReasons));
            for (var wave = 1; wave <= 5; wave++)
            {
                var index = wave - 1;
                builder.AppendLine("[" + label + "] W" + wave +
                    " n=" + waveSamples[index] +
                    " Heart=" + Average(heartTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture) +
                    " Resources=" + Average(resourceTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture) +
                    " Recruit=" + Average(recruitTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture) +
                    " Basic=" + Average(basicTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture) +
                    " Hero=" + Average(heroTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture) +
                    " Board=" + Average(boardTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture) +
                    " Bench=" + Average(benchTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture) +
                    " Residual=" + Average(residualTotals[index], waveSamples[index]).ToString("0.00", CultureInfo.InvariantCulture));
            }

            builder.AppendLine("[" + label + "] FirstLeakRate=" + Rate(firstLeakCount, SampleCount).ToString("P2", CultureInfo.InvariantCulture) +
                " RecruitStalls=" + totalRecruitStalls + " Reasons=" + FormatMap(recruitStallReasons) +
                " Merges=" + totalMerges);
            builder.AppendLine("[" + label + "] W6BossSpawn=" + w6BossSpawnCount + "/" + SampleCount +
                " HittableBasicAvg=" + Average(w6HittableBasic, SampleCount).ToString("0.00", CultureInfo.InvariantCulture) +
                " HittableHeroAvg=" + Average(w6HittableHero, SampleCount).ToString("0.00", CultureInfo.InvariantCulture) +
                " SingleTargetDPSAll=" + W6AverageSingleTargetDps.ToString("0.00", CultureInfo.InvariantCulture) +
                " SingleTargetDPSQualified=" + W6QualifiedAverageSingleTargetDps.ToString("0.00", CultureInfo.InvariantCulture) +
                " HeroIds=" + FormatMap(heroIds) + " HeroLevels=" + FormatAverageMap(heroLevelTotals, heroLevelSamples));
            return builder.ToString();
        }

        private string FormatReach()
        {
            var builder = new StringBuilder();
            for (var wave = 2; wave <= 6; wave++)
            {
                if (wave > 2) builder.Append(' ');
                builder.Append("W").Append(wave).Append('=').Append(reached[wave]).Append('/').Append(SampleCount)
                    .Append('(').Append(Rate(reached[wave], SampleCount).ToString("P2", CultureInfo.InvariantCulture)).Append(')');
            }
            return builder.ToString();
        }

        private static string ResolveDeathReason(CoreLoopMatchEndRun matchEnd)
        {
            if (matchEnd == null) return "Other";
            if (matchEnd.EndReason.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0) return "Boss";
            if (matchEnd.EndReason.IndexOf("Heart", StringComparison.OrdinalIgnoreCase) >= 0) return "Heart";
            return "Other";
        }

        internal static string ResolveReasonForCsv(CoreLoopMatchEndRun matchEnd)
        {
            return ResolveDeathReason(matchEnd);
        }

        private static void Increment(Dictionary<string, int> values, string key, int amount = 1)
        {
            if (string.IsNullOrEmpty(key)) key = "Other";
            values.TryGetValue(key, out var current);
            values[key] = current + amount;
        }

        private static string FormatMap(Dictionary<string, int> values)
        {
            if (values.Count == 0) return "None";
            var builder = new StringBuilder();
            foreach (var pair in values)
            {
                if (builder.Length > 0) builder.Append(',');
                builder.Append(pair.Key).Append('=').Append(pair.Value);
            }
            return builder.ToString();
        }

        private static string FormatCounts(int[] values)
        {
            var builder = new StringBuilder();
            for (var wave = 1; wave < values.Length; wave++)
            {
                if (values[wave] == 0) continue;
                if (builder.Length > 0) builder.Append(',');
                builder.Append('W').Append(wave).Append('=').Append(values[wave]);
            }
            return builder.Length == 0 ? "None" : builder.ToString();
        }

        private static string FormatAverageMap(Dictionary<string, int> totals, Dictionary<string, int> samples)
        {
            if (totals.Count == 0) return "None";
            var builder = new StringBuilder();
            foreach (var pair in totals)
            {
                if (builder.Length > 0) builder.Append(',');
                samples.TryGetValue(pair.Key, out var count);
                builder.Append(pair.Key).Append('=').Append(Average(pair.Value, count).ToString("0.00", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        private static double Rate(int numerator, int denominator) => denominator == 0 ? 0d : numerator / (double)denominator;
        private static double Average(double total, int count) => count == 0 ? 0d : total / count;
        private static double Average(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0d;
            double total = 0d;
            foreach (var value in values) total += value;
            return total / values.Count;
        }
    }

    public sealed class W1ToW5FunnelSample
    {
        private readonly int[] heart = new int[6];
        private readonly int[] resources = new int[6];
        private readonly int[] recruits = new int[6];
        private readonly int[] basics = new int[6];
        private readonly int[] heroes = new int[6];
        private readonly int[] board = new int[6];
        private readonly int[] bench = new int[6];
        private readonly int[] residual = new int[6];
        private readonly bool[] reached = new bool[7];

        private W1ToW5FunnelSample(int runSeed, string side, CoreLoopSideRun run, EnemyPressureSideRun pressure, CoreLoopMatchEndRun matchEnd, W6BareCalibrationSideRun w6)
        {
            RunSeed = runSeed;
            Side = side;
            DeathWave = run.DeathWave;
            FirstLeakWave = run.FirstLeakWave;
            DeathReason = W1ToW5SurvivalFunnelAggregate.ResolveReasonForCsv(matchEnd);
            Merges = run.MergesPerformed;
            RecruitStalls = run.RecruitStallCount;
            for (var wave = 2; wave <= 6; wave++) reached[wave] = run.StartRecorded[wave];
            for (var wave = 1; wave <= 5; wave++)
            {
                var index = wave - 1;
                if (!run.FunnelEndRecorded[wave]) continue;
                heart[index] = run.HeartAtEnd[wave];
                resources[index] = run.ResourcesAtEnd[wave];
                recruits[index] = run.RecruitAtEnd[wave];
                basics[index] = run.BasicUnitCountAtEnd[wave];
                heroes[index] = run.HeroAtEnd[wave];
                board[index] = run.BoardOccupiedAtEnd[wave];
                bench[index] = run.BenchOccupiedAtEnd[wave];
                residual[index] = pressure == null ? 0 : pressure.ResidualAtNextWaveStart[wave];
            }
            W6BossSpawned = w6 != null && w6.BossSpawned;
            W6HittableBasic = w6 == null ? 0 : w6.HittableBasicCount;
            W6HittableHero = w6 == null ? 0 : w6.HittableHeroCount;
            W6SingleTargetDps = w6 == null ? 0f : w6.EstimatedSingleTargetDps;
            W6HeroDescriptors = w6 == null ? string.Empty : string.Join(";", w6.HittableHeroDescriptors.ToArray());
        }

        public int RunSeed { get; }
        public string Side { get; }
        public int DeathWave { get; }
        public int FirstLeakWave { get; }
        public string DeathReason { get; }
        public int Merges { get; }
        public int RecruitStalls { get; }
        public bool W6BossSpawned { get; }
        public int W6HittableBasic { get; }
        public int W6HittableHero { get; }
        public float W6SingleTargetDps { get; }
        public string W6HeroDescriptors { get; }

        internal static W1ToW5FunnelSample Create(int runSeed, string side, CoreLoopSideRun run, EnemyPressureSideRun pressure, CoreLoopMatchEndRun matchEnd, W6BareCalibrationSideRun w6)
        {
            return new W1ToW5FunnelSample(runSeed, side, run, pressure, matchEnd, w6);
        }

        internal string ToCsv(bool swapDeckInputs)
        {
            var builder = new StringBuilder();
            builder.Append(RunSeed).Append(',').Append(Side).Append(',').Append(swapDeckInputs ? 1 : 0);
            for (var wave = 2; wave <= 6; wave++) builder.Append(',').Append(reached[wave] ? 1 : 0);
            builder.Append(',').Append(DeathWave).Append(',').Append(FirstLeakWave).Append(',').Append(DeathReason)
                .Append(',').Append(Merges).Append(',').Append(RecruitStalls);
            for (var index = 0; index < 5; index++)
            {
                builder.Append(',').Append(heart[index]).Append(',').Append(resources[index]).Append(',').Append(recruits[index])
                    .Append(',').Append(basics[index]).Append(',').Append(heroes[index]).Append(',').Append(board[index])
                    .Append(',').Append(bench[index]).Append(',').Append(residual[index]);
            }
            builder.Append(',').Append(W6BossSpawned ? 1 : 0).Append(',').Append(W6HittableBasic).Append(',')
                .Append(W6HittableHero).Append(',').Append(W6SingleTargetDps.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append('"').Append((W6HeroDescriptors ?? string.Empty).Replace("\"", "\"\"")).Append('"');
            return builder.ToString();
        }
    }
}
