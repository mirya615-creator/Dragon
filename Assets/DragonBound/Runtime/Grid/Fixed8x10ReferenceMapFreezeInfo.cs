namespace DragonBound.Grid
{
    /// <summary>
    /// Read-only design record for the Fixed8x10_ReferenceMap01 freeze candidate.
    /// It deliberately does not drive gameplay; the layout definition remains the
    /// single source of runtime cells and ordered paths.
    /// </summary>
    public sealed class Fixed8x10ReferenceMapFreezeInfo
    {
        public static readonly Fixed8x10ReferenceMapFreezeInfo Current =
            new Fixed8x10ReferenceMapFreezeInfo();

        private Fixed8x10ReferenceMapFreezeInfo()
        {
        }

        public string LayoutId => BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01Id;
        public int BoardColumns => 8;
        public int BoardRows => 10;
        public int AiHalfRows => 5;
        public int PlayerHalfRows => 5;
        public int TotalCells => 80;
        public string CellAspectRatio => "1:1";
        public int UnlockedCellsPerSide => 6;
        public int LockedCellsPerSide => 18;
        public int PlayerPathNodeCount => 16;
        public int AiPathNodeCount => 16;
        public string CoordinateConvention =>
            "Config rows top-to-bottom; Runtime y bottom-to-top; single conversion layer.";

        public bool Matches(FixedBoardLayoutDefinition layout)
        {
            return layout != null &&
                layout.LayoutId == LayoutId &&
                layout.Columns == BoardColumns &&
                layout.Rows == BoardRows &&
                layout.PlayerInitialUnlockedCells.Count == UnlockedCellsPerSide &&
                layout.AiInitialUnlockedCells.Count == UnlockedCellsPerSide &&
                layout.PlayerUnlockableCells.Count == LockedCellsPerSide &&
                layout.AiUnlockableCells.Count == LockedCellsPerSide &&
                layout.PlayerLaneWaypoints.Count == PlayerPathNodeCount &&
                layout.AiLaneWaypoints.Count == AiPathNodeCount;
        }
    }
}
