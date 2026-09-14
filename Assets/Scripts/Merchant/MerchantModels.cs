using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public enum MerchantPaymentType
{
    Gold,
    RewardedAd
}

[Serializable]
public sealed class MerchantProduct
{
    public string ProductId;
    public string ChineseName;
    public string EnglishName;
    public string Rarity;
    public string ItemType;
    public int GoldPrice;
    public string Introduction;
    public string IconKey;
    public bool GoldPurchasable;
    public MerchantPaymentType PaymentType;
    public string AdPlacementId;
}

[Serializable]
public sealed class MerchantOffer
{
    public string OfferId;
    public List<MerchantProduct> Products = new List<MerchantProduct>();
    public bool Purchased;
    public string PurchasedProductId;
}

public sealed class MerchantRunResult
{
    public bool Applied;
    public int CompletedRunCount;
    public MerchantOffer Offer;
}

public enum MerchantPurchaseStatus
{
    Success,
    InsufficientGold,
    OfferUnavailable,
    ProductUnavailable,
    AlreadyOwned,
    AlreadyPurchased,
    AdVerificationFailed
}

public sealed class MerchantPurchaseResult
{
    public MerchantPurchaseStatus Status;
    public long GoldBalance;
    public bool Applied;
}

public sealed class MerchantInventory
{
    public List<MerchantProduct> Products = new List<MerchantProduct>();
    public List<MerchantProduct> LoadoutProducts = new List<MerchantProduct>();
}

[Serializable]
public sealed class MerchantLotteryOffer
{
    public string LotteryOfferId;
    public string MerchantOfferId;
    public List<MerchantProduct> Products = new List<MerchantProduct>();
    public bool Drawn;
    public string WinningProductId;
}

public enum MerchantLotteryStatus
{
    Success,
    OfferUnavailable,
    AlreadyDrawn,
    AlreadyPurchased,
    AdVerificationFailed,
    NoEligibleProducts
}

public sealed class MerchantLotteryResult
{
    public MerchantLotteryStatus Status;
    public MerchantProduct WinningProduct;
    public MerchantInventory Inventory;
    public bool Applied;
}

public sealed class MerchantRemoveResult
{
    public bool Removed;
    public MerchantInventory Inventory;
}

public sealed class MerchantDayKey
{
    public string Value;
}

public static class MerchantPresentationStore
{
    private const string PendingEventKeyPrefix = "dragonbound.merchant.pending-event.v1.";
    private const string PresentedEventKeyPrefix = "dragonbound.merchant.presented-event.v1.";
    private static readonly HashSet<string> PendingPlayers = new HashSet<string>();

    public static void MarkPending(string playerId)
    {
        MarkPending(playerId, string.Empty);
    }

    public static void MarkPending(string playerId, string eventId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return;
        PendingPlayers.Add(playerId);
        PlayerPrefs.SetString(PendingKey(playerId), eventId ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static bool HasPending(string playerId)
    {
        return !string.IsNullOrWhiteSpace(playerId) &&
               (PendingPlayers.Contains(playerId) || PlayerPrefs.HasKey(PendingKey(playerId)));
    }

    public static bool ShouldPresent(string playerId, string eventId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(eventId))
            return false;
        if (HasPending(playerId)) return true;
        return !string.Equals(
            PlayerPrefs.GetString(PresentedKey(playerId), string.Empty),
            eventId,
            StringComparison.Ordinal);
    }

    public static void MarkPresented(string playerId, string eventId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(eventId)) return;
        PendingPlayers.Remove(playerId);
        PlayerPrefs.DeleteKey(PendingKey(playerId));
        PlayerPrefs.SetString(PresentedKey(playerId), eventId);
        PlayerPrefs.Save();
    }

    public static bool TryConsumePending(string playerId)
    {
        if (!HasPending(playerId)) return false;
        PendingPlayers.Remove(playerId);
        PlayerPrefs.DeleteKey(PendingKey(playerId));
        PlayerPrefs.Save();
        return true;
    }

    private static string PendingKey(string playerId)
    {
        return PendingEventKeyPrefix + playerId;
    }

    private static string PresentedKey(string playerId)
    {
        return PresentedEventKeyPrefix + playerId;
    }
}

/// <summary>
/// Merchant service boundary. The production implementation maps each method to
/// one Go unary call; the Main UI never reads PlayerPrefs or rolls products itself.
/// </summary>
public interface IMerchantGateway
{
    Task<MerchantDayKey> GetDayKeyAsync(
        CancellationToken cancellationToken);

    Task<MerchantRunResult> RecordCompletedRunAsync(
        string playerId,
        string runId,
        CancellationToken cancellationToken);

    Task<MerchantOffer> GetCurrentOfferAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task<MerchantInventory> GetInventoryAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task<MerchantInventory> SetItemLoadoutAsync(
        string playerId,
        IReadOnlyList<string> activeItemIds,
        IReadOnlyList<string> passiveItemIds,
        CancellationToken cancellationToken);

    Task<MerchantLotteryOffer> GetLotteryOfferAsync(
        string playerId,
        string merchantOfferId,
        CancellationToken cancellationToken);

    Task<MerchantLotteryResult> DrawLotteryAsync(
        string playerId,
        string lotteryOfferId,
        string placementId,
        string adVerificationId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<MerchantPurchaseResult> PurchaseAsync(
        string playerId,
        string offerId,
        string productId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<MerchantPurchaseResult> ClaimRewardedAdProductAsync(
        string playerId,
        string offerId,
        string productId,
        string placementId,
        string adVerificationId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<MerchantRemoveResult> RemoveInventoryItemAsync(
        string playerId,
        string productId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface IMerchantItemIconProvider
{
    UnityEngine.Sprite Load(string iconKey);
}

public sealed class ResourcesMerchantItemIconProvider : IMerchantItemIconProvider
{
    private static readonly HashSet<string> MissingIconKeys = new HashSet<string>();

    public UnityEngine.Sprite Load(string iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return null;

        UnityEngine.Sprite sprite = UnityEngine.Resources.Load<UnityEngine.Sprite>(iconKey);
        if (sprite == null && MissingIconKeys.Add(iconKey))
        {
            UnityEngine.Debug.LogWarning(
                $"Merchant item icon is missing at Resources/{iconKey}. " +
                "The authored placeholder will remain visible.");
        }

        return sprite;
    }
}
