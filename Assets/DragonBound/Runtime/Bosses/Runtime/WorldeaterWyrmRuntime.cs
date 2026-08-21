using System;
using System.Collections.Generic;
using DragonBound.Bosses.Contracts;

namespace DragonBound.Bosses.Runtime
{
    public enum WorldeaterTargetClass
    {
        Basic,
        Minion,
        SubBoss
    }

    public readonly struct WorldeaterTarget
    {
        public WorldeaterTarget(string runtimeId, WorldeaterTargetClass targetClass, int storedLevel)
        {
            RuntimeId = runtimeId ?? string.Empty;
            TargetClass = targetClass;
            StoredLevel = storedLevel;
        }

        public string RuntimeId { get; }
        public WorldeaterTargetClass TargetClass { get; }
        public int StoredLevel { get; }
    }

    public interface IWorldeaterBossTarget
    {
        float InitialMaxHitPoints { get; }
        float MaxHitPoints { get; }
        bool IsAlive { get; }
        void ApplyReflectedDamage(float damage);
        void AddHealth(float amount);
    }

    public interface IWorldeaterTargetPort
    {
        IReadOnlyList<WorldeaterTarget> GetEligibleTargets();
        bool IsStillEligible(WorldeaterTarget target);
        bool Consume(WorldeaterTarget target);
    }

    public interface IWorldeaterSummonPort
    {
        void SpawnMinions(int count, float maxHitPoints, float moveSpeedCellsPerSecond);
    }

    public interface IWorldeaterSpellbreaker
    {
        SpellbreakerOutcome Evaluate(BossCastAttempt attempt);
    }

    public enum WorldeaterCastKind
    {
        Devour,
        Summon
    }

    public enum WorldeaterCastOutcome
    {
        Started,
        Resolved,
        Blocked,
        NoTarget,
        TargetInvalid,
        BossDead
    }

    public readonly struct WorldeaterCastEvent
    {
        public WorldeaterCastEvent(
            WorldeaterCastKind kind,
            WorldeaterCastOutcome outcome,
            int castNumber,
            float elapsedSeconds,
            int affectedCount,
            float reflectionDamage,
            string targetRuntimeId)
        {
            Kind = kind;
            Outcome = outcome;
            CastNumber = castNumber;
            ElapsedSeconds = elapsedSeconds;
            AffectedCount = affectedCount;
            ReflectionDamage = reflectionDamage;
            TargetRuntimeId = targetRuntimeId ?? string.Empty;
        }

        public WorldeaterCastKind Kind { get; }
        public WorldeaterCastOutcome Outcome { get; }
        public int CastNumber { get; }
        public float ElapsedSeconds { get; }
        public int AffectedCount { get; }
        public float ReflectionDamage { get; }
        public string TargetRuntimeId { get; }
    }

    public sealed class WorldeaterWyrmRuntime
    {
        private readonly BossDefinition definition;
        private readonly IWorldeaterBossTarget boss;
        private readonly IWorldeaterTargetPort targets;
        private readonly IWorldeaterSummonPort summons;
        private readonly IWorldeaterSpellbreaker spellbreaker;
        private float elapsedSeconds;
        private float nextDevourStart = WorldeaterWyrmConfiguration.FirstDevourDelaySeconds;
        private float nextSummonStart = WorldeaterWyrmConfiguration.FirstSummonDelaySeconds;
        private float devourWindupEnd;
        private float summonWindupEnd;
        private bool devourActive;
        private bool summonActive;
        private WorldeaterTarget lockedTarget;
        private bool hasLockedTarget;
        private bool deathHandled;

        public WorldeaterWyrmRuntime(
            BossDefinition definition,
            IWorldeaterBossTarget boss,
            IWorldeaterTargetPort targets,
            IWorldeaterSummonPort summons,
            IWorldeaterSpellbreaker spellbreaker = null)
        {
            if (definition.BossId != FixedBossIds.W20WorldeaterWyrm || definition.Wave.Value != 20)
            {
                throw new ArgumentException("Worldeater runtime requires the fixed W20 definition.", nameof(definition));
            }

            if (definition.GoalEffect != BossGoalEffect.InstantDefeat)
            {
                throw new ArgumentException("Worldeater runtime requires InstantDefeat as its GoalEffect.", nameof(definition));
            }

            this.definition = definition;
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.summons = summons ?? throw new ArgumentNullException(nameof(summons));
            this.spellbreaker = spellbreaker;
        }

