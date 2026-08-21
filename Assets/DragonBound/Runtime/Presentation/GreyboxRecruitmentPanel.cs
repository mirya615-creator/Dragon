using DragonBound.Core;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class GreyboxRecruitmentPanel : MonoBehaviour
    {
        [SerializeField] private Button recruitButton;
        [SerializeField] private Text recruitButtonLabel;

        private TeamState team;
        private RecruitmentService recruitment;
        private GreyboxBoardView boardView;

        public RectTransform RecruitButtonRect => (RectTransform)recruitButton.transform;
        public Button RecruitButton => recruitButton;
        public Text RecruitButtonLabel => recruitButtonLabel;

        public void Configure(
            Button button,
            Text buttonLabel)
        {
            recruitButton = button;
            recruitButtonLabel = buttonLabel;
        }

        public void Initialize(TeamState value, RecruitmentService service, GreyboxBoardView view)
        {
            team = value;
            recruitment = service;
            boardView = view;
            recruitButton.onClick.AddListener(Recruit);
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
            var attempt = recruitment.TryRecruit();
            if (attempt.Status == RecruitmentStatus.InsufficientResources)
            {
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
            if (recruitment != null && team != null)
            {
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
}
