using DragonBound.Items;
using NUnit.Framework;

namespace DragonBound.Tests.EditMode
{
    public sealed class DevelopmentItemRunSnapshotProviderTests
    {
        [Test]
        public void ConfigureBuildsValidatedPlayerSnapshotAndKeepsAiEmptyByDefault()
        {
            var provider = new DevelopmentItemRunSnapshotProvider();

            Assert.IsTrue(provider.TryConfigure(
                new[] { ItemIds.WyrmfangSnare, ItemIds.DrakeheartRelic },
                false,
                out var configureReason), configureReason);
            Assert.IsTrue(provider.TryGetValidatedSnapshots(
                out var player,
                out var ai,
                out var snapshotReason), snapshotReason);

            CollectionAssert.AreEqual(new[] { ItemIds.WyrmfangSnare }, player.ActiveItems);
            CollectionAssert.AreEqual(new[] { ItemIds.DrakeheartRelic }, player.PassiveItems);
            Assert.IsEmpty(ai.ActiveItems);
            Assert.IsEmpty(ai.PassiveItems);
        }

        [Test]
        public void ConfigureCanMirrorTheValidatedPlayerSnapshotToAi()
        {
            var provider = new DevelopmentItemRunSnapshotProvider();

            Assert.IsTrue(provider.TryConfigure(
                new[] { ItemIds.WinterveilRune, ItemIds.SpellbreakerSeal },
                true,
                out var configureReason), configureReason);
            Assert.IsTrue(provider.TryGetValidatedSnapshots(
                out var player,
                out var ai,
                out var snapshotReason), snapshotReason);

            CollectionAssert.AreEqual(player.ActiveItems, ai.ActiveItems);
            CollectionAssert.AreEqual(player.PassiveItems, ai.PassiveItems);
        }

        [Test]
        public void InvalidConfigurationIsRejectedWithoutReplacingTheLastValidSnapshot()
        {
            var provider = new DevelopmentItemRunSnapshotProvider();
            Assert.IsTrue(provider.TryConfigure(
                new[] { ItemIds.WyrmfangSnare },
                false,
                out var validReason), validReason);

            Assert.IsFalse(provider.TryConfigure(
                new[] { ItemIds.WyrmfangSnare, ItemIds.WinterveilRune, ItemIds.RuneburstMine },
                false,
                out var invalidReason));
            Assert.AreEqual(ItemOperationFailure.ActiveSlotsFull, invalidReason);
            Assert.IsTrue(provider.TryGetValidatedSnapshots(
                out var player,
                out _,
                out var snapshotReason), snapshotReason);
            CollectionAssert.AreEqual(new[] { ItemIds.WyrmfangSnare }, player.ActiveItems);
        }
    }
}
