using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Grid;

namespace DragonBound.Recruitment
{
    public enum RecruitItemKind
    {
        BasicUnit,
        HeroComponent,
        Shovel
    }

    public enum HeroRecipeRarity
    {
        Purple,
        Gold
    }

    public enum HeroComponentPool
    {
        Shared,
        Purple,
        Gold
    }

    public enum HeroComponentCategory
    {
        PublicCore,
        PurplePartner,
        SharedRouteGoldPartner,
        DedicatedGold
    }

    public enum HeroFormationOrientation
    {
        Vertical,
        Horizontal
    }

    public enum HeroComponentRecipeRole
    {
        DragonCore,
        Focus,
        Crown,
        Person,
        Weapon
    }

    // Presentation names can remain intentionally unresolved while the gameplay identity is frozen.
    public enum HeroNameFreezeState
    {
        Frozen,
        Pending
    }

    // Catalog entries may exist before their combat implementation is enabled.
    public enum HeroRuntimeCombatState
    {
        NotImplemented,
        Implemented
    }

    public sealed class HeroComponentDefinition
    {
        public HeroComponentDefinition(string id, HeroComponentPool pool, int copiesPerRun, bool isUnique)
            : this(
                id,
                id,
                id,
                pool == HeroComponentPool.Shared
                    ? HeroComponentCategory.PublicCore
                    : pool == HeroComponentPool.Purple
                        ? HeroComponentCategory.PurplePartner
                        : HeroComponentCategory.DedicatedGold,
                copiesPerRun,
                isUnique,
                new string[0],
                id)
        {
        }

        public HeroComponentDefinition(
            string id,
            string displayNameZh,
            string displayNameEn,
            HeroComponentCategory category,
            int copiesPerRun,
            bool isUnique,
            IReadOnlyList<string> compatibleHeroIds,
            string iconKey,
            string artSlotId = null)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(displayNameZh) ||
                string.IsNullOrWhiteSpace(displayNameEn))
            {
                throw new ArgumentException("A component id and both display names are required.");
            }

