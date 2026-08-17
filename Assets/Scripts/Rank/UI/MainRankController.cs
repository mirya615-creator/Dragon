using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainRankController : MonoBehaviour
{
    private const int PromotionPreviewMilliseconds = 800;

    private TMP_Text rankText;
    private Transform threeStar;
    private Transform fourStar;
    private Transform fiveStar;
    private readonly List<GameObject> threeStarImages = new List<GameObject>();
    private readonly List<GameObject> fourStarImages = new List<GameObject>();
    private readonly List<GameObject> fiveStarImages = new List<GameObject>();
    private IPlayerRankGateway rankGateway;
    private CancellationTokenSource lifetimeCancellation;

    private void Awake()
    {
        rankGateway = new LocalPlayerRankGateway();
        lifetimeCancellation = new CancellationTokenSource();
        ResolveView();
    }

    private async void Start()
    {
        AuthSession session = AuthSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("MainRankController requires an authenticated PlayerId.");
            return;
        }

        try
        {
            PlayerRankState state = await rankGateway.GetRankAsync(
                session.PlayerId,
                lifetimeCancellation.Token);
            RankProgressResult promotion = RankPromotionStore.Consume(session.PlayerId);

            if (promotion != null && promotion.PromotionFromState != null)
            {
                Display(promotion.PromotionFromState);
                await Task.Delay(PromotionPreviewMilliseconds, lifetimeCancellation.Token);
                state = promotion.State;
            }

            Display(state);
        }
        catch (OperationCanceledException)
        {
            // Scene was unloaded while rank data was being read.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to load player rank: {exception.Message}");
        }
    }

    private void OnDestroy()
    {
        if (lifetimeCancellation == null) return;
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        lifetimeCancellation = null;
    }

    private void ResolveView()
    {
        Transform rankTextTransform = transform.Find("RankText");
        rankText = rankTextTransform != null ? rankTextTransform.GetComponent<TMP_Text>() : null;
        threeStar = transform.Find("threeStar");
        fourStar = transform.Find("fourStar");
        fiveStar = transform.Find("fiveStar");

        CollectStarImages(threeStar, threeStarImages);
        CollectStarImages(fourStar, fourStarImages);
        CollectStarImages(fiveStar, fiveStarImages);

        if (rankText == null || threeStarImages.Count != 3 ||
            fourStarImages.Count != 4 || fiveStarImages.Count != 5)
        {
            Debug.LogError(
                "MainRankController expects RankText plus threeStar/fourStar/fiveStar " +
                "with 3/4/5 star children containing Img.");
        }
    }

    private static void CollectStarImages(Transform group, List<GameObject> output)
    {
        output.Clear();
        if (group == null) return;

        var ordered = new List<KeyValuePair<int, GameObject>>();
        for (int index = 0; index < group.childCount; index++)
        {
            Transform star = group.GetChild(index);
            Transform image = star.Find("Img");
            if (image == null) continue;

            int order = index;
            string suffix = star.name.StartsWith("star", StringComparison.OrdinalIgnoreCase)
                ? star.name.Substring(4)
                : string.Empty;
            if (int.TryParse(suffix, out int parsedOrder)) order = parsedOrder;
            ordered.Add(new KeyValuePair<int, GameObject>(order, image.gameObject));
        }

        ordered.Sort((left, right) => left.Key.CompareTo(right.Key));
        foreach (KeyValuePair<int, GameObject> item in ordered) output.Add(item.Value);
    }

    private void Display(PlayerRankState state)
    {
        if (state == null) return;
        if (rankText != null) rankText.text = RankProgressionRules.GetDisplayName(state);

        SetGroup(threeStar, threeStarImages, state.RequiredStars == 3, state.CurrentStars);
        SetGroup(fourStar, fourStarImages, state.RequiredStars == 4, state.CurrentStars);
        SetGroup(fiveStar, fiveStarImages, state.RequiredStars == 5, state.CurrentStars);
    }

    private static void SetGroup(
        Transform group,
        List<GameObject> images,
        bool visible,
        int filledStars)
    {
        if (group != null) group.gameObject.SetActive(visible);
        for (int index = 0; index < images.Count; index++)
        {
            images[index].SetActive(visible && index < filledStars);
        }
    }
}
