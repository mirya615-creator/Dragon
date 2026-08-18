using System;
using DragonBound.Core;

namespace DragonBound.Grid
{
    public static class DragonBoundBoardLayout
    {
        public static BoardGrid CreateDefault(TeamSide side = TeamSide.Player)
        {
            return Create(BattlefieldLayoutDefinitions.Default, side);
        }

        public static BoardGrid Create(string layoutId, TeamSide side = TeamSide.Player)
        {
            return Create(BattlefieldLayoutDefinitions.Get(layoutId), side);
        }

        public static BoardGrid Create(BattlefieldLayoutDefinition layout, TeamSide side = TeamSide.Player)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            return new BoardGrid(layout, side);
        }

        public static BoardGrid CreateFormationOnly(
            BattlefieldLayoutDefinition layout,
            TeamSide side = TeamSide.Player)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            return new BoardGrid(layout, side, false);
        }

        // Retained for existing callers that expect the original 3x3 formation plus five-card bench.
        public static BoardGrid CreateInitial()
        {
            return Create(BattlefieldLayoutDefinitions.Legacy3x3);
        }

        // Retained for existing callers that need the original 3x3 formation without a bench.
        public static BoardGrid CreateFormationOnly()
        {
            return CreateFormationOnly(BattlefieldLayoutDefinitions.Legacy3x3);
        }
    }
}
