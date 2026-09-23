using System;
using DragonBound.Presentation;
using System.Collections;
using System.Collections.Generic;
using DragonBound.AI;
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
        private const string ShovelSpriteResourcePath = "ComponentUI/shovel";
        private const string BoardSelectPrefabResourcePath = "prefabs/BoardSelect";
        private const string BeachSelectSpriteResourcePath = "GameUI/BeachSelect";
        private const string BoardSelectSpriteResourcePath = "GameUI/BoardSelect";
        private const string InformPrefabResourcePath = "prefabs/Inform";
        private const float MirroredUnitArtAnchoredPositionX = -38f;
        private const float MirroredHeroArtAnchoredPositionX = 13f;
        private const float AttackFacingHorizontalDeadZone = 0.5f;
        private const float DeploymentFlightDuration = 0.20f;
        private const float DeploymentArcHeightInCells = 0.4f;
        private const float SwapReturnArcHeightInCells = -0.25f;
        private const float LandingPressPixels = 3f;
        private const float LandingSettleDuration = 0.06f;
        private const float SynthesisComponentFadeDuration = 0.08f;
        private const float SynthesisComponentEndScale = 0.8f;
        private const float DragTargetSwitchHysteresisPixels = 12f;

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
        private readonly Dictionary<GridPosition, GameObject> beachSelectionViews =
            new Dictionary<GridPosition, GameObject>();
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
        private readonly Dictionary<string, int> observedHeroLevels =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> pendingSynthesisPairIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string > pendingHiddenFlightIds=
            new HashSet <string>(StringComparer.Ordinal);
        private readonly HashSet <string > pendingBasicUnitLevelUpIds=
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> soulChainControlledUnitIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, DeploymentAnimationState> deploymentAnimations =
            new Dictionary<string, DeploymentAnimationState>(StringComparer.Ordinal);
        private readonly HashSet<DraggableUnitView> synthesisComponentGhosts =
            new HashSet<DraggableUnitView>();
        private BoardGrid board;
        private BoardRecruitDestination unitDestination;
        private RecruitmentService recruitment;
        private ShovelUnlockService shovelUnlockService;
        private DragPlacementController drag;
        private FixedBoardCanvasView fixedBoardCanvas;
        private string selectedUnitId;
        private string activeShovelDragId;
        private GridPosition? activeBeachDragOrigin;
        private GridPosition? stableDragTarget;
        private GameObject boardSelectPrefab;
        private GameObject sourceSelectPreview;
        private GameObject boardSelectPreview;
        private Sprite beachSelectSprite;
        private Sprite boardSelectSprite;
        private bool isRefreshingUnits;
        private bool refreshUnitsPending;
        private bool bloodcrownSuppressionActive;
        private UnitInformController unitInform;

        private sealed class DeploymentAnimationState
        {
            public Coroutine Routine;
            public DraggableUnitView Ghost;
            public Vector3 BaseScale = Vector3.one;
            public Vector2 BaseAnchoredPosition;
            public bool HasBaseAnchoredPosition;
            public bool HideCommittedView = true;
            public bool OwnsCombatSuspension;
        }

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
        public Sprite DragPathSprite => dragArrowPreview != null ? dragArrowPreview.PathSprite : null;
        public bool IsBoardSelectVisible => boardSelectPreview != null && boardSelectPreview.activeSelf;

        private static bool PreserveMirroredUnitPosition
        {
            get
            {
                return string.Equals(
                    UiAssets.Active?.VariantId,
                    "V2",
                    StringComparison.Ordinal);
            }
        }
        public bool IsSourceSelectVisible
        {
            get
            {
                if (sourceSelectPreview != null && sourceSelectPreview.activeSelf)
                {
                    return true;
                }

                foreach (var selection in beachSelectionViews.Values)
                {
                    if (selection != null && selection.activeSelf)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        public int VisibleBeachSelectionCount
        {
            get
            {
                var count = 0;
                foreach (var selection in beachSelectionViews.Values)
                {
                    if (selection != null && selection.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void CollectFrostcrownMarkedEnemyIds(ISet<string> targetRuntimeIds)
        {
            if (targetRuntimeIds == null || unitDestination == null)
            {
                return;
            }

            foreach (var activePair in unitDestination.GetActiveHeroPairs())
            {
                var pairLink = activePair.PairLink;
                if (!string.Equals(
                        pairLink.HeroId,
                        HeroSliceCatalog.CrownHunterLeaderHeroId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var targetRuntimeId = pairLink.CombatProxy.HuntMarkTargetRuntimeId;
                if (!string.IsNullOrWhiteSpace(targetRuntimeId))
                {
                    targetRuntimeIds.Add(targetRuntimeId);
                }
            }
        }
        public Color BasicUnitColor => basicUnitColor;
        public Color HeroComponentColor => heroComponentColor;
        public Color PurpleHeroColor => purpleHeroColor;
        public Color GoldHeroColor => goldHeroColor;
        public event Action<string> FlameDrakeFireballReleased;
        public event Action<string> SkyborneValkyrieArrowReleased;
        public event Action<string> StarfallArchmageGemReleased;
        public event Action<string> NightfangSkillAnimationCompleted;
        public event Action<string> BowProjectileReleased;
        public event Action<string> WindclawSkillReleased;
        public event Action<string> EmberShamanFireballReleased;
        public event Action<string> RuneboltMageBoltReleased;
        public event Action<string> StoneboundWarlockRockReleased;
        public event Action<string> ThunderlordChainReleased;
        public event Action<string> ThunderlordSkillReleased;
        public event Action<string> AbyssalHarpoonReleased;
        public event Action<string> AbyssalHarpoonSkillReleased;

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

        public void SetBloodcrownSuppression(bool active)
        {
            bloodcrownSuppressionActive = active;
            foreach (var entry in unitViews)
            {
                entry.Value?.SetBloodcrownSuppressed(active && IsDeployedBasic(entry.Key));
            }
        }

        private bool IsDeployedBasic(string runtimeId)
        {
            return unitDestination != null &&
                   unitDestination.TryGetCard(runtimeId, out var card) &&
                   card.Kind == RecruitItemKind.BasicUnit &&
                   board != null &&
                   board.TryGetPosition(runtimeId, out var position) &&
                   board.TryGetCellType(position, out var cellType) &&
                   cellType == CellType.Battle;
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

        public bool TryGetHeroCurrentTargetRuntimeId(
            string pairLinkId,
            out string targetRuntimeId)
        {
            targetRuntimeId = string.Empty;
            if (string.IsNullOrWhiteSpace(pairLinkId) || unitDestination == null)
            {
                return false;
            }

            foreach (var activePair in unitDestination.GetActiveHeroPairs())
            {
                var pairLink = activePair.PairLink;
                if (pairLink == null ||
                    !string.Equals(pairLink.PairLinkId, pairLinkId, StringComparison.Ordinal))
                {
                    continue;
                }

                targetRuntimeId = pairLink.CombatProxy.CurrentTargetRuntimeId ?? string.Empty;
                return !string.IsNullOrWhiteSpace(targetRuntimeId);
            }

            return false;
        }

        public bool TryGetHeroAttackOrigin(string pairLinkId, out Vector3 position)
        {
            if (!string.IsNullOrWhiteSpace(pairLinkId) &&
                pairPresentations.TryGetValue(pairLinkId, out var pairView) &&
                pairView != null)
            {
                pairView.TryGetAttackOrigin(out position);
                return true;
            }

            position = transform.position;
            return false;
        }

        public bool SetHeroFormationArtVisible(string pairLinkId, bool visible)
        {
            if (string.IsNullOrWhiteSpace(pairLinkId) ||
                !pairPresentations.TryGetValue(pairLinkId, out var pairView) ||
                pairView == null)
            {
                return false;
            }

            pairView.SetHeroArtVisible(visible);
            return true;
        }

        public bool PlayHeroFormationAttackAnimation(string pairLinkId, bool useSkillAnimation = false)
        {
            return !string.IsNullOrWhiteSpace(pairLinkId) &&
                   pairPresentations.TryGetValue(pairLinkId, out var pairView) &&
                   pairView != null &&
                   pairView.PlayAttackAnimation(useSkillAnimation);
        }

        public bool PlayBasicUnitAttackAnimation(string runtimeId)
        {
            return !string.IsNullOrWhiteSpace(runtimeId) &&
                   unitViews.TryGetValue(runtimeId, out var unitView) &&
                   unitView != null &&
                   unitView.PlayBasicAttackAnimation();
        }

        public bool FaceAttackerTowardsTarget(string runtimeId, float targetWorldX)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return false;
            }

            if (unitViews.TryGetValue(runtimeId, out var unitView) && unitView != null)
            {
                var horizontalDelta = targetWorldX - unitView.RectTransform.position.x;
                if (Mathf.Abs(horizontalDelta) <= AttackFacingHorizontalDeadZone)
                {
                    return true;
                }

                unitView.FaceArtTowards(
    horizontalDelta > 0f,
    MirroredUnitArtAnchoredPositionX,
    PreserveMirroredUnitPosition);
                return true;
            }

            if (pairPresentations.TryGetValue(runtimeId, out var pairView) && pairView != null)
            {
                // Use the formation center rather than the offset art pivot. Otherwise the
                // mirrored PosX correction can make a vertically aligned target look lateral.
                var horizontalDelta = targetWorldX - pairView.RectTransform.position.x;
                if (Mathf.Abs(horizontalDelta) <= AttackFacingHorizontalDeadZone)
                {
                    return true;
                }

                pairView.FaceArtTowards(
                    horizontalDelta > 0f,
                    MirroredHeroArtAnchoredPositionX);
                return true;
            }

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

            var guideLayer = fixedBoardCanvas.EnsureDeploymentGuideLayer();
            if (dragArrowPreview != null && guideLayer != null &&
                dragArrowPreview.transform.parent != guideLayer)
            {
                dragArrowPreview.transform.SetParent(guideLayer, false);
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
                shovelUnlockService.ShovelUsed -= HandleShovelUsed;
            }

            shovelUnlockService = service;
            if (shovelUnlockService != null)
            {
                shovelUnlockService.StateChanged += HandleShovelStateChanged;
                shovelUnlockService.ShovelUsed += HandleShovelUsed;
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
                unitDestination.BasicUnitMerged += HandleBasicUnitMerged;
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
                if (cellView.CellType == CellType.Bench)
                {
                    BindBeachSelection(cellView.Position, cellView);
                }
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
            if (allowInteraction)
            {
                EnsureUnitInform();
            }
        }

        private void OnDestroy()
        {
            StopAllSynthesisComponentFades();
            StopAllDeploymentAnimations();
            CancelActiveDrag(false);
            if (unitDestination != null)
            {
                unitDestination.HeroPairLinked -= HandleHeroPairLinked;
                unitDestination.HeroPairUnlinked -= HandleHeroPairUnlinked;
                unitDestination.BasicUnitLevelChanged -= HandleBasicUnitLevelChanged;
                unitDestination.BasicUnitMerged -= HandleBasicUnitMerged;
            }

            if (shovelUnlockService != null)
            {
                shovelUnlockService.StateChanged -= HandleShovelStateChanged;
                shovelUnlockService.ShovelUsed -= HandleShovelUsed;
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
            StopAllSynthesisComponentFades();
            StopAllDeploymentAnimations();
            CancelActiveDrag(false);
            CancelShovelSelection();
            unitInform?.Hide();
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

        /// <summary>
        /// Refreshes the authoritative board and gives visible AI movement the same arced flight
        /// and landing response used by player deployment. Gameplay has already committed the
        /// action; this method only supplies the readable AI transition.
        /// </summary>
        public void RefreshUnits(AiBoardAction action, float transitionSeconds)
        {
            _ = transitionSeconds;
            var sourceWorld = Vector3.zero;
            var targetWorld = Vector3.zero;
            var canAnimate = action.HasSource && action.HasTarget &&
                             TryGetCellWorldPosition(action.Source, out sourceWorld) &&
                             TryGetCellWorldPosition(action.Target, out targetWorld);
            var swappedUnitId = string.Empty;
            if (canAnimate)
            {
                // The action is already committed. A remaining occupant in the source cell is
                // therefore the unit displaced by an AI swap and must fly back at the same time.
                board.TryGetOccupant(action.Source, out swappedUnitId);
            }

            RefreshUnits();
            if (!canAnimate || string.IsNullOrWhiteSpace(action.RuntimeId))
            {
                return;
            }

            PlayDeploymentFlight(
                action.RuntimeId,
                sourceWorld,
                targetWorld,
                DeploymentArcHeightInCells);
            if (!string.IsNullOrWhiteSpace(swappedUnitId) &&
                !string.Equals(swappedUnitId, action.RuntimeId, StringComparison.Ordinal))
            {
                PlayDeploymentFlight(
                    swappedUnitId,
                    targetWorld,
                    sourceWorld,
                    SwapReturnArcHeightInCells);
            }
        }

        private void RefreshUnitsCore()
        {
            var previousViews = new Dictionary<string, DraggableUnitView>(unitViews, StringComparer.Ordinal);
            var usedViews = new HashSet<DraggableUnitView>();
            var currentIds = new HashSet<string>(StringComparer.Ordinal);
            var pairedComponentIds = new HashSet<string>(StringComparer.Ordinal);
            if (unitDestination != null)
            {
                foreach (var activePair in unitDestination.GetActiveHeroPairs())
                {
                    pairedComponentIds.Add(activePair.PairLink.ComponentAId);
                    pairedComponentIds.Add(activePair.PairLink.ComponentBId);
                }
            }

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
                unitView.BowProjectileReleased -= HandleBowProjectileReleased;
                unitView.BowProjectileReleased += HandleBowProjectileReleased;
                unitViews.Add(occupant.UnitId, unitView);
                usedViews.Add(unitView);

                RecruitCard currentCard = null;
                if (unitDestination != null &&
                    unitDestination.TryGetCard(occupant.UnitId, out currentCard))
                {
                    unitView.SetStandardPresentation();
                    ApplyCardPresentation(currentCard, unitView);
                    unitView.InitializeArtFacing(
    board.Side == TeamSide.AI,
    MirroredUnitArtAnchoredPositionX,
    PreserveMirroredUnitPosition);
                }

                if (unitLabels.TryGetValue(occupant.UnitId, out var label))
                {
                    var hideComponentName = currentCard != null &&
                                            currentCard.Kind == RecruitItemKind.HeroComponent;
                    unitView.SetLabel(hideComponentName ? string.Empty : label);
                }

                // Preserve the authoritative pair state throughout the refresh. Resetting
                // every view to unpaired here and hiding it again later can expose the two
                // component cards briefly while the synthesis presentation is rebuilt.
                unitView.SetPairedPresentation(pairedComponentIds.Contains(occupant.UnitId));
                unitView.SetSoulChainControlled(
                    soulChainControlledUnitIds.Contains(occupant.UnitId));
                unitView.SetBloodcrownSuppressed(
                    bloodcrownSuppressionActive && IsDeployedBasic(occupant.UnitId));
                SnapUnit(occupant.UnitId);
                if (deploymentAnimations.TryGetValue(occupant.UnitId, out var deploymentState) &&
                    deploymentState.HideCommittedView)
                {
                    unitView.SetDeploymentVisualHidden(true);
                }
                else if (pendingHiddenFlightIds.Remove(occupant.UnitId))
                {
                    // RefreshUnitsCore ran while the view was marked as "hidden flight" so
                    // keep the committedView visually hidden until PlayDeploymentFlight's
                    // ghost animation finishes. Remove() returns true exactly once, which is
                    // enough to suppress the next RefreshUnits pass.
                    unitView.SetDeploymentVisualHidden(true);
                }
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
            ClearBeachDragPreview();
            stableDragTarget = null;
            unitInform?.Hide();
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
                ShowDragSourceSelection(unitId);
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
            ShowDragSourceSelection(unitId);
            return true;
        }

        public void UpdateDraggedUnit(string unitId, Vector2 screenPosition)
        {
            if (string.Equals(activeShovelDragId, unitId, StringComparison.Ordinal))
            {
                UpdateShovelDragPreview(screenPosition);
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

            if (TryGetStableDragPositionAt(screenPosition, out var target) &&
                drag.CanPreviewTarget(target) &&
                cells.TryGetValue(target, out var targetCell))
            {
                var parent = fixedBoardCanvas != null
                    ? fixedBoardCanvas.EnsureDeploymentGuideLayer()
                    : dragArrowPreview.transform.parent as RectTransform;
                if (parent != null && dragArrowPreview.transform.parent != parent)
                {
                    dragArrowPreview.transform.SetParent(parent, false);
                }
                dragArrowPreview.Show(parent, sourceCell.ContentAnchor.position, targetCell.ContentAnchor.position);
                ShowBoardSelect(targetCell);
                if (activeBeachDragOrigin.HasValue &&
                    board.TryGetCellType(target, out var targetType) &&
                    targetType == CellType.Battle)
                {
                    ShowDeploymentRangePreview(unitId, targetCell);
                }
                else
                {
                    HideDeploymentRangePreview();
                }
                return;
            }

            HideDragArrow();
            HideDeploymentRangePreview();
        }

        public void CompleteDrag(string unitId, Vector2 screenPosition)
        {
            if (string.Equals(activeShovelDragId, unitId, StringComparison.Ordinal))
            {
                activeShovelDragId = null;
                var unlocked = TryGetStableDragPositionAt(screenPosition, out var shovelTarget) &&
                               shovelUnlockService != null &&
                               shovelUnlockService.TryUnlockCell(shovelTarget);
                if (!unlocked)
                {
                    shovelUnlockService?.CancelSelection();
                }

                HideDragArrow();
                HideRangePreview();
                ClearBeachDragPreview();
                return;
            }

            if (drag == null || !drag.IsDragging)
            {
                HideDragArrow();
                SnapUnit(unitId);
                HideRangePreview();
                ClearBeachDragPreview();
                return;
            }

            var origin = default(GridPosition);
            var target = default(GridPosition);
            var originWorld = Vector3.zero;
            var targetWorld = Vector3.zero;
            var hasOrigin = board != null && board.TryGetPosition(unitId, out origin);
            var hasTarget = TryGetStableDragPositionAt(screenPosition, out target);
            var targetUnitId = string.Empty;
            if (hasTarget)
            {
                board.TryGetOccupant(target, out targetUnitId);
            }

            var canAnimatePositions = hasOrigin && hasTarget &&
                                      TryGetCellWorldPosition(origin, out originWorld) &&
                                      TryGetCellWorldPosition(target, out targetWorld);
            var movementTouchesBattlefield = false;
            if (canAnimatePositions &&
                board.TryGetCellType(origin, out var sourceType) &&
                board.TryGetCellType(target, out var targetType))
            {
                movementTouchesBattlefield = sourceType == CellType.Battle ||
                                             targetType == CellType.Battle;
            }

            var status = DragDropStatus.Cancelled;
            if (hasTarget)
            {
                status = drag.Drop(target);
            }
            else
            {
                drag.Cancel();
            }

            HideDragArrow();
            RefreshUnits();
            if (canAnimatePositions && board.Side == TeamSide.Player)
            {
                if (status == DragDropStatus.Moved && movementTouchesBattlefield)
                {
                    PlayDeploymentFlight(
                        unitId,
                        originWorld,
                        targetWorld,
                        DeploymentArcHeightInCells);
                }
                else if (status == DragDropStatus.Swapped &&
                         !string.IsNullOrWhiteSpace(targetUnitId))
                {
                    PlayDeploymentFlight(
                        unitId,
                        originWorld,
                        targetWorld,
                        DeploymentArcHeightInCells);
                    PlayDeploymentFlight(
                        targetUnitId,
                        targetWorld,
                        originWorld,
                        SwapReturnArcHeightInCells);
                }
            }
            HideRangePreview();
            ClearBeachDragPreview();
            stableDragTarget = null;
        }

        private bool TryGetCellWorldPosition(GridPosition position, out Vector3 worldPosition)
        {
            if (cells.TryGetValue(position, out var cell) &&
                cell != null && cell.ContentAnchor != null)
            {
                worldPosition = cell.ContentAnchor.position;
                return true;
            }

            worldPosition = transform.position;
            return false;
        }

        private void PlayDeploymentFlight(
            string runtimeId,
            Vector3 startWorld,
            Vector3 targetWorld,
            float arcHeightInCells)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) ||
                !unitViews.TryGetValue(runtimeId, out var committedView) ||
                committedView == null)
            {
                return;
            }

            StopDeploymentAnimation(runtimeId);
            var fxLayer = fixedBoardCanvas != null
                ? fixedBoardCanvas.EnsureDeploymentFxLayer()
                : unitLayer;
            if (fxLayer == null)
            {
                return;
            }

            var ghost = committedView.CreateDragGhost(fxLayer);
            ghost.gameObject.name = $"DeploymentGhost_{runtimeId}";
            ghost.RectTransform.position = startWorld;
            ghost.RectTransform.sizeDelta = committedView.RectTransform.sizeDelta;
            ghost.SetDragGhostOpacity(1f);
            ghost.transform.SetAsLastSibling();

            var state = new DeploymentAnimationState
            {
                Ghost = ghost,
                BaseScale = committedView.RectTransform.localScale,
                HideCommittedView = true
            };
            deploymentAnimations[runtimeId] = state;
            committedView.SetDeploymentVisualHidden(true);
            state.OwnsCombatSuspension = unitDestination != null &&
                                             unitDestination.SetDeploymentAnimationCombatSuspended(
                                                 runtimeId,
                                                 true);
            state.Routine = StartCoroutine(AnimateDeploymentFlight(
                runtimeId,
                state,
                startWorld,
                targetWorld,
                arcHeightInCells));
        }

        private IEnumerator AnimateDeploymentFlight(
            string runtimeId,
            DeploymentAnimationState state,
            Vector3 startWorld,
            Vector3 targetWorld,
            float arcHeightInCells)
        {
            var ghostRect = state.Ghost != null ? state.Ghost.RectTransform : null;
            var fxLayer = ghostRect != null ? ghostRect.parent as RectTransform : null;
            if (ghostRect == null || fxLayer == null)
            {
                FinishDeploymentAnimation(runtimeId, state);
                yield break;
            }

            var start = fxLayer.InverseTransformPoint(startWorld);
            var target = fxLayer.InverseTransformPoint(targetWorld);
            start.z = 0f;
            target.z = 0f;
            var arcHeight = Mathf.Max(40f, ghostRect.rect.height * Mathf.Abs(arcHeightInCells));
            var control = (start + target) * 0.5f +
                          (Vector3.up * arcHeight * Mathf.Sign(arcHeightInCells));
            var ghostBaseScale = ghostRect.localScale;
            var elapsed = 0f;
            while (ghostRect != null && elapsed < DeploymentFlightDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / DeploymentFlightDuration);
                var eased = 1f - Mathf.Pow(1f - progress, 3f);
                var inverse = 1f - eased;
                ghostRect.localPosition =
                    (inverse * inverse * start) +
                    (2f * inverse * eased * control) +
                    (eased * eased * target);
                ghostRect.localScale = ghostBaseScale;
                yield return null;
            }

            if (state.Ghost != null)
            {
                Destroy(state.Ghost.gameObject);
                state.Ghost = null;
            }

            state.HideCommittedView = false;
            if (!unitViews.TryGetValue(runtimeId, out var landedView) || landedView == null)
            {
                FinishDeploymentAnimation(runtimeId, state);
                yield break;
            }

            landedView.SetDeploymentVisualHidden(false);
            var landedRect = landedView.RectTransform;
            var baseScale = landedRect.localScale;
            state.BaseScale = baseScale;
            state.BaseAnchoredPosition = landedRect.anchoredPosition;
            state.HasBaseAnchoredPosition = true;

            // Landing press: a single-frame downward nudge instead of a scale
            // squash. A pure offset reads as weight and can never read as a
            // spring, because scale never leaves 1 in the first place.
            landedRect.anchoredPosition =
                state.BaseAnchoredPosition - new Vector2(0f, LandingPressPixels);
            yield return null;
            landedRect.anchoredPosition = state.BaseAnchoredPosition;

            FinishDeploymentAnimation(runtimeId, state);
        }

        private static IEnumerator AnimateDeploymentScale(
            RectTransform target,
            Vector3 baseScale,
            Vector2 multiplier,
            float duration)
        {
            if (target == null)
            {
                yield break;
            }

            var source = target.localScale;
            var destination = new Vector3(
                baseScale.x * multiplier.x,
                baseScale.y * multiplier.y,
                baseScale.z);
            var elapsed = 0f;
            while (target != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var eased = progress * progress * (3f - (2f * progress));
                target.localScale = Vector3.LerpUnclamped(source, destination, eased);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = destination;
            }
        }

        /// <summary>
        /// Marks runtime ids as "pending hidden flight" so the next RefreshUnitsCore pass
        /// will hide their committed views as soon as they are snapped into place. Pair
        /// this with a PlayRecruitDropStagger call so the same ids fly in afterwards.
        /// Must be invoked BEFORE RefreshUnits; otherwise the committed view would already
        /// be visible on the bench.
        /// </summary>
        public void MarkUnitsForHiddenFlight(IEnumerable<string> runtimeIds)
        {
            if (runtimeIds == null)
            {
                return;
            }

            foreach (var runtimeId in runtimeIds)
            {
                if (!string.IsNullOrWhiteSpace(runtimeId))
                {
                    pendingHiddenFlightIds.Add(runtimeId);
                }
            }
        }

        /// <summary>
        /// Plays a staggered drop animation: each runtime id flies from <paramref name="originWorld"/>
        /// to its current cell's world position. <paramref name="onCompleted"/> fires after the
        /// final card finishes its flight + landing settle, so the caller can safely re-enable
        /// any disabled UI and re-refresh labels that were written after the flight started.
        /// </summary>
        public void PlayRecruitDropStagger(
            IReadOnlyList<string> newRuntimeIds,
            Vector3 originWorld,
            float perCardStaggerSeconds,
            float perCardDurationSeconds,
            float arcHeightInCells,
            Action onCompleted = null)
        {
            if (newRuntimeIds == null || newRuntimeIds.Count == 0)
            {
                onCompleted?.Invoke();
                return;
            }

            if (!isActiveAndEnabled)
            {
                onCompleted?.Invoke();
                return;
            }

            StartCoroutine(AnimateRecruitDropStagger(
                newRuntimeIds,
                originWorld,
                perCardStaggerSeconds,
                perCardDurationSeconds,
                arcHeightInCells,
                onCompleted));
        }

        private IEnumerator AnimateRecruitDropStagger(
            IReadOnlyList<string> newRuntimeIds,
            Vector3 originWorld,
            float perCardStaggerSeconds,
            float perCardDurationSeconds,
            float arcHeightInCells,
            Action onCompleted)
        {
            // RefreshUnitsCore is synchronous: by the time control returns from RefreshUnits,
            // every view is already in unitViews. MarkUnitsForHiddenFlight was invoked before
            // RefreshUnits, so the committed views are visually hidden at this point — only
            // the deployment ghost will be visible during the flight.
            var pending = perCardStaggerSeconds;
            for (var index = 0; index < newRuntimeIds.Count; index++)
            {
                if (pending > 0f)
                {
                    yield return new WaitForSecondsRealtime(pending);
                }

                pending = perCardStaggerSeconds;
                var runtimeId = newRuntimeIds[index];
                if (string.IsNullOrWhiteSpace(runtimeId) ||
                    !unitViews.TryGetValue(runtimeId, out var view) ||
                    view == null ||
                    !board.TryGetPosition(runtimeId, out var gridPosition) ||
                    !cells.TryGetValue(gridPosition, out var cell) ||
                    cell == null)
                {
                    continue;
                }

                PlayDeploymentFlight(
                    runtimeId,
                    originWorld,
                    cell.transform.position,
                    arcHeightInCells);
            }

            // Wait for the final card to finish its flight + landing settle before
            // notifying the caller so the recruit button can safely re-enable.
            var settleTime = perCardDurationSeconds + LandingSettleDuration;
            yield return new WaitForSecondsRealtime(settleTime);

            onCompleted?.Invoke();
        }


        private void FinishDeploymentAnimation(string runtimeId, DeploymentAnimationState state)
        {
            ReleaseDeploymentCombatSuspension(runtimeId, state);
            if (deploymentAnimations.TryGetValue(runtimeId, out var current) &&
                ReferenceEquals(current, state))
            {
                deploymentAnimations.Remove(runtimeId);
            }

            if (unitViews.TryGetValue(runtimeId, out var view) && view != null)
            {
                view.SetDeploymentVisualHidden(false);
                view.RectTransform.localScale = state.BaseScale;
                if (state.HasBaseAnchoredPosition)
                {
                    view.RectTransform.anchoredPosition = state.BaseAnchoredPosition;
                }
            }
        }

        private void StopDeploymentAnimation(string runtimeId)
        {
            if (!deploymentAnimations.TryGetValue(runtimeId, out var state))
            {
                return;
            }

            if (state.Routine != null)
            {
                StopCoroutine(state.Routine);
            }
            if (state.Ghost != null)
            {
                Destroy(state.Ghost.gameObject);
            }
            ReleaseDeploymentCombatSuspension(runtimeId, state);
            if (unitViews.TryGetValue(runtimeId, out var view) && view != null)
            {
                view.SetDeploymentVisualHidden(false);
                view.RectTransform.localScale = state.BaseScale;
                if (state.HasBaseAnchoredPosition)
                {
                    view.RectTransform.anchoredPosition = state.BaseAnchoredPosition;
                }
            }
            deploymentAnimations.Remove(runtimeId);
        }

        private void ReleaseDeploymentCombatSuspension(
            string runtimeId,
            DeploymentAnimationState state)
        {
            if (state == null || !state.OwnsCombatSuspension)
            {
                return;
            }

            state.OwnsCombatSuspension = false;
            unitDestination?.SetDeploymentAnimationCombatSuspended(runtimeId, false);
        }

        private void StopAllDeploymentAnimations()
        {
            if (deploymentAnimations.Count == 0)
            {
                return;
            }

            var runtimeIds = new List<string>(deploymentAnimations.Keys);
            foreach (var runtimeId in runtimeIds)
            {
                StopDeploymentAnimation(runtimeId);
            }
        }

        public void CancelActiveDrag()
        {
            CancelActiveDrag(true);
        }

        public void HideRangePreview()
        {
            selectedUnitId = null;
            SetRangePreviewVisible(false);
            unitInform?.Hide();
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

            RecruitCard selectedCard = null;
            if (unitDestination != null)
            {
                unitDestination.TryGetCard(unitId, out selectedCard);
            }

            var isBasicUnit = selectedCard != null &&
                              selectedCard.Kind == RecruitItemKind.BasicUnit;

            if (rangePreview == null ||
                board == null ||
                !board.TryGetPosition(unitId, out var position) ||
                !cells.TryGetValue(position, out var cellView) ||
                (cellView.CellType != CellType.Battle &&
                 !(isBasicUnit && cellView.CellType == CellType.Bench)) ||
                !unitShowsRange.TryGetValue(unitId, out var showRange) ||
                !showRange)
            {
                SetRangePreviewVisible(false);
                selectedUnitId = null;
                unitInform?.Hide();
                return;
            }

            selectedUnitId = unitId;
            var radius = unitRangeCells.TryGetValue(unitId, out var configuredRadius)
                ? configuredRadius
                : 1.5f;
            ShowRange(cellView.ContentAnchor.position, cellView, null, radius);
            if (isBasicUnit)
            {
                EnsureUnitInform()?.ShowBasic(selectedCard);
            }
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
            EnsureUnitInform()?.ShowHero(pairLink.CombatProxy);
            return true;
        }

        private UnitInformController EnsureUnitInform()
        {
            if (!allowInteraction)
            {
                return null;
            }

            if (unitInform != null)
            {
                return unitInform;
            }

            var prefab = DragonBound.Presentation.UiAssets.Load<GameObject>(InformPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"Unit inform prefab is missing at Resources/{InformPrefabResourcePath}.");
                return null;
            }

            var parentCanvas = canvas != null ? canvas.rootCanvas : GetComponentInParent<Canvas>()?.rootCanvas;
            var parent = parentCanvas != null ? parentCanvas.transform : transform;
            var instance = Instantiate(prefab, parent, false);
            instance.name = "Inform";
            unitInform = instance.GetComponent<UnitInformController>();
            if (unitInform == null)
            {
                unitInform = instance.AddComponent<UnitInformController>();
            }

            unitInform.Hide();
            return unitInform;
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
                if (unitView != null &&
                    ResourcesCampComponentArtProvider.Shared.TryGetBasicUnitSprite(
                        card.ConfigId,
                        out var basicUnitSprite))
                {
                    unitView.SetCardSprite(basicUnitSprite);
                    unitView.SetCardColor(Color.white);
                }
                else
                {
                    unitView?.SetCardColor(GetRecruitItemColor(card.Kind));
                }
                var stats = BasicUnitCatalog.GetStats(card.ConfigId, card.Level);
                unitView?.ConfigureBasicAttackAnimation(
                    card.ConfigId,
                    ResolveBasicAttackAnimationSpeed(stats));
                unitLabels[card.RuntimeId] = BasicUnitCatalog.GetDisplayName(card.ConfigId);
                unitRangeCells[card.RuntimeId] = stats.RangeCells;
                unitShowsRange[card.RuntimeId] = true;
                unitView?.SetBasicLevel(card.Level);
                return;
            }

            var isHeroComponent = card.Kind == RecruitItemKind.HeroComponent;
            unitView?.SetUnitLabelVisibility(!isHeroComponent);
            unitView?.SetBeachTextVisibility(card.Kind == RecruitItemKind.Shovel, false);

            if (card.Kind == RecruitItemKind.Shovel && TryApplyShovelSprite(unitView))
            {
                // The authored BeachItem is reused by every bench card. Reapply the shovel
                // sprite whenever a shovel occupies the slot so a previous card cannot leak.
            }
            else if (isHeroComponent &&
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
            FlushPendingBasicUnitLevelUpFx();
        }

        private void HandleHeroPairLinked(HeroPairLinkedEvent linked)
        {
            if (linked.PairLink != null)
            {
                PlaySynthesisComponentFade(linked.PairLink.ComponentAId);
                PlaySynthesisComponentFade(linked.PairLink.ComponentBId);
                pendingSynthesisPairIds.Add(linked.PairLink.PairLinkId);
            }

            RefreshUnits();
        }

        private void PlaySynthesisComponentFade(string componentId)
        {
            if (string.IsNullOrEmpty(componentId) ||
                !unitViews.TryGetValue(componentId, out var source) ||
                source == null)
            {
                return;
            }

            var parent = heroEffectLayer != null ? heroEffectLayer : unitLayer;
            if (parent == null)
            {
                return;
            }

            var ghost = source.CreateDragGhost(parent);
            ghost.gameObject.name = $"SynthesisGhost_{componentId}";
            ghost.RectTransform.anchoredPosition = source.RectTransform.anchoredPosition;
            ghost.RectTransform.sizeDelta = source.RectTransform.sizeDelta;
            ghost.RectTransform.localScale = source.RectTransform.localScale;
            ghost.SetDragGhostOpacity(1f);
            synthesisComponentGhosts.Add(ghost);
            StartCoroutine(AnimateSynthesisComponentFade(ghost));
        }

        private IEnumerator AnimateSynthesisComponentFade(DraggableUnitView ghost)
        {
            var rect = ghost != null ? ghost.RectTransform : null;
            var startScale = rect != null ? rect.localScale : Vector3.one;
            var endScale = new Vector3(
                startScale.x * SynthesisComponentEndScale,
                startScale.y * SynthesisComponentEndScale,
                startScale.z);
            var elapsed = 0f;
            while (ghost != null && elapsed < SynthesisComponentFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / SynthesisComponentFadeDuration);
                var eased = progress * progress * (3f - (2f * progress));
                ghost.SetDragGhostOpacity(1f - eased);
                rect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
                yield return null;
            }

            if (ghost != null)
            {
                synthesisComponentGhosts.Remove(ghost);
                Destroy(ghost.gameObject);
            }
        }

        private void StopAllSynthesisComponentFades()
        {
            if (synthesisComponentGhosts.Count == 0)
            {
                return;
            }

            foreach (var ghost in synthesisComponentGhosts)
            {
                if (ghost != null)
                {
                    Destroy(ghost.gameObject);
                }
            }

            synthesisComponentGhosts.Clear();
        }

        private void HandleBowProjectileReleased(string runtimeId)
        {
            BowProjectileReleased?.Invoke(runtimeId);
        }

        private static float ResolveBasicAttackAnimationSpeed(BasicUnitStats stats)
        {
            if (stats.Archetype != BasicUnitArchetype.Bow)
            {
                return 1f;
            }

            const float releaseClipTime = 17f / 60f;
            const float controllerStateSpeed = 0.8f;
            const float timingMargin = 1.05f;
            return Mathf.Max(
                1f,
                releaseClipTime * stats.AttackSpeed / controllerStateSpeed * timingMargin);
        }

        private void HandleBasicUnitLevelChanged(string runtimeId)
        {
            RefreshUnits();
            QueueBasicUnitLevelUpFx(runtimeId);
        }

        private void HandleBasicUnitMerged(BasicUnitMergedEvent merged)
        {
            RefreshUnits();
            QueueBasicUnitLevelUpFx(merged.TargetUnitId);
        }

        private void PlayBasicUnitLevelUpFx(string runtimeId)
        {
            // The level-up effect lives on the surviving card view, so it can only start
            // once the refresh above has (re)bound that unit to a live card instance.
            if (string.IsNullOrWhiteSpace(runtimeId) ||
                !unitViews.TryGetValue(runtimeId, out var unitView) ||
                unitView == null)
            {
                return;
            }

            unitView.PlayLevelUpFx();
        }

        private void QueueBasicUnitLevelUpFx(string runtimeId)
        {
            // The level-up overlay must not start inside the same frame that builds it:
            // CompleteDrag runs a second RefreshUnits right after the merge, which
            // deactivates every authored bench card and would kill the effect coroutine
            // before a single frame of it is ever rendered. Defer to LateUpdate so the
            // refresh settles first.
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return;
            }

            pendingBasicUnitLevelUpIds.Add(runtimeId);
        }

        private void FlushPendingBasicUnitLevelUpFx()
        {
            if (pendingBasicUnitLevelUpIds.Count == 0)
            {
                return;
            }

            foreach (var runtimeId in pendingBasicUnitLevelUpIds)
            {
                PlayBasicUnitLevelUpFx(runtimeId);
            }

            pendingBasicUnitLevelUpIds.Clear();
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
                if (pendingSynthesisPairIds.Remove(pairLink.PairLinkId))
                {
                    var definition = HeroSliceCatalog.Get(pairLink.HeroId);
                    pairView.PlaySynthesisAnimation(definition.Rarity);
                }

                var currentLevel = pairLink.CombatProxy.Level;
                if (observedHeroLevels.TryGetValue(pairLink.PairLinkId, out var previousLevel))
                {
                    if (currentLevel > previousLevel)
                    {
                        pairView.PlayLevelUpAnimation();
                    }

                    observedHeroLevels[pairLink.PairLinkId] = currentLevel;
                }
                else
                {
                    // Initial formation/restored state establishes a baseline and must not
                    // masquerade as a level-up event.
                    observedHeroLevels.Add(pairLink.PairLinkId, currentLevel);
                }
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
                GetHeroRarityColor(definition.Rarity),
                definition.Rarity);
            pairView.SetCombatRuntimeId(pairLink.PairLinkId);
            pairView.FlameDrakeFireballReleased -= HandleFlameDrakeFireballReleased;
            pairView.FlameDrakeFireballReleased += HandleFlameDrakeFireballReleased;
            pairView.SkyborneValkyrieArrowReleased -= HandleSkyborneValkyrieArrowReleased;
            pairView.SkyborneValkyrieArrowReleased += HandleSkyborneValkyrieArrowReleased;
            pairView.StarfallArchmageGemReleased -= HandleStarfallArchmageGemReleased;
            pairView.StarfallArchmageGemReleased += HandleStarfallArchmageGemReleased;
            pairView.NightfangSkillAnimationCompleted -= HandleNightfangSkillAnimationCompleted;
            pairView.NightfangSkillAnimationCompleted += HandleNightfangSkillAnimationCompleted;
            pairView.WindclawSkillReleased -= HandleWindclawSkillReleased;
            pairView.WindclawSkillReleased += HandleWindclawSkillReleased;
            pairView.EmberShamanFireballReleased -= HandleEmberShamanFireballReleased;
            pairView.EmberShamanFireballReleased += HandleEmberShamanFireballReleased;
            pairView.RuneboltMageBoltReleased -= HandleRuneboltMageBoltReleased;
            pairView.RuneboltMageBoltReleased += HandleRuneboltMageBoltReleased;
            pairView.StoneboundWarlockRockReleased -= HandleStoneboundWarlockRockReleased;
            pairView.StoneboundWarlockRockReleased += HandleStoneboundWarlockRockReleased;
            pairView.ThunderlordChainReleased -= HandleThunderlordChainReleased;
            pairView.ThunderlordChainReleased += HandleThunderlordChainReleased;
            pairView.ThunderlordSkillReleased -= HandleThunderlordSkillReleased;
            pairView.ThunderlordSkillReleased += HandleThunderlordSkillReleased;
            pairView.AbyssalHarpoonReleased -= HandleAbyssalHarpoonReleased;
            pairView.AbyssalHarpoonReleased += HandleAbyssalHarpoonReleased;
            pairView.AbyssalHarpoonSkillReleased -= HandleAbyssalHarpoonSkillReleased;
            pairView.AbyssalHarpoonSkillReleased += HandleAbyssalHarpoonSkillReleased;
            pairView.SetHeroAnimation(pairLink.HeroId);
            pairView.SetHeroLevel(combat.Level);
            pairView.InitializeArtFacing(
                board.Side == TeamSide.AI,
                MirroredHeroArtAnchoredPositionX);
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
                pairView.FlameDrakeFireballReleased -= HandleFlameDrakeFireballReleased;
                pairView.SkyborneValkyrieArrowReleased -= HandleSkyborneValkyrieArrowReleased;
                pairView.StarfallArchmageGemReleased -= HandleStarfallArchmageGemReleased;
                pairView.NightfangSkillAnimationCompleted -= HandleNightfangSkillAnimationCompleted;
                pairView.WindclawSkillReleased -= HandleWindclawSkillReleased;
                pairView.EmberShamanFireballReleased -= HandleEmberShamanFireballReleased;
                pairView.RuneboltMageBoltReleased -= HandleRuneboltMageBoltReleased;
                pairView.StoneboundWarlockRockReleased -= HandleStoneboundWarlockRockReleased;
                pairView.ThunderlordChainReleased -= HandleThunderlordChainReleased;
                pairView.ThunderlordSkillReleased -= HandleThunderlordSkillReleased;
                pairView.AbyssalHarpoonReleased -= HandleAbyssalHarpoonReleased;
                pairView.AbyssalHarpoonSkillReleased -= HandleAbyssalHarpoonSkillReleased;
                Destroy(pairView.gameObject);
            }

            pairPresentations.Remove(pairLinkId);
            observedHeroLevels.Remove(pairLinkId);
            pendingSynthesisPairIds.Remove(pairLinkId);
        }

        private void HandleFlameDrakeFireballReleased(string attackerRuntimeId)
        {
            FlameDrakeFireballReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleSkyborneValkyrieArrowReleased(string attackerRuntimeId)
        {
            SkyborneValkyrieArrowReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleStarfallArchmageGemReleased(string attackerRuntimeId)
        {
            StarfallArchmageGemReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleNightfangSkillAnimationCompleted(string attackerRuntimeId)
        {
            NightfangSkillAnimationCompleted?.Invoke(attackerRuntimeId);
        }

        private void HandleWindclawSkillReleased(string attackerRuntimeId)
        {
            WindclawSkillReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleEmberShamanFireballReleased(string attackerRuntimeId)
        {
            EmberShamanFireballReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleRuneboltMageBoltReleased(string attackerRuntimeId)
        {
            RuneboltMageBoltReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleStoneboundWarlockRockReleased(string attackerRuntimeId)
        {
            StoneboundWarlockRockReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleThunderlordChainReleased(string attackerRuntimeId)
        {
            ThunderlordChainReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleThunderlordSkillReleased(string attackerRuntimeId)
        {
            ThunderlordSkillReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleAbyssalHarpoonReleased(string attackerRuntimeId)
        {
            AbyssalHarpoonReleased?.Invoke(attackerRuntimeId);
        }

        private void HandleAbyssalHarpoonSkillReleased(string attackerRuntimeId)
        {
            AbyssalHarpoonSkillReleased?.Invoke(attackerRuntimeId);
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
            if (mutation.Kind == GridMutationKind.CellUnlocked &&
                cells.TryGetValue(mutation.To, out var unlockedCell) &&
                unlockedCell != null)
            {
                // Capture the locked art before the refresh switches it, then let the wipe
                // erase the locked copy diagonally while the unlocked art shows underneath.
                CellUnlockWipeView.Play(unlockedCell);
                RefreshCellStates();
                return;
            }

            RefreshCellStates();
        }

        private void HandleShovelUsed(GridPosition position)
        {
            if (!cells.TryGetValue(position, out var cell) || cell == null)
            {
                return;
            }

            // The dig ghost must live on an effect layer, never on the bench card: the unlock
            // refreshes the bench right after, which would destroy a card-attached ghost.
            var layer = heroEffectLayer != null ? heroEffectLayer : unitLayer;
            if (layer == null)
            {
                return;
            }

            ShovelDigEffectView.Play(layer, cell.RectTransform);
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
                ? screen.transform.FindUi("ART_ScreenBackground/BeachContainer")
                : null;
            if (beachContainer == null)
            {
                return;
            }

            var beachItemPrefab = DragonBound.Presentation.UiAssets.Load<GameObject>("prefabs/BeachItem");
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

                var beachItem = slot.FindUi("BeachItem");
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

                var nameTransform = beachItem.FindUi("Text (TMP)");
                var levelTransform = beachItem.FindUi("Text");
                var artTransform = beachItem.FindUi("Image");
                itemView.ConfigureBeach(
                    artTransform != null ? artTransform.GetComponent<Image>() : null,
                    nameTransform != null ? nameTransform.GetComponent<Graphic>() : null,
                    levelTransform != null ? levelTransform.GetComponent<Graphic>() : null,
                    canvasGroup);
                TryApplyShovelSprite(itemView);
                beachItem.gameObject.SetActive(false);

                cell.Configure(0, 0, CellType.Bench, slot.GetComponent<Image>(), slot);
                beachItemViewsByCell[cell] = itemView;
                authoredBeachItemViews.Add(itemView);
                benchCells.Add(cell);
            }
        }

        private static bool TryApplyShovelSprite(DraggableUnitView itemView)
        {
            if (itemView == null)
            {
                return false;
            }

            var shovelSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(ShovelSpriteResourcePath);
            if (shovelSprite == null)
            {
                return false;
            }

            itemView.SetCardSprite(shovelSprite);
            itemView.SetCardColor(Color.white);
            return true;
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

        private void BindBeachSelection(GridPosition position, GridCellView cellView)
        {
            if (cellView == null)
            {
                return;
            }

            EnsureSelectionSprites();
            var selection = cellView.transform.FindUi("Select")?.gameObject;
            if (selection == null)
            {
                selection = Instantiate(RequireBoardSelectPrefab(), cellView.transform, false);
                selection.name = "Select";
            }

            ApplySelectionSprite(selection, beachSelectSprite);
            StretchToParent(selection);
            DisableGraphicRaycasts(selection);
            selection.transform.SetAsLastSibling();
            selection.SetActive(false);
            beachSelectionViews[position] = selection;
        }

        private void ShowDragSourceSelection(string unitId)
        {
            if (board == null ||
                !board.TryGetPosition(unitId, out var origin) ||
                !cells.TryGetValue(origin, out var sourceCell) ||
                sourceCell == null ||
                sourceCell.ContentAnchor == null)
            {
                return;
            }

            EnsureSelectionSprites();
            if (beachItemViews.ContainsKey(origin) &&
                beachSelectionViews.TryGetValue(origin, out var beachSelection) &&
                beachSelection != null)
            {
                activeBeachDragOrigin = origin;
                ApplySelectionSprite(beachSelection, beachSelectSprite);
                beachSelection.SetActive(true);
                return;
            }

            if (sourceSelectPreview == null)
            {
                sourceSelectPreview = Instantiate(
                    RequireBoardSelectPrefab(),
                    sourceCell.ContentAnchor,
                    false);
                sourceSelectPreview.name = "BeachSourceSelect";
                DisableGraphicRaycasts(sourceSelectPreview);
            }
            else if (sourceSelectPreview.transform.parent != sourceCell.ContentAnchor)
            {
                sourceSelectPreview.transform.SetParent(sourceCell.ContentAnchor, false);
            }

            ApplySelectionSprite(sourceSelectPreview, beachSelectSprite);
            StretchToParent(sourceSelectPreview);
            sourceSelectPreview.transform.SetAsLastSibling();
            sourceSelectPreview.SetActive(true);
        }

        private void UpdateShovelDragPreview(Vector2 screenPosition)
        {
            if (!activeBeachDragOrigin.HasValue ||
                !cells.TryGetValue(activeBeachDragOrigin.Value, out var sourceCell) ||
                !TryGetPositionAt(screenPosition, out var target) ||
                !cells.TryGetValue(target, out var targetCell) ||
                !board.TryGetCellType(target, out var targetType) ||
                targetType != CellType.Locked ||
                (board.Layout != null && !board.Layout.IsUnlockable(target, board.Side)))
            {
                HideDragArrow();
                HideBoardSelect();
                return;
            }

            var parent = fixedBoardCanvas != null
                ? fixedBoardCanvas.EnsureDeploymentGuideLayer()
                : dragArrowPreview != null
                    ? dragArrowPreview.transform.parent as RectTransform
                    : null;
            if (dragArrowPreview != null && parent != null && dragArrowPreview.transform.parent != parent)
            {
                dragArrowPreview.transform.SetParent(parent, false);
            }
            dragArrowPreview?.Show(
                parent,
                sourceCell.ContentAnchor.position,
                targetCell.ContentAnchor.position);
            ShowBoardSelect(targetCell);
        }

        private void ShowBoardSelect(GridCellView targetCell)
        {
            if (targetCell == null || targetCell.ContentAnchor == null)
            {
                HideBoardSelect();
                return;
            }

            EnsureSelectionSprites();
            if (boardSelectPreview == null)
            {
                boardSelectPreview = Instantiate(
                    RequireBoardSelectPrefab(),
                    targetCell.ContentAnchor,
                    false);
                boardSelectPreview.name = "BeachSelect";
                DisableGraphicRaycasts(boardSelectPreview);
            }
            else if (boardSelectPreview.transform.parent != targetCell.ContentAnchor)
            {
                boardSelectPreview.transform.SetParent(targetCell.ContentAnchor, false);
            }

            ApplySelectionSprite(boardSelectPreview, boardSelectSprite);
            StretchToParent(boardSelectPreview);
            boardSelectPreview.transform.SetAsLastSibling();
            boardSelectPreview.SetActive(true);
        }

        private void ShowDeploymentRangePreview(string unitId, GridCellView targetCell)
        {
            if (!activeBeachDragOrigin.HasValue ||
                targetCell == null ||
                targetCell.CellType != CellType.Battle ||
                !unitShowsRange.TryGetValue(unitId, out var showRange) ||
                !showRange ||
                !unitRangeCells.TryGetValue(unitId, out var radiusCells) ||
                radiusCells <= 0f)
            {
                HideDeploymentRangePreview();
                return;
            }

            ShowRange(
                targetCell.ContentAnchor.position,
                targetCell,
                null,
                radiusCells);
        }

        private void HideDeploymentRangePreview()
        {
            if (activeBeachDragOrigin.HasValue)
            {
                SetRangePreviewVisible(false);
            }
        }

        private void HideBoardSelect()
        {
            if (boardSelectPreview != null)
            {
                boardSelectPreview.SetActive(false);
            }
        }

        private void ClearBeachDragPreview()
        {
            foreach (var selection in beachSelectionViews.Values)
            {
                if (selection != null)
                {
                    selection.SetActive(false);
                }
            }

            activeBeachDragOrigin = null;
            if (sourceSelectPreview != null)
            {
                sourceSelectPreview.SetActive(false);
            }
            HideBoardSelect();
            SetRangePreviewVisible(false);
        }

        private GameObject RequireBoardSelectPrefab()
        {
            if (boardSelectPrefab == null)
            {
                boardSelectPrefab = DragonBound.Presentation.UiAssets.Load<GameObject>(BoardSelectPrefabResourcePath);
            }

            if (boardSelectPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Missing board selection prefab at Resources/{BoardSelectPrefabResourcePath}.");
            }

            return boardSelectPrefab;
        }

        private void EnsureSelectionSprites()
        {
            if (beachSelectSprite == null)
            {
                beachSelectSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(BeachSelectSpriteResourcePath);
            }

            if (boardSelectSprite == null)
            {
                boardSelectSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(BoardSelectSpriteResourcePath);
            }

            if (beachSelectSprite == null || boardSelectSprite == null)
            {
                throw new InvalidOperationException(
                    "Missing BeachSelect or BoardSelect sprite in Resources/GameUI.");
            }
        }

        private static void ApplySelectionSprite(GameObject selection, Sprite sprite)
        {
            if (selection == null || sprite == null)
            {
                return;
            }

            var image = selection.GetComponent<Image>();
            if (image == null)
            {
                image = selection.GetComponentInChildren<Image>(true);
            }

            if (image == null)
            {
                throw new InvalidOperationException(
                    $"{selection.name} requires an Image component for its selection sprite.");
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static void StretchToParent(GameObject value)
        {
            if (value == null || !(value.transform is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void DisableGraphicRaycasts(GameObject value)
        {
            foreach (var graphic in value.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private void CancelActiveDrag(bool refreshView)
        {
            stableDragTarget = null;
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
            ClearBeachDragPreview();
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
            var found = false;
            var nearestDistance = float.PositiveInfinity;
            var nearestPosition = default(GridPosition);
            var eventCamera = GetEventCamera();
            foreach (var entry in cells)
            {
                var cellRect = entry.Value != null ? entry.Value.RectTransform : null;
                if (cellRect == null ||
                    !RectTransformUtility.RectangleContainsScreenPoint(
                        cellRect,
                        screenPosition,
                        eventCamera))
                {
                    continue;
                }

                var center = RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    cellRect.TransformPoint(cellRect.rect.center));
                var distance = (center - screenPosition).sqrMagnitude;
                if (!found ||
                    distance < nearestDistance - Mathf.Epsilon ||
                    (Mathf.Approximately(distance, nearestDistance) &&
                     entry.Key.CompareTo(nearestPosition) < 0))
                {
                    found = true;
                    nearestDistance = distance;
                    nearestPosition = entry.Key;
                }
            }

            position = nearestPosition;
            return found;
        }

        private bool TryGetStableDragPositionAt(Vector2 screenPosition, out GridPosition position)
        {
            if (!TryGetPositionAt(screenPosition, out var candidate))
            {
                stableDragTarget = null;
                position = default;
                return false;
            }

            var eventCamera = GetEventCamera();
            if (stableDragTarget.HasValue &&
                stableDragTarget.Value != candidate &&
                cells.TryGetValue(stableDragTarget.Value, out var previousCell) &&
                previousCell != null &&
                cells.TryGetValue(candidate, out var candidateCell) &&
                candidateCell != null)
            {
                var previousCenter = RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    previousCell.RectTransform.TransformPoint(previousCell.RectTransform.rect.center));
                var candidateCenter = RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    candidateCell.RectTransform.TransformPoint(candidateCell.RectTransform.rect.center));
                var previousDistance = Vector2.Distance(screenPosition, previousCenter);
                var candidateDistance = Vector2.Distance(screenPosition, candidateCenter);
                if (previousDistance <= candidateDistance + DragTargetSwitchHysteresisPixels)
                {
                    position = stableDragTarget.Value;
                    return true;
                }
            }

            stableDragTarget = candidate;
            position = candidate;
            return true;
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
