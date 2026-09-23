using DragonBound.Bosses.Runtime;
using DragonBound.Core;
using DragonBound.Presentation;
using Spine.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class EnemyView : MonoBehaviour
    {
        private const int WaveAnimationCount = 4;
        private const string WaveAnimationRootName = "ART_EnemyAnimation";
        private const string FrostcrownMarkName = "ART_FrostcrownHunterMark";
        private const string FrostcrownMarkSpritePath = "VFX/Frostcrown Hunter/road";
        private const string FrostMireMarkName = "ART_FrostMireMark";
        private const string FrostMireMarkSpritePath = "VFX/Item/微信图片_20260909164612_605_101";
        private const string WinterveilMarkName = "ART_WinterveilMark";
        private const string WinterveilMarkSpritePath = "VFX/Item/微信图片_20260909164615_606_101";
        private const string DeathVfxName = "ART_EnemyDeathVFX";
        private const string DeathControllerResourcePath = "Animation/DieBoom";
        private const string StormShieldVfxName = "ART_StormShieldVFX";
        private const string StormShieldControllerResourcePath = "Animation/ShieldShieldW";
        private const string StormShieldFirstFrameResourcePath = "VFX/ShieldShieldW/Shield_O4_0";
        private const float DeathDestroyPadding = 0.02f;
        private static readonly Vector2 FrostcrownMarkSize = new Vector2(22f, 33f);
        private const float StatusMarkCenterY = 58f;
        private const float StatusMarkCenterSpacing = 24f;
        private static readonly Vector2 DeathVfxSize = new Vector2(110f, 110f);
        private static RuntimeAnimatorController[] waveAnimationControllers;
        private static bool waveAnimationControllersLoaded;
        private static readonly Dictionary<int, RuntimeAnimatorController> BossAnimationControllers =
            new Dictionary<int, RuntimeAnimatorController>();
        private static readonly HashSet<int> MissingBossAnimationWaves = new HashSet<int>();
        private static readonly HashSet <int > MissingBossSkeletonWaves= new HashSet<int>();
        private static Texture2D healthFillFallbackTexture;
        private static Sprite healthFillFallbackSprite; 
        private static Sprite frostcrownMarkSprite;
        private static Sprite frostMireMarkSprite;
        private static Sprite winterveilMarkSprite;
        private static RuntimeAnimatorController deathAnimationController;
        private static bool deathAnimationControllerLoaded;
        private static bool deathAnimationMissingWarningLogged;
        private static RuntimeAnimatorController stormShieldAnimationController;
        private static Sprite stormShieldFirstFrame;
        private static bool stormShieldResourcesLoaded;
        private static bool stormShieldMissingWarningLogged;

        [SerializeField] private Image body;
        [SerializeField] private Image hpFill;
        [SerializeField] private Image overHp;

        [Header("Over-hp delay bar")]
        [SerializeField, Min(0f)] private float overHpDelayDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float overHpDecreaseDuration = 0.5f;
        [SerializeField] private Text runtimeLabel;
        [SerializeField] private PressureRaceArtCatalog artCatalog;
        [Header("Health bar animation")]
        [SerializeField, Min(0.01f)] private float healthDecreaseDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float healthIncreaseDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float deathHealthDecreaseDuration = 0.12f;
        [Header("Hit reaction")]
        [SerializeField, Min(0.01f)] private float hitShakeDuration = 0.16f;
        [SerializeField, Min(0f)] private float hitShakeDistance = 8f;
        [SerializeField, Min(1f)] private float bossHitShakeMultiplier = 1.6f;
        [SerializeField, Range(0.05f, 0.9f)] private float hitShakeRecoilPortion = 0.3f;
        [SerializeField, Range(0f, 30f)] private float hitShakeLeanDegrees = 10f;
        [SerializeField, Range(0f, 0.5f)] private float hitShakeFootRatio = 0f;
        [Header("Authored presentation")]
        [SerializeField] private bool preserveAuthoredColor = true;
        [SerializeField] private bool preserveAuthoredSprite;
        [SerializeField] private bool preserveAuthoredSize = true;
        [Header("W12 storm shield presentation")]
        [SerializeField] private Vector2 stormShieldVfxSize = new Vector2(110f, 110f);

        [SerializeField] private Image hpTrack;

        [SerializeField] private SkeletonGraphic waveSkeleton;
        [SerializeField] private SkeletonDataAsset[] waveSkeletonData;

        [Serializable]
        public struct EnemySkeletonWaveEntry
        {
            public int wave;                     // 6 / 12 / 16 / 20
            public SkeletonDataAsset skeleton;
            public float scale;                  // 该 Boss 的缩放，1 = 沿用节点当前值
        }

        [SerializeField] private EnemySkeletonWaveEntry[] bossSkeletons;

        private int boundWaveSkeletonIndex = -1;
        private Sprite authoredHpTrackSprite;
        private static Sprite bossHpTrackSprite;
        private const string BossHpResourcePath = "GameUI/BossHp";

        private float displayedOverHpRatio = 1f;
        private float overHpDelayTimer;

        private float displayedHealthRatio = 1f;
        private float targetHealthRatio = 1f;
        private bool healthBarInitialized;
        private bool isDying;
        private int healthVisualHoldCount;
        private float heldHealthRatio = 1f;
        private Animator waveAnimator;
        private Image waveAnimationImage;
        private RectTransform waveAnimationRoot;
        private Vector3 authoredWaveAnimationScale = Vector3.one;
        private bool waveAnimationScaleCaptured;
        private Vector3 authoredSkeletonScale = Vector3.one;
        private bool skeletonScaleCaptured;
        private Vector2 authoredWaveAnimationPosition;
        private bool waveAnimationPositionCaptured;
        private float hitShakeRemaining;
        private Vector2 hitShakeBackward = Vector2.left;
        private Quaternion authoredWaveAnimationRotation = Quaternion.identity;
        private bool boundAsBoss;
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
        private Image frostcrownMarkImage;
        private Image frostMireMarkImage;
        private Image winterveilMarkImage;
        private Image deathVfxImage;
        private Animator deathVfxAnimator;
        private float deathSequenceElapsed;
        private float deathSequenceDuration;
        private bool deathHealthBarHidden;
        private Image stormShieldVfxImage;
        private Animator stormShieldVfxAnimator;
        private string stormShieldRuntimeId;
        private float lastStormShieldHitPoints;

        public string RuntimeId { get; private set; }
        /// <summary>Stable ART_* handoff identifier for the currently bound archetype.</summary>
        public string ArtSlotId { get; private set; }
        public RectTransform RectTransform => transform as RectTransform;
        public Vector3 VisualImpactPosition
        {
            get
            {
                if (waveAnimationImage == null)
                {
                    ResolveWaveAnimationView();
                }

                return waveAnimationImage != null
                    ? waveAnimationImage.rectTransform.position
                    : RectTransform.position;
            }
        }
        public bool IsHealthVisualHeld => healthVisualHoldCount > 0;
        public bool IsFrostcrownMarked =>
            frostcrownMarkImage != null && frostcrownMarkImage.gameObject.activeSelf;
        public Image FrostcrownMarkImage => frostcrownMarkImage;
        public bool IsFrostMireMarked =>
            frostMireMarkImage != null && frostMireMarkImage.gameObject.activeSelf;
        public Image FrostMireMarkImage => frostMireMarkImage;
        public bool IsWinterveilMarked =>
            winterveilMarkImage != null && winterveilMarkImage.gameObject.activeSelf;
        public Image WinterveilMarkImage => winterveilMarkImage;
        public bool IsStormShieldVisible =>
            stormShieldVfxImage != null && stormShieldVfxImage.gameObject.activeSelf;

        public void SetFrostcrownMarked(bool marked)
        {
            if (marked)
            {
                EnsureFrostcrownMarkView();
            }

            if (frostcrownMarkImage != null)
            {
                frostcrownMarkImage.gameObject.SetActive(marked);
            }

            LayoutStatusMarks();
        }

        private void EnsureFrostcrownMarkView()
        {
            if (frostcrownMarkImage != null)
            {
                return;
            }

            var existing = transform.FindUi(FrostcrownMarkName);
            if (existing != null)
            {
                frostcrownMarkImage = existing.GetComponent<Image>();
            }

            if (frostcrownMarkImage == null)
            {
                var root = new GameObject(
                    FrostcrownMarkName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, StatusMarkCenterY);
                rect.sizeDelta = FrostcrownMarkSize;
                frostcrownMarkImage = root.GetComponent<Image>();
            }

            frostcrownMarkSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(FrostcrownMarkSpritePath);
            frostcrownMarkImage.sprite = frostcrownMarkSprite;
            frostcrownMarkImage.color = Color.white;
            frostcrownMarkImage.preserveAspect = true;
            frostcrownMarkImage.raycastTarget = false;

            var healthTrack = transform.FindUi("ART_EnemyHpTrack");
            if (healthTrack != null)
            {
                frostcrownMarkImage.rectTransform.SetSiblingIndex(
                    healthTrack.GetSiblingIndex());
            }
        }

        public void SetFrostMireMarked(bool marked)
        {
            if (marked)
            {
                EnsureFrostMireMarkView();
            }

            if (frostMireMarkImage != null)
            {
                frostMireMarkImage.gameObject.SetActive(marked);
            }

            LayoutStatusMarks();
        }

        private void EnsureFrostMireMarkView()
        {
            if (frostMireMarkImage != null)
            {
                return;
            }

            var existing = transform.FindUi(FrostMireMarkName);
            if (existing != null)
            {
                frostMireMarkImage = existing.GetComponent<Image>();
            }

            if (frostMireMarkImage == null)
            {
                var root = new GameObject(
                    FrostMireMarkName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, StatusMarkCenterY);
                rect.sizeDelta = FrostcrownMarkSize;
                frostMireMarkImage = root.GetComponent<Image>();
            }

            frostMireMarkSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(FrostMireMarkSpritePath);
            frostMireMarkImage.sprite = frostMireMarkSprite;
            frostMireMarkImage.color = Color.white;
            frostMireMarkImage.preserveAspect = true;
            frostMireMarkImage.raycastTarget = false;

            var healthTrack = transform.FindUi("ART_EnemyHpTrack");
            if (healthTrack != null)
            {
                frostMireMarkImage.rectTransform.SetSiblingIndex(
                    healthTrack.GetSiblingIndex());
            }
        }

        public void SetWinterveilMarked(bool marked)
        {
            if (marked)
            {
                EnsureWinterveilMarkView();
            }

            if (winterveilMarkImage != null)
            {
                winterveilMarkImage.gameObject.SetActive(marked);
            }

            LayoutStatusMarks();
        }

        private void EnsureWinterveilMarkView()
        {
            if (winterveilMarkImage != null)
            {
                return;
            }

            var existing = transform.FindUi(WinterveilMarkName);
            if (existing != null)
            {
                winterveilMarkImage = existing.GetComponent<Image>();
            }

            if (winterveilMarkImage == null)
            {
                var root = new GameObject(
                    WinterveilMarkName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, StatusMarkCenterY);
                rect.sizeDelta = FrostcrownMarkSize;
                winterveilMarkImage = root.GetComponent<Image>();
            }

            winterveilMarkSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(WinterveilMarkSpritePath);
            winterveilMarkImage.sprite = winterveilMarkSprite;
            winterveilMarkImage.color = Color.white;
            winterveilMarkImage.preserveAspect = true;
            winterveilMarkImage.raycastTarget = false;

            var healthTrack = transform.FindUi("ART_EnemyHpTrack");
            if (healthTrack != null)
            {
                winterveilMarkImage.rectTransform.SetSiblingIndex(
                    healthTrack.GetSiblingIndex());
            }
        }

        private void LayoutStatusMarks()
        {
            var frostcrownVisible = IsFrostcrownMarked;
            var frostMireVisible = IsFrostMireMarked;
            var winterveilVisible = IsWinterveilMarked;
            var visibleCount =
                (frostcrownVisible ? 1 : 0) +
                (frostMireVisible ? 1 : 0) +
                (winterveilVisible ? 1 : 0);
            var nextX = -((visibleCount - 1) * StatusMarkCenterSpacing * 0.5f);

            if (frostcrownVisible)
            {
                frostcrownMarkImage.rectTransform.anchoredPosition =
                    new Vector2(nextX, StatusMarkCenterY);
                nextX += StatusMarkCenterSpacing;
            }

            if (frostMireVisible)
            {
                frostMireMarkImage.rectTransform.anchoredPosition =
                    new Vector2(nextX, StatusMarkCenterY);
                nextX += StatusMarkCenterSpacing;
            }

            if (winterveilVisible)
            {
                winterveilMarkImage.rectTransform.anchoredPosition =
                    new Vector2(nextX, StatusMarkCenterY);
            }
        }

        public void HoldHealthVisual()
        {
            // A lethal combat event may arrive before its authored projectile is released.
            // Once death presentation has started, never let that projectile retain the
            // previous non-zero health bar and keep the corpse looking alive.
            if (isDying)
            {
                return;
            }

            if (healthVisualHoldCount == 0)
            {
                heldHealthRatio = targetHealthRatio;
            }
            healthVisualHoldCount++;
        }

        public void ReleaseHealthVisual()
        {
            if (healthVisualHoldCount <= 0)
            {
                return;
            }

            healthVisualHoldCount--;
            // Publish the latest sampled health after every completed hit. Other overlapping
            // projectiles may still retain this view, but they must not keep the bar frozen at
            // the value from the first hit until every visual has finished.
            if (heldHealthRatio < targetHealthRatio)
            {
                overHpDelayTimer = overHpDelayDuration;
            }
            targetHealthRatio = heldHealthRatio;
        }

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
            if (isDying)
            {
                return;
            }

            RuntimeId = enemy.RuntimeId;
            SetFrostMireMarked(enemy.IsAlive && enemy.IsFrostMireAffected);
            SetWinterveilMarked(enemy.IsWinterveilAffected);
            boundAsBoss = enemy.Archetype == EnemyArchetype.Boss;
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

                body.color = normalColor;
            }
            UpdateStormShieldVisual(enemy.StormcallerShieldHitPoints);
            if (hpTrack != null)
            {
                if (enemy.Archetype == EnemyArchetype.Boss)
                {
                    if (bossHpTrackSprite == null)
                    {
                        bossHpTrackSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(BossHpResourcePath);
                    }
                    if (bossHpTrackSprite != null)
                    {
                        hpTrack.sprite = bossHpTrackSprite;
                    }
                }
                else
                {
                    hpTrack.sprite = authoredHpTrackSprite;   // 非 Boss 还原作者底图
                }
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

                if (IsHealthVisualHeld && healthBarInitialized)
                {
                    heldHealthRatio = healthRatio;
                    return;
                }

                if (!healthBarInitialized)
                {
                    displayedHealthRatio = healthRatio;
                    targetHealthRatio = healthRatio;
                    hpFill.fillAmount = healthRatio;
                    displayedOverHpRatio = healthRatio;                    
                    if (overHp != null) overHp.fillAmount = healthRatio;   
                    overHpDelayTimer = 0f;                                  
                    healthBarInitialized = true;
                }
                else
                {
                    // 掉血时启动延迟计时器（回血不延迟，直接 snap）
                    if (healthRatio < targetHealthRatio)
                    {
                        overHpDelayTimer = overHpDelayDuration;
                    }
                    targetHealthRatio = healthRatio;
                }
            }
        }
        public void ShowDeathFlash()
        {
            if (isDying)
            {
                return;
            }

            isDying = true;
            healthVisualHoldCount = 0;
            targetHealthRatio = 0f;
            heldHealthRatio = 0f;
            overHpDelayTimer = 0f;
            deathSequenceElapsed = 0f;
            deathHealthBarHidden = false;
            hitShakeRemaining = 0f;
            RestoreAuthoredWaveAnimation();

            SetFrostcrownMarked(false);
            SetFrostMireMarked(false);
            SetWinterveilMarked(false);
            HideStormShieldVisual();
            HideLivingPresentationForDeath();
            PlayDeathVfx();
        }

        public void PlayHitShake()
        {
            if (waveAnimationRoot == null)
            {
                ResolveWaveAnimationView();
            }

            if (waveAnimationRoot == null || !waveAnimationRoot.gameObject.activeInHierarchy)
            {
                return;
            }

            // Restart from the prefab-authored origin so rapid hits never accumulate drift.
            waveAnimationRoot.anchoredPosition = authoredWaveAnimationPosition;
            waveAnimationRoot .localRotation = authoredWaveAnimationRotation;
            hitShakeRemaining = Mathf.Max(0.01f, hitShakeDuration);
        }

        public void SetWaveAnimationMirrored(bool mirrored)
        {
            if (waveAnimationRoot == null)
            {
                ResolveWaveAnimationView();
            }

            if (waveAnimationRoot == null)
            {
                return;
            }

            var scale = authoredWaveAnimationScale;
            scale.x = mirrored ? -scale.x : scale.x;
            waveAnimationRoot.localScale = scale;
            hitShakeBackward = mirrored ? Vector2.right : Vector2.left;
        }

        private void Update()
        {
            UpdateHealthBar();
            if (isDying)
            {
                UpdateDeathSequence();
            }
            else
            {
                UpdateHitShake();
            }
        }

        private void UpdateDeathSequence()
        {
            deathSequenceElapsed += Time.unscaledDeltaTime;

            if (!deathHealthBarHidden && deathSequenceElapsed >= deathHealthDecreaseDuration)
            {
                deathHealthBarHidden = true;
                displayedHealthRatio = 0f;
                displayedOverHpRatio = 0f;
                if (hpFill != null)
                {
                    hpFill.fillAmount = 0f;
                }
                if (overHp != null)
                {
                    overHp.fillAmount = 0f;
                }
                if (hpTrack != null)
                {
                    hpTrack.gameObject.SetActive(false);
                }
            }

            if (deathSequenceElapsed >= deathSequenceDuration)
            {
                Destroy(gameObject);
            }
        }

        private void HideLivingPresentationForDeath()
        {
            if (waveAnimationRoot != null)
            {
                if (waveAnimationPositionCaptured)
                {
                    waveAnimationRoot.anchoredPosition = authoredWaveAnimationPosition;
                }
                waveAnimationRoot.gameObject.SetActive(false);
            }

            if (body != null)
            {
                body.enabled = false;
            }
            if (runtimeLabel != null)
            {
                runtimeLabel.gameObject.SetActive(false);
            }
        }

        private void PlayDeathVfx()
        {
            EnsureDeathVfxView();
            var clipLength = 0f;
            if (deathVfxAnimator != null && deathVfxAnimator.runtimeAnimatorController != null)
            {
                var clips = deathVfxAnimator.runtimeAnimatorController.animationClips;
                for (var index = 0; index < clips.Length; index++)
                {
                    if (clips[index] != null)
                    {
                        clipLength = Mathf.Max(clipLength, clips[index].length);
                    }
                }

                deathVfxAnimator.enabled = true;
                deathVfxAnimator.Rebind();
                deathVfxAnimator.Play(0, 0, 0f);
                deathVfxAnimator.Update(0f);
            }

            deathSequenceDuration = Mathf.Max(
                deathHealthDecreaseDuration,
                clipLength > 0f ? clipLength : 0.15f) + DeathDestroyPadding;
        }

        private void EnsureDeathVfxView()
        {
            if (deathVfxImage != null && deathVfxAnimator != null)
            {
                deathVfxImage.gameObject.SetActive(true);
                deathVfxImage.rectTransform.SetAsLastSibling();
                return;
            }

            var existing = transform.FindUi(DeathVfxName);
            if (existing == null)
            {
                var root = new GameObject(
                    DeathVfxName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Animator));
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = DeathVfxSize;
                existing = rect;
            }

            deathVfxImage = existing.GetComponent<Image>();
            if (deathVfxImage == null)
            {
                deathVfxImage = existing.gameObject.AddComponent<Image>();
            }
            deathVfxAnimator = existing.GetComponent<Animator>();
            if (deathVfxAnimator == null)
            {
                deathVfxAnimator = existing.gameObject.AddComponent<Animator>();
            }
            deathVfxImage.color = Color.white;
            deathVfxImage.preserveAspect = true;
            deathVfxImage.raycastTarget = false;

            if (!deathAnimationControllerLoaded)
            {
                deathAnimationController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(DeathControllerResourcePath);
                deathAnimationControllerLoaded = true;
            }

            deathVfxAnimator.runtimeAnimatorController = deathAnimationController;
            deathVfxAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            deathVfxAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            existing.gameObject.SetActive(true);
            existing.SetAsLastSibling();

            if (deathAnimationController == null && !deathAnimationMissingWarningLogged)
            {
                deathAnimationMissingWarningLogged = true;
                Debug.LogWarning(
                    $"Enemy death presentation requires Resources/{DeathControllerResourcePath}.controller.",
                    this);
            }
        }

        private void UpdateHitShake()
        {
            if (waveAnimationRoot == null || hitShakeRemaining <= 0f)
            {
                return;
            }

            var duration = Mathf.Max(0.01f, hitShakeDuration);
            hitShakeRemaining = Mathf.Max(0f, hitShakeRemaining - Time.deltaTime);
            var progress = 1f - (hitShakeRemaining / duration);

            // One-shot recoil lean: rotate around the foot point, then settle home.
            // The card root keeps advancing along the lane; only the body leans
            // back, so a hit reads as being staggered, not shoved sideways.
            var recoilPortion = Mathf.Clamp(hitShakeRecoilPortion, 0.05f, 0.9f);
            float push;
            if (progress < recoilPortion)
            {
                var t = progress / recoilPortion;
                push = 1f - ((1f - t) * (1f - t));
            }
            else
            {
                var t = (progress - recoilPortion) / (1f - recoilPortion);
                push = 1f - (t * t * (3f - 2f * t));
            }

            var maxRadians = Mathf.Max(0f, hitShakeLeanDegrees) * Mathf.Deg2Rad *
                (boundAsBoss ? bossHitShakeMultiplier : 1f);
            var angle = -hitShakeBackward.x * maxRadians * push;

            waveAnimationRoot.localRotation = authoredWaveAnimationRotation *
                Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);

            // The pivot sits at the card center: compensate the position so the
            // foot point stays anchored in parent space while the body rotates.
            var scale = waveAnimationRoot.localScale;
            var footArm = Mathf.Abs(scale.y) * waveAnimationRoot.rect.height *
                (0.5f - Mathf.Clamp(hitShakeFootRatio, 0f, 0.5f));
            var sin = Mathf.Sin(angle);
            var cos = Mathf.Cos(angle);
            waveAnimationRoot.anchoredPosition =
                authoredWaveAnimationPosition + new Vector2(-footArm * sin, footArm * (cos - 1f));

            if (hitShakeRemaining <= 0f)
            {
                RestoreAuthoredWaveAnimation();
            }
        }

        private void RestoreAuthoredWaveAnimation()
        {
            if (waveAnimationRoot == null)
            {
                return;
            }

            if (waveAnimationPositionCaptured)
            {
                waveAnimationRoot.anchoredPosition = authoredWaveAnimationPosition;
            }

            waveAnimationRoot.localRotation = authoredWaveAnimationRotation;
        }

        private void OnDisable()
        {
            SetFrostcrownMarked(false);
            SetFrostMireMarked(false);
            SetWinterveilMarked(false);
            HideStormShieldVisual();
            hitShakeRemaining = 0f;
            RestoreAuthoredWaveAnimation();
        }

        private void UpdateStormShieldVisual(float shieldHitPoints)
        {
            if (shieldHitPoints <= 0.0001f)
            {
                HideStormShieldVisual();
                stormShieldRuntimeId = RuntimeId;
                lastStormShieldHitPoints = 0f;
                return;
            }

            EnsureStormShieldView();
            if (stormShieldVfxImage == null || stormShieldVfxAnimator == null ||
                stormShieldVfxAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            var effectObject = stormShieldVfxImage.gameObject;
            var shouldRestart = !effectObject.activeSelf ||
                                stormShieldRuntimeId != RuntimeId ||
                                shieldHitPoints > lastStormShieldHitPoints + 0.0001f;
            effectObject.SetActive(true);
            stormShieldVfxImage.rectTransform.SetAsLastSibling();
            if (shouldRestart)
            {
                stormShieldVfxAnimator.enabled = true;
                stormShieldVfxAnimator.Rebind();
                stormShieldVfxAnimator.Play(0, 0, 0f);
                stormShieldVfxAnimator.Update(0f);
            }

            stormShieldRuntimeId = RuntimeId;
            lastStormShieldHitPoints = shieldHitPoints;
        }

        private void EnsureStormShieldView()
        {
            if (stormShieldVfxImage != null && stormShieldVfxAnimator != null)
            {
                return;
            }

            var existing = transform.FindUi(StormShieldVfxName);
            if (existing == null)
            {
                var root = new GameObject(
                    StormShieldVfxName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Animator));
                root.SetActive(false);
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = stormShieldVfxSize;
                existing = rect;
            }

            stormShieldVfxImage = existing.GetComponent<Image>();
            if (stormShieldVfxImage == null)
            {
                stormShieldVfxImage = existing.gameObject.AddComponent<Image>();
            }
            stormShieldVfxAnimator = existing.GetComponent<Animator>();
            if (stormShieldVfxAnimator == null)
            {
                stormShieldVfxAnimator = existing.gameObject.AddComponent<Animator>();
            }

            LoadStormShieldResources();
            stormShieldVfxImage.sprite = stormShieldFirstFrame;
            stormShieldVfxImage.color = Color.white;
            stormShieldVfxImage.preserveAspect = true;
            stormShieldVfxImage.raycastTarget = false;
            stormShieldVfxAnimator.runtimeAnimatorController = stormShieldAnimationController;
            stormShieldVfxAnimator.updateMode = AnimatorUpdateMode.Normal;
            stormShieldVfxAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            existing.SetAsLastSibling();

            if (stormShieldAnimationController == null && !stormShieldMissingWarningLogged)
            {
                stormShieldMissingWarningLogged = true;
                Debug.LogWarning(
                    $"Enemy shield presentation requires Resources/{StormShieldControllerResourcePath}.controller.",
                    this);
            }
        }

        private static void LoadStormShieldResources()
        {
            if (stormShieldResourcesLoaded)
            {
                return;
            }

            stormShieldResourcesLoaded = true;
            stormShieldAnimationController =
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(StormShieldControllerResourcePath);
            stormShieldFirstFrame = DragonBound.Presentation.UiAssets.Load<Sprite>(StormShieldFirstFrameResourcePath);
        }

        private void HideStormShieldVisual()
        {
            if (stormShieldVfxImage == null)
            {
                return;
            }

            if (stormShieldVfxAnimator != null)
            {
                stormShieldVfxAnimator.enabled = false;
            }
            stormShieldVfxImage.gameObject.SetActive(false);
        }

        private void UpdateHealthBar()
        {
            if (!healthBarInitialized || hpFill == null)
            {
                return;
            }

            // ① fill 快速追向 target（原有逻辑，仅去掉 early return）
            if (!Mathf.Approximately(displayedHealthRatio, targetHealthRatio))
            {
                var duration = isDying
                    ? deathHealthDecreaseDuration
                    : targetHealthRatio < displayedHealthRatio
                        ? healthDecreaseDuration
                        : healthIncreaseDuration;
                var speed = 1f / Mathf.Max(0.01f, duration);
                displayedHealthRatio = Mathf.MoveTowards(
                    displayedHealthRatio,
                    targetHealthRatio,
                    speed * (isDying ? Time.unscaledDeltaTime : Time.deltaTime));
                hpFill.fillAmount = displayedHealthRatio;
            }

            // ② overHp 延迟追平（新增，无条件执行）
            UpdateOverHp();
        }

        private void UpdateOverHp()
        {
            if (overHp == null)
            {
                return;
            }

            // 回血或持平：overHp 直接 snap 到 target，不滞后（黄色条不应戳到绿色条上方）
            if (targetHealthRatio >= displayedOverHpRatio)
            {
                if (!Mathf.Approximately(displayedOverHpRatio, targetHealthRatio))
                {
                    displayedOverHpRatio = targetHealthRatio;
                    overHp.fillAmount = displayedOverHpRatio;
                }
                overHpDelayTimer = 0f;
                return;
            }

            // 掉血：延迟期内保持不动
            if (overHpDelayTimer > 0f)
            {
                overHpDelayTimer -= Time.deltaTime;
                return;
            }

            // 延迟结束：缓慢追向 target
            var speed = 1f / Mathf.Max(0.01f, overHpDecreaseDuration);
            displayedOverHpRatio = Mathf.MoveTowards(
                displayedOverHpRatio,
                targetHealthRatio,
                speed * Time.deltaTime);
            overHp.fillAmount = displayedOverHpRatio;
        }


        private void ResolveHealthBarView()
        {
            if (hpFill == null)
            {
                var healthTrack = transform.FindUi("ART_EnemyHpTrack");
                var healthFillTransform = healthTrack != null
                    ? healthTrack.FindUi("ART_EnemyHpFill")
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

            if (overHp ==null )
            {
                var track = transform.FindUi("ART_EnemyHpTrack");
                var overHpTransform = track != null ? track.FindUi("overHp") : null;
                overHp =overHpTransform != null?overHpTransform .GetComponent<Image >() : null;
            }
            if (overHp !=null )
            {
                overHp .type =Image .Type.Filled;
                overHp .fillMethod = Image.FillMethod.Horizontal;
                overHp .fillOrigin = (int)Image.OriginHorizontal.Left;
                overHp .fillClockwise = true;
                overHp .raycastTarget = false;
                overHp .fillAmount = 1f;
            }

            if(hpTrack ==null )
            {
                var track = transform.FindUi("ART_EnemyHpTrack");
                hpTrack =track !=null ?track .GetComponent <Image>() : null;
            }
            if (hpTrack !=null && authoredHpTrackSprite ==null )
            {
                authoredHpTrackSprite = hpTrack.sprite;
            }
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
            var animationRoot = transform.FindUi(WaveAnimationRootName);
            if (animationRoot == null)
            {
                return;
            }

            waveAnimationRoot = animationRoot as RectTransform;
            if (waveAnimationRoot != null && !waveAnimationScaleCaptured)
            {
                authoredWaveAnimationScale = waveAnimationRoot.localScale;
                waveAnimationScaleCaptured = true;
            }
            if (waveAnimationRoot != null && !waveAnimationPositionCaptured)
            {
                authoredWaveAnimationPosition = waveAnimationRoot.anchoredPosition;
                authoredWaveAnimationRotation = waveAnimationRoot.localRotation;
                waveAnimationPositionCaptured = true;
            }

            if (waveSkeleton != null && !skeletonScaleCaptured)
            {
                authoredSkeletonScale = waveSkeleton.transform.localScale;
                skeletonScaleCaptured = true;
            }


            var healthTrack = transform.FindUi("ART_EnemyHpTrack");
            if (healthTrack != null)
            {
                // Keep the authored RectTransform, but render the health bar above
                // the full-card sprite animation.
                healthTrack.SetAsLastSibling();
            }

            var imageTransform = animationRoot.FindUi("Image");
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

            var authoredAnimatorTransform = animationRoot.FindUi("Animator");
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
            if (waveSkeleton != null)
            {
                var asset = ResolveSkeletonForWave(enemy, out var slotKey, out var scale);
                if (asset != null)
                {
                    if (boundWaveSkeletonIndex != slotKey || !waveSkeleton.IsValid)
                    {
                        waveSkeleton.skeletonDataAsset = asset;
                        waveSkeleton.Initialize(true);
                        waveSkeleton.AnimationState.SetAnimation(0, "Walk", true);
                        waveSkeleton.Update(0f);
                        boundWaveSkeletonIndex = slotKey;
                        waveSkeleton.transform.localScale = authoredSkeletonScale * scale;  // Boss 尺寸
                    }
                    if (waveAnimationImage != null) waveAnimationImage.enabled = false;
                    if (waveAnimator != null) waveAnimator.enabled = false;
                    return;
                }
            }

                if (waveAnimationImage == null || waveAnimator == null)
            {
                ResolveWaveAnimationView();
            }

                if (waveAnimationImage == null || waveAnimator == null)
            {
                return;
            }

            var isBoss = enemy.Archetype == EnemyArchetype.Boss;
            var animationIndex = isBoss
                ? -Mathf.Max(1, enemy.SpawnWaveIndex)
                : (Mathf.Max(1, enemy.SpawnWaveIndex) - 1) % WaveAnimationCount;
            if (boundWaveAnimationIndex == animationIndex &&
                waveAnimator.runtimeAnimatorController != null)
            {
                return;
            }

            var controller = isBoss
                ? GetBossAnimationController(enemy.SpawnWaveIndex)
                : GetWaveAnimationController(animationIndex);
            if (controller == null)
            {
                waveAnimationImage.enabled = false;
                waveAnimator.enabled = false;
                return;
            }

            boundWaveAnimationIndex = animationIndex;
            waveAnimationImage.enabled = true;
            waveAnimator.enabled = true;
            waveAnimator.runtimeAnimatorController = controller;
            waveAnimator.Rebind();
            waveAnimator.Update(0f);
        }

        private SkeletonDataAsset ResolveSkeletonForWave(EnemyRuntime enemy, out int slotKey, out float scale)
        {
            scale = 1f;
            var spawnWave = Mathf.Max(1, enemy.SpawnWaveIndex);

            if (enemy.Archetype == EnemyArchetype.Boss)
            {
                // Boss 与波次一一对应，不做循环；slotKey 取负波次，避免与小兵 index(0..3) 撞键。
                slotKey = -spawnWave;
                if (bossSkeletons != null)
                {
                    for (var index = 0; index < bossSkeletons.Length; index++)
                    {
                        var entry = bossSkeletons[index];
                        if (entry.wave != spawnWave || entry.skeleton == null)
                        {
                            continue;
                        }

                        scale = entry.scale > 0f ? entry.scale : 1f;
                        return entry.skeleton;
                    }
                }

                slotKey = -1;
                if (MissingBossSkeletonWaves.Add(spawnWave))
                {
                    Debug.LogWarning($"Boss skeleton is not configured for wave {spawnWave}.");
                }

                return null;    // 未配置 → 回退原 Animator 路径（V2 无对应键 → 空白）
            }

            if (waveSkeletonData == null || waveSkeletonData.Length == 0)
            {
                slotKey = -1;
                return null;
            }

            slotKey = (spawnWave - 1) % waveSkeletonData.Length;
            return waveSkeletonData[slotKey];
        }

        private static RuntimeAnimatorController GetBossAnimationController(int spawnWave)
        {
            if (BossAnimationControllers.TryGetValue(spawnWave, out var cached) && cached != null)
            {
                return cached;
            }

            string controllerName;
            switch (spawnWave)
            {
                case 6:
                    controllerName = "BossW06";
                    break;
                case 12:
                    controllerName = "BossW12";
                    break;
                case 16:
                    controllerName = "BossW16";
                    break;
                case 20:
                    controllerName = "BossW20";
                    break;
                default:
                    return null;
            }

            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/" + controllerName);
            if (controller != null)
            {
                BossAnimationControllers[spawnWave] = controller;
                return controller;
            }

            if (MissingBossAnimationWaves.Add(spawnWave))
            {
                Debug.LogWarning(
                    $"Boss animation controller 'Resources/Animation/{controllerName}' is missing for wave {spawnWave}.");
            }

            return null;
        }

        private static RuntimeAnimatorController GetWaveAnimationController(int animationIndex)
        {
            if (!waveAnimationControllersLoaded)
            {
                waveAnimationControllersLoaded = true;
                waveAnimationControllers = new RuntimeAnimatorController[WaveAnimationCount];
                var controllers = DragonBound.Presentation.UiAssets.LoadAll<RuntimeAnimatorController>("Animation");
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
