using DragonBound.Core;
using DragonBound.Bosses.Runtime;
using DragonBound.Combat;
using DragonBound.Foundation.Contracts;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class W16BloodcrownIntegrationTests
    {
        [Test]
        public void ProductionW16CreatesIndependentBossesWithoutIncreasingRegularWaveCount()
        {
            var configuration = TwentyWavePressureConfiguration.CreateCoreLoopV2();
            var runtime = new TwentyWavePressureRuntime(new MatchController(1601), null, null, 1601, configuration);

            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(16));

            Assert.IsNotNull(runtime.PlayerW16Boss);
            Assert.IsNotNull(runtime.AiW16Boss);
            Assert.AreEqual(BloodcrownTyrantConfiguration.BossId, runtime.PlayerW16Boss.BossId);
            Assert.AreEqual(BloodcrownTyrantConfiguration.GreyboxMaxHitPoints, runtime.PlayerW16Boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(BloodcrownTyrantConfiguration.GreyboxMaxHitPoints, runtime.AiW16Boss.MaxHitPoints, 0.0001f);
            Assert.AreEqual(BloodcrownTyrantConfiguration.BossMoveSpeedCellsPerSecond,
                runtime.PlayerW16Boss.BaseMovementSpeedMultiplier * runtime.PlayerPath.TotalDistance / 12f,
                0.0001f);
            Assert.AreEqual(1, runtime.PlayerSpawnedThisWave,
                "W16 Normal #1 is spawned by the shared queue before the independent Boss slot.");
            Assert.LessOrEqual(runtime.PlayerSpawnedThisWave,
                runtime.Configuration.GetWave(16).EnemyCountPerSide);
        }

        [Test]
        public void W16DecreeRunsThroughProductionTickAndDeathRestoresPolicy()
        {
            var runtime = new TwentyWavePressureRuntime(new MatchController(1602), null, null, 1602);
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(16));

            runtime.Tick(9f);
            Assert.IsTrue(runtime.PlayerW16BossRuntime.IsDecreeApplied);
            Assert.AreEqual(1, runtime.PlayerW16BossRuntime.CastAttemptCount);

            runtime.PlayerW16Boss.ApplyDamage(100000f);
            runtime.Tick(0.01f);
            Assert.IsTrue(runtime.PlayerW16BossRuntime.IsDead);
        }

        [Test]
        public void W16SpellbreakerUsesRuntimeMaxHpAndLeavesDecreeInactive()
        {
            var runtime = new TwentyWavePressureRuntime(new MatchController(1604), null, null, 1604);
            runtime.SetSpellbreakerResolver(TeamSide.Player, new BlockingSpellbreaker());
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(16));

            runtime.Tick(9f);
            Assert.AreEqual(2160f, runtime.PlayerW16Boss.HitPoints, 0.0001f);
            Assert.IsFalse(runtime.PlayerW16BossRuntime.IsDecreeApplied);
            Assert.AreEqual(1, runtime.PlayerW16BossRuntime.CastAttemptCount);
        }

        [Test]
        public void ExistingW6AndW12BossEntrypointsRemainUnchanged()
        {
            var runtime = new TwentyWavePressureRuntime(new MatchController(1603), null, null, 1603);
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.JumpToWave(6));
            Assert.AreEqual(SoulchainBinderConfiguration.BossId, runtime.PlayerW6Boss.BossId);
            Assert.IsTrue(runtime.JumpToWave(12));
            Assert.AreEqual(StormcallerPriestConfiguration.BossId, runtime.PlayerW12Boss.BossId);
            Assert.AreEqual(600f, runtime.PlayerW6Boss.MaxHitPoints, 0.0001f);
        }
    }

    internal sealed class BlockingSpellbreaker : ISoulChainSpellbreakerResolver
    {
        public bool ShouldBlockCast(SoulChainBossCastContext context) => true;
    }
}
