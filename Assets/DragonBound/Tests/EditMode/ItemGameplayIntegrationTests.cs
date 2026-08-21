using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemGameplayIntegrationTests
    {
        [Test]
        public void TwentyWaveRun_LocksIndependentSnapshotsAndAppliesDrakeheartOnlyOnce()
        {
            var playerSnapshot = CreateSnapshot(ItemIds.WinterveilRune, ItemIds.DrakeheartRelic);
            var aiSnapshot = CreateSnapshot(null, ItemIds.DrakeheartRelic);
            var match = new MatchController(812);
            var runtime = new TwentyWavePressureRuntime(
                match, null, null, 812, itemSnapshotProvider: new FixedSnapshots(playerSnapshot, aiSnapshot));

            Assert.IsTrue(runtime.StartRun());
            Assert.AreEqual(6, match.Player.HatchlingMaxHealth);
            Assert.AreEqual(6, match.AI.HatchlingMaxHealth);
            Assert.AreSame(playerSnapshot, runtime.PlayerItems.Snapshot);
            Assert.AreSame(aiSnapshot, runtime.AiItems.Snapshot);

            Assert.IsFalse(runtime.StartRun());
            Assert.AreEqual(6, match.Player.HatchlingMaxHealth);
            Assert.AreNotSame(runtime.PlayerItems, runtime.AiItems);
            Assert.IsFalse(runtime.AiItems.Snapshot.IsActive(ItemIds.WinterveilRune));
        }

        [Test]
        public void TwentyWaveCommand_TargetsOwnNormalAndBoss_RecoversAndKeepsBothSidesIsolated()
        {
            var playerSnapshot = CreateSnapshot(ItemIds.WinterveilRune, null);
            var aiSnapshot = CreateSnapshot(ItemIds.WinterveilRune, null);
            var runtime = new TwentyWavePressureRuntime(
                new MatchController(813), null, null, 813,
                itemSnapshotProvider: new FixedSnapshots(playerSnapshot, aiSnapshot));

            Assert.IsTrue(runtime.StartRun());
            Assert.IsFalse(runtime.TryUseItem(TeamSide.Player, ItemIds.WinterveilRune, out var reason));
            Assert.AreEqual("NoAliveTargets", reason);
            Assert.AreEqual(0f, runtime.PlayerItems.GetCooldownRemainingSeconds(ItemIds.WinterveilRune));

            runtime.Tick(4f);
            Assert.IsTrue(runtime.JumpToWave(6));
            var playerBoss = runtime.PlayerW6Boss;
            var aiBoss = runtime.AiW6Boss;
            Assert.IsNotNull(playerBoss);
            Assert.IsNotNull(aiBoss);
            Assert.IsTrue(runtime.TryUseItem(TeamSide.Player, ItemIds.WinterveilRune, out reason));
            Assert.AreEqual(0.9f, playerBoss.MovementSpeedMultiplier, 0.001f);
            Assert.AreEqual(1f, aiBoss.MovementSpeedMultiplier, 0.001f);
            Assert.IsTrue(runtime.PlayerItems.GetCooldownRemainingSeconds(ItemIds.WinterveilRune) > 29.9f);
            Assert.AreEqual(0f, runtime.AiItems.GetCooldownRemainingSeconds(ItemIds.WinterveilRune));
            Assert.IsFalse(runtime.TryUseItem(TeamSide.Player, ItemIds.WinterveilRune, out reason));
            Assert.AreEqual("Cooldown", reason);

            runtime.Tick(5f);
            Assert.AreEqual(1f, playerBoss.MovementSpeedMultiplier, 0.001f);
            runtime.Tick(25f);
            Assert.AreEqual(0f, runtime.PlayerItems.GetCooldownRemainingSeconds(ItemIds.WinterveilRune), 0.001f);
        }

        [Test]
        public void Hud_BindsTwoSlotsAndClicksFormalPlayerItemCommand()
        {
            var snapshot = CreateSnapshot(ItemIds.WinterveilRune, null);
            var match = new MatchController(814);
            var runtime = new TwentyWavePressureRuntime(
                match, null, null, 814, itemSnapshotProvider: new FixedSnapshots(snapshot, ItemRunSnapshot.Empty));
            Assert.IsTrue(runtime.StartRun());
            runtime.Tick(4f);

            var root = new GameObject("ItemHudTest");
            var hud = root.AddComponent<GreyboxHudView>();
            var pause = CreateButton(root.transform, "Pause");
            var first = CreateButton(root.transform, "Item1");
            var second = CreateButton(root.transform, "Item2");
            var firstLabel = first.GetComponentInChildren<Text>();
            var secondLabel = second.GetComponentInChildren<Text>();
            hud.Configure(pause, pause.GetComponentInChildren<Text>(), firstLabel, firstLabel, firstLabel, firstLabel, firstLabel);
            hud.ConfigureActiveItemSlots(first, firstLabel, second, secondLabel);
            hud.Initialize(match, match.Player);
            hud.BindItemRuntime(runtime);

            StringAssert.Contains(ItemIds.WinterveilRune, firstLabel.text);
            Assert.AreEqual("EMPTY", secondLabel.text);
            first.onClick.Invoke();
            Assert.Greater(runtime.PlayerItems.GetCooldownRemainingSeconds(ItemIds.WinterveilRune), 29.9f);
            StringAssert.Contains("CD 30s", firstLabel.text);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void TwentyWaveRun_UsesItsLockedSnapshotAfterTheSourceLoadoutChanges()
        {
            var profile = CreateProfile(ItemIds.WinterveilRune, null);
            Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out _));
            var runtime = new TwentyWavePressureRuntime(
                new MatchController(815), null, null, 815,
                itemSnapshotProvider: new FixedSnapshots(snapshot, ItemRunSnapshot.Empty));
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(profile.Loadout.TryUnequip(ItemIds.WinterveilRune));
            runtime.Tick(4f);

            Assert.IsTrue(runtime.TryUseItem(TeamSide.Player, ItemIds.WinterveilRune, out _));
            Assert.IsTrue(runtime.PlayerItems.Snapshot.IsActive(ItemIds.WinterveilRune));
        }

        private static ItemRunSnapshot CreateSnapshot(string active, string passive)
        {
            var profile = CreateProfile(active, passive);
            Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out _));
            return snapshot;
        }

        private static ItemProfile CreateProfile(string active, string passive)
        {
            var profile = new ItemProfile();
            Assert.IsTrue(profile.RefreshDay(new FixedDayKey(), out _));
            Assert.IsTrue(profile.RefreshAuthoritativeAccountProgress(new FixedProgress(), out _));
            if (!string.IsNullOrEmpty(active))
            {
                Assert.IsTrue(profile.Inventory.TryGrantOwned(active));
                Assert.IsTrue(profile.Loadout.TryEquip(active, profile.Inventory, out _));
            }
            if (!string.IsNullOrEmpty(passive))
            {
                Assert.IsTrue(profile.Inventory.TryGrantOwned(passive));
                Assert.IsTrue(profile.Loadout.TryEquip(passive, profile.Inventory, out _));
            }
            return profile;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return buttonObject.GetComponent<Button>();
        }

        private sealed class FixedSnapshots : IItemRunSnapshotProvider
        {
            private readonly ItemRunSnapshot player;
            private readonly ItemRunSnapshot ai;
            public FixedSnapshots(ItemRunSnapshot player, ItemRunSnapshot ai) { this.player = player; this.ai = ai; }
            public bool TryGetValidatedSnapshots(out ItemRunSnapshot playerSnapshot, out ItemRunSnapshot aiSnapshot, out string reason)
            {
                playerSnapshot = player;
                aiSnapshot = ai;
                reason = ItemOperationFailure.None;
                return true;
            }
        }

        private sealed class FixedDayKey : IItemDayKeyProvider
        {
            public string GetDayKey() { return "server-day"; }
        }

        private sealed class FixedProgress : IItemAccountProgressProvider
        {
            public bool TryGetNormalCompletedMatchCount(out int completedMatchCount)
            {
                completedMatchCount = 5;
                return true;
            }
        }
    }
}
