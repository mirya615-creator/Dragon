using System;
using System.Collections.Generic;
using System.Text;
using DragonBound.Core;
using UnityEngine;

namespace DragonBound.Recruitment
{
    public enum RecruitDestinationPlan
    {
        AddToEmptySlots,
        RefreshBench
    }

    public interface IRecruitDestination
    {
        int PendingRefreshCount { get; }
        bool PendingRefreshContainsUniqueHeroComponent { get; }
        RecruitDestinationPlan Plan(int cardCount);
        RecruitDestinationReceipt Commit(RecruitDestinationPlan plan, RecruitBatch batch);
    }

    public sealed class RecruitDestinationReceipt
    {
        public RecruitDestinationReceipt(IReadOnlyList<string> removedUnitIds)
        {
            RemovedUnitIds = removedUnitIds ?? throw new ArgumentNullException(nameof(removedUnitIds));
            RemovedCards = new RecruitCard[0];
        }

        public RecruitDestinationReceipt(IReadOnlyList<RecruitCard> removedCards)
        {
            RemovedCards = removedCards ?? throw new ArgumentNullException(nameof(removedCards));
            var removedUnitIds = new string[removedCards.Count];
            for (var index = 0; index < removedCards.Count; index++)
            {
                if (removedCards[index] == null)
                {
                    throw new ArgumentException("Removed cards cannot contain null entries.", nameof(removedCards));
                }

                removedUnitIds[index] = removedCards[index].RuntimeId;
            }

            RemovedUnitIds = removedUnitIds;
        }

        public IReadOnlyList<string> RemovedUnitIds { get; }
        public IReadOnlyList<RecruitCard> RemovedCards { get; }
    }

    public enum RecruitmentStatus
    {
        Success,
        InsufficientResources
    }

    public readonly struct RecruitmentAttempt
    {
        public RecruitmentAttempt(
            long sequence,
            RecruitmentStatus status,
            int cost,
            RecruitBatch batch,
            bool refreshedBench,
            int resourcesBefore,
            int resourcesAfter,
            string resultSummary,
            IReadOnlyList<string> refreshedUnitIds,
            IReadOnlyList<RecruitCard> refreshedCards)
        {
            Sequence = sequence;
            Status = status;
            Cost = cost;
            Batch = batch;
            RefreshedBench = refreshedBench;
            ResourcesBefore = resourcesBefore;
            ResourcesAfter = resourcesAfter;
            ResultSummary = resultSummary;
            RefreshedUnitIds = refreshedUnitIds ?? throw new ArgumentNullException(nameof(refreshedUnitIds));
            RefreshedCards = refreshedCards ?? throw new ArgumentNullException(nameof(refreshedCards));
        }

        public long Sequence { get; }
        public RecruitmentStatus Status { get; }
        public int Cost { get; }
        public RecruitBatch Batch { get; }
        public bool RefreshedBench { get; }
        public int ResourcesBefore { get; }
        public int ResourcesAfter { get; }
        public string ResultSummary { get; }
        public IReadOnlyList<string> RefreshedUnitIds { get; }
        public IReadOnlyList<RecruitCard> RefreshedCards { get; }
    }

    public static class RecruitmentPrice
    {
        public static int GetCost(int recruitmentNumber)
        {
            if (recruitmentNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(recruitmentNumber));
            }

