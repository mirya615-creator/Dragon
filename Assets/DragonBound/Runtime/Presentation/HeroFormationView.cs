using System;
using DragonBound.Presentation;
using System.Collections;
using System.Collections.Generic;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

namespace DragonBound.Presentation
{
    // Authored as a prefab so final art can replace every greybox element in the Inspector.
    public sealed class HeroFormationView : MonoBehaviour
    {
        private const string LevelUpVfxName = "ART_LevelUpVFX";
        private const string LevelUpControllerResourcePath = "Animation/LevelUp";
        private const float LevelUpAnimationSpeed = 0.1f;
        private const string SynthesisVfxName = "ART_SynthesisVFX";
        private const string PurpleSynthesisControllerResourcePath = "Animation/synthesisP";
        private const string GoldSynthesisControllerResourcePath = "Animation/synthesisG";
        private const float SynthesisAnimationSpeed = 0.5f;
        private const float SynthesisHeroRevealStartNormalized = 0.35f;
        private const float SynthesisHeroRevealDuration = 0.08f;
        private const float SynthesisHeroRevealStartScale = 0.85f;
        private const float ArtFacingTransitionSeconds = 0.14f;
        private static readonly Vector2 LevelUpVfxPosition = new Vector2(3.4f, -13f);
        private static readonly Vector2 LevelUpVfxSize = new Vector2(150f, 100f);

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image connectorLine;
        [SerializeField] private Image doubleCellBorder;
        [SerializeField] private Text heroNameLabel;
        [SerializeField] private Image runeImage;
        [SerializeField] private Animator heroAttackAnimator;
        [SerializeField] private Image particalBgImage;
        [SerializeField] private SkeletonGraphic heroAttackSkeleton;

        private Vector3 connectorScale = Vector3.one;
        private bool connectorScaleCaptured;
        private string configuredAnimationHeroId = string.Empty;
        private string configuredSpineHeroId = string.Empty;
        private int observedAttackSequence;
        private bool attackSequenceObserved;
        private int lastAttackAnimationFrame = -1;
        private RuntimeAnimatorController defaultAttackController;
        private RuntimeAnimatorController skillAttackController;
        private Coroutine attackResetCoroutine;
        private int attackPlaybackVersion;
        private bool attackAnimationPlaying;
        private bool attackAnimationIsSkill;
        private string combatRuntimeId = string.Empty;
        private Vector3 authoredHeroArtScale = Vector3.one;
        private Vector3 authoredParticalBgScale = Vector3.one;
        private bool heroArtScalesCaptured;
        private Vector2 authoredHeroArtAnchoredPosition;
        private bool heroArtAnchoredPositionCaptured;
        private static Sprite purpleFrameSprite;
        private static Sprite goldFrameSprite;
        private static bool rarityFramesLoaded;
        private Image levelUpVfxImage;
        private Animator levelUpVfxAnimator;
        private float levelUpVfxRemainingSeconds;
        private Image synthesisVfxImage;
        private Animator synthesisVfxAnimator;
        private float synthesisVfxRemainingSeconds;
        private float synthesisHeroRevealRemainingSeconds;
        private float synthesisHeroRevealFadeRemainingSeconds;
        private bool synthesisHeroArtHidden;
        private bool synthesisHeroRevealStarted;
        private CanvasGroup synthesisHeroCanvasGroup;
        private Vector3 synthesisHeroRevealTargetScale = Vector3.one;
        private float synthesisHeroRevealScaleFactor = 1f;
        private Coroutine artFacingCoroutine;
        private bool artFacingInitialized;
        private bool artFacingMirrored;

        public RectTransform RectTransform => (RectTransform)transform;
        public Image RuneImage => runeImage;
        public Animator HeroAttackAnimator => heroAttackAnimator;
        public Text HeroNameLabel => heroNameLabel;
        public Image LevelUpVfxImage => levelUpVfxImage;
        public Animator LevelUpVfxAnimator => levelUpVfxAnimator;
        public float LevelUpPlaybackSpeed => LevelUpAnimationSpeed;
        public bool IsLevelUpVfxVisible =>
            levelUpVfxImage != null && levelUpVfxImage.gameObject.activeSelf;
        public Image SynthesisVfxImage => synthesisVfxImage;
        public Animator SynthesisVfxAnimator => synthesisVfxAnimator;
        public float SynthesisPlaybackSpeed => SynthesisAnimationSpeed;
        public bool IsSynthesisVfxVisible =>
            synthesisVfxImage != null && synthesisVfxImage.gameObject.activeSelf;
        public bool IsArtMirrored => artFacingInitialized
            ? artFacingMirrored
            : heroAttackAnimator != null &&
              Mathf.Sign(heroAttackAnimator.transform.localScale.x) !=
              Mathf.Sign(authoredHeroArtScale.x);

