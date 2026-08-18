using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class UnitRangeRulesTests
    {
        [Test]
        public void FrozenBasicUnitRangesUseCircularRadiusInCells()
        {
            Assert.AreEqual(1.5f, UnitRangeRules.GetRadius(BasicUnitArchetype.Axe));
            Assert.AreEqual(2f, UnitRangeRules.GetRadius(BasicUnitArchetype.Rider));
            Assert.AreEqual(2.5f, UnitRangeRules.GetRadius(BasicUnitArchetype.Spear));
            Assert.AreEqual(3.5f, UnitRangeRules.GetRadius(BasicUnitArchetype.Bow));
        }

        [Test]
        public void RecruitmentConfigMapsToTheExpectedRangeProfile()
        {
            Assert.AreEqual(3.5f, UnitRangeRules.GetRadiusForConfig("unit.longbow"));
            Assert.AreEqual(2.5f, UnitRangeRules.GetRadiusForConfig("unit.spear"));
            Assert.AreEqual(2f, UnitRangeRules.GetRadiusForConfig("unit.twinaxe"));
            Assert.AreEqual(1.5f, UnitRangeRules.GetRadiusForConfig("unit.axe"));
        }
    }
}
