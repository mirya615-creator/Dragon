using DragonBound.Core;
using DragonBound.Items;
using GameShared.Random;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemPassiveCombatEffectsTests
    {
        [Test]
        public void PassiveCatalog_ImplementsAllBGroupEffects()
        {
            Assert.AreEqual(20, ItemCatalog.FormalCandidates.Count);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.PactOfEndurance).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.FarwatchCrest).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.FrostMire).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.WarTempo).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.VeteransMark).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.QuartermastersSatchel).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.SpellbreakerSeal).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.RivalryOath).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.DraconicPresence).IsFormalCandidate);
        }

        [Test]
        public void PactOfEndurance_UsesOwnPlusFiveAndOpponentPlusThree()
        {
            var own = new TeamState(TeamSide.Player);
            var opponent = new TeamState(TeamSide.AI);
            var effect = new PactOfEnduranceEffect();
            effect.OnRunStart(new ItemRunContext(own, new EnemyRegistry(), runSeed: 1, opposingTeam: opponent));

            Assert.AreEqual(8, own.HatchlingMaxHealth);
            Assert.AreEqual(8, own.HatchlingHealth);
            Assert.AreEqual(6, opponent.HatchlingMaxHealth);
            Assert.AreEqual(6, opponent.HatchlingHealth);
        }

        [Test]
        public void FarwatchCrest_OnlyDoublesEligibleBowRanges()
        {
            var units = new ItemCombatUnitRegistry();
            var sky = new ItemCombatUnitState("sky", ItemCombatUnitKind.Hero, heroId: "HERO_SKYHUNTER_VALKYRIE");
            var wind = new ItemCombatUnitState("wind", ItemCombatUnitKind.Hero, heroId: "HERO_WINDCLAW_RANGER");
            var bow = new ItemCombatUnitState("bow", ItemCombatUnitKind.Basic, isBasicArcher: true);
            var melee = new ItemCombatUnitState("melee", ItemCombatUnitKind.Basic);
            units.Register(sky); units.Register(wind); units.Register(bow); units.Register(melee);
            var effect = new FarwatchCrestEffect();
            effect.OnRunStart(new ItemRunContext(new TeamState(TeamSide.Player), new EnemyRegistry(), units));

            Assert.AreEqual(3, effect.LastAffectedUnitCount);
            Assert.AreEqual(2f, sky.RangeMultiplier);
            Assert.AreEqual(2f, wind.RangeMultiplier);
            Assert.AreEqual(2f, bow.RangeMultiplier);
            Assert.AreEqual(1f, melee.RangeMultiplier);
        }

        [Test]
        public void FrostMireAndDraconicPresenceApplyCappedRouteSlows()
        {
            var team = new TeamState(TeamSide.Player);
            var enemies = new EnemyRegistry();
            var enemy = new EnemyRuntime("normal", TeamSide.Player);
            enemies.Register(enemy);
            var units = new ItemCombatUnitRegistry();
            units.Register(new ItemCombatUnitState("hero-1", ItemCombatUnitKind.Hero));
            units.Register(new ItemCombatUnitState("hero-2", ItemCombatUnitKind.Hero));
            units.Register(new ItemCombatUnitState("hero-3", ItemCombatUnitKind.Hero));
            var context = new ItemRunContext(team, enemies, units);

            var frost = new FrostMireEffect();
            frost.OnRunStart(context);
            var presence = new DraconicPresenceEffect();
            presence.OnRunStart(context);

            Assert.AreEqual(1, frost.LastAffectedEnemyCount);
            Assert.AreEqual(0.06f, presence.AppliedSlowFraction, 0.001f);
            Assert.AreEqual(0.9f, enemy.MovementSpeedMultiplier, 0.001f);
        }

        [Test]
        public void WarTempoAndRivalryOathApplyOwnAndOpponentMultipliers()
        {
            var ownUnits = new ItemCombatUnitRegistry();
            var opponentUnits = new ItemCombatUnitRegistry();
            var own = new ItemCombatUnitState("own", ItemCombatUnitKind.Basic);
            var opponent = new ItemCombatUnitState("opponent", ItemCombatUnitKind.Hero);
            ownUnits.Register(own); opponentUnits.Register(opponent);
            var context = new ItemRunContext(
                new TeamState(TeamSide.Player), new EnemyRegistry(), ownUnits, 1,
                new TeamState(TeamSide.AI), null, opponentUnits);

            new WarTempoEffect().OnRunStart(context);
            Assert.AreEqual(1.1f, own.AttackSpeedMultiplier, 0.001f);
            Assert.AreEqual(1.1f, opponent.AttackSpeedMultiplier, 0.001f);
            new RivalryOathEffect().OnRunStart(context);
            Assert.AreEqual(1.65f, own.AttackSpeedMultiplier, 0.001f);
            Assert.AreEqual(1.43f, opponent.AttackSpeedMultiplier, 0.001f);
        }

        [Test]
        public void VeteransMarkPromotesOnlyEligibleLevelOneBasicAtFivePercent()
        {
            var seed = FindSeed(ItemIds.VeteransMark + ".1");
            var units = new ItemCombatUnitRegistry();
            var basic = new ItemCombatUnitState("basic", ItemCombatUnitKind.Basic);
            units.Register(basic);
            var context = new ItemRunContext(new TeamState(TeamSide.Player), new EnemyRegistry(), units, seed);
            var effect = new VeteransMarkEffect();

            effect.HandleCombatEvent(context, new ItemCombatEvent(ItemCombatEventKind.RecruitSucceeded, TeamSide.Player, "basic"));

            Assert.AreEqual(1, effect.LastPromotedCount);
            Assert.AreEqual(2, basic.Level);
        }

        [Test]
        public void QuartermasterSatchelAddsOneNonStackingBenchSlot()
        {
            var capacity = new ItemBenchCapacityState();
            var context = new ItemRunContext(new TeamState(TeamSide.Player), new EnemyRegistry(), benchCapacity: capacity);
            var effect = new QuartermasterSatchelEffect();
            effect.OnRunStart(context);
            effect.OnRunStart(context);

            Assert.IsTrue(effect.Applied);
            Assert.AreEqual(1, capacity.Capacity);
        }

        [Test]
        public void SpellbreakerSealProvidesDeterministicFiftyPercentBossCastPort()
        {
            var seed = FindSeed(ItemIds.SpellbreakerSeal + ".1");
            var effect = new SpellbreakerSealEffect();
            var context = new ItemRunContext(new TeamState(TeamSide.Player), new EnemyRegistry(), runSeed: seed);

            Assert.IsTrue(effect.TryBlockBossCast(context, new ItemBossCastAttempt("BOSS_TEST", 1000f), out var reason), reason);
            Assert.AreEqual(1, effect.EvaluatedCastCount);
            Assert.AreEqual(1, effect.BlockedCastCount);
        }

        private static int FindSeed(string stream)
        {
            for (var seed = 1; seed < 10000; seed++)
            {
                if (new RunRandom(seed).NextUnit(stream) < 0.05f) return seed;
            }

            Assert.Fail("Could not find deterministic seed in bounded test range.");
            return 1;
        }
    }
}
