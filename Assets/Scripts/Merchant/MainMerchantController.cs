using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMerchantController : MonoBehaviour
{
    private enum MerchantTab
    {
        Chant,
        Lottery
    }

    private const string OfferItemPrefabPath = "prefabs/ItemBg";
    private const string OwnedItemPrefabPath = "prefabs/Item";
    private const string MerchantAdPlacement = "merchant_item";
    private const string MerchantLotteryAdPlacement = "lottery";
    private const string SelectedTabSpritePath = "Main/Merchant/图层 72";
    private const string UnselectedTabSpritePath = "Main/Merchant/图层 73";
    private const int ActiveItemLimit = 2;
    private const int PassiveItemLimit = 6;
    private const string ItemLimitMessage = "Item limit reached";
    private const string DeleteConfirmationMessage = "Are you sure to delete the item?";
    private const string NotEnoughGoldMessage = "Not Enough Gold";
    private const float TipDurationSeconds = 3f;
    private const float LotteryFastStepSeconds = 0.06f;
    private const float LotterySlowStepSeconds = 0.28f;
    private const float LotteryActiveScale = 1.1f;
    private const float LotteryWinnerScale = 1.12f;
    private const int LotteryMinimumFinishRounds = 2;

    private readonly List<Button> buyButtons = new List<Button>();
    private readonly List<TMP_Text> buyButtonTexts = new List<TMP_Text>();
    private readonly HashSet<Button> insufficientGoldButtons = new HashSet<Button>();
    private readonly List<Transform> lotteryItemViews = new List<Transform>();
    private readonly List<Vector3> lotteryItemBaseScales = new List<Vector3>();
    private readonly List<Color> lotteryItemBaseColors = new List<Color>();
    private readonly HashSet<string> displayedProductIds = new HashSet<string>();
    private GameObject merchantPanel;
    private GameObject cancelItemPanel;
    private GameObject lotteryContainer;
    private Transform itemContainer;
    private Transform ownedItemContainer;
    private Transform activeItemColumn;
    private Transform passiveItemColumn;
    private GameObject offerItemPrefab;
    private GameObject ownedItemPrefab;
    private Button chantButton;
    private Button lotteryButton;
    private Image chantTabImage;
    private Image lotteryTabImage;
    private Sprite selectedTabSprite;
    private Sprite unselectedTabSprite;
    private Button lotteryDrawButton;
    private Button cancelItemButton;
    private Button confirmItemButton;
    private IMerchantGateway merchantGateway;
    private IAuthSessionStore authSessionStore;
    private IMerchantItemIconProvider iconProvider;
    private IRewardedAdService rewardedAdService;
    private CancellationTokenSource lifetimeCancellation;
    private string playerId;
    private bool purchaseInProgress;
    private bool lotteryDrawInProgress;
    private bool deleteInProgress;
    private Coroutine lotterySpinRoutine;
    private TaskCompletionSource<bool> lotterySpinCompletion;
    private int lotterySpinTargetViewIndex = -1;
    private MerchantProduct selectedDeleteProduct;
    private MerchantLotteryOffer currentLotteryOffer;
    private MerchantOffer currentOffer;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        merchantGateway = services.Merchant;
        authSessionStore = services.AuthSession;
        rewardedAdService = services.RewardedAds;
        iconProvider = new ResourcesMerchantItemIconProvider();
        lifetimeCancellation = new CancellationTokenSource();

        Transform panelTransform = transform.Find("MerchantPanel");
        itemContainer = panelTransform?.Find("Bg/ChatItemCon");
        lotteryContainer = panelTransform?.Find("Bg/LotteryContainer")?.gameObject;
        chantButton = panelTransform?.Find("Bg/ChantBtn")?.GetComponent<Button>();
        lotteryButton = panelTransform?.Find("Bg/LotteryBtn")?.GetComponent<Button>();
        chantTabImage = ResolveTabImage(chantButton);
        lotteryTabImage = ResolveTabImage(lotteryButton);
        selectedTabSprite = Resources.Load<Sprite>(SelectedTabSpritePath);
        unselectedTabSprite = Resources.Load<Sprite>(UnselectedTabSpritePath);
        lotteryDrawButton = panelTransform?.Find("Bg/LotteryContainer/LotteryBtn")
            ?.GetComponent<Button>();
        ResolveLotteryItems();
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
        Transform cancelPanelTransform = panelTransform?.Find("Bg/CancleItemPanel");
        cancelItemPanel = cancelPanelTransform?.gameObject;
        cancelItemButton = (cancelPanelTransform?.Find("CancleBtn") ??
                            cancelPanelTransform?.Find("Bg/CancleBtn"))?.GetComponent<Button>();
        confirmItemButton = (cancelPanelTransform?.Find("ConfirmBtn") ??
                             cancelPanelTransform?.Find("Bg/ConfirmBtn"))?.GetComponent<Button>();
        if (panelTransform == null || itemContainer == null || lotteryContainer == null ||
            chantButton == null || lotteryButton == null || lotteryDrawButton == null ||
            chantTabImage == null || lotteryTabImage == null ||
            selectedTabSprite == null || unselectedTabSprite == null ||
            lotteryItemViews.Count != 8 || ownedItemContainer == null ||
            activeItemColumn == null || passiveItemColumn == null ||
            activeItemColumn == passiveItemColumn || offerItemPrefab == null ||
            ownedItemPrefab == null || cancelItemPanel == null || cancelItemButton == null ||
            confirmItemButton == null)
        {
            Debug.LogError(
                "MainMerchantController requires MerchantPanel/Bg with ChantBtn, LotteryBtn, " +
                "ChatItemCon and LotteryContainer containing LotteryBtn and 8 LotteryItems, " +
                "Bg/ItemContainer with ActiveColumn and PassiveColumn, Bg/CancleItemPanel " +
                "with CancleBtn and ConfirmBtn, Resources/Main/Merchant/图层 72 & 73, " +
                "and Resources/prefabs/ItemBg and Item.");
            enabled = false;
            return;
        }

        // Pure sprite-driven tab highlight: disable ColorTint so the select sprite
        // is never darkened by the pressed/selected transition.
        chantButton.transition = Selectable.Transition.None;
        lotteryButton.transition = Selectable.Transition.None;
        chantButton.onClick.AddListener(OnChantTabClicked);
        lotteryButton.onClick.AddListener(OnLotteryTabClicked);
        lotteryDrawButton.onClick.AddListener(OnLotteryDrawClicked);
        cancelItemButton.onClick.AddListener(OnCancelDeleteClicked);
        confirmItemButton.onClick.AddListener(OnConfirmDeleteClicked);
        cancelItemPanel.SetActive(false);
        merchantPanel = panelTransform.gameObject;
        ShowTab(MerchantTab.Chant);
        merchantPanel.SetActive(false);
        ShowTip(string.Empty);
    }

    private async void Start()
    {
        AuthSession session = authSessionStore.Current;
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

            MerchantOffer offer = await merchantGateway.GetCurrentOfferAsync(
                playerId,
                lifetimeCancellation.Token);
            if (offer == null || offer.Products == null || offer.Products.Count == 0) return;
            if (!MerchantPresentationStore.ShouldPresent(playerId, offer.OfferId)) return;

            MerchantLotteryOffer lotteryOffer = await merchantGateway.GetLotteryOfferAsync(
                playerId,
                offer.OfferId,
                lifetimeCancellation.Token);

            Populate(offer);
            PopulateLottery(lotteryOffer);
            merchantPanel.SetActive(true);
            merchantPanel.transform.SetAsLastSibling();
            ShowTab(MerchantTab.Chant);
            MerchantPresentationStore.MarkPresented(playerId, offer.OfferId);
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
        CancelLotterySpinAnimation();
        if (chantButton != null) chantButton.onClick.RemoveListener(OnChantTabClicked);
        if (lotteryButton != null) lotteryButton.onClick.RemoveListener(OnLotteryTabClicked);
        if (lotteryDrawButton != null) lotteryDrawButton.onClick.RemoveListener(OnLotteryDrawClicked);
        if (cancelItemButton != null) cancelItemButton.onClick.RemoveListener(OnCancelDeleteClicked);
        if (confirmItemButton != null) confirmItemButton.onClick.RemoveListener(OnConfirmDeleteClicked);
        TipTextService.Hide();
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }

    private void ResolveLotteryItems()
    {
        lotteryItemViews.Clear();
        if (lotteryContainer == null) return;

        Transform containerTransform = lotteryContainer.transform;
        for (int index = 0; index < containerTransform.childCount; index++)
        {
            Transform child = containerTransform.GetChild(index);
            if (!child.name.StartsWith("LotteryItem", StringComparison.OrdinalIgnoreCase)) continue;
            if (child.GetComponent<Image>() == null)
            {
                Debug.LogError($"{child.name} requires an Image.");
                continue;
            }

            lotteryItemViews.Add(child);
        }
    }

    private void OnChantTabClicked()
    {
        if (lotteryDrawInProgress) return;
        ShowTab(MerchantTab.Chant);
    }

    private void OnLotteryTabClicked()
    {
        if (lotteryDrawInProgress) return;
        ShowTab(MerchantTab.Lottery);
    }

    private void ShowTab(MerchantTab tab)
    {
        bool showChant = tab == MerchantTab.Chant;
        if (itemContainer != null) itemContainer.gameObject.SetActive(showChant);
        if (lotteryContainer != null) lotteryContainer.SetActive(!showChant);

        ApplyTabVisual(showChant);

        Button selectedButton = showChant ? chantButton : lotteryButton;
        if (selectedButton != null && selectedButton.gameObject.activeInHierarchy)
        {
            selectedButton.Select();
        }
    }

    /// <summary>Swaps the selected/unselected tab sprites (图层 72 / 图层 73).</summary>
    private void ApplyTabVisual(bool chantIsSelected)
    {
        if (chantTabImage != null)
        {
            chantTabImage.sprite = chantIsSelected ? selectedTabSprite : unselectedTabSprite;
        }
        if (lotteryTabImage != null)
        {
            lotteryTabImage.sprite = chantIsSelected ? unselectedTabSprite : selectedTabSprite;
        }
    }

    /// <summary>
    /// Resolves the Image that renders the tab button. Prefers Button.targetGraphic
    /// (the actual render target) and falls back to an Image on the button or on an
    /// "Image" child, so buttons whose sprite sits on a child node still work.
    /// </summary>
    private static Image ResolveTabImage(Button button)
    {
        if (button == null) return null;
        Image image = button.targetGraphic as Image;
        if (image != null) return image;
        image = button.GetComponent<Image>();
        if (image != null) return image;
        return button.transform.Find("Image")?.GetComponent<Image>();
    }

    private void PopulateLottery(MerchantLotteryOffer offer)
    {
        CancelLotterySpinAnimation();
        currentLotteryOffer = offer;
        int productCount = offer?.Products != null ? offer.Products.Count : 0;
        for (int index = 0; index < lotteryItemViews.Count; index++)
        {
            Transform itemView = lotteryItemViews[index];
            bool hasProduct = index < productCount && offer.Products[index] != null;
            itemView.gameObject.SetActive(hasProduct);
            itemView.localScale = Vector3.one;
            Image image = itemView.GetComponent<Image>();
            if (image != null) image.color = Color.white;
            if (!hasProduct) continue;

            MerchantProduct product = offer.Products[index];
            Sprite icon = iconProvider.Load(product.IconKey);
            if (image != null && icon != null)
            {
                image.sprite = icon;
                image.color = Color.white;
            }
        }

        if (offer != null && offer.Drawn)
        {
            HighlightLotteryWinner(offer.WinningProductId);
        }
        RefreshAcquisitionState();
    }

    private async void OnLotteryDrawClicked()
    {
        if (lotteryDrawInProgress || purchaseInProgress ||
            (currentOffer != null && currentOffer.Purchased) ||
            currentLotteryOffer == null ||
            currentLotteryOffer.Drawn || currentLotteryOffer.Products == null ||
            currentLotteryOffer.Products.Count == 0)
        {
            return;
        }

        lotteryDrawInProgress = true;
        RefreshAcquisitionState();
        ShowTip(string.Empty);
        try
        {
            RewardedAdResult adResult = await rewardedAdService.ShowAsync(
                MerchantLotteryAdPlacement,
                lifetimeCancellation.Token);
            if (adResult != RewardedAdResult.Completed) return;

            StartLotterySpinAnimation();
            MerchantLotteryResult result = await merchantGateway.DrawLotteryAsync(
                playerId,
                currentLotteryOffer.LotteryOfferId,
                MerchantLotteryAdPlacement,
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                lifetimeCancellation.Token);
            if (result.Status == MerchantLotteryStatus.Success ||
                result.Status == MerchantLotteryStatus.AlreadyDrawn)
            {
                if (result.WinningProduct == null)
                {
                    CancelLotterySpinAnimation();
                    ShowTip("Unavailable");
                    return;
                }

                int winnerViewIndex = FindLotteryWinnerViewIndex(result.WinningProduct.ProductId);
                if (winnerViewIndex < 0 ||
                    !await CompleteLotterySpinAnimationAsync(winnerViewIndex))
                {
                    CancelLotterySpinAnimation();
                    ShowTip("Unavailable");
                    return;
                }

                if (result.Inventory != null) PopulateOwnedItems(result.Inventory);
                if (currentOffer != null)
                {
                    currentOffer.Purchased = true;
                    currentOffer.PurchasedProductId = result.WinningProduct.ProductId;
                }
                currentLotteryOffer.Drawn = true;
                currentLotteryOffer.WinningProductId = result.WinningProduct.ProductId;
                HighlightLotteryWinner(result.WinningProduct.ProductId);
                ShowTip("Won: " + result.WinningProduct.EnglishName);
                ShowSoldOut();
                return;
            }

            CancelLotterySpinAnimation();
            if (result.Status == MerchantLotteryStatus.AlreadyPurchased)
            {
                if (currentOffer != null) currentOffer.Purchased = true;
                ShowTip(string.Empty);
                ShowSoldOut();
                return;
            }

            if (result.Status == MerchantLotteryStatus.AdVerificationFailed)
            {
                ShowTip("Video unavailable");
                return;
            }

            ShowTip("Unavailable");
        }
        catch (OperationCanceledException)
        {
            CancelLotterySpinAnimation();
        }
        catch (Exception exception)
        {
            CancelLotterySpinAnimation();
            Debug.LogError($"Unable to draw Merchant lottery: {exception.Message}");
            ShowTip("Unavailable");
        }
        finally
        {
            lotteryDrawInProgress = false;
            RefreshAcquisitionState();
        }
    }

    private int FindLotteryWinnerViewIndex(string productId)
    {
        if (currentLotteryOffer?.Products == null) return -1;
        int count = Mathf.Min(lotteryItemViews.Count, currentLotteryOffer.Products.Count);
        for (int index = 0; index < count; index++)
        {
            MerchantProduct product = currentLotteryOffer.Products[index];
            if (product != null && string.Equals(product.ProductId, productId, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private void StartLotterySpinAnimation()
    {
        CancelLotterySpinAnimation();
        CaptureLotterySpinVisualState();
        lotterySpinTargetViewIndex = -1;
        lotterySpinCompletion = new TaskCompletionSource<bool>();
        lotterySpinRoutine = StartCoroutine(PlayLotterySpinRoutine());
    }

    private Task<bool> CompleteLotterySpinAnimationAsync(int targetViewIndex)
    {
        if (lotterySpinRoutine == null || lotterySpinCompletion == null)
        {
            return Task.FromResult(false);
        }
        lotterySpinTargetViewIndex = targetViewIndex;
        return lotterySpinCompletion.Task;
    }

    private IEnumerator PlayLotterySpinRoutine()
    {
        List<int> spinOrder = BuildLotterySpinOrder();
        if (spinOrder.Count == 0)
        {
            FinishLotterySpin(false);
            yield break;
        }

        int orderPosition = 0;
        ApplyLotterySpinHighlight(spinOrder[orderPosition]);
        while (lotterySpinTargetViewIndex < 0)
        {
            yield return new WaitForSecondsRealtime(LotteryFastStepSeconds);
            orderPosition = (orderPosition + 1) % spinOrder.Count;
            ApplyLotterySpinHighlight(spinOrder[orderPosition]);
        }

        int targetOrderPosition = spinOrder.IndexOf(lotterySpinTargetViewIndex);
        if (targetOrderPosition < 0)
        {
            FinishLotterySpin(false);
            yield break;
        }

        int landingOffset = (targetOrderPosition - orderPosition + spinOrder.Count) % spinOrder.Count;
        int remainingSteps = LotteryMinimumFinishRounds * spinOrder.Count + landingOffset;
        for (int step = 0; step < remainingSteps; step++)
        {
            float progress = (step + 1f) / remainingSteps;
            float interval = Mathf.Lerp(
                LotteryFastStepSeconds,
                LotterySlowStepSeconds,
                progress * progress);
            yield return new WaitForSecondsRealtime(interval);
            orderPosition = (orderPosition + 1) % spinOrder.Count;
            ApplyLotterySpinHighlight(spinOrder[orderPosition]);
        }

        yield return PulseLotteryWinner(lotterySpinTargetViewIndex);
        FinishLotterySpin(true);
    }

    private IEnumerator PulseLotteryWinner(int viewIndex)
    {
        if (viewIndex < 0 || viewIndex >= lotteryItemViews.Count) yield break;
        Transform winner = lotteryItemViews[viewIndex];
        Vector3 baseScale = viewIndex < lotteryItemBaseScales.Count
            ? lotteryItemBaseScales[viewIndex]
            : Vector3.one;
        const float halfPulseSeconds = 0.12f;
        for (int pulse = 0; pulse < 2; pulse++)
        {
            float elapsed = 0f;
            while (elapsed < halfPulseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                winner.localScale = baseScale * Mathf.Lerp(
                    LotteryActiveScale, LotteryWinnerScale, elapsed / halfPulseSeconds);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < halfPulseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                winner.localScale = baseScale * Mathf.Lerp(
                    LotteryWinnerScale, LotteryActiveScale, elapsed / halfPulseSeconds);
                yield return null;
            }
        }
    }

    private List<int> BuildLotterySpinOrder()
    {
        Canvas.ForceUpdateCanvases();
        List<int> indices = new List<int>();
        Vector2 center = Vector2.zero;
        for (int index = 0; index < lotteryItemViews.Count; index++)
        {
            Transform item = lotteryItemViews[index];
            if (item == null || !item.gameObject.activeSelf) continue;
            indices.Add(index);
            center += (Vector2)item.localPosition;
        }
        if (indices.Count == 0) return indices;
        center /= indices.Count;
        indices.Sort((left, right) =>
        {
            float leftAngle = GetClockwiseLotteryAngle(lotteryItemViews[left].localPosition, center);
            float rightAngle = GetClockwiseLotteryAngle(lotteryItemViews[right].localPosition, center);
            return leftAngle.CompareTo(rightAngle);
        });
        return indices;
    }

    private static float GetClockwiseLotteryAngle(Vector2 position, Vector2 center)
    {
        Vector2 offset = position - center;
        return Mathf.Repeat(Mathf.PI * 0.5f - Mathf.Atan2(offset.y, offset.x), Mathf.PI * 2f);
    }

    private void CaptureLotterySpinVisualState()
    {
        lotteryItemBaseScales.Clear();
        lotteryItemBaseColors.Clear();
        foreach (Transform item in lotteryItemViews)
        {
            lotteryItemBaseScales.Add(item != null ? item.localScale : Vector3.one);
            Image image = item != null ? item.GetComponent<Image>() : null;
            lotteryItemBaseColors.Add(image != null ? image.color : Color.white);
        }
    }

    private void ApplyLotterySpinHighlight(int activeViewIndex)
    {
        for (int index = 0; index < lotteryItemViews.Count; index++)
        {
            Transform item = lotteryItemViews[index];
            if (item == null || !item.gameObject.activeSelf) continue;
            Vector3 baseScale = index < lotteryItemBaseScales.Count
                ? lotteryItemBaseScales[index]
                : Vector3.one;
            Color baseColor = index < lotteryItemBaseColors.Count
                ? lotteryItemBaseColors[index]
                : Color.white;
            bool active = index == activeViewIndex;
            item.localScale = active ? baseScale * LotteryActiveScale : baseScale;
            Image image = item.GetComponent<Image>();
            if (image != null)
            {
                image.color = active
                    ? baseColor
                    : new Color(baseColor.r * 0.55f, baseColor.g * 0.55f, baseColor.b * 0.55f, baseColor.a);
            }
        }
    }

    private void RestoreLotterySpinVisuals()
    {
        for (int index = 0; index < lotteryItemViews.Count; index++)
        {
            Transform item = lotteryItemViews[index];
            if (item == null) continue;
            item.localScale = index < lotteryItemBaseScales.Count
                ? lotteryItemBaseScales[index]
                : Vector3.one;
            Image image = item.GetComponent<Image>();
            if (image != null && index < lotteryItemBaseColors.Count)
            {
                image.color = lotteryItemBaseColors[index];
            }
        }
    }

    private void FinishLotterySpin(bool completed)
    {
        RestoreLotterySpinVisuals();
        lotterySpinRoutine = null;
        lotterySpinTargetViewIndex = -1;
        TaskCompletionSource<bool> completion = lotterySpinCompletion;
        lotterySpinCompletion = null;
        completion?.TrySetResult(completed);
    }

    private void CancelLotterySpinAnimation()
    {
        if (lotterySpinRoutine != null)
        {
            StopCoroutine(lotterySpinRoutine);
            lotterySpinRoutine = null;
        }
        RestoreLotterySpinVisuals();
        lotterySpinTargetViewIndex = -1;
        TaskCompletionSource<bool> completion = lotterySpinCompletion;
        lotterySpinCompletion = null;
        completion?.TrySetResult(false);
    }

    private void HighlightLotteryWinner(string productId)
    {
        RestoreLotterySpinVisuals();
        if (currentLotteryOffer?.Products == null) return;
        for (int index = 0; index < lotteryItemViews.Count; index++)
        {
            Transform itemView = lotteryItemViews[index];
            bool isWinner = index < currentLotteryOffer.Products.Count &&
                currentLotteryOffer.Products[index] != null &&
                string.Equals(
                    currentLotteryOffer.Products[index].ProductId,
                    productId,
                    StringComparison.Ordinal);
            itemView.localScale = isWinner ? Vector3.one * LotteryWinnerScale : Vector3.one;
            Image image = itemView.GetComponent<Image>();
            if (image != null) image.color = Color.white;
        }
    }

    private void Populate(MerchantOffer offer)
    {
        currentOffer = offer;
        buyButtons.Clear();
        buyButtonTexts.Clear();
        insufficientGoldButtons.Clear();
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
        RefreshAcquisitionState();
    }

    private async void OnBuyClicked(
        MerchantOffer offer,
        MerchantProduct product,
        Button selectedButton,
        TMP_Text selectedPriceText)
    {
        if (purchaseInProgress || lotteryDrawInProgress ||
            insufficientGoldButtons.Contains(selectedButton) ||
            (currentOffer != null && currentOffer.Purchased)) return;
        if (IsOwnedItemLimitReached(product))
        {
            ShowTip(ItemLimitMessage);
            return;
        }

        purchaseInProgress = true;
        ShowTip(string.Empty);
        RefreshAcquisitionState();

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

                string rewardScope = offer.OfferId + "|" + product.ProductId;
                string adEventId = PendingAdEventStore.GetOrCreate(
                    playerId, placementId, rewardScope);
                result = await merchantGateway.ClaimRewardedAdProductAsync(
                    playerId,
                    offer.OfferId,
                    product.ProductId,
                    placementId,
                    adEventId,
                    adEventId,
                    lifetimeCancellation.Token);
                if (result.Status == MerchantPurchaseStatus.Success ||
                    result.Status == MerchantPurchaseStatus.AlreadyPurchased ||
                    result.Status == MerchantPurchaseStatus.AlreadyOwned)
                {
                    PendingAdEventStore.Complete(
                        playerId, placementId, rewardScope, adEventId);
                }
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
                    offer.Purchased = true;
                    offer.PurchasedProductId = product.ProductId;
                    ShowSoldOut();
                    return;
                case MerchantPurchaseStatus.InsufficientGold:
                    insufficientGoldButtons.Add(selectedButton);
                    selectedPriceText.text = product.GoldPrice.ToString(CultureInfo.InvariantCulture);
                    ShowTip(NotEnoughGoldMessage);
                    break;
                case MerchantPurchaseStatus.AlreadyPurchased:
                case MerchantPurchaseStatus.AlreadyOwned:
                    ShowTip(string.Empty);
                    offer.Purchased = true;
                    ShowSoldOut();
                    return;
                case MerchantPurchaseStatus.AdVerificationFailed:
                    ShowTip("Video unavailable");
                    break;
                default:
                    selectedPriceText.text = "Gone";
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
            selectedPriceText.text = "Gone";
        }
        finally
        {
            purchaseInProgress = false;
            RefreshAcquisitionState();
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (Button button in buyButtons)
        {
            if (button != null)
            {
                button.interactable = interactable &&
                    !insufficientGoldButtons.Contains(button);
            }
        }
    }

    private void ShowSoldOut()
    {
        if (currentOffer != null) currentOffer.Purchased = true;
        SetButtonsInteractable(false);
        foreach (TMP_Text buttonText in buyButtonTexts)
        {
            if (buttonText != null) buttonText.text = "Sold out";
        }
        if (lotteryDrawButton != null) lotteryDrawButton.interactable = false;
    }

    private void RefreshAcquisitionState()
    {
        bool requestInProgress = purchaseInProgress || lotteryDrawInProgress;
        if (chantButton != null) chantButton.interactable = !requestInProgress;
        if (lotteryButton != null) lotteryButton.interactable = !requestInProgress;

        if (currentOffer != null && currentOffer.Purchased)
        {
            ShowSoldOut();
            return;
        }

        SetButtonsInteractable(!requestInProgress);
        if (lotteryDrawButton != null)
        {
            lotteryDrawButton.interactable = !requestInProgress &&
                currentLotteryOffer != null &&
                !currentLotteryOffer.Drawn &&
                currentLotteryOffer.Products != null &&
                currentLotteryOffer.Products.Count > 0;
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

    private bool IsOwnedItemLimitReached(MerchantProduct product)
    {
        if (product == null)
        {
            return false;
        }

        if (string.Equals(product.ItemType, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return CountOwnedItemViews(activeItemColumn) >= ActiveItemLimit;
        }

        if (string.Equals(product.ItemType, "Passive", StringComparison.OrdinalIgnoreCase))
        {
            return CountOwnedItemViews(passiveItemColumn) >= PassiveItemLimit;
        }

        return false;
    }

    private static int CountOwnedItemViews(Transform column)
    {
        if (column == null)
        {
            return 0;
        }

        int count = 0;
        for (int index = 0; index < column.childCount; index++)
        {
            GameObject item = column.GetChild(index).gameObject;
            if (item.activeSelf && item.name.StartsWith("Item_", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
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
        TipTextService.Show(DeleteConfirmationMessage, TipDurationSeconds);
    }

    private void OnCancelDeleteClicked()
    {
        if (deleteInProgress) return;
        selectedDeleteProduct = null;
        cancelItemPanel.SetActive(false);
        TipTextService.Hide();
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
            TipTextService.Hide();
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
        TipTextService.Show(message, TipDurationSeconds);
    }

    private static void SetText(Transform root, string childName, string value)
    {
        TMP_Text text = root.Find(childName)?.GetComponent<TMP_Text>();
        if (text != null) text.text = value;
    }
}