        public bool TryGetAttackOrigin(out Vector3 position)
        {
            if (heroAttackAnimator != null && heroAttackAnimator.gameObject.activeInHierarchy)
            {
                position = heroAttackAnimator.transform.position;
                return true;
            }

            position = RectTransform.position;
            return false;
        }
        public event Action<string> FlameDrakeFireballReleased;
        public event Action<string> SkyborneValkyrieArrowReleased;
        public event Action<string> StarfallArchmageGemReleased;
        public event Action<string> NightfangSkillAnimationCompleted;
        public event Action<string> WindclawSkillReleased;
        public event Action<string> EmberShamanFireballReleased;
        public event Action<string> RuneboltMageBoltReleased;
        public event Action<string> StoneboundWarlockRockReleased;
        public event Action<string> ThunderlordChainReleased;
        public event Action<string> ThunderlordSkillReleased;
        public event Action<string> AbyssalHarpoonReleased;
        public event Action<string> AbyssalHarpoonSkillReleased;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void DisableStandaloneRuneboltMagePreviewAnimators()
        {
            foreach (var animator in Resources.FindObjectsOfTypeAll<Animator>())
            {
                if (animator == null ||
                    !animator.gameObject.scene.IsValid() ||
                    animator.GetComponentInParent<HeroFormationView>(true) != null ||
                    animator.runtimeAnimatorController == null ||
                    !string.Equals(
                        animator.runtimeAnimatorController.name,
                        "Runebolt Mage",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                animator.gameObject.SetActive(false);
            }
        }

        public void SetCombatRuntimeId(string runtimeId)
        {
            combatRuntimeId = runtimeId ?? string.Empty;
        }

        public void Configure(
            CanvasGroup group,
            Image line,
            Image border,
            Text nameLabel,
            Image equippedRuneImage = null,
            Animator attackAnimator = null)
        {
            canvasGroup = group;
            connectorLine = line;
            doubleCellBorder = border;
            heroNameLabel = nameLabel;
            runeImage = equippedRuneImage;
            heroAttackAnimator = attackAnimator;
            CaptureHeroArtScales();
        }

        public void Initialize(
            Vector2 center,
            Vector2 primaryOffset,
            Vector2 secondaryOffset,
            Vector2 footprintSize,
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
            DisableRaycast(doubleCellBorder);

            // HeroNameLabel is authored UI. Its layout and style remain prefab-owned;
            // the displayed level is synchronized by SetHeroLevel.

            if (doubleCellBorder != null)
            {
                ApplyRarityFrame(rarity, footprintSize, secondaryOffset - primaryOffset);
            }

            if (connectorLine != null)
            {
                if (heroAttackAnimator == null)
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
            }

            SetProgress(0f);
        }

        public void SetRune(string runtimeRuneId)
        {
            SetRuneSprite(RuneUiSpriteCatalog.Load(runtimeRuneId));
        }

        public void SetHeroLevel(int level)
        {
            if (heroNameLabel != null)
            {
                heroNameLabel.text = $"LV {Mathf.Max(1, level)}";
            }
        }

        public void SetRuneSprite(Sprite sprite)
        {
            if (runeImage == null)
            {
                return;
            }

            runeImage.sprite = sprite;
            runeImage.raycastTarget = false;
            runeImage.gameObject.SetActive(sprite != null);
        }

        public void SetHeroAnimation(string heroId)
        {
            ApplyParticalBackground(heroId);
            ApplyHeroSpineArt(heroId);
            if (heroAttackAnimator == null ||
                string.Equals(configuredAnimationHeroId, heroId, StringComparison.Ordinal))
            {
                return;
            }

            CancelAttackReset();
            configuredAnimationHeroId = heroId ?? string.Empty;
            attackSequenceObserved = false;
            observedAttackSequence = 0;
            defaultAttackController = HeroAnimationControllerCatalog.Load(heroId);
            skillAttackController = HeroAnimationControllerCatalog.LoadSkill(heroId);
            heroAttackAnimator.runtimeAnimatorController = defaultAttackController;
            if (defaultAttackController == null)
            {
                heroAttackAnimator.enabled = false;
                return;
            }

            // Keep the authored first frame visible without autoplaying when the formation is created.
            heroAttackAnimator.enabled = true;
            var relay = heroAttackAnimator.GetComponent<HeroAnimationEventRelay>();
            if (relay == null)
            {
                relay = heroAttackAnimator.gameObject.AddComponent<HeroAnimationEventRelay>();
            }
            relay.Bind(this);
            heroAttackAnimator.Rebind();
            heroAttackAnimator.Update(0f);
            heroAttackAnimator.speed = 0f;
        }

        public void SetArtMirrored(bool mirrored)
        {
            CaptureHeroArtScales();
            StopArtFacingTransition();
            artFacingInitialized = true;
            artFacingMirrored = mirrored;
            if (heroAttackAnimator != null)
            {
                var scale = authoredHeroArtScale;
                if (mirrored)
                {
                    scale.x = -scale.x;
                }

                synthesisHeroRevealTargetScale = scale;
                heroAttackAnimator.transform.localScale = synthesisHeroRevealStarted
                    ? scale * synthesisHeroRevealScaleFactor
                    : scale;
            }
            if (heroAttackSkeleton != null)
            {
                var spineScale = heroAttackSkeleton.transform.localScale;
                spineScale.x = mirrored ? -Mathf.Abs(spineScale.x) : Mathf.Abs(spineScale.x);
                heroAttackSkeleton.transform.localScale = spineScale;
            }

            if (particalBgImage != null)
            {
                var scale = authoredParticalBgScale;
                if (mirrored)
                {
                    scale.x = -scale.x;
                }

                particalBgImage.rectTransform.localScale = scale;
            }
        }

        public void InitializeArtFacing(bool mirrored, float mirroredAnchoredPositionX)
        {
            if (artFacingInitialized)
            {
                return;
            }

            ApplyArtFacing(mirrored, mirroredAnchoredPositionX, false);
        }

        public void FaceArtTowards(bool mirrored, float mirroredAnchoredPositionX)
        {
            ApplyArtFacing(mirrored, mirroredAnchoredPositionX, true);
        }

        private void ApplyArtFacing(
            bool mirrored,
            float mirroredAnchoredPositionX,
            bool animate)
        {
            CaptureHeroArtScales();
            CaptureHeroArtAnchoredPosition();
            var heroRect = heroAttackAnimator != null
                ? heroAttackAnimator.transform as RectTransform
                : null;
            if (heroRect == null)
            {
                return;
            }

            var targetHeroScaleX = mirrored
                ? -authoredHeroArtScale.x
                : authoredHeroArtScale.x;
            var targetPositionX = mirrored
                ? mirroredAnchoredPositionX
                : authoredHeroArtAnchoredPosition.x;
            var targetParticalScaleX = mirrored
                ? -authoredParticalBgScale.x
                : authoredParticalBgScale.x;

            if (artFacingInitialized && artFacingMirrored == mirrored)
            {
                return;
            }

            StopArtFacingTransition();
            artFacingInitialized = true;
            artFacingMirrored = mirrored;
            var targetHeroScale = authoredHeroArtScale;
            targetHeroScale.x = targetHeroScaleX;
            synthesisHeroRevealTargetScale = targetHeroScale;

            var particalRect = particalBgImage != null
                ? particalBgImage.rectTransform
                : null;
            if (!animate || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                SetArtFacingValues(
                    heroRect,
                    targetHeroScaleX,
                    targetPositionX,
                    particalRect,
                    targetParticalScaleX);
                return;
            }

            artFacingCoroutine = StartCoroutine(AnimateArtFacing(
                heroRect,
                heroRect.localScale.x,
                targetHeroScaleX,
                heroRect.anchoredPosition.x,
                targetPositionX,
                particalRect,
                particalRect != null ? particalRect.localScale.x : 0f,
                targetParticalScaleX));
        }

        private IEnumerator AnimateArtFacing(
            RectTransform heroRect,
            float startHeroScaleX,
            float targetHeroScaleX,
            float startPositionX,
            float targetPositionX,
            RectTransform particalRect,
            float startParticalScaleX,
            float targetParticalScaleX)
        {
            var elapsed = 0f;
            while (elapsed < ArtFacingTransitionSeconds && heroRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / ArtFacingTransitionSeconds);
                var eased = progress * progress * (3f - (2f * progress));
                SetArtFacingValues(
                    heroRect,
                    Mathf.LerpUnclamped(startHeroScaleX, targetHeroScaleX, eased),
                    Mathf.LerpUnclamped(startPositionX, targetPositionX, eased),
                    particalRect,
                    Mathf.LerpUnclamped(
                        startParticalScaleX,
                        targetParticalScaleX,
                        eased));
                yield return null;
            }

            if (heroRect != null)
            {
                SetArtFacingValues(
                    heroRect,
                    targetHeroScaleX,
                    targetPositionX,
                    particalRect,
                    targetParticalScaleX);
            }
            artFacingCoroutine = null;
        }

        private static void SetArtFacingValues(
            RectTransform heroRect,
            float heroScaleX,
            float anchoredPositionX,
            RectTransform particalRect,
            float particalScaleX)
        {
            var heroScale = heroRect.localScale;
            heroScale.x = heroScaleX;
            heroRect.localScale = heroScale;

            var position = heroRect.anchoredPosition;
            position.x = anchoredPositionX;
            heroRect.anchoredPosition = position;

            if (particalRect == null)
            {
                return;
            }

            var particalScale = particalRect.localScale;
            particalScale.x = particalScaleX;
            particalRect.localScale = particalScale;
        }

        private void StopArtFacingTransition()
        {
            if (artFacingCoroutine == null)
            {
                return;
            }

            StopCoroutine(artFacingCoroutine);
            artFacingCoroutine = null;
        }

        public void SetHeroArtAnchoredPositionX(float? anchoredPositionX)
        {
            if (heroAttackAnimator == null)
            {
                return;
            }

            CaptureHeroArtAnchoredPosition();
            var heroArtRect = heroAttackAnimator.transform as RectTransform;
            if (heroArtRect == null)
            {
                return;
            }

            var position = authoredHeroArtAnchoredPosition;
            if (anchoredPositionX.HasValue)
            {
                position.x = anchoredPositionX.Value;
            }

            heroArtRect.anchoredPosition = position;
        }

        private void CaptureHeroArtScales()
        {
            if (heroArtScalesCaptured)
            {
                return;
            }

            authoredHeroArtScale = heroAttackAnimator != null
                ? heroAttackAnimator.transform.localScale
                : Vector3.one;
            authoredParticalBgScale = particalBgImage != null
                ? particalBgImage.rectTransform.localScale
                : Vector3.one;
            heroArtScalesCaptured = true;
        }

        private void CaptureHeroArtAnchoredPosition()
        {
            if (heroArtAnchoredPositionCaptured || heroAttackAnimator == null)
            {
                return;
            }

            var heroArtRect = heroAttackAnimator.transform as RectTransform;
            if (heroArtRect == null)
            {
                return;
            }

            authoredHeroArtAnchoredPosition = heroArtRect.anchoredPosition;
            heroArtAnchoredPositionCaptured = true;
        }

        private void ApplyParticalBackground(string heroId)
        {
            if (particalBgImage == null)
            {
                var particalBg = transform.FindUi("ParticalBg");
                if (particalBg != null)
                {
                    particalBgImage = particalBg.GetComponent<Image>();
                }
            }

            if (particalBgImage == null)
            {
                return;
            }

            var sprite = HeroParticalBackgroundCatalog.Load(heroId);
            particalBgImage.sprite = sprite;
            particalBgImage.raycastTarget = false;
            particalBgImage.gameObject.SetActive(sprite != null);
        }

        private void ApplyHeroSpineArt(string heroId)
        {
            if (heroAttackSkeleton == null ||
                string.Equals(configuredSpineHeroId, heroId, StringComparison.Ordinal))
            {
                return;
            }

            configuredSpineHeroId = heroId ?? string.Empty;
            var skeleton = HeroSpineArtCatalog.Load(heroId);
            if (skeleton == null)
            {
                heroAttackSkeleton.gameObject.SetActive(false);
                return;
            }

            heroAttackSkeleton.gameObject.SetActive(true);
            heroAttackSkeleton.skeletonDataAsset = skeleton;
            heroAttackSkeleton.Initialize(true);
            // Idle: hold the authored setup pose; the attack clip is triggered per attack.  
            heroAttackSkeleton.AnimationState.SetEmptyAnimation(0, 0f);
            heroAttackSkeleton.Skeleton.SetToSetupPose();
            heroAttackSkeleton.Update(0f);

        }


        public void ObserveAttackSequence(int attackSequence)
        {
            attackSequence = Mathf.Max(0, attackSequence);
            if (!attackSequenceObserved || attackSequence < observedAttackSequence)
            {
                observedAttackSequence = attackSequence;
                attackSequenceObserved = true;
                return;
            }

            if (attackSequence == observedAttackSequence)
            {
                return;
            }

            observedAttackSequence = attackSequence;
            PlayAttackAnimation();
        }

        public bool PlayAttackAnimation(bool useSkillAnimation = false)
        {
            if (heroAttackAnimator == null)
            {
                return PlaySpineAttackAnimation();
            }

            var desiredController = useSkillAnimation && skillAttackController != null

                ? skillAttackController
                : defaultAttackController;
            if (heroAttackAnimator == null ||
                desiredController == null ||
                !heroAttackAnimator.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (attackAnimationPlaying)
            {
                // Multi-target attacks may request the same animation several times. Treat those
                // as the same action, but never let a Basic and a skill replace one another while
                // the current clip is still playing.
                return attackAnimationIsSkill == useSkillAnimation;
            }

            // A powered attack also increments SuccessfulAttackSequence. The board observes that
            // sequence in LateUpdate; do not let its default-animation request replace a skill
            // animation that CombatFx started earlier in the same frame.
            if (!useSkillAnimation &&
                lastAttackAnimationFrame == Time.frameCount &&
                skillAttackController != null &&
                heroAttackAnimator.runtimeAnimatorController == skillAttackController)
            {
                return true;
            }

            var controllerChanged = heroAttackAnimator.runtimeAnimatorController != desiredController;
            if (lastAttackAnimationFrame == Time.frameCount && !controllerChanged)
            {
                return true;
            }

            CancelAttackReset();
            lastAttackAnimationFrame = Time.frameCount;
            attackAnimationPlaying = true;
            attackAnimationIsSkill = useSkillAnimation;
            heroAttackAnimator.enabled = true;
            heroAttackAnimator.speed = 1f;
            if (controllerChanged)
            {
                heroAttackAnimator.runtimeAnimatorController = desiredController;
                heroAttackAnimator.Rebind();
            }
            // State hash 0 restarts the current/default state at the authored first frame.
            heroAttackAnimator.Play(0, 0, 0f);
            heroAttackAnimator.Update(0f);
            int playbackVersion = ++attackPlaybackVersion;
            attackResetCoroutine = StartCoroutine(
                RestoreDefaultPoseAfterPlayback(desiredController, playbackVersion));
            return true;
        }

        private bool PlaySpineAttackAnimation()
        {
            if (heroAttackSkeleton == null ||
                !heroAttackSkeleton.gameObject.activeInHierarchy ||
                heroAttackSkeleton.Skeleton == null)
            {
                return false;
            }
            // Play the authored swing once, then fall back to the setup pose so the hero reads  
            // as idle until the next attack.  
            var entry = heroAttackSkeleton.AnimationState.SetAnimation(
            0, HeroSpineArtCatalog.AttackAnimationName, false);
            if (entry == null)
            {
                return false;
            }
            heroAttackSkeleton.AnimationState.AddEmptyAnimation(0, 0f, 0f);
            return true;

        }


        private IEnumerator RestoreDefaultPoseAfterPlayback(
            RuntimeAnimatorController playedController,
            int playbackVersion)
        {
            // Animator state information is not reliable until at least the next frame.
            yield return null;

            float elapsed = 0f;
            float fallbackSeconds = ResolveAttackResetFallbackSeconds(playedController);
            while (elapsed < fallbackSeconds)
            {
                if (playbackVersion != attackPlaybackVersion ||
                    heroAttackAnimator == null ||
                    !heroAttackAnimator.gameObject.activeInHierarchy ||
                    heroAttackAnimator.runtimeAnimatorController != playedController)
                {
                    yield break;
                }

                AnimatorStateInfo state = heroAttackAnimator.GetCurrentAnimatorStateInfo(0);
                if (!heroAttackAnimator.IsInTransition(0) && state.normalizedTime >= 1f)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (playbackVersion != attackPlaybackVersion ||
                heroAttackAnimator == null ||
                defaultAttackController == null ||
                !heroAttackAnimator.gameObject.activeInHierarchy)
            {
                yield break;
            }

            heroAttackAnimator.runtimeAnimatorController = defaultAttackController;
            heroAttackAnimator.enabled = true;
            heroAttackAnimator.Rebind();
            heroAttackAnimator.Update(0f);
            heroAttackAnimator.speed = 0f;
            attackAnimationPlaying = false;
            attackAnimationIsSkill = false;
            attackResetCoroutine = null;
        }

        private static float ResolveAttackResetFallbackSeconds(
            RuntimeAnimatorController controller)
        {
            float longestClipSeconds = 0f;
            AnimationClip[] clips = controller != null ? controller.animationClips : null;
            if (clips != null)
            {
                for (int index = 0; index < clips.Length; index++)
                {
                    if (clips[index] != null)
                        longestClipSeconds = Mathf.Max(longestClipSeconds, clips[index].length);
                }
            }

            // Some authored controller states play below 1x speed. normalizedTime is the primary
            // completion signal; this timeout only prevents malformed controllers from sticking.
            return Mathf.Max(1f, longestClipSeconds * 4f + 0.5f);
        }

        private void CancelAttackReset()
        {
            attackPlaybackVersion++;
            attackAnimationPlaying = false;
            attackAnimationIsSkill = false;
            if (attackResetCoroutine == null) return;
            StopCoroutine(attackResetCoroutine);
            attackResetCoroutine = null;
        }

        public bool PlayLevelUpAnimation()
        {
            EnsureLevelUpVfx();
            if (levelUpVfxAnimator == null ||
                levelUpVfxAnimator.runtimeAnimatorController == null ||
                !gameObject.activeInHierarchy)
            {
                return false;
            }

            ApplyLevelUpVfxLayout();
            var vfxObject = levelUpVfxAnimator.gameObject;
            vfxObject.SetActive(true);
            levelUpVfxAnimator.transform.SetAsLastSibling();
            levelUpVfxAnimator.enabled = true;
            // LevelUp.controller owns the effective state speed. Keep the Animator at 1
            // so the two speed multipliers never compound.
            levelUpVfxAnimator.speed = 1f;
            levelUpVfxAnimator.Rebind();
            levelUpVfxAnimator.Play(0, 0, 0f);
            levelUpVfxAnimator.Update(0f);

            var clipLength = 0f;
            var clips = levelUpVfxAnimator.runtimeAnimatorController.animationClips;
            for (var index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null)
                {
                    clipLength = Mathf.Max(clipLength, clips[index].length);
                }
            }

            levelUpVfxRemainingSeconds =
                (clipLength > 0f ? clipLength : 0.1f) / LevelUpAnimationSpeed;
            return true;
        }

        private void EnsureLevelUpVfx()
        {
            if (levelUpVfxImage != null && levelUpVfxAnimator != null)
            {
                ApplyLevelUpVfxLayout();
                return;
            }

            var existing = transform.FindUi(LevelUpVfxName);
            if (existing == null)
            {
                var root = new GameObject(
                    LevelUpVfxName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Animator));
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = LevelUpVfxPosition;
                rect.sizeDelta = LevelUpVfxSize;
                existing = rect;
            }

            levelUpVfxImage = existing.GetComponent<Image>();
            levelUpVfxAnimator = existing.GetComponent<Animator>();
            if (levelUpVfxImage != null)
            {
                levelUpVfxImage.color = Color.white;
                levelUpVfxImage.raycastTarget = false;
            }

            ApplyLevelUpVfxLayout();

            if (levelUpVfxAnimator != null)
            {
                levelUpVfxAnimator.runtimeAnimatorController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(LevelUpControllerResourcePath);
                levelUpVfxAnimator.speed = 1f;
            }

            existing.gameObject.SetActive(false);
        }

        private void ApplyLevelUpVfxLayout()
        {
            if (levelUpVfxImage == null)
            {
                return;
            }

            var rect = levelUpVfxImage.rectTransform;
            rect.anchoredPosition = LevelUpVfxPosition;
            rect.sizeDelta = LevelUpVfxSize;
            // The authored frames are portrait sprites. Stretch them into the intended
            // 150x100 effect area so the visible VFX matches the HeroFormation artwork.
            levelUpVfxImage.preserveAspect = false;
        }

        public bool PlaySynthesisAnimation(HeroRecipeRarity rarity)
        {
            if (synthesisHeroArtHidden)
            {
                SetHeroArtVisible(true);
                synthesisHeroArtHidden = false;
                if (synthesisHeroCanvasGroup != null)
                {
                    synthesisHeroCanvasGroup.alpha = 1f;
                }
            }
            CompleteSynthesisHeroReveal();
            EnsureSynthesisVfx();
            if (synthesisVfxAnimator == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            var controllerPath = rarity == HeroRecipeRarity.Gold
                ? GoldSynthesisControllerResourcePath
                : PurpleSynthesisControllerResourcePath;
            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"Hero synthesis animation is missing at Resources/{controllerPath}.", this);
                return false;
            }

            ApplySynthesisVfxLayout();
            var vfxObject = synthesisVfxAnimator.gameObject;
            vfxObject.SetActive(true);
            synthesisVfxAnimator.transform.SetAsLastSibling();
            synthesisVfxAnimator.runtimeAnimatorController = controller;
            // Both controller states are authored at Speed 0.5. Animator speed stays at
            // one so the state speed is not multiplied a second time.
            synthesisVfxAnimator.speed = 1f;
            synthesisVfxAnimator.enabled = true;
            synthesisVfxAnimator.Rebind();
            synthesisVfxAnimator.Play(0, 0, 0f);
            synthesisVfxAnimator.Update(0f);

            var clipLength = 0f;
            var clips = controller.animationClips;
            for (var index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null)
                {
                    clipLength = Mathf.Max(clipLength, clips[index].length);
                }
            }

