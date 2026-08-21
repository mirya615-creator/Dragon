using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    /// <summary>
    /// Per-run telemetry for the real W1-W6 schedule. This is an analysis container only;
    /// it never supplies production Boss HP or changes combat decisions.
    /// </summary>
    public sealed class W6BareCalibrationRun
    {
        public W6BareCalibrationRun()
        {
            Player = new W6BareCalibrationSideRun();
            AI = new W6BareCalibrationSideRun();
        }

        public W6BareCalibrationSideRun Player { get; }
        public W6BareCalibrationSideRun AI { get; }

        internal void RecordLifecycle(
            TeamSide side,
            EnemyLifecycleEvent value,
            float elapsedTime,
            BoardRecruitDestination destination)
        {
            GetSide(side).RecordLifecycle(value, elapsedTime, destination);
        }

        internal void RecordCombat(TeamSide side, CombatEvent value, float elapsedTime)
        {
            GetSide(side).RecordCombat(value, elapsedTime);
        }

        internal void RecordCast(TeamSide side, SoulChainCastEvent value, TwentyWavePressureRuntime runtime)
        {
            GetSide(side).RecordCast(value, runtime == null ? 0f : runtime.ElapsedRunTime);
        }

        internal void SetResiduals(TeamSide side, int[] residuals)
        {
            GetSide(side).SetResiduals(residuals);
        }

        private W6BareCalibrationSideRun GetSide(TeamSide side)
        {
            return side == TeamSide.Player ? Player : AI;
        }
    }

    public sealed class W6BareCalibrationSideRun
    {
        private readonly int[] residualAtNextWaveStart = new int[7];

        public bool BossSpawned { get; private set; }
        public string BossRuntimeId { get; private set; } = string.Empty;
        public float BossMaxHitPoints { get; private set; }
        public float BossSpawnTimeSeconds { get; private set; } = -1f;
        public float BossSpawnX { get; private set; }
        public float BossSpawnY { get; private set; }
        public float BossSpawnPathProgress { get; private set; }
        public float BossKillTimeSeconds { get; private set; } = -1f;
        public float BossLeakTimeSeconds { get; private set; } = -1f;
        public bool BossKilled => BossKillTimeSeconds >= 0f;
        public bool BossLeaked => BossLeakTimeSeconds >= 0f;
        public float BossTtkSeconds => BossKilled ? BossKillTimeSeconds - BossSpawnTimeSeconds : -1f;
        public int BasicCountAtBossSpawn { get; private set; }
        public int HeroCountAtBossSpawn { get; private set; }
        public int HeroLevelSumAtBossSpawn { get; private set; }
        public int BoardOccupiedAtBossSpawn { get; private set; }
        public int BoardOpenCellsAtBossSpawn { get; private set; }
        public int BenchOccupiedAtBossSpawn { get; private set; }
        /// <summary>
        /// Position-independent configured single-target potential of deployed Basic units and
        /// completed Hero pairs at the W6 Boss spawn snapshot.
        /// </summary>
        public float BoardQualityAtBossSpawn { get; private set; }
        /// <summary>Documented proxy: deployed Basic count + active Hero level sum.</summary>
        public int CombatPowerProxyAtBossSpawn => BasicCountAtBossSpawn + HeroLevelSumAtBossSpawn;
        public float BossDamageTotal { get; private set; }
        public float BasicDamageToBoss { get; private set; }
        public float HeroDamageToBoss { get; private set; }
        public float OtherDamageToBoss { get; private set; }
        public int BossDamageEventCount { get; private set; }
        public float BossDamageFirst3Seconds { get; private set; }
        public float BasicDamageFirst3Seconds { get; private set; }
        public float HeroDamageFirst3Seconds { get; private set; }
        public int BossDamageEventsFirst3Seconds { get; private set; }
        public float BossDamageFirst5Seconds { get; private set; }
        public float BasicDamageFirst5Seconds { get; private set; }
        public float HeroDamageFirst5Seconds { get; private set; }
        public int BossDamageEventsFirst5Seconds { get; private set; }
        public int SoulChainCastsStarted { get; private set; }
        public int SoulChainCastsSucceeded { get; private set; }
        public int SoulChainCastsFailed { get; private set; }
        public float SoulChainControlUnitSeconds { get; private set; }
        public int SecondCastStarted { get; private set; }
        public int SecondCastSucceeded { get; private set; }
        public int SecondCastEnded { get; private set; }
        public bool BossAliveAtW7Start { get; private set; }
        public float EstimatedSingleTargetDps { get; private set; }
        public int HittableBasicCount { get; private set; }
        public int HittableHeroCount { get; private set; }
        public List<string> HittableHeroDescriptors { get; } = new List<string>();
        public List<string> ReachTelemetryDescriptors { get; } = new List<string>();
        public int HeartAtW6End { get; private set; } = -1;
        public bool InstantDefeatAtW6End { get; private set; }
        public int MatchEndWave { get; private set; } = -1;
        public bool QualifiedBaseline => BossSpawned && (BasicCountAtBossSpawn + HeroCountAtBossSpawn) > 0;
        public IReadOnlyList<int> ResidualAtNextWaveStart => residualAtNextWaveStart;

        internal void RecordLifecycle(
            EnemyLifecycleEvent value,
            float elapsedTime,
            BoardRecruitDestination destination)
        {
            if (value.Archetype != EnemyArchetype.Boss || value.SpawnWave != 6)
            {
                return;
            }

            if (value.Kind == EnemyLifecycleEventKind.Spawned && !BossSpawned)
            {
                BossSpawned = true;
                BossRuntimeId = value.RuntimeId ?? string.Empty;
                BossMaxHitPoints = value.MaxHitPoints;
                BossSpawnTimeSeconds = elapsedTime;
                BossSpawnPathProgress = value.PathProgress;
                CaptureBoardSnapshot(destination);
            }
            else if (value.Kind == EnemyLifecycleEventKind.Killed &&
                     string.Equals(BossRuntimeId, value.RuntimeId, StringComparison.Ordinal))
            {
                BossKillTimeSeconds = elapsedTime;
            }
            else if (value.Kind == EnemyLifecycleEventKind.Leaked &&
                     string.Equals(BossRuntimeId, value.RuntimeId, StringComparison.Ordinal))
            {
                BossLeakTimeSeconds = elapsedTime;
            }
        }

        internal void RecordCombat(CombatEvent value, float elapsedTime)
        {
            if (!BossSpawned || !string.Equals(BossRuntimeId, value.TargetRuntimeId, StringComparison.Ordinal) ||
                value.Damage <= 0f)
            {
                return;
            }

            BossDamageTotal += value.Damage;
            BossDamageEventCount++;
            switch (value.DamageOwnerKind)
            {
                case CombatDamageOwnerKind.BasicUnit:
                    BasicDamageToBoss += value.Damage;
                    break;
                case CombatDamageOwnerKind.Hero:
                    HeroDamageToBoss += value.Damage;
                    break;
                default:
                    OtherDamageToBoss += value.Damage;
                    break;
            }

            var secondsSinceSpawn = elapsedTime - BossSpawnTimeSeconds;
            if (secondsSinceSpawn >= -0.0001f && secondsSinceSpawn <= 3.0001f)
            {
                BossDamageFirst3Seconds += value.Damage;
                BossDamageEventsFirst3Seconds++;
                if (value.DamageOwnerKind == CombatDamageOwnerKind.BasicUnit)
                {
                    BasicDamageFirst3Seconds += value.Damage;
                }
                else if (value.DamageOwnerKind == CombatDamageOwnerKind.Hero)
                {
                    HeroDamageFirst3Seconds += value.Damage;
                }
            }

            if (secondsSinceSpawn >= -0.0001f && secondsSinceSpawn <= 5.0001f)
            {
                BossDamageFirst5Seconds += value.Damage;
                BossDamageEventsFirst5Seconds++;
                if (value.DamageOwnerKind == CombatDamageOwnerKind.BasicUnit)
                {
                    BasicDamageFirst5Seconds += value.Damage;
                }
                else if (value.DamageOwnerKind == CombatDamageOwnerKind.Hero)
                {
                    HeroDamageFirst5Seconds += value.Damage;
                }
            }
        }

        internal void RecordCast(SoulChainCastEvent value, float elapsedTime)
        {
            if (value.Kind == SoulChainCastEventKind.CastStarted)
            {
                SoulChainCastsStarted++;
                if (value.CastNumber == 2) SecondCastStarted++;
            }
            else if (value.Kind == SoulChainCastEventKind.EffectApplied)
            {
                SoulChainCastsSucceeded++;
                SoulChainControlUnitSeconds += value.ControlUnitSeconds;
                if (value.CastNumber == 2) SecondCastSucceeded++;
            }
            else if (value.Kind == SoulChainCastEventKind.CastFailed)
            {
                SoulChainCastsFailed++;
            }
            else if (value.Kind == SoulChainCastEventKind.EffectEnded && value.CastNumber == 2)
            {
                SecondCastEnded++;
            }
        }

        internal void SetResiduals(int[] residuals)
        {
            if (residuals == null) return;
            for (var wave = 1; wave <= 6 && wave < residuals.Length; wave++)
            {
                residualAtNextWaveStart[wave] = Math.Max(0, residuals[wave]);
            }
        }

        internal void RecordW6End(int heart, bool instantDefeat, int matchEndWave)
        {
            if (HeartAtW6End >= 0) return;
            HeartAtW6End = Math.Max(0, heart);
            InstantDefeatAtW6End = instantDefeat;
            MatchEndWave = Math.Max(1, matchEndWave);
        }

        internal void RecordW7Start(bool bossAlive)
        {
            BossAliveAtW7Start = bossAlive;
        }

        internal void RecordHittableSnapshot(EnemyRuntime boss, TeamSide side, BoardRecruitDestination destination)
        {
            if (boss == null || destination == null) return;
            BossSpawnX = boss.CombatPosition.X;
            BossSpawnY = boss.CombatPosition.Y;
            EstimatedSingleTargetDps = 0f;
            HittableBasicCount = 0;
            HittableHeroCount = 0;
            HittableHeroDescriptors.Clear();
            ReachTelemetryDescriptors.Clear();
            foreach (var unit in destination.GetDeployedUnits())
            {
                var stats = BasicUnitCatalog.GetStats(unit.Card.ConfigId, unit.Card.Level);
                var distance = Distance(unit.CombatPosition, boss.CombatPosition);
                var hittable = distance <= stats.RangeCells + 0.0001f;
                ReachTelemetryDescriptors.Add(
                    "Basic|" + unit.Card.RuntimeId + "|Pos=" + FormatPoint(unit.CombatPosition) +
                    "|Distance=" + distance.ToString("0.000", CultureInfo.InvariantCulture) +
                    "|Range=" + stats.RangeCells.ToString("0.000", CultureInfo.InvariantCulture) +
                    "|Hittable=" + (hittable ? "1" : "0") +
                    "|Dps=" + (stats.Attack * stats.AttackSpeed).ToString("0.000", CultureInfo.InvariantCulture));
                if (hittable)
                {
                    HittableBasicCount++;
                    EstimatedSingleTargetDps += stats.Attack * stats.AttackSpeed;
                }
            }

            foreach (var hero in destination.GetActiveHeroPairs())
            {
                var combat = hero.PairLink.CombatProxy;
                var range = combat.RangeCells;
                var distance = Distance(hero.CombatPosition, boss.CombatPosition);
                var hittable = distance <= range + 0.0001f;
                HittableHeroDescriptors.Add(
                    combat.Definition.Id + "|Lv" + combat.Level + "|Range" + range.ToString("0.00", CultureInfo.InvariantCulture) +
                    "|Type" + combat.Definition.AttackType + "|Hittable" + (hittable ? "1" : "0"));
                ReachTelemetryDescriptors.Add(
                    "Hero|" + combat.Definition.Id + "|Lv=" + combat.Level + "|Pos=" + FormatPoint(hero.CombatPosition) +
                    "|Distance=" + distance.ToString("0.000", CultureInfo.InvariantCulture) +
                    "|Range=" + range.ToString("0.000", CultureInfo.InvariantCulture) +
                    "|Hittable=" + (hittable ? "1" : "0") +
                    "|Dps=" + (combat.Attack * combat.AttackSpeed).ToString("0.000", CultureInfo.InvariantCulture));
                if (hittable)
                {
                    HittableHeroCount++;
                    EstimatedSingleTargetDps += combat.Attack * combat.AttackSpeed;
                }
            }
        }

        private static float Distance(CombatPoint first, CombatPoint second)
        {
            return (float)Math.Sqrt(first.DistanceSquared(second));
        }

        private static string FormatPoint(CombatPoint point)
        {
            return point.X.ToString("0.000", CultureInfo.InvariantCulture) + ":" +
                   point.Y.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private void CaptureBoardSnapshot(BoardRecruitDestination destination)
        {
            if (destination == null) return;
            BoardQualityAtBossSpawn = 0f;
            foreach (var unit in destination.GetDeployedUnits())
            {
                if (unit.Card.Kind != RecruitItemKind.BasicUnit) continue;
                BasicCountAtBossSpawn++;
                var stats = BasicUnitCatalog.GetStats(unit.Card.ConfigId, unit.Card.Level);
                BoardQualityAtBossSpawn += stats.Attack * stats.AttackSpeed;
            }

            var heroes = destination.GetActiveHeroPairs();
            HeroCountAtBossSpawn = heroes.Count;
            foreach (var hero in heroes)
            {
                HeroLevelSumAtBossSpawn += hero.PairLink.CombatProxy.Level;
                BoardQualityAtBossSpawn += hero.PairLink.CombatProxy.Attack * hero.PairLink.CombatProxy.AttackSpeed;
            }

            BoardOccupiedAtBossSpawn = destination.DeployedCount;
            BoardOpenCellsAtBossSpawn = destination.Board.UnlockedBattleCellCount;
            BenchOccupiedAtBossSpawn = destination.CampCount;
        }
    }

    public sealed class W6BareCalibrationAggregate
    {
        private readonly List<W6BareCalibrationSideRun> samples = new List<W6BareCalibrationSideRun>();

        public int SampleCount => samples.Count;
        public IReadOnlyList<W6BareCalibrationSideRun> Samples => samples;
        public int BossSpawnCount => Count(sample => sample.BossSpawned);
        public int QualifiedBaselineCount => Count(sample => sample.QualifiedBaseline);
        public int BossKillCount => Count(sample => sample.BossKilled);
        public int BossLeakCount => Count(sample => sample.BossLeaked);
        public int BossUnresolvedCount => Math.Max(0, BossSpawnCount - BossKillCount - BossLeakCount);
        public double BossSpawnRate => Rate(BossSpawnCount, SampleCount);
        public double QualifiedBaselineRate => Rate(QualifiedBaselineCount, SampleCount);
        public double BossKillRate => Rate(BossKillCount, SampleCount);
        public double BossLeakRate => Rate(BossLeakCount, SampleCount);
        public double AverageBossTtkSeconds => Average(Values(sample => sample.BossTtkSeconds, true, true));
        public double AverageBossDamage => Average(Values(sample => sample.BossDamageTotal, false));
        public double AverageBasicDamage => Average(Values(sample => sample.BasicDamageToBoss, false));
        public double AverageHeroDamage => Average(Values(sample => sample.HeroDamageToBoss, false));
        public double AverageBossDamageFirst3Seconds => Average(Values(sample => sample.BossDamageFirst3Seconds, false));
        public double AverageBasicDamageFirst3Seconds => Average(Values(sample => sample.BasicDamageFirst3Seconds, false));
        public double AverageHeroDamageFirst3Seconds => Average(Values(sample => sample.HeroDamageFirst3Seconds, false));
        public double AverageBossDamageFirst5Seconds => Average(Values(sample => sample.BossDamageFirst5Seconds, false));
        public double AverageBasicDamageFirst5Seconds => Average(Values(sample => sample.BasicDamageFirst5Seconds, false));
        public double AverageHeroDamageFirst5Seconds => Average(Values(sample => sample.HeroDamageFirst5Seconds, false));
        public double AverageBossDamageFirst3SecondsSpawned => Average(Values(sample => sample.BossDamageFirst3Seconds, false, true));
        public double AverageBasicDamageFirst3SecondsSpawned => Average(Values(sample => sample.BasicDamageFirst3Seconds, false, true));
        public double AverageHeroDamageFirst3SecondsSpawned => Average(Values(sample => sample.HeroDamageFirst3Seconds, false, true));
        public double AverageBossDamageFirst5SecondsSpawned => Average(Values(sample => sample.BossDamageFirst5Seconds, false, true));
        public double AverageBasicDamageFirst5SecondsSpawned => Average(Values(sample => sample.BasicDamageFirst5Seconds, false, true));
        public double AverageHeroDamageFirst5SecondsSpawned => Average(Values(sample => sample.HeroDamageFirst5Seconds, false, true));
        public double AverageControlUnitSeconds => Average(Values(sample => sample.SoulChainControlUnitSeconds, false));
        public double AverageBasicAtBossSpawn => Average(Values(sample => sample.BasicCountAtBossSpawn, false, true));
        public double AverageHeroAtBossSpawn => Average(Values(sample => sample.HeroCountAtBossSpawn, false, true));
        public double AverageHeartAtW6End => Average(Values(sample => sample.HeartAtW6End, true));
        public double AverageSoulChainCastsStarted => Average(Values(sample => sample.SoulChainCastsStarted, false));
        public double AverageSoulChainCastsSucceeded => Average(Values(sample => sample.SoulChainCastsSucceeded, false));
        public double AverageSoulChainCastsFailed => Average(Values(sample => sample.SoulChainCastsFailed, false));

        internal void Add(W6BareCalibrationSideRun sample)
        {
            if (sample != null) samples.Add(sample);
        }

        public double PercentileBossTtk(double percentile)
        {
            return Percentile(Values(sample => sample.BossTtkSeconds, true, true), percentile);
        }

        public double AverageResidualAtNextWaveStart(int wave)
        {
            if (wave < 1 || wave > 6) throw new ArgumentOutOfRangeException(nameof(wave));
            var values = new List<double>();
            foreach (var sample in samples) values.Add(sample.ResidualAtNextWaveStart[wave]);
            return Average(values);
        }

        public double BossAliveAtW7StartRate => Rate(Count(sample => sample.BossAliveAtW7Start), SampleCount);

        public string FormatQualityStrata()
        {
            var qualified = new List<W6BareCalibrationSideRun>();
            foreach (var sample in samples) if (sample.QualifiedBaseline) qualified.Add(sample);
            qualified.Sort((left, right) => left.CombatPowerProxyAtBossSpawn.CompareTo(right.CombatPowerProxyAtBossSpawn));
            if (qualified.Count == 0) return "NoQualifiedSamples";
            var firstEnd = Math.Max(1, qualified.Count / 3);
            var secondEnd = Math.Max(firstEnd + 1, (qualified.Count * 2) / 3);
            secondEnd = Math.Min(qualified.Count, secondEnd);
            return "Low=" + FormatStratum(qualified, 0, firstEnd) +
                   "; Normal=" + FormatStratum(qualified, firstEnd, secondEnd) +
                   "; High=" + FormatStratum(qualified, secondEnd, qualified.Count);
        }

        public string Format(string label)
        {
            var builder = new StringBuilder();
            builder.Append('[').Append(label).Append("] Samples=").Append(SampleCount)
                .Append(" BossSpawn=").Append(BossSpawnRate.ToString("P2", CultureInfo.InvariantCulture))
                .Append(" Qualified=").Append(QualifiedBaselineRate.ToString("P2", CultureInfo.InvariantCulture))
                .Append(" Killed=").Append(BossKillRate.ToString("P2", CultureInfo.InvariantCulture))
                .Append(" Leaked=").Append(BossLeakRate.ToString("P2", CultureInfo.InvariantCulture))
                .Append(" Unresolved=").Append(BossUnresolvedCount)
                .Append(" BossTTK Mean=").Append(AverageBossTtkSeconds.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" P10=").Append(PercentileBossTtk(0.10).ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" P25=").Append(PercentileBossTtk(0.25).ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" P50=").Append(PercentileBossTtk(0.50).ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" P75=").Append(PercentileBossTtk(0.75).ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" P90=").Append(PercentileBossTtk(0.90).ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" DamageBasic=").Append(AverageBasicDamage.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" DamageHero=").Append(AverageHeroDamage.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" DamageTotal=").Append(AverageBossDamage.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" Damage0-3=").Append(AverageBossDamageFirst3Seconds.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" Damage0-5=").Append(AverageBossDamageFirst5Seconds.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" ControlUnitSeconds=").Append(AverageControlUnitSeconds.ToString("0.00", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private int Count(Func<W6BareCalibrationSideRun, bool> predicate)
        {
            var count = 0;
            foreach (var sample in samples) if (predicate(sample)) count++;
            return count;
        }

        private static string FormatStratum(List<W6BareCalibrationSideRun> values, int start, int end)
        {
            if (start >= end) return "n=0";
            var ttks = new List<double>();
            var kills = 0;
            var proxyTotal = 0;
            for (var index = start; index < end; index++)
            {
                var sample = values[index];
                proxyTotal += sample.CombatPowerProxyAtBossSpawn;
                if (sample.BossKilled)
                {
                    kills++;
                    ttks.Add(sample.BossTtkSeconds);
                }
            }

            return "n=" + (end - start).ToString(CultureInfo.InvariantCulture) +
                   ",Proxy=" + (proxyTotal / (double)(end - start)).ToString("0.00", CultureInfo.InvariantCulture) +
                   ",Kill=" + Rate(kills, end - start).ToString("P2", CultureInfo.InvariantCulture) +
                   ",TTKP50=" + Percentile(ttks, 0.50).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private List<double> Values(
            Func<W6BareCalibrationSideRun, float> selector,
            bool onlyPositive,
            bool qualifiedOnly = false)
        {
            var values = new List<double>();
            foreach (var sample in samples)
            {
                var value = selector(sample);
                if ((!onlyPositive || value >= 0f) && (!qualifiedOnly || sample.QualifiedBaseline)) values.Add(value);
            }
            return values;
        }

        private static double Rate(int numerator, int denominator) => denominator == 0 ? 0d : numerator / (double)denominator;
        private static double Average(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0d;
            double total = 0d;
            foreach (var value in values) total += value;
            return total / values.Count;
        }

        private static double Percentile(IReadOnlyList<double> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0d;
            var ordered = new List<double>(values);
            ordered.Sort();
            var position = (ordered.Count - 1) * Math.Max(0d, Math.Min(1d, percentile));
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            return lower == upper ? ordered[lower] : ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower);
        }
    }

    public sealed class W6BareFullScheduleCalibrationReport
    {
        private readonly W6BareCalibrationAggregate player = new W6BareCalibrationAggregate();
        private readonly W6BareCalibrationAggregate ai = new W6BareCalibrationAggregate();

        public W6BareFullScheduleCalibrationReport(int firstRunSeed, int sampleCount, float bossMaxHitPoints)
        {
            FirstRunSeed = firstRunSeed;
            SampleCount = sampleCount;
            BossMaxHitPoints = bossMaxHitPoints;
        }

        public int FirstRunSeed { get; }
        public int SampleCount { get; }
        public float BossMaxHitPoints { get; }
        public W6BareCalibrationAggregate Player => player;
        public W6BareCalibrationAggregate AI => ai;

        internal void Add(int runSeed, CoreLoopRunResult result)
        {
            if (result == null) return;
            result.W6Calibration.SetResiduals(TeamSide.Player, result.PlayerEnemyPressure.ResidualAtNextWaveStart);
            result.W6Calibration.SetResiduals(TeamSide.AI, result.AiEnemyPressure.ResidualAtNextWaveStart);
            player.Add(result.W6Calibration.Player);
            ai.Add(result.W6Calibration.AI);
        }

        public string FormatReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine("W6 Bare Full-Schedule Calibration");
            builder.AppendLine("RealSchedule=W1-W6; Items=Disabled; Runes=Disabled; BossHP=AnalysisInputOnly; FormalBossHP=PENDING");
            builder.AppendLine("BossMaxHitPoints=" + BossMaxHitPoints.ToString("0.00", CultureInfo.InvariantCulture));
            builder.AppendLine(player.Format("Player"));
            builder.AppendLine(ai.Format("AI"));
            return builder.ToString();
        }

        public string ToCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("runSeed,side,bossHp,bossSpawned,qualifiedBaseline,bossKilled,bossLeaked,bossAliveAtW7Start,bossTtkSeconds,bossSpawnPathProgress,bossSpawnX,bossSpawnY,basicAtSpawn,heroAtSpawn,heroLevelSum,boardOccupied,boardOpen,benchOccupied,powerProxy,estimatedSingleTargetDps,hittableBasicCount,hittableHeroCount,reachTelemetry,bossDamage,basicDamage,heroDamage,otherDamage,bossDamage0To3,basicDamage0To3,heroDamage0To3,bossDamageEvents0To3,bossDamage0To5,basicDamage0To5,heroDamage0To5,bossDamageEvents0To5,castsStarted,castsSucceeded,castsFailed,controlUnitSeconds,heartAtW6End,instantDefeatAtW6End,matchEndWave,residualW1,residualW2,residualW3,residualW4,residualW5,residualW6");
            AppendCsvSamples(builder, player.Samples, "Player");
            AppendCsvSamples(builder, ai.Samples, "AI");
            return builder.ToString();
        }

        private void AppendCsvSamples(StringBuilder builder, IReadOnlyList<W6BareCalibrationSideRun> samples, string side)
        {
            for (var index = 0; index < samples.Count; index++)
            {
                var value = samples[index];
                builder.Append(FirstRunSeed + index).Append(',').Append(side).Append(',')
                    .Append(BossMaxHitPoints.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BossSpawned ? 1 : 0).Append(',').Append(value.QualifiedBaseline ? 1 : 0).Append(',')
                    .Append(value.BossKilled ? 1 : 0).Append(',').Append(value.BossLeaked ? 1 : 0).Append(',')
                    .Append(value.BossAliveAtW7Start ? 1 : 0).Append(',')
                    .Append(value.BossTtkSeconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BossSpawnPathProgress.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BossSpawnX.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BossSpawnY.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BasicCountAtBossSpawn).Append(',').Append(value.HeroCountAtBossSpawn).Append(',')
                    .Append(value.HeroLevelSumAtBossSpawn).Append(',').Append(value.BoardOccupiedAtBossSpawn).Append(',')
                    .Append(value.BoardOpenCellsAtBossSpawn).Append(',').Append(value.BenchOccupiedAtBossSpawn).Append(',')
                    .Append(value.CombatPowerProxyAtBossSpawn).Append(',')
                    .Append(value.EstimatedSingleTargetDps.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.HittableBasicCount).Append(',').Append(value.HittableHeroCount).Append(',')
                    .Append(EscapeCsv(string.Join(";", value.ReachTelemetryDescriptors.ToArray()))).Append(',')
                    .Append(value.BossDamageTotal.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BasicDamageToBoss.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.HeroDamageToBoss.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.OtherDamageToBoss.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BossDamageFirst3Seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BasicDamageFirst3Seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.HeroDamageFirst3Seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BossDamageEventsFirst3Seconds).Append(',')
                    .Append(value.BossDamageFirst5Seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BasicDamageFirst5Seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.HeroDamageFirst5Seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.BossDamageEventsFirst5Seconds).Append(',')
                    .Append(value.SoulChainCastsStarted).Append(',').Append(value.SoulChainCastsSucceeded).Append(',')
                    .Append(value.SoulChainCastsFailed).Append(',')
                    .Append(value.SoulChainControlUnitSeconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(value.HeartAtW6End).Append(',').Append(value.InstantDefeatAtW6End ? 1 : 0).Append(',')
                    .Append(value.MatchEndWave);
                for (var wave = 1; wave <= 6; wave++) builder.Append(',').Append(value.ResidualAtNextWaveStart[wave]);
                builder.AppendLine();
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
