using DragonBound.Combat;
using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class TargetingSystemTests
    {
        private readonly TargetingSystem targeting = new TargetingSystem();

        [Test]
        public void TargetOutsideRange_IsNotAttacked()
        {
            var enemy = Enemy("outside", 0, 0.5f, new CombatPoint(1.51f, 0f));

            Assert.IsNull(targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                1.5f,
                new[] { enemy }));
        }

        [Test]
        public void TargetEnteringRange_CanBeAttacked()
        {
            var enemy = Enemy("moving", 0, 0.2f, new CombatPoint(4f, 0f));
            Assert.IsNull(targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                1.5f,
                new[] { enemy }));

            enemy.SetTargetingState(0, 0.4f, new CombatPoint(1.4f, 0f));

            Assert.AreSame(enemy, targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                1.5f,
                new[] { enemy }));
        }

        [Test]
        public void FrontmostTargetInRange_IsSelected()
        {
            var rear = Enemy("rear", 1, 0.25f, new CombatPoint(1f, 0f));
            var front = Enemy("front", 2, 0.50f, new CombatPoint(1.2f, 0f));

            Assert.AreSame(front, targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                2f,
                new[] { rear, front }));
        }

        [Test]
        public void DiagonalTargetInsideCircle_IsValid()
        {
            var enemy = Enemy("diagonal", 0, 0.5f, new CombatPoint(1f, 1f));

            Assert.IsTrue(targeting.IsWithinRange(
                new CombatPoint(0f, 0f),
                enemy,
                1.5f));
        }

        [Test]
        public void PathEnemy_BecomesAttackableAtSecondNode_AndStaysAttackableAfterKnockback()
        {
            var path = CreateStraightPath();
            var enemy = new EnemyRuntime("protected.enemy", TeamSide.Player, 100f);
            path.PlaceAtSpawn(enemy);

            Assert.IsTrue(enemy.IsAlive);
            Assert.IsFalse(enemy.IsAttackable);
            Assert.AreEqual(0f, enemy.ApplyDamage(10f).HealthDamage);
            Assert.AreEqual(100f, enemy.HitPoints);

            Assert.IsFalse(path.Advance(enemy, 4.9f, 10f));
            Assert.AreEqual(0, enemy.PathIndex);
            Assert.IsFalse(enemy.IsAttackable);

            Assert.IsFalse(path.Advance(enemy, 0.1f, 10f));
            Assert.AreEqual(1, enemy.PathIndex);
            Assert.IsTrue(enemy.IsAttackable);
            Assert.AreEqual(10f, enemy.ApplyDamage(10f).HealthDamage);

            Assert.IsTrue(path.MoveBackwardByPathDistance(enemy, 0.75f));
            Assert.AreEqual(0, enemy.PathIndex);
            Assert.IsTrue(enemy.IsAttackable);
        }

        [Test]
        public void Targeting_IgnoresSpawnProtectedEnemy()
        {
            var protectedEnemy = new EnemyRuntime("protected", TeamSide.Player);
            CreateStraightPath().PlaceAtSpawn(protectedEnemy);
            var ordinaryCombatTarget = new EnemyRuntime("unpathed", TeamSide.Player);
            ordinaryCombatTarget.SetCombatPosition(new CombatPoint(0.5f, 0f));

            Assert.AreSame(ordinaryCombatTarget, targeting.SelectFrontmostInRange(
                new CombatPoint(0f, 0f),
                2f,
                new[] { protectedEnemy, ordinaryCombatTarget }));
        }

        private static EnemyPath CreateStraightPath()
        {
            return new EnemyPath(
                new[] { "Spawn", "PathPoint_1", "DragonGoal" },
                new[]
                {
                    new CombatPoint(0f, 0f),
                    new CombatPoint(1f, 0f),
                    new CombatPoint(2f, 0f)
                });
        }

        private static EnemyRuntime Enemy(
            string id,
            int pathIndex,
            float pathProgress,
            CombatPoint position)
        {
            var enemy = new EnemyRuntime(id, TeamSide.Player);
            enemy.SetTargetingState(pathIndex, pathProgress, position);
            return enemy;
        }
    }
}
