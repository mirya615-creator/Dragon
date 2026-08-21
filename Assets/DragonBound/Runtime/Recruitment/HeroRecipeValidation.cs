using System;
using System.Collections.Generic;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using UnityEngine;

namespace DragonBound.Recruitment
{
    /// <summary>
    /// Development-only audit of the formal recipe and PairLink seam. It creates an isolated
    /// temporary board and never touches a live RecruitDeck or the 24-instance bag.
    /// </summary>
    public sealed class HeroRecipeValidationResult
    {
        internal HeroRecipeValidationResult(
            string heroId,
            string componentA,
            string componentB,
            string direction,
            string recipeId,
            string executor,
            bool registered,
            bool pairLinkTest,
            bool wrongDirectionRejected,
            bool missingComponentRejected,
            bool pairBreaksAndReforms)
        {
            HeroId = heroId;
            ComponentA = componentA;
            ComponentB = componentB;
            Direction = direction;
            RecipeId = recipeId;
            Executor = executor;
            Registered = registered;
            PairLinkTest = pairLinkTest;
            WrongDirectionRejected = wrongDirectionRejected;
            MissingComponentRejected = missingComponentRejected;
            PairBreaksAndReforms = pairBreaksAndReforms;
        }

        public string HeroId { get; }
        public string ComponentA { get; }
        public string ComponentB { get; }
        public string Direction { get; }
        public string RecipeId { get; }
        public string Executor { get; }
        public bool Registered { get; }
        public bool PairLinkTest { get; }
        public bool WrongDirectionRejected { get; }
        public bool MissingComponentRejected { get; }
        public bool PairBreaksAndReforms { get; }
        public bool Passed => Registered && PairLinkTest && WrongDirectionRejected &&
                               MissingComponentRejected && PairBreaksAndReforms;
    }

    public sealed class HeroRecipeBagCoverageReport
    {
        internal HeroRecipeBagCoverageReport(int sampleCount, int fullyCoveredCount, IReadOnlyList<string> missing)
        {
            SampleCount = sampleCount;
            FullyCoveredCount = fullyCoveredCount;
            MissingHeroIds = missing;
        }

        public int SampleCount { get; }
        public int FullyCoveredCount { get; }
        public int IncompleteCount => SampleCount - FullyCoveredCount;
        public IReadOnlyList<string> MissingHeroIds { get; }
        public bool Passed => IncompleteCount == 0;
    }

    public static class HeroRecipeValidation
    {
        public static bool IsAvailable => Application.isEditor || Debug.isDebugBuild;

        public static IReadOnlyList<HeroRecipeValidationResult> ValidateAll()
        {
            EnsureAvailable();
            var results = new List<HeroRecipeValidationResult>(HeroRecipeCatalog.Definitions.Count);
            foreach (var recipe in HeroRecipeCatalog.Definitions)
            {
                results.Add(Validate(recipe.HeroId));
            }

            return results.AsReadOnly();
        }

        public static HeroRecipeValidationResult Validate(string heroId)
        {
            EnsureAvailable();
            var recipe = HeroRecipeCatalog.Get(heroId);
            var metadata = HeroDefinitionCatalog.GetMetadata(recipe.HeroId);
            var registered = GreyboxRecruitmentCatalog.Create().GetRecipe(recipe.HeroId).RecipeId == recipe.RecipeId &&
                             metadata.RuntimeCombatState == HeroRuntimeCombatState.Implemented;

            var valid = CreateScenario(recipe, "valid");
            var validPair = Form(valid, recipe);
            var executor = validPair?.CombatProxy?.GetType().Name ?? "NONE";
            var pairLinkTest = validPair != null &&
                               string.Equals(validPair.HeroId, recipe.HeroId, StringComparison.Ordinal) &&
                               string.Equals(validPair.RecipeId, recipe.RecipeId, StringComparison.Ordinal) &&
                               validPair.CombatProxy != null &&
                               validPair.CombatProxy.Definition.Id == recipe.HeroId &&
                               validPair.CombatProxy.TickFormation(HeroCombatState.FormationDurationSeconds + 0.01f) &&
                               validPair.CombatProxy.IsFormationComplete;

            var wrongDirectionRejected = ValidateWrongDirection(recipe);
            var missingComponentRejected = ValidateMissingComponent(recipe);
            var pairBreaksAndReforms = ValidateBreakAndReform(recipe);

            return new HeroRecipeValidationResult(
                recipe.HeroId,
                recipe.ComponentAId,
                recipe.ComponentBId,
                recipe.FormationRule,
                recipe.RecipeId,
                executor,
                registered,
                pairLinkTest,
                wrongDirectionRejected,
                missingComponentRejected,
                pairBreaksAndReforms);
        }

