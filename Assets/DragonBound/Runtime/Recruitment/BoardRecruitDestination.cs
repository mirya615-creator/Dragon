using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Runes;
using UnityEngine;

namespace DragonBound.Recruitment
{
    public readonly struct DeployedBasicUnit
    {
        public DeployedBasicUnit(
            RecruitCard card,
            GridPosition gridPosition,
            bool isCombatSuspended = false)
            : this(
                card,
                gridPosition,
                TargetingSystem.FromBoardPosition(gridPosition),
                isCombatSuspended)
        {
        }

        public DeployedBasicUnit(
            RecruitCard card,
            GridPosition gridPosition,
            CombatPoint combatPosition,
            bool isCombatSuspended = false)
        {
            Card = card;
            GridPosition = gridPosition;
            CombatPosition = combatPosition;
            IsCombatSuspended = isCombatSuspended;
        }

        public RecruitCard Card { get; }
        public GridPosition GridPosition { get; }
        public CombatPoint CombatPosition { get; }
        public bool IsCombatSuspended { get; }
    }

    public readonly struct ActiveHeroPair
    {
        public ActiveHeroPair(
            HeroPairLink pairLink,
            ComponentRuntime componentA,
            ComponentRuntime componentB)
            : this(
                pairLink,
                componentA,
                componentB,
                TargetingSystem.FromBoardPosition(componentA.CurrentCell),
                TargetingSystem.FromBoardPosition(componentB.CurrentCell))
        {
        }

        public ActiveHeroPair(
            HeroPairLink pairLink,
            ComponentRuntime componentA,
            ComponentRuntime componentB,
            CombatPoint componentACombatPosition,
            CombatPoint componentBCombatPosition)
        {
            PairLink = pairLink;
            ComponentA = componentA;
            ComponentB = componentB;
            CombatPosition = new CombatPoint(
                (componentACombatPosition.X + componentBCombatPosition.X) * 0.5f,
                (componentACombatPosition.Y + componentBCombatPosition.Y) * 0.5f);
        }

        public HeroPairLink PairLink { get; }
        public ComponentRuntime ComponentA { get; }
        public ComponentRuntime ComponentB { get; }
        public CombatPoint CombatPosition { get; }
    }

    public readonly struct HeroPairLinkedEvent
    {
        public HeroPairLinkedEvent(HeroPairLink pairLink)
        {
            PairLink = pairLink;
        }

        public HeroPairLink PairLink { get; }
    }

    public readonly struct HeroPairUnlinkedEvent
    {
        public HeroPairUnlinkedEvent(HeroPairLink pairLink, string reason)
        {
            PairLink = pairLink;
            Reason = reason;
        }

        public HeroPairLink PairLink { get; }
        public string Reason { get; }
    }

    public readonly struct CombatRegistrationChangedEvent
    {
        public CombatRegistrationChangedEvent(string unitId, bool isRegistered)
        {
            UnitId = unitId;
            IsRegistered = isRegistered;
        }

        public string UnitId { get; }
        public bool IsRegistered { get; }
    }

    public readonly struct BasicUnitMergedEvent
    {
        public BasicUnitMergedEvent(string sourceUnitId, string targetUnitId)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
        }

