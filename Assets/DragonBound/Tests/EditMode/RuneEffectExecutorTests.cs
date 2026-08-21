using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Runes;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneEffectExecutorTests
    {
        [Test]
        public void Catalog_StoresEveryCombatParameterWithoutSilentFallbacks()
        {
            Assert.AreEqual(.55f, RuneCatalog.Get("Ricochet").GetParameter("DamageMultiplier"), .0001f);
            Assert.AreEqual(1.80f, RuneCatalog.Get("Skybreaker").GetParameter("PrimaryDamageMultiplier"), .0001f);
            Assert.AreEqual(.80f, RuneCatalog.Get("Skybreaker").GetParameter("SecondaryDamageMultiplier"), .0001f);
            Assert.AreEqual(.35f, RuneCatalog.Get("Wyrmguard").GetParameter("DamageMultiplier"), .0001f);
            Assert.AreEqual(2.5f, RuneCatalog.Get("Warcry").GetParameter("Radius"), .0001f);
        }

        [Test]
        public void Ricochet_UsesASecondTargetAndConfiguredFiftyFivePercentDamage()
        {
            var registry = CreateRegistry(out var primary, out var secondary);
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Ricochet"), new FixedRandom(0f), "hero.1");

            var results = executor.OnBasicAttackSucceeded(new RuneCombatContext(default(CombatPoint), 100f, 3f, registry), primary);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(secondary, results[0].Target);
            Assert.AreEqual(55f, results[0].Damage, .0001f);
            Assert.AreEqual(AttackKind.RuneRicochet, results[0].Kind);
        }

        [Test]
        public void Volley_TriggersFiveRuneDerivedBoltsOnTheTenthSuccessfulAttack()
        {
            var registry = CreateRegistry(out var primary, out _);
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Volley"), new FixedRandom(.99f), "hero.1");
            var context = new RuneCombatContext(default(CombatPoint), 10f, 3f, registry);
            for (var index = 0; index < 9; index++)
            {
                Assert.AreEqual(0, executor.OnBasicAttackSucceeded(context, primary).Count);
            }

            var bolts = executor.OnBasicAttackSucceeded(context, primary);
            Assert.AreEqual(5, bolts.Count);
            Assert.AreEqual(AttackKind.RuneVolleyBolt, bolts[0].Kind);
            Assert.AreEqual(3.5f, bolts[0].Damage, .0001f);
        }

        [Test]
        public void Frostbite_RefreshesInsteadOfStackingSlowPercent()
        {
            var registry = CreateRegistry(out var primary, out _);
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Frostbite"), new FixedRandom(.99f), "hero.1");
            var context = new RuneCombatContext(default(CombatPoint), 10f, 3f, registry);

            executor.OnBasicAttackSucceeded(context, primary);
            executor.OnBasicAttackSucceeded(context, primary);

            Assert.AreEqual(.90f, primary.MovementSpeedMultiplier, .0001f);
            Assert.AreEqual(1.5f, primary.MovementSlowRemainingSeconds, .0001f);
        }

        [Test]
        public void RuneDerivedKill_DoesNotCreateBladeTempestFollowUp()
        {
            var registry = CreateRegistry(out var primary, out _);
            var executor = new RuneEffectExecutor(RuneCatalog.Get("BladeTempest"), new FixedRandom(0f), "hero.1");

            Assert.AreEqual(0, executor.OnHeroKill(
                    new RuneCombatContext(default(CombatPoint), 100f, 3f, registry), primary, true).Count);
        }

        [Test]
        public void Longshot_UsesFinalRangeForLinearZeroToTwentyPercentBonus()
        {
            var registry = CreateRegistry(out var primary, out _);
            primary.SetTargetingState(1, .3f, new CombatPoint(3f, 0f));
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Longshot"), new FixedRandom(.99f), "hero.1");

            Assert.AreEqual(1.20f, executor.GetBasicAttackDamageMultiplier(default(CombatPoint), primary, 3f), .0001f);
            primary.SetTargetingState(1, .3f, default(CombatPoint));
            Assert.AreEqual(1f, executor.GetBasicAttackDamageMultiplier(default(CombatPoint), primary, 3f), .0001f);
        }

        [Test]
        public void Ambush_RollsOnlyOncePerHeroAndEnemyRuntime()
        {
            var registry = CreateRegistry(out var primary, out var secondary);
            secondary.SetTargetingState(2, .8f, new CombatPoint(.5f, 0f));
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Ambush"), new FixedRandom(0f), "hero.1");
            var context = new RuneCombatContext(default(CombatPoint), 100f, 3f, registry);

            Assert.AreEqual(2, executor.OnBasicAttackSucceeded(context, primary).Count);
            Assert.AreEqual(0, executor.OnBasicAttackSucceeded(context, primary).Count);
        }

        [Test]
        public void Windhawk_InterceptsFrontmostThenHonorsItsTwoSecondIcd()
        {
            var registry = CreateRegistry(out var primary, out var secondary);
            primary.SetTargetingState(1, .2f, new CombatPoint(0f, 0f));
            secondary.SetTargetingState(2, .9f, new CombatPoint(1f, 0f));
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Windhawk"), new FixedRandom(0f), "hero.1");
            var context = new RuneCombatContext(default(CombatPoint), 100f, 3f, registry);

            var first = executor.OnBasicAttackSucceeded(context, primary);
            Assert.AreEqual(1, first.Count);
            Assert.AreSame(secondary, first[0].Target);
            Assert.AreEqual(90f, first[0].Damage, .0001f);
            Assert.AreEqual(0, executor.OnBasicAttackSucceeded(context, primary).Count);
            executor.Tick(context, 2f);
            Assert.AreEqual(1, executor.OnBasicAttackSucceeded(context, primary).Count);
        }

        [Test]
        public void Skybreaker_DealsConfiguredPrimaryAndNearbySecondaryDamageWithoutControl()
        {
            var registry = CreateRegistry(out var primary, out var secondary);
            primary.SetTargetingState(1, .3f, new CombatPoint(0f, 0f));
            secondary.SetTargetingState(2, .8f, new CombatPoint(.8f, 0f));
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Skybreaker"), new FixedRandom(0f), "hero.1");

            var results = executor.OnBasicAttackSucceeded(
                new RuneCombatContext(default(CombatPoint), 100f, 3f, registry), primary);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(180f, results[0].Damage, .0001f);
            Assert.AreEqual(80f, results[1].Damage, .0001f);
            Assert.IsFalse(primary.IsStunned);
            Assert.IsFalse(secondary.IsStunned);
        }

        [Test]
        public void Wyrmguard_RefreshesOneSpiritInsteadOfCreatingStackedSummons()
        {
            var registry = CreateRegistry(out var primary, out _);
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Wyrmguard"), new FixedRandom(.99f), "hero.1");
            var context = new RuneCombatContext(default(CombatPoint), 100f, 3f, registry);

            executor.OnHeroLevelUp();
            Assert.IsTrue(executor.HasActiveSummon);
            Assert.AreEqual(3, executor.Tick(context, 2f).Count);
            executor.OnHeroLevelUp();
            Assert.AreEqual(3, executor.Tick(context, 2f).Count);
        }

        [Test]
        public void Dragonbloom_RefreshesOneSummonAndRuneDerivedKillCannotRetriggerIt()
        {
            var registry = CreateRegistry(out var primary, out _);
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Dragonbloom"), new FixedRandom(0f), "hero.1");
            var context = new RuneCombatContext(default(CombatPoint), 100f, 3f, registry);

            executor.OnHeroKill(context, primary, false);
            Assert.IsTrue(executor.HasActiveSummon);
            Assert.AreEqual(1, executor.Tick(context, 1f).Count);
            Assert.AreEqual(0, executor.OnHeroKill(context, primary, true).Count);
        }

        [Test]
        public void Warcry_UsesTenSecondIcdAndEmitsOneNonStackingBuffCommand()
        {
            var registry = CreateRegistry(out var primary, out _);
            var executor = new RuneEffectExecutor(RuneCatalog.Get("Warcry"), new FixedRandom(0f), "hero.1");
            var context = new RuneCombatContext(default(CombatPoint), 100f, 3f, registry);

            var first = executor.OnBasicAttackSucceeded(context, primary);
            Assert.AreEqual(1, first.Count);
            Assert.IsTrue(first[0].IsWarcry);
            Assert.AreEqual(1.20f, first[0].WarcryMultiplier, .0001f);
            Assert.AreEqual(0, executor.OnBasicAttackSucceeded(context, primary).Count);
            executor.Tick(context, 10f);
            Assert.AreEqual(1, executor.OnBasicAttackSucceeded(context, primary).Count);
        }

        private static EnemyRegistry CreateRegistry(out EnemyRuntime primary, out EnemyRuntime secondary)
        {
            var registry = new EnemyRegistry();
            primary = new EnemyRuntime("enemy.primary", TeamSide.Player, 100f);
            secondary = new EnemyRuntime("enemy.secondary", TeamSide.Player, 100f);
            primary.SetTargetingState(1, .3f, new CombatPoint(0f, 0f));
            secondary.SetTargetingState(2, .8f, new CombatPoint(1f, 0f));
            registry.Register(primary);
            registry.Register(secondary);
            return registry;
        }

        private sealed class FixedRandom : IRunRandom
        {
            private readonly float unit;
            public FixedRandom(float unit) { this.unit = unit; }
            public int Seed => 1;
            public long CallIndex { get; private set; }
            public int NextInt(string context, int minInclusive, int maxExclusive) { CallIndex++; return minInclusive; }
            public float NextUnit(string context) { CallIndex++; return unit; }
        }
    }
}
