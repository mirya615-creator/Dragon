using System;
using System.Collections.Generic;
using DragonBound.Grid;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Stable presentation-only identifiers for the fixed-board art handoff. These names are
    /// intentionally independent from map coordinates, occupancy, input, and combat logic.
    /// </summary>
    public static class FixedBoardArtContract
    {
        public const string MapBackground = "ART_MapBackground";
        public const string MapFrame = "ART_MapFrame";
        public const string AiHalfBackground = "ART_AiHalfBackground";
        public const string PlayerHalfBackground = "ART_PlayerHalfBackground";
        public const string CenterDivider = "ART_CenterDivider";
        public const string ForegroundDecoration = "ART_ForegroundDecoration";

        public const string CellUnlocked = "ART_Cell_Unlocked";
        public const string CellLocked = "ART_Cell_Locked";
        public const string CellBorder = "ART_Cell_Border";
        public const string LockMarker = "ART_LockMarker";

        public const string LaneStraightHorizontal = "ART_LaneStraightHorizontal";
        public const string LaneStraightVertical = "ART_LaneStraightVertical";
        public const string LaneCornerLeftUp = "ART_LaneCornerLeftUp";
        public const string LaneCornerLeftDown = "ART_LaneCornerLeftDown";
        public const string LaneCornerRightUp = "ART_LaneCornerRightUp";
        public const string LaneCornerRightDown = "ART_LaneCornerRightDown";

        public const string PlayerSpawn = "ART_PlayerSpawn";
        public const string PlayerGoal = "ART_PlayerGoal";
        public const string AiSpawn = "ART_AiSpawn";
        public const string AiGoal = "ART_AiGoal";

        public static readonly IReadOnlyList<string> MapSlots = new[]
        {
            MapBackground,
            MapFrame,
            AiHalfBackground,
            PlayerHalfBackground,
            CenterDivider,
            ForegroundDecoration
        };

        public static readonly IReadOnlyList<string> CellSlots = new[]
        {
            CellUnlocked,
            CellLocked,
            CellBorder,
            LockMarker,
            LaneStraightHorizontal,
            LaneStraightVertical,
            LaneCornerLeftUp,
            LaneCornerLeftDown,
            LaneCornerRightUp,
            LaneCornerRightDown,
            PlayerSpawn,
            PlayerGoal,
            AiSpawn,
            AiGoal
        };

        public static string GetCellSurfaceSlot(
            FixedBoardLayoutDefinition layout,
            FixedBoardCellDefinition definition)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            switch (definition.Role)
            {
                case FixedBoardCellRole.Deployment:
                    return definition.DeployState == FixedBoardDeployState.Unlocked
                        ? CellUnlocked
                        : CellLocked;
                case FixedBoardCellRole.Spawn:
                    return definition.Owner == FixedBoardCellOwner.Player ? PlayerSpawn : AiSpawn;
                case FixedBoardCellRole.Goal:
                    return definition.Owner == FixedBoardCellOwner.Player ? PlayerGoal : AiGoal;
                case FixedBoardCellRole.Lane:
                    return GetLaneSurfaceSlot(layout, definition.Owner, definition.Coordinate);
                case FixedBoardCellRole.PermanentTerrain:
                case FixedBoardCellRole.Separator:
                    // Compatibility for retained development layouts. ReferenceMap01 itself
                    // never uses these roles, but its presentation host remains reusable.
                    return definition.ArtSlotId;
                default:
                    throw new InvalidOperationException(
                        $"The fixed reference map does not support a surface slot for {definition.Role}.");
            }
        }

        public static string GetLaneSurfaceSlot(
            FixedBoardLayoutDefinition layout,
            FixedBoardCellOwner owner,
            GridPosition position)
        {
            var route = owner == FixedBoardCellOwner.Player
                ? layout.PlayerLaneWaypoints
                : owner == FixedBoardCellOwner.AI
                    ? layout.AiLaneWaypoints
                    : throw new ArgumentOutOfRangeException(nameof(owner));
            var index = IndexOf(route, position);
            if (index <= 0 || index >= route.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(position),
                    "Lane surface slots require an intermediate R waypoint.");
            }

            var previous = route[index - 1];
            var next = route[index + 1];
            var left = previous.X < position.X || next.X < position.X;
            var right = previous.X > position.X || next.X > position.X;
            var down = previous.Y < position.Y || next.Y < position.Y;
            var up = previous.Y > position.Y || next.Y > position.Y;
            if (left && right)
            {
                return LaneStraightHorizontal;
            }

            if (up && down)
            {
                return LaneStraightVertical;
            }

            if (left && up)
            {
                return LaneCornerLeftUp;
            }

            if (left && down)
            {
                return LaneCornerLeftDown;
            }

            if (right && up)
            {
                return LaneCornerRightUp;
            }

            if (right && down)
            {
                return LaneCornerRightDown;
            }

            throw new InvalidOperationException("Lane waypoint has no valid orthogonal surface shape.");
        }

        private static int IndexOf(IReadOnlyList<GridPosition> positions, GridPosition value)
        {
            for (var index = 0; index < positions.Count; index++)
            {
                if (positions[index] == value)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
