using System;
using System.IO;
using DragonBound.Recruitment;
using DragonBound.Runes;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class RuneProductClosureTests
    {
        [Test]
        public void Profile_FirstCreate_SaveAndReload_PreservesInventoryAndLoadout()
        {
            WithRepository((repository, path) =>
            {
                var created = repository.Load();
                Assert.AreEqual(RuneProfileLoadStatus.Created, created.Status);
                Assert.AreEqual(1, created.Data.AccountDay);
                created.Data.AccountDay = 3;
                created.Data.Inventory.AddComplete("Power", 2);
                Assert.IsTrue(created.Data.Loadout.Assign(FirstHeroId, "Power", created.Data.Inventory));
                Assert.IsTrue(created.Data.Loadout.Assign(SecondHeroId, "Power", created.Data.Inventory));
                Assert.IsTrue(repository.Save(created.Data, out var saveError), saveError);

                var loaded = repository.Load();
                Assert.AreEqual(RuneProfileLoadStatus.Loaded, loaded.Status);
                Assert.AreEqual(RuneSaveData.CurrentSchemaVersion, loaded.Data.SchemaVersion);
                Assert.AreEqual(3, loaded.Data.AccountDay);
                Assert.AreEqual(2, loaded.Data.Inventory.OwnedCount("Power"));
                Assert.AreEqual("Power", loaded.Data.Loadout.GetRune(FirstHeroId));
                Assert.AreEqual("Power", loaded.Data.Loadout.GetRune(SecondHeroId));
                Assert.IsNull(loaded.Data.Loadout.RunStartSnapshot);
            });
        }

        [Test]
        public void Profile_UsesBackup_WhenPrimaryBecomesCorrupt()
        {
            WithRepository((repository, path) =>
            {
                var profile = new RuneSaveData { AccountDay = 3 };
                profile.Inventory.AddComplete("Power");
                Assert.IsTrue(repository.Save(profile, out var firstError), firstError);
                profile.Inventory.AddComplete("Power");
                Assert.IsTrue(repository.Save(profile, out var secondError), secondError);
                Assert.IsTrue(File.Exists(repository.BackupPath));

                File.WriteAllText(path, "{ this is not a profile }");
                var recovered = repository.Load();
                Assert.AreEqual(RuneProfileLoadStatus.RecoveredFromBackup, recovered.Status);
                Assert.AreEqual(1, recovered.Data.Inventory.OwnedCount("Power"));
            });
        }

        [Test]
        public void Profile_MigratesSchemaOneAndRejectsUnsupportedOrCorruptPayloads()
        {
            WithRepository((repository, path) =>
            {
                File.WriteAllText(path,
                    "{\"SchemaVersion\":1,\"RuneContentVersion\":\"RuneContent.V1\",\"AccountDay\":3," +
                    "\"InventoryEntries\":[{\"RuneId\":\"Power\",\"Rarity\":1,\"OwnedCount\":1,\"FragmentCount\":0}]," +
                    "\"LoadoutAssignments\":[{\"HeroId\":\"" + FirstHeroId + "\",\"RuneId\":\"Power\"}]}" );
                var migrated = repository.Load();
                Assert.AreEqual(RuneProfileLoadStatus.Loaded, migrated.Status);
                Assert.AreEqual(RuneSaveData.CurrentSchemaVersion, migrated.Data.SchemaVersion);
                Assert.AreEqual("Power", migrated.Data.Loadout.GetRune(FirstHeroId));

                File.WriteAllText(path, "{\"SchemaVersion\":999,\"RuneContentVersion\":\"RuneContent.V1\"}");
                var rejected = repository.Load();
                Assert.AreEqual(RuneProfileLoadStatus.CorruptFallback, rejected.Status);
                Assert.AreEqual(0, rejected.Data.Inventory.OwnedCount("Power"));
            });
        }

        [Test]
        public void DayThreeGate_BlocksDeepLinkCraftEquipAndDropsUntilUnlocked()
        {
            var profile = new RuneSaveData { AccountDay = 1 };
            profile.Inventory.AddComplete("Power");
            profile.Inventory.AddFragment("Ricochet", RuneInventory.EpicFragmentsPerRune);
            var gate = new RuneFeatureGate(new RuneProfileProgressionProvider(profile));
            var service = new RuneLoadoutService(profile, gate);
            var rewardService = new RuneRunRewardService(991, profile.Inventory, gate, null);

            Assert.IsFalse(service.TryEquip(FirstHeroId, "Power", out var equipError));
            Assert.AreEqual("RuneSystemLockedUntilDay3", equipError);
            Assert.IsFalse(service.TryCraft("Ricochet", out var craftError));
            Assert.AreEqual("RuneSystemLockedUntilDay3", craftError);
            Assert.IsNull(rewardService.CompleteWave(20));
            Assert.AreEqual(0, rewardService.SuccessfulRewards);

            profile.AccountDay = 3;
            Assert.IsTrue(service.TryEquip(FirstHeroId, "Power", out equipError), equipError);
            Assert.IsTrue(service.TryCraft("Ricochet", out craftError), craftError);
            Assert.AreEqual(1, profile.Inventory.OwnedCount("Ricochet"));
            Assert.IsFalse(service.TryEquip(SecondHeroId, "Power", out equipError));
            Assert.AreEqual("InsufficientOwnedCopies", equipError);
        }

        [Test]
        public void RunStartSnapshot_IsStableAfterLaterEditableLoadoutChanges()
        {
            var profile = new RuneSaveData { AccountDay = 3 };
            profile.Inventory.AddComplete("Power", 2);
            var service = new RuneLoadoutService(
                profile,
                new RuneFeatureGate(new RuneProfileProgressionProvider(profile)));
            Assert.IsTrue(service.TryEquip(FirstHeroId, "Power", out var equipError), equipError);
            Assert.IsTrue(service.LockForRunStart(out var lockError), lockError);
            var snapshot = profile.Loadout.RunStartSnapshot;
            Assert.IsNotNull(snapshot);
            Assert.AreEqual("Power", snapshot.GetRune(FirstHeroId));

            profile.Loadout.UnlockForLoadoutEditing();
            Assert.IsTrue(service.TryUnequip(FirstHeroId, out var removeError), removeError);
            Assert.IsTrue(service.TryEquip(SecondHeroId, "Power", out equipError), equipError);
            Assert.AreEqual("Power", snapshot.GetRune(FirstHeroId));
            Assert.AreEqual(string.Empty, snapshot.GetRune(SecondHeroId));
            Assert.AreEqual("Power", profile.Loadout.GetRune(SecondHeroId));
        }

        private static string FirstHeroId => HeroDefinitionCatalog.Definitions[0].Id;
        private static string SecondHeroId => HeroDefinitionCatalog.Definitions[1].Id;

        private static void WithRepository(Action<LocalRuneProfileRepository, string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), "dragonbound-rune-profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "profile.json");
                action(new LocalRuneProfileRepository(path), path);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
