using System;
using DragonBound.Core;
using GameShared.Random;

namespace DragonBound.Items
{
    public interface IItemBenchCapacityPort
    {
        int Capacity { get; }
        bool TryIncrease(int amount, bool noStack, out string reason);
    }

    public sealed class ItemBenchCapacityState : IItemBenchCapacityPort
    {
        public int Capacity { get; private set; }

        public bool TryIncrease(int amount, bool noStack, out string reason)
        {
            reason = ItemOperationFailure.None;
            if (amount <= 0)
            {
                reason = "InvalidCapacityIncrease";
                return false;
            }

            if (noStack && Capacity > 0) return true;
            Capacity += amount;
            return true;
        }
    }

    public sealed class ItemBossCastAttempt
    {
        public ItemBossCastAttempt(string bossId, float maxHitPoints)
        {
            BossId = bossId ?? string.Empty;
            MaxHitPoints = maxHitPoints;
        }

        public string BossId { get; }
        public float MaxHitPoints { get; }
    }

    public interface IItemSpellbreakerPort
    {
        bool TryBlockBossCast(ItemBossCastAttempt attempt, out string reason);
    }

    public sealed class PactOfEnduranceEffect : IItemEffectRuntime
    {
        public const int OwnHeartBonus = 5;
        public const int OpponentHeartBonus = 3;
        public string ItemId => Items.ItemIds.PactOfEndurance;
        public bool Applied { get; private set; }

