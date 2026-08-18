using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;

namespace DragonBound.Grid
{
    public enum BattlefieldLaneSide
    {
        Left,
        Right
    }

    public enum BattlefieldRangeBand
    {
        Near,
        Middle,
        Far
    }

    public readonly struct BattlefieldCellDefinition
    {
        public BattlefieldCellDefinition(GridPosition position, CellType cellType)
        {
            if (cellType == CellType.Bench)
            {
                throw new ArgumentException("Formation cells cannot use the bench cell type.", nameof(cellType));
            }

            Position = position;
            CellType = cellType;
        }

        public GridPosition Position { get; }
        public CellType CellType { get; }
    }

    public sealed class BattlefieldLaneDefinition
    {
        private readonly string[] nodeNames;
        private readonly CombatPoint[] combatPoints;

        public BattlefieldLaneDefinition(
            TeamSide side,
            BattlefieldLaneSide laneSide,
            IReadOnlyList<string> pathNodeNames,
            IReadOnlyList<CombatPoint> pathCombatPoints)
        {
            if (pathNodeNames == null || pathCombatPoints == null ||
                pathNodeNames.Count < 2 || pathNodeNames.Count != pathCombatPoints.Count)
            {
                throw new ArgumentException("A lane requires matching spawn and goal nodes.");
            }

            Side = side;
            LaneSide = laneSide;
            nodeNames = new string[pathNodeNames.Count];
            combatPoints = new CombatPoint[pathCombatPoints.Count];
            for (var index = 0; index < pathNodeNames.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(pathNodeNames[index]))
                {
                    throw new ArgumentException("Lane path node names cannot be empty.", nameof(pathNodeNames));
                }

                nodeNames[index] = pathNodeNames[index];
                combatPoints[index] = pathCombatPoints[index];
            }

            if (!string.Equals(nodeNames[nodeNames.Length - 1], "DragonGoal", StringComparison.Ordinal))
            {
                throw new ArgumentException("The final lane node must be DragonGoal.", nameof(pathNodeNames));
            }
        }

        public TeamSide Side { get; }
        public BattlefieldLaneSide LaneSide { get; }
        public IReadOnlyList<string> NodeNames => nodeNames;
        public IReadOnlyList<CombatPoint> CombatPoints => combatPoints;

        public float GetDistanceTo(CombatPoint point)
        {
            var closestDistanceSquared = float.MaxValue;
            for (var index = 0; index < combatPoints.Length - 1; index++)
            {
                closestDistanceSquared = Math.Min(
                    closestDistanceSquared,
                    DistanceSquaredToSegment(point, combatPoints[index], combatPoints[index + 1]));
            }

            return (float)Math.Sqrt(closestDistanceSquared);
        }