        public static HeroRecipeBagCoverageReport AuditNormalRunSeeds(int firstRunSeed, int sampleCount)
        {
            EnsureAvailable();
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            var catalog = GreyboxRecruitmentCatalog.Create();
            var missing = new List<string>();
            var fullyCovered = 0;
            for (var offset = 0; offset < sampleCount; offset++)
            {
                var seed = unchecked(firstRunSeed + offset);
                var bag = LimitedComponentBag.CreateBag(
                    seed,
                    LimitedComponentBag.DefaultContentVersion,
                    catalog);
                var deck = new RecruitDeck(catalog, seed, "recipe-audit." + seed, bag);
                var delivered = new HashSet<string>(StringComparer.Ordinal);
                for (var recruit = 0; recruit < 11; recruit++)
                {
                    foreach (var card in deck.DrawNext().Cards)
                    {
                        if (card.Kind == RecruitItemKind.HeroComponent)
                        {
                            delivered.Add(card.ConfigId);
                        }
                    }
                }

                var seedComplete = true;
                foreach (var recipe in catalog.Recipes)
                {
                    if (!delivered.Contains(recipe.ComponentAId) || !delivered.Contains(recipe.ComponentBId))
                    {
                        seedComplete = false;
                        missing.Add($"{seed}:{recipe.HeroId}");
                    }
                }

                if (seedComplete)
                {
                    fullyCovered++;
                }
            }

            return new HeroRecipeBagCoverageReport(sampleCount, fullyCovered, missing.AsReadOnly());
        }

        private static ValidationScenario CreateScenario(HeroRecipeDefinition recipe, string prefix)
        {
            var board = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            var destination = new BoardRecruitDestination(board);
            var batch = DragonRouteHeroDevelopmentFactory.CreateBatch(
                recipe.HeroId,
                "recipe-validation." + prefix + "." + recipe.HeroId);
            destination.Commit(RecruitDestinationPlan.AddToEmptySlots, batch);
            return new ValidationScenario(board, destination, batch.Cards[0].RuntimeId, batch.Cards[1].RuntimeId);
        }

        private static HeroPairLink Form(ValidationScenario scenario, HeroRecipeDefinition recipe)
        {
            if (!TryFindFormation(scenario.Board, recipe, out var first, out var second) ||
                !Move(scenario, scenario.FirstRuntimeId, first) ||
                !Move(scenario, scenario.SecondRuntimeId, second))
            {
                return null;
            }

            scenario.Destination.TryResolvePostDrop(scenario.SecondRuntimeId);
            return scenario.Destination.TryGetPairLinkForComponent(
                scenario.FirstRuntimeId,
                out var pairLink)
                ? pairLink
                : null;
        }

        private static bool ValidateWrongDirection(HeroRecipeDefinition recipe)
        {
            var scenario = CreateScenario(recipe, "wrong");
            if (!TryFindFormation(scenario.Board, recipe, out var first, out var second) ||
                !Move(scenario, scenario.FirstRuntimeId, second) ||
                !Move(scenario, scenario.SecondRuntimeId, first))
            {
                return false;
            }

            scenario.Destination.TryResolvePostDrop(scenario.SecondRuntimeId);
            return scenario.Destination.ActivePairLinkCount == 0;
        }

