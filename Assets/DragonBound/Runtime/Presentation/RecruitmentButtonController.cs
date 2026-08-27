using System;
using System.Collections;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class RecruitmentButtonController : MonoBehaviour
    {
        [SerializeField] private Button recruitButton;
        [SerializeField] private Text recruitButtonLabel;
        [SerializeField] private TMP_Text tipText;

        private TeamState team;
        private RecruitmentService recruitment;
        private GreyboxBoardView boardView;
        private bool initialized;
        private bool unavailableAtPointerDown;
        private Coroutine tipHideCoroutine;

        public Button RecruitButton => recruitButton;
        public Text RecruitButtonLabel => recruitButtonLabel;

        public void Initialize(
            TeamState playerTeam,
            RecruitmentService recruitmentService,
            GreyboxBoardView playerBoardView,
            Button button,
            Text buttonLabel,
            TMP_Text unavailableTipText)
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
            tipText = unavailableTipText;

            recruitButton.onClick.RemoveListener(Recruit);
            recruitButton.onClick.AddListener(Recruit);
            BindUnavailableClick();
            HideTip();
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
                ShowUnavailableReason();
                RefreshButton();
                return;
            }

            HideTip();
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

            recruitButton.interactable = recruitment.CanRecruitNext;
            recruitButtonLabel.text = recruitment.NextCost.ToString();
            if (recruitment.CanRecruitNext)
            {
                HideTip();
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
            if (!initialized || recruitment == null || recruitment.CanRecruitNext || tipText == null)
            {
                return;
            }

            tipText.text = !recruitment.CanAffordNext
                ? $"Not enough Supplies. Need {recruitment.NextCost}."
                : "Recruitment is currently unavailable.";
            tipText.gameObject.SetActive(true);
            if (tipHideCoroutine != null)
            {
                StopCoroutine(tipHideCoroutine);
            }

            tipHideCoroutine = StartCoroutine(HideTipAfterDelay());
        }

        private void HideTip()
        {
            if (tipHideCoroutine != null)
            {
                StopCoroutine(tipHideCoroutine);
                tipHideCoroutine = null;
            }

            if (tipText != null)
            {
                tipText.gameObject.SetActive(false);
            }
        }

        private IEnumerator HideTipAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            tipHideCoroutine = null;
            if (tipText != null)
            {
                tipText.gameObject.SetActive(false);
            }
        }
    }
}
