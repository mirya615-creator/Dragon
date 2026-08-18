using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMerchantController : MonoBehaviour
{
    private const string ItemPrefabPath = "prefabs/ItemBg";

    private readonly List<Button> buyButtons = new List<Button>();
    private readonly List<TMP_Text> buyButtonTexts = new List<TMP_Text>();
    private readonly List<Image> activeItemImages = new List<Image>();
    private readonly List<Image> passiveItemImages = new List<Image>();
    private readonly HashSet<string> displayedProductIds = new HashSet<string>();
    private GameObject merchantPanel;
    private Transform itemContainer;
    private Transform ownedItemContainer;
    private GameObject itemPrefab;
    private TMP_Text tipText;
    private IMerchantGateway merchantGateway;
    private IMerchantItemIconProvider iconProvider;
    private CancellationTokenSource lifetimeCancellation;
    private string playerId;
    private bool purchaseInProgress;
    private Coroutine hideTipCoroutine;
    private int activeItemCount;
    private int passiveItemCount;

    private void Awake()
    {
        merchantGateway = new LocalMerchantGateway();
        iconProvider = new ResourcesMerchantItemIconProvider();
        lifetimeCancellation = new CancellationTokenSource();

        Transform panelTransform = transform.Find("MerchantPanel");
        itemContainer = panelTransform?.Find("Bg/ChatItemCon");
        ownedItemContainer = panelTransform?.Find("Bg/ItemContainer");
        itemPrefab = Resources.Load<GameObject>(ItemPrefabPath);
        tipText = panelTransform?.Find("Bg/TipText")?.GetComponent<TMP_Text>();
        if (panelTransform == null || itemContainer == null || ownedItemContainer == null || itemPrefab == null)
        {
            Debug.LogError(
                "MainMerchantController requires MerchantPanel/Bg/ChatItemCon, Bg/ItemContainer, " +
                "and Resources/prefabs/ItemBg.");
            enabled = false;
            return;
        }

        ResolveOwnedItemSlots();
        merchantPanel = panelTransform.gameObject;
        merchantPanel.SetActive(false);
        ShowTip(string.Empty);
    }

    private async void Start()
    {
        AuthSession session = AuthSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("MainMerchantController requires an authenticated PlayerId.");
            return;
        }

        playerId = session.PlayerId;
        try
        {
            MerchantInventory inventory = await merchantGateway.GetInventoryAsync(
                playerId,
                lifetimeCancellation.Token);
            PopulateOwnedItems(inventory);

            // Login -> Main never opens Merchant. Only a completed second run
            // marks a one-shot presentation request before returning to Main.
            if (!MerchantPresentationStore.TryConsumePending(playerId)) return;

            MerchantOffer offer = await merchantGateway.GetCurrentOfferAsync(
                playerId,
                lifetimeCancellation.Token);
            if (offer == null || offer.Products == null || offer.Products.Count == 0) return;

            Populate(offer);
            merchantPanel.SetActive(true);
            merchantPanel.transform.SetAsLastSibling();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to load Merchant offer: {exception.Message}");
        }
    }

    private void OnDestroy()
    {
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }

    private void Populate(MerchantOffer offer)
    {
        buyButtons.Clear();
        buyButtonTexts.Clear();
        for (int index = itemContainer.childCount - 1; index >= 0; index--)
        {
            GameObject existing = itemContainer.GetChild(index).gameObject;
            existing.SetActive(false);
            Destroy(existing);
        }

        foreach (MerchantProduct product in offer.Products)
        {
            GameObject itemObject = Instantiate(itemPrefab, itemContainer, false);
            itemObject.name = "ItemBg_" + product.ProductId;

            SetText(
                itemObject.transform,
                "RankText",
                MerchantItemCatalog.GetEnglishRarity(product.Rarity));
            SetText(itemObject.transform, "NameText", product.EnglishName);
            SetText(
                itemObject.transform,
                "InformText",
                product.ItemType + " : " +
                MerchantItemCatalog.GetEnglishIntroduction(product.ProductId));

            Transform buyTransform = itemObject.transform.Find("BuyBtn");
            Button buyButton = buyTransform?.GetComponent<Button>();
            TMP_Text priceText = buyTransform?.Find("Text (TMP)")?.GetComponent<TMP_Text>();
            if (buyButton == null || priceText == null)
            {
                Debug.LogError("ItemBg prefab requires BuyBtn with a Text (TMP) child.");
                continue;
            }

            priceText.text = product.GoldPrice.ToString(CultureInfo.InvariantCulture);
            Image itemImage = itemObject.transform.Find("CItemImg")?.GetComponent<Image>();
            Sprite icon = iconProvider.Load(product.IconKey);
            if (itemImage != null && icon != null) itemImage.sprite = icon;

            MerchantProduct selectedProduct = product;
            buyButton.onClick.AddListener(() => OnBuyClicked(offer, selectedProduct, buyButton, priceText));
            buyButtons.Add(buyButton);
            buyButtonTexts.Add(priceText);
        }
    }

    private async void OnBuyClicked(
        MerchantOffer offer,
        MerchantProduct product,
        Button selectedButton,
        TMP_Text selectedPriceText)
    {
        if (purchaseInProgress) return;
        purchaseInProgress = true;
        ShowTip(string.Empty);
        SetButtonsInteractable(false);

        try
        {
            MerchantPurchaseResult result = await merchantGateway.PurchaseAsync(
                playerId,
                offer.OfferId,
                product.ProductId,
                Guid.NewGuid().ToString("N"),
                lifetimeCancellation.Token);
            switch (result.Status)
            {
                case MerchantPurchaseStatus.Success:
                    ShowTip(string.Empty);
                    AddOwnedProduct(product);
                    ShowSoldOut();
                    return;
                case MerchantPurchaseStatus.InsufficientGold:
                    ShowTip("Insufficient gold");
                    break;
                case MerchantPurchaseStatus.AlreadyPurchased:
                    ShowTip(string.Empty);
                    ShowSoldOut();
                    return;
                default:
                    selectedPriceText.text = "Unavailable";
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to purchase Merchant product: {exception.Message}");
            selectedPriceText.text = "Unavailable";
        }
        finally
        {
            purchaseInProgress = false;
        }

        SetButtonsInteractable(true);
        selectedButton.interactable = true;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (Button button in buyButtons)
        {
            if (button != null) button.interactable = interactable;
        }
    }

    private void ShowSoldOut()
    {
        SetButtonsInteractable(false);
        foreach (TMP_Text buttonText in buyButtonTexts)
        {
            if (buttonText != null) buttonText.text = "Sold out";
        }
    }

    private void ResolveOwnedItemSlots()
    {
        activeItemImages.Clear();
        passiveItemImages.Clear();
        int activeSlotCount = ownedItemContainer.childCount / 2;
        for (int index = 0; index < ownedItemContainer.childCount; index++)
        {
            Image itemImage = ownedItemContainer.GetChild(index)
                .Find("ItemImg")?.GetComponent<Image>();
            if (itemImage == null) continue;

            if (index < activeSlotCount) activeItemImages.Add(itemImage);
            else passiveItemImages.Add(itemImage);
        }
    }

    private void PopulateOwnedItems(MerchantInventory inventory)
    {
        activeItemCount = 0;
        passiveItemCount = 0;
        displayedProductIds.Clear();
        if (inventory?.Products == null) return;

        foreach (MerchantProduct product in inventory.Products)
        {
            AddOwnedProduct(product);
        }
    }

    private void AddOwnedProduct(MerchantProduct product)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.ProductId) ||
            !displayedProductIds.Add(product.ProductId))
        {
            return;
        }

        bool isActive = string.Equals(product.ItemType, "Active", StringComparison.OrdinalIgnoreCase);
        List<Image> targetRow = isActive ? activeItemImages : passiveItemImages;
        int targetIndex = isActive ? activeItemCount : passiveItemCount;
        if (targetIndex >= targetRow.Count)
        {
            Debug.LogWarning($"No free {product.ItemType} Merchant item slot for {product.ProductId}.");
            return;
        }

        Image targetImage = targetRow[targetIndex];
        Sprite icon = iconProvider.Load(product.IconKey);
        if (targetImage != null && icon != null)
        {
            targetImage.sprite = icon;
            targetImage.color = Color.white;
        }

        if (isActive) activeItemCount++;
        else passiveItemCount++;
    }

    private void ShowTip(string message)
    {
        if (tipText == null) return;
        if (hideTipCoroutine != null)
        {
            StopCoroutine(hideTipCoroutine);
            hideTipCoroutine = null;
        }

        if (string.IsNullOrEmpty(message))
        {
            tipText.text = string.Empty;
            tipText.gameObject.SetActive(false);
            return;
        }

        tipText.text = message;
        tipText.gameObject.SetActive(true);
        hideTipCoroutine = StartCoroutine(HideTipAfterDelay());
    }

    private IEnumerator HideTipAfterDelay()
    {
        yield return new WaitForSecondsRealtime(3f);
        hideTipCoroutine = null;
        if (tipText == null) yield break;
        tipText.text = string.Empty;
        tipText.gameObject.SetActive(false);
    }

    private static void SetText(Transform root, string childName, string value)
    {
        TMP_Text text = root.Find(childName)?.GetComponent<TMP_Text>();
        if (text != null) text.text = value;
    }
}
