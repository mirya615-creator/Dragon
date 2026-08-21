using UnityEngine;

namespace DragonBound.HandoffUi
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HandoffResponsiveLayout : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float phoneMaxWidth = 1080f;
        [SerializeField, Min(1f)] private float tabletMaxWidth = 1500f;
        [SerializeField, Min(0.1f)] private float tabletAspectThreshold = 0.7f;
        [SerializeField] private RectTransform fixedFormatContent;
        [SerializeField] private Vector2 fixedFormatAspect = new Vector2(9f, 16f);

        public bool IsTablet { get; private set; }
        public float PhoneMaxWidth => phoneMaxWidth;
        public float TabletMaxWidth => tabletMaxWidth;

        private void OnEnable() => Apply();
        private void Update() => Apply();
        public void Apply()
        {
            var rect = (RectTransform)transform;
            var width = rect.rect.width;
            var height = rect.rect.height;
            if (width <= 0f || height <= 0f) return;
            IsTablet = width / height >= tabletAspectThreshold;
            var maxWidth = IsTablet ? tabletMaxWidth : phoneMaxWidth;
            rect.localScale = Vector3.one;
            if (fixedFormatContent == null || fixedFormatAspect.y <= 0f) return;
            var targetWidth = Mathf.Min(width, maxWidth, height * fixedFormatAspect.x / fixedFormatAspect.y);
            var targetHeight = targetWidth * fixedFormatAspect.y / fixedFormatAspect.x;
            fixedFormatContent.anchorMin = new Vector2(0.5f, 0.5f);
            fixedFormatContent.anchorMax = new Vector2(0.5f, 0.5f);
            fixedFormatContent.anchoredPosition = Vector2.zero;
            fixedFormatContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            fixedFormatContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        }
    }
}
