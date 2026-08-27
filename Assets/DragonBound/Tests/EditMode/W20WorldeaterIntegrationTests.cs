using DragonBound.Core;
using DragonBound.Bosses.Runtime;
using DragonBound.Foundation.Contracts;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class W20WorldeaterIntegrationTests
    {
        [Test]
        public void ProductionW20CreatesBothBossSlotsWithoutIncreasingRegularCount()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            var runtime = new TwentyWavePressureRuntime(new MatchController(2001), null, null, 2001, configuration);

            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(20));
            Assert.IsNotNull(runtime.PlayerW20Boss);
            Assert.IsNotNull(runtime.AiW20Boss);
            Assert.AreEqual(WorldeaterWyrmConfiguration.BossId, runtime.PlayerW20Boss.BossId);
            Assert.AreEqual(5000f, runtime.PlayerW20Boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(0.20f, runtime.PlayerW20Boss.BaseMovementSpeedMultiplier * runtime.PlayerPath.TotalDistance / 12f, 0.0001f);
            Assert.AreEqual(1, runtime.PlayerSpawnedThisWave);
            Assert.AreEqual(43, configuration.GetWave(20).EnemyCountPerSide);
        }

        [Test]
        public void ProductionW20WithoutBasicSummonsSubBossAndItSurvivesBossDeath()
        {
            var match = new MatchController(2002);
            var runtime = new TwentyWavePressureRuntime(match, null, null, 2002);
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(20));

            runtime.Tick(12.75f);
            Assert.AreEqual(1, CountWorldeaterSubBosses(runtime.PlayerEnemyRegistry));

            runtime.PlayerW20Boss.ApplyDamage(100000f);
            runtime.Tick(0.01f);
            Assert.IsTrue(runtime.PlayerW20BossRuntime.IsDead);
            Assert.AreEqual(1, CountWorldeaterSubBosses(runtime.PlayerEnemyRegistry));
        }

        [Test]
        public void W20SubBossReachingGoalInstantDefeatsItsSide()
        {
            var match = new MatchController(2003, 1000);
            var runtime = new TwentyWavePressureRuntime(match, null, null, 2003);
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(20));
            runtime.Tick(12.75f);
            runtime.Tick(20.1f);

            Assert.IsTrue(match.Player.IsInstantDefeated);
            Assert.AreEqual(0, match.Player.HatchlingHealth);
        }

        [Test]
        public void WorldeaterBossRewardMappingRemainsHeroOnly()
        {
            Assert.AreEqual(20, BossExperienceRewards.Get(WorldeaterWyrmConfiguration.BossId));
            var boss = new EnemyRuntime("w20.boss", TeamSide.Player, 5000f,
                EnemyArchetype.Boss, 0, WorldeaterWyrmConfiguration.BossId);
            Assert.AreEqual(20, boss.ExperienceReward);
            var minion = new EnemyRuntime("w20.minion", TeamSide.Player, 330f,
                EnemyArchetype.Swarm, 1, WorldeaterWyrmConfiguration.MinionId);
            Assert.AreEqual(0, minion.ExperienceReward);
        }

        private static int CountSwarms(EnemyRegistry registry)
        {
            var count = 0;
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy.Archetype == EnemyArchetype.Swarm)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountWorldeaterSubBosses(EnemyRegistry registry)
        {
            var count = 0;
            foreach (var enemy in registry.Snapshot())
            {
                if (enemy.Archetype == EnemyArchetype.Boss &&
                    enemy.BossId == WorldeaterWyrmConfiguration.SubBossId)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
