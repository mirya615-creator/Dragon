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

        // V1 的 SlectRoad 是"8 段菱形虚线"，段高 10 时菱形只有约 9×4 屏幕像素且带缝，
        // 观感过细；V2 的图本身就是一条细实线，保持原段高即可。
        private const float V1SegmentHeight = 100f;
        private const float V2SegmentHeight = 18f;
        private const float SegmentOverlap = 4f;

        // V2 的 SlectRoad（100×100，Tight）实测：线体条带高 9px，含 6 段虚线，
        // 每段宽 15px、间隔仅 2px——间隔是美术图上写死的，代码等比放大概不掉。
        // 这里把平铺单元裁成"单段虚线"，间隔交给代码：
        //   V2DashSourceWidth = 单段虚线的源像素宽（实测 15）
        //   V2DashSourceGap   = 期望的虚线间隔（源像素；渲染时按段高等比换算）
        private const float V2DashSourceWidth = 15f;
        private const float V2DashSourceGap = 8f;

        // 按变体取值，默认 V1（编辑器未激活注册表时 UiAssets.Active 为 null，行为不变）。
        private float segmentHeight = V1SegmentHeight;

        // V2：单段虚线之间的正间隔（渲染像素）；V1：0（保持"负重叠贴缝"旧逻辑）。
        private float segmentGap;

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

            // The arrow GameObject starts inactive in the authored scenes, so Awake never runs
            // and Configure is only invoked by the editor scene builder. Initialize lazily here
            // instead of relying on either, otherwise the first Show would dereference a null
            // tiling sprite.
            if (tiledPathSprite == null)
            {
                ApplyAuthoredPathSprite();
                if (tiledPathSprite == null)
                {
                    Hide();
                    return;
                }
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
                segmentHeight;

            float step;
            int segmentCount;
            if (segmentGap > 0f)
            {
                // V2 单段虚线：理想周期 = 段宽 + 间隔；再按实际距离均匀分布，
                // 让首段贴起点、末段右缘正好贴终点（间距在理想值附近轻微浮动）。
                if (distance <= segmentWidth)
                {
                    step = segmentWidth;
                    segmentCount = 1;
                }
                else
                {
                    float idealStep = segmentWidth + segmentGap;
                    int gapCount = Mathf.Max(1, Mathf.RoundToInt((distance - segmentWidth) / idealStep));
                    step = Mathf.Max(segmentWidth, (distance - segmentWidth) / gapCount);
                    segmentCount = gapCount + 1;
                }
            }
            else
            {
                // V1：整条多段图平铺，相邻 tile 负重叠贴缝（间隔写在图里），行为不变。
                step = Mathf.Max(1f, segmentWidth - SegmentOverlap);
                segmentCount = Mathf.Max(
                    1,
                    Mathf.CeilToInt((distance + SegmentOverlap) / step));
            }


            rect.sizeDelta = new Vector2(distance, segmentHeight);

            EnsureSegmentCount(segmentCount);

            for (int i = 0; i < segmentCount; i++)
            {
                Image segment = shaftSegments[i];
                RectTransform segmentRect = segment.rectTransform;

                segmentRect.anchorMin = new Vector2(0f, 0.5f);
                segmentRect.anchorMax = new Vector2(0f, 0.5f);
                segmentRect.pivot = new Vector2(0f, 0.5f);
                // 段数向上取整，最后一整段会溢出终点；把它往回挪，让线末端正好
                // 停在 distance 处（与前一段多叠一点，图案不会被压扁）。
                // 注意：不能用 RectMask2D 裁剪——根 RectTransform 带旋转，
                // RectMask2D 用对角点算裁剪矩形会得到退化矩形（宽/高为 0 或负），
                // 会按拖动方向把整条线裁没（表现为"时有时无"）。
                float x = Mathf.Min(i * step, Mathf.Max(0f, distance - segmentWidth));
                segmentRect.anchoredPosition = new Vector2(x, 0f);
                segmentRect.sizeDelta = new Vector2(segmentWidth, segmentHeight);
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
            // 注意：SlectRoad 是以 Tight 方式导入的，Unity 已经把 textureRect trim 到线条
            // 包围盒，此时再叠加下面那套"整图坐标"的裁剪常量会让矩形越界（Sprite.Create
            // 抛 ArgumentException）或裁到错误区域。只有图是 FullRect（textureRect ≈ 整图）
            // 时才回退到常量路径。
            bool isV2Variant = string.Equals(
    DragonBound.Presentation.UiAssets.Active?.VariantId ?? "V1",
    "V2",
    System.StringComparison.Ordinal);
            segmentHeight = isV2Variant ? V2SegmentHeight : V1SegmentHeight;

            var textureRect = pathSprite.textureRect;
            bool isTrimmed =
                textureRect.width < pathSprite.texture.width * 0.9f ||
                textureRect.height < pathSprite.texture.height * 0.9f;
            Rect crop;
            bool singleDashTile = false;
            if (isTrimmed)
            {
                if (isV2Variant)
                {
                    // V2：只裁一段虚线作为平铺单元（Tight 已把 textureRect trim 到
                    // 99×9 的线条条带，第一段虚线就从左缘 x=0 开始），间隔交给 segmentGap。
                    crop = new Rect(
                        textureRect.x,
                        textureRect.y,
                        Mathf.Min(V2DashSourceWidth, textureRect.width),
                        textureRect.height);
                    singleDashTile = true;
                }
                else
                {
                    // V1：保持整条多段图平铺（菱形虚线的节奏写在图里）。
                    crop = textureRect;
                }
            }
            else if (pathSprite.texture.height > 500)
            {
                crop = new Rect(textureRect.x + V1LineX, textureRect.y + V1LineYBottom, V1LineW, V1LineH);
            }
            else
            {
                crop = new Rect(textureRect.x + V2LineX, textureRect.y + V2LineYBottom, V2LineW, V2LineH);
            }

            // Sprite.Create 要求裁剪矩形完全落在纹理内，浮点尾差也要夹住。
            float maxX = pathSprite.texture.width;
            float maxY = pathSprite.texture.height;
            crop.x = Mathf.Clamp(crop.x, 0f, Mathf.Max(0f, maxX - 1f));
            crop.y = Mathf.Clamp(crop.y, 0f, Mathf.Max(0f, maxY - 1f));
            crop.width = Mathf.Clamp(crop.width, 1f, maxX - crop.x);
            crop.height = Mathf.Clamp(crop.height, 1f, maxY - crop.y);

            // 间隔按"源像素 → 渲染像素"等比换算：渲染间隔 = 源间隔 × 段高 / 裁剪高。
            // 只有真正裁成单段时才有意义；V1 与 V2 的 FullRect 兜底路径保持 0（旧贴缝逻辑）。
            segmentGap = singleDashTile
                ? V2DashSourceGap / Mathf.Max(1f, crop.height) * segmentHeight
                : 0f;

            var ppu = pathSprite.pixelsPerUnit > 0f ? pathSprite.pixelsPerUnit : 100f;
            // 缓存守卫必须把裁剪尺寸也纳入：裁成单段后 crop 尺寸变了，只比 texture
            // 会复用旧的整条平铺单元。
            if (tiledPathSprite == null ||
                tiledPathSprite.texture != pathSprite.texture ||
                !Mathf.Approximately(tiledPathSprite.rect.width, crop.width) ||
                !Mathf.Approximately(tiledPathSprite.rect.height, crop.height))
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
            // 不要用 Shaft 的 authored 颜色：那是给旧箭头美术调的染色，
            // 会把 V2 的 SlectRoad 原色（青蓝）染成绿色。白色 = 显示图片原本颜色。
            segment.color = Color.white;
            segment.gameObject.SetActive(true);
            shaftSegments.Add(segment);
            return segment;
        }

    }
}
