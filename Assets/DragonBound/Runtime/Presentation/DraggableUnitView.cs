using System.Collections;
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
        [SerializeField] private Graphic alternateLabel;
        [SerializeField] private Graphic levelLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [Header("Optional authored level presentation")]
        [SerializeField] private Text heroLevelLabel;
        [Header("Soul Chain presentation")]
        [SerializeField] private Image soulChainOverlay;

        private GreyboxBoardView boardView;
        private string unitId;
        private bool interactive = true;
        private bool dragging;
        private bool pairedPresentation;
        private bool isBeachPresentation;
        private Color authoredArtColor = Color.white;
        private bool hasAuthoredArtColor;
        private Sprite authoredArtSprite;
        private Coroutine soulChainVisualCoroutine;
        private bool soulChainControlled;
        private readonly FixedSlotDragGesture gesture = new FixedSlotDragGesture();

        private const float SoulChainFlashPeakAlpha = 0.75f;
        private const float SoulChainDarkAlpha = 0.45f;
        private const float SoulChainFlashHalfPhaseSeconds = 0.12f;
        private const int SoulChainFlashCount = 2;

        public RectTransform RectTransform => (RectTransform)transform;
        public Image ArtImage => artImage;
        public Text HeroLevelLabel => heroLevelLabel;
        public bool IsPairedPresentationHidden => pairedPresentation;
        public bool IsDragging => dragging;
        public bool IsSoulChainControlled => soulChainControlled;

        public void Configure(Image art, Text valueLabel, CanvasGroup group)
        {
            artImage = art;
            label = valueLabel;
            alternateLabel = null;
            levelLabel = null;
            canvasGroup = group;
            isBeachPresentation = false;
            CaptureAuthoredArtColor();
        }

        public void ConfigureBeach(
            Image art,
            Graphic nameValueLabel,
            Graphic levelValueLabel,
            CanvasGroup group)
        {
            artImage = art;
            label = null;
            alternateLabel = nameValueLabel;
            levelLabel = levelValueLabel;
            canvasGroup = group;
            isBeachPresentation = true;
            CaptureAuthoredArtColor();
        }

        public void ConfigureLevelPresentation(Text levelText)
        {
            heroLevelLabel = levelText;
        }

        public void ConfigureSoulChainPresentation(Image overlay)
        {
            soulChainOverlay = overlay;
            if (soulChainOverlay != null)
            {
                soulChainOverlay.raycastTarget = false;
                SetSoulChainOverlayAlpha(0f);
            }
        }

        public void Initialize(GreyboxBoardView value, string id)
        {
            ResetSoulChainVisual();
            boardView = value;
            unitId = id;
            CaptureAuthoredArtColor();
            SetLabel("U");
            SetLevelVisible(false);
        }

        public void SetInteractive(bool value)
        {
            interactive = value;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = graphic != soulChainOverlay && value;
            }

            if (soulChainOverlay != null)
            {
                soulChainOverlay.raycastTarget = false;
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
            SetGraphicText(label, value);
            SetGraphicText(alternateLabel, value);
        }

        public void SetUnitLabelVisibility(bool visible)
        {
            SetGraphicVisibility(label, visible);
        }

        public void SetStandardPresentation()
        {
            SetLevelVisible(false);
            SetUnitLabelVisibility(true);
            SetGraphicText(levelLabel, string.Empty);
            if (artImage != null)
            {
                artImage.sprite = authoredArtSprite;
                artImage.color = authoredArtColor;
            }
        }

        public void SetCardColor(Color color)
        {
            if (artImage != null)
            {
                artImage.color = color;
            }
        }

        public void SetCardSprite(Sprite sprite)
        {
            if (artImage == null)
            {
                return;
            }

            artImage.sprite = sprite;
            artImage.type = Image.Type.Simple;
            artImage.preserveAspect = sprite != null;
        }

        public void SetSoulChainControlled(bool controlled)
        {
            ResolveSoulChainOverlay();
            if (soulChainControlled == controlled)
            {
                return;
            }

            soulChainControlled = controlled;
            if (soulChainVisualCoroutine != null)
            {
                StopCoroutine(soulChainVisualCoroutine);
                soulChainVisualCoroutine = null;
            }

            if (!controlled)
            {
                SetSoulChainOverlayAlpha(0f);
                return;
            }

            if (isActiveAndEnabled)
            {
                soulChainVisualCoroutine = StartCoroutine(PlaySoulChainVisual());
            }
            else
            {
                SetSoulChainOverlayAlpha(SoulChainDarkAlpha);
            }
        }

        private IEnumerator PlaySoulChainVisual()
        {
            var flashDuration = SoulChainFlashHalfPhaseSeconds * SoulChainFlashCount * 2f;
            var elapsed = 0f;
            while (soulChainControlled && elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                var pulse = Mathf.PingPong(
                    elapsed / SoulChainFlashHalfPhaseSeconds,
                    1f);
                SetSoulChainOverlayAlpha(pulse * SoulChainFlashPeakAlpha);
                yield return null;
            }

            if (soulChainControlled)
            {
                SetSoulChainOverlayAlpha(SoulChainDarkAlpha);
            }

            soulChainVisualCoroutine = null;
        }

        private void ResolveSoulChainOverlay()
        {
            if (soulChainOverlay != null)
            {
                return;
            }

            var existing = transform.Find("ART_SoulChainOverlay");
            if (existing != null)
            {
                soulChainOverlay = existing.GetComponent<Image>();
                return;
            }

            var overlayObject = new GameObject(
                "ART_SoulChainOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.layer = gameObject.layer;
            var overlayRect = (RectTransform)overlayObject.transform;
            overlayRect.SetParent(transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.SetAsLastSibling();
            soulChainOverlay = overlayObject.GetComponent<Image>();
            soulChainOverlay.color = new Color(0.14f, 0.08f, 0.18f, 0f);
            soulChainOverlay.raycastTarget = false;
        }

        private void SetSoulChainOverlayAlpha(float alpha)
        {
            if (soulChainOverlay == null)
            {
                return;
            }

            var color = soulChainOverlay.color;
            color.a = Mathf.Clamp01(alpha);
            soulChainOverlay.color = color;
        }

        private void ResetSoulChainVisual(bool resolveOverlay = true)
        {
            soulChainControlled = false;
            if (soulChainVisualCoroutine != null)
            {
                StopCoroutine(soulChainVisualCoroutine);
                soulChainVisualCoroutine = null;
            }

            // OnDisable can run while Unity is changing this object's active state. Creating
            // and parenting the fallback overlay in that callback is forbidden, so teardown
            // paths only clear an overlay that was already resolved during initialization.
            if (resolveOverlay)
            {
                ResolveSoulChainOverlay();
            }

            SetSoulChainOverlayAlpha(0f);
        }

        // Reuses the authored level corner so basic cards keep a short, readable main label.
        public void SetBasicLevel(int level)
        {
            SetGraphicText(levelLabel, level.ToString());
            if (heroLevelLabel == null)
            {
                return;
            }

            heroLevelLabel.gameObject.SetActive(true);
            heroLevelLabel.text = $"Lv{level}";
        }

        public void SetBeachTextVisibility(bool showName, bool showLevel)
        {
            if (!isBeachPresentation)
            {
                return;
            }

            SetGraphicVisibility(alternateLabel, showName);
            SetGraphicVisibility(levelLabel, showLevel);
        }

        private void SetLevelVisible(bool visible)
        {
            if (heroLevelLabel != null)
            {
                heroLevelLabel.gameObject.SetActive(visible);
            }
        }

        private void CaptureAuthoredArtColor()
        {
            if (hasAuthoredArtColor)
            {
                return;
            }

            authoredArtColor = artImage != null ? artImage.color : Color.white;
            authoredArtSprite = artImage != null ? artImage.sprite : null;
            hasAuthoredArtColor = true;
        }

        private static void SetGraphicText(Graphic graphic, string value)
        {
            if (graphic == null)
            {
                return;
            }

            if (graphic is Text legacyText)
            {
                legacyText.text = value;
                return;
            }

            var textProperty = graphic.GetType().GetProperty("text");
            if (textProperty != null && textProperty.CanWrite)
            {
                textProperty.SetValue(graphic, value, null);
            }
        }

        private static void SetGraphicVisibility(Graphic graphic, bool visible)
        {
            if (graphic == null)
            {
                return;
            }

            if (!visible)
            {
                SetGraphicText(graphic, string.Empty);
            }

            if (graphic.gameObject.activeSelf != visible)
            {
                graphic.gameObject.SetActive(visible);
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
            ghost.ResetSoulChainVisual();
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
            ResetSoulChainVisual(false);
        }
    }
}
