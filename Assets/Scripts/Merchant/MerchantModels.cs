using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    private static readonly HashSet<string> PendingPlayers = new HashSet<string>();

    public static void MarkPending(string playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId)) PendingPlayers.Add(playerId);
    }

    public static bool TryConsumePending(string playerId)
    {
        return !string.IsNullOrWhiteSpace(playerId) && PendingPlayers.Remove(playerId);
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
    private const string IconRoot = "Merchant/Icons/";

    public UnityEngine.Sprite Load(string iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return null;
        return UnityEngine.Resources.Load<UnityEngine.Sprite>(IconRoot + iconKey);
    }
}
