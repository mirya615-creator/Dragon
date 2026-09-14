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
    private const string WeekSelectedSpritePath = "Main/Rank/图层 26";
    private const string WeekUnselectedSpritePath = "Main/Rank/图层 27";
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
    private LeaderboardPeriodType selectedPeriod;
    private int loadVersion;
    private bool viewReady;
    private Image weekImage;
    private Image monthImage;
    private Image chantTabImage;
    private Image lotteryTabImage;
    private Sprite selectedTabSprite;
    private Sprite unselectedTabSprite;
    private Sprite weekSelectedSprite;
    private Sprite weekUnselectedSprite;
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
        weekButton = background?.FindUi("WeekBtn")?.GetComponent<Button>();
        monthButton = background?.FindUi("MonthBtn")?.GetComponent<Button>();
        container = background?.FindUi("LeaderLimit/LeaderContainer");
        myLeaderItem = background?.FindUi("MyLeaderItemBg");
        myAvatarImage = myLeaderItem?.FindUi("AvatarImg")?.GetComponent<Image>();
        myLeaderboardPositionText = myLeaderItem?.FindUi("LeaderImg/Text")?.GetComponent<TMP_Text>();
        myRankText = myLeaderItem?.FindUi("RankText")?.GetComponent<TMP_Text>();
        myTotalStarsText = myLeaderItem?.FindUi("RankText/StarAct")?.GetComponent<TMP_Text>();
        itemPrefab = DragonBound.Presentation.UiAssets.Load<GameObject>(ItemResourcePath);

        weekImage = weekButton.GetComponent<Image>();
        monthImage = monthButton.GetComponent<Image>();
        weekSelectedSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(WeekSelectedSpritePath);
        weekUnselectedSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(WeekUnselectedSpritePath);
        firstPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(FirstPlaceSpritePath);
        secondPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(SecondPlaceSpritePath);
        thirdPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(ThirdPlaceSpritePath);
        otherPlaceSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(OtherPlaceSpritePath);


        // 纯 sprite 驱动的 Tab 高亮：避免 ColorTint 给选中态叠一层透明色。
        weekButton.transition = Selectable.Transition.None;
        monthButton.transition = Selectable.Transition.None;


        viewReady = weekButton != null && monthButton != null &&
            weekImage != null && monthImage != null &&
            weekSelectedSprite != null && weekUnselectedSprite != null &&
            firstPlaceSprite != null && secondPlaceSprite != null &&
            thirdPlaceSprite != null && otherPlaceSprite != null &&
            container != null && itemPrefab != null && myLeaderItem != null &&
            myAvatarImage != null && myLeaderboardPositionText != null &&
            myRankText != null && myTotalStarsText != null;
        if (!viewReady)
        {
            Debug.LogError(
                "MainLeaderboardController requires Bg/WeekBtn+Image, Bg/MonthBtn+Image, " +
                "Main/Rank/图层 26 & 27, First, Second, Third & Other, " +
                "Bg/LeaderLimit/LeaderContainer and " +
                "Bg/MyLeaderItemBg with LeaderImg/Text, AvatarImg and RankText/StarAct.",
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
        weekImage.sprite = weekIsSelected ? weekSelectedSprite : weekUnselectedSprite;
        monthImage.sprite = weekIsSelected ? weekUnselectedSprite : weekSelectedSprite;
        // 让两张图按按钮原大小显示，不被 Image 的 Preserve Aspect 拉伸
        weekImage.preserveAspect = false;
        monthImage.preserveAspect = false;
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

        for (int index = 0; index < players.Count; index++)
        {
            LeaderboardPlayer player = players[index];
            GameObject item = Instantiate(itemPrefab, container, false);
            item.name = $"LeaderItemBg_{index + 1}";
            ApplyLeaderboardPositionVisual(item.transform, index + 1);

            Image avatar = item.transform.FindUi("AvatarImg")?.GetComponent<Image>();
            if (avatar != null)
            {
                PlayerAvatarPrefabPresenter.Mount(
                    avatar.rectTransform,
                    ResolveAvatarId(player));
            }

            PlayerRankState rank = RankProgressionRules.Calculate(player.TotalRankStars);
            string rankName = player.RankLevel >= 10
                ? rank.RankName
                : RankProgressionRules.GetDisplayName(rank);
            SetText(item.transform.FindUi("RankText"), rankName);
            SetText(item.transform.FindUi("RankText/StarAct"), player.TotalRankStars.ToString());
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ApplyLeaderboardPositionVisual(Transform item, int position)
    {
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
        ApplyLeaderboardPositionVisual(myLeaderItem, position);

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
