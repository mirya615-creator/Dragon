using DragonBound.Core;
using DragonBound.Presentation;
using DragonBound.Bosses.Contracts;
using DragonBound.Bosses.Runtime;
using DragonBound.Grid;
using DragonBound.Recruitment;
using DragonBound.Runes;
using DragonBound.UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DragonBoundScreenView : MonoBehaviour
    {
        [SerializeField] private GreyboxBattlefieldSideView aiBattlefieldView;
        [SerializeField] private GreyboxBattlefieldSideView playerBattlefieldView;
        [FormerlySerializedAs("hudView")]
        [SerializeField] private GreyboxHudView overlayController;
        [SerializeField] private GreyboxRecruitmentPanel recruitmentView;
        [SerializeField] private RecruitmentButtonController recruitmentButtonController;
        [SerializeField] private CampPanelView campPanelView;
        [SerializeField] private FixedBoardCanvasView fixedBoardCanvas;
        [SerializeField] private BoardBackgroundClickReceiver rangeDismissSurface;
        [SerializeField] private GoalHealthView playerGoalHealthView;
        [SerializeField] private GoalHealthView aiGoalHealthView;

        private TwentyWavePressureRuntime runeTipRuntime;
        private TwentyWavePressureRuntime soulChainVisualRuntime;
        private TwentyWavePressureRuntime bloodcrownVisualRuntime;
        private TwentyWavePressureRuntime worldeaterVisualRuntime;

        public GreyboxBoardView BoardView => PlayerBoardView;
        public GreyboxBoardView PlayerBoardView => playerBattlefieldView != null ? playerBattlefieldView.BoardView : null;
        public GreyboxBoardView AiBoardView => aiBattlefieldView != null ? aiBattlefieldView.BoardView : null;
        public GreyboxBattlefieldSideView PlayerBattlefieldView => playerBattlefieldView;
        public GreyboxBattlefieldSideView AiBattlefieldView => aiBattlefieldView;
        public GreyboxHudView OverlayController => overlayController;
        public GreyboxRecruitmentPanel RecruitmentView => recruitmentView;
        public RecruitmentButtonController RecruitmentButtonController => recruitmentButtonController;
        public CampPanelView CampPanelView => campPanelView;
        public FixedBoardCanvasView FixedBoardCanvas => fixedBoardCanvas;
        public GoalHealthView PlayerGoalHealthView => playerGoalHealthView;
        public GoalHealthView AiGoalHealthView => aiGoalHealthView;

        public void Configure(
            GreyboxBattlefieldSideView aiBattlefield,
            GreyboxBattlefieldSideView playerBattlefield,
            GreyboxHudView overlay,
            GreyboxRecruitmentPanel recruitment)
        {
            aiBattlefieldView = aiBattlefield;
            playerBattlefieldView = playerBattlefield;
            overlayController = overlay;
            recruitmentView = recruitment;
        }

        public void ConfigureAuthoredUi(
            FixedBoardCanvasView canvas,
            BoardBackgroundClickReceiver dismissSurface)
        {
            fixedBoardCanvas = canvas;
            rangeDismissSurface = dismissSurface;
        }

        public void Initialize(
            MatchController match,
            BoardGrid playerBoard,
            BoardGrid aiBoard,
            RecruitmentService recruitment,
            RecruitmentService aiRecruitment,
            BoardRecruitDestination playerRecruitDestination,
            BoardRecruitDestination aiRecruitDestination)
        {
            ResolveOverlayController();
            if (aiBattlefieldView == null ||
                playerBattlefieldView == null ||
                overlayController == null)
            {
                throw new System.InvalidOperationException(
                    $"Editable screen prefab references are incomplete. " +
                    $"AI={aiBattlefieldView != null} Player={playerBattlefieldView != null} " +
                    $"Overlay={overlayController != null}");
            }

            ConfigureFixedBoardCanvas(playerBoard, aiBoard);
            BindGoalHealthViews(match);
            BindRangeDismissSurface();

            aiBattlefieldView.Initialize(match, match.AI, aiBoard, aiRecruitDestination);
            playerBattlefieldView.Initialize(match, match.Player, playerBoard, playerRecruitDestination);
            AiBoardView.BindRecruitment(aiRecruitment);
            PlayerBoardView.BindRecruitment(recruitment);
            overlayController.Initialize(
                match,
                match.Player,
                recruitment,
                aiRecruitment,
                playerRecruitDestination,
                aiRecruitDestination);
            if (recruitmentView != null)
            {
                recruitmentView.Initialize(
                    match.Player,
                    recruitment,
                    PlayerBoardView);
            }
            else
            {
                ResolveRecruitmentButtonController();
                recruitmentButtonController.Initialize(
                    match.Player,
                    recruitment,
                    PlayerBoardView,
                    ResolveRecruitButton(),
                    ResolveRecruitButtonLabel());
            }
            ResolveCampPanelView();
            if (campPanelView != null)
            {
                campPanelView.Initialize(recruitment, playerRecruitDestination);
            }
        }

        public void BindWaveRuntime(IWaveRuntime runtime)
        {
            if (runtime == null)
            {
                throw new System.ArgumentNullException(nameof(runtime));
            }

            aiBattlefieldView.BindEnemyRegistry(runtime.AiEnemyRegistry);
            playerBattlefieldView.BindEnemyRegistry(runtime.PlayerEnemyRegistry);
            aiBattlefieldView.BindCombatRuntime(runtime);
            playerBattlefieldView.BindCombatRuntime(runtime);
            overlayController.BindWaveRuntime(runtime);
        }

        public void BindItemRuntime(TwentyWavePressureRuntime runtime)
        {
            if (runtime == null)
            {
                throw new System.ArgumentNullException(nameof(runtime));
            }

            ResolveOverlayController();
            overlayController.BindItemRuntime(runtime);
            BindSoulChainVisuals(runtime);
            BindBloodcrownVisuals(runtime);
            BindWorldeaterVisuals(runtime);
            BindRuneDropTip(runtime);
        }

        private void BindSoulChainVisuals(TwentyWavePressureRuntime runtime)
        {
            if (soulChainVisualRuntime != null)
            {
                soulChainVisualRuntime.SoulChainCastEmitted -= HandleSoulChainVisual;
            }

            soulChainVisualRuntime = runtime;
            soulChainVisualRuntime.SoulChainCastEmitted += HandleSoulChainVisual;
        }

        private void HandleSoulChainVisual(TeamSide side, SoulChainCastEvent value)
        {
            var boardView = side == TeamSide.Player ? PlayerBoardView : AiBoardView;
            boardView?.SetSoulChainControlledUnits(value.ControlledRuntimeIds);
        }

        private void BindBloodcrownVisuals(TwentyWavePressureRuntime runtime)
        {
            if (bloodcrownVisualRuntime != null)
            {
                bloodcrownVisualRuntime.BloodcrownLifecycleEmitted -= HandleBloodcrownVisual;
            }

            bloodcrownVisualRuntime = runtime;
            bloodcrownVisualRuntime.BloodcrownLifecycleEmitted += HandleBloodcrownVisual;
        }

        private void HandleBloodcrownVisual(TeamSide side, BossSkillLifecycleEvent value)
        {
            if (value.Lifecycle != BossSkillLifecycle.Resolve &&
                value.Lifecycle != BossSkillLifecycle.EffectEnded)
            {
                return;
            }

            var boardView = side == TeamSide.Player ? PlayerBoardView : AiBoardView;
            boardView?.SetBloodcrownSuppression(value.Lifecycle == BossSkillLifecycle.Resolve);
        }

        private void BindWorldeaterVisuals(TwentyWavePressureRuntime runtime)
        {
            if (worldeaterVisualRuntime != null)
            {
                worldeaterVisualRuntime.WorldeaterCastEmitted -= HandleWorldeaterVisual;
            }

            worldeaterVisualRuntime = runtime;
            worldeaterVisualRuntime.WorldeaterCastEmitted += HandleWorldeaterVisual;
        }

        private void HandleWorldeaterVisual(TeamSide side, WorldeaterCastEvent value)
        {
            if (value.Kind != WorldeaterCastKind.Devour ||
                value.Outcome != WorldeaterCastOutcome.Resolved)
            {
                return;
            }

            var boardView = side == TeamSide.Player ? PlayerBoardView : AiBoardView;
            boardView?.RefreshUnits();
        }

        private void BindRuneDropTip(TwentyWavePressureRuntime runtime)
        {
            if (runeTipRuntime != null)
            {
                runeTipRuntime.PlayerRuneRewardGranted -= HandleRuneRewardGranted;
            }

            runeTipRuntime = runtime;
            runeTipRuntime.PlayerRuneRewardGranted += HandleRuneRewardGranted;
        }

        private void HandleRuneRewardGranted(RuneReward reward)
        {
            if (reward == null)
            {
                return;
            }

            var message = ResolveRuneDisplayName(reward.RuneId);
            TipTextService.Show(message, 3f);
        }

        private static string ResolveRuneDisplayName(string runeId)
        {
            switch (runeId)
            {
                case "Might": return "Rune of Might";
                case "Farreach": return "Farreach Rune";
                case "Power": return "Power Rune";
                case "Longshot": return "Longshot Rune";
                case "Frostbite": return "Frostbite Rune";
                case "Ricochet": return "Ricochet Rune";
                case "Volley": return "Volley Rune";
                case "BladeTempest": return "Blade Tempest Rune";
                case "Ambush": return "Ambush Rune";
                case "Windhawk": return "Windhawk Rune";
                case "Skybreaker": return "Skybreaker Rune";
                case "Wyrmguard": return "Wyrmguard Rune";
                case "Dragonbloom": return "Dragonbloom Rune";
                case "Warcry": return "Warcry Rune";
                default: return string.IsNullOrWhiteSpace(runeId) ? "Rune" : runeId;
            }
        }

        private void ResolveOverlayController()
        {
            if (overlayController != null)
            {
                return;
            }

            overlayController = GetComponentInChildren<GameOverlayController>(true);
            if (overlayController != null)
            {
                return;
            }

            var host = transform.FindUi("ART_ScreenBackground/GameOverlayController") ??
                       transform.FindUi("GameOverlayController");
            if (host != null)
            {
                overlayController = host.gameObject.AddComponent<GameOverlayController>();
            }
        }

        private void ResolveCampPanelView()
        {
            if (campPanelView != null)
            {
                return;
            }

            var campPanel = transform.FindUi("campPanel") ??
                            transform.FindUi("ART_ScreenBackground/campPanel");
            if (campPanel == null)
            {
                return;
            }

            campPanelView = campPanel.GetComponent<CampPanelView>();
            if (campPanelView == null)
            {
                campPanelView = campPanel.gameObject.AddComponent<CampPanelView>();
            }
        }

        private void ResolveRecruitmentButtonController()
        {
            if (recruitmentButtonController != null)
            {
                return;
            }

            var host = transform.FindUi("ART_ScreenBackground/RecruitmentButtonController") ??
                       transform.FindUi("ART_ScreenBackground/ART_RecruitButton");
            if (host == null)
            {
                throw new System.InvalidOperationException(
                    "ART_ScreenBackground/RecruitmentButtonController is missing.");
            }

            recruitmentButtonController = host.GetComponent<RecruitmentButtonController>();
            if (recruitmentButtonController == null)
            {
                recruitmentButtonController = host.gameObject.AddComponent<RecruitmentButtonController>();
            }
        }

        private Button ResolveRecruitButton()
        {
            var target = transform.FindUi("ART_ScreenBackground/ART_RecruitButton");
            var button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                throw new System.InvalidOperationException(
                    "ART_ScreenBackground/ART_RecruitButton requires a Button component.");
            }

            return button;
        }

        private Text ResolveRecruitButtonLabel()
        {
            var target = transform.FindUi(
                "ART_ScreenBackground/ART_RecruitButton/RecruitButtonLabel");
            var label = target != null ? target.GetComponent<Text>() : null;
            if (label == null)
            {
                throw new System.InvalidOperationException(
                    "ART_RecruitButton/RecruitButtonLabel requires a Text component.");
            }

            return label;
        }

        private void OnDestroy()
        {
            if (runeTipRuntime != null)
            {
                runeTipRuntime.PlayerRuneRewardGranted -= HandleRuneRewardGranted;
            }

            if (soulChainVisualRuntime != null)
            {
                soulChainVisualRuntime.SoulChainCastEmitted -= HandleSoulChainVisual;
            }

            if (bloodcrownVisualRuntime != null)
            {
                bloodcrownVisualRuntime.BloodcrownLifecycleEmitted -= HandleBloodcrownVisual;
            }

            if (worldeaterVisualRuntime != null)
            {
                worldeaterVisualRuntime.WorldeaterCastEmitted -= HandleWorldeaterVisual;
            }

            if (rangeDismissSurface != null)
            {
                rangeDismissSurface.Clicked -= HandleRangeDismissClick;
            }
        }

        private void BindRangeDismissSurface()
        {
            if (rangeDismissSurface == null)
            {
                throw new System.InvalidOperationException(
                    "The authored RangeDismissSurface is missing from DragonBoundPortraitScreen.");
            }

            rangeDismissSurface.Clicked -= HandleRangeDismissClick;
            rangeDismissSurface.Clicked += HandleRangeDismissClick;
        }

        private void HandleRangeDismissClick()
        {
            PlayerBoardView?.HideRangePreview();
            AiBoardView?.HideRangePreview();
        }

        private void ConfigureFixedBoardCanvas(BoardGrid playerBoard, BoardGrid aiBoard)
        {
            if (!(playerBoard?.Layout is FixedBoardLayoutDefinition fixedLayout))
            {
                return;
            }

            if (!(aiBoard?.Layout is FixedBoardLayoutDefinition aiLayout) || aiLayout != fixedLayout)
            {
                throw new System.InvalidOperationException("Both sides must use the same fixed board layout.");
            }

            if (fixedBoardCanvas == null)
            {
                throw new System.InvalidOperationException(
                    "The authored fixed board is missing from DragonBoundPortraitScreen.");
            }

            fixedBoardCanvas.BindAuthored((RectTransform)transform, fixedLayout);
            aiBattlefieldView.ConfigureFixedBoardCanvas(fixedBoardCanvas);
            playerBattlefieldView.ConfigureFixedBoardCanvas(fixedBoardCanvas);
        }

        private void BindGoalHealthViews(MatchController match)
        {
            playerGoalHealthView = BindGoalHealthView("ART_PlayerGoal_7_0", match.Player);
            aiGoalHealthView = BindGoalHealthView("ART_AiGoal_0_9", match.AI);
        }

        private GoalHealthView BindGoalHealthView(string goalName, TeamState team)
        {
            Transform goal = null;
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == goalName)
                {
                    goal = candidate;
                    break;
                }
            }

            var heartRoot = goal?.FindUi("HpBg") as RectTransform;
            if (heartRoot == null)
            {
                return null;
            }

            var view = heartRoot.GetComponent<GoalHealthView>();
            if (view == null)
            {
                view = heartRoot.gameObject.AddComponent<GoalHealthView>();
            }

            view.Initialize(team, heartRoot);
            return view;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GoalHealthView : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<GameObject> hearts =
            new System.Collections.Generic.List<GameObject>();
        private TeamState team;
        private RectTransform heartRoot;
        private GameObject heartTemplate;
        private GridLayoutGroup heartLayout;
        private float minimumLayoutHeight;
        private float fixedBottomPosition;
        private int displayedHealth = -1;

        public int HeartCount => hearts.Count;
        public int VisibleHeartCount
        {
            get
            {
                var count = 0;
                foreach (var heart in hearts)
                {
                    if (heart != null && heart.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Initialize(TeamState value, RectTransform root)
        {
            team = value ?? throw new System.ArgumentNullException(nameof(value));
            heartRoot = root != null
                ? root
                : throw new System.ArgumentNullException(nameof(root));

            hearts.Clear();
            for (var index = 0; index < heartRoot.childCount; index++)
            {
                var child = heartRoot.GetChild(index);
                if (child.name.StartsWith("heart", System.StringComparison.OrdinalIgnoreCase))
                {
                    hearts.Add(child.gameObject);
                }
            }

            if (hearts.Count == 0)
            {
                throw new System.InvalidOperationException(
                    $"{heartRoot.name} must contain an authored heart UI.");
            }

            heartTemplate = hearts[0];
            heartLayout = heartRoot.GetComponent<GridLayoutGroup>();
            if (heartLayout == null)
            {
                throw new System.InvalidOperationException(
                    $"{heartRoot.name} must contain its authored GridLayoutGroup.");
            }

            minimumLayoutHeight = Mathf.Max(0f, heartRoot.rect.height);
            fixedBottomPosition =
                heartRoot.anchoredPosition.y - (heartRoot.rect.height * heartRoot.pivot.y);
            var pivot = heartRoot.pivot;
            pivot.y = 0f;
            heartRoot.pivot = pivot;
            heartRoot.anchoredPosition = new Vector2(
                heartRoot.anchoredPosition.x,
                fixedBottomPosition);

            heartLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            heartLayout.constraintCount = 3;
            heartLayout.startCorner = GridLayoutGroup.Corner.LowerLeft;
            heartLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            heartLayout.childAlignment = TextAnchor.LowerLeft;
            displayedHealth = -1;
            Refresh();
        }

        private void Update()
        {
            if (team != null && displayedHealth != team.HatchlingHealth)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var currentHealth = Mathf.Max(0, team.HatchlingHealth);
            EnsureCapacity(currentHealth);

            for (var index = 0; index < hearts.Count; index++)
            {
                hearts[index].SetActive(index < currentHealth);
            }

            RefreshLayoutSize(currentHealth);

            displayedHealth = currentHealth;
        }

        private void RefreshLayoutSize(int currentHealth)
        {
            if (heartRoot == null || heartLayout == null)
            {
                return;
            }

            var columnCount = Mathf.Max(1, heartLayout.constraintCount);
            var rowCount = Mathf.Max(1, Mathf.CeilToInt(currentHealth / (float)columnCount));
            var requiredHeight =
                heartLayout.padding.vertical +
                (rowCount * heartLayout.cellSize.y) +
                ((rowCount - 1) * heartLayout.spacing.y);
            heartRoot.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(minimumLayoutHeight, requiredHeight));
            heartRoot.anchoredPosition = new Vector2(
                heartRoot.anchoredPosition.x,
                fixedBottomPosition);
        }

        private void EnsureCapacity(int requiredCount)
        {
            while (hearts.Count < requiredCount)
            {
                var clone = Instantiate(heartTemplate, heartRoot, false);
                clone.name = $"heart{hearts.Count + 1}";
                hearts.Add(clone);
            }
        }
    }
}
