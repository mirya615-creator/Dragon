using DragonBound.Core;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class MatchControllerTests
    {
        [Test]
        public void MatchTransitionsThroughInitializingReadyAndRunning()
        {
            var match = new MatchController(73);

            Assert.AreEqual(MatchState.Initializing, match.State);
            Assert.IsTrue(match.TryTransition(MatchState.Ready));
            Assert.IsTrue(match.TryTransition(MatchState.Running));
            Assert.IsTrue(match.TryTransition(MatchState.Victory));
            Assert.IsFalse(match.TryTransition(MatchState.Preparing));
        }

        [Test]
        public void PlayerAndAIStateRemainIndependentInSnapshot()
        {
            var match = new MatchController(73);
            match.SetCurrentWave(2);
            match.Player.AddResources(25);
            match.Player.RecordRecruitment();
            match.AI.AddResources(10);
            match.AI.ApplyHatchlingDamage(1);

            var snapshot = match.CaptureSnapshot();

            Assert.AreEqual(73, snapshot.RunSeed);
            Assert.AreEqual(2, snapshot.CurrentWave);
            Assert.AreEqual(45, snapshot.Player.Resources);
            Assert.AreEqual(1, snapshot.Player.RecruitmentCount);
            Assert.AreEqual(30, snapshot.AI.Resources);
            Assert.AreEqual(2, snapshot.AI.HatchlingHealth);
        }

        [Test]
        public void SettlementUsesDocumentedPriorityOrder()
        {
            var rule = new FinalBossSettlementRule();
            var hatchlingDefeat = new SettlementContext(0, 100, true, false, 0f, 100f, 1f, 99f);
            var lowerBossHealthWins = new SettlementContext(100, 100, false, false, 20f, 30f, 30f, 20f);
            var fasterClearWinsFinalTie = new SettlementContext(80, 80, false, false, 20f, 20f, 35f, 40f);

            Assert.AreEqual(SettlementDecision.PlayerDefeat, rule.Evaluate(hatchlingDefeat));
            Assert.AreEqual(SettlementDecision.PlayerVictory, rule.Evaluate(lowerBossHealthWins));
            Assert.AreEqual(SettlementDecision.PlayerVictory, rule.Evaluate(fasterClearWinsFinalTie));
        }
    }
}
