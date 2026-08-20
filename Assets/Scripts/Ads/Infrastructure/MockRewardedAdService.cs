using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Development rewarded-video preview. It blocks the Main UI for three seconds.
/// </summary>
public sealed class MockRewardedAdService : IRewardedAdService
{
    private const int DurationSeconds = 3;

    private bool isShowing;
    private GameObject overlay;

    public async Task<RewardedAdResult> ShowAsync(
        string placementId,
        CancellationToken cancellationToken)
    {
        if (isShowing || string.IsNullOrWhiteSpace(placementId)) return RewardedAdResult.Failed;

        isShowing = true;
        TMP_Text countdownText = CreateOverlay();
        if (countdownText == null)
        {
            isShowing = false;
            return RewardedAdResult.Failed;
        }
        try
        {
            for (int remaining = DurationSeconds; remaining > 0; remaining--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                countdownText.text = $"Rewarded Video\n{remaining}";
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return RewardedAdResult.Completed;
        }
        finally
        {
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
            overlay = null;
            isShowing = false;
        }
    }

    private TMP_Text CreateOverlay()
    {
        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Rewarded ad preview requires an active Canvas.");
            return null;
        }

        Transform overlayParent = canvas.transform;
        overlay = new GameObject("RewardedAdOverlay", typeof(RectTransform), typeof(Image));
        overlay.layer = overlayParent.gameObject.layer;
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.SetParent(overlayParent, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        Image blocker = overlay.GetComponent<Image>();
        blocker.color = new Color32(15, 18, 24, 255);
        blocker.raycastTarget = true;

        GameObject labelObject = new GameObject("CountdownText", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = overlay.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(overlayRect, false);
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(900f, 300f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 64f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }
}
