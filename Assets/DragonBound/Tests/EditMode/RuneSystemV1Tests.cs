using NUnit.Framework;
using DragonBound.Runes;
using GameShared.Random;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneSystemV1Tests
    {
        [Test] public void Catalog_ContainsExactlyFourteenConfiguredRunes()
        {
            Assert.AreEqual(14, RuneCatalog.All.Count); Assert.AreEqual(RuneRarity.Legendary, RuneCatalog.Get("Warcry").Rarity);
            var presentation = new RunePresentationCatalog().Get("Warcry"); Assert.IsNotEmpty(presentation.IconKey); Assert.IsTrue(presentation.UsesGreyboxPlaceholder);
        }

        [Test] public void Inventory_PreservesDuplicatesAndCraftsEpicAndLegendary()
        {
            var inventory = new RuneInventory(); inventory.AddComplete("Ricochet", 3); Assert.AreEqual(3, inventory.OwnedCount("Ricochet"));
            inventory.AddFragment("Ricochet", 3); Assert.IsTrue(inventory.CanCraftRune("Ricochet")); Assert.IsTrue(inventory.CraftRune("Ricochet"));
            inventory.AddFragment("Warcry", 5); Assert.IsTrue(inventory.CraftRune("Warcry")); Assert.AreEqual(1, inventory.OwnedCount("Warcry"));
            Assert.Throws<System.InvalidOperationException>(() => inventory.AddFragment("Might"));
        }

        [Test] public void Loadout_ValidatesCopiesAndLocksAtRunStart()
        {
            var inventory = new RuneInventory(); inventory.AddComplete("Power", 2);
            var loadout = new HeroRuneLoadout(); Assert.IsTrue(loadout.Assign("HeroA", "Power", inventory)); Assert.IsTrue(loadout.Assign("HeroB", "Power", inventory));
            Assert.IsFalse(loadout.Assign("HeroC", "Power", inventory)); Assert.IsTrue(loadout.LockAtRunStart(inventory)); Assert.IsFalse(loadout.Unassign("HeroA"));
        }

        [Test] public void Drops_AreWaveGatedCappedAndDeterministic()
        {
            var left = new RuneDropState(); var right = new RuneDropState();
            Assert.IsNull(RuneDropRules.TryRollCompletedWave(1, 2, left));
            for (var wave = 3; wave <= 20; wave++)
            {
                var a = RuneDropRules.TryRollCompletedWave(9182, wave, left); var b = RuneDropRules.TryRollCompletedWave(9182, wave, right);
                Assert.AreEqual(a == null, b == null); if (a != null) { Assert.AreEqual(a.RuneId, b.RuneId); Assert.AreEqual(a.IsComplete, b.IsComplete); }
            }
            Assert.LessOrEqual(left.SuccessfulRewards, 4);
        }

        [Test] public void Drops_UseConfiguredWaveBandsAndKeepEarlyWavesOutOfThePool()
        {
            Assert.AreEqual(0f, RuneDropRules.ChanceForWave(1));
            Assert.AreEqual(0f, RuneDropRules.ChanceForWave(2));
            Assert.AreEqual(.12f, RuneDropRules.ChanceForWave(3));
            Assert.AreEqual(.18f, RuneDropRules.ChanceForWave(7));
            Assert.AreEqual(.28f, RuneDropRules.ChanceForWave(13));
            Assert.AreEqual(.40f, RuneDropRules.ChanceForWave(17));
            Assert.IsNull(RuneDropRules.TryRollCompletedWave(42, 1, new RuneDropState()));
            Assert.AreEqual(RuneRarity.Excellent, RuneDropRules.RollRarity(6, new FixedRandom(.99f), "test"));
            Assert.AreEqual(RuneRarity.Legendary, RuneDropRules.RollRarity(12, new FixedRandom(.99f), "test"));
            Assert.AreEqual(RuneRarity.Legendary, RuneDropRules.RollRarity(20, new FixedRandom(.99f), "test"));
        }

        [Test] public void RewardCap_PreventsFurtherCompletedWaveRollsAfterFourSuccesses()
        {
            var state = new RuneDropState();
            Assert.IsTrue(state.RecordSuccess());
            Assert.IsTrue(state.RecordSuccess());
            Assert.IsTrue(state.RecordSuccess());
            Assert.IsTrue(state.RecordSuccess());
            Assert.IsTrue(state.IsCapped);
            Assert.IsNull(RuneDropRules.TryRollCompletedWave(8675309, 20, state));
        }

        [Test] public void Legendary_DropsAreAlwaysFragments_WhenDropOccurs()
        {
            var inventory = new RuneInventory(); var state = new RuneDropState();
            for (var seed = 1; seed < 20000; seed++)
            {
                var reward = RuneDropRules.TryRollCompletedWave(seed, 20, state);
                if (reward != null && reward.Rarity == RuneRarity.Legendary) { Assert.IsFalse(reward.IsComplete); Assert.IsTrue(reward.IsFragment); RuneDropRules.GrantToInventory(reward, inventory); return; }
                if (state.IsCapped) state = new RuneDropState();
            }
            Assert.Fail("Expected a deterministic Legendary test reward.");
        }

        [Test] public void ModifierPipeline_AppliesMightPowerAndFarreachWithoutMutatingBase()
        {
            var input = new RuneModifierInput { BaseAttackDamage = 100f, BaseRange = 2f };
            Assert.AreEqual(108f, RuneModifierPipeline.Evaluate(input, RuneCatalog.Get("Might")).AttackDamage, .001f);
            Assert.AreEqual(115f, RuneModifierPipeline.Evaluate(input, RuneCatalog.Get("Power")).AttackDamage, .001f);
            Assert.AreEqual(2.75f, RuneModifierPipeline.Evaluate(input, RuneCatalog.Get("Farreach")).Range, .001f);
            Assert.AreEqual(100f, input.BaseAttackDamage);
        }

        [Test] public void RuneEventLayer_KeepsCountersPerRuntimeHero()
        {
            var layer = new RuneEventLayer();
            for (var i = 0; i < 10; i++) layer.Emit(new RuneCombatEvent(RuneCombatEventType.OnBasicAttackSucceeded, "H1", "Hero", DragonBound.Core.TeamSide.Player));
            Assert.AreEqual(10, layer.GetOrCreate("H1").BasicAttackCounter); Assert.AreEqual(0, layer.GetOrCreate("H2").BasicAttackCounter);
        }

        private sealed class FixedRandom : IRunRandom
        {
            private readonly float unit;
            public FixedRandom(float unit) { this.unit = unit; }
            public int Seed => 1;
            public long CallIndex { get; private set; }
            public int NextInt(string context, int minInclusive, int maxExclusive) { CallIndex++; return minInclusive; }
            public float NextUnit(string context) { CallIndex++; return unit; }
        }
    }
}