            return checked(10 + (2 * (recruitmentNumber - 1)));
        }
    }

    public sealed class RecruitmentService
    {
        public const int CardsPerRecruitment = 5;

        private readonly TeamState team;
        private readonly RecruitDeck deck;
        private readonly IRecruitDestination destination;
        private readonly HashSet<string> appearedHeroComponentIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> discardedHeroComponentIds =
            new HashSet<string>(StringComparer.Ordinal);
        private long attemptSequence;
        private RecruitmentAttempt lastAttempt;

        public RecruitmentService(TeamState team, RecruitDeck deck, IRecruitDestination destination)
        {
            this.team = team ?? throw new ArgumentNullException(nameof(team));
            this.deck = deck ?? throw new ArgumentNullException(nameof(deck));
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
        }

        public event Action<RecruitmentAttempt> Attempted;

        public int NextCost => RecruitmentPrice.GetCost(deck.CompletedRecruitments + 1);
        public int CompletedRecruitments => deck.CompletedRecruitments;
        public bool UsesFiniteComponentBag => deck.UsesFiniteComponentBag;
        public int InitialHeroComponents => deck.InitialHeroComponents;
        public RecruitDestinationPlan NextDestinationPlan => destination.Plan(CardsPerRecruitment);
        public int PendingRefreshCount => destination.PendingRefreshCount;
        public bool PendingRefreshContainsUniqueHeroComponent =>
            destination.PendingRefreshContainsUniqueHeroComponent;
        public int RemainingHeroComponents => deck.RemainingHeroComponents;
        public int DrawnHeroComponents => deck.DrawnHeroComponents;
        public int DiscardedHeroComponents => deck.DiscardedHeroComponents;
        public bool EnableHeroComponents => deck.EnableHeroComponents;
        public bool HeroSliceMode => deck.HeroSliceMode;
        public bool CanAffordNext => team.Resources >= NextCost;
        public bool HasLastAttempt { get; private set; }
        public RecruitmentAttempt LastAttempt => lastAttempt;
        public string LastRecruitResult => HasLastAttempt ? lastAttempt.ResultSummary : "NONE";

        public int GetRemainingHeroComponentCount(string configId)
        {
            return deck.GetRemainingHeroComponentCount(configId);
        }

        public int GetInitialHeroComponentCount(string configId)
        {
            return deck.GetInitialHeroComponentCount(configId);
        }

        public bool IsUniqueHeroComponent(string configId)
        {
            return deck.IsUniqueHeroComponent(configId);
        }

        public bool HasHeroComponentAppeared(string configId)
        {
            return !string.IsNullOrWhiteSpace(configId) && appearedHeroComponentIds.Contains(configId);
        }

        public bool WasHeroComponentDiscarded(string configId)
        {
            return !string.IsNullOrWhiteSpace(configId) && discardedHeroComponentIds.Contains(configId);
        }

        public RecruitmentAttempt TryRecruit()
        {
            var cost = NextCost;
            var resourcesBefore = team.Resources;
            if (team.Resources < cost)
            {
                return Publish(
                    RecruitmentStatus.InsufficientResources,
                    cost,
                    null,
                    false,
                    resourcesBefore,
                    resourcesBefore,
                    "INSUFFICIENT_RESOURCES",
                    new string[0],
                    new RecruitCard[0]);
            }

            var plan = destination.Plan(CardsPerRecruitment);
            RecruitBatch batch;
            RecruitDestinationReceipt receipt;
            if (deck.UsesFiniteComponentBag)
            {
                // The finite bag is previewed until its cards are successfully accepted by the bench.
                // This keeps the bag cursor and its deterministic streams unchanged on a failed commit.
                batch = deck.PeekNext();
                ValidateBatchCardCount(batch);
                receipt = destination.Commit(plan, batch);
                if (!team.TrySpendResources(cost))
                {
                    throw new InvalidOperationException("Resources changed during a synchronous recruitment transaction.");
                }

                deck.CommitPreviewedBatch(batch);
            }
            else
            {
                if (!team.TrySpendResources(cost))
                {
                    throw new InvalidOperationException("Resources changed during a synchronous recruitment transaction.");
                }

                batch = deck.DrawNext();
                ValidateBatchCardCount(batch);
                receipt = destination.Commit(plan, batch);
            }

            RecordHeroComponentState(batch.Cards, receipt.RemovedCards);
            team.RecordRecruitment();
            var resourcesAfter = team.Resources;
            return Publish(
                RecruitmentStatus.Success,
                cost,
                batch,
                plan == RecruitDestinationPlan.RefreshBench,
                resourcesBefore,
                resourcesAfter,
                FormatResult(batch),
                receipt.RemovedUnitIds,
                receipt.RemovedCards);
        }

        private static void ValidateBatchCardCount(RecruitBatch batch)
        {
            if (batch == null || batch.Cards.Count != CardsPerRecruitment)
            {
                throw new InvalidOperationException(
                    $"Recruitment {batch?.RecruitmentNumber ?? 0} produced {batch?.Cards.Count ?? 0} cards instead of {CardsPerRecruitment}.");
            }
        }

        private void RecordHeroComponentState(
            IReadOnlyList<RecruitCard> drawnCards,
            IReadOnlyList<RecruitCard> removedCards)
        {
            foreach (var card in drawnCards)
            {
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    appearedHeroComponentIds.Add(card.ConfigId);
                }
            }

            foreach (var card in removedCards)
            {
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    discardedHeroComponentIds.Add(card.ConfigId);
                    deck.MarkComponentDiscarded(card.SourceInstanceId);
                }
            }
        }

        private RecruitmentAttempt Publish(
            RecruitmentStatus status,
            int cost,
            RecruitBatch batch,
            bool refreshedBench,
            int resourcesBefore,
            int resourcesAfter,
            string resultSummary,
            IReadOnlyList<string> refreshedUnitIds,
            IReadOnlyList<RecruitCard> refreshedCards)
        {
            attemptSequence++;
            var attempt = new RecruitmentAttempt(
                attemptSequence,
                status,
                cost,
                batch,
                refreshedBench,
                resourcesBefore,
                resourcesAfter,
                resultSummary,
                refreshedUnitIds,
                refreshedCards);
            lastAttempt = attempt;
            HasLastAttempt = true;
            Attempted?.Invoke(attempt);
            Debug.Log(
                $"RecruitNumber={attempt.Sequence} Cost={attempt.Cost} " +
                $"ResourcesBefore={attempt.ResourcesBefore} ResourcesAfter={attempt.ResourcesAfter} " +
                $"Result[5]={attempt.ResultSummary} " +
                $"RefreshedUnits={FormatIds(attempt.RefreshedUnitIds)}");
            foreach (var card in attempt.RefreshedCards)
            {
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    Debug.Log(
                        $"ComponentDiscardedByRefresh RuntimeId={card.RuntimeId} " +
                        $"ConfigId={card.ConfigId} SourceInstanceId={card.SourceInstanceId}");
                }
            }

            return attempt;
        }

        private static string FormatResult(RecruitBatch batch)
        {
            var result = new StringBuilder("[");
            for (var index = 0; index < batch.Cards.Count; index++)
            {
                if (index > 0)
                {
                    result.Append(", ");
                }

                result.Append(batch.Cards[index].ConfigId);
            }

            result.Append("]");
            return result.ToString();
        }

        private static string FormatIds(IReadOnlyList<string> ids)
        {
            var result = new StringBuilder("[");
            for (var index = 0; index < ids.Count; index++)
            {
                if (index > 0)
                {
                    result.Append(", ");
                }

                result.Append(ids[index]);
            }

            result.Append("]");
            return result.ToString();
        }
    }
}
