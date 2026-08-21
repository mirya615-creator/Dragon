using System;
using DragonBound.Grid;
using DragonBound.Recruitment;

namespace DragonBound.Core
{
    /// <summary>
    /// Read-only accounting for the 24 finite component instances on one side. The current
    /// board has no separate camp container: recruits are committed directly to Bench cells.
    /// </summary>
    public sealed class ComponentLifecycleSnapshot
    {
        private ComponentLifecycleSnapshot(int remaining, int bench, int boardUnpaired, int pairLinked, int discarded, int delivered)
        {
            RemainingInBag = remaining;
            ComponentsInBench = bench;
            ComponentsOnBoardUnpaired = boardUnpaired;
            ComponentsInPairLinks = pairLinked;
            ComponentsDiscarded = discarded;
            TotalDeliveredComponents = delivered;
        }

        public int RemainingInBag { get; }
        public int ComponentsInCamp => 0;
        public int ComponentsInBench { get; }
        public int ComponentsOnBoardUnpaired { get; }
        public int ComponentsInPairLinks { get; }
        public int ComponentsDiscarded { get; }
        public int TotalDeliveredComponents { get; }
        public int ComponentsLostOther => Math.Max(0, TotalDeliveredComponents - ComponentsInBench - ComponentsOnBoardUnpaired - ComponentsInPairLinks - ComponentsDiscarded);
        public int ConservedTotal => RemainingInBag + ComponentsInCamp + ComponentsInBench + ComponentsOnBoardUnpaired + ComponentsInPairLinks + ComponentsDiscarded;
        public bool IsConserved => ConservedTotal == 24 && ComponentsLostOther == 0;

        public static ComponentLifecycleSnapshot Capture(RecruitmentService recruitment, BoardRecruitDestination destination, BoardGrid board)
        {
            if (recruitment == null) throw new ArgumentNullException(nameof(recruitment));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (board == null) throw new ArgumentNullException(nameof(board));

            var bench = 0;
            var boardUnpaired = 0;
            var pairLinked = 0;
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind != RecruitItemKind.HeroComponent || !board.TryGetPosition(card.RuntimeId, out var position) || !board.TryGetCellType(position, out var type)) continue;
                if (type == CellType.Bench) bench++;
                else if (type == CellType.Battle && destination.TryGetPairLinkForComponent(card.RuntimeId, out _)) pairLinked++;
                else if (type == CellType.Battle) boardUnpaired++;
            }

            return new ComponentLifecycleSnapshot(recruitment.RemainingHeroComponents, bench, boardUnpaired, pairLinked, recruitment.DiscardedHeroComponents, recruitment.DrawnHeroComponents);
        }
    }
}
