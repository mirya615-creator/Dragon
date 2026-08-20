using System;
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

    private readonly List<Sprite> generatedAvatarSprites = new List<Sprite>();
    private readonly List<Texture2D> generatedAvatarTextures = new List<Texture2D>();
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

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        leaderboardGateway = services.Leaderboard;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();

        Transform background = transform.Find("Bg");
        weekButton = background?.Find("WeekBtn")?.GetComponent<Button>();
        monthButton = background?.Find("MonthBtn")?.GetComponent<Button>();
        container = background?.Find("LeaderLimit/LeaderContainer");
        myLeaderItem = background?.Find("MyLeaderItemBg");
        myAvatarImage = myLeaderItem?.Find("AvatarImg")?.GetComponent<Image>();
        myLeaderboardPositionText = myLeaderItem?.Find("LeaderImg/Text")?.GetComponent<TMP_Text>();
        myRankText = myLeaderItem?.Find("RankText")?.GetComponent<TMP_Text>();
        myTotalStarsText = myLeaderItem?.Find("RankText/StarAct")?.GetComponent<TMP_Text>();
        itemPrefab = Resources.Load<GameObject>(ItemResourcePath);

        viewReady = weekButton != null && monthButton != null && container != null &&
                    itemPrefab != null && myLeaderItem != null && myAvatarImage != null &&
                    myLeaderboardPositionText != null && myRankText != null &&
                    myTotalStarsText != null;
        if (!viewReady)
        {
            Debug.LogError(
                "MainLeaderboardController requires Bg/WeekBtn, Bg/MonthBtn, " +
                "Bg/LeaderLimit/LeaderContainer and Bg/MyLeaderItemBg with " +
                "LeaderImg/Text, AvatarImg and RankText/StarAct.",
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
        ClearGeneratedAvatars();
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
        int requestVersion = ++loadVersion;
        _ = LoadLeaderboardAsync(periodType, requestVersion);
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
        ClearGeneratedAvatars();
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
            SetText(item.transform.Find("LeaderImg/Text"), (index + 1).ToString());

            Image avatar = item.transform.Find("AvatarImg")?.GetComponent<Image>();
            if (avatar != null)
            {
                avatar.sprite = CreateAvatar(player.PlayerId);
                avatar.preserveAspect = true;
            }

            PlayerRankState rank = RankProgressionRules.Calculate(player.TotalRankStars);
            string rankName = player.RankLevel >= 10
                ? rank.RankName
                : RankProgressionRules.GetDisplayName(rank);
            SetText(item.transform.Find("RankText"), rankName);
            SetText(item.transform.Find("RankText/StarAct"), player.TotalRankStars.ToString());
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void RenderLocalPlayer(string playerId, LeaderboardResult result)
    {
        LeaderboardPlayer player = result?.LocalPlayer;
        int position = result?.LocalPlayerPosition ?? 0;
        myLeaderboardPositionText.text = position > 0 ? position.ToString() : "-";

        if (player == null)
        {
            myRankText.text = string.Empty;
            myTotalStarsText.text = "0";
            return;
        }

        myAvatarImage.sprite = CreateAvatar(playerId);
        myAvatarImage.preserveAspect = true;
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

        ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = container.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        containerRect.anchorMin = new Vector2(0f, 1f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.pivot = new Vector2(0.5f, 1f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(0f, containerRect.sizeDelta.y);

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

    private Sprite CreateAvatar(string seed)
    {
        const int size = 32;
        int hash = string.IsNullOrEmpty(seed) ? 0 : seed.GetHashCode();
        float hue = Mathf.Abs(hash % 360) / 360f;
        Color background = Color.HSVToRGB(hue, 0.55f, 0.85f);
        Color foreground = Color.Lerp(background, Color.white, 0.65f);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "LeaderboardAvatar",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        Vector2 headCenter = new Vector2(15.5f, 20f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool head = Vector2.Distance(new Vector2(x, y), headCenter) <= 6f;
                float bodyX = (x - 15.5f) / 12f;
                float bodyY = (y - 2f) / 12f;
                bool body = y >= 2 && y <= 14 && bodyX * bodyX + bodyY * bodyY <= 1f;
                pixels[y * size + x] = head || body ? foreground : background;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        generatedAvatarTextures.Add(texture);
        generatedAvatarSprites.Add(sprite);
        return sprite;
    }

    private void ClearGeneratedAvatars()
    {
        foreach (Sprite sprite in generatedAvatarSprites)
        {
            if (sprite != null) Destroy(sprite);
        }
        foreach (Texture2D texture in generatedAvatarTextures)
        {
            if (texture != null) Destroy(texture);
        }
        generatedAvatarSprites.Clear();
        generatedAvatarTextures.Clear();
    }

    private static void SetText(Transform target, string value)
    {
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value;
    }
}
