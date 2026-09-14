using System.Linq;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Recruitment;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class GreyboxGameplayAnimationTestConsoleTests
    {
        [Test]
        public void DirectBasicPlacementUsesBattleCellWithoutChangingCamp()
        {
            var destination = new BoardRecruitDestination(DragonBoundBoardLayout.CreateInitial());
            var card = new RecruitCard(
                "dev.basic.axe",
                RecruitItemKind.BasicUnit,
                "basic.axe_raider",
                string.Empty);
            var position = destination.Board.GetPositions(CellType.Battle)[0];

            Assert.IsTrue(destination.TryDebugPlaceCard(card, position));
            Assert.AreEqual(0, destination.CampCount);
            Assert.AreEqual(1, destination.DeployedCount);
            Assert.IsTrue(destination.IsCombatRegistered(card.RuntimeId));
        }

        [Test]
        public void DirectHeroPlacementFormsRequestedPairWithoutRequiringEmptyCamp()
        {
            var destination = new BoardRecruitDestination(DragonBoundBoardLayout.CreateInitial());
            var bench = destination.Board.GetPositions(CellType.Bench)[0];
            Assert.IsTrue(destination.TryDebugPlaceCard(
                new RecruitCard(
                    "dev.camp.basic",
                    RecruitItemKind.BasicUnit,
                    "basic.axe_raider",
                    string.Empty),
                bench));

            Assert.IsTrue(DragonRouteHeroDevelopmentFactory.TrySpawnPairDirect(
                destination,
                DragonBoundHeroIds.DragonRider,
                "dev.hero.dragon",
                out var pair));
            Assert.IsNotNull(pair);
            Assert.AreEqual(DragonBoundHeroIds.DragonRider, pair.HeroId);
            Assert.AreEqual(1, destination.CampCount);
            Assert.AreEqual(1, destination.ActivePairLinkCount);
        }

        [Test]
        public void DevelopmentEnemyUsesSelectedAnimationWaveOnRealRoute()
        {
            var runtime = new TwentyWavePressureRuntime(new MatchController(404), null, null, 404);

            Assert.IsTrue(runtime.TryDebugSpawnEnemy(TeamSide.Player, 4));
            var enemy = runtime.PlayerEnemyRegistry.Enemies.Single();
            Assert.AreEqual(4, enemy.SpawnWaveIndex);
            Assert.AreEqual(TeamSide.Player, enemy.Team);
            Assert.AreEqual(0f, enemy.PathProgress, 0.0001f);
        }

        [Test]
        public void DevelopmentBossStartsRequestedRealBossWave()
        {
            var runtime = new TwentyWavePressureRuntime(new MatchController(606), null, null, 606);

            Assert.IsTrue(runtime.TryDebugSpawnBoss(6));
            Assert.AreEqual(6, runtime.CurrentWave);
            Assert.IsTrue(runtime.PlayerEnemyRegistry.Enemies.Any(value =>
                value.Archetype == EnemyArchetype.Boss && value.SpawnWaveIndex == 6));
            Assert.IsNotNull(runtime.PlayerW6BossRuntime);
        }
    }
}
