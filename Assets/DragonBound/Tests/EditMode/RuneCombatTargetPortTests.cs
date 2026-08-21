using DragonBound.Combat;
using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneCombatTargetPortTests
    {
        [Test]
        public void EnemyAdapter_ExposesTypedTargetAndDamageWithoutRuntimeTypeLeak()
        {
            var enemy = new EnemyRuntime("rune-target", TeamSide.Player, 100f, EnemyArchetype.Boss);
            var target = new EnemyRuntimeRuneCombatTarget(enemy);

            Assert.AreEqual("rune-target", target.RuntimeId);
            Assert.IsTrue(target.IsBoss);
            var result = target.ApplyRuneDamage(25f);

            Assert.AreEqual(25f, result.HealthDamage, 0.0001f);
            Assert.IsFalse(result.Killed);
            Assert.AreEqual(75f, enemy.HitPoints, 0.0001f);
        }

        [Test]
        public void EnemyRegistryAdapter_ProvidesStableTypedSnapshotAndSlowPort()
        {
            var registry = new EnemyRegistry();
            var enemy = new EnemyRuntime("rune-normal", TeamSide.Player);
            registry.Register(enemy);
            var targets = new EnemyRegistryRuneCombatTargetRegistry(registry).Snapshot();

            Assert.AreEqual(1, targets.Count);
            Assert.IsTrue(targets[0].TryApplyRuneSlow(0.1f, 5f));
            Assert.AreEqual(0.9f, enemy.MovementSpeedMultiplier, 0.0001f);
        }
    }
}
