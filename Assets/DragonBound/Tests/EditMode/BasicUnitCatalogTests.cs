using DragonBound.Combat;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class BasicUnitCatalogTests
    {
        [TestCase("basic.axe_raider", 1.5f, new[] { 3.00f, 4.50f, 6.30f, 8.19f, 10.24f }, new[] { 1.25f, 1.88f, 2.62f, 3.41f, 4.27f })]
        [TestCase("basic.longbow_hunter", 3.5f, new[] { 2.00f, 3.00f, 4.20f, 5.46f, 6.82f }, new[] { 1.25f, 1.88f, 2.62f, 3.41f, 4.27f })]
        [TestCase("basic.spear_raider", 2.5f, new[] { 2.00f, 3.00f, 4.20f, 5.46f, 6.82f }, new[] { 1.38f, 2.06f, 2.89f, 3.75f, 4.69f })]
        [TestCase("basic.twinaxe_berserker", 2f, new[] { 2.00f, 3.00f, 4.20f, 5.46f, 6.82f }, new[] { 1.25f, 1.88f, 2.62f, 3.41f, 4.27f })]
        public void LevelsOneThroughFiveUseExactConfiguredStats(
            string configId,
            float expectedRange,
            float[] expectedAttack,
            float[] expectedSpeed)
        {
            for (var level = 1; level <= 5; level++)
            {
                var stats = BasicUnitCatalog.GetStats(configId, level);
                Assert.AreEqual(level, stats.Level);
                Assert.AreEqual(expectedAttack[level - 1], stats.Attack, 0.0001f);
                Assert.AreEqual(expectedSpeed[level - 1], stats.AttackSpeed, 0.0001f);
                Assert.AreEqual(expectedRange, stats.RangeCells, 0.0001f);
            }
        }
    }
}
