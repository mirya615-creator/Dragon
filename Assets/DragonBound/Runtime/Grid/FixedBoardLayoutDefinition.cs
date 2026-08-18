using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;

namespace DragonBound.Grid
{
    public enum FixedBoardCellRole
    {
        Deployment,
        Lane,
        Spawn,
        Goal,
        PermanentTerrain,
        Separator
    }

    public enum FixedBoardCellOwner
    {
        None,
        Player,
        AI
    }

    public enum FixedBoardDeployState
    {
        NotApplicable,
        Unlocked,
        LockedUnlockable
    }

    public enum FixedBoardRoadTileType
    {
        None,
        Spawn,
        Goal,
        StraightHorizontal,
        StraightVertical,
        CornerLeftUp,
        CornerLeftDown,
        CornerRightUp,
        CornerRightDown
    }

    public readonly struct FixedBoardCellDefinition
    {
        public FixedBoardCellDefinition(
            GridPosition coordinate,
            FixedBoardCellRole role,
            FixedBoardCellOwner owner,
            FixedBoardDeployState deployState,
            string artSlotId)
        {
            if (string.IsNullOrWhiteSpace(artSlotId) ||
                !artSlotId.StartsWith("ART_", StringComparison.Ordinal))
            {
                throw new ArgumentException("An ART_* slot id is required.", nameof(artSlotId));
            }

            if (role == FixedBoardCellRole.Deployment)
            {
                if (owner == FixedBoardCellOwner.None || deployState == FixedBoardDeployState.NotApplicable)
                {
                    throw new ArgumentException("Deployment cells require an owner and deployment state.");
                }
            }
            else if (deployState != FixedBoardDeployState.NotApplicable)
            {
                throw new ArgumentException("Only deployment cells may have a deployment state.", nameof(deployState));
            }

            Coordinate = coordinate;
            Role = role;
            Owner = owner;
            DeployState = deployState;
            ArtSlotId = artSlotId;
        }

        public GridPosition Coordinate { get; }
        public FixedBoardCellRole Role { get; }
        public FixedBoardCellOwner Owner { get; }
        public FixedBoardDeployState DeployState { get; }
        public string ArtSlotId { get; }
    }

    /// <summary>
    /// A formal board definition. Every formal map owns the same 8 by 10 coordinate space;
    /// only its deployment-state mask, lanes, and semantic ART_* slots may vary.
    /// </summary>
    public sealed class FixedBoardLayoutDefinition : BattlefieldLayoutDefinition
    {
        public const int FixedColumns = 8;
        public const int FixedRows = 10;
        public const float LogicalCellSize = 1f;

        private readonly FixedBoardCellDefinition[] cellDefinitions;
        private readonly Dictionary<GridPosition, FixedBoardCellDefinition> cellsByCoordinate;
        private readonly Dictionary<TeamSide, GridPosition[]> initialUnlockedCells;
        private readonly Dictionary<TeamSide, GridPosition[]> unlockableCells;
        private readonly Dictionary<TeamSide, GridPosition[]> potentialDeploymentCells;
        private readonly Dictionary<TeamSide, GridPosition[]> laneWaypoints;
        private readonly bool requiresConnectedDeploymentMask;

        public FixedBoardLayoutDefinition(
            string layoutId,
            string displayName,
            IReadOnlyList<FixedBoardCellDefinition> cells,
            IReadOnlyList<GridPosition> playerLaneWaypoints,
            IReadOnlyList<GridPosition> aiLaneWaypoints,
            string themeId,
            bool isDevelopmentLayout,
            bool requiresConnectedDeploymentMask = true)
            : base(
                layoutId,
                FixedColumns,
                FixedRows,
                CreateDeploymentCells(cells),
                CreateBenchSlots(),
                CreateLanes(playerLaneWaypoints, aiLaneWaypoints),
                1,
                1,
                GetInitialUnlocked(cells, FixedBoardCellOwner.Player))
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A display name is required.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(themeId))
            {
                throw new ArgumentException("A theme id is required.", nameof(themeId));
            }

            if (cells == null || cells.Count != FixedColumns * FixedRows)
            {
                throw new ArgumentException("A fixed layout must define all 80 board cells.", nameof(cells));
            }

