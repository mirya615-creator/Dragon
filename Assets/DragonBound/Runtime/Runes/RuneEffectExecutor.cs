using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using GameShared.Random;

namespace DragonBound.Runes
{
    /// <summary>
    /// Legacy Core facade. The port-only execution engine is owned by
    /// DragonBound.Runes.Runtime; this class only adapts EnemyRuntime inputs and results.
    /// </summary>
    public sealed class RuneEffectExecutor
    {
        private readonly RuneEffectExecutionEngine engine;

        public RuneEffectExecutor(
            RuneDefinition definition,
            IRunRandom random,
            string heroRuntimeId)
        {
            engine = new RuneEffectExecutionEngine(
                definition,
                random,
                heroRuntimeId,
                RuneCombatDeterminism.AlgorithmVersion);
        }

        public RuneDefinition Definition => engine.Definition;
        public RuneRuntimeState State => engine.State;
        public bool HasActiveSummon => engine.HasActiveSummon;

        public float GetBasicAttackDamageMultiplier(CombatPoint origin, EnemyRuntime target, float effectiveRange)
        {
            return engine.GetBasicAttackDamageMultiplier(origin, ToTarget(target), effectiveRange);
        }

        public float GetBasicAttackDamageMultiplier(CombatPoint origin, IRuneCombatTarget target, float effectiveRange)
        {
            return engine.GetBasicAttackDamageMultiplier(origin, target, effectiveRange);
        }

        public List<RuneDamageResult> OnBasicAttackSucceeded(RuneCombatContext context, EnemyRuntime primaryTarget)
        {
            return ToLegacyResults(
                engine.OnBasicAttackSucceeded(ToTargetContext(context), ToTarget(primaryTarget)),
                context.Registry,
                primaryTarget);
        }

        public List<RuneTargetDamageResult> OnBasicAttackSucceeded(
            RuneTargetCombatContext context,
            IRuneCombatTarget primaryTarget)
        {
            return engine.OnBasicAttackSucceeded(context, primaryTarget);
        }

        public List<RuneDamageResult> OnHeroKill(
            RuneCombatContext context,
            EnemyRuntime killedEnemy,
            bool wasRuneDerived)
        {
            return ToLegacyResults(
                engine.OnHeroKill(ToTargetContext(context), ToTarget(killedEnemy), wasRuneDerived),
                context.Registry,
                killedEnemy);
        }

        public List<RuneTargetDamageResult> OnHeroKill(
            RuneTargetCombatContext context,
            IRuneCombatTarget killedEnemy,
            bool wasRuneDerived)
        {
            return engine.OnHeroKill(context, killedEnemy, wasRuneDerived);
        }

        public void OnHeroLevelUp()
        {
            engine.OnHeroLevelUp();
        }

        public List<RuneDamageResult> Tick(RuneCombatContext context, float deltaSeconds)
        {
            return ToLegacyResults(engine.Tick(ToTargetContext(context), deltaSeconds), context.Registry, null);
        }

        public List<RuneTargetDamageResult> Tick(RuneTargetCombatContext context, float deltaSeconds)
        {
            return engine.Tick(context, deltaSeconds);
        }

        private static RuneTargetCombatContext ToTargetContext(RuneCombatContext context)
        {
            return new RuneTargetCombatContext(
                context.Origin,
                context.AttackDamage,
                context.EffectiveRange,
                new EnemyRegistryRuneCombatTargetRegistry(context.Registry));
        }

        private static IRuneCombatTarget ToTarget(EnemyRuntime target)
        {
            return target == null ? null : new EnemyRuntimeRuneCombatTarget(target);
        }

        private static List<RuneDamageResult> ToLegacyResults(
            IReadOnlyList<RuneTargetDamageResult> targetResults,
            EnemyRegistry registry,
            EnemyRuntime primaryTarget)
        {
            var results = new List<RuneDamageResult>();
            if (targetResults == null)
            {
                return results;
            }

            foreach (var targetResult in targetResults)
            {
                if (targetResult.IsWarcry)
                {
                    results.Add(RuneDamageResult.CreateWarcry(
                        targetResult.WarcryCenter,
                        targetResult.EffectRadius,
                        targetResult.WarcryMultiplier,
                        targetResult.WarcryDuration));
                    continue;
                }

                EnemyRuntime target = null;
                if (registry != null && !string.IsNullOrEmpty(targetResult.TargetRuntimeId))
                {
                    registry.TryGet(targetResult.TargetRuntimeId, out target);
                }

                if (target == null && primaryTarget != null &&
                    string.Equals(primaryTarget.RuntimeId, targetResult.TargetRuntimeId, StringComparison.Ordinal))
                {
                    target = primaryTarget;
                }

                results.Add(new RuneDamageResult(
                    targetResult.Kind,
                    target,
                    targetResult.Damage,
                    targetResult.Killed,
                    targetResult.EffectRadius,
                    targetResult.ShieldDamage,
                    targetResult.HealthDamage));
            }

            return results;
        }
    }

    public readonly struct RuneCombatContext
    {
        public RuneCombatContext(CombatPoint origin, float attackDamage, float effectiveRange, EnemyRegistry registry)
        {
            Origin = origin;
            AttackDamage = attackDamage;
            EffectiveRange = effectiveRange;
            Registry = registry;
        }

        public CombatPoint Origin { get; }
        public float AttackDamage { get; }
        public float EffectiveRange { get; }
        public EnemyRegistry Registry { get; }
    }

    public readonly struct RuneDamageResult
    {
        private RuneDamageResult(
            AttackKind kind,
            EnemyRuntime target,
            float damage,
            bool killed,
            float effectRadius,
            bool isWarcry,
            CombatPoint warcryCenter,
            float warcryMultiplier,
            float warcryDuration,
            float shieldDamage = 0f,
            float healthDamage = 0f)
        {
            Kind = kind;
            Target = target;
            Damage = damage;
            Killed = killed;
            EffectRadius = effectRadius;
            IsWarcry = isWarcry;
            WarcryCenter = warcryCenter;
            WarcryMultiplier = warcryMultiplier;
            WarcryDuration = warcryDuration;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
        }

        public RuneDamageResult(AttackKind kind, EnemyRuntime target, float damage, bool killed, float effectRadius = 0f,
            float shieldDamage = 0f, float healthDamage = 0f)
            : this(kind, target, damage, killed, effectRadius, false, default(CombatPoint), 1f, 0f, shieldDamage, healthDamage)
        {
        }

        public AttackKind Kind { get; }
        public EnemyRuntime Target { get; }
        public float Damage { get; }
        public bool Killed { get; }
        public float EffectRadius { get; }
        public bool IsWarcry { get; }
        public CombatPoint WarcryCenter { get; }
        public float WarcryMultiplier { get; }
        public float WarcryDuration { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }

        public static RuneDamageResult CreateWarcry(CombatPoint center, float radius, float multiplier, float duration)
        {
            return new RuneDamageResult(default(AttackKind), null, 0f, false, radius, true, center, multiplier, duration);
        }
    }
}
