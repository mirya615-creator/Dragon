using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class GreyboxBoardView : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private GridCellView[] cellViews;
        [SerializeField] private RectTransform unitLayer;
        [SerializeField] private DraggableUnitView unitPrefab;
        [Header("Optional authored hero presentation")]
        [SerializeField] private DraggableUnitView heroPrefab;
        [SerializeField] private HeroFormationView heroFormationEffectPrefab;
        [SerializeField] private RectTransform heroEffectLayer;
        [SerializeField] private Image rangePreview;
        [Header("Fixed-slot drag preview")]
        [SerializeField] private DragArrowPreviewView dragArrowPreview;
        [SerializeField] private bool allowInteraction = true;
        [SerializeField] private bool showDebugRangeBands;
        [Header("Recruit item colors")]
        [SerializeField] private Color basicUnitColor = Color.white;
        [SerializeField] private Color heroComponentColor = new Color(0.32f, 0.62f, 1f, 1f);
        [SerializeField] private Color purpleHeroColor = new Color(0.67f, 0.35f, 1f, 0.92f);
        [SerializeField] private Color goldHeroColor = new Color(1f, 0.73f, 0.16f, 0.92f);

        private readonly Dictionary<GridPosition, GridCellView> cells =
            new Dictionary<GridPosition, GridCellView>();
        private readonly Dictionary<string, DraggableUnitView> unitViews =
            new Dictionary<string, DraggableUnitView>(StringComparer.Ordinal);
        private readonly Dictionary<GridPosition, DraggableUnitView> beachItemViews =
            new Dictionary<GridPosition, DraggableUnitView>();
        private readonly Dictionary<GridCellView, DraggableUnitView> beachItemViewsByCell =
            new Dictionary<GridCellView, DraggableUnitView>();
        private readonly HashSet<DraggableUnitView> authoredBeachItemViews =
            new HashSet<DraggableUnitView>();
        private readonly Dictionary<string, string> unitLabels =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> unitRangeCells =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> unitShowsRange =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroFormationView> pairPresentations =
            new Dictionary<string, HeroFormationView>(StringComparer.Ordinal);
        private readonly HashSet<string> soulChainControlledUnitIds =
            new HashSet<string>(StringComparer.Ordinal);
        private BoardGrid board;
        private BoardRecruitDestination unitDestination;
        private RecruitmentService recruitment;
        private ShovelUnlockService shovelUnlockService;
        private DragPlacementController drag;
        private FixedBoardCanvasView fixedBoardCanvas;
        private string selectedUnitId;
        private string activeShovelDragId;
        private bool isRefreshingUnits;
        private bool refreshUnitsPending;

        public Canvas Canvas => canvas;
        public BoardGrid Board => board;
        public DragPlacementController Drag => drag;
        public IReadOnlyList<GridCellView> CellViews => cellViews;
        public Image RangePreview => rangePreview;
        public bool AllowInteraction => allowInteraction;
        public RectTransform UnitLayer => unitLayer;
        public DraggableUnitView HeroPrefab => heroPrefab;
        public HeroFormationView HeroFormationEffectPrefab => heroFormationEffectPrefab;
        public bool HasDragGhost => false;
        public bool IsDragGhostVisible => false;
        public RectTransform DragGhostRectTransform => null;
        public bool HasDragArrowPreview => dragArrowPreview != null;
        public bool IsDragArrowVisible => dragArrowPreview != null && dragArrowPreview.IsVisible;
        public Color BasicUnitColor => basicUnitColor;
        public Color HeroComponentColor => heroComponentColor;
        public Color PurpleHeroColor => purpleHeroColor;
        public Color GoldHeroColor => goldHeroColor;

        public void SetSoulChainControlledUnits(IReadOnlyList<string> controlledRuntimeIds)
        {
            soulChainControlledUnitIds.Clear();
            if (controlledRuntimeIds != null)
            {
                for (var index = 0; index < controlledRuntimeIds.Count; index++)
                {
                    var runtimeId = controlledRuntimeIds[index];
                    if (!string.IsNullOrWhiteSpace(runtimeId))
                    {
                        soulChainControlledUnitIds.Add(runtimeId);
                    }
                }
            }

            foreach (var entry in unitViews)
            {
                entry.Value?.SetSoulChainControlled(
                    soulChainControlledUnitIds.Contains(entry.Key));
            }
        }

        public void ConfigureRecruitItemColors(
            Color basic,
            Color component,
            Color purpleHero,
            Color goldHero)
        {
            basicUnitColor = basic;
            heroComponentColor = component;
            purpleHeroColor = purpleHero;
            goldHeroColor = goldHero;
            if (board != null)
            {
                RefreshUnits();
            }
        }

        public Color GetRecruitItemColor(RecruitItemKind kind)
        {
            return kind == RecruitItemKind.HeroComponent
                ? heroComponentColor
                : basicUnitColor;
        }

        public Color GetHeroRarityColor(HeroRecipeRarity rarity)
        {
            return rarity == HeroRecipeRarity.Gold
                ? goldHeroColor
                : purpleHeroColor;
        }

        public bool TryGetUnitPosition(string runtimeId, out Vector3 position)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                unitViews.TryGetValue(runtimeId, out var unitView) &&
                unitView != null)
            {
                position = unitView.RectTransform.position;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                pairPresentations.TryGetValue(runtimeId, out var pairView) &&
                pairView != null)
            {
                position = pairView.RectTransform.position;
                return true;
            }

            position = transform.position;
            return false;
        }

        public bool TryGetBasicBattleUnitAtScreenPoint(
            Vector2 screenPosition,
            out string runtimeId,
            out RectTransform unitRect)
        {
            runtimeId = null;
            unitRect = null;
            var rootCanvas = canvas != null ? canvas.rootCanvas : GetComponentInParent<Canvas>()?.rootCanvas;
            var eventCamera = rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

            foreach (var pair in unitViews)
            {
                var view = pair.Value;
                if (view == null || !view.gameObject.activeInHierarchy ||
                    unitDestination == null ||
                    !unitDestination.TryGetCard(pair.Key, out var card) ||
                    card.Kind != RecruitItemKind.BasicUnit ||
                    board == null || !board.TryGetPosition(pair.Key, out var position) ||
                    !cells.TryGetValue(position, out var cell) || cell.CellType != CellType.Battle ||
                    !RectTransformUtility.RectangleContainsScreenPoint(view.RectTransform, screenPosition, eventCamera))
                {
                    continue;
                }

                runtimeId = pair.Key;
                unitRect = view.RectTransform;
                return true;
            }

            return false;
        }

        public void Configure(
            Canvas targetCanvas,
            GridCellView[] views,
            RectTransform targetUnitLayer,
            DraggableUnitView cardPrefab)
        {
            Configure(targetCanvas, views, targetUnitLayer, cardPrefab, null, true);
        }

        public void Configure(
            Canvas targetCanvas,
            GridCellView[] views,
            RectTransform targetUnitLayer,
            DraggableUnitView cardPrefab,
            Image targetRangePreview,
            bool interactionEnabled,
            DragArrowPreviewView targetDragArrowPreview = null)
        {
            canvas = targetCanvas;
            cellViews = views;
            unitLayer = targetUnitLayer;
            unitPrefab = cardPrefab;
            rangePreview = targetRangePreview;
            allowInteraction = interactionEnabled;
            dragArrowPreview = targetDragArrowPreview;
        }

        public void ConfigureHeroPresentation(
            DraggableUnitView authoredHeroPrefab,
            HeroFormationView authoredFormationEffectPrefab,
            RectTransform effectLayer = null)
        {
            heroPrefab = authoredHeroPrefab;
            heroFormationEffectPrefab = authoredFormationEffectPrefab;
            heroEffectLayer = effectLayer;
        }

        public void ConfigureFixedBoardCanvas(FixedBoardCanvasView canvasView)
        {
            fixedBoardCanvas = canvasView ?? throw new ArgumentNullException(nameof(canvasView));
            if (unitLayer != null && fixedBoardCanvas.UnitLayer != null &&
                unitLayer.parent != fixedBoardCanvas.UnitLayer)
            {
                unitLayer.SetParent(fixedBoardCanvas.UnitLayer, false);
            }

            if (rangePreview != null && fixedBoardCanvas.CombatFxLayer != null &&
                rangePreview.rectTransform.parent != fixedBoardCanvas.CombatFxLayer)
            {
                rangePreview.rectTransform.SetParent(fixedBoardCanvas.CombatFxLayer, false);
                rangePreview.rectTransform.SetAsFirstSibling();
                rangePreview.raycastTarget = false;
            }

            fixedBoardCanvas.BackgroundClicked -= HandleBackgroundClicked;
            fixedBoardCanvas.BackgroundClicked += HandleBackgroundClicked;

            if (dragArrowPreview != null && fixedBoardCanvas.OverlayLayer != null &&
                dragArrowPreview.transform.parent != fixedBoardCanvas.OverlayLayer)
            {
                dragArrowPreview.transform.SetParent(fixedBoardCanvas.OverlayLayer, false);
            }
        }

        public void BindRecruitment(RecruitmentService service)
        {
            recruitment = service;
            if (board != null)
            {
                RefreshUnits();
            }
        }

        public void BindShovelUnlockService(ShovelUnlockService service)
        {
            if (shovelUnlockService != null)
            {
                shovelUnlockService.StateChanged -= HandleShovelStateChanged;
            }

            shovelUnlockService = service;
            if (shovelUnlockService != null)
            {
                shovelUnlockService.StateChanged += HandleShovelStateChanged;
            }
        }

        public void Initialize(BoardGrid value)
        {
            Initialize(value, null);
        }

        public void Initialize(BoardGrid value, BoardRecruitDestination destination)
        {
            if (board != null)
            {
                throw new InvalidOperationException("The board view is already initialized.");
            }

            board = value ?? throw new ArgumentNullException(nameof(value));
            unitDestination = destination;
            if (unitDestination != null)
            {
                unitDestination.HeroPairLinked += HandleHeroPairLinked;
                unitDestination.HeroPairUnlinked += HandleHeroPairUnlinked;
                unitDestination.BasicUnitLevelChanged += HandleBasicUnitLevelChanged;
            }

            if (canvas == null || unitLayer == null || unitPrefab == null ||
                (fixedBoardCanvas == null && cellViews == null))
            {
                throw new InvalidOperationException("Editable board view references are incomplete.");
            }

            BindLayoutCells();

            foreach (var cellView in cellViews)
            {
                if (cellView == null || cells.ContainsKey(cellView.Position))
                {
                    throw new InvalidOperationException("Editable grid cell references are null or duplicated.");
                }

                cells.Add(cellView.Position, cellView);
                cellView.Clicked -= HandleCellClicked;
                cellView.Clicked += HandleCellClicked;
            }

            RefreshCellStates();
            board.Changed += HandleBoardChanged;

            drag = new DragPlacementController(board, unitDestination, true);
            if (rangePreview != null)
            {
                rangePreview.raycastTarget = false;
                SetRangePreviewVisible(false);
            }

            RefreshUnits();
        }

        private void OnDestroy()
        {
            CancelActiveDrag(false);
            if (unitDestination != null)
            {
                unitDestination.HeroPairLinked -= HandleHeroPairLinked;
                unitDestination.HeroPairUnlinked -= HandleHeroPairUnlinked;
                unitDestination.BasicUnitLevelChanged -= HandleBasicUnitLevelChanged;
            }

            if (shovelUnlockService != null)
            {
                shovelUnlockService.StateChanged -= HandleShovelStateChanged;
            }

            foreach (var cellView in cells.Values)
            {
                cellView.Clicked -= HandleCellClicked;
            }

            if (fixedBoardCanvas != null)
            {
                fixedBoardCanvas.BackgroundClicked -= HandleBackgroundClicked;
            }

            if (board != null)
            {
                board.Changed -= HandleBoardChanged;
            }
        }

        private void OnDisable()
        {
            CancelActiveDrag(false);
            CancelShovelSelection();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelActiveDrag();
                CancelShovelSelection();
            }
        }

        public void RefreshUnits()
        {
            if (isRefreshingUnits)
            {
                refreshUnitsPending = true;
                return;
            }

            do
            {
                refreshUnitsPending = false;
                isRefreshingUnits = true;
                try
                {
                    RefreshUnitsCore();
                }
                finally
                {
                    isRefreshingUnits = false;
                }
            }
            while (refreshUnitsPending);
        }

        private void RefreshUnitsCore()
        {
            var previousViews = new Dictionary<string, DraggableUnitView>(unitViews, StringComparer.Ordinal);
            var usedViews = new HashSet<DraggableUnitView>();
            var currentIds = new HashSet<string>(StringComparer.Ordinal);
            unitViews.Clear();
            foreach (var beachItemView in authoredBeachItemViews)
            {
                if (beachItemView != null)
                {
                    beachItemView.gameObject.SetActive(false);
                }
            }

            foreach (var occupant in board.GetOccupants())
            {
                if (!cells.ContainsKey(occupant.Position))
                {
                    continue;
                }

                currentIds.Add(occupant.UnitId);
                DraggableUnitView unitView;
                var usesBeachItem = beachItemViews.TryGetValue(occupant.Position, out var beachItemView);
                var reusedPreviousView = previousViews.TryGetValue(occupant.UnitId, out var previousView) &&
                                         previousView != null;
                if (usesBeachItem)
                {
                    unitView = beachItemView;
                }
                else if (reusedPreviousView && !authoredBeachItemViews.Contains(previousView))
                {
                    unitView = previousView;
                }
                else
                {
                    unitView = Instantiate(unitPrefab, unitLayer);
                    unitView.gameObject.name = $"Card_{occupant.UnitId}";
                    reusedPreviousView = false;
                }

                var samePreviousView = reusedPreviousView && ReferenceEquals(previousView, unitView);
                if (!samePreviousView)
                {
                    unitView.Initialize(this, occupant.UnitId);
                }

                unitView.gameObject.SetActive(true);
                unitView.SetInteractive(allowInteraction);
                unitViews.Add(occupant.UnitId, unitView);
                usedViews.Add(unitView);

                RecruitCard currentCard = null;
                if (unitDestination != null &&
                    unitDestination.TryGetCard(occupant.UnitId, out currentCard))
                {
                    unitView.SetStandardPresentation();
                    ApplyCardPresentation(currentCard, unitView);
                }

                if (unitLabels.TryGetValue(occupant.UnitId, out var label))
                {
                    var hideComponentName = currentCard != null &&
                                            currentCard.Kind == RecruitItemKind.HeroComponent;
                    unitView.SetLabel(hideComponentName ? string.Empty : label);
                }

                unitView.SetPairedPresentation(false);
                unitView.SetSoulChainControlled(
                    soulChainControlledUnitIds.Contains(occupant.UnitId));
                SnapUnit(occupant.UnitId);
            }

            foreach (var entry in previousViews)
            {
                if (entry.Value != null &&
                    !usedViews.Contains(entry.Value) &&
                    !authoredBeachItemViews.Contains(entry.Value))
                {
                    Destroy(entry.Value.gameObject);
                }
            }

            foreach (var previousId in previousViews.Keys)
            {
                if (currentIds.Contains(previousId))
                {
                    continue;
                }

                unitLabels.Remove(previousId);
                unitRangeCells.Remove(previousId);
                unitShowsRange.Remove(previousId);
                if (string.Equals(selectedUnitId, previousId, StringComparison.Ordinal))
                {
                    HideRangePreview();
                }
            }

            RefreshPairPresentations();
        }

        public void SetUnitLabel(string unitId, string label)
        {
            SetUnitPresentation(unitId, label, 1.5f, true);
        }

        public void SetUnitPresentation(string unitId, string label, float rangeCells)
        {
            SetUnitPresentation(unitId, label, rangeCells, true);
        }

        public void SetUnitPresentation(string unitId, string label, float rangeCells, bool showRange)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                throw new ArgumentException("A unit id is required.", nameof(unitId));
            }

            if (showRange && rangeCells <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rangeCells));
            }

            unitLabels[unitId] = label;
            unitRangeCells[unitId] = rangeCells;
            unitShowsRange[unitId] = showRange;
            if (unitViews.TryGetValue(unitId, out var unitView))
            {
                var hideComponentName = unitDestination != null &&
                                        unitDestination.TryGetCard(unitId, out var card) &&
                                        card.Kind == RecruitItemKind.HeroComponent;
                unitView.SetUnitLabelVisibility(!hideComponentName);
                unitView.SetLabel(hideComponentName ? string.Empty : label);
            }
        }

        public bool BeginDrag(string unitId)
        {
            if (allowInteraction &&
                unitDestination != null &&
                unitDestination.TryGetCard(unitId, out var card) &&
                card.Kind == RecruitItemKind.Shovel &&
                shovelUnlockService != null &&
                shovelUnlockService.BeginSelection(unitId))
            {
                activeShovelDragId = unitId;
                HideDragArrow();
                HideRangePreview();
                return true;
            }

            if (!allowInteraction || drag == null || !drag.BeginDrag(unitId))
            {
                return false;
            }

            if (!unitViews.TryGetValue(unitId, out var unitView) || unitView == null)
            {
                drag.Cancel();
                return false;
            }

            HideDragArrow();
            HideRangePreview();
            return true;
        }

        public void UpdateDraggedUnit(string unitId, Vector2 screenPosition)
        {
            if (string.Equals(activeShovelDragId, unitId, StringComparison.Ordinal))
            {
                return;
            }

            if (dragArrowPreview == null ||
                drag == null ||
                !drag.IsDragging ||
                !board.TryGetPosition(unitId, out var source) ||
                !cells.TryGetValue(source, out var sourceCell))
            {
                return;
            }

            if (TryGetPositionAt(screenPosition, out var target) &&
                drag.CanPreviewTarget(target) &&
                cells.TryGetValue(target, out var targetCell))
            {
                var parent = dragArrowPreview.transform.parent as RectTransform;
                dragArrowPreview.Show(parent, sourceCell.ContentAnchor.position, targetCell.ContentAnchor.position);
                return;
            }

            HideDragArrow();
        }

        public void CompleteDrag(string unitId, Vector2 screenPosition)
        {
            if (string.Equals(activeShovelDragId, unitId, StringComparison.Ordinal))
            {
                activeShovelDragId = null;
                var unlocked = TryGetPositionAt(screenPosition, out var shovelTarget) &&
                               shovelUnlockService != null &&
                               shovelUnlockService.TryUnlockCell(shovelTarget);
                if (!unlocked)
                {
                    shovelUnlockService?.CancelSelection();
                }

                HideDragArrow();
                HideRangePreview();
                return;
            }

            if (drag == null || !drag.IsDragging)
            {
                HideDragArrow();
                SnapUnit(unitId);
                HideRangePreview();
                return;
            }

            if (TryGetPositionAt(screenPosition, out var target))
            {
                drag.Drop(target);
            }
            else
            {
                drag.Cancel();
            }

            HideDragArrow();
            RefreshUnits();
            HideRangePreview();
        }

        public void CancelActiveDrag()
        {
            CancelActiveDrag(true);
        }

        public void HideRangePreview()
        {
            selectedUnitId = null;
            SetRangePreviewVisible(false);
        }

        public GridCellView GetCellView(GridPosition position)
        {
            return cells.TryGetValue(position, out var cellView) ? cellView : null;
        }

        public void SelectUnit(string unitId)
        {
            if (unitDestination != null &&
                unitDestination.TryGetCard(unitId, out var card) &&
                card.Kind == RecruitItemKind.Shovel)
            {
                if (shovelUnlockService != null)
                {
                    if (shovelUnlockService.IsSelecting &&
                        string.Equals(shovelUnlockService.SelectedBenchShovelRuntimeId, unitId, StringComparison.Ordinal))
                    {
                        shovelUnlockService.CancelSelection();
                    }
                    else
                    {
                        shovelUnlockService.BeginSelection(unitId);
                    }
                }

                HideRangePreview();
                return;
            }

            if (TrySelectPairRange(unitId))
            {
                return;
            }

            if (rangePreview == null ||
                board == null ||
                !board.TryGetPosition(unitId, out var position) ||
                !cells.TryGetValue(position, out var cellView) ||
                cellView.CellType != CellType.Battle ||
                !unitShowsRange.TryGetValue(unitId, out var showRange) ||
                !showRange)
            {
                SetRangePreviewVisible(false);
                selectedUnitId = null;
                return;
            }

            selectedUnitId = unitId;
            var radius = unitRangeCells.TryGetValue(unitId, out var configuredRadius)
                ? configuredRadius
                : 1.5f;
            ShowRange(cellView.ContentAnchor.position, cellView, null, radius);
        }

        private bool TrySelectPairRange(string componentId)
        {
            if (rangePreview == null ||
                unitDestination == null ||
                !unitDestination.TryGetPairLinkForComponent(componentId, out var pairLink) ||
                !pairLink.CombatProxy.IsFormationComplete ||
                !unitDestination.TryGetComponent(pairLink.ComponentAId, out var componentA) ||
                !unitDestination.TryGetComponent(pairLink.ComponentBId, out var componentB) ||
                !cells.TryGetValue(componentA.CurrentCell, out var firstCell) ||
                !cells.TryGetValue(componentB.CurrentCell, out var secondCell))
            {
                return false;
            }

            selectedUnitId = componentId;
            var worldCenter = (firstCell.ContentAnchor.position + secondCell.ContentAnchor.position) * 0.5f;
            ShowRange(worldCenter, firstCell, secondCell, pairLink.CombatProxy.RangeCells);
            return true;
        }

        private void ShowRange(
            Vector3 worldCenter,
            GridCellView firstCell,
            GridCellView secondCell,
            float radiusCells)
        {
            var cellSize = Mathf.Min(
                firstCell.RectTransform.rect.width,
                firstCell.RectTransform.rect.height);
            if (secondCell != null)
            {
                cellSize = Mathf.Min(
                    cellSize,
                    Mathf.Min(secondCell.RectTransform.rect.width, secondCell.RectTransform.rect.height));
            }

            var rangeParent = rangePreview.rectTransform.parent as RectTransform ?? unitLayer;
            rangePreview.rectTransform.anchoredPosition = rangeParent.InverseTransformPoint(worldCenter);
            rangePreview.rectTransform.sizeDelta = Vector2.one * (cellSize * radiusCells * 2f);
            rangePreview.rectTransform.SetAsFirstSibling();
            SetRangePreviewVisible(true);
        }

        private void SetRangePreviewVisible(bool visible)
        {
            if (rangePreview == null)
            {
                return;
            }

            rangePreview.enabled = visible;
            rangePreview.gameObject.SetActive(visible);
        }

        private void ApplyCardPresentation(RecruitCard card, DraggableUnitView unitView)
        {
            if (card.Kind == RecruitItemKind.BasicUnit)
            {
                unitView?.SetUnitLabelVisibility(true);
                unitView?.SetBeachTextVisibility(true, true);
                unitView?.SetCardColor(GetRecruitItemColor(card.Kind));
                var stats = BasicUnitCatalog.GetStats(card.ConfigId, card.Level);
                unitLabels[card.RuntimeId] = BasicUnitCatalog.GetDisplayName(card.ConfigId);
                unitRangeCells[card.RuntimeId] = stats.RangeCells;
                unitShowsRange[card.RuntimeId] = true;
                unitView?.SetBasicLevel(card.Level);
                return;
            }

            var isHeroComponent = card.Kind == RecruitItemKind.HeroComponent;
            unitView?.SetUnitLabelVisibility(!isHeroComponent);
            unitView?.SetBeachTextVisibility(card.Kind == RecruitItemKind.Shovel, false);

            if (isHeroComponent &&
                unitView != null &&
                ResourcesCampComponentArtProvider.Shared.TryGetHeroComponentSprite(
                    card.ConfigId,
                    out var componentSprite))
            {
                unitView.SetCardSprite(componentSprite);
                unitView.SetCardColor(Color.white);
            }
            else
            {
                unitView?.SetCardColor(GetRecruitItemColor(card.Kind));
            }

            unitLabels[card.RuntimeId] = allowInteraction
                ? HeroSliceCardPresentation.GetLabel(card, recruitment)
                : HeroSliceCardPresentation.GetEnglishLabel(card, recruitment);
            unitRangeCells[card.RuntimeId] = 0f;
            unitShowsRange[card.RuntimeId] = false;
        }

        public void CancelShovelSelection()
        {
            shovelUnlockService?.CancelSelection();
        }

        private void HandleCellClicked(GridPosition position)
        {
            // Any board-cell tap is an explicit selection change. This also lets an empty
            // deployment or road cell dismiss a previously selected unit's range preview.
            HideRangePreview();
            if (allowInteraction && shovelUnlockService != null)
            {
                shovelUnlockService.TryUnlockCell(position);
            }
        }

        private void HandleBackgroundClicked()
        {
            HideRangePreview();
            if (shovelUnlockService != null && shovelUnlockService.IsSelecting)
            {
                shovelUnlockService.CancelSelection();
            }
        }

        private void HandleShovelStateChanged()
        {
            // BeginSelection raises StateChanged from inside the pointer/drag callback. Rebuilding
            // BeachItem views at that point deactivates the object Unity is currently dispatching
            // input to and can recursively enter RefreshUnits via DraggableUnitView.OnDisable.
            if (board != null && (shovelUnlockService == null || !shovelUnlockService.IsSelecting))
            {
                RefreshUnits();
            }
        }

        private void LateUpdate()
        {
            if (unitDestination == null || board == null)
            {
                return;
            }

            RefreshPairPresentations();
        }

        private void HandleHeroPairLinked(HeroPairLinkedEvent linked)
        {
            RefreshUnits();
        }

        private void HandleBasicUnitLevelChanged(string runtimeId)
        {
            RefreshUnits();
        }

        private void HandleHeroPairUnlinked(HeroPairUnlinkedEvent unlinked)
        {
            RemovePairPresentation(unlinked.PairLink.PairLinkId);
            if (unitViews.TryGetValue(unlinked.PairLink.ComponentAId, out var firstView))
            {
                firstView.SetPairedPresentation(false);
            }

            if (unitViews.TryGetValue(unlinked.PairLink.ComponentBId, out var secondView))
            {
                secondView.SetPairedPresentation(false);
            }

            if (string.Equals(selectedUnitId, unlinked.PairLink.ComponentAId, StringComparison.Ordinal) ||
                string.Equals(selectedUnitId, unlinked.PairLink.ComponentBId, StringComparison.Ordinal))
            {
                HideRangePreview();
            }
        }

        private void RefreshPairPresentations()
        {
            if (unitDestination == null)
            {
                return;
            }

            var activeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var activePair in unitDestination.GetActiveHeroPairs())
            {
                var pairLink = activePair.PairLink;
                activeIds.Add(pairLink.PairLinkId);
                if (unitViews.TryGetValue(pairLink.ComponentAId, out var firstView))
                {
                    firstView.SetPairedPresentation(true);
                }

                if (unitViews.TryGetValue(pairLink.ComponentBId, out var secondView))
                {
                    secondView.SetPairedPresentation(true);
                }

                if (heroFormationEffectPrefab == null ||
                    !TryGetPairLayout(activePair, heroEffectLayer != null ? heroEffectLayer : unitLayer, out var layout))
                {
                    continue;
                }

                if (!pairPresentations.TryGetValue(pairLink.PairLinkId, out var pairView) || pairView == null)
                {
                    var parent = heroEffectLayer != null ? heroEffectLayer : unitLayer;
                    pairView = Instantiate(heroFormationEffectPrefab, parent);
                    pairView.gameObject.name = $"HeroPair_{pairLink.PairLinkId}";
                    pairPresentations[pairLink.PairLinkId] = pairView;
                }

                ApplyPairPresentation(activePair, pairView, layout);
            }

            var staleIds = new List<string>();
            foreach (var pairId in pairPresentations.Keys)
            {
                if (!activeIds.Contains(pairId))
                {
                    staleIds.Add(pairId);
                }
            }

            foreach (var pairId in staleIds)
            {
                RemovePairPresentation(pairId);
            }
        }

        private void ApplyPairPresentation(
            ActiveHeroPair activePair,
            HeroFormationView pairView,
            PairLayout layout)
        {
            var pairLink = activePair.PairLink;
            var combat = pairLink.CombatProxy;
            var definition = HeroSliceCatalog.Get(pairLink.HeroId);
            pairView.Initialize(
                layout.Center,
                layout.Primary - layout.Center,
                layout.Secondary - layout.Center,
                layout.PairSize,
                layout.CellSize,
                GetHeroRarityColor(definition.Rarity),
                definition.Rarity);
            pairView.SetHeroAnimation(pairLink.HeroId);
            pairView.ObserveAttackSequence(combat.SuccessfulAttackSequence);
            pairView.SetRune(combat.RuneId);
            pairView.SetProgress(combat.FormationProgress);
        }

        private void RemovePairPresentation(string pairLinkId)
        {
            if (!pairPresentations.TryGetValue(pairLinkId, out var pairView))
            {
                return;
            }

            if (pairView != null)
            {
                Destroy(pairView.gameObject);
            }

            pairPresentations.Remove(pairLinkId);
        }

        private void Start()
        {
            RefreshLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (board != null)
            {
                RefreshLayout();
            }
        }

        private void RefreshLayout()
        {
            foreach (var unitId in unitViews.Keys)
            {
                SnapUnit(unitId);
            }

            RefreshPairPresentations();
            if (!string.IsNullOrEmpty(selectedUnitId))
            {
                SelectUnit(selectedUnitId);
            }
        }

        private void HandleBoardChanged(GridMutation mutation)
        {
            RefreshCellStates();
        }

        private void BindLayoutCells()
        {
            if (board == null || board.Layout == null)
            {
                return;
            }

            if (board.FixedLayout != null && fixedBoardCanvas != null)
            {
                BindFixedLayoutCells();
                return;
            }

            var known = new Dictionary<GridPosition, GridCellView>();
            foreach (var existing in cellViews)
            {
                if (existing != null && !known.ContainsKey(existing.Position))
                {
                    known.Add(existing.Position, existing);
                }
            }

            GridCellView template = null;
            foreach (var existing in known.Values)
            {
                if (existing.CellType != CellType.Bench)
                {
                    template = existing;
                    break;
                }
            }

            if (template == null)
            {
                throw new InvalidOperationException("A board layout requires one authored battle-cell template.");
            }

            foreach (var definition in board.Layout.FormationCells)
            {
                if (!known.TryGetValue(definition.Position, out var cell))
                {
                    cell = Instantiate(template, template.transform.parent);
                    cell.gameObject.name = $"RuntimeBoardCell_{definition.Position.X}_{definition.Position.Y}";
                    known.Add(definition.Position, cell);
                }

                ConfigureFormationCell(cell, definition.Position, definition.CellType);
            }

            // Player boards also bind the existing five authored bench slots. AI boards do not include them.
            foreach (var benchPosition in board.Layout.BenchPositions)
            {
                if (!board.TryGetCellType(benchPosition, out var benchType))
                {
                    continue;
                }

                if (known.TryGetValue(benchPosition, out var benchCell))
                {
                    benchCell.ApplyRuntimeState(benchType, null, false);
                }
            }

            var bound = new List<GridCellView>();
            foreach (var entry in known)
            {
                if (board.TryGetCellType(entry.Key, out _))
                {
                    bound.Add(entry.Value);
                }
                else
                {
                    entry.Value.gameObject.SetActive(false);
                }
            }

            bound.Sort((first, second) => first.Position.CompareTo(second.Position));
            cellViews = bound.ToArray();
        }

        private void BindFixedLayoutCells()
        {
            var bound = new List<GridCellView>();
            foreach (var position in board.FixedLayout.GetPotentialDeploymentCells(board.Side))
            {
                bound.Add(fixedBoardCanvas.GetDeploymentCell(position, board.Side));
            }

            // Bench cells remain in the authored prefab below the board. Their logical coordinates
            // are deliberately outside the 8 x 10 map, so bind the visible slots by stable order.
            var authoredBenchCells = new List<GridCellView>();
            if (cellViews != null)
            {
                foreach (var existing in cellViews)
                {
                    if (existing == null)
                    {
                        continue;
                    }

                    if (existing.CellType == CellType.Bench)
                    {
                        authoredBenchCells.Add(existing);
                    }
                    else
                    {
                        // Compatibility with older authored scenes. The current scene no longer
                        // contains the legacy Battlefield-local formation cells.
                        existing.gameObject.SetActive(false);
                    }
                }
            }

            if (board.Side == TeamSide.Player)
            {
                var benchPositions = board.Layout.BenchPositions;
                if (authoredBenchCells.Count < benchPositions.Count)
                {
                    BindBeachBenchCells(authoredBenchCells, benchPositions.Count);
                }

                if (authoredBenchCells.Count < benchPositions.Count)
                {
                    throw new InvalidOperationException(
                        "The fixed board requires five BeachContainer/ImgBg slots with BeachItem.prefab instances.");
                }

                for (var index = 0; index < authoredBenchCells.Count; index++)
                {
                    var benchCell = authoredBenchCells[index];
                    if (index >= benchPositions.Count)
                    {
                        benchCell.gameObject.SetActive(false);
                        continue;
                    }

                    var position = benchPositions[index];
                    benchCell.Configure(
                        position.X,
                        position.Y,
                        CellType.Bench,
                        benchCell.ArtImage,
                        benchCell.ContentAnchor);
                    benchCell.gameObject.SetActive(true);
                    bound.Add(benchCell);
                    if (beachItemViewsByCell.TryGetValue(benchCell, out var beachItemView))
                    {
                        beachItemViews[position] = beachItemView;
                    }
                }
            }
            else
            {
                foreach (var benchCell in authoredBenchCells)
                {
                    benchCell.gameObject.SetActive(false);
                }
            }

            bound.Sort((first, second) => first.Position.CompareTo(second.Position));
            cellViews = bound.ToArray();
        }

        private void BindBeachBenchCells(List<GridCellView> benchCells, int requiredCount)
        {
            if (benchCells == null || benchCells.Count >= requiredCount)
            {
                return;
            }

            var screen = fixedBoardCanvas.GetComponentInParent<DragonBoundScreenView>();
            var beachContainer = screen != null
                ? screen.transform.Find("ART_ScreenBackground/BeachContainer")
                : null;
            if (beachContainer == null)
            {
                return;
            }

            var beachItemPrefab = Resources.Load<GameObject>("prefabs/BeachItem");
            for (var childIndex = 0;
                 childIndex < beachContainer.childCount && benchCells.Count < requiredCount;
                 childIndex++)
            {
                var slot = beachContainer.GetChild(childIndex) as RectTransform;
                if (slot == null || !slot.name.StartsWith("ImgBg", StringComparison.Ordinal))
                {
                    continue;
                }

                var cell = slot.GetComponent<GridCellView>();
                if (cell == null)
                {
                    cell = slot.gameObject.AddComponent<GridCellView>();
                }

                var beachItem = slot.Find("BeachItem");
                if (beachItem == null && beachItemPrefab != null)
                {
                    beachItem = Instantiate(beachItemPrefab, slot, false).transform;
                    beachItem.name = "BeachItem";
                }

                if (beachItem == null)
                {
                    continue;
                }

                var itemView = beachItem.GetComponent<DraggableUnitView>();
                if (itemView == null)
                {
                    itemView = beachItem.gameObject.AddComponent<DraggableUnitView>();
                }

                var canvasGroup = beachItem.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = beachItem.gameObject.AddComponent<CanvasGroup>();
                }

                var nameTransform = beachItem.Find("Text (TMP)");
                var levelTransform = beachItem.Find("Text");
                itemView.ConfigureBeach(
                    beachItem.GetComponent<Image>(),
                    nameTransform != null ? nameTransform.GetComponent<Graphic>() : null,
                    levelTransform != null ? levelTransform.GetComponent<Graphic>() : null,
                    canvasGroup);
                beachItem.gameObject.SetActive(false);

                cell.Configure(0, 0, CellType.Bench, slot.GetComponent<Image>(), slot);
                beachItemViewsByCell[cell] = itemView;
                authoredBeachItemViews.Add(itemView);
                benchCells.Add(cell);
            }
        }

        private void ConfigureFormationCell(GridCellView cell, GridPosition position, CellType type)
        {
            var layout = board.Layout;
            var columns = Mathf.Max(1, layout.Width);
            var rows = Mathf.Max(1, layout.Height);
            var centerX = 0.22f + ((position.X + 0.5f) / columns * 0.58f);
            var firstRow = 1;
            var vertical = ((position.Y - firstRow) + 0.5f) / rows;
            var centerY = board.Side == DragonBound.Core.TeamSide.Player
                ? 0.84f - (vertical * 0.65f)
                : 0.16f + (vertical * 0.65f);
            var cellSize = 106f * (4f / Mathf.Max(columns, rows));
            var transform = cell.RectTransform;
            transform.anchorMin = new Vector2(centerX, centerY);
            transform.anchorMax = new Vector2(centerX, centerY);
            transform.pivot = new Vector2(0.5f, 0.5f);
            transform.anchoredPosition = Vector2.zero;
            transform.sizeDelta = Vector2.one * cellSize;
            cell.Configure(position.X, position.Y, type, cell.ArtImage, cell.ContentAnchor);
        }

        private void RefreshCellStates()
        {
            if (board == null)
            {
                return;
            }

            foreach (var entry in cells)
            {
                if (board.TryGetCellType(entry.Key, out var type))
                {
                    BattlefieldRangeBand? band = null;
                    if (type != CellType.Bench && board.Layout != null)
                    {
                        band = board.GetRangeBand(entry.Key);
                    }

                    entry.Value.gameObject.SetActive(true);
                    entry.Value.ApplyRuntimeState(type, band, showDebugRangeBands);
                }
            }
        }

        private void SnapUnit(string unitId)
        {
            if (!unitViews.TryGetValue(unitId, out var unitView) ||
                !board.TryGetPosition(unitId, out var position) ||
                !cells.TryGetValue(position, out var cellView))
            {
                return;
            }

            if (beachItemViews.TryGetValue(position, out var beachItemView) &&
                ReferenceEquals(unitView, beachItemView))
            {
                return;
            }

            unitView.RectTransform.anchoredPosition = unitLayer.InverseTransformPoint(cellView.ContentAnchor.position);
            var size = cellView.RectTransform.rect.size;
            unitView.RectTransform.sizeDelta = new Vector2(size.x * 0.86f, size.y * 0.86f);
        }

        private void HideDragArrow()
        {
            dragArrowPreview?.Hide();
        }

        private void CancelActiveDrag(bool refreshView)
        {
            if (!string.IsNullOrEmpty(activeShovelDragId))
            {
                activeShovelDragId = null;
                shovelUnlockService?.CancelSelection();
            }

            if (drag != null && drag.IsDragging)
            {
                drag.Cancel();
            }

            HideDragArrow();
            HideRangePreview();
            if (refreshView && board != null)
            {
                RefreshUnits();
            }
        }

        private bool TryGetPairLayout(ActiveHeroPair activePair, RectTransform targetLayer, out PairLayout layout)
        {
            if (targetLayer == null ||
                !cells.TryGetValue(activePair.ComponentA.CurrentCell, out var primaryCell) ||
                !cells.TryGetValue(activePair.ComponentB.CurrentCell, out var secondaryCell))
            {
                layout = default;
                return false;
            }

            var primary = (Vector2)targetLayer.InverseTransformPoint(primaryCell.ContentAnchor.position);
            var secondary = (Vector2)targetLayer.InverseTransformPoint(secondaryCell.ContentAnchor.position);
            var primarySize = primaryCell.RectTransform.rect.size;
            var secondarySize = secondaryCell.RectTransform.rect.size;
            var cellSize = new Vector2(
                Mathf.Min(primarySize.x, secondarySize.x),
                Mathf.Min(primarySize.y, secondarySize.y));
            var pairSize = new Vector2(
                Mathf.Abs(secondary.x - primary.x) + cellSize.x,
                Mathf.Abs(secondary.y - primary.y) + cellSize.y);
            layout = new PairLayout(
                primary,
                secondary,
                (primary + secondary) * 0.5f,
                pairSize,
                cellSize);
            return true;
        }

        private bool TryGetPositionAt(Vector2 screenPosition, out GridPosition position)
        {
            foreach (var entry in cells)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        entry.Value.RectTransform,
                        screenPosition,
                        GetEventCamera()))
                {
                    position = entry.Key;
                    return true;
                }
            }

            position = default;
            return false;
        }

        private Camera GetEventCamera()
        {
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private readonly struct PairLayout
        {
            public PairLayout(
                Vector2 primary,
                Vector2 secondary,
                Vector2 center,
                Vector2 pairSize,
                Vector2 cellSize)
            {
                Primary = primary;
                Secondary = secondary;
                Center = center;
                PairSize = pairSize;
                CellSize = cellSize;
            }

            public Vector2 Primary { get; }
            public Vector2 Secondary { get; }
            public Vector2 Center { get; }
            public Vector2 PairSize { get; }
            public Vector2 CellSize { get; }
        }
    }
}
