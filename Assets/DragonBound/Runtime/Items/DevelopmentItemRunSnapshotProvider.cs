using System;
using System.Collections.Generic;

namespace DragonBound.Items
{
    public static class MerchantItemSnapshotFactory
    {
        private static readonly IReadOnlyDictionary<string, string> LegacyToRuntime =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "ITEM_MANABURST_MINE", ItemIds.RuneburstMine },
                { "ITEM_RUNE_TEMPERING", ItemIds.RuneOfTempering },
                { "ITEM_PACT_ENDURANCE", ItemIds.PactOfEndurance },
                { "ITEM_VETERAN_MARK", ItemIds.VeteransMark },
                { "ITEM_QUARTERMASTER_SATCHEL", ItemIds.QuartermastersSatchel },
                { "ITEM_FORGEGIFTERS_GIFT", ItemIds.ForgekeepersGift }
            };

        public static bool TryCreate(
            IEnumerable<string> merchantProductIds,
            out IItemRunSnapshotProvider provider,
            out string reason)
        {
            var runtimeItemIds = new List<string>();
            if (merchantProductIds != null)
            {
                foreach (string merchantProductId in merchantProductIds)
                {
                    if (string.IsNullOrWhiteSpace(merchantProductId)) continue;
                    string runtimeItemId = LegacyToRuntime.TryGetValue(
                        merchantProductId,
                        out string mappedItemId)
                        ? mappedItemId
                        : merchantProductId;
                    if (ItemCatalog.Get(runtimeItemId) == null) continue;
                    if (!runtimeItemIds.Contains(runtimeItemId)) runtimeItemIds.Add(runtimeItemId);
                }
            }

            var developmentProvider = new DevelopmentItemRunSnapshotProvider();
            if (!developmentProvider.TryConfigure(runtimeItemIds, false, out reason))
            {
                provider = null;
                return false;
            }

            provider = developmentProvider;
            reason = ItemOperationFailure.None;
            return true;
        }
    }

    /// <summary>
    /// Mutable development-only input for manual gameplay QA. It builds snapshots through the
    /// same ItemProfile validation path as a validated account profile.
    /// </summary>
    public sealed class DevelopmentItemRunSnapshotProvider : IItemRunSnapshotProvider
    {
        private ItemRunSnapshot playerSnapshot = ItemRunSnapshot.Empty;
        private ItemRunSnapshot aiSnapshot = ItemRunSnapshot.Empty;

        public bool TryConfigure(
            IEnumerable<string> playerItemIds,
            bool mirrorPlayerLoadoutToAi,
            out string reason)
        {
            if (!TryCreateSnapshot(playerItemIds, out var nextPlayerSnapshot, out reason))
            {
                return false;
            }

            playerSnapshot = nextPlayerSnapshot;
            aiSnapshot = mirrorPlayerLoadoutToAi ? nextPlayerSnapshot : ItemRunSnapshot.Empty;
            reason = ItemOperationFailure.None;
            return true;
        }

        public bool TryGetValidatedSnapshots(
            out ItemRunSnapshot player,
            out ItemRunSnapshot ai,
            out string reason)
        {
            player = playerSnapshot;
            ai = aiSnapshot;
            reason = ItemOperationFailure.None;
            return true;
        }

        private static bool TryCreateSnapshot(
            IEnumerable<string> itemIds,
            out ItemRunSnapshot snapshot,
            out string reason)
        {
            snapshot = null;
            var profile = new ItemProfile();
            if (!profile.RefreshDay(new DevelopmentDayKeyProvider(), out reason) ||
                !profile.RefreshAuthoritativeAccountProgress(new DevelopmentProgressProvider(), out reason))
            {
                return false;
            }

            if (itemIds != null)
            {
                foreach (var itemId in itemIds)
                {
                    if (string.IsNullOrWhiteSpace(itemId) || !profile.Inventory.TryGrantOwned(itemId))
                    {
                        reason = ItemOperationFailure.UnknownItem;
                        return false;
                    }

                    if (!profile.Loadout.TryEquip(itemId, profile.Inventory, out reason))
                    {
                        return false;
                    }
                }
            }

            return profile.TryCreateRunSnapshot(out snapshot, out reason);
        }

        private sealed class DevelopmentDayKeyProvider : IItemDayKeyProvider
        {
            public string GetDayKey() => "DEV-QA-DAY";
        }

        private sealed class DevelopmentProgressProvider : IItemAccountProgressProvider
        {
            public bool TryGetNormalCompletedMatchCount(out int completedMatchCount)
            {
                completedMatchCount = ItemProfile.UnlockCompletedMatchCount;
                return true;
            }
        }
    }
}