        public event Action<WorldeaterCastEvent> CastEvent;
        public BossDefinition Definition => definition;
        public float ElapsedSeconds => elapsedSeconds;
        public bool IsDead => deathHandled;
        public int DevourCastCount { get; private set; }
        public int SummonCastCount { get; private set; }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (deathHandled)
            {
                return;
            }

            if (!boss.IsAlive)
            {
                deathHandled = true;
                devourActive = false;
                summonActive = false;
                Emit(WorldeaterCastKind.Devour, WorldeaterCastOutcome.BossDead, 0, 0f, string.Empty);
                return;
            }

            var targetTime = elapsedSeconds + deltaSeconds;
            while (elapsedSeconds < targetTime - 0.0001f && !deathHandled)
            {
                var nextBoundary = targetTime;
                if (!devourActive)
                {
                    nextBoundary = Math.Min(nextBoundary, nextDevourStart);
                }
                else
                {
                    nextBoundary = Math.Min(nextBoundary, devourWindupEnd);
                }

                if (!summonActive)
                {
                    nextBoundary = Math.Min(nextBoundary, nextSummonStart);
                }
                else
                {
                    nextBoundary = Math.Min(nextBoundary, summonWindupEnd);
                }

                elapsedSeconds = nextBoundary;
                ProcessBoundary();
            }

            if (!deathHandled && elapsedSeconds < targetTime - 0.0001f)
            {
                elapsedSeconds = targetTime;
                ProcessBoundary();
            }
        }

        private void ProcessBoundary()
        {
            if (!boss.IsAlive)
            {
                deathHandled = true;
                return;
            }

            if (!devourActive && elapsedSeconds >= nextDevourStart - 0.0001f)
            {
                BeginDevour();
            }

            if (devourActive && elapsedSeconds >= devourWindupEnd - 0.0001f)
            {
                ResolveDevour();
            }

            if (!summonActive && elapsedSeconds >= nextSummonStart - 0.0001f)
            {
                BeginSummon();
            }

            if (summonActive && elapsedSeconds >= summonWindupEnd - 0.0001f)
            {
                ResolveSummon();
            }
        }

        private void BeginDevour()
        {
            var candidates = targets.GetEligibleTargets();
            if (candidates == null || candidates.Count == 0)
            {
                DevourCastCount++;
                nextDevourStart = elapsedSeconds + WorldeaterWyrmConfiguration.DevourCooldownSeconds;
                Emit(WorldeaterCastKind.Devour, WorldeaterCastOutcome.NoTarget, DevourCastCount, 0f, string.Empty);
                return;
            }

            lockedTarget = SelectTarget(candidates);
            hasLockedTarget = true;
            devourActive = true;
            devourWindupEnd = elapsedSeconds + WorldeaterWyrmConfiguration.DevourWindupSeconds;
            DevourCastCount++;
            Emit(WorldeaterCastKind.Devour, WorldeaterCastOutcome.Started, DevourCastCount, 0f, lockedTarget.RuntimeId);
        }