            DisplayName = displayName;
            ThemeId = themeId;
            IsDevelopmentLayout = isDevelopmentLayout;
            this.requiresConnectedDeploymentMask = requiresConnectedDeploymentMask;
            CellSize = LogicalCellSize;
            cellDefinitions = new FixedBoardCellDefinition[cells.Count];
            cellsByCoordinate = new Dictionary<GridPosition, FixedBoardCellDefinition>();
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                if (cell.Coordinate.X < 0 || cell.Coordinate.X >= FixedColumns ||
                    cell.Coordinate.Y < 0 || cell.Coordinate.Y >= FixedRows ||
                    cellsByCoordinate.ContainsKey(cell.Coordinate))
                {
                    throw new ArgumentException("Fixed board cells must be unique and inside the 8 by 10 board.", nameof(cells));
                }

                cellDefinitions[index] = cell;
                cellsByCoordinate.Add(cell.Coordinate, cell);
            }

            initialUnlockedCells = new Dictionary<TeamSide, GridPosition[]>
            {
                { TeamSide.Player, GetInitialUnlocked(cells, FixedBoardCellOwner.Player) },
                { TeamSide.AI, GetInitialUnlocked(cells, FixedBoardCellOwner.AI) }
            };
            unlockableCells = new Dictionary<TeamSide, GridPosition[]>
            {
                { TeamSide.Player, GetUnlockable(cells, FixedBoardCellOwner.Player) },
                { TeamSide.AI, GetUnlockable(cells, FixedBoardCellOwner.AI) }
            };
            potentialDeploymentCells = new Dictionary<TeamSide, GridPosition[]>
            {
                { TeamSide.Player, GetDeploymentCells(cells, FixedBoardCellOwner.Player) },
                { TeamSide.AI, GetDeploymentCells(cells, FixedBoardCellOwner.AI) }
            };
            laneWaypoints = new Dictionary<TeamSide, GridPosition[]>
            {
                { TeamSide.Player, CopyWaypoints(playerLaneWaypoints, nameof(playerLaneWaypoints)) },
                { TeamSide.AI, CopyWaypoints(aiLaneWaypoints, nameof(aiLaneWaypoints)) }
            };

            ValidateDeploymentMasks();
            ValidateLaneMasks();
        }

        public string DisplayName { get; }
        public int Rows => FixedRows;
        public int Columns => FixedColumns;
        public float CellSize { get; }
        public IReadOnlyList<FixedBoardCellDefinition> CellDefinitions => cellDefinitions;
        public IReadOnlyList<GridPosition> PlayerInitialUnlockedCells => initialUnlockedCells[TeamSide.Player];
        public IReadOnlyList<GridPosition> PlayerUnlockableCells => unlockableCells[TeamSide.Player];
        public IReadOnlyList<GridPosition> AiInitialUnlockedCells => initialUnlockedCells[TeamSide.AI];
        public IReadOnlyList<GridPosition> AiUnlockableCells => unlockableCells[TeamSide.AI];
        public IReadOnlyList<GridPosition> PlayerLaneWaypoints => laneWaypoints[TeamSide.Player];
        public IReadOnlyList<GridPosition> AiLaneWaypoints => laneWaypoints[TeamSide.AI];
        public GridPosition PlayerSpawnPoint => PlayerLaneWaypoints[0];
        public GridPosition PlayerGoalPoint => PlayerLaneWaypoints[PlayerLaneWaypoints.Count - 1];
        public GridPosition AiSpawnPoint => AiLaneWaypoints[0];
        public GridPosition AiGoalPoint => AiLaneWaypoints[AiLaneWaypoints.Count - 1];
        public string ThemeId { get; }
        public bool IsDevelopmentLayout { get; }
        public override bool RequiresOrthogonalUnlockAdjacency => requiresConnectedDeploymentMask;
        public override int FormationCellCount => potentialDeploymentCells[TeamSide.Player].Length;
        public override int InitialUnlockedCellCount => initialUnlockedCells[TeamSide.Player].Length;

        /// <summary>
        /// Converts the authored map convention (row zero at the top) into the runtime
        /// coordinate convention used by Unity anchors (y zero at the bottom).
        /// </summary>
        public static GridPosition FromConfigCoordinate(int configRow, int configColumn)
        {
            return LayoutCoordinateConverter.ToRuntime(configRow, configColumn);
        }

        public static int ToConfigRow(GridPosition position)
        {
            return LayoutCoordinateConverter.ToConfigRow(position);
        }

        public bool TryGetRoadTileType(GridPosition position, out FixedBoardRoadTileType tileType)
        {
            foreach (var side in new[] { TeamSide.Player, TeamSide.AI })
            {
                var path = laneWaypoints[side];
                for (var index = 0; index < path.Length; index++)
                {
                    if (path[index] != position)
                    {
                        continue;
                    }

                    if (index == 0)
                    {
                        tileType = FixedBoardRoadTileType.Spawn;
                        return true;
                    }

                    if (index == path.Length - 1)
                    {
                        tileType = FixedBoardRoadTileType.Goal;
                        return true;
                    }

                    tileType = GetIntermediateRoadTileType(path[index - 1], position, path[index + 1]);
                    return true;
                }
            }

            tileType = FixedBoardRoadTileType.None;
            return false;
        }

        public IReadOnlyList<GridPosition> GetPotentialDeploymentCells(TeamSide side)
        {
            return potentialDeploymentCells[side];
        }

        public IReadOnlyList<GridPosition> GetInitialUnlockedCells(TeamSide side)
        {
            return initialUnlockedCells[side];
        }

        public IReadOnlyList<GridPosition> GetUnlockableCells(TeamSide side)
        {
            return unlockableCells[side];
        }

        public bool TryGetCellDefinition(GridPosition position, out FixedBoardCellDefinition definition)
        {
            return cellsByCoordinate.TryGetValue(position, out definition);
        }

        public bool IsOwnedDeploymentCell(GridPosition position, TeamSide side)
        {
            return cellsByCoordinate.TryGetValue(position, out var cell) &&
                cell.Role == FixedBoardCellRole.Deployment &&
                cell.Owner == ToOwner(side);
        }

        public override BattlefieldSideTransform GetTransform(TeamSide side)
        {
            return new BattlefieldSideTransform(
                side,
                Columns,
                Rows,
                Rows - 1,
                GetLane(side));
        }

        public override List<KeyValuePair<GridPosition, CellType>> CreateBoardCells(
            TeamSide side,
            bool includeBench)
        {
            var result = new List<KeyValuePair<GridPosition, CellType>>(
                potentialDeploymentCells[side].Length + (includeBench ? BenchCapacity : 0));
            foreach (var position in potentialDeploymentCells[side])
            {
                var definition = cellsByCoordinate[position];
                result.Add(new KeyValuePair<GridPosition, CellType>(
                    position,
                    definition.DeployState == FixedBoardDeployState.Unlocked
                        ? CellType.Battle
                        : CellType.Locked));
            }

            if (includeBench)
            {
                foreach (var position in BenchPositions)
                {
                    result.Add(new KeyValuePair<GridPosition, CellType>(position, CellType.Bench));
                }
            }

            return result;
        }

        public override bool IsUnlockable(GridPosition position, TeamSide side)
        {
            return cellsByCoordinate.TryGetValue(position, out var cell) &&
                cell.Role == FixedBoardCellRole.Deployment &&
                cell.Owner == ToOwner(side) &&
                cell.DeployState == FixedBoardDeployState.LockedUnlockable;
        }

        public override bool IsUnlockable(GridPosition position)
        {
            return IsUnlockable(position, TeamSide.Player) || IsUnlockable(position, TeamSide.AI);
        }

        public override GridPosition GetFairCounterpart(GridPosition position, TeamSide side)
        {
            if (!IsOwnedDeploymentCell(position, side))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return new GridPosition((Columns - 1) - position.X, (Rows - 1) - position.Y);
        }

        private void ValidateDeploymentMasks()
        {
            foreach (var side in new[] { TeamSide.Player, TeamSide.AI })
            {
                var potential = potentialDeploymentCells[side];
                var unlocked = initialUnlockedCells[side];
                var unlockable = unlockableCells[side];
                if (potential.Length != unlocked.Length + unlockable.Length ||
                    (requiresConnectedDeploymentMask && !AreOrthogonallyConnected(potential)) ||
                    !AreOrthogonallyConnected(unlocked))
                {
                    throw new ArgumentException("Deployment masks must be connected and partition unlocked and unlockable cells.");
                }

                foreach (var position in potential)
                {
                    var counterpart = new GridPosition((Columns - 1) - position.X, (Rows - 1) - position.Y);
                    if (!IsOwnedDeploymentCell(counterpart, Opponent(side)))
                    {
                        throw new ArgumentException("Player and AI deployment masks must be rotationally equivalent.");
                    }
                }
            }
        }

        private void ValidateLaneMasks()
        {
            foreach (var side in new[] { TeamSide.Player, TeamSide.AI })
            {
                var waypoints = laneWaypoints[side];
                if (waypoints.Length < 2)
                {
                    throw new ArgumentException("A lane needs spawn and goal waypoints.");
                }

                var lanePositions = new HashSet<GridPosition>();
                for (var index = 0; index < waypoints.Length; index++)
                {
                    var waypoint = waypoints[index];
                    if (!cellsByCoordinate.TryGetValue(waypoint, out var cell) ||
                        cell.Role == FixedBoardCellRole.Deployment ||
                        cell.Owner != ToOwner(side))
                    {
                        throw new ArgumentException("Lane waypoints cannot overlap deployment cells.");
                    }

                    var expectedRole = index == 0
                        ? FixedBoardCellRole.Spawn
                        : index == waypoints.Length - 1
                            ? FixedBoardCellRole.Goal
                            : FixedBoardCellRole.Lane;
                    if (cell.Role != expectedRole || !lanePositions.Add(waypoint))
                    {
                        throw new ArgumentException("Lane waypoint roles and order must match the configured map cells.");
                    }

                    if (index > 0 && !BoardGrid.AreOrthogonallyAdjacent(waypoints[index - 1], waypoint))
                    {
                        throw new ArgumentException("Lane waypoints must be orthogonally adjacent.");
                    }
                }

                foreach (var cell in cellDefinitions)
                {
                    if (cell.Owner == ToOwner(side) &&
                        (cell.Role == FixedBoardCellRole.Lane ||
                         cell.Role == FixedBoardCellRole.Spawn ||
                         cell.Role == FixedBoardCellRole.Goal) &&
                        !lanePositions.Contains(cell.Coordinate))
                    {
                        throw new ArgumentException("Every configured lane, spawn, and goal cell must belong to its explicit lane path.");
                    }
                }
            }

            if (GetLaneLength(PlayerLaneWaypoints) != GetLaneLength(AiLaneWaypoints))
            {
                throw new ArgumentException("Player and AI lanes must have equal total length.");
            }
        }

        private static float GetLaneLength(IReadOnlyList<GridPosition> waypoints)
        {
            var total = 0f;
            for (var index = 0; index < waypoints.Count - 1; index++)
            {
                var deltaX = waypoints[index + 1].X - waypoints[index].X;
                var deltaY = waypoints[index + 1].Y - waypoints[index].Y;
                total += (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            }

            return total;
        }

        private static FixedBoardRoadTileType GetIntermediateRoadTileType(
            GridPosition previous,
            GridPosition current,
            GridPosition next)
        {
            var hasLeft = previous.X < current.X || next.X < current.X;
            var hasRight = previous.X > current.X || next.X > current.X;
            var hasUp = previous.Y > current.Y || next.Y > current.Y;
            var hasDown = previous.Y < current.Y || next.Y < current.Y;

            if (hasLeft && hasRight)
            {
                return FixedBoardRoadTileType.StraightHorizontal;
            }

            if (hasUp && hasDown)
            {
                return FixedBoardRoadTileType.StraightVertical;
            }

            if (hasLeft && hasUp)
            {
                return FixedBoardRoadTileType.CornerLeftUp;
            }

            if (hasLeft && hasDown)
            {
                return FixedBoardRoadTileType.CornerLeftDown;
            }

            if (hasRight && hasUp)
            {
                return FixedBoardRoadTileType.CornerRightUp;
            }

            if (hasRight && hasDown)
            {
                return FixedBoardRoadTileType.CornerRightDown;
            }

            throw new ArgumentException("An intermediate road tile must connect two orthogonal path directions.");
        }

        private static bool AreOrthogonallyConnected(IReadOnlyList<GridPosition> positions)
        {
            if (positions.Count == 0)
            {
                return false;
            }

            var remaining = new HashSet<GridPosition>(positions);
            var queue = new Queue<GridPosition>();
            queue.Enqueue(positions[0]);
            remaining.Remove(positions[0]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var candidate in positions)
                {
                    if (remaining.Contains(candidate) && BoardGrid.AreOrthogonallyAdjacent(current, candidate))
                    {
                        remaining.Remove(candidate);
                        queue.Enqueue(candidate);
                    }
                }
            }

            return remaining.Count == 0;
        }

        private static FixedBoardCellOwner ToOwner(TeamSide side)
        {
            return side == TeamSide.Player ? FixedBoardCellOwner.Player : FixedBoardCellOwner.AI;
        }

        private static TeamSide Opponent(TeamSide side)
        {
            return side == TeamSide.Player ? TeamSide.AI : TeamSide.Player;
        }

        private static BattlefieldCellDefinition[] CreateDeploymentCells(IReadOnlyList<FixedBoardCellDefinition> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            var result = new List<BattlefieldCellDefinition>();
            foreach (var cell in cells)
            {
                if (cell.Role == FixedBoardCellRole.Deployment)
                {
                    result.Add(new BattlefieldCellDefinition(cell.Coordinate, CellType.Locked));
                }
            }

            return result.ToArray();
        }

        private static List<GridPosition> CreateBenchSlots()
        {
            return new List<GridPosition>
            {
                // Bench slots are deliberately outside the 8 x 10 map coordinate space.
                new GridPosition(0, -1),
                new GridPosition(1, -1),
                new GridPosition(2, -1),
                new GridPosition(3, -1),
                new GridPosition(4, -1)
            };
        }

        private static List<BattlefieldLaneDefinition> CreateLanes(
            IReadOnlyList<GridPosition> playerWaypoints,
            IReadOnlyList<GridPosition> aiWaypoints)
        {
            return new List<BattlefieldLaneDefinition>
            {
                CreateLane(TeamSide.Player, BattlefieldLaneSide.Left, playerWaypoints, FixedRows - 1),
                CreateLane(TeamSide.AI, BattlefieldLaneSide.Right, aiWaypoints, FixedRows - 1)
            };
        }

        private static BattlefieldLaneDefinition CreateLane(
            TeamSide side,
            BattlefieldLaneSide laneSide,
            IReadOnlyList<GridPosition> waypoints,
            int maximumDeploymentY)
        {
            var copied = CopyWaypoints(waypoints, nameof(waypoints));
            var names = new string[copied.Length];
            var points = new CombatPoint[copied.Length];
            for (var index = 0; index < copied.Length; index++)
            {
                names[index] = index == 0
                    ? "Spawn"
                    : index == copied.Length - 1 ? "DragonGoal" : $"PathPoint_{index}";
                points[index] = new CombatPoint(copied[index].X, maximumDeploymentY - copied[index].Y);
            }

            return new BattlefieldLaneDefinition(side, laneSide, names, points);
        }

        private static GridPosition[] GetInitialUnlocked(
            IReadOnlyList<FixedBoardCellDefinition> cells,
            FixedBoardCellOwner owner)
        {
            return GetCells(cells, owner, FixedBoardDeployState.Unlocked);
        }

        private static GridPosition[] GetUnlockable(
            IReadOnlyList<FixedBoardCellDefinition> cells,
            FixedBoardCellOwner owner)
        {
            return GetCells(cells, owner, FixedBoardDeployState.LockedUnlockable);
        }

        private static GridPosition[] GetDeploymentCells(
            IReadOnlyList<FixedBoardCellDefinition> cells,
            FixedBoardCellOwner owner)
        {
            var positions = new List<GridPosition>();
            foreach (var cell in cells)
            {
                if (cell.Role == FixedBoardCellRole.Deployment && cell.Owner == owner)
                {
                    positions.Add(cell.Coordinate);
                }
            }

            positions.Sort();
            return positions.ToArray();
        }

        private static GridPosition[] GetCells(
            IReadOnlyList<FixedBoardCellDefinition> cells,
            FixedBoardCellOwner owner,
            FixedBoardDeployState state)
        {
            var positions = new List<GridPosition>();
            foreach (var cell in cells)
            {
                if (cell.Role == FixedBoardCellRole.Deployment &&
                    cell.Owner == owner &&
                    cell.DeployState == state)
                {
                    positions.Add(cell.Coordinate);
                }
            }

            positions.Sort();
            return positions.ToArray();
        }

        private static GridPosition[] CopyWaypoints(IReadOnlyList<GridPosition> values, string parameterName)
        {
            if (values == null || values.Count < 2)
            {
                throw new ArgumentException("A lane requires at least spawn and goal points.", parameterName);
            }

            var copied = new GridPosition[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copied[index] = values[index];
            }

            return copied;
        }
    }
}
