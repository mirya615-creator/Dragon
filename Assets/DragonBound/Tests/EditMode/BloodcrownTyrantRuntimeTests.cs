using System.Collections.Generic;
using DragonBound.Bosses.Contracts;
using DragonBound.Bosses.Runtime;
using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class BloodcrownTyrantRuntimeTests
    {
        [Test]
        public void ConfigurationUsesFixedIdentityAndKeeps2400ExplicitlyGreybox()
        {
            var definition = BloodcrownTyrantConfiguration.CreateGreyboxDefinition();
            var slot = BloodcrownTyrantConfiguration.CreateGreyboxSlot();

            Assert.AreEqual(FixedBossIds.W16BloodcrownTyrant, definition.BossId);
            Assert.AreEqual(16, definition.Wave.Value);
            Assert.AreEqual(2400f, definition.MaxHitPoints, 0.0001f);
            Assert.AreEqual(0.20f, definition.MoveSpeed, 0.0001f);
            Assert.AreEqual(BossGoalEffect.InstantDefeat, definition.GoalEffect);
            Assert.AreEqual(15, definition.HeroXpReward);
            Assert.IsTrue(slot.IsIndependentBossSlot);
            Assert.AreEqual(0, slot.RegularEnemyCountContribution);
        }

        [Test]
        public void FirstCastStartsAtEightAndResolvesAfterOneSecond()
        {
            var fixture = CreateFixture();
            var lifecycle = new List<BossSkillLifecycleEvent>();
            fixture.Runtime.LifecycleEmitted += lifecycle.Add;

            fixture.Runtime.Tick(7.99f);
            Assert.AreEqual(0, lifecycle.Count);

            fixture.Runtime.Tick(0.01f);
            Assert.AreEqual(BossSkillLifecycle.Start, lifecycle[0].Lifecycle);
            Assert.AreEqual(BossSkillLifecycle.Windup, lifecycle[1].Lifecycle);
            Assert.AreEqual(1, fixture.Runtime.CastAttemptCount);
            Assert.IsFalse(fixture.BasicPolicy.IsDecreeActive);

            fixture.Runtime.Tick(0.99f);
            Assert.IsFalse(fixture.Results.ContainsResolved);
            fixture.Runtime.Tick(0.01f);
            Assert.IsTrue(fixture.Results.ContainsResolved);
            Assert.IsTrue(fixture.BasicPolicy.IsDecreeActive);
            Assert.AreEqual(1, fixture.BasicPolicy.EffectiveCombatLevel);
            Assert.IsTrue(fixture.BasicPolicy.IsMergeBlocked);
        }

        [Test]
        public void SpellbreakerBlockReflectsTenPercentAndRetriesAfterTwelveSeconds()
        {
            var fixture = CreateFixture(new BlockingSpellbreaker());
            var lifecycle = new List<BossSkillLifecycleEvent>();
            fixture.Runtime.LifecycleEmitted += lifecycle.Add;

            fixture.Runtime.Tick(9f);
            Assert.AreEqual(240f, fixture.Boss.ReflectedDamage, 0.0001f);
            Assert.IsFalse(fixture.BasicPolicy.IsDecreeActive);
            Assert.IsFalse(fixture.BasicPolicy.IsMergeBlocked);
            Assert.AreEqual(1, fixture.Runtime.CastAttemptCount);
            Assert.AreEqual(1, fixture.Results.BlockedCount);
            Assert.AreEqual(BossSkillLifecycle.Blocked, lifecycle[2].Lifecycle);
            Assert.AreEqual(BossSkillLifecycle.Cooldown, lifecycle[3].Lifecycle);

            fixture.Runtime.Tick(11.99f);
            Assert.AreEqual(1, fixture.Runtime.CastAttemptCount);
            fixture.Runtime.Tick(0.01f);
            Assert.AreEqual(2, fixture.Runtime.CastAttemptCount);
        }

        [Test]
        public void SuccessfulDecreeDoesNotCastAgainAndAppliesToFutureBasicsThroughPort()
        {
            var fixture = CreateFixture(new PassingSpellbreaker());

            fixture.Runtime.Tick(9f);
            fixture.BasicPolicy.RegisterFutureBasic();
            fixture.Runtime.Tick(100f);

            Assert.AreEqual(1, fixture.Runtime.CastAttemptCount);
            Assert.AreEqual(1, fixture.BasicPolicy.EffectiveCombatLevel);
            Assert.IsTrue(fixture.BasicPolicy.FutureBasicsUseOverride);
        }

        [Test]
        public void BossDeathRestoresStoredLevelPolicyAndMergeEntry()
        {
            var fixture = CreateFixture();
            fixture.Runtime.Tick(9f);
            fixture.Boss.IsAlive = false;
            fixture.Runtime.Tick(0f);

            Assert.IsTrue(fixture.Runtime.IsDead);
            Assert.IsFalse(fixture.BasicPolicy.IsDecreeActive);
            Assert.AreEqual(0, fixture.BasicPolicy.EffectiveCombatLevel);
            Assert.IsFalse(fixture.BasicPolicy.IsMergeBlocked);
            Assert.IsTrue(fixture.BasicPolicy.StoredLevelsRemainAuthoritative);
        }

        [Test]
        public void LastHitXpAwardRequiresFormalValidHeroOwner()
        {
            var fixture = CreateFixture();
            var hero = new CombatDamageOwner(
                CombatDamageOwnerKind.Hero,
                TeamSide.Player,
                "pair.01",
                "hero-id");
            var basic = new CombatDamageOwner(
                CombatDamageOwnerKind.BasicUnit,
                TeamSide.Player,
                "basic.01");

            Assert.IsTrue(fixture.Runtime.CreateLastHitXpAward(hero, true).GrantedToHero);
            Assert.AreEqual(15, fixture.Runtime.CreateLastHitXpAward(hero, true).XpAmount);
            Assert.IsFalse(fixture.Runtime.CreateLastHitXpAward(basic, true).GrantedToHero);
            Assert.IsFalse(fixture.Runtime.CreateLastHitXpAward(hero, false).GrantedToHero);
        }

        [Test]
        public void DecreeUsesLevelOneAttackAndSpeedThenPreservesStoredRangeAndModifiers()
        {
            var pipeline = new CapturingModifierPipeline();
            var projected = BloodcrownBasicCombatPolicy.Apply(
                new BloodcrownBasicCombatInput(
                    storedLevel: 5,
                    levelOneBaseAttack: 10f,
                    levelOneBaseAttackSpeed: 1f,
                    storedLevelRange: 4.5f),
                pipeline);

            Assert.AreEqual(5, projected.StoredLevel);
            Assert.AreEqual(1, projected.EffectiveCombatLevel);
            Assert.AreEqual(10f, pipeline.AttackInput, 0.0001f);
            Assert.AreEqual(1f, pipeline.AttackSpeedInput, 0.0001f);
            Assert.AreEqual(15f, projected.Attack, 0.0001f);
            Assert.AreEqual(1.25f, projected.AttackSpeed, 0.0001f);
            Assert.AreEqual(4.5f, projected.Range, 0.0001f);
        }

        [Test]
        public void DecreeBlocksEveryMergeEntryAndKeepsDuplicateRecruitIndependent()
        {
            foreach (BloodcrownMergeEntry entry in System.Enum.GetValues(typeof(BloodcrownMergeEntry)))
            {
                Assert.IsFalse(BloodcrownMergePolicy.CanMerge(entry, true), entry.ToString());
                Assert.IsTrue(BloodcrownMergePolicy.CanMerge(entry, false), entry.ToString());
            }

            Assert.IsTrue(BloodcrownMergePolicy.KeepsDuplicateRecruitIndependent(true));
            Assert.IsFalse(BloodcrownMergePolicy.KeepsDuplicateRecruitIndependent(false));
        }

        private static Fixture CreateFixture(IBloodcrownSpellbreaker spellbreaker = null)
        {
            var boss = new FakeBossTarget(2400f);
            var policy = new FakeBasicPolicyPort();
            var results = new ResultCapture();
            var runtime = new BloodcrownTyrantRuntime(
                BloodcrownTyrantConfiguration.CreateGreyboxDefinition(),
                boss,
                policy,
                spellbreaker);
            runtime.CastResultEmitted += results.Capture;
            return new Fixture(runtime, boss, policy, results);
        }

        private sealed class Fixture
        {
            public Fixture(
                BloodcrownTyrantRuntime runtime,
                FakeBossTarget boss,
                FakeBasicPolicyPort basicPolicy,
                ResultCapture results)
            {
                Runtime = runtime;
                Boss = boss;
                BasicPolicy = basicPolicy;
                Results = results;
            }

            public BloodcrownTyrantRuntime Runtime { get; }
            public FakeBossTarget Boss { get; }
            public FakeBasicPolicyPort BasicPolicy { get; }
            public ResultCapture Results { get; }
        }

        private sealed class FakeBossTarget : IBloodcrownBossTarget
        {
            public FakeBossTarget(float maxHitPoints)
            {
                MaxHitPoints = maxHitPoints;
                IsAlive = true;
            }

            public float MaxHitPoints { get; }
            public bool IsAlive { get; set; }
            public float ReflectedDamage { get; private set; }

            public void ApplyReflectedDamage(float damage)
            {
                ReflectedDamage += damage;
            }
        }

        private sealed class FakeBasicPolicyPort : IBloodcrownBasicPolicyPort
        {
            public bool IsDecreeActive { get; private set; }
            public int EffectiveCombatLevel { get; private set; }
            public bool IsMergeBlocked { get; private set; }
            public bool FutureBasicsUseOverride { get; private set; }
            public bool StoredLevelsRemainAuthoritative { get; private set; } = true;

            public void EnableDecree(int effectiveCombatLevel)
            {
                IsDecreeActive = true;
                EffectiveCombatLevel = effectiveCombatLevel;
                FutureBasicsUseOverride = true;
            }

            public void DisableDecree()
            {
                IsDecreeActive = false;
                EffectiveCombatLevel = 0;
                FutureBasicsUseOverride = false;
            }

            public void SetMergeBlocked(bool blocked)
            {
                IsMergeBlocked = blocked;
            }

            public void RegisterFutureBasic()
            {
                if (IsDecreeActive)
                {
                    FutureBasicsUseOverride = EffectiveCombatLevel == 1;
                }
            }
        }

        private sealed class ResultCapture
        {
            public int BlockedCount { get; private set; }
            public bool ContainsResolved { get; private set; }

            public void Capture(BossCastResult result)
            {
                if (result.Outcome == BossCastOutcome.Blocked)
                {
                    BlockedCount++;
                }

                if (result.Outcome == BossCastOutcome.Resolved)
                {
                    ContainsResolved = true;
                }
            }
        }

        private sealed class BlockingSpellbreaker : IBloodcrownSpellbreaker
        {
            public SpellbreakerOutcome Evaluate(BossCastAttempt attempt)
            {
                return SpellbreakerOutcome.Blocked;
            }
        }

        private sealed class PassingSpellbreaker : IBloodcrownSpellbreaker
        {
            public SpellbreakerOutcome Evaluate(BossCastAttempt attempt)
            {
                return SpellbreakerOutcome.Passed;
            }
        }

        private sealed class CapturingModifierPipeline : IBloodcrownBasicModifierPipeline
        {
            public float AttackInput { get; private set; }
            public float AttackSpeedInput { get; private set; }

            public float ApplyAttack(float levelOneBaseAttack)
            {
                AttackInput = levelOneBaseAttack;
                return levelOneBaseAttack * 1.5f;
            }

            public float ApplyAttackSpeed(float levelOneBaseAttackSpeed)
            {
                AttackSpeedInput = levelOneBaseAttackSpeed;
                return levelOneBaseAttackSpeed * 1.25f;
            }
        }
    }
}
