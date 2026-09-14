using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Runes;

public sealed class GoRuneProfileGateway : IRuneProfileGateway
{
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;
    private readonly GoBootstrapClient bootstrap;
    private readonly Dictionary<string, long> firstInstanceByRune =
        new Dictionary<string, long>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> loadoutByHero =
        new Dictionary<string, string>(StringComparer.Ordinal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private readonly LocalRuneRewardService developmentRunes =
        new LocalRuneRewardService("dragonbound.runes.go-dev-overlay-v1.");
    private bool developmentOverlayActive;
#endif

    internal GoRuneProfileGateway(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        GoBootstrapClient bootstrap)
    {
        this.transport = transport;
        this.contexts = contexts;
        this.bootstrap = bootstrap;
    }

    public async Task<RuneProfile> SettleRunAsync(
        string playerId,
        string runId,
        IReadOnlyList<RuneReward> rewards,
        CancellationToken cancellationToken)
    {
        // Rune rewards are already committed by /runs/{id}/finish.
        // When AccountDay is explicitly overridden for local testing, the server still sees
        // the account's real day. Keep those client-visible test drops in a local overlay so
        // they can be verified in Main without mutating production account data.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GoBootstrapResponse developmentBootstrap = await bootstrap.GetAsync(cancellationToken);
        developmentOverlayActive = ShouldUseDevelopmentRuneOverlay(
            ResolveAccountDay(developmentBootstrap));
        if (developmentOverlayActive)
        {
            await developmentRunes.SettleRunAsync(
                playerId,
                runId,
                rewards,
                cancellationToken);
        }
#endif
        return await GetProfileAsync(playerId, cancellationToken);
    }

    public async Task<RuneProfile> GetProfileAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        GoRuneListResponse response = await transport.SendAsync<object, GoRuneListResponse>(
            "GET", "/v1/runes", null, contexts.Create(), cancellationToken);
        firstInstanceByRune.Clear();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (GoPlayerRuneEntry entry in response.player_runes ?? new List<GoPlayerRuneEntry>())
        {
            if (string.IsNullOrWhiteSpace(entry.rune_id)) continue;
            counts.TryGetValue(entry.rune_id, out int count);
            counts[entry.rune_id] = count + 1;
            if (!firstInstanceByRune.ContainsKey(entry.rune_id))
                firstInstanceByRune.Add(entry.rune_id, entry.rune_instance_id);
        }

        GoBootstrapResponse bootstrapState = await bootstrap.GetAsync(cancellationToken);
        int serverAccountDay = ResolveAccountDay(bootstrapState);
        int accountDay = serverAccountDay;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Keep production progression server-owned, but allow the existing editor
        // test menu to override the signed-in account day during local testing.
        accountDay = LocalRuneProgressionSettings.ResolveAccountDay(accountDay);
#endif
        var profile = new RuneProfile
        {
            AccountDay = accountDay,
            FragmentBalance = Math.Max(0, response.fragment_balance)
        };
        foreach (GoRuneCatalogEntry catalog in response.runes ?? new List<GoRuneCatalogEntry>())
        {
            counts.TryGetValue(catalog.rune_id, out int owned);
            profile.Inventory.Add(new RuneInventoryEntry
            {
                RuneId = catalog.rune_id,
                OwnedCount = owned,
                FragmentCount = catalog.quality == "epic" || catalog.quality == "legendary"
                    ? (int)Math.Min(int.MaxValue, Math.Max(0, response.fragment_balance))
                    : 0
            });
        }
        foreach (KeyValuePair<string, string> pair in loadoutByHero)
            profile.Loadouts.Add(new HeroRuneLoadoutEntry { HeroId = pair.Key, RuneId = pair.Value });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        developmentOverlayActive = ShouldUseDevelopmentRuneOverlay(serverAccountDay);
        if (developmentOverlayActive)
        {
            RuneProfile developmentProfile = await developmentRunes.GetProfileAsync(
                playerId,
                cancellationToken);
            MergeDevelopmentProfile(profile, developmentProfile);
        }
#endif
        return profile;
    }

