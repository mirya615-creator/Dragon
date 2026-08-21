using DragonBound.Bosses.Contracts;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Foundation.Contracts;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class BossesContractsTests
    {
        [Test]
        public void FixedBossesExposeTheFourV1IdentitiesAndWaves()
        {
            Assert.AreEqual("BOSS_SOULCHAIN_BINDER", FixedBosses.W6.BossId.Value);
            Assert.AreEqual(6, FixedBosses.W6.Wave.Value);
            Assert.AreEqual("BOSS_STORMCALLER_PRIEST", FixedBosses.W12.BossId.Value);
            Assert.AreEqual(12, FixedBosses.W12.Wave.Value);
            Assert.AreEqual("BOSS_BLOODCROWN_TYRANT", FixedBosses.W16.BossId.Value);
            Assert.AreEqual(16, FixedBosses.W16.Wave.Value);
            Assert.AreEqual("BOSS_WORLDEATER_WYRM", FixedBosses.W20.BossId.Value);
            Assert.AreEqual(20, FixedBosses.W20.Wave.Value);
        }

        [Test]
        public void BossDefinitionTakesHpAndMoveSpeedAsInputs()
        {
            var definition = new BossDefinition(
                FixedBossIds.W16BloodcrownTyrant,
                new WaveNumber(16),
                1234.5f,
                0.37f,
                BossGoalEffect.InstantDefeat,
                15);

            Assert.AreEqual(1234.5f, definition.MaxHitPoints, 0.0001f);
            Assert.AreEqual(0.37f, definition.MoveSpeed, 0.0001f);
            Assert.AreEqual(15, definition.HeroXpReward);
        }

        [Test]
        public void SkillLifecycleAndCastResultRepresentBlockedReflection()
        {
            var skill = new BossSkillId("BLOODCROWN_DECREE");
            var lifecycle = new BossSkillLifecycleEvent(
                FixedBossIds.W16BloodcrownTyrant, skill, 2, BossSkillLifecycle.Blocked, 22f);
            var attempt = new BossCastAttempt(
                lifecycle.BossId, lifecycle.SkillId, lifecycle.AttemptNumber, lifecycle.ElapsedSeconds, true, true);
            var result = new BossCastResult(
                attempt,
                BossCastOutcome.Blocked,
                SpellbreakerOutcome.Blocked,
                240f,
                BossGoalEffect.None,
                false);

            Assert.AreEqual(BossSkillLifecycle.Blocked, lifecycle.Lifecycle);
            Assert.IsTrue(result.WasBlocked);
            Assert.AreEqual(240f, result.ReflectedDamage, 0.0001f);
            Assert.IsFalse(result.RewardGranted);
        }

        [Test]
        public void SkillLifecycleIncludesEveryRequiredPhase()
        {
            Assert.That(System.Enum.GetNames(typeof(BossSkillLifecycle)),
                Is.EquivalentTo(new[] { "Start", "Windup", "Resolve", "Blocked", "Cooldown" }));
        }

        [Test]
        public void OnlyFormalHeroLastHitCanReceiveBossXp()
        {
            var heroOwner = new CombatDamageOwner(
                CombatDamageOwnerKind.Hero, TeamSide.Player, "hero-runtime", "hero-id");
            var basicOwner = new CombatDamageOwner(
                CombatDamageOwnerKind.BasicUnit, TeamSide.Player, "basic-runtime");

            Assert.IsTrue(new BossLastHitXpAward(FixedBossIds.W6SoulchainBinder, 6, heroOwner, true).GrantedToHero);
            Assert.IsFalse(new BossLastHitXpAward(FixedBossIds.W6SoulchainBinder, 6, basicOwner, true).GrantedToHero);
            Assert.IsFalse(new BossLastHitXpAward(FixedBossIds.W6SoulchainBinder, 6, heroOwner, false).GrantedToHero);
        }

        [Test]
        public void SummonPolicyCarriesRewardsAndWavePersistenceRules()
        {
            var policy = new BossSummonPolicy(
                BossSummonSpawnSource.BossSkill,
                0,
                0,
                false,
                true,
                true);
            var summon = new BossSummonDefinition(
                FixedBossIds.W20WorldeaterWyrm,
                "WORLDEATER_MINION",
                EnemyArchetype.Elite,
                4,
                330f,
                0.75f,
                BossGoalEffect.InstantDefeat,
                policy);

            Assert.AreEqual(BossSummonSpawnSource.BossSkill, summon.Policy.SpawnSource);
            Assert.IsFalse(summon.Policy.DespawnOnBossDeath);
            Assert.IsTrue(summon.Policy.BlocksWaveScheduleCompletion);
            Assert.IsTrue(summon.Policy.PersistsAcrossWave);
            Assert.AreEqual(4, summon.Count);
        }
    }
}
