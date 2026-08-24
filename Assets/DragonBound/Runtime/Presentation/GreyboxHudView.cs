using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Recruitment;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public class GreyboxHudView : MonoBehaviour
    {
        private const int ActiveItemSortingOrder = 105;
        private const int PauseButtonSortingOrder = 110;
        private const int PausePanelSortingOrder = 120;

        [SerializeField] private Button pauseButton;
        [SerializeField] private Text pauseLabel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button finishMatchButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Text resourceLabel;
        [SerializeField] private Text waveLabel;
        [SerializeField] private Text debugLabel;
        [SerializeField] private Text enemyDebugLabel;
        [SerializeField] private Button activeItemSlotOne;
        [SerializeField] private Button activeItemSlotTwo;
        [SerializeField] private Text activeItemSlotOneLabel;
        [SerializeField] private Text activeItemSlotTwoLabel;
        [SerializeField] private RectTransform activeItemContainer;
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
        private IWaveRuntime waveRuntime;
        private TwentyWavePressureRuntime itemRuntime;

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
                finishMatchButton.onClick.RemoveListener(SettlePausedMatchAsDefeat);
                finishMatchButton.onClick.AddListener(SettlePausedMatchAsDefeat);
            }
            if (pausePanel != null && match.State != MatchState.Paused)
            {
                pausePanel.SetActive(false);
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
            itemRuntime = runtime;
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
                finishMatchButton.onClick.RemoveListener(SettlePausedMatchAsDefeat);
            }

            ReleaseGlobalPause();

            RemoveActiveItemListeners();
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

            activeItemContainer = background.Find("ActiveItemContainer") as RectTransform;
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

            var authoredPanel = background.Find("PausePanel");
            if (authoredPanel != null)
            {
                pausePanel = authoredPanel.gameObject;
                finishMatchButton = authoredPanel.Find("Bg/PauseBtn")?.GetComponent<Button>();
                continueButton = authoredPanel.Find("Bg/ContinueBtn")?.GetComponent<Button>();
                EnsureOverlayCanvas(pausePanel, PausePanelSortingOrder, true);
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

        private void SettlePausedMatchAsDefeat()
        {
            if (match == null || match.State != MatchState.Paused)
            {
                return;
            }

            // Leaving from the pause panel is a normal completed loss, not an abnormal exit.
            // MatchController notifies every existing settlement/diagnostic consumer through
            // its ordinary Defeat transition.
            if (!match.TryTransition(MatchState.Defeat))
            {
                return;
            }

            ReleaseGlobalPause();
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
                pauseButton.interactable = match.State != MatchState.Paused;
            }
            RefreshActiveItemSlots();
        }

        private void EnsureActiveItemSlots()
        {
            if (activeItemSlotOne != null && activeItemSlotTwo != null)
            {
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
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(slot.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label = labelObject.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        }

        private void RemoveActiveItemListeners()
        {
            activeItemSlotOne?.onClick.RemoveListener(UseFirstActiveItem);
            activeItemSlotTwo?.onClick.RemoveListener(UseSecondActiveItem);
        }

        private void UseFirstActiveItem() { TryUseActiveItem(0); }
        private void UseSecondActiveItem() { TryUseActiveItem(1); }

        private void TryUseActiveItem(int slot)
        {
            var snapshot = itemRuntime?.PlayerItems?.Snapshot;
            if (snapshot == null || slot < 0 || slot >= snapshot.ActiveItems.Count)
            {
                return;
            }

            itemRuntime.TryUseItem(TeamSide.Player, snapshot.ActiveItems[slot], out _);
            Refresh();
        }

        private void RefreshActiveItemSlots()
        {
            RefreshActiveItemSlot(activeItemSlotOne, activeItemSlotOneLabel, 0);
            RefreshActiveItemSlot(activeItemSlotTwo, activeItemSlotTwoLabel, 1);
        }

        private void RefreshActiveItemSlot(Button button, Text label, int slot)
        {
            if (button == null || label == null)
            {
                return;
            }

            var snapshot = itemRuntime?.PlayerItems?.Snapshot;
            if (snapshot == null || slot >= snapshot.ActiveItems.Count)
            {
                label.text = "EMPTY";
                button.interactable = false;
                return;
            }

            var itemId = snapshot.ActiveItems[slot];
            var cooldown = itemRuntime.PlayerItems.GetCooldownRemainingSeconds(itemId);
            button.interactable = cooldown <= 0.0001f && match != null && match.State == MatchState.Running;
            label.text = cooldown > 0.0001f
                ? itemId + "\nCD " + Mathf.CeilToInt(cooldown) + "s"
                : itemId + "\nREADY";
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
