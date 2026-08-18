using System;
using System.Linq;
using DragonBound.AI;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class HeroSliceTests
    {
        [Test]
        public void HeroComponent_CannotAttack()
        {
            Assert.IsNull(typeof(ComponentRuntime).GetMethod("Attack"));
            Assert.IsNull(typeof(ComponentRuntime).GetMethod("TickCombat"));
            Assert.IsNull(typeof(ComponentRuntime).GetProperty("CombatProxy"));
        }

        [Test]
        public void HeroComponent_HasNoRange()
        {
            Assert.IsNull(typeof(ComponentRuntime).GetProperty("Range"));
            Assert.IsNull(typeof(ComponentRuntime).GetProperty("RangeCells"));
        }

        [Test]
        public void HeroComponent_DoesNotGainExperience()
        {
            Assert.IsNull(typeof(ComponentRuntime).GetMethod("AddExperience"));
            Assert.IsNull(typeof(ComponentRuntime).GetProperty("Experience"));
            Assert.IsNull(typeof(ComponentRuntime).GetProperty("Level"));
        }

        [Test]
        public void ComponentsRemainIndependentAfterPairing()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"));
            MoveDirect(context, "sigil", new GridPosition(0, 1));

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sky", new GridPosition(0, 2)));
            var pairLink = GetOnlyPair(context);

            Assert.AreEqual(HeroSliceCatalog.WindclawRangerRecipeId, pairLink.RecipeId);
            Assert.AreEqual(HeroSliceCatalog.WindclawRangerHeroId, pairLink.HeroId);
            Assert.IsTrue(context.Destination.TryGetCard("sigil", out _));
            Assert.IsTrue(context.Destination.TryGetCard("sky", out _));
            Assert.IsTrue(context.Destination.TryGetComponent("sigil", out var sigil));
            Assert.IsTrue(context.Destination.TryGetComponent("sky", out var sky));
            Assert.AreEqual(pairLink.PairLinkId, sigil.PairLinkId);
            Assert.AreEqual(pairLink.PairLinkId, sky.PairLinkId);
        }

        [Test]
        public void PairLinkOnlyFormsWhenBothComponentsAreBattleDeployed()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"));

            Assert.IsFalse(context.Destination.TryResolvePostDrop("sigil"));
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);

            MoveDirect(context, "sigil", new GridPosition(0, 1));
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
            MoveDirect(context, "sky", new GridPosition(0, 2));
            Assert.AreEqual(1, context.Destination.ActivePairLinkCount);
        }

        [Test]
        public void PairCannotFormFromBench()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"));
            Assert.IsFalse(context.Destination.TryResolvePostDrop("sigil"));
            Assert.IsFalse(context.Destination.TryResolvePostDrop("sky"));
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);

            MoveDirect(context, "sigil", new GridPosition(0, 1));
            Assert.IsFalse(context.Destination.TryResolvePostDrop("sky"));
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
        }

        [Test]
        public void DiagonalComponents_DoNotFormHeroPairLink()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"));
            MoveDirect(context, "sigil", new GridPosition(0, 1));
            MoveDirect(context, "sky", new GridPosition(1, 2));

            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
        }

        [Test]
        public void SeparatedComponents_DoNotFormHeroPairLink()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"));
            MoveDirect(context, "sigil", new GridPosition(0, 1));
            MoveDirect(context, "sky", new GridPosition(2, 2));

            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
        }

        [Test]
        public void WrongRecipe_DoesNotFormHeroPairLink()
        {
            var context = CreateContext(Sky("sky"), Knight("knight"));
            MoveDirect(context, "sky", new GridPosition(0, 2));
            MoveDirect(context, "knight", new GridPosition(1, 1));

            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
        }

        [Test]
        public void PairLinkDoesNotOwnBoardCells()
        {
            var context = FormWindclaw();
            var pairLink = GetOnlyPair(context);

            Assert.IsTrue(context.Board.TryGetOccupant(new GridPosition(0, 1), out var first));
            Assert.IsTrue(context.Board.TryGetOccupant(new GridPosition(0, 2), out var second));
            CollectionAssert.AreEquivalent(
                new[] { pairLink.ComponentAId, pairLink.ComponentBId },
                new[] { first, second });
            Assert.IsFalse(context.Board.TryGetPosition(pairLink.PairLinkId, out _));
            Assert.AreEqual(2, context.Board.GetOccupants().Count(value =>
                pairLink.ContainsComponent(value.UnitId)));
        }

        [Test]
        public void DraggingEitherComponentBreaksPair()
        {
            foreach (var draggedComponentId in new[] { "sigil", "sky" })
            {
                var context = FormWindclaw();
                var oldPair = GetOnlyPair(context);
                oldPair.CombatProxy.TickFormation(HeroCombatState.FormationDurationSeconds);
                var registry = new EnemyRegistry();
                registry.Register(Enemy("target", EnemyArchetype.Normal, 500f, new CombatPoint(1f, 1f)));
                oldPair.CombatProxy.TickCombat(
                    1f / oldPair.CombatProxy.AttackSpeed,
                    new CombatPoint(0.5f, 1f),
                    registry);
                Assert.IsNotNull(oldPair.CombatProxy.CurrentTargetRuntimeId);

                var drag = new DragPlacementController(context.Board, context.Destination, true);
                Assert.IsTrue(drag.BeginDrag(draggedComponentId));
                Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
                Assert.IsTrue(oldPair.CombatProxy.IsCombatSuspended);
                Assert.IsNull(oldPair.CombatProxy.CurrentTargetRuntimeId);
                Assert.AreEqual(1, oldPair.CombatProxy.AttackNumber);
                drag.Cancel();
            }
        }

        [Test]
        public void DraggingLinkedComponentBreaksPair()
        {
            var context = FormWindclaw();
            var drag = new DragPlacementController(context.Board, context.Destination, true);

            Assert.IsTrue(drag.BeginDrag("sigil"));
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
            Assert.IsTrue(context.Destination.TryGetComponent("sky", out var partner));
            Assert.IsNull(partner.PairLinkId);
            drag.Cancel();
        }

        [Test]
        public void OnlySelectedComponentMoves()
        {
            var context = FormWindclaw();

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", new GridPosition(2, 2)));

            Assert.IsTrue(context.Board.TryGetPosition("sigil", out var moved));
            Assert.IsTrue(context.Board.TryGetPosition("sky", out var partner));
            Assert.AreEqual(new GridPosition(2, 2), moved);
            Assert.AreEqual(new GridPosition(0, 2), partner);
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
        }

        [Test]
        public void PartnerRemainsInOriginalCell()
        {
            var context = FormWindclaw();
            Assert.IsTrue(context.Board.TryGetPosition("sky", out var partnerOrigin));
            var occupiedBench = context.Board.GetPositions(CellType.Bench)[2];
            Assert.IsTrue(context.Board.TryGetOccupant(occupiedBench, out var benchUnitId));

            Assert.AreEqual(DragDropStatus.Swapped, Drag(context, "sigil", occupiedBench));

            Assert.IsTrue(context.Board.TryGetPosition("sky", out var partnerAfter));
            Assert.AreEqual(partnerOrigin, partnerAfter);
            Assert.IsTrue(context.Board.TryGetPosition("sigil", out var selectedAfter));
            Assert.AreEqual(occupiedBench, selectedAfter);
            Assert.IsTrue(context.Board.TryGetPosition(benchUnitId, out var swappedAfter));
            Assert.AreEqual(new GridPosition(0, 1), swappedAfter);
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
        }

        [Test]
        public void InvalidDropRestoresPair()
        {
            var context = FormWindclaw();
            var originalPairId = GetOnlyPair(context).PairLinkId;
            var locked = context.Board.GetPositions(CellType.Locked)[0];

            Assert.AreEqual(DragDropStatus.Reverted, Drag(context, "sigil", locked));

            Assert.IsTrue(context.Board.TryGetPosition("sigil", out var sigilPosition));
            Assert.IsTrue(context.Board.TryGetPosition("sky", out var skyPosition));
            Assert.AreEqual(new GridPosition(0, 1), sigilPosition);
            Assert.AreEqual(new GridPosition(0, 2), skyPosition);
            var restored = GetOnlyPair(context);
            Assert.AreNotEqual(originalPairId, restored.PairLinkId);
        }

        [Test]
        public void PairRestoresAfterInvalidDropRollback()
        {
            InvalidDropRestoresPair();
        }

        [Test]
        public void ReplacingPartnerCreatesNewRecipe()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"), Knight("knight"));
            MoveDirect(context, "sigil", new GridPosition(0, 1));
            MoveDirect(context, "sky", new GridPosition(0, 2));
            MoveDirect(context, "knight", new GridPosition(1, 2));
            Assert.AreEqual(HeroSliceCatalog.WindclawRangerRecipeId, GetOnlyPair(context).RecipeId);

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", new GridPosition(1, 1)));

            var relinked = GetOnlyPair(context);
            Assert.AreEqual(HeroSliceCatalog.DragonRiderRecipeId, relinked.RecipeId);
            Assert.IsTrue(relinked.ContainsComponent("sigil"));
            Assert.IsTrue(relinked.ContainsComponent("knight"));
            Assert.IsTrue(context.Destination.TryGetComponent("sky", out var sky));
            Assert.IsNull(sky.PairLinkId);
        }

        [Test]
        public void EachComponentCanBelongToOnlyOnePairLink()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"), Knight("knight"));
            MoveDirect(context, "sky", new GridPosition(0, 2));
            MoveDirect(context, "sigil", new GridPosition(0, 1));
            MoveDirect(context, "knight", new GridPosition(1, 2));

            Assert.AreEqual(1, context.Destination.ActivePairLinkCount);
            var pair = GetOnlyPair(context);
            Assert.IsTrue(pair.ContainsComponent("sigil"));
            Assert.IsTrue(context.Destination.TryGetComponent("knight", out var knight));
            Assert.IsNull(knight.PairLinkId);
        }

        [Test]
        public void PairCanBreakAndReformRepeatedly()
        {
            var context = FormWindclaw();
            var previousPairId = GetOnlyPair(context).PairLinkId;

            for (var cycle = 0; cycle < 3; cycle++)
            {
                Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", new GridPosition(2, 2)));
                Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
                Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", new GridPosition(0, 1)));

                var reformed = GetOnlyPair(context);
                Assert.AreEqual(HeroSliceCatalog.WindclawRangerRecipeId, reformed.RecipeId);
                Assert.AreNotEqual(previousPairId, reformed.PairLinkId);
                previousPairId = reformed.PairLinkId;
            }
        }

        [Test]
        public void PairReformsAfterValidBoardPlacement()
        {
            var context = FormWindclaw();
            var emptyBench = context.Board.GetPositions(CellType.Bench)[0];

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", emptyBench));
            Assert.AreEqual(0, context.Destination.ActivePairLinkCount);
            Assert.IsFalse(context.Destination.IsCombatRegistered("sigil"));
            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", new GridPosition(0, 1)));

            Assert.AreEqual(1, context.Destination.ActivePairLinkCount);
            Assert.IsTrue(context.Destination.IsCombatRegistered("sigil"));
            Assert.AreEqual(HeroSliceCatalog.WindclawRangerRecipeId, GetOnlyPair(context).RecipeId);
        }

        [Test]
        public void PairingDoesNotConsumeComponentRuntime()
        {
            var context = FormWindclaw();
            Assert.IsTrue(context.Destination.TryGetComponent("sigil", out var sigil));
            Assert.IsTrue(context.Destination.TryGetComponent("sky", out var sky));
            Assert.AreEqual("sigil", sigil.ComponentId);
            Assert.AreEqual("sky", sky.ComponentId);
            Assert.AreNotEqual(sigil.ComponentId, sky.ComponentId);
        }

        [Test]
        public void PairingNeverReturnsComponentToBag()
        {
            var deck = CreateSliceDeck(661, "bag");
            var first = deck.DrawNext();
            var second = deck.DrawNext();
            Assert.AreEqual(1, deck.RemainingHeroComponents);
            var context = CreateContext(
                first.Cards.Single(card => card.ConfigId == HeroSliceCatalog.DragonSigilComponentId),
                second.Cards.Single(card => card.ConfigId == HeroSliceCatalog.SkyRangerComponentId));

            MoveDirect(context, first.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.DragonSigilComponentId).RuntimeId, new GridPosition(0, 1));
            MoveDirect(context, second.Cards.Single(card =>
                card.ConfigId == HeroSliceCatalog.SkyRangerComponentId).RuntimeId, new GridPosition(0, 2));

            Assert.AreEqual(1, context.Destination.ActivePairLinkCount);
            Assert.AreEqual(1, deck.RemainingHeroComponents);
        }

        [Test]
        public void AttackOriginUsesLinkedCellMidpoint()
        {
            var context = FormWindclaw();
            var activePair = context.Destination.GetActiveHeroPairs().Single();
            var first = TargetingSystem.FromBoardPosition(activePair.ComponentA.CurrentCell);
            var second = TargetingSystem.FromBoardPosition(activePair.ComponentB.CurrentCell);
            Assert.AreEqual((first.X + second.X) * 0.5f, activePair.CombatPosition.X, 0.0001f);
            Assert.AreEqual((first.Y + second.Y) * 0.5f, activePair.CombatPosition.Y, 0.0001f);
        }

        [Test]
        public void EverFormedHeroIdsDoesNotBlockRelink()
        {
            var context = FormWindclaw();
            Assert.IsTrue(context.Destination.HasEverFormedHero(HeroSliceCatalog.WindclawRangerHeroId));
            CollectionAssert.AreEquivalent(
                new[] { HeroSliceCatalog.WindclawRangerHeroId },
                context.Destination.EverFormedHeroIds);

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", new GridPosition(2, 2)));
            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sigil", new GridPosition(0, 1)));

            Assert.AreEqual(1, context.Destination.ActivePairLinkCount);
            Assert.IsTrue(context.Destination.HasEverFormedHero(HeroSliceCatalog.WindclawRangerHeroId));
            Assert.AreEqual(1, context.Destination.EverFormedHeroIds.Count);
        }

        [Test]
        public void ExperienceAndLevelPersistWhenRoleComponentRelinksToNewSigil()
        {
            var context = CreateContext(Sigil("sigil.1"), Sigil("sigil.2"), Sky("sky"));
            MoveDirect(context, "sigil.1", new GridPosition(0, 1));
            MoveDirect(context, "sky", new GridPosition(0, 2));
            MoveDirect(context, "sigil.2", new GridPosition(1, 1));
            var original = GetOnlyPair(context);
            original.CombatProxy.AddExperience(20);
            Assert.AreEqual(2, original.CombatProxy.Level);

            Assert.AreEqual(DragDropStatus.Moved, Drag(context, "sky", new GridPosition(1, 2)));

            var relinked = GetOnlyPair(context);
            Assert.IsTrue(relinked.ContainsComponent("sigil.2"));
            Assert.IsTrue(relinked.ContainsComponent("sky"));
            Assert.AreEqual(20, relinked.CombatProxy.Experience);
            Assert.AreEqual(2, relinked.CombatProxy.Level);
        }

        [Test]
        public void WindclawStopsAtLevelThree()
        {
            var pair = GetOnlyPair(FormWindclaw());
            pair.CombatProxy.AddExperience(1000);
            Assert.AreEqual(3, pair.CombatProxy.Level);
            Assert.AreEqual(60, pair.CombatProxy.Experience);
            Assert.IsFalse(pair.CombatProxy.AddExperience(3));
        }

        [Test]
        public void DragonRiderStopsAtLevelFive()
        {
            var pair = GetOnlyPair(FormDragonRider());
            pair.CombatProxy.AddExperience(1000);
            Assert.AreEqual(5, pair.CombatProxy.Level);
            Assert.AreEqual(175, pair.CombatProxy.Experience);
            Assert.IsFalse(pair.CombatProxy.AddExperience(3));
        }

        [Test]
        public void HeroPairFormedLateDoesNotReceivePastExperience()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"));
            const int killsBeforeFormation = 12;
            Assert.AreEqual(0, context.Destination.GetActiveHeroPairs().Count);

            var pair = FormWindclaw(context);

            Assert.AreEqual(0, pair.CombatProxy.Experience, $"Past {killsBeforeFormation} kills must not be replayed.");
            pair.CombatProxy.AddExperience(1);
            Assert.AreEqual(1, pair.CombatProxy.Experience);
        }

        [Test]
        public void AIUsesSameHeroComponentCounts()
        {
            var player = CreateSliceDeck(991, "player");
            var ai = CreateSliceDeck(991, "ai");
            for (var recruit = 0; recruit < 3; recruit++)
            {
                CollectionAssert.AreEqual(
                    player.DrawNext().Cards.Select(card => card.ConfigId).ToArray(),
                    ai.DrawNext().Cards.Select(card => card.ConfigId).ToArray());
            }

            Assert.AreEqual(0, player.RemainingHeroComponents);
            Assert.AreEqual(0, ai.RemainingHeroComponents);
        }

        [Test]
        public void AIHeroPairUsesSameExperienceRules()
        {
            var playerPair = GetOnlyPair(FormWindclaw());
            var aiPair = GetOnlyPair(FormWindclaw());
            foreach (var reward in new[] { 1, 1, 3, 1, 3, 20 })
            {
                playerPair.CombatProxy.AddExperience(reward);
                aiPair.CombatProxy.AddExperience(reward);
            }

            Assert.AreEqual(playerPair.CombatProxy.Experience, aiPair.CombatProxy.Experience);
            Assert.AreEqual(playerPair.CombatProxy.Level, aiPair.CombatProxy.Level);
            Assert.AreEqual(playerPair.CombatProxy.Attack, aiPair.CombatProxy.Attack, 0.0001f);
        }

        [Test]
        public void ActiveHeroPairsShareThreeWaveKillExperienceOnBothSides()
        {
            var player = FormBothHeroes(TeamSide.Player);
            var ai = FormBothHeroes(TeamSide.AI);
            var match = new MatchController(404);
            Assert.IsTrue(match.TryTransition(MatchState.Ready));
            Assert.IsTrue(match.TryTransition(MatchState.Running));
            var runtime = new ThreeWaveSliceRuntime(match, player.Destination, ai.Destination);
            for (var tick = 0; tick < 900 && !runtime.IsComplete; tick++)
            {
                runtime.Tick(0.1f);
            }

            var playerPairs = player.Destination.GetActiveHeroPairs();
            var aiPairs = ai.Destination.GetActiveHeroPairs();
            Assert.AreEqual(2, playerPairs.Count);
            Assert.AreEqual(2, aiPairs.Count);
            Assert.GreaterOrEqual(playerPairs[0].PairLink.CombatProxy.Experience, 20);
            Assert.AreEqual(
                playerPairs[0].PairLink.CombatProxy.Experience,
                playerPairs[1].PairLink.CombatProxy.Experience);
            Assert.AreEqual(
                aiPairs[0].PairLink.CombatProxy.Experience,
                aiPairs[1].PairLink.CombatProxy.Experience);
            Assert.AreEqual(
                playerPairs[0].PairLink.CombatProxy.Experience,
                aiPairs[0].PairLink.CombatProxy.Experience);
        }

        [Test]
        public void AIFormsBothSlicePairLinksDeterministically()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var destination = new BoardRecruitDestination(board);
            var team = new TeamState(TeamSide.AI);
            team.AddResources(36);
            var recruitment = new RecruitmentService(team, CreateSliceDeck(818, "ai"), destination);
            var ai = new BasicUnitAiController(board, destination, recruitment);

            Assert.AreEqual(RecruitmentStatus.Success, ai.RecruitOrRefresh().Status);
            Assert.AreEqual(RecruitmentStatus.Success, ai.RecruitOrRefresh().Status);
            Assert.AreEqual(RecruitmentStatus.Success, ai.RecruitOrRefresh().Status);
            Assert.AreEqual(2, destination.ActivePairLinkCount);
            Assert.IsTrue(destination.HasActiveHero(HeroSliceCatalog.WindclawRangerHeroId));
            Assert.IsTrue(destination.HasActiveHero(HeroSliceCatalog.DragonRiderHeroId));
        }

        [Test]
        public void BasicOnlyModeStillPassesAllTests()
        {
            var deck = new RecruitDeck(
                GreyboxRecruitmentCatalog.Create(),
                new RunRandom(105),
                "basic",
                enableHeroComponents: false,
                heroSliceMode: false);
            for (var index = 0; index < 100; index++)
            {
                Assert.IsTrue(deck.DrawNext().Cards.All(card => card.Kind == RecruitItemKind.BasicUnit));
            }

            Assert.AreEqual(0, deck.RemainingHeroComponents);
        }

        [Test]
        public void WindclawEveryFifthAttackIsElitePowerShot()
        {
            var pair = GetOnlyPair(FormWindclaw());
            pair.CombatProxy.TickFormation(HeroCombatState.FormationDurationSeconds);
            var registry = new EnemyRegistry();
            var elite = Enemy("elite", EnemyArchetype.Elite, 500f, new CombatPoint(0.5f, 2f));
            registry.Register(elite);
            var results = pair.CombatProxy.TickCombat(
                5f / pair.CombatProxy.AttackSpeed,
                new CombatPoint(0.5f, 2f),
                registry);
            var powerShot = results.Single(result => result.Kind == AttackKind.WindclawPowerShot);
            Assert.AreEqual(14f * 1.8f * 1.25f, powerShot.Damage, 0.001f);
        }

        [Test]
        public void DragonRiderUsesAreaDiveAndFlame()
        {
            var pair = GetOnlyPair(FormDragonRider());
            pair.CombatProxy.TickFormation(HeroCombatState.FormationDurationSeconds);
            var registry = new EnemyRegistry();
            registry.Register(Enemy("front", EnemyArchetype.Normal, 1000f, new CombatPoint(1f, 2f)));
            registry.Register(Enemy("near", EnemyArchetype.Normal, 1000f, new CombatPoint(1.3f, 2f)));

            var first = pair.CombatProxy.TickCombat(6f, new CombatPoint(0f, 2f), registry);
            Assert.IsTrue(first.Any(result => result.Kind == AttackKind.DragonRiderArea));
            Assert.IsTrue(first.Any(result => result.Kind == AttackKind.DragonRiderDive));
            var flame = pair.CombatProxy.TickCombat(1f, new CombatPoint(0f, 2f), registry);
            Assert.IsTrue(flame.Any(result => result.Kind == AttackKind.DragonRiderFlame));
        }

        private static Context FormWindclaw()
        {
            var context = CreateContext(Sigil("sigil"), Sky("sky"));
            FormWindclaw(context);
            return context;
        }

        private static HeroPairLink FormWindclaw(Context context)
        {
            MoveDirect(context, "sigil", new GridPosition(0, 1));
            MoveDirect(context, "sky", new GridPosition(0, 2));
            return GetOnlyPair(context);
        }

        private static Context FormDragonRider()
        {
            var context = CreateContext(Sigil("sigil"), Knight("knight"));
            MoveDirect(context, "sigil", new GridPosition(0, 1));
            MoveDirect(context, "knight", new GridPosition(0, 2));
            return context;
        }

        private static Context FormBothHeroes(TeamSide side = TeamSide.Player)
        {
            var context = CreateContext(side,
                Sigil("sigil.windclaw"),
                Sky("sky"),
                Sigil("sigil.rider"),
                Knight("knight"));
            MoveDirect(context, "sigil.windclaw", new GridPosition(0, 1));
            MoveDirect(context, "sky", new GridPosition(0, 2));
            MoveDirect(context, "sigil.rider", new GridPosition(1, 1));
            MoveDirect(context, "knight", new GridPosition(1, 2));
            return context;
        }

        private static Context CreateContext(params RecruitCard[] provided)
        {
            return CreateContext(TeamSide.Player, provided);
        }

        private static Context CreateContext(TeamSide side, params RecruitCard[] provided)
        {
            var cards = provided.ToList();
            while (cards.Count < RecruitBatch.CardsPerRecruitment)
            {
                var id = $"filler.{cards.Count}";
                cards.Add(new RecruitCard(id, RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty));
            }

            var board = DragonBoundBoardLayout.Create(BattlefieldLayoutDefinitions.Legacy3x3, side);
            var destination = new BoardRecruitDestination(board);
            destination.Commit(
                RecruitDestinationPlan.AddToEmptySlots,
                new RecruitBatch(1, cards));
            return new Context(board, destination);
        }

        private static RecruitCard Sigil(string runtimeId)
        {
            return Component(runtimeId, HeroSliceCatalog.DragonSigilComponentId, false);
        }

        private static RecruitCard Sky(string runtimeId)
        {
            return Component(runtimeId, HeroSliceCatalog.SkyRangerComponentId, true);
        }

        private static RecruitCard Knight(string runtimeId)
        {
            return Component(runtimeId, HeroSliceCatalog.DragonKnightComponentId, true);
        }

        private static RecruitCard Component(string runtimeId, string configId, bool unique)
        {
            return new RecruitCard(runtimeId, RecruitItemKind.HeroComponent, configId, runtimeId + ".source", 1, unique);
        }

        private static RecruitDeck CreateSliceDeck(int seed, string prefix)
        {
            return new RecruitDeck(
                GreyboxRecruitmentCatalog.Create(),
                new RunRandom(seed),
                prefix,
                enableHeroComponents: true,
                heroSliceMode: true);
        }

        private static void MoveDirect(Context context, string runtimeId, GridPosition target)
        {
            Assert.IsTrue(context.Board.TryGetPosition(runtimeId, out var origin));
            Assert.IsTrue(context.Board.TryMove(origin, target));
            context.Destination.TryResolvePostDrop(runtimeId);
        }

        private static DragDropStatus Drag(Context context, string runtimeId, GridPosition target)
        {
            var drag = new DragPlacementController(context.Board, context.Destination, true);
            Assert.IsTrue(drag.BeginDrag(runtimeId));
            return drag.Drop(target);
        }

        private static HeroPairLink GetOnlyPair(Context context)
        {
            return context.Destination.GetActiveHeroPairs().Single().PairLink;
        }

        private static EnemyRuntime Enemy(
            string id,
            EnemyArchetype archetype,
            float hitPoints,
            CombatPoint position)
        {
            var enemy = new EnemyRuntime(id, TeamSide.Player, hitPoints, archetype);
            enemy.SetTargetingState(1, 0.5f, position);
            return enemy;
        }

        private readonly struct Context
        {
            public Context(BoardGrid board, BoardRecruitDestination destination)
            {
                Board = board;
                Destination = destination;
            }

            public BoardGrid Board { get; }
            public BoardRecruitDestination Destination { get; }
        }
    }
}
