using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
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
                recruitmentView.Initialize(match.Player, recruitment, PlayerBoardView);
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

            var host = transform.Find("ART_ScreenBackground/GameOverlayController") ??
                       transform.Find("GameOverlayController");
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

            var campPanel = transform.Find("ART_ScreenBackground/campPanel");
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

            var host = transform.Find("ART_ScreenBackground/RecruitmentButtonController") ??
                       transform.Find("ART_ScreenBackground/ART_RecruitButton");
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
            var target = transform.Find("ART_ScreenBackground/ART_RecruitButton");
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
            var target = transform.Find(
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
    }
}