        private void ResolveDevour()
        {
            devourActive = false;
            nextDevourStart = elapsedSeconds + WorldeaterWyrmConfiguration.DevourCooldownSeconds;
            if (!hasLockedTarget)
            {
                return;
            }

            if (!targets.IsStillEligible(lockedTarget))
            {
                hasLockedTarget = false;
                Emit(WorldeaterCastKind.Devour, WorldeaterCastOutcome.TargetInvalid, DevourCastCount, 0f, lockedTarget.RuntimeId);
                return;
            }

            var attempt = new BossCastAttempt(
                definition.BossId,
                new BossSkillId(WorldeaterWyrmConfiguration.SkillId),
                DevourCastCount,
                elapsedSeconds,
                true,
                true);
            var breaker = spellbreaker == null
                ? SpellbreakerOutcome.NotEvaluated
                : spellbreaker.Evaluate(attempt);
            if (breaker == SpellbreakerOutcome.Blocked)
            {
                var reflection = boss.MaxHitPoints * WorldeaterWyrmConfiguration.SpellbreakerReflectionFraction;
                boss.ApplyReflectedDamage(reflection);
                Emit(WorldeaterCastKind.Devour, WorldeaterCastOutcome.Blocked, DevourCastCount, reflection, lockedTarget.RuntimeId);
                hasLockedTarget = false;
                return;
            }

            if (targets.Consume(lockedTarget))
            {
                var fraction = lockedTarget.TargetClass == WorldeaterTargetClass.Basic
                    ? WorldeaterWyrmConfiguration.BasicGrowthFraction
                    : lockedTarget.TargetClass == WorldeaterTargetClass.Minion
                        ? WorldeaterWyrmConfiguration.MinionGrowthFraction
                        : WorldeaterWyrmConfiguration.SubBossGrowthFraction;
                boss.AddHealth(boss.InitialMaxHitPoints * fraction);
                Emit(WorldeaterCastKind.Devour, WorldeaterCastOutcome.Resolved, DevourCastCount, 0f, lockedTarget.RuntimeId);
            }
            else
            {
                Emit(WorldeaterCastKind.Devour, WorldeaterCastOutcome.TargetInvalid, DevourCastCount, 0f, lockedTarget.RuntimeId);
            }

            hasLockedTarget = false;
        }

        private void BeginSummon()
        {
            summonActive = true;
            summonWindupEnd = elapsedSeconds + WorldeaterWyrmConfiguration.SummonWindupSeconds;
            SummonCastCount++;
            Emit(WorldeaterCastKind.Summon, WorldeaterCastOutcome.Started, SummonCastCount, 0f, string.Empty);
        }

        private void ResolveSummon()
        {
            summonActive = false;
            nextSummonStart = elapsedSeconds + WorldeaterWyrmConfiguration.SummonCooldownSeconds;
            var attempt = new BossCastAttempt(
                definition.BossId,
                new BossSkillId(WorldeaterWyrmConfiguration.SummonSkillId),
                SummonCastCount,
                elapsedSeconds,
                true,
                true);
            var breaker = spellbreaker == null
                ? SpellbreakerOutcome.NotEvaluated
                : spellbreaker.Evaluate(attempt);
            if (breaker == SpellbreakerOutcome.Blocked)
            {
                var reflection = boss.MaxHitPoints * WorldeaterWyrmConfiguration.SpellbreakerReflectionFraction;
                boss.ApplyReflectedDamage(reflection);
                Emit(WorldeaterCastKind.Summon, WorldeaterCastOutcome.Blocked, SummonCastCount, reflection, string.Empty);
                return;
            }

            summons.SpawnMinions(
                WorldeaterWyrmConfiguration.SummonCount,
                WorldeaterWyrmConfiguration.MinionMaxHitPoints,
                WorldeaterWyrmConfiguration.MinionMoveSpeedCellsPerSecond);
            Emit(WorldeaterCastKind.Summon, WorldeaterCastOutcome.Resolved, SummonCastCount, 0f, string.Empty);
        }

        private static WorldeaterTarget SelectTarget(IReadOnlyList<WorldeaterTarget> candidates)
        {
            var selected = candidates[0];
            for (var index = 1; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.TargetClass < selected.TargetClass ||
                    (candidate.TargetClass == selected.TargetClass &&
                     (candidate.StoredLevel < selected.StoredLevel ||
                      (candidate.StoredLevel == selected.StoredLevel &&
                       string.CompareOrdinal(candidate.RuntimeId, selected.RuntimeId) < 0))))
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private void Emit(
            WorldeaterCastKind kind,
            WorldeaterCastOutcome outcome,
            int castNumber,
            float reflectionDamage,
            string targetRuntimeId)
        {
            CastEvent?.Invoke(new WorldeaterCastEvent(
                kind,
                outcome,
                castNumber,
                elapsedSeconds,
                outcome == WorldeaterCastOutcome.Resolved && kind == WorldeaterCastKind.Summon
                    ? WorldeaterWyrmConfiguration.SummonCount
                    : 0,
                reflectionDamage,
                targetRuntimeId));
        }
    }
}
