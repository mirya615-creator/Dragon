using DragonBound.Items;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class ItemProfileSnapshotProviderTests
    {
        [Test]
        public void ProviderBuildsPlayerSnapshotFromValidatedProfileAndPreservesAiSnapshot()
        {
            var profile = CreateUnlockedProfile();
            Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.WinterveilRune));
            Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.WinterveilRune, profile.Inventory, out _));
            var aiSnapshot = ItemRunSnapshot.Empty;
            var provider = new ItemProfileRunSnapshotProvider(
                new FixedSource(profile, aiSnapshot));

            Assert.IsTrue(provider.TryGetValidatedSnapshots(out var player, out var ai, out var reason), reason);
            Assert.IsTrue(player.IsActive(ItemIds.WinterveilRune));
            Assert.AreSame(aiSnapshot, ai);
        }

        [Test]
        public void ProviderRejectsLockedProfileWithoutFabricatingProgressOrLoadout()
        {
            var provider = new ItemProfileRunSnapshotProvider(
                new FixedSource(new ItemProfile(), ItemRunSnapshot.Empty));

            Assert.IsFalse(provider.TryGetValidatedSnapshots(out var player, out var ai, out var reason));
            Assert.IsNull(player);
            Assert.IsNull(ai);
            Assert.AreEqual(ItemOperationFailure.Locked, reason);
        }

        [Test]
        public void ProviderPassesThroughAuthoritativeSourceFailure()
        {
            var provider = new ItemProfileRunSnapshotProvider(new FailingSource());

            Assert.IsFalse(provider.TryGetValidatedSnapshots(out _, out _, out var reason));
            Assert.AreEqual("ServerProfileUnavailable", reason);
        }

        private static ItemProfile CreateUnlockedProfile()
        {
            var profile = new ItemProfile();
            Assert.IsTrue(profile.RefreshDay(new FixedDayKey(), out _));
            Assert.IsTrue(profile.RefreshAuthoritativeAccountProgress(new FixedProgress(), out _));
            return profile;
        }

        private sealed class FixedSource : IItemValidatedProfileSnapshotSource
        {
            private readonly ItemProfile profile;
            private readonly ItemRunSnapshot aiSnapshot;

            public FixedSource(ItemProfile profile, ItemRunSnapshot aiSnapshot)
            {
                this.profile = profile;
                this.aiSnapshot = aiSnapshot;
            }

            public bool TryGetValidatedPlayerProfile(
                out ItemProfile result,
                out ItemRunSnapshot ai,
                out string reason)
            {
                result = profile;
                ai = aiSnapshot;
                reason = ItemOperationFailure.None;
                return true;
            }
        }

        private sealed class FailingSource : IItemValidatedProfileSnapshotSource
        {
            public bool TryGetValidatedPlayerProfile(
                out ItemProfile profile,
                out ItemRunSnapshot aiSnapshot,
                out string reason)
            {
                profile = null;
                aiSnapshot = null;
                reason = "ServerProfileUnavailable";
                return false;
            }
        }

        private sealed class FixedDayKey : IItemDayKeyProvider
        {
            public string GetDayKey() => "server-day";
        }

        private sealed class FixedProgress : IItemAccountProgressProvider
        {
            public bool TryGetNormalCompletedMatchCount(out int completedMatchCount)
            {
                completedMatchCount = ItemProfile.UnlockCompletedMatchCount;
                return true;
            }
        }
    }
}
