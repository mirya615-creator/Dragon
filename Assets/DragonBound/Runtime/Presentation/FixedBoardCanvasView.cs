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
    [DisallowMultipleComponent]
    public sealed class FixedBoardCanvasView : MonoBehaviour
    {
        private const float HorizontalMargin = 0.04f;
        private const float ArenaMinY = 0.23f;
        private const float ArenaMaxY = 0.89f;

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

        private FixedBoardLayoutDefinition layout;

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
        /// Pure presentation seam between config rows 4 and 5. This is intentionally
        /// below road art, so it never breaks the grid-aligned route tiles.
        /// </summary>
        public RectTransform CenterDivider => centerDivider;
        public int CellViewCount => cellViews.Count;
        /// <summary>
        /// Number of semantic map cells rendered by the fixed-board canvas. This includes
        /// permanent terrain and lane cells, while <see cref="CellViewCount"/> remains the
        /// number of interactive deployment cells.
        /// </summary>
        public int SemanticTileCount => visualCells.Count;
        public Vector2 CellSize => boardRect == null || layout == null
            ? Vector2.zero
            : new Vector2(
                boardRect.rect.width / layout.Columns,
                boardRect.rect.height / layout.Rows);

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
        /// Binds gameplay to the fixed board already serialized in the scene/prefab. This path
        /// deliberately does not create, resize, reposition or restyle any authored UI object.
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

            var anchor = new Vector2(
                (position.X + 0.5f) / layout.Columns,
                (position.Y + 0.5f) / layout.Rows);
            target.anchorMin = anchor;
            target.anchorMax = anchor;
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;
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
            RefreshBoardBounds();
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
            var workshop = screenRoot.Find("ART_HeroWorkshop");
            if (workshop != null)
            {
                overlayLayer.SetSiblingIndex(workshop.GetSiblingIndex());
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
                    cell.HighlightImage,
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
            target.anchorMin = new Vector2(
                position.X / (float)layout.Columns,
                position.Y / (float)layout.Rows);
            target.anchorMax = new Vector2(
                (position.X + 1f) / layout.Columns,
                (position.Y + 1f) / layout.Rows);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private void RefreshBoardBounds()
        {
            if (screenRoot == null || screenRoot.rect.width <= 0f || screenRoot.rect.height <= 0f)
            {
                return;
            }

            var availableWidth = screenRoot.rect.width * (1f - (HorizontalMargin * 2f));
            var availableHeight = screenRoot.rect.height * (ArenaMaxY - ArenaMinY);
            var cellSize = Mathf.Min(availableWidth / layout.Columns, availableHeight / layout.Rows);
            var size = new Vector2(cellSize * layout.Columns, cellSize * layout.Rows);
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
