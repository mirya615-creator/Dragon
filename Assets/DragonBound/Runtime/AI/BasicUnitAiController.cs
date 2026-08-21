using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.AI
{
    public enum AiRecruitBlockedReason
    {
        None,
        InsufficientResources,
        RunEnded,
        InvalidState,
        CampPolicyBlocked,
        BenchPolicyBlocked,
        BoardPolicyBlocked,
        Other
    }

    /// <summary>
    /// Explains why a pair of owned, matching hero components could not yet be arranged into
    /// its formal recipe. These reasons are diagnostic only; matching pairs remain pending and
    /// are reconsidered after every later AI decision cycle.
    /// </summary>
    public enum AiRecipeBlockedReason
    {
        None,
        NoLegalAdjacentCells,
        ComponentInCampNotHandled,
        ComponentInBenchNotHandled,
        ComponentOnBoardNotMovable,
        BoardCapacity,
        BenchCapacity,
        PlacementPolicy,
        DirectionConstraint,
        PairAlreadyReserved,
        Other
    }

    public sealed class PendingFormableRecipe
    {
        internal PendingFormableRecipe(string key, string recipeId, string firstComponentRuntimeId, string secondComponentRuntimeId)
        {
            Key = key;
            RecipeId = recipeId;
            FirstComponentRuntimeId = firstComponentRuntimeId;
            SecondComponentRuntimeId = secondComponentRuntimeId;
        }

        public string Key { get; }
        public string RecipeId { get; }
        public string FirstComponentRuntimeId { get; }
        public string SecondComponentRuntimeId { get; }
        public int RetryCount { get; internal set; }
        public AiRecipeBlockedReason LastFailureReason { get; internal set; }
    }

    public readonly struct AiRecruitTelemetry
    {
        public AiRecruitTelemetry(
            int currentResources,
            int nextRecruitCost,
            bool canAffordRecruit,
            int recruitCount,
            int campOccupiedSlotCount,
            int benchOccupiedSlotCount,
            int boardOccupiedSlotCount,
            int boardOpenCellCount,
            int unpairedComponentCount,
            int pairLinkCount,
            bool aiWantedToRecruit,
            bool aiRecruitAttempted,
            bool aiRecruitSucceeded,
            AiRecruitBlockedReason recruitBlockedReason,
            bool legacyCampPolicyWouldBlock)
        {
            CurrentResources = currentResources;
            NextRecruitCost = nextRecruitCost;
            CanAffordRecruit = canAffordRecruit;
            RecruitCount = recruitCount;
            CampOccupiedSlotCount = campOccupiedSlotCount;
            BenchOccupiedSlotCount = benchOccupiedSlotCount;
            BoardOccupiedSlotCount = boardOccupiedSlotCount;
            BoardOpenCellCount = boardOpenCellCount;
            UnpairedComponentCount = unpairedComponentCount;
            PairLinkCount = pairLinkCount;
            AIWantedToRecruit = aiWantedToRecruit;
            AIRecruitAttempted = aiRecruitAttempted;
            AIRecruitSucceeded = aiRecruitSucceeded;
            RecruitBlockedReason = recruitBlockedReason;
            LegacyCampPolicyWouldBlock = legacyCampPolicyWouldBlock;
        }

        public int CurrentResources { get; }
        public int NextRecruitCost { get; }
        public bool CanAffordRecruit { get; }
        public int RecruitCount { get; }
        public int CampOccupiedSlotCount { get; }
        public int BenchOccupiedSlotCount { get; }
        public int BoardOccupiedSlotCount { get; }
        public int BoardOpenCellCount { get; }
        public int UnpairedComponentCount { get; }
        public int PairLinkCount { get; }
        public bool AIWantedToRecruit { get; }
        public bool AIRecruitAttempted { get; }
        public bool AIRecruitSucceeded { get; }
        public AiRecruitBlockedReason RecruitBlockedReason { get; }
        public bool LegacyCampPolicyWouldBlock { get; }
    }

    public sealed class BasicUnitAiController
    {
        private readonly BoardGrid board;
        private readonly BoardRecruitDestination destination;
        private readonly RecruitmentService recruitment;
        private readonly ShovelUnlockService shovelUnlocks;
        private readonly TeamState team;
        private readonly Dictionary<AiRecruitBlockedReason, int> recruitStallCounts =
            new Dictionary<AiRecruitBlockedReason, int>();
        private readonly Dictionary<string, PendingFormableRecipe> pendingFormableRecipes =
            new Dictionary<string, PendingFormableRecipe>(StringComparer.Ordinal);
        private readonly Dictionary<AiRecipeBlockedReason, int> recipeFailureCounts =
            new Dictionary<AiRecipeBlockedReason, int>();
        private AiRecruitBlockedReason activeStallReason;
        private int lastRecipeStateVersion = -1;

        public BasicUnitAiController(
            BoardGrid board,
            BoardRecruitDestination destination,
            RecruitmentService recruitment)
            : this(board, destination, recruitment, null, null)
        {
        }

        /// <summary>
        /// V0 survival controller. It deliberately receives only one side's board, recruitment
        /// service, shovel inventory, and team state, so it has no path to inspect or mutate the
        /// opposing battlefield.
        /// </summary>
        public BasicUnitAiController(
            BoardGrid board,
            BoardRecruitDestination destination,
            RecruitmentService recruitment,
            ShovelUnlockService shovelUnlocks,
            TeamState team)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.recruitment = recruitment ?? throw new ArgumentNullException(nameof(recruitment));
            this.shovelUnlocks = shovelUnlocks;
            this.team = team;
            if (team != null && team.Side != board.Side)
            {
                throw new ArgumentException("AI team state must match its board side.", nameof(team));
            }

            Diagnostics = team == null
                ? null
                : new AiSurvivalDiagnostics(board.Side, board, destination, recruitment, team);
        }

        public AiSurvivalDiagnostics Diagnostics { get; }
        public TeamSide Side => board.Side;
        public bool LastCycleChanged { get; private set; }
        public AiRecruitTelemetry LastRecruitTelemetry { get; private set; }
        public int RecruitStallCount { get; private set; }
        public int FirstRecruitStallWave { get; private set; } = -1;
        public int AvailableRecipePairCount { get; private set; }
        public int FormableRecipeCount { get; private set; }
        public int BlockedRecipeCount { get; private set; }
        public int RecipeOpportunityCreated { get; private set; }
        public int RecipeFormationAttempted { get; private set; }
        public int RecipeFormationSucceeded { get; private set; }
        public int RecipeFormationFailed { get; private set; }
        public int RecipeRetryCount { get; private set; }
        public IReadOnlyCollection<PendingFormableRecipe> PendingFormableRecipes => pendingFormableRecipes.Values;
        public IReadOnlyDictionary<AiRecipeBlockedReason, int> RecipeFailureCounts => recipeFailureCounts;
        /// <summary>
        /// Counts affordable decision cycles which the retired component-in-camp guard would
        /// have rejected. This is diagnostic-only evidence for the former recruit stall and
        /// does not affect the current refresh policy.
        /// </summary>
        public int LegacyCampPolicyBlockCount { get; private set; }
        public IReadOnlyDictionary<AiRecruitBlockedReason, int> RecruitStallCounts => recruitStallCounts;

        public RecruitmentAttempt RecruitOrRefresh()
        {
            var attempt = recruitment.TryRecruit();
            if (attempt.Status == RecruitmentStatus.Success)
            {
                if (recruitment.HeroSliceMode)
                {
                    MergeAllAvailable();
                    DeployHeroComponentsAndForm();
                }
                else
                {
                    MaintainBoard();
                }
            }

            return attempt;
        }

        /// <summary>
        /// One legal V0 decision cycle. A component that cannot be safely moved out of the bench
        /// blocks refreshing it; the controller waits rather than discarding finite components.
        /// </summary>
        public void Tick(int currentWave = 0)
        {
            var objectsBefore = destination.TotalObjectCount;
            var linksBefore = destination.ActivePairLinkCount;
            var openCellsBefore = board.UnlockedBattleCellCount;
            var resourcesBefore = team?.Resources ?? 0;

            // Do not hold a recruited Forge Pick while space is already constrained. A valid
            // own locked cell is enough to use it; board-full is not a prerequisite.
            TryUseBenchShovelImmediately();
            MaintainBoard();

            var wantedToRecruit = team == null || team.HatchlingHealth > 0;
            var attemptedRecruit = false;
            var succeededRecruit = false;
            var blockedReason = AiRecruitBlockedReason.None;
            var legacyCampPolicyWouldBlock = CanRecruitWithoutDiscardingComponents() == false;
            if (!wantedToRecruit)
            {
                blockedReason = AiRecruitBlockedReason.RunEnded;
            }
            else if (!recruitment.CanAffordNext)
            {
                blockedReason = AiRecruitBlockedReason.InsufficientResources;
            }
            else
            {
                // Keep all components that can be staged on the board. If the five-slot
                // camp still contains overflow after that, the next legal refresh is allowed
                // to resolve it through the existing permanent-discard rule. Otherwise the
                // old V0 guard deadlocks recruitment before it can ever draw another shovel.
                attemptedRecruit = true;
                var attempt = RecruitOrRefresh();
                succeededRecruit = attempt.Status == RecruitmentStatus.Success;
                if (!succeededRecruit)
                {
                    blockedReason = attempt.Status == RecruitmentStatus.InsufficientResources
                        ? AiRecruitBlockedReason.InsufficientResources
                        : AiRecruitBlockedReason.Other;
                }
                else if (TryUseBenchShovelImmediately())
                {
                    // A fresh recruit can contain a shovel. Use it before the camp has another
                    // chance to be overwritten, then fill the newly opened legal cell.
                    MaintainBoard();
                }
            }

            LastRecruitTelemetry = CaptureRecruitTelemetry(
                wantedToRecruit,
                attemptedRecruit,
                succeededRecruit,
                blockedReason,
                legacyCampPolicyWouldBlock);
            if (LastRecruitTelemetry.AIWantedToRecruit &&
                LastRecruitTelemetry.CanAffordRecruit &&
                LastRecruitTelemetry.LegacyCampPolicyWouldBlock)
            {
                LegacyCampPolicyBlockCount++;
            }
            UpdateRecruitStall(currentWave);
            LastCycleChanged = objectsBefore != destination.TotalObjectCount ||
                               linksBefore != destination.ActivePairLinkCount ||
                               openCellsBefore != board.UnlockedBattleCellCount ||
                               (team != null && resourcesBefore != team.Resources);
        }

        public void RecordWaveEnd(int wave, int kills, int leaks)
        {
            Diagnostics?.RecordWaveEnd(wave, kills, leaks);
        }

        public void RecordRunEnd(int wave, int kills, int leaks)
        {
            Diagnostics?.RecordRunEnd(wave, kills, leaks);
        }

        public int MergeAllAvailable()
        {
            var mergedCount = 0;
            while (TryFindMergePair(out var source, out var target))
            {
                var drag = new DragPlacementController(board, destination, true);
                if (!drag.BeginDrag(source.UnitId) ||
                    drag.Drop(target.Position) != DragDropStatus.Merged)
                {
                    Debug.LogError(
                        $"AI merge resolution failed Source={source.UnitId} Target={target.UnitId}");
                    break;
                }

                mergedCount++;
            }

            if (mergedCount > 0)
            {
                Debug.Log(
                    $"AIMergeCompleted Count={mergedCount} " +
                    $"CampCount={destination.CampCount} DeployedCount={destination.DeployedCount}");
            }

            return mergedCount;
        }

        public int DeployOpeningUnits(int maximumUnits)
        {
            if (maximumUnits < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumUnits));
            }

            var cards = new List<RecruitCard>();
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId) &&
                    destination.TryGetCard(runtimeId, out var card) &&
                    card.Kind == RecruitItemKind.BasicUnit)
                {
                    cards.Add(card);
                }
            }

            cards.Sort((first, second) =>
                BasicUnitCatalog.GetStats(first.ConfigId, first.Level).RangeCells.CompareTo(
                    BasicUnitCatalog.GetStats(second.ConfigId, second.Level).RangeCells));
            var battle = new List<GridPosition>();
            foreach (var position in board.GetPositions(CellType.Battle))
            {
                if (!board.IsOccupied(position))
                {
                    battle.Add(position);
                }
            }

            battle.Sort(CompareRoadProximity);

            var deployed = 0;
            for (var index = 0; index < Math.Min(maximumUnits, Math.Min(cards.Count, battle.Count)); index++)
            {
                if (board.TryGetPosition(cards[index].RuntimeId, out var origin) &&
                    board.TryMove(origin, battle[index]))
                {
                    deployed++;
                }
            }

            MergeAllAvailable();
            return deployed;
        }

        public int DeployHeroComponentsAndForm()
        {
            var deployed = FormAllAvailableRecipes();
            deployed += StageBenchComponentsForRetention();
            deployed += FormAllAvailableRecipes();
            if (deployed > 0)
            {
                Debug.Log(
                    $"AIHeroComponentsDeployed Count={deployed} " +
                    $"ActivePairLinks={destination.ActivePairLinkCount}");
            }

            return deployed;
        }

        private int StageBenchComponentsForRetention()
        {
            var deployed = 0;
            var components = new List<RecruitCard>();
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId) &&
                    destination.TryGetCard(runtimeId, out var card) &&
                    card.Kind == RecruitItemKind.HeroComponent)
                {
                    components.Add(card);
                }
            }

            components.Sort((first, second) =>
            {
                var unique = second.IsUnique.CompareTo(first.IsUnique);
                return unique != 0 ? unique : string.CompareOrdinal(first.RuntimeId, second.RuntimeId);
            });
            foreach (var component in components)
            {
                if (!TryFindComponentParkingBattle(out var target) &&
                    !TryFindBasicUnitSwapBattle(out target))
                {
                    continue;
                }

                if (!TryMoveUnpairedComponent(component, target))
                {
                    continue;
                }

                deployed++;
                destination.TryResolvePostDrop(component.RuntimeId);
            }

            return deployed;
        }

        private int FormAllAvailableRecipes()
        {
            var movedCount = 0;
            var initialStateVersion = destination.StateVersion;
            if (initialStateVersion == lastRecipeStateVersion)
            {
                return movedCount;
            }

            for (var pass = 0; pass < 24; pass++)
            {
                var stateVersion = pass == 0 ? initialStateVersion : destination.StateVersion;
                if (stateVersion == lastRecipeStateVersion)
                {
                    break;
                }

                lastRecipeStateVersion = stateVersion;
                var candidates = GetAvailableRecipeCandidates();
                RefreshRecipeAvailability(candidates);
                var activeKeys = new HashSet<string>(StringComparer.Ordinal);
                var formed = false;
                foreach (var candidate in candidates)
                {
                    var key = BuildRecipeKey(candidate);
                    activeKeys.Add(key);
                    var pending = GetOrCreatePendingRecipe(candidate, key);
                    if (!TryFindRecipeFormation(candidate, out var firstTarget, out var secondTarget, out var blockedReason))
                    {
                        RecordRecipeFailure(pending, blockedReason);
                        continue;
                    }

                    RecipeFormationAttempted++;
                    if (pending.RetryCount > 0)
                    {
                        RecipeRetryCount++;
                    }

                    if (TryArrangeRecipe(candidate, firstTarget, secondTarget, out var moves))
                    {
                        RecipeFormationSucceeded++;
                        pendingFormableRecipes.Remove(key);
                        movedCount += moves;
                        lastRecipeStateVersion = -1;
                        formed = true;
                        break;
                    }

                    RecordRecipeFailure(pending, AiRecipeBlockedReason.Other);
                }

                RemoveResolvedOrUnavailablePendingRecipes(activeKeys);
                if (!formed)
                {
                    break;
                }
            }

            return movedCount;
        }

        private List<HeroRecipeCandidate> GetAvailableRecipeCandidates()
        {
            var components = new List<RecruitCard>();
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind == RecruitItemKind.HeroComponent &&
                    !destination.TryGetPairLinkForComponent(card.RuntimeId, out _))
                {
                    components.Add(card);
                }
            }

            components.Sort((first, second) => string.CompareOrdinal(first.RuntimeId, second.RuntimeId));
            var candidates = new List<HeroRecipeCandidate>();
            for (var firstIndex = 0; firstIndex < components.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < components.Count; secondIndex++)
                {
                    var first = components[firstIndex];
                    var second = components[secondIndex];
                    if (HeroSliceCatalog.TryGetRecipeDefinition(first.ConfigId, second.ConfigId, out var recipe))
                    {
                        candidates.Add(new HeroRecipeCandidate(recipe, first, second));
                    }
                }
            }

            candidates.Sort((first, second) =>
            {
                var recipe = string.CompareOrdinal(first.Recipe.RecipeId, second.Recipe.RecipeId);
                return recipe != 0
                    ? recipe
                    : string.CompareOrdinal(first.First.RuntimeId, second.First.RuntimeId);
            });
            return candidates;
        }

        private void RefreshRecipeAvailability(IReadOnlyList<HeroRecipeCandidate> candidates)
        {
            AvailableRecipePairCount = candidates.Count;
            FormableRecipeCount = 0;
            BlockedRecipeCount = 0;
            foreach (var candidate in candidates)
            {
                if (TryFindRecipeFormation(candidate, out _, out _, out _))
                {
                    FormableRecipeCount++;
                }
                else
                {
                    BlockedRecipeCount++;
                }
            }
        }

        private bool TryFindRecipeFormation(
            HeroRecipeCandidate candidate,
            out GridPosition firstTarget,
            out GridPosition secondTarget,
            out AiRecipeBlockedReason blockedReason)
        {
            firstTarget = default;
            secondTarget = default;
            var foundAdjacentCells = false;
            foreach (var candidateFirstTarget in GetSideLocalBattlePositions())
            {
                if (!TryGetRequiredPositionForComponent(
                        candidate.Recipe,
                        candidate.First.ConfigId,
                        candidateFirstTarget,
                        candidate.Second.ConfigId,
                        out var candidateSecondTarget) ||
                    !IsEmptyOrBattleCell(candidateSecondTarget))
                {
                    continue;
                }

                foundAdjacentCells = true;
                if (!IsRecipeSlotUsable(candidate.First, candidate.Second.RuntimeId, candidateFirstTarget) ||
                    !IsRecipeSlotUsable(candidate.Second, candidate.First.RuntimeId, candidateSecondTarget))
                {
                    continue;
                }

                firstTarget = candidateFirstTarget;
                secondTarget = candidateSecondTarget;
                blockedReason = AiRecipeBlockedReason.None;
                return true;
            }

            blockedReason = foundAdjacentCells
                ? AiRecipeBlockedReason.BoardCapacity
                : AiRecipeBlockedReason.NoLegalAdjacentCells;
            return false;
        }

        private bool IsEmptyOrBattleCell(GridPosition position)
        {
            return board.TryGetCellType(position, out var type) && type == CellType.Battle;
        }

        private bool IsRecipeSlotUsable(RecruitCard component, string otherComponentRuntimeId, GridPosition target)
        {
            if (!IsEmptyOrBattleCell(target))
            {
                return false;
            }

            if (!board.TryGetOccupant(target, out var runtimeId))
            {
                return true;
            }

            if (string.Equals(runtimeId, component.RuntimeId, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(runtimeId, otherComponentRuntimeId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!destination.TryGetCard(runtimeId, out var occupant))
            {
                return false;
            }

            if (occupant.Kind == RecruitItemKind.BasicUnit)
            {
                return true;
            }

            return occupant.Kind == RecruitItemKind.HeroComponent &&
                   !destination.TryGetPairLinkForComponent(runtimeId, out _);
        }

        private bool TryArrangeRecipe(
            HeroRecipeCandidate candidate,
            GridPosition firstTarget,
            GridPosition secondTarget,
            out int movedCount)
        {
            movedCount = 0;
            if (!TryMoveUnpairedComponent(candidate.First, firstTarget, out var firstMoved))
            {
                return false;
            }

            if (!TryMoveUnpairedComponent(candidate.Second, secondTarget, out var secondMoved))
            {
                return false;
            }

            movedCount = (firstMoved ? 1 : 0) + (secondMoved ? 1 : 0);
            destination.TryResolvePostDrop(candidate.First.RuntimeId);
            destination.TryResolvePostDrop(candidate.Second.RuntimeId);
            return destination.TryGetPairLinkForComponent(candidate.First.RuntimeId, out var link) &&
                   (string.Equals(link.ComponentAId, candidate.Second.RuntimeId, StringComparison.Ordinal) ||
                    string.Equals(link.ComponentBId, candidate.Second.RuntimeId, StringComparison.Ordinal));
        }

        private bool TryMoveUnpairedComponent(RecruitCard component, GridPosition target)
        {
            return TryMoveUnpairedComponent(component, target, out _);
        }

        private bool TryMoveUnpairedComponent(RecruitCard component, GridPosition target, out bool moved)
        {
            moved = false;
            if (component.Kind != RecruitItemKind.HeroComponent ||
                destination.TryGetPairLinkForComponent(component.RuntimeId, out _) ||
                !board.TryGetPosition(component.RuntimeId, out var origin))
            {
                return false;
            }

            if (origin == target)
            {
                return true;
            }

            if (!board.TryGetOccupant(target, out var targetRuntimeId))
            {
                moved = board.TryMove(origin, target);
                return moved;
            }

            if (!destination.TryGetCard(targetRuntimeId, out var targetCard) ||
                (targetCard.Kind != RecruitItemKind.BasicUnit &&
                 (targetCard.Kind != RecruitItemKind.HeroComponent ||
                  destination.TryGetPairLinkForComponent(targetRuntimeId, out _))))
            {
                return false;
            }

            var status = TryAdjust(component.RuntimeId, target);
            moved = status == DragDropStatus.Moved || status == DragDropStatus.Swapped;
            return moved;
        }

        private bool TryFindBasicUnitSwapBattle(out GridPosition target)
        {
            var candidates = new List<BoardOccupant>();
            foreach (var occupant in GetSideLocalOccupants())
            {
                if (board.TryGetCellType(occupant.Position, out var cellType) &&
                    cellType == CellType.Battle &&
                    destination.TryGetCard(occupant.UnitId, out var card) &&
                    card.Kind == RecruitItemKind.BasicUnit)
                {
                    candidates.Add(occupant);
                }
            }

            candidates.Sort((first, second) =>
            {
                destination.TryGetCard(first.UnitId, out var firstCard);
                destination.TryGetCard(second.UnitId, out var secondCard);
                var level = firstCard.Level.CompareTo(secondCard.Level);
                if (level != 0)
                {
                    return level;
                }

                var road = CompareRoadProximity(second.Position, first.Position);
                return road != 0 ? road : CompareSideLocalPosition(first.Position, second.Position);
            });

            if (candidates.Count == 0)
            {
                target = default;
                return false;
            }

            target = candidates[0].Position;
            return true;
        }

        private PendingFormableRecipe GetOrCreatePendingRecipe(HeroRecipeCandidate candidate, string key)
        {
            if (pendingFormableRecipes.TryGetValue(key, out var pending))
            {
                return pending;
            }

            pending = new PendingFormableRecipe(
                key,
                candidate.Recipe.RecipeId,
                candidate.First.RuntimeId,
                candidate.Second.RuntimeId);
            pendingFormableRecipes.Add(key, pending);
            RecipeOpportunityCreated++;
            return pending;
        }

        private void RecordRecipeFailure(PendingFormableRecipe pending, AiRecipeBlockedReason reason)
        {
            pending.RetryCount++;
            pending.LastFailureReason = reason;
            RecipeFormationFailed++;
            recipeFailureCounts.TryGetValue(reason, out var count);
            recipeFailureCounts[reason] = count + 1;
        }

        private void RemoveResolvedOrUnavailablePendingRecipes(ISet<string> activeKeys)
        {
            var stale = new List<string>();
            foreach (var pair in pendingFormableRecipes)
            {
                if (!activeKeys.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var key in stale)
            {
                pendingFormableRecipes.Remove(key);
            }
        }

        private static string BuildRecipeKey(HeroRecipeCandidate candidate)
        {
            return candidate.Recipe.RecipeId + "|" + candidate.First.RuntimeId + "|" + candidate.Second.RuntimeId;
        }

        private void MaintainBoard()
        {
            MergeAllAvailable();
            // Finite components are the only route to heroes, so retain and form them before
            // basic deployment consumes every battle cell.
            DeployHeroComponentsAndForm();
            if (HasBenchHeroComponent() && board.FreeBattleCellCount == 0 &&
                TryUseBenchShovelForCapacity())
            {
                DeployHeroComponentsAndForm();
            }
            DeployOpeningUnits(int.MaxValue);
            MergeAllAvailable();
            DeployHeroComponentsAndForm();
            MergeAllAvailable();
        }

        private bool HasBenchHeroComponent()
        {
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId) &&
                    destination.TryGetCard(runtimeId, out var card) &&
                    card.Kind == RecruitItemKind.HeroComponent)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryMoveBasicUnitToEmptyBenchSlot()
        {
            GridPosition emptyBench = default;
            var foundEmptyBench = false;
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (!board.IsOccupied(position))
                {
                    emptyBench = position;
                    foundEmptyBench = true;
                    break;
                }
            }

            if (!foundEmptyBench)
            {
                return false;
            }

            var candidates = new List<BoardOccupant>();
            foreach (var occupant in board.GetOccupants())
            {
                if (board.TryGetCellType(occupant.Position, out var cellType) &&
                    cellType == CellType.Battle &&
                    destination.TryGetCard(occupant.UnitId, out var card) &&
                    card.Kind == RecruitItemKind.BasicUnit)
                {
                    candidates.Add(occupant);
                }
            }

            candidates.Sort((first, second) =>
            {
                destination.TryGetCard(first.UnitId, out var firstCard);
                destination.TryGetCard(second.UnitId, out var secondCard);
                var level = firstCard.Level.CompareTo(secondCard.Level);
                if (level != 0)
                {
                    return level;
                }

                var range = BasicUnitCatalog.GetStats(firstCard.ConfigId, firstCard.Level).RangeCells.CompareTo(
                    BasicUnitCatalog.GetStats(secondCard.ConfigId, secondCard.Level).RangeCells);
                return range != 0 ? range : first.Position.CompareTo(second.Position);
            });

            return candidates.Count > 0 && board.TryMove(candidates[0].Position, emptyBench);
        }

        private bool CanRecruitWithoutDiscardingComponents()
        {
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (board.TryGetOccupant(position, out var runtimeId) &&
                    destination.TryGetCard(runtimeId, out var card) &&
                    card.Kind == RecruitItemKind.HeroComponent)
                {
                    return false;
                }
            }

            return true;
        }

        private AiRecruitTelemetry CaptureRecruitTelemetry(
            bool wantedToRecruit,
            bool attemptedRecruit,
            bool succeededRecruit,
            AiRecruitBlockedReason blockedReason,
            bool legacyCampPolicyWouldBlock)
        {
            var unpairedComponents = 0;
            foreach (var card in destination.GetBoardCards())
            {
                if (card.Kind == RecruitItemKind.HeroComponent &&
                    !destination.TryGetPairLinkForComponent(card.RuntimeId, out _))
                {
                    unpairedComponents++;
                }
            }

            var campCount = destination.CampCount;
            return new AiRecruitTelemetry(
                team?.Resources ?? 0,
                recruitment.NextCost,
                recruitment.CanAffordNext,
                recruitment.CompletedRecruitments,
                campCount,
                campCount,
                destination.DeployedCount,
                board.UnlockedBattleCellCount,
                unpairedComponents,
                destination.ActivePairLinkCount,
                wantedToRecruit,
                attemptedRecruit,
                succeededRecruit,
                blockedReason,
                legacyCampPolicyWouldBlock);
        }

        private void UpdateRecruitStall(int currentWave)
        {
            var telemetry = LastRecruitTelemetry;
            var isStalled = telemetry.CanAffordRecruit &&
                            telemetry.AIWantedToRecruit &&
                            !telemetry.AIRecruitSucceeded &&
                            telemetry.RecruitBlockedReason != AiRecruitBlockedReason.None;
            if (!isStalled)
            {
                activeStallReason = AiRecruitBlockedReason.None;
                return;
            }

            if (activeStallReason == telemetry.RecruitBlockedReason)
            {
                return;
            }

            activeStallReason = telemetry.RecruitBlockedReason;
            RecruitStallCount++;
            recruitStallCounts.TryGetValue(telemetry.RecruitBlockedReason, out var reasonCount);
            recruitStallCounts[telemetry.RecruitBlockedReason] = reasonCount + 1;
            if (FirstRecruitStallWave < 0 && currentWave > 0)
            {
                FirstRecruitStallWave = currentWave;
            }

            Debug.Log(
                $"AI_RECRUIT_STALL Side={Side} Wave={currentWave} Reason={telemetry.RecruitBlockedReason} " +
                $"Resources={telemetry.CurrentResources} NextCost={telemetry.NextRecruitCost} " +
                $"Camp={telemetry.CampOccupiedSlotCount} Board={telemetry.BoardOccupiedSlotCount}/" +
                $"{telemetry.BoardOpenCellCount} UnpairedComponents={telemetry.UnpairedComponentCount}");
        }

        private bool TryUseBenchShovelForCapacity()
        {
            if (shovelUnlocks == null ||
                board.FreeBattleCellCount > 0 ||
                !HasBenchDeployableCard() ||
                board.GetPositions(CellType.Locked).Count == 0 ||
                !TryUseBenchShovelImmediately())
            {
                return false;
            }

            return true;
        }

        private bool TryUseBenchShovelImmediately()
        {
            if (shovelUnlocks == null ||
                shovelUnlocks.AvailableShovelCount == 0 ||
                board.GetPositions(CellType.Locked).Count == 0 ||
                !shovelUnlocks.BeginSelection())
            {
                return false;
            }

            var locked = new List<GridPosition>(board.GetPositions(CellType.Locked));
            locked.Sort(CompareRoadProximity);
            foreach (var candidate in locked)
            {
                if (shovelUnlocks.TryUnlockCell(candidate))
                {
                    Debug.Log($"AIShovelUnlocked Side={board.Side} Cell={candidate}");
                    return true;
                }
            }

            shovelUnlocks.CancelSelection();
            return false;
        }

        private bool HasBenchDeployableCard()
        {
            foreach (var position in board.GetPositions(CellType.Bench))
            {
                if (!board.TryGetOccupant(position, out var runtimeId) ||
                    !destination.TryGetCard(runtimeId, out var card))
                {
                    continue;
                }

                if (card.Kind == RecruitItemKind.BasicUnit || card.Kind == RecruitItemKind.HeroComponent)
                {
                    return true;
                }
            }

            return false;
        }

        public DragDropStatus TryAdjust(string runtimeId, GridPosition target)
        {
            var drag = new DragPlacementController(board, destination, true);
            return drag.BeginDrag(runtimeId) ? drag.Drop(target) : DragDropStatus.Reverted;
        }

        private bool TryFindMergePair(out BoardOccupant source, out BoardOccupant target)
        {
            var occupants = GetSideLocalOccupants();
            for (var firstIndex = 0; firstIndex < occupants.Count; firstIndex++)
            {
                var first = occupants[firstIndex];
                if (!board.TryGetCellType(first.Position, out var firstType) ||
                    firstType != CellType.Battle ||
                    !destination.TryGetCard(first.UnitId, out var firstCard) ||
                    firstCard.Level >= BasicUnitCatalog.MaxLevel)
                {
                    continue;
                }

                for (var secondIndex = firstIndex + 1; secondIndex < occupants.Count; secondIndex++)
                {
                    var second = occupants[secondIndex];
                    if (!board.TryGetCellType(second.Position, out var secondType) ||
                        secondType != CellType.Battle ||
                        !destination.TryGetCard(second.UnitId, out var secondCard) ||
                        !firstCard.IsSameBasicUnitAndLevel(secondCard))
                    {
                        continue;
                    }

                    if (CompareMergeTarget(first, second) <= 0)
                    {
                        source = second;
                        target = first;
                    }
                    else
                    {
                        source = first;
                        target = second;
                    }

                    return true;
                }
            }

            source = default;
            target = default;
            return false;
        }

        private bool TryFindHeroComponentTarget(RecruitCard component, out GridPosition target)
        {
            foreach (var occupant in GetSideLocalOccupants())
            {
                if (!destination.TryGetCard(occupant.UnitId, out var candidate) ||
                    candidate.Kind != RecruitItemKind.HeroComponent ||
                    !destination.TryGetComponent(occupant.UnitId, out var candidateRuntime) ||
                    !string.IsNullOrEmpty(candidateRuntime.PairLinkId) ||
                    !HeroSliceCatalog.TryGetRecipeDefinition(component.ConfigId, candidate.ConfigId, out var recipe) ||
                    !board.TryGetCellType(occupant.Position, out var cellType) ||
                    cellType != CellType.Battle)
                {
                    continue;
                }

                if (TryGetRequiredPositionForComponent(
                        recipe,
                        candidate.ConfigId,
                        occupant.Position,
                        component.ConfigId,
                        out var requiredPosition) &&
                    IsEmptyBattleCell(requiredPosition))
                {
                    target = requiredPosition;
                    return true;
                }

                if (TryMoveCandidateIntoConfiguredFormation(
                        component,
                        candidate,
                        occupant.Position,
                        recipe,
                        out target))
                {
                    return true;
                }
            }

            target = default;
            return false;
        }

        private bool TryMoveCandidateIntoConfiguredFormation(
            RecruitCard movingComponent,
            RecruitCard candidate,
            GridPosition candidatePosition,
            HeroRecipeDefinition recipe,
            out GridPosition target)
        {
            foreach (var possibleTarget in GetSideLocalBattlePositions())
            {
                if (board.IsOccupied(possibleTarget) ||
                    !TryGetRequiredPositionForComponent(
                        recipe,
                        movingComponent.ConfigId,
                        possibleTarget,
                        candidate.ConfigId,
                        out var candidateTarget) ||
                    !IsEmptyBattleCell(candidateTarget))
                {
                    continue;
                }

                if (!board.TryMove(candidatePosition, candidateTarget))
                {
                    continue;
                }

                destination.TryResolvePostDrop(candidate.RuntimeId);
                target = possibleTarget;
                return true;
            }

            target = default;
            return false;
        }

        private bool IsEmptyBattleCell(GridPosition position)
        {
            return board.TryGetCellType(position, out var cellType) &&
                   cellType == CellType.Battle &&
                   !board.IsOccupied(position);
        }

        private bool TryFindComponentParkingBattle(out GridPosition target)
        {
            var battle = new List<GridPosition>(GetSideLocalBattlePositions());
            battle.Sort((first, second) =>
            {
                var distance = GetLaneDistance(second).CompareTo(GetLaneDistance(first));
                return distance != 0 ? distance : CompareSideLocalPosition(first, second);
            });
            foreach (var position in battle)
            {
                if (!board.IsOccupied(position))
                {
                    target = position;
                    return true;
                }
            }

            target = default;
            return false;
        }

        private int CompareMergeTarget(BoardOccupant first, BoardOccupant second)
        {
            board.TryGetCellType(first.Position, out var firstType);
            board.TryGetCellType(second.Position, out var secondType);
            var firstDeployed = firstType == CellType.Battle;
            var secondDeployed = secondType == CellType.Battle;
            if (firstDeployed != secondDeployed)
            {
                return firstDeployed ? -1 : 1;
            }

            if (firstDeployed)
            {
                var laneDistance = GetLaneDistance(first.Position).CompareTo(GetLaneDistance(second.Position));
                if (laneDistance != 0)
                {
                    return laneDistance;
                }
            }

            return CompareSideLocalPosition(first.Position, second.Position);
        }

        private int CompareRoadProximity(GridPosition first, GridPosition second)
        {
            var distance = GetLaneDistance(first).CompareTo(GetLaneDistance(second));
            return distance != 0 ? distance : CompareSideLocalPosition(first, second);
        }

        private IReadOnlyList<GridPosition> GetSideLocalBattlePositions()
        {
            var positions = new List<GridPosition>(board.GetPositions(CellType.Battle));
            positions.Sort(CompareSideLocalPosition);
            return positions;
        }

        private IReadOnlyList<BoardOccupant> GetSideLocalOccupants()
        {
            var occupants = new List<BoardOccupant>(board.GetOccupants());
            occupants.Sort((first, second) =>
            {
                var position = CompareSideLocalPosition(first.Position, second.Position);
                return position != 0
                    ? position
                    : string.CompareOrdinal(first.UnitId, second.UnitId);
            });
            return occupants;
        }

        private int CompareSideLocalPosition(GridPosition first, GridPosition second)
        {
            return ToSideLocalPosition(first).CompareTo(ToSideLocalPosition(second));
        }

        private GridPosition ToSideLocalPosition(GridPosition position)
        {
            if (board.Side == TeamSide.Player || board.Layout == null)
            {
                return position;
            }

            if (board.TryGetCellType(position, out var cellType) && cellType != CellType.Battle)
            {
                return position;
            }

            if (board.Layout is FixedBoardLayoutDefinition fixedLayout &&
                fixedLayout.IsOwnedDeploymentCell(position, TeamSide.AI))
            {
                return fixedLayout.GetFairCounterpart(position, TeamSide.AI);
            }

            return board.Layout.GetFairCounterpart(position, TeamSide.AI);
        }

        private GridPosition FromSideLocalPosition(GridPosition position)
        {
            if (board.Side == TeamSide.Player || board.Layout == null)
            {
                return position;
            }

            if (board.Layout is FixedBoardLayoutDefinition)
            {
                // Recipe rules operate in Player-local coordinates and may
                // temporarily produce an adjacent coordinate outside that
                // side's deployment mask. Keep conversion total; board
                // legality is checked by the caller afterwards.
                return new GridPosition(
                    FixedBoardLayoutDefinition.FixedColumns - 1 - position.X,
                    FixedBoardLayoutDefinition.FixedRows - 1 - position.Y);
            }

            return board.Layout.GetFairCounterpart(position, TeamSide.Player);
        }

        private bool TryGetRequiredPositionForComponent(
            HeroRecipeDefinition recipe,
            string fixedComponentId,
            GridPosition fixedPosition,
            string movingComponentId,
            out GridPosition requiredPosition)
        {
            var localFixedPosition = ToSideLocalPosition(fixedPosition);
            if (!recipe.TryGetRequiredPositionForComponent(
                    fixedComponentId,
                    localFixedPosition,
                    movingComponentId,
                    out var localRequiredPosition))
            {
                requiredPosition = default;
                return false;
            }

            requiredPosition = FromSideLocalPosition(localRequiredPosition);
            return true;
        }

        private float GetLaneDistance(GridPosition position)
        {
            return board.GetLaneDistance(position);
        }

        private readonly struct HeroRecipeCandidate
        {
            public HeroRecipeCandidate(HeroRecipeDefinition recipe, RecruitCard first, RecruitCard second)
            {
                Recipe = recipe;
                First = first;
                Second = second;
            }

            public HeroRecipeDefinition Recipe { get; }
            public RecruitCard First { get; }
            public RecruitCard Second { get; }
        }
    }
}
