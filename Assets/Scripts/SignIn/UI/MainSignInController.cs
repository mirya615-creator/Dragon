using System;
using DragonBound.Presentation;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainSignInController : MonoBehaviour
{
    private const int SignInDayCount = 7;
    private const string V1TodaySpritePath = "Main/Signin/Today";
    private const string V1OtherSpritePath = "Main/Signin/other";
    private const string V2TodaySpritePath = "UIResources/Main/SignUp/Today";
    private const string V2OtherSpritePath = "UIResources/Main/SignUp/Other";
    private const string AutoOpenDayKeyPrefix = "dragonbound.signin.auto-open.v1.";
    private const float DisabledDayAlpha = 0.55f;

    private static readonly string[] DayNames =
    {
        string.Empty, "One", "Two", "Three", "Four", "Five", "Six", "Seven"
    };

    private GameObject signPanel;
    private Button openButton;
    private Button closeButton;
    private readonly Button[] dayButtons = new Button[SignInDayCount];
    private readonly Image[] dayImages = new Image[SignInDayCount];
    private readonly CanvasGroup[] dayVisualGroups = new CanvasGroup[SignInDayCount];
    private readonly float[] dayAuthoredAlphas = new float[SignInDayCount];
    private readonly UnityAction[] dayClickHandlers = new UnityAction[SignInDayCount];
    private Sprite todaySprite;
    private Sprite otherSprite;
    private ISignInGateway signInGateway;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;
    private SignInStatus status;
    private string playerId;
    private bool requestInProgress;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        signInGateway = services.SignIn;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();

        if (!ResolveView())
        {
            enabled = false;
            return;
        }

        openButton.onClick.AddListener(OpenPanel);
        closeButton.onClick.AddListener(ClosePanel);
        for (int index = 0; index < SignInDayCount; index++)
        {
            int day = index + 1;
            dayClickHandlers[index] = () => OnDayClicked(day);
            dayButtons[index].onClick.AddListener(dayClickHandlers[index]);
        }
        SetAllDayButtonsInteractable(false);
        ApplyDaySprites(0);
        signPanel.SetActive(false);
    }

    private async void Start()
    {
        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("MainSignInController requires an authenticated PlayerId.");
            return;
        }

        playerId = session.PlayerId;
        try
        {
            status = await signInGateway.GetStatusAsync(playerId, lifetimeCancellation.Token);
            RefreshView();
            if (status.CanClaim && ShouldAutoOpenToday(playerId, status.DayKey))
            {
                MarkAutoOpenedToday(playerId, status.DayKey);
                signPanel.SetActive(true);
            }
            else
            {
                signPanel.SetActive(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError("Unable to load sign-in status: " + exception.Message);
            SetAllDayButtonsInteractable(false);
        }
    }

    private void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(OpenPanel);
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
        for (int index = 0; index < SignInDayCount; index++)
        {
            if (dayButtons[index] != null && dayClickHandlers[index] != null)
                dayButtons[index].onClick.RemoveListener(dayClickHandlers[index]);
        }
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }

    private bool ResolveView()
    {
        Transform panel = transform.FindUi("SignPanel");
        Transform imageRoot = panel?.FindUi("Image");
        Transform content = imageRoot?.FindUi("ContentCon");
        signPanel = panel?.gameObject;
        openButton = transform.FindUi("SignBtn")?.GetComponent<Button>();
        closeButton = imageRoot?.FindUi("closeBtn")?.GetComponent<Button>();
        ResolveDaySprites();

        bool complete = signPanel != null && openButton != null && closeButton != null &&
                        content != null && todaySprite != null && otherSprite != null;
        for (int index = 0; index < SignInDayCount; index++)
        {
            Transform dayRoot;
            if (index < SignInDayCount - 1)
            {
                string dayName = index == 0 ? "Image" : "Image (" + index + ")";
                dayRoot = content?.FindUi(dayName);
            }
            else
            {
                dayRoot = imageRoot?.FindUi("Image (6)");
            }

            dayButtons[index] = dayRoot?.GetComponent<Button>();
            dayImages[index] = dayRoot?.GetComponent<Image>();
            complete &= dayButtons[index] != null && dayImages[index] != null;
            if (dayRoot != null)
            {
                CanvasGroup group = dayRoot.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = dayRoot.gameObject.AddComponent<CanvasGroup>();
                }

                dayVisualGroups[index] = group;
                dayAuthoredAlphas[index] = group.alpha;

                if (dayButtons[index] != null)
                {
                    // CanvasGroup owns the disabled appearance for the complete card.
                    // Keep Selectable from tinting only the root Image a second time.
                    ColorBlock colors = dayButtons[index].colors;
                    colors.disabledColor = colors.normalColor;
                    dayButtons[index].colors = colors;
                }
            }
        }

        if (!complete)
        {
            Debug.LogError(
                "MainSignInController requires SignBtn, SignPanel/Image/closeBtn, " +
                "seven sign-in day buttons, and Today/other sprites.");
        }
        return complete;
    }

    private void ResolveDaySprites()
    {
        UiAssetRegistry registry = UiAssets.Active;
        if (registry != null && string.Equals(registry.VariantId, "V2", StringComparison.Ordinal))
        {
            todaySprite = registry.Load<Sprite>(V2TodaySpritePath);
            otherSprite = registry.Load<Sprite>(V2OtherSpritePath);
            return;
        }

        todaySprite = UiAssets.Load<Sprite>(V1TodaySpritePath);
        otherSprite = UiAssets.Load<Sprite>(V1OtherSpritePath);
    }

    private void OpenPanel()
    {
        if (status != null) signPanel.SetActive(true);
    }

    private void ClosePanel()
    {
        if (!requestInProgress) signPanel.SetActive(false);
    }

    private void OnDayClicked(int day)
    {
        if (requestInProgress || status == null || !status.CanClaim ||
            day != status.CycleDay)
        {
            return;
        }
        _ = ClaimAsync();
    }

    private async System.Threading.Tasks.Task ClaimAsync()
    {
        if (requestInProgress || status == null || !status.CanClaim) return;
        requestInProgress = true;
        SetAllDayButtonsInteractable(false);
        try
        {
            SignInClaimResult result = await signInGateway.ClaimAsync(
                playerId,
                status.DayKey,
                SignInClaimMode.Standard,
                lifetimeCancellation.Token);
            if (result == null)
                throw new InvalidOperationException("Sign-in response is empty.");
            status.TotalClaims = result.TotalClaims;
            if (result.CycleDay >= 1 && result.CycleDay <= SignInDayCount)
                status.CycleDay = result.CycleDay;
            status.CanClaim = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ClientServiceException exception) when (
            exception.Code == "SIGNIN_ALREADY_CLAIMED" ||
            exception.Code == "SIGNIN_DAY_KEY_CHANGED")
        {
            Debug.LogWarning("Sign-in state changed; refreshing server status.");
            await RefreshStatusAfterConflictAsync();
        }
        catch (Exception exception)
        {
            if (exception is ClientServiceException serviceException)
            {
                Debug.LogError(
                    "Unable to claim sign-in reward." +
                    " HttpStatus=" + serviceException.HttpStatus +
                    " Code=" + serviceException.Code +
                    " TraceId=" + serviceException.TraceId +
                    " Message=" + serviceException.Message);
            }
            else
            {
                Debug.LogError("Unable to claim sign-in reward: " + exception.Message);
            }
        }
        finally
        {
            requestInProgress = false;
            RefreshView();
        }
    }

    private void RefreshView()
    {
        if (status == null || status.CycleDay < 1 || status.CycleDay > SignInDayCount)
        {
            ApplyDaySprites(0);
            SetAllDayButtonsInteractable(false);
            return;
        }

        ApplyDaySprites(status.CycleDay);
        for (int index = 0; index < SignInDayCount; index++)
        {
            int day = index + 1;
            bool isPastDay = day < status.CycleDay;
            bool isClaimedCurrentDay = day == status.CycleDay && !status.CanClaim;
            bool showDisabledVisual = isPastDay || isClaimedCurrentDay;

            // Future days stay visually available but cannot be claimed early.
            // requestInProgress temporarily blocks the current day without dimming it.
            bool allowPointerInteraction = !requestInProgress &&
                                           status.CanClaim &&
                                           day == status.CycleDay;
            SetDayState(index, allowPointerInteraction, !showDisabledVisual);
        }
    }

    private const int PreserveAuthoredSpriteMask = 1 << 6;
    private void ApplyDaySprites(int currentDay)
    {
        for (int index = 0; index < SignInDayCount; index++)
        {
            if (dayImages[index] != null) continue;
            if ((PreserveAuthoredSpriteMask &(1 << index)) != 0) continue;
                dayImages[index].sprite = index + 1 == currentDay ? todaySprite : otherSprite;
        }
    }

    private void SetAllDayButtonsInteractable(bool value)
    {
        for (int index = 0; index < SignInDayCount; index++)
        {
            SetDayState(index, value, value);
        }
    }

    private void SetDayState(int index, bool interactable, bool normalVisual)
    {
        if (index < 0 || index >= SignInDayCount)
        {
            return;
        }

        if (dayButtons[index] != null)
        {
            dayButtons[index].interactable = interactable;
        }

        CanvasGroup group = dayVisualGroups[index];
        if (group == null)
        {
            return;
        }

        float authoredAlpha = dayAuthoredAlphas[index];
        group.alpha = normalVisual ? authoredAlpha : authoredAlpha * DisabledDayAlpha;
        group.interactable = interactable;
        group.blocksRaycasts = interactable;
    }

    private static bool ShouldAutoOpenToday(string currentPlayerId, string dayKey)
    {
        if (string.IsNullOrWhiteSpace(currentPlayerId) || string.IsNullOrWhiteSpace(dayKey))
            return false;

        return !string.Equals(
            PlayerPrefs.GetString(AutoOpenPreferenceKey(currentPlayerId), string.Empty),
            dayKey,
            StringComparison.Ordinal);
    }

    private static void MarkAutoOpenedToday(string currentPlayerId, string dayKey)
    {
        PlayerPrefs.SetString(AutoOpenPreferenceKey(currentPlayerId), dayKey);
        PlayerPrefs.Save();
    }

    private static string AutoOpenPreferenceKey(string currentPlayerId)
    {
        return AutoOpenDayKeyPrefix + currentPlayerId;
    }

    public static string FormatDay(int cycleDay)
    {
        if (cycleDay < 1 || cycleDay > 7)
            throw new ArgumentOutOfRangeException(nameof(cycleDay));
        return "Day " + DayNames[cycleDay];
    }

    public static string FormatReward(SignInReward reward)
    {
        if (reward == null) return string.Empty;
        string name = string.IsNullOrWhiteSpace(reward.DisplayName)
            ? reward.Type == SignInRewardType.Gold ? "Gold" : reward.RuneRarity + " Rune"
            : reward.DisplayName;
        return name + ":" + Math.Max(0, reward.Amount);
    }

    public static string FormatRewards(IReadOnlyList<SignInReward> rewards)
    {
        if (rewards == null || rewards.Count == 0) return string.Empty;
        var builder = new StringBuilder();
        for (int index = 0; index < rewards.Count; index++)
        {
            if (index > 0) builder.Append("\n");
            builder.Append(FormatReward(rewards[index]));
        }
        return builder.ToString();
    }

    private async System.Threading.Tasks.Task RefreshStatusAfterConflictAsync()
    {
        try
        {
            status = await signInGateway.GetStatusAsync(
                playerId, lifetimeCancellation.Token);
            RefreshView();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError("Unable to reconcile sign-in status: " + exception.Message);
            RefreshView();
        }
    }

}
