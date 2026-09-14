using System;
using DragonBound.Presentation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainRankController : MonoBehaviour
{
    private const int PromotionPreviewMilliseconds = 800;
    private const int RankReadAttempts = 3;
    private const int RankReadRetryMilliseconds = 300;

    private TMP_Text rankText;
    private Transform threeStar;
    private Transform fourStar;
    private Transform fiveStar;
    private readonly List<GameObject> threeStarImages = new List<GameObject>();
    private readonly List<GameObject> fourStarImages = new List<GameObject>();
    private readonly List<GameObject> fiveStarImages = new List<GameObject>();
    private IPlayerRankGateway rankGateway;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        rankGateway = services.Rank;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();
        ResolveView();
    }

    private async void Start()
    {
        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("MainRankController requires an authenticated PlayerId.");
            return;
        }

        RankProgressResult settlement = RankSettlementSnapshotStore.Peek(session.PlayerId);
        RankProgressResult promotion = RankPromotionStore.Consume(session.PlayerId);
        PlayerRankState expectedState = settlement?.State ?? promotion?.State;
        try
        {
            if (promotion != null && promotion.PromotionFromState != null)
            {
                Display(promotion.PromotionFromState);
                await Task.Delay(PromotionPreviewMilliseconds, lifetimeCancellation.Token);
                Display(promotion.State);
            }
            else if (expectedState != null)
            {
                Display(expectedState);
            }

            PlayerRankState state = await LoadReconciledRankAsync(
                session.PlayerId,
                expectedState,
                lifetimeCancellation.Token);
            if (state != null)
            {
                Display(state);
                RankSettlementSnapshotStore.Clear(session.PlayerId);
            }
        }
        catch (OperationCanceledException)
        {
            // Scene was unloaded while rank data was being read.
        }
        catch (Exception exception)
        {
            if (expectedState != null)
            {
                Debug.LogWarning(
                    $"Unable to reconcile player rank; keeping the latest settlement snapshot: " +
                    exception.Message);
            }
            else
            {
                Debug.LogError($"Unable to load player rank: {exception.Message}");
            }
        }
    }

    private async Task<PlayerRankState> LoadReconciledRankAsync(
        string playerId,
        PlayerRankState expectedState,
        CancellationToken cancellationToken)
    {
        Exception lastException = null;
        for (int attempt = 0; attempt < RankReadAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                PlayerRankState fetched = await rankGateway.GetRankAsync(playerId, cancellationToken);
                if (expectedState == null || IsSameOrNewer(fetched, expectedState)) return fetched;
                lastException = new InvalidOperationException(
                    "Rank query returned data older than the completed Run settlement.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastException = exception;
            }

            if (attempt + 1 < RankReadAttempts)
            {
                await Task.Delay(
                    RankReadRetryMilliseconds * (attempt + 1),
                    cancellationToken);
            }
        }

        if (expectedState != null)
        {
            Debug.LogWarning(
                "Rank reconciliation did not reach the completed Run version; " +
                "the settlement snapshot remains visible.");
            return null;
        }

        throw lastException ?? new InvalidOperationException("Rank query did not return a result.");
    }

    private static bool IsSameOrNewer(PlayerRankState fetched, PlayerRankState expected)
    {
        if (fetched == null || expected == null) return fetched != null;
        if (fetched.Version > 0 && expected.Version > 0)
        {
            if (fetched.Version > expected.Version) return true;
            if (fetched.Version < expected.Version) return false;
        }

        return fetched.Level == expected.Level &&
               fetched.Division == expected.Division &&
               fetched.CurrentStars == expected.CurrentStars;
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
        Transform rankTextTransform = transform.FindUi("RankText");
        rankText = rankTextTransform != null ? rankTextTransform.GetComponent<TMP_Text>() : null;
        threeStar = transform.FindUi("threeStar");
        fourStar = transform.FindUi("fourStar");
        fiveStar = transform.FindUi("fiveStar");

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
            Transform image = star.FindUi("Img");
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
