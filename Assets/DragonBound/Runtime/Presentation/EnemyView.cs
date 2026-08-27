using DragonBound.Core;
using DragonBound.Bosses.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class EnemyView : MonoBehaviour
    {
        private const int WaveAnimationCount = 4;
        private const string WaveAnimationRootName = "ART_EnemyAnimation";
        private static RuntimeAnimatorController[] waveAnimationControllers;
        private static bool waveAnimationControllersLoaded;
        private static Texture2D healthFillFallbackTexture;
        private static Sprite healthFillFallbackSprite;

        // Test-stage boss tint shared by both routes: #3F7CFF.
        private static readonly Color32 BossTestColor = new Color32(63, 124, 255, 255);
        private static readonly Color32 SubBossTestColor = new Color32(124, 76, 255, 255);
        private static readonly Color32 StormShieldColor = new Color32(75, 220, 255, 255);

        [SerializeField] private Image body;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text runtimeLabel;
        [SerializeField] private PressureRaceArtCatalog artCatalog;
        [Header("Health bar animation")]
        [SerializeField, Min(0.01f)] private float healthDecreaseDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float healthIncreaseDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float deathHealthDecreaseDuration = 0.12f;
        [Header("Authored presentation")]
        [SerializeField] private bool preserveAuthoredColor = true;
        [SerializeField] private bool preserveAuthoredSprite;
        [SerializeField] private bool preserveAuthoredSize = true;

        private float displayedHealthRatio = 1f;
        private float targetHealthRatio = 1f;
        private bool healthBarInitialized;
        private bool isDying;
        private Animator waveAnimator;
        private Image waveAnimationImage;
        private int boundWaveAnimationIndex = -1;
        private Color normalColor;
        private Color authoredBodyColor;
        private Sprite authoredSprite;
        private Vector2 authoredSizeDelta;
        private Vector3 authoredLocalScale;
        private Quaternion authoredLocalRotation;
        private Vector2 authoredAnchorMin;
        private Vector2 authoredAnchorMax;
        private Vector2 authoredPivot;
        private bool authoredPresentationCaptured;

        public string RuntimeId { get; private set; }
        /// <summary>Stable ART_* handoff identifier for the currently bound archetype.</summary>
        public string ArtSlotId { get; private set; }
        public RectTransform RectTransform => transform as RectTransform;

        private void Awake()
        {
            CaptureAuthoredPresentation();
            ResolveHealthBarView();
            ResolveWaveAnimationView();
        }

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
            healthBarInitialized = false;
            ResolveHealthBarView();
            CaptureAuthoredPresentation(true);
        }

        public void Bind(EnemyRuntime enemy)
        {
            RuntimeId = enemy.RuntimeId;
            BindWaveAnimation(enemy);
            ArtSlotId = artCatalog != null
                ? artCatalog.GetSlotId(enemy.Archetype)
                : GetFallbackArtSlotId(enemy.Archetype);
            gameObject.name = $"Enemy_{RuntimeId}";
            RestoreAuthoredTransform();
            if (body != null)
            {
                var sprite = artCatalog != null ? artCatalog.GetEnemySprite(enemy.Archetype) : null;
                if (preserveAuthoredSprite)
                {
                    body.sprite = authoredSprite;
                }
                else if (sprite != null)
                {
                    body.sprite = sprite;
                }

                if (preserveAuthoredColor)
                {
                    normalColor = authoredBodyColor;
                }
                else
                {
                    normalColor = body.color;
                }

                if (enemy.Archetype == EnemyArchetype.Boss)
                {
                    normalColor = enemy.BossId == WorldeaterWyrmConfiguration.SubBossId
                        ? SubBossTestColor
                        : BossTestColor;
                }
                else if (enemy.StormcallerShieldHitPoints > 0.0001f)
                {
                    normalColor = StormShieldColor;
                }

                body.color = normalColor;
            }

            if (runtimeLabel != null)
            {
                runtimeLabel.text = enemy.BossId == WorldeaterWyrmConfiguration.SubBossId
                    ? "SB"
                    : enemy.Archetype == EnemyArchetype.Boss
                        ? "B"
                        : enemy.StormcallerShieldHitPoints > 0.0001f
                            ? "S"
                            : enemy.Archetype == EnemyArchetype.Swarm ? "M" : "E";
            }

            if (hpFill != null)
            {
                var healthRatio = enemy.MaxHitPoints <= 0
                    ? 0f
                    : Mathf.Clamp01(enemy.HitPoints / enemy.MaxHitPoints);

                if (!healthBarInitialized)
                {
                    displayedHealthRatio = healthRatio;
                    targetHealthRatio = healthRatio;
                    hpFill.fillAmount = healthRatio;
                    healthBarInitialized = true;
                }
                else
                {
                    targetHealthRatio = healthRatio;
                }
            }
        }

        public void ShowDeathFlash()
        {
            isDying = true;
            targetHealthRatio = 0f;
        }

        private void Update()
        {
            UpdateHealthBar();
        }

        private void UpdateHealthBar()
        {
            if (!healthBarInitialized || hpFill == null ||
                Mathf.Approximately(displayedHealthRatio, targetHealthRatio))
            {
                return;
            }

            var duration = isDying
                ? deathHealthDecreaseDuration
                : targetHealthRatio < displayedHealthRatio
                    ? healthDecreaseDuration
                    : healthIncreaseDuration;
            var speed = 1f / Mathf.Max(0.01f, duration);
            displayedHealthRatio = Mathf.MoveTowards(
                displayedHealthRatio,
                targetHealthRatio,
                speed * Time.deltaTime);
            hpFill.fillAmount = displayedHealthRatio;
        }

        private void ResolveHealthBarView()
        {
            if (hpFill == null)
            {
                var healthTrack = transform.Find("ART_EnemyHpTrack");
                var healthFillTransform = healthTrack != null
                    ? healthTrack.Find("ART_EnemyHpFill")
                    : null;
                hpFill = healthFillTransform != null
                    ? healthFillTransform.GetComponent<Image>()
                    : null;
            }

            if (hpFill == null)
            {
                return;
            }

            // Fill Amount is the only visual value controlled at runtime. The
            // authored RectTransform, sprite, material and colour stay untouched.
            if (hpFill.sprite == null)
            {
                hpFill.sprite = GetOrCreateHealthFillSprite();
            }

            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFill.fillClockwise = true;
            hpFill.raycastTarget = false;
            hpFill.fillAmount = 1f;
            displayedHealthRatio = 1f;
            targetHealthRatio = 1f;
        }

        private static Sprite GetOrCreateHealthFillSprite()
        {
            if (healthFillFallbackSprite != null)
            {
                return healthFillFallbackSprite;
            }

            healthFillFallbackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "EnemyHealthFillFallbackTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            healthFillFallbackTexture.SetPixel(0, 0, Color.white);
            healthFillFallbackTexture.Apply(false, true);

            healthFillFallbackSprite = Sprite.Create(
                healthFillFallbackTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            healthFillFallbackSprite.name = "EnemyHealthFillFallbackSprite";
            healthFillFallbackSprite.hideFlags = HideFlags.HideAndDontSave;
            return healthFillFallbackSprite;
        }

        private void ResolveWaveAnimationView()
        {
            var animationRoot = transform.Find(WaveAnimationRootName);
            if (animationRoot == null)
            {
                return;
            }

            var healthTrack = transform.Find("ART_EnemyHpTrack");
            if (healthTrack != null)
            {
                // Keep the authored RectTransform, but render the health bar above
                // the full-card sprite animation.
                healthTrack.SetAsLastSibling();
            }

            var imageTransform = animationRoot.Find("Image");
            waveAnimationImage = imageTransform != null
                ? imageTransform.GetComponent<Image>()
                : animationRoot.GetComponent<Image>();
            if (waveAnimationImage == null)
            {
                return;
            }

            // The clips animate Image.m_Sprite at an empty relative path, so the
            // Animator must live on the same GameObject as the animated Image.
            waveAnimator = waveAnimationImage.GetComponent<Animator>();
            if (waveAnimator == null)
            {
                waveAnimator = waveAnimationImage.gameObject.AddComponent<Animator>();
            }

            var authoredAnimatorTransform = animationRoot.Find("Animator");
            var authoredAnimator = authoredAnimatorTransform != null
                ? authoredAnimatorTransform.GetComponent<Animator>()
                : null;
            if (authoredAnimator != null && authoredAnimator != waveAnimator)
            {
                authoredAnimator.enabled = false;
            }

            waveAnimationImage.raycastTarget = false;
        }

        private void BindWaveAnimation(EnemyRuntime enemy)
        {
            if (waveAnimationImage == null || waveAnimator == null)
            {
                ResolveWaveAnimationView();
            }

            if (waveAnimationImage == null || waveAnimator == null)
            {
                return;
            }

            var useWaveAnimation = enemy.Archetype != EnemyArchetype.Boss;
            waveAnimationImage.enabled = useWaveAnimation;
            waveAnimator.enabled = useWaveAnimation;
            if (!useWaveAnimation)
            {
                return;
            }

            var animationIndex = (Mathf.Max(1, enemy.SpawnWaveIndex) - 1) % WaveAnimationCount;
            if (boundWaveAnimationIndex == animationIndex && waveAnimator.runtimeAnimatorController != null)
            {
                return;
            }

            var controller = GetWaveAnimationController(animationIndex);
            if (controller == null)
            {
                waveAnimationImage.enabled = false;
                waveAnimator.enabled = false;
                return;
            }

            boundWaveAnimationIndex = animationIndex;
            waveAnimator.runtimeAnimatorController = controller;
            waveAnimator.Rebind();
            waveAnimator.Update(0f);
        }

        private static RuntimeAnimatorController GetWaveAnimationController(int animationIndex)
        {
            if (!waveAnimationControllersLoaded)
            {
                waveAnimationControllersLoaded = true;
                waveAnimationControllers = new RuntimeAnimatorController[WaveAnimationCount];
                var controllers = Resources.LoadAll<RuntimeAnimatorController>("Animation");
                for (var index = 0; index < controllers.Length; index++)
                {
                    var controller = controllers[index];
                    if (controller == null)
                    {
                        continue;
                    }

                    for (var slot = 0; slot < WaveAnimationCount; slot++)
                    {
                        if (string.Equals(controller.name, $"Enemy0{slot + 1}", System.StringComparison.Ordinal))
                        {
                            waveAnimationControllers[slot] = controller;
                            break;
                        }
                    }
                }
            }

            return animationIndex >= 0 && animationIndex < waveAnimationControllers.Length
                ? waveAnimationControllers[animationIndex]
                : null;
        }

        private void CaptureAuthoredPresentation(bool force = false)
        {
            if (authoredPresentationCaptured && !force)
            {
                return;
            }

            if (body != null)
            {
                authoredBodyColor = body.color;
                normalColor = authoredBodyColor;
                authoredSprite = body.sprite;
            }
            else
            {
                authoredBodyColor = Color.white;
                normalColor = authoredBodyColor;
                authoredSprite = null;
            }

            var rect = RectTransform;
            if (rect != null)
            {
                authoredSizeDelta = rect.sizeDelta;
                authoredLocalScale = rect.localScale;
                authoredLocalRotation = rect.localRotation;
                authoredAnchorMin = rect.anchorMin;
                authoredAnchorMax = rect.anchorMax;
                authoredPivot = rect.pivot;
            }

            authoredPresentationCaptured = true;
        }

        private void RestoreAuthoredTransform()
        {
            if (!preserveAuthoredSize)
            {
                return;
            }

            CaptureAuthoredPresentation();
            var rect = RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.sizeDelta = authoredSizeDelta;
            rect.localScale = authoredLocalScale;
            rect.localRotation = authoredLocalRotation;
            rect.anchorMin = authoredAnchorMin;
            rect.anchorMax = authoredAnchorMax;
            rect.pivot = authoredPivot;
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