        public string SourceUnitId { get; }
        public string TargetUnitId { get; }
    }

    public sealed class BoardRecruitDestination :
        IRecruitDestination,
        IBoardUnitDropResolver,
        IBoardPostDropResolver,
        IBoardDragLifecycle,
        IBoardDragEligibility
    {
        private readonly BoardGrid board;
        private readonly Dictionary<string, RecruitCard> cardsByRuntimeId =
            new Dictionary<string, RecruitCard>(StringComparer.Ordinal);
        private readonly HashSet<string> combatSuspendedUnitIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> combatRegisteredUnitIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ComponentRuntime> componentsById =
            new Dictionary<string, ComponentRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroPairLink> pairLinksById =
            new Dictionary<string, HeroPairLink>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroProgressionState> progressionByRoleAndRecipe =
            new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroPairCombatProxy> combatProxiesByProgressionKey =
            new Dictionary<string, HeroPairCombatProxy>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroPairCombatProxy> dragBrokenCombatProxiesByUnitId =
            new Dictionary<string, HeroPairCombatProxy>(StringComparer.Ordinal);
        private readonly HashSet<string> everFormedHeroIds =
            new HashSet<string>(StringComparer.Ordinal);
        private RuneLoadoutSnapshot runeLoadoutSnapshot;
        private readonly int runeRunSeed;
        private bool runeLoadoutSnapshotSealed;
        private int pairLinkSequence;
        private int stateVersion;
        private Func<bool> mergeBlockedProvider;

        public BoardRecruitDestination(
            BoardGrid board,
            RuneLoadoutSnapshot runeLoadoutSnapshot = null,
            int runeRunSeed = 0)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.runeLoadoutSnapshot = runeLoadoutSnapshot ?? RuneLoadoutSnapshot.Empty;
            this.runeRunSeed = runeRunSeed;
            board.Changed += HandleBoardChanged;
        }

        public int CampCount => CountOccupants(CellType.Bench);
        public int DeployedCount => CountOccupants(CellType.Battle);
        public int TotalObjectCount => board.GetOccupants().Count;
        public BoardGrid Board => board;
        public int PendingRefreshCount => CampCount;
        public int ActivePairLinkCount => pairLinksById.Count;
        /// <summary>Monotonic revision for AI planning; increments on board or PairLink state changes.</summary>
        public int StateVersion => stateVersion;
        public IReadOnlyCollection<string> EverFormedHeroIds => everFormedHeroIds;
        public event Action<HeroPairLinkedEvent> HeroPairLinked;
        public event Action<HeroPairUnlinkedEvent> HeroPairUnlinked;
        public event Action<CombatRegistrationChangedEvent> CombatRegistrationChanged;
        public event Action<BasicUnitMergedEvent> BasicUnitMerged;

        /// <summary>Integration hook for boss policies that temporarily forbid all Basic merges.</summary>
        public void SetMergeBlockedProvider(Func<bool> provider)
        {
            mergeBlockedProvider = provider;
        }

        /// <summary>
        /// The bootstrap may apply the loadout while the match is still preparing. This updates
        /// any showcase/preparation PairLinks that were formed early, then sealing at Run start
        /// makes the snapshot immutable for all active combat.
        /// </summary>
        public bool TrySetRuneLoadoutSnapshot(RuneLoadoutSnapshot snapshot)
        {
            if (runeLoadoutSnapshotSealed)
            {
                return false;
            }

            runeLoadoutSnapshot = snapshot ?? RuneLoadoutSnapshot.Empty;
            foreach (var pairLink in pairLinksById.Values)
            {
                pairLink.CombatProxy.ConfigureRune(
                    RuneCatalog.Get(runeLoadoutSnapshot.GetRune(pairLink.HeroId)),
                    runeRunSeed);
            }
            return true;
        }

        public void SealRuneLoadoutSnapshot()
        {
            runeLoadoutSnapshotSealed = true;
        }

        public bool PendingRefreshContainsUniqueHeroComponent
        {
            get
            {
                foreach (var position in board.GetPositions(CellType.Bench))
                {
                    if (board.TryGetOccupant(position, out var runtimeId) &&
                        cardsByRuntimeId.TryGetValue(runtimeId, out var card) &&
                        card.Kind == RecruitItemKind.HeroComponent &&
                        card.IsUnique)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public List<RecruitCard> GetDeployedCards()
        {
            var result = new List<RecruitCard>();
            foreach (var occupant in board.GetOccupants())
            {
                if (board.TryGetCellType(occupant.Position, out var cellType) &&
                    cellType == CellType.Battle &&
                    cardsByRuntimeId.TryGetValue(occupant.UnitId, out var card))
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public List<RecruitCard> GetBoardCards()
        {
            var result = new List<RecruitCard>();
            foreach (var occupant in board.GetOccupants())
            {
                if (cardsByRuntimeId.TryGetValue(occupant.UnitId, out var card))
                {
                    result.Add(card);
                }
            }

            return result;
        }

        public List<DeployedBasicUnit> GetDeployedUnits()
        {
            var result = new List<DeployedBasicUnit>();
            foreach (var position in board.GetPositions(CellType.Battle))
            {
                if (board.TryGetOccupant(position, out var runtimeId) &&
                    cardsByRuntimeId.TryGetValue(runtimeId, out var card) &&
                    card.Kind == RecruitItemKind.BasicUnit)
                {
                    result.Add(new DeployedBasicUnit(
                        card,
                        position,
                        board.GetCombatPosition(position),
                        combatSuspendedUnitIds.Contains(runtimeId)));
                }
            }

            return result;
        }

        public List<ActiveHeroPair> GetActiveHeroPairs()
        {
            var result = new List<ActiveHeroPair>();
            foreach (var pairLink in pairLinksById.Values)
            {
                if (componentsById.TryGetValue(pairLink.ComponentAId, out var componentA) &&
                    componentsById.TryGetValue(pairLink.ComponentBId, out var componentB) &&
                    IsBattleCell(componentA.CurrentCell) &&
                    IsBattleCell(componentB.CurrentCell))
                {
                    result.Add(new ActiveHeroPair(
                        pairLink,
                        componentA,
                        componentB,
                        board.GetCombatPosition(componentA.CurrentCell),
                        board.GetCombatPosition(componentB.CurrentCell)));
                }
            }

            result.Sort((first, second) =>
                string.CompareOrdinal(first.PairLink.PairLinkId, second.PairLink.PairLinkId));
            return result;
        }

        public void TickPairLinks(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            foreach (var pairLink in pairLinksById.Values)
            {
                pairLink.CombatProxy.TickFormation(deltaSeconds);
            }
        }

        public RecruitDestinationPlan Plan(int cardCount)
        {
            if (cardCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cardCount));
            }

            var bench = board.GetPositions(CellType.Bench);
            var occupiedCount = 0;
            foreach (var position in bench)
            {
                if (board.IsOccupied(position))
                {
                    occupiedCount++;
                }
            }

            if (occupiedCount == 0 && bench.Count == cardCount)
            {
                return RecruitDestinationPlan.AddToEmptySlots;
            }

            if (bench.Count == cardCount)
            {
                return RecruitDestinationPlan.RefreshBench;
            }

            throw new InvalidOperationException(
                $"The bench must contain exactly {cardCount} slots for recruitment.");
        }

        public RecruitDestinationReceipt Commit(RecruitDestinationPlan plan, RecruitBatch batch)
        {
            if (batch == null || batch.Cards == null || batch.Cards.Count == 0)
            {
                throw new ArgumentException("A non-empty recruitment batch is required.", nameof(batch));
            }

            if (plan != Plan(batch.Cards.Count))
            {
                throw new InvalidOperationException("The bench changed before recruitment commit.");
            }

            var bench = board.GetPositions(CellType.Bench);
            var removedCards = new List<RecruitCard>();
            if (plan == RecruitDestinationPlan.RefreshBench)
            {
                foreach (var position in bench)
                {
                    if (!board.TryGetOccupant(position, out var oldRuntimeId))
                    {
                        continue;
                    }

                    if (!cardsByRuntimeId.TryGetValue(oldRuntimeId, out var oldCard))
                    {
                        throw new InvalidOperationException(
                            $"Bench occupant {oldRuntimeId} has no recruitment card.");
                    }

                    BreakPairForComponent(oldRuntimeId, "BenchRefresh");
                    removedCards.Add(oldCard);
                    if (componentsById.ContainsKey(oldRuntimeId))
                    {
                        RemoveProgressionForOwner(oldRuntimeId);
                    }
                    cardsByRuntimeId.Remove(oldRuntimeId);
                    componentsById.Remove(oldRuntimeId);
                    combatSuspendedUnitIds.Remove(oldRuntimeId);
                    if (!board.TryRemoveAt(position))
                    {
                        throw new InvalidOperationException($"Failed to clear bench position {position}.");
                    }
                }
            }

            var cardIndex = 0;
            foreach (var position in bench)
            {
                if (board.IsOccupied(position))
                {
                    continue;
                }

                var card = batch.Cards[cardIndex++];
                if (!board.TryPlace(card.RuntimeId, position))
                {
                    throw new InvalidOperationException($"Failed to place recruited card at {position}.");
                }

                cardsByRuntimeId.Add(card.RuntimeId, card);
                if (card.Kind == RecruitItemKind.HeroComponent)
                {
                    componentsById.Add(
                        card.RuntimeId,
                        new ComponentRuntime(
                            card.RuntimeId,
                            card.ConfigId,
                            card.SourceInstanceId,
                            position));
                }

                if (cardIndex == batch.Cards.Count)
                {
                    break;
                }
            }

            if (cardIndex != batch.Cards.Count)
            {
                throw new InvalidOperationException("The planned bench transaction did not place every recruited card.");
            }

            ReconcileCombatRegistrations();
            return new RecruitDestinationReceipt(removedCards);
        }

        public bool TryGetCard(string runtimeId, out RecruitCard card)
        {
            return cardsByRuntimeId.TryGetValue(runtimeId, out card);
        }

        public bool TryRemoveUnit(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) ||
                !cardsByRuntimeId.ContainsKey(runtimeId) ||
                !board.TryGetPosition(runtimeId, out var position))
            {
                return false;
            }

            BreakPairForComponent(runtimeId, "WorldeaterDevour");
            RemoveProgressionForOwner(runtimeId);
            componentsById.Remove(runtimeId);
            combatSuspendedUnitIds.Remove(runtimeId);
            cardsByRuntimeId.Remove(runtimeId);
            if (!board.TryRemoveAt(position))
            {
                return false;
            }

            ReconcileCombatRegistrations();
            return true;
        }

        public int GetBenchShovelCount()
        {
            var count = 0;
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId) && IsBenchShovel(runtimeId))
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsBenchShovel(string runtimeId)
        {
            return !string.IsNullOrWhiteSpace(runtimeId) &&
                   cardsByRuntimeId.TryGetValue(runtimeId, out var card) &&
                   card.Kind == RecruitItemKind.Shovel &&
                   board.TryGetPosition(runtimeId, out var position) &&
                   board.TryGetCellType(position, out var cellType) &&
                   cellType == CellType.Bench;
        }

        public bool TryGetFirstBenchShovel(out string runtimeId)
        {
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var candidate) && IsBenchShovel(candidate))
                {
                    runtimeId = candidate;
                    return true;
                }
            }

            runtimeId = null;
            return false;
        }

        public bool TryConsumeBenchShovel(string runtimeId)
        {
            if (!IsBenchShovel(runtimeId) || !board.TryGetPosition(runtimeId, out var position))
            {
                return false;
            }

            if (!board.TryRemoveAt(position))
            {
                return false;
            }

            cardsByRuntimeId.Remove(runtimeId);
            return true;
        }

        public bool CanBeginDrag(string unitId)
        {
            return !cardsByRuntimeId.TryGetValue(unitId, out var card) ||
                   card.Kind != RecruitItemKind.Shovel;
        }

        public int GetCurrentHeroComponentCount(string configId)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("A component config id is required.", nameof(configId));
            }

            var count = 0;
            foreach (var card in cardsByRuntimeId.Values)
            {
                if (card.Kind == RecruitItemKind.HeroComponent &&
                    string.Equals(card.ConfigId, configId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryGetComponent(string componentId, out ComponentRuntime component)
        {
            return componentsById.TryGetValue(componentId, out component);
        }

        public bool TryGetPairLink(string pairLinkId, out HeroPairLink pairLink)
        {
            return pairLinksById.TryGetValue(pairLinkId, out pairLink);
        }

        public bool TryGetPairLinkForComponent(string componentId, out HeroPairLink pairLink)
        {
            pairLink = null;
            return componentsById.TryGetValue(componentId, out var component) &&
                   !string.IsNullOrEmpty(component.PairLinkId) &&
                   pairLinksById.TryGetValue(component.PairLinkId, out pairLink);
        }

        public bool HasActiveRecipe(string recipeId)
        {
            foreach (var pairLink in pairLinksById.Values)
            {
                if (string.Equals(pairLink.RecipeId, recipeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasActiveHero(string heroId)
        {
            heroId = DragonBoundLegacyAliases.ResolveHeroId(heroId);
            foreach (var pairLink in pairLinksById.Values)
            {
                if (string.Equals(pairLink.HeroId, heroId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasEverFormedHero(string heroId)
        {
            return !string.IsNullOrWhiteSpace(heroId) && everFormedHeroIds.Contains(heroId);
        }

        public bool IsCombatRegistered(string runtimeId)
        {
            return !string.IsNullOrWhiteSpace(runtimeId) && combatRegisteredUnitIds.Contains(runtimeId);
        }

        public bool SetCombatSuspended(string runtimeId, bool suspended)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) ||
                !cardsByRuntimeId.TryGetValue(runtimeId, out var card) ||
                card.Kind != RecruitItemKind.BasicUnit)
            {
                return false;
            }

            if (!suspended)
            {
                return combatSuspendedUnitIds.Remove(runtimeId);
            }

            if (!board.TryGetPosition(runtimeId, out var position) ||
                !board.TryGetCellType(position, out var cellType) ||
                cellType != CellType.Battle)
            {
                return false;
            }

            return combatSuspendedUnitIds.Add(runtimeId);
        }

        public bool IsCombatSuspended(string runtimeId)
        {
            return !string.IsNullOrWhiteSpace(runtimeId) &&
                   combatSuspendedUnitIds.Contains(runtimeId);
        }

        public bool CanResolveOccupiedDrop(
            string sourceUnitId,
            string targetUnitId,
            GridPosition source,
            GridPosition target,
            CellType sourceType,
            CellType targetType)
        {
            if (!TryGetCurrentCards(sourceUnitId, targetUnitId, source, target, out var sourceCard, out var targetCard))
            {
                return false;
            }

            if (sourceCard.IsSameBasicUnitAndLevel(targetCard) &&
                (mergeBlockedProvider?.Invoke() ?? false))
            {
                return false;
            }

            if (sourceCard.IsSameBasicUnitAndLevel(targetCard))
            {
                return sourceCard.Level < BasicUnitCatalog.MaxLevel;
            }

            return IsMovableCellType(sourceType) && IsMovableCellType(targetType);
        }

        public OccupiedDropResolution ResolveOccupiedDrop(
            string sourceUnitId,
            string targetUnitId,
            GridPosition source,
            GridPosition target,
            CellType sourceType,
            CellType targetType)
        {
            if (!TryGetCurrentCards(sourceUnitId, targetUnitId, source, target, out var sourceCard, out var targetCard))
            {
                return OccupiedDropResolution.Rejected;
            }

            if (sourceCard.IsSameBasicUnitAndLevel(targetCard) &&
                (mergeBlockedProvider?.Invoke() ?? false))
            {
                return OccupiedDropResolution.Rejected;
            }

            if (sourceCard.IsSameBasicUnitAndLevel(targetCard))
            {
                if (sourceCard.Level >= BasicUnitCatalog.MaxLevel || !targetCard.TryIncreaseLevel())
                {
                    return OccupiedDropResolution.Rejected;
                }

                cardsByRuntimeId.Remove(sourceUnitId);
                if (componentsById.ContainsKey(sourceUnitId))
                {
                    RemoveProgressionForOwner(sourceUnitId);
                    componentsById.Remove(sourceUnitId);
                }
                combatSuspendedUnitIds.Remove(sourceUnitId);
                if (!board.TryRemoveAt(source))
                {
                    throw new InvalidOperationException("A validated merge source could not be removed.");
                }

                Debug.Log(
                    $"BasicUnitMerged Source={sourceUnitId} Target={targetUnitId} " +
                    $"ConfigId={targetCard.ConfigId} Level={targetCard.Level}");
                BasicUnitMerged?.Invoke(new BasicUnitMergedEvent(sourceUnitId, targetUnitId));
                return OccupiedDropResolution.Merged;
            }

            if (IsMovableCellType(sourceType) && IsMovableCellType(targetType))
            {
                TryGetPairLinkForComponent(targetUnitId, out var targetPairLink);
                BreakPairForComponent(targetUnitId, "SwapTargetMoved");
                if (board.TrySwap(source, target))
                {
                    targetPairLink?.CombatProxy.ResetTargetingAfterRelocation();
                    Debug.Log($"UnitsSwapped First={sourceUnitId} Second={targetUnitId}");
                    return OccupiedDropResolution.Swapped;
                }
            }

            return OccupiedDropResolution.Rejected;
        }

        public void OnDragStarted(string unitId, GridPosition origin)
        {
            if (TryGetPairLinkForComponent(unitId, out var pairLink))
            {
                dragBrokenCombatProxiesByUnitId[unitId] = pairLink.CombatProxy;
            }

            BreakPairForComponent(unitId, "DragStarted");
            SetCombatSuspended(unitId, true);
        }

        public void OnDragCompleted(DragCompletion completion)
        {
            if ((completion.Status == DragDropStatus.Moved || completion.Status == DragDropStatus.Swapped) &&
                dragBrokenCombatProxiesByUnitId.TryGetValue(completion.UnitId, out var dragBrokenCombatProxy))
            {
                dragBrokenCombatProxy.ResetTargetingAfterRelocation();
            }

            dragBrokenCombatProxiesByUnitId.Remove(completion.UnitId);
            if (componentsById.TryGetValue(completion.UnitId, out var component) &&
                board.TryGetPosition(component.ComponentId, out var position))
            {
                component.CurrentCell = position;
            }

            SetCombatSuspended(completion.UnitId, false);
            ReconcileCombatRegistrations();
            ResolveAllAvailableLinks();
        }

        public bool TryResolvePostDrop(string movedUnitId)
        {
            ReconcileInvalidLinks("PostDropInvalidated");
            ResolveAllAvailableLinks();
            return TryGetPairLinkForComponent(movedUnitId, out _);
        }

        public bool TryCreatePairLinkAt(string componentId, out HeroPairLink pairLink)
        {
            pairLink = null;
            if (!componentsById.TryGetValue(componentId, out var component) ||
                !string.IsNullOrEmpty(component.PairLinkId) ||
                !IsBattleCell(component.CurrentCell))
            {
                return false;
            }

            var candidates = new List<ComponentRuntime>();
            foreach (var neighbour in GetOrthogonalNeighbours(component.CurrentCell))
            {
                if (!board.TryGetOccupant(neighbour, out var neighbourId) ||
                    !componentsById.TryGetValue(neighbourId, out var candidate) ||
                    !string.IsNullOrEmpty(candidate.PairLinkId) ||
                    !IsBattleCell(candidate.CurrentCell) ||
                    !TryGetRecipeDefinitionAtFormation(
                        component.RecipeTag,
                        component.CurrentCell,
                        candidate.RecipeTag,
                        candidate.CurrentCell,
                        out _))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            candidates.Sort((first, second) =>
            {
                var position = ToFormationLocalPosition(first.CurrentCell).CompareTo(
                    ToFormationLocalPosition(second.CurrentCell));
                return position != 0
                    ? position
                    : string.CompareOrdinal(first.ComponentId, second.ComponentId);
            });

            foreach (var candidate in candidates)
            {
                if (!TryGetRecipeDefinitionAtFormation(
                        component.RecipeTag,
                        component.CurrentCell,
                        candidate.RecipeTag,
                        candidate.CurrentCell,
                        out var recipe))
                {
                    continue;
                }

                var progressOwner = GetProgressOwnerRuntime(recipe, component, candidate);
                var progressionKey = $"{progressOwner.ComponentId}|{recipe.RecipeId}";
                if (!progressionByRoleAndRecipe.TryGetValue(progressionKey, out var progression))
                {
                    progression = new HeroProgressionState(recipe.HeroId);
                    progressionByRoleAndRecipe.Add(progressionKey, progression);
                }

                pairLinkSequence++;
                var pairLinkId = $"pair.{recipe.RecipeId}.{pairLinkSequence:00}";
                var metadata = HeroDefinitionCatalog.GetMetadata(recipe.HeroId);
                if (metadata.RuntimeCombatState != HeroRuntimeCombatState.Implemented)
                {
                    continue;
                }

                var definition = HeroSliceCatalog.Get(recipe.HeroId);
                if (!combatProxiesByProgressionKey.TryGetValue(progressionKey, out var combatProxy))
                {
                    combatProxy = new HeroPairCombatProxy(
                        recipe.HeroId,
                        progression,
                        board.Side,
                        progressOwner.ComponentId,
                        recipe.RecipeId);
                    combatProxiesByProgressionKey.Add(progressionKey, combatProxy);
                }
                else
                {
                    combatProxy.SetCombatSuspended(false);
                }

                var assignedRuneId = runeLoadoutSnapshot == null
                    ? string.Empty
                    : runeLoadoutSnapshot.GetRune(recipe.HeroId);
                combatProxy.ConfigureRune(RuneCatalog.Get(assignedRuneId), runeRunSeed);

                pairLink = new HeroPairLink(
                    pairLinkId,
                    component.ComponentId,
                    candidate.ComponentId,
                    recipe.RecipeId,
                    recipe.HeroId,
                    definition.Rarity,
                    combatProxy);
                component.PairLinkId = pairLinkId;
                candidate.PairLinkId = pairLinkId;
                pairLinksById.Add(pairLinkId, pairLink);
                stateVersion++;
                var isFirstFormation = everFormedHeroIds.Add(recipe.HeroId);
                Debug.Log(
                    $"HeroPairLinked PairLinkId={pairLinkId} RecipeId={recipe.RecipeId} HeroId={recipe.HeroId} " +
                    $"ComponentA={component.ComponentId} ComponentB={candidate.ComponentId} " +
                    $"CellA={component.CurrentCell} CellB={candidate.CurrentCell} " +
                    $"FirstFormation={isFirstFormation}");
                HeroPairLinked?.Invoke(new HeroPairLinkedEvent(pairLink));
                return true;
            }

            return false;
        }

        private void HandleBoardChanged(GridMutation mutation)
        {
            stateVersion++;
            if (!string.IsNullOrEmpty(mutation.UnitId) &&
                componentsById.TryGetValue(mutation.UnitId, out var component))
            {
                if (board.TryGetPosition(component.ComponentId, out var position))
                {
                    component.CurrentCell = position;
                }
                else
                {
                    BreakPairForComponent(component.ComponentId, "ComponentRemoved");
                }
            }

            ReconcileInvalidLinks("BoardChanged");
            ReconcileCombatRegistrations();
        }

        private void ResolveAllAvailableLinks()
        {
            var components = new List<ComponentRuntime>(componentsById.Values);
            components.Sort((first, second) =>
            {
                var position = first.CurrentCell.CompareTo(second.CurrentCell);
                return position != 0
                    ? position
                    : string.CompareOrdinal(first.ComponentId, second.ComponentId);
            });

            foreach (var component in components)
            {
                if (string.IsNullOrEmpty(component.PairLinkId))
                {
                    TryCreatePairLinkAt(component.ComponentId, out _);
                }
            }
        }

        private void ReconcileInvalidLinks(string reason)
        {
            var invalidLinks = new List<string>();
            foreach (var pairLink in pairLinksById.Values)
            {
                if (!IsLinkValid(pairLink))
                {
                    invalidLinks.Add(pairLink.PairLinkId);
                }
            }

            foreach (var pairLinkId in invalidLinks)
            {
                BreakPairLink(pairLinkId, reason);
            }
        }

        private bool TryGetRecipeDefinitionAtFormation(
            string firstComponentId,
            GridPosition firstPosition,
            string secondComponentId,
            GridPosition secondPosition,
            out HeroRecipeDefinition recipe)
        {
            var firstLocal = ToFormationLocalPosition(firstPosition);
            var secondLocal = ToFormationLocalPosition(secondPosition);
            return HeroSliceCatalog.TryGetRecipeDefinitionAtFormation(
                firstComponentId,
                firstLocal,
                secondComponentId,
                secondLocal,
                out recipe);
        }

        private GridPosition ToFormationLocalPosition(GridPosition position)
        {
            if (board.Side == TeamSide.Player || board.Layout == null)
            {
                return position;
            }

            if (board.Layout is FixedBoardLayoutDefinition)
            {
                return new GridPosition(
                    FixedBoardLayoutDefinition.FixedColumns - 1 - position.X,
                    FixedBoardLayoutDefinition.FixedRows - 1 - position.Y);
            }

            return board.Layout.GetFairCounterpart(position, TeamSide.AI);
        }

        private bool IsLinkValid(HeroPairLink pairLink)
        {
            return componentsById.TryGetValue(pairLink.ComponentAId, out var componentA) &&
                   componentsById.TryGetValue(pairLink.ComponentBId, out var componentB) &&
                   string.Equals(componentA.PairLinkId, pairLink.PairLinkId, StringComparison.Ordinal) &&
                   string.Equals(componentB.PairLinkId, pairLink.PairLinkId, StringComparison.Ordinal) &&
                   IsBattleCell(componentA.CurrentCell) &&
                   IsBattleCell(componentB.CurrentCell) &&
                   TryGetRecipeDefinitionAtFormation(
                       componentA.RecipeTag,
                       componentA.CurrentCell,
                       componentB.RecipeTag,
                       componentB.CurrentCell,
                       out var recipe) &&
                   string.Equals(recipe.RecipeId, pairLink.RecipeId, StringComparison.Ordinal) &&
                   string.Equals(recipe.HeroId, pairLink.HeroId, StringComparison.Ordinal);
        }

        private bool BreakPairForComponent(string componentId, string reason)
        {
            if (!componentsById.TryGetValue(componentId, out var component) ||
                string.IsNullOrEmpty(component.PairLinkId))
            {
                return false;
            }

            return BreakPairLink(component.PairLinkId, reason);
        }

        private bool BreakPairLink(string pairLinkId, string reason)
        {
            if (!pairLinksById.TryGetValue(pairLinkId, out var pairLink))
            {
                return false;
            }

            pairLinksById.Remove(pairLinkId);
            stateVersion++;
            if (componentsById.TryGetValue(pairLink.ComponentAId, out var componentA) &&
                string.Equals(componentA.PairLinkId, pairLinkId, StringComparison.Ordinal))
            {
                componentA.PairLinkId = null;
            }

            if (componentsById.TryGetValue(pairLink.ComponentBId, out var componentB) &&
                string.Equals(componentB.PairLinkId, pairLinkId, StringComparison.Ordinal))
            {
                componentB.PairLinkId = null;
            }

            pairLink.CombatProxy.SetCombatSuspended(true);
            Debug.Log(
                $"HeroPairUnlinked PairLinkId={pairLinkId} RecipeId={pairLink.RecipeId} HeroId={pairLink.HeroId} " +
                $"Reason={reason}");
            HeroPairUnlinked?.Invoke(new HeroPairUnlinkedEvent(pairLink, reason));
            return true;
        }

        private void RemoveProgressionForOwner(string ownerRuntimeId)
        {
            var prefix = ownerRuntimeId + "|";
            var keys = new List<string>();
            foreach (var key in progressionByRoleAndRecipe.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keys.Add(key);
                }
            }

            foreach (var key in keys)
            {
                progressionByRoleAndRecipe.Remove(key);
                if (combatProxiesByProgressionKey.TryGetValue(key, out var proxy))
                {
                    proxy.StopAndReset();
                    combatProxiesByProgressionKey.Remove(key);
                }
            }
        }

        private bool IsBattleCell(GridPosition position)
        {
            return board.TryGetCellType(position, out var cellType) && cellType == CellType.Battle;
        }

        private static bool IsMovableCellType(CellType cellType)
        {
            return cellType == CellType.Battle || cellType == CellType.Bench;
        }

        private void ReconcileCombatRegistrations()
        {
            var registeredNow = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in cardsByRuntimeId.Values)
            {
                if (board.TryGetPosition(card.RuntimeId, out var position) && IsBattleCell(position))
                {
                    registeredNow.Add(card.RuntimeId);
                }
            }

            var removed = new List<string>();
            foreach (var runtimeId in combatRegisteredUnitIds)
            {
                if (!registeredNow.Contains(runtimeId))
                {
                    removed.Add(runtimeId);
                }
            }

            removed.Sort(StringComparer.Ordinal);
            foreach (var runtimeId in removed)
            {
                combatRegisteredUnitIds.Remove(runtimeId);
                combatSuspendedUnitIds.Remove(runtimeId);
                CombatRegistrationChanged?.Invoke(new CombatRegistrationChangedEvent(runtimeId, false));
            }

            var added = new List<string>();
            foreach (var runtimeId in registeredNow)
            {
                if (!combatRegisteredUnitIds.Contains(runtimeId))
                {
                    added.Add(runtimeId);
                }
            }

            added.Sort(StringComparer.Ordinal);
            foreach (var runtimeId in added)
            {
                combatRegisteredUnitIds.Add(runtimeId);
                CombatRegistrationChanged?.Invoke(new CombatRegistrationChangedEvent(runtimeId, true));
            }
        }

        private static ComponentRuntime GetProgressOwnerRuntime(
            HeroRecipeDefinition recipe,
            ComponentRuntime first,
            ComponentRuntime second)
        {
            if (string.IsNullOrWhiteSpace(recipe.ProgressOwnerComponentId))
            {
                throw new InvalidOperationException(
                    $"Recipe {recipe.HeroId} cannot enter combat without ProgressOwnerComponentId.");
            }

            if (string.Equals(first.RecipeTag, recipe.ProgressOwnerComponentId, StringComparison.Ordinal))
            {
                return first;
            }

            if (string.Equals(second.RecipeTag, recipe.ProgressOwnerComponentId, StringComparison.Ordinal))
            {
                return second;
            }

            throw new InvalidOperationException(
                $"Recipe {recipe.HeroId} progress owner {recipe.ProgressOwnerComponentId} is not linked.");
        }

        private static IEnumerable<GridPosition> GetOrthogonalNeighbours(GridPosition position)
        {
            yield return new GridPosition(position.X - 1, position.Y);
            yield return new GridPosition(position.X + 1, position.Y);
            yield return new GridPosition(position.X, position.Y - 1);
            yield return new GridPosition(position.X, position.Y + 1);
        }

        private bool TryGetCurrentCards(
            string sourceUnitId,
            string targetUnitId,
            GridPosition source,
            GridPosition target,
            out RecruitCard sourceCard,
            out RecruitCard targetCard)
        {
            sourceCard = null;
            targetCard = null;
            return !string.Equals(sourceUnitId, targetUnitId, StringComparison.Ordinal) &&
                   board.TryGetOccupant(source, out var actualSource) &&
                   board.TryGetOccupant(target, out var actualTarget) &&
                   string.Equals(actualSource, sourceUnitId, StringComparison.Ordinal) &&
                   string.Equals(actualTarget, targetUnitId, StringComparison.Ordinal) &&
                   cardsByRuntimeId.TryGetValue(sourceUnitId, out sourceCard) &&
                   cardsByRuntimeId.TryGetValue(targetUnitId, out targetCard);
        }

        private int CountOccupants(CellType cellType)
        {
            var count = 0;
            foreach (var occupant in board.GetOccupants())
            {
                if (board.TryGetCellType(occupant.Position, out var occupantType) && occupantType == cellType)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
