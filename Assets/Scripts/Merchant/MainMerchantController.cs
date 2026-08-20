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
    private const string OfferItemPrefabPath = "prefabs/ItemBg";
    private const string OwnedItemPrefabPath = "prefabs/Item";
    private const string MerchantAdPlacement = "merchant_rewarded_item";

    private readonly List<Button> buyButtons = new List<Button>();
    private readonly List<TMP_Text> buyButtonTexts = new List<TMP_Text>();
    private readonly HashSet<string> displayedProductIds = new HashSet<string>();
    private GameObject merchantPanel;
    private GameObject cancelItemPanel;
    private Transform itemContainer;
    private Transform ownedItemContainer;
    private Transform activeItemColumn;
    private Transform passiveItemColumn;
    private GameObject offerItemPrefab;
    private GameObject ownedItemPrefab;
    private TMP_Text tipText;
    private Button cancelItemButton;
    private Button confirmItemButton;
    private IMerchantGateway merchantGateway;
    private IMerchantItemIconProvider iconProvider;
    private IRewardedAdService rewardedAdService;
    private CancellationTokenSource lifetimeCancellation;
    private string playerId;
    private bool purchaseInProgress;
    private bool deleteInProgress;
    private Coroutine hideTipCoroutine;
    private MerchantProduct selectedDeleteProduct;

    private void Awake()
    {
        merchantGateway = new LocalMerchantGateway();
        iconProvider = new ResourcesMerchantItemIconProvider();
        lifetimeCancellation = new CancellationTokenSource();

        Transform panelTransform = transform.Find("MerchantPanel");
        itemContainer = panelTransform?.Find("Bg/ChatItemCon");
        ownedItemContainer = panelTransform?.Find("Bg/ItemContainer") ??
                             panelTransform?.Find("Bg/MyItemBg/ItemContainer");
        if (ownedItemContainer != null)
        {
            activeItemColumn = ownedItemContainer.Find("ActiveColumn");
            passiveItemColumn = ownedItemContainer.Find("PassiveColumn");
            if (activeItemColumn == null && ownedItemContainer.childCount > 0)
            {
                activeItemColumn = ownedItemContainer.GetChild(0);
            }
            if (passiveItemColumn == null && ownedItemContainer.childCount > 1)
            {
                passiveItemColumn = ownedItemContainer.GetChild(1);
            }
        }
        offerItemPrefab = Resources.Load<GameObject>(OfferItemPrefabPath);
        ownedItemPrefab = Resources.Load<GameObject>(OwnedItemPrefabPath);
        tipText = panelTransform?.Find("Bg/TipText")?.GetComponent<TMP_Text>();
        rewardedAdService = new MockRewardedAdService(
            transform,
            tipText != null ? tipText.font : null);
        Transform cancelPanelTransform = panelTransform?.Find("Bg/CancleItemPanel");
        cancelItemPanel = cancelPanelTransform?.gameObject;
        cancelItemButton = (cancelPanelTransform?.Find("CancleBtn") ??
                            cancelPanelTransform?.Find("Bg/CancleBtn"))?.GetComponent<Button>();
        confirmItemButton = (cancelPanelTransform?.Find("ConfirmBtn") ??
                             cancelPanelTransform?.Find("Bg/ConfirmBtn"))?.GetComponent<Button>();
        if (panelTransform == null || itemContainer == null || ownedItemContainer == null ||
            activeItemColumn == null || passiveItemColumn == null ||
            activeItemColumn == passiveItemColumn || offerItemPrefab == null ||
            ownedItemPrefab == null || cancelItemPanel == null || cancelItemButton == null ||
            confirmItemButton == null)
        {
            Debug.LogError(
                "MainMerchantController requires MerchantPanel/Bg/ChatItemCon, " +
                "Bg/ItemContainer with ActiveColumn and PassiveColumn, Bg/CancleItemPanel " +
                "with CancleBtn and ConfirmBtn, and Resources/prefabs/ItemBg and Item.");
            enabled = false;
            return;
        }

        cancelItemButton.onClick.AddListener(OnCancelDeleteClicked);
        confirmItemButton.onClick.AddListener(OnConfirmDeleteClicked);
        cancelItemPanel.SetActive(false);
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
        if (cancelItemButton != null) cancelItemButton.onClick.RemoveListener(OnCancelDeleteClicked);
        if (confirmItemButton != null) confirmItemButton.onClick.RemoveListener(OnConfirmDeleteClicked);
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
            GameObject itemObject = Instantiate(offerItemPrefab, itemContainer, false);
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

            priceText.text = product.PaymentType == MerchantPaymentType.RewardedAd
                ? "Video"
                : product.GoldPrice.ToString(CultureInfo.InvariantCulture);
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
        bool purchaseCompleted = false;
        ShowTip(string.Empty);
        SetButtonsInteractable(false);

        try
        {
            MerchantPurchaseResult result;
            if (product.PaymentType == MerchantPaymentType.RewardedAd)
            {
                string placementId = string.IsNullOrWhiteSpace(product.AdPlacementId)
                    ? MerchantAdPlacement
                    : product.AdPlacementId;
                RewardedAdResult adResult = await rewardedAdService.ShowAsync(
                    placementId,
                    lifetimeCancellation.Token);
                if (adResult != RewardedAdResult.Completed) return;

                result = await merchantGateway.ClaimRewardedAdProductAsync(
                    playerId,
                    offer.OfferId,
                    product.ProductId,
                    placementId,
                    Guid.NewGuid().ToString("N"),
                    Guid.NewGuid().ToString("N"),
                    lifetimeCancellation.Token);
            }
            else
            {
                result = await merchantGateway.PurchaseAsync(
                    playerId,
                    offer.OfferId,
                    product.ProductId,
                    Guid.NewGuid().ToString("N"),
                    lifetimeCancellation.Token);
            }
            switch (result.Status)
            {
                case MerchantPurchaseStatus.Success:
                    ShowTip(string.Empty);
                    AddOwnedProduct(product);
                    ShowSoldOut();
                    purchaseCompleted = true;
                    return;
                case MerchantPurchaseStatus.InsufficientGold:
                    ShowTip("Insufficient gold");
                    break;
                case MerchantPurchaseStatus.AlreadyPurchased:
                case MerchantPurchaseStatus.AlreadyOwned:
                    ShowTip(string.Empty);
                    ShowSoldOut();
                    purchaseCompleted = true;
                    return;
                case MerchantPurchaseStatus.AdVerificationFailed:
                    ShowTip("Video unavailable");
                    break;
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
            if (!purchaseCompleted) SetButtonsInteractable(true);
        }
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

    private void PopulateOwnedItems(MerchantInventory inventory)
    {
        displayedProductIds.Clear();
        ClearOwnedItemColumn(activeItemColumn);
        ClearOwnedItemColumn(passiveItemColumn);
        if (inventory?.Products == null) return;

        foreach (MerchantProduct product in inventory.Products)
        {
            AddOwnedProduct(product);
        }
    }

    private void AddOwnedProduct(MerchantProduct product)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
        {
            return;
        }

        Transform targetColumn;
        if (string.Equals(product.ItemType, "Active", StringComparison.OrdinalIgnoreCase))
        {
            targetColumn = activeItemColumn;
        }
        else if (string.Equals(product.ItemType, "Passive", StringComparison.OrdinalIgnoreCase))
        {
            targetColumn = passiveItemColumn;
        }
        else
        {
            Debug.LogWarning(
                $"Unknown Merchant ItemType '{product.ItemType}' for {product.ProductId}.");
            return;
        }

        if (!displayedProductIds.Add(product.ProductId)) return;

        GameObject itemObject = Instantiate(ownedItemPrefab, targetColumn, false);
        itemObject.name = "Item_" + product.ProductId;

        Image targetImage = itemObject.transform.Find("ItemImg")?.GetComponent<Image>();
        Sprite icon = iconProvider.Load(product.IconKey);
        if (targetImage != null && icon != null)
        {
            targetImage.sprite = icon;
            targetImage.color = Color.white;
        }

        Button deleteButton = itemObject.transform.Find("DelBtn")?.GetComponent<Button>();
        if (targetImage == null || deleteButton == null)
        {
            Debug.LogError("Item prefab requires ItemImg and DelBtn.");
            displayedProductIds.Remove(product.ProductId);
            itemObject.SetActive(false);
            Destroy(itemObject);
            return;
        }

        MerchantProduct capturedProduct = product;
        deleteButton.interactable = true;
        deleteButton.onClick.AddListener(() => OnDeleteItemClicked(capturedProduct));
    }

    private static void ClearOwnedItemColumn(Transform column)
    {
        if (column == null) return;
        for (int index = column.childCount - 1; index >= 0; index--)
        {
            GameObject existing = column.GetChild(index).gameObject;
            existing.SetActive(false);
            Destroy(existing);
        }
    }

    private void OnDeleteItemClicked(MerchantProduct product)
    {
        if (deleteInProgress || product == null) return;
        selectedDeleteProduct = product;
        cancelItemPanel.SetActive(true);
        cancelItemPanel.transform.SetAsLastSibling();
    }

    private void OnCancelDeleteClicked()
    {
        if (deleteInProgress) return;
        selectedDeleteProduct = null;
        cancelItemPanel.SetActive(false);
    }

    private async void OnConfirmDeleteClicked()
    {
        if (deleteInProgress || selectedDeleteProduct == null) return;
        deleteInProgress = true;
        cancelItemButton.interactable = false;
        confirmItemButton.interactable = false;

        try
        {
            MerchantRemoveResult result = await merchantGateway.RemoveInventoryItemAsync(
                playerId,
                selectedDeleteProduct.ProductId,
                Guid.NewGuid().ToString("N"),
                lifetimeCancellation.Token);
            PopulateOwnedItems(result.Inventory);
            selectedDeleteProduct = null;
            cancelItemPanel.SetActive(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to remove Merchant item: {exception.Message}");
        }
        finally
        {
            deleteInProgress = false;
            if (cancelItemButton != null) cancelItemButton.interactable = true;
            if (confirmItemButton != null) confirmItemButton.interactable = true;
        }
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
