using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// PlayerPrefs-backed development implementation. Replace with a Go unary-call
/// implementation of IMerchantGateway without changing Game or Main UI code.
/// </summary>
public sealed class LocalMerchantGateway : IMerchantGateway
{
    private const int RunsPerOffer = 2;
    private const int ProductsPerOffer = 3;
    private const string StateKeyPrefix = "dragonbound.merchant.state.";
    private const string RunKeyPrefix = "dragonbound.merchant.run.";
    private const string PurchaseKeyPrefix = "dragonbound.merchant.purchase.";
    private const string RemoveKeyPrefix = "dragonbound.merchant.remove.";

    private readonly IPlayerGoldGateway goldGateway = new LocalPlayerGoldGateway();

    [Serializable]
    private sealed class LocalMerchantState
    {
        public string DayKey;
        public int CompletedRunCount;
        public MerchantOffer CurrentOffer;
        public List<string> OwnedProductIds = new List<string>();
    }

    public Task<MerchantDayKey> GetDayKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new MerchantDayKey { Value = GetLocalDayKey() });
    }

    public Task<MerchantRunResult> RecordCompletedRunAsync(
        string playerId,
        string runId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("Run ID is required.", nameof(runId));
        }

        string runKey = RunKeyPrefix + HashKey(playerId + ":" + runId);
        LocalMerchantState state = LoadState(playerId);
        if (PlayerPrefs.HasKey(runKey))
        {
            return Task.FromResult(new MerchantRunResult
            {
                Applied = false,
                CompletedRunCount = state.CompletedRunCount,
                Offer = GetAvailableOffer(state)
            });
        }

        // Entering and completing a later run expires the preceding offer.
        if (state.CurrentOffer != null) state.CurrentOffer = null;

        if (GetOwnedCount(state) >= MerchantItemCatalog.All.Count)
        {
            state.CompletedRunCount = 0;
            state.CurrentOffer = null;
        }
        else
        {
            state.CompletedRunCount++;
            if (state.CompletedRunCount >= RunsPerOffer)
            {
                state.CompletedRunCount = 0;
                state.CurrentOffer = CreateOffer(state);
            }
        }

        SaveState(playerId, state);
        PlayerPrefs.SetInt(runKey, 1);
        PlayerPrefs.Save();

        return Task.FromResult(new MerchantRunResult
        {
            Applied = true,
            CompletedRunCount = state.CompletedRunCount,
            Offer = GetAvailableOffer(state)
        });
    }

    public Task<MerchantOffer> GetCurrentOfferAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        return Task.FromResult(GetAvailableOffer(LoadState(playerId)));
    }

    public Task<MerchantInventory> GetInventoryAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);

        return Task.FromResult(CreateInventory(LoadState(playerId)));
    }

    public Task<MerchantRemoveResult> RemoveInventoryItemAsync(
        string playerId,
        string productId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        if (string.IsNullOrWhiteSpace(productId))
        {
            throw new ArgumentException("Product ID is required.", nameof(productId));
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        string removeKey = RemoveKeyPrefix + HashKey(
            playerId + ":" + productId + ":" + idempotencyKey);
        LocalMerchantState state = LoadState(playerId);
        if (PlayerPrefs.HasKey(removeKey))
        {
            return Task.FromResult(new MerchantRemoveResult
            {
                Removed = false,
                Inventory = CreateInventory(state)
            });
        }

        bool removed = state.OwnedProductIds.Remove(productId);
        if (removed) SaveState(playerId, state);
        PlayerPrefs.SetInt(removeKey, removed ? 1 : 0);
        PlayerPrefs.Save();
        return Task.FromResult(new MerchantRemoveResult
        {
            Removed = removed,
            Inventory = CreateInventory(state)
        });
    }

    public async Task<MerchantPurchaseResult> PurchaseAsync(
        string playerId,
        string offerId,
        string productId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePlayerId(playerId);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        LocalMerchantState state = LoadState(playerId);
        string purchaseKey = PurchaseKeyPrefix + HashKey(
            playerId + ":" + offerId + ":" + productId + ":" + idempotencyKey);
        if (PlayerPrefs.HasKey(purchaseKey))
        {
            PlayerGoldState gold = await goldGateway.GetGoldAsync(playerId, cancellationToken);
            return new MerchantPurchaseResult
            {
                Status = MerchantPurchaseStatus.Success,
                GoldBalance = gold.Balance,
                Applied = false
            };
        }

        MerchantOffer offer = state.CurrentOffer;
        if (offer == null || offer.OfferId != offerId)
        {
            return await ResultAsync(MerchantPurchaseStatus.OfferUnavailable, playerId, cancellationToken);
        }
        if (offer.Purchased)
        {
            return await ResultAsync(MerchantPurchaseStatus.AlreadyPurchased, playerId, cancellationToken);
        }

        if (state.OwnedProductIds.Contains(productId))
        {
            return await ResultAsync(MerchantPurchaseStatus.AlreadyOwned, playerId, cancellationToken);
        }

        MerchantProduct product = offer.Products.Find(item => item.ProductId == productId);
        if (product == null || !product.GoldPurchasable)
        {
            return await ResultAsync(MerchantPurchaseStatus.ProductUnavailable, playerId, cancellationToken);
        }

        GoldSpendResult spend = await goldGateway.TrySpendAsync(
            playerId,
            product.GoldPrice,
            "merchant:" + offerId + ":" + productId + ":" + idempotencyKey,
            cancellationToken);
        if (!spend.Success)
        {
            return new MerchantPurchaseResult
            {
                Status = MerchantPurchaseStatus.InsufficientGold,
                GoldBalance = spend.Balance,
                Applied = false
            };
        }

        offer.Purchased = true;
        offer.PurchasedProductId = productId;
        if (!state.OwnedProductIds.Contains(productId)) state.OwnedProductIds.Add(productId);
        SaveState(playerId, state);
        PlayerPrefs.SetInt(purchaseKey, 1);
        PlayerPrefs.Save();

        return new MerchantPurchaseResult
        {
            Status = MerchantPurchaseStatus.Success,
            GoldBalance = spend.Balance,
            Applied = true
        };
    }

    private static MerchantOffer CreateOffer(LocalMerchantState state)
    {
        List<MerchantProduct> candidates = MerchantItemCatalog.GetGoldCandidates();
        var ownedIds = new HashSet<string>(state.OwnedProductIds);
        candidates.RemoveAll(product => ownedIds.Contains(product.ProductId));
        if (candidates.Count == 0) return null;

        var selected = new List<MerchantProduct>();
        int count = Math.Min(ProductsPerOffer, candidates.Count);
        for (int index = 0; index < count; index++)
        {
            int selectedIndex = UnityEngine.Random.Range(0, candidates.Count);
            selected.Add(candidates[selectedIndex]);
            candidates.RemoveAt(selectedIndex);
        }

        return new MerchantOffer
        {
            OfferId = Guid.NewGuid().ToString("N"),
            Products = selected,
            Purchased = false,
            PurchasedProductId = string.Empty
        };
    }

    private static MerchantOffer GetAvailableOffer(LocalMerchantState state)
    {
        return state.CurrentOffer != null && !state.CurrentOffer.Purchased
            ? state.CurrentOffer
            : null;
    }

    private static MerchantInventory CreateInventory(LocalMerchantState state)
    {
        var inventory = new MerchantInventory();
        foreach (string productId in state.OwnedProductIds)
        {
            MerchantProduct product = MerchantItemCatalog.Find(productId);
            if (product != null) inventory.Products.Add(product);
        }
        return inventory;
    }

    private static async Task<MerchantPurchaseResult> ResultAsync(
        MerchantPurchaseStatus status,
        string playerId,
        CancellationToken cancellationToken)
    {
        PlayerGoldState gold = await new LocalPlayerGoldGateway().GetGoldAsync(
            playerId,
            cancellationToken);
        return new MerchantPurchaseResult
        {
            Status = status,
            GoldBalance = gold.Balance,
            Applied = false
        };
    }

    private static LocalMerchantState LoadState(string playerId)
    {
        string currentDayKey = GetLocalDayKey();
        string json = PlayerPrefs.GetString(GetStateKey(playerId), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateFreshDailyState(playerId, currentDayKey);
        }

        try
        {
            LocalMerchantState state = JsonUtility.FromJson<LocalMerchantState>(json);
            if (state == null || state.DayKey != currentDayKey)
            {
                return CreateFreshDailyState(playerId, currentDayKey);
            }
            if (state.OwnedProductIds == null) state.OwnedProductIds = new List<string>();
            state.OwnedProductIds = new List<string>(new HashSet<string>(state.OwnedProductIds));
            return state;
        }
        catch (Exception)
        {
            return CreateFreshDailyState(playerId, currentDayKey);
        }
    }

    private static void SaveState(string playerId, LocalMerchantState state)
    {
        PlayerPrefs.SetString(GetStateKey(playerId), JsonUtility.ToJson(state));
    }

    private static string GetStateKey(string playerId)
    {
        return StateKeyPrefix + HashKey(playerId);
    }

    private static int GetOwnedCount(LocalMerchantState state)
    {
        var knownIds = new HashSet<string>();
        foreach (string productId in state.OwnedProductIds)
        {
            if (MerchantItemCatalog.Find(productId) != null) knownIds.Add(productId);
        }
        return knownIds.Count;
    }

    private static LocalMerchantState CreateFreshDailyState(string playerId, string dayKey)
    {
        var state = new LocalMerchantState { DayKey = dayKey };
        SaveState(playerId, state);
        PlayerPrefs.Save();
        return state;
    }

    private static string GetLocalDayKey()
    {
        // Development fallback. The Go implementation must return the authoritative
        // server DayKey through GetDayKeyAsync and perform the same reset atomically.
        return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void ValidatePlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        }
    }

    private static string HashKey(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(digest).Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }
    }
}
