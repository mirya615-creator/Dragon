using UnityEngine;

namespace DragonBound.Presentation
{
    /// <summary>
    /// 用一张可拉伸的"延伸段"贴图填满安全区外的屏幕边缘（顶部刘海/状态栏、底部导航栏），
    /// 使 SafeAreaRoot 内的边框美术与屏幕边缘之间不露出底层背景。
    /// 节点需挂在 Canvas 下（不受 SafeAreaFitter 收缩影响）。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaEdgeFill : MonoBehaviour
    {
        public enum ScreenEdge
        {
            Top = 0,
            Bottom = 1,
        }

        [SerializeField] private ScreenEdge edge = ScreenEdge.Top;
        [SerializeField] private float overlap = 4f;
        [SerializeField] private float minHeight = 0f;
        [SerializeField] private float maxHeight = 0f;

        // 实际承载贴图的子节点：按原图宽高比定尺寸，底边与本节点底边对齐，
        // 多出来的部分向上溢出屏幕顶边被自然裁掉（不拉伸、不压缩）。
        [SerializeField] private RectTransform artRect;
        [SerializeField] private UnityEngine.UI.Image artImage;
        [SerializeField] private float sourceAspect = 0.5510204f;

#if UNITY_EDITOR
        [SerializeField] private bool simulateInEditor = true;
        [SerializeField] private float simulatedTopInset = 240f;
        [SerializeField] private float simulatedBottomInset = 120f;
#endif

        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
        private bool dirty = true;

        private void Awake()
        {
            Cache();
            Apply(true);
        }

        private void OnEnable()
        {
            Cache();
            dirty = true;
            Apply(true);
        }

        private void OnValidate()
        {
            dirty = true;
        }

        private void Update()
        {
            Apply(false);
        }

        private void Cache()
        {
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                rootCanvas = rootCanvas.rootCanvas;
            }
        }

        private void Apply(bool force)
        {
            if (rectTransform == null)
            {
                Cache();
            }

            if (rectTransform == null)
            {
                return;
            }

            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var safeArea = ResolveSafeArea(screenSize);
            if (!force && !dirty
                && safeArea == lastSafeArea
                && screenSize == lastScreenSize)
            {
                return;
            }

            dirty = false;
            lastSafeArea = safeArea;
            lastScreenSize = screenSize;

            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            var scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
            if (scale <= 0f)
            {
                scale = 1f;
            }

            var insetPixels = edge == ScreenEdge.Top
                ? screenSize.y - safeArea.yMax
                : safeArea.yMin;
            if (insetPixels < 0f)
            {
                insetPixels = 0f;
            }

            var height = insetPixels / scale + Mathf.Max(0f, overlap);
            if (height < minHeight)
            {
                height = minHeight;
            }

            if (maxHeight > 0f && height > maxHeight)
            {
                height = maxHeight;
            }

            if (insetPixels <= 0.5f && minHeight <= 0f)
            {
                height = 0f;
            }

            if (edge == ScreenEdge.Top)
            {
                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                rectTransform.anchorMin = new Vector2(0f, 0f);
                rectTransform.anchorMax = new Vector2(1f, 0f);
                rectTransform.pivot = new Vector2(0.5f, 0f);
            }

            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(0f, height);

            ApplyArtSize();
        }

        /// <summary>
        /// 子节点按贴图原始宽高比铺满宽度并保持比例，底边对齐本节点底边。
        /// 缺口越大，露出贴图越靠上的部分；多出部分溢出屏幕顶边被裁掉。
        /// </summary>
        private void ApplyArtSize()
        {
            if (artRect == null)
            {
                return;
            }

            var width = 0f;
            if (rootCanvas != null)
            {
                var canvasRect = rootCanvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    width = canvasRect.rect.width;
                }
            }

            if (width <= 0f)
            {
                width = rectTransform.rect.width;
            }

            if (width <= 0f)
            {
                return;
            }

            var aspect = sourceAspect;
            var sprite = artImage != null ? artImage.sprite : null;
            if (sprite != null)
            {
                var spriteRect = sprite.rect;
                if (spriteRect.height > 0f)
                {
                    aspect = spriteRect.width / spriteRect.height;
                }
            }

            if (aspect <= 0f)
            {
                return;
            }

            artRect.anchorMin = new Vector2(0f, 0f);
            artRect.anchorMax = new Vector2(1f, 0f);
            artRect.pivot = new Vector2(0.5f, 0f);
            artRect.anchoredPosition = Vector2.zero;
            artRect.sizeDelta = new Vector2(0f, width / aspect);
        }

        private Rect ResolveSafeArea(Vector2Int screenSize)
        {
            var safeArea = Screen.safeArea;

#if UNITY_EDITOR
            if (simulateInEditor && !Application.isPlaying)
            {
                var top = Mathf.Clamp(simulatedTopInset, 0f, screenSize.y * 0.5f);
                var bottom = Mathf.Clamp(simulatedBottomInset, 0f, screenSize.y * 0.5f);
                safeArea = new Rect(0f, bottom, screenSize.x, screenSize.y - top - bottom);
            }
#endif

            return safeArea;
        }
    }
}
