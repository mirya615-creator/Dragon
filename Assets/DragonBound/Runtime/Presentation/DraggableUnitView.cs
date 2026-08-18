using DragonBound.Grid;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class DraggableUnitView : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private const float FallbackDragThresholdPixels = 10f;

        [SerializeField] private Image artImage;
        [SerializeField] private Text label;
        [SerializeField] private CanvasGroup canvasGroup;
        [Header("Optional authored hero presentation")]
        [SerializeField] private Image rarityBorder;
        [SerializeField] private Text heroLevelLabel;
        [SerializeField] private Text heroExperienceLabel;
        [SerializeField] private Image heroExperienceFill;

        private GreyboxBoardView boardView;
        private string unitId;
        private bool interactive = true;
        private bool dragging;
        private bool pairedPresentation;
        private Color authoredArtColor = Color.white;
        private readonly FixedSlotDragGesture gesture = new FixedSlotDragGesture();

        public RectTransform RectTransform => (RectTransform)transform;
        public Image ArtImage => artImage;
        public Image RarityBorder => rarityBorder;
        public Text HeroLevelLabel => heroLevelLabel;
        public Text HeroExperienceLabel => heroExperienceLabel;
        public Image HeroExperienceFill => heroExperienceFill;
        public bool IsPairedPresentationHidden => pairedPresentation;
        public bool IsDragging => dragging;

        public void Configure(Image art, Text valueLabel, CanvasGroup group)
        {
            artImage = art;
            label = valueLabel;
            canvasGroup = group;
        }

        public void ConfigureHeroPresentation(
            Image border,
            Text levelText,
            Text experienceText,
            Image experienceFill)
        {
            rarityBorder = border;
            heroLevelLabel = levelText;
            heroExperienceLabel = experienceText;
            heroExperienceFill = experienceFill;
        }

        public void Initialize(GreyboxBoardView value, string id)
        {
            boardView = value;
            unitId = id;
            authoredArtColor = artImage != null ? artImage.color : Color.white;
            SetLabel("U");
            SetHeroDetailsVisible(false);
        }

        public void SetInteractive(bool value)
        {
            interactive = value;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = value;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = value;
                canvasGroup.blocksRaycasts = value;
            }
        }

        public void SetPairedPresentation(bool value)
        {
            pairedPresentation = value;
            if (canvasGroup != null && !dragging)
            {
                canvasGroup.alpha = value ? 0f : 1f;
                canvasGroup.blocksRaycasts = interactive;
            }
        }

        public void SetLabel(string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        public void SetStandardPresentation()
        {
            SetHeroDetailsVisible(false);
            if (artImage != null)
            {
                artImage.color = authoredArtColor;
            }
        }

        // Reuses the authored level corner so basic cards keep a short, readable main label.
        public void SetBasicLevel(int level)
        {
            if (heroLevelLabel == null)
            {
                return;
            }

            heroLevelLabel.gameObject.SetActive(true);
            heroLevelLabel.text = $"Lv{level}";
        }

        public void SetHeroPresentation(
            string heroName,
            int level,
            string experienceText,
            float experienceProgress,
            Color rarityColor,
            bool formationComplete,
            float formationProgress)
        {
            var hasAuthoredDetails = heroLevelLabel != null && heroExperienceLabel != null;
            SetLabel(hasAuthoredDetails
                ? heroName
                : $"{heroName}\nLv{level}  XP {experienceText}");

            if (heroLevelLabel != null)
            {
                heroLevelLabel.gameObject.SetActive(true);
                heroLevelLabel.text = $"Lv{level}";
            }

            if (heroExperienceLabel != null)
            {
                heroExperienceLabel.gameObject.SetActive(true);
                heroExperienceLabel.text = $"XP {experienceText}";
            }

            if (heroExperienceFill != null)
            {
                heroExperienceFill.gameObject.SetActive(true);
                heroExperienceFill.fillAmount = Mathf.Clamp01(experienceProgress);
            }

            if (rarityBorder != null)
            {
                rarityBorder.gameObject.SetActive(true);
                var borderColor = rarityColor;
                var outline = rarityBorder.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = borderColor;
                    outline.useGraphicAlpha = false;
                    borderColor.a = 0.04f;
                }
                else
                {
                    borderColor.a = 0.24f;
                }

                rarityBorder.color = borderColor;
            }

            if (artImage != null)
            {
                var pulse = formationComplete
                    ? 1f
                    : Mathf.Lerp(0.55f, 1f, Mathf.PingPong(formationProgress * 4f, 1f));
                artImage.color = Color.Lerp(authoredArtColor, rarityColor, formationComplete ? 0.22f : pulse * 0.48f);
            }
        }

        private void SetHeroDetailsVisible(bool visible)
        {
            if (rarityBorder != null)
            {
                rarityBorder.gameObject.SetActive(visible);
            }

            if (heroLevelLabel != null)
            {
                heroLevelLabel.gameObject.SetActive(visible);
            }

            if (heroExperienceLabel != null)
            {
                heroExperienceLabel.gameObject.SetActive(visible);
            }

            if (heroExperienceFill != null)
            {
                heroExperienceFill.gameObject.SetActive(visible);
            }
        }

        public DraggableUnitView CreateDragGhost(RectTransform parent)
        {
            if (parent == null)
            {
                throw new System.ArgumentNullException(nameof(parent));
            }

            var ghost = Instantiate(this, parent);
            ghost.gameObject.name = $"DragGhost_{unitId}";
            ghost.boardView = null;
            ghost.unitId = null;
            ghost.dragging = false;
            ghost.pairedPresentation = false;
            ghost.SetInteractive(false);
            foreach (var graphic in ghost.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            if (ghost.canvasGroup != null)
            {
                ghost.canvasGroup.interactable = false;
                ghost.canvasGroup.blocksRaycasts = false;
                ghost.canvasGroup.alpha = 0.78f;
            }

            ghost.transform.SetAsLastSibling();
            return ghost;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (CanProcessInput())
            {
                gesture.PointerDown(eventData.pointerId, eventData.position.x, eventData.position.y);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var threshold = eventData.useDragThreshold && EventSystem.current != null
                ? EventSystem.current.pixelDragThreshold
                : FallbackDragThresholdPixels;
            if (!CanProcessInput() ||
                !gesture.TryBeginDrag(eventData.pointerId, eventData.position.x, eventData.position.y, threshold))
            {
                return;
            }

            dragging = boardView != null && boardView.BeginDrag(unitId);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging && gesture.OwnsPointer(eventData.pointerId))
            {
                boardView.UpdateDraggedUnit(unitId, eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (gesture.OwnsPointer(eventData.pointerId) && dragging)
            {
                CompleteDrag(eventData.position);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!gesture.OwnsPointer(eventData.pointerId))
            {
                return;
            }

            var isTap = gesture.PointerUp(eventData.pointerId);
            if (dragging)
            {
                CompleteDrag(eventData.position);
                return;
            }

            if (isTap)
            {
                boardView?.SelectUnit(unitId);
            }
        }

        private void CompleteDrag(Vector2 screenPosition)
        {
            dragging = false;
            gesture.Cancel();
            boardView?.CompleteDrag(unitId, screenPosition);
        }

        private bool CanProcessInput()
        {
            return interactive && boardView != null && boardView.AllowInteraction;
        }

        private void OnDisable()
        {
            if (dragging)
            {
                dragging = false;
                boardView?.CancelActiveDrag();
            }

            gesture.Cancel();
        }
    }
}
