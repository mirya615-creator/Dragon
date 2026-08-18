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
