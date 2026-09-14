using DragonBound.Core;
using DragonBound.Combat;
using DragonBound.Grid;
using DragonBound.Recruitment;
using DragonBound.UI;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private RecruitButtonResourceProgress resourceProgress;
        private bool unavailableAtPointerDown;

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

        public void Initialize(
            TeamState value,
            RecruitmentService service,
            GreyboxBoardView view)
        {
            team = value;
            recruitment = service;
            boardView = view;
            resourceProgress = RecruitButtonResourceProgress.Attach(recruitButton);
            recruitButton.onClick.AddListener(Recruit);
            BindUnavailableClick();
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
            if (attempt.Status != RecruitmentStatus.Success || attempt.Batch == null)
            {
                ShowUnavailableReason();
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
                recruitButton.interactable = recruitment.CanRecruitNext;
                recruitButtonLabel.text = recruitment.HasFreeRecruit
                    ? "FREE"
                    : recruitment.NextCost.ToString();
                resourceProgress ??= RecruitButtonResourceProgress.Attach(recruitButton);
                resourceProgress.Refresh(
                    team.Resources,
                    recruitment.EffectiveNextCost,
                    !recruitment.CanAffordNext);
            }
        }

        private void BindUnavailableClick()
        {
            var trigger = recruitButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = recruitButton.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers ??= new System.Collections.Generic.List<EventTrigger.Entry>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(_ =>
                unavailableAtPointerDown = recruitment != null && !recruitment.CanRecruitNext);
            trigger.triggers.Add(pointerDown);

            var pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pointerClick.callback.AddListener(_ =>
            {
                if (unavailableAtPointerDown)
                {
                    ShowUnavailableReason();
                }
            });
            trigger.triggers.Add(pointerClick);
        }

        private void ShowUnavailableReason()
        {
            if (recruitment == null || recruitment.CanRecruitNext)
            {
                return;
            }

            string message = !recruitment.CanAffordNext
                ? $"Not enough Supplies. Need {recruitment.EffectiveNextCost}."
                : "Recruitment is currently unavailable.";
            TipTextService.Show(message, 3f);
        }

    }
}
