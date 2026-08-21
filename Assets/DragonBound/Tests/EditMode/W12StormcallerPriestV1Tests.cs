using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class W12StormcallerPriestV1Tests
    {
        [Test]
        public void W12ProductionSpawnsFixedStormcallerWithoutIncreasingNormalCount()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            var runtime = new TwentyWavePressureRuntime(
                new MatchController(1201), null, null, 1201, configuration);

            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(12));
            Assert.IsNotNull(runtime.PlayerW12Boss);
            Assert.AreEqual(StormcallerPriestConfiguration.BossId, runtime.PlayerW12Boss.BossId);
            Assert.AreEqual(StormcallerPriestConfiguration.GreyboxMaxHitPoints, runtime.PlayerW12Boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(EnemyArchetype.Boss, runtime.PlayerW12Boss.Archetype);
            Assert.AreEqual(27, configuration.GetWave(12).EnemyCountPerSide);
            Assert.IsTrue(configuration.GetWave(12).HasBossSlot);
            Assert.IsTrue(runtime.PlayerEnemyRegistry.Count >= 2);
        }

        [Test]
        public void StormCallUsesFrozenTimelineAndStartsCooldownAfterEffect()
        {
            var fixture = CreateFixture();
            var events = new List<StormcallerCastEvent>();
            fixture.Runtime.CastEvent += events.Add;

            for (var index = 0; index < 380; index++)
            {
                fixture.Runtime.Tick(0.1f);
            }

            AssertEvent(events, StormcallerCastEventKind.CastStarted, 1, 7f);
            AssertEvent(events, StormcallerCastEventKind.EffectApplied, 1, 7.75f);
            AssertEvent(events, StormcallerCastEventKind.EffectEnded, 1, 15.75f);
            AssertEvent(events, StormcallerCastEventKind.CastStarted, 2, 27.75f);
            AssertEvent(events, StormcallerCastEventKind.EffectApplied, 2, 28.50f);
            AssertEvent(events, StormcallerCastEventKind.EffectEnded, 2, 36.50f);
        }

        [Test]
        public void StormCallSnapshotsAllNormalTargetsInRangeOnlyAtResolution()
        {
            var fixture = CreateFixture();
            var inRange = new EnemyRuntime("normal.in", TeamSide.Player, 100f, EnemyArchetype.Normal);
            inRange.SetTargetingState(0, 0.1f, new CombatPoint(1f, 0f));
            var outside = new EnemyRuntime("normal.out", TeamSide.Player, 100f, EnemyArchetype.Normal);
            outside.SetTargetingState(0, 0.1f, new CombatPoint(4f, 0f));
            var bossTarget = new EnemyRuntime("other.boss", TeamSide.Player, 100f, EnemyArchetype.Boss, 0, "BOSS_OTHER");
            bossTarget.SetTargetingState(0, 0.1f, new CombatPoint(1f, 0f));
            fixture.Registry.Register(inRange);
            fixture.Registry.Register(outside);
            fixture.Registry.Register(bossTarget);

            fixture.Runtime.Tick(7.75f);
            Assert.AreEqual(1, fixture.Runtime.LastAffectedCount);
            Assert.AreEqual(60f, inRange.StormcallerShieldHitPoints, 0.0001f);
            Assert.AreEqual(1.15f, inRange.StormcallerMovementSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, outside.StormcallerShieldHitPoints, 0.0001f);
            Assert.AreEqual(0f, bossTarget.StormcallerShieldHitPoints, 0.0001f);
        }

        [Test]
        public void StormCallWithNoTargetsStillCompletesAndStartsCooldown()
        {
            var fixture = CreateFixture();

            fixture.Runtime.Tick(7.75f);

            Assert.AreEqual(1, fixture.Runtime.CastsSucceeded);
            Assert.AreEqual(0, fixture.Runtime.LastAffectedCount);
            Assert.IsTrue(fixture.Runtime.IsEffectActive);

            fixture.Runtime.Tick(8f);

            Assert.IsFalse(fixture.Runtime.IsEffectActive);
            Assert.AreEqual(12f, fixture.Runtime.CooldownRemainingSeconds, 0.0001f);
        }

        [Test]
        public void ShieldAbsorbsBeforeBodyAndOverflowReachesHealth()
        {
            var target = new EnemyRuntime("normal.shield", TeamSide.Player, 100f, EnemyArchetype.Normal);
            target.ApplyStormcallerShield(60f);

            var first = target.ApplyDamage(40f);
            Assert.AreEqual(40f, first.ShieldDamage, 0.0001f);
            Assert.AreEqual(0f, first.HealthDamage, 0.0001f);
            Assert.AreEqual(100f, target.HitPoints, 0.0001f);
            Assert.AreEqual(20f, target.StormcallerShieldHitPoints, 0.0001f);

            var second = target.ApplyDamage(30f);
            Assert.AreEqual(20f, second.ShieldDamage, 0.0001f);
            Assert.AreEqual(10f, second.HealthDamage, 0.0001f);
            Assert.AreEqual(90f, target.HitPoints, 0.0001f);
            Assert.AreEqual(0f, target.StormcallerShieldHitPoints, 0.0001f);
        }

        [Test]
        public void RecastRefreshesOneShieldAndSpeedBuffWithoutStacking()
        {
            var fixture = CreateFixture();
            var target = new EnemyRuntime("normal.refresh", TeamSide.Player, 100f, EnemyArchetype.Normal);
            target.SetTargetingState(0, 0.1f, new CombatPoint(1f, 0f));
            fixture.Registry.Register(target);

            fixture.Runtime.Tick(7.75f);
            target.ApplyDamage(20f);
            target.TickControl(2f);
            fixture.Runtime.Tick(20.75f);
            Assert.AreEqual(60f, target.StormcallerShieldHitPoints, 0.0001f);
            Assert.AreEqual(1.15f, target.StormcallerMovementSpeedMultiplier, 0.0001f);
            Assert.LessOrEqual(target.StormcallerSpeedBuffRemainingSeconds, 8.0001f);
        }

        [Test]
        public void BossDeathDoesNotClearActiveStormcallerBuff()
        {
            var fixture = CreateFixture();
            var target = new EnemyRuntime("normal.buff", TeamSide.Player, 100f, EnemyArchetype.Normal);
            target.SetTargetingState(0, 0.1f, new CombatPoint(1f, 0f));
            fixture.Registry.Register(target);
            fixture.Runtime.Tick(7.75f);
            fixture.Boss.ApplyDamage(1200f);
            fixture.Runtime.Tick(0.1f);
            Assert.AreEqual(60f, target.StormcallerShieldHitPoints, 0.0001f);
            Assert.AreEqual(1.15f, target.StormcallerMovementSpeedMultiplier, 0.0001f);
            target.TickControl(8f);
            Assert.AreEqual(1f, target.StormcallerMovementSpeedMultiplier, 0.0001f);
        }

        [Test]
        public void SpellbreakerFailureReflectsTenPercentWithoutApplyingEffects()
        {
            var fixture = CreateFixture(new BlockingSpellbreaker());
            var target = new EnemyRuntime("normal.blocked", TeamSide.Player, 100f, EnemyArchetype.Normal);
            target.SetTargetingState(0, 0.1f, new CombatPoint(1f, 0f));
            fixture.Registry.Register(target);

            fixture.Runtime.Tick(7.75f);
            Assert.AreEqual(1, fixture.Runtime.CastsFailed);
            Assert.AreEqual(1080f, fixture.Boss.HitPoints, 0.0001f);
            Assert.AreEqual(0f, target.StormcallerShieldHitPoints, 0.0001f);
            Assert.AreEqual(1f, target.StormcallerMovementSpeedMultiplier, 0.0001f);
            Assert.AreEqual(12f, fixture.Runtime.CooldownRemainingSeconds, 0.0001f);
        }

        [Test]
        public void BossExperienceMapAwardsOnlyHeroLastHitForW6AndW12()
        {
            Assert.AreEqual(6, BossExperienceRewards.Get(BossExperienceRewards.SoulchainBinderBossId));
            Assert.AreEqual(10, BossExperienceRewards.Get(BossExperienceRewards.StormcallerPriestBossId));
            Assert.AreEqual(15, BossExperienceRewards.Get(BossExperienceRewards.BloodcrownTyrantBossId));
            Assert.AreEqual(20, BossExperienceRewards.Get(BossExperienceRewards.WorldeaterWyrmBossId));

            var boss = new EnemyRuntime(
                "w12.boss",
                TeamSide.Player,
                100f,
                EnemyArchetype.Boss,
                0,
                BossExperienceRewards.StormcallerPriestBossId);
            boss.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Hero,
                TeamSide.Player,
                "pair.w12",
                "HERO_STORMCALLER_TEST"));
            Assert.AreEqual(10, HeroXpSettlement.GetAwardedExperience(boss));

            boss.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.BasicUnit,
                TeamSide.Player,
                "basic.w12"));
            Assert.AreEqual(0, HeroXpSettlement.GetAwardedExperience(boss));
        }

        private static Fixture CreateFixture(ISoulChainSpellbreakerResolver spellbreaker = null)
        {
            var boss = new EnemyRuntime(
                "w12.fixture.boss",
                TeamSide.Player,
                StormcallerPriestConfiguration.GreyboxMaxHitPoints,
                EnemyArchetype.Boss,
                0,
                StormcallerPriestConfiguration.BossId);
            boss.SetTargetingState(0, 0f, new CombatPoint(0f, 0f));
            var registry = new EnemyRegistry();
            registry.Register(boss);
            var runtime = new StormcallerPriestRuntime(boss, TeamSide.Player, registry, spellbreaker);
            return new Fixture(boss, registry, runtime);
        }

        private static void AssertEvent(
            IReadOnlyList<StormcallerCastEvent> events,
            StormcallerCastEventKind kind,
            int castNumber,
            float expected)
        {
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind && events[index].CastNumber == castNumber)
                {
                    Assert.AreEqual(expected, events[index].ElapsedSeconds, 0.001f);
                    return;
                }
            }

            Assert.Fail("Missing event " + kind + " for cast " + castNumber);
        }

        private sealed class Fixture
        {
            public Fixture(EnemyRuntime boss, EnemyRegistry registry, StormcallerPriestRuntime runtime)
            {
                Boss = boss;
                Registry = registry;
                Runtime = runtime;
            }

            public EnemyRuntime Boss { get; }
            public EnemyRegistry Registry { get; }
            public StormcallerPriestRuntime Runtime { get; }
        }

        private sealed class BlockingSpellbreaker : ISoulChainSpellbreakerResolver
        {
            public bool ShouldBlockCast(SoulChainBossCastContext context)
            {
                return true;
            }
        }
    }
}
