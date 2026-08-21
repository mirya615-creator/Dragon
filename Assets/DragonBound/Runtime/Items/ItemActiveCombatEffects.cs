using System;
using System.Collections.Generic;
using DragonBound.Core;
using GameShared.Random;

namespace DragonBound.Items
{
    public enum ItemCombatUnitKind
    {
        Basic,
        Hero
    }

    /// <summary>Typed unit state supplied by the Board/Combat integration owner.</summary>
    public sealed class ItemCombatUnitState
    {
        public ItemCombatUnitState(
            string runtimeId,
            ItemCombatUnitKind kind,
            int level = 1,
            int maxLevel = 5,
            int nextLevelExperience = 20,
            string heroId = "",
            bool isBasicArcher = false)
        {
            if (string.IsNullOrWhiteSpace(runtimeId)) throw new ArgumentException("Unit id is required.", nameof(runtimeId));
            if (level < 1 || maxLevel < level) throw new ArgumentOutOfRangeException(nameof(level));
            RuntimeId = runtimeId;
            Kind = kind;
            Level = level;
            MaxLevel = maxLevel;
            NextLevelExperience = nextLevelExperience;
            HeroId = heroId ?? string.Empty;
            IsBasicArcher = isBasicArcher;
            IsAlive = true;
            AttackSpeedMultiplier = 1f;
        }

        public string RuntimeId { get; }
        public ItemCombatUnitKind Kind { get; }
        public string HeroId { get; }
        public bool IsBasicArcher { get; }
        public int Level { get; internal set; }
        public int MaxLevel { get; }
        public int Experience { get; internal set; }
        public int NextLevelExperience { get; internal set; }
        public float AttackSpeedMultiplier { get; internal set; }
        public float RangeMultiplier { get; internal set; } = 1f;
        public bool IsAlive { get; set; }
    }

    public interface IItemUnitProgressionPort
    {
        bool TryAdjustLevel(ItemCombatUnitState unit, int delta, out string reason);
        bool TryCompleteNextHeroLevel(ItemCombatUnitState unit, out string reason);
    }

    /// <summary>Deterministic greybox registry; production adapters can forward these calls to the
    /// existing Basic/Hero upgrade and XP settlement services.</summary>
    public sealed class ItemCombatUnitRegistry : IItemUnitProgressionPort
    {
        private readonly Dictionary<string, ItemCombatUnitState> units =
            new Dictionary<string, ItemCombatUnitState>(StringComparer.Ordinal);

        public IReadOnlyCollection<ItemCombatUnitState> Units => units.Values;
        public IItemUnitProgressionPort Progression => this;

        public bool Register(ItemCombatUnitState unit)
        {
            return unit != null && units.TryAdd(unit.RuntimeId, unit);
        }

        public bool TryGet(string runtimeId, out ItemCombatUnitState unit)
        {
            return units.TryGetValue(runtimeId ?? string.Empty, out unit);
        }

        public bool TryAdjustLevel(ItemCombatUnitState unit, int delta, out string reason)
        {
            reason = ItemOperationFailure.None;
            if (unit == null || !units.ContainsKey(unit.RuntimeId))
            {
                reason = "UnitNotFound";
                return false;
            }

            unit.Level = Math.Max(1, Math.Min(unit.MaxLevel, unit.Level + delta));
            return true;
        }

        public bool TryCompleteNextHeroLevel(ItemCombatUnitState unit, out string reason)
        {
            reason = ItemOperationFailure.None;
            if (unit == null || unit.Kind != ItemCombatUnitKind.Hero || !units.ContainsKey(unit.RuntimeId))
            {
                reason = "HeroProgressionUnavailable";
                return false;
            }

            if (unit.Level >= unit.MaxLevel)
            {
                reason = "MaxLevelReached";
                return false;
            }

            unit.Level++;
            unit.Experience = unit.NextLevelExperience;
            return true;
        }
    }

    public abstract class ItemCooldownEffectBase : IItemEffectRuntime
    {
        protected ItemCooldownEffectBase(float cooldownSeconds)
        {
            CooldownSeconds = cooldownSeconds;
        }

        public abstract string ItemId { get; }
        public float CooldownSeconds { get; }
        public float CooldownRemainingSeconds { get; protected set; }

        public virtual void OnRunStart(ItemRunContext context)
        {
        }

        public virtual void Tick(ItemRunContext context, float deltaSeconds)
        {
            if (deltaSeconds > 0f)
            {
                CooldownRemainingSeconds = Math.Max(0f, CooldownRemainingSeconds - deltaSeconds);
            }
        }

