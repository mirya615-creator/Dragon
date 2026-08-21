using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DragonBound.Core
{
    /// <summary>One real-runtime Boss observation for a single side and seed.</summary>
    public sealed class JointBossCalibrationSample
    {
        internal JointBossCalibrationSample(
            int wave,
            float maxHitPoints,
            bool spawned,
            bool killed,
            bool reachedGoal,
            float spawnTimeSeconds,
            float resolutionTimeSeconds,
            float damage,
            float damageFirst3Seconds,
            float damageFirst5Seconds,
            int summonCount)
        {
            Wave = wave;
            MaxHitPoints = maxHitPoints;
            Spawned = spawned;
            Killed = killed;
            ReachedGoal = reachedGoal;
            SpawnTimeSeconds = spawnTimeSeconds;
            ResolutionTimeSeconds = resolutionTimeSeconds;
            Damage = damage;
            DamageFirst3Seconds = damageFirst3Seconds;
            DamageFirst5Seconds = damageFirst5Seconds;
            SummonCount = summonCount;
        }

        public int Wave { get; }
        public float MaxHitPoints { get; }
        public bool Spawned { get; }
        public bool Killed { get; }
        public bool ReachedGoal { get; }
        public float SpawnTimeSeconds { get; }
        public float ResolutionTimeSeconds { get; }
        public float TtkSeconds => Killed ? Math.Max(0f, ResolutionTimeSeconds - SpawnTimeSeconds) : -1f;
        public float Damage { get; }
        public float DamageFirst3Seconds { get; }
        public float DamageFirst5Seconds { get; }
        public int SummonCount { get; }
    }

    /// <summary>One side of one complete W1-W20 diagnostic run.</summary>
    public sealed class JointBalanceCalibrationSideSample
    {
        internal JointBalanceCalibrationSideSample(
            string side,
            int runEndWave,
            int deathWave,
            int firstLeakWave,
            bool reachedWaveTwenty,
            IReadOnlyList<JointBossCalibrationSample> bosses)
        {
            Side = side;
            RunEndWave = runEndWave;
            DeathWave = deathWave;
            FirstLeakWave = firstLeakWave;
            ReachedWaveTwenty = reachedWaveTwenty;
            Bosses = bosses;
        }

        public string Side { get; }
        public int RunEndWave { get; }
        public int DeathWave { get; }
        public int FirstLeakWave { get; }
        public bool ReachedWaveTwenty { get; }
        public IReadOnlyList<JointBossCalibrationSample> Bosses { get; }
    }

    /// <summary>Deterministic multi-seed output for the real W1-W20 pressure runtime.</summary>
    public sealed class JointBalanceCalibrationReport
    {
        private readonly List<JointBalanceCalibrationRunSample> runs =
            new List<JointBalanceCalibrationRunSample>();

        internal JointBalanceCalibrationReport(int firstRunSeed, int sampleCount, string buildId)
        {
            FirstRunSeed = firstRunSeed;
            SampleCount = sampleCount;
            BuildId = buildId ?? string.Empty;
        }

        public int FirstRunSeed { get; }
        public int SampleCount { get; }
        public string BuildId { get; }
        public IReadOnlyList<JointBalanceCalibrationRunSample> Runs => runs;

        internal void Add(JointBalanceCalibrationRunSample sample)
        {
            if (sample != null) runs.Add(sample);
        }

        public string ToCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("buildId,runSeed,side,runEndWave,deathWave,firstLeakWave,reachedWaveTwenty,wave,bossHp,bossSpawned,bossKilled,bossReachedGoal,spawnTimeSeconds,ttkSeconds,damage,damage0To3,damage0To5,summonCount");
            foreach (var run in runs)
            {
                foreach (var side in new[] { run.Player, run.AI })
                {
                    foreach (var boss in side.Bosses)
                    {
                        builder.Append(BuildId.Replace(",", ";")).Append(',')
                            .Append(run.RunSeed).Append(',').Append(side.Side).Append(',')
                            .Append(side.RunEndWave).Append(',').Append(side.DeathWave).Append(',')
                            .Append(side.FirstLeakWave).Append(',').Append(side.ReachedWaveTwenty ? 1 : 0).Append(',')
                            .Append(boss.Wave).Append(',').Append(Format(boss.MaxHitPoints)).Append(',')
                            .Append(boss.Spawned ? 1 : 0).Append(',').Append(boss.Killed ? 1 : 0).Append(',')
                            .Append(boss.ReachedGoal ? 1 : 0).Append(',').Append(Format(boss.SpawnTimeSeconds)).Append(',')
                            .Append(Format(boss.TtkSeconds)).Append(',').Append(Format(boss.Damage)).Append(',')
                            .Append(Format(boss.DamageFirst3Seconds)).Append(',').Append(Format(boss.DamageFirst5Seconds)).Append(',')
                            .Append(boss.SummonCount).AppendLine();
                    }
                }
            }

            return builder.ToString();
        }

        public string FormatSummary()
        {
            var builder = new StringBuilder();
            builder.AppendLine("JointBalanceCalibration Build=" + BuildId + " Seeds=" + SampleCount);
            foreach (var sideName in new[] { "Player", "AI" })
            {
                var sideRuns = new List<JointBalanceCalibrationSideSample>();
                foreach (var run in runs) sideRuns.Add(sideName == "Player" ? run.Player : run.AI);
                builder.AppendLine("[" + sideName + "] ReachedW20=" + Rate(Count(sideRuns, sample => sample.ReachedWaveTwenty), sideRuns.Count).ToString("P2", CultureInfo.InvariantCulture) +
                    " EndWaveP50=" + PercentileEndWave(sideRuns, .50).ToString("0.00", CultureInfo.InvariantCulture));
                foreach (var wave in new[] { 6, 12, 16, 20 })
                {
                    var bosses = GetBosses(sideRuns, wave);
                    builder.AppendLine("[" + sideName + "] W" + wave +
                        " Spawn=" + Rate(Count(bosses, boss => boss.Spawned), sideRuns.Count).ToString("P2", CultureInfo.InvariantCulture) +
                        " Kill=" + Rate(Count(bosses, boss => boss.Killed), sideRuns.Count).ToString("P2", CultureInfo.InvariantCulture) +
                        " Goal=" + Rate(Count(bosses, boss => boss.ReachedGoal), sideRuns.Count).ToString("P2", CultureInfo.InvariantCulture) +
                        " TTK_P50=" + PercentileTtk(bosses, .50).ToString("0.00", CultureInfo.InvariantCulture) + "s");
                }
            }

            return builder.ToString();
        }

        private static List<JointBossCalibrationSample> GetBosses(
            IReadOnlyList<JointBalanceCalibrationSideSample> sideRuns,
            int wave)
        {
            var result = new List<JointBossCalibrationSample>();
            foreach (var side in sideRuns)
            {
                foreach (var boss in side.Bosses)
                {
                    if (boss.Wave == wave) result.Add(boss);
                }
            }

            return result;
        }

        private static int Count<T>(IReadOnlyList<T> values, Func<T, bool> predicate)
        {
            var count = 0;
            foreach (var value in values) if (predicate(value)) count++;
            return count;
        }

        private static double PercentileTtk(IReadOnlyList<JointBossCalibrationSample> values, double percentile)
        {
            var ordered = new List<float>();
            foreach (var value in values) if (value.Killed) ordered.Add(value.TtkSeconds);
            return Percentile(ordered, percentile);
        }

        private static double PercentileEndWave(IReadOnlyList<JointBalanceCalibrationSideSample> values, double percentile)
        {
            var ordered = new List<float>();
            foreach (var value in values) ordered.Add(value.RunEndWave);
            return Percentile(ordered, percentile);
        }

        private static double Percentile(List<float> values, double percentile)
        {
            if (values.Count == 0) return -1d;
            values.Sort();
            var index = (int)Math.Round((values.Count - 1) * percentile, MidpointRounding.AwayFromZero);
            return values[Math.Max(0, Math.Min(values.Count - 1, index))];
        }

        private static double Rate(int numerator, int denominator)
        {
            return denominator == 0 ? 0d : numerator / (double)denominator;
        }

        private static string Format(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    public sealed class JointBalanceCalibrationRunSample
    {
        internal JointBalanceCalibrationRunSample(
            int runSeed,
            JointBalanceCalibrationSideSample player,
            JointBalanceCalibrationSideSample ai)
        {
            RunSeed = runSeed;
            Player = player;
            AI = ai;
        }

        public int RunSeed { get; }
        public JointBalanceCalibrationSideSample Player { get; }
        public JointBalanceCalibrationSideSample AI { get; }
    }

    internal sealed class JointBalanceCalibrationSideRun
    {
        private readonly JointBossCalibrationAccumulator[] bosses = CreateBosses();

        public void RecordLifecycle(EnemyLifecycleEvent value, float elapsedSeconds)
        {
            if (value.Archetype == EnemyArchetype.Swarm && value.SpawnWave == 20 && value.Kind == EnemyLifecycleEventKind.Spawned)
            {
                bosses[20].SummonCount++;
                return;
            }

            if (value.Archetype != EnemyArchetype.Boss || !IsBossWave(value.SpawnWave)) return;
            var boss = bosses[value.SpawnWave];
            if (value.Kind == EnemyLifecycleEventKind.Spawned)
            {
                boss.Spawned = true;
                boss.RuntimeId = value.RuntimeId;
                boss.MaxHitPoints = value.MaxHitPoints;
                boss.SpawnTimeSeconds = elapsedSeconds;
            }
            else if (string.Equals(boss.RuntimeId, value.RuntimeId, StringComparison.Ordinal) &&
                     (value.Kind == EnemyLifecycleEventKind.Killed || value.Kind == EnemyLifecycleEventKind.Leaked))
            {
                boss.Killed = value.Kind == EnemyLifecycleEventKind.Killed;
                boss.ReachedGoal = value.Kind == EnemyLifecycleEventKind.Leaked;
                boss.ResolutionTimeSeconds = elapsedSeconds;
            }
        }

        public void RecordCombat(CombatEvent value, float elapsedSeconds)
        {
            foreach (var wave in new[] { 6, 12, 16, 20 })
            {
                var boss = bosses[wave];
                if (!boss.Spawned || !string.Equals(boss.RuntimeId, value.TargetRuntimeId, StringComparison.Ordinal)) continue;
                boss.Damage += Math.Max(0f, value.Damage);
                var sinceSpawn = elapsedSeconds - boss.SpawnTimeSeconds;
                if (sinceSpawn <= 3f) boss.DamageFirst3Seconds += Math.Max(0f, value.Damage);
                if (sinceSpawn <= 5f) boss.DamageFirst5Seconds += Math.Max(0f, value.Damage);
                return;
            }
        }

        public JointBalanceCalibrationSideSample CreateSample(string side, CoreLoopSideRun run)
        {
            var samples = new List<JointBossCalibrationSample>();
            foreach (var wave in new[] { 6, 12, 16, 20 })
            {
                var boss = bosses[wave];
                samples.Add(new JointBossCalibrationSample(
                    wave,
                    boss.MaxHitPoints,
                    boss.Spawned,
                    boss.Killed,
                    boss.ReachedGoal,
                    boss.SpawnTimeSeconds,
                    boss.ResolutionTimeSeconds,
                    boss.Damage,
                    boss.DamageFirst3Seconds,
                    boss.DamageFirst5Seconds,
                    boss.SummonCount));
            }

            return new JointBalanceCalibrationSideSample(
                side,
                run.RunEndWave,
                run.DeathWave,
                run.FirstLeakWave,
                run.ReachedWaveTwenty,
                samples);
        }

        private static JointBossCalibrationAccumulator[] CreateBosses()
        {
            var result = new JointBossCalibrationAccumulator[21];
            for (var index = 0; index < result.Length; index++) result[index] = new JointBossCalibrationAccumulator();
            return result;
        }

        private static bool IsBossWave(int wave)
        {
            return wave == 6 || wave == 12 || wave == 16 || wave == 20;
        }
    }

    internal sealed class JointBossCalibrationAccumulator
    {
        public string RuntimeId = string.Empty;
        public bool Spawned;
        public bool Killed;
        public bool ReachedGoal;
        public float MaxHitPoints;
        public float SpawnTimeSeconds = -1f;
        public float ResolutionTimeSeconds = -1f;
        public float Damage;
        public float DamageFirst3Seconds;
        public float DamageFirst5Seconds;
        public int SummonCount;
    }
}
