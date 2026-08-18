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
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button workshopButton;

        private TeamState team;
        private RecruitmentService recruitment;
        private GreyboxBoardView boardView;

        public RectTransform RecruitButtonRect => (RectTransform)recruitButton.transform;
        public Button RecruitButton => recruitButton;
        public Text RecruitButtonLabel => recruitButtonLabel;
        public Text StatusLabel => statusLabel;
        public event System.Action WorkshopRequested;

        public void SetWorkshopButton(Button button)
        {
            if (workshopButton != null)
            {
                workshopButton.onClick.RemoveListener(OpenWorkshop);
            }

            workshopButton = button;
        }

        public void Configure(Button button, Text buttonLabel, Text status, Button workshop = null)
        {
            recruitButton = button;
            recruitButtonLabel = buttonLabel;
            statusLabel = status;
            workshopButton = workshop;
        }

        public void Initialize(TeamState value, RecruitmentService service, GreyboxBoardView view)
        {
            team = value;
            recruitment = service;
            boardView = view;
            recruitButton.onClick.AddListener(Recruit);
            if (workshopButton != null)
            {
                workshopButton.onClick.AddListener(OpenWorkshop);
            }
            statusLabel.text = string.Empty;
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

            if (workshopButton != null)
            {
                workshopButton.onClick.RemoveListener(OpenWorkshop);
            }
        }

        private void OpenWorkshop()
        {
            WorkshopRequested?.Invoke();
        }

        private void Recruit()
        {
            var attempt = recruitment.TryRecruit();
            if (attempt.Status == RecruitmentStatus.InsufficientResources)
            {
                statusLabel.text = "Not enough resources";
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

            statusLabel.text = attempt.RefreshedBench
                ? $"Refreshed {attempt.RefreshedUnitIds.Count} units"
                : string.Empty;
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
