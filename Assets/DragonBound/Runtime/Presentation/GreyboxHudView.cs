using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Recruitment;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class GreyboxHudView : MonoBehaviour
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private Text pauseLabel;
        [SerializeField] private Text resourceLabel;
        [SerializeField] private Text waveLabel;
        [SerializeField] private Text recruitmentLabel;
        [SerializeField] private Text playerHealthLabel;
        [SerializeField] private Text aiHealthLabel;
        [SerializeField] private Text debugLabel;
        [SerializeField] private Text enemyDebugLabel;
        [SerializeField] private Button activeItemSlotOne;
        [SerializeField] private Button activeItemSlotTwo;
        [SerializeField] private Text activeItemSlotOneLabel;
        [SerializeField] private Text activeItemSlotTwoLabel;
        [SerializeField] private bool showDebugOverlay;

        private MatchController match;
        private TeamState team;
        private RecruitmentService playerRecruitment;
        private RecruitmentService aiRecruitment;
        private BoardRecruitDestination playerRecruitDestination;
        private BoardRecruitDestination aiRecruitDestination;
        private MatchState stateBeforePause = MatchState.Preparing;
        private IWaveRuntime waveRuntime;
        private TwentyWavePressureRuntime itemRuntime;

        public void Configure(
            Button pause,
            Text pauseText,
            Text resources,
            Text wave,
            Text recruitments,
            Text playerHealth,
            Text aiHealth,
            Text debug = null,
            Text enemyDebug = null)
        {
            pauseButton = pause;
            pauseLabel = pauseText;
            resourceLabel = resources;
            waveLabel = wave;
            recruitmentLabel = recruitments;
            playerHealthLabel = playerHealth;
            aiHealthLabel = aiHealth;
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
            pauseButton.onClick.AddListener(TogglePause);
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

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(TogglePause);
            }

            RemoveActiveItemListeners();
        }

        private void TogglePause()
        {
            if (match.State == MatchState.Paused)
            {
                match.TryTransition(stateBeforePause);
            }
            else if (match.State == MatchState.Preparing ||
                     match.State == MatchState.Running ||
                     match.State == MatchState.BossPrompt)
            {
                stateBeforePause = match.State;
                match.TryTransition(MatchState.Paused);
            }

            Refresh();
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
            recruitmentLabel.text = $"Recruits: {team.RecruitmentCount}";
            if (playerHealthLabel != null)
            {
                playerHealthLabel.text = $"PLAYER {FormatHearts(match.Player.HatchlingHealth)}";
            }

            if (aiHealthLabel != null)
            {
                aiHealthLabel.text = $"AI {FormatHearts(match.AI.HatchlingHealth)}";
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
            pauseLabel.text = match.State == MatchState.Paused ? ">" : "II";
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
            slot.transform.SetParent(transform, false);
            var rect = slot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.02f + index * 0.25f, 0.03f);
            rect.anchorMax = new Vector2(0.25f + index * 0.25f, 0.10f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = slot.GetComponent<Image>();
            image.color = new Color(0.16f, 0.20f, 0.24f, 0.9f);
            button = slot.GetComponent<Button>();
            button.targetGraphic = image;
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

        private static string FormatHearts(int health)
        {
            return new string('\u2665', Mathf.Max(0, health));
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
