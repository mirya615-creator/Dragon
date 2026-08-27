using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DragonBound.Tests.EditMode
{
    public sealed class RecruitmentServiceTests
    {
        [Test]
        public void RecruitRefresh_RemovesOnlyBenchUnits()
        {
            var team = new TeamState(TeamSide.Player);
            team.AddResources(100);
            var board = DragonBoundBoardLayout.CreateInitial();
            var deck = new RecruitDeck(GreyboxRecruitmentCatalog.Create(), new RunSeed(73).Random, "player");
            var service = new RecruitmentService(team, deck, new BoardRecruitDestination(board));

            var first = service.TryRecruit();
            var firstIds = GetBenchOccupants(board);
            var second = service.TryRecruit();
            var secondIds = GetBenchOccupants(board);

            Assert.AreEqual(RecruitmentStatus.Success, first.Status);
            Assert.IsFalse(first.RefreshedBench);
            Assert.AreEqual(RecruitmentStatus.Success, second.Status);
            Assert.IsTrue(second.RefreshedBench);
            Assert.AreEqual(5, firstIds.Count);
            Assert.AreEqual(5, secondIds.Count);
            foreach (var runtimeId in firstIds)
            {
                Assert.IsFalse(secondIds.Contains(runtimeId));
            }
            CollectionAssert.AreEquivalent(firstIds, second.RefreshedUnitIds);

            Assert.AreEqual(78, team.Resources);
            Assert.AreEqual(2, team.RecruitmentCount);
            Assert.AreEqual(0, deck.RemainingHeroComponents);
            Assert.AreEqual(100, first.ResourcesBefore);
            Assert.AreEqual(90, first.ResourcesAfter);
            Assert.AreEqual(5, first.Batch.Cards.Count);
            Assert.AreEqual(5, second.Batch.Cards.Count);
        }

        [Test]
        public void RecruitRefresh_DoesNotRemoveDeployedUnits()
        {
            var team = new TeamState(TeamSide.Player);
            team.AddResources(100);
            var board = DragonBoundBoardLayout.CreateInitial();
            var deck = new RecruitDeck(GreyboxRecruitmentCatalog.Create(), new RunSeed(73).Random, "player");
            var destination = new BoardRecruitDestination(board);
            var service = new RecruitmentService(team, deck, destination);

            var first = service.TryRecruit();
            var bench = board.GetPositions(CellType.Bench);
            var battle = board.GetPositions(CellType.Battle)[0];
            Assert.IsTrue(board.TryMove(bench[0], battle));

            var deployedId = "";
            Assert.IsTrue(board.TryGetOccupant(battle, out deployedId));
            var second = service.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.Success, first.Status);
            Assert.AreEqual(RecruitmentStatus.Success, second.Status);
            Assert.IsTrue(second.RefreshedBench);
            Assert.AreEqual(2, team.RecruitmentCount);
            Assert.AreEqual(78, team.Resources);
            Assert.AreEqual(5, GetBenchOccupants(board).Count);
            Assert.IsTrue(board.TryGetPosition(deployedId, out var deployedPosition));
            Assert.AreEqual(battle, deployedPosition);
            Assert.IsFalse(GetBenchOccupants(board).Contains(deployedId));
            CollectionAssert.DoesNotContain(second.RefreshedUnitIds, deployedId);
            Assert.AreEqual(4, second.RefreshedUnitIds.Count);
        }

        [Test]
        public void RecruitRefresh_RemovesUnitReturnedFromBattleToBench()
        {
            var team = new TeamState(TeamSide.Player);
            team.AddResources(100);
            var board = DragonBoundBoardLayout.CreateInitial();
            var destination = new BoardRecruitDestination(board);
            var service = new RecruitmentService(
                team,
                new RecruitDeck(GreyboxRecruitmentCatalog.Create(), new RunSeed(73).Random, "player"),
                destination);
            var first = service.TryRecruit();
            var bench = board.GetPositions(CellType.Bench);
            var battle = board.GetPositions(CellType.Battle)[0];
            var returnedId = first.Batch.Cards[0].RuntimeId;
            Assert.IsTrue(board.TryMove(bench[0], battle));
            var drag = new DragPlacementController(board, destination, true);
            Assert.IsTrue(drag.BeginDrag(returnedId));
            Assert.AreEqual(DragDropStatus.Moved, drag.Drop(bench[0]));

            var second = service.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.Success, second.Status);
            Assert.IsTrue(second.RefreshedBench);
            CollectionAssert.Contains(second.RefreshedUnitIds, returnedId);
            Assert.IsFalse(destination.TryGetCard(returnedId, out _));
            Assert.IsFalse(board.TryGetPosition(returnedId, out _));
            Assert.AreEqual(5, destination.CampCount);
            Assert.AreEqual(0, destination.DeployedCount);
        }

        [Test]
        public void RecruitmentPlanDoesNotDependOnBenchOccupancy()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var destination = new BoardRecruitDestination(board);
            var bench = board.GetPositions(CellType.Bench);

            Assert.AreEqual(
                RecruitDestinationPlan.AddToEmptySlots,
                destination.Plan(RecruitmentService.CardsPerRecruitment));

            Assert.IsTrue(board.TryPlace("existing.card", bench[0]));
            Assert.AreEqual(
                RecruitDestinationPlan.RefreshBench,
                destination.Plan(RecruitmentService.CardsPerRecruitment));

            foreach (var position in bench.Skip(1))
            {
                Assert.IsTrue(board.TryPlace($"existing.{position.X}", position));
            }

            Assert.AreEqual(
                RecruitDestinationPlan.RefreshBench,
                destination.Plan(RecruitmentService.CardsPerRecruitment));
        }

        [Test]
        public void InsufficientResourcesDoesNotMutateDestinationOrDeck()
        {
            var team = new TeamState(TeamSide.Player);
            team.AddResources(9);
            var board = DragonBoundBoardLayout.CreateInitial();
            var deck = new RecruitDeck(GreyboxRecruitmentCatalog.Create(), new RunSeed(73).Random, "player");
            var service = new RecruitmentService(team, deck, new BoardRecruitDestination(board));

            var attempt = service.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.InsufficientResources, attempt.Status);
            Assert.AreEqual(9, team.Resources);
            Assert.AreEqual(0, board.GetOccupants().Count);
            Assert.AreEqual(0, deck.CompletedRecruitments);
        }

        [Test]
        public void RecruitmentAttemptResultContainsFiveBasicCardsAndResourceAudit()
        {
            var team = new TeamState(TeamSide.Player);
            team.AddResources(20);
            var board = DragonBoundBoardLayout.CreateInitial();
            var deck = new RecruitDeck(GreyboxRecruitmentCatalog.Create(), new RunSeed(91).Random, "player");
            var attempt = new RecruitmentService(team, deck, new BoardRecruitDestination(board)).TryRecruit();

            Assert.AreEqual(RecruitmentStatus.Success, attempt.Status);
            Assert.AreEqual(1, attempt.Sequence);
            Assert.AreEqual(20, attempt.ResourcesBefore);
            Assert.AreEqual(10, attempt.ResourcesAfter);
            Assert.AreEqual(5, attempt.Batch.Cards.Count);
            Assert.IsTrue(attempt.Batch.Cards.All(card => card.Kind == RecruitItemKind.BasicUnit));
            StringAssert.StartsWith("[", attempt.ResultSummary);
            StringAssert.EndsWith("]", attempt.ResultSummary);
            Assert.AreEqual(5, board.GetOccupants().Count);
        }

        [Test]
        public void DiscardedComponent_DoesNotReturn()
        {
            var team = new TeamState(TeamSide.Player);
            team.AddResources(100);
            var board = DragonBoundBoardLayout.CreateInitial();
            var deck = new RecruitDeck(
                GreyboxRecruitmentCatalog.Create(),
                new RunSeed(73).Random,
                "player",
                true,
                true);
            var destination = new BoardRecruitDestination(board);
            var service = new RecruitmentService(team, deck, destination);

            var first = service.TryRecruit();
            var discarded = first.Batch.Cards.Single(card => card.Kind == RecruitItemKind.HeroComponent);
            Assert.IsTrue(service.HasHeroComponentAppeared(discarded.ConfigId));
            Assert.IsFalse(service.WasHeroComponentDiscarded(discarded.ConfigId));
            Assert.IsFalse(destination.PendingRefreshContainsUniqueHeroComponent);
            Assert.IsFalse(service.PendingRefreshContainsUniqueHeroComponent);

            LogAssert.Expect(
                LogType.Log,
                new Regex(
                    "ComponentDiscardedByRefresh .*ConfigId=" +
                    Regex.Escape(HeroSliceRecruitmentConfig.DragonSigilId)));
            var second = service.TryRecruit();

            CollectionAssert.Contains(second.RefreshedUnitIds, discarded.RuntimeId);
            CollectionAssert.Contains(second.RefreshedCards, discarded);
            Assert.IsTrue(service.WasHeroComponentDiscarded(discarded.ConfigId));
            Assert.GreaterOrEqual(destination.GetCurrentHeroComponentCount(discarded.ConfigId), 0);
            Assert.AreEqual(5, second.RefreshedCards.Count);
            Assert.IsTrue(destination.PendingRefreshContainsUniqueHeroComponent);
            Assert.IsTrue(service.PendingRefreshContainsUniqueHeroComponent);

            var laterSourceIds = new HashSet<string>();
            foreach (var card in second.Batch.Cards)
            {
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    laterSourceIds.Add(card.SourceInstanceId);
                }
            }

            foreach (var card in deck.DrawNext().Cards)
            {
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    laterSourceIds.Add(card.SourceInstanceId);
                }
            }

            foreach (var card in deck.DrawNext().Cards)
            {
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    laterSourceIds.Add(card.SourceInstanceId);
                }
            }

            CollectionAssert.DoesNotContain(laterSourceIds, discarded.SourceInstanceId);
            Assert.AreEqual(0, deck.RemainingHeroComponents);
        }

        [Test]
        public void ProtectedRecruitmentRequiresHeroComponentsToLeaveBenchBeforeRefresh()
        {
            var team = new TeamState(TeamSide.Player);
            team.AddResources(100);
            var board = DragonBoundBoardLayout.CreateInitial();
            var deck = new RecruitDeck(
                GreyboxRecruitmentCatalog.Create(),
                new RunSeed(73).Random,
                "protected.player",
                true,
                true);
            var service = new RecruitmentService(
                team,
                deck,
                new BoardRecruitDestination(board),
                protectHeroComponentsOnRefresh: true);

            Assert.AreEqual(RecruitmentStatus.Success, service.TryRecruit().Status);
            var resourcesAfterFirst = team.Resources;
            var blocked = service.TryRecruit();

            Assert.AreEqual(RecruitmentStatus.PendingHeroComponents, blocked.Status);
            Assert.AreEqual("DEPLOY_HERO_COMPONENTS", blocked.ResultSummary);
            Assert.AreEqual(resourcesAfterFirst, team.Resources);
            Assert.AreEqual(1, deck.CompletedRecruitments);
            Assert.IsTrue(service.IsRefreshBlockedByHeroComponents);
        }

        private static HashSet<string> GetBenchOccupants(BoardGrid board)
        {
            var ids = new HashSet<string>();
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId))
                {
                    ids.Add(runtimeId);
                }
            }

            return ids;
        }
    }
}
