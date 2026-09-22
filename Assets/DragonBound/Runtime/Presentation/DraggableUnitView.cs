using System;
using DragonBound.Presentation;
using System.Collections;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Grid;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class DraggableUnitView : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private const float FallbackDragThresholdPixels = 10f;
        private const float InputSizePixels = 100f;
        private const string InputReceiverName = "InputReceiver";

        [SerializeField] private Image artImage;
        [SerializeField] private Text label;
        [SerializeField] private Graphic alternateLabel;
        [SerializeField] private Graphic levelLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Animator basicAttackAnimator;
        [SerializeField] private Graphic inputReceiver;
        [Header("Optional authored level presentation")]
        [SerializeField] private Text heroLevelLabel;
        [Header("Soul Chain presentation")]
        [SerializeField] private Image soulChainOverlay;
        [Header("Basic unit level-up presentation")]
        [SerializeField] private Animator levelUpAnimator;

        private GreyboxBoardView boardView;
        private string unitId;
        private bool interactive = true;
        private bool dragging;
        private bool pairedPresentation;
        private bool deploymentVisualHidden;
        private bool isBeachPresentation;
        private Color authoredArtColor = Color.white;
        private bool hasAuthoredArtColor;
        private Sprite authoredArtSprite;
        private Vector3 authoredArtScale = Vector3.one;
        private bool hasAuthoredArtScale;
        private Vector2 authoredArtAnchoredPosition;
        private bool hasAuthoredArtAnchoredPosition;
        private Coroutine soulChainVisualCoroutine;
        private bool soulChainControlled;
        private bool bloodcrownSuppressed;
        private string configuredBasicAnimationId = string.Empty;
        private int lastBasicAttackAnimationFrame = -1;
        private float basicAttackAnimationSpeed = 1f;
        private readonly FixedSlotDragGesture gesture = new FixedSlotDragGesture();

        private const float SoulChainFlashPeakAlpha = 0.75f;
        private const float SoulChainDarkAlpha = 0.45f;
        private static readonly Color SoulChainOverlayColor = new Color(0.14f, 0.08f, 0.18f, 1f);
        private static readonly Color BloodcrownOverlayColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        private const float SoulChainFlashHalfPhaseSeconds = 0.12f;
        private const int SoulChainFlashCount = 2;
        private const float ArtFacingTransitionSeconds = 0.14f;
        private const string LevelUpFxNodeName = "ART_LevelUpFx";
        private const float LevelUpFxAuthoredSeconds = 0.6f;
        private const float LevelUpFxMaxSeconds = 2f;

        private Coroutine artFacingCoroutine;
        private bool artFacingInitialized;
        private bool artFacingMirrored;

        private Coroutine levelUpFxCoroutine;
        private bool levelUpFxResolved;
        private float levelUpFxDuration = LevelUpFxAuthoredSeconds;

        public RectTransform RectTransform => (RectTransform)transform;
        public Image ArtImage => artImage;
        public Animator BasicAttackAnimator => basicAttackAnimator;
        public Graphic InputReceiver => inputReceiver;
        public Text HeroLevelLabel => heroLevelLabel;
        public bool IsPairedPresentationHidden => pairedPresentation;
        public bool IsDragging => dragging;
        public bool IsSoulChainControlled => soulChainControlled;
        public bool IsBloodcrownSuppressed => bloodcrownSuppressed;
        public bool IsArtMirrored => artFacingInitialized
            ? artFacingMirrored
            : artImage != null &&
              Mathf.Sign(artImage.rectTransform.localScale.x) !=
              Mathf.Sign(authoredArtScale.x);
        public event Action<string> BowProjectileReleased;

        public void Configure(Image art, Text valueLabel, CanvasGroup group)
        {
            artImage = art;
            basicAttackAnimator = artImage != null ? artImage.GetComponent<Animator>() : null;
            label = valueLabel;
            alternateLabel = null;
            levelLabel = null;
            canvasGroup = group;
            isBeachPresentation = false;
            CaptureAuthoredArtColor();
            CaptureAuthoredArtScale();
            CaptureAuthoredArtAnchoredPosition();
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
            CaptureAuthoredArtScale();
            CaptureAuthoredArtAnchoredPosition();
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
            EnsureInputReceiver();
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                // Artwork may intentionally extend beyond one grid cell. It must never grow
                // the touch target into an adjacent cell; only the dedicated receiver owns UI input.
                graphic.raycastTarget = ReferenceEquals(graphic, inputReceiver) && value;
            }

            if (soulChainOverlay != null)
            {
                soulChainOverlay.raycastTarget = false;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = value;
                // A formed hero still consists of two independently draggable component
                // entities. Hide their artwork, but keep each component cell as the input
                // target so dragging either half can break the pair.
                canvasGroup.blocksRaycasts = value && !deploymentVisualHidden;
            }
        }

        private void EnsureInputReceiver()
        {
            if (inputReceiver != null)
            {
                return;
            }

            var existing = transform.FindUi(InputReceiverName);
            if (existing != null)
            {
                inputReceiver = existing.GetComponent<Graphic>();
            }

            if (inputReceiver == null)
            {
                var receiverObject = new GameObject(
                    InputReceiverName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var receiverRect = receiverObject.GetComponent<RectTransform>();
                receiverRect.SetParent(transform, false);
                receiverRect.anchorMin = new Vector2(0.5f, 0.5f);
                receiverRect.anchorMax = new Vector2(0.5f, 0.5f);
                receiverRect.pivot = new Vector2(0.5f, 0.5f);
                receiverRect.anchoredPosition = Vector2.zero;
                receiverRect.sizeDelta = new Vector2(InputSizePixels, InputSizePixels);
                var receiverImage = receiverObject.GetComponent<Image>();
                receiverImage.color = new Color(1f, 1f, 1f, 0f);
                inputReceiver = receiverImage;
            }

            inputReceiver.transform.SetAsFirstSibling();
        }

        public void SetPairedPresentation(bool value)
        {
            pairedPresentation = value;
            if (canvasGroup != null && !dragging)
            {
                ApplyVisibilityToCanvasGroup();
            }
        }

        public void SetDeploymentVisualHidden(bool hidden)
        {
            deploymentVisualHidden = hidden;
            if (!dragging)
            {
                ApplyVisibilityToCanvasGroup();
            }
        }

        public void SetDragGhostOpacity(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private void ApplyVisibilityToCanvasGroup()
        {
            if (canvasGroup == null)
            {
                return;
            }

            var visible = !pairedPresentation && !deploymentVisualHidden;
            canvasGroup.alpha = visible ? 1f : 0f;
            // Paired component graphics are transparent input zones. Deployment-hidden
            // views, on the other hand, must not intercept input while their ghost flies.
            canvasGroup.blocksRaycasts = interactive && !deploymentVisualHidden;
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

        public void SetArtMirrored(bool mirrored)
        {
            if (artImage == null)
            {
                return;
            }

            CaptureAuthoredArtScale();
            StopArtFacingTransition();
            artFacingInitialized = true;
            artFacingMirrored = mirrored;
            var scale = authoredArtScale;
            if (mirrored)
            {
                scale.x = -scale.x;
            }

            artImage.rectTransform.localScale = scale;
        }

        public void InitializeArtFacing(
    bool mirrored,
    float mirroredAnchoredPositionX,
    bool preserveAnchoredPosition = false)
        {
            if (artFacingInitialized)
            {
                return;
            }

            ApplyArtFacing(
                mirrored,
                mirroredAnchoredPositionX,
                false,
                preserveAnchoredPosition);
        }

        public void FaceArtTowards(
    bool mirrored,
    float mirroredAnchoredPositionX,
    bool preserveAnchoredPosition = false)
        {
            ApplyArtFacing(
                mirrored,
                mirroredAnchoredPositionX,
                true,
                preserveAnchoredPosition);
        }

        private void ApplyArtFacing(
    bool mirrored,
    float mirroredAnchoredPositionX,
    bool animate,
    bool preserveAnchoredPosition)

        {
            if (artImage == null)
            {
                return;
            }

            CaptureAuthoredArtScale();
            CaptureAuthoredArtAnchoredPosition();
            var rect = artImage.rectTransform;
            var targetScaleX = mirrored ? -authoredArtScale.x : authoredArtScale.x;
            var targetPositionX = preserveAnchoredPosition
    ? authoredArtAnchoredPosition.x
    : mirrored
        ? mirroredAnchoredPositionX
        : authoredArtAnchoredPosition.x;

            if (artFacingInitialized && artFacingMirrored == mirrored)
            {
                return;
            }

            StopArtFacingTransition();
            artFacingInitialized = true;
            artFacingMirrored = mirrored;
            if (!animate || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                SetArtFacingValues(rect, targetScaleX, targetPositionX);
                return;
            }

            artFacingCoroutine = StartCoroutine(AnimateArtFacing(
                rect,
                rect.localScale.x,
                targetScaleX,
                rect.anchoredPosition.x,
                targetPositionX));
        }

        private IEnumerator AnimateArtFacing(
            RectTransform rect,
            float startScaleX,
            float targetScaleX,
            float startPositionX,
            float targetPositionX)
        {
            var elapsed = 0f;
            while (elapsed < ArtFacingTransitionSeconds && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / ArtFacingTransitionSeconds);
                var eased = progress * progress * (3f - (2f * progress));
                SetArtFacingValues(
                    rect,
                    Mathf.LerpUnclamped(startScaleX, targetScaleX, eased),
                    Mathf.LerpUnclamped(startPositionX, targetPositionX, eased));
                yield return null;
            }

            if (rect != null)
            {
                SetArtFacingValues(rect, targetScaleX, targetPositionX);
            }
            artFacingCoroutine = null;
        }

        private static void SetArtFacingValues(
            RectTransform rect,
            float scaleX,
            float anchoredPositionX)
        {
            var scale = rect.localScale;
            scale.x = scaleX;
            rect.localScale = scale;

            var position = rect.anchoredPosition;
            position.x = anchoredPositionX;
            rect.anchoredPosition = position;
        }

        private void StopArtFacingTransition()
        {
            if (artFacingCoroutine == null)
            {
                return;
            }

            StopCoroutine(artFacingCoroutine);
            artFacingCoroutine = null;
        }

        public void SetArtAnchoredPositionX(float? anchoredPositionX)
        {
            if (artImage == null)
            {
                return;
            }

            CaptureAuthoredArtAnchoredPosition();
            var position = authoredArtAnchoredPosition;
            if (anchoredPositionX.HasValue)
            {
                position.x = anchoredPositionX.Value;
            }

            artImage.rectTransform.anchoredPosition = position;
        }

        public void ConfigureBasicAttackAnimation(string configId, float playbackSpeed = 1f)
        {
            if (isBeachPresentation || artImage == null)
            {
                return;
            }

            if (basicAttackAnimator == null)
            {
                basicAttackAnimator = artImage.GetComponent<Animator>();
                if (basicAttackAnimator == null)
                {
                    basicAttackAnimator = artImage.gameObject.AddComponent<Animator>();
                }
            }

            basicAttackAnimationSpeed = Mathf.Max(0.01f, playbackSpeed);
            if (string.Equals(configuredBasicAnimationId, configId, StringComparison.Ordinal) &&
                basicAttackAnimator.runtimeAnimatorController != null)
            {
                return;
            }

            configuredBasicAnimationId = configId ?? string.Empty;
            lastBasicAttackAnimationFrame = -1;
            basicAttackAnimator.runtimeAnimatorController =
                BasicUnitAnimationControllerCatalog.Load(configId);
            if (basicAttackAnimator.runtimeAnimatorController == null)
            {
                basicAttackAnimator.enabled = false;
                return;
            }

            // Keep the first authored frame visible until this unit actually attacks.
            basicAttackAnimator.enabled = true;
            var relay = basicAttackAnimator.GetComponent<BasicUnitAnimationEventRelay>();
            if (relay == null)
            {
                relay = basicAttackAnimator.gameObject.AddComponent<BasicUnitAnimationEventRelay>();
            }
            relay.Bind(this);
            basicAttackAnimator.Rebind();
            basicAttackAnimator.Play(0, 0, 0f);
            basicAttackAnimator.Update(0f);
            basicAttackAnimator.speed = 0f;
        }

        public bool PlayBasicAttackAnimation()
        {
            if (basicAttackAnimator == null ||
                basicAttackAnimator.runtimeAnimatorController == null ||
                lastBasicAttackAnimationFrame == Time.frameCount)
            {
                return false;
            }

            // Piercing and sweeping attacks can emit several damage events in one frame.
            // Restart only once so one gameplay attack always produces one animation.
            lastBasicAttackAnimationFrame = Time.frameCount;
            basicAttackAnimator.enabled = true;
            basicAttackAnimator.speed = basicAttackAnimationSpeed;
            basicAttackAnimator.Play(0, 0, 0f);
            basicAttackAnimator.Update(0f);
            return true;
        }

        // Optional V2 presentation: the authored UnitLevelUp overlay only exists on the
        // variant that ships the effect node, so missing clips stay a silent no-op here.
        public void PlayLevelUpFx()
        {
            if (!ResolveLevelUpFx())
            {
                return;
            }

            if (levelUpFxCoroutine != null)
            {
                StopCoroutine(levelUpFxCoroutine);
                levelUpFxCoroutine = null;
            }

            levelUpFxCoroutine = StartCoroutine(RunLevelUpFx());
        }

        private bool ResolveLevelUpFx()
        {
            if (levelUpAnimator != null)
            {
                return levelUpAnimator.runtimeAnimatorController != null;
            }

            if (levelUpFxResolved)
            {
                return false;
            }

            levelUpFxResolved = true;
            var node = transform.FindUi(LevelUpFxNodeName);
            if (node == null)
            {
                return false;
            }

            levelUpAnimator = node.GetComponent<Animator>();
            if (levelUpAnimator == null ||
                levelUpAnimator.runtimeAnimatorController == null)
            {
                levelUpAnimator = null;
                return false;
            }

            levelUpAnimator.speed = 1f;
            levelUpAnimator.enabled = true;
            node.gameObject.SetActive(false);
            return true;
        }

        private IEnumerator RunLevelUpFx()
        {
            var animator = levelUpAnimator;
            animator.gameObject.SetActive(true);
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);

            levelUpFxDuration = ResolveLevelUpFxDuration(animator);
            var elapsed = 0f;
            while (elapsed < levelUpFxDuration && elapsed < LevelUpFxMaxSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                {
                    break;
                }

                yield return null;
            }

            HideLevelUpFx();
            levelUpFxCoroutine = null;
        }

        private static float ResolveLevelUpFxDuration(Animator animator)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.length <= 0f)
            {
                return LevelUpFxAuthoredSeconds;
            }

            var stateSpeed = Mathf.Abs(state.speed);
            if (stateSpeed <= 0.0001f)
            {
                stateSpeed = 1f;
            }

            var controllerSpeed = Mathf.Abs(animator.speed);
            if (controllerSpeed <= 0.0001f)
            {
                controllerSpeed = 1f;
            }

            // The authored state plays the flash back slowly, so the derived duration can
            // only be trusted when it reaches at least the intended one-shot length.
            return Mathf.Clamp(
                state.length / (stateSpeed * controllerSpeed),
                LevelUpFxAuthoredSeconds,
                LevelUpFxMaxSeconds);
        }

        private void HideLevelUpFx()
        {
            if (levelUpAnimator != null &&
                levelUpAnimator.gameObject.activeSelf)
            {
                levelUpAnimator.gameObject.SetActive(false);
            }
        }

        internal void NotifyBowProjectileRelease()
        {
            if (!string.IsNullOrEmpty(unitId))
            {
                BowProjectileReleased?.Invoke(unitId);
            }
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
                if (bloodcrownSuppressed)
                {
                    SetSuppressionOverlay(BloodcrownOverlayColor, SoulChainDarkAlpha);
                }
                else
                {
                    SetSoulChainOverlayAlpha(0f);
                }
                return;
            }

            SetSuppressionOverlay(SoulChainOverlayColor, 0f);

            if (isActiveAndEnabled)
            {
                soulChainVisualCoroutine = StartCoroutine(PlaySoulChainVisual());
            }
            else
            {
                SetSoulChainOverlayAlpha(SoulChainDarkAlpha);
            }
        }

        public void SetBloodcrownSuppressed(bool suppressed)
        {
            ResolveSoulChainOverlay();
            if (bloodcrownSuppressed == suppressed)
            {
                return;
            }

            bloodcrownSuppressed = suppressed;
            if (soulChainVisualCoroutine != null)
            {
                StopCoroutine(soulChainVisualCoroutine);
                soulChainVisualCoroutine = null;
            }

            if (suppressed)
            {
                SetSuppressionOverlay(BloodcrownOverlayColor, SoulChainDarkAlpha);
            }
            else if (soulChainControlled)
            {
                SetSuppressionOverlay(SoulChainOverlayColor, SoulChainDarkAlpha);
            }
            else
            {
                SetSoulChainOverlayAlpha(0f);
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
                SetSuppressionOverlay(
                    bloodcrownSuppressed ? BloodcrownOverlayColor : SoulChainOverlayColor,
                    SoulChainDarkAlpha);
            }

            soulChainVisualCoroutine = null;
        }

        private void ResolveSoulChainOverlay()
        {
            if (soulChainOverlay != null)
            {
                return;
            }

            var existing = transform.FindUi("ART_SoulChainOverlay");
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

        private void SetSuppressionOverlay(Color color, float alpha)
        {
            if (soulChainOverlay == null)
            {
                return;
            }

            color.a = Mathf.Clamp01(alpha);
            soulChainOverlay.color = color;
        }

        private void ResetSoulChainVisual(bool resolveOverlay = true)
        {
            soulChainControlled = false;
            bloodcrownSuppressed = false;
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

        private void CaptureAuthoredArtScale()
        {
            if (hasAuthoredArtScale || artImage == null)
            {
                return;
            }

            authoredArtScale = artImage.rectTransform.localScale;
            hasAuthoredArtScale = true;
        }

        private void CaptureAuthoredArtAnchoredPosition()
        {
            if (hasAuthoredArtAnchoredPosition || artImage == null)
            {
                return;
            }

            authoredArtAnchoredPosition = artImage.rectTransform.anchoredPosition;
            hasAuthoredArtAnchoredPosition = true;
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
            ghost.deploymentVisualHidden = false;
            ghost.ResetSoulChainVisual();
            ghost.FreezeBasicAttackAnimationForVisualProxy();
            ghost.HideLevelUpFx();
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

        private void FreezeBasicAttackAnimationForVisualProxy()
        {
            if (basicAttackAnimator == null ||
                basicAttackAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            // UnitAni controllers use the attack clip as their default state. A freshly
            // instantiated flight proxy would therefore attack even without a combat event.
            basicAttackAnimator.enabled = true;
            basicAttackAnimator.Rebind();
            basicAttackAnimator.Play(0, 0, 0f);
            basicAttackAnimator.Update(0f);
            basicAttackAnimator.speed = 0f;
            basicAttackAnimator.enabled = false;
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

            gesture.PointerUp(eventData.pointerId);
            if (dragging)
            {
                CompleteDrag(eventData.position);
                return;
            }

            // Unity sends PointerClick after PointerUp when the press and release belong to
            // this card. Keep tap selection in OnPointerClick so the card both owns and
            // consumes the complete click event instead of relying on pointer-up ordering.
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null &&
                eventData.button == PointerEventData.InputButton.Left &&
                CanProcessInput())
            {
                boardView.SelectUnit(unitId);
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
            StopArtFacingTransition();
            if (levelUpFxCoroutine != null)
            {
                StopCoroutine(levelUpFxCoroutine);
                levelUpFxCoroutine = null;
                HideLevelUpFx();
            }

            if (dragging)
            {
                dragging = false;
                boardView?.CancelActiveDrag();
            }

            gesture.Cancel();
            ResetSoulChainVisual(false);
        }
    }

    [DisallowMultipleComponent]
    public sealed class BasicUnitAnimationEventRelay : MonoBehaviour
    {
        private DraggableUnitView owner;

        public void Bind(DraggableUnitView value)
        {
            owner = value;
        }

        // Called after the seventeenth authored BOW attack frame.
        public void OnBowProjectileRelease()
        {
            owner?.NotifyBowProjectileRelease();
        }
    }

    internal static class BasicUnitAnimationControllerCatalog
    {
        private static readonly IReadOnlyDictionary<BasicUnitArchetype, string> ResourcePaths =
            new Dictionary<BasicUnitArchetype, string>
            {
                { BasicUnitArchetype.Axe, "Animation/UnitAni/AXE" },
                { BasicUnitArchetype.Rider, "Animation/UnitAni/BERSERKER" },
                { BasicUnitArchetype.Bow, "Animation/UnitAni/BOW" },
                { BasicUnitArchetype.Spear, "Animation/UnitAni/SPEAR" }
            };

        private static readonly Dictionary<BasicUnitArchetype, RuntimeAnimatorController> Cache =
            new Dictionary<BasicUnitArchetype, RuntimeAnimatorController>();
        private static readonly HashSet<string> MissingControllerWarnings =
            new HashSet<string>(StringComparer.Ordinal);

        public static RuntimeAnimatorController Load(string configId)
        {
            BasicUnitArchetype archetype;
            try
            {
                archetype = BasicUnitCatalog.GetArchetype(configId);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (Cache.TryGetValue(archetype, out var cached) && cached != null)
            {
                return cached;
            }

            if (!ResourcePaths.TryGetValue(archetype, out var resourcePath))
            {
                return null;
            }

            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(resourcePath);
            if (controller != null)
            {
                Cache[archetype] = controller;
                return controller;
            }

            if (MissingControllerWarnings.Add(resourcePath))
            {
                Debug.LogWarning(
                    $"Basic-unit animation controller '{resourcePath}' is missing for '{configId}'.");
            }

            return null;
        }
    }
}
