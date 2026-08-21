using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;

namespace DragonBound.Runes
{
    public enum RuneCombatEventType { OnBasicAttackSucceeded, OnHeroKill, OnFirstBasicHitTarget, OnNthBasicAttack, OnHeroLevelUp, RuntimeHeroSpawned, RuntimeHeroStopped, TargetChanged, RunEnded }
    public enum RuneDamageSourceType { HeroBasic, RuneDerived }

    public readonly struct RuneCombatEvent
    {
        public RuneCombatEvent(RuneCombatEventType type, string heroRuntimeId, string heroId, TeamSide side, string targetRuntimeId = "", bool killed = false)
        { Type = type; HeroRuntimeId = heroRuntimeId ?? string.Empty; HeroId = heroId ?? string.Empty; Side = side; TargetRuntimeId = targetRuntimeId ?? string.Empty; Killed = killed; }
        public RuneCombatEventType Type { get; }
        public string HeroRuntimeId { get; }
        public string HeroId { get; }
        public TeamSide Side { get; }
        public string TargetRuntimeId { get; }
        public bool Killed { get; }
    }
    public readonly struct RuneDamageContext
    {
        public RuneDamageContext(string heroRuntimeId, string heroId, TeamSide side, float amount, RuneDamageSourceType sourceType)
        { HeroRuntimeId = heroRuntimeId ?? string.Empty; HeroId = heroId ?? string.Empty; Side = side; Amount = amount; SourceType = sourceType; }
        public string HeroRuntimeId { get; }
        public string HeroId { get; }
        public TeamSide Side { get; }
        public float Amount { get; }
        public RuneDamageSourceType SourceType { get; }
        public CombatDamageOwner ToCombatOwner() { return new CombatDamageOwner(CombatDamageOwnerKind.Hero, Side, HeroRuntimeId, HeroId); }
    }

    /// <summary>Rune execution input expressed only through the Combat target port.</summary>
    public readonly struct RuneTargetCombatContext
    {
        public RuneTargetCombatContext(
            CombatPoint origin,
            float attackDamage,
            float effectiveRange,
            IRuneCombatTargetRegistry targets)
        {
            Origin = origin;
            AttackDamage = attackDamage;
            EffectiveRange = effectiveRange;
            Targets = targets;
        }

        public CombatPoint Origin { get; }
        public float AttackDamage { get; }
        public float EffectiveRange { get; }
        public IRuneCombatTargetRegistry Targets { get; }
    }

    /// <summary>Rune execution output that does not expose an EnemyRuntime.</summary>
    public readonly struct RuneTargetDamageResult
    {
        private RuneTargetDamageResult(
            AttackKind kind,
            IRuneCombatTarget target,
            float damage,
            bool killed,
            float effectRadius,
            bool isWarcry,
            CombatPoint warcryCenter,
            float warcryMultiplier,
            float warcryDuration,
            float shieldDamage,
            float healthDamage)
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

        public RuneTargetDamageResult(
            AttackKind kind,
            IRuneCombatTarget target,
            float damage,
            bool killed,
            float effectRadius = 0f,
            float shieldDamage = 0f,
            float healthDamage = 0f)
            : this(kind, target, damage, killed, effectRadius, false, default(CombatPoint), 1f, 0f, shieldDamage, healthDamage)
        {
        }

        public AttackKind Kind { get; }
        public IRuneCombatTarget Target { get; }
        public string TargetRuntimeId => Target == null ? string.Empty : Target.RuntimeId;
        public float Damage { get; }
        public bool Killed { get; }
        public float EffectRadius { get; }
        public bool IsWarcry { get; }
        public CombatPoint WarcryCenter { get; }
        public float WarcryMultiplier { get; }
        public float WarcryDuration { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }

        public static RuneTargetDamageResult CreateWarcry(CombatPoint center, float radius, float multiplier, float duration)
        {
            return new RuneTargetDamageResult(default(AttackKind), null, 0f, false, radius, true, center, multiplier, duration, 0f, 0f);
        }
    }

    public sealed class RuneRuntimeState
    {
        public int BasicAttackCounter { get; private set; }
        public float CooldownRemaining { get; private set; }
        public readonly HashSet<string> FirstHitTargets = new HashSet<string>(StringComparer.Ordinal);
        public bool HasSummon { get; private set; }
        public float TemporaryBuffRemaining { get; private set; }
        public void RecordBasicAttack() { BasicAttackCounter++; }
        public void Tick(float seconds) { if (seconds > 0f) { CooldownRemaining = Math.Max(0f, CooldownRemaining - seconds); TemporaryBuffRemaining = Math.Max(0f, TemporaryBuffRemaining - seconds); } }
        public bool TryStartCooldown(float seconds) { if (CooldownRemaining > 0f) return false; CooldownRemaining = Math.Max(0f, seconds); return true; }
        public void SetSummon(bool active) { HasSummon = active; }
        public void SetTemporaryBuff(float seconds) { TemporaryBuffRemaining = Math.Max(TemporaryBuffRemaining, seconds); }
    }

    public sealed class RuneEventLayer
    {
        private readonly Dictionary<string, RuneRuntimeState> states = new Dictionary<string, RuneRuntimeState>(StringComparer.Ordinal);
        public event Action<RuneCombatEvent> Emitted;
        public RuneRuntimeState GetOrCreate(string heroRuntimeId) { RuneRuntimeState state; if (!states.TryGetValue(heroRuntimeId, out state)) { state = new RuneRuntimeState(); states.Add(heroRuntimeId, state); } return state; }
        public void Emit(RuneCombatEvent value) { if (value.Type == RuneCombatEventType.OnBasicAttackSucceeded) GetOrCreate(value.HeroRuntimeId).RecordBasicAttack(); if (Emitted != null) Emitted(value); }
        public void Tick(float seconds) { foreach (var state in states.Values) state.Tick(seconds); }
        public void Clear() { states.Clear(); }
    }
}
