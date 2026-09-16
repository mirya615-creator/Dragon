using System;
using DragonBound.Presentation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainLeaderboardController : MonoBehaviour
{
    private const string ItemResourcePath = "prefabs/LeaderItemBg";
    private const string V2SelectedTabSpritePath = "UIResources/Main/Rank/图层 41 拷贝";
    private const string V2UnselectedTabSpritePath = "UIResources/Main/Rank/图层 40";
    private const string FirstPlaceSpritePath = "Main/Rank/First";
    private const string SecondPlaceSpritePath = "Main/Rank/Second";
    private const string ThirdPlaceSpritePath = "Main/Rank/Third";
    private const string OtherPlaceSpritePath = "Main/Rank/Other";

    private ILeaderboardGateway leaderboardGateway;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;
    private Button weekButton;
    private Button monthButton;
    private Transform container;
    private RectTransform containerRect;
    private ScrollRect scrollRect;
    private GameObject itemPrefab;
    private Transform myLeaderItem;
    private Image myAvatarImage;
    private TMP_Text myLeaderboardPositionText;
    private TMP_Text myRankText;
    private TMP_Text myTotalStarsText;
    private readonly Transform[] podiumItems = new Transform[3];
    private LeaderboardPeriodType selectedPeriod;
    private int loadVersion;
    private bool viewReady;
    private bool usesPodiumLayout;
    private bool podiumReady = true;
    private Image weekImage;
    private Image monthImage;
    private Image chantTabImage;
    private Image lotteryTabImage;
    private Sprite selectedTabSprite;
    private Sprite unselectedTabSprite;
    private Sprite firstPlaceSprite;
    private Sprite secondPlaceSprite;
    private Sprite thirdPlaceSprite;
    private Sprite otherPlaceSprite;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        leaderboardGateway = services.Leaderboard;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();

        Transform background = transform.FindUi("Bg");
        weekButton = FindButton(background, "WeekBtn", "WeekBtnh");
        monthButton = background?.FindUi("MonthBtn")?.GetComponent<Button>();
        container = background?.FindUi("LeaderLimit/LeaderContainer");
        myLeaderItem = background?.FindUi("MyLeaderItemBg");
        myAvatarImage = myLeaderItem?.FindUi("AvatarImg")?.GetComponent<Image>();
        Transform myPositionText = myLeaderItem?.FindUi("LeaderText") ??
            myLeaderItem?.FindUi("LeaderImg/Text");
        myLeaderboardPositionText = myPositionText?.GetComponent<TMP_Text>();
        myRankText = myLeaderItem?.FindUi("RankText")?.GetComponent<TMP_Text>();
        myTotalStarsText = myLeaderItem?.FindUi("RankText/StarAct")?.GetComponent<TMP_Text>();
        ResolvePodium(background);
        itemPrefab = DragonBound.Presentation.UiAssets.Load<GameObject>(ItemResourcePath);

        weekImage = weekButton != null ? weekButton.GetComponent<Image>() : null;
        monthImage = monthButton != null ? monthButton.GetComponent<Image>() : null;
        ResolveTabSprites();
        if (!usesPodiumLayout)
        {
            firstPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(FirstPlaceSpritePath);
            secondPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(SecondPlaceSpritePath);
            thirdPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(ThirdPlaceSpritePath);
            otherPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(OtherPlaceSpritePath);
        }


        // 纯 sprite 驱动的 Tab 高亮：避免 ColorTint 给选中态叠一层透明色。
        if (weekButton != null) weekButton.transition = Selectable.Transition.None;
        if (monthButton != null) monthButton.transition = Selectable.Transition.None;


        viewReady = weekButton != null && monthButton != null &&
            weekImage != null && monthImage != null &&
            selectedTabSprite != null && unselectedTabSprite != null &&
            podiumReady &&
            (usesPodiumLayout ||
             (firstPlaceSprite != null && secondPlaceSprite != null &&
              thirdPlaceSprite != null && otherPlaceSprite != null)) &&
            container != null && itemPrefab != null && myLeaderItem != null &&
            myAvatarImage != null && myLeaderboardPositionText != null &&
            myRankText != null && myTotalStarsText != null;
        if (!viewReady)
        {
            Debug.LogError(
                "MainLeaderboardController requires Bg/WeekBtn+Image, Bg/MonthBtn+Image, " +
                "Main/Rank/图层 41 拷贝 & 图层 40, either Bg/OST with FirstItem, " +
                "SecondItem and ThirdItem or the V1 rank sprites, " +
                "Bg/LeaderLimit/LeaderContainer and " +
                "Bg/MyLeaderItemBg with LeaderText or LeaderImg/Text, AvatarImg and RankText/StarAct.",
                this);
            return;
        }

        weekButton.onClick.AddListener(ShowWeeklyLeaderboard);
        monthButton.onClick.AddListener(ShowMonthlyLeaderboard);
        ConfigureScrolling();
    }

    private void OnEnable()
    {
        if (viewReady) SelectPeriod(LeaderboardPeriodType.Weekly);
    }

    private void OnDestroy()
    {
        loadVersion++;
        if (weekButton != null) weekButton.onClick.RemoveListener(ShowWeeklyLeaderboard);
        if (monthButton != null) monthButton.onClick.RemoveListener(ShowMonthlyLeaderboard);
        if (lifetimeCancellation != null)
        {
            lifetimeCancellation.Cancel();
            lifetimeCancellation.Dispose();
            lifetimeCancellation = null;
        }
    }

    private void ShowWeeklyLeaderboard()
    {
        SelectPeriod(LeaderboardPeriodType.Weekly);
    }

    private void ShowMonthlyLeaderboard()
    {
        SelectPeriod(LeaderboardPeriodType.Monthly);
    }

    private void SelectPeriod(LeaderboardPeriodType periodType)
    {
        selectedPeriod = periodType;
        weekButton.interactable = selectedPeriod != LeaderboardPeriodType.Weekly;
        monthButton.interactable = selectedPeriod != LeaderboardPeriodType.Monthly;
        ApplyTabVisual();
        int requestVersion = ++loadVersion;
        _ = LoadLeaderboardAsync(periodType, requestVersion);
    }

    private void ApplyTabVisual()
    {
        bool weekIsSelected = selectedPeriod == LeaderboardPeriodType.Weekly;
        weekImage.sprite = weekIsSelected ? selectedTabSprite : unselectedTabSprite;
        monthImage.sprite = weekIsSelected ? unselectedTabSprite : selectedTabSprite;
        // 让两张图按按钮原大小显示，不被 Image 的 Preserve Aspect 拉伸
        weekImage.preserveAspect = false;
        monthImage.preserveAspect = false;
    }

    private void ResolveTabSprites()
    {
        UiAssetRegistry registry = UiAssets.Active;
        if (registry != null && string.Equals(registry.VariantId, "V2", StringComparison.Ordinal))
        {
            selectedTabSprite = registry.Load<Sprite>(V2SelectedTabSpritePath);
            unselectedTabSprite = registry.Load<Sprite>(V2UnselectedTabSpritePath);
            return;
        }

        // V1 keeps its independently authored scene sprites and never references V2 art.
        unselectedTabSprite = weekImage != null ? weekImage.sprite : null;
        selectedTabSprite = monthImage != null ? monthImage.sprite : null;
    }

    private static Button FindButton(Transform root, params string[] semanticKeys)
    {
        if (root == null || semanticKeys == null) return null;
        for (int index = 0; index < semanticKeys.Length; index++)
        {
            Transform candidate = root.FindUi(semanticKeys[index]);
            Button button = candidate != null ? candidate.GetComponent<Button>() : null;
            if (button != null) return button;
        }

        return null;
    }

    private void ResolvePodium(Transform background)
    {
        Transform podium = background?.FindUi("OST");
        usesPodiumLayout = podium != null;
        if (!usesPodiumLayout) return;

        string[] itemNames = { "FirstItem", "SecondItem", "ThirdItem" };
        for (int index = 0; index < itemNames.Length; index++)
        {
            Transform item = podium.FindUi(itemNames[index]);
            podiumItems[index] = item;
            if (item == null || item.FindUi("AvatarImg")?.GetComponent<Image>() == null ||
                item.FindUi("RankText/StarAct")?.GetComponent<TMP_Text>() == null)
            {
                podiumReady = false;
                Debug.LogError(
                    $"Bg/OST/{itemNames[index]} requires AvatarImg and RankText/StarAct.",
                    this);
            }
        }
    }

    private async Task LoadLeaderboardAsync(
        LeaderboardPeriodType periodType,
        int requestVersion)
    {
        try
        {
            AuthSession session = authSessionStore.Current;
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
            {
                Debug.LogError(
                    "MainLeaderboardController requires an authenticated PlayerId.",
                    this);
                return;
            }

            LeaderboardResult result = await leaderboardGateway.GetLeaderboardAsync(
                session.PlayerId,
                periodType,
                lifetimeCancellation.Token);
            if (requestVersion != loadVersion || !isActiveAndEnabled ||
                selectedPeriod != periodType)
            {
                return;
            }

            Render(result?.Players ?? Array.Empty<LeaderboardPlayer>());
            RenderLocalPlayer(session.PlayerId, result);
        }
        catch (OperationCanceledException)
        {
            // Scene was unloaded while leaderboard data was being read.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to load {periodType} leaderboard: {exception.Message}", this);
        }
    }

    private void Render(IReadOnlyList<LeaderboardPlayer> players)
    {
        for (int index = container.childCount - 1; index >= 0; index--)
        {
            GameObject previousItem = container.GetChild(index).gameObject;
            previousItem.SetActive(false);
            Destroy(previousItem);
        }

        int firstListIndex = 0;
        if (usesPodiumLayout)
        {
            RenderPodium(players);
            firstListIndex = podiumItems.Length;
        }

        for (int index = firstListIndex; index < players.Count; index++)
        {
            LeaderboardPlayer player = players[index];
            GameObject item = Instantiate(itemPrefab, container, false);
            item.name = $"LeaderItemBg_{index + 1}";
            ApplyLeaderboardPositionVisual(item.transform, index + 1);
            RenderPlayer(item.transform, player, true);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void RenderPodium(IReadOnlyList<LeaderboardPlayer> players)
    {
        for (int index = 0; index < podiumItems.Length; index++)
        {
            Transform item = podiumItems[index];
            if (item == null) continue;

            LeaderboardPlayer player = index < players.Count ? players[index] : null;
            bool hasPlayer = player != null;
            item.gameObject.SetActive(hasPlayer);
            if (hasPlayer) RenderPlayer(item, player, false);
        }
    }

    private void RenderPlayer(Transform item, LeaderboardPlayer player, bool showRankName)
    {
        if (item == null || player == null) return;

        Image avatar = item.FindUi("AvatarImg")?.GetComponent<Image>();
        if (avatar != null)
        {
            PlayerAvatarPrefabPresenter.Mount(
                avatar.rectTransform,
                ResolveAvatarId(player));
        }

        if (showRankName)
        {
            PlayerRankState rank = RankProgressionRules.Calculate(player.TotalRankStars);
            string rankName = player.RankLevel >= 10
                ? rank.RankName
                : RankProgressionRules.GetDisplayName(rank);
            SetText(item.FindUi("RankText"), rankName);
        }

        SetText(item.FindUi("RankText/StarAct"), player.TotalRankStars.ToString());
    }

    private void ApplyLeaderboardPositionVisual(Transform item, int position)
    {
        Transform directPositionText = item.FindUi("LeaderText");
        if (directPositionText != null)
        {
            SetText(directPositionText, position > 0 ? position.ToString() : "-");
            return;
        }

        Transform leaderImageTransform = item.FindUi("LeaderImg");
        Image leaderImage = leaderImageTransform?.GetComponent<Image>();
        Transform positionTextTransform = leaderImageTransform?.FindUi("Text");

        if (leaderImage != null)
        {
            switch (position)
            {
                case 1:
                    leaderImage.sprite = firstPlaceSprite;
                    break;
                case 2:
                    leaderImage.sprite = secondPlaceSprite;
                    break;
                case 3:
                    leaderImage.sprite = thirdPlaceSprite;
                    break;
                default:
                    leaderImage.sprite = otherPlaceSprite;
                    break;
            }

            leaderImage.preserveAspect = false;
        }

        if (positionTextTransform != null)
        {
            bool showPositionText = position <= 0 || position > 3;
            positionTextTransform.gameObject.SetActive(showPositionText);
            if (showPositionText)
                SetText(positionTextTransform, position > 0 ? position.ToString() : "-");
        }
    }

    private void RenderLocalPlayer(string playerId, LeaderboardResult result)
    {
        LeaderboardPlayer player = result?.LocalPlayer;
        int position = result?.LocalPlayerPosition ?? 0;
        if (usesPodiumLayout)
        {
            myLeaderboardPositionText.text = position > 0 ? position.ToString() : "-";
        }
        else
        {
            ApplyLeaderboardPositionVisual(myLeaderItem, position);
        }

        if (player == null)
        {
            myRankText.text = string.Empty;
            myTotalStarsText.text = "0";
            return;
        }

        PlayerAvatarPrefabPresenter.Mount(
            myAvatarImage.rectTransform,
            PlayerAvatarProfile.GetOrCreateAvatarId(playerId));
        PlayerRankState rank = RankProgressionRules.Calculate(player.TotalRankStars);
        myRankText.text = player.RankLevel >= 10
            ? rank.RankName
            : RankProgressionRules.GetDisplayName(rank);
        myTotalStarsText.text = player.TotalRankStars.ToString();
    }

    private void ConfigureScrolling()
    {
        containerRect = (RectTransform)container;
        RectTransform viewport = container.parent as RectTransform;
        if (viewport == null)
        {
            Debug.LogError("LeaderContainer requires a RectTransform parent viewport.", this);
            viewReady = false;
            return;
        }

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
        }

        // LeaderContainer placement is authored in the Main scene. Do not add a
        // ContentSizeFitter or normalize its RectTransform here: both operations
        // overwrite the manually tuned size and position when the panel opens.

        scrollRect = viewport.GetComponent<ScrollRect>();
        if (scrollRect == null) scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.content = containerRect;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 45f;
    }

    private string ResolveAvatarId(LeaderboardPlayer player)
    {
        AuthSession session = authSessionStore.Current;
        if (session != null && player != null &&
            string.Equals(player.PlayerId, session.PlayerId, StringComparison.Ordinal))
        {
            return PlayerAvatarProfile.GetOrCreateAvatarId(session.PlayerId);
        }
        return PlayerAvatarProfile.ResolveAvatarId(player?.PlayerId, player?.AvatarId);
    }

    private static void SetText(Transform target, string value)
    {
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value;
    }
}
