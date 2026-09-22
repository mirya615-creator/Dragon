using System;
using DragonBound.Presentation;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using DragonBound.UI;
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

        private TeamState team;
        private RecruitmentService recruitment;
        private GreyboxBoardView boardView;
        private RecruitButtonResourceProgress resourceProgress;
        private bool initialized;
        private bool unavailableAtPointerDown;
        private bool dropAnimationInFlight;

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
            resourceProgress = RecruitButtonResourceProgress.Attach(recruitButton);

            recruitButton.onClick.RemoveListener(Recruit);
            recruitButton.onClick.AddListener(Recruit);
            BindUnavailableClick();
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

            if (dropAnimationInFlight)
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

            // 收集本次新放入的 5 个 runtimeId（顺序与 BoardRecruitDestination.Commit 的
            // board.TryPlace 一致），先标记再 RefreshUnits —— 这样 RefreshUnitsCore 在
            // SnapUnit 之后会立刻把 view 视觉隐藏，避免用户看到 view 突然出现在 bench 上。
            var newRuntimeIds = new List<string>(attempt.Batch.Cards.Count);
            foreach (var card in attempt.Batch.Cards)
            {
                newRuntimeIds.Add(card.RuntimeId);
            }
            boardView.MarkUnitsForHiddenFlight(newRuntimeIds);

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

            dropAnimationInFlight = true;
            recruitButton.interactable = false;
            boardView.PlayRecruitDropStagger(
                newRuntimeIds,
                recruitButton.transform.position,
                perCardStaggerSeconds: 0.10f,
                perCardDurationSeconds: 0.34f,
                arcHeightInCells: -0.55f,
                onCompleted: () =>
                {
                    // 飞行结束后再 RefreshUnits 一次，让 SetUnitPresentation 写入的
                    // label/range 真正画到落定的 view 上（飞行期间 view 被隐藏，label
                    // 写入字典但没刷到 view，所以需要这次补刷）。
                    boardView.RefreshUnits();
                    dropAnimationInFlight = false;
                    RefreshButton();
                });
        }

        private void RefreshButton()
        {
            if (!initialized || recruitment == null || team == null ||
                recruitButton == null || recruitButtonLabel == null)
            {
                return;
            }

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
            if (!initialized || recruitment == null || recruitment.CanRecruitNext)
            {
                return;
            }

            string message = !recruitment.CanAffordNext
                ? $"Not enough Supplies. Need {recruitment.EffectiveNextCost}."
                : "Recruitment is currently unavailable.";
            TipTextService.Show(message, 3f);
        }
    }

    /// <summary>
    /// Keeps the disabled recruit button grey while revealing the collected-resource
    /// portion of its original artwork from left to right.
    /// </summary>
    internal sealed class RecruitButtonResourceProgress
    {
        private const string FillObjectName = "RecruitProgressFill";

        private Button button;
        private Image sourceImage;
        private Image fillImage;

        public static RecruitButtonResourceProgress Attach(Button targetButton)
        {
            if (targetButton == null)
            {
                return null;
            }

            var progress = new RecruitButtonResourceProgress();
            progress.Bind(targetButton);
            return progress;
        }

        public void Refresh(int collectedResources, int requiredResources, bool isResourceBlocked)
        {
            EnsureFillImage();
            if (fillImage == null)
            {
                return;
            }

            SyncArtwork();
            fillImage.gameObject.SetActive(isResourceBlocked && requiredResources > 0);
            fillImage.fillAmount = requiredResources <= 0
                ? 1f
                : Mathf.Clamp01((float)Mathf.Max(0, collectedResources) / requiredResources);
        }

        private void Bind(Button targetButton)
        {
            button = targetButton;
            sourceImage = button.targetGraphic as Image;
            if (sourceImage == null)
            {
                sourceImage = button.GetComponent<Image>();
            }

            EnsureFillImage();
            SyncArtwork();
        }

        private void EnsureFillImage()
        {
            if (fillImage != null)
            {
                return;
            }

            if (button == null)
            {
                return;
            }

            var existing = button.transform.FindUi(FillObjectName);
            if (existing != null)
            {
                fillImage = existing.GetComponent<Image>();
            }

            if (fillImage == null)
            {
                var fillObject = new GameObject(
                    FillObjectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var fillRect = (RectTransform)fillObject.transform;
                fillRect.SetParent(button.transform, false);
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                fillImage = fillObject.GetComponent<Image>();
            }

            fillImage.transform.SetAsFirstSibling();
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillClockwise = true;
            fillImage.gameObject.SetActive(false);
        }

        private void SyncArtwork()
        {
            if (sourceImage == null || fillImage == null)
            {
                return;
            }

            fillImage.sprite = sourceImage.sprite;
            fillImage.overrideSprite = sourceImage.overrideSprite;
            fillImage.color = sourceImage.color;
            fillImage.material = sourceImage.material;
            fillImage.preserveAspect = sourceImage.preserveAspect;
            fillImage.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
        }
    }
}
