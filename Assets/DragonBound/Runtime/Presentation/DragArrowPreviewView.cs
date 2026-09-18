using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace DragonBound.Presentation
{
    // The authored shaft and head can be replaced by UI artists without changing drag rules.
    public sealed class DragArrowPreviewView : MonoBehaviour
    {
        private const string DragPathSpriteResourcePath = "GameUI/SlectRoad";
        private const float TiledPathHeight = 10f;
        private const float V1TexHeight = 1254f;
        private const float V1LineX = 213f, V1LineYBottom = 607f, V1LineW = 827f, V1LineH = 49f;
        private const float V2TexHeight = 100f;
        private const float V2LineX = 1f, V2LineYBottom = 47f, V2LineW = 98f, V2LineH = 9f;

        private Sprite tiledPathSprite;   // 缓存，避免每次 Configure 都 new
        private readonly List<Image> shaftSegments = new List<Image>();

        private const float SegmentHeight = 10f;
        private const float SegmentOverlap = 4f;

        [SerializeField] private Image shaft;
        [SerializeField] private Text headLabel;

        public bool IsVisible => gameObject.activeSelf;
        public Sprite PathSprite => shaft != null ? shaft.sprite : null;

        private void Awake()
        {
            ApplyAuthoredPathSprite();
            DisableAllGraphicRaycasts();
        }

        public void Configure(Image shaftImage, Text arrowHead)
        {
            shaft = shaftImage;
            headLabel = arrowHead;
            ApplyAuthoredPathSprite();
            DisableAllGraphicRaycasts();

            Hide();
        }

        public void Show(RectTransform parent, Vector3 sourceWorld, Vector3 targetWorld)
        {
            if (parent == null)
            {
                Hide();
                return;
            }

            var source = (Vector2)parent.InverseTransformPoint(sourceWorld);
            var target = (Vector2)parent.InverseTransformPoint(targetWorld);
            var delta = target - source;
            if (delta.sqrMagnitude < 0.01f)
            {
                Hide();
                return;
            }

            var rect = (RectTransform)transform;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = source;
            // SlectRoad has generous transparent padding around its centered line art.
            // A taller rect keeps the visible line readable without modifying the source asset.
            float distance = delta.magnitude;
            float segmentWidth =
                tiledPathSprite.rect.width /
                Mathf.Max(1f, tiledPathSprite.rect.height) *
                SegmentHeight;

            float step = Mathf.Max(1f, segmentWidth - SegmentOverlap);
            int segmentCount = Mathf.Max(
                1,
                Mathf.CeilToInt((distance + SegmentOverlap) / step));

            rect.sizeDelta = new Vector2(distance, SegmentHeight);

            EnsureSegmentCount(segmentCount);

            for (int i = 0; i < segmentCount; i++)
            {
                Image segment = shaftSegments[i];
                RectTransform segmentRect = segment.rectTransform;

                segmentRect.anchorMin = new Vector2(0f, 0.5f);
                segmentRect.anchorMax = new Vector2(0f, 0.5f);
                segmentRect.pivot = new Vector2(0f, 0.5f);
                segmentRect.anchoredPosition = new Vector2(i * step, 0f);
                segmentRect.sizeDelta = new Vector2(segmentWidth, SegmentHeight);
            }
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            // Keep the authored arrow art above runtime unit cards without changing board state.
            transform.SetAsLastSibling();
            DisableAllGraphicRaycasts();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void DisableAllGraphicRaycasts()
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private void ApplyAuthoredPathSprite()
        {
            if (shaft == null)
            {
                return;
            }

            var pathSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(DragPathSpriteResourcePath);
            if (pathSprite == null)
            {
                throw new System.InvalidOperationException(
                    $"Missing drag path sprite at Resources/{DragPathSpriteResourcePath}.");
            }

            // 源图是"细线 + 大片透明边距"，整张平铺会让线细到看不见，
            // 因此用 Sprite.Create 只取线条那一横条作为平铺单元。
            var textureRect = pathSprite.textureRect;
            Rect crop;
            if (pathSprite.texture.height > 500)
            {
                crop = new Rect(textureRect.x + V1LineX, textureRect.y + V1LineYBottom, V1LineW, V1LineH);
            }
            else
            {
                crop = new Rect(textureRect.x + V2LineX, textureRect.y + V2LineYBottom, V2LineW, V2LineH);
            }

            var ppu = pathSprite.pixelsPerUnit > 0f ? pathSprite.pixelsPerUnit : 100f;
            if (tiledPathSprite == null || tiledPathSprite.texture != pathSprite.texture)
            {
                tiledPathSprite = Sprite.Create(pathSprite.texture, crop, new Vector2(0.5f, 0.5f), ppu);
            }

            shaft.sprite = tiledPathSprite;
            shaft.type = Image.Type.Simple;
            shaft.preserveAspect = false;
            shaft.raycastTarget = false;
            shaft.gameObject.SetActive(false);

            if (headLabel != null)
            {
                headLabel.gameObject.SetActive(false);
            }
        }
        private void EnsureSegmentCount(int count)
        {
            while (shaftSegments.Count < count)
            {
                CreateSegment();
            }

            for (int i = 0; i < shaftSegments.Count; i++)
            {
                shaftSegments[i].gameObject.SetActive(i < count);
            }
        }
        private Image CreateSegment()
        {
            var segment = Instantiate(shaft, shaft.transform.parent);
            segment.name = "SlectRoadSegment";
            segment.sprite = tiledPathSprite;
            segment.type = Image.Type.Simple;
            segment.preserveAspect = false;
            segment.raycastTarget = false;
            segment.gameObject.SetActive(true);
            shaftSegments.Add(segment);
            return segment;
        }

    }
}
