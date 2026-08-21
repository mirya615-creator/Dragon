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
    public sealed class W6CombatReachSideSymmetryTests
    {
        [Test]
        public void MirroredBasicAndHeroFormationHasSymmetricReachAndDamage()
        {
            var player = RunMirrorSide(TeamSide.Player, "mirror.player");
            var ai = RunMirrorSide(TeamSide.AI, "mirror.ai");

            Assert.AreEqual(player.BasicPosition.X + ai.BasicPosition.X, 3f, 0.0001f);
            Assert.AreEqual(player.HeroPosition.X + ai.HeroPosition.X, 3f, 0.0001f);
            Assert.AreEqual(player.BossSpawnPosition.X + ai.BossSpawnPosition.X, 3f, 0.0001f);
            Assert.AreEqual(player.BasicDistanceToBoss, ai.BasicDistanceToBoss, 0.0001f);
            Assert.AreEqual(player.HeroDistanceToBoss, ai.HeroDistanceToBoss, 0.0001f);
            Assert.AreEqual(player.BasicHittable, ai.BasicHittable);
            Assert.AreEqual(player.HeroHittable, ai.HeroHittable);
            Assert.AreEqual(player.FirstBasicAttackSeconds, ai.FirstBasicAttackSeconds, 0.051f);
            Assert.AreEqual(player.FirstHeroAttackSeconds, ai.FirstHeroAttackSeconds, 0.051f);
            Assert.AreEqual(player.BasicAttackEvents, ai.BasicAttackEvents);
            Assert.AreEqual(player.HeroAttackEvents, ai.HeroAttackEvents);
            Assert.AreEqual(player.BasicDamage, ai.BasicDamage, 0.001f);
            Assert.AreEqual(player.HeroDamage, ai.HeroDamage, 0.001f);
            Assert.Greater(player.BasicAttackEvents + player.HeroAttackEvents, 0);
        }

        [Test]
        public void W6CalibrationTelemetrySeparatesFirstThreeAndFiveSecondWindows()
        {
            var report = CoreLoopRhythmDiagnostics.RunW6BareCalibration(1, 1, 500f);
            var sample = report.Player.Samples[0];
            Assert.That(sample.BossDamageFirst3Seconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(sample.BossDamageFirst5Seconds, Is.GreaterThanOrEqualTo(sample.BossDamageFirst3Seconds));
            Assert.That(sample.BossDamageEventCount, Is.GreaterThanOrEqualTo(sample.BossDamageEventsFirst3Seconds));
            Assert.That(sample.BossDamageEventCount, Is.GreaterThanOrEqualTo(sample.BossDamageEventsFirst5Seconds));
        }

        private static MirrorResult RunMirrorSide(TeamSide side, string prefix)
        {
            var layout = BattlefieldLayoutDefinitions.Compact4x4;
            var board = DragonBoundBoardLayout.Create(layout, side);
            var destination = new BoardRecruitDestination(board);
            var batch = DragonRouteHeroDevelopmentFactory.CreateBatch(
                HeroSliceCatalog.WindclawRangerHeroId,
                prefix);
            destination.Commit(destination.Plan(RecruitBatch.CardsPerRecruitment), batch);

            var sky = batch.Cards.Single(card => card.ConfigId == HeroSliceCatalog.SkyRangerComponentId);
            var sigil = batch.Cards.Single(card => card.ConfigId == HeroSliceCatalog.DragonSigilComponentId);
            var basic = batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            MoveCard(board, sky.RuntimeId, side == TeamSide.Player ? new GridPosition(0, 2) : new GridPosition(3, 2));
            MoveCard(board, sigil.RuntimeId, side == TeamSide.Player ? new GridPosition(0, 1) : new GridPosition(3, 1));
            MoveCard(board, basic.RuntimeId, side == TeamSide.Player ? new GridPosition(1, 1) : new GridPosition(2, 1));
            Assert.IsTrue(destination.TryResolvePostDrop(sigil.RuntimeId));

            var combatEvents = new List<CombatEvent>();
            var runtime = new PressureRaceSideRuntime(
                side == TeamSide.Player ? "MirrorPlayer" : "MirrorAi",
                "Mirror",
                side,
                new TeamState(side),
                destination,
                _ => { },
                combatEvents.Add);
            var boss = runtime.SpawnBoss(6, SoulchainBinderConfiguration.BossId, 500f, 0.20f);
            var deployedBasic = destination.GetDeployedUnits().Single(unit => unit.Card.RuntimeId == basic.RuntimeId);
            var hero = destination.GetActiveHeroPairs().Single();
            var targeting = new TargetingSystem();
            var basicStats = BasicUnitCatalog.GetStats(basic.ConfigId, basic.Level);
            var basicHittable = targeting.IsWithinRange(deployedBasic.CombatPosition, boss, basicStats.RangeCells);
            var heroHittable = targeting.IsWithinRange(hero.CombatPosition, boss, hero.PairLink.CombatProxy.RangeCells);
            var basicDistance = Distance(deployedBasic.CombatPosition, boss.CombatPosition);
            var heroDistance = Distance(hero.CombatPosition, boss.CombatPosition);
            var bossSpawnPosition = boss.CombatPosition;
            var firstBasic = -1f;
            var firstHero = -1f;
            var elapsed = 0f;
            var basicEvents = 0;
            var heroEvents = 0;
            var basicDamage = 0f;
            var heroDamage = 0f;

            for (var index = 0; index < 100; index++)
            {
                runtime.Tick(0.05f, 6);
                elapsed += 0.05f;
                foreach (var value in combatEvents)
                {
                    if (value.DamageOwnerKind == CombatDamageOwnerKind.BasicUnit)
                    {
                        basicEvents++;
                        basicDamage += value.Damage;
                        if (firstBasic < 0f) firstBasic = elapsed;
                    }
                    else if (value.DamageOwnerKind == CombatDamageOwnerKind.Hero)
                    {
                        heroEvents++;
                        heroDamage += value.Damage;
                        if (firstHero < 0f) firstHero = elapsed;
                    }
                }
                combatEvents.Clear();
            }

            return new MirrorResult(
                board.GetCombatPosition(deployedBasic.GridPosition),
                hero.CombatPosition,
                bossSpawnPosition,
                basicDistance,
                heroDistance,
                basicHittable,
                heroHittable,
                firstBasic,
                firstHero,
                basicEvents,
                heroEvents,
                basicDamage,
                heroDamage);
        }

        private static void MoveCard(BoardGrid board, string runtimeId, GridPosition target)
        {
            Assert.IsTrue(board.TryGetPosition(runtimeId, out var origin));
            Assert.IsTrue(board.TryMove(origin, target), $"Could not move {runtimeId} to {target}.");
        }

        private static float Distance(CombatPoint first, CombatPoint second)
        {
            return (float)Math.Sqrt(first.DistanceSquared(second));
        }

        private sealed class MirrorResult
        {
            public MirrorResult(
                CombatPoint basicPosition,
                CombatPoint heroPosition,
                CombatPoint bossSpawnPosition,
                float basicDistanceToBoss,
                float heroDistanceToBoss,
                bool basicHittable,
                bool heroHittable,
                float firstBasicAttackSeconds,
                float firstHeroAttackSeconds,
                int basicAttackEvents,
                int heroAttackEvents,
                float basicDamage,
                float heroDamage)
            {
                BasicPosition = basicPosition;
                HeroPosition = heroPosition;
                BossSpawnPosition = bossSpawnPosition;
                BasicDistanceToBoss = basicDistanceToBoss;
                HeroDistanceToBoss = heroDistanceToBoss;
                BasicHittable = basicHittable;
                HeroHittable = heroHittable;
                FirstBasicAttackSeconds = firstBasicAttackSeconds;
                FirstHeroAttackSeconds = firstHeroAttackSeconds;
                BasicAttackEvents = basicAttackEvents;
                HeroAttackEvents = heroAttackEvents;
                BasicDamage = basicDamage;
                HeroDamage = heroDamage;
            }

            public CombatPoint BasicPosition { get; }
            public CombatPoint HeroPosition { get; }
            public CombatPoint BossSpawnPosition { get; }
            public float BasicDistanceToBoss { get; }
            public float HeroDistanceToBoss { get; }
            public bool BasicHittable { get; }
            public bool HeroHittable { get; }
            public float FirstBasicAttackSeconds { get; }
            public float FirstHeroAttackSeconds { get; }
            public int BasicAttackEvents { get; }
            public int HeroAttackEvents { get; }
            public float BasicDamage { get; }
            public float HeroDamage { get; }
        }
    }
}
