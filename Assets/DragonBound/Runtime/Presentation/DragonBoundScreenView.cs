using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
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
        [SerializeField] private FixedBoardCanvasView fixedBoardCanvas;

        public GreyboxBoardView BoardView => PlayerBoardView;
        public GreyboxBoardView PlayerBoardView => playerBattlefieldView != null ? playerBattlefieldView.BoardView : null;
        public GreyboxBoardView AiBoardView => aiBattlefieldView != null ? aiBattlefieldView.BoardView : null;
        public GreyboxBattlefieldSideView PlayerBattlefieldView => playerBattlefieldView;
        public GreyboxBattlefieldSideView AiBattlefieldView => aiBattlefieldView;
        public GreyboxHudView HudView => hudView;
        public GreyboxRecruitmentPanel RecruitmentView => recruitmentView;
        public HeroWorkshopView HeroWorkshopView => heroWorkshopView;
        public FixedBoardCanvasView FixedBoardCanvas => fixedBoardCanvas;

        public void Configure(
            GreyboxBattlefieldSideView aiBattlefield,
            GreyboxBattlefieldSideView playerBattlefield,
            GreyboxHudView hud,
            GreyboxRecruitmentPanel recruitment,
            HeroWorkshopView workshop = null)
        {
            aiBattlefieldView = aiBattlefield;
            playerBattlefieldView = playerBattlefield;
            hudView = hud;
            recruitmentView = recruitment;
            heroWorkshopView = workshop;
        }

        public void ConfigureAuthoredFixedBoard(FixedBoardCanvasView canvas)
        {
            fixedBoardCanvas = canvas;
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
            recruitmentView.SetWorkshopButton(ResolveBenchWorkshopButton());
            recruitmentView.Initialize(match.Player, recruitment, PlayerBoardView);
            if (heroWorkshopView != null)
            {
                heroWorkshopView.Initialize(recruitment, playerRecruitDestination);
                recruitmentView.WorkshopRequested += OpenHeroWorkshop;
            }
        }

        public void BindWaveRuntime(ThreeWaveSliceRuntime runtime)
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
            if (recruitmentView != null)
            {
                recruitmentView.WorkshopRequested -= OpenHeroWorkshop;
            }
        }

        private void OpenHeroWorkshop()
        {
            heroWorkshopView?.Open();
        }

        private Button ResolveBenchWorkshopButton()
        {
            var badge = FindDescendant(transform, "ART_BenchBadge");
            if (badge == null)
            {
                throw new System.InvalidOperationException(
                    "The editable screen prefab is missing ART_BenchBadge.");
            }

            var image = badge.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }

            var button = badge.GetComponent<Button>();
            if (button == null)
            {
                button = badge.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
            }
            return button;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            foreach (Transform child in root)
            {
                if (child.name == objectName) return child;
                var nested = FindDescendant(child, objectName);
                if (nested != null) return nested;
            }
            return null;
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
                fixedBoardCanvas = transform.Find("ART_FixedBoardCanvas")
                    ?.GetComponent<FixedBoardCanvasView>();
            }

            if (fixedBoardCanvas == null)
            {
                throw new System.InvalidOperationException(
                    "The editable screen prefab is missing ART_FixedBoardCanvas. " +
                    "Run DragonBound/UI/Bake Editable Fixed Board before entering Play mode.");
            }

            fixedBoardCanvas.BindAuthoredLayout(fixedLayout);
            aiBattlefieldView.ConfigureFixedBoardCanvas(fixedBoardCanvas);
            playerBattlefieldView.ConfigureFixedBoardCanvas(fixedBoardCanvas);
            hudView?.SetDebugOverlayVisible(false);
        }
    }
}
