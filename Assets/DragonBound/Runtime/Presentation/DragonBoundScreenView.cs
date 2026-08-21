using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using DragonBound.Runes;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DragonBoundScreenView : MonoBehaviour
    {
        [SerializeField] private GreyboxBattlefieldSideView aiBattlefieldView;
        [SerializeField] private GreyboxBattlefieldSideView playerBattlefieldView;
        [SerializeField] private GreyboxHudView hudView;
        [SerializeField] private GreyboxRecruitmentPanel recruitmentView;
        [SerializeField] private HeroWorkshopView heroWorkshopView;
        [SerializeField] private RuneLoadoutView runeLoadoutView;
        [SerializeField] private ItemLoadoutView itemLoadoutView;
        [SerializeField] private FixedBoardCanvasView fixedBoardCanvas;
        [SerializeField] private BoardBackgroundClickReceiver rangeDismissSurface;
        [SerializeField] private Button itemEntryButton;

        public GreyboxBoardView BoardView => PlayerBoardView;
        public GreyboxBoardView PlayerBoardView => playerBattlefieldView != null ? playerBattlefieldView.BoardView : null;
        public GreyboxBoardView AiBoardView => aiBattlefieldView != null ? aiBattlefieldView.BoardView : null;
        public GreyboxBattlefieldSideView PlayerBattlefieldView => playerBattlefieldView;
        public GreyboxBattlefieldSideView AiBattlefieldView => aiBattlefieldView;
        public GreyboxHudView HudView => hudView;
        public GreyboxRecruitmentPanel RecruitmentView => recruitmentView;
        public HeroWorkshopView HeroWorkshopView => heroWorkshopView;
        public RuneLoadoutView RuneLoadoutView => runeLoadoutView;
        public ItemLoadoutView ItemLoadoutView => itemLoadoutView;
        public bool IsRuneLoadoutOpen => runeLoadoutView != null && runeLoadoutView.IsOpen;
        public FixedBoardCanvasView FixedBoardCanvas => fixedBoardCanvas;

        public void Configure(
            GreyboxBattlefieldSideView aiBattlefield,
            GreyboxBattlefieldSideView playerBattlefield,
            GreyboxHudView hud,
            GreyboxRecruitmentPanel recruitment,
            HeroWorkshopView workshop = null,
            RuneLoadoutView runeLoadout = null)
        {
            aiBattlefieldView = aiBattlefield;
            playerBattlefieldView = playerBattlefield;
            hudView = hud;
            recruitmentView = recruitment;
            heroWorkshopView = workshop;
            runeLoadoutView = runeLoadout;
        }

        public void ConfigureItemLoadout(ItemLoadoutView itemLoadout)
        {
            itemLoadoutView = itemLoadout;
        }

        public void ConfigureAuthoredUi(
            FixedBoardCanvasView canvas,
            BoardBackgroundClickReceiver dismissSurface,
            Button itemButton)
        {
            fixedBoardCanvas = canvas;
            rangeDismissSurface = dismissSurface;
            itemEntryButton = itemButton;
        }

        public void Initialize(
            MatchController match,
            BoardGrid playerBoard,
            BoardGrid aiBoard,
            RecruitmentService recruitment,
            RecruitmentService aiRecruitment,
            BoardRecruitDestination playerRecruitDestination,
            BoardRecruitDestination aiRecruitDestination,
            RuneLoadoutService runeLoadoutService = null,
            System.Func<bool> canEditRuneLoadout = null,
            DragonBound.Items.DevelopmentItemRunSnapshotProvider itemSnapshotProvider = null)
        {
            if (aiBattlefieldView == null ||
                playerBattlefieldView == null ||
                hudView == null ||
                recruitmentView == null)
            {
                throw new System.InvalidOperationException(
                    $"Editable screen prefab references are incomplete. " +
                    $"AI={aiBattlefieldView != null} Player={playerBattlefieldView != null} " +
                    $"Hud={hudView != null} Recruitment={recruitmentView != null}");
            }

            ConfigureFixedBoardCanvas(playerBoard, aiBoard);
            BindRangeDismissSurface();

            aiBattlefieldView.Initialize(match, match.AI, aiBoard, aiRecruitDestination);
            playerBattlefieldView.Initialize(match, match.Player, playerBoard, playerRecruitDestination);
            AiBoardView.BindRecruitment(aiRecruitment);
            PlayerBoardView.BindRecruitment(recruitment);
            hudView.Initialize(
                match,
                match.Player,
                recruitment,
                aiRecruitment,
                playerRecruitDestination,
                aiRecruitDestination);
            recruitmentView.Initialize(match.Player, recruitment, PlayerBoardView);
            if (heroWorkshopView != null)
            {
                heroWorkshopView.Initialize(recruitment, playerRecruitDestination);
                recruitmentView.WorkshopRequested += OpenHeroWorkshop;
            }
            if (runeLoadoutView != null && runeLoadoutService != null)
            {
                runeLoadoutView.Initialize(runeLoadoutService, canEditRuneLoadout);
                recruitmentView.RuneLoadoutRequested += OpenRuneLoadout;
            }
            if (itemLoadoutView != null && itemSnapshotProvider != null)
            {
                itemLoadoutView.Initialize(itemSnapshotProvider, () => match != null && match.State == MatchState.Ready);
                BindItemEntryButton();
            }
            else if (itemSnapshotProvider != null)
            {
                throw new System.InvalidOperationException(
                    "The authored Item loadout view is missing from DragonBoundPortraitScreen.");
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
            hudView.BindWaveRuntime(runtime);
        }

        private void OnDestroy()
        {
            if (rangeDismissSurface != null)
            {
                rangeDismissSurface.Clicked -= HandleRangeDismissClick;
            }
            if (itemEntryButton != null)
            {
                itemEntryButton.onClick.RemoveListener(OpenItemLoadout);
            }

            if (recruitmentView != null)
            {
                recruitmentView.WorkshopRequested -= OpenHeroWorkshop;
                recruitmentView.RuneLoadoutRequested -= OpenRuneLoadout;
            }
        }

        private void BindItemEntryButton()
        {
            if (itemEntryButton == null)
            {
                throw new System.InvalidOperationException(
                    "The authored ItemEntryButton is missing from DragonBoundPortraitScreen.");
            }

            itemEntryButton.onClick.RemoveListener(OpenItemLoadout);
            itemEntryButton.onClick.AddListener(OpenItemLoadout);
        }

        private void OpenItemLoadout()
        {
            itemLoadoutView?.Open();
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

        private void OpenHeroWorkshop()
        {
            heroWorkshopView?.Open();
        }

        private void OpenRuneLoadout()
        {
            runeLoadoutView?.Open();
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