        private static bool ValidateMissingComponent(HeroRecipeDefinition recipe)
        {
            var board = DragonBoundBoardLayout.CreateDefault(TeamSide.Player);
            var destination = new BoardRecruitDestination(board);
            var batch = DragonRouteHeroDevelopmentFactory.CreateBatch(
                recipe.HeroId,
                "recipe-validation.missing." + recipe.HeroId);
            var cards = new List<RecruitCard>
            {
                batch.Cards[0],
                new RecruitCard("missing.basic.1", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty),
                new RecruitCard("missing.basic.2", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty),
                new RecruitCard("missing.basic.3", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty),
                new RecruitCard("missing.basic.4", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty)
            };
            destination.Commit(RecruitDestinationPlan.AddToEmptySlots, new RecruitBatch(1, cards));
            var battle = board.GetPositions(CellType.Battle)[0];
            if (!board.TryGetPosition(batch.Cards[0].RuntimeId, out var bench) || !board.TryMove(bench, battle))
            {
                return false;
            }

            destination.TryResolvePostDrop(batch.Cards[0].RuntimeId);
            return destination.ActivePairLinkCount == 0;
        }

        private static bool ValidateBreakAndReform(HeroRecipeDefinition recipe)
        {
            var scenario = CreateScenario(recipe, "reform");
            var pair = Form(scenario, recipe);
            if (pair == null)
            {
                return false;
            }

            var bench = scenario.Board.GetPositions(CellType.Bench)[0];
            var drag = new DragPlacementController(scenario.Board, scenario.Destination, true);
            if (!drag.BeginDrag(scenario.FirstRuntimeId) || drag.Drop(bench) != DragDropStatus.Moved ||
                scenario.Destination.ActivePairLinkCount != 0)
            {
                return false;
            }

            if (!TryFindFormation(scenario.Board, recipe, out var first, out var second) ||
                !Move(scenario, scenario.FirstRuntimeId, first))
            {
                return false;
            }

            scenario.Destination.TryResolvePostDrop(scenario.FirstRuntimeId);
            return scenario.Destination.ActivePairLinkCount == 1 &&
                   scenario.Destination.GetActiveHeroPairs()[0].PairLink.HeroId == recipe.HeroId;
        }

        private static bool Move(ValidationScenario scenario, string runtimeId, GridPosition target)
        {
            return scenario.Board.TryGetPosition(runtimeId, out var origin) &&
                   scenario.Board.TryMove(origin, target);
        }

        private static bool TryFindFormation(
            BoardGrid board,
            HeroRecipeDefinition recipe,
            out GridPosition firstCell,
            out GridPosition secondCell)
        {
            foreach (var first in board.GetPositions(CellType.Battle))
            {
                foreach (var second in board.GetPositions(CellType.Battle))
                {
                    if (first == second ||
                        !recipe.MatchesFormation(recipe.ComponentAId, first, recipe.ComponentBId, second))
                    {
                        continue;
                    }

                    firstCell = first;
                    secondCell = second;
                    return true;
                }
            }

            firstCell = default;
            secondCell = default;
            return false;
        }

        private static void EnsureAvailable()
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException("Hero recipe validation is development-only.");
            }
        }

        private sealed class ValidationScenario
        {
            public ValidationScenario(
                BoardGrid board,
                BoardRecruitDestination destination,
                string firstRuntimeId,
                string secondRuntimeId)
            {
                Board = board;
                Destination = destination;
                FirstRuntimeId = firstRuntimeId;
                SecondRuntimeId = secondRuntimeId;
            }

            public BoardGrid Board { get; }
            public BoardRecruitDestination Destination { get; }
            public string FirstRuntimeId { get; }
            public string SecondRuntimeId { get; }
        }

    }
}
