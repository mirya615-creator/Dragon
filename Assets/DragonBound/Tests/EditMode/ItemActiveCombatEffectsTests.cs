using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Items;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemActiveCombatEffectsTests
    {
        [Test]
        public void ActiveCatalog_ImplementsAllSixAGroupEffects()
        {
            Assert.AreEqual(20, ItemCatalog.FormalCandidates.Count);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.WyrmfangSnare).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.RuneburstMine).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.FrenzyRune).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.RuneOfTempering).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.WarforgeSigil).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.DragonfallJudgment).IsFormalCandidate);
        }

        [Test]
        public void WyrmfangSnare_UsesNormalFractionAndBossCap()
        {
            var team = new TeamState(TeamSide.Player);
            var registry = new EnemyRegistry();
            var normal = new EnemyRuntime("normal", TeamSide.Player, 100f, EnemyArchetype.Normal);
            var boss = new EnemyRuntime("boss", TeamSide.Player, 5000f, EnemyArchetype.Boss);
            registry.Register(normal);
            registry.Register(boss);
            var context = new ItemRunContext(team, registry);
            var effect = new WyrmfangSnareEffect();

            Assert.IsTrue(effect.TryActivate(context, out var reason), reason);
            Assert.AreEqual(40f, effect.LastDamage, 0.001f);
            effect.Tick(context, 45f);
            context.SetActivationTarget("boss");
            Assert.IsTrue(effect.TryActivate(context, out reason), reason);
            Assert.AreEqual(120f, effect.LastDamage, 0.001f);
        }

        [Test]
        public void RuneburstMine_UsesOnePointTwoFiveAreaAndBossPercentageCap()
        {
            var team = new TeamState(TeamSide.Player);
            var registry = new EnemyRegistry();
            var first = new EnemyRuntime("first", TeamSide.Player, 100f, EnemyArchetype.Normal);
            var second = new EnemyRuntime("second", TeamSide.Player, 100f, EnemyArchetype.Normal);
            var boss = new EnemyRuntime("boss", TeamSide.Player, 5000f, EnemyArchetype.Boss);
            first.SetCombatPosition(new CombatPoint(0f, 0f));
            second.SetCombatPosition(new CombatPoint(1f, 0f));
            boss.SetCombatPosition(new CombatPoint(1.2f, 0f));
            registry.Register(first);
            registry.Register(second);
            registry.Register(boss);
            var context = new ItemRunContext(team, registry);
            context.SetActivationPoint(new CombatPoint(0f, 0f));
            var effect = new RuneburstMineEffect();

            Assert.IsTrue(effect.TryActivate(context, out var reason), reason);
            Assert.AreEqual(3, effect.LastAffectedEnemyCount);
            Assert.AreEqual(240f, effect.LastTotalDamage, 0.001f);
            Assert.AreEqual(4920f, boss.HitPoints, 0.001f);
        }

        [Test]
        public void RuneburstMine_EmptyPlacementDoesNotStartCooldown()
        {
            var team = new TeamState(TeamSide.Player);
            var registry = new EnemyRegistry();
            var enemy = new EnemyRuntime("enemy", TeamSide.Player, 100f, EnemyArchetype.Normal);
            enemy.SetCombatPosition(new CombatPoint(5f, 0f));
            registry.Register(enemy);
            var context = new ItemRunContext(team, registry);
            context.SetActivationPoint(new CombatPoint(0f, 0f));
            var effect = new RuneburstMineEffect();

            Assert.IsFalse(effect.TryActivate(context, out var reason));
            Assert.AreEqual("NoAliveTargets", reason);
            Assert.AreEqual(0f, effect.CooldownRemainingSeconds, 0.001f);
            Assert.AreEqual(100f, enemy.HitPoints, 0.001f);
        }

        [Test]
        public void FrenzyRune_MultipliesAttackSpeedAtMostTwicePerUnit()
        {
            var team = new TeamState(TeamSide.Player);
            var units = new ItemCombatUnitRegistry();
            var unit = new ItemCombatUnitState("basic-1", ItemCombatUnitKind.Basic);
            units.Register(unit);
            var effect = new FrenzyRuneEffect();
            var context = new ItemRunContext(team, new EnemyRegistry(), units);
            context.SetActivationTarget(unit.RuntimeId);

            Assert.IsTrue(effect.TryActivate(context, out var reason), reason);
            effect.Tick(context, 60f);
            Assert.IsTrue(effect.TryActivate(context, out reason), reason);
            effect.Tick(context, 60f);
            Assert.IsFalse(effect.TryActivate(context, out reason));
            Assert.AreEqual("MaxActivationsPerUnit", reason);
            Assert.AreEqual(1.96f, unit.AttackSpeedMultiplier, 0.001f);
        }

        [Test]
        public void UnitTargetedActiveItem_DoesNotFallBackToAnAutomaticTarget()
        {
            var team = new TeamState(TeamSide.Player);
            var units = new ItemCombatUnitRegistry();
            var unit = new ItemCombatUnitState("basic-1", ItemCombatUnitKind.Basic);
            units.Register(unit);
            var effect = new FrenzyRuneEffect();
            var context = new ItemRunContext(team, new EnemyRegistry(), units);

            Assert.IsFalse(effect.TryActivate(context, out var reason));
            Assert.AreEqual("NoAliveTargets", reason);
            Assert.AreEqual(1f, unit.AttackSpeedMultiplier, 0.001f);
            Assert.AreEqual(0f, effect.CooldownRemainingSeconds, 0.001f);
        }

        [Test]
        public void RuneOfTempering_ClampsAtLevelBoundariesAndUsesTypedProgression()
        {
            var team = new TeamState(TeamSide.Player);
            var units = new ItemCombatUnitRegistry();
            var unit = new ItemCombatUnitState("hero-1", ItemCombatUnitKind.Hero, 1, 3);
            units.Register(unit);
            var effect = new RuneOfTemperingEffect();
            var context = new ItemRunContext(team, new EnemyRegistry(), units, 123);
            context.SetActivationTarget(unit.RuntimeId);

            Assert.IsTrue(effect.TryActivate(context, out var reason), reason);
            Assert.That(effect.LastLevelDelta, Is.EqualTo(1).Or.EqualTo(-1));
            Assert.That(unit.Level, Is.InRange(1, 2));
        }

        [Test]
        public void WarforgeSigil_UsesHeroProgressionPortAndCooldown()
        {
            var team = new TeamState(TeamSide.Player);
            var units = new ItemCombatUnitRegistry();
            var hero = new ItemCombatUnitState("hero-1", ItemCombatUnitKind.Hero, 1, 5, 20);
            units.Register(hero);
            var effect = new WarforgeSigilEffect();
            var context = new ItemRunContext(team, new EnemyRegistry(), units);
            context.SetActivationTarget(hero.RuntimeId);

            Assert.IsTrue(effect.TryActivate(context, out var reason), reason);
            Assert.IsTrue(effect.LastUsedHeroProgression);
            Assert.AreEqual(2, hero.Level);
            Assert.AreEqual(20, hero.Experience);
            Assert.IsFalse(effect.TryActivate(context, out reason));
            Assert.AreEqual("Cooldown", reason);
        }

        [Test]
        public void DragonfallJudgment_TriggersOnceAndLeavesWorldeaterMinionPending()
        {
            var team = new TeamState(TeamSide.Player);
            var registry = new EnemyRegistry();
            var normal = new EnemyRuntime("normal", TeamSide.Player, 100f, EnemyArchetype.Normal, 1);
            var minion = new EnemyRuntime("minion", TeamSide.Player, 330f, EnemyArchetype.Swarm, 2, "BOSS_WORLDEATER_WYRM");
            registry.Register(normal);
            registry.Register(minion);
            var context = new ItemRunContext(team, registry);
            var effect = new DragonfallJudgmentEffect();

            effect.HandleCombatEvent(context, new ItemCombatEvent(ItemCombatEventKind.EnemyApproachingGoal, TeamSide.Player, "normal"));
            Assert.IsTrue(effect.Used);
            Assert.AreEqual(20f, normal.HitPoints, 0.001f);
            effect.HandleCombatEvent(context, new ItemCombatEvent(ItemCombatEventKind.EnemyApproachingGoal, TeamSide.Player, "minion"));
            Assert.IsFalse(effect.WorldeaterMinionInteractionPending);

            var second = new DragonfallJudgmentEffect();
            second.HandleCombatEvent(context, new ItemCombatEvent(ItemCombatEventKind.EnemyApproachingGoal, TeamSide.Player, "minion"));
            Assert.IsTrue(second.WorldeaterMinionInteractionPending);
            Assert.AreEqual(330f, minion.HitPoints, 0.001f);
        }
    }
}