        internal static float DistanceSquaredToSegment(CombatPoint point, CombatPoint start, CombatPoint end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var lengthSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (lengthSquared <= 0.0001f)
            {
                return point.DistanceSquared(start);
            }

            var projection = ((point.X - start.X) * deltaX) + ((point.Y - start.Y) * deltaY);
            var progress = Math.Max(0f, Math.Min(1f, projection / lengthSquared));
            var closest = new CombatPoint(start.X + (deltaX * progress), start.Y + (deltaY * progress));
            return point.DistanceSquared(closest);
        }
    }

    public readonly struct BattlefieldSideTransform
    {
        private readonly int maximumFormationY;
        private readonly BattlefieldLaneDefinition lane;

        internal BattlefieldSideTransform(
            TeamSide side,
            int width,
            int height,
            int maximumFormationY,
            BattlefieldLaneDefinition lane)
        {
            Side = side;
            Width = width;
            Height = height;
            this.maximumFormationY = maximumFormationY;
            this.lane = lane;
        }

        public TeamSide Side { get; }
        public int Width { get; }
        public int Height { get; }
        public BattlefieldLaneSide LaneSide => lane.LaneSide;
        public bool IsHorizontallyMirrored => lane.LaneSide == BattlefieldLaneSide.Right;

        public CombatPoint ToCombatPoint(GridPosition position)
        {
            return new CombatPoint(position.X, maximumFormationY - position.Y);
        }

        public float GetLaneDistance(GridPosition position)
        {
            return lane.GetDistanceTo(ToCombatPoint(position));
        }
    }

    public class BattlefieldLayoutDefinition
    {
        private const float RangeEpsilon = 0.0001f;
        private const float CellHalfExtent = 0.5f;

        private readonly BattlefieldCellDefinition[] formationCells;
        private readonly GridPosition[] benchPositions;
        private readonly GridPosition[] playerInitialUnlockedCells;
        private readonly Dictionary<GridPosition, CellType> formationCellsByPosition;
        private readonly Dictionary<TeamSide, BattlefieldLaneDefinition> lanes;
        private readonly Dictionary<TeamSide, List<float>> rangeColumnDistances;
        private readonly int maximumFormationY;
        private readonly int nearColumnCount;
        private readonly int middleColumnCount;

        public BattlefieldLayoutDefinition(
            string layoutId,
            int width,
            int height,
            IReadOnlyList<BattlefieldCellDefinition> cells,
            IReadOnlyList<GridPosition> benchSlots,
            IReadOnlyList<BattlefieldLaneDefinition> laneDefinitions,
            int nearColumnCount = 1,
            int middleColumnCount = 1,
            IReadOnlyList<GridPosition> initialUnlockedCells = null)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                throw new ArgumentException("A layout id is required.", nameof(layoutId));
            }

            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Layout dimensions must be positive.");
            }

            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A layout requires formation cells.", nameof(cells));
            }

            if (benchSlots == null)
            {
                throw new ArgumentNullException(nameof(benchSlots));
            }

            if (laneDefinitions == null || laneDefinitions.Count != 2)
            {
                throw new ArgumentException("A layout requires one player lane and one AI lane.", nameof(laneDefinitions));
            }

            if (nearColumnCount < 1 || middleColumnCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(nearColumnCount), "Range bands require near and middle columns.");
            }

            LayoutId = layoutId;
            Width = width;
            Height = height;
            this.nearColumnCount = nearColumnCount;
            this.middleColumnCount = middleColumnCount;
            formationCells = new BattlefieldCellDefinition[cells.Count];
            formationCellsByPosition = new Dictionary<GridPosition, CellType>();
            maximumFormationY = int.MinValue;
            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                if (formationCellsByPosition.ContainsKey(cell.Position))
                {
                    throw new ArgumentException($"Duplicate formation cell {cell.Position}.", nameof(cells));
                }

                formationCells[index] = cell;
                formationCellsByPosition.Add(cell.Position, cell.CellType);
                maximumFormationY = Math.Max(maximumFormationY, cell.Position.Y);
            }

            benchPositions = new GridPosition[benchSlots.Count];
            for (var index = 0; index < benchSlots.Count; index++)
            {
                var position = benchSlots[index];
                if (formationCellsByPosition.ContainsKey(position))
                {
                    throw new ArgumentException($"Bench slot {position} overlaps a formation cell.", nameof(benchSlots));
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (benchPositions[previous] == position)
                    {
                        throw new ArgumentException($"Duplicate bench slot {position}.", nameof(benchSlots));
                    }
                }

                benchPositions[index] = position;
            }

            playerInitialUnlockedCells = initialUnlockedCells == null
                ? Array.Empty<GridPosition>()
                : CopyInitialUnlockedCells(initialUnlockedCells);

            lanes = new Dictionary<TeamSide, BattlefieldLaneDefinition>();
            foreach (var lane in laneDefinitions)
            {
                if (lane == null || lanes.ContainsKey(lane.Side))
                {
                    throw new ArgumentException("Each side must have exactly one lane.", nameof(laneDefinitions));
                }

                lanes.Add(lane.Side, lane);
            }

            if (!lanes.ContainsKey(TeamSide.Player) || !lanes.ContainsKey(TeamSide.AI))
            {
                throw new ArgumentException("Both player and AI lanes are required.", nameof(laneDefinitions));
            }

            ValidateLaneSeparation();
            rangeColumnDistances = BuildRangeColumnDistances();
        }

        public string LayoutId { get; }
        public int Width { get; }
        public int Height { get; }
        public virtual IReadOnlyList<BattlefieldCellDefinition> FormationCells => formationCells;
        public virtual IReadOnlyList<GridPosition> BenchPositions => benchPositions;
        public virtual IReadOnlyList<GridPosition> InitialUnlockedCells => playerInitialUnlockedCells;
        public virtual int InitialUnlockedCellCount => playerInitialUnlockedCells.Length;
        public virtual int FormationCellCount => formationCells.Length;
        public virtual int BenchCapacity => benchPositions.Length;
        public virtual bool RequiresOrthogonalUnlockAdjacency => true;

        public virtual BattlefieldSideTransform GetTransform(TeamSide side)
        {
            return new BattlefieldSideTransform(side, Width, Height, maximumFormationY, GetLane(side));
        }

        public BattlefieldLaneDefinition GetLane(TeamSide side)
        {
            if (!lanes.TryGetValue(side, out var lane))
            {
                throw new ArgumentOutOfRangeException(nameof(side));
            }

            return lane;
        }

        public bool TryGetCellType(GridPosition position, out CellType cellType)
        {
            if (formationCellsByPosition.TryGetValue(position, out cellType))
            {
                return true;
            }

            for (var index = 0; index < benchPositions.Length; index++)
            {
                if (benchPositions[index] == position)
                {
                    cellType = CellType.Bench;
                    return true;
                }
            }

            cellType = CellType.Locked;
            return false;
        }

        public CombatPoint GetCombatPosition(GridPosition position, TeamSide side = TeamSide.Player)
        {
            if (!TryGetCellType(position, out _))
            {
                throw new ArgumentOutOfRangeException(nameof(position), "The position is not part of this layout.");
            }

            return GetTransform(side).ToCombatPoint(position);
        }

        public float GetLaneDistance(GridPosition position, TeamSide side)
        {
            if (!formationCellsByPosition.ContainsKey(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Lane distance is defined for formation cells only.");
            }

            return GetTransform(side).GetLaneDistance(position);
        }

        public BattlefieldRangeBand GetRangeBand(GridPosition position, TeamSide side)
        {
            var distance = GetLaneDistance(position, side);
            var columns = rangeColumnDistances[side];
            var distanceColumn = GetDistanceColumn(columns, distance);
            if (distanceColumn < nearColumnCount)
            {
                return BattlefieldRangeBand.Near;
            }

            return distanceColumn < nearColumnCount + middleColumnCount
                ? BattlefieldRangeBand.Middle
                : BattlefieldRangeBand.Far;
        }

        public virtual List<KeyValuePair<GridPosition, CellType>> CreateBoardCells(
            TeamSide side,
            bool includeBench)
        {
            var result = new List<KeyValuePair<GridPosition, CellType>>(
                formationCells.Length + (includeBench ? benchPositions.Length : 0));
            for (var index = 0; index < formationCells.Length; index++)
            {
                var cell = formationCells[index];
                result.Add(new KeyValuePair<GridPosition, CellType>(
                    cell.Position,
                    GetInitialCellType(cell, side)));
            }

            if (includeBench)
            {
                for (var index = 0; index < benchPositions.Length; index++)
                {
                    result.Add(new KeyValuePair<GridPosition, CellType>(benchPositions[index], CellType.Bench));
                }
            }

            return result;
        }

        public virtual bool IsUnlockable(GridPosition position)
        {
            return formationCellsByPosition.TryGetValue(position, out var cellType) &&
                cellType == CellType.Locked;
        }

        public virtual bool IsUnlockable(GridPosition position, TeamSide side)
        {
            return IsUnlockable(position);
        }

        public virtual GridPosition GetFairCounterpart(GridPosition position, TeamSide side)
        {
            if (!formationCellsByPosition.ContainsKey(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return side == TeamSide.Player
                ? new GridPosition((Width - 1) - position.X, position.Y)
                : new GridPosition((Width - 1) - position.X, position.Y);
        }

        private CellType GetInitialCellType(BattlefieldCellDefinition cell, TeamSide side)
        {
            if (playerInitialUnlockedCells.Length == 0)
            {
                return cell.CellType;
            }

            var expected = side == TeamSide.Player
                ? cell.Position
                : GetFairCounterpart(cell.Position, TeamSide.Player);
            for (var index = 0; index < playerInitialUnlockedCells.Length; index++)
            {
                if (playerInitialUnlockedCells[index] == expected)
                {
                    return CellType.Battle;
                }
            }

            return CellType.Locked;
        }

        private GridPosition[] CopyInitialUnlockedCells(IReadOnlyList<GridPosition> values)
        {
            var copied = new GridPosition[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                var position = values[index];
                if (!formationCellsByPosition.ContainsKey(position))
                {
                    throw new ArgumentException(
                        $"Initial unlocked cell {position} is not a formation cell.",
                        nameof(values));
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (copied[previous] == position)
                    {
                        throw new ArgumentException($"Duplicate initial unlocked cell {position}.", nameof(values));
                    }
                }

                copied[index] = position;
            }

            return copied;
        }

        private Dictionary<TeamSide, List<float>> BuildRangeColumnDistances()
        {
            var values = new Dictionary<TeamSide, List<float>>();
            foreach (var side in new[] { TeamSide.Player, TeamSide.AI })
            {
                var distances = new List<float>();
                for (var index = 0; index < formationCells.Length; index++)
                {
                    var distance = GetLaneDistance(formationCells[index].Position, side);
                    var isKnown = false;
                    for (var knownIndex = 0; knownIndex < distances.Count; knownIndex++)
                    {
                        if (Math.Abs(distances[knownIndex] - distance) <= RangeEpsilon)
                        {
                            isKnown = true;
                            break;
                        }
                    }

                    if (!isKnown)
                    {
                        distances.Add(distance);
                    }
                }

                distances.Sort();
                if (nearColumnCount + middleColumnCount > distances.Count)
                {
                    throw new ArgumentException(
                        "Range band column counts exceed the available lane-distance columns.",
                        nameof(nearColumnCount));
                }

                values.Add(side, distances);
            }

            return values;
        }

        private static int GetDistanceColumn(IReadOnlyList<float> columns, float distance)
        {
            for (var index = 0; index < columns.Count; index++)
            {
                if (Math.Abs(columns[index] - distance) <= RangeEpsilon)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("A formation cell has no lane distance column.");
        }

        private void ValidateLaneSeparation()
        {
            foreach (var lane in lanes.Values)
            {
                for (var cellIndex = 0; cellIndex < formationCells.Length; cellIndex++)
                {
                    var cellPoint = GetCombatPosition(formationCells[cellIndex].Position, lane.Side);
                    for (var segment = 0; segment < lane.CombatPoints.Count - 1; segment++)
                    {
                        var distanceSquared = BattlefieldLaneDefinition.DistanceSquaredToSegment(
                            cellPoint,
                            lane.CombatPoints[segment],
                            lane.CombatPoints[segment + 1]);
                        // A route may touch a cell boundary, but it cannot enter the cell interior.
                        if (distanceSquared < (CellHalfExtent * CellHalfExtent) - RangeEpsilon)
                        {
                            throw new ArgumentException(
                                $"Lane for {lane.Side} overlaps formation cell {formationCells[cellIndex].Position}.",
                                nameof(lanes));
                        }
                    }
                }
            }
        }

    }

    public static class BattlefieldLayoutDefinitions
    {
        public const string Fixed8x10ReferenceMap01Id = "Fixed8x10_ReferenceMap01";
        // Retained as a source-compatible symbol while the default configuration moves to Map01.
        public const string Fixed8x10ZhaoYunReferenceId = Fixed8x10ReferenceMap01Id;
        public const string Fixed8x10HorizontalStartId = "Fixed8x10_HorizontalStart";
        public const string Fixed8x10VerticalStartId = "Fixed8x10_VerticalStart";
        public const string Fixed8x10BalancedStartId = "Fixed8x10_BalancedStart";
        public const string Compact4x4Id = "Compact_4x4";
        public const string Spacious5x5Id = "Spacious_5x5";
        public const string Legacy3x3Id = "Legacy_3x3";

        private static readonly FixedBoardLayoutDefinition fixed8x10ReferenceMap01 =
            CreateReferenceMap01Layout();
        private static readonly FixedBoardLayoutDefinition fixed8x10HorizontalStart = CreateFixedLayout(
            Fixed8x10HorizontalStartId,
            "8x10 Horizontal Start",
            new[]
            {
                new GridPosition(0, 1), new GridPosition(1, 1), new GridPosition(2, 1),
                new GridPosition(0, 2), new GridPosition(1, 2), new GridPosition(2, 2)
            },
            "Greybox_Horizontal");
        private static readonly FixedBoardLayoutDefinition fixed8x10VerticalStart = CreateFixedLayout(
            Fixed8x10VerticalStartId,
            "8x10 Vertical Start",
            new[]
            {
                new GridPosition(0, 1), new GridPosition(1, 1),
                new GridPosition(0, 2), new GridPosition(1, 2),
                new GridPosition(0, 3), new GridPosition(1, 3)
            },
            "Greybox_Vertical");
        private static readonly FixedBoardLayoutDefinition fixed8x10BalancedStart = CreateFixedLayout(
            Fixed8x10BalancedStartId,
            "8x10 Balanced Start",
            new[]
            {
                new GridPosition(0, 1), new GridPosition(1, 1), new GridPosition(2, 1),
                new GridPosition(0, 2), new GridPosition(1, 2), new GridPosition(0, 3)
            },
            "Greybox_Balanced");
        private static readonly BattlefieldLayoutDefinition compact4x4 = CreateStandard(
            Compact4x4Id,
            4,
            4);
        private static readonly BattlefieldLayoutDefinition spacious5x5 = CreateStandard(
            Spacious5x5Id,
            5,
            5);
        private static readonly BattlefieldLayoutDefinition legacy3x3 = CreateLegacy();

        private static readonly FixedBoardLayoutDefinition[] formalLayouts =
        {
            fixed8x10ReferenceMap01,
            fixed8x10HorizontalStart,
            fixed8x10VerticalStart,
            fixed8x10BalancedStart
        };

        public static BattlefieldLayoutDefinition Default => fixed8x10ReferenceMap01;
        public static FixedBoardLayoutDefinition Fixed8x10ReferenceMap01 => fixed8x10ReferenceMap01;
        public static FixedBoardLayoutDefinition Fixed8x10ZhaoYunReference => fixed8x10ReferenceMap01;
        public static FixedBoardLayoutDefinition Fixed8x10HorizontalStart => fixed8x10HorizontalStart;
        public static FixedBoardLayoutDefinition Fixed8x10VerticalStart => fixed8x10VerticalStart;
        public static FixedBoardLayoutDefinition Fixed8x10BalancedStart => fixed8x10BalancedStart;
        public static IReadOnlyList<FixedBoardLayoutDefinition> FormalLayouts => formalLayouts;
        public static BattlefieldLayoutDefinition Compact4x4 => compact4x4;
        public static BattlefieldLayoutDefinition Spacious5x5 => spacious5x5;
        public static BattlefieldLayoutDefinition Legacy3x3 => legacy3x3;

        public static bool TryGet(string layoutId, out BattlefieldLayoutDefinition layout)
        {
            if (string.Equals(layoutId, Fixed8x10ReferenceMap01Id, StringComparison.Ordinal))
            {
                layout = fixed8x10ReferenceMap01;
                return true;
            }

            if (string.Equals(layoutId, Fixed8x10HorizontalStartId, StringComparison.Ordinal))
            {
                layout = fixed8x10HorizontalStart;
                return true;
            }

            if (string.Equals(layoutId, Fixed8x10VerticalStartId, StringComparison.Ordinal))
            {
                layout = fixed8x10VerticalStart;
                return true;
            }

            if (string.Equals(layoutId, Fixed8x10BalancedStartId, StringComparison.Ordinal))
            {
                layout = fixed8x10BalancedStart;
                return true;
            }

            if (string.Equals(layoutId, Compact4x4Id, StringComparison.Ordinal))
            {
                layout = compact4x4;
                return true;
            }

            if (string.Equals(layoutId, Spacious5x5Id, StringComparison.Ordinal))
            {
                layout = spacious5x5;
                return true;
            }

            if (string.Equals(layoutId, Legacy3x3Id, StringComparison.Ordinal))
            {
                layout = legacy3x3;
                return true;
            }

            layout = null;
            return false;
        }

        public static BattlefieldLayoutDefinition Get(string layoutId)
        {
            if (!TryGet(layoutId, out var layout))
            {
                throw new ArgumentException($"Unknown battlefield layout '{layoutId}'.", nameof(layoutId));
            }

            return layout;
        }

        public static bool TryGetFixed(string layoutId, out FixedBoardLayoutDefinition layout)
        {
            foreach (var candidate in formalLayouts)
            {
                if (string.Equals(candidate.LayoutId, layoutId, StringComparison.Ordinal))
                {
                    layout = candidate;
                    return true;
                }
            }

            layout = null;
            return false;
        }

        private static FixedBoardLayoutDefinition CreateReferenceMap01Layout()
        {
            var playerLane = CreateReferenceMap01PlayerLane();
            var aiLane = CreateReferenceMap01AiLane();
            return new FixedBoardLayoutDefinition(
                Fixed8x10ReferenceMap01Id,
                "固定8×10参考地图01",
                CreateReferenceMap01Cells(),
                playerLane,
                aiLane,
                "ReferenceMap01",
                true,
                false);
        }

        private static FixedBoardCellDefinition[] CreateReferenceMap01Cells()
        {
            var configRows = new[]
            {
                "GLLLLLLS",
                "RLLUUULR",
                "RLLUUULR",
                "RLLRRRRR",
                "RRRRLLLL",
                "LLLLRRRR",
                "RRRRRLLR",
                "RLUUULLR",
                "RLUUULLR",
                "SLLLLLLG"
            };
            var cells = new FixedBoardCellDefinition[FixedBoardLayoutDefinition.FixedColumns * FixedBoardLayoutDefinition.FixedRows];
            for (var configRow = 0; configRow < configRows.Length; configRow++)
            {
                var row = configRows[configRow];
                if (row.Length != FixedBoardLayoutDefinition.FixedColumns)
                {
                    throw new InvalidOperationException("ReferenceMap01 must define exactly eight roles in every config row.");
                }

                for (var column = 0; column < row.Length; column++)
                {
                    var owner = configRow < FixedBoardLayoutDefinition.FixedRows / 2
                        ? FixedBoardCellOwner.AI
                        : FixedBoardCellOwner.Player;
                    var coordinate = FixedBoardLayoutDefinition.FromConfigCoordinate(configRow, column);
                    cells[GetFixedCellIndex(coordinate.X, coordinate.Y)] =
                        CreateReferenceMap01Cell(coordinate, owner, row[column]);
                }
            }

            return cells;
        }

        private static FixedBoardCellDefinition CreateReferenceMap01Cell(
            GridPosition coordinate,
            FixedBoardCellOwner owner,
            char roleCode)
        {
            switch (roleCode)
            {
                case 'U':
                    return new FixedBoardCellDefinition(
                        coordinate,
                        FixedBoardCellRole.Deployment,
                        owner,
                        FixedBoardDeployState.Unlocked,
                        "ART_Cell_Unlocked");
                case 'L':
                    return new FixedBoardCellDefinition(
                        coordinate,
                        FixedBoardCellRole.Deployment,
                        owner,
                        FixedBoardDeployState.LockedUnlockable,
                        "ART_Cell_Locked");
                case 'R':
                    return new FixedBoardCellDefinition(
                        coordinate,
                        FixedBoardCellRole.Lane,
                        owner,
                        FixedBoardDeployState.NotApplicable,
                        "ART_LaneBase");
                case 'S':
                    return new FixedBoardCellDefinition(
                        coordinate,
                        FixedBoardCellRole.Spawn,
                        owner,
                        FixedBoardDeployState.NotApplicable,
                        owner == FixedBoardCellOwner.AI ? "ART_AiSpawnGate" : "ART_PlayerSpawnGate");
                case 'G':
                    return new FixedBoardCellDefinition(
                        coordinate,
                        FixedBoardCellRole.Goal,
                        owner,
                        FixedBoardDeployState.NotApplicable,
                        owner == FixedBoardCellOwner.AI ? "ART_AiGoal" : "ART_PlayerGoal");
                default:
                    throw new ArgumentOutOfRangeException(nameof(roleCode), roleCode, "ReferenceMap01 contains an undefined cell role.");
            }
        }

        private static GridPosition[] CreateReferenceMap01PlayerLane()
        {
            return CreateRuntimeWaypointList(new[]
            {
                new GridPosition(9, 0), new GridPosition(8, 0), new GridPosition(7, 0), new GridPosition(6, 0),
                new GridPosition(6, 1), new GridPosition(6, 2), new GridPosition(6, 3), new GridPosition(6, 4),
                new GridPosition(5, 4), new GridPosition(5, 5), new GridPosition(5, 6), new GridPosition(5, 7),
                new GridPosition(6, 7), new GridPosition(7, 7), new GridPosition(8, 7), new GridPosition(9, 7)
            });
        }

        private static GridPosition[] CreateReferenceMap01AiLane()
        {
            return CreateRuntimeWaypointList(new[]
            {
                new GridPosition(0, 7), new GridPosition(1, 7), new GridPosition(2, 7), new GridPosition(3, 7),
                new GridPosition(3, 6), new GridPosition(3, 5), new GridPosition(3, 4), new GridPosition(3, 3),
                new GridPosition(4, 3), new GridPosition(4, 2), new GridPosition(4, 1), new GridPosition(4, 0),
                new GridPosition(3, 0), new GridPosition(2, 0), new GridPosition(1, 0), new GridPosition(0, 0)
            });
        }

        private static GridPosition[] CreateRuntimeWaypointList(IReadOnlyList<GridPosition> configCoordinates)
        {
            var result = new GridPosition[configCoordinates.Count];
            for (var index = 0; index < configCoordinates.Count; index++)
            {
                result[index] = LayoutCoordinateConverter.ToRuntime(
                    configCoordinates[index].X,
                    configCoordinates[index].Y);
            }

            return result;
        }

        private static FixedBoardLayoutDefinition CreateFixedLayout(
            string layoutId,
            string displayName,
            IReadOnlyList<GridPosition> playerInitialUnlocked,
            string themeId)
        {
            var playerLane = new[]
            {
                new GridPosition(5, 4),
                new GridPosition(5, 3),
                new GridPosition(5, 2),
                new GridPosition(5, 1),
                new GridPosition(5, 0)
            };
            var aiLane = new[]
            {
                new GridPosition(2, 5),
                new GridPosition(2, 6),
                new GridPosition(2, 7),
                new GridPosition(2, 8),
                new GridPosition(2, 9)
            };
            return new FixedBoardLayoutDefinition(
                layoutId,
                displayName,
                CreateFixedCells(
                    playerInitialUnlocked,
                    playerLane,
                    aiLane,
                    CreateLegacyPlayerPotentialDeploymentCells()),
                playerLane,
                aiLane,
                themeId,
                true);
        }

        private static FixedBoardCellDefinition[] CreateFixedCells(
            IReadOnlyList<GridPosition> playerInitialUnlocked,
            IReadOnlyList<GridPosition> playerLane,
            IReadOnlyList<GridPosition> aiLane,
            IReadOnlyList<GridPosition> playerPotentialDeploymentCells)
        {
            var cells = new FixedBoardCellDefinition[FixedBoardLayoutDefinition.FixedColumns * FixedBoardLayoutDefinition.FixedRows];
            for (var y = 0; y < FixedBoardLayoutDefinition.FixedRows; y++)
            {
                for (var x = 0; x < FixedBoardLayoutDefinition.FixedColumns; x++)
                {
                    // The fixed board is a complete authored map surface. Non-gameplay cells
                    // are terrain owned by one half of the board, never anonymous environment.
                    var owner = y < FixedBoardLayoutDefinition.FixedRows / 2
                        ? FixedBoardCellOwner.Player
                        : FixedBoardCellOwner.AI;
                    cells[GetFixedCellIndex(x, y)] = new FixedBoardCellDefinition(
                        new GridPosition(x, y),
                        FixedBoardCellRole.PermanentTerrain,
                        owner,
                        FixedBoardDeployState.NotApplicable,
                        owner == FixedBoardCellOwner.Player
                            ? "ART_PlayerTerrain"
                            : "ART_AiTerrain");
                }
            }

            ApplyLaneCells(cells, playerLane, FixedBoardCellOwner.Player);
            ApplyLaneCells(cells, aiLane, FixedBoardCellOwner.AI);

            foreach (var position in playerPotentialDeploymentCells)
            {
                var unlocked = Contains(playerInitialUnlocked, position);
                SetFixedCell(
                    cells,
                    position,
                    FixedBoardCellRole.Deployment,
                    FixedBoardCellOwner.Player,
                    unlocked ? FixedBoardDeployState.Unlocked : FixedBoardDeployState.LockedUnlockable,
                    unlocked ? "ART_PlayerDeploymentUnlocked" : "ART_PlayerDeploymentLocked");

                var aiPosition = new GridPosition(
                    (FixedBoardLayoutDefinition.FixedColumns - 1) - position.X,
                    (FixedBoardLayoutDefinition.FixedRows - 1) - position.Y);
                SetFixedCell(
                    cells,
                    aiPosition,
                    FixedBoardCellRole.Deployment,
                    FixedBoardCellOwner.AI,
                    unlocked ? FixedBoardDeployState.Unlocked : FixedBoardDeployState.LockedUnlockable,
                    unlocked ? "ART_AiDeploymentUnlocked" : "ART_AiDeploymentLocked");
            }

            return cells;
        }

        private static GridPosition[] CreateZhaoYunReferencePlayerDeploymentCells()
        {
            var positions = new GridPosition[16];
            var index = 0;
            // y = 0 is reserved by the existing five-slot bench coordinate system.
            for (var y = 1; y <= 4; y++)
            {
                for (var x = 1; x <= 4; x++)
                {
                    positions[index++] = new GridPosition(x, y);
                }
            }

            return positions;
        }

        private static GridPosition[] CreateRotationalCounterpart(IReadOnlyList<GridPosition> positions)
        {
            var counterpart = new GridPosition[positions.Count];
            for (var index = 0; index < positions.Count; index++)
            {
                counterpart[index] = new GridPosition(
                    (FixedBoardLayoutDefinition.FixedColumns - 1) - positions[index].X,
                    (FixedBoardLayoutDefinition.FixedRows - 1) - positions[index].Y);
            }

            return counterpart;
        }

        private static List<GridPosition> CreateLegacyPlayerPotentialDeploymentCells()
        {
            var positions = new List<GridPosition>(16);
            for (var y = 1; y <= 4; y++)
            {
                for (var x = 0; x <= 2; x++)
                {
                    positions.Add(new GridPosition(x, y));
                }
            }

            positions.Add(new GridPosition(3, 1));
            positions.Add(new GridPosition(3, 2));
            positions.Add(new GridPosition(3, 3));
            positions.Add(new GridPosition(4, 2));
            return positions;
        }

        private static void ApplyLaneCells(
            FixedBoardCellDefinition[] cells,
            IReadOnlyList<GridPosition> lane,
            FixedBoardCellOwner owner)
        {
            for (var index = 0; index < lane.Count; index++)
            {
                var role = index == 0
                    ? FixedBoardCellRole.Spawn
                    : index == lane.Count - 1 ? FixedBoardCellRole.Goal : FixedBoardCellRole.Lane;
                SetFixedCell(
                    cells,
                    lane[index],
                    role,
                    owner,
                    FixedBoardDeployState.NotApplicable,
                    role == FixedBoardCellRole.Spawn
                        ? owner == FixedBoardCellOwner.Player ? "ART_PlayerSpawn" : "ART_AiSpawn"
                        : role == FixedBoardCellRole.Goal
                            ? owner == FixedBoardCellOwner.Player ? "ART_PlayerGoal" : "ART_AiGoal"
                            : owner == FixedBoardCellOwner.Player ? "ART_PlayerLane" : "ART_AiLane");
            }
        }

        private static bool Contains(IReadOnlyList<GridPosition> positions, GridPosition target)
        {
            foreach (var position in positions)
            {
                if (position == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetFixedCell(
            FixedBoardCellDefinition[] cells,
            GridPosition position,
            FixedBoardCellRole role,
            FixedBoardCellOwner owner,
            FixedBoardDeployState deployState,
            string artSlotId)
        {
            cells[GetFixedCellIndex(position.X, position.Y)] = new FixedBoardCellDefinition(
                position,
                role,
                owner,
                deployState,
                artSlotId);
        }

        private static int GetFixedCellIndex(int x, int y)
        {
            return (y * FixedBoardLayoutDefinition.FixedColumns) + x;
        }

        private static BattlefieldLayoutDefinition CreateStandard(string layoutId, int width, int height)
        {
            var cells = new List<BattlefieldCellDefinition>(width * height);
            for (var y = 1; y <= height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    cells.Add(new BattlefieldCellDefinition(
                        new GridPosition(x, y),
                        CellType.Locked));
                }
            }

            return new BattlefieldLayoutDefinition(
                layoutId,
                width,
                height,
                cells,
                CreateBenchSlots(0),
                CreateSymmetricLanes(width, height),
                1,
                width == 4 ? 1 : 2,
                CreateInitialUnlockedCells());
        }

        private static List<GridPosition> CreateInitialUnlockedCells()
        {
            return new List<GridPosition>
            {
                new GridPosition(0, 1),
                new GridPosition(1, 1),
                new GridPosition(2, 1),
                new GridPosition(0, 2),
                new GridPosition(1, 2),
                new GridPosition(2, 2)
            };
        }

        private static BattlefieldLayoutDefinition CreateLegacy()
        {
            var cells = new List<BattlefieldCellDefinition>();
            for (var y = 1; y <= 2; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    cells.Add(new BattlefieldCellDefinition(new GridPosition(x, y), CellType.Battle));
                }
            }

            for (var x = 0; x < 3; x++)
            {
                cells.Add(new BattlefieldCellDefinition(new GridPosition(x, 3), CellType.Locked));
            }

            return new BattlefieldLayoutDefinition(
                Legacy3x3Id,
                3,
                3,
                cells,
                CreateBenchSlots(0),
                CreateLegacyPerimeterLanes(),
                1,
                1);
        }

        private static List<GridPosition> CreateBenchSlots(int y)
        {
            return new List<GridPosition>
            {
                new GridPosition(0, y),
                new GridPosition(1, y),
                new GridPosition(2, y),
                new GridPosition(3, y),
                new GridPosition(4, y)
            };
        }

        private static List<BattlefieldLaneDefinition> CreateSymmetricLanes(int width, int height)
        {
            var highestCombatY = height - 1;
            // One cell from the nearest column keeps melee range meaningful without
            // letting the lane overlap a deployable slot.
            var playerLaneX = -1f;
            var aiLaneX = width;
            return new List<BattlefieldLaneDefinition>
            {
                new BattlefieldLaneDefinition(
                    TeamSide.Player,
                    BattlefieldLaneSide.Left,
                    new[] { "Spawn", "PathPoint_1", "PathPoint_2", "PathPoint_3", "DragonGoal" },
                    new[]
                    {
                        new CombatPoint(playerLaneX, highestCombatY + 0.5f),
                        new CombatPoint(playerLaneX, highestCombatY * 0.75f),
                        new CombatPoint(playerLaneX, highestCombatY * 0.5f),
                        new CombatPoint(playerLaneX, highestCombatY * 0.25f),
                        new CombatPoint(playerLaneX, -0.5f)
                    }),
                new BattlefieldLaneDefinition(
                    TeamSide.AI,
                    BattlefieldLaneSide.Right,
                    new[] { "Spawn", "PathPoint_1", "PathPoint_2", "PathPoint_3", "DragonGoal" },
                    new[]
                    {
                        new CombatPoint(aiLaneX, highestCombatY + 0.5f),
                        new CombatPoint(aiLaneX, highestCombatY * 0.75f),
                        new CombatPoint(aiLaneX, highestCombatY * 0.5f),
                        new CombatPoint(aiLaneX, highestCombatY * 0.25f),
                        new CombatPoint(aiLaneX, -0.5f)
                    })
            };
        }

        private static List<BattlefieldLaneDefinition> CreateLegacyPerimeterLanes()
        {
            return new List<BattlefieldLaneDefinition>
            {
                new BattlefieldLaneDefinition(
                    TeamSide.Player,
                    BattlefieldLaneSide.Left,
                    new[] { "Spawn", "PathPoint_1", "PathPoint_2", "PathPoint_3", "DragonGoal" },
                    new[]
                    {
                        new CombatPoint(-0.5f, 2.5f),
                        new CombatPoint(-0.5f, -0.5f),
                        new CombatPoint(2.5f, -0.5f),
                        new CombatPoint(2.5f, 2.5f),
                        new CombatPoint(-0.5f, 2.5f)
                    }),
                new BattlefieldLaneDefinition(
                    TeamSide.AI,
                    BattlefieldLaneSide.Right,
                    new[] { "Spawn", "PathPoint_1", "PathPoint_2", "PathPoint_3", "DragonGoal" },
                    new[]
                    {
                        new CombatPoint(2.5f, 2.5f),
                        new CombatPoint(2.5f, -0.5f),
                        new CombatPoint(-0.5f, -0.5f),
                        new CombatPoint(-0.5f, 2.5f),
                        new CombatPoint(2.5f, 2.5f)
                    })
            };
        }
    }
}
