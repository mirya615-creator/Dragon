using System;
using System.Collections.Generic;
using System.Threading;
using DragonBound.Bootstrap;
using DragonBound.Items;
using UnityEngine;
using UnityEngine.UI;

public interface IGameplayItemVisualProvider
{
    Sprite LoadIcon(MerchantProduct product);
}

public sealed class ResourcesGameplayItemVisualProvider : IGameplayItemVisualProvider
{
    private readonly IMerchantItemIconProvider merchantIconProvider =
        new ResourcesMerchantItemIconProvider();

    public Sprite LoadIcon(MerchantProduct product)
    {
        return product == null ? null : merchantIconProvider.Load(product.IconKey);
    }
}

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

    private sealed class SlotView
    {
        public Transform Transform;
        public Image Image;
        public Image CooldownMask;
        public Sprite EmptySprite;
        public Color EmptyColor;
        public MerchantProduct Product;
    }

    private readonly SlotView[] activeSlots = new SlotView[ActiveSlotCount];
    private readonly SlotView[] passiveSlots = new SlotView[PassiveSlotCount];
    private readonly List<MerchantProduct> activeItems = new List<MerchantProduct>();
    private readonly List<MerchantProduct> passiveItems = new List<MerchantProduct>();

    private CancellationTokenSource lifetimeCancellation;
    private IGameplayItemVisualProvider visualProvider;
    private DragonBoundBootstrap bootstrap;

    public IReadOnlyList<MerchantProduct> ActiveItems => activeItems;
    public IReadOnlyList<MerchantProduct> PassiveItems => passiveItems;
    public event Action LoadoutLoaded;

    private void Awake()
    {
        visualProvider = new ResourcesGameplayItemVisualProvider();
        lifetimeCancellation = new CancellationTokenSource();
        bootstrap = FindObjectOfType<DragonBoundBootstrap>();

        if (!ResolveSlots())
        {
            InitializeBootstrap(
                new EmptyItemRunSnapshotProvider(),
                Array.Empty<ExternalRuneLoadoutAssignment>(),
                1);
            enabled = false;
            return;
        }

        ClearAllSlots();
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

    /// <summary>
    /// Reserved visual hook for replacing Resources icons with Addressables,
    /// AssetBundles, downloaded sprites, or another production UI provider.
    /// </summary>
    public void SetVisualProvider(IGameplayItemVisualProvider provider)
    {
        visualProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        RefreshBoundIcons(activeSlots);
        RefreshBoundIcons(passiveSlots);
    }

    public MerchantProduct GetActiveItem(int index)
    {
        return index >= 0 && index < activeSlots.Length ? activeSlots[index].Product : null;
    }

    public MerchantProduct GetPassiveItem(int index)
    {
        return index >= 0 && index < passiveSlots.Length ? passiveSlots[index].Product : null;
    }

    private bool ResolveSlots()
    {
        Transform activeContainer = transform.Find("Active");
        Transform passiveContainer = transform.Find("Passtive") ?? transform.Find("Passive");
        if (activeContainer == null || passiveContainer == null)
        {
            Debug.LogError(
                "Greybox ItemContainer requires Active and Passtive child containers.",
                this);
            return false;
        }

        for (int index = 0; index < activeSlots.Length; index++)
        {
            activeSlots[index] = ResolveSlot(activeContainer, "Active" + index);
        }

        for (int index = 0; index < passiveSlots.Length; index++)
        {
            passiveSlots[index] = ResolveSlot(passiveContainer, "Passtive" + index);
        }

        return AllSlotsResolved(activeSlots) && AllSlotsResolved(passiveSlots);
    }

    private SlotView ResolveSlot(Transform container, string slotName)
    {
        Transform slotTransform = container.Find(slotName);
        Image slotImage = slotTransform != null ? slotTransform.GetComponent<Image>() : null;
        Image cooldownMask = slotTransform != null
            ? slotTransform.Find("CooldownMask")?.GetComponent<Image>()
            : null;

        if (slotTransform == null || slotImage == null)
        {
            Debug.LogError(
                $"Gameplay item slot '{container.name}/{slotName}' requires an Image.",
                this);
            return null;
        }

        return new SlotView
        {
            Transform = slotTransform,
            Image = slotImage,
            CooldownMask = cooldownMask,
            EmptySprite = slotImage.sprite,
            EmptyColor = slotImage.color
        };
    }

    private static bool AllSlotsResolved(SlotView[] slots)
    {
        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index] == null) return false;
        }

        return true;
    }

    private void ApplyInventory(MerchantInventory inventory)
    {
        ClearAllSlots();
        if (inventory?.Products == null) return;

        foreach (MerchantProduct product in inventory.Products)
        {
            if (product == null) continue;

            if (string.Equals(product.ItemType, "Active", StringComparison.OrdinalIgnoreCase))
            {
                if (activeItems.Count >= activeSlots.Length) continue;
                Bind(activeSlots[activeItems.Count], product);
                activeItems.Add(product);
                continue;
            }

            if (string.Equals(product.ItemType, "Passive", StringComparison.OrdinalIgnoreCase))
            {
                if (passiveItems.Count >= passiveSlots.Length) continue;
                Bind(passiveSlots[passiveItems.Count], product);
                passiveItems.Add(product);
            }
        }
    }

    private void ClearAllSlots()
    {
        activeItems.Clear();
        passiveItems.Clear();
        ClearSlots(activeSlots);
        ClearSlots(passiveSlots);
    }

    private static void ClearSlots(SlotView[] slots)
    {
        foreach (SlotView slot in slots)
        {
            if (slot == null) continue;
            slot.Product = null;
            if (slot.CooldownMask != null)
            {
                slot.CooldownMask.fillAmount = 0f;
                slot.CooldownMask.gameObject.SetActive(false);
            }
            slot.Image.sprite = slot.EmptySprite;
            slot.Image.color = slot.EmptyColor;
            slot.Transform.gameObject.SetActive(false);
        }
    }

    private void Bind(SlotView slot, MerchantProduct product)
    {
        slot.Product = product;
        slot.Transform.gameObject.SetActive(true);
        ApplyIcon(slot);
    }

    private void RefreshBoundIcons(SlotView[] slots)
    {
        foreach (SlotView slot in slots)
        {
            if (slot?.Product != null) ApplyIcon(slot);
        }
    }

    private void ApplyIcon(SlotView slot)
    {
        Sprite icon = visualProvider?.LoadIcon(slot.Product);
        if (icon == null)
        {
            // Item art is optional during frontend development. Keep the authored
            // placeholder until the visual provider can supply the final icon.
            slot.Image.sprite = slot.EmptySprite;
            slot.Image.color = slot.EmptyColor;
            return;
        }

        slot.Image.sprite = icon;
        slot.Image.color = Color.white;
    }
}
