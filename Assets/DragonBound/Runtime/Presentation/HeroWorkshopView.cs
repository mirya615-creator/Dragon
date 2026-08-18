using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    public sealed class HeroWorkshopView : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button componentLibraryTab;
        [SerializeField] private Button heroGalleryTab;
        [SerializeField] private GameObject componentLibraryPage;
        [SerializeField] private GameObject heroGalleryPage;
        [SerializeField] private Text runtimeModeLabel;
        [SerializeField] private Text heroCountLabel;
        [SerializeField] private Text detailNameLabel;
        [SerializeField] private Text detailRarityLabel;
        [SerializeField] private Text detailFormationLabel;
        [SerializeField] private Text detailSkillLabel;
        [SerializeField] private Transform componentGrid;
        [SerializeField] private Transform heroGrid;
        [SerializeField] private HeroWorkshopComponentEntryView componentEntryTemplate;
        [SerializeField] private HeroWorkshopGalleryEntryView heroEntryTemplate;

        private readonly Dictionary<string, HeroWorkshopComponentEntryView> componentEntries =
            new Dictionary<string, HeroWorkshopComponentEntryView>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroWorkshopGalleryEntryView> heroEntries =
            new Dictionary<string, HeroWorkshopGalleryEntryView>(StringComparer.Ordinal);

        private RecruitmentService recruitment;
        private BoardRecruitDestination destination;
        private string selectedHeroId;
        private bool initialized;
        private bool showingGallery;

        public int FormedHeroCount => destination != null ? destination.EverFormedHeroIds.Count : 0;
        public int GalleryEntryCount => heroEntries.Count;
        public int ComponentEntryCount => componentEntries.Count;

        public void Configure(
            Button close,
            Button componentsTab,
            Button galleryTab,
            GameObject componentsPage,
            GameObject galleryPage,
            Text mode,
            Text count,
            Text detailName,
            Text detailRarity,
            Text detailFormation,
            Text detailSkill,
            Transform componentsGrid,
            Transform heroesGrid,
            HeroWorkshopComponentEntryView componentTemplate,
            HeroWorkshopGalleryEntryView heroTemplate)
        {
            closeButton = close;
            componentLibraryTab = componentsTab;
            heroGalleryTab = galleryTab;
            componentLibraryPage = componentsPage;
            heroGalleryPage = galleryPage;
            runtimeModeLabel = mode;
            heroCountLabel = count;
            detailNameLabel = detailName;
            detailRarityLabel = detailRarity;
            detailFormationLabel = detailFormation;
            detailSkillLabel = detailSkill;
            componentGrid = componentsGrid;
            heroGrid = heroesGrid;
            componentEntryTemplate = componentTemplate;
            heroEntryTemplate = heroTemplate;
        }

        public void Initialize(RecruitmentService value, BoardRecruitDestination recruitDestination)
        {
            recruitment = value ?? throw new ArgumentNullException(nameof(value));
            destination = recruitDestination ?? throw new ArgumentNullException(nameof(recruitDestination));
            initialized = true;
            selectedHeroId = DragonBoundHeroIds.WindclawRanger;

            closeButton.onClick.AddListener(Close);
            componentLibraryTab.onClick.AddListener(ShowComponentLibrary);
            heroGalleryTab.onClick.AddListener(ShowHeroGallery);
            recruitment.Attempted += HandleRecruitmentAttempted;
            BuildEntries();
            SetTab(false);
            Refresh();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void ShowComponentLibrary()
        {
            SetTab(false);
        }

        public void ShowHeroGallery()
        {
            SetTab(true);
        }

        public void Refresh()
        {
            if (!initialized)
            {
                return;
            }

            RefreshHeader();
            RefreshComponents();
            RefreshGallery();
            RefreshHeroDetails();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (componentLibraryTab != null)
            {
                componentLibraryTab.onClick.RemoveListener(ShowComponentLibrary);
            }

            if (heroGalleryTab != null)
            {
                heroGalleryTab.onClick.RemoveListener(ShowHeroGallery);
            }

            if (recruitment != null)
            {
                recruitment.Attempted -= HandleRecruitmentAttempted;
            }
        }

        private void HandleRecruitmentAttempted(RecruitmentAttempt attempt)
        {
            Refresh();
        }

        private void BuildEntries()
        {
            if (componentEntryTemplate == null || heroEntryTemplate == null)
            {
                throw new InvalidOperationException("Hero workshop entry templates must be assigned on the editable prefab.");
            }

            foreach (var component in HeroComponentCatalog.Definitions)
            {
                var entry = Instantiate(componentEntryTemplate, componentGrid);
                entry.name = component.ArtSlotId;
                entry.gameObject.SetActive(true);
                componentEntries.Add(component.Id, entry);
            }

            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                if (!HeroDefinitionCatalog.GetMetadata(hero.Id).GalleryVisible)
                {
                    continue;
                }

                var entry = Instantiate(heroEntryTemplate, heroGrid);
                entry.name = HeroDefinitionCatalog.GetMetadata(hero.Id).ArtSlotId;
                entry.gameObject.SetActive(true);
                heroEntries.Add(hero.Id, entry);
            }
        }

        private void SetTab(bool gallery)
        {
            showingGallery = gallery;
            componentLibraryPage.SetActive(!gallery);
            heroGalleryPage.SetActive(gallery);
            var componentTabImage = componentLibraryTab != null ? componentLibraryTab.GetComponent<Image>() : null;
            var galleryTabImage = heroGalleryTab != null ? heroGalleryTab.GetComponent<Image>() : null;
            if (componentTabImage != null)
            {
                componentTabImage.color = gallery
                    ? new Color(0.28f, 0.25f, 0.22f, 1f)
                    : new Color(0.78f, 0.36f, 0.14f, 1f);
            }

            if (galleryTabImage != null)
            {
                galleryTabImage.color = gallery
                    ? new Color(0.78f, 0.36f, 0.14f, 1f)
                    : new Color(0.28f, 0.25f, 0.22f, 1f);
            }

            if (runtimeModeLabel != null)
            {
                runtimeModeLabel.gameObject.SetActive(!gallery);
            }
            Refresh();
        }

        private void RefreshHeader()
        {
            if (runtimeModeLabel != null)
            {
                runtimeModeLabel.text = GetRuntimeModeText();
            }

            if (heroCountLabel != null)
            {
                heroCountLabel.text = $"已合成英雄 {FormedHeroCount} / 12";
            }
        }

        private void RefreshComponents()
        {
            foreach (var component in HeroComponentCatalog.Definitions)
            {
                if (!componentEntries.TryGetValue(component.Id, out var entry))
                {
                    continue;
                }

                var currentCount = destination.GetCurrentHeroComponentCount(component.Id);
                var remainingCount = recruitment.GetRemainingHeroComponentCount(component.Id);
                var state = GetComponentState(component, currentCount, remainingCount, out var color);
                var initialCount = recruitment.GetInitialHeroComponentCount(component.Id);
                if (initialCount <= 0)
                {
                    initialCount = component.CopiesPerRun;
                }

                var count = $"{remainingCount}/{initialCount}";
                entry.SetData(component.Id, component.DisplayNameZh, count, state, color);
            }
        }

        private void RefreshGallery()
        {
            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                var metadata = HeroDefinitionCatalog.GetMetadata(hero.Id);
                if (!metadata.GalleryVisible)
                {
                    continue;
                }

                if (!heroEntries.TryGetValue(hero.Id, out var entry))
                {
                    continue;
                }

                var active = destination.HasActiveHero(hero.Id);
                var formed = destination.HasEverFormedHero(hero.Id);
                var status = active ? "上阵" : formed ? "已合成" : "未合成";
                var color = active || formed ? GetRarityColor(hero.Rarity) : new Color(0.20f, 0.22f, 0.24f, 0.96f);
                entry.SetData(
                    hero.Id,
                    GetHeroDisplayName(hero, metadata),
                    status,
                    color,
                    hero.Id == selectedHeroId,
                    SelectHero);
            }
        }

        private void SelectHero(string heroId)
        {
            selectedHeroId = heroId;
            RefreshGallery();
            RefreshHeroDetails();
        }

        private void RefreshHeroDetails()
        {
            if (string.IsNullOrWhiteSpace(selectedHeroId))
            {
                return;
            }

            var hero = HeroDefinitionCatalog.Get(selectedHeroId);
            var recipe = HeroRecipeCatalog.Get(selectedHeroId);
            var skill = FrozenHeroConfigurationCatalog.GetSkill(hero.SkillId);
            var metadata = HeroDefinitionCatalog.GetMetadata(hero.Id);
            if (detailNameLabel != null)
            {
                detailNameLabel.text = GetHeroDisplayName(hero, metadata);
            }

            if (detailRarityLabel != null)
            {
                detailRarityLabel.text = hero.Rarity == HeroRecipeRarity.Purple ? "紫色" : "金色";
                detailRarityLabel.color = GetRarityColor(hero.Rarity);
            }

            if (detailFormationLabel != null)
            {
                detailFormationLabel.text = FormatFormation(recipe);
            }

            if (detailSkillLabel != null)
            {
                detailSkillLabel.text = FormatSkill(skill);
            }
        }

        private string GetRuntimeModeText()
        {
            if (!recruitment.EnableHeroComponents)
            {
                return "英雄组件已关闭";
            }

            return $"配置 {recruitment.InitialHeroComponents}  |  牌袋 {recruitment.RemainingHeroComponents}/{recruitment.InitialHeroComponents}  |  已抽 {recruitment.DrawnHeroComponents}  |  已丢弃 {recruitment.DiscardedHeroComponents}";
        }

        private string GetComponentState(
            HeroComponentDefinition component,
            int currentCount,
            int remainingCount,
            out Color color)
        {
            if (!recruitment.EnableHeroComponents)
            {
                color = new Color(0.31f, 0.33f, 0.35f, 0.90f);
                return "组件未启用";
            }

            if (recruitment.HeroSliceMode &&
                !HeroSliceRecruitmentConfig.TryGetComponent(component.Id, out _))
            {
                color = new Color(0.25f, 0.30f, 0.34f, 0.90f);
                return "仅配置";
            }

            if (currentCount > 0)
            {
                color = GetCategoryColor(component.Category);
                return "当前存在";
            }

            if (recruitment.WasHeroComponentDiscarded(component.Id))
            {
                color = new Color(0.44f, 0.27f, 0.27f, 0.90f);
                return "刷新丢失";
            }

            if (!recruitment.HasHeroComponentAppeared(component.Id))
            {
                color = new Color(0.34f, 0.36f, 0.39f, 0.90f);
                return "尚未出现";
            }

            if (remainingCount == 0)
            {
                color = new Color(0.27f, 0.29f, 0.31f, 0.90f);
                return "牌袋耗尽";
            }

            color = new Color(0.40f, 0.43f, 0.46f, 0.90f);
            return "已经出现";
        }

        private static string FormatFormation(HeroRecipeDefinition recipe)
        {
            if (recipe.FormationOrientation == HeroFormationOrientation.Vertical)
            {
                return GetComponentName(recipe.TopComponentId) + "  (上)\n" +
                       "        |\n" +
                       GetComponentName(recipe.BottomComponentId) + "  (下)";
            }

            return GetComponentName(recipe.LeftComponentId) + "  (左)   ->   " +
                   GetComponentName(recipe.RightComponentId) + "  (右)";
        }

        private static string FormatSkill(SkillDefinition skill)
        {
            var displayName = string.IsNullOrWhiteSpace(skill.DisplayNameZh)
                ? skill.DisplayNameEn
                : skill.DisplayNameZh;
            var result = displayName + "\n" + GetTriggerText(skill);
            if (skill.DamageMultiplier > 0f)
            {
                result += $"，伤害 x{skill.DamageMultiplier:0.##}";
            }

            if (skill.Cooldown > 0f)
            {
                result += $"，冷却 {skill.Cooldown:0.##} 秒";
            }

            if (skill.BaseStunDuration > 0f)
            {
                result += $"，眩晕 {skill.BaseStunDuration:0.##} 秒";
            }

            return result;
        }

        private static string GetTriggerText(SkillDefinition skill)
        {
            switch (skill.TriggerType)
            {
                case HeroSkillTriggerType.EveryNthAttack:
                    return $"每 {skill.TriggerCount} 次攻击触发";
                case HeroSkillTriggerType.Cooldown:
                    return "按冷却触发";
                case HeroSkillTriggerType.OnHit:
                    return "命中时触发";
                case HeroSkillTriggerType.OnFirstAttack:
                    return "首次攻击触发";
                case HeroSkillTriggerType.OnSameTargetAttack:
                    return "持续攻击同一目标时触发";
                case HeroSkillTriggerType.NormalAttack:
                    return "普通攻击效果";
                default:
                    return "被动效果";
            }
        }

        private static string GetComponentName(string componentId)
        {
            return HeroComponentCatalog.Get(componentId).DisplayNameZh;
        }

        private static string GetHeroDisplayName(HeroDefinition hero, HeroCatalogMetadata metadata)
        {
            // Pending names still have explicit temporary display labels for the slice.
            return hero.DisplayNameZh;
        }

        private static Color GetRarityColor(HeroRecipeRarity rarity)
        {
            return rarity == HeroRecipeRarity.Purple
                ? new Color(0.64f, 0.42f, 0.90f, 0.98f)
                : new Color(0.92f, 0.72f, 0.24f, 0.98f);
        }

        private static Color GetCategoryColor(HeroComponentCategory category)
        {
            switch (category)
            {
                case HeroComponentCategory.PublicCore:
                    return new Color(0.30f, 0.60f, 0.71f, 0.98f);
                case HeroComponentCategory.PurplePartner:
                    return new Color(0.61f, 0.42f, 0.87f, 0.98f);
                case HeroComponentCategory.SharedRouteGoldPartner:
                    return new Color(0.78f, 0.54f, 0.26f, 0.98f);
                default:
                    return new Color(0.90f, 0.70f, 0.24f, 0.98f);
            }
        }
    }
}
