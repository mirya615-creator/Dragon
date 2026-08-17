using System;
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
    private GameObject merchantPanel;
    private Transform itemContainer;
    private GameObject itemPrefab;
    private IMerchantGateway merchantGateway;
    private IMerchantItemIconProvider iconProvider;
    private CancellationTokenSource lifetimeCancellation;
    private string playerId;
    private bool purchaseInProgress;

    private void Awake()
    {
        merchantGateway = new LocalMerchantGateway();
        iconProvider = new ResourcesMerchantItemIconProvider();
        lifetimeCancellation = new CancellationTokenSource();

        Transform panelTransform = transform.Find("MerchantPanel");
        itemContainer = panelTransform?.Find("Bg/ChatItemCon");
        itemPrefab = Resources.Load<GameObject>(ItemPrefabPath);
        if (panelTransform == null || itemContainer == null || itemPrefab == null)
        {
            Debug.LogError(
                "MainMerchantController requires MerchantPanel/Bg/ChatItemCon and Resources/prefabs/ItemBg.");
            enabled = false;
            return;
        }

        merchantPanel = panelTransform.gameObject;
        merchantPanel.SetActive(false);
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

            SetText(itemObject.transform, "RankText", product.Rarity);
            SetText(itemObject.transform, "NameText", product.ChineseName);
            SetText(
                itemObject.transform,
                "InformText",
                product.ItemType + " : " + product.Introduction);

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
                    selectedPriceText.text = "Purchased";
                    return;
                case MerchantPurchaseStatus.InsufficientGold:
                    selectedPriceText.text = "Not enough";
                    break;
                case MerchantPurchaseStatus.AlreadyPurchased:
                    selectedPriceText.text = "Purchased";
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

    private static void SetText(Transform root, string childName, string value)
    {
        TMP_Text text = root.Find(childName)?.GetComponent<TMP_Text>();
        if (text != null) text.text = value;
    }
}
