using System;
using System.Collections.Generic;
using System.Text;

namespace DragonBound.Core
{
    public enum DamageCompositionSource
    {
        Basic,
        Hero,
        ComponentPairLink,
        RuneDerived,
        Item,
        OtherSystem
    }

    public sealed class DamageCompositionTelemetry
    {
        private readonly float[] totalBySource = new float[6];
        private readonly float[] bossBySource = new float[6];
        private readonly float[] normalBySource = new float[6];

        public float TotalDamage { get; private set; }
        public float BossDamage { get; private set; }
        public float NormalDamage { get; private set; }

        public void RecordDamage(DamageCompositionSource source, float amount, bool bossTarget)
        {
            if (amount <= 0f)
            {
                return;
            }

            var index = (int)source;
            totalBySource[index] += amount;
            TotalDamage += amount;
            if (bossTarget)
            {
                bossBySource[index] += amount;
                BossDamage += amount;
            }
            else
            {
                normalBySource[index] += amount;
                NormalDamage += amount;
            }
        }

        public float GetTotal(DamageCompositionSource source)
        {
            return totalBySource[(int)source];
        }

        public float GetBossDamage(DamageCompositionSource source)
        {
            return bossBySource[(int)source];
        }

        public float GetNormalDamage(DamageCompositionSource source)
        {
            return normalBySource[(int)source];
        }

        public float GetShare(DamageCompositionSource source)
        {
            return TotalDamage <= 0f ? 0f : GetTotal(source) / TotalDamage;
        }

        public float SumSourceTotals()
        {
            var total = 0f;
            for (var index = 0; index < totalBySource.Length; index++)
            {
                total += totalBySource[index];
            }

            return total;
        }
    }

    [Serializable]
    public sealed class W6SoulChainTelemetryResult
    {
        public string FixtureId = W6SoulChainTelemetryRunner.FixtureId;
        public int RunSeed;
        public bool SoulChainEnabled;
        public bool TestFixture = true;
        public int BasicCount;
        public int HeroCount;
        public int ComponentPairLinkCount;
        public float BossGreyboxHitPoints;
        public float BossTtkSeconds;
        public float BossDamage;
        public float NormalDamage;
        public bool BossAliveAtW7Start;
        public int SoulChainCastStarted;
        public int SoulChainCastSucceeded;
        public int SoulChainCastFailed;
        public int SecondCastStarted;
        public int SecondCastApplied;
        public int SecondCastEnded;
        public float TotalControlUnitSeconds;
        public int[] CastAffectedCountByCast = new int[2];
        public float[] ControlUnitSecondsByCast = new float[2];
        public float[] SourceDamage = new float[6];
        public float[] SourceShare = new float[6];
        public float[] BossSourceDamage = new float[6];
        public float[] NormalSourceDamage = new float[6];

        public float SourceSum()
        {
            var sum = 0f;
            for (var index = 0; index < SourceDamage.Length; index++)
            {
                sum += SourceDamage[index];
            }

            return sum;
        }
    }

    [Serializable]
    public sealed class W6SoulChainTelemetryAggregate
    {
        public string FixtureId = W6SoulChainTelemetryRunner.FixtureId;
        public bool SoulChainEnabled;
        public int FirstRunSeed;
        public int SeedCount;
        public bool TestFixture = true;
        public int BasicCount = 4;
        public int HeroCount = 1;
        public int ComponentPairLinkCount = 1;
        public float BossGreyboxHitPoints = SoulchainBinderConfiguration.GreyboxMaxHitPoints;
        public float AverageBossTtkSeconds;
        public float AverageBossDamage;
        public float AverageNormalDamage;
        public float BossAliveAtW7StartRate;
        public float AverageSoulChainCastStarted;
        public float AverageSoulChainCastSucceeded;
        public float AverageSoulChainCastFailed;
        public float AverageSecondCastStarted;
        public float AverageSecondCastApplied;
        public float AverageSecondCastEnded;
        public float AverageTotalControlUnitSeconds;
        public float[] AverageCastAffectedCountByCast = new float[2];
        public float[] AverageControlUnitSecondsByCast = new float[2];
        public float[] AverageSourceDamage = new float[6];
        public float[] AverageSourceShare = new float[6];
        public float[] AverageBossSourceDamage = new float[6];
        public float[] AverageNormalSourceDamage = new float[6];

