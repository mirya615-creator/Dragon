using System;
using System.Collections.Generic;

namespace DragonBound.Grid
{
    public enum DragDropStatus
    {
        Moved,
        Merged,
        Swapped,
        Reverted,
        Cancelled
    }

    public enum OccupiedDropResolution
    {
        Rejected,
        Merged,
        Swapped
    }

    public interface IBoardUnitDropResolver
    {
        bool CanResolveOccupiedDrop(
            string sourceUnitId,
            string targetUnitId,
            GridPosition source,
            GridPosition target,
            CellType sourceType,
            CellType targetType);

        OccupiedDropResolution ResolveOccupiedDrop(
            string sourceUnitId,
            string targetUnitId,
            GridPosition source,
            GridPosition target,
            CellType sourceType,
            CellType targetType);
    }

    public interface IBoardPostDropResolver
    {
        bool TryResolvePostDrop(string movedUnitId);
    }

    public interface IBoardDragLifecycle
    {
        void OnDragStarted(string unitId, GridPosition origin);
        void OnDragCompleted(DragCompletion completion);
    }

    public readonly struct DragCompletion
    {
        public DragCompletion(long sequence, string unitId, GridPosition origin, GridPosition finalPosition, DragDropStatus status)
        {
            Sequence = sequence;
            UnitId = unitId;
            Origin = origin;
            FinalPosition = finalPosition;
            Status = status;
        }

        public long Sequence { get; }
        public string UnitId { get; }
        public GridPosition Origin { get; }
        public GridPosition FinalPosition { get; }
        public DragDropStatus Status { get; }
    }

    public sealed class DragPlacementController
    {
        private readonly BoardGrid board;
        private readonly IBoardUnitDropResolver occupiedDropResolver;
        private readonly IBoardDragLifecycle dragLifecycle;
        private static readonly IReadOnlyList<GridPosition> noHighlightedPositions =
            new List<GridPosition>().AsReadOnly();
        private string activeUnitId;
        private GridPosition activeOrigin;
        private long completionSequence;

        public DragPlacementController(
            BoardGrid board,
            IBoardUnitDropResolver resolver = null,
            bool allowBattleReposition = true)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            occupiedDropResolver = resolver;
            dragLifecycle = resolver as IBoardDragLifecycle;
            AllowBattleReposition = allowBattleReposition;
        }

        public bool AllowBattleReposition { get; }
        public bool IsDragging => activeUnitId != null;
        // Target legality remains a runtime concern, but drag feedback is now arrow-only.
        public IReadOnlyList<GridPosition> HighlightedPositions => noHighlightedPositions;
        public event Action<DragCompletion> Completed;

        public bool BeginDrag(string unitId)
        {
            if (IsDragging ||
                !board.TryGetPosition(unitId, out var origin))
            {
                return false;
            }

            activeUnitId = unitId;
            activeOrigin = origin;
            dragLifecycle?.OnDragStarted(unitId, origin);
            return true;
        }

        public bool IsHighlighted(GridPosition position)
        {
            return false;
        }

        public bool CanPreviewTarget(GridPosition target)
        {
            return IsDragging && IsTargetAccepted(target);
        }

        public DragDropStatus Drop(GridPosition target)
        {
            if (!IsDragging)
            {
                throw new InvalidOperationException("No drag operation is active.");
            }

            var status = ResolveDrop(target);
            var finalPosition = status == DragDropStatus.Moved ||
                                status == DragDropStatus.Merged ||
                                status == DragDropStatus.Swapped
                ? target
                : activeOrigin;
            Complete(status, finalPosition);
            return status;
        }

        private DragDropStatus ResolveDrop(GridPosition target)
        {
            if (!IsTargetAccepted(target) ||
                !board.TryGetCellType(activeOrigin, out var sourceType) ||
                !board.TryGetCellType(target, out var targetType))
            {
                board.ReportDropRejectedBecauseNoFreeBattleCell(activeOrigin, target);
                return DragDropStatus.Reverted;
            }

            if (!board.TryGetOccupant(target, out var targetUnitId))
            {
                if (!board.TryMove(activeOrigin, target))
                {
                    return DragDropStatus.Reverted;
                }

                return DragDropStatus.Moved;
            }

            if (occupiedDropResolver == null)
            {
                return DragDropStatus.Reverted;
            }

            switch (occupiedDropResolver.ResolveOccupiedDrop(
                        activeUnitId,
                        targetUnitId,
                        activeOrigin,
                        target,
                        sourceType,
                        targetType))
            {
                case OccupiedDropResolution.Merged:
                    return DragDropStatus.Merged;
                case OccupiedDropResolution.Swapped:
                    return DragDropStatus.Swapped;
                default:
                    return DragDropStatus.Reverted;
            }
        }

        public void Cancel()
        {
            if (!IsDragging)
            {
                return;
            }

            Complete(DragDropStatus.Cancelled, activeOrigin);
        }

        private bool IsTargetAccepted(GridPosition target)
        {
            if (!board.TryGetCellType(activeOrigin, out var sourceType) ||
                !board.TryGetCellType(target, out var targetType) ||
                target == activeOrigin ||
                !board.IsPlaceable(target) ||
                !IsTransitionAllowed(sourceType, targetType))
            {
                return false;
            }

            if (!board.TryGetOccupant(target, out var targetUnitId))
            {
                return true;
            }

            return occupiedDropResolver != null &&
                   occupiedDropResolver.CanResolveOccupiedDrop(
                       activeUnitId,
                       targetUnitId,
                       activeOrigin,
                       target,
                       sourceType,
                       targetType);
        }

        private bool IsTransitionAllowed(CellType sourceType, CellType targetType)
        {
            if (sourceType == CellType.Bench)
            {
                return targetType == CellType.Bench || targetType == CellType.Battle;
            }

            return sourceType == CellType.Battle &&
                   (targetType == CellType.Battle || targetType == CellType.Bench) &&
                   AllowBattleReposition;
        }

        private void Complete(DragDropStatus status, GridPosition finalPosition)
        {
            completionSequence++;
            var completion = new DragCompletion(
                completionSequence,
                activeUnitId,
                activeOrigin,
                finalPosition,
                status);

            activeUnitId = null;
            dragLifecycle?.OnDragCompleted(completion);
            Completed?.Invoke(completion);
        }
    }
}
