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
        [SerializeField] private PressureRaceArtCatalog artCatalog;

        private float flashRemaining;
        private Color normalColor;

        public string RuntimeId { get; private set; }
        /// <summary>Stable ART_* handoff identifier for the currently bound archetype.</summary>
        public string ArtSlotId { get; private set; }
        public RectTransform RectTransform => transform as RectTransform;

        public void Configure(
            Image image,
            Image healthFill,
            Text label,
            PressureRaceArtCatalog catalog = null)
        {
            body = image;
            hpFill = healthFill;
            runtimeLabel = label;
            artCatalog = catalog;
            normalColor = body != null ? body.color : Color.white;
        }

        public void Bind(EnemyRuntime enemy)
        {
            RuntimeId = enemy.RuntimeId;
            ArtSlotId = artCatalog != null
                ? artCatalog.GetSlotId(enemy.Archetype)
                : GetFallbackArtSlotId(enemy.Archetype);
            gameObject.name = $"Enemy_{RuntimeId}";
            if (body != null)
            {
                var sprite = artCatalog != null ? artCatalog.GetEnemySprite(enemy.Archetype) : null;
                if (sprite != null)
                {
                    body.sprite = sprite;
                }

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

        private static string GetFallbackArtSlotId(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Fast:
                    return PressureRaceArtCatalog.EnemyFast;
                case EnemyArchetype.Swarm:
                    return PressureRaceArtCatalog.EnemySwarm;
                case EnemyArchetype.Elite:
                    return PressureRaceArtCatalog.EnemyElite;
                case EnemyArchetype.Boss:
                    return PressureRaceArtCatalog.EnemyBossReserved;
                default:
                    return PressureRaceArtCatalog.EnemyNormal;
            }
        }
    }
}
