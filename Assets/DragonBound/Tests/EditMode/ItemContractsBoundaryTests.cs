using System;
using NUnit.Framework;
using DragonBound.Foundation.Contracts;
using DragonBound.Items.Contracts;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemContractsBoundaryTests
    {
        [Test]
        public void Catalog_PreservesTwentyStableIdsAndConfiguredImplementationState()
        {
            Assert.AreEqual(20, ItemIds.All.Count);
            Assert.AreEqual(20, ItemCatalog.All.Count);

            var configured = 0;
            var pending = 0;
            for (var i = 0; i < ItemCatalog.All.Count; i++)
            {
                var entry = ItemCatalog.All[i];
                if (entry.State == ItemConfigurationState.Configured) configured++;
                if (entry.State == ItemConfigurationState.Pending) pending++;
            }

            Assert.AreEqual(20, configured);
            Assert.AreEqual(0, pending);
            Assert.IsTrue(ItemCatalog.TryGet(ItemIds.WinterveilRune, out var winterveil));
            Assert.AreEqual(ItemCategory.Active, winterveil.Category);
            Assert.IsTrue(ItemCatalog.TryGet(ItemIds.DrakeheartRelic, out var drakeheart));
            Assert.AreEqual(ItemCategory.Passive, drakeheart.Category);
            Assert.IsFalse(ItemCatalog.TryGet(new ItemId("ITEM_UNKNOWN"), out _));
        }

        [Test]
        public void Snapshot_CopiesCollectionsAndCarriesTypedNotConfiguredState()
        {
            var active = new[] { ItemIds.WinterveilRune };
            var snapshot = new ItemSnapshot(
                new RunId(17),
                active,
                new[] { ItemIds.DrakeheartRelic },
                ItemSnapshotState.Ready);
            active[0] = ItemIds.WyrmfangSnare;

            Assert.IsTrue(snapshot.IsReady);
            Assert.AreEqual(ItemIds.WinterveilRune, snapshot.ActiveItems[0]);
            Assert.AreEqual(ItemIds.DrakeheartRelic, snapshot.PassiveItems[0]);
            Assert.AreEqual(ItemSnapshotState.NotConfigured, ItemSnapshot.Empty.State);
        }

        [Test]
        public void CommandsAndResults_KeepMerchantAdAndLedgerOpaque()
        {
            var claim = new AdRewardClaim("claim-1", "item-reward", "session-1", new DayKey("server-day"));
            var merchant = new MerchantSelection("offer-1", ItemIds.WinterveilRune);
            var claimCommand = ItemCommand.Claim(claim);
            var selectCommand = ItemCommand.Select(merchant);
            var result = new ItemResult(
                ItemResultState.Pending,
                ItemIds.WinterveilRune,
                "AwaitingAuthority",
                new Cooldown(30f, 30f),
                new LedgerReference("opaque-ledger-ref"));

            Assert.AreEqual(ItemCommandKind.ClaimAdReward, claimCommand.Kind);
            Assert.AreEqual("session-1", claimCommand.AdRewardClaim.ClientSessionId);
            Assert.AreEqual(ItemCommandKind.SelectMerchantOffer, selectCommand.Kind);
            Assert.IsTrue(selectCommand.MerchantSelection.IsValid);
            Assert.AreEqual(ItemResultState.Pending, result.State);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.LedgerReference.IsAssigned);
        }

        [Test]
        public void Cooldown_ExposesReadinessWithoutImplementingEffectNumbers()
        {
            Assert.IsTrue(Cooldown.Ready.IsReady);
            Assert.IsFalse(new Cooldown(30f, 30f).IsReady);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Cooldown(5f, 6f));
        }

        [Test]
        public void AuthorityPorts_AcceptOnlySuppliedAccountProgressAndDayKey()
        {
            var progressProvider = new FixedProgressProvider(new AccountProgress(5));
            var dayKeyProvider = new FixedDayKeyProvider(new DayKey("server-day"));

            Assert.IsTrue(progressProvider.TryGetAccountProgress(out var progress));
            Assert.AreEqual(5, progress.NormalCompletedMatchCount);
            Assert.IsTrue(dayKeyProvider.TryGetDayKey(out var dayKey));
            Assert.AreEqual("server-day", dayKey.Value);
        }

        private sealed class FixedProgressProvider : IItemAccountProgressProvider
        {
            private readonly AccountProgress progress;

            public FixedProgressProvider(AccountProgress progress) { this.progress = progress; }

            public bool TryGetAccountProgress(out AccountProgress value)
            {
                value = progress;
                return true;
            }
        }

        private sealed class FixedDayKeyProvider : IItemDayKeyProvider
        {
            private readonly DayKey dayKey;

            public FixedDayKeyProvider(DayKey dayKey) { this.dayKey = dayKey; }

            public bool TryGetDayKey(out DayKey value)
            {
                value = dayKey;
                return true;
            }
        }
    }
}
