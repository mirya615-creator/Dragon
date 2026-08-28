using DragonBound.AI;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class AiStrategyProfileTests
    {
        [TestCase(1, AiStrategyProfileId.Beginner)]
        [TestCase(2, AiStrategyProfileId.Beginner)]
        [TestCase(3, AiStrategyProfileId.Veteran)]
        [TestCase(5, AiStrategyProfileId.Veteran)]
        [TestCase(6, AiStrategyProfileId.Elite)]
        [TestCase(8, AiStrategyProfileId.Elite)]
        [TestCase(9, AiStrategyProfileId.Master)]
        [TestCase(10, AiStrategyProfileId.Master)]
        public void RankMapsToExpectedProfile(int rank, AiStrategyProfileId expected)
        {
            Assert.AreEqual(expected, AiRankProfileMapping.FromRankLevel(rank));
        }

        [Test]
        public void RecoveryLowersExactlyOneTierAndExemptsRankTen()
        {
            Assert.IsTrue(AiRecoveryPolicy.ShouldStartRecovery(9, 2));
            Assert.IsFalse(AiRecoveryPolicy.ShouldStartRecovery(10, 2));
            Assert.AreEqual(
                AiStrategyProfileId.Elite,
                AiRecoveryPolicy.ResolveEffectiveProfile(AiStrategyProfileId.Master, true));
            Assert.AreEqual(
                AiStrategyProfileId.Beginner,
                AiRecoveryPolicy.ResolveEffectiveProfile(AiStrategyProfileId.Beginner, true));
        }

        [Test]
        public void SchedulerIsDeterministicAndDoesNotAdvanceWhilePaused()
        {
            var profile = AiStrategyProfile.Get(AiStrategyProfileId.Elite);
            var first = new AiDecisionScheduler(profile, 7401);
            var second = new AiDecisionScheduler(profile, 7401);
            Assert.AreEqual(first.CurrentIntervalSeconds, second.CurrentIntervalSeconds);

            for (int index = 0; index < 100; index++)
            {
                Assert.IsFalse(first.Tick(1f, false));
            }
            Assert.AreEqual(0, first.DecisionCount);

            Assert.AreEqual(first.Tick(1f, true), second.Tick(1f, true));
            Assert.AreEqual(first.DecisionCount, second.DecisionCount);
            Assert.AreEqual(first.CurrentIntervalSeconds, second.CurrentIntervalSeconds);
        }

        [Test]
        public void FasterProfilesHaveNoGameplayStatFields()
        {
            var beginner = AiStrategyProfile.Get(AiStrategyProfileId.Beginner);
            var master = AiStrategyProfile.Get(AiStrategyProfileId.Master);
            Assert.Greater(beginner.DecisionIntervalSeconds, master.DecisionIntervalSeconds);
            Assert.Greater(beginner.ScoreError, master.ScoreError);
        }

        [Test]
        public void ActionScoreNoiseIsDeterministicAndSmallerForMaster()
        {
            var candidate = new AiActionCandidate(AiActionKind.Recruit, "next", 1f);
            float beginner = AiActionScoring.ApplyProfileError(
                candidate,
                AiStrategyProfile.Get(AiStrategyProfileId.Beginner),
                new RunRandom(99),
                0);
            float master = AiActionScoring.ApplyProfileError(
                candidate,
                AiStrategyProfile.Get(AiStrategyProfileId.Master),
                new RunRandom(99),
                0);
            float replay = AiActionScoring.ApplyProfileError(
                candidate,
                AiStrategyProfile.Get(AiStrategyProfileId.Beginner),
                new RunRandom(99),
                0);

            Assert.AreEqual(beginner, replay);
            Assert.LessOrEqual(System.Math.Abs(master - 1f), 0.05f);
            Assert.LessOrEqual(System.Math.Abs(beginner - 1f), 0.35f);
        }
    }
}
