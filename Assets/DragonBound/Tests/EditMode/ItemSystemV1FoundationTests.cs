using System.Collections.Generic;
using System.Linq;
using DragonBound.Core;
using DragonBound.Items;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemSystemV1FoundationTests
    {
        [Test]
        public void Catalog_HasTwentyUniqueStableIdsAndOnlyTwoFormalCandidates()
        {
            Assert.AreEqual(20, ItemCatalog.All.Count);
            Assert.AreEqual(20, ItemCatalog.All.Select(definition => definition.ItemId).Distinct().Count());
            Assert.AreEqual(20, ItemCatalog.FormalCandidates.Count);
            Assert.AreEqual(0, ItemCatalog.All.Count(definition => definition.Status == ItemImplementationStatus.Pending));
            Assert.AreEqual(2, ItemLoadout.MaxActiveItems);
            Assert.AreEqual(6, ItemLoadout.MaxPassiveItems);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.DrakeheartRelic).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.WinterveilRune).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.WyrmfangSnare).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.DragonfallJudgment).IsFormalCandidate);
            Assert.IsTrue(ItemCatalog.Get(ItemIds.DraconicPresence).IsFormalCandidate);
        }

        [Test]
        public void Inventory_EnforcesOneOwnedCopyAndPreservesFragments()
        {
            var inventory = new ItemDailyInventory();
            Assert.IsTrue(inventory.TryGrantOwned(ItemIds.DrakeheartRelic));
            Assert.IsFalse(inventory.TryGrantOwned(ItemIds.DrakeheartRelic));
            Assert.IsTrue(inventory.TryAddFragments(ItemIds.WyrmfangSnare, 2));
            Assert.AreEqual(1, inventory.OwnedCount(ItemIds.DrakeheartRelic));
            Assert.AreEqual(2, inventory.FragmentCount(ItemIds.WyrmfangSnare));
        }

        [Test]
        public void Loadout_RejectsUnownedPendingAndDuplicateItems()
        {
            var inventory = new ItemDailyInventory();
            var loadout = new ItemLoadout();
            string reason;
            Assert.IsFalse(loadout.TryEquip(ItemIds.DrakeheartRelic, inventory, out reason));
            Assert.AreEqual(ItemOperationFailure.NotOwned, reason);
            Assert.IsTrue(inventory.TryGrantOwned(ItemIds.DrakeheartRelic));
            Assert.IsTrue(loadout.TryEquip(ItemIds.DrakeheartRelic, inventory, out reason));
            Assert.IsFalse(loadout.TryEquip(ItemIds.DrakeheartRelic, inventory, out reason));
            Assert.AreEqual(ItemOperationFailure.DuplicateItem, reason);
            Assert.IsTrue(inventory.TryGrantOwned(ItemIds.WinterveilRune));
            Assert.IsFalse(loadout.TryEquip("ITEM_UNKNOWN", inventory, out reason));
            Assert.AreEqual(ItemOperationFailure.UnknownItem, reason);
        }

        [Test]
        public void Profile_FifthNormalCompletionPermanentlyUnlocksAcrossDayChanges()
        {
            var profile = new ItemProfile();
            string reason;
            Assert.IsTrue(profile.RefreshDay(new FixedDay("day-1"), out reason));
            Assert.IsTrue(profile.RefreshAuthoritativeAccountProgress(new FixedProgress(4), out reason));
            Assert.IsFalse(profile.IsUnlocked);
            Assert.IsFalse(profile.TryCreateRunSnapshot(out _, out reason));
            Assert.AreEqual(ItemOperationFailure.Locked, reason);

            Assert.IsTrue(profile.RefreshDay(new FixedDay("day-2"), out reason));
            Assert.IsTrue(profile.RefreshAuthoritativeAccountProgress(new FixedProgress(5), out reason));
            Assert.IsTrue(profile.IsUnlocked);
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.DrakeheartRelic));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.DrakeheartRelic, profile.Inventory, out reason));
            Assert.IsTrue(profile.RefreshDay(new FixedDay("day-3"), out reason));
            Assert.AreEqual(0, profile.Inventory.OwnedCount(ItemIds.DrakeheartRelic));
            Assert.IsFalse(profile.Loadout.Contains(ItemIds.DrakeheartRelic));
            Assert.IsTrue(profile.IsUnlocked);
        }

        [Test]
        public void Profile_RejectsLegacyDayNumberSchemaInsteadOfTreatingItAsCompletedMatches()
        {
            var profile = new ItemProfile();
            var legacy = new ItemProfileData
            {
                SchemaVersion = 1,
                DayKey = "day-99"
            };

            Assert.IsFalse(profile.TryRestorePersistentData(legacy, out var reason));
            Assert.AreEqual(ItemOperationFailure.IncompatibleSchema, reason);
        }

        [Test]
        public void AccountProgressRule_CountsVictoryAndDefeatButNotAbnormalExit()
        {
            Assert.IsTrue(ItemAccountProgressRules.CountsAsNormalCompletedMatch(ItemMatchCompletionOutcome.Victory));
            Assert.IsTrue(ItemAccountProgressRules.CountsAsNormalCompletedMatch(ItemMatchCompletionOutcome.Defeat));
            Assert.IsFalse(ItemAccountProgressRules.CountsAsNormalCompletedMatch(ItemMatchCompletionOutcome.AbnormalExit));
        }

        [Test]
        public void RunSnapshot_IsImmutableAcrossLoadoutEditsAndPersistentCopy()
        {
            var profile = CreateUnlockedProfile();
            string reason;
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.DrakeheartRelic));
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.WinterveilRune));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.DrakeheartRelic, profile.Inventory, out reason));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.WinterveilRune, profile.Inventory, out reason));
            Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out reason));
            Assert.IsTrue(snapshot.Contains(ItemIds.DrakeheartRelic));
            Assert.IsTrue(profile.Loadout.TryUnequip(ItemIds.DrakeheartRelic));
            Assert.IsTrue(snapshot.Contains(ItemIds.DrakeheartRelic));

            var data = profile.CreatePersistentData();
            var restored = new ItemProfile();
            Assert.IsTrue(restored.TryRestorePersistentData(data, out reason));
            Assert.IsTrue(restored.Loadout.Contains(ItemIds.WinterveilRune));
        }

        [Test]
        public void PersistentRestore_PreservesCompletionCountAndPermanentUnlock()
        {
            var profile = CreateUnlockedProfile();
            var data = profile.CreatePersistentData();
            var restored = new ItemProfile();

            Assert.IsTrue(restored.TryRestorePersistentData(data, out var reason));
            Assert.AreEqual(5, restored.NormalCompletedMatchCount);
            Assert.IsTrue(restored.IsUnlocked);
        }

        [Test]
        public void Drakeheart_IncreasesMaxAndCurrentHeartAtRunStart()
        {
            var profile = CreateUnlockedProfile();
            string reason;
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.DrakeheartRelic));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.DrakeheartRelic, profile.Inventory, out reason));
            Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out reason));
            var team = new TeamState(TeamSide.Player);
            var runtime = new ItemRunRuntime(snapshot, team, new EnemyRegistry());
            Assert.IsTrue(runtime.StartRun(out reason));
            Assert.AreEqual(6, team.HatchlingMaxHealth);
            Assert.AreEqual(6, team.HatchlingHealth);
        }

        [Test]
        public void Winterveil_SlowsAllAliveEnemyTypesAndRestoresAfterFiveSeconds()
        {
            var profile = CreateUnlockedProfile();
            string reason;
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.WinterveilRune));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.WinterveilRune, profile.Inventory, out reason));
            Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out reason));
            var team = new TeamState(TeamSide.Player);
            var registry = new EnemyRegistry();
            var enemies = new List<EnemyRuntime>
            {
                new EnemyRuntime("normal", TeamSide.Player, archetype: EnemyArchetype.Normal),
                new EnemyRuntime("fast", TeamSide.Player, archetype: EnemyArchetype.Fast),
                new EnemyRuntime("elite", TeamSide.Player, archetype: EnemyArchetype.Elite),
                new EnemyRuntime("boss", TeamSide.Player, archetype: EnemyArchetype.Boss)
            };
            foreach (var enemy in enemies) registry.Register(enemy);
            var runtime = new ItemRunRuntime(snapshot, team, registry);
            Assert.IsTrue(runtime.StartRun(out reason));
            Assert.IsTrue(runtime.TryUse(ItemIds.WinterveilRune, out reason));
            Assert.AreEqual(30f, ((WinterveilRuneEffect)runtimeEffect(runtime, ItemIds.WinterveilRune)).CooldownRemainingSeconds, .001f);
            foreach (var enemy in enemies) Assert.AreEqual(.9f, enemy.MovementSpeedMultiplier, .001f);
            foreach (var enemy in enemies) enemy.TickControl(5f);
            foreach (var enemy in enemies) Assert.AreEqual(1f, enemy.MovementSpeedMultiplier, .001f);
            runtime.Tick(30f);
            Assert.IsTrue(runtime.TryUse(ItemIds.WinterveilRune, out reason));
        }

        [Test]
        public void ItemEffects_AreIndependentAcrossRuns()
        {
            var profile = CreateUnlockedProfile();
            string reason;
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.DrakeheartRelic));
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.WinterveilRune));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.DrakeheartRelic, profile.Inventory, out reason));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.WinterveilRune, profile.Inventory, out reason));
            Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out reason));
            var first = new TeamState(TeamSide.Player);
            var second = new TeamState(TeamSide.Player);
            var firstRuntime = new ItemRunRuntime(snapshot, first, CreateRegistry("first"));
            var secondRuntime = new ItemRunRuntime(snapshot, second, CreateRegistry("second"));
            Assert.IsTrue(firstRuntime.StartRun(out reason));
            Assert.IsTrue(secondRuntime.StartRun(out reason));
            Assert.AreEqual(6, first.HatchlingMaxHealth);
            Assert.AreEqual(6, second.HatchlingMaxHealth);
            Assert.IsTrue(firstRuntime.TryUse(ItemIds.WinterveilRune, out reason));
            Assert.IsTrue(secondRuntime.TryUse(ItemIds.WinterveilRune, out reason));
            Assert.AreEqual(30f, ((WinterveilRuneEffect)runtimeEffect(firstRuntime, ItemIds.WinterveilRune)).CooldownRemainingSeconds, .001f);
            Assert.AreEqual(30f, ((WinterveilRuneEffect)runtimeEffect(secondRuntime, ItemIds.WinterveilRune)).CooldownRemainingSeconds, .001f);
        }

        private static ItemProfile CreateUnlockedProfile()
        {
            var profile = new ItemProfile();
            string reason;
            Assert.IsTrue(profile.RefreshDay(new FixedDay("day-2"), out reason));
            Assert.IsTrue(profile.RefreshAuthoritativeAccountProgress(new FixedProgress(5), out reason));
            return profile;
        }

        private static EnemyRegistry CreateRegistry(string prefix)
        {
            var registry = new EnemyRegistry();
            registry.Register(new EnemyRuntime(prefix + ".enemy", TeamSide.Player));
            return registry;
        }

        private static IItemEffectRuntime runtimeEffect(ItemRunRuntime runtime, string itemId)
        {
            IItemEffectRuntime effect;
            Assert.IsTrue(runtime.TryGetEffect(itemId, out effect));
            return effect;
        }

        private sealed class FixedDay : IItemDayKeyProvider
        {
            private readonly string key;

            public FixedDay(string key)
            {
                this.key = key;
            }

            public string GetDayKey() { return key; }
        }

        private sealed class FixedProgress : IItemAccountProgressProvider
        {
            private readonly int count;

            public FixedProgress(int count) { this.count = count; }
            public bool TryGetNormalCompletedMatchCount(out int completedMatchCount)
            {
                completedMatchCount = count;
                return true;
            }
        }
    }
}