    public async Task<RuneProfileMutationResult> CraftRuneAsync(
        string playerId,
        string runeId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runeId))
            throw new ArgumentException("Rune ID is required.", nameof(runeId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        try
        {
            GoCraftRuneResponse response = await transport.SendAsync<object, GoCraftRuneResponse>(
                "POST",
                "/v1/runes/" + Uri.EscapeDataString(runeId) + "/craft",
                null,
                contexts.Create(idempotencyKey),
                cancellationToken);
            return new RuneProfileMutationResult
            {
                Succeeded = response != null && response.rune_instance_id > 0,
                Profile = await GetProfileAsync(playerId, cancellationToken)
            };
        }
        catch (ClientServiceException exception) when (exception.HttpStatus == 422)
        {
            return new RuneProfileMutationResult
            {
                Succeeded = false,
                Profile = await GetProfileAsync(playerId, cancellationToken)
            };
        }
    }

    public async Task<RuneProfileMutationResult> EquipRuneAsync(
        string playerId,
        string heroId,
        string runeId,
        CancellationToken cancellationToken)
    {
        if (!firstInstanceByRune.TryGetValue(runeId ?? string.Empty, out long instanceId))
            await GetProfileAsync(playerId, cancellationToken);
        if (!firstInstanceByRune.TryGetValue(runeId ?? string.Empty, out instanceId))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (developmentOverlayActive)
            {
                RuneProfileMutationResult developmentResult =
                    await developmentRunes.EquipRuneAsync(
                        playerId,
                        heroId,
                        runeId,
                        cancellationToken);
                if (developmentResult.Succeeded)
                {
                    developmentResult.Profile = await GetProfileAsync(playerId, cancellationToken);
                    return developmentResult;
                }
            }
#endif
            return new RuneProfileMutationResult { Succeeded = false, Profile = await GetProfileAsync(playerId, cancellationToken) };
        }

        await transport.SendAsync<GoEquipRuneRequest, GoEquipRuneResponse>(
            "PUT",
            "/v1/heroes/" + Uri.EscapeDataString(heroId) + "/rune",
            new GoEquipRuneRequest { rune_instance_id = instanceId },
            contexts.Create("rune-equip:" + heroId + ":" + instanceId),
            cancellationToken);
        loadoutByHero[heroId] = runeId;
        return new RuneProfileMutationResult
        {
            Succeeded = true,
            Profile = await GetProfileAsync(playerId, cancellationToken)
        };
    }

    public async Task<RuneProfileMutationResult> UnequipRuneAsync(
        string playerId,
        string heroId,
        CancellationToken cancellationToken)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (developmentOverlayActive)
        {
            RuneProfileMutationResult developmentResult =
                await developmentRunes.UnequipRuneAsync(
                    playerId,
                    heroId,
                    cancellationToken);
            if (developmentResult.Succeeded)
            {
                developmentResult.Profile = await GetProfileAsync(playerId, cancellationToken);
                return developmentResult;
            }
        }
