using System;
using DragonBound.Grid;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    // Development-only aggregate for comparing layout capacity without a telemetry backend.
    public sealed class GreyboxRunStatistics
    {
        private readonly BoardGrid board;
        private readonly BoardRecruitDestination destination;
        private readonly RecruitmentService recruitment;

        public GreyboxRunStatistics(
            string layoutId,
            BoardGrid board,
            BoardRecruitDestination destination,
            RecruitmentService recruitment)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                throw new ArgumentException("A layout id is required.", nameof(layoutId));
            }

            LayoutId = layoutId;
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.recruitment = recruitment ?? throw new ArgumentNullException(nameof(recruitment));

            board.Changed += HandleBoardChanged;
            board.DropRejectedBecauseNoSpace += _ => DropRejectedBecauseNoSpace++;
            recruitment.Attempted += HandleRecruitment;
            destination.HeroPairLinked += _ => PairReformCount++;
            destination.HeroPairUnlinked += _ => PairBreakCount++;
        }

        public string LayoutId { get; }
        public int RecruitCount { get; private set; }
        public int MoveCount { get; private set; }
        public int SwapCount { get; private set; }
        public int MergeCount { get; private set; }
        public int PairBreakCount { get; private set; }
        public int PairReformCount { get; private set; }
        public int RecruitRejectedBecauseNoSpace { get; private set; }
        public int DropRejectedBecauseNoSpace { get; private set; }
        public int FirstRecruitWithNoFreeBattleCell { get; private set; }
        public int MaxSimultaneousBasicUnits { get; private set; }
        public int MaxSimultaneousPairLinks { get; private set; }
        public int MaxUnpairedComponents { get; private set; }

        public int UnlockedCellCount => board.GetPositions(CellType.Battle).Count;
        public int OccupiedBattleCellCount => destination.DeployedCount;
        public int FreeBattleCellCount => UnlockedCellCount - OccupiedBattleCellCount;
        public int ActivePairLinkCount => destination.ActivePairLinkCount;

        public int BasicUnitCount
        {
            get
            {
                var count = 0;
                foreach (var occupant in board.GetOccupants())
                {
                    if (destination.TryGetCard(occupant.UnitId, out var card) &&
                        card.Kind == RecruitItemKind.BasicUnit)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int UnpairedComponentCount
        {
            get
            {
                var count = 0;
                foreach (var occupant in board.GetOccupants())
                {
                    if (destination.TryGetComponent(occupant.UnitId, out var component) &&
                        string.IsNullOrEmpty(component.PairLinkId))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void RecordDropResult(DragDropStatus status)
        {
            switch (status)
            {
                case DragDropStatus.Merged:
                    MergeCount++;
                    break;
                case DragDropStatus.Reverted:
                    DropRejectedBecauseNoSpace++;
                    break;
            }

            RefreshMaximums();
        }

        public override string ToString()
        {
            return
                $"LayoutId={LayoutId} RecruitCount={RecruitCount} " +
                $"UnlockedCellCount={UnlockedCellCount} OccupiedBattleCellCount={OccupiedBattleCellCount} " +
                $"FreeBattleCellCount={FreeBattleCellCount} BasicUnitCount={BasicUnitCount} " +
                $"UnpairedComponentCount={UnpairedComponentCount} ActivePairLinkCount={ActivePairLinkCount} " +
                $"MoveCount={MoveCount} SwapCount={SwapCount} MergeCount={MergeCount} " +
                $"PairBreakCount={PairBreakCount} PairReformCount={PairReformCount} " +
                $"RecruitRejectedBecauseNoSpace={RecruitRejectedBecauseNoSpace} " +
                $"DropRejectedBecauseNoSpace={DropRejectedBecauseNoSpace} " +
                $"FirstRecruitWithNoFreeBattleCell={FirstRecruitWithNoFreeBattleCell} " +
                $"MaxSimultaneousBasicUnits={MaxSimultaneousBasicUnits} " +
                $"MaxSimultaneousPairLinks={MaxSimultaneousPairLinks} " +
                $"MaxUnpairedComponents={MaxUnpairedComponents}";
        }

        private void HandleRecruitment(RecruitmentAttempt attempt)
        {
            if (attempt.Status == RecruitmentStatus.Success)
            {
                RecruitCount++;
                if (FreeBattleCellCount == 0 && FirstRecruitWithNoFreeBattleCell == 0)
                {
                    FirstRecruitWithNoFreeBattleCell = RecruitCount;
                }
            }

            RefreshMaximums();
        }

        private void HandleBoardChanged(GridMutation mutation)
        {
            if (mutation.Kind == GridMutationKind.Moved)
            {
                MoveCount++;
            }
            else if (mutation.Kind == GridMutationKind.Swapped &&
                     mutation.From.HasValue &&
                     mutation.From.Value.CompareTo(mutation.To) < 0)
            {
                SwapCount++;
            }

            RefreshMaximums();
        }

        private void RefreshMaximums()
        {
            MaxSimultaneousBasicUnits = Math.Max(MaxSimultaneousBasicUnits, BasicUnitCount);
            MaxSimultaneousPairLinks = Math.Max(MaxSimultaneousPairLinks, ActivePairLinkCount);
            MaxUnpairedComponents = Math.Max(MaxUnpairedComponents, UnpairedComponentCount);
        }
    }
}
