using DragonBound.Combat;
using DragonBound.Core;
using NUnit.Framework;
using System.Reflection;

namespace DragonBound.Tests.EditMode
{
    public sealed class HeroXpLastHitSettlementTests
    {
        [Test]
        public void LastHeroHitGetsAllXpAndPreviousDamageOwnerGetsNone()
        {
            var enemy = new EnemyRuntime("enemy.last-hit", TeamSide.Player, 100f, EnemyArchetype.Normal);
            enemy.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Hero, TeamSide.Player, "pair.a", "hero.a"));
            enemy.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Hero, TeamSide.Player, "pair.b", "hero.b"));

            Assert.AreEqual(1, HeroXpSettlement.GetAwardedExperience(enemy));
            Assert.AreEqual("pair.b", enemy.LastDamageOwner.SourceRuntimeId);
            Assert.AreEqual("hero.b", enemy.LastDamageOwner.HeroId);
        }

        [Test]
        public void BasicUnitLastHitAwardsNoHeroXp()
        {
            var enemy = new EnemyRuntime("enemy.basic", TeamSide.Player, 10f, EnemyArchetype.Elite);
            enemy.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.BasicUnit, TeamSide.Player, "basic.01"));

            Assert.AreEqual(0, HeroXpSettlement.GetAwardedExperience(enemy));
            Assert.IsTrue(HeroXpSettlement.IsBasicUnitLastHit(enemy));
        }

        [Test]
        public void HeroXpCannotCrossCombatSide()
        {
            var enemy = new EnemyRuntime("enemy.side", TeamSide.AI, 10f, EnemyArchetype.Elite);
            enemy.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Hero, TeamSide.Player, "pair.player", "hero.player"));

            Assert.AreEqual(0, HeroXpSettlement.GetAwardedExperience(enemy));
        }

        [Test]
        public void MultiTargetHeroKillsSettleEachEnemyIndependently()
        {
            var first = new EnemyRuntime("enemy.one", TeamSide.Player, 10f, EnemyArchetype.Normal);
            var second = new EnemyRuntime("enemy.two", TeamSide.Player, 10f, EnemyArchetype.Fast);
            var third = new EnemyRuntime("enemy.three", TeamSide.Player, 10f, EnemyArchetype.Elite);
            var owner = new CombatDamageOwner(
                CombatDamageOwnerKind.Hero, TeamSide.Player, "pair.aoe", "hero.aoe");
            first.RecordDamageOwner(owner);
            second.RecordDamageOwner(owner);
            third.RecordDamageOwner(owner);

            Assert.AreEqual(5,
                HeroXpSettlement.GetAwardedExperience(first) +
                HeroXpSettlement.GetAwardedExperience(second) +
                HeroXpSettlement.GetAwardedExperience(third));
        }

        [Test]
        public void DotLastHitCanRecordItsHeroAfterTheDamageTickSetsZeroHp()
        {
            var enemy = new EnemyRuntime("enemy.dot", TeamSide.Player, 10f, EnemyArchetype.Normal);
            SetHitPointsForPostDamageResult(enemy, 0f);
            enemy.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Hero, TeamSide.Player, "pair.dot", "hero.dot"));

            Assert.AreEqual(1, HeroXpSettlement.GetAwardedExperience(enemy));
            Assert.AreEqual("pair.dot", enemy.LastDamageOwner.SourceRuntimeId);
        }

        [Test]
        public void GroundHazardLastHitCanRecordItsCreatingHeroAfterTheDamageTickSetsZeroHp()
        {
            var enemy = new EnemyRuntime("enemy.hazard", TeamSide.AI, 10f, EnemyArchetype.Elite);
            SetHitPointsForPostDamageResult(enemy, 0f);
            enemy.RecordDamageOwner(new CombatDamageOwner(
                CombatDamageOwnerKind.Hero, TeamSide.AI, "pair.hazard", "hero.hazard"));

            Assert.AreEqual(3, HeroXpSettlement.GetAwardedExperience(enemy));
            Assert.AreEqual("pair.hazard", enemy.LastDamageOwner.SourceRuntimeId);
        }

        [Test]
        public void CleanupWithoutCombatOwnerAwardsNoXp()
        {
            var enemy = new EnemyRuntime("enemy.cleanup", TeamSide.Player, 10f, EnemyArchetype.Elite);
            Assert.AreEqual(0, HeroXpSettlement.GetAwardedExperience(enemy));
            Assert.IsFalse(HeroXpSettlement.IsHeroLastHit(enemy));
        }

        [Test]
        public void PairLinkProgressionProxyKeepsXpWhenReused()
        {
            var progression = new HeroProgressionState(HeroSliceCatalog.WindclawRangerHeroId);
            var proxy = new HeroPairCombatProxy(
                HeroSliceCatalog.WindclawRangerHeroId,
                progression,
                TeamSide.Player,
                "pair.persist",
                "recipe.windclaw");
            proxy.AddExperience(3);

            var reformedProxy = new HeroPairCombatProxy(
                HeroSliceCatalog.WindclawRangerHeroId,
                progression,
                TeamSide.Player,
                "pair.reformed",
                "recipe.windclaw");

            Assert.AreEqual(3, reformedProxy.Experience);
            Assert.AreEqual(1, reformedProxy.Level);
        }

        private static void SetHitPointsForPostDamageResult(EnemyRuntime enemy, float value)
        {
            var setter = typeof(EnemyRuntime)
                .GetProperty(nameof(EnemyRuntime.HitPoints))
                .GetSetMethod(true);
            setter.Invoke(enemy, new object[] { value });
        }
    }
}
