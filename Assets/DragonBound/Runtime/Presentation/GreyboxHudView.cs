using DragonBound.Core;
using DragonBound.Presentation;
using DragonBound.Combat;
using DragonBound.Bosses.Runtime;
using DragonBound.Items;
using DragonBound.Recruitment;
using DragonBound.Runes;
using DragonBound.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public class GreyboxHudView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int ActiveItemSortingOrder = 105;
        private const int PauseButtonSortingOrder = 110;
        private const int PausePanelSortingOrder = 120;
        private const int BossWarningSortingOrder = 125;
        private const int SettlementPanelSortingOrder = 130;
        private const string ArcaneThunderburstControllerPath = "Animation/Arcane Thunderburst";
        private const float ArcaneThunderburstPlaybackSpeed = 0.2f;
        private const float ArcaneThunderburstImpactFrame = 2f;
        private const string SettlementVictorySpritePath = "GameUI/SettlementUI/Victory";
        private const string SettlementDefeatSpritePath = "GameUI/SettlementUI/Defeat";
        private const int RuntimeCircleTextureSize = 64;
        private static Sprite runtimeCircleSprite;
        private static readonly Dictionary<string, Sprite> ItemIconCache =
            new Dictionary<string, Sprite>();
        private static readonly HashSet<string> MissingItemIconKeys =
            new HashSet<string>();

        [SerializeField] private Button pauseButton;
        [SerializeField] private Text pauseLabel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button finishMatchButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject bossWarning;
        [SerializeField] private Button bossWarningConfirmButton;
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private Image settlementResultImage;
        [SerializeField] private Text resourceLabel;
        [SerializeField] private Text waveLabel;
        [SerializeField] private Text debugLabel;
        [SerializeField] private Text enemyDebugLabel;
        [SerializeField] private Button activeItemSlotOne;
        [SerializeField] private Button activeItemSlotTwo;
        [SerializeField] private Text activeItemSlotOneLabel;
        [SerializeField] private Text activeItemSlotTwoLabel;
        [SerializeField] private Image activeItemSlotOneCooldownMask;
        [SerializeField] private Image activeItemSlotTwoCooldownMask;
        [SerializeField] private RectTransform activeItemContainer;
        [SerializeField] private bool showDebugOverlay;

        private MatchController match;
        private TeamState team;
        private RecruitmentService playerRecruitment;
        private RecruitmentService aiRecruitment;
        private BoardRecruitDestination aiRecruitDestination;
        private MatchState stateBeforePause = MatchState.Preparing;
        private float timeScaleBeforePause = 1f;
        private bool ownsGlobalPause;
        private bool ownsBossWarningPause;
        private float timeScaleBeforeBossWarning = 1f;
        private bool pauseExitRequested;
        private IWaveRuntime waveRuntime;
        private TwentyWavePressureRuntime itemRuntime;
        // Passive items share the authored radial masks during the opening preparation window.
        private readonly Image[] passiveItemCooldownMasks = new Image[6];
        private readonly Transform[] passiveItemSlots = new Transform[6];
        private readonly Coroutine[] passiveItemConsumedEffects = new Coroutine[6];
        private readonly bool[] passiveItemConsumedVisualized = new bool[6];
        private readonly Dictionary<Image, Sprite> itemSlotFallbackSprites =
            new Dictionary<Image, Sprite>();
        private readonly Dictionary<Image, Color> itemSlotFallbackColors =
            new Dictionary<Image, Color>();
        private int activeDragSlot = -1;
        private string activeDragItemId;
        private RectTransform activeItemDragRoot;
        private RectTransform activeItemDragGhost;
        private Image activeItemAreaPreview;
        private bool activeItemDropIsOnPath;
        private bool activeItemDropIsValid;
        private CombatPoint activeItemDropPoint;
        private Vector2 activeItemDropScreenPosition;
        private float activeItemDropPixelsPerCell;
        private string activeItemDropTargetId;
        private readonly HashSet<string> arcaneThunderburstHeldEnemyIds =
            new HashSet<string>();
        private readonly HashSet<GameObject> activeArcaneThunderburstVfxRoots =
            new HashSet<GameObject>();
        private int suppressedClickSlot = -1;
        private Coroutine clearClickSuppressionCoroutine;
        private PauseRuneRewardPresenter pauseRuneRewardPresenter;
        private readonly Dictionary<Button, List<EventTrigger.Entry>> activeItemDragEntries =
            new Dictionary<Button, List<EventTrigger.Entry>>();

        public bool HasActiveItemDragVisual => activeItemDragRoot != null;

        public event System.Action PauseExitRequested;

        public void Configure(
            Button pause,
            Text pauseText,
            Text resources,
            Text wave,
            Text debug = null,
            Text enemyDebug = null)
        {
            pauseButton = pause;
            pauseLabel = pauseText;
            resourceLabel = resources;
            waveLabel = wave;
            debugLabel = debug;
            enemyDebugLabel = enemyDebug;
        }

        public void Initialize(
            MatchController value,
            TeamState playerTeam,
            RecruitmentService playerRecruitment = null,
            RecruitmentService aiRecruitment = null,
            BoardRecruitDestination playerRecruitDestination = null,
            BoardRecruitDestination aiRecruitDestination = null)
        {
            match = value;
            team = playerTeam;
            this.playerRecruitment = playerRecruitment;
            this.aiRecruitment = aiRecruitment;
            this.aiRecruitDestination = aiRecruitDestination;
            ResolveAuthoredScreenControls();
            if (pauseButton == null)
            {
                throw new System.InvalidOperationException(
                    "ART_ScreenBackground/ART_PauseButton is missing from DragonBoundPortraitScreen.");
            }

            pauseButton.onClick.RemoveListener(PauseGameFromButton);
            pauseButton.onClick.AddListener(PauseGameFromButton);
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(ResumeGame);
                continueButton.onClick.AddListener(ResumeGame);
            }
            if (finishMatchButton != null)
            {
                finishMatchButton.onClick.RemoveListener(ExitPausedMatch);
                finishMatchButton.onClick.AddListener(ExitPausedMatch);
            }
            if (bossWarningConfirmButton != null)
            {
                bossWarningConfirmButton.onClick.RemoveListener(ConfirmBossWarning);
                bossWarningConfirmButton.onClick.AddListener(ConfirmBossWarning);
            }
            if (bossWarning != null && match.State != MatchState.BossPrompt)
            {
                bossWarning.SetActive(false);
            }
            if (pausePanel != null && match.State != MatchState.Paused)
            {
                pausePanel.SetActive(false);
            }
            match.StateChanged -= HandleMatchStateChanged;
            match.StateChanged += HandleMatchStateChanged;
            if (settlementPanel != null &&
                match.State != MatchState.Victory && match.State != MatchState.Defeat)
            {
                settlementPanel.SetActive(false);
            }
            Refresh();
        }

        public void BindWaveRuntime(IWaveRuntime runtime)
        {
            waveRuntime = runtime;
            Refresh();
        }

        public void BindRuneRewardServices(
            RuneRunRewardService playerRewards,
            RuneRunRewardService aiRewards)
        {
            if (pauseRuneRewardPresenter == null && pausePanel != null)
            {
                pauseRuneRewardPresenter =
                    pausePanel.GetComponent<PauseRuneRewardPresenter>() ??
                    pausePanel.AddComponent<PauseRuneRewardPresenter>();
            }

            pauseRuneRewardPresenter?.Bind(playerRewards, aiRewards);
        }

        public void BindItemRuntime(TwentyWavePressureRuntime runtime)
        {
            if (itemRuntime != null)
            {
                itemRuntime.BossWarningRequested -= HandleBossWarningRequested;
            }
            ResetPassiveItemConsumedEffects();
            itemRuntime = runtime;
            if (itemRuntime != null)
            {
                if (bossWarning != null && bossWarningConfirmButton != null)
                {
                    itemRuntime.BossWarningRequested += HandleBossWarningRequested;
                }
            }
            EnsureActiveItemSlots();
            Refresh();
        }

        // Public configuration keeps the existing prefab optional while allowing a scene to
        // provide authored placeholder controls later without changing the command contract.
        public void ConfigureActiveItemSlots(Button first, Text firstLabel, Button second, Text secondLabel)
        {
            RemoveActiveItemListeners();
            activeItemSlotOne = first;
            activeItemSlotOneLabel = firstLabel;
            activeItemSlotTwo = second;
            activeItemSlotTwoLabel = secondLabel;
            HideObsoleteItemLabel(activeItemSlotOneLabel);
            HideObsoleteItemLabel(activeItemSlotTwoLabel);
            activeItemSlotOneCooldownMask = EnsureCooldownMask(first != null ? first.transform : null);
            activeItemSlotTwoCooldownMask = EnsureCooldownMask(second != null ? second.transform : null);
            AddActiveItemListeners();
        }

        public void SetDebugOverlayVisible(bool visible)
        {
            showDebugOverlay = visible;
            Refresh();
        }

        protected virtual void LateUpdate()
        {
            Refresh();
        }

        protected virtual void OnDestroy()
        {
            if (match != null)
            {
                match.StateChanged -= HandleMatchStateChanged;
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(PauseGameFromButton);
            }
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(ResumeGame);
            }
            if (finishMatchButton != null)
            {
                finishMatchButton.onClick.RemoveListener(ExitPausedMatch);
            }
            if (bossWarningConfirmButton != null)
            {
                bossWarningConfirmButton.onClick.RemoveListener(ConfirmBossWarning);
            }
            if (itemRuntime != null)
            {
                itemRuntime.BossWarningRequested -= HandleBossWarningRequested;
            }

            ReleaseGlobalPause();
            ReleaseBossWarningPause();

            RemoveActiveItemListeners();
            ReleaseArcaneThunderburstEnemyVisuals();
            foreach (var effectRoot in activeArcaneThunderburstVfxRoots)
            {
                if (effectRoot != null)
                {
                    Destroy(effectRoot);
                }
            }
            activeArcaneThunderburstVfxRoots.Clear();
            CancelActiveItemDrag();
        }

        private void PauseGameFromButton()
        {
            if (match.State == MatchState.Ready ||
                match.State == MatchState.Preparing ||
                match.State == MatchState.Running ||
                match.State == MatchState.BossPrompt)
            {
                PauseGame();
            }

            Refresh();
        }

        private void ResolveAuthoredScreenControls()
        {
            var screen = GetComponentInParent<DragonBoundScreenView>();
            var background = screen != null
                ? screen.transform.FindUi("ART_ScreenBackground")
                : null;
            if (background == null)
            {
                return;
            }

            ResolvePassiveItemCooldownMasks(screen.transform);

            var authoredItemContainer = screen.transform.FindUi("ItemContainer");
            if (authoredItemContainer == null)
            {
                foreach (var candidate in screen.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == "ItemContainer")
                    {
                        authoredItemContainer = candidate;
                        break;
                    }
                }
            }
            var authoredActiveContainer = authoredItemContainer?.FindUi("Active");
            if (authoredActiveContainer != null)
            {
                activeItemSlotOne =
                    authoredActiveContainer.FindUi("Active")?.GetComponent<Button>() ??
                    authoredActiveContainer.FindUi("Active0")?.GetComponent<Button>();
                activeItemSlotTwo =
                    authoredActiveContainer.FindUi("Active1")?.GetComponent<Button>();
                var activeButtons = authoredActiveContainer.GetComponentsInChildren<Button>(true);
                if (activeItemSlotOne == null && activeButtons.Length > 0)
                {
                    activeItemSlotOne = activeButtons[0];
                }
                if (activeItemSlotTwo == null && activeButtons.Length > 1)
                {
                    activeItemSlotTwo = activeButtons[1];
                }
                activeItemContainer = authoredActiveContainer as RectTransform;
            }

            if (activeItemContainer == null)
            {
                activeItemContainer = background.FindUi("ActiveItemContainer") as RectTransform;
            }
            if (activeItemContainer == null)
            {
                activeItemContainer = background as RectTransform;
            }

            var authoredResourceLabel = background.FindUi("ResourceLabel")?.GetComponent<Text>();
            if (authoredResourceLabel != null)
            {
                resourceLabel = authoredResourceLabel;
            }

            var authoredWaveLabel = background.FindUi("WaveLabel")?.GetComponent<Text>();
            if (authoredWaveLabel != null)
            {
                waveLabel = authoredWaveLabel;
            }

            var authoredButton = background.FindUi("ART_PauseButton")?.GetComponent<Button>();
            if (authoredButton != null)
            {
                pauseButton = authoredButton;
                pauseLabel = authoredButton.transform.FindUi("PauseLabel")?.GetComponent<Text>();
                EnsureOverlayCanvas(authoredButton.gameObject, PauseButtonSortingOrder, true);
            }

            // Current authored hierarchy keeps PausePanel beside ART_ScreenBackground.
            // Retain the old nested lookup so older screen prefabs remain compatible.
            var authoredPanel = screen.transform.FindUi("PausePanel") ??
                                background.FindUi("PausePanel");
            if (authoredPanel != null)
            {
                pausePanel = authoredPanel.gameObject;
                pauseRuneRewardPresenter =
                    pausePanel.GetComponent<PauseRuneRewardPresenter>() ??
                    pausePanel.AddComponent<PauseRuneRewardPresenter>();
                finishMatchButton = authoredPanel.FindUi("Bg/PauseBtn")?.GetComponent<Button>();
                continueButton = authoredPanel.FindUi("Bg/ContinueBtn")?.GetComponent<Button>();
                EnsureOverlayCanvas(pausePanel, PausePanelSortingOrder, true);
            }

            var authoredSettlement = screen.transform.FindUi("SettlementPanel");
            if (authoredSettlement != null)
            {
                settlementPanel = authoredSettlement.gameObject;
                settlementResultImage = authoredSettlement.FindUi("SettleImg")?.GetComponent<Image>();
                EnsureOverlayCanvas(settlementPanel, SettlementPanelSortingOrder, true);
            }

            var authoredBossWarning = screen.transform.FindUi("BossWarning");
            if (authoredBossWarning != null)
            {
                bossWarning = authoredBossWarning.gameObject;
                bossWarningConfirmButton = authoredBossWarning.FindUi("ConfirmBtn")?.GetComponent<Button>();
                EnsureOverlayCanvas(bossWarning, BossWarningSortingOrder, true);
            }

            var debugRoot = background.FindUi("Debug");
            if (debugRoot != null)
            {
                var authoredDebugLabel = debugRoot.FindUi("DebugLabel")?.GetComponent<Text>();
                if (authoredDebugLabel != null)
                {
                    debugLabel = authoredDebugLabel;
                }

                var authoredEnemyDebugLabel = debugRoot.FindUi("EnemyDebugLabel")?.GetComponent<Text>();
                if (authoredEnemyDebugLabel != null)
                {
                    enemyDebugLabel = authoredEnemyDebugLabel;
                }
            }
        }

        private static void EnsureOverlayCanvas(GameObject target, int sortingOrder, bool needsRaycaster)
        {
            if (target == null)
            {
                return;
            }

            var canvas = target.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = target.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (needsRaycaster && target.GetComponent<GraphicRaycaster>() == null)
            {
                target.AddComponent<GraphicRaycaster>();
            }
        }

        private void PauseGame()
        {
            var previousState = match.State;
            if (!match.TryTransition(MatchState.Paused))
            {
                return;
            }

            stateBeforePause = previousState;
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            ownsGlobalPause = true;
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
                pausePanel.transform.SetAsLastSibling();
                pauseRuneRewardPresenter?.RefreshRewards();
            }
        }

        private void ResumeGame()
        {
            if (match == null || match.State != MatchState.Paused)
            {
                return;
            }

            if (!match.TryTransition(stateBeforePause))
            {
                return;
            }

            ReleaseGlobalPause();
            Refresh();
        }

        private void HandleBossWarningRequested(int wave)
        {
            if (match == null || match.State != MatchState.BossPrompt || bossWarning == null)
            {
                return;
            }

            timeScaleBeforeBossWarning = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            ownsBossWarningPause = true;
            bossWarning.SetActive(true);
            bossWarning.transform.SetAsLastSibling();
            if (bossWarningConfirmButton != null)
            {
                bossWarningConfirmButton.interactable = true;
            }
            Refresh();
        }

        private void ConfirmBossWarning()
        {
            if (itemRuntime == null || !itemRuntime.ConfirmBossWarning())
            {
                return;
            }

            if (bossWarningConfirmButton != null)
            {
                bossWarningConfirmButton.interactable = false;
            }
            ReleaseBossWarningPause();
            Refresh();
        }

        private void ReleaseBossWarningPause()
        {
            if (ownsBossWarningPause)
            {
                // A separate player pause owns Time.timeScale while MatchState is Paused.
                if (match == null || match.State != MatchState.Paused)
                {
                    Time.timeScale = timeScaleBeforeBossWarning;
                }
                ownsBossWarningPause = false;
            }

            if (bossWarning != null)
            {
                bossWarning.SetActive(false);
            }
        }

        private void ExitPausedMatch()
        {
            if (match == null || match.State != MatchState.Paused || pauseExitRequested)
            {
                return;
            }

            if (PauseExitRequested == null)
            {
                Debug.LogError("Pause exit requires a reward/scene-transition handler.");
                return;
            }

            pauseExitRequested = true;
            if (finishMatchButton != null) finishMatchButton.interactable = false;
            if (continueButton != null) continueButton.interactable = false;
            PauseExitRequested.Invoke();
        }

        public void CancelPauseExitRequest()
        {
            if (match == null || match.State != MatchState.Paused)
            {
                return;
            }

            pauseExitRequested = false;
            if (finishMatchButton != null) finishMatchButton.interactable = true;
            if (continueButton != null) continueButton.interactable = true;
        }

        private void HandleMatchStateChanged(MatchState state)
        {
            if (state != MatchState.Victory && state != MatchState.Defeat)
            {
                return;
            }

            // A terminal match state already stops the combat runtimes. Release a possible
            // pause-owned time scale so the settlement UI and following scene remain healthy.
            ReleaseGlobalPause();
            ReleaseBossWarningPause();
            ApplySettlementResultImage(state);
            if (settlementPanel != null)
            {
                SetSettlementChildActive("GoldText", false);
                SetSettlementChildActive("ReciveBtn", false);
                SetSettlementChildActive("DoubleBtn", false);
                settlementPanel.SetActive(true);
                settlementPanel.transform.SetAsLastSibling();
            }
            Refresh();
        }

        private void ApplySettlementResultImage(MatchState state)
        {
            if (settlementResultImage == null)
            {
                Debug.LogError("SettlementPanel/SettleImg with an Image component is missing.");
                return;
            }

            var resourcePath = state == MatchState.Victory
                ? SettlementVictorySpritePath
                : SettlementDefeatSpritePath;
            var sprite = DragonBound.Presentation.UiAssets.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Debug.LogError("Settlement result sprite is missing at Resources/" + resourcePath + ".png");
                return;
            }

            settlementResultImage.sprite = sprite;
            settlementResultImage.preserveAspect = true;
            settlementResultImage.gameObject.SetActive(true);
        }

        private void SetSettlementChildActive(string childName, bool active)
        {
            Transform child = settlementPanel != null
                ? settlementPanel.transform.FindUi(childName)
                : null;
            if (child != null) child.gameObject.SetActive(active);
        }

        private void ReleaseGlobalPause()
        {
            if (ownsGlobalPause)
            {
                Time.timeScale = timeScaleBeforePause;
                ownsGlobalPause = false;
                if (match != null && match.State == MatchState.Paused)
                {
                    match.TryTransition(stateBeforePause);
                }
            }

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        private void Refresh()
        {
            if (match == null || team == null)
            {
                return;
            }

            resourceLabel.text = team.Resources.ToString();
            if (match.State == MatchState.Initializing)
            {
                waveLabel.text = "INITIALIZING...";
            }
            else if (match.State == MatchState.Ready)
            {
                waveLabel.text = "READY\nWAITING";
            }
            else if (waveRuntime != null && match.State == MatchState.Running)
            {
                waveLabel.text =
                    $"WAVE {waveRuntime.CurrentWave}\n{Mathf.CeilToInt(waveRuntime.WaveRemainingSeconds):00}s";
            }
            else
            {
                waveLabel.text = match.State.ToString().ToUpperInvariant();
            }
            if (debugLabel != null)
            {
                debugLabel.gameObject.SetActive(showDebugOverlay);
                if (showDebugOverlay)
                {
                    debugLabel.text =
                    $"AI Supplies: {match.AI.Resources}   AI Recruit Count: {match.AI.RecruitmentCount}\n" +
                    $"AI Camp Count: {GetCampCount(aiRecruitDestination)}   " +
                    $"AI Deployed Count: {GetDeployedCount(aiRecruitDestination)}\n" +
                    $"AI Last Recruit Result: {GetLastResult(aiRecruitment)}\n" +
                    $"Player Last Recruit Result: {GetLastResult(playerRecruitment)}\n" +
                    $"Player Components Remaining: {GetRemainingHeroComponents(playerRecruitment)}   " +
                    $"AI Components Remaining: {GetRemainingHeroComponents(aiRecruitment)}\n" +
                    $"State: {match.State}";
                }
            }

            if (enemyDebugLabel != null)
            {
                enemyDebugLabel.gameObject.SetActive(showDebugOverlay);
                if (showDebugOverlay && waveRuntime != null)
                {
                    enemyDebugLabel.text =
                        FormatEnemies("AI", waveRuntime.AiEnemyRegistry) + "\n" +
                        FormatEnemies("PLAYER", waveRuntime.PlayerEnemyRegistry);
                }
            }
            if (pauseButton != null)
            {
                pauseButton.interactable = match.State != MatchState.Paused &&
                                           match.State != MatchState.BossPrompt;
            }
            RefreshActiveItemSlots();
        }

        private void EnsureActiveItemSlots()
        {
            if (activeItemSlotOne != null && activeItemSlotTwo != null)
            {
                activeItemSlotOneCooldownMask = EnsureCooldownMask(activeItemSlotOne.transform);
                activeItemSlotTwoCooldownMask = EnsureCooldownMask(activeItemSlotTwo.transform);
                AddActiveItemListeners();
                return;
            }

            CreateActiveItemSlot("ActiveItemSlot1", 0, out activeItemSlotOne, out activeItemSlotOneLabel);
            CreateActiveItemSlot("ActiveItemSlot2", 1, out activeItemSlotTwo, out activeItemSlotTwoLabel);
            AddActiveItemListeners();
        }

        private void CreateActiveItemSlot(string slotName, int index, out Button button, out Text label)
        {
            var slot = new GameObject(slotName, typeof(RectTransform), typeof(Image), typeof(Button));
            var parent = activeItemContainer != null ? activeItemContainer : transform;
            slot.transform.SetParent(parent, false);
            var rect = slot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.02f + index * 0.25f, 0.03f);
            rect.anchorMax = new Vector2(0.25f + index * 0.25f, 0.10f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = slot.GetComponent<Image>();
            image.color = new Color(0.16f, 0.20f, 0.24f, 0.9f);
            button = slot.GetComponent<Button>();
            button.targetGraphic = image;
            EnsureOverlayCanvas(slot, ActiveItemSortingOrder + index, true);
            label = null;
            var cooldownMask = EnsureCooldownMask(slot.transform);
            cooldownMask.transform.SetAsLastSibling();
        }

        private void AddActiveItemListeners()
        {
            if (activeItemSlotOne != null)
            {
                activeItemSlotOne.onClick.RemoveListener(UseFirstActiveItem);
                activeItemSlotOne.onClick.AddListener(UseFirstActiveItem);
            }
            if (activeItemSlotTwo != null)
            {
                activeItemSlotTwo.onClick.RemoveListener(UseSecondActiveItem);
                activeItemSlotTwo.onClick.AddListener(UseSecondActiveItem);
            }
            AddActiveItemDragTriggers(activeItemSlotOne);
            AddActiveItemDragTriggers(activeItemSlotTwo);
        }

        private void RemoveActiveItemListeners()
        {
            activeItemSlotOne?.onClick.RemoveListener(UseFirstActiveItem);
            activeItemSlotTwo?.onClick.RemoveListener(UseSecondActiveItem);
            RemoveActiveItemDragTriggers();
        }

        private void AddActiveItemDragTriggers(Button button)
        {
            if (button == null || activeItemDragEntries.ContainsKey(button))
            {
                return;
            }

            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }
            if (trigger.triggers == null)
            {
                trigger.triggers = new List<EventTrigger.Entry>();
            }

            var entries = new List<EventTrigger.Entry>
            {
                CreateDragTrigger(EventTriggerType.BeginDrag, data => OnBeginDrag((PointerEventData)data)),
                CreateDragTrigger(EventTriggerType.Drag, data => OnDrag((PointerEventData)data)),
                CreateDragTrigger(EventTriggerType.EndDrag, data => OnEndDrag((PointerEventData)data))
            };
            for (var index = 0; index < entries.Count; index++)
            {
                trigger.triggers.Add(entries[index]);
            }
            activeItemDragEntries.Add(button, entries);
        }

        private static EventTrigger.Entry CreateDragTrigger(
            EventTriggerType eventType,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(callback);
            return entry;
        }

        private void RemoveActiveItemDragTriggers()
        {
            foreach (var pair in activeItemDragEntries)
            {
                var trigger = pair.Key != null ? pair.Key.GetComponent<EventTrigger>() : null;
                if (trigger == null || trigger.triggers == null)
                {
                    continue;
                }
                for (var index = 0; index < pair.Value.Count; index++)
                {
                    trigger.triggers.Remove(pair.Value[index]);
                }
            }
            activeItemDragEntries.Clear();
        }

        private void UseFirstActiveItem() { TryUseActiveItem(0); }
        private void UseSecondActiveItem() { TryUseActiveItem(1); }

        private void TryUseActiveItem(int slot)
        {
            if (suppressedClickSlot == slot)
            {
                suppressedClickSlot = -1;
                return;
            }

            var snapshot = itemRuntime?.PlayerItems?.Snapshot;
            if (snapshot == null || slot < 0 || slot >= snapshot.ActiveItems.Count)
            {
                return;
            }

            if (IsPathPositionedItem(snapshot.ActiveItems[slot]))
            {
                ShowTip("Drag onto enemy path");
                return;
            }

            if (IsEnemyTargetedItem(snapshot.ActiveItems[slot]))
            {
                ShowTip("Drag onto an enemy");
                return;
            }

            if (IsUnitTargetedItem(snapshot.ActiveItems[slot]))
            {
                ShowTip("Drag onto a basic unit");
                return;
            }

            if (!itemRuntime.TryUseItem(
                    TeamSide.Player,
                    snapshot.ActiveItems[slot],
                    out var reason))
            {
                if (reason == "NoAliveTargets")
                {
                    ShowTip("No enemies");
                }
                Debug.LogWarning(
                    $"Active item rejected: Item={snapshot.ActiveItems[slot]} Reason={reason}",
                    this);
            }
            Refresh();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            CancelActiveItemDrag();
            var slot = ResolveActiveItemSlot(eventData);
            if (!CanBeginActiveItemDrag(slot, out var itemId))
            {
                return;
            }

            activeDragSlot = slot;
            activeDragItemId = itemId;
            CreateActiveItemDragVisual(slot);
            UpdateActiveItemDrag(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (activeDragSlot >= 0)
            {
                UpdateActiveItemDrag(eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (activeDragSlot < 0)
            {
                return;
            }

            UpdateActiveItemDrag(eventData.position);
            var releasedSlot = activeDragSlot;
            var releasedItemId = activeDragItemId;
            var releasedOnPath = activeItemDropIsOnPath;
            var validDrop = activeItemDropIsValid;
            var dropPoint = activeItemDropPoint;
            var dropScreenPosition = activeItemDropScreenPosition;
            var dropPixelsPerCell = activeItemDropPixelsPerCell;
            var targetRuntimeId = activeItemDropTargetId;
            var isUnitTargeted = IsUnitTargetedItem(releasedItemId);
            var isEnemyTargeted = IsEnemyTargetedItem(releasedItemId);
            var isTargeted = isUnitTargeted || isEnemyTargeted;
            var isArcaneThunderburst =
                string.Equals(releasedItemId, ItemIds.RuneburstMine, System.StringComparison.Ordinal);
            if (validDrop && isArcaneThunderburst)
            {
                HoldArcaneThunderburstEnemyVisuals(dropPoint);
            }
            CancelActiveItemDrag();
            SuppressClickAfterDrag(releasedSlot);

            if (!validDrop)
            {
                ShowTip(isEnemyTargeted
                    ? "Drag onto an enemy"
                    : isUnitTargeted
                        ? "Drag onto a basic unit"
                        : releasedOnPath ? "No enemies" : "Invalid placement");
                return;
            }

            var used = isTargeted
                ? itemRuntime.TryUseItemOnUnit(
                    TeamSide.Player,
                    releasedItemId,
                    targetRuntimeId,
                    out var reason)
                : itemRuntime.TryUseItemAtPoint(
                    TeamSide.Player,
                    releasedItemId,
                    dropPoint,
                    out reason);
            if (!used)
            {
                if (isArcaneThunderburst)
                {
                    ReleaseArcaneThunderburstEnemyVisuals();
                }
                ShowTip(FormatActiveItemFailure(reason, isTargeted));
                Debug.LogWarning(
                    $"Dragged active item rejected: Item={releasedItemId} Target={targetRuntimeId} Reason={reason}",
                    this);
            }
            else if (isArcaneThunderburst)
            {
                PlayArcaneThunderburstAnimation(dropScreenPosition, dropPixelsPerCell);
            }
            Refresh();
        }

        private bool CanBeginActiveItemDrag(int slot, out string itemId)
        {
            itemId = null;
            var snapshot = itemRuntime?.PlayerItems?.Snapshot;
            if (snapshot == null || slot < 0 || slot >= snapshot.ActiveItems.Count ||
                match == null || match.State != MatchState.Running)
            {
                return false;
            }

            itemId = snapshot.ActiveItems[slot];
            if ((!IsPathPositionedItem(itemId) &&
                 !IsUnitTargetedItem(itemId) &&
                 !IsEnemyTargetedItem(itemId)) ||
                itemRuntime.PlayerItems.IsInitialCooldownActiveFor(itemId) ||
                itemRuntime.PlayerItems.GetCooldownRemainingSeconds(itemId) > 0.0001f)
            {
                return false;
            }

            var button = slot == 0 ? activeItemSlotOne : activeItemSlotTwo;
            return button != null && button.interactable;
        }

        private static bool IsPathPositionedItem(string itemId)
        {
            return itemId == ItemIds.RuneburstMine;
        }

        private static bool IsUnitTargetedItem(string itemId)
        {
            return itemId == ItemIds.FrenzyRune ||
                   itemId == ItemIds.RuneOfTempering ||
                   itemId == ItemIds.WarforgeSigil;
        }

        private static bool IsEnemyTargetedItem(string itemId)
        {
            return itemId == ItemIds.WyrmfangSnare;
        }

        private static string FormatActiveItemFailure(string reason, bool unitTargeted)
        {
            if (reason == "InitialCooldown") return "On cooldown";
            if (reason == "Cooldown") return "On cooldown";
            if (reason == "MaxActivationsPerUnit") return "Maximum stacks reached";
            if (reason == "MaxLevel") return "Maximum level reached";
            if (reason == "NoAliveTargets") return unitTargeted ? "Invalid unit" : "No enemies";
            return unitTargeted ? "Invalid unit" : "Invalid placement";
        }

        private int ResolveActiveItemSlot(PointerEventData eventData)
        {
            var source = eventData.pointerPressRaycast.gameObject != null
                ? eventData.pointerPressRaycast.gameObject.transform
                : eventData.pointerPress != null
                    ? eventData.pointerPress.transform
                    : null;
            if (source == null)
            {
                return -1;
            }

            if (activeItemSlotOne != null &&
                (source == activeItemSlotOne.transform || source.IsChildOf(activeItemSlotOne.transform)))
            {
                return 0;
            }

            if (activeItemSlotTwo != null &&
                (source == activeItemSlotTwo.transform || source.IsChildOf(activeItemSlotTwo.transform)))
            {
                return 1;
            }

            return -1;
        }

        private void CreateActiveItemDragVisual(int slot)
        {
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null)
            {
                return;
            }

            var rootObject = new GameObject(
                "ActiveItemDragVisual",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            activeItemDragRoot = rootObject.GetComponent<RectTransform>();
            activeItemDragRoot.SetParent(canvas.transform, false);
            activeItemDragRoot.anchorMin = Vector2.zero;
            activeItemDragRoot.anchorMax = Vector2.one;
            activeItemDragRoot.offsetMin = Vector2.zero;
            activeItemDragRoot.offsetMax = Vector2.zero;
            var overlayCanvas = rootObject.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = SettlementPanelSortingOrder + 10;
            var group = rootObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var previewObject = new GameObject("AoEPreview", typeof(RectTransform), typeof(Image));
            previewObject.transform.SetParent(activeItemDragRoot, false);
            activeItemAreaPreview = previewObject.GetComponent<Image>();
            activeItemAreaPreview.sprite = GetRuntimeCircleSprite();
            activeItemAreaPreview.preserveAspect = true;
            activeItemAreaPreview.raycastTarget = false;
            activeItemAreaPreview.color = new Color(0.95f, 0.25f, 0.18f, 0.24f);

            var sourceButton = slot == 0 ? activeItemSlotOne : activeItemSlotTwo;
            var sourceImage = sourceButton != null ? sourceButton.targetGraphic as Image : null;
            var ghostObject = new GameObject("ItemGhost", typeof(RectTransform), typeof(Image));
            ghostObject.transform.SetParent(activeItemDragRoot, false);
            activeItemDragGhost = ghostObject.GetComponent<RectTransform>();
            var ghostImage = ghostObject.GetComponent<Image>();
            ghostImage.sprite = sourceImage != null ? sourceImage.sprite : null;
            ghostImage.color = new Color(1f, 1f, 1f, 0.78f);
            ghostImage.preserveAspect = true;
            ghostImage.raycastTarget = false;
            var sourceRect = sourceButton != null ? sourceButton.transform as RectTransform : null;
            activeItemDragGhost.sizeDelta = sourceRect != null
                ? sourceRect.rect.size
                : new Vector2(96f, 96f);
            activeItemDragGhost.SetAsLastSibling();
        }

        private void UpdateActiveItemDrag(Vector2 screenPosition)
        {
            if (activeItemDragRoot == null)
            {
                activeItemDropIsValid = false;
                return;
            }

            SetDragVisualScreenPosition(activeItemDragGhost, screenPosition);
            activeItemDropTargetId = null;
            if (IsEnemyTargetedItem(activeDragItemId))
            {
                activeItemDropIsOnPath = false;
                activeItemDropIsValid = TryResolveEnemyDrop(
                    screenPosition,
                    out activeItemDropTargetId,
                    out var targetCenter,
                    out var targetSize);
                UpdateTargetedItemPreview(screenPosition, targetCenter, targetSize);
                return;
            }

            if (IsUnitTargetedItem(activeDragItemId))
            {
                activeItemDropIsOnPath = false;
                activeItemDropIsValid = TryResolveBasicUnitDrop(
                    screenPosition,
                    out activeItemDropTargetId,
                    out var targetCenter,
                    out var targetSize);
                if (activeItemAreaPreview != null)
                {
                    SetDragVisualScreenPosition(
                        activeItemAreaPreview.rectTransform,
                        activeItemDropIsValid ? targetCenter : screenPosition);
                    var canvas = activeItemDragRoot.GetComponentInParent<Canvas>()?.rootCanvas;
                    var scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
                    activeItemAreaPreview.preserveAspect = false;
                    activeItemAreaPreview.rectTransform.sizeDelta = activeItemDropIsValid
                        ? targetSize / scaleFactor
                        : Vector2.one * 80f;
                    activeItemAreaPreview.color = activeItemDropIsValid
                        ? new Color(0.30f, 0.95f, 0.42f, 0.32f)
                        : new Color(0.95f, 0.25f, 0.18f, 0.24f);
                    activeItemAreaPreview.gameObject.SetActive(true);
                }
                return;
            }

            activeItemDropIsOnPath = TryResolvePositionedItemDrop(
                screenPosition,
                out activeItemDropPoint,
                out var snappedScreenPosition,
                out var pixelsPerCell,
                out var containsEnemy);
            activeItemDropScreenPosition = snappedScreenPosition;
            activeItemDropPixelsPerCell = pixelsPerCell;
            activeItemDropIsValid = activeItemDropIsOnPath && containsEnemy;

            if (activeItemAreaPreview != null)
            {
                SetDragVisualScreenPosition(activeItemAreaPreview.rectTransform, snappedScreenPosition);
                var canvas = activeItemDragRoot.GetComponentInParent<Canvas>()?.rootCanvas;
                var scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
                activeItemAreaPreview.rectTransform.sizeDelta = Vector2.one *
                    (RuneburstMineEffect.AreaRadius * 2f * pixelsPerCell / scaleFactor);
                activeItemAreaPreview.color = activeItemDropIsValid
                    ? new Color(0.30f, 0.95f, 0.42f, 0.28f)
                    : new Color(0.95f, 0.25f, 0.18f, 0.24f);
                activeItemAreaPreview.gameObject.SetActive(pixelsPerCell > 0f);
            }
        }

        private bool TryResolveEnemyDrop(
            Vector2 screenPosition,
            out string runtimeId,
            out Vector2 targetCenter,
            out Vector2 targetSize)
        {
            runtimeId = null;
            targetCenter = screenPosition;
            targetSize = Vector2.zero;
            var lane = GetComponentInParent<DragonBoundScreenView>()?
                .PlayerBattlefieldView?.LaneView;
            var canvas = activeItemDragRoot != null
                ? activeItemDragRoot.GetComponentInParent<Canvas>()?.rootCanvas
                : null;
            var eventCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            if (lane == null ||
                !lane.TryGetEnemyAtScreenPoint(
                    screenPosition,
                    eventCamera,
                    out runtimeId,
                    out var enemyRect) ||
                itemRuntime?.PlayerEnemyRegistry == null ||
                !itemRuntime.PlayerEnemyRegistry.TryGet(runtimeId, out var enemy) ||
                enemy.Team != TeamSide.Player || !enemy.IsAttackable)
            {
                runtimeId = null;
                return false;
            }

            GetScreenRect(enemyRect, eventCamera, out targetCenter, out targetSize);
            return true;
        }

        private void UpdateTargetedItemPreview(
            Vector2 screenPosition,
            Vector2 targetCenter,
            Vector2 targetSize)
        {
            if (activeItemAreaPreview == null)
            {
                return;
            }

            SetDragVisualScreenPosition(
                activeItemAreaPreview.rectTransform,
                activeItemDropIsValid ? targetCenter : screenPosition);
            var canvas = activeItemDragRoot.GetComponentInParent<Canvas>()?.rootCanvas;
            var scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            activeItemAreaPreview.preserveAspect = false;
            activeItemAreaPreview.rectTransform.sizeDelta = activeItemDropIsValid
                ? targetSize / scaleFactor
                : Vector2.one * 80f;
            activeItemAreaPreview.color = activeItemDropIsValid
                ? new Color(0.30f, 0.95f, 0.42f, 0.32f)
                : new Color(0.95f, 0.25f, 0.18f, 0.24f);
            activeItemAreaPreview.gameObject.SetActive(true);
        }

        private static void GetScreenRect(
            RectTransform rect,
            Camera eventCamera,
            out Vector2 centre,
            out Vector2 size)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            centre = (bottomLeft + topRight) * 0.5f;
            size = new Vector2(
                Mathf.Abs(topRight.x - bottomLeft.x),
                Mathf.Abs(topRight.y - bottomLeft.y));
        }

        private bool TryResolveBasicUnitDrop(
            Vector2 screenPosition,
            out string runtimeId,
            out Vector2 targetCenter,
            out Vector2 targetSize)
        {
            runtimeId = null;
            targetCenter = screenPosition;
            targetSize = Vector2.zero;
            var boardView = GetComponentInParent<DragonBoundScreenView>()?.PlayerBoardView;
            if (boardView == null ||
                !boardView.TryGetBasicBattleUnitAtScreenPoint(screenPosition, out runtimeId, out var unitRect) ||
                itemRuntime?.PlayerItems?.UnitRegistry == null ||
                !itemRuntime.PlayerItems.UnitRegistry.TryGet(runtimeId, out var unit) ||
                unit.Kind != ItemCombatUnitKind.Basic || !unit.IsAlive)
            {
                runtimeId = null;
                return false;
            }

            var canvas = activeItemDragRoot.GetComponentInParent<Canvas>()?.rootCanvas;
            var eventCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            var corners = new Vector3[4];
            unitRect.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            targetCenter = (bottomLeft + topRight) * 0.5f;
            targetSize = new Vector2(
                Mathf.Abs(topRight.x - bottomLeft.x),
                Mathf.Abs(topRight.y - bottomLeft.y));
            return true;
        }

        private bool TryResolvePositionedItemDrop(
            Vector2 screenPosition,
            out CombatPoint combatPoint,
            out Vector2 snappedScreenPosition,
            out float pixelsPerCell,
            out bool containsEnemy)
        {
            combatPoint = default(CombatPoint);
            snappedScreenPosition = screenPosition;
            pixelsPerCell = 0f;
            containsEnemy = false;
            var screen = GetComponentInParent<DragonBoundScreenView>();
            var lane = screen?.PlayerBattlefieldView?.LaneView;
            var path = itemRuntime?.PlayerPath;
            var waypoints = lane?.Waypoints;
            if (path == null || waypoints == null || waypoints.Count < 2 ||
                path.NodeCount != waypoints.Count)
            {
                return false;
            }

            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var eventCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            var bestDistanceSquared = float.MaxValue;
            for (var index = 0; index < waypoints.Count - 1; index++)
            {
                if (waypoints[index] == null || waypoints[index + 1] == null)
                {
                    continue;
                }

                var fromScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, waypoints[index].position);
                var toScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, waypoints[index + 1].position);
                var screenDelta = toScreen - fromScreen;
                var screenLengthSquared = screenDelta.sqrMagnitude;
                if (screenLengthSquared <= 0.0001f)
                {
                    continue;
                }

                var t = Mathf.Clamp01(Vector2.Dot(screenPosition - fromScreen, screenDelta) / screenLengthSquared);
                var candidateScreen = fromScreen + screenDelta * t;
                var distanceSquared = (screenPosition - candidateScreen).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                var fromCombat = path.GetNodeCombatPosition(index);
                var toCombat = path.GetNodeCombatPosition(index + 1);
                var combatLength = Mathf.Sqrt(fromCombat.DistanceSquared(toCombat));
                if (combatLength <= 0.0001f)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                snappedScreenPosition = candidateScreen;
                pixelsPerCell = Mathf.Sqrt(screenLengthSquared) / combatLength;
                combatPoint = CombatPoint.Lerp(fromCombat, toCombat, t);
            }

            if (pixelsPerCell <= 0f ||
                bestDistanceSquared > Mathf.Pow(Mathf.Max(24f, pixelsPerCell * 0.55f), 2f))
            {
                return false;
            }

            foreach (var enemy in itemRuntime.PlayerEnemyRegistry.Enemies)
            {
                if (enemy.Team == TeamSide.Player && enemy.IsAttackable &&
                    enemy.CombatPosition.DistanceSquared(combatPoint) <=
                    RuneburstMineEffect.AreaRadius * RuneburstMineEffect.AreaRadius + 0.0001f)
                {
                    containsEnemy = true;
                    break;
                }
            }

            return true;
        }

        private void SetDragVisualScreenPosition(RectTransform target, Vector2 screenPosition)
        {
            if (target == null || activeItemDragRoot == null)
            {
                return;
            }

            var canvas = activeItemDragRoot.GetComponentInParent<Canvas>()?.rootCanvas;
            var eventCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    activeItemDragRoot,
                    screenPosition,
                    eventCamera,
                    out var localPoint))
            {
                target.anchoredPosition = localPoint;
            }
        }

        private void CancelActiveItemDrag()
        {
            activeDragSlot = -1;
            activeDragItemId = null;
            activeItemDropIsOnPath = false;
            activeItemDropIsValid = false;
            activeItemDropScreenPosition = Vector2.zero;
            activeItemDropPixelsPerCell = 0f;
            activeItemDropTargetId = null;
            if (activeItemDragRoot != null)
            {
                Destroy(activeItemDragRoot.gameObject);
            }
            activeItemDragRoot = null;
            activeItemDragGhost = null;
            activeItemAreaPreview = null;
        }

        private void HoldArcaneThunderburstEnemyVisuals(CombatPoint center)
        {
            ReleaseArcaneThunderburstEnemyVisuals();
            var lane = GetComponentInParent<DragonBoundScreenView>()?
                .PlayerBattlefieldView?.LaneView;
            var registry = itemRuntime?.PlayerEnemyRegistry;
            if (lane == null || registry == null)
            {
                return;
            }

            var radiusSquared =
                RuneburstMineEffect.AreaRadius * RuneburstMineEffect.AreaRadius + 0.0001f;
            foreach (var enemy in registry.Enemies)
            {
                if (enemy.Team != TeamSide.Player || !enemy.IsAttackable ||
                    enemy.CombatPosition.DistanceSquared(center) > radiusSquared)
                {
                    continue;
                }

                lane.HoldEnemyHealthVisual(enemy.RuntimeId);
                arcaneThunderburstHeldEnemyIds.Add(enemy.RuntimeId);
            }
        }

        private void ReleaseArcaneThunderburstEnemyVisuals()
        {
            if (arcaneThunderburstHeldEnemyIds.Count == 0)
            {
                return;
            }

            var lane = GetComponentInParent<DragonBoundScreenView>()?
                .PlayerBattlefieldView?.LaneView;
            if (lane != null)
            {
                foreach (var runtimeId in arcaneThunderburstHeldEnemyIds)
                {
                    // This publishes the pending health ratio, applies hit feedback and
                    // starts DieBoom for enemies removed by the lethal item hit.
                    lane.ReleaseEnemyHealthVisual(runtimeId);
                }
            }

            arcaneThunderburstHeldEnemyIds.Clear();
        }

        private void PlayArcaneThunderburstAnimation(
            Vector2 screenPosition,
            float pixelsPerCell)
        {
            var controller =
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(ArcaneThunderburstControllerPath);
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (controller == null || canvas == null || pixelsPerCell <= 0f)
            {
                Debug.LogWarning(
                    $"Arcane Thunderburst presentation requires Resources/{ArcaneThunderburstControllerPath} " +
                    "and a valid route scale.",
                    this);
                ReleaseArcaneThunderburstEnemyVisuals();
                return;
            }

            var rootObject = new GameObject(
                "ArcaneThunderburstVFX",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var effectCanvas = rootObject.GetComponent<Canvas>();
            effectCanvas.overrideSorting = true;
            effectCanvas.sortingOrder = ActiveItemSortingOrder - 1;
            var canvasGroup = rootObject.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var effectObject = new GameObject(
                "Image",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));
            var effectRect = effectObject.GetComponent<RectTransform>();
            effectRect.SetParent(root, false);
            effectRect.anchorMin = new Vector2(0.5f, 0.5f);
            effectRect.anchorMax = new Vector2(0.5f, 0.5f);
            effectRect.pivot = new Vector2(0.5f, 0.5f);
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root,
                screenPosition,
                eventCamera,
                out var localPoint);
            effectRect.anchoredPosition = localPoint;
            var scaleFactor = Mathf.Max(0.0001f, canvas.scaleFactor);
            var diameter = RuneburstMineEffect.AreaRadius * 2f * pixelsPerCell / scaleFactor;
            effectRect.sizeDelta = Vector2.one * diameter;

            var image = effectObject.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            var animator = effectObject.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            // Controller state Speed is authored at 0.2; keep Animator at one to avoid
            // multiplying the presentation speed down to 0.04.
            animator.speed = 1f;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);

            activeArcaneThunderburstVfxRoots.Add(rootObject);
            StartCoroutine(CompleteArcaneThunderburstAnimation(rootObject, controller));
        }

        private IEnumerator CompleteArcaneThunderburstAnimation(
            GameObject effectRoot,
            RuntimeAnimatorController controller)
        {
            var clipLength = 0f;
            var frameRate = 60f;
            var clips = controller.animationClips;
            for (var index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null)
                {
                    continue;
                }

                if (clips[index].length >= clipLength)
                {
                    clipLength = clips[index].length;
                    frameRate = Mathf.Max(1f, clips[index].frameRate);
                }
            }

            var totalDuration =
                (clipLength > 0f ? clipLength : 0.1f) / ArcaneThunderburstPlaybackSpeed;
            var impactDelay = Mathf.Min(
                totalDuration,
                ArcaneThunderburstImpactFrame / frameRate / ArcaneThunderburstPlaybackSpeed);
            var elapsed = 0f;
            while (elapsed < impactDelay)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Frame two is the first full explosion frame. Release health and lethal
            // presentation here so damage, hit reaction and DieBoom read as one impact.
            ReleaseArcaneThunderburstEnemyVisuals();

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (effectRoot != null)
            {
                activeArcaneThunderburstVfxRoots.Remove(effectRoot);
                Destroy(effectRoot);
            }
        }

        private void SuppressClickAfterDrag(int slot)
        {
            suppressedClickSlot = slot;
            if (clearClickSuppressionCoroutine != null)
            {
                StopCoroutine(clearClickSuppressionCoroutine);
            }
            clearClickSuppressionCoroutine = StartCoroutine(ClearClickSuppressionAfterFrame());
        }

        private IEnumerator ClearClickSuppressionAfterFrame()
        {
            yield return null;
            suppressedClickSlot = -1;
            clearClickSuppressionCoroutine = null;
        }

        private void RefreshActiveItemSlots()
        {
            RefreshPassiveItemSlots();
            RefreshActiveItemSlot(
                activeItemSlotOne,
                activeItemSlotOneLabel,
                activeItemSlotOneCooldownMask,
                0);
            RefreshActiveItemSlot(
                activeItemSlotTwo,
                activeItemSlotTwoLabel,
                activeItemSlotTwoCooldownMask,
                1);
        }

        private void RefreshActiveItemSlot(Button button, Text label, Image cooldownMask, int slot)
        {
            if (button == null)
            {
                return;
            }

            var snapshot = itemRuntime?.PlayerItems?.Snapshot;
            if (snapshot == null || slot >= snapshot.ActiveItems.Count)
            {
                ApplyItemIcon(button.targetGraphic as Image ?? button.GetComponent<Image>(), null);
                if (label != null) label.text = string.Empty;
                button.interactable = false;
                SetCooldownMask(cooldownMask, 0f, false);
                button.gameObject.SetActive(false);
                return;
            }

            if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);
            var itemId = snapshot.ActiveItems[slot];
            ApplyItemIcon(button.targetGraphic as Image ?? button.GetComponent<Image>(), itemId);
            var cooldown = itemRuntime.PlayerItems.GetCooldownRemainingSeconds(itemId);
            var cooldownDuration = itemRuntime.PlayerItems.GetCooldownDurationSeconds(itemId);
            var initialItemCooldownDuration = itemRuntime.PlayerItems.GetInitialCooldownDurationSeconds(itemId);
            var initialItemCooldownRemaining = itemRuntime.PlayerItems.GetInitialCooldownRemainingSeconds(itemId);
            bool initialCooldownVisible = itemRuntime.PlayerItems.IsInitialCooldownActiveFor(itemId) &&
                                          initialItemCooldownDuration > 0.0001f;
            button.interactable = !initialCooldownVisible &&
                                  cooldown <= 0.0001f &&
                                  match != null && match.State == MatchState.Running;
            if (label != null) label.text = string.Empty;
            if (initialCooldownVisible)
            {
                SetCooldownMask(
                    cooldownMask,
                    Mathf.Clamp01(initialItemCooldownRemaining / initialItemCooldownDuration),
                    true);
            }
            else
            {
                SetCooldownMask(
                    cooldownMask,
                    cooldownDuration > 0.0001f ? Mathf.Clamp01(cooldown / cooldownDuration) : 0f,
                    cooldown > 0.0001f && cooldownDuration > 0.0001f);
            }
        }

        private void RefreshPassiveItemSlots()
        {
            var snapshot = itemRuntime?.PlayerItems?.Snapshot;
            for (int index = 0; index < passiveItemSlots.Length; index++)
            {
                Transform slot = passiveItemSlots[index];
                if (slot == null) continue;

                bool hasItem = snapshot != null && index < snapshot.PassiveItems.Count;
                string itemId = hasItem ? snapshot.PassiveItems[index] : null;
                Image slotImage = slot.GetComponent<Image>();
                ApplyItemIcon(slotImage, itemId);
                Image cooldownMask = passiveItemCooldownMasks[index];
                float cooldown = hasItem
                    ? itemRuntime.PlayerItems.GetCooldownRemainingSeconds(itemId)
                    : 0f;
                float cooldownDuration = hasItem
                    ? itemRuntime.PlayerItems.GetCooldownDurationSeconds(itemId)
                    : 0f;
                bool showCooldown = hasItem && cooldown > 0.0001f && cooldownDuration > 0.0001f;
                SetCooldownMask(
                    cooldownMask,
                    showCooldown ? Mathf.Clamp01(cooldown / cooldownDuration) : 0f,
                    showCooldown);
                bool consumed = hasItem &&
                                itemRuntime.PlayerItems.IsItemConsumed(itemId);
                Button slotButton = slot.GetComponent<Button>();
                if (slotButton != null)
                {
                    slotButton.interactable = hasItem && !consumed;
                }

                // CanvasGroup dimming remains visible even if the Button's ColorTint
                // transition uses an authored white Disabled Color.
                CanvasGroup group = slot.GetComponent<CanvasGroup>();
                if (consumed || group != null)
                {
                    if (group == null) group = slot.gameObject.AddComponent<CanvasGroup>();
                    if (consumed && !passiveItemConsumedVisualized[index])
                    {
                        passiveItemConsumedVisualized[index] = true;
                        passiveItemConsumedEffects[index] = StartCoroutine(
                            PlayPassiveItemConsumedEffect(index, group));
                    }
                    else if (passiveItemConsumedEffects[index] == null)
                    {
                        group.alpha = consumed ? 0.55f : 1f;
                    }
                    group.interactable = hasItem && !consumed;
                    group.blocksRaycasts = hasItem && !consumed;
                }
                if (!hasItem)
                {
                    StopPassiveItemConsumedEffect(index);
                    passiveItemConsumedVisualized[index] = false;
                }
                if (slot.gameObject.activeSelf != hasItem) slot.gameObject.SetActive(hasItem);
            }
        }

        private IEnumerator PlayPassiveItemConsumedEffect(int index, CanvasGroup group)
        {
            for (int flash = 0; flash < 2; flash++)
            {
                group.alpha = 0.25f;
                yield return new WaitForSecondsRealtime(0.1f);
                group.alpha = 1f;
                yield return new WaitForSecondsRealtime(0.1f);
            }

            group.alpha = 0.55f;
            passiveItemConsumedEffects[index] = null;
        }

        private void ResetPassiveItemConsumedEffects()
        {
            for (int index = 0; index < passiveItemConsumedEffects.Length; index++)
            {
                StopPassiveItemConsumedEffect(index);
                passiveItemConsumedVisualized[index] = false;
            }
        }

        private void StopPassiveItemConsumedEffect(int index)
        {
            Coroutine effect = passiveItemConsumedEffects[index];
            if (effect == null) return;
            StopCoroutine(effect);
            passiveItemConsumedEffects[index] = null;
        }

        private void ApplyItemIcon(Image image, string itemId)
        {
            if (image == null) return;
            RememberItemSlotFallback(image);

            ItemDefinition definition = ItemCatalog.Get(itemId);
            Sprite icon = LoadItemIcon(definition);
            if (icon != null)
            {
                image.sprite = icon;
                image.color = Color.white;
                image.preserveAspect = true;
                return;
            }

            image.sprite = itemSlotFallbackSprites[image];
            image.color = itemSlotFallbackColors[image];
        }

        private void RememberItemSlotFallback(Image image)
        {
            if (itemSlotFallbackSprites.ContainsKey(image)) return;
            itemSlotFallbackSprites.Add(image, image.sprite);
            itemSlotFallbackColors.Add(image, image.color);
        }

        private static Sprite LoadItemIcon(ItemDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.IconKey)) return null;
            if (ItemIconCache.TryGetValue(definition.IconKey, out Sprite cached)) return cached;

            Sprite sprite = DragonBound.Presentation.UiAssets.Load<Sprite>(definition.IconKey);
            if (sprite != null)
            {
                ItemIconCache[definition.IconKey] = sprite;
                return sprite;
            }

            if (MissingItemIconKeys.Add(definition.IconKey))
            {
                Debug.LogWarning(
                    $"Gameplay item icon is missing at Resources/{definition.IconKey}. " +
                    $"The authored placeholder will be used for {definition.ItemId}.");
            }
            return null;
        }

        private static void HideObsoleteItemLabel(Text label)
        {
            if (label == null) return;
            label.text = string.Empty;
            label.gameObject.SetActive(false);
        }

        private void ShowTip(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            TipTextService.Show(message, 3f);
        }

        private void ResolvePassiveItemCooldownMasks(Transform screen)
        {
            Transform passiveContainer = null;
            foreach (Transform candidate in screen.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "Passtive" || candidate.name == "Passive")
                {
                    passiveContainer = candidate;
                    break;
                }
            }

            if (passiveContainer == null) return;
            for (int index = 0; index < passiveItemCooldownMasks.Length; index++)
            {
                Transform slot = passiveContainer.FindUi("Passtive" + index) ??
                                 passiveContainer.FindUi("Passive" + index);
                passiveItemCooldownMasks[index] = EnsureCooldownMask(slot);
                passiveItemSlots[index] = slot;
            }
        }

        private static Image EnsureCooldownMask(Transform slot)
        {
            if (slot == null) return null;
            var existing = slot.FindUi("CooldownMask")?.GetComponent<Image>();
            var maskObject = existing != null
                ? existing.gameObject
                : new GameObject("CooldownMask", typeof(RectTransform), typeof(Image));
            if (existing == null)
            {
                maskObject.transform.SetParent(slot, false);
            }
            var rect = maskObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = maskObject.GetComponent<Image>();
            image.sprite = GetRuntimeCircleSprite();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            image.preserveAspect = true;
            image.color = new Color(0f, 0f, 0f, 0.58f);
            image.raycastTarget = false;
            image.transform.SetAsLastSibling();
            if (existing == null)
            {
                maskObject.SetActive(false);
            }
            return image;
        }

        private static Sprite GetRuntimeCircleSprite()
        {
            if (runtimeCircleSprite != null)
            {
                return runtimeCircleSprite;
            }

            var size = RuntimeCircleTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DragonBound_RuntimeCircleTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Mathf.Sqrt(
                        (x - center) * (x - center) +
                        (y - center) * (y - center));
                    var alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(radius - distance + 1f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            runtimeCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            runtimeCircleSprite.name = "DragonBound_RuntimeCircleSprite";
            runtimeCircleSprite.hideFlags = HideFlags.HideAndDontSave;
            return runtimeCircleSprite;
        }

        private static void SetCooldownMask(Image mask, float fillAmount, bool visible)
        {
            if (mask == null) return;
            mask.fillAmount = fillAmount;
            if (mask.gameObject.activeSelf != visible) mask.gameObject.SetActive(visible);
        }

        private static string FormatEnemies(string label, EnemyRegistry registry)
        {
            var text = new StringBuilder(label + " ENEMY DEBUG");
            if (registry == null || registry.Count == 0)
            {
                text.Append("\nNONE");
                return text.ToString();
            }

            foreach (var enemy in registry.Enemies)
            {
                text.Append('\n');
                text.Append(enemy.RuntimeId);
                text.Append(" i=");
                text.Append(enemy.PathIndex);
                text.Append(" p=");
                text.Append(enemy.PathProgress.ToString("0.00"));
                text.Append(" hp=");
                text.Append(enemy.HitPoints.ToString("0.##"));
                text.Append(" state=");
                text.Append(enemy.State);
                text.Append(" team=");
                text.Append(enemy.Team);
            }

            return text.ToString();
        }

        private static int GetCampCount(BoardRecruitDestination destination)
        {
            return destination != null ? destination.CampCount : 0;
        }

        private static int GetDeployedCount(BoardRecruitDestination destination)
        {
            return destination != null ? destination.DeployedCount : 0;
        }

        private static string GetLastResult(RecruitmentService service)
        {
            return service != null ? service.LastRecruitResult : "NONE";
        }

        private static int GetRemainingHeroComponents(RecruitmentService service)
        {
            return service != null ? service.RemainingHeroComponents : 0;
        }
    }
}
