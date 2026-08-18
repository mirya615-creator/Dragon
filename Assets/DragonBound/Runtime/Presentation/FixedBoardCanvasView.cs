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

        [SerializeField] private string layoutId;
        [SerializeField] private RectTransform screenRoot;
        [SerializeField] private RectTransform boardRect;
        [SerializeField] private RectTransform terrainLayer;
        [SerializeField] private RectTransform cellLayer;
        [SerializeField] private RectTransform roadLayer;
        [SerializeField] private RectTransform laneLayer;
        [SerializeField] private RectTransform unitLayer;
        [SerializeField] private RectTransform combatFxLayer;
        [SerializeField] private RectTransform overlayLayer;
        [SerializeField] private RectTransform centerDivider;
        [SerializeField] private BoardDebugOverlay debugOverlay;

        private FixedBoardLayoutDefinition layout;

        public FixedBoardLayoutDefinition Layout => layout;
        public RectTransform BoardRect => boardRect;
        public RectTransform LaneLayer => laneLayer;
        public RectTransform UnitLayer => unitLayer;
        public RectTransform CombatFxLayer => combatFxLayer;
        public RectTransform OverlayLayer => overlayLayer;
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

            var existing = targetScreenRoot.Find("ART_FixedBoardCanvas");
            if (existing != null && existing.TryGetComponent<FixedBoardCanvasView>(out var existingView))
            {
                existingView.BindAuthoredLayout(definition);
                return existingView;
            }

            var root = new GameObject("ART_FixedBoardCanvas", typeof(RectTransform), typeof(FixedBoardCanvasView));
            root.transform.SetParent(targetScreenRoot, false);
            var result = root.GetComponent<FixedBoardCanvasView>();
            result.Initialize(targetScreenRoot, definition, authoredCellTemplate);
            return result;
        }

        /// <summary>
        /// Binds gameplay state to a board already authored and saved in the screen prefab.
        /// This method deliberately does not create objects or change RectTransforms, sprites,
        /// colors, hierarchy, or other artist-owned presentation values.
        /// </summary>
        public void BindAuthoredLayout(FixedBoardLayoutDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!string.IsNullOrWhiteSpace(layoutId) &&
                !string.Equals(layoutId, definition.LayoutId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Authored fixed board layout '{layoutId}' cannot bind runtime layout '{definition.LayoutId}'.");
            }

            layout = definition;
            layoutId = definition.LayoutId;
            screenRoot = screenRoot != null ? screenRoot : transform.parent as RectTransform;
            boardRect = boardRect != null ? boardRect : (RectTransform)transform;
            ResolveAuthoredReferences();
            RebuildAuthoredLookups();
        }

        /// <summary>
        /// Editor-bake operation. Saves board bounds for the project's reference resolution;
        /// runtime binding never recalculates or overwrites these authored values.
        /// </summary>
        public void BakeReferenceBounds(Vector2 referenceResolution)
        {
            if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(referenceResolution));
            }

            RefreshBoardBounds(referenceResolution);
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

            var waypoints = side == TeamSide.Player
                ? definition.PlayerLaneWaypoints
                : definition.AiLaneWaypoints;
            if (laneArt.TryGetValue(side, out var authoredInstances) &&
                authoredInstances.Count == waypoints.Count - 2)
            {
                return;
            }

            if (Application.isPlaying)
            {
                throw new InvalidOperationException(
                    $"The {side} lane art is missing from the authored fixed-board prefab. Re-bake the editable board.");
            }

            ClearLaneArt(side);
            var instances = new List<RectTransform>();
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
            layoutId = definition.LayoutId;
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

        private void ResolveAuthoredReferences()
        {
            terrainLayer = ResolveRect(terrainLayer, boardRect, "ART_FixedBoardTerrainLayer");
            cellLayer = ResolveRect(cellLayer, boardRect, "ART_FixedBoardCellLayer");
            roadLayer = ResolveRect(roadLayer, boardRect, "ART_FixedBoardRoadLayer");
            laneLayer = ResolveRect(laneLayer, boardRect, "ART_FixedBoardLaneLayer");
            unitLayer = ResolveRect(unitLayer, boardRect, "ART_FixedBoardUnitLayer");
            combatFxLayer = ResolveRect(combatFxLayer, boardRect, "ART_FixedBoardCombatFxLayer");
            overlayLayer = ResolveRect(overlayLayer, screenRoot, "ART_FixedBoardOverlay");
            centerDivider = ResolveRect(centerDivider, terrainLayer, "ART_CenterDivider");
            if (debugOverlay == null && overlayLayer != null)
            {
                debugOverlay = overlayLayer.GetComponentInChildren<BoardDebugOverlay>(true);
            }

            if (terrainLayer == null || cellLayer == null || roadLayer == null ||
                laneLayer == null || unitLayer == null || combatFxLayer == null || overlayLayer == null)
            {
                throw new InvalidOperationException(
                    "The authored fixed-board prefab is incomplete. Re-bake it before entering Play mode.");
            }
        }

        private void RebuildAuthoredLookups()
        {
            cellViews.Clear();
            visualCells.Clear();
            laneArt.Clear();
            mapArtSlots.Clear();

            foreach (var cell in boardRect.GetComponentsInChildren<GridCellView>(true))
            {
                if (cell == null || !layout.TryGetCellDefinition(cell.Position, out var definition))
                {
                    continue;
                }

                cell.BindAuthoredFixedBoardDefinition(definition);
                if (!visualCells.TryAdd(cell.Position, cell))
                {
                    throw new InvalidOperationException($"Duplicate authored fixed-board cell at {cell.Position}.");
                }

                if (definition.Role == FixedBoardCellRole.Deployment)
                {
                    cellViews.Add(cell.Position, cell);
                }
            }

            if (visualCells.Count != layout.Columns * layout.Rows)
            {
                throw new InvalidOperationException(
                    $"Authored fixed board has {visualCells.Count} cells; expected {layout.Columns * layout.Rows}.");
            }

            AddMapArtSlots(boardRect);
            AddMapArtSlots(overlayLayer);

            RebuildLaneArtLookup(TeamSide.Player, layout.PlayerLaneWaypoints);
            RebuildLaneArtLookup(TeamSide.AI, layout.AiLaneWaypoints);
        }

        private void RebuildLaneArtLookup(TeamSide side, IReadOnlyList<GridPosition> waypoints)
        {
            var owner = side == TeamSide.Player
                ? FixedBoardCellOwner.Player
                : FixedBoardCellOwner.AI;
            var instances = new List<RectTransform>();
            for (var index = 1; index < waypoints.Count - 1; index++)
            {
                var slotId = FixedBoardArtContract.GetLaneSurfaceSlot(layout, owner, waypoints[index]);
                if (mapArtSlots.TryGetValue(slotId, out var slot))
                {
                    instances.Add((RectTransform)slot.transform);
                }
            }

            laneArt[side] = instances;
        }

        private void AddMapArtSlots(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var slot in root.GetComponentsInChildren<FixedBoardMapArtSlot>(true))
            {
                if (slot != null && !string.IsNullOrWhiteSpace(slot.ArtSlotId))
                {
                    mapArtSlots[slot.ArtSlotId] = slot;
                }
            }
        }

        private static RectTransform ResolveRect(
            RectTransform current,
            RectTransform parent,
            string childName)
        {
            if (current != null || parent == null)
            {
                return current;
            }

            return parent.Find(childName) as RectTransform;
        }

        private void CreateLayers()
        {
            terrainLayer = CreateRuntimeLayer("ART_FixedBoardTerrainLayer", boardRect);
            cellLayer = CreateRuntimeLayer("ART_FixedBoardCellLayer", boardRect);
            roadLayer = CreateRuntimeLayer("ART_FixedBoardRoadLayer", boardRect);
            laneLayer = CreateRuntimeLayer("ART_FixedBoardLaneLayer", boardRect);
            unitLayer = CreateRuntimeLayer("ART_FixedBoardUnitLayer", boardRect);
            combatFxLayer = CreateRuntimeLayer("ART_FixedBoardCombatFxLayer", boardRect);
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

            RefreshBoardBounds(screenRoot.rect.size);
        }

        private void RefreshBoardBounds(Vector2 screenSize)
        {
            var availableWidth = screenSize.x * (1f - (HorizontalMargin * 2f));
            var availableHeight = screenSize.y * (ArenaMaxY - ArenaMinY);
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

            foreach (var instance in existing)
            {
                if (instance != null)
                {
                    Destroy(instance.gameObject);
                }
            }

            laneArt.Remove(side);
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