        public float SourceSum()
        {
            var sum = 0f;
            for (var index = 0; index < AverageSourceDamage.Length; index++)
            {
                sum += AverageSourceDamage[index];
            }

            return sum;
        }
    }

    [Serializable]
    public sealed class W6SoulChainTelemetryComparison
    {
        public int FirstRunSeed;
        public int SeedCount;
        public bool TestFixture = true;
        public W6SoulChainTelemetryAggregate SoulChainDisabled;
        public W6SoulChainTelemetryAggregate SoulChainEnabled;
        public float AverageBossTtkDeltaSeconds;

        public string ToCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("mode,source,total_damage,damage_share,boss_damage,normal_damage,average_boss_ttk_seconds,boss_alive_at_w7_start_rate,soulchain_cast_started,soulchain_cast_succeeded,soulchain_cast_failed,second_cast_started,second_cast_applied,second_cast_ended,cast1_affected,cast1_control_unit_seconds,cast2_affected,cast2_control_unit_seconds,control_unit_seconds");
            AppendRows(builder, SoulChainDisabled);
            AppendRows(builder, SoulChainEnabled);
            builder.AppendLine();
            builder.AppendLine("comparison,average_boss_ttk_delta_seconds");
            builder.Append("disabled_to_enabled,").Append(AverageBossTtkDeltaSeconds.ToString("0.000"));
            builder.AppendLine();
            return builder.ToString();
        }

