using DragonBound.Core;
using DragonBound.Combat;
using DragonBound.Bosses.Contracts;
using DragonBound.Bosses.Runtime;
using DragonBound.Items;
using DragonBound.Recruitment;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
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
        private const int RuntimeCircleTextureSize = 64;
        private static Sprite runtimeCircleSprite;

        [SerializeField] private Button pauseButton;
        [SerializeField] private Text pauseLabel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button finishMatchButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject bossWarning;
        [SerializeField] private Button bossWarningConfirmButton;
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private TMP_Text settlementResultText;
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
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private bool showDebugOverlay;

        private MatchController match;
        private TeamState team;
        private RecruitmentService playerRecruitment;
        private RecruitmentService aiRecruitment;
        private BoardRecruitDestination playerRecruitDestination;
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
        private Coroutine tipHideCoroutine;
        private float initialItemCooldownDuration;
        private float initialItemCooldownRemaining;
        private int activeDragSlot = -1;
        private string activeDragItemId;
        private RectTransform activeItemDragRoot;
        private RectTransform activeItemDragGhost;
        private Image activeItemAreaPreview;
        private bool activeItemDropIsOnPath;
        private bool activeItemDropIsValid;
        private CombatPoint activeItemDropPoint;
        private string activeItemDropTargetId;
        private int suppressedClickSlot = -1;
        private Coroutine clearClickSuppressionCoroutine;
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
            this.playerRecruitDestination = playerRecruitDestination;
            this.aiRecruitDestination = aiRecruitDestination;
            if (this.playerRecruitDestination != null)
            {
                this.playerRecruitDestination.BasicMergeBlocked -= HandleBasicMergeBlocked;
                this.playerRecruitDestination.BasicMergeBlocked += HandleBasicMergeBlocked;
            }
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

        public void BindItemRuntime(TwentyWavePressureRuntime runtime)
        {
            if (itemRuntime != null)
            {
                itemRuntime.BossWarningRequested -= HandleBossWarningRequested;
                itemRuntime.SoulChainCastEmitted -= HandleSoulChainCast;
                itemRuntime.StormcallerCastEmitted -= HandleStormcallerCast;
                itemRuntime.BloodcrownLifecycleEmitted -= HandleBloodcrownLifecycle;
                itemRuntime.WorldeaterCastEmitted -= HandleWorldeaterCast;
            }
            itemRuntime = runtime;
            if (itemRuntime != null)
            {
                if (bossWarning != null && bossWarningConfirmButton != null)
                {
                    itemRuntime.BossWarningRequested += HandleBossWarningRequested;
                }
                itemRuntime.SoulChainCastEmitted += HandleSoulChainCast;
                itemRuntime.StormcallerCastEmitted += HandleStormcallerCast;
                itemRuntime.BloodcrownLifecycleEmitted += HandleBloodcrownLifecycle;
                itemRuntime.WorldeaterCastEmitted += HandleWorldeaterCast;
            }
            initialItemCooldownDuration = runtime != null && runtime.Configuration != null
                ? runtime.Configuration.GetWave(1).FirstSpawnDelaySeconds
                : 0f;
            initialItemCooldownRemaining = initialItemCooldownDuration;
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
            if (initialItemCooldownRemaining > 0f &&
                match != null && match.State == MatchState.Running)
            {
                initialItemCooldownRemaining = Mathf.Max(
                    0f,
                    initialItemCooldownRemaining - Time.deltaTime);
            }
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
                itemRuntime.SoulChainCastEmitted -= HandleSoulChainCast;
                itemRuntime.StormcallerCastEmitted -= HandleStormcallerCast;
                itemRuntime.BloodcrownLifecycleEmitted -= HandleBloodcrownLifecycle;
                itemRuntime.WorldeaterCastEmitted -= HandleWorldeaterCast;
            }
            if (playerRecruitDestination != null)
            {
                playerRecruitDestination.BasicMergeBlocked -= HandleBasicMergeBlocked;
            }

            ReleaseGlobalPause();
            ReleaseBossWarningPause();

            RemoveActiveItemListeners();
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
                ? screen.transform.Find("ART_ScreenBackground")
                : null;
            if (background == null)
            {
                return;
            }

            tipText = screen.transform.Find("TipText")?.GetComponent<TMP_Text>();
            ResolvePassiveItemCooldownMasks(screen.transform);

            var authoredItemContainer = screen.transform.Find("ItemContainer");
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
            var authoredActiveContainer = authoredItemContainer?.Find("Active");
            if (authoredActiveContainer != null)
            {
                activeItemSlotOne =
                    authoredActiveContainer.Find("Active")?.GetComponent<Button>() ??
                    authoredActiveContainer.Find("Active0")?.GetComponent<Button>();
                activeItemSlotTwo =
                    authoredActiveContainer.Find("Active1")?.GetComponent<Button>();
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
                activeItemContainer = background.Find("ActiveItemContainer") as RectTransform;
            }
            if (activeItemContainer == null)
            {
                activeItemContainer = background as RectTransform;
            }

            var authoredResourceLabel = background.Find("ResourceLabel")?.GetComponent<Text>();
            if (authoredResourceLabel != null)
            {
                resourceLabel = authoredResourceLabel;
            }

            var authoredWaveLabel = background.Find("WaveLabel")?.GetComponent<Text>();
            if (authoredWaveLabel != null)
            {
                waveLabel = authoredWaveLabel;
            }

            var authoredButton = background.Find("ART_PauseButton")?.GetComponent<Button>();
            if (authoredButton != null)
            {
                pauseButton = authoredButton;
                pauseLabel = authoredButton.transform.Find("PauseLabel")?.GetComponent<Text>();
                EnsureOverlayCanvas(authoredButton.gameObject, PauseButtonSortingOrder, true);
            }

            // Current authored hierarchy keeps PausePanel beside ART_ScreenBackground.
            // Retain the old nested lookup so older screen prefabs remain compatible.
            var authoredPanel = screen.transform.Find("PausePanel") ??
                                background.Find("PausePanel");
            if (authoredPanel != null)
            {
                pausePanel = authoredPanel.gameObject;
                finishMatchButton = authoredPanel.Find("Bg/PauseBtn")?.GetComponent<Button>();
                continueButton = authoredPanel.Find("Bg/ContinueBtn")?.GetComponent<Button>();
                EnsureOverlayCanvas(pausePanel, PausePanelSortingOrder, true);
            }

            var authoredSettlement = screen.transform.Find("SettlementPanel");
            if (authoredSettlement != null)
            {
                settlementPanel = authoredSettlement.gameObject;
                settlementResultText = authoredSettlement.Find("Text")?.GetComponent<TMP_Text>();
                EnsureOverlayCanvas(settlementPanel, SettlementPanelSortingOrder, true);
            }

            var authoredBossWarning = screen.transform.Find("BossWarning");
            if (authoredBossWarning != null)
            {
                bossWarning = authoredBossWarning.gameObject;
                bossWarningConfirmButton = authoredBossWarning.Find("ConfirmBtn")?.GetComponent<Button>();
                EnsureOverlayCanvas(bossWarning, BossWarningSortingOrder, true);
            }

            var debugRoot = background.Find("Debug");
            if (debugRoot != null)
            {
                var authoredDebugLabel = debugRoot.Find("DebugLabel")?.GetComponent<Text>();
                if (authoredDebugLabel != null)
                {
                    debugLabel = authoredDebugLabel;
                }

                var authoredEnemyDebugLabel = debugRoot.Find("EnemyDebugLabel")?.GetComponent<Text>();
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
            if (settlementResultText != null)
            {
                settlementResultText.text = state == MatchState.Victory ? "Victory" : "Defeat";
            }
            if (settlementPanel != null)
            {
                settlementPanel.SetActive(true);
                settlementPanel.transform.SetAsLastSibling();
            }
            Refresh();
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
            var targetRuntimeId = activeItemDropTargetId;
            var isUnitTargeted = IsUnitTargetedItem(releasedItemId);
            CancelActiveItemDrag();
            SuppressClickAfterDrag(releasedSlot);

            if (!validDrop)
            {
                ShowTip(isUnitTargeted
                    ? "Drag onto a basic unit"
                    : releasedOnPath ? "No enemies" : "Invalid placement");
                return;
            }

            var used = isUnitTargeted
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
                ShowTip(FormatActiveItemFailure(reason, isUnitTargeted));
                Debug.LogWarning(
                    $"Dragged active item rejected: Item={releasedItemId} Target={targetRuntimeId} Reason={reason}",
                    this);
            }
            Refresh();
        }

        private bool CanBeginActiveItemDrag(int slot, out string itemId)
        {
            itemId = null;
            var snapshot = itemRuntime?.PlayerItems?.Snapshot;
            if (snapshot == null || slot < 0 || slot >= snapshot.ActiveItems.Count ||
                match == null || match.State != MatchState.Running ||
                initialItemCooldownRemaining > 0.0001f)
            {
                return false;
            }

            itemId = snapshot.ActiveItems[slot];
            if ((!IsPathPositionedItem(itemId) && !IsUnitTargetedItem(itemId)) ||
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

        private static string FormatActiveItemFailure(string reason, bool unitTargeted)
        {
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
                if (enemy.Team == TeamSide.Player && enemy.IsAlive &&
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
            activeItemDropTargetId = null;
            if (activeItemDragRoot != null)
            {
                Destroy(activeItemDragRoot.gameObject);
            }
            activeItemDragRoot = null;
            activeItemDragGhost = null;
            activeItemAreaPreview = null;
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
            bool initialCooldownVisible = initialItemCooldownRemaining > 0.0001f &&
                                          initialItemCooldownDuration > 0.0001f;
            float initialFill = initialCooldownVisible
                ? Mathf.Clamp01(initialItemCooldownRemaining / initialItemCooldownDuration)
                : 0f;
            SetPassiveInitialCooldownVisual(initialFill, initialCooldownVisible);
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
                if (label != null) label.text = string.Empty;
                button.interactable = false;
                SetCooldownMask(cooldownMask, 0f, false);
                button.gameObject.SetActive(false);
                return;
            }

            if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);
            var itemId = snapshot.ActiveItems[slot];
            var cooldown = itemRuntime.PlayerItems.GetCooldownRemainingSeconds(itemId);
            var cooldownDuration = itemRuntime.PlayerItems.GetCooldownDurationSeconds(itemId);
            bool initialCooldownVisible = initialItemCooldownRemaining > 0.0001f &&
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

        private static void HideObsoleteItemLabel(Text label)
        {
            if (label == null) return;
            label.text = string.Empty;
            label.gameObject.SetActive(false);
        }

        private void ShowTip(string message)
        {
            if (tipText == null || string.IsNullOrWhiteSpace(message)) return;
            if (tipHideCoroutine != null) StopCoroutine(tipHideCoroutine);
            tipText.text = message;
            tipText.gameObject.SetActive(true);
            tipHideCoroutine = StartCoroutine(HideTipAfterDelay());
        }

        private void ShowBossTip(string message)
        {
            ShowTip($"Boss : {message}");
        }

        private void HandleBasicMergeBlocked()
        {
            ShowBossTip("Merge blocked");
        }

        private void HandleSoulChainCast(TeamSide side, SoulChainCastEvent value)
        {
            if (side != TeamSide.Player) return;
            if (value.Kind == SoulChainCastEventKind.CastStarted)
            {
                ShowBossTip("Soul Chain incoming");
            }
            else if (value.Kind == SoulChainCastEventKind.EffectApplied)
            {
                ShowBossTip(value.AffectedCount > 0
                    ? $"Soul Chain locked {value.AffectedCount} unit(s)"
                    : "Soul Chain found no target");
            }
            else if (value.Kind == SoulChainCastEventKind.CastFailed)
            {
                ShowBossTip("Soul Chain interrupted");
            }
        }

        private void HandleStormcallerCast(TeamSide side, StormcallerCastEvent value)
        {
            if (side != TeamSide.Player) return;
            if (value.Kind == StormcallerCastEventKind.CastStarted)
            {
                ShowBossTip("Storm Call incoming");
            }
            else if (value.Kind == StormcallerCastEventKind.EffectApplied)
            {
                ShowBossTip($"Storm Call shielded and hastened {value.AffectedCount} enemy unit(s)");
            }
            else if (value.Kind == StormcallerCastEventKind.CastFailed)
            {
                ShowBossTip("Storm Call interrupted");
            }
        }

        private void HandleBloodcrownLifecycle(TeamSide side, BossSkillLifecycleEvent value)
        {
            if (side != TeamSide.Player) return;
            if (value.Lifecycle == BossSkillLifecycle.Start)
            {
                ShowBossTip("Bloodcrown Decree incoming");
            }
            else if (value.Lifecycle == BossSkillLifecycle.Resolve)
            {
                ShowBossTip("All Basic units are treated as Lv1 and cannot merge");
            }
            else if (value.Lifecycle == BossSkillLifecycle.Blocked)
            {
                ShowBossTip("Bloodcrown Decree interrupted");
            }
        }

        private void HandleWorldeaterCast(TeamSide side, WorldeaterCastEvent value)
        {
            if (side != TeamSide.Player) return;
            if (value.Outcome == WorldeaterCastOutcome.Started)
            {
                ShowBossTip(value.Kind == WorldeaterCastKind.Devour
                    ? "Worldeater is targeting Devour"
                    : "Worldeater is summoning");
            }
            else if (value.Outcome == WorldeaterCastOutcome.Blocked)
            {
                ShowBossTip("Worldeater skill interrupted");
            }
            else if (value.Outcome == WorldeaterCastOutcome.Resolved)
            {
                ShowBossTip(value.Kind == WorldeaterCastKind.Devour
                    ? "Devoured a target and increased HP"
                    : value.Kind == WorldeaterCastKind.SummonSubBoss
                        ? "Summoned a SubBoss"
                        : $"Summoned {value.AffectedCount} minions");
            }
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
                Transform slot = passiveContainer.Find("Passtive" + index) ??
                                 passiveContainer.Find("Passive" + index);
                passiveItemCooldownMasks[index] = EnsureCooldownMask(slot);
                passiveItemSlots[index] = slot;
            }
        }

        private void SetPassiveInitialCooldownVisual(float fillAmount, bool visible)
        {
            fillAmount = Mathf.Clamp01(fillAmount);
            for (int index = 0; index < passiveItemCooldownMasks.Length; index++)
            {
                Image mask = passiveItemCooldownMasks[index];
                if (mask == null) continue;
                bool hasItem = passiveItemSlots[index] != null &&
                               passiveItemSlots[index].gameObject.activeSelf;
                bool show = visible && hasItem;
                mask.fillAmount = show ? fillAmount : 0f;
                if (mask.gameObject.activeSelf != show) mask.gameObject.SetActive(show);
            }
        }

        private IEnumerator HideTipAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            tipHideCoroutine = null;
            if (tipText == null) yield break;
            tipText.text = string.Empty;
            tipText.gameObject.SetActive(false);
        }

        private static Image EnsureCooldownMask(Transform slot)
        {
            if (slot == null) return null;
            var existing = slot.Find("CooldownMask")?.GetComponent<Image>();
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
