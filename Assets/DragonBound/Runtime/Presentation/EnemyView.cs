using DragonBound.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField] private Image body;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text runtimeLabel;

        private float flashRemaining;
        private Color normalColor;

        public string RuntimeId { get; private set; }
        public RectTransform RectTransform => transform as RectTransform;

        public void Configure(Image image, Image healthFill, Text label)
        {
            body = image;
            hpFill = healthFill;
            runtimeLabel = label;
            normalColor = body != null ? body.color : Color.white;
        }

        public void Bind(EnemyRuntime enemy)
        {
            RuntimeId = enemy.RuntimeId;
            gameObject.name = $"Enemy_{RuntimeId}";
            if (body != null)
            {
                if (normalColor == default)
                {
                    normalColor = body.color;
                }

                body.color = flashRemaining > 0f ? Color.white : normalColor;
            }

            if (runtimeLabel != null)
            {
                runtimeLabel.text = "E";
            }

            if (hpFill != null)
            {
                hpFill.fillAmount = enemy.MaxHitPoints <= 0
                    ? 0f
                    : Mathf.Clamp01(enemy.HitPoints / enemy.MaxHitPoints);
            }
        }

        public void ShowDeathFlash()
        {
            flashRemaining = 0.16f;
            if (body != null)
            {
                body.color = Color.white;
            }
        }

        private void Update()
        {
            if (flashRemaining <= 0f)
            {
                return;
            }

            flashRemaining -= Time.deltaTime;
            if (flashRemaining <= 0f && body != null)
            {
                body.color = normalColor;
            }
        }
    }
}