        private static void AppendRows(StringBuilder builder, W6SoulChainTelemetryAggregate aggregate)
        {
            if (aggregate == null)
            {
                return;
            }

            for (var index = 0; index < 6; index++)
            {
                var source = ((DamageCompositionSource)index).ToString();
                builder.Append(aggregate.SoulChainEnabled ? "enabled," : "disabled,")
                    .Append(source).Append(',')
                    .Append(aggregate.AverageSourceDamage[index].ToString("0.000")).Append(',')
                    .Append(aggregate.AverageSourceShare[index].ToString("0.000000")).Append(',')
                    .Append(aggregate.AverageBossSourceDamage[index].ToString("0.000")).Append(',')
                    .Append(aggregate.AverageNormalSourceDamage[index].ToString("0.000")).Append(',')
                    .Append(aggregate.AverageBossTtkSeconds.ToString("0.000")).Append(',')
                    .Append(aggregate.BossAliveAtW7StartRate.ToString("0.000000")).Append(',')
                    .Append(aggregate.AverageSoulChainCastStarted.ToString("0.000")).Append(',')
                    .Append(aggregate.AverageSoulChainCastSucceeded.ToString("0.000")).Append(',')
                    .Append(aggregate.AverageSoulChainCastFailed.ToString("0.000")).Append(',')
                    .Append(aggregate.AverageSecondCastStarted.ToString("0.000")).Append(',')
                    .Append(aggregate.AverageSecondCastApplied.ToString("0.000")).Append(',')
                    .Append(aggregate.AverageSecondCastEnded.ToString("0.000")).Append(',')
                    .Append(aggregate.AverageCastAffectedCountByCast[0].ToString("0.000")).Append(',')
                    .Append(aggregate.AverageControlUnitSecondsByCast[0].ToString("0.000")).Append(',')
                    .Append(aggregate.AverageCastAffectedCountByCast[1].ToString("0.000")).Append(',')
                    .Append(aggregate.AverageControlUnitSecondsByCast[1].ToString("0.000")).Append(',')
                    .Append(aggregate.AverageTotalControlUnitSeconds.ToString("0.000"))
                    .AppendLine();
            }
        }
    }

    public static class W6SoulChainTelemetryRunner
    {
        public const string FixtureId = "TEST_FIXTURE_W6_BASIC4_HERO1_PAIR1_LV1";
        public const int W6NormalCount = 16;
        public const float W6NormalMaxHitPoints = 63f;
        public const float W7StartSeconds = 29f;

        public static W6SoulChainTelemetryComparison RunSeedRange(int firstRunSeed, int seedCount)
        {
            if (seedCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seedCount));
            }

            var disabled = new List<W6SoulChainTelemetryResult>(seedCount);
            var enabled = new List<W6SoulChainTelemetryResult>(seedCount);
            for (var offset = 0; offset < seedCount; offset++)
            {
                var seed = firstRunSeed + offset;
                disabled.Add(RunOne(seed, false));
                enabled.Add(RunOne(seed, true));
            }

            var comparison = new W6SoulChainTelemetryComparison
            {
                FirstRunSeed = firstRunSeed,
                SeedCount = seedCount,
                SoulChainDisabled = Aggregate(disabled, false, firstRunSeed),
                SoulChainEnabled = Aggregate(enabled, true, firstRunSeed)
            };
            comparison.AverageBossTtkDeltaSeconds =
                comparison.SoulChainEnabled.AverageBossTtkSeconds - comparison.SoulChainDisabled.AverageBossTtkSeconds;
            return comparison;
        }

        public static W6SoulChainTelemetryResult RunOne(int runSeed, bool soulChainEnabled)
        {
            var boss = new EnemyRuntime(
                "testfixture.w6.boss",
                TeamSide.Player,
                SoulchainBinderConfiguration.GreyboxMaxHitPoints,
                EnemyArchetype.Boss,
                0);
            var provider = new FixtureTargetProvider();
            var controller = new SoulChainController(boss, TeamSide.Player, provider, runSeed, null, soulChainEnabled);
            var telemetry = new DamageCompositionTelemetry();
            var secondStarted = 0;
            var secondApplied = 0;
            var secondEnded = 0;
            var totalControlUnitSeconds = 0f;
            var castAffectedCounts = new int[2];
            var castControlUnitSeconds = new float[2];
            controller.CastEvent += value =>
            {
                if (value.CastNumber == 2 && value.Kind == SoulChainCastEventKind.CastStarted) secondStarted++;
                if (value.CastNumber == 2 && value.Kind == SoulChainCastEventKind.EffectApplied) secondApplied++;
                if (value.CastNumber == 2 && value.Kind == SoulChainCastEventKind.EffectEnded) secondEnded++;
                if (value.Kind == SoulChainCastEventKind.EffectApplied)
                {
                    totalControlUnitSeconds += value.ControlUnitSeconds;
                    if (value.CastNumber >= 1 && value.CastNumber <= 2)
                    {
                        castAffectedCounts[value.CastNumber - 1] = value.AffectedCount;
                        castControlUnitSeconds[value.CastNumber - 1] = value.ControlUnitSeconds;
                    }
                }
            };

            var bossTtk = -1f;
            var normalRemaining = W6NormalCount * W6NormalMaxHitPoints;
            var elapsed = 0f;
            var bossAliveAtW7Start = false;
            var capturedW7Start = false;
            const float step = 0.1f;
            while (elapsed < 60f && (boss.IsAlive || normalRemaining > 0f))
            {
                controller.Tick(step);
                ApplyFixtureDamage(provider, boss, telemetry, ref normalRemaining, step);
                elapsed += step;
                if (bossTtk < 0f && !boss.IsAlive)
                {
                    bossTtk = elapsed;
                }

                if (!capturedW7Start && elapsed >= W7StartSeconds - 0.0001f)
                {
                    bossAliveAtW7Start = boss.IsAlive;
                    capturedW7Start = true;
                }
            }

            var result = new W6SoulChainTelemetryResult
            {
                RunSeed = runSeed,
                SoulChainEnabled = soulChainEnabled,
                BasicCount = 4,
                HeroCount = 1,
                ComponentPairLinkCount = 1,
                BossGreyboxHitPoints = SoulchainBinderConfiguration.GreyboxMaxHitPoints,
                BossTtkSeconds = bossTtk < 0f ? 0f : bossTtk,
                BossDamage = telemetry.BossDamage,
                NormalDamage = telemetry.NormalDamage,
                BossAliveAtW7Start = capturedW7Start && bossAliveAtW7Start,
                SoulChainCastStarted = controller.CastsStarted,
                SoulChainCastSucceeded = controller.CastsSucceeded,
                SoulChainCastFailed = controller.CastsFailed,
                SecondCastStarted = secondStarted,
                SecondCastApplied = secondApplied,
                SecondCastEnded = secondEnded,
                TotalControlUnitSeconds = totalControlUnitSeconds
            };
            result.CastAffectedCountByCast = castAffectedCounts;
            result.ControlUnitSecondsByCast = castControlUnitSeconds;
            for (var index = 0; index < 6; index++)
            {
                var source = (DamageCompositionSource)index;
                result.SourceDamage[index] = telemetry.GetTotal(source);
                result.SourceShare[index] = telemetry.GetShare(source);
                result.BossSourceDamage[index] = telemetry.GetBossDamage(source);
                result.NormalSourceDamage[index] = telemetry.GetNormalDamage(source);
            }

            return result;
        }

        private static void ApplyFixtureDamage(
            FixtureTargetProvider provider,
            EnemyRuntime boss,
            DamageCompositionTelemetry telemetry,
            ref float normalRemaining,
            float deltaSeconds)
        {
            var basicDamage = 0f;
            for (var index = 0; index < provider.UnitIds.Count; index++)
            {
                var runtimeId = provider.UnitIds[index];
                if (!provider.IsDisabled(runtimeId))
                {
                    basicDamage += 2.5f * deltaSeconds;
                }
            }

            ApplyDamage(DamageCompositionSource.Basic, basicDamage, boss, telemetry, ref normalRemaining);
            ApplyDamage(DamageCompositionSource.Hero, 4.5f * deltaSeconds, boss, telemetry, ref normalRemaining);
            ApplyDamage(DamageCompositionSource.ComponentPairLink, 3.5f * deltaSeconds, boss, telemetry, ref normalRemaining);
            ApplyDamage(DamageCompositionSource.RuneDerived, 0.5f * deltaSeconds, boss, telemetry, ref normalRemaining);
            ApplyDamage(DamageCompositionSource.Item, 0f, boss, telemetry, ref normalRemaining);
        }

        private static void ApplyDamage(
            DamageCompositionSource source,
            float requested,
            EnemyRuntime boss,
            DamageCompositionTelemetry telemetry,
            ref float normalRemaining)
        {
            if (requested <= 0f)
            {
                return;
            }

            var bossDamage = Math.Min(requested, boss.IsAlive ? boss.HitPoints : 0f);
            if (bossDamage > 0f)
            {
                boss.ApplyDamage(bossDamage);
                telemetry.RecordDamage(source, bossDamage, true);
            }

            var normalDamage = Math.Min(requested, normalRemaining);
            if (normalDamage > 0f)
            {
                normalRemaining -= normalDamage;
                telemetry.RecordDamage(source, normalDamage, false);
            }
        }

        private static W6SoulChainTelemetryAggregate Aggregate(
            List<W6SoulChainTelemetryResult> results,
            bool soulChainEnabled,
            int firstRunSeed)
        {
            var aggregate = new W6SoulChainTelemetryAggregate
            {
                SoulChainEnabled = soulChainEnabled,
                FirstRunSeed = firstRunSeed,
                SeedCount = results.Count
            };
            for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
            {
                var result = results[resultIndex];
                aggregate.AverageBossTtkSeconds += result.BossTtkSeconds;
                aggregate.AverageBossDamage += result.BossDamage;
                aggregate.AverageNormalDamage += result.NormalDamage;
                aggregate.BossAliveAtW7StartRate += result.BossAliveAtW7Start ? 1f : 0f;
                aggregate.AverageSoulChainCastStarted += result.SoulChainCastStarted;
                aggregate.AverageSoulChainCastSucceeded += result.SoulChainCastSucceeded;
                aggregate.AverageSoulChainCastFailed += result.SoulChainCastFailed;
                aggregate.AverageSecondCastStarted += result.SecondCastStarted;
                aggregate.AverageSecondCastApplied += result.SecondCastApplied;
                aggregate.AverageSecondCastEnded += result.SecondCastEnded;
                aggregate.AverageTotalControlUnitSeconds += result.TotalControlUnitSeconds;
                for (var castIndex = 0; castIndex < 2; castIndex++)
                {
                    aggregate.AverageCastAffectedCountByCast[castIndex] += result.CastAffectedCountByCast[castIndex];
                    aggregate.AverageControlUnitSecondsByCast[castIndex] += result.ControlUnitSecondsByCast[castIndex];
                }
                for (var index = 0; index < 6; index++)
                {
                    aggregate.AverageSourceDamage[index] += result.SourceDamage[index];
                    aggregate.AverageSourceShare[index] += result.SourceShare[index];
                    aggregate.AverageBossSourceDamage[index] += result.BossSourceDamage[index];
                    aggregate.AverageNormalSourceDamage[index] += result.NormalSourceDamage[index];
                }
            }

            var divisor = results.Count;
            aggregate.AverageBossTtkSeconds /= divisor;
            aggregate.AverageBossDamage /= divisor;
            aggregate.AverageNormalDamage /= divisor;
            aggregate.BossAliveAtW7StartRate /= divisor;
            aggregate.AverageSoulChainCastStarted /= divisor;
            aggregate.AverageSoulChainCastSucceeded /= divisor;
            aggregate.AverageSoulChainCastFailed /= divisor;
            aggregate.AverageSecondCastStarted /= divisor;
            aggregate.AverageSecondCastApplied /= divisor;
            aggregate.AverageSecondCastEnded /= divisor;
            aggregate.AverageTotalControlUnitSeconds /= divisor;
            for (var castIndex = 0; castIndex < 2; castIndex++)
            {
                aggregate.AverageCastAffectedCountByCast[castIndex] /= divisor;
                aggregate.AverageControlUnitSecondsByCast[castIndex] /= divisor;
            }
            for (var index = 0; index < 6; index++)
            {
                aggregate.AverageSourceDamage[index] /= divisor;
                aggregate.AverageSourceShare[index] /= divisor;
                aggregate.AverageBossSourceDamage[index] /= divisor;
                aggregate.AverageNormalSourceDamage[index] /= divisor;
            }

            return aggregate;
        }

        private sealed class FixtureTargetProvider : ISoulChainTargetProvider
        {
            private readonly Dictionary<string, bool> disabled =
                new Dictionary<string, bool>(StringComparer.Ordinal);
            private readonly List<SoulChainBasicCandidate> candidates = new List<SoulChainBasicCandidate>
            {
                new SoulChainBasicCandidate("fixture.basic.01", new DragonBound.Grid.GridPosition(0, 0)),
                new SoulChainBasicCandidate("fixture.basic.02", new DragonBound.Grid.GridPosition(1, 0)),
                new SoulChainBasicCandidate("fixture.basic.03", new DragonBound.Grid.GridPosition(0, 1)),
                new SoulChainBasicCandidate("fixture.basic.04", new DragonBound.Grid.GridPosition(1, 1))
            };

            public FixtureTargetProvider()
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    disabled[candidates[index].RuntimeId] = false;
                }
            }

            public IReadOnlyList<SoulChainBasicCandidate> GetBasicCandidates()
            {
                return candidates;
            }

            public bool SetAttackDisabled(string runtimeId, bool value)
            {
                if (!disabled.ContainsKey(runtimeId))
                {
                    return false;
                }

                disabled[runtimeId] = value;
                return true;
            }

            public bool IsDisabled(string runtimeId)
            {
                return disabled[runtimeId];
            }

            public IReadOnlyList<string> UnitIds
            {
                get
                {
                    var result = new List<string>(candidates.Count);
                    for (var index = 0; index < candidates.Count; index++) result.Add(candidates[index].RuntimeId);
                    return result;
                }
            }
        }
    }
}