#endif
        await transport.SendAsync<object, GoEquipRuneResponse>(
            "DELETE",
            "/v1/heroes/" + Uri.EscapeDataString(heroId) + "/rune",
            null,
            contexts.Create("rune-unequip:" + heroId),
            cancellationToken);
        loadoutByHero.Remove(heroId);
        return new RuneProfileMutationResult
        {
            Succeeded = true,
            Profile = await GetProfileAsync(playerId, cancellationToken)
        };
    }

    private static int ResolveAccountDay(GoBootstrapResponse value)
    {
        if (!DateTimeOffset.TryParse(value?.player?.created_at, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset createdAt) ||
            !DateTimeOffset.TryParse(value?.server_time, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset serverTime))
            return 1;
        return Math.Max(1, (serverTime.UtcDateTime.Date - createdAt.UtcDateTime.Date).Days + 1);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static bool ShouldUseDevelopmentRuneOverlay(int serverAccountDay)
    {
        return LocalRuneProgressionSettings.IsDevelopmentOverrideActive &&
               serverAccountDay < RuneFeatureGate.UnlockAccountDay &&
               LocalRuneProgressionSettings.ResolveAccountDay(serverAccountDay) >=
               RuneFeatureGate.UnlockAccountDay;
    }

    private static void MergeDevelopmentProfile(RuneProfile target, RuneProfile source)
    {
        if (target == null || source == null) return;

        for (int sourceIndex = 0; sourceIndex < source.Inventory.Count; sourceIndex++)
        {
            RuneInventoryEntry sourceEntry = source.Inventory[sourceIndex];
            if (sourceEntry == null || string.IsNullOrWhiteSpace(sourceEntry.RuneId)) continue;

            RuneInventoryEntry targetEntry = null;
            for (int targetIndex = 0; targetIndex < target.Inventory.Count; targetIndex++)
            {
                if (target.Inventory[targetIndex].RuneId != sourceEntry.RuneId) continue;
                targetEntry = target.Inventory[targetIndex];
                break;
            }

            if (targetEntry == null)
            {
                targetEntry = new RuneInventoryEntry { RuneId = sourceEntry.RuneId };
                target.Inventory.Add(targetEntry);
            }

            targetEntry.OwnedCount = AddWithoutOverflow(
                targetEntry.OwnedCount,
                sourceEntry.OwnedCount);
            targetEntry.FragmentCount = AddWithoutOverflow(
                targetEntry.FragmentCount,
                sourceEntry.FragmentCount);
        }

        target.LastRunRewards.Clear();
        for (int index = 0; index < source.LastRunRewards.Count; index++)
            target.LastRunRewards.Add(source.LastRunRewards[index]);

        for (int sourceIndex = 0; sourceIndex < source.Loadouts.Count; sourceIndex++)
        {
            HeroRuneLoadoutEntry sourceLoadout = source.Loadouts[sourceIndex];
            if (sourceLoadout == null || string.IsNullOrWhiteSpace(sourceLoadout.HeroId)) continue;

            bool alreadyAssigned = false;
            for (int targetIndex = 0; targetIndex < target.Loadouts.Count; targetIndex++)
            {
                if (target.Loadouts[targetIndex].HeroId != sourceLoadout.HeroId) continue;
                alreadyAssigned = true;
                break;
            }
            if (!alreadyAssigned)
            {
                target.Loadouts.Add(new HeroRuneLoadoutEntry
                {
                    HeroId = sourceLoadout.HeroId,
                    RuneId = sourceLoadout.RuneId
                });
            }
        }
    }

    private static int AddWithoutOverflow(int current, int amount)
    {
        return (int)Math.Min(int.MaxValue, Math.Max(0L, current) + Math.Max(0L, amount));
    }
#endif
}

public sealed class GoMerchantGateway : IMerchantGateway
{
    private const int ProductsPerLottery = 8;
    private readonly IUnaryTransport transport;
    private readonly GoApiContextFactory contexts;
    private readonly IPlayerGoldGateway gold;
    private readonly Dictionary<string, MerchantProduct> catalog =
        new Dictionary<string, MerchantProduct>(StringComparer.Ordinal);
    private readonly HashSet<string> ownedProductIds =
        new HashSet<string>(StringComparer.Ordinal);
    private MerchantOffer currentOffer;
    private MerchantLotteryOffer currentLotteryOffer;
    private bool currentLotteryAvailable;

    internal GoMerchantGateway(
        IUnaryTransport transport,
        GoApiContextFactory contexts,
        IPlayerGoldGateway gold)
    {
        this.transport = transport;
        this.contexts = contexts;
        this.gold = gold;
    }

    public async Task<MerchantDayKey> GetDayKeyAsync(CancellationToken cancellationToken)
    {
        GoItemListResponse items = await GetItemsAsync(cancellationToken);
        return new MerchantDayKey { Value = items.day_key };
    }

    public async Task<MerchantRunResult> RecordCompletedRunAsync(
        string playerId,
        string runId,
        CancellationToken cancellationToken)
    {
        return new MerchantRunResult
        {
            Applied = false,
            Offer = await GetCurrentOfferAsync(playerId, cancellationToken)
        };
    }

