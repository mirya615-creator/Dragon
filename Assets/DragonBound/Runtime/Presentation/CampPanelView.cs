using System;
using DragonBound.Presentation;
using System.Collections.Generic;
using System.Globalization;
using DragonBound.Combat;
using DragonBound.Recruitment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CampPanelView : MonoBehaviour
    {
        private const float SkillTextMaxWidth = 590f;
        private const float SelectedTabScaleMultiplier = 1.10f;
        private const string DeckSpritePath = "GameUI/CampUI/Deck";
        private const string DeckSelectedSpritePath = "GameUI/CampUI/DeckClick";
        private const string CollectionSpritePath = "GameUI/CampUI/Collection";
        private const string CollectionSelectedSpritePath = "GameUI/CampUI/CollectionClick";

        private static readonly string[] BasicUnitIds =
        {
            "basic.axe_raider",
            "basic.longbow_hunter",
            "basic.spear_raider",
            "basic.twinaxe_berserker"
        };

        [SerializeField] private CampArtCatalog artCatalog;

        private readonly List<UnitEntry> unitEntries = new List<UnitEntry>();
        private readonly List<ComponentEntry> componentEntries = new List<ComponentEntry>();
        private readonly List<HeroEntry> heroEntries = new List<HeroEntry>();

        private RecruitmentService recruitment;
        private BoardRecruitDestination destination;
        private ICampArtProvider artProvider;
        private Image firstComponentImage;
        private Image secondComponentImage;
        private TMP_Text heroNameText;
        private TMP_Text skillText;
        private Transform deckPart;
        private Transform collectionPart;
        private Button deckButton;
        private Button collectionButton;
        private Image deckButtonImage;
        private Image collectionButtonImage;
        private Sprite deckSprite;
        private Sprite deckSelectedSprite;
        private Sprite collectionSprite;
        private Sprite collectionSelectedSprite;
        private Vector3 deckButtonBaseScale;
        private Vector3 collectionButtonBaseScale;
        private string selectedHeroId;
        private bool initialized;

        public int UnitEntryCount => unitEntries.Count;
        public int ComponentEntryCount => componentEntries.Count;
        public int HeroEntryCount => heroEntries.Count;
        public string SelectedHeroId => selectedHeroId;

        public void Initialize(
            RecruitmentService recruitmentService,
            BoardRecruitDestination recruitDestination,
            ICampArtProvider provider = null)
        {
            if (initialized)
            {
                return;
            }

            recruitment = recruitmentService ?? throw new ArgumentNullException(nameof(recruitmentService));
            destination = recruitDestination ?? throw new ArgumentNullException(nameof(recruitDestination));
            artProvider = provider ??
                          (ICampArtProvider)artCatalog ??
                          ResourcesCampComponentArtProvider.Shared;

            ResolveUi();
            BuildUnitEntries();
            BuildComponentEntries();
            BuildHeroEntries();
            BindTabButtons();
            recruitment.Attempted += HandleRecruitmentAttempted;
            initialized = true;
            Refresh();
        }

        public void SetArtProvider(ICampArtProvider provider)
        {
            artProvider = provider ??
                          (ICampArtProvider)artCatalog ??
                          ResourcesCampComponentArtProvider.Shared;
            Refresh();
        }

        public void Refresh()
        {
            if (!initialized)
            {
                return;
            }

            RefreshUnits();
            RefreshComponents();
            RefreshHeroes();
            RefreshSelectedHero();
        }

        public void SelectHero(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return;
            }

            selectedHeroId = heroId;
            RefreshHeroes();
            RefreshSelectedHero();
        }

        public static string BuildHeroSummary(
            HeroRecipeDefinition recipe,
            string firstComponentName,
            string secondComponentName,
            string descriptionEn)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }
            if (string.IsNullOrWhiteSpace(firstComponentName) ||
                string.IsNullOrWhiteSpace(secondComponentName) ||
                string.IsNullOrWhiteSpace(descriptionEn))
            {
                throw new ArgumentException("Hero summary requires component names and an English description.");
            }
            if (recipe.FormationOrientation != HeroFormationOrientation.Horizontal)
            {
                throw new InvalidOperationException(
                    "Camp hero summaries only support horizontal hero formations.");
            }

            return "Left: " + firstComponentName +
                   "  Right: " + secondComponentName +
                   "\n" + descriptionEn.Trim();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (recruitment != null)
            {
                recruitment.Attempted -= HandleRecruitmentAttempted;
            }
            if (deckButton != null)
            {
                deckButton.onClick.RemoveListener(ShowDeck);
            }
            if (collectionButton != null)
            {
                collectionButton.onClick.RemoveListener(ShowCollection);
            }
        }

        private void HandleRecruitmentAttempted(RecruitmentAttempt attempt)
        {
            Refresh();
        }

        private void ResolveUi()
        {
            var campBg = Require(transform, "CampBg");
            deckPart = Require(campBg, "DeckPart");
            collectionPart = Require(campBg, "CollectionPart");

            unitEntries.Clear();
            componentEntries.Clear();
            heroEntries.Clear();

            unitContainer = Require(deckPart, "UnitContainer");
            componentContainer = Require(deckPart, "ComponentContainer");
            heroContainer = Require(collectionPart, "HeroContainer");
            firstComponentImage = RequireComponent<Image>(collectionPart, "Img1");
            secondComponentImage = RequireComponent<Image>(collectionPart, "Img2");
            heroNameText = FindHeroNameText(collectionPart);
            skillText = RequireComponent<TMP_Text>(collectionPart, "SkillText");

            skillText.enableWordWrapping = true;
            skillText.overflowMode = TextOverflowModes.Overflow;
            skillText.alignment = TextAlignmentOptions.TopLeft;
            var skillRect = skillText.rectTransform;
            var currentWidth = skillRect.rect.width;
            skillRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                currentWidth > 0f ? Mathf.Min(currentWidth, SkillTextMaxWidth) : SkillTextMaxWidth);
        }

        private void BindTabButtons()
        {
            var buttonRoot = Require(transform, "BtnImg");
            deckButton = RequireComponent<Button>(buttonRoot, "DeckBtn");
            collectionButton = RequireComponent<Button>(buttonRoot, "CollectionBtn");
            deckButtonImage = RequireButtonImage(deckButton);
            collectionButtonImage = RequireButtonImage(collectionButton);
            // Keep each button's position and click destination, but intentionally exchange
            // the Deck and Collection visual sets.
            deckSprite = RequireResourceSprite(CollectionSpritePath);
            deckSelectedSprite = RequireResourceSprite(CollectionSelectedSpritePath);
            collectionSprite = RequireResourceSprite(DeckSpritePath);
            collectionSelectedSprite = RequireResourceSprite(DeckSelectedSpritePath);
            deckButtonBaseScale = deckButton.transform.localScale;
            collectionButtonBaseScale = collectionButton.transform.localScale;

            deckButton.onClick.AddListener(ShowDeck);
            collectionButton.onClick.AddListener(ShowCollection);
            SetSelectedTab(deckPart.gameObject.activeSelf || !collectionPart.gameObject.activeSelf);
        }

        private void ShowDeck()
        {
            SetSelectedTab(true);
        }

        private void ShowCollection()
        {
            SetSelectedTab(false);
        }

        private void SetSelectedTab(bool deckSelected)
        {
            deckPart.gameObject.SetActive(deckSelected);
            collectionPart.gameObject.SetActive(!deckSelected);
            deckButtonImage.sprite = deckSelected ? deckSelectedSprite : deckSprite;
            collectionButtonImage.sprite = deckSelected ? collectionSprite : collectionSelectedSprite;
            deckButton.transform.localScale = deckButtonBaseScale *
                                              (deckSelected ? SelectedTabScaleMultiplier : 1f);
            collectionButton.transform.localScale = collectionButtonBaseScale *
                                                    (deckSelected ? 1f : SelectedTabScaleMultiplier);
        }

        private Transform unitContainer;
        private Transform componentContainer;
        private Transform heroContainer;

        private void BuildUnitEntries()
        {
            var slots = GetDirectChildren(unitContainer);
            RequireSlotCount("UnitContainer", slots.Count, BasicUnitIds.Length);
            for (var i = 0; i < BasicUnitIds.Length; i++)
            {
                var slot = slots[i];
                // The authored Unit slot keeps its own background art, so the runtime
                // artwork is written to the nested UIImage child instead. The older nested
                // Img lookup and the slot root itself stay as compatibility fallbacks.
                var image = FindDirectChildComponent<Image>(slot, "UIImage") ??
                            FindDirectChildComponent<Image>(slot, "Img") ??
                            slot.GetComponent<Image>();
                if (image == null)
                {
                    throw new InvalidOperationException(slot.name + " is missing an Image component.");
                }

                unitEntries.Add(new UnitEntry(BasicUnitIds[i], image));
            }
        }

        private void BuildComponentEntries()
        {
            var definitions = HeroComponentCatalog.Definitions;
            var slots = GetDirectChildren(componentContainer);
            RequireSlotCount("ComponentContainer", slots.Count, definitions.Count);

            for (var i = 0; i < definitions.Count; i++)
            {
                var slot = slots[i];
                slot.gameObject.SetActive(true);
                // Keep the root Image as the authored slot background. Component artwork is
                // rendered by the direct ComUI child so refreshing it cannot replace the
                // background or interfere with the Text (TMP) count overlay.
                var background = slot.GetComponent<Image>();
                var icon = FindDirectChildComponent<Image>(slot, "ComUI");
                var count = FindDirectChildComponent<TMP_Text>(slot, "Text (TMP)");
                if (background == null || icon == null || count == null)
                {
                    throw new InvalidOperationException(
                        slot.name +
                        " requires a root Image, child ComUI Image, and child Text (TMP).");
                }

                componentEntries.Add(new ComponentEntry(definitions[i], icon, count));
            }

            for (var i = definitions.Count; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        private void BuildHeroEntries()
        {
            var slots = GetDirectChildren(heroContainer);
            var visibleHeroes = new List<HeroDefinition>();
            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                if (HeroDefinitionCatalog.GetMetadata(hero.Id).GalleryVisible)
                {
                    visibleHeroes.Add(hero);
                }
            }

            RequireSlotCount("HeroContainer", slots.Count, visibleHeroes.Count);
            for (var i = 0; i < visibleHeroes.Count; i++)
            {
                var hero = visibleHeroes[i];
                var slotImage = slots[i].GetComponent<Image>();
                var heroImage = FindDirectChildComponent<Image>(slots[i], "HeroUI");
                if (slotImage == null || heroImage == null)
                {
                    throw new InvalidOperationException(
                        slots[i].name + " requires a root Image and child HeroUI Image.");
                }

                var button = slots[i].GetComponent<Button>();
                if (button == null)
                {
                    button = slots[i].gameObject.AddComponent<Button>();
                }
                button.targetGraphic = slotImage;
                var heroId = hero.Id;
                button.onClick.AddListener(() => SelectHero(heroId));
                heroEntries.Add(new HeroEntry(hero, heroImage, slotImage));
            }

            selectedHeroId = visibleHeroes.Count > 0 ? visibleHeroes[0].Id : string.Empty;
        }

        private void RefreshUnits()
        {
            foreach (var entry in unitEntries)
            {
                if (artProvider != null && artProvider.TryGetBasicUnitSprite(entry.UnitId, out var sprite))
                {
                    entry.Image.sprite = sprite;
                }
            }
        }

        private void RefreshComponents()
        {
            foreach (var entry in componentEntries)
            {
                var remaining = recruitment.GetRemainingHeroComponentCount(entry.Definition.Id);
                entry.Count.text = remaining.ToString(CultureInfo.InvariantCulture);
                var canStillAppear = recruitment.EnableHeroComponents && remaining > 0;
                if (artProvider != null && artProvider.TryGetHeroComponentSprite(entry.Definition.Id, out var sprite))
                {
                    entry.Image.sprite = sprite;
                    entry.Image.color = canStillAppear ? Color.white : GetExhaustedColor(Color.white);
                }
                else
                {
                    var availableColor = entry.AuthoredColor;
                    entry.Image.color = canStillAppear ? availableColor : GetExhaustedColor(availableColor);
                }
            }
        }

        private static Color GetExhaustedColor(Color availableColor)
        {
            const float brightness = 0.34f;
            return new Color(
                availableColor.r * brightness,
                availableColor.g * brightness,
                availableColor.b * brightness,
                availableColor.a);
        }

        private void RefreshHeroes()
        {
            foreach (var entry in heroEntries)
            {
                if (artProvider != null && artProvider.TryGetHeroSprite(entry.Definition.Id, out var sprite))
                {
                    entry.Image.sprite = sprite;
                    entry.Image.color = Color.white;
                }

                var outline = entry.SlotImage.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = entry.SlotImage.gameObject.AddComponent<Outline>();
                }
                outline.enabled = entry.Definition.Id == selectedHeroId;
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(3f, -3f);
            }
        }

        private void RefreshSelectedHero()
        {
            if (string.IsNullOrWhiteSpace(selectedHeroId))
            {
                return;
            }

            var hero = HeroDefinitionCatalog.Get(selectedHeroId);
            var recipe = HeroRecipeCatalog.Get(selectedHeroId);
            var metadata = HeroDefinitionCatalog.GetMetadata(selectedHeroId);
            var firstComponentId = GetFirstComponentId(recipe);
            var secondComponentId = GetSecondComponentId(recipe);
            heroNameText.text = hero.DisplayNameEn;
            skillText.text = BuildHeroSummary(
                recipe,
                HeroComponentCatalog.Get(firstComponentId).DisplayNameEn,
                HeroComponentCatalog.Get(secondComponentId).DisplayNameEn,
                metadata.DescriptionEn);
            ApplyComponentDetail(firstComponentImage, firstComponentId);
            ApplyComponentDetail(secondComponentImage, secondComponentId);
        }

        private void ApplyComponentDetail(Image image, string componentId)
        {
            var component = HeroComponentCatalog.Get(componentId);
            if (artProvider != null && artProvider.TryGetHeroComponentSprite(componentId, out var sprite))
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                image.color = GetComponentCategoryColor(component.Category);
            }
        }

        private static string GetFirstComponentId(HeroRecipeDefinition recipe)
        {
            return recipe.LeftComponentId;
        }

        private static string GetSecondComponentId(HeroRecipeDefinition recipe)
        {
            return recipe.RightComponentId;
        }

        private static Transform Require(Transform parent, string path)
        {
            var result = parent.FindUi(path);
            if (result == null)
            {
                throw new InvalidOperationException(parent.name + "/" + path + " is missing.");
            }
            return result;
        }

        private static T RequireComponent<T>(Transform parent, string path) where T : Component
        {
            var target = Require(parent, path);
            var result = target.GetComponent<T>();
            if (result == null)
            {
                throw new InvalidOperationException(target.name + " is missing " + typeof(T).Name + ".");
            }
            return result;
        }

        private static Image RequireButtonImage(Button button)
        {
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null)
            {
                throw new InvalidOperationException(button.name + " is missing an Image component.");
            }

            button.targetGraphic = image;
            return image;
        }

        private static Sprite RequireResourceSprite(string resourcePath)
        {
            var sprite = DragonBound.Presentation.UiAssets.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "Camp tab sprite is missing at Resources/" + resourcePath + ".png.");
            }

            return sprite;
        }

        private static T FindDirectChildComponent<T>(Transform parent, string childName)
            where T : Component
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName ||
                    child.name.StartsWith(childName + " (", StringComparison.Ordinal))
                {
                    var component = child.GetComponent<T>();
                    if (component != null)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static TMP_Text FindHeroNameText(Transform collectionPart)
        {
            TMP_Text best = null;
            var bestWidth = float.MinValue;
            for (var i = 0; i < collectionPart.childCount; i++)
            {
                var child = collectionPart.GetChild(i);
                if (child.name != "Text")
                {
                    continue;
                }

                var candidate = child.GetComponent<TMP_Text>();
                if (candidate != null && candidate.rectTransform.rect.width > bestWidth)
                {
                    best = candidate;
                    bestWidth = candidate.rectTransform.rect.width;
                }
            }

            if (best == null)
            {
                throw new InvalidOperationException("CollectionPart/Text TMP is missing.");
            }
            return best;
        }

        private static List<Transform> GetDirectChildren(Transform parent)
        {
            var result = new List<Transform>(parent.childCount);
            for (var i = 0; i < parent.childCount; i++)
            {
                result.Add(parent.GetChild(i));
            }
            return result;
        }

        private static void RequireSlotCount(string containerName, int actual, int expected)
        {
            if (actual < expected)
            {
                throw new InvalidOperationException(
                    containerName + " requires " + expected + " slots, but only " + actual + " were found.");
            }
        }

        private static Color GetComponentCategoryColor(HeroComponentCategory category)
        {
            switch (category)
            {
                case HeroComponentCategory.PublicCore:
                    return new Color(0.30f, 0.60f, 0.71f, 1f);
                case HeroComponentCategory.PurplePartner:
                    return new Color(0.61f, 0.42f, 0.87f, 1f);
                case HeroComponentCategory.SharedRouteGoldPartner:
                    return new Color(0.78f, 0.54f, 0.26f, 1f);
                default:
                    return new Color(0.90f, 0.70f, 0.24f, 1f);
            }
        }

        private sealed class UnitEntry
        {
            public UnitEntry(string unitId, Image image)
            {
                UnitId = unitId;
                Image = image;
            }

            public string UnitId { get; }
            public Image Image { get; }
        }

        private sealed class ComponentEntry
        {
            public ComponentEntry(HeroComponentDefinition definition, Image image, TMP_Text count)
            {
                Definition = definition;
                Image = image;
                Count = count;
                AuthoredColor = image.color;
            }

            public HeroComponentDefinition Definition { get; }
            public Image Image { get; }
            public TMP_Text Count { get; }
            public Color AuthoredColor { get; }
        }

        private sealed class HeroEntry
        {
            public HeroEntry(HeroDefinition definition, Image image, Image slotImage)
            {
                Definition = definition;
                Image = image;
                SlotImage = slotImage;
            }

            public HeroDefinition Definition { get; }
            public Image Image { get; }
            public Image SlotImage { get; }
        }
    }
}
