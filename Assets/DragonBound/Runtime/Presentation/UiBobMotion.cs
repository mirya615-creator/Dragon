using UnityEngine;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiBobMotion : MonoBehaviour
    {
        [SerializeField] private float amplitudeY = 10f;
        [SerializeField] private float periodSeconds = 3f;
        [SerializeField] private float phaseDegrees = 0f;

        private RectTransform target;
        private Vector2 basePosition;
        private bool baseCaptured;

        private void Awake()
        {
            target = (RectTransform)transform;
            basePosition = target.anchoredPosition;
            baseCaptured = true;
        }

        private void Update()
        {
            if (!baseCaptured)
                return;

            var cycle = Time.unscaledTime / Mathf.Max(0.01f, periodSeconds) + (phaseDegrees / 360f);
            var offset = Mathf.Sin(cycle * Mathf.PI * 2f) * amplitudeY;
            target.anchoredPosition = basePosition + Vector2.up * offset;
        }

        private void OnValidate()
        {
            amplitudeY = Mathf.Max(0f, amplitudeY);
            periodSeconds = Mathf.Max(0.01f, periodSeconds);
        }
    }
}
