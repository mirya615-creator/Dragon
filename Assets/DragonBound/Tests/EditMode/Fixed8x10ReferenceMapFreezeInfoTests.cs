using DragonBound.Grid;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class Fixed8x10ReferenceMapFreezeInfoTests
    {
        [Test]
        public void FreezeInfoMatchesTheReferenceMapWithoutDrivingIt()
        {
            var freezeInfo = Fixed8x10ReferenceMapFreezeInfo.Current;
            var layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;

            Assert.AreEqual(BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id, freezeInfo.LayoutId);
            Assert.AreEqual(8, freezeInfo.BoardColumns);
            Assert.AreEqual(10, freezeInfo.BoardRows);
            Assert.AreEqual(80, freezeInfo.TotalCells);
            Assert.AreEqual(6, freezeInfo.UnlockedCellsPerSide);
            Assert.AreEqual(18, freezeInfo.LockedCellsPerSide);
            Assert.AreEqual(16, freezeInfo.PlayerPathNodeCount);
            Assert.AreEqual(16, freezeInfo.AiPathNodeCount);
            Assert.IsTrue(freezeInfo.Matches(layout));
            Assert.AreEqual("1:1", freezeInfo.CellAspectRatio);
            StringAssert.Contains("single conversion layer", freezeInfo.CoordinateConvention);
        }
    }
}