        public void OnRunStart(ItemRunContext context)
        {
            if (Applied) return;
            context.OwnTeam.ApplyHatchlingHealthBonus(OwnHeartBonus);
            context.OpposingTeam?.ApplyHatchlingHealthBonus(OpponentHeartBonus);
            Applied = true;
        }

        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }
        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }

    public sealed class FarwatchCrestEffect : IItemEffectRuntime
    {
        public const float RangeMultiplier = 2f;
        public string ItemId => Items.ItemIds.FarwatchCrest;
        public int LastAffectedUnitCount { get; private set; }

        public void OnRunStart(ItemRunContext context)
        {
            LastAffectedUnitCount = 0;
            foreach (var unit in context.UnitRegistry.Units)
            {
                if ((unit.Kind == ItemCombatUnitKind.Hero &&
                     (unit.HeroId == "HERO_SKYHUNTER_VALKYRIE" || unit.HeroId == "HERO_WINDCLAW_RANGER")) ||
                    (unit.Kind == ItemCombatUnitKind.Basic && unit.IsBasicArcher))
                {
                    unit.RangeMultiplier *= RangeMultiplier;
                    LastAffectedUnitCount++;
                }
            }
        }

        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }
        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }

    public sealed class FrostMireEffect : IItemEffectRuntime
    {
        public const float SlowFraction = 0.10f;
        private const float RunDurationSeconds = 1000000f;
        public string ItemId => Items.ItemIds.FrostMire;
        public int LastAffectedEnemyCount { get; private set; }

        public void OnRunStart(ItemRunContext context)
        {
            LastAffectedEnemyCount = 0;
            foreach (var enemy in context.OwnRouteEnemies.Enemies)
            {
                if (enemy.Team == context.OwnTeam.Side && enemy.IsAlive &&
                    enemy.ApplyMovementSlow(SlowFraction, RunDurationSeconds))
                {
                    LastAffectedEnemyCount++;
                }
            }
        }

        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }
        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
            if (combatEvent.Kind != ItemCombatEventKind.EnemySpawned ||
                !context.OwnRouteEnemies.TryGet(combatEvent.RuntimeId, out var enemy) ||
                enemy.Team != context.OwnTeam.Side || !enemy.IsAlive)
            {
                return;
            }

            if (enemy.ApplyMovementSlow(SlowFraction, RunDurationSeconds))
            {
                LastAffectedEnemyCount++;
            }
        }
    }

    public sealed class WarTempoEffect : IItemEffectRuntime
    {
        public const float AttackSpeedMultiplier = 1.10f;
        public string ItemId => Items.ItemIds.WarTempo;

        public void OnRunStart(ItemRunContext context)
        {
            Apply(context.UnitRegistry);
            Apply(context.OpposingUnitRegistry);
        }

        private static void Apply(ItemCombatUnitRegistry registry)
        {
            if (registry == null) return;
            foreach (var unit in registry.Units) unit.AttackSpeedMultiplier *= AttackSpeedMultiplier;
        }

        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }
        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }

    public sealed class VeteransMarkEffect : IItemEffectRuntime
    {
        public const float DirectLevelTwoChance = 0.05f;
        private int recruitOrdinal;
        public string ItemId => Items.ItemIds.VeteransMark;
        public int LastPromotedCount { get; private set; }

        public void OnRunStart(ItemRunContext context) { }
        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "EventDriven";
            return false;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent)
        {
            if (combatEvent.Kind != ItemCombatEventKind.RecruitSucceeded ||
                !context.UnitRegistry.TryGet(combatEvent.RuntimeId, out var unit) ||
                unit.Kind != ItemCombatUnitKind.Basic || unit.Level != 1)
            {
                return;
            }

            recruitOrdinal++;
            var random = new RunRandom(context.RunSeed);
            if (random.NextUnit(ItemId + "." + recruitOrdinal) >= DirectLevelTwoChance) return;
            if (context.UnitRegistry.Progression.TryAdjustLevel(unit, 1, out _)) LastPromotedCount++;
        }
    }

    public sealed class QuartermasterSatchelEffect : IItemEffectRuntime
    {
        public const int BenchBonus = 1;
        public string ItemId => Items.ItemIds.QuartermastersSatchel;
        public bool Applied { get; private set; }

        public void OnRunStart(ItemRunContext context)
        {
            if (Applied) return;
            Applied = context.BenchCapacity.TryIncrease(BenchBonus, true, out _);
        }

        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }
        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }

    public sealed class SpellbreakerSealEffect : IItemEffectRuntime
    {
        public const float BlockChance = 0.50f;
        public const float ReflectionFraction = 0.10f;
        public string ItemId => Items.ItemIds.SpellbreakerSeal;
        public int EvaluatedCastCount { get; private set; }
        public int BlockedCastCount { get; private set; }

        public void OnRunStart(ItemRunContext context) { }
        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "EventDriven";
            return false;
        }

        public bool TryBlockBossCast(ItemRunContext context, ItemBossCastAttempt attempt, out string reason)
        {
            reason = ItemOperationFailure.None;
            if (attempt == null || attempt.MaxHitPoints <= 0f)
            {
                reason = "InvalidBossCast";
                return false;
            }

            EvaluatedCastCount++;
            bool blocked;
            if (context.Spellbreaker != null)
            {
                blocked = context.Spellbreaker.TryBlockBossCast(attempt, out reason);
            }
            else
            {
                var random = new RunRandom(context.RunSeed);
                blocked = random.NextUnit(ItemId + "." + EvaluatedCastCount) < BlockChance;
            }

            if (blocked) BlockedCastCount++;
            return blocked;
        }

        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }

    public sealed class RivalryOathEffect : IItemEffectRuntime
    {
        public const float OwnAttackSpeedMultiplier = 1.50f;
        public const float OpponentAttackSpeedMultiplier = 1.30f;
        public string ItemId => Items.ItemIds.RivalryOath;

        public void OnRunStart(ItemRunContext context)
        {
            Apply(context.UnitRegistry, OwnAttackSpeedMultiplier);
            Apply(context.OpposingUnitRegistry, OpponentAttackSpeedMultiplier);
        }

        private static void Apply(ItemCombatUnitRegistry registry, float multiplier)
        {
            if (registry == null) return;
            foreach (var unit in registry.Units) unit.AttackSpeedMultiplier *= multiplier;
        }

        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }
        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }

    public sealed class DraconicPresenceEffect : IItemEffectRuntime
    {
        public const float SlowPerHero = 0.02f;
        public const float MaxSlow = 0.10f;
        private const float RunDurationSeconds = 1000000f;
        public string ItemId => Items.ItemIds.DraconicPresence;
        public int HeroCount { get; private set; }
        public float AppliedSlowFraction { get; private set; }

        public void OnRunStart(ItemRunContext context)
        {
            HeroCount = 0;
            foreach (var unit in context.UnitRegistry.Units)
            {
                if (unit.Kind == ItemCombatUnitKind.Hero && unit.IsAlive) HeroCount++;
            }

            AppliedSlowFraction = Math.Min(MaxSlow, HeroCount * SlowPerHero);
            if (AppliedSlowFraction <= 0f) return;
            foreach (var enemy in context.OwnRouteEnemies.Enemies)
            {
                if (enemy.Team == context.OwnTeam.Side && enemy.IsAlive)
                {
                    enemy.ApplyMovementSlow(AppliedSlowFraction, RunDurationSeconds);
                }
            }
        }

        public void Tick(ItemRunContext context, float deltaSeconds) { }
        public bool TryActivate(ItemRunContext context, out string reason)
        {
            reason = "PassiveOnly";
            return false;
        }
        public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
    }
}
