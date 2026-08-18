using System;

namespace DragonBound.Grid
{
    /// <summary>
    /// The sole conversion boundary for Fixed8x10_ReferenceMap01's authored
    /// top-down coordinates and Unity's bottom-up grid coordinates.
    /// </summary>
    public static class LayoutCoordinateConverter
    {
        public const int BoardColumns = 8;
        public const int BoardRows = 10;

        public static GridPosition ToRuntime(int configRow, int configColumn)
        {
            ValidateConfigCoordinate(configRow, configColumn);
            return new GridPosition(configColumn, (BoardRows - 1) - configRow);
        }

        public static int ToConfigRow(GridPosition runtimePosition)
        {
            ValidateRuntimeCoordinate(runtimePosition);
            return (BoardRows - 1) - runtimePosition.Y;
        }

        public static int ToConfigColumn(GridPosition runtimePosition)
        {
            ValidateRuntimeCoordinate(runtimePosition);
            return runtimePosition.X;
        }

        private static void ValidateConfigCoordinate(int configRow, int configColumn)
        {
            if (configRow < 0 || configRow >= BoardRows ||
                configColumn < 0 || configColumn >= BoardColumns)
            {
                throw new ArgumentOutOfRangeException(nameof(configRow));
            }
        }

        private static void ValidateRuntimeCoordinate(GridPosition runtimePosition)
        {
            if (runtimePosition.X < 0 || runtimePosition.X >= BoardColumns ||
                runtimePosition.Y < 0 || runtimePosition.Y >= BoardRows)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimePosition));
            }
        }
    }
}