    public async Task<MerchantOffer> GetCurrentOfferAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        try
        {
            GoMerchantResponse response = await transport.SendAsync<object, GoMerchantResponse>(
                "GET", "/v1/shop/merchant", null, contexts.Create(), cancellationToken);
            var offer = new MerchantOffer { OfferId = response.event_id };
            foreach (GoMerchantOffer wire in response.offers ?? new List<GoMerchantOffer>())
                offer.Products.Add(ToProduct(wire));
            if (currentOffer == null ||
                !string.Equals(currentOffer.OfferId, offer.OfferId, StringComparison.Ordinal))
                currentLotteryOffer = null;
            currentOffer = offer;
            currentLotteryAvailable = response.lottery_available;
            return offer;
        }
        catch (ClientServiceException exception) when (
            exception.HttpStatus == 404 || exception.HttpStatus == 422)
        {
            return null;
        }
    }

    public async Task<MerchantInventory> GetInventoryAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        GoItemListResponse response = await GetItemsAsync(cancellationToken);
        var inventory = new MerchantInventory();
        var owned = new HashSet<string>(response.owned_item_ids ?? new List<string>(), StringComparer.Ordinal);
        ownedProductIds.Clear();
        foreach (string itemId in owned) ownedProductIds.Add(itemId);
        foreach (GoItemCatalogEntry item in response.items ?? new List<GoItemCatalogEntry>())
        {
            MerchantProduct product = ToProduct(item);
            catalog[item.item_id] = product;
            if (owned.Contains(item.item_id)) inventory.Products.Add(product);
        }
        foreach (GoItemLoadoutEntry item in response.loadout ?? new List<GoItemLoadoutEntry>())
        {
            if (item == null || string.IsNullOrWhiteSpace(item.item_id)) continue;
            if (catalog.TryGetValue(item.item_id, out MerchantProduct product) &&
                !inventory.LoadoutProducts.Contains(product))
                inventory.LoadoutProducts.Add(product);
        }
        return inventory;
    }

    public async Task<MerchantInventory> SetItemLoadoutAsync(
        string playerId,
        IReadOnlyList<string> activeItemIds,
        IReadOnlyList<string> passiveItemIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        if ((activeItemIds?.Count ?? 0) > 2 || (passiveItemIds?.Count ?? 0) > 6)
            throw new ArgumentException("Item loadout exceeds the server slot limits.");
        var request = new GoItemLoadoutRequest();
        if (activeItemIds != null) request.active_item_ids.AddRange(activeItemIds);
        if (passiveItemIds != null) request.passive_item_ids.AddRange(passiveItemIds);
        await transport.SendAsync<GoItemLoadoutRequest, GoItemLoadoutResponse>(
            "PUT", "/v1/items/loadout", request, contexts.Create(), cancellationToken);
        return await GetInventoryAsync(playerId, cancellationToken);
    }

    public Task<MerchantLotteryOffer> GetLotteryOfferAsync(
        string playerId,
        string merchantOfferId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentLotteryAvailable || currentOffer == null ||
            !string.Equals(currentOffer.OfferId, merchantOfferId, StringComparison.Ordinal) ||
            currentOffer.Products == null || currentOffer.Products.Count == 0)
        {
            return Task.FromResult<MerchantLotteryOffer>(null);
        }

        if (currentLotteryOffer != null &&
            string.Equals(currentLotteryOffer.MerchantOfferId, merchantOfferId, StringComparison.Ordinal))
            return Task.FromResult(currentLotteryOffer);

        var excludedIds = new HashSet<string>(ownedProductIds, StringComparer.Ordinal);
        foreach (MerchantProduct product in currentOffer.Products)
        {
            if (product != null) excludedIds.Add(product.ProductId);
        }
        var candidates = new List<MerchantProduct>();
        foreach (MerchantProduct product in MerchantItemCatalog.All)
        {
            if (product != null && !excludedIds.Contains(product.ProductId))
                candidates.Add(MerchantItemCatalog.Find(product.ProductId));
        }
        candidates.Sort((left, right) => StableHash(merchantOfferId + "|" + left.ProductId)
            .CompareTo(StableHash(merchantOfferId + "|" + right.ProductId)));
        if (candidates.Count > ProductsPerLottery)
            candidates.RemoveRange(ProductsPerLottery, candidates.Count - ProductsPerLottery);
        if (candidates.Count == 0) return Task.FromResult<MerchantLotteryOffer>(null);

        currentLotteryOffer = new MerchantLotteryOffer
        {
            LotteryOfferId = merchantOfferId + ":lottery",
            MerchantOfferId = merchantOfferId,
            Products = candidates,
            Drawn = false,
            WinningProductId = string.Empty
        };
        return Task.FromResult(currentLotteryOffer);
    }

    public async Task<MerchantLotteryResult> DrawLotteryAsync(
        string playerId,
        string lotteryOfferId,
        string placementId,
        string adVerificationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (currentOffer == null || currentLotteryOffer == null || !currentLotteryAvailable ||
            !string.Equals(currentLotteryOffer.LotteryOfferId, lotteryOfferId, StringComparison.Ordinal) ||
            currentLotteryOffer.Products == null || currentLotteryOffer.Products.Count == 0)
        {
            return new MerchantLotteryResult
            {
                Status = MerchantLotteryStatus.OfferUnavailable,
                Applied = false
            };
        }
        if (string.IsNullOrWhiteSpace(adVerificationId))
        {
            return new MerchantLotteryResult
            {
                Status = MerchantLotteryStatus.AdVerificationFailed,
                Applied = false
            };
        }

        int winnerIndex = SelectStableIndex(
            lotteryOfferId,
            currentLotteryOffer.Products.Count);
        MerchantProduct winner = currentLotteryOffer.Products[winnerIndex];
        await GoPlayerEnergyGateway.SendClientAdSuccessAsync(
            transport,
            contexts,
            playerId,
            string.IsNullOrWhiteSpace(placementId) ? "lottery" : placementId,
            adVerificationId,
            string.Empty,
            currentOffer.OfferId + "|" + winner.ProductId,
            cancellationToken);

        MerchantPurchaseResult purchase = await PurchaseAsync(
            playerId,
            currentOffer.OfferId,
            winner.ProductId,
            idempotencyKey,
            cancellationToken);
        MerchantLotteryStatus status;
        switch (purchase.Status)
        {
            case MerchantPurchaseStatus.Success:
                status = MerchantLotteryStatus.Success;
                break;
            case MerchantPurchaseStatus.AlreadyPurchased:
            case MerchantPurchaseStatus.AlreadyOwned:
                status = MerchantLotteryStatus.AlreadyPurchased;
                break;
            case MerchantPurchaseStatus.AdVerificationFailed:
                status = MerchantLotteryStatus.AdVerificationFailed;
                break;
            default:
                status = MerchantLotteryStatus.OfferUnavailable;
                break;
        }

        return new MerchantLotteryResult
        {
            Status = status,
            WinningProduct = status == MerchantLotteryStatus.Success ? winner : null,
            Inventory = status == MerchantLotteryStatus.Success
                ? await GetInventoryAsync(playerId, cancellationToken)
                : null,
            Applied = purchase.Applied
        };
    }

    public async Task<MerchantPurchaseResult> PurchaseAsync(
        string playerId,
        string offerId,
        string productId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await transport.SendAsync<GoMerchantClaimRequest, GoMerchantClaimResponse>(
                "POST",
                "/v1/shop/merchant/" + Uri.EscapeDataString(offerId) + "/claim",
                new GoMerchantClaimRequest { item_id = productId },
                contexts.Create(idempotencyKey),
                cancellationToken);
            PlayerGoldState balance = await gold.GetGoldAsync(playerId, cancellationToken);
            return new MerchantPurchaseResult
            {
                Status = MerchantPurchaseStatus.Success,
                GoldBalance = balance.Balance,
                Applied = true
            };
        }
        catch (ClientServiceException exception)
        {
            return new MerchantPurchaseResult
            {
                Status = MapPurchaseStatus(exception),
                GoldBalance = 0,
                Applied = false
            };
        }
    }

    public async Task<MerchantPurchaseResult> ClaimRewardedAdProductAsync(
        string playerId,
        string offerId,
        string productId,
        string placementId,
        string adVerificationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adVerificationId))
            throw new ArgumentException("Ad event ID is required.", nameof(adVerificationId));

        await GoPlayerEnergyGateway.SendClientAdSuccessAsync(
            transport,
            contexts,
            playerId,
            string.IsNullOrWhiteSpace(placementId) ? "merchant_item" : placementId,
            adVerificationId,
            string.Empty,
            offerId + "|" + productId,
            cancellationToken);

        // The ad claim establishes the temporary trusted entitlement. The normal
        // merchant claim remains authoritative for item ownership and offer state.
        return await PurchaseAsync(
            playerId,
            offerId,
            productId,
            string.IsNullOrWhiteSpace(idempotencyKey)
                ? "merchant.ad:" + adVerificationId
                : idempotencyKey,
            cancellationToken);
    }

    public async Task<MerchantRemoveResult> RemoveInventoryItemAsync(
        string playerId,
        string productId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return new MerchantRemoveResult
        {
            Removed = false,
            Inventory = await GetInventoryAsync(playerId, cancellationToken)
        };
    }

    private async Task<GoItemListResponse> GetItemsAsync(CancellationToken cancellationToken)
    {
        return await transport.SendAsync<object, GoItemListResponse>(
            "GET", "/v1/items", null, contexts.Create(), cancellationToken);
    }

    private MerchantProduct ToProduct(GoMerchantOffer value)
    {
        string itemId = value.item_id ?? string.Empty;
        if (!catalog.TryGetValue(itemId, out MerchantProduct known))
            known = MerchantItemCatalog.Find(itemId);
        if (known != null)
        {
            known.GoldPrice = value.price_gold;
            known.PaymentType = value.delivery == "ad"
                ? MerchantPaymentType.RewardedAd
                : MerchantPaymentType.Gold;
            known.AdPlacementId = value.delivery == "ad" ? "merchant_item" : string.Empty;
            return known;
        }
        return new MerchantProduct
        {
            ProductId = value.item_id,
            ChineseName = value.item_id,
            EnglishName = value.item_id,
            Rarity = NormalizeRarity(value.rarity),
            GoldPrice = value.price_gold,
            GoldPurchasable = value.delivery == "gold",
            PaymentType = value.delivery == "ad" ? MerchantPaymentType.RewardedAd : MerchantPaymentType.Gold,
            AdPlacementId = value.delivery == "ad" ? "merchant_item" : string.Empty
        };
    }

    private static MerchantProduct ToProduct(GoItemCatalogEntry value)
    {
        MerchantProduct product = MerchantItemCatalog.Find(value.item_id) ?? new MerchantProduct
        {
            ProductId = value.item_id,
            ChineseName = value.item_id,
            EnglishName = value.item_id
        };
        product.Rarity = NormalizeRarity(value.rarity);
        product.ItemType = value.slot_type;
        product.GoldPrice = value.price_gold;
        product.GoldPurchasable = true;
        product.PaymentType = MerchantPaymentType.Gold;
        if (string.IsNullOrWhiteSpace(product.IconKey))
            product.IconKey = MerchantItemCatalog.GetIconKey(value.item_id);
        return product;
    }

    private static int SelectStableIndex(string value, int count)
    {
        return (int)(StableHash(value) % (uint)count);
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash;
        }
    }

    private static string NormalizeRarity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
    }

    private static MerchantPurchaseStatus MapPurchaseStatus(ClientServiceException exception)
    {
        switch (exception.Code)
        {
            case "INSUFFICIENT_GOLD": return MerchantPurchaseStatus.InsufficientGold;
            case "ITEM_ALREADY_OWNED": return MerchantPurchaseStatus.AlreadyOwned;
            case "MERCHANT_ALREADY_CLAIMED": return MerchantPurchaseStatus.AlreadyPurchased;
            case "NOT_FOUND": return MerchantPurchaseStatus.OfferUnavailable;
            default: return MerchantPurchaseStatus.ProductUnavailable;
        }
    }
}

public sealed class UnavailableRewardedAdService : IRewardedAdService
{
    public Task<RewardedAdResult> ShowAsync(string placementId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RewardedAdResult.Failed);
    }

    public Task<RewardedAdResult> ShowAsync(
        RewardedAdPlaybackRequest request,
        CancellationToken cancellationToken)
    {
        return ShowAsync(request?.PlacementId, cancellationToken);
    }
}

public sealed class UnavailableShareService : IShareService
{
    public Task<ShareResult> ShareAsync(ShareRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ShareResult.Failed);
    }
}
