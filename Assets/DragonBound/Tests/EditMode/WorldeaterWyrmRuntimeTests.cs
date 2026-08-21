using System.Collections.Generic;
using DragonBound.Bosses.Contracts;
using DragonBound.Bosses.Runtime;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class WorldeaterWyrmRuntimeTests
    {
        [Test]
        public void ConfigurationKeepsW20IdentityGreyboxHpAndSummonPolicy()
        {
            var boss = WorldeaterWyrmConfiguration.CreateGreyboxDefinition();
            var minion = WorldeaterWyrmConfiguration.CreateMinionDefinition();

            Assert.AreEqual(FixedBossIds.W20WorldeaterWyrm, boss.BossId);
            Assert.AreEqual(20, boss.Wave.Value);
            Assert.AreEqual(5000f, boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(0.20f, boss.MoveSpeed, 0.0001f);
            Assert.AreEqual(20, boss.HeroXpReward);
            Assert.AreEqual(4, minion.Count);
            Assert.AreEqual(330f, minion.MaxHitPoints, 0.0001f);
            Assert.AreEqual(0.75f, minion.MoveSpeed, 0.0001f);
            Assert.AreEqual(0, minion.Policy.HeroXpReward);
            Assert.AreEqual(0, minion.Policy.RunResourceReward);
            Assert.IsFalse(minion.Policy.DespawnOnBossDeath);
            Assert.IsFalse(minion.Policy.BlocksWaveScheduleCompletion);
            Assert.IsTrue(minion.Policy.PersistsAcrossWave);
        }

        [Test]
        public void DevourSelectsLowestBasicAndUsesStableRuntimeOrder()
        {
            var fixture = CreateFixture(new WorldeaterTarget("basic.z", WorldeaterTargetClass.Basic, 1));
            fixture.Targets.Add(new WorldeaterTarget("basic.a", WorldeaterTargetClass.Basic, 1));
            fixture.Targets.Add(new WorldeaterTarget("basic.low", WorldeaterTargetClass.Basic, 0));
            fixture.Runtime.Tick(11f);

            Assert.AreEqual("basic.low", fixture.Targets.ConsumedId);
            Assert.AreEqual(5250f, fixture.Boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(5250f, fixture.Boss.HitPoints, 0.0001f);
        }

        [Test]
        public void DevourLocksTargetAndInvalidTargetConsumesCooldownWithoutSpellbreaker()
        {
            var fixture = CreateFixture(new WorldeaterTarget("basic.a", WorldeaterTargetClass.Basic, 1));
            fixture.Targets.InvalidAfterStart = true;
            fixture.Runtime.Tick(11f);

            Assert.IsNull(fixture.Targets.ConsumedId);
            Assert.AreEqual(0, fixture.Spellbreaker.Attempts);
            fixture.Runtime.Tick(14.99f);
            Assert.AreEqual(1, fixture.Runtime.DevourCastCount);
            fixture.Runtime.Tick(0.01f);
            Assert.AreEqual(2, fixture.Runtime.DevourCastCount);
        }

        [Test]
        public void DevourWithoutTargetDoesNotEnterWindupOrSpellbreakerButStartsFullCooldown()
        {
            var fixture = CreateFixture();
            fixture.Runtime.Tick(10f);

            Assert.AreEqual(1, fixture.Runtime.DevourCastCount);
            Assert.AreEqual(0, fixture.Spellbreaker.Attempts);
            fixture.Runtime.Tick(15f);
            Assert.AreEqual(2, fixture.Runtime.DevourCastCount);
        }

        [Test]
        public void SpellbreakerReflectsCurrentMaxHpAndDoesNotConsumeTarget()
        {
            var fixture = CreateFixture(new WorldeaterTarget("basic.a", WorldeaterTargetClass.Basic, 1), true);
            fixture.Runtime.Tick(11f);

            Assert.AreEqual(1, fixture.Spellbreaker.Attempts);
            Assert.AreEqual(4500f, fixture.Boss.HitPoints, 0.0001f);
            Assert.IsNull(fixture.Targets.ConsumedId);
        }

        [Test]
        public void SummonResolvesAtTwelvePointSevenFiveAndAlwaysAddsFourWithoutCap()
        {
            var fixture = CreateFixture();
            fixture.Runtime.Tick(12.74f);
            Assert.AreEqual(0, fixture.Summons.TotalSpawned);
            fixture.Runtime.Tick(0.01f);
            Assert.AreEqual(4, fixture.Summons.TotalSpawned);
            fixture.Runtime.Tick(18.75f);
            Assert.AreEqual(8, fixture.Summons.TotalSpawned);
        }

        [Test]
        public void SpellbreakerBlocksSummonWithoutSpawningAndReflectsTenPercent()
        {
            var fixture = CreateFixture(default(WorldeaterTarget), true);
            fixture.Runtime.Tick(12.75f);

            Assert.AreEqual(0, fixture.Summons.TotalSpawned);
            Assert.AreEqual(4500f, fixture.Boss.HitPoints, 0.0001f);
            Assert.AreEqual(1, fixture.Spellbreaker.Attempts);
        }

        private static Fixture CreateFixture(WorldeaterTarget target = default(WorldeaterTarget), bool block = false)
        {
            var boss = new FakeBoss(5000f);
            var targets = new FakeTargets();
            if (!string.IsNullOrWhiteSpace(target.RuntimeId))
            {
                targets.Add(target);
            }

            var spellbreaker = new FakeSpellbreaker(block);
            var summons = new FakeSummons();
            var runtime = new WorldeaterWyrmRuntime(
                WorldeaterWyrmConfiguration.CreateGreyboxDefinition(),
                boss,
                targets,
                summons,
                spellbreaker);
            return new Fixture(runtime, boss, targets, spellbreaker, summons);
        }

        private sealed class Fixture
        {
            public Fixture(WorldeaterWyrmRuntime runtime, FakeBoss boss, FakeTargets targets, FakeSpellbreaker spellbreaker, FakeSummons summons)
            {
                Runtime = runtime;
                Boss = boss;
                Targets = targets;
                Spellbreaker = spellbreaker;
                Summons = summons;
            }
            public WorldeaterWyrmRuntime Runtime { get; }
            public FakeBoss Boss { get; }
            public FakeTargets Targets { get; }
            public FakeSpellbreaker Spellbreaker { get; }
            public FakeSummons Summons { get; }
        }

        private sealed class FakeBoss : IWorldeaterBossTarget
        {
            public FakeBoss(float hp) { InitialMaxHitPoints = hp; MaxHitPoints = hp; HitPoints = hp; }
            public float InitialMaxHitPoints { get; }
            public float MaxHitPoints { get; private set; }
            public float HitPoints { get; private set; }
            public bool IsAlive => HitPoints > 0f;
            public void ApplyReflectedDamage(float damage) { HitPoints = System.Math.Max(0f, HitPoints - damage); }
            public void AddHealth(float amount) { MaxHitPoints += amount; HitPoints += amount; }
        }

        private sealed class FakeTargets : IWorldeaterTargetPort
        {
            private readonly List<WorldeaterTarget> targets = new List<WorldeaterTarget>();
            public string ConsumedId { get; private set; }
            public bool InvalidAfterStart { get; set; }
            public void Add(WorldeaterTarget target) => targets.Add(target);
            public IReadOnlyList<WorldeaterTarget> GetEligibleTargets() => targets;
            public bool IsStillEligible(WorldeaterTarget target) => !InvalidAfterStart;
            public bool Consume(WorldeaterTarget target) { ConsumedId = target.RuntimeId; return true; }
        }

        private sealed class FakeSummons : IWorldeaterSummonPort
        {
            public int TotalSpawned { get; private set; }
            public void SpawnMinions(int count, float maxHitPoints, float moveSpeedCellsPerSecond) { TotalSpawned += count; }
        }

        private sealed class FakeSpellbreaker : IWorldeaterSpellbreaker
        {
            private readonly bool block;
            public FakeSpellbreaker(bool block) { this.block = block; }
            public int Attempts { get; private set; }
            public SpellbreakerOutcome Evaluate(BossCastAttempt attempt)
            {
                Attempts++;
                return block ? SpellbreakerOutcome.Blocked : SpellbreakerOutcome.Passed;
            }
        }
    }
}