            if (copiesPerRun < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(copiesPerRun));
            }

            Id = id;
            DisplayNameZh = displayNameZh;
            DisplayNameEn = displayNameEn;
            Category = category;
            Pool = GetPool(category);
            CopiesPerRun = copiesPerRun;
            IsUnique = isUnique;
            CompatibleHeroIds = compatibleHeroIds ?? throw new ArgumentNullException(nameof(compatibleHeroIds));
            IconKey = iconKey ?? string.Empty;
            ArtSlotId = string.IsNullOrWhiteSpace(artSlotId) ? IconKey : artSlotId;
        }

        public string Id { get; }
        public string CanonicalId => Id;
        public string DisplayNameZh { get; }
        public string DisplayNameEn { get; }
        public string DisplayNameKey => "component." + Id;
        public HeroComponentCategory Category { get; }
        public HeroComponentPool Pool { get; }
        public int CopiesPerRun { get; }
        public bool IsUnique { get; }
        public bool IsPublicCore => Category == HeroComponentCategory.PublicCore;
        public HeroComponentRecipeRole RecipeRole => GetRecipeRole(Id);
        public IReadOnlyList<string> CompatibleHeroIds { get; }
        public string IconKey { get; }
        public string ArtSlotId { get; }

        public IReadOnlyList<string> CompatibleRecipeIds => GetCompatibleRecipeIds();
        public IReadOnlyList<string> LegacyAliases => DragonBoundLegacyAliases.GetComponentAliases(Id);

        private static HeroComponentPool GetPool(HeroComponentCategory category)
        {
            switch (category)
            {
                case HeroComponentCategory.PublicCore:
                    return HeroComponentPool.Shared;
                case HeroComponentCategory.PurplePartner:
                    return HeroComponentPool.Purple;
                default:
                    return HeroComponentPool.Gold;
            }
        }

        private static HeroComponentRecipeRole GetRecipeRole(string componentId)
        {
            if (string.Equals(componentId, DragonBoundComponentIds.ContractHatchling, StringComparison.Ordinal))
            {
                return HeroComponentRecipeRole.DragonCore;
            }

            if (string.Equals(componentId, DragonBoundComponentIds.RuneStaff, StringComparison.Ordinal))
            {
                return HeroComponentRecipeRole.Focus;
            }

            if (string.Equals(componentId, DragonBoundComponentIds.AncestralWarCrown, StringComparison.Ordinal))
            {
                return HeroComponentRecipeRole.Crown;
            }

            return string.Equals(componentId, DragonBoundComponentIds.RuneDagger, StringComparison.Ordinal) ||
                   string.Equals(componentId, DragonBoundComponentIds.AncientHarpoon, StringComparison.Ordinal) ||
                   string.Equals(componentId, DragonBoundComponentIds.DragonboneLongbow, StringComparison.Ordinal)
                ? HeroComponentRecipeRole.Weapon
                : HeroComponentRecipeRole.Person;
        }

        private IReadOnlyList<string> GetCompatibleRecipeIds()
        {
            var result = new List<string>(CompatibleHeroIds.Count);
            foreach (var heroId in CompatibleHeroIds)
            {
                result.Add(FrozenHeroConfigurationCatalog.GetRecipe(heroId).RecipeId);
            }

            return result.AsReadOnly();
        }
    }

    public sealed class HeroRecipeDefinition
    {
        public HeroRecipeDefinition(
            string heroId,
            HeroRecipeRarity rarity,
            HeroFormationOrientation formationOrientation,
            string topComponentId,
            string bottomComponentId,
            string leftComponentId,
            string rightComponentId,
            string formationPrefabId,
            string progressOwnerComponentId)
            : this(
                heroId,
                heroId,
                rarity,
                formationOrientation,
                topComponentId,
                bottomComponentId,
                leftComponentId,
                rightComponentId,
                formationPrefabId,
                progressOwnerComponentId)
        {
        }

        public HeroRecipeDefinition(
            string recipeId,
            string heroId,
            HeroRecipeRarity rarity,
            HeroFormationOrientation formationOrientation,
            string topComponentId,
            string bottomComponentId,
            string leftComponentId,
            string rightComponentId,
            string formationPrefabId,
            string progressOwnerComponentId)
        {
            if (string.IsNullOrWhiteSpace(recipeId) ||
                string.IsNullOrWhiteSpace(heroId) ||
                string.IsNullOrWhiteSpace(formationPrefabId))
            {
                throw new ArgumentException("A hero recipe requires an id and formation prefab id.");
            }

            RecipeId = recipeId;
            HeroId = heroId;
            Rarity = rarity;
            FormationOrientation = formationOrientation;
            TopComponentId = topComponentId;
            BottomComponentId = bottomComponentId;
            LeftComponentId = leftComponentId;
            RightComponentId = rightComponentId;
            FormationPrefabId = formationPrefabId;
            ProgressOwnerComponentId = progressOwnerComponentId;

            switch (FormationOrientation)
            {
                case HeroFormationOrientation.Vertical:
                    if (string.IsNullOrWhiteSpace(TopComponentId) ||
                        string.IsNullOrWhiteSpace(BottomComponentId) ||
                        !string.IsNullOrWhiteSpace(LeftComponentId) ||
                        !string.IsNullOrWhiteSpace(RightComponentId) ||
                        string.Equals(TopComponentId, BottomComponentId, StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "A vertical hero recipe requires two different top and bottom component ids.");
                    }

                    ComponentAId = TopComponentId;
                    ComponentBId = BottomComponentId;
                    break;
                case HeroFormationOrientation.Horizontal:
                    if (string.IsNullOrWhiteSpace(LeftComponentId) ||
                        string.IsNullOrWhiteSpace(RightComponentId) ||
                        !string.IsNullOrWhiteSpace(TopComponentId) ||
                        !string.IsNullOrWhiteSpace(BottomComponentId) ||
                        string.Equals(LeftComponentId, RightComponentId, StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "A horizontal hero recipe requires two different left and right component ids.");
                    }

                    ComponentAId = LeftComponentId;
                    ComponentBId = RightComponentId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(formationOrientation));
            }
        }

        public string RecipeId { get; }
        public string HeroId { get; }
        public HeroRecipeRarity Rarity { get; }
        public HeroFormationOrientation FormationOrientation { get; }
        public string TopComponentId { get; }
        public string BottomComponentId { get; }
        public string LeftComponentId { get; }
        public string RightComponentId { get; }
        public string FormationPrefabId { get; }
        public string ComponentAId { get; }
        public string ComponentBId { get; }
        public string ProgressOwnerComponentId { get; }
        public IReadOnlyList<string> RequiredComponentIds =>
            Array.AsReadOnly(new[] { ComponentAId, ComponentBId });
        public HeroComponentRecipeRole ProgressOwnerRole => HeroComponentRecipeRole.Person;
        public string FormationRule => FormationOrientation == HeroFormationOrientation.Vertical
            ? "VerticalTopAboveBottom"
            : "HorizontalLeftBeforeRight";
        public bool IsEnabledForRecipeMatching => true;
        public bool IsEnabledForRecruitment =>
            HeroDefinitionCatalog.GetMetadata(HeroId).RuntimeCombatState == HeroRuntimeCombatState.Implemented;
        public bool IsEnabledForCombat =>
            HeroDefinitionCatalog.GetMetadata(HeroId).RuntimeCombatState == HeroRuntimeCombatState.Implemented;

        public bool Matches(string firstComponentId, string secondComponentId)
        {
            return (string.Equals(ComponentAId, firstComponentId, StringComparison.Ordinal) &&
                    string.Equals(ComponentBId, secondComponentId, StringComparison.Ordinal)) ||
                   (string.Equals(ComponentAId, secondComponentId, StringComparison.Ordinal) &&
                    string.Equals(ComponentBId, firstComponentId, StringComparison.Ordinal));
        }

        public bool MatchesFormation(
            string firstComponentId,
            GridPosition firstPosition,
            string secondComponentId,
            GridPosition secondPosition)
        {
            switch (FormationOrientation)
            {
                case HeroFormationOrientation.Vertical:
                    return firstPosition.X == secondPosition.X &&
                           Math.Abs(firstPosition.Y - secondPosition.Y) == 1 &&
                           ((string.Equals(firstComponentId, TopComponentId, StringComparison.Ordinal) &&
                             string.Equals(secondComponentId, BottomComponentId, StringComparison.Ordinal) &&
                             firstPosition.Y > secondPosition.Y) ||
                            (string.Equals(secondComponentId, TopComponentId, StringComparison.Ordinal) &&
                             string.Equals(firstComponentId, BottomComponentId, StringComparison.Ordinal) &&
                             secondPosition.Y > firstPosition.Y));
                case HeroFormationOrientation.Horizontal:
                    return firstPosition.Y == secondPosition.Y &&
                           Math.Abs(firstPosition.X - secondPosition.X) == 1 &&
                           ((string.Equals(firstComponentId, LeftComponentId, StringComparison.Ordinal) &&
                             string.Equals(secondComponentId, RightComponentId, StringComparison.Ordinal) &&
                             firstPosition.X < secondPosition.X) ||
                            (string.Equals(secondComponentId, LeftComponentId, StringComparison.Ordinal) &&
                             string.Equals(firstComponentId, RightComponentId, StringComparison.Ordinal) &&
                             secondPosition.X < firstPosition.X));
                default:
                    return false;
            }
        }

        public bool TryGetRequiredPositionForComponent(
            string fixedComponentId,
            GridPosition fixedPosition,
            string movingComponentId,
            out GridPosition requiredPosition)
        {
            requiredPosition = default;
            switch (FormationOrientation)
            {
                case HeroFormationOrientation.Vertical:
                    if (string.Equals(fixedComponentId, TopComponentId, StringComparison.Ordinal) &&
                        string.Equals(movingComponentId, BottomComponentId, StringComparison.Ordinal))
                    {
                        requiredPosition = new GridPosition(fixedPosition.X, fixedPosition.Y - 1);
                        return true;
                    }

                    if (string.Equals(fixedComponentId, BottomComponentId, StringComparison.Ordinal) &&
                        string.Equals(movingComponentId, TopComponentId, StringComparison.Ordinal))
                    {
                        requiredPosition = new GridPosition(fixedPosition.X, fixedPosition.Y + 1);
                        return true;
                    }

                    return false;
                case HeroFormationOrientation.Horizontal:
                    if (string.Equals(fixedComponentId, LeftComponentId, StringComparison.Ordinal) &&
                        string.Equals(movingComponentId, RightComponentId, StringComparison.Ordinal))
                    {
                        requiredPosition = new GridPosition(fixedPosition.X + 1, fixedPosition.Y);
                        return true;
                    }

                    if (string.Equals(fixedComponentId, RightComponentId, StringComparison.Ordinal) &&
                        string.Equals(movingComponentId, LeftComponentId, StringComparison.Ordinal))
                    {
                        requiredPosition = new GridPosition(fixedPosition.X - 1, fixedPosition.Y);
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }
    }

    public sealed class HeroComponentInstanceDefinition
    {
        public HeroComponentInstanceDefinition(string instanceId, string componentId, int copyNumber)
        {
            if (string.IsNullOrWhiteSpace(instanceId) ||
                string.IsNullOrWhiteSpace(componentId) ||
                copyNumber < 1)
            {
                throw new ArgumentException("A component instance requires ids and a positive copy number.");
            }

            InstanceId = instanceId;
            ComponentId = componentId;
            CopyNumber = copyNumber;
        }

        public string InstanceId { get; }
        public string ComponentId { get; }
        public string ComponentInstanceId => InstanceId;
        public string ComponentDefinitionId => ComponentId;
        public int CopyNumber { get; }
    }

    // This metadata is deliberately separate from combat values. It lets content and UI expose the
    // full frozen catalog without accidentally enabling combat for entries outside the current slice.
    public sealed class HeroCatalogMetadata
    {
        public HeroCatalogMetadata(
            string heroId,
            string recipeId,
            HeroNameFreezeState nameFreezeState,
            HeroRuntimeCombatState runtimeCombatState,
            bool galleryVisible,
            string artSlotId,
            string descriptionEn)
        {
            if (string.IsNullOrWhiteSpace(heroId) ||
                string.IsNullOrWhiteSpace(recipeId) ||
                string.IsNullOrWhiteSpace(artSlotId) ||
                string.IsNullOrWhiteSpace(descriptionEn))
            {
                throw new ArgumentException(
                    "Hero catalog metadata requires hero, recipe, art slot ids, and an English description.");
            }

            HeroId = heroId;
            RecipeId = recipeId;
            NameFreezeState = nameFreezeState;
            RuntimeCombatState = runtimeCombatState;
            GalleryVisible = galleryVisible;
            ArtSlotId = artSlotId;
            DescriptionEn = descriptionEn;
        }

        public string HeroId { get; }
        public string RecipeId { get; }
        public HeroNameFreezeState NameFreezeState { get; }
        public HeroRuntimeCombatState RuntimeCombatState { get; }
        public bool GalleryVisible { get; }
        public string ArtSlotId { get; }
        public string DescriptionEn { get; }
    }

    public sealed class RecruitmentCatalog
    {
        private readonly Dictionary<string, HeroComponentDefinition> componentById;
        private readonly Dictionary<string, HeroRecipeDefinition> recipeByHeroId;
        private readonly Dictionary<string, HeroRecipeDefinition> recipeById;

        public RecruitmentCatalog(
            IReadOnlyList<string> basicUnitIds,
            IReadOnlyList<HeroComponentDefinition> components,
            IReadOnlyList<HeroRecipeDefinition> recipes,
            IReadOnlyList<HeroComponentInstanceDefinition> componentBagTemplate = null)
        {
            BasicUnitIds = basicUnitIds ?? throw new ArgumentNullException(nameof(basicUnitIds));
            Components = components ?? throw new ArgumentNullException(nameof(components));
            Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));

            if (BasicUnitIds.Count == 0 || Components.Count == 0 || Recipes.Count == 0)
            {
                throw new ArgumentException("Recruitment catalog sections cannot be empty.");
            }

            var basicIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var basicUnitId in BasicUnitIds)
            {
                if (string.IsNullOrWhiteSpace(basicUnitId) || !basicIds.Add(basicUnitId))
                {
                    throw new ArgumentException("Basic unit ids must be non-empty and unique.", nameof(basicUnitIds));
                }
            }

            componentById = new Dictionary<string, HeroComponentDefinition>(StringComparer.Ordinal);
            var totalCopies = 0;
            foreach (var component in Components)
            {
                if (component == null || componentById.ContainsKey(component.Id))
                {
                    throw new ArgumentException("Hero component ids must be unique.", nameof(components));
                }

                componentById.Add(component.Id, component);

                if (component.IsUnique && component.CopiesPerRun != 1)
                {
                    throw new ArgumentException($"Unique component {component.Id} must have one copy.", nameof(components));
                }

                totalCopies += component.CopiesPerRun;
            }

            if (totalCopies != 24)
            {
                throw new ArgumentException("The greybox hero pool must contain exactly 24 component copies.", nameof(components));
            }

            ComponentBagTemplate = componentBagTemplate ?? BuildBagTemplate(Components);
            if (ComponentBagTemplate.Count != totalCopies)
            {
                throw new ArgumentException("The component bag template must match the configured copy total.", nameof(componentBagTemplate));
            }

            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            var instanceCountByComponent = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var instance in ComponentBagTemplate)
            {
                if (instance == null ||
                    !instanceIds.Add(instance.InstanceId) ||
                    !componentById.ContainsKey(instance.ComponentId))
                {
                    throw new ArgumentException("Component bag instances must be unique and reference known components.", nameof(componentBagTemplate));
                }

                instanceCountByComponent.TryGetValue(instance.ComponentId, out var instanceCount);
                instanceCountByComponent[instance.ComponentId] = instanceCount + 1;
            }

            foreach (var component in Components)
            {
                instanceCountByComponent.TryGetValue(component.Id, out var instanceCount);
                if (instanceCount != component.CopiesPerRun)
                {
                    throw new ArgumentException($"Component bag count does not match {component.Id}.", nameof(componentBagTemplate));
                }
            }

            var heroIds = new HashSet<string>(StringComparer.Ordinal);
            recipeByHeroId = new Dictionary<string, HeroRecipeDefinition>(StringComparer.Ordinal);
            recipeById = new Dictionary<string, HeroRecipeDefinition>(StringComparer.Ordinal);
            foreach (var recipe in Recipes)
            {
                if (recipe == null ||
                    !heroIds.Add(recipe.HeroId) ||
                    string.IsNullOrWhiteSpace(recipe.RecipeId) ||
                    recipeById.ContainsKey(recipe.RecipeId) ||
                    !componentById.ContainsKey(recipe.ComponentAId) ||
                    !componentById.ContainsKey(recipe.ComponentBId))
                {
                    throw new ArgumentException("Hero recipes must use unique hero ids and known components.", nameof(recipes));
                }

                recipeByHeroId.Add(recipe.HeroId, recipe);
                recipeById.Add(recipe.RecipeId, recipe);
            }
        }

        public IReadOnlyList<string> BasicUnitIds { get; }
        public IReadOnlyList<HeroComponentDefinition> Components { get; }
        public IReadOnlyList<HeroRecipeDefinition> Recipes { get; }
        public IReadOnlyList<HeroComponentInstanceDefinition> ComponentBagTemplate { get; }

        public HeroComponentDefinition GetComponent(string id)
        {
            id = DragonBoundLegacyAliases.ResolveComponentId(id);
            if (!componentById.TryGetValue(id, out var component))
            {
                throw new KeyNotFoundException($"Unknown hero component {id}.");
            }

            return component;
        }

        public HeroRecipeDefinition GetRecipe(string recipeOrHeroId)
        {
            if (!recipeById.TryGetValue(recipeOrHeroId, out var recipe) &&
                !recipeByHeroId.TryGetValue(DragonBoundLegacyAliases.ResolveHeroId(recipeOrHeroId), out recipe))
            {
                throw new KeyNotFoundException($"Unknown hero recipe {recipeOrHeroId}.");
            }

            return recipe;
        }

        private static IReadOnlyList<HeroComponentInstanceDefinition> BuildBagTemplate(
            IReadOnlyList<HeroComponentDefinition> components)
        {
            var instances = new List<HeroComponentInstanceDefinition>(24);
            foreach (var component in components)
            {
                for (var copy = 1; copy <= component.CopiesPerRun; copy++)
                {
                    instances.Add(new HeroComponentInstanceDefinition(
                        $"{component.Id}_{copy:00}",
                        component.Id,
                        copy));
                }
            }

            return instances.AsReadOnly();
        }
    }

    public sealed class RecruitCard
    {
        public RecruitCard(
            string runtimeId,
            RecruitItemKind kind,
            string configId,
            string sourceInstanceId,
            int level = BasicUnitCatalog.MinLevel,
            bool isUnique = false)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) || string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("A recruit card requires a runtime id and config id.");
            }

            RuntimeId = runtimeId;
            Kind = kind;
            ConfigId = configId;
            SourceInstanceId = sourceInstanceId;
            IsUnique = kind == RecruitItemKind.HeroComponent && isUnique;
            if (kind == RecruitItemKind.BasicUnit)
            {
                BasicUnitCatalog.GetStats(configId, level);
                Level = level;
            }
        }

        public string RuntimeId { get; }
        public RecruitItemKind Kind { get; }
        public string ConfigId { get; }
        public string SourceInstanceId { get; }
        public string ComponentInstanceId => SourceInstanceId;
        public string ComponentDefinitionId => ConfigId;
        public bool IsUnique { get; }
        public int Level { get; private set; }

        public bool IsSameBasicUnitAndLevel(RecruitCard other)
        {
            return other != null &&
                   Kind == RecruitItemKind.BasicUnit &&
                   other.Kind == RecruitItemKind.BasicUnit &&
                   string.Equals(ConfigId, other.ConfigId, StringComparison.Ordinal) &&
                   Level == other.Level;
        }

        internal bool TryIncreaseLevel()
        {
            return TryAdjustLevel(1);
        }

        internal bool TryAdjustLevel(int delta)
        {
            if (Kind != RecruitItemKind.BasicUnit)
            {
                return false;
            }

            Level = Math.Max(
                BasicUnitCatalog.MinLevel,
                Math.Min(BasicUnitCatalog.MaxLevel, Level + delta));
            return true;
        }
    }

    public sealed class RecruitBatch
    {
        public const int CardsPerRecruitment = 5;

        public RecruitBatch(int recruitmentNumber, IReadOnlyList<RecruitCard> cards)
        {
            if (recruitmentNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(recruitmentNumber));
            }

            if (cards == null || cards.Count != CardsPerRecruitment)
            {
                throw new ArgumentException(
                    $"Every recruitment must produce exactly {CardsPerRecruitment} cards.",
                    nameof(cards));
            }

            RecruitmentNumber = recruitmentNumber;
            Cards = cards;
        }

        public int RecruitmentNumber { get; }
        public IReadOnlyList<RecruitCard> Cards { get; }
    }

    public sealed class ComponentRuntime
    {
        public ComponentRuntime(
            string componentId,
            string recipeTag,
            string sourceInstanceId,
            GridPosition currentCell)
        {
            if (string.IsNullOrWhiteSpace(componentId) || string.IsNullOrWhiteSpace(recipeTag))
            {
                throw new ArgumentException("A component runtime requires component and recipe ids.");
            }

            ComponentId = componentId;
            RecipeTag = recipeTag;
            SourceInstanceId = sourceInstanceId;
            CurrentCell = currentCell;
        }

        public string ComponentId { get; }
        public string RecipeTag { get; }
        public string SourceInstanceId { get; }
        public GridPosition CurrentCell { get; internal set; }
        public string PairLinkId { get; internal set; }
    }

    public sealed class HeroPairLink
    {
        public HeroPairLink(
            string pairLinkId,
            string componentAId,
            string componentBId,
            string recipeId,
            string heroId,
            HeroRecipeRarity rarity,
            HeroPairCombatProxy combatProxy)
        {
            if (string.IsNullOrWhiteSpace(pairLinkId) ||
                string.IsNullOrWhiteSpace(componentAId) ||
                string.IsNullOrWhiteSpace(componentBId) ||
                string.IsNullOrWhiteSpace(recipeId) ||
                string.IsNullOrWhiteSpace(heroId) ||
                string.Equals(componentAId, componentBId, StringComparison.Ordinal))
            {
                throw new ArgumentException("A hero pair link requires two different components and a recipe.");
            }

            PairLinkId = pairLinkId;
            ComponentAId = componentAId;
            ComponentBId = componentBId;
            RecipeId = recipeId;
            HeroId = heroId;
            Rarity = rarity;
            CombatProxy = combatProxy ?? throw new ArgumentNullException(nameof(combatProxy));
        }

        public string PairLinkId { get; }
        public string ComponentAId { get; }
        public string ComponentBId { get; }
        public string RecipeId { get; }
        public string HeroId { get; }
        public HeroRecipeRarity Rarity { get; }
        public HeroPairCombatProxy CombatProxy { get; }

        public bool ContainsComponent(string componentId)
        {
            return string.Equals(ComponentAId, componentId, StringComparison.Ordinal) ||
                   string.Equals(ComponentBId, componentId, StringComparison.Ordinal);
        }
    }
}
