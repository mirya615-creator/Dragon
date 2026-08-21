using System;
using System.Collections;
using System.IO;
using System.Linq;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Recruitment;
using DragonBound.Runes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonBound.Tests.PlayMode
{
    public sealed class RuneLoadoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_ProvidesLockedDayOneView_ThenLocksDayThreeSnapshotAndRestoresIt()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "dragonbound-rune-play-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var repository = new LocalRuneProfileRepository(Path.Combine(directory, "profile.json"));
            try
            {
                Assert.IsTrue(repository.Save(new RuneSaveData { AccountDay = 1 }, out var firstSave), firstSave);
                DragonBoundBootstrap.RuneProfileRepositoryOverrideForTests = repository;
                yield return SceneManager.LoadSceneAsync("Greybox_Main", LoadSceneMode.Single);
                yield return null;

                var firstBootstrap = FindBootstrap();
                var firstScreen = FindScreen();
                Assert.IsNotNull(firstScreen.RuneLoadoutView);
                Assert.IsNotNull(firstScreen.RecruitmentView.RuneLoadoutButton);
                firstScreen.RecruitmentView.RuneLoadoutButton.onClick.Invoke();
                yield return null;
                Assert.IsTrue(firstScreen.RuneLoadoutView.IsOpen);
                Assert.IsTrue(firstScreen.RuneLoadoutView.IsFeatureLocked);
                Assert.AreEqual(12, firstScreen.RuneLoadoutView.HeroEntryCount);
                Assert.AreEqual(14, firstScreen.RuneLoadoutView.RuneEntryCount);
                Assert.IsFalse(firstBootstrap.PlayerRuneLoadout.TryEquip(FirstHeroId, "Power", out var lockedReason));
                Assert.AreEqual("RuneSystemLockedUntilDay3", lockedReason);
                firstScreen.RuneLoadoutView.Close();

                DragonBoundBootstrap.RuneProfileRepositoryOverrideForTests = null;
                yield return SceneManager.LoadSceneAsync("HeroSlice_Main", LoadSceneMode.Single);
                yield return null;
                var profile = new RuneSaveData { AccountDay = 3 };
                profile.Inventory.AddComplete("Power");
                Assert.IsTrue(repository.Save(profile, out var secondSave), secondSave);

                DragonBoundBootstrap.RuneProfileRepositoryOverrideForTests = repository;
                yield return SceneManager.LoadSceneAsync("Greybox_Main", LoadSceneMode.Single);
                yield return null;
                var bootstrap = FindBootstrap();
                var screen = FindScreen();
                screen.RecruitmentView.RuneLoadoutButton.onClick.Invoke();
                yield return null;
                Assert.IsFalse(screen.RuneLoadoutView.IsFeatureLocked);
                var powerEntry = screen.RuneLoadoutView.GetComponentsInChildren<DragonBound.Presentation.RuneLoadoutEntryView>(true)
                    .Single(entry => entry.EntryId == "Power");
                powerEntry.Button.onClick.Invoke();
                Assert.AreEqual("Power", bootstrap.PlayerRuneLoadout.Loadout.GetRune(FirstHeroId));
                screen.RuneLoadoutView.Close();

                while (bootstrap.Match.State == MatchState.Ready)
                {
                    yield return null;
                }

                var snapshot = bootstrap.RuneSaveData.Loadout.RunStartSnapshot;
                Assert.IsNotNull(snapshot);
                Assert.AreEqual("Power", snapshot.GetRune(FirstHeroId));
                Assert.IsFalse(bootstrap.PlayerRuneLoadout.TryUnequip(FirstHeroId, out var runningReason));
                Assert.AreEqual("RunInProgress", runningReason);

                yield return SceneManager.LoadSceneAsync("HeroSlice_Main", LoadSceneMode.Single);
                yield return null;
                DragonBoundBootstrap.RuneProfileRepositoryOverrideForTests = repository;
                yield return SceneManager.LoadSceneAsync("Greybox_Main", LoadSceneMode.Single);
                yield return null;
                Assert.AreEqual("Power", FindBootstrap().PlayerRuneLoadout.Loadout.GetRune(FirstHeroId));

                DragonBoundBootstrap.RuneProfileRepositoryOverrideForTests = null;
                yield return SceneManager.LoadSceneAsync("HeroSlice_Main", LoadSceneMode.Single);
                yield return null;
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            finally
            {
                DragonBoundBootstrap.RuneProfileRepositoryOverrideForTests = null;
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static DragonBoundBootstrap FindBootstrap()
        {
            var bootstrap = UnityEngine.Object.FindObjectOfType<DragonBoundBootstrap>();
            Assert.IsNotNull(bootstrap);
            return bootstrap;
        }

        private static DragonBound.Presentation.DragonBoundScreenView FindScreen()
        {
            var screen = UnityEngine.Object.FindObjectOfType<DragonBound.Presentation.DragonBoundScreenView>();
            Assert.IsNotNull(screen);
            return screen;
        }

        private static string FirstHeroId => HeroDefinitionCatalog.Definitions[0].Id;
    }
}