        protected bool BeginActivation(out string reason)
        {
            if (CooldownRemainingSeconds > 0.0001f)
            {
                reason = "Cooldown";
                return false;
            }

            reason = ItemOperationFailure.None;
            return true;
        }

        protected void CompleteActivation()
        {
            CooldownRemainingSeconds = CooldownSeconds;
        }

        public abstract bool TryActivate(ItemRunContext context, out string reason);
        public virtual void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
        }
    }

    internal static class ItemCombatEffectTargeting
    {
        public static EnemyRuntime SelectEnemy(ItemRunContext context, string targetId)
        {
            if (!string.IsNullOrWhiteSpace(targetId) && context.OwnRouteEnemies.TryGet(targetId, out var selected) &&
                selected.Team == context.OwnTeam.Side && selected.IsAlive)
            {
                return selected;
            }

            EnemyRuntime result = null;
            foreach (var enemy in context.OwnRouteEnemies.Enemies)
            {
                if (enemy.Team != context.OwnTeam.Side || !enemy.IsAlive) continue;
                if (result == null || enemy.PathProgress > result.PathProgress ||
                    (Math.Abs(enemy.PathProgress - result.PathProgress) < 0.0001f && enemy.SpawnSequence < result.SpawnSequence))
                {
                    result = enemy;
                }
            }

            return result;
        }

        public static ItemCombatUnitState SelectUnit(ItemRunContext context, string targetId)
        {
            if (!string.IsNullOrWhiteSpace(targetId) && context.UnitRegistry.TryGet(targetId, out var selected) && selected.IsAlive)
            {
                return selected;
            }

            foreach (var unit in context.UnitRegistry.Units)
            {
                if (unit.IsAlive) return unit;
            }

            return null;
        }

        public static float DamageFor(EnemyRuntime enemy, float normalDamage, float bossCap, float bossPercent)
        {
            if (enemy.Archetype == EnemyArchetype.Boss)
            {
                return Math.Min(bossCap, enemy.MaxHitPoints * bossPercent);
            }

            return normalDamage;
        }
    }

    public sealed class WyrmfangSnareEffect : ItemCooldownEffectBase
    {
        public const float NormalMaxHealthFraction = 0.40f;
        public const float BossMaxHealthFraction = 0.05f;
        public const float BossDamageCap = 120f;

        public WyrmfangSnareEffect() : base(45f) { }
        public override string ItemId => Items.ItemIds.WyrmfangSnare;
        public float LastDamage { get; private set; }
        public string LastTargetId { get; private set; }

        public override bool TryActivate(ItemRunContext context, out string reason)
        {
            LastDamage = 0f;
            LastTargetId = string.Empty;
            if (!BeginActivation(out reason)) return false;
            var target = ItemCombatEffectTargeting.SelectEnemy(context, context.ActivationTargetId);
            if (target == null)
            {
                reason = "NoAliveTargets";
                return false;
            }

            LastTargetId = target.RuntimeId;
            LastDamage = target.Archetype == EnemyArchetype.Boss
                ? Math.Min(BossDamageCap, target.MaxHitPoints * BossMaxHealthFraction)
                : target.MaxHitPoints * NormalMaxHealthFraction;
            target.ApplyDamage(LastDamage);
            CompleteActivation();
            return true;
        }
    }

    public sealed class RuneburstMineEffect : ItemCooldownEffectBase
    {
        public const float AreaRadius = 1.25f;
        public const float NormalDamage = 80f;
        public const float BossDamageCap = 80f;
        public const float BossMaxHealthFraction = 0.03f;

        public RuneburstMineEffect() : base(60f) { }
        public override string ItemId => Items.ItemIds.RuneburstMine;
        public int LastAffectedEnemyCount { get; private set; }
        public float LastTotalDamage { get; private set; }

        public override bool TryActivate(ItemRunContext context, out string reason)
        {
            LastAffectedEnemyCount = 0;
            LastTotalDamage = 0f;
            if (!BeginActivation(out reason)) return false;
            var target = ItemCombatEffectTargeting.SelectEnemy(context, context.ActivationTargetId);
            if (target == null)
            {
                reason = "NoAliveTargets";
                return false;
            }

            foreach (var enemy in context.OwnRouteEnemies.Enemies)
            {
                if (enemy.Team != context.OwnTeam.Side || !enemy.IsAlive ||
                    enemy.CombatPosition.DistanceSquared(target.CombatPosition) > AreaRadius * AreaRadius + 0.0001f)
                {
                    continue;
                }

                var damage = ItemCombatEffectTargeting.DamageFor(enemy, NormalDamage, BossDamageCap, BossMaxHealthFraction);
                enemy.ApplyDamage(damage);
                LastAffectedEnemyCount++;
                LastTotalDamage += damage;
            }

            CompleteActivation();
            return true;
        }
    }

    public sealed class FrenzyRuneEffect : ItemCooldownEffectBase
    {
        public const float AttackSpeedMultiplier = 1.40f;
        public const int MaxActivationsPerUnit = 2;

        private readonly Dictionary<string, int> activations = new Dictionary<string, int>(StringComparer.Ordinal);
        public FrenzyRuneEffect() : base(60f) { }
        public override string ItemId => Items.ItemIds.FrenzyRune;

        public override bool TryActivate(ItemRunContext context, out string reason)
        {
            if (!BeginActivation(out reason)) return false;
            var unit = ItemCombatEffectTargeting.SelectUnit(context, context.ActivationTargetId);
            if (unit == null)
            {
                reason = "NoAliveTargets";
                return false;
            }

            activations.TryGetValue(unit.RuntimeId, out var count);
            if (count >= MaxActivationsPerUnit)
            {
                reason = "MaxActivationsPerUnit";
                return false;
            }

            unit.AttackSpeedMultiplier *= AttackSpeedMultiplier;
            activations[unit.RuntimeId] = count + 1;
            CompleteActivation();
            return true;
        }
    }

    public sealed class RuneOfTemperingEffect : ItemCooldownEffectBase
    {
        public const int MaxDelta = 1;
        public RuneOfTemperingEffect() : base(45f) { }
        public override string ItemId => Items.ItemIds.RuneOfTempering;
        public int LastLevelDelta { get; private set; }

        public override bool TryActivate(ItemRunContext context, out string reason)
        {
            LastLevelDelta = 0;
            if (!BeginActivation(out reason)) return false;
            var unit = ItemCombatEffectTargeting.SelectUnit(context, context.ActivationTargetId);
            if (unit == null)
            {
                reason = "NoAliveTargets";
                return false;
            }

            var random = new RunRandom(context.RunSeed);
            LastLevelDelta = random.NextUnit(ItemId + "." + context.NextActivationOrdinal) < 0.5f ? MaxDelta : -MaxDelta;
            if (!context.UnitRegistry.Progression.TryAdjustLevel(unit, LastLevelDelta, out reason))
            {
                LastLevelDelta = 0;
                return false;
            }

            CompleteActivation();
            return true;
        }
    }

    public sealed class WarforgeSigilEffect : ItemCooldownEffectBase
    {
        public WarforgeSigilEffect() : base(90f) { }
        public override string ItemId => Items.ItemIds.WarforgeSigil;
        public bool LastUsedHeroProgression { get; private set; }

        public override bool TryActivate(ItemRunContext context, out string reason)
        {
            LastUsedHeroProgression = false;
            if (!BeginActivation(out reason)) return false;
            var unit = ItemCombatEffectTargeting.SelectUnit(context, context.ActivationTargetId);
            if (unit == null)
            {
                reason = "NoAliveTargets";
                return false;
            }

            if (unit.Kind == ItemCombatUnitKind.Hero)
            {
                if (!context.UnitRegistry.Progression.TryCompleteNextHeroLevel(unit, out reason)) return false;
                LastUsedHeroProgression = true;
            }
            else if (!context.UnitRegistry.Progression.TryAdjustLevel(unit, 1, out reason))
            {
                return false;
            }

            CompleteActivation();
            return true;
        }
    }

    public sealed class DragonfallJudgmentEffect : IItemEffectRuntime
    {
        public const float NormalMaxHealthFraction = 0.80f;
        public const float BossMaxHealthFraction = 0.08f;
        public const float BossDamageCap = 200f;

        public string ItemId => Items.ItemIds.DragonfallJudgment;
        public bool Used { get; private set; }
        public bool WorldeaterMinionInteractionPending { get; private set; }
        public float LastDamage { get; private set; }

        public void OnRunStart(ItemRunContext context) { }
        public void Tick(ItemRunContext context, float deltaSeconds) { }

        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "EventDriven";
            return false;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
            if (Used || combatEvent.Kind != ItemCombatEventKind.EnemyApproachingGoal) return;
            if (!context.OwnRouteEnemies.TryGet(combatEvent.RuntimeId, out var enemy) || !enemy.IsAlive) return;
            if (enemy.Archetype == EnemyArchetype.Swarm && enemy.BossId.IndexOf("WORLDEATER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                WorldeaterMinionInteractionPending = true;
                return;
            }

            LastDamage = enemy.Archetype == EnemyArchetype.Boss
                ? Math.Min(BossDamageCap, enemy.MaxHitPoints * BossMaxHealthFraction)
                : enemy.MaxHitPoints * NormalMaxHealthFraction;
            enemy.ApplyDamage(LastDamage);
            Used = true;
        }
    }
}
