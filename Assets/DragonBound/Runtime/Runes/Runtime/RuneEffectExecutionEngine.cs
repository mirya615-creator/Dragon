using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using GameShared.Random;

namespace DragonBound.Runes
{
    /// <summary>Stable deterministic prefix version shared by all Rune combat engines.</summary>
    public static class RuneCombatDeterminism
    {
        public const string AlgorithmVersion = "RuneDrop.V1";
    }

    /// <summary>
    /// Pure Rune combat execution over Combat.Contracts target ports. Core owns all
    /// EnemyRuntime adaptation and legacy result conversion.
    /// </summary>
    public sealed class RuneEffectExecutionEngine
    {
        private readonly RuneDefinition definition;
        private readonly RuneRuntimeState state = new RuneRuntimeState();
        private readonly IRunRandom random;
        private readonly string randomPrefix;
        private int randomCall;
        private float summonRemaining;
        private float summonAttackElapsed;

        public RuneEffectExecutionEngine(
            RuneDefinition definition,
            IRunRandom random,
            string heroRuntimeId,
            string algorithmVersion = RuneCombatDeterminism.AlgorithmVersion)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (string.IsNullOrWhiteSpace(heroRuntimeId))
            {
                throw new ArgumentException("A stable runtime hero id is required.", nameof(heroRuntimeId));
            }

            if (string.IsNullOrWhiteSpace(algorithmVersion))
            {
                throw new ArgumentException("A stable Rune combat algorithm version is required.", nameof(algorithmVersion));
            }

            randomPrefix = algorithmVersion + ".Combat." + definition.RuneId + "." + heroRuntimeId;
        }

        public RuneDefinition Definition => definition;
        public RuneRuntimeState State => state;
        public bool HasActiveSummon => summonRemaining > 0.0001f;

        public float GetBasicAttackDamageMultiplier(CombatPoint origin, IRuneCombatTarget target, float effectiveRange)
        {
            if (definition.EffectType != RuneEffectType.LongshotDistanceDamage || target == null || effectiveRange <= 0f)
            {
                return 1f;
            }

            var distance = Distance(origin, target.CombatPosition);
            var percent = definition.GetParameter("MaxDamagePercent") * Math.Min(1f, distance / effectiveRange);
            return 1f + Math.Max(0f, percent);
        }

        public List<RuneTargetDamageResult> OnBasicAttackSucceeded(
            RuneTargetCombatContext context,
            IRuneCombatTarget primaryTarget)
        {
            var results = new List<RuneTargetDamageResult>();
            if (primaryTarget == null)
            {
                return results;
            }

            state.RecordBasicAttack();
            switch (definition.EffectType)
            {
                case RuneEffectType.LongshotDistanceDamage:
                {
                    var bonus = GetBasicAttackDamageMultiplier(context.Origin, primaryTarget, context.EffectiveRange) - 1f;
                    if (bonus > 0f)
                    {
                        AddDamage(results, primaryTarget, context.AttackDamage * bonus, AttackKind.RuneLongshot);
                    }
                    break;
                }
                case RuneEffectType.FrostbiteSlow:
                    ApplyFrostbite(primaryTarget);
                    break;
                case RuneEffectType.Ricochet:
                    if (Roll("Ricochet") < definition.GetParameter("Chance"))
                    {
                        var target = FindFirstNonPrimaryTarget(context, primaryTarget.RuntimeId);
                        if (target != null)
                        {
                            AddDamage(results, target, context.AttackDamage * definition.GetParameter("DamageMultiplier"), AttackKind.RuneRicochet);
                        }
                    }
                    break;
                case RuneEffectType.Volley:
                    ResolveVolley(context, results);
                    break;
                case RuneEffectType.Ambush:
                    ResolveAmbush(context, primaryTarget, results);
                    break;
                case RuneEffectType.Windhawk:
                    ResolveWindhawk(context, primaryTarget, results);
                    break;
                case RuneEffectType.Skybreaker:
                    ResolveSkybreaker(context, primaryTarget, results);
                    break;
                case RuneEffectType.Warcry:
                    ResolveWarcry(context, results);
                    break;
            }

            return results;
        }

