using System;
using System.Collections.Generic;
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

    private readonly IPlayerGoldGateway goldGateway = new LocalPlayerGoldGateway();

    [Serializable]
    private sealed class LocalMerchantState
    {
        public int CompletedRunCount;
        public MerchantOffer CurrentOffer;
        public List<string> OwnedProductIds = new List<string>();
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

        state.CompletedRunCount++;
        if (state.CompletedRunCount >= RunsPerOffer)
        {
            state.CompletedRunCount = 0;
            state.CurrentOffer = CreateOffer();
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

    private static MerchantOffer CreateOffer()
    {
        List<MerchantProduct> candidates = MerchantItemCatalog.GetGoldCandidates();
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
        string json = PlayerPrefs.GetString(GetStateKey(playerId), string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new LocalMerchantState();

        try
        {
            LocalMerchantState state = JsonUtility.FromJson<LocalMerchantState>(json);
            if (state == null) return new LocalMerchantState();
            if (state.OwnedProductIds == null) state.OwnedProductIds = new List<string>();
            return state;
        }
        catch (Exception)
        {
            return new LocalMerchantState();
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
