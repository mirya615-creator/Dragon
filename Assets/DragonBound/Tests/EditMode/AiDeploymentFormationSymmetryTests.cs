using DragonBound.AI;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class AiDeploymentFormationSymmetryTests
    {
        [Test]
        public void SameInputReplayKeepsSideLocalFormationSymmetric()
        {
            var result = new AiDeploymentFormationSymmetryTrace().Run(1701, 8);

            Assert.AreEqual(8, result.Cycles.Count);
            Assert.AreEqual(-1, result.FirstDivergenceCycle, result.FirstDivergenceReason);
        }

        [Test]
        public void SameInputReplayIsDeterministic()
        {
            var first = new AiDeploymentFormationSymmetryTrace().Run(1701, 8);
            var second = new AiDeploymentFormationSymmetryTrace().Run(1701, 8);

            Assert.AreEqual(first.ToCsv(), second.ToCsv());
            Assert.AreEqual(first.FirstDivergenceCycle, second.FirstDivergenceCycle);
            Assert.AreEqual(first.FirstDivergenceReason, second.FirstDivergenceReason);
        }
    }
}
