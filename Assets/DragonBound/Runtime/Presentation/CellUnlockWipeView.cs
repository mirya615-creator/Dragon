using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Presentation-only wipe transition for a freshly unlocked deployment cell. The cell art
    /// switches to the unlocked sprite immediately underneath; this effect covers the cell with
    /// a copy of the previously displayed locked sprite and erases that copy through a shrinking
    /// RectMask2D window, revealing the unlocked art horizontally from right to left — the same
    /// corner the shovel digs from. The window starts shrinking after a short delay so the
    /// erase begins when the shovel has swung half a cycle. Nothing under the mask is ever
    /// rotated: RectMask2D computes its clip rectangle from axis-aligned corners and
    /// degenerates when rotated (see DragArrowPreviewView).
    /// </summary>
    public sealed class CellUnlockWipeView : MonoBehaviour
    {
        // The dig animation swings SwingCycles(3) times over 0.4s (ShovelDigEffectView:
        // TotalDurationSeconds 0.55 minus FadeTailSeconds 0.15), so one cycle is ~0.133s and
        // half a cycle — the first swing extreme — lands at ~0.07s.
        private const float DelaySeconds = 0.07f;
        private const float WipeDurationSeconds = 0.4f;
        private const float EdgeSizePixels = 16f;
        private const float EdgeMaxAlpha = 0.85f;
        private const string RootName = "CellUnlockWipe";

        private RectTransform maskRect;
        private RectTransform lockedCopyRect;
        private RectTransform edgeRect;
        private Image edgeImage;
        private Vector2 rootSize;

        /// <summary>
        /// Plays the wipe on the supplied cell. Call this BEFORE the cell art is refreshed to the
        /// unlocked state so the currently displayed (locked) sprite can be captured and erased.
        /// </summary>
        public static void Play(GridCellView cell)
        {
            if (cell == null || cell.ContentAnchor == null)
            {
                return;
            }

            // A Boss-locked cell can be re-locked and unlocked again, so replace a running wipe.
            var existing = cell.transform.FindUi(RootName);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            var sprite = cell.CurrentDevelopmentSprite;
            if (sprite == null)
            {
                // Without authored development art there is nothing to erase; the cell simply
                // switches states, which is the pre-effect behaviour.
                return;
            }

            var rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CellUnlockWipeView));
            var root = (RectTransform)rootObject.transform;
            root.SetParent(cell.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();

            var maskObject = new GameObject("WipeMask", typeof(RectTransform), typeof(RectMask2D));
            var mask = (RectTransform)maskObject.transform;
            mask.SetParent(root, false);
            mask.anchorMin = Vector2.zero;
            mask.anchorMax = Vector2.one;
            mask.offsetMin = mask.offsetMax = Vector2.zero;

            var copyObject = new GameObject(
                "LockedCopy",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var copy = (RectTransform)copyObject.transform;
            copy.SetParent(mask, false);
            copy.anchorMin = copy.anchorMax = Vector2.zero;
            copy.pivot = new Vector2(0f, 0f);
            var copyImage = copyObject.GetComponent<Image>();
            copyImage.sprite = sprite;
            copyImage.raycastTarget = false;

            var edgeObject = new GameObject(
                "WipeEdge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var edge = (RectTransform)edgeObject.transform;
            edge.SetParent(root, false);
            edge.anchorMin = edge.anchorMax = new Vector2(0.5f, 0.5f);
            // A vertical bar spanning the cell height: the erase front moves horizontally,
            // so the highlight rides it like the edge of a squeegee.
            edge.sizeDelta = new Vector2(EdgeSizePixels, root.rect.size.y);
            var edgeImageComponent = edgeObject.GetComponent<Image>();
            edgeImageComponent.color = new Color(1f, 1f, 1f, 0f);
            edgeImageComponent.raycastTarget = false;

            var effect = rootObject.GetComponent<CellUnlockWipeView>();
            effect.maskRect = mask;
            effect.lockedCopyRect = copy;
            effect.edgeRect = edge;
            effect.edgeImage = edgeImageComponent;
            effect.rootSize = root.rect.size;
            effect.StartCoroutine(effect.Animate());
        }

        private IEnumerator Animate()
        {
            edgeImage.gameObject.SetActive(false);
            ApplyProgress(0f);

            var elapsed = 0f;
            while (elapsed < DelaySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            edgeImage.gameObject.SetActive(true);
            elapsed = 0f;
            while (elapsed < WipeDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / WipeDurationSeconds);
                var eased = progress * progress * (3f - 2f * progress);
                ApplyProgress(eased);

                var fadeIn = Mathf.Clamp01(progress / 0.2f);
                var fadeOut = Mathf.Clamp01((1f - progress) / 0.4f);
                edgeImage.color = new Color(1f, 1f, 1f, Mathf.Min(fadeIn, fadeOut) * EdgeMaxAlpha);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void ApplyProgress(float progress)
        {
            // The clip window shrinks toward the left edge; the unlocked art is revealed
            // horizontally from the right (where the shovel digs) moving left.
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = new Vector2(1f - progress, 1f);
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;

            // The copy is anchored to the mask's lower-left corner and stays pinned to the cell
            // for the whole wipe; only the clip window slides, so no per-frame compensation.
            lockedCopyRect.anchoredPosition = Vector2.zero;
            lockedCopyRect.sizeDelta = rootSize;

            // The erase front rides the mask's right edge, which sweeps from the cell's right
            // border to its left (the pivot of the root sits at the cell center, hence the
            // half-width offset).
            edgeRect.anchoredPosition = new Vector2(
                (0.5f - progress) * rootSize.x,
                0f);
        }
    }
}
