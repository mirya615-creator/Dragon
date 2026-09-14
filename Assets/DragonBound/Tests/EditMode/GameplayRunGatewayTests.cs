using System.Threading;
using DragonBound.Services;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class GameplayRunGatewayTests
    {
        [TestCase(true, "Greybox_Main", true)]
        [TestCase(true, "Main", false)]
        [TestCase(true, "Login", false)]
        [TestCase(false, "Greybox_Main", false)]
        public void DirectDevelopmentSceneEntryRequiresDevelopmentGreyboxInitialScene(
            bool isDevelopment,
            string sceneName,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                GameplayLaunchContext.IsDirectDevelopmentScene(isDevelopment, sceneName));
        }

        [Test]
        public void DiagnosticSeedReplaysAllGameplayStreams()
        {
            var gateway = new LocalGameplayRunGateway();
            var request = new StartGameplayRunRequest
            {
                UseDiagnosticSeed = true,
                DiagnosticSeed = 20260801
            };

            var first = gateway.StartRunAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            var second = gateway.StartRunAsync(request, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(first.RunSeed, second.RunSeed);
            Assert.AreEqual(first.PlayerRecruitSeed, second.PlayerRecruitSeed);
            Assert.AreEqual(first.AiRecruitSeed, second.AiRecruitSeed);
            Assert.AreEqual(first.CombatSeed, second.CombatSeed);
            Assert.AreNotEqual(first.PlayerRecruitSeed, first.AiRecruitSeed);
            Assert.AreEqual(SharedRandomProtocolV1.Version, first.RandomProtocolVersion);
            Assert.AreEqual(first.RunSeed, first.WaveRandomSeed);
            Assert.AreEqual(
                SharedRandomProtocolV1.DeriveSeed(first.RunSeed, "player.recruit"),
                first.PlayerRecruitSeed);
            Assert.AreEqual(
                SharedRandomProtocolV1.DeriveSeed(first.RunSeed, "ai.recruit"),
                first.AiRecruitSeed);
            Assert.AreEqual(
                SharedRandomProtocolV1.DeriveSeed(first.RunSeed, "combat"),
                first.CombatSeed);
        }

        [Test]
        public void NormalLocalRunsReceiveIndependentRunIdentityAndSeed()
        {
            var gateway = new LocalGameplayRunGateway();
            var first = gateway.StartRunAsync(
                new StartGameplayRunRequest(),
                CancellationToken.None).GetAwaiter().GetResult();
            var second = gateway.StartRunAsync(
                new StartGameplayRunRequest(),
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreNotEqual(first.RunId, second.RunId);
            Assert.AreNotEqual(first.RunSeed, second.RunSeed);
        }

        [Test]
        public void SameLaunchNonceReturnsSameRunConfiguration()
        {
            var gateway = new LocalGameplayRunGateway();
            var request = new StartGameplayRunRequest
            {
                ClientRunNonce = System.Guid.NewGuid().ToString("N")
            };

            var first = gateway.StartRunAsync(request, CancellationToken.None)
                .GetAwaiter().GetResult();
            var second = gateway.StartRunAsync(request, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(first.RunId, second.RunId);
            Assert.AreEqual(first.RunSeed, second.RunSeed);
            Assert.AreEqual(90, first.ReconnectGraceSeconds);
        }

        [TestCase(1, "Beginner")]
        [TestCase(3, "Veteran")]
        [TestCase(6, "Elite")]
        [TestCase(9, "Master")]
        public void LocalRunMapsRankToFourTierProfile(int rankLevel, string expectedProfile)
        {
            var gateway = new LocalGameplayRunGateway();
            var result = gateway.StartRunAsync(
                new StartGameplayRunRequest
                {
                    PlayerId = System.Guid.NewGuid().ToString("N"),
                    PlayerRankLevel = rankLevel,
                    UseDiagnosticSeed = true,
                    DiagnosticSeed = 7401
                },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(rankLevel, result.PlayerRankLevel);
            Assert.AreEqual(expectedProfile, result.AiProfile);
            Assert.AreNotEqual(0, result.AiDecisionSeed);
            Assert.AreEqual(LocalGameplayRunGateway.LocalAiAlgorithmVersion, result.AiAlgorithmVersion);
            Assert.IsFalse(result.IsRecoveryMatch);
        }

        [Test]
        public void TwoNormalDefeatsCreateExactlyOneEasierRecoveryRun()
        {
            string playerId = System.Guid.NewGuid().ToString("N");
            var gateway = new LocalGameplayRunGateway();
            for (int index = 0; index < 2; index++)
            {
                gateway.FinishRunAsync(
                    new FinishGameplayRunRequest
                    {
                        PlayerId = playerId,
                        RunId = System.Guid.NewGuid().ToString("N"),
                        IdempotencyKey = System.Guid.NewGuid().ToString("N"),
                        ProposedResult = ServerMatchResult.Defeat,
                        TerminationReason = GameplayTerminationReason.Natural,
                        FaultAttribution = GameplayFaultAttribution.None
                    },
                    CancellationToken.None).GetAwaiter().GetResult();
            }

            var recovery = gateway.StartRunAsync(
                new StartGameplayRunRequest
                {
                    PlayerId = playerId,
                    PlayerRankLevel = 9
                },
                CancellationToken.None).GetAwaiter().GetResult();
            var following = gateway.StartRunAsync(
                new StartGameplayRunRequest
                {
                    PlayerId = playerId,
                    PlayerRankLevel = 9
                },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(recovery.IsRecoveryMatch);
            Assert.AreEqual("Elite", recovery.AiProfile);
            Assert.IsFalse(following.IsRecoveryMatch);
            Assert.AreEqual("Master", following.AiProfile);
        }

        [TestCase(GameplayFaultAttribution.Player, ServerMatchResult.Defeat, true)]
        [TestCase(GameplayFaultAttribution.Server, ServerMatchResult.NoContest, false)]
        [TestCase(GameplayFaultAttribution.Unknown, ServerMatchResult.Pending, false)]
        public void FinishMapsAttributionToAuthoritativeSettlement(
            GameplayFaultAttribution attribution,
            ServerMatchResult expected,
            bool applyProgress)
        {
            var gateway = new LocalGameplayRunGateway();
            var request = new FinishGameplayRunRequest
            {
                RunId = System.Guid.NewGuid().ToString("N"),
                IdempotencyKey = System.Guid.NewGuid().ToString("N"),
                ProposedResult = ServerMatchResult.Victory,
                TerminationReason = attribution == GameplayFaultAttribution.Unknown
                    ? GameplayTerminationReason.Unknown
                    : GameplayTerminationReason.Natural,
                FaultAttribution = attribution
            };

            FinishGameplayRunResult result = gateway.FinishRunAsync(
                request, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(expected, result.Result);
            Assert.AreEqual(applyProgress, result.ApplyRank);
            Assert.AreEqual(applyProgress, result.CountCompletedRun);
            Assert.AreEqual(applyProgress, result.GrantRewards);
        }

        [Test]
        public void PreparedLaunchResultIsConsumedExactlyOnce()
        {
            string nonce = System.Guid.NewGuid().ToString("N");
            var source = new StartGameplayRunResult
            {
                RunId = System.Guid.NewGuid().ToString("N"),
                RunSeed = 71,
                PlayerRecruitSeed = 72,
                AiRecruitSeed = 73,
                CombatSeed = 74,
                RulesVersion = "rules-v1",
                PlayerRankLevel = 4
            };

            GameplayLaunchContext.StorePrepared(nonce, source);
            source.RunId = "mutated-after-store";

            Assert.IsFalse(GameplayLaunchContext.TryTakePrepared("wrong-nonce", out _));
            Assert.IsTrue(GameplayLaunchContext.TryTakePrepared(nonce, out var prepared));
            Assert.AreNotEqual(source.RunId, prepared.RunId);
            Assert.AreEqual(71, prepared.RunSeed);
            Assert.AreEqual(4, prepared.PlayerRankLevel);
            Assert.IsFalse(GameplayLaunchContext.TryTakePrepared(nonce, out _));
        }
    }
}
