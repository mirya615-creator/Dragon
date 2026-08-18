using System;
using System.Collections.Generic;
using DragonBound.Grid;
using UnityEngine;

namespace DragonBound.Recruitment
{
    // Development-only test input. It never draws from, returns to, or changes the formal deck.
    public static class DragonRouteHeroDevelopmentFactory
    {
        private static readonly string[] FillerBasicIds =
        {
            "basic.axe_raider",
            "basic.longbow_hunter",
            "basic.spear_raider"
        };

        public static bool IsAvailable => Application.isEditor || Debug.isDebugBuild;

        public static RecruitBatch CreateBatch(string heroId, string runtimePrefix)
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException("Dragon route development input is unavailable in this build.");
            }

            if (string.IsNullOrWhiteSpace(runtimePrefix))
            {
                throw new ArgumentException("A runtime prefix is required.", nameof(runtimePrefix));
            }

            var recipe = GetImplementedHeroRecipe(heroId);
            var firstComponent = GetFirstComponentId(recipe);
            var secondComponent = GetSecondComponentId(recipe);
            return new RecruitBatch(
                1,
                new RecruitCard[]
                {
                    CreateComponentCard(runtimePrefix + ".component.a", firstComponent),
                    CreateComponentCard(runtimePrefix + ".component.b", secondComponent),
                    new RecruitCard(runtimePrefix + ".basic.0", RecruitItemKind.BasicUnit, FillerBasicIds[0], string.Empty),
                    new RecruitCard(runtimePrefix + ".basic.1", RecruitItemKind.BasicUnit, FillerBasicIds[1], string.Empty),
                    new RecruitCard(runtimePrefix + ".basic.2", RecruitItemKind.BasicUnit, FillerBasicIds[2], string.Empty)
                });
        }

        public static bool TrySpawnPair(
            BoardRecruitDestination destination,
            string heroId,
            string runtimePrefix,
            out HeroPairLink pairLink)
        {
            pairLink = null;
            if (!IsAvailable || destination == null || destination.CampCount != 0)
            {
                return false;
            }

            var recipe = GetImplementedHeroRecipe(heroId);
            var batch = CreateBatch(heroId, runtimePrefix);
            if (destination.Plan(RecruitBatch.CardsPerRecruitment) != RecruitDestinationPlan.AddToEmptySlots)
            {
                return false;
            }

            destination.Commit(RecruitDestinationPlan.AddToEmptySlots, batch);
            if (!TryFindEmptyFormation(destination.Board, recipe, out var firstCell, out var secondCell))
            {
                return false;
            }

            var firstRuntimeId = batch.Cards[0].RuntimeId;
            var secondRuntimeId = batch.Cards[1].RuntimeId;
            if (!destination.Board.TryGetPosition(firstRuntimeId, out var firstBench) ||
                !destination.Board.TryGetPosition(secondRuntimeId, out var secondBench) ||
                !destination.Board.TryMove(firstBench, firstCell) ||
                !destination.Board.TryMove(secondBench, secondCell))
            {
                return false;
            }

            destination.TryResolvePostDrop(secondRuntimeId);
            return destination.TryGetPairLinkForComponent(firstRuntimeId, out pairLink);
        }

        private static HeroRecipeDefinition GetImplementedHeroRecipe(string heroId)
        {
            var recipe = HeroRecipeCatalog.Get(heroId);
            var metadata = HeroDefinitionCatalog.GetMetadata(recipe.HeroId);
            if (metadata.RuntimeCombatState != HeroRuntimeCombatState.Implemented)
            {
                throw new ArgumentException("Only implemented HeroSlice heroes are available to this development factory.", nameof(heroId));
            }

            return recipe;
        }

        private static RecruitCard CreateComponentCard(string runtimeId, string componentId)
        {
            var definition = HeroComponentCatalog.Get(componentId);
            return new RecruitCard(
                runtimeId,
                RecruitItemKind.HeroComponent,
                componentId,
                "DEV." + componentId,
                1,
                definition.IsUnique);
        }

        private static string GetFirstComponentId(HeroRecipeDefinition recipe)
        {
            return recipe.FormationOrientation == HeroFormationOrientation.Vertical
                ? recipe.TopComponentId
                : recipe.LeftComponentId;
        }

        private static string GetSecondComponentId(HeroRecipeDefinition recipe)
        {
            return recipe.FormationOrientation == HeroFormationOrientation.Vertical
                ? recipe.BottomComponentId
                : recipe.RightComponentId;
        }

        private static bool TryFindEmptyFormation(
            BoardGrid board,
            HeroRecipeDefinition recipe,
            out GridPosition firstCell,
            out GridPosition secondCell)
        {
            var battleCells = board.GetPositions(CellType.Battle);
            foreach (var first in battleCells)
            {
                if (board.IsOccupied(first))
                {
                    continue;
                }

                foreach (var second in battleCells)
                {
                    if (first.Equals(second) || board.IsOccupied(second) ||
                        !recipe.MatchesFormation(
                            GetFirstComponentId(recipe),
                            first,
                            GetSecondComponentId(recipe),
                            second))
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
    }
}
