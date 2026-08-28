using System;
using System.Collections.Generic;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    // Authored as a prefab so final art can replace every greybox element in the Inspector.
    public sealed class HeroFormationView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image connectorLine;
        [SerializeField] private Image primaryFlash;
        [SerializeField] private Image secondaryFlash;
        [SerializeField] private Image doubleCellBorder;
        [SerializeField] private Text heroNameLabel;
        [SerializeField] private Image runeImage;
        [SerializeField] private Animator heroAttackAnimator;

        private Vector3 connectorScale = Vector3.one;
        private bool connectorScaleCaptured;
        private string configuredAnimationHeroId = string.Empty;
        private int observedAttackSequence;
        private bool attackSequenceObserved;
        private static Sprite purpleFrameSprite;
        private static Sprite goldFrameSprite;
        private static bool rarityFramesLoaded;

        public RectTransform RectTransform => (RectTransform)transform;
        public Image RuneImage => runeImage;
        public Animator HeroAttackAnimator => heroAttackAnimator;

        public void Configure(
            CanvasGroup group,
            Image line,
            Image firstFlash,
            Image secondFlash,
            Image border,
            Text nameLabel,
            Image equippedRuneImage = null,
            Animator attackAnimator = null)
        {
            canvasGroup = group;
            connectorLine = line;
            primaryFlash = firstFlash;
            secondaryFlash = secondFlash;
            doubleCellBorder = border;
            heroNameLabel = nameLabel;
            runeImage = equippedRuneImage;
            heroAttackAnimator = attackAnimator;
        }

        public void Initialize(
            Vector2 center,
            Vector2 primaryOffset,
            Vector2 secondaryOffset,
            Vector2 footprintSize,
            Vector2 cellSize,
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
            DisableRaycast(primaryFlash);
            DisableRaycast(secondaryFlash);
            DisableRaycast(doubleCellBorder);

            // HeroNameLabel is authored UI. Its text, layout, style, active state and
            // raycast setting intentionally remain exactly as configured in the prefab.

            PositionFlash(primaryFlash, primaryOffset, cellSize, rarityColor);
            PositionFlash(secondaryFlash, secondaryOffset, cellSize, rarityColor);
            if (doubleCellBorder != null)
            {
                ApplyRarityFrame(rarity, footprintSize, secondaryOffset - primaryOffset);
            }

            if (connectorLine != null)
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

            SetProgress(0f);
        }

        public void SetRune(string runtimeRuneId)
        {
            SetRuneSprite(RuneUiSpriteCatalog.Load(runtimeRuneId));
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
            if (heroAttackAnimator == null ||
                string.Equals(configuredAnimationHeroId, heroId, StringComparison.Ordinal))
            {
                return;
            }

            configuredAnimationHeroId = heroId ?? string.Empty;
            attackSequenceObserved = false;
            observedAttackSequence = 0;
            heroAttackAnimator.runtimeAnimatorController = HeroAnimationControllerCatalog.Load(heroId);
            if (heroAttackAnimator.runtimeAnimatorController == null)
            {
                heroAttackAnimator.enabled = false;
                return;
            }

            // Keep the authored first frame visible without autoplaying when the formation is created.
            heroAttackAnimator.enabled = true;
            heroAttackAnimator.Rebind();
            heroAttackAnimator.Update(0f);
            heroAttackAnimator.speed = 0f;
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
            if (heroAttackAnimator == null || heroAttackAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            heroAttackAnimator.enabled = true;
            heroAttackAnimator.speed = 1f;
            // State hash 0 restarts the current/default state at the authored first frame.
            heroAttackAnimator.Play(0, 0, 0f);
            heroAttackAnimator.Update(0f);
        }

        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            var flash = Mathf.Sin(progress * Mathf.PI);
            SetGraphicAlpha(primaryFlash, flash);
            SetGraphicAlpha(secondaryFlash, flash);
            if (connectorLine != null)
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
            purpleFrameSprite = Resources.Load<Sprite>("GameUI/HeroPurple");
            goldFrameSprite = Resources.Load<Sprite>("GameUI/HeroGold");
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

        private static void PositionFlash(Image flash, Vector2 position, Vector2 size, Color color)
        {
            if (flash == null)
            {
                return;
            }

            flash.color = color;
            flash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            flash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            flash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            flash.rectTransform.anchoredPosition = position;
            flash.rectTransform.sizeDelta = size;
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            var color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
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

            var sprite = Resources.Load<Sprite>(resourcePath);
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
                [DragonBoundHeroIds.DragonRider] = "Animation/Flame Drake Rider ",
                [DragonBoundHeroIds.StarfallArchmage] = "Animation/Starfall Archmage",
                [DragonBoundHeroIds.ThunderJarl] = "Animation/Thunderlord",
                [DragonBoundHeroIds.NightfangAssassin] = "Animation/Nightfang Assassin",
                [DragonBoundHeroIds.LeviathanHunter] = "Animation/Abyssal Harpooner",
                [DragonBoundHeroIds.SkyhunterValkyrie] = "Animation/Skyborne Valkyrie"
            };

        private static readonly Dictionary<string, RuntimeAnimatorController> ControllerCache =
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

            var controller = Resources.Load<RuntimeAnimatorController>(resourcePath);
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
    }
}
