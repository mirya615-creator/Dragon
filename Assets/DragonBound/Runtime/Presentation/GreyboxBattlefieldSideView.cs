using System;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class GreyboxBattlefieldSideView : MonoBehaviour
    {
        [SerializeField] private TeamSide side;
        [SerializeField] private GreyboxBoardView boardView;
        [SerializeField] private GreyboxLaneView laneView;
        [SerializeField] private Text sideLabel;
        [SerializeField] private Text hatchlingLabel;
        [SerializeField] private Text enemyProgressLabel;
        [SerializeField] private Image bossProgressFill;
        [SerializeField] private CombatFxView combatFxView;

        private TeamState team;

        public TeamSide Side => side;
        public GreyboxBoardView BoardView => boardView;
        public GreyboxLaneView LaneView => laneView;

        public void ConfigureFixedBoardCanvas(FixedBoardCanvasView canvasView)
        {
            if (canvasView == null)
            {
                throw new ArgumentNullException(nameof(canvasView));
            }

            boardView?.ConfigureFixedBoardCanvas(canvasView);
            laneView?.ConfigureFixedBoardCanvas(canvasView);
            combatFxView?.ConfigureFixedBoardCanvas(canvasView);
            if (!canvasView.IsAuthoredLayout)
            {
                HideLegacyFixedBoardVisuals();
            }
        }

        private void HideLegacyFixedBoardVisuals()
        {
            HideChild("ART_Background");
            HideChild("ART_Spawn");
            HideChild("ART_Hatchling");
            HideChild("ART_BossTrack");
            if (sideLabel != null) sideLabel.gameObject.SetActive(false);
            if (hatchlingLabel != null) hatchlingLabel.gameObject.SetActive(false);
            if (enemyProgressLabel != null) enemyProgressLabel.gameObject.SetActive(false);
        }

        private void HideChild(string childName)
        {
            var child = transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        public void Configure(
            TeamSide teamSide,
            GreyboxBoardView board,
            GreyboxLaneView lane,
            Text sideText,
            Text hatchlingText,
            Text progressText,
            Image bossFill,
            CombatFxView combatFx = null)
        {
            side = teamSide;
            boardView = board;
            laneView = lane;
            sideLabel = sideText;
            hatchlingLabel = hatchlingText;
            enemyProgressLabel = progressText;
            bossProgressFill = bossFill;
            combatFxView = combatFx;
            combatFxView?.Initialize(teamSide);
        }

        public void Initialize(
            MatchController match,
            TeamState value,
            BoardGrid board,
            BoardRecruitDestination destination)
        {
            if (value == null || value.Side != side)
            {
                throw new ArgumentException("Battlefield side and team state must match.", nameof(value));
            }

            team = value;
            if (boardView == null)
            {
                throw new InvalidOperationException("The battlefield view is missing its board view reference.");
            }

            if (laneView == null)
            {
                throw new InvalidOperationException("The battlefield view is missing its lane view reference.");
            }

            combatFxView?.BindPresentationSources(laneView, boardView);
            if (board.Layout != null)
            {
                laneView.ConfigureLayout(board.Layout, side);
            }
            boardView.Initialize(board, destination);
            laneView.Initialize(match);
            Refresh();
        }

        public void BindEnemyRegistry(EnemyRegistry registry)
        {
            laneView.BindEnemyRegistry(registry, side);
        }

        public void BindCombatRuntime(IWaveRuntime runtime)
        {
            combatFxView?.Bind(runtime);
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (team == null)
            {
                return;
            }

            if (sideLabel != null)
            {
                sideLabel.text = side == TeamSide.Player ? "PLAYER" : "AI";
            }
            if (hatchlingLabel != null)
            {
                hatchlingLabel.text = "HATCHLING";
            }
            if (enemyProgressLabel != null)
            {
                enemyProgressLabel.text = $"ENEMIES {team.RemainingEnemyCount}";
            }
            if (bossProgressFill != null)
            {
                bossProgressFill.fillAmount = team.RemainingEnemyCount <= 0
                    ? 0f
                    : Mathf.Clamp01(team.RemainingEnemyCount / 10f);
            }
        }
    }
}
