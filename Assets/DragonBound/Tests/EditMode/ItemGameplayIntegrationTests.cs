using System.Linq;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.Items;
using DragonBound.Presentation;
using DragonBound.Recruitment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemGameplayIntegrationTests
    {
        [Test]
        public void RenamedActiveItemsExposeCurrentEnglishDisplayNames()
        {
            Assert.AreEqual("Winterveil Scroll", ItemCatalog.GetEnglishDisplayName(ItemIds.WinterveilRune));
            Assert.AreEqual("Arcane Thunderburst", ItemCatalog.GetEnglishDisplayName(ItemIds.RuneburstMine));
            Assert.AreEqual("Berserker War Drum", ItemCatalog.GetEnglishDisplayName(ItemIds.FrenzyRune));
            Assert.AreEqual("Tempering Hammer", ItemCatalog.GetEnglishDisplayName(ItemIds.RuneOfTempering));
        }

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
            Assert.IsTrue(runtime.JumpToWave(TwentyWavePressureConfiguration.SoulChainBossWave));
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
        public void BerserkerWarDrum_TargetsTheDeployedBasicUnitRegistryUsedByCombat()
        {
            var destination = CreateDestinationWithDeployedBasic(out var basic);
            var snapshot = CreateSnapshot(ItemIds.FrenzyRune, null);
            var runtime = new TwentyWavePressureRuntime(
                new MatchController(817), destination, null, 817,
                itemSnapshotProvider: new FixedSnapshots(snapshot, ItemRunSnapshot.Empty));

            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.TryUseItemOnUnit(
                TeamSide.Player,
                ItemIds.FrenzyRune,
                basic.RuntimeId,
                out var reason), reason);
            Assert.IsTrue(runtime.PlayerItems.UnitRegistry.TryGet(basic.RuntimeId, out var itemUnit));
            Assert.AreEqual(FrenzyRuneEffect.AttackSpeedMultiplier, itemUnit.AttackSpeedMultiplier, 0.001f);
            Assert.Greater(runtime.PlayerItems.GetCooldownRemainingSeconds(ItemIds.FrenzyRune), 59.9f);
        }

        [Test]
        public void WarforgeSigil_UpdatesTheRealRecruitCardLevel()
        {
            var destination = CreateDestinationWithDeployedBasic(out var basic);
            var snapshot = CreateSnapshot(ItemIds.WarforgeSigil, null);
            var runtime = new TwentyWavePressureRuntime(
                new MatchController(818), destination, null, 818,
                itemSnapshotProvider: new FixedSnapshots(snapshot, ItemRunSnapshot.Empty));

            Assert.AreEqual(1, basic.Level);
            Assert.IsTrue(runtime.StartRun());
            Assert.IsTrue(runtime.TryUseItemOnUnit(
                TeamSide.Player,
                ItemIds.WarforgeSigil,
                basic.RuntimeId,
                out var reason), reason);
            Assert.AreEqual(2, basic.Level);
            Assert.IsTrue(runtime.PlayerItems.UnitRegistry.TryGet(basic.RuntimeId, out var itemUnit));
            Assert.AreEqual(2, itemUnit.Level);
            Assert.Greater(runtime.PlayerItems.GetCooldownRemainingSeconds(ItemIds.WarforgeSigil), 89.9f);
        }

        [Test]
        public void RuneburstMine_KillUsesFormalEnemySettlement()
        {
            var snapshot = CreateSnapshot(ItemIds.RuneburstMine, null);
            var match = new MatchController(816);
            var runtime = new TwentyWavePressureRuntime(
                match,
                null,
                null,
                816,
                itemSnapshotProvider: new FixedSnapshots(snapshot, ItemRunSnapshot.Empty));
            CombatEvent itemCombat = default;
            bool itemCombatSeen = false;
            runtime.CombatEmitted += value =>
            {
                if (value.DamageOwnerKind != CombatDamageOwnerKind.Item) return;
                itemCombat = value;
                itemCombatSeen = true;
            };

            Assert.IsTrue(runtime.StartRun());
            runtime.Tick(TwentyWavePressureConfiguration.StartPreparationSeconds);
            Assert.AreEqual(1, runtime.PlayerAliveEnemyCount);
            int resourcesBefore = match.Player.Resources;

            Assert.IsTrue(runtime.TryUseItemAtPoint(
                TeamSide.Player,
                ItemIds.RuneburstMine,
                runtime.PlayerEnemyRegistry.Enemies.First().CombatPosition,
                out var reason), reason);

            Assert.AreEqual(0, runtime.PlayerAliveEnemyCount);
            Assert.AreEqual(1, runtime.PlayerTotalKilled);
            Assert.AreEqual(resourcesBefore + 1, match.Player.Resources);
            Assert.IsTrue(itemCombatSeen);
            Assert.IsTrue(itemCombat.Killed);
            Assert.AreEqual(CombatDamageOwnerKind.Item, itemCombat.DamageOwnerKind);
            Assert.AreEqual(ItemIds.RuneburstMine, itemCombat.DamageOwnerRuntimeId);
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
            var authoredMask = new GameObject("CooldownMask", typeof(RectTransform), typeof(Image));
            authoredMask.transform.SetParent(first.transform, false);
            var firstLabel = first.GetComponentInChildren<Text>();
            var secondLabel = second.GetComponentInChildren<Text>();
            hud.Configure(pause, pause.GetComponentInChildren<Text>(), firstLabel, firstLabel);
            hud.ConfigureActiveItemSlots(first, firstLabel, second, secondLabel);
            hud.Initialize(match, match.Player);
            hud.BindItemRuntime(runtime);

            Assert.AreEqual(string.Empty, firstLabel.text);
            Assert.AreEqual(string.Empty, secondLabel.text);
            Assert.IsTrue(first.gameObject.activeSelf);
            Assert.IsFalse(second.gameObject.activeSelf);
            first.onClick.Invoke();
            Assert.Greater(runtime.PlayerItems.GetCooldownRemainingSeconds(ItemIds.WinterveilRune), 29.9f);
            Assert.AreEqual(string.Empty, firstLabel.text);
            var cooldownMask = first.transform.Find("CooldownMask").GetComponent<Image>();
            Assert.IsTrue(cooldownMask.gameObject.activeSelf);
            Assert.Greater(cooldownMask.fillAmount, 0.99f);
            Assert.IsNotNull(cooldownMask.sprite);
            Assert.AreEqual(Image.Type.Filled, cooldownMask.type);
            Assert.AreEqual(first.transform.childCount - 1, cooldownMask.transform.GetSiblingIndex());
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

        private static BoardRecruitDestination CreateDestinationWithDeployedBasic(out RecruitCard basic)
        {
            var board = DragonBoundBoardLayout.Create(
                BattlefieldLayoutDefinitions.Compact4x4,
                TeamSide.Player);
            var destination = new BoardRecruitDestination(board);
            var batch = DragonRouteHeroDevelopmentFactory.CreateBatch(
                HeroSliceCatalog.WindclawRangerHeroId,
                "item.integration");
            destination.Commit(destination.Plan(RecruitBatch.CardsPerRecruitment), batch);
            basic = batch.Cards.First(card => card.Kind == RecruitItemKind.BasicUnit);
            Assert.IsTrue(board.TryGetPosition(basic.RuntimeId, out var source));
            var target = board.GetPositions(CellType.Battle)
                .First(position => !board.TryGetOccupant(position, out _));
            Assert.IsTrue(board.TryMove(source, target));
            return destination;
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
