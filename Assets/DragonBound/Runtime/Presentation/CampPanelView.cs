using System;
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
            artProvider = provider ?? artCatalog;

            ResolveUi();
            BuildUnitEntries();
            BuildComponentEntries();
            BuildHeroEntries();
            recruitment.Attempted += HandleRecruitmentAttempted;
            initialized = true;
            Refresh();
        }

        public void SetArtProvider(ICampArtProvider provider)
        {
            artProvider = provider;
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

        public static string BuildSkillSummary(HeroRecipeDefinition recipe, SkillDefinition skill)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }
            if (skill == null)
            {
                throw new ArgumentNullException(nameof(skill));
            }

            var fields = new List<string>
            {
                "Formation: " + (recipe.FormationOrientation == HeroFormationOrientation.Horizontal ? "Line" : "Column"),
                "Skill: " + GetEnglishSkillName(skill),
                "Trigger: " + GetEnglishTriggerText(skill)
            };

            if (skill.DamageMultiplier > 0f)
            {
                fields.Add("Damage: x" + FormatNumber(skill.DamageMultiplier));
            }
            if (skill.Cooldown > 0f)
            {
                fields.Add("Cooldown: " + FormatNumber(skill.Cooldown) + "s");
            }
            if (skill.BaseStunDuration > 0f)
            {
                fields.Add("Stun: " + FormatNumber(skill.BaseStunDuration) + "s");
            }

            return string.Join("    ", fields);
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
        }

        private void HandleRecruitmentAttempted(RecruitmentAttempt attempt)
        {
            Refresh();
        }

        private void ResolveUi()
        {
            var campBg = Require(transform, "CampBg");
            var deckPart = Require(campBg, "DeckPart");
            var collectionPart = Require(campBg, "CollectionPart");

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
                var image = RequireComponent<Image>(slot, "Img");
                var label = slot.GetComponentInChildren<TMP_Text>(true);
                if (label == null)
                {
                    throw new InvalidOperationException(slot.name + " is missing Text (TMP).");
                }

                unitEntries.Add(new UnitEntry(BasicUnitIds[i], image, label));
            }
        }

        private void BuildComponentEntries()
        {
            var definitions = HeroComponentCatalog.Definitions;
            var slots = GetDirectChildren(componentContainer);
            RequireSlotCount("ComponentContainer", slots.Count, definitions.Count);
            var prefab = Resources.Load<GameObject>("prefabs/HeroDetail");
            if (prefab == null)
            {
                throw new InvalidOperationException("Resources/prefabs/HeroDetail.prefab is missing.");
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var slot = slots[i];
                slot.gameObject.SetActive(true);
                var detail = slot.Find("HeroDetail");
                if (detail == null)
                {
                    var instance = Instantiate(prefab, slot, false);
                    instance.name = "HeroDetail";
                    detail = instance.transform;
                }

                var rect = detail.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var icon = detail.GetComponent<Image>();
                var count = detail.Find("Count")?.GetComponent<TMP_Text>();
                if (icon == null || count == null)
                {
                    throw new InvalidOperationException("HeroDetail requires a root Image and Count TMP text.");
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
                var image = slots[i].GetComponent<Image>();
                if (image == null)
                {
                    throw new InvalidOperationException(slots[i].name + " is missing an Image component.");
                }

                var button = slots[i].GetComponent<Button>();
                if (button == null)
                {
                    button = slots[i].gameObject.AddComponent<Button>();
                }
                button.targetGraphic = image;
                var heroId = hero.Id;
                button.onClick.AddListener(() => SelectHero(heroId));
                heroEntries.Add(new HeroEntry(hero, image));
            }

            selectedHeroId = visibleHeroes.Count > 0 ? visibleHeroes[0].Id : string.Empty;
        }

        private void RefreshUnits()
        {
            foreach (var entry in unitEntries)
            {
                entry.Label.text = BasicUnitCatalog.GetDisplayName(entry.UnitId);
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
                var initial = recruitment.GetInitialHeroComponentCount(entry.Definition.Id);
                if (initial <= 0)
                {
                    initial = entry.Definition.CopiesPerRun;
                }

                entry.Count.text = remaining + "/" + initial;
                var canStillAppear = recruitment.EnableHeroComponents && remaining > 0;
                if (artProvider != null && artProvider.TryGetHeroComponentSprite(entry.Definition.Id, out var sprite))
                {
                    entry.Image.sprite = sprite;
                    entry.Image.color = canStillAppear ? Color.white : new Color(0.34f, 0.34f, 0.34f, 0.72f);
                }
                else
                {
                    entry.Image.color = canStillAppear
                        ? GetComponentCategoryColor(entry.Definition.Category)
                        : new Color(0.34f, 0.34f, 0.34f, 0.72f);
                }
            }
        }

        private void RefreshHeroes()
        {
            foreach (var entry in heroEntries)
            {
                var rarityColor = GetRarityColor(entry.Definition.Rarity);
                if (artProvider != null && artProvider.TryGetHeroSprite(entry.Definition.Id, out var sprite))
                {
                    entry.Image.sprite = sprite;
                    entry.Image.color = Color.white;
                }
                else
                {
                    entry.Image.color = rarityColor;
                }

                var outline = entry.Image.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = entry.Image.gameObject.AddComponent<Outline>();
                }
                outline.effectColor = entry.Definition.Id == selectedHeroId ? Color.white : rarityColor;
                outline.effectDistance = entry.Definition.Id == selectedHeroId
                    ? new Vector2(3f, -3f)
                    : new Vector2(1.5f, -1.5f);
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
            var skill = FrozenHeroConfigurationCatalog.GetSkill(hero.SkillId);
            heroNameText.text = hero.DisplayNameEn;
            skillText.text = BuildSkillSummary(recipe, skill);
            ApplyComponentDetail(firstComponentImage, GetFirstComponentId(recipe));
            ApplyComponentDetail(secondComponentImage, GetSecondComponentId(recipe));
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
            return recipe.FormationOrientation == HeroFormationOrientation.Horizontal
                ? recipe.LeftComponentId
                : recipe.TopComponentId;
        }

        private static string GetSecondComponentId(HeroRecipeDefinition recipe)
        {
            return recipe.FormationOrientation == HeroFormationOrientation.Horizontal
                ? recipe.RightComponentId
                : recipe.BottomComponentId;
        }

        private static string GetEnglishSkillName(SkillDefinition skill)
        {
            return string.IsNullOrWhiteSpace(skill.DisplayNameEn) ? skill.SkillId : skill.DisplayNameEn;
        }

        private static string GetEnglishTriggerText(SkillDefinition skill)
        {
            switch (skill.TriggerType)
            {
                case HeroSkillTriggerType.EveryNthAttack:
                    return "Every " + skill.TriggerCount + " attacks";
                case HeroSkillTriggerType.Cooldown:
                    return "Cooldown trigger";
                case HeroSkillTriggerType.OnHit:
                    return "On hit";
                case HeroSkillTriggerType.OnFirstAttack:
                    return "First attack";
                case HeroSkillTriggerType.OnSameTargetAttack:
                    return "Repeated attacks on the same target";
                case HeroSkillTriggerType.NormalAttack:
                    return "Normal attack";
                default:
                    return "Passive";
            }
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static Transform Require(Transform parent, string path)
        {
            var result = parent.Find(path);
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

        private static Color GetRarityColor(HeroRecipeRarity rarity)
        {
            return rarity == HeroRecipeRarity.Purple
                ? new Color(0.64f, 0.42f, 0.90f, 1f)
                : new Color(0.92f, 0.72f, 0.24f, 1f);
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
            public UnitEntry(string unitId, Image image, TMP_Text label)
            {
                UnitId = unitId;
                Image = image;
                Label = label;
            }

            public string UnitId { get; }
            public Image Image { get; }
            public TMP_Text Label { get; }
        }

        private sealed class ComponentEntry
        {
            public ComponentEntry(HeroComponentDefinition definition, Image image, TMP_Text count)
            {
                Definition = definition;
                Image = image;
                Count = count;
            }

            public HeroComponentDefinition Definition { get; }
            public Image Image { get; }
            public TMP_Text Count { get; }
        }

        private sealed class HeroEntry
        {
            public HeroEntry(HeroDefinition definition, Image image)
            {
                Definition = definition;
                Image = image;
            }

            public HeroDefinition Definition { get; }
            public Image Image { get; }
        }
    }
}
