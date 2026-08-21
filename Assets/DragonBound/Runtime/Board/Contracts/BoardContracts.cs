using System;
using DragonBound.Core;

namespace DragonBound.Grid
{
    public readonly struct GridPosition : IEquatable<GridPosition>, IComparable<GridPosition>
    {
        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public int CompareTo(GridPosition other)
        {
            var yComparison = Y.CompareTo(other.Y);
            return yComparison != 0 ? yComparison : X.CompareTo(other.X);
        }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString() => $"({X}, {Y})";
        public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
        public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
    }

    public enum GridMutationKind
    {
        Placed,
        Moved,
        Swapped,
        Removed,
        CellUnlocked
    }

    public readonly struct GridMutation
    {
        public GridMutation(long sequence, GridMutationKind kind, string unitId, GridPosition? from, GridPosition to)
        {
            Sequence = sequence;
            Kind = kind;
            UnitId = unitId;
            From = from;
            To = to;
        }

        public long Sequence { get; }
        public GridMutationKind Kind { get; }
        public string UnitId { get; }
        public GridPosition? From { get; }
        public GridPosition To { get; }
    }

    public readonly struct BoardOccupant
    {
        public BoardOccupant(string unitId, GridPosition position)
        {
            UnitId = unitId;
            Position = position;
        }

        public string UnitId { get; }
        public GridPosition Position { get; }
    }

    public readonly struct GridDropRejectedBecauseNoSpace
    {
        public GridDropRejectedBecauseNoSpace(GridPosition source, GridPosition target)
        {
            Source = source;
            Target = target;
        }

        public GridPosition Source { get; }
        public GridPosition Target { get; }
    }

    public readonly struct BoardSide
    {
        public BoardSide(TeamSide side, bool isMirrored)
        {
            Side = side;
            IsMirrored = isMirrored;
        }

        public TeamSide Side { get; }
        public bool IsMirrored { get; }
    }

    public interface IBoardPositionTransform
    {
        TeamSide Side { get; }
        GridPosition GetFairCounterpart(GridPosition position);
    }
}
