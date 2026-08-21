using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;

namespace DragonBound.Grid
{
    public enum CellType
    {
        Locked,
        Battle,
        Bench
    }

    public sealed class BoardGrid
    {
        private readonly Dictionary<GridPosition, CellType> cells = new Dictionary<GridPosition, CellType>();
        private readonly Dictionary<GridPosition, string> occupants = new Dictionary<GridPosition, string>();
        private readonly Dictionary<string, GridPosition> unitPositions =
            new Dictionary<string, GridPosition>(StringComparer.Ordinal);
        private long mutationSequence;

        public BoardGrid(BattlefieldLayoutDefinition layout, TeamSide side, bool includeBench = true)
            : this(CreateCells(layout, side, includeBench))
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Side = side;
        }

        public BoardGrid(IEnumerable<KeyValuePair<GridPosition, CellType>> layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            foreach (var entry in layout)
            {
                if (cells.ContainsKey(entry.Key))
                {
                    throw new ArgumentException($"Duplicate grid position {entry.Key}.", nameof(layout));
                }

                cells.Add(entry.Key, entry.Value);
            }

            if (cells.Count == 0)
            {
                throw new ArgumentException("A board requires at least one cell.", nameof(layout));
            }
        }

        public event Action<GridMutation> Changed;
        public event Action<GridDropRejectedBecauseNoSpace> DropRejectedBecauseNoSpace;
        public BattlefieldLayoutDefinition Layout { get; }
        public FixedBoardLayoutDefinition FixedLayout => Layout as FixedBoardLayoutDefinition;
        public TeamSide Side { get; }
        public int CellCount => cells.Count;

        public int UnlockedBattleCellCount => GetPositions(CellType.Battle).Count;

        public int OccupiedBattleCellCount
        {
            get
            {
                var count = 0;
                foreach (var entry in occupants)
                {
                    if (cells.TryGetValue(entry.Key, out var type) && type == CellType.Battle)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int FreeBattleCellCount => UnlockedBattleCellCount - OccupiedBattleCellCount;

        public IReadOnlyList<GridPosition> GetPositions(CellType cellType)
        {
            var positions = new List<GridPosition>();
            foreach (var entry in cells)
            {
                if (entry.Value == cellType)
                {
                    positions.Add(entry.Key);
                }
            }

            positions.Sort();
            return positions;
        }

        public IReadOnlyList<BoardOccupant> GetOccupants()
        {
            var values = new List<BoardOccupant>(unitPositions.Count);
            foreach (var entry in unitPositions)
            {
                values.Add(new BoardOccupant(entry.Key, entry.Value));
            }

            values.Sort((first, second) => first.Position.CompareTo(second.Position));
            return values;
        }

        public bool TryGetCellType(GridPosition position, out CellType cellType)
        {
            return cells.TryGetValue(position, out cellType);
        }

        public bool TryGetOccupant(GridPosition position, out string unitId)
        {
            return occupants.TryGetValue(position, out unitId);
        }

        public bool TryGetPosition(string unitId, out GridPosition position)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                position = default;
                return false;
            }

            return unitPositions.TryGetValue(unitId, out position);
        }

        public bool TryPlace(string unitId, GridPosition position)
        {
            if (string.IsNullOrWhiteSpace(unitId) ||
                unitPositions.ContainsKey(unitId) ||
                !IsPlaceable(position) ||
                occupants.ContainsKey(position))
            {
                return false;
            }

            occupants.Add(position, unitId);
            unitPositions.Add(unitId, position);
            Publish(GridMutationKind.Placed, unitId, null, position);
            return true;
        }

        public bool TryMove(GridPosition from, GridPosition to)
        {
            if (from == to ||
                !occupants.TryGetValue(from, out var unitId) ||
                !unitPositions.ContainsKey(unitId) ||
                !IsPlaceable(to) ||
                occupants.ContainsKey(to))
            {
                return false;
            }

            occupants.Remove(from);
            occupants.Add(to, unitId);
            unitPositions[unitId] = to;
            Publish(GridMutationKind.Moved, unitId, from, to);
            return true;
        }

        public bool TrySwap(GridPosition first, GridPosition second)
        {
            if (first == second ||
                !IsPlaceable(first) ||
                !IsPlaceable(second) ||
                !occupants.TryGetValue(first, out var firstUnitId) ||
                !occupants.TryGetValue(second, out var secondUnitId) ||
                string.Equals(firstUnitId, secondUnitId, StringComparison.Ordinal))
            {
                return false;
            }

            occupants[first] = secondUnitId;
            occupants[second] = firstUnitId;
            unitPositions[firstUnitId] = second;
            unitPositions[secondUnitId] = first;
            Publish(GridMutationKind.Swapped, firstUnitId, first, second);
            Publish(GridMutationKind.Swapped, secondUnitId, second, first);
            return true;
        }

        public bool TryRemoveAt(GridPosition position)
        {
            if (!occupants.TryGetValue(position, out var unitId))
            {
                return false;
            }

            occupants.Remove(position);
            unitPositions.Remove(unitId);
            Publish(GridMutationKind.Removed, unitId, position, position);
            return true;
        }

        public bool TryUnlock(GridPosition position, CellType unlockedType = CellType.Battle)
        {
            if (unlockedType == CellType.Locked ||
                !cells.TryGetValue(position, out var currentType) ||
                currentType != CellType.Locked)
            {
                return false;
            }

            cells[position] = unlockedType;
            Publish(GridMutationKind.CellUnlocked, string.Empty, null, position);
            return true;
        }

        public bool TryDebugUnlockCell(GridPosition position, CellType unlockedType = CellType.Battle)
        {
            if (Layout != null &&
                (!Layout.IsUnlockable(position, Side) ||
                 (Layout.RequiresOrthogonalUnlockAdjacency && !HasOrthogonallyAdjacentBattleCell(position))))
            {
                return false;
            }

            return TryUnlock(position, unlockedType);
        }

        public bool TryDebugUnlockCell(int x, int y, CellType unlockedType = CellType.Battle)
        {
            return TryDebugUnlockCell(new GridPosition(x, y), unlockedType);
        }

        public bool IsOccupied(GridPosition position)
        {
            return occupants.ContainsKey(position);
        }

        public bool IsPlaceable(GridPosition position)
        {
            return cells.TryGetValue(position, out var cellType) && cellType != CellType.Locked;
        }

        public CombatPoint GetCombatPosition(GridPosition position)
        {
            if (Layout != null)
            {
                return Layout.GetCombatPosition(position, Side);
            }

            if (!cells.ContainsKey(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), "The position is not part of this board.");
            }

            return TargetingSystem.FromBoardPosition(position);
        }

