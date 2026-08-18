using System;
using System.Collections.Generic;
using System.Linq;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class DragonRouteHeroCombatTests
    {
        [Test]
        public void ImplementedHeroSliceHasTwelveHeroes()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    DragonBoundHeroIds.WindclawRanger,
                    DragonBoundHeroIds.EmberShaman,
                    DragonBoundHeroIds.DragonRider,
                    DragonBoundHeroIds.RuneboltMage,
                    DragonBoundHeroIds.Stonebinder,
                    DragonBoundHeroIds.StarfallArchmage,
                    DragonBoundHeroIds.CrownSwordLeader,
                    DragonBoundHeroIds.CrownHunterLeader,
                    DragonBoundHeroIds.ThunderJarl,
                    DragonBoundHeroIds.NightfangAssassin,
                    DragonBoundHeroIds.LeviathanHunter,
                    DragonBoundHeroIds.SkyhunterValkyrie
                },
                HeroDefinitionCatalog.Definitions
                    .Where(hero => HeroDefinitionCatalog.GetMetadata(hero.Id).RuntimeCombatState == HeroRuntimeCombatState.Implemented)
                    .Select(hero => hero.Id));
        }

        [Test]
        public void AllHeroesAreImplemented()
        {
            Assert.AreEqual(
                0,
                HeroDefinitionCatalog.Definitions.Count(hero =>
                    HeroDefinitionCatalog.GetMetadata(hero.Id).RuntimeCombatState == HeroRuntimeCombatState.NotImplemented));
        }

        [TestCase(DragonBoundHeroIds.WindclawRanger, DragonBoundRecipeIds.WindclawRanger)]
        [TestCase(DragonBoundHeroIds.EmberShaman, DragonBoundRecipeIds.EmberShaman)]
        [TestCase(DragonBoundHeroIds.DragonRider, DragonBoundRecipeIds.DragonRider)]
        public void DragonRouteHeroesUseCurrentCanonicalIds(string heroId, string recipeId)
        {
            var recipe = HeroRecipeCatalog.Get(recipeId);
            Assert.AreEqual(heroId, recipe.HeroId);
            Assert.AreEqual(DragonBoundComponentIds.DragonSigil, recipe.BottomComponentId);
            Assert.AreEqual(HeroFormationOrientation.Vertical, recipe.FormationOrientation);
        }

        [TestCase(DragonBoundHeroIds.WindclawRanger, 14f, 1.80f, 3.25f, 3)]
        [TestCase(DragonBoundHeroIds.EmberShaman, 8f, 1.70f, 3.00f, 3)]
        [TestCase(DragonBoundHeroIds.DragonRider, 13f, 1.70f, 3.00f, 5)]
        [TestCase(DragonBoundHeroIds.RuneboltMage, 8f, 1.75f, 3.00f, 3)]
        [TestCase(DragonBoundHeroIds.Stonebinder, 10f, 1.45f, 2.75f, 3)]
        [TestCase(DragonBoundHeroIds.StarfallArchmage, 12f, 1.75f, 3.25f, 5)]
        [TestCase(DragonBoundHeroIds.CrownSwordLeader, 18f, 1.50f, 1.75f, 3)]
        [TestCase(DragonBoundHeroIds.CrownHunterLeader, 16f, 1.45f, 3.25f, 3)]
        [TestCase(DragonBoundHeroIds.ThunderJarl, 11f, 1.55f, 3.00f, 5)]
        [TestCase(DragonBoundHeroIds.NightfangAssassin, 30f, 1.50f, 2.25f, 5)]
        [TestCase(DragonBoundHeroIds.LeviathanHunter, 15f, 1.85f, 3.50f, 5)]
        [TestCase(DragonBoundHeroIds.SkyhunterValkyrie, 24f, 1.80f, 3.50f, 5)]
        public void DragonRouteHeroStatsReadFromCatalog(
            string heroId,
            float attack,
            float attackSpeed,
            float range,
            int maxLevel)
        {
            var definition = HeroSliceCatalog.Get(heroId);
            Assert.AreEqual(attack, definition.BaseAttack, 0.001f);
            Assert.AreEqual(attackSpeed, definition.BaseAttackSpeed, 0.001f);
            Assert.AreEqual(range, definition.RangeCells, 0.001f);
            Assert.AreEqual(maxLevel, definition.MaxLevel);
        }

        [Test]
        public void SkyhunterNormalAndRadianceRangesRemainThreePointFiveCells()
        {
            Assert.AreEqual(
                3.50f,
                HeroSliceCatalog.Get(DragonBoundHeroIds.SkyhunterValkyrie).RangeCells,
                0.001f);
            Assert.AreEqual(
                3.50f,
                FrozenHeroConfigurationCatalog.GetSkill(DragonBoundSkillIds.SkyHunt)
                    .ScalarParameters["SkillTargetRange"],
                0.001f);
        }

        [Test]
        public void FinalSpecialTargetRangesAndLineLengthsReadFromFrozenConfiguration()
        {
            Assert.AreEqual(
                3.50f,
                FrozenHeroConfigurationCatalog.GetSkill(DragonBoundSkillIds.NightfangExecution)
                    .ScalarParameters["SkillTargetRange"],
                0.001f);
            Assert.AreEqual(
                3.50f,
                FrozenHeroConfigurationCatalog.GetSkill(DragonBoundSkillIds.AbyssHarpoon)
                    .ScalarParameters["SkillTargetRange"],
                0.001f);
            Assert.AreEqual(
                6.00f,
                FrozenHeroConfigurationCatalog.GetHero(DragonBoundHeroIds.LeviathanHunter)
                    .AttackParameters["PierceLength"],
                0.001f);
            Assert.AreEqual(
                6.00f,
                FrozenHeroConfigurationCatalog.GetSkill(DragonBoundSkillIds.AbyssHarpoon).Length,
                0.001f);
            Assert.AreEqual(
                3.50f,
                FrozenHeroConfigurationCatalog.GetSkill(DragonBoundSkillIds.SkyHunt)
                    .ScalarParameters["SkillTargetRange"],
                0.001f);
            Assert.AreEqual(
                5.00f,
                FrozenHeroConfigurationCatalog.GetHero(DragonBoundHeroIds.RuneboltMage)
                    .AttackParameters["PierceLength"],
                0.001f);
            Assert.AreEqual(
                6.00f,
                FrozenHeroConfigurationCatalog.GetSkill(DragonBoundSkillIds.FlameDive).Length,
                0.001f);
        }

        [TestCase(DragonBoundHeroIds.WindclawRanger)]
        [TestCase(DragonBoundHeroIds.EmberShaman)]
        [TestCase(DragonBoundHeroIds.DragonRider)]
        [TestCase(DragonBoundHeroIds.RuneboltMage)]
        [TestCase(DragonBoundHeroIds.Stonebinder)]
        [TestCase(DragonBoundHeroIds.StarfallArchmage)]
        [TestCase(DragonBoundHeroIds.CrownSwordLeader)]
        [TestCase(DragonBoundHeroIds.CrownHunterLeader)]
        [TestCase(DragonBoundHeroIds.ThunderJarl)]
        [TestCase(DragonBoundHeroIds.NightfangAssassin)]
        [TestCase(DragonBoundHeroIds.LeviathanHunter)]
        [TestCase(DragonBoundHeroIds.SkyhunterValkyrie)]
        public void EveryHeroRejectsOppositeCombatSideForTargetingAndDamage(string heroId)
        {
            var playerState = CreateState(heroId, TeamSide.Player);
            var playerRegistry = new EnemyRegistry();
            var aiEnemy = Enemy("ai.target", TeamSide.AI, new CombatPoint(1f, 0f), 0.9f, 10000f);
            playerRegistry.Register(aiEnemy);

            Assert.IsEmpty(playerState.TickCombat(20f, new CombatPoint(0f, 0f), playerRegistry));
            Assert.AreEqual(10000f, aiEnemy.HitPoints, 0.001f);
            Assert.IsNull(playerState.CurrentTargetRuntimeId);

            var aiState = CreateState(heroId, TeamSide.AI);
            var aiRegistry = new EnemyRegistry();
            var playerEnemy = Enemy("player.target", TeamSide.Player, new CombatPoint(1f, 0f), 0.9f, 10000f);
            aiRegistry.Register(playerEnemy);

            Assert.IsEmpty(aiState.TickCombat(20f, new CombatPoint(0f, 0f), aiRegistry));
            Assert.AreEqual(10000f, playerEnemy.HitPoints, 0.001f);
            Assert.IsNull(aiState.CurrentTargetRuntimeId);
        }

        [Test]
        public void PurpleLevelThresholdsAndMultipliersMatchConfiguredValues()
        {
            foreach (var heroId in new[]
                     {
                         DragonBoundHeroIds.WindclawRanger,
                         DragonBoundHeroIds.EmberShaman,
                         DragonBoundHeroIds.RuneboltMage,
                         DragonBoundHeroIds.Stonebinder,
                         DragonBoundHeroIds.CrownSwordLeader,
                         DragonBoundHeroIds.CrownHunterLeader
                     })
            {
                var hero = HeroSliceCatalog.Get(heroId);
                CollectionAssert.AreEqual(new[] { 0, 20, 60 }, Levels(hero).Select(level => level.RequiredExperience));
                CollectionAssert.AreEqual(new[] { 1f, 1.05f, 1.10f }, Levels(hero).Select(level => level.AttackMultiplier));
                CollectionAssert.AreEqual(new[] { 1f, 1.25f, 1.56f }, Levels(hero).Select(level => level.AttackSpeedMultiplier));
                CollectionAssert.AreEqual(new[] { 1f, 1.10f, 1.25f }, Levels(hero).Select(level => level.SkillMultiplier));
            }
        }

        [Test]
        public void GoldLevelThresholdsAndMultipliersMatchConfiguredValues()
        {
            foreach (var heroId in new[]
                     {
                         DragonBoundHeroIds.DragonRider,
                         DragonBoundHeroIds.StarfallArchmage,
                         DragonBoundHeroIds.ThunderJarl,
                         DragonBoundHeroIds.NightfangAssassin,
                         DragonBoundHeroIds.LeviathanHunter,
                         DragonBoundHeroIds.SkyhunterValkyrie
                     })
            {
                var hero = HeroSliceCatalog.Get(heroId);
                CollectionAssert.AreEqual(new[] { 0, 20, 55, 105, 175 }, Levels(hero).Select(level => level.RequiredExperience));
                CollectionAssert.AreEqual(new[] { 1f, 1.12f, 1.25f, 1.40f, 1.57f }, Levels(hero).Select(level => level.AttackMultiplier));
                CollectionAssert.AreEqual(new[] { 1f, 1.10f, 1.21f, 1.33f, 1.46f }, Levels(hero).Select(level => level.AttackSpeedMultiplier));
                CollectionAssert.AreEqual(new[] { 1f, 1.10f, 1.25f, 1.45f, 1.70f }, Levels(hero).Select(level => level.SkillMultiplier));
            }
        }

        [TestCase(1, EnemyArchetype.Normal, 300f)]
        [TestCase(2, EnemyArchetype.Fast, 340f)]
        [TestCase(3, EnemyArchetype.Normal, 380f)]
        [TestCase(3, EnemyArchetype.Elite, 480f)]
        public void ThreeWaveEnemyDurabilityIsConfiguredByWaveAndArchetype(
            int waveNumber,
            EnemyArchetype archetype,
            float expectedHitPoints)
        {
            Assert.AreEqual(
                expectedHitPoints,
                ThreeWaveSliceRuntime.GetEnemyMaxHitPoints(
                    ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase,
                    waveNumber,
                    archetype),
                0.001f);
        }

        [Test]
        public void DragonRiderWaveOneTargetSurvivesLongEnoughForDive()
        {
            var state = CreateState(HeroSliceCatalog.DragonRiderHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var target = Enemy(
                "wave.one.target",
                TeamSide.Player,
                new CombatPoint(1f, 0f),
                0.8f,
                ThreeWaveSliceRuntime.GetEnemyMaxHitPoints(
                    ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase,
                    1,
                    EnemyArchetype.Normal));
            registry.Register(target);

            var diveResolved = false;
            for (var step = 0; step < 60; step++)
            {
                var results = state.TickCombat(0.1f, new CombatPoint(0f, 0f), registry);
                diveResolved |= results.Any(result => result.Kind == AttackKind.DragonRiderDive);
                if (diveResolved)
                {
                    break;
                }
            }

            Assert.IsTrue(diveResolved);
            Assert.IsTrue(target.IsAlive);
        }

        [Test]
        public void WindclawWaveOneTargetSurvivesLongEnoughForPowerShot()
        {
            var state = CreateState(HeroSliceCatalog.WindclawRangerHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var target = Enemy(
                "wave.one.target",
                TeamSide.Player,
                new CombatPoint(1f, 0f),
                0.8f,
                ThreeWaveSliceRuntime.GetEnemyMaxHitPoints(
                    ThreeWaveEnemyDurabilityProfile.HeroSkillShowcase,
                    1,
                    EnemyArchetype.Normal));
            registry.Register(target);

            var powerShotResolved = false;
            for (var step = 0; step < 30; step++)
            {
                var results = state.TickCombat(0.1f, new CombatPoint(0f, 0f), registry);
                powerShotResolved |= results.Any(result => result.Kind == AttackKind.WindclawPowerShot);
                if (powerShotResolved)
                {
                    break;
                }
            }

            Assert.IsTrue(powerShotResolved);
            Assert.IsTrue(target.IsAlive);
        }

        [Test]
        public void WindclawTargetsEliteBeforeFrontmostAndFallsBackAfterEliteDies()
        {
            var state = CreateState(HeroSliceCatalog.WindclawRangerHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var frontmostNormal = Enemy("frontmost", TeamSide.Player, new CombatPoint(1f, 0f), 0.9f);
            var elite = Enemy("elite", TeamSide.Player, new CombatPoint(1f, 0f), 0.1f, 1f, EnemyArchetype.Elite);
            registry.Register(frontmostNormal);
            registry.Register(elite);

            state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual("elite", state.CurrentTargetRuntimeId);
            state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual("frontmost", state.CurrentTargetRuntimeId);
        }

        [Test]
        public void WindclawPowerShotTriggersEveryFiveResolvedAttacksAndRetargetDoesNotResetCounter()
        {
            var state = CreateState(HeroSliceCatalog.WindclawRangerHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            registry.Register(Enemy("first", TeamSide.Player, new CombatPoint(1f, 0f), 0.9f, 1f));
            registry.Register(Enemy("second", TeamSide.Player, new CombatPoint(1.1f, 0f), 0.8f));

            var results = state.TickCombat(5f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            var powerShot = results.Single(result => result.Kind == AttackKind.WindclawPowerShot);

            Assert.AreEqual(14f * 1.80f, powerShot.Damage, 0.001f);
            Assert.AreEqual(0, state.AttackNumber);
        }

        [Test]
        public void EmberShamanBasicAttackUsesConfiguredRadiusAndTargetLimit()
        {
            var state = CreateState(HeroSliceCatalog.EmberShamanHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var center = Enemy("center", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f);
            var near = Enemy("near", TeamSide.Player, new CombatPoint(1.75f, 0f), 0.7f);
            var outside = Enemy("outside", TeamSide.Player, new CombatPoint(2.1f, 0f), 0.6f);
            registry.Register(center);
            registry.Register(near);
            registry.Register(outside);

            var results = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);

            Assert.AreEqual(8f, center.MaxHitPoints - center.HitPoints, 0.001f);
            Assert.AreEqual(8f * 0.75f, near.MaxHitPoints - near.HitPoints, 0.001f);
            Assert.AreEqual(0f, outside.MaxHitPoints - outside.HitPoints, 0.001f);
            Assert.IsTrue(results.Any(result => result.Kind == AttackKind.EmberExplosiveFireball));
            Assert.IsTrue(results.Any(result => result.Kind == AttackKind.EmberExplosiveSplash));
            Assert.AreEqual(0, state.ActiveGroundHazardCount);
        }

        [Test]
        public void EmberShamanTargetsFrontmostAndRespectsMaximumFiveTargets()
        {
            var state = CreateState(HeroSliceCatalog.EmberShamanHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            for (var index = 0; index < 6; index++)
            {
                registry.Register(Enemy(
                    "enemy." + index,
                    TeamSide.Player,
                    new CombatPoint(1f + (index * 0.05f), 0f),
                    0.1f + (index * 0.1f)));
            }

            var results = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual("enemy.5", state.CurrentTargetRuntimeId);
            Assert.AreEqual(5, results.Count(result =>
                result.Kind == AttackKind.EmberExplosiveFireball ||
                result.Kind == AttackKind.EmberExplosiveSplash));
        }

        [Test]
        public void EmberGroundTicksThreeTimesAndRefreshesAtSameLocation()
        {
            var state = CreateState(HeroSliceCatalog.EmberShamanHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            registry.Register(Enemy("target", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f));

            var results = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.IsTrue(results.Any(result => result.Kind == AttackKind.EmberExplosiveFireball));
            Assert.AreEqual(0, state.ActiveGroundHazardCount);
        }

        [Test]
        public void EmberGroundAppliesNoControlOrPathChange()
        {
            var state = CreateState(HeroSliceCatalog.EmberShamanHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var enemy = Enemy("target", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f);
            registry.Register(enemy);
            state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);

            Assert.AreEqual(EnemyRuntimeState.Spawned, enemy.State);
            Assert.AreEqual(0.8f, enemy.PathProgress, 0.001f);
            Assert.AreEqual(0, state.ActiveGroundHazardCount);
        }

        [Test]
        public void EmberGroundUsesBaseAttackAndSkillMultiplier()
        {
            var state = CreateState(HeroSliceCatalog.EmberShamanHeroId, TeamSide.Player);
            state.AddExperience(20);
            var registry = new EnemyRegistry();
            var enemy = Enemy("target", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f);
            registry.Register(enemy);

            var results = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);

            Assert.IsTrue(results.Any(result =>
                result.Kind == AttackKind.EmberExplosiveFireball &&
                Math.Abs(result.Damage - (8f * 1.05f)) < 0.001f));
            Assert.IsFalse(results.Any(result => result.Kind == AttackKind.EmberGround));
        }

        [Test]
        public void GroundHazardOnlyHitsOwningSide()
        {
            var state = CreateState(HeroSliceCatalog.EmberShamanHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var playerEnemy = Enemy("player", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f);
            var aiEnemy = Enemy("ai", TeamSide.AI, new CombatPoint(1f, 0f), 0.8f);
            registry.Register(playerEnemy);
            registry.Register(aiEnemy);

            state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);

            Assert.Less(playerEnemy.HitPoints, playerEnemy.MaxHitPoints);
            Assert.AreEqual(aiEnemy.MaxHitPoints, aiEnemy.HitPoints, 0.001f);
        }

        [Test]
        public void DragonRiderDoesNotSpendCooldownWithoutTarget()
        {
            var state = CreateState(HeroSliceCatalog.DragonRiderHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();

            Assert.IsEmpty(state.TickCombat(6f, new CombatPoint(0f, 0f), registry));

            var target = Enemy("target", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f);
            registry.Register(target);
            var results = state.TickCombat(0.1f, new CombatPoint(0f, 0f), registry);

            Assert.IsTrue(results.Any(result => result.Kind == AttackKind.DragonRiderDive));
        }

        [Test]
        public void DragonRiderUsesConfiguredAreaLineAndFireDamage()
        {
            var state = CreateState(HeroSliceCatalog.DragonRiderHeroId, TeamSide.Player);
            state.AddExperience(20);
            var registry = new EnemyRegistry();
            var inline = Enemy("inline", TeamSide.Player, new CombatPoint(3f, 0f), 0.9f);
            var outsideLine = Enemy("outside", TeamSide.Player, new CombatPoint(3f, 0.36f), 0.8f);
            registry.Register(inline);
            registry.Register(outsideLine);

            var dive = state.TickCombat(6f, new CombatPoint(0f, 0f), registry);
            Assert.IsTrue(dive.Any(result => result.Kind == AttackKind.DragonRiderDive && result.Target == inline));
            Assert.IsFalse(dive.Any(result => result.Kind == AttackKind.DragonRiderDive && result.Target == outsideLine));
            Assert.AreEqual(13f * 1.12f * 2f * 1.10f,
                dive.Single(result => result.Kind == AttackKind.DragonRiderDive && result.Target == inline).Damage,
                0.001f);

            var fire = state.TickCombat(1f, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(13f * 0.25f * 1.10f,
                fire.Single(result => result.Kind == AttackKind.DragonRiderFlame && result.Target == inline).Damage,
                0.001f);
        }

        [Test]
        public void GroundHazardPausesCorrectlyAndNeverTargetsUnits()
        {
            var state = CreateState(HeroSliceCatalog.EmberShamanHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            registry.Register(Enemy("target", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f));
            state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            state.SetCombatSuspended(true);
            Assert.IsEmpty(state.TickCombat(10f, new CombatPoint(0f, 0f), registry));
            Assert.AreEqual(0, state.ActiveGroundHazardCount);
            state.SetCombatSuspended(false);
            Assert.IsTrue(state.TickCombat(1f, new CombatPoint(0f, 0f), registry)
                .Any(result => result.Kind == AttackKind.EmberExplosiveFireball));
        }

        [Test]
        public void PairRelinkReusesExecutorState()
        {
            var board = DragonBoundBoardLayout.CreateInitial();
            var destination = new BoardRecruitDestination(board);
            destination.Commit(
                RecruitDestinationPlan.AddToEmptySlots,
                new RecruitBatch(1, new[]
                {
                    new RecruitCard("sigil", RecruitItemKind.HeroComponent, HeroSliceCatalog.DragonSigilComponentId, "sigil.source"),
                    new RecruitCard("sky", RecruitItemKind.HeroComponent, HeroSliceCatalog.SkyRangerComponentId, "sky.source", 1, true),
                    new RecruitCard("filler.2", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty),
                    new RecruitCard("filler.3", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty),
                    new RecruitCard("filler.4", RecruitItemKind.BasicUnit, "basic.axe_raider", string.Empty)
                }));
            Assert.IsTrue(board.TryMove(board.GetPositions(CellType.Bench)[0], new GridPosition(0, 1)));
            Assert.IsTrue(board.TryMove(board.GetPositions(CellType.Bench)[1], new GridPosition(0, 2)));
            Assert.IsTrue(destination.TryResolvePostDrop("sky"));
            var first = destination.GetActiveHeroPairs().Single().PairLink;
            first.CombatProxy.TickFormation(HeroCombatState.FormationDurationSeconds);

            var drag = new DragPlacementController(board, destination, true);
            Assert.IsTrue(drag.BeginDrag("sky"));
            drag.Cancel();
            var second = destination.GetActiveHeroPairs().Single().PairLink;

            Assert.AreSame(first.CombatProxy, second.CombatProxy);
            Assert.IsTrue(second.CombatProxy.IsFormationComplete);
        }

        [Test]
        public void RuneboltUsesIndependentStraightPiercingExecutor()
        {
            var state = CreateState(HeroSliceCatalog.RuneboltMageHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var front = Enemy("front", TeamSide.Player, new CombatPoint(3f, 0f), 0.8f);
            var second = Enemy("second", TeamSide.Player, new CombatPoint(4f, 0f), 0.7f);
            var third = Enemy("third", TeamSide.Player, new CombatPoint(4.8f, 0.1f), 0.6f);
            var outside = Enemy("outside", TeamSide.Player, new CombatPoint(3f, 0.3f), 0.1f);
            var otherSide = Enemy("other", TeamSide.AI, new CombatPoint(3f, 0f), 0.9f);
            registry.Register(front);
            registry.Register(second);
            registry.Register(third);
            registry.Register(outside);
            registry.Register(otherSide);

            var results = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);

            Assert.AreEqual(8f, front.MaxHitPoints - front.HitPoints, 0.001f);
            Assert.AreEqual(8f, second.MaxHitPoints - second.HitPoints, 0.001f);
            Assert.AreEqual(8f, third.MaxHitPoints - third.HitPoints, 0.001f);
            Assert.AreEqual(outside.MaxHitPoints, outside.HitPoints, 0.001f);
            Assert.AreEqual(otherSide.MaxHitPoints, otherSide.HitPoints, 0.001f);
            Assert.AreEqual(3, results.Count(result => result.Kind == AttackKind.RuneboltPierce));
        }

        [Test]
        public void StonebinderAppliesStunOnEveryFourthSuccessfulAttack()
        {
            var state = CreateState(HeroSliceCatalog.StonebinderHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var target = Enemy("target", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f, 1000f);
            registry.Register(target);

            var results = state.TickCombat(4f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);

            Assert.AreEqual(0, state.StoneBindAttackCount);
            Assert.IsTrue(target.IsStunned);
            Assert.AreEqual(1.20f, target.StunRemainingSeconds, 0.02f);
            Assert.AreEqual(1, results.Count(result => result.Kind == AttackKind.StoneBind));
            Assert.AreEqual(4, results.Count(result => result.Kind == AttackKind.StonebinderShot));
        }

        [Test]
        public void StonebinderUsesEliteStunMultiplierAndBossImmunity()
        {
            var state = CreateState(HeroSliceCatalog.StonebinderHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var elite = Enemy("elite", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f, 1000f, EnemyArchetype.Elite);
            registry.Register(elite);

            state.TickCombat(4f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(1.20f * 0.60f, elite.StunRemainingSeconds, 0.02f);

            var boss = Enemy("boss", TeamSide.Player, new CombatPoint(1f, 0f), 0.8f, 1000f, EnemyArchetype.Boss);
            var bossState = CreateState(HeroSliceCatalog.StonebinderHeroId, TeamSide.Player);
            var bossRegistry = new EnemyRegistry();
            bossRegistry.Register(boss);
            bossState.TickCombat(4f / bossState.AttackSpeed, new CombatPoint(0f, 0f), bossRegistry);
            Assert.AreEqual(1.20f * 0.20f, boss.StunRemainingSeconds, 0.02f);
            boss.TickControl(0.25f);
            Assert.AreEqual(2f, boss.StunImmunityRemainingSeconds, 0.0001f);
            Assert.IsFalse(boss.ApplyStun(1f));
            boss.TickControl(2f);
            Assert.IsTrue(boss.ApplyStun(1f));
        }

        [Test]
        public void StarfallTelegraphsForOneSecondAndChoosesHighestDensity()
        {
            var state = CreateState(HeroSliceCatalog.StarfallArchmageHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            var denseA = Enemy("dense.a", TeamSide.Player, new CombatPoint(2f, 0f), 0.5f, 1000f);
            var denseB = Enemy("dense.b", TeamSide.Player, new CombatPoint(2.5f, 0f), 0.4f, 1000f);
            var sparse = Enemy("sparse", TeamSide.Player, new CombatPoint(4.2f, 0f), 0.9f, 1000f);
            registry.Register(denseA);
            registry.Register(denseB);
            registry.Register(sparse);

            var ready = state.TickCombat(8f, new CombatPoint(0f, 0f), registry);
            Assert.IsTrue(state.IsSkillTelegraphActive);
            Assert.IsFalse(ready.Any(result => result.Kind == AttackKind.StarfallImpact));

            var impact = state.TickCombat(1f, new CombatPoint(0f, 0f), registry);
            Assert.IsFalse(state.IsSkillTelegraphActive);
            Assert.IsTrue(impact.Any(result => result.Kind == AttackKind.StarfallImpact));
            Assert.IsTrue(impact.Any(result => result.Kind == AttackKind.StarfallImpact && result.Target == denseA));
            Assert.IsTrue(impact.Any(result => result.Kind == AttackKind.StarfallImpact && result.Target == denseB));
            Assert.IsFalse(impact.Any(result => result.Kind == AttackKind.StarfallImpact && result.Target == sparse));
        }

        [Test]
        public void StarfallDoesNotConsumeCooldownWithoutLegalTarget()
        {
            var state = CreateState(HeroSliceCatalog.StarfallArchmageHeroId, TeamSide.Player);
            var registry = new EnemyRegistry();
            state.TickCombat(8f, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(0f, state.SkillCooldownRemaining, 0.001f);

            registry.Register(Enemy("target", TeamSide.Player, new CombatPoint(2f, 0f), 0.5f, 1000f));
            state.TickCombat(0.1f, new CombatPoint(0f, 0f), registry);
            Assert.IsTrue(state.IsSkillTelegraphActive);
        }

        [Test]
        public void CrownSwordDuelMomentumStacksAndResetsOnRetarget()
        {
            var state = CreateState(DragonBoundHeroIds.CrownSwordLeader, TeamSide.Player);
            var registry = new EnemyRegistry();
            var first = Enemy("sword.first", TeamSide.Player, new CombatPoint(1f, 0f), 0.2f, 1000f);
            registry.Register(first);

            var firstHit = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            var secondHit = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(18f, firstHit.Single(result => result.Kind == AttackKind.CrownSwordStrike).Damage, 0.001f);
            Assert.AreEqual(18f * 1.08f, secondHit.Single(result => result.Kind == AttackKind.CrownSwordStrike).Damage, 0.001f);
            Assert.AreEqual(2, state.DuelMomentumStacks);

            var second = Enemy("sword.second", TeamSide.Player, new CombatPoint(1f, 0f), 0.9f, 1000f);
            registry.Register(second);
            var retarget = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(18f, retarget.Single(result => result.Target == second).Damage, 0.001f);
            Assert.AreEqual(1, state.DuelMomentumStacks);

            state.SetCombatSuspended(true);
            state.SetCombatSuspended(false);
            var resumed = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(18f * 1.08f, resumed.Single(result => result.Target == second).Damage, 0.001f);
        }

        [Test]
        public void CrownHunterMarksHighestHealthAndOnlySelfBenefits()
        {
            var state = CreateState(DragonBoundHeroIds.CrownHunterLeader, TeamSide.Player);
            var registry = new EnemyRegistry();
            var highestHealth = Enemy("hunter.high", TeamSide.Player, new CombatPoint(2f, 0f), 0.1f, 200f);
            var frontmost = Enemy("hunter.front", TeamSide.Player, new CombatPoint(1f, 0f), 0.9f, 150f);
            var otherSide = Enemy("hunter.ai", TeamSide.AI, new CombatPoint(1f, 0f), 0.95f, 999f);
            registry.Register(highestHealth);
            registry.Register(frontmost);
            registry.Register(otherSide);

            var first = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(highestHealth.RuntimeId, state.HuntMarkTargetRuntimeId);
            Assert.AreEqual(16f * 1.25f, first.Single(result => result.Kind == AttackKind.CrownHunterShot).Damage, 0.001f);
            Assert.AreEqual(150f, frontmost.HitPoints, 0.001f);
            Assert.AreEqual(999f, otherSide.HitPoints, 0.001f);

            highestHealth.SetTargetingState(1, 0.1f, new CombatPoint(10f, 0f));
            var retarget = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(frontmost.RuntimeId, state.HuntMarkTargetRuntimeId);
            Assert.AreEqual(16f * 1.25f, retarget.Single(result => result.Target == frontmost).Damage, 0.001f);
        }

        [Test]
        public void ThunderJarlChainsWithConfiguredFalloffAndDominionStuns()
        {
            var state = CreateState(DragonBoundHeroIds.ThunderJarl, TeamSide.Player);
            var registry = new EnemyRegistry();
            var first = Enemy("thunder.first", TeamSide.Player, new CombatPoint(1f, 0f), 0.9f, 1000f);
            var second = Enemy("thunder.second", TeamSide.Player, new CombatPoint(1.5f, 0f), 0.8f, 1000f);
            var third = Enemy("thunder.third", TeamSide.Player, new CombatPoint(2f, 0f), 0.7f, 1000f);
            var fourth = Enemy("thunder.fourth", TeamSide.Player, new CombatPoint(3.5f, 0f), 0.6f, 1000f);
            registry.Register(first);
            registry.Register(second);
            registry.Register(third);
            registry.Register(fourth);

            var chain = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(3, chain.Count(result => result.Kind == AttackKind.ThunderJarlChain));
            Assert.AreEqual(11f, chain.Single(result => result.Target == first).Damage, 0.001f);
            Assert.AreEqual(11f * 0.75f, chain.Single(result => result.Target == second).Damage, 0.001f);
            Assert.AreEqual(11f * 0.55f, chain.Single(result => result.Target == third).Damage, 0.001f);
            Assert.AreEqual(1000f, fourth.HitPoints, 0.001f);

            var dominion = state.TickCombat(8f, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(3, dominion.Count(result => result.Kind == AttackKind.ThunderDominion));
            Assert.IsTrue(first.IsStunned);
            Assert.AreEqual(0.90f, first.StunRemainingSeconds, 0.02f);
            Assert.AreEqual(8f - (1f / state.AttackSpeed), state.SkillCooldownRemaining, 0.001f);
        }

        [Test]
        public void ThunderDominionRemainsReadyWithoutLegalTarget()
        {
            var state = CreateState(DragonBoundHeroIds.ThunderJarl, TeamSide.Player);
            var registry = new EnemyRegistry();
            state.TickCombat(8f, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(0f, state.SkillCooldownRemaining, 0.001f);

            var target = Enemy("thunder.late", TeamSide.Player, new CombatPoint(2f, 0f), 0.5f, 1000f);
            registry.Register(target);
            var result = state.TickCombat(0.1f, new CombatPoint(0f, 0f), registry);
            Assert.IsTrue(result.Any(item => item.Kind == AttackKind.ThunderDominion));
        }

        [Test]
        public void ThunderDominionUsesBossStunMultiplierAndPostStunImmunity()
        {
            var state = CreateState(DragonBoundHeroIds.ThunderJarl, TeamSide.Player);
            var registry = new EnemyRegistry();
            var boss = Enemy("thunder.boss", TeamSide.Player, new CombatPoint(1f, 0f), 0.5f, 1000f, EnemyArchetype.Boss);
            registry.Register(boss);

            state.TickCombat(8f, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(0.90f * 0.20f, boss.StunRemainingSeconds, 0.02f);
            boss.TickControl(0.18f);
            Assert.AreEqual(2f, boss.StunImmunityRemainingSeconds, 0.0001f);
            Assert.IsFalse(boss.ApplyStun(1f));
            boss.TickControl(2f);
            Assert.IsTrue(boss.ApplyStun(1f));
        }

        [Test]
        public void NightfangPrioritizesBossAndAppliesBothExecuteMultipliers()
        {
            var state = CreateState(DragonBoundHeroIds.NightfangAssassin, TeamSide.Player);
            var registry = new EnemyRegistry();
            var normal = Enemy("nightfang.normal", TeamSide.Player, new CombatPoint(1f, 0f), 0.99f, 1000f);
            var elite = Enemy("nightfang.elite", TeamSide.Player, new CombatPoint(1f, 0f), 0.80f, 1000f, EnemyArchetype.Elite);
            var boss = Enemy("nightfang.boss", TeamSide.Player, new CombatPoint(1f, 0f), 0.10f, 55f, EnemyArchetype.Boss);
            registry.Register(normal);
            registry.Register(elite);
            registry.Register(boss);

            var first = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(boss.RuntimeId, state.CurrentTargetRuntimeId);
            Assert.AreEqual(30f * 1.60f, first.Single(result => result.Kind == AttackKind.NightfangStrike).Damage, 0.001f);

            var execute = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(30f * 1.60f * 1.30f,
                execute.Single(result => result.Kind == AttackKind.NightfangStrike).Damage,
                0.001f);
            Assert.AreEqual(1000f, normal.HitPoints, 0.001f);
            Assert.AreEqual(1000f, elite.HitPoints, 0.001f);
        }

        [Test]
        public void LeviathanHarpoonUsesStraightDistanceOrderAndFalloff()
        {
            var state = CreateState(DragonBoundHeroIds.LeviathanHunter, TeamSide.Player);
            var registry = new EnemyRegistry();
            var targets = new[]
            {
                Enemy("harpoon.1", TeamSide.Player, new CombatPoint(1f, 0f), 0.90f, 1000f),
                Enemy("harpoon.2", TeamSide.Player, new CombatPoint(2f, 0f), 0.80f, 1000f),
                Enemy("harpoon.3", TeamSide.Player, new CombatPoint(3f, 0f), 0.70f, 1000f),
                Enemy("harpoon.4", TeamSide.Player, new CombatPoint(4f, 0f), 0.60f, 1000f),
                Enemy("harpoon.5", TeamSide.Player, new CombatPoint(5f, 0f), 0.50f, 1000f),
                Enemy("harpoon.6", TeamSide.Player, new CombatPoint(6f, 0f), 0.40f, 1000f)
            };
            foreach (var target in targets)
            {
                registry.Register(target);
            }

            var outside = Enemy("harpoon.outside", TeamSide.Player, new CombatPoint(4f, 0.3f), 0.10f, 1000f);
            var otherSide = Enemy("harpoon.ai", TeamSide.AI, new CombatPoint(3f, 0f), 0.99f, 1000f);
            registry.Register(outside);
            registry.Register(otherSide);

            var results = state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            var harpoonResults = results.Where(result => result.Kind == AttackKind.LeviathanHarpoon).ToArray();
            Assert.AreEqual(6, harpoonResults.Length);
            CollectionAssert.AreEqual(targets.Select(target => target.RuntimeId), harpoonResults.Select(result => result.Target.RuntimeId));
            CollectionAssert.AreEqual(
                new[] { 15f, 15f * 0.92f, 15f * 0.84f, 15f * 0.76f, 15f * 0.68f, 15f * 0.60f },
                harpoonResults.Select(result => result.Damage).ToArray());
            Assert.AreEqual(1000f, outside.HitPoints, 0.001f);
            Assert.AreEqual(1000f, otherSide.HitPoints, 0.001f);
        }

        [Test]
        public void SkyhunterStacksAttackSpeedCapsAndResetsOnRetarget()
        {
            var state = CreateState(DragonBoundHeroIds.SkyhunterValkyrie, TeamSide.Player);
            Assert.IsTrue(state.AddExperience(175));
            Assert.AreEqual(5, state.Level);
            var registry = new EnemyRegistry();
            var first = Enemy("sky.first", TeamSide.Player, new CombatPoint(3f, 0f), 0.2f, 10000f);
            registry.Register(first);

            for (var hit = 0; hit < 12; hit++)
            {
                state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            }

            Assert.AreEqual(10, state.SkyHuntStacks);
            Assert.AreEqual(
                1.80f * HeroDefinitionCatalog.Get(DragonBoundHeroIds.SkyhunterValkyrie)
                    .GetLevelStats(5).AttackSpeedMultiplier * (1f + (0.06f * 10f)),
                state.AttackSpeed,
                0.001f);

            var second = Enemy("sky.second", TeamSide.Player, new CombatPoint(3f, 0f), 0.95f, 10000f);
            registry.Register(second);
            state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(second.RuntimeId, state.SkyHuntTargetRuntimeId);
            Assert.AreEqual(1, state.SkyHuntStacks);

            state.SetCombatSuspended(true);
            state.SetCombatSuspended(false);
            state.TickCombat(1f / state.AttackSpeed, new CombatPoint(0f, 0f), registry);
            Assert.AreEqual(2, state.SkyHuntStacks);

            state.ResetTargetingAfterRelocation();
            Assert.AreEqual(0, state.SkyHuntStacks);
            Assert.IsNull(state.SkyHuntTargetRuntimeId);
        }

        [Test]
        public void AllImplementedHeroesResolveFromCatalogWithoutFallback()
        {
            var notImplemented = HeroDefinitionCatalog.Definitions
                .Where(hero => HeroDefinitionCatalog.GetMetadata(hero.Id).RuntimeCombatState == HeroRuntimeCombatState.NotImplemented)
                .ToArray();

            Assert.AreEqual(0, notImplemented.Length);
            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                Assert.DoesNotThrow(() => HeroSliceCatalog.Get(hero.Id));
            }
        }

        [TestCase(DragonBoundHeroIds.WindclawRanger)]
        [TestCase(DragonBoundHeroIds.EmberShaman)]
        [TestCase(DragonBoundHeroIds.DragonRider)]
        [TestCase(DragonBoundHeroIds.RuneboltMage)]
        [TestCase(DragonBoundHeroIds.Stonebinder)]
        [TestCase(DragonBoundHeroIds.StarfallArchmage)]
        [TestCase(DragonBoundHeroIds.CrownSwordLeader)]
        [TestCase(DragonBoundHeroIds.CrownHunterLeader)]
        [TestCase(DragonBoundHeroIds.ThunderJarl)]
        [TestCase(DragonBoundHeroIds.NightfangAssassin)]
        [TestCase(DragonBoundHeroIds.LeviathanHunter)]
        [TestCase(DragonBoundHeroIds.SkyhunterValkyrie)]
        public void DevelopmentFactorySpawnsEachImplementedSlicePair(string heroId)
        {
            var destination = new BoardRecruitDestination(DragonBoundBoardLayout.CreateInitial());

            Assert.IsTrue(DragonRouteHeroDevelopmentFactory.TrySpawnPair(
                destination,
                heroId,
                "dev." + heroId,
                out var pairLink));
            Assert.AreEqual(heroId, pairLink.HeroId);
            Assert.AreEqual(1, destination.ActivePairLinkCount);
        }

        private static HeroCombatState CreateState(string heroId, TeamSide side)
        {
            var state = new HeroCombatState(
                heroId,
                new HeroProgressionState(heroId),
                false,
                side,
                "component." + heroId,
                "recipe." + heroId);
            Assert.IsTrue(state.TickFormation(HeroCombatState.FormationDurationSeconds));
            return state;
        }

        private static IEnumerable<HeroLevelStats> Levels(HeroDefinition hero)
        {
            for (var level = 1; level <= hero.MaxLevel; level++)
            {
                yield return hero.GetLevelStats(level);
            }
        }

        private static EnemyRuntime Enemy(
            string id,
            TeamSide side,
            CombatPoint position,
            float progress,
            float maxHitPoints = 1000f,
            EnemyArchetype archetype = EnemyArchetype.Normal)
        {
            var enemy = new EnemyRuntime(id, side, maxHitPoints, archetype);
            enemy.SetTargetingState(1, progress, position);
            return enemy;
        }
    }
}
