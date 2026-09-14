using System;
using System.Collections.Generic;
using System.Threading;
using DragonBound.Bootstrap;
using DragonBound.Items;
using UnityEngine;

/// <summary>
/// Loads the player's Main-scene Merchant inventory into the authored gameplay
/// loadout slots. The Merchant inventory is the temporary loadout source while
/// owned items and equipped items are the same concept.
/// </summary>
[DisallowMultipleComponent]
public sealed class GreyboxMerchantLoadoutController : MonoBehaviour
{
    private const int ActiveSlotCount = 2;
    private const int PassiveSlotCount = 6;
    private readonly List<MerchantProduct> activeItems = new List<MerchantProduct>();
    private readonly List<MerchantProduct> passiveItems = new List<MerchantProduct>();

    private CancellationTokenSource lifetimeCancellation;
    private DragonBoundBootstrap bootstrap;

    public IReadOnlyList<MerchantProduct> ActiveItems => activeItems;
    public IReadOnlyList<MerchantProduct> PassiveItems => passiveItems;
    public event Action LoadoutLoaded;

    private void Awake()
    {
        lifetimeCancellation = new CancellationTokenSource();
        bootstrap = FindObjectOfType<DragonBoundBootstrap>();
    }

    private async void Start()
    {
        try
        {
            IClientServices services = ClientCompositionRoot.Current;
            AuthSession session = services.AuthSession.Current;
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
            {
                Debug.LogWarning(
                    "Greybox loadout was not loaded because no authenticated PlayerId is available.",
                    this);
                InitializeBootstrap(
                    new EmptyItemRunSnapshotProvider(),
                    Array.Empty<ExternalRuneLoadoutAssignment>(),
                    1);
                return;
            }

            MerchantDayKey dayKey = await services.Merchant.GetDayKeyAsync(
                lifetimeCancellation.Token);
            MerchantInventory inventory = await services.Merchant.GetInventoryAsync(
                session.PlayerId,
                lifetimeCancellation.Token);
            global::RuneProfile runeProfile = await services.Runes.GetProfileAsync(
                session.PlayerId,
                lifetimeCancellation.Token);
            if (lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }

            if (!RuneGameplayLoadoutAdapter.TryCreateAssignments(
                    runeProfile,
                    out IReadOnlyList<ExternalRuneLoadoutAssignment> runeSnapshot,
                    out string runeSnapshotFailure))
            {
                throw new InvalidOperationException(
                    "Rune profile cannot create a gameplay loadout snapshot: " + runeSnapshotFailure);
            }

            ApplyInventory(inventory);
            var merchantProductIds = new List<string>();
            if (inventory?.Products != null)
            {
                foreach (MerchantProduct product in inventory.Products)
                {
                    if (product != null) merchantProductIds.Add(product.ProductId);
                }
            }

            if (!MerchantItemSnapshotFactory.TryCreate(
                    merchantProductIds,
                    out IItemRunSnapshotProvider snapshotProvider,
                    out string snapshotFailure))
            {
                throw new InvalidOperationException(
                    "Merchant inventory cannot create a gameplay item snapshot: " + snapshotFailure);
            }
            if (bootstrap == null)
            {
                throw new InvalidOperationException(
                    "Greybox_Main requires DragonBoundBootstrap for Merchant gameplay items.");
            }

            InitializeBootstrap(snapshotProvider, runeSnapshot, runeProfile.AccountDay);
            Debug.Log(
                $"Merchant loadout loaded: Active={activeItems.Count}, " +
                $"Passive={passiveItems.Count}, Runes={runeSnapshot.Count}, " +
                $"DayKey={dayKey?.Value ?? "unknown"}",
                this);
            LoadoutLoaded?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to load gameplay Merchant items: {exception.Message}", this);
            InitializeBootstrap(
                new EmptyItemRunSnapshotProvider(),
                Array.Empty<ExternalRuneLoadoutAssignment>(),
                1);
        }
    }

    private void InitializeBootstrap(
        IItemRunSnapshotProvider itemSnapshot,
        IReadOnlyList<ExternalRuneLoadoutAssignment> runeSnapshot,
        int runeAccountDay)
    {
        if (bootstrap == null || bootstrap.IsInitialized) return;
        if (!bootstrap.TrySetPlayerRuneLoadoutSnapshot(
                runeSnapshot ?? Array.Empty<ExternalRuneLoadoutAssignment>(),
                out string snapshotError))
        {
            Debug.LogError(
                "Greybox Rune loadout snapshot was rejected before initialization: " + snapshotError,
                this);
        }
        if (!bootstrap.TrySetPlayerRuneAccountDay(
                Math.Max(1, runeAccountDay),
                out string accountDayError))
        {
            Debug.LogError(
                "Greybox Rune AccountDay was rejected before initialization: " + accountDayError,
                this);
        }

        bootstrap.InitializeWithItemSnapshotProvider(itemSnapshot);
    }

    private void OnDestroy()
    {
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }

    public MerchantProduct GetActiveItem(int index)
    {
        return index >= 0 && index < activeItems.Count ? activeItems[index] : null;
    }

    public MerchantProduct GetPassiveItem(int index)
    {
        return index >= 0 && index < passiveItems.Count ? passiveItems[index] : null;
    }

    private void ApplyInventory(MerchantInventory inventory)
    {
        // This component owns only the loadout data. GreyboxHudView is the single writer
        // for gameplay slot sprites, visibility and cooldown masks. Keeping that boundary
        // prevents a late empty Merchant response from erasing a development snapshot UI.
        activeItems.Clear();
        passiveItems.Clear();
        if (inventory?.LoadoutProducts == null) return;

        foreach (MerchantProduct product in inventory.LoadoutProducts)
        {
            if (product == null) continue;

            if (string.Equals(product.ItemType, "Active", StringComparison.OrdinalIgnoreCase))
            {
                if (activeItems.Count >= ActiveSlotCount) continue;
                activeItems.Add(product);
                continue;
            }

            if (string.Equals(product.ItemType, "Passive", StringComparison.OrdinalIgnoreCase))
            {
                if (passiveItems.Count >= PassiveSlotCount) continue;
                passiveItems.Add(product);
            }
        }
    }
}