        public float GetLaneDistance(GridPosition position)
        {
            if (Layout == null)
            {
                throw new InvalidOperationException("Lane distance requires a battlefield layout definition.");
            }

            return Layout.GetLaneDistance(position, Side);
        }

        public BattlefieldRangeBand GetRangeBand(GridPosition position)
        {
            if (Layout == null)
            {
                throw new InvalidOperationException("Range bands require a battlefield layout definition.");
            }

            return Layout.GetRangeBand(position, Side);
        }

        public void ReportDropRejectedBecauseNoFreeBattleCell(GridPosition source, GridPosition target)
        {
            if (!TryGetCellType(source, out var sourceType) ||
                !TryGetCellType(target, out var targetType) ||
                sourceType != CellType.Bench ||
                targetType != CellType.Battle ||
                FreeBattleCellCount != 0)
            {
                return;
            }

            DropRejectedBecauseNoSpace?.Invoke(new GridDropRejectedBecauseNoSpace(source, target));
        }

        public static bool AreOrthogonallyAdjacent(GridPosition first, GridPosition second)
        {
            return Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) == 1;
        }

        private void Publish(GridMutationKind kind, string unitId, GridPosition? from, GridPosition to)
        {
            mutationSequence++;
            Changed?.Invoke(new GridMutation(mutationSequence, kind, unitId, from, to));
        }

        private bool HasOrthogonallyAdjacentBattleCell(GridPosition position)
        {
            foreach (var candidate in GetPositions(CellType.Battle))
            {
                if (AreOrthogonallyAdjacent(candidate, position))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<KeyValuePair<GridPosition, CellType>> CreateCells(
            BattlefieldLayoutDefinition layout,
            TeamSide side,
            bool includeBench)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            return layout.CreateBoardCells(side, includeBench);
        }
    }
}