            synthesisVfxRemainingSeconds =
                (clipLength > 0f ? clipLength : 0.1f) / SynthesisAnimationSpeed;
            synthesisHeroRevealRemainingSeconds =
                synthesisVfxRemainingSeconds * SynthesisHeroRevealStartNormalized;
            synthesisHeroRevealFadeRemainingSeconds = 0f;
            synthesisHeroRevealStarted = false;
            synthesisHeroArtHidden = heroAttackAnimator != null && heroAttackAnimator.gameObject.activeSelf;
            if (synthesisHeroArtHidden)
            {
                EnsureSynthesisHeroCanvasGroup();
                if (synthesisHeroCanvasGroup != null)
                {
                    synthesisHeroCanvasGroup.alpha = 0f;
                }
                SetHeroArtVisible(false);
            }

            return true;
        }

        private void EnsureSynthesisVfx()
        {
            if (synthesisVfxImage != null && synthesisVfxAnimator != null)
            {
                ApplySynthesisVfxLayout();
                return;
            }

            var existing = transform.FindUi(SynthesisVfxName);
            if (existing == null)
            {
                var root = new GameObject(
                    SynthesisVfxName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Animator));
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                existing = rect;
            }

            synthesisVfxImage = existing.GetComponent<Image>();
            synthesisVfxAnimator = existing.GetComponent<Animator>();
            if (synthesisVfxImage != null)
            {
                synthesisVfxImage.color = Color.white;
                synthesisVfxImage.preserveAspect = true;
                synthesisVfxImage.raycastTarget = false;
            }

