using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Presentation-only dig animation played when a shovel unlocks a deployment cell
    /// (inspired by "Zhao Yun and A Dou"): a shovel ghost wobbles over the target cell,
    /// swinging left/right three times with a downward peck and a squash on each swing,
    /// then fades out. Everything is built at runtime on an effect layer, so the ghost
    /// survives bench rebuilds triggered by RefreshUnits. No prefab is required.
    /// </summary>
    public sealed class ShovelDigEffectView : MonoBehaviour
    {
        private const float TotalDurationSeconds = 0.55f;
        private const float FadeTailSeconds = 0.15f;
        private const int SwingCycles = 3;
        private const float MaxSwingDegrees = 22f;
        private const float PeckDepthPixels = 10f;
        private const float PeckTowardCenterPixels = 4f;
        private const float SquashFactorX = 1.08f;
        private const float SquashFactorY = 0.92f;
        private const float ShovelScale = 0.86f;
        private const float PositionOffsetRatio = 0.18f;
        private const string ShovelSpriteResourcePath = "ComponentUI/shovel";

        private static Sprite cachedShovelSprite;

        private RectTransform pivot;
        private CanvasGroup canvasGroup;
        private Vector2 baseSize;
        private Vector2 originAnchoredPosition;

        /// <summary>
        /// Creates and plays a dig effect centered on the supplied cell rect. The effect
        /// parents to the given layer (heroEffectLayer/unitLayer), never to the cell or a
        /// bench card, so unit rebuilds cannot destroy it mid-animation.
        /// </summary>
        public static void Play(RectTransform layer, RectTransform cellRect)
        {
            if (layer == null || cellRect == null)
            {
                return;
            }

            var shovel = LoadShovelSprite();
            if (shovel == null)
            {
                return;
            }

            var rootObject = new GameObject(
                "ShovelDigEffect",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(ShovelDigEffectView));
            var root = (RectTransform)rootObject.transform;
            root.SetParent(layer, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            var cellSize = cellRect.rect.size;
            root.sizeDelta = new Vector2(cellSize.x * ShovelScale, cellSize.y * ShovelScale);
            root.position = cellRect.position;
            // The shovel digs from the cell's upper-right corner toward its center, matching
            // the right-to-left erase direction of the unlock wipe. The effect layer is never
            // rotated, so offsetting in its local space is safe.
            root.anchoredPosition += new Vector2(
                cellSize.x * PositionOffsetRatio,
                cellSize.y * PositionOffsetRatio);

            // The shovel rotates around the bottom of its handle, so the pivot sits near the
            // lower end of the card. Swinging around the blade would read as wobbling paper.
            var pivotObject = new GameObject("Pivot", typeof(RectTransform));
            var pivotRect = (RectTransform)pivotObject.transform;
            pivotRect.SetParent(root, false);
            pivotRect.anchorMin = Vector2.zero;
            pivotRect.anchorMax = Vector2.one;
            pivotRect.offsetMin = pivotRect.offsetMax = Vector2.zero;
            pivotRect.pivot = new Vector2(0.5f, 0.08f);

            var imageObject = new GameObject(
                "Shovel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var imageRect = (RectTransform)imageObject.transform;
            imageRect.SetParent(pivotRect, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            var image = imageObject.GetComponent<Image>();
            image.sprite = shovel;
            image.raycastTarget = false;

            var effect = rootObject.GetComponent<ShovelDigEffectView>();
            effect.pivot = pivotRect;
            effect.canvasGroup = rootObject.GetComponent<CanvasGroup>();
            effect.baseSize = root.sizeDelta;
            effect.originAnchoredPosition = root.anchoredPosition;
            effect.StartCoroutine(effect.Animate());
        }

        private static Sprite LoadShovelSprite()
        {
            if (cachedShovelSprite == null)
            {
                cachedShovelSprite = UiAssets.Load<Sprite>(ShovelSpriteResourcePath);
            }

            return cachedShovelSprite;
        }

        private IEnumerator Animate()
        {
            var rect = (RectTransform)transform;
            var swingDuration = Mathf.Max(0.01f, TotalDurationSeconds - FadeTailSeconds);
            var elapsed = 0f;
            while (elapsed < TotalDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var swingProgress = Mathf.Clamp01(elapsed / swingDuration);
                var phase = swingProgress * SwingCycles * Mathf.PI * 2f;
                var swing = Mathf.Sin(phase);

                if (pivot != null)
                {
                    pivot.localRotation = Quaternion.Euler(0f, 0f, swing * MaxSwingDegrees);
                }

                // Each swing extreme pecks toward the cell center (leftward and downward from
                // the upper-right dig position) and squashes briefly, which sells the
                // "digging" impact without any additional art.
                var peck = Mathf.Max(0f, swing);
                rect.sizeDelta = new Vector2(
                    baseSize.x * Mathf.Lerp(1f, SquashFactorX, peck),
                    baseSize.y * Mathf.Lerp(1f, SquashFactorY, peck));
                rect.anchoredPosition = originAnchoredPosition + new Vector2(
                    -peck * PeckTowardCenterPixels,
                    -peck * PeckDepthPixels);

                if (canvasGroup != null && elapsed > swingDuration)
                {
                    canvasGroup.alpha = Mathf.Clamp01((TotalDurationSeconds - elapsed) / FadeTailSeconds);
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
