using System;
using DragonBound.Grid;

namespace DragonBound.Recruitment
{
    /// <summary>
    /// The sole route for both recruited and externally granted shovels to unlock a deployment
    /// cell. It never uses the debug adjacency rule: a shovel may target any own locked cell.
    /// </summary>
    public sealed class ShovelUnlockService
    {
        private readonly BoardGrid board;
        private readonly BoardRecruitDestination destination;
        private int grantedShovelCount;
        private string selectedBenchShovelRuntimeId;
        private bool selectedGrantedShovel;

        public ShovelUnlockService(BoardGrid board, BoardRecruitDestination destination)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
            if (destination.Board != board)
            {
                throw new ArgumentException("The shovel destination must belong to the supplied board.", nameof(destination));
            }
        }

        public event Action StateChanged;
        public event Action<int> ShovelGrantedExternally;
        public event Action<GridPosition> ShovelUsed;

        public int GrantedShovelCount => grantedShovelCount;
        public int BenchShovelCount => destination.GetBenchShovelCount();
        public int AvailableShovelCount => grantedShovelCount + BenchShovelCount;
        public bool IsSelecting => selectedGrantedShovel || !string.IsNullOrEmpty(selectedBenchShovelRuntimeId);
        public string SelectedBenchShovelRuntimeId => selectedBenchShovelRuntimeId;

        public void GrantShovel(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            grantedShovelCount = checked(grantedShovelCount + count);
            ShovelGrantedExternally?.Invoke(count);
            StateChanged?.Invoke();
        }

        public bool BeginSelection(string benchShovelRuntimeId = null)
        {
            if (IsSelecting)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(benchShovelRuntimeId))
            {
                if (!destination.IsBenchShovel(benchShovelRuntimeId))
                {
                    return false;
                }

                selectedBenchShovelRuntimeId = benchShovelRuntimeId;
            }
            else if (grantedShovelCount > 0)
            {
                selectedGrantedShovel = true;
            }
            else if (!destination.TryGetFirstBenchShovel(out selectedBenchShovelRuntimeId))
            {
                return false;
            }

            StateChanged?.Invoke();
            return true;
        }

        public void CancelSelection()
        {
            if (!IsSelecting)
            {
                return;
            }

            selectedBenchShovelRuntimeId = null;
            selectedGrantedShovel = false;
            StateChanged?.Invoke();
        }

        public bool TryUnlockCell(GridPosition position)
        {
            if (!IsSelecting ||
                !board.TryGetCellType(position, out var cellType) ||
                cellType != CellType.Locked ||
                (board.Layout != null && !board.Layout.IsUnlockable(position, board.Side)))
            {
                return false;
            }

            // Validate the selected source before changing the grid, so an already refreshed card
            // can never result in a free unlock.
            if (selectedGrantedShovel)
            {
                if (grantedShovelCount < 1)
                {
                    CancelSelection();
                    return false;
                }
            }
            else if (!destination.IsBenchShovel(selectedBenchShovelRuntimeId))
            {
                CancelSelection();
                return false;
            }

            if (!board.TryUnlock(position))
            {
                return false;
            }

            if (selectedGrantedShovel)
            {
                grantedShovelCount--;
            }
            else if (!destination.TryConsumeBenchShovel(selectedBenchShovelRuntimeId))
            {
                throw new InvalidOperationException("A validated shovel could not be consumed.");
            }

            selectedBenchShovelRuntimeId = null;
            selectedGrantedShovel = false;
            ShovelUsed?.Invoke(position);
            StateChanged?.Invoke();
            return true;
        }
    }
}
