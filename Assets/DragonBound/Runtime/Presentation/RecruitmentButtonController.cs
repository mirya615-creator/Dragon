using System;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class RecruitmentButtonController : MonoBehaviour
    {
        [SerializeField] private Button recruitButton;
        [SerializeField] private Text recruitButtonLabel;

        private TeamState team;
        private RecruitmentService recruitment;
        private GreyboxBoardView boardView;
        private bool initialized;

        public Button RecruitButton => recruitButton;
        public Text RecruitButtonLabel => recruitButtonLabel;

        public void Initialize(
            TeamState playerTeam,
            RecruitmentService recruitmentService,
            GreyboxBoardView playerBoardView,
            Button button,
            Text buttonLabel)
        {
            if (initialized)
            {
                return;
            }

            team = playerTeam ?? throw new ArgumentNullException(nameof(playerTeam));
            recruitment = recruitmentService ?? throw new ArgumentNullException(nameof(recruitmentService));
            boardView = playerBoardView ?? throw new ArgumentNullException(nameof(playerBoardView));
            recruitButton = button ?? throw new ArgumentNullException(nameof(button));
            recruitButtonLabel = buttonLabel ?? throw new ArgumentNullException(nameof(buttonLabel));

            recruitButton.onClick.RemoveListener(Recruit);
            recruitButton.onClick.AddListener(Recruit);
            initialized = true;
            RefreshButton();
        }

        private void LateUpdate()
        {
            RefreshButton();
        }

        private void OnDestroy()
        {
            if (recruitButton != null)
            {
                recruitButton.onClick.RemoveListener(Recruit);
            }
        }

        private void Recruit()
        {
            if (!initialized)
            {
                return;
            }

            var attempt = recruitment.TryRecruit();
            if (attempt.Status != RecruitmentStatus.Success || attempt.Batch == null)
            {
                RefreshButton();
                return;
            }

            boardView.RefreshUnits();
            foreach (var card in attempt.Batch.Cards)
            {
                boardView.SetUnitPresentation(
                    card.RuntimeId,
                    HeroSliceCardPresentation.GetLabel(card, recruitment),
                    card.Kind == RecruitItemKind.BasicUnit
                        ? UnitRangeRules.GetRadiusForConfig(card.ConfigId)
                        : 0f,
                    card.Kind == RecruitItemKind.BasicUnit);
            }

            RefreshButton();
        }

        private void RefreshButton()
        {
            if (!initialized || recruitment == null || team == null ||
                recruitButton == null || recruitButtonLabel == null)
            {
                return;
            }

            recruitButton.interactable = recruitment.CanAffordNext;
            if (recruitment.PendingRefreshCount > 0 &&
                recruitment.PendingRefreshContainsUniqueHeroComponent)
            {
                recruitButtonLabel.text =
                    $"REFRESH WILL LOSE UNIQUE\nCOST {recruitment.NextCost}";
            }
            else if (recruitment.PendingRefreshCount > 0)
            {
                recruitButtonLabel.text =
                    $"REFRESH {recruitment.PendingRefreshCount} LEFT\nCOST {recruitment.NextCost}";
            }
            else if (!recruitment.CanAffordNext)
            {
                recruitButtonLabel.text = $"Need {recruitment.NextCost} Supplies";
            }
            else
            {
                recruitButtonLabel.text = $"RECRUIT\n{recruitment.NextCost} Supplies";
            }
        }
    }
}
