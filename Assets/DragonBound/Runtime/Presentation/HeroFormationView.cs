using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    // Authored as a prefab so final art can replace every greybox element in the Inspector.
    public sealed class HeroFormationView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image connectorLine;
        [SerializeField] private Image primaryFlash;
        [SerializeField] private Image secondaryFlash;
        [SerializeField] private Image doubleCellBorder;
        [SerializeField] private Text heroNameLabel;

        private Vector3 connectorScale = Vector3.one;
        private bool connectorScaleCaptured;

        public RectTransform RectTransform => (RectTransform)transform;

        public void Configure(
            CanvasGroup group,
            Image line,
            Image firstFlash,
            Image secondFlash,
            Image border,
            Text nameLabel)
        {
            canvasGroup = group;
            connectorLine = line;
            primaryFlash = firstFlash;
            secondaryFlash = secondFlash;
            doubleCellBorder = border;
            heroNameLabel = nameLabel;
        }

        public void Initialize(
            Vector2 center,
            Vector2 primaryOffset,
            Vector2 secondaryOffset,
            Vector2 footprintSize,
            Vector2 cellSize,
            string heroName,
            Color rarityColor)
        {
            RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            RectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform.anchoredPosition = center;
            RectTransform.sizeDelta = footprintSize;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            DisableRaycast(connectorLine);
            DisableRaycast(primaryFlash);
            DisableRaycast(secondaryFlash);
            DisableRaycast(doubleCellBorder);
            DisableRaycast(heroNameLabel);
            if (heroNameLabel != null)
            {
                heroNameLabel.text = heroName;
            }

            PositionFlash(primaryFlash, primaryOffset, cellSize, rarityColor);
            PositionFlash(secondaryFlash, secondaryOffset, cellSize, rarityColor);
            if (doubleCellBorder != null)
            {
                var borderColor = rarityColor;
                var outline = doubleCellBorder.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = borderColor;
                    outline.useGraphicAlpha = false;
                }

                borderColor.a = 0.06f;
                doubleCellBorder.color = borderColor;
                doubleCellBorder.rectTransform.anchorMin = Vector2.zero;
                doubleCellBorder.rectTransform.anchorMax = Vector2.one;
                doubleCellBorder.rectTransform.offsetMin = Vector2.zero;
                doubleCellBorder.rectTransform.offsetMax = Vector2.zero;
            }

            if (connectorLine != null)
            {
                var delta = secondaryOffset - primaryOffset;
                connectorLine.color = rarityColor;
                connectorLine.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                connectorLine.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                connectorLine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                connectorLine.rectTransform.anchoredPosition = (primaryOffset + secondaryOffset) * 0.5f;
                connectorLine.rectTransform.sizeDelta =
                    new Vector2(delta.magnitude, Mathf.Max(3f, Mathf.Min(cellSize.x, cellSize.y) * 0.08f));
                connectorLine.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                if (!connectorScaleCaptured)
                {
                    connectorScale = connectorLine.rectTransform.localScale;
                    connectorScaleCaptured = true;
                }
                else
                {
                    connectorLine.rectTransform.localScale = connectorScale;
                }
            }

            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            var flash = Mathf.Sin(progress * Mathf.PI);
            SetGraphicAlpha(primaryFlash, flash);
            SetGraphicAlpha(secondaryFlash, flash);
            SetGraphicAlpha(doubleCellBorder, Mathf.Lerp(0.12f, 0.20f, flash));
            if (connectorLine != null)
            {
                connectorLine.rectTransform.localScale =
                    new Vector3(connectorScale.x * Mathf.SmoothStep(0f, 1f, progress), connectorScale.y, connectorScale.z);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private static void DisableRaycast(Graphic graphic)
        {
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }

        private static void PositionFlash(Image flash, Vector2 position, Vector2 size, Color color)
        {
            if (flash == null)
            {
                return;
            }

            flash.color = color;
            flash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            flash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            flash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            flash.rectTransform.anchoredPosition = position;
            flash.rectTransform.sizeDelta = size;
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            var color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }
    }
}