        public List<RuneTargetDamageResult> OnHeroKill(
            RuneTargetCombatContext context,
            IRuneCombatTarget killedEnemy,
            bool wasRuneDerived)
        {
            var results = new List<RuneTargetDamageResult>();
            if (killedEnemy == null || wasRuneDerived)
            {
                return results;
            }

            if (definition.EffectType == RuneEffectType.BladeTempest &&
                Roll("BladeTempest") < definition.GetParameter("Chance"))
            {
                foreach (var target in FindNearest(context, killedEnemy.CombatPosition, (int)definition.GetParameter("TargetCount")))
                {
                    AddDamage(results, target, context.AttackDamage * definition.GetParameter("DamageMultiplier"), AttackKind.RuneBladeTempest);
                }
            }
            else if (definition.EffectType == RuneEffectType.Dragonbloom &&
                     Roll("Dragonbloom") < definition.GetParameter("Chance"))
            {
                RefreshSummon(definition.GetParameter("DurationSeconds"));
            }

            return results;
        }

        public void OnHeroLevelUp()
        {
            if (definition.EffectType == RuneEffectType.Wyrmguard)
            {
                RefreshSummon(definition.GetParameter("DurationSeconds"));
            }
        }

        public List<RuneTargetDamageResult> Tick(RuneTargetCombatContext context, float deltaSeconds)
        {
            var results = new List<RuneTargetDamageResult>();
            if (deltaSeconds <= 0f)
            {
                return results;
            }

            state.Tick(deltaSeconds);
            if (summonRemaining <= 0.0001f)
            {
                return results;
            }

            summonRemaining = Math.Max(0f, summonRemaining - deltaSeconds);
            summonAttackElapsed += deltaSeconds;
            var rate = definition.GetParameter("AttackRate");
            var interval = rate > 0f ? 1f / rate : float.MaxValue;
            while (summonAttackElapsed + 0.0001f >= interval)
            {
                var target = FindFrontmost(context, null);
                if (target == null)
                {
                    summonAttackElapsed = Math.Min(summonAttackElapsed, interval);
                    break;
                }

                summonAttackElapsed -= interval;
                AddDamage(
                    results,
                    target,
                    context.AttackDamage * definition.GetParameter("DamageMultiplier"),
                    definition.EffectType == RuneEffectType.Wyrmguard
                        ? AttackKind.RuneWyrmguardSpirit
                        : AttackKind.RuneDragonbloom);
            }

            if (summonRemaining <= 0.0001f)
            {
                state.SetSummon(false);
                summonAttackElapsed = 0f;
            }

            return results;
        }

        private void ApplyFrostbite(IRuneCombatTarget target)
        {
            var boss = target.IsBoss;
            target.TryApplyRuneSlow(
                definition.GetParameter(boss ? "BossSlow" : "NormalSlow"),
                definition.GetParameter(boss ? "BossDuration" : "NormalDuration"));
        }

        private void ResolveVolley(RuneTargetCombatContext context, ICollection<RuneTargetDamageResult> results)
        {
            var threshold = Math.Max(1, (int)definition.GetParameter("AttackThreshold"));
            if (state.BasicAttackCounter % threshold != 0)
            {
                return;
            }

            var targets = GetAliveTargets(context);
            if (targets.Count == 0)
            {
                return;
            }

            var boltCount = Math.Max(1, (int)definition.GetParameter("BoltCount"));
            for (var index = 0; index < boltCount; index++)
            {
                AddDamage(results, targets[index % targets.Count], context.AttackDamage * definition.GetParameter("DamageMultiplier"), AttackKind.RuneVolleyBolt);
            }
        }

        private void ResolveAmbush(
            RuneTargetCombatContext context,
            IRuneCombatTarget primaryTarget,
            ICollection<RuneTargetDamageResult> results)
        {
            if (!state.FirstHitTargets.Add(primaryTarget.RuntimeId) || Roll("Ambush") >= definition.GetParameter("Chance"))
            {
                return;
            }

            var radius = definition.GetParameter("Radius");
            foreach (var target in GetAliveTargets(context))
            {
                if (Distance(primaryTarget.CombatPosition, target.CombatPosition) <= radius + 0.0001f)
                {
                    AddDamage(results, target, context.AttackDamage * definition.GetParameter("DamageMultiplier"), AttackKind.RuneAmbush, radius);
                }
            }
        }

        private void ResolveWindhawk(
            RuneTargetCombatContext context,
            IRuneCombatTarget primaryTarget,
            ICollection<RuneTargetDamageResult> results)
        {
            if (state.CooldownRemaining > 0f || Roll("Windhawk") >= definition.GetParameter("Chance"))
            {
                return;
            }

            var target = FindFrontmost(context, primaryTarget.RuntimeId);
            var multiplier = definition.GetParameter("InterceptDamageMultiplier");
            if (target == null)
            {
                target = primaryTarget;
                multiplier = definition.GetParameter("FallbackDamageMultiplier");
            }

            if (target != null && state.TryStartCooldown(definition.GetParameter("IcdSeconds")))
            {
                AddDamage(results, target, context.AttackDamage * multiplier, AttackKind.RuneWindhawk);
            }
        }

