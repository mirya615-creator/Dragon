using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Runes;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneEffectExecutionEngineBoundaryTests
    {
        [Test]
        public void LegacyAndEngine_SkybreakerPreserveTargetsDamageKillShieldHealthAndRandomKey()
        {
            var legacyRegistry = CreateSkybreakerRegistry(out var legacyPrimary, out _);
            var portRegistry = CreateSkybreakerRegistry(out var portPrimary, out _);
            var legacyRandom = new RecordingRandom(0f);
            var engineRandom = new RecordingRandom(0f);
            var legacy = new RuneEffectExecutor(RuneCatalog.Get("Skybreaker"), legacyRandom, "hero.compare");
            var engine = new RuneEffectExecutionEngine(RuneCatalog.Get("Skybreaker"), engineRandom, "hero.compare");

            var legacyResults = legacy.OnBasicAttackSucceeded(
                new RuneCombatContext(default(CombatPoint), 100f, 3f, legacyRegistry),
                legacyPrimary);
            var engineResults = engine.OnBasicAttackSucceeded(
                new RuneTargetCombatContext(
                    default(CombatPoint),
                    100f,
                    3f,
                    new EnemyRegistryRuneCombatTargetRegistry(portRegistry)),
                new EnemyRuntimeRuneCombatTarget(portPrimary));

            Assert.AreEqual(2, legacyResults.Count);
            Assert.AreEqual(2, engineResults.Count);
            Assert.AreEqual(legacyResults[0].Target.RuntimeId, engineResults[0].TargetRuntimeId);
            Assert.AreEqual(legacyResults[0].Damage, engineResults[0].Damage, .0001f);
            Assert.AreEqual(legacyResults[0].Killed, engineResults[0].Killed);
            Assert.AreEqual(60f, legacyResults[0].ShieldDamage, .0001f);
            Assert.AreEqual(100f, legacyResults[0].HealthDamage, .0001f);
            Assert.AreEqual(legacyResults[0].ShieldDamage, engineResults[0].ShieldDamage, .0001f);
            Assert.AreEqual(legacyResults[0].HealthDamage, engineResults[0].HealthDamage, .0001f);
            Assert.IsTrue(legacyResults[0].Killed);
            CollectionAssert.AreEqual(legacyRandom.Contexts, engineRandom.Contexts);
            CollectionAssert.AreEqual(
                new[] { "RuneDrop.V1.Combat.Skybreaker.hero.compare.Skybreaker.0" },
                legacyRandom.Contexts);
        }

        [Test]
        public void LegacyAndEngine_FrostbitePreserveNormalAndBossSlowSemantics()
        {
            var legacyRegistry = new EnemyRegistry();
            var portRegistry = new EnemyRegistry();
            var legacyNormal = new EnemyRuntime("normal", TeamSide.Player);
            var legacyBoss = new EnemyRuntime("boss", TeamSide.Player, 100f, EnemyArchetype.Boss);
            var portNormal = new EnemyRuntime("normal", TeamSide.Player);
            var portBoss = new EnemyRuntime("boss", TeamSide.Player, 100f, EnemyArchetype.Boss);
            legacyRegistry.Register(legacyNormal);
            legacyRegistry.Register(legacyBoss);
            portRegistry.Register(portNormal);
            portRegistry.Register(portBoss);
            var legacy = new RuneEffectExecutor(RuneCatalog.Get("Frostbite"), new RecordingRandom(.99f), "hero.compare");
            var engine = new RuneEffectExecutionEngine(RuneCatalog.Get("Frostbite"), new RecordingRandom(.99f), "hero.compare");
            var legacyContext = new RuneCombatContext(default(CombatPoint), 10f, 3f, legacyRegistry);
            var portContext = new RuneTargetCombatContext(
                default(CombatPoint), 10f, 3f, new EnemyRegistryRuneCombatTargetRegistry(portRegistry));

            legacy.OnBasicAttackSucceeded(legacyContext, legacyNormal);
            legacy.OnBasicAttackSucceeded(legacyContext, legacyBoss);
            engine.OnBasicAttackSucceeded(portContext, new EnemyRuntimeRuneCombatTarget(portNormal));
            engine.OnBasicAttackSucceeded(portContext, new EnemyRuntimeRuneCombatTarget(portBoss));

            Assert.AreEqual(legacyNormal.MovementSpeedMultiplier, portNormal.MovementSpeedMultiplier, .0001f);
            Assert.AreEqual(legacyNormal.MovementSlowRemainingSeconds, portNormal.MovementSlowRemainingSeconds, .0001f);
            Assert.AreEqual(legacyBoss.MovementSpeedMultiplier, portBoss.MovementSpeedMultiplier, .0001f);
            Assert.AreEqual(legacyBoss.MovementSlowRemainingSeconds, portBoss.MovementSlowRemainingSeconds, .0001f);
            Assert.AreEqual(.90f, portNormal.MovementSpeedMultiplier, .0001f);
            Assert.AreEqual(.95f, portBoss.MovementSpeedMultiplier, .0001f);
        }

        [Test]
        public void LegacyAndEngine_WarcryPreserveCommandAndRandomCallOrder()
        {
            var legacyRegistry = new EnemyRegistry();
            var portRegistry = new EnemyRegistry();
            var legacyTarget = new EnemyRuntime("target", TeamSide.Player);
            var portTarget = new EnemyRuntime("target", TeamSide.Player);
            legacyRegistry.Register(legacyTarget);
            portRegistry.Register(portTarget);
            var legacyRandom = new RecordingRandom(0f);
            var engineRandom = new RecordingRandom(0f);
            var legacy = new RuneEffectExecutor(RuneCatalog.Get("Warcry"), legacyRandom, "hero.compare");
            var engine = new RuneEffectExecutionEngine(RuneCatalog.Get("Warcry"), engineRandom, "hero.compare");

            var legacyResult = legacy.OnBasicAttackSucceeded(
                new RuneCombatContext(new CombatPoint(2f, 1f), 10f, 3f, legacyRegistry), legacyTarget);
            var engineResult = engine.OnBasicAttackSucceeded(
                new RuneTargetCombatContext(
                    new CombatPoint(2f, 1f),
                    10f,
                    3f,
                    new EnemyRegistryRuneCombatTargetRegistry(portRegistry)),
                new EnemyRuntimeRuneCombatTarget(portTarget));

            Assert.AreEqual(1, legacyResult.Count);
            Assert.AreEqual(1, engineResult.Count);
            Assert.IsTrue(legacyResult[0].IsWarcry);
            Assert.IsTrue(engineResult[0].IsWarcry);
            Assert.AreEqual(legacyResult[0].WarcryCenter, engineResult[0].WarcryCenter);
            Assert.AreEqual(legacyResult[0].WarcryMultiplier, engineResult[0].WarcryMultiplier, .0001f);
            Assert.AreEqual(legacyResult[0].WarcryDuration, engineResult[0].WarcryDuration, .0001f);
            CollectionAssert.AreEqual(legacyRandom.Contexts, engineRandom.Contexts);
            CollectionAssert.AreEqual(
                new[] { "RuneDrop.V1.Combat.Warcry.hero.compare.Warcry.0" },
                legacyRandom.Contexts);
        }

        private static EnemyRegistry CreateSkybreakerRegistry(out EnemyRuntime primary, out EnemyRuntime secondary)
        {
            var registry = new EnemyRegistry();
            primary = new EnemyRuntime("enemy.primary", TeamSide.Player, 100f, EnemyArchetype.Boss);
            secondary = new EnemyRuntime("enemy.secondary", TeamSide.Player, 100f);
            primary.ApplyStormcallerShield(60f);
            primary.SetTargetingState(1, .3f, new CombatPoint(0f, 0f));
            secondary.SetTargetingState(2, .8f, new CombatPoint(.8f, 0f));
            registry.Register(primary);
            registry.Register(secondary);
            return registry;
        }

        private sealed class RecordingRandom : IRunRandom
        {
            private readonly float unit;

            public RecordingRandom(float unit)
            {
                this.unit = unit;
            }

            public int Seed => 1;
            public long CallIndex { get; private set; }
            public List<string> Contexts { get; } = new List<string>();

            public int NextInt(string context, int minInclusive, int maxExclusive)
            {
                CallIndex++;
                Contexts.Add(context);
                return minInclusive;
            }

            public float NextUnit(string context)
            {
                CallIndex++;
                Contexts.Add(context);
                return unit;
            }
        }
    }
}
