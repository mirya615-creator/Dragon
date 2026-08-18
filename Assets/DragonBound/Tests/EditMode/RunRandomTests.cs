using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RunRandomTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            var first = new RunSeed(73).Random;
            var second = new RunSeed(73).Random;

            for (var i = 0; i < 16; i++)
            {
                Assert.AreEqual(first.NextInt("test.sequence", -10, 50), second.NextInt("test.sequence", -10, 50));
            }
        }
    }
}