            ApplySynthesisVfxLayout();
            existing.gameObject.SetActive(false);
        }

        private void ApplySynthesisVfxLayout()
        {
            if (synthesisVfxImage == null)
            {
                return;
            }

            var rect = synthesisVfxImage.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                Mathf.Max(180f, RectTransform.rect.width),
                Mathf.Max(130f, RectTransform.rect.height + 20f));
            rect.localScale = Vector3.one;
            synthesisVfxImage.preserveAspect = true;
        }

        private void Update()
        {
            if (levelUpVfxRemainingSeconds > 0f)
            {
                levelUpVfxRemainingSeconds = Mathf.Max(
                    0f,
                    levelUpVfxRemainingSeconds - Time.deltaTime);
                if (levelUpVfxRemainingSeconds <= 0f && levelUpVfxImage != null)
                {
                    levelUpVfxImage.gameObject.SetActive(false);
                }
            }

            if (synthesisVfxRemainingSeconds <= 0f)
            {
                return;
            }

            synthesisVfxRemainingSeconds = Mathf.Max(
                0f,
                synthesisVfxRemainingSeconds - Time.deltaTime);
            if (synthesisHeroArtHidden)
            {
                synthesisHeroRevealRemainingSeconds = Mathf.Max(
                    0f,
                    synthesisHeroRevealRemainingSeconds - Time.deltaTime);
                if (synthesisHeroRevealRemainingSeconds <= 0f)
                {
                    BeginSynthesisHeroReveal();
                    synthesisHeroArtHidden = false;
                }
            }

            if (synthesisHeroRevealFadeRemainingSeconds > 0f)
            {
                synthesisHeroRevealFadeRemainingSeconds = Mathf.Max(
                    0f,
                    synthesisHeroRevealFadeRemainingSeconds - Time.deltaTime);
                var progress = 1f -
                    (synthesisHeroRevealFadeRemainingSeconds / SynthesisHeroRevealDuration);
                var eased = progress * progress * (3f - (2f * progress));
                if (synthesisHeroCanvasGroup != null)
                {
                    synthesisHeroCanvasGroup.alpha = eased;
                }
                if (heroAttackAnimator != null)
                {
                    synthesisHeroRevealScaleFactor = Mathf.LerpUnclamped(
                        SynthesisHeroRevealStartScale,
                        1f,
                        eased);
                    heroAttackAnimator.transform.localScale = Vector3.LerpUnclamped(
                        synthesisHeroRevealTargetScale * SynthesisHeroRevealStartScale,
                        synthesisHeroRevealTargetScale,
                        eased);
                }
            }

            if (synthesisVfxRemainingSeconds <= 0f)
            {
                if (synthesisHeroArtHidden)
                {
                    BeginSynthesisHeroReveal();
                    synthesisHeroArtHidden = false;
                }

                CompleteSynthesisHeroReveal();

                if (synthesisVfxImage != null)
                {
                    synthesisVfxImage.gameObject.SetActive(false);
                }
            }
        }

        private void BeginSynthesisHeroReveal()
        {
            SetHeroArtVisible(true);
            if (heroAttackAnimator == null)
            {
                return;
            }

            EnsureSynthesisHeroCanvasGroup();
            synthesisHeroRevealStarted = true;
            synthesisHeroRevealTargetScale = heroAttackAnimator.transform.localScale;
            synthesisHeroRevealScaleFactor = SynthesisHeroRevealStartScale;
            heroAttackAnimator.transform.localScale =
                synthesisHeroRevealTargetScale * SynthesisHeroRevealStartScale;
            if (synthesisHeroCanvasGroup != null)
            {
                synthesisHeroCanvasGroup.alpha = 0f;
            }
            synthesisHeroRevealFadeRemainingSeconds = SynthesisHeroRevealDuration;
        }

        private void CompleteSynthesisHeroReveal()
        {
            synthesisHeroRevealFadeRemainingSeconds = 0f;
            if (!synthesisHeroRevealStarted)
            {
                return;
            }

            if (synthesisHeroCanvasGroup != null)
            {
                synthesisHeroCanvasGroup.alpha = 1f;
            }
            if (heroAttackAnimator != null)
            {
                heroAttackAnimator.transform.localScale = synthesisHeroRevealTargetScale;
            }
            synthesisHeroRevealScaleFactor = 1f;
            synthesisHeroRevealStarted = false;
        }

        private void EnsureSynthesisHeroCanvasGroup()
        {
            if (heroAttackAnimator == null || synthesisHeroCanvasGroup != null)
            {
                return;
            }

            synthesisHeroCanvasGroup = heroAttackAnimator.GetComponent<CanvasGroup>();
            if (synthesisHeroCanvasGroup == null)
            {
                synthesisHeroCanvasGroup = heroAttackAnimator.gameObject.AddComponent<CanvasGroup>();
            }
            synthesisHeroCanvasGroup.interactable = false;
            synthesisHeroCanvasGroup.blocksRaycasts = false;
        }

        public void SetHeroArtVisible(bool visible)
        {
            if (heroAttackAnimator != null)
            {
                CancelAttackReset();
                if (!visible)
                {
                    heroAttackAnimator.speed = 0f;
                    heroAttackAnimator.gameObject.SetActive(false);
                    return;
                }

                heroAttackAnimator.gameObject.SetActive(true);
                if (defaultAttackController != null)
                {
                    heroAttackAnimator.runtimeAnimatorController = defaultAttackController;
                    heroAttackAnimator.enabled = true;
                    heroAttackAnimator.Rebind();
                    heroAttackAnimator.Update(0f);
                    heroAttackAnimator.speed = 0f;
                }
            }
            else if (connectorLine != null)
            {
                connectorLine.gameObject.SetActive(visible);
            }
        }

        internal void NotifyFlameDrakeFireballRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                FlameDrakeFireballReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifySkyborneValkyrieArrowRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                SkyborneValkyrieArrowReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyStarfallArchmageGemRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                StarfallArchmageGemReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyNightfangSkillAnimationEnd()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                NightfangSkillAnimationCompleted?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyWindclawSkillRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                WindclawSkillReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyEmberShamanFireballRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                EmberShamanFireballReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyRuneboltMageBoltRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                RuneboltMageBoltReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyStoneboundWarlockRockRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                StoneboundWarlockRockReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyThunderlordChainRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                ThunderlordChainReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyThunderlordSkillRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                ThunderlordSkillReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyAbyssalHarpoonRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                AbyssalHarpoonReleased?.Invoke(combatRuntimeId);
            }
        }

        internal void NotifyAbyssalHarpoonSkillRelease()
        {
            if (!string.IsNullOrEmpty(combatRuntimeId))
            {
                AbyssalHarpoonSkillReleased?.Invoke(combatRuntimeId);
            }
        }

        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (connectorLine != null && heroAttackAnimator == null)
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
            purpleFrameSprite = DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/HeroPurple");
            goldFrameSprite = DragonBound.Presentation.UiAssets.Load<Sprite>("GameUI/HeroGold");
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

    }

    [DisallowMultipleComponent]
    public sealed class HeroAnimationEventRelay : MonoBehaviour
    {
        private HeroFormationView owner;

        public void Bind(HeroFormationView value)
        {
            owner = value;
        }

        // Called by the authored Flame Drake Rider clip on its sixteenth frame.
        public void OnFlameDrakeFireballRelease()
        {
            owner?.NotifyFlameDrakeFireballRelease();
        }

        // Called by the authored Skyborne Valkyrie clip after its thirteenth frame.
        public void OnSkyborneValkyrieArrowRelease()
        {
            owner?.NotifySkyborneValkyrieArrowRelease();
        }

        // Called by both authored Starfall Archmage clips after their sixteenth frame.
        public void OnStarfallArchmageGemRelease()
        {
            owner?.NotifyStarfallArchmageGemRelease();
        }

        // Called after the authored Nightfang Assassin startup clip has fully played.
        public void OnNightfangSkillAnimationEnd()
        {
            owner?.NotifyNightfangSkillAnimationEnd();
        }

        // Called by the authored Windclaw Ranger skill clip after its tenth frame.
        public void OnWindclawSkillRelease()
        {
            owner?.NotifyWindclawSkillRelease();
        }

        // Called by the authored Ember Shaman clip after its thirteenth frame.
        public void OnEmberShamanFireballRelease()
        {
            owner?.NotifyEmberShamanFireballRelease();
        }

        // Called by the authored Runebolt Mage clip after its fourteenth frame.
        public void OnRuneboltMageBoltRelease()
        {
            owner?.NotifyRuneboltMageBoltRelease();
        }

        // Called by the authored Stonebound Warlock clip after its fourteenth frame.
        public void OnStoneboundWarlockRockRelease()
        {
            owner?.NotifyStoneboundWarlockRockRelease();
        }

        // Called by the authored Thunderlord clip after its tenth frame.
        public void OnThunderlordChainRelease()
        {
            owner?.NotifyThunderlordChainRelease();
        }

        // Called by the authored Thunderlord skill clip after its tenth frame.
        public void OnThunderlordSkillRelease()
        {
            owner?.NotifyThunderlordSkillRelease();
        }

        // Called by the authored Abyssal Harpooner attack clip after its sixteenth frame.
        public void OnAbyssalHarpoonRelease()
        {
            owner?.NotifyAbyssalHarpoonRelease();
        }

        // Called by the authored Abyssal Harpooner skill clip after its fifteenth frame.
        public void OnAbyssalHarpoonSkillRelease()
        {
            owner?.NotifyAbyssalHarpoonSkillRelease();
        }
    }

    /// <summary>
    /// Single presentation mapping shared by Main's WeaponPanel and in-run hero formations.
    /// Gameplay uses canonical runtime rune ids; the actual art remains replaceable under Resources/RuneUI.
    /// </summary>
    public static class RuneUiSpriteCatalog
    {
        private const string ResourcePrefix = "RuneUI/";

        private static readonly IReadOnlyDictionary<string, int> ResourceNumbers =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Might"] = 1,
                ["Farreach"] = 2,
                ["Power"] = 3,
                ["Longshot"] = 4,
                ["Frostbite"] = 5,
                ["Ricochet"] = 6,
                ["Volley"] = 7,
                ["BladeTempest"] = 8,
                ["Ambush"] = 9,
                ["Windhawk"] = 10,
                ["Skybreaker"] = 11,
                ["Wyrmguard"] = 12,
                ["Dragonbloom"] = 13,
                ["Warcry"] = 14
            };

        private static readonly Dictionary<string, Sprite> SpriteCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingSpriteWarnings =
            new HashSet<string>(StringComparer.Ordinal);

        public static string GetResourcePath(string runtimeRuneId)
        {
            if (string.IsNullOrWhiteSpace(runtimeRuneId) ||
                !ResourceNumbers.TryGetValue(runtimeRuneId.Trim(), out var resourceNumber))
            {
                return string.Empty;
            }

            return ResourcePrefix + resourceNumber;
        }

        public static Sprite Load(string runtimeRuneId)
        {
            var resourcePath = GetResourcePath(runtimeRuneId);
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (SpriteCache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var sprite = DragonBound.Presentation.UiAssets.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                SpriteCache[resourcePath] = sprite;
                return sprite;
            }

            if (MissingSpriteWarnings.Add(resourcePath))
            {
                Debug.LogWarning($"Rune UI sprite '{resourcePath}' is missing for rune '{runtimeRuneId}'.");
            }

            return null;
        }
    }

    /// <summary>
    /// Maps formal hero ids to the authored, non-looping UI animation controllers under Resources/Animation.
    /// </summary>
    public static class HeroAnimationControllerCatalog
    {
        private static readonly IReadOnlyDictionary<string, string> ResourcePaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DragonBoundHeroIds.WindclawRanger] = "Animation/Windclaw Ranger",
                [DragonBoundHeroIds.EmberShaman] = "Animation/Ember Shaman",
                [DragonBoundHeroIds.RuneboltMage] = "Animation/Runebolt Mage",
                [DragonBoundHeroIds.Stonebinder] = "Animation/Stonebound Warlock",
                [DragonBoundHeroIds.CrownSwordLeader] = "Animation/Oathcrown Blademaster",
                [DragonBoundHeroIds.CrownHunterLeader] = "Animation/Frostcrown Hunter",
                [DragonBoundHeroIds.DragonRider] = "Animation/Flame Drake Rider",
                [DragonBoundHeroIds.StarfallArchmage] = "Animation/Starfall Archmage",
                [DragonBoundHeroIds.ThunderJarl] = "Animation/Thunderlord",
                [DragonBoundHeroIds.NightfangAssassin] = "Animation/Nightfang Assassin",
                [DragonBoundHeroIds.LeviathanHunter] = "Animation/Abyssal Harpooner",
                [DragonBoundHeroIds.SkyhunterValkyrie] = "Animation/Skyborne Valkyrie"
            };

        private static readonly IReadOnlyDictionary<string, string> SkillResourcePaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DragonBoundHeroIds.DragonRider] = "Animation/Flame Drake Rider S",
                [DragonBoundHeroIds.StarfallArchmage] = "Animation/StarfallArchmageStartUP",
                [DragonBoundHeroIds.NightfangAssassin] = "Animation/NightfangAssassinStartUp",
                [DragonBoundHeroIds.WindclawRanger] = "Animation/Windclaw Ranger s",
                [DragonBoundHeroIds.ThunderJarl] = "Animation/ThunderlordStartUp",
                [DragonBoundHeroIds.LeviathanHunter] = "Animation/Abyssal HarpoonerStartUp"
            };

        private static readonly Dictionary<string, RuntimeAnimatorController> ControllerCache =
            new Dictionary<string, RuntimeAnimatorController>(StringComparer.Ordinal);
        private static readonly Dictionary<string, RuntimeAnimatorController> SkillControllerCache =
            new Dictionary<string, RuntimeAnimatorController>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingControllerWarnings =
            new HashSet<string>(StringComparer.Ordinal);

        public static string GetResourcePath(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId) ||
                !ResourcePaths.TryGetValue(heroId.Trim(), out var resourcePath))
            {
                return string.Empty;
            }

            return resourcePath;
        }

        public static RuntimeAnimatorController Load(string heroId)
        {
            var resourcePath = GetResourcePath(heroId);
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (ControllerCache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(resourcePath);
            if (controller == null)
            {
                var separatorIndex = resourcePath.LastIndexOf('/');
                var expectedName = (separatorIndex >= 0
                        ? resourcePath.Substring(separatorIndex + 1)
                        : resourcePath)
                    .Trim();
                foreach (var candidate in DragonBound.Presentation.UiAssets.LoadAll<RuntimeAnimatorController>("Animation"))
                {
                    if (candidate != null &&
                        string.Equals(candidate.name.Trim(), expectedName, StringComparison.Ordinal))
                    {
                        controller = candidate;
                        break;
                    }
                }
            }

            if (controller != null)
            {
                ControllerCache[resourcePath] = controller;
                return controller;
            }

            if (MissingControllerWarnings.Add(resourcePath))
            {
                Debug.LogWarning($"Hero animation controller '{resourcePath}' is missing for hero '{heroId}'.");
            }

            return null;
        }

        public static RuntimeAnimatorController LoadSkill(string heroId)
        {
            var resourcePath = GetSkillResourcePath(heroId);
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (!SkillControllerCache.TryGetValue(resourcePath, out var controller) || controller == null)
            {
                controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(resourcePath);
                SkillControllerCache[resourcePath] = controller;
            }

            if (controller == null && MissingControllerWarnings.Add(resourcePath))
            {
                Debug.LogWarning($"Hero skill animation controller is missing at Resources/{resourcePath}.");
            }

            return controller;
        }

        public static string GetSkillResourcePath(string heroId)
        {
            return !string.IsNullOrWhiteSpace(heroId) &&
                   SkillResourcePaths.TryGetValue(heroId.Trim(), out var resourcePath)
                ? resourcePath
                : string.Empty;
        }
    }

    /// <summary>  
    /// Maps formal hero ids to the authored Spine skeleton used by the V2 hero attack art. Keys  
    /// live under the active variant's Resources/Animations/Hero folder, so V1 keeps resolving  
    /// Animator controllers while V2 resolves Spine skeletons from the same hero id.  
    /// </summary>  
    public static class HeroSpineArtCatalog
    {
        private const string DefaultAttackAnimationName = "Attack";

        private static readonly IReadOnlyDictionary<string, string> SkeletonResourcePaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DragonBoundHeroIds.CrownSwordLeader] =
                    "Animations/Hero/Oathcrown Blademaster/normal/Spine/誓冠剑士 拆_SkeletonData",
                [DragonBoundHeroIds.WindclawRanger] =
                    "Animations/Hero/Windclaw Ranger/normal/Spine/WindclawRanger_SkeletonData",
                [DragonBoundHeroIds.RuneboltMage] =
                "Animations/Hero/Runebolt Mage/normal/Spine/Runebolt Mage_SkeletonData"

            };

        private static readonly Dictionary<string, SkeletonDataAsset> SkeletonCache =
            new Dictionary<string, SkeletonDataAsset>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingSkeletonWarnings =
            new HashSet<string>(StringComparer.Ordinal);

        public static string AttackAnimationName => DefaultAttackAnimationName;

        public static SkeletonDataAsset Load(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId) ||
                !SkeletonResourcePaths.TryGetValue(heroId.Trim(), out var resourcePath))
            {
                return null;
            }

            if (SkeletonCache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            // Probe silently: UiAssets.Load logs an error on a miss, and most heroes have no  
            // Spine art yet, so an unregistered key is an expected state rather than a failure.  
            var registry = UiAssets.Active;
            var skeleton = registry != null ? registry.Load<SkeletonDataAsset>(resourcePath) : null;
            if (skeleton == null && MissingSkeletonWarnings.Add(resourcePath))
            {
                Debug.LogWarning(
                    $"Hero Spine skeleton '{resourcePath}' is unavailable for hero '{heroId}'.");
            }

            SkeletonCache[resourcePath] = skeleton;
            return skeleton;
        }
    }


    /// <summary>
    /// Resolves the optional formation background authored below
    /// Resources/Hero/&lt;English hero name&gt;/ParticalBg.
    /// </summary>
    public static class HeroParticalBackgroundCatalog
    {
        private static readonly IReadOnlyDictionary<string, string> ResourceFolders =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DragonBoundHeroIds.WindclawRanger] = "Windclaw Ranger",
                [DragonBoundHeroIds.EmberShaman] = "Ember Shaman",
                [DragonBoundHeroIds.RuneboltMage] = "Runebolt Mage",
                [DragonBoundHeroIds.Stonebinder] = "Stonebound Warlock",
                [DragonBoundHeroIds.CrownSwordLeader] = "Oathcrown Blademaster",
                [DragonBoundHeroIds.CrownHunterLeader] = "Frostcrown Hunter",
                [DragonBoundHeroIds.DragonRider] = "Flame Drake Rider",
                [DragonBoundHeroIds.StarfallArchmage] = "Starfall Archmage",
                [DragonBoundHeroIds.ThunderJarl] = "Thunderlord",
                [DragonBoundHeroIds.NightfangAssassin] = "Nightfang Assassin",
                [DragonBoundHeroIds.LeviathanHunter] = "Abyssal Harpooner",
                [DragonBoundHeroIds.SkyhunterValkyrie] = "Skyborne Valkyrie"
            };

        private static readonly Dictionary<string, Sprite> SpriteCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static Sprite Load(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId) ||
                !ResourceFolders.TryGetValue(heroId.Trim(), out var heroFolder))
            {
                return null;
            }

            if (SpriteCache.TryGetValue(heroFolder, out var cached))
            {
                return cached;
            }

            var sprites = DragonBound.Presentation.UiAssets.LoadAll<Sprite>($"Hero/{heroFolder}/ParticalBg");
            var sprite = sprites != null && sprites.Length > 0 ? sprites[0] : null;
            if (sprite != null)
            {
                SpriteCache[heroFolder] = sprite;
            }
            return sprite;
        }
    }
}
