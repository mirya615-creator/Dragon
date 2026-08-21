using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DragonBound.Combat;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    /// <summary>Diagnostic-only W12 calibration sample; it never supplies Production HP.</summary>
    public sealed class W12BuildEnvelopeCalibrationRun
    {
        public W12BuildEnvelopeCalibrationRun()
        {
            Player = new W12BuildEnvelopeSideRun();
            AI = new W12BuildEnvelopeSideRun();
        }

        public W12BuildEnvelopeSideRun Player { get; }
        public W12BuildEnvelopeSideRun AI { get; }

        internal void RecordDirectSetup(BoardRecruitDestination player, BoardRecruitDestination ai)
        {
            Player.RecordDirectSetup(player);
            AI.RecordDirectSetup(ai);
        }

        internal W12BuildEnvelopeSideRun GetSide(TeamSide side)
        {
            return side == TeamSide.Player ? Player : AI;
        }
    }

    public sealed class W12BuildEnvelopeSideRun
    {
        public int DirectSetupBoardUnits { get; private set; }
        public int DirectSetupHeroCount { get; private set; }
        public int DirectSetupBenchUnits { get; private set; }
        public bool BossSpawned { get; private set; }
        public bool BossKilled { get; private set; }
        public bool BossReachedGoal { get; private set; }
        public bool BossResidualAtW13 { get; private set; }
        public string BossRuntimeId { get; private set; } = string.Empty;
        public float BossMaxHitPoints { get; private set; }
        public float BossSpawnTimeSeconds { get; private set; } = -1f;
        public float BossKillTimeSeconds { get; private set; } = -1f;
        public float BossGoalTimeSeconds { get; private set; } = -1f;
        public float BossTtkSeconds => BossKilled ? BossKillTimeSeconds - BossSpawnTimeSeconds : -1f;
        public float BossDamageTotal { get; private set; }
        public float BossDamageFirst3Seconds { get; private set; }
        public float BossDamageFirst5Seconds { get; private set; }
        public float BasicDamageToBoss { get; private set; }
        public float HeroDamageToBoss { get; private set; }
        public float OtherDamageToBoss { get; private set; }
        public int BossDamageEventCount { get; private set; }
        public float ShieldDamage { get; private set; }
        public float BodyDamage { get; private set; }
        public int StormCallStarted { get; private set; }
        public int StormCallSucceeded { get; private set; }
        public int StormCallFailed { get; private set; }
        public int FirstCastAffectedNormalCount { get; private set; }
        public int SecondCastAffectedNormalCount { get; private set; }
        public int FirstCastSucceeded { get; private set; }
        public int SecondCastSucceeded { get; private set; }
        public int ItemActivations { get; private set; }
        public int W13Residual { get; private set; }
        public int MatchEndWave { get; private set; } = -1;
        public string MatchEndReason { get; private set; } = "NotRecorded";

        internal void RecordLifecycle(EnemyLifecycleEvent value, float elapsedSeconds)
        {
            if (value.Archetype != EnemyArchetype.Boss || value.SpawnWave != 12)
            {
                return;
            }

            if (value.Kind == EnemyLifecycleEventKind.Spawned && !BossSpawned)
            {
                BossSpawned = true;
                BossRuntimeId = value.RuntimeId ?? string.Empty;
                BossMaxHitPoints = value.MaxHitPoints;
                BossSpawnTimeSeconds = elapsedSeconds;
            }
            else if (value.Kind == EnemyLifecycleEventKind.Killed)
            {
                BossKilled = true;
                BossKillTimeSeconds = elapsedSeconds;
            }
            else if (value.Kind == EnemyLifecycleEventKind.Leaked)
            {
                BossReachedGoal = true;
                BossGoalTimeSeconds = elapsedSeconds;
            }
        }

        internal void RecordCombat(CombatEvent value, float elapsedSeconds)
        {
            if (!BossSpawned)
            {
                return;
            }

            ShieldDamage += value.ShieldDamage;
            BodyDamage += value.HealthDamage;
            if (!string.Equals(value.TargetRuntimeId, BossRuntimeId, StringComparison.Ordinal))
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

            var sinceSpawn = elapsedSeconds - BossSpawnTimeSeconds;
            if (sinceSpawn >= -0.0001f && sinceSpawn <= 3.0001f)
            {
                BossDamageFirst3Seconds += value.Damage;
            }

            if (sinceSpawn >= -0.0001f && sinceSpawn <= 5.0001f)
            {
                BossDamageFirst5Seconds += value.Damage;
            }
        }

        internal void RecordCast(StormcallerCastEvent value)
        {
            switch (value.Kind)
            {
                case StormcallerCastEventKind.CastStarted:
                    StormCallStarted++;
                    break;
                case StormcallerCastEventKind.EffectApplied:
                    StormCallSucceeded++;
                    if (value.CastNumber == 1)
                    {
                        FirstCastSucceeded++;
                        FirstCastAffectedNormalCount = value.AffectedCount;
                    }
                    else if (value.CastNumber == 2)
                    {
                        SecondCastSucceeded++;
                        SecondCastAffectedNormalCount = value.AffectedCount;
                    }
                    break;
                case StormcallerCastEventKind.CastFailed:
                    StormCallFailed++;
                    break;
            }
        }

        internal void RecordItemActivation(bool activated)
        {
            if (activated) ItemActivations++;
        }

        internal void RecordDirectSetup(BoardRecruitDestination destination)
        {
            if (destination == null)
            {
                return;
            }

            DirectSetupBoardUnits = destination.DeployedCount;
            DirectSetupHeroCount = destination.ActivePairLinkCount;
            DirectSetupBenchUnits = destination.CampCount;
        }

        internal void RecordW13Residual(int residual)
        {
            W13Residual = Math.Max(0, residual);
            BossResidualAtW13 = BossSpawned && !BossKilled && !BossReachedGoal;
        }

        internal void RecordMatchEnd(int wave, string reason)
        {
            MatchEndWave = wave;
            MatchEndReason = reason ?? "Unknown";
        }
    }

    public sealed class W12BuildEnvelopeCalibrationReport
    {
        private readonly List<W12BuildEnvelopeCalibrationRun> runs = new List<W12BuildEnvelopeCalibrationRun>();

        public W12BuildEnvelopeCalibrationReport(
            int firstRunSeed,
            int sampleCount,
            float bossMaxHitPoints,
            string cohort = "EndToEnd")
        {
            FirstRunSeed = firstRunSeed;
            SampleCount = sampleCount;
            BossMaxHitPoints = bossMaxHitPoints;
            Cohort = cohort ?? "Unknown";
            Player = new W12BuildEnvelopeCalibrationAggregate();
            AI = new W12BuildEnvelopeCalibrationAggregate();
        }

        public int FirstRunSeed { get; }
        public int SampleCount { get; }
        public float BossMaxHitPoints { get; }
        public string Cohort { get; }
        public W12BuildEnvelopeCalibrationAggregate Player { get; }
        public W12BuildEnvelopeCalibrationAggregate AI { get; }
        public IReadOnlyList<W12BuildEnvelopeCalibrationRun> Runs => runs;

        internal void Add(W12BuildEnvelopeCalibrationRun run)
        {
            runs.Add(run);
            Player.Add(run.Player);
            AI.Add(run.AI);
        }

        public string ToCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("candidateHp,cohort,runSeed,side,bossHp,bossSpawned,bossKilled,bossReachedGoal,bossResidualAtW13,bossTtkSeconds,bossDamage,bossDamage0To3,bossDamage0To5,basicDamage,heroDamage,otherDamage,shieldDamage,bodyDamage,stormCallStarted,stormCallSucceeded,stormCallFailed,firstCastAffected,secondCastAffected,itemActivations,directBoardUnits,directHeroCount,directBenchUnits,w13Residual,matchEndWave,matchEndReason");
            for (var index = 0; index < runs.Count; index++)
            {
                AppendCsv(builder, BossMaxHitPoints, Cohort, FirstRunSeed + index, "Player", runs[index].Player);
                AppendCsv(builder, BossMaxHitPoints, Cohort, FirstRunSeed + index, "AI", runs[index].AI);
            }

            return builder.ToString();
        }

        private static void AppendCsv(StringBuilder builder, float candidateHp, string cohort, int seed, string side, W12BuildEnvelopeSideRun run)
        {
            builder.Append(candidateHp.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(cohort.Replace(",", ";")).Append(',').Append(seed).Append(',').Append(side).Append(',')
                .Append(run.BossMaxHitPoints.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.BossSpawned ? 1 : 0).Append(',').Append(run.BossKilled ? 1 : 0).Append(',')
                .Append(run.BossReachedGoal ? 1 : 0).Append(',').Append(run.BossResidualAtW13 ? 1 : 0).Append(',')
                .Append(run.BossTtkSeconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.BossDamageTotal.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.BossDamageFirst3Seconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.BossDamageFirst5Seconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.BasicDamageToBoss.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.HeroDamageToBoss.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.OtherDamageToBoss.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.ShieldDamage.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.BodyDamage.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(run.StormCallStarted).Append(',').Append(run.StormCallSucceeded).Append(',')
                .Append(run.StormCallFailed).Append(',').Append(run.FirstCastAffectedNormalCount).Append(',')
                .Append(run.SecondCastAffectedNormalCount).Append(',').Append(run.ItemActivations).Append(',')
                .Append(run.DirectSetupBoardUnits).Append(',').Append(run.DirectSetupHeroCount).Append(',')
                .Append(run.DirectSetupBenchUnits).Append(',')
                .Append(run.W13Residual).Append(',').Append(run.MatchEndWave).Append(',')
                .Append(run.MatchEndReason.Replace(",", ";")).AppendLine();
        }
    }

    public sealed class W12BuildEnvelopeCalibrationAggregate
    {
        private readonly List<W12BuildEnvelopeSideRun> samples = new List<W12BuildEnvelopeSideRun>();

        public IReadOnlyList<W12BuildEnvelopeSideRun> Samples => samples;
        public int SampleCount => samples.Count;
        public int BossSpawnCount => Count(sample => sample.BossSpawned);
        public int BossKillCount => Count(sample => sample.BossKilled);
        public int BossGoalCount => Count(sample => sample.BossReachedGoal);
        public int BossResidualCount => Count(sample => sample.BossResidualAtW13);
        public int FirstCastSuccessCount => Count(sample => sample.FirstCastSucceeded > 0);
        public int SecondCastSuccessCount => Count(sample => sample.SecondCastSucceeded > 0);
        public int SpellbreakerFailureCount
        {
            get
            {
                var total = 0;
                foreach (var sample in samples) total += sample.StormCallFailed;
                return total;
            }
        }
        public float BossSpawnRate => Rate(BossSpawnCount, SampleCount);
        public float BossKillRate => Rate(BossKillCount, SampleCount);
        public float BossGoalRate => Rate(BossGoalCount, SampleCount);
        public float BossResidualRate => Rate(BossResidualCount, SampleCount);
        public float FirstCastSuccessRate => Rate(FirstCastSuccessCount, BossSpawnCount);
        public float SecondCastSuccessRate => Rate(SecondCastSuccessCount, BossSpawnCount);
        public double AverageBossDamage => Average(sample => sample.BossDamageTotal);
        public double AverageBossDamageFirst3Seconds => Average(sample => sample.BossDamageFirst3Seconds);
        public double AverageBossDamageFirst5Seconds => Average(sample => sample.BossDamageFirst5Seconds);
        public double AverageShieldDamage => Average(sample => sample.ShieldDamage);
        public double AverageBodyDamage => Average(sample => sample.BodyDamage);
        public double AverageW13Residual => Average(sample => sample.W13Residual);
        public double AverageFirstCastAffected => Average(sample => sample.FirstCastAffectedNormalCount);
        public double AverageSecondCastAffected => Average(sample => sample.SecondCastAffectedNormalCount);
        public double AverageItemActivations => Average(sample => sample.ItemActivations);

        internal void Add(W12BuildEnvelopeSideRun sample)
        {
            samples.Add(sample);
        }

        public int TtkSampleCount => Count(sample => sample.BossKilled);

        public double PercentileBossTtk(double percentile)
        {
            var values = new List<float>();
            foreach (var sample in samples)
            {
                if (sample.BossKilled) values.Add(sample.BossTtkSeconds);
            }

            if (values.Count == 0) return -1d;
            values.Sort();
            var index = (int)Math.Round((values.Count - 1) * percentile, MidpointRounding.AwayFromZero);
            return values[Math.Max(0, Math.Min(values.Count - 1, index))];
        }

        public int Window32To36Count => Count(sample => sample.BossKilled && sample.BossTtkSeconds >= 32f && sample.BossTtkSeconds <= 36f);

        public float Window32To36Rate => Rate(Window32To36Count, TtkSampleCount);

        private int Count(Func<W12BuildEnvelopeSideRun, bool> predicate)
        {
            var count = 0;
            foreach (var sample in samples) if (predicate(sample)) count++;
            return count;
        }

        private double Average(Func<W12BuildEnvelopeSideRun, float> selector)
        {
            if (samples.Count == 0) return 0d;
            double total = 0d;
            foreach (var sample in samples) total += selector(sample);
            return total / samples.Count;
        }

        private static float Rate(int numerator, int denominator)
        {
            return denominator == 0 ? 0f : numerator / (float)denominator;
        }
    }
}
