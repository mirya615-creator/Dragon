using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Runes;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneEffectExecutorTargetPortAdapterTests
    {
        [Test]
        public void PortPath_RicochetSelectsTheFirstNonPrimaryRuntimeIdAndReturnsPortResult()
        {
            var primary = new PortTarget("enemy.primary", false, new CombatPoint(0f, 0f));
            var first = new PortTarget("enemy.alpha", false, new CombatPoint(1f, 0f));
            var later = new PortTarget("enemy.omega", false, new CombatPoint(2f, 0f));
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Ricochet"), new FixedRandom(0f), "hero.port");
            var context = new RuneTargetCombatContext(
                default(CombatPoint),
                100f,
                3f,
                new PortRegistry(primary, later, first));

            var results = executor.OnBasicAttackSucceeded(context, primary);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("enemy.alpha", results[0].TargetRuntimeId);
            Assert.AreSame(first, results[0].Target);
            Assert.AreEqual(55f, results[0].Damage, .0001f);
            Assert.AreEqual(45f, first.HitPoints, .0001f);
            Assert.AreEqual(100f, later.HitPoints, .0001f);
        }

        [Test]
        public void PortPath_FrostbiteUsesBossAndNormalParametersWithoutEnemyRuntime()
        {
            var normal = new PortTarget("enemy.normal", false, default(CombatPoint));
            var boss = new PortTarget("enemy.boss", true, default(CombatPoint));
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Frostbite"), new FixedRandom(.99f), "hero.port");
            var context = new RuneTargetCombatContext(
                default(CombatPoint),
                10f,
                3f,
                new PortRegistry(normal, boss));

            executor.OnBasicAttackSucceeded(context, normal);
            executor.OnBasicAttackSucceeded(context, boss);

            Assert.AreEqual(.10f, normal.SlowFraction, .0001f);
            Assert.AreEqual(1.5f, normal.SlowDuration, .0001f);
            Assert.AreEqual(.05f, boss.SlowFraction, .0001f);
            Assert.AreEqual(1f, boss.SlowDuration, .0001f);
        }

        private sealed class PortRegistry : IRuneCombatTargetRegistry
        {
            private readonly IReadOnlyList<IRuneCombatTarget> targets;

            public PortRegistry(params IRuneCombatTarget[] targets)
            {
                this.targets = targets;
            }

            public IReadOnlyList<IRuneCombatTarget> Snapshot() => targets;
        }

        private sealed class PortTarget : IRuneCombatTarget
        {
            public PortTarget(string runtimeId, bool isBoss, CombatPoint position)
            {
                RuntimeId = runtimeId;
                IsBoss = isBoss;
                CombatPosition = position;
                HitPoints = 100f;
            }

            public string RuntimeId { get; }
            public bool IsAlive => HitPoints > 0.0001f;
            public bool IsBoss { get; }
            public float PathProgress => 0f;
            public CombatPoint CombatPosition { get; }
            public float HitPoints { get; private set; }
            public float SlowFraction { get; private set; }
            public float SlowDuration { get; private set; }

            public RuneDamageApplication ApplyRuneDamage(float damage)
            {
                var healthDamage = System.Math.Min(System.Math.Max(0f, damage), HitPoints);
                HitPoints -= healthDamage;
                return new RuneDamageApplication(damage, 0f, healthDamage, !IsAlive);
            }

            public bool TryApplyRuneSlow(float slowFraction, float durationSeconds)
            {
                SlowFraction = slowFraction;
                SlowDuration = durationSeconds;
                return true;
            }
        }

        private sealed class FixedRandom : IRunRandom
        {
            private readonly float unit;

            public FixedRandom(float unit)
            {
                this.unit = unit;
            }

            public int Seed => 1;
            public long CallIndex { get; private set; }
            public int NextInt(string context, int minInclusive, int maxExclusive)
            {
                CallIndex++;
                return minInclusive;
            }

            public float NextUnit(string context)
            {
                CallIndex++;
                return unit;
            }
        }
    }
}