        private void ResolveSkybreaker(
            RuneTargetCombatContext context,
            IRuneCombatTarget primaryTarget,
            ICollection<RuneTargetDamageResult> results)
        {
            if (Roll("Skybreaker") >= definition.GetParameter("Chance"))
            {
                return;
            }

            AddDamage(results, primaryTarget, context.AttackDamage * definition.GetParameter("PrimaryDamageMultiplier"), AttackKind.RuneSkybreakerPrimary, definition.GetParameter("Radius"));
            var radius = definition.GetParameter("Radius");
            foreach (var target in GetAliveTargets(context))
            {
                if (target.RuntimeId != primaryTarget.RuntimeId &&
                    Distance(primaryTarget.CombatPosition, target.CombatPosition) <= radius + 0.0001f)
                {
                    AddDamage(results, target, context.AttackDamage * definition.GetParameter("SecondaryDamageMultiplier"), AttackKind.RuneSkybreakerSecondary, radius);
                }
            }
        }

        private void ResolveWarcry(RuneTargetCombatContext context, ICollection<RuneTargetDamageResult> results)
        {
            if (state.CooldownRemaining > 0f || Roll("Warcry") >= definition.GetParameter("Chance"))
            {
                return;
            }

            if (state.TryStartCooldown(definition.GetParameter("IcdSeconds")))
            {
                results.Add(RuneTargetDamageResult.CreateWarcry(
                    context.Origin,
                    definition.GetParameter("Radius"),
                    definition.GetParameter("AttackSpeedMultiplier"),
                    definition.GetParameter("DurationSeconds")));
            }
        }

        private void RefreshSummon(float duration)
        {
            summonRemaining = Math.Max(summonRemaining, Math.Max(0f, duration));
            state.SetSummon(summonRemaining > 0f);
        }

        private float Roll(string eventName)
        {
            return random.NextUnit(randomPrefix + "." + eventName + "." + randomCall++);
        }

        private static void AddDamage(
            ICollection<RuneTargetDamageResult> results,
            IRuneCombatTarget target,
            float damage,
            AttackKind kind,
            float radius = 0f)
        {
            if (target == null || !target.IsAlive || damage < 0f)
            {
                return;
            }

            var application = target.ApplyRuneDamage(damage);
            results.Add(new RuneTargetDamageResult(
                kind,
                target,
                damage,
                application.Killed,
                radius,
                application.ShieldDamage,
                application.HealthDamage));
        }

        private static IRuneCombatTarget FindFirstNonPrimaryTarget(RuneTargetCombatContext context, string primaryRuntimeId)
        {
            foreach (var target in GetAliveTargets(context))
            {
                if (!string.Equals(target.RuntimeId, primaryRuntimeId, StringComparison.Ordinal))
                {
                    return target;
                }
            }

            return null;
        }

        private static IRuneCombatTarget FindFrontmost(RuneTargetCombatContext context, string excludedRuntimeId)
        {
            IRuneCombatTarget selected = null;
            foreach (var target in GetAliveTargets(context))
            {
                if (string.Equals(target.RuntimeId, excludedRuntimeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (selected == null || target.PathProgress > selected.PathProgress ||
                    (Math.Abs(target.PathProgress - selected.PathProgress) < 0.0001f &&
                     string.CompareOrdinal(target.RuntimeId, selected.RuntimeId) < 0))
                {
                    selected = target;
                }
            }

            return selected;
        }

        private static List<IRuneCombatTarget> FindNearest(RuneTargetCombatContext context, CombatPoint center, int count)
        {
            var targets = GetAliveTargets(context);
            targets.Sort((first, second) =>
            {
                var distance = Distance(center, first.CombatPosition).CompareTo(Distance(center, second.CombatPosition));
                return distance != 0 ? distance : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
            });
            if (targets.Count > count)
            {
                targets.RemoveRange(count, targets.Count - count);
            }

            return targets;
        }

        private static List<IRuneCombatTarget> GetAliveTargets(RuneTargetCombatContext context)
        {
            var targets = context.Targets == null
                ? new List<IRuneCombatTarget>()
                : new List<IRuneCombatTarget>(context.Targets.Snapshot());
            targets.RemoveAll(target => target == null || !target.IsAlive);
            targets.Sort((first, second) => string.CompareOrdinal(first.RuntimeId, second.RuntimeId));
            return targets;
        }

        private static float Distance(CombatPoint first, CombatPoint second)
        {
            var x = first.X - second.X;
            var y = first.Y - second.Y;
            return (float)Math.Sqrt((x * x) + (y * y));
        }
    }
}
