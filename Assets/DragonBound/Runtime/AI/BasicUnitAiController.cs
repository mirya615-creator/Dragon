using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.AI
{
    public sealed class BasicUnitAiController
    {
        private readonly BoardGrid board;
        private readonly BoardRecruitDestination destination;
        private readonly RecruitmentService recruitment;

        public BasicUnitAiController(
            BoardGrid board,
            BoardRecruitDestination destination,
            RecruitmentService recruitment)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.recruitment = recruitment ?? throw new ArgumentNullException(nameof(recruitment));
        }

        public RecruitmentAttempt RecruitOrRefresh()
        {
            var attempt = recruitment.TryRecruit();
            if (attempt.Status == RecruitmentStatus.Success)
            {
                MergeAllAvailable();
                if (recruitment.HeroSliceMode)
                {
                    DeployHeroComponentsAndForm();
                }
            }

            return attempt;
        }

        public int MergeAllAvailable()
        {
            var mergedCount = 0;
            while (TryFindMergePair(out var source, out var target))
            {
                var drag = new DragPlacementController(board, destination, true);
                if (!drag.BeginDrag(source.UnitId) ||
                    drag.Drop(target.Position) != DragDropStatus.Merged)
                {
                    Debug.LogError(
                        $"AI merge resolution failed Source={source.UnitId} Target={target.UnitId}");
                    break;
                }

                mergedCount++;
            }

            if (mergedCount > 0)
            {
                Debug.Log(
                    $"AIMergeCompleted Count={mergedCount} " +
                    $"CampCount={destination.CampCount} DeployedCount={destination.DeployedCount}");
            }

            return mergedCount;
        }

        public int DeployOpeningUnits(int maximumUnits)
        {
            if (maximumUnits < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumUnits));
            }

            var cards = new List<RecruitCard>();
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId) &&
                    destination.TryGetCard(runtimeId, out var card) &&
                    card.Kind == RecruitItemKind.BasicUnit)
                {
                    cards.Add(card);
                }
            }

            cards.Sort((first, second) =>
                BasicUnitCatalog.GetStats(first.ConfigId, first.Level).RangeCells.CompareTo(
                    BasicUnitCatalog.GetStats(second.ConfigId, second.Level).RangeCells));
            var battle = new List<GridPosition>();
            foreach (var position in board.GetPositions(CellType.Battle))
            {
                if (!board.IsOccupied(position))
                {
                    battle.Add(position);
                }
            }

            battle.Sort(CompareRoadProximity);

            var deployed = 0;
            for (var index = 0; index < Math.Min(maximumUnits, Math.Min(cards.Count, battle.Count)); index++)
            {
                if (board.TryGetPosition(cards[index].RuntimeId, out var origin) &&
                    board.TryMove(origin, battle[index]))
                {
                    deployed++;
                }
            }

            MergeAllAvailable();
            return deployed;
        }

        public int DeployHeroComponentsAndForm()
        {
            var deployed = 0;
            var components = new List<RecruitCard>();
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId) &&
                    destination.TryGetCard(runtimeId, out var card) &&
                    card.Kind == RecruitItemKind.HeroComponent)
                {
                    components.Add(card);
                }
            }

            components.Sort((first, second) =>
            {
                var unique = second.IsUnique.CompareTo(first.IsUnique);
                return unique != 0 ? unique : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
            });
            foreach (var component in components)
            {
                if (!board.TryGetPosition(component.RuntimeId, out var origin))
                {
                    continue;
                }

                if (!TryFindHeroComponentTarget(component, out var target) &&
                    !TryFindFirstEmptyBattle(out target))
                {
                    continue;
                }

                if (!board.TryMove(origin, target))
                {
                    continue;
                }

                deployed++;
                destination.TryResolvePostDrop(component.RuntimeId);
            }

            if (deployed > 0)
            {
                Debug.Log(
                    $"AIHeroComponentsDeployed Count={deployed} " +
                    $"ActivePairLinks={destination.ActivePairLinkCount}");
            }

            return deployed;
        }

        public DragDropStatus TryAdjust(string runtimeId, GridPosition target)
        {
            var drag = new DragPlacementController(board, destination, true);
            return drag.BeginDrag(runtimeId) ? drag.Drop(target) : DragDropStatus.Reverted;
        }

        private bool TryFindMergePair(out BoardOccupant source, out BoardOccupant target)
        {
            var occupants = board.GetOccupants();
            for (var firstIndex = 0; firstIndex < occupants.Count; firstIndex++)
            {
                var first = occupants[firstIndex];
                if (!board.TryGetCellType(first.Position, out var firstType) ||
                    firstType != CellType.Battle ||
                    !destination.TryGetCard(first.UnitId, out var firstCard) ||
                    firstCard.Level >= BasicUnitCatalog.MaxLevel)
                {
                    continue;
                }

                for (var secondIndex = firstIndex + 1; secondIndex < occupants.Count; secondIndex++)
                {
                    var second = occupants[secondIndex];
                    if (!board.TryGetCellType(second.Position, out var secondType) ||
                        secondType != CellType.Battle ||
                        !destination.TryGetCard(second.UnitId, out var secondCard) ||
                        !firstCard.IsSameBasicUnitAndLevel(secondCard))
                    {
                        continue;
                    }

                    if (CompareMergeTarget(first, second) <= 0)
                    {
                        source = second;
                        target = first;
                    }
                    else
                    {
                        source = first;
                        target = second;
                    }

                    return true;
                }
            }

            source = default;
            target = default;
            return false;
        }

        private bool TryFindHeroComponentTarget(RecruitCard component, out GridPosition target)
        {
            foreach (var occupant in board.GetOccupants())
            {
                if (!destination.TryGetCard(occupant.UnitId, out var candidate) ||
                    candidate.Kind != RecruitItemKind.HeroComponent ||
                    !destination.TryGetComponent(occupant.UnitId, out var candidateRuntime) ||
                    !string.IsNullOrEmpty(candidateRuntime.PairLinkId) ||
                    !HeroSliceCatalog.TryGetRecipeDefinition(component.ConfigId, candidate.ConfigId, out var recipe) ||
                    !board.TryGetCellType(occupant.Position, out var cellType) ||
                    cellType != CellType.Battle)
                {
                    continue;
                }

                if (recipe.TryGetRequiredPositionForComponent(
                        candidate.ConfigId,
                        occupant.Position,
                        component.ConfigId,
                        out var requiredPosition) &&
                    IsEmptyBattleCell(requiredPosition))
                {
                    target = requiredPosition;
                    return true;
                }

                if (TryMoveCandidateIntoConfiguredFormation(
                        component,
                        candidate,
                        occupant.Position,
                        recipe,
                        out target))
                {
                    return true;
                }
            }

            target = default;
            return false;
        }

        private bool TryMoveCandidateIntoConfiguredFormation(
            RecruitCard movingComponent,
            RecruitCard candidate,
            GridPosition candidatePosition,
            HeroRecipeDefinition recipe,
            out GridPosition target)
        {
            foreach (var possibleTarget in board.GetPositions(CellType.Battle))
            {
                if (board.IsOccupied(possibleTarget) ||
                    !recipe.TryGetRequiredPositionForComponent(
                        movingComponent.ConfigId,
                        possibleTarget,
                        candidate.ConfigId,
                        out var candidateTarget) ||
                    !IsEmptyBattleCell(candidateTarget))
                {
                    continue;
                }

                if (!board.TryMove(candidatePosition, candidateTarget))
                {
                    continue;
                }

                destination.TryResolvePostDrop(candidate.RuntimeId);
                target = possibleTarget;
                return true;
            }

            target = default;
            return false;
        }

        private bool IsEmptyBattleCell(GridPosition position)
        {
            return board.TryGetCellType(position, out var cellType) &&
                   cellType == CellType.Battle &&
                   !board.IsOccupied(position);
        }

        private bool TryFindFirstEmptyBattle(out GridPosition target)
        {
            foreach (var position in board.GetPositions(CellType.Battle))
            {
                if (!board.IsOccupied(position))
                {
                    target = position;
                    return true;
                }
            }

            target = default;
            return false;
        }

        private int CompareMergeTarget(BoardOccupant first, BoardOccupant second)
        {
            board.TryGetCellType(first.Position, out var firstType);
            board.TryGetCellType(second.Position, out var secondType);
            var firstDeployed = firstType == CellType.Battle;
            var secondDeployed = secondType == CellType.Battle;
            if (firstDeployed != secondDeployed)
            {
                return firstDeployed ? -1 : 1;
            }

            if (firstDeployed)
            {
                var laneDistance = GetLaneDistance(first.Position).CompareTo(GetLaneDistance(second.Position));
                if (laneDistance != 0)
                {
                    return laneDistance;
                }
            }

            return first.Position.CompareTo(second.Position);
        }

        private int CompareRoadProximity(GridPosition first, GridPosition second)
        {
            var distance = GetLaneDistance(first).CompareTo(GetLaneDistance(second));
            return distance != 0 ? distance : first.CompareTo(second);
        }

        private float GetLaneDistance(GridPosition position)
        {
            return board.GetLaneDistance(position);
        }
    }
}
