using System;
using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Runtime layout host for the formal board. It only arranges instances of authored
    /// cell and ART_Path templates; gameplay state remains owned by the two BoardGrid instances.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FixedBoardCanvasView : MonoBehaviour
    {
        private const float HorizontalMargin = 0.04f;
        private const float ArenaMinY = 0.23f;
        private const float ArenaMaxY = 0.89f;
        private const float DefaultVisualCellSize = 110f;
        private const float DefaultCenterRiverGap = 120f;

        private readonly Dictionary<GridPosition, GridCellView> cellViews =
            new Dictionary<GridPosition, GridCellView>();
        private readonly Dictionary<GridPosition, GridCellView> visualCells =
            new Dictionary<GridPosition, GridCellView>();
        private readonly Dictionary<TeamSide, List<RectTransform>> laneArt =
            new Dictionary<TeamSide, List<RectTransform>>();
        private readonly Dictionary<string, FixedBoardMapArtSlot> mapArtSlots =
            new Dictionary<string, FixedBoardMapArtSlot>(StringComparer.Ordinal);

        [SerializeField] private bool authoredLayout;
        [SerializeField] private RectTransform screenRoot;
        [SerializeField] private RectTransform boardRect;
        [SerializeField] private RectTransform terrainLayer;
        [SerializeField] private RectTransform cellLayer;
        [SerializeField] private RectTransform roadLayer;
        [SerializeField] private RectTransform laneLayer;
        [SerializeField] private RectTransform unitLayer;
        [SerializeField] private RectTransform combatFxLayer;
        [SerializeField] private BoardBackgroundClickReceiver backgroundClickReceiver;
        [SerializeField] private RectTransform overlayLayer;
        [SerializeField] private RectTransform centerDivider;
        [SerializeField] private BoardDebugOverlay debugOverlay;
        [Header("River board layout")]
        [SerializeField, Min(1f)] private float visualCellSize = DefaultVisualCellSize;
        [SerializeField, Min(1f)] private float centerRiverGap = DefaultCenterRiverGap;
        [SerializeField, HideInInspector] private bool authoredRiverLayoutApplied;

        private FixedBoardLayoutDefinition layout;
        private float visualLayoutScale = 1f;
#if UNITY_EDITOR
        private bool applyingEditorPreview;
        private Vector2 lastEditorScreenSize = new Vector2(float.NaN, float.NaN);
        private float lastEditorCellSize = float.NaN;
        private float lastEditorRiverGap = float.NaN;
#endif

        public FixedBoardLayoutDefinition Layout => layout;
        public bool IsAuthoredLayout => authoredLayout;
        public RectTransform BoardRect => boardRect;
        public RectTransform LaneLayer => laneLayer;
        public RectTransform UnitLayer => unitLayer;
        public RectTransform CombatFxLayer => combatFxLayer;
        public RectTransform OverlayLayer => overlayLayer;
        public event Action BackgroundClicked;
        /// <summary>
        /// Development-only, non-interactive map inspection layer. It is created disabled and
        /// is excluded from the formal UI hierarchy.
        /// </summary>
        public BoardDebugOverlay DebugOverlay => debugOverlay;
        /// <summary>
        /// Pure presentation river between config rows 4 and 5. This is intentionally
        /// below road art, so it never breaks the grid-aligned route tiles.
        /// </summary>
        public RectTransform CenterDivider => centerDivider;
        public float VisualCellSize => visualCellSize > 0f ? visualCellSize : DefaultVisualCellSize;
        public float CenterRiverGap => centerRiverGap > 0f ? centerRiverGap : DefaultCenterRiverGap;
        public bool AuthoredRiverLayoutApplied => authoredRiverLayoutApplied;
        public int CellViewCount => cellViews.Count;
        /// <summary>
        /// Number of semantic map cells rendered by the fixed-board canvas. This includes
        /// permanent terrain and lane cells, while <see cref="CellViewCount"/> remains the
        /// number of interactive deployment cells.
        /// </summary>
        public int SemanticTileCount => visualCells.Count;
        public Vector2 CellSize => layout == null
            ? Vector2.zero
            : Vector2.one * (VisualCellSize * visualLayoutScale);

        public int LaneArtCount(TeamSide side)
        {
            return laneArt.TryGetValue(side, out var instances) ? instances.Count : 0;
        }

        public static FixedBoardCanvasView Create(
            RectTransform targetScreenRoot,
            FixedBoardLayoutDefinition definition,
            GridCellView authoredCellTemplate)
        {
            if (targetScreenRoot == null)
            {
                throw new ArgumentNullException(nameof(targetScreenRoot));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (authoredCellTemplate == null)
            {
                throw new ArgumentNullException(nameof(authoredCellTemplate));
            }

            var existing = targetScreenRoot.Find("ART_FixedBoardCanvasRuntime");
            if (existing != null && existing.TryGetComponent<FixedBoardCanvasView>(out var existingView))
            {
                if (existingView.layout != definition)
                {
                    throw new InvalidOperationException("A fixed board canvas cannot switch layouts during a match.");
                }

                return existingView;
            }

            var root = new GameObject("ART_FixedBoardCanvasRuntime", typeof(RectTransform), typeof(FixedBoardCanvasView));
            root.transform.SetParent(targetScreenRoot, false);
            var result = root.GetComponent<FixedBoardCanvasView>();
            result.Initialize(targetScreenRoot, definition, authoredCellTemplate);
            return result;
        }

        /// <summary>
        /// Binds gameplay to the fixed board already serialized in the scene/prefab and reapplies
        /// its centralized two-half river geometry. No gameplay coordinate or cell state changes.
        /// </summary>
        public void BindAuthored(RectTransform targetScreenRoot, FixedBoardLayoutDefinition definition)
        {
            if (!authoredLayout)
            {
                throw new InvalidOperationException(
                    "Greybox_Main requires an authored fixed board. Run the UI authoring migration in the Editor.");
            }

            if (targetScreenRoot == null) throw new ArgumentNullException(nameof(targetScreenRoot));
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            screenRoot = targetScreenRoot;
            layout = definition;
            boardRect = boardRect != null ? boardRect : (RectTransform)transform;
            ResolveAuthoredReferences();
            RebuildAuthoredLookups();
            ApplyRiverVisualLayout();
            if (backgroundClickReceiver != null)
            {
                backgroundClickReceiver.Clicked -= HandleBackgroundClicked;
                backgroundClickReceiver.Clicked += HandleBackgroundClicked;
            }
        }

        /// <summary>Editor-authoring seam. Call only after the generated preview hierarchy is saved.</summary>
        public void MarkAsAuthored()
        {
            authoredLayout = true;
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (!Application.isPlaying) RefreshEditorPreviewIfNeeded(true);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) RefreshEditorPreviewIfNeeded(true);
        }

        private void Update()
        {
            if (!Application.isPlaying) RefreshEditorPreviewIfNeeded(false);
        }

        private void RefreshEditorPreviewIfNeeded(bool force)
        {
            if (applyingEditorPreview || !authoredLayout || screenRoot == null ||
                boardRect == null || terrainLayer == null || cellLayer == null ||
                roadLayer == null || laneLayer == null || unitLayer == null ||
                combatFxLayer == null || overlayLayer == null || centerDivider == null)
            {
                return;
            }

            var screenSize = screenRoot.rect.size;
            if (!force && screenSize == lastEditorScreenSize &&
                Mathf.Approximately(lastEditorCellSize, VisualCellSize) &&
                Mathf.Approximately(lastEditorRiverGap, CenterRiverGap))
            {
                return;
            }

            applyingEditorPreview = true;
            try
            {
                layout = BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01;
                ResolveAuthoredReferences();
                RebuildAuthoredLookups();
                ApplyRiverVisualLayout();
                lastEditorScreenSize = screenSize;
                lastEditorCellSize = VisualCellSize;
                lastEditorRiverGap = CenterRiverGap;
            }
            finally
            {
                applyingEditorPreview = false;
            }
        }
#endif

        /// <summary>
        /// Applies the formal two-half presentation in the Editor. Gameplay coordinates remain
        /// the original 8 by 10 logical map; only authored RectTransforms are repositioned.
        /// </summary>
        public void ApplyAuthoredRiverLayout(FixedBoardLayoutDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            layout = definition;
            boardRect = boardRect != null ? boardRect : (RectTransform)transform;
            ResolveAuthoredReferences();
            RebuildAuthoredLookups();
            ApplyRiverVisualLayout();
            authoredRiverLayoutApplied = true;
        }

        public bool TryGetCellView(GridPosition position, out GridCellView cellView)
        {
            return cellViews.TryGetValue(position, out cellView);
        }

        public bool TryGetVisualCell(GridPosition position, out GridCellView cellView)
        {
            return visualCells.TryGetValue(position, out cellView);
        }

        public bool TryGetArtSlot(GridPosition position, out FixedBoardArtSlot artSlot)
        {
            artSlot = null;
            return visualCells.TryGetValue(position, out var cellView) &&
                cellView != null &&
                cellView.TryGetComponent(out artSlot);
        }

        public bool TryGetMapArtSlot(string artSlotId, out FixedBoardMapArtSlot artSlot)
        {
            return mapArtSlots.TryGetValue(artSlotId, out artSlot);
        }

        public void SetDebugOverlayVisible(bool visible)
        {
            debugOverlay?.SetVisible(visible);
        }

        public GridCellView GetDeploymentCell(GridPosition position, TeamSide side)
        {
            if (!layout.IsOwnedDeploymentCell(position, side) ||
                !cellViews.TryGetValue(position, out var cellView))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return cellView;
        }

        public void BindLaneArt(
            FixedBoardLayoutDefinition definition,
            TeamSide side,
            RectTransform authoredRoadTemplate)
        {
            if (definition != layout)
            {
                throw new InvalidOperationException("Lane art must use the active fixed board layout.");
            }

            if (authoredRoadTemplate == null)
            {
                throw new ArgumentNullException(nameof(authoredRoadTemplate));
            }

            ClearLaneArt(side);
            var waypoints = side == TeamSide.Player
                ? definition.PlayerLaneWaypoints
                : definition.AiLaneWaypoints;
            var instances = new List<RectTransform>();
            if (authoredLayout)
            {
                var slots = roadLayer.GetComponentsInChildren<FixedBoardMapArtSlot>(true);
                for (var index = 1; index < waypoints.Count - 1; index++)
                {
                    var position = waypoints[index];
                    var slotId = FixedBoardArtContract.GetLaneSurfaceSlot(
                        layout,
                        side == TeamSide.Player ? FixedBoardCellOwner.Player : FixedBoardCellOwner.AI,
                        position);
                    FixedBoardMapArtSlot match = null;
                    foreach (var slot in slots)
                    {
                        if (slot != null && string.Equals(slot.ArtSlotId, slotId, StringComparison.Ordinal))
                        {
                            match = slot;
                            break;
                        }
                    }

                    if (match == null)
                    {
                        throw new InvalidOperationException($"Authored road art is missing: {slotId}");
                    }

                    instances.Add((RectTransform)match.transform);
                }

                laneArt.Add(side, instances);
                return;
            }

            for (var index = 1; index < waypoints.Count - 1; index++)
            {
                var position = waypoints[index];
                if (!layout.TryGetCellDefinition(position, out var cell) ||
                    cell.Role != FixedBoardCellRole.Lane)
                {
                    throw new InvalidOperationException("Lane art may only bind a configured R tile.");
                }

                var roadTile = Instantiate(authoredRoadTemplate, roadLayer);
                var slotId = FixedBoardArtContract.GetLaneSurfaceSlot(
                    layout,
                    side == TeamSide.Player ? FixedBoardCellOwner.Player : FixedBoardCellOwner.AI,
                    position);
                roadTile.gameObject.name = $"{slotId}_{position.X}_{position.Y}";
                roadTile.gameObject.SetActive(true);
                ConfigureCellRect(roadTile, position);
                DisableLaneArtInput(roadTile);
                var roadSlot = roadTile.GetComponent<FixedBoardMapArtSlot>();
                if (roadSlot == null)
                {
                    roadSlot = roadTile.gameObject.AddComponent<FixedBoardMapArtSlot>();
                }

                roadSlot.Bind(slotId);
                instances.Add(roadTile);
            }

            laneArt.Add(side, instances);
        }

        public void PositionAtCell(RectTransform target, GridPosition position, bool preserveSize = false)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = GetCellCenter(position);
            if (!preserveSize)
            {
                target.sizeDelta = Vector2.zero;
            }
        }

        private void Initialize(
            RectTransform targetScreenRoot,
            FixedBoardLayoutDefinition definition,
            GridCellView authoredCellTemplate)
        {
            screenRoot = targetScreenRoot;
            layout = definition;
            boardRect = (RectTransform)transform;
            PlaceBelowRuntimeUnits();
            CreateLayers();
            CreateMapArtSlots();
            CreateCells(authoredCellTemplate);
            CreateCenterDivider();
            CreateOverlayLayer();
            debugOverlay = BoardDebugOverlay.Create(overlayLayer, layout);
            ApplyRiverVisualLayout();
        }

        private void CreateLayers()
        {
            terrainLayer = CreateRuntimeLayer("ART_FixedBoardTerrainLayer", boardRect);
            cellLayer = CreateRuntimeLayer("ART_FixedBoardCellLayer", boardRect);
            CreateBackgroundClickSurface();
            roadLayer = CreateRuntimeLayer("ART_FixedBoardRoadLayer", boardRect);
            laneLayer = CreateRuntimeLayer("ART_FixedBoardLaneLayer", boardRect);
            unitLayer = CreateRuntimeLayer("ART_FixedBoardUnitLayer", boardRect);
            combatFxLayer = CreateRuntimeLayer("ART_FixedBoardCombatFxLayer", boardRect);
        }

        private void CreateBackgroundClickSurface()
        {
            var surface = new GameObject(
                "BoardBackgroundClickSurface",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = surface.GetComponent<RectTransform>();
            rect.SetParent(cellLayer, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            var image = surface.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            backgroundClickReceiver = surface.AddComponent<BoardBackgroundClickReceiver>();
            backgroundClickReceiver.Clicked += HandleBackgroundClicked;
        }

        private void HandleBackgroundClicked()
        {
            BackgroundClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (backgroundClickReceiver != null)
            {
                backgroundClickReceiver.Clicked -= HandleBackgroundClicked;
            }
        }

        private void CreateMapArtSlots()
        {
            CreateMapArtSlot(
                FixedBoardArtContract.MapBackground,
                terrainLayer,
                Vector2.zero,
                Vector2.one,
                new Color(0.08f, 0.1f, 0.13f, 1f));
            CreateMapArtSlot(
                FixedBoardArtContract.AiHalfBackground,
                terrainLayer,
                new Vector2(0f, 0.5f),
                Vector2.one,
                new Color(0.32f, 0.24f, 0.38f, 0.10f));
            CreateMapArtSlot(
                FixedBoardArtContract.PlayerHalfBackground,
                terrainLayer,
                Vector2.zero,
                new Vector2(1f, 0.5f),
                new Color(0.22f, 0.35f, 0.34f, 0.10f));
            CreateMapArtSlot(
                FixedBoardArtContract.ForegroundDecoration,
                terrainLayer,
                Vector2.zero,
                Vector2.one,
                Color.clear);
        }

        private void CreateOverlayLayer()
        {
            overlayLayer = CreateRuntimeLayer("ART_FixedBoardOverlay", screenRoot);
            var campPanel = screenRoot.Find("ART_ScreenBackground/campPanel");
            if (campPanel != null)
            {
                overlayLayer.SetSiblingIndex(campPanel.GetSiblingIndex());
            }
            else
            {
                overlayLayer.SetAsLastSibling();
            }

            CreateMapArtSlot(
                FixedBoardArtContract.MapFrame,
                overlayLayer,
                Vector2.zero,
                Vector2.one,
                new Color(0.9f, 0.94f, 0.96f, 0.008f),
                true);
        }

        private void CreateCenterDivider()
        {
            var divider = new GameObject("ART_CenterDivider", typeof(RectTransform), typeof(Image));
            centerDivider = divider.GetComponent<RectTransform>();
            centerDivider.SetParent(terrainLayer, false);
            centerDivider.anchorMin = new Vector2(0f, 0.5f);
            centerDivider.anchorMax = new Vector2(1f, 0.5f);
            centerDivider.pivot = new Vector2(0.5f, 0.5f);
            centerDivider.sizeDelta = new Vector2(0f, 2f);
            centerDivider.anchoredPosition = Vector2.zero;

            var image = divider.GetComponent<Image>();
            image.color = new Color(0.9f, 0.92f, 0.95f, 0.16f);
            image.raycastTarget = false;
            var slot = divider.AddComponent<FixedBoardMapArtSlot>();
            slot.Bind(FixedBoardArtContract.CenterDivider);
            mapArtSlots.Add(FixedBoardArtContract.CenterDivider, slot);
        }

        private void CreateCells(GridCellView authoredCellTemplate)
        {
            foreach (var definition in layout.CellDefinitions)
            {
                var parent = definition.Role == FixedBoardCellRole.Deployment
                    ? cellLayer
                    : terrainLayer;
                var cell = Instantiate(authoredCellTemplate, parent);
                cell.gameObject.name =
                    $"{definition.ArtSlotId}_{definition.Coordinate.X}_{definition.Coordinate.Y}";
                ConfigureCellRect(cell.RectTransform, definition.Coordinate);
                var type = definition.Role == FixedBoardCellRole.Deployment &&
                    definition.DeployState == FixedBoardDeployState.Unlocked
                    ? CellType.Battle
                    : CellType.Locked;
                cell.Configure(
                    definition.Coordinate.X,
                    definition.Coordinate.Y,
                    type,
                    cell.ArtImage,
                    cell.ContentAnchor);
                cell.ApplyFixedBoardDefinition(definition);
                cell.ApplyFixedBoardArtContract(
                    FixedBoardArtContract.GetCellSurfaceSlot(layout, definition));
                visualCells.Add(definition.Coordinate, cell);
                if (definition.Role == FixedBoardCellRole.Deployment)
                {
                    cellViews.Add(definition.Coordinate, cell);
                }
                else
                {
                    DisableMapTileInput(cell);
                }
            }
        }

        private static void DisableMapTileInput(GridCellView cell)
        {
            foreach (var graphic in cell.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static void DisableLaneArtInput(RectTransform art)
        {
            foreach (var graphic in art.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private void ConfigureCellRect(RectTransform target, GridPosition position)
        {
            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = GetCellCenter(position);
            target.sizeDelta = CellSize;
        }

        private void RefreshBoardBounds()
        {
            // Authored scene/prefab roots are positioned by the UI artist. Runtime binding may
            // arrange their internal board content, but must never replace the saved anchors,
            // position, size, pivot or scale of ART_FixedBoardCanvas / ART_FixedBoardOverlay.
            if (authoredLayout)
            {
                visualLayoutScale = 1f;
                return;
            }

            if (screenRoot == null || screenRoot.rect.width <= 0f || screenRoot.rect.height <= 0f)
            {
                return;
            }

            var availableWidth = screenRoot.rect.width * (1f - (HorizontalMargin * 2f));
            var availableHeight = screenRoot.rect.height * (ArenaMaxY - ArenaMinY);
            var requestedWidth = VisualCellSize * layout.Columns;
            var requestedHeight = VisualCellSize * layout.Rows + CenterRiverGap;
            var fitScale = Mathf.Min(1f, availableWidth / requestedWidth, availableHeight / requestedHeight);
            visualLayoutScale = fitScale;
            var size = new Vector2(requestedWidth * fitScale, requestedHeight * fitScale);
            var center = new Vector2(0.5f, (ArenaMinY + ArenaMaxY) * 0.5f);
            boardRect.anchorMin = center;
            boardRect.anchorMax = center;
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = Vector2.zero;
            boardRect.sizeDelta = size;

            if (overlayLayer != null)
            {
                overlayLayer.anchorMin = center;
                overlayLayer.anchorMax = center;
                overlayLayer.pivot = new Vector2(0.5f, 0.5f);
                overlayLayer.anchoredPosition = Vector2.zero;
                overlayLayer.sizeDelta = size;
            }
        }

        private Vector2 GetCellCenter(GridPosition position)
        {
            var cellSize = VisualCellSize * visualLayoutScale;
            var riverGap = CenterRiverGap * visualLayoutScale;
            var halfRows = layout.Rows / 2;
            var x = (position.X - ((layout.Columns - 1f) * 0.5f)) * cellSize;
            float y;
            if (position.Y < halfRows)
            {
                y = -(riverGap * 0.5f) - (halfRows * cellSize) +
                    ((position.Y + 0.5f) * cellSize);
            }
            else
            {
                y = (riverGap * 0.5f) +
                    (((position.Y - halfRows) + 0.5f) * cellSize);
            }

            return new Vector2(x, y);
        }

        private void ApplyRiverVisualLayout()
        {
            if (layout == null || boardRect == null) return;

            RefreshBoardBounds();
            foreach (var pair in visualCells)
            {
                if (pair.Value != null) ConfigureCellRect(pair.Value.RectTransform, pair.Key);
            }

            RepositionCoordinateNamedChildren(roadLayer);
            RepositionAuthoredLaneWaypoints(TeamSide.Player);
            RepositionAuthoredLaneWaypoints(TeamSide.AI);
            ConfigureMapArtLayout();
        }

        private void RepositionCoordinateNamedChildren(RectTransform root)
        {
            if (root == null) return;
            foreach (RectTransform child in root)
            {
                if (child != null && TryReadCoordinateSuffix(child.name, out var position))
                {
                    ConfigureCellRect(child, position);
                }
            }
        }

        private void RepositionAuthoredLaneWaypoints(TeamSide side)
        {
            if (laneLayer == null) return;
            var lane = layout.GetLane(side);
            var positions = side == TeamSide.Player
                ? layout.PlayerLaneWaypoints
                : layout.AiLaneWaypoints;
            for (var index = 0; index < lane.NodeNames.Count && index < positions.Count; index++)
            {
                var waypoint = laneLayer.Find(lane.NodeNames[index]) as RectTransform;
                if (waypoint != null) PositionAtCell(waypoint, positions[index]);
            }
        }

        private void ConfigureMapArtLayout()
        {
            var cellSize = VisualCellSize * visualLayoutScale;
            var riverGap = CenterRiverGap * visualLayoutScale;
            var boardWidth = cellSize * layout.Columns;
            var halfHeight = cellSize * (layout.Rows / 2f);
            SetMapArtRect(FixedBoardArtContract.MapBackground, Vector2.zero,
                new Vector2(boardWidth, halfHeight * 2f + riverGap));
            SetMapArtRect(FixedBoardArtContract.ForegroundDecoration, Vector2.zero,
                new Vector2(boardWidth, halfHeight * 2f + riverGap));
            SetMapArtRect(FixedBoardArtContract.MapFrame, Vector2.zero,
                new Vector2(boardWidth, halfHeight * 2f + riverGap));
            SetMapArtRect(FixedBoardArtContract.AiHalfBackground,
                new Vector2(0f, (riverGap + halfHeight) * 0.5f),
                new Vector2(boardWidth, halfHeight));
            SetMapArtRect(FixedBoardArtContract.PlayerHalfBackground,
                new Vector2(0f, -(riverGap + halfHeight) * 0.5f),
                new Vector2(boardWidth, halfHeight));

            if (centerDivider != null)
            {
                SetFixedRect(centerDivider, Vector2.zero, new Vector2(boardWidth, riverGap));
                foreach (var graphic in centerDivider.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        private void SetMapArtRect(string slotId, Vector2 position, Vector2 size)
        {
            if (mapArtSlots.TryGetValue(slotId, out var slot) && slot != null &&
                slot.transform is RectTransform rect)
            {
                SetFixedRect(rect, position, size);
            }
        }

        private static void SetFixedRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static bool TryReadCoordinateSuffix(string value, out GridPosition position)
        {
            position = default;
            if (string.IsNullOrEmpty(value)) return false;
            var lastSeparator = value.LastIndexOf('_');
            if (lastSeparator <= 0 || !int.TryParse(value.Substring(lastSeparator + 1), out var y))
            {
                return false;
            }

            var previousSeparator = value.LastIndexOf('_', lastSeparator - 1);
            if (previousSeparator < 0 ||
                !int.TryParse(value.Substring(previousSeparator + 1, lastSeparator - previousSeparator - 1), out var x))
            {
                return false;
            }

            position = new GridPosition(x, y);
            return true;
        }

        private void PlaceBelowRuntimeUnits()
        {
            var aiUnits = screenRoot.Find("AiUnitLayer");
            if (aiUnits != null)
            {
                boardRect.SetSiblingIndex(aiUnits.GetSiblingIndex());
            }
        }

        private void ClearLaneArt(TeamSide side)
        {
            if (!laneArt.TryGetValue(side, out var existing))
            {
                return;
            }

            if (!authoredLayout)
            {
                foreach (var instance in existing)
                {
                    if (instance != null)
                    {
                        Destroy(instance.gameObject);
                    }
                }
            }

            laneArt.Remove(side);
        }

        private void ResolveAuthoredReferences()
        {
            terrainLayer = RequireRect(terrainLayer, transform, "ART_FixedBoardTerrainLayer");
            cellLayer = RequireRect(cellLayer, transform, "ART_FixedBoardCellLayer");
            roadLayer = RequireRect(roadLayer, transform, "ART_FixedBoardRoadLayer");
            laneLayer = RequireRect(laneLayer, transform, "ART_FixedBoardLaneLayer");
            unitLayer = RequireRect(unitLayer, transform, "ART_FixedBoardUnitLayer");
            combatFxLayer = RequireRect(combatFxLayer, transform, "ART_FixedBoardCombatFxLayer");
            overlayLayer = RequireRect(overlayLayer, screenRoot, "ART_FixedBoardOverlay");
            centerDivider = centerDivider != null
                ? centerDivider
                : terrainLayer.Find("ART_CenterDivider") as RectTransform;
            backgroundClickReceiver = backgroundClickReceiver != null
                ? backgroundClickReceiver
                : cellLayer.GetComponentInChildren<BoardBackgroundClickReceiver>(true);
            debugOverlay = debugOverlay != null
                ? debugOverlay
                : overlayLayer.GetComponentInChildren<BoardDebugOverlay>(true);
            if (centerDivider == null || backgroundClickReceiver == null)
            {
                throw new InvalidOperationException("The authored fixed board hierarchy is incomplete.");
            }
        }

        private void RebuildAuthoredLookups()
        {
            cellViews.Clear();
            visualCells.Clear();
            mapArtSlots.Clear();
            laneArt.Clear();

            foreach (var cell in boardRect.GetComponentsInChildren<GridCellView>(true))
            {
                if (cell == null || !layout.TryGetCellDefinition(cell.Position, out var definition)) continue;
                if (visualCells.ContainsKey(cell.Position))
                {
                    throw new InvalidOperationException($"Duplicate authored fixed-board cell: {cell.Position}");
                }

                cell.BindAuthoredFixedBoardDefinition(definition);
                visualCells.Add(cell.Position, cell);
                if (definition.Role == FixedBoardCellRole.Deployment)
                {
                    cellViews.Add(cell.Position, cell);
                }
            }

            if (visualCells.Count != layout.CellDefinitions.Count || cellViews.Count != 48)
            {
                throw new InvalidOperationException(
                    $"Authored fixed board does not match {layout.LayoutId}. " +
                    $"Semantic={visualCells.Count}/{layout.CellDefinitions.Count} Deployment={cellViews.Count}/48");
            }

            RegisterMapArtSlots(boardRect);
            RegisterMapArtSlots(overlayLayer);
        }

        private void RegisterMapArtSlots(RectTransform root)
        {
            foreach (var slot in root.GetComponentsInChildren<FixedBoardMapArtSlot>(true))
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.ArtSlotId)) continue;
                if (!mapArtSlots.ContainsKey(slot.ArtSlotId)) mapArtSlots.Add(slot.ArtSlotId, slot);
            }
        }

        private static RectTransform RequireRect(RectTransform current, Transform parent, string name)
        {
            var result = current != null ? current : parent.Find(name) as RectTransform;
            if (result == null) throw new InvalidOperationException($"Authored fixed-board node is missing: {name}");
            return result;
        }

        private static RectTransform CreateRuntimeLayer(string name, Transform parent)
        {
            var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(parent, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.pivot = new Vector2(0.5f, 0.5f);
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            return layer;
        }

        private void CreateMapArtSlot(
            string slotId,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            bool addOutline = false)
        {
            var node = new GameObject(slotId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = node.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (addOutline)
            {
                var outline = node.AddComponent<Outline>();
                outline.effectColor = new Color(0.92f, 0.95f, 0.98f, 0.18f);
                outline.effectDistance = new Vector2(1.5f, 1.5f);
            }

            var slot = node.AddComponent<FixedBoardMapArtSlot>();
            slot.Bind(slotId);
            mapArtSlots.Add(slotId, slot);
        }
    }
}
