using DragonBound.Recruitment;
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
        private static Sprite purpleFrameSprite;
        private static Sprite goldFrameSprite;
        private static bool rarityFramesLoaded;

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
            Color rarityColor,
            HeroRecipeRarity rarity)
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

            // HeroNameLabel is authored UI. Its text, layout, style, active state and
            // raycast setting intentionally remain exactly as configured in the prefab.

            PositionFlash(primaryFlash, primaryOffset, cellSize, rarityColor);
            PositionFlash(secondaryFlash, secondaryOffset, cellSize, rarityColor);
            if (doubleCellBorder != null)
            {
                ApplyRarityFrame(rarity, footprintSize, secondaryOffset - primaryOffset);
            }

            if (connectorLine != null)
            {
                var delta = secondaryOffset - primaryOffset;
                connectorLine.color = rarityColor;
                connectorLine.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                connectorLine.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                connectorLine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                connectorLine.rectTransform.anchoredPosition = (primaryOffset + secondaryOffset) * 0.5f;
                var authoredHeight = connectorLine.rectTransform.sizeDelta.y;
                connectorLine.rectTransform.sizeDelta =
                    new Vector2(delta.magnitude, authoredHeight);
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

        private void ApplyRarityFrame(
            HeroRecipeRarity rarity,
            Vector2 footprintSize,
            Vector2 pairDirection)
        {
            LoadRarityFrames();
            doubleCellBorder.sprite = rarity == HeroRecipeRarity.Gold
                ? goldFrameSprite
                : purpleFrameSprite;
            doubleCellBorder.color = Color.white;
            doubleCellBorder.type = Image.Type.Simple;
            doubleCellBorder.preserveAspect = false;
            doubleCellBorder.raycastTarget = false;

            var frameRect = doubleCellBorder.rectTransform;
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = Vector2.zero;

            var vertical = Mathf.Abs(pairDirection.y) > Mathf.Abs(pairDirection.x);
            frameRect.localRotation = vertical
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.identity;
            frameRect.sizeDelta = vertical
                ? new Vector2(footprintSize.y, footprintSize.x)
                : footprintSize;
        }

        private static void LoadRarityFrames()
        {
            if (rarityFramesLoaded)
            {
                return;
            }

            rarityFramesLoaded = true;
            purpleFrameSprite = Resources.Load<Sprite>("GameUI/HeroPurple");
            goldFrameSprite = Resources.Load<Sprite>("GameUI/HeroGold");
            if (purpleFrameSprite == null || goldFrameSprite == null)
            {
                Debug.LogError(
                    "HeroFormation rarity UI is missing. Expected Resources/GameUI/HeroPurple and HeroGold sprites.");
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
