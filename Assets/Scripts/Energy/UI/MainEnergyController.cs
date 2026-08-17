using System;
using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the Main scene energy display and guarded Start button flow.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainEnergyController : MonoBehaviour
{
    private const int RewardedAdEnergy = 10;
    private const int RewardedAdDailyLimit = 3;
    private const string EnergyRewardPlacement = "energy_reward";

    private GameObject addEnergyPanel;
    private Button startButton;
    private Button addEnergyButton;
    private Button closeEnergyPanelButton;
    private Button videoButton;
    private TMP_Text currentAmountText;
    private TMP_Text maximumAmountText;
    private TMP_Text tipText;
    private TMP_Text rewardAmountText;
    private IPlayerEnergyGateway energyGateway;
    private IRewardedAdService rewardedAdService;
    private CancellationTokenSource lifetimeCancellation;
    private Coroutine recoveryRefreshCoroutine;
    private PlayerEnergyState currentEnergyState;
    private bool requestInProgress;
    private bool refreshInProgress;
    private bool adInProgress;
    private bool adStatusRefreshInProgress;
    private bool adDailyLimitReached;
    private bool transitionRequested;
    private int rewardedAdClaimsUsed;

    private void Awake()
    {
        energyGateway = new LocalPlayerEnergyGateway();
        lifetimeCancellation = new CancellationTokenSource();

        if (!ResolveView())
        {
            enabled = false;
            return;
        }

        rewardedAdService = new MockRewardedAdService(transform, currentAmountText.font);
        tipText.text = string.Empty;
        rewardAmountText.text = "+" + RewardedAdEnergy;
        maximumAmountText.text = "/" + LocalPlayerEnergyGateway.MaximumEnergy;
        addEnergyPanel.SetActive(false);
        startButton.onClick.AddListener(OnStartClicked);
        addEnergyButton.onClick.AddListener(ShowAddEnergyPanel);
        closeEnergyPanelButton.onClick.AddListener(HideAddEnergyPanel);
        videoButton.onClick.AddListener(OnVideoClicked);
    }

    private async void Start()
    {
        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("MainEnergyController requires an authenticated player session.");
            startButton.interactable = false;
            return;
        }

        try
        {
            PlayerEnergyState state = await energyGateway.GetEnergyAsync(
                playerId, lifetimeCancellation.Token);
            RefreshEnergy(state);
            DailyRewardStatus adStatus = await energyGateway.GetRewardedAdStatusAsync(
                playerId,
                RewardedAdDailyLimit,
                lifetimeCancellation.Token);
            RefreshAdStatus(adStatus);
            recoveryRefreshCoroutine = StartCoroutine(RecoveryRefreshRoutine(playerId));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            startButton.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (addEnergyButton != null) addEnergyButton.onClick.RemoveListener(ShowAddEnergyPanel);
        if (closeEnergyPanelButton != null) closeEnergyPanelButton.onClick.RemoveListener(HideAddEnergyPanel);
        if (videoButton != null) videoButton.onClick.RemoveListener(OnVideoClicked);
        if (recoveryRefreshCoroutine != null) StopCoroutine(recoveryRefreshCoroutine);
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
    }

    private IEnumerator RecoveryRefreshRoutine(string playerId)
    {
        WaitForSecondsRealtime interval = new WaitForSecondsRealtime(1f);
        while (true)
        {
            yield return interval;
            RefreshRecoveredEnergy(playerId);
        }
    }

    private async void RefreshRecoveredEnergy(string playerId)
    {
        if (refreshInProgress || requestInProgress || transitionRequested) return;
        refreshInProgress = true;
        try
        {
            PlayerEnergyState state = await energyGateway.GetEnergyAsync(
                playerId, lifetimeCancellation.Token);
            RefreshEnergy(state);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            refreshInProgress = false;
        }
    }

    private async void OnStartClicked()
    {
        if (requestInProgress || adInProgress || transitionRequested || addEnergyPanel.activeSelf) return;

        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId) || SceneLoader.Instance == null)
        {
            Debug.LogError("Cannot start the game without a player session and SceneLoader.");
            return;
        }

        requestInProgress = true;
        startButton.interactable = false;

        try
        {
            EnergyConsumeResult result = await energyGateway.ConsumeEnergyAsync(
                playerId,
                LocalPlayerEnergyGateway.GameStartCost,
                Guid.NewGuid().ToString("N"),
                lifetimeCancellation.Token);

            RefreshEnergy(result.State);
            if (!result.Succeeded)
            {
                ShowTip("Not enough");
                return;
            }

            ShowTip(string.Empty);
            transitionRequested = true;
            SceneLoader.Instance.LoadSceneAsync("Game");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            requestInProgress = false;
            if (!transitionRequested && startButton != null)
            {
                startButton.interactable = true;
            }
        }
    }

    private void ShowAddEnergyPanel()
    {
        if (requestInProgress || adInProgress || transitionRequested) return;
        ShowTip(adDailyLimitReached ? "Not enough" : string.Empty);
        addEnergyPanel.SetActive(true);
        startButton.interactable = false;
        UpdateEnergyPanelButtons();
        RefreshAdStatusForOpenPanel();
    }

    private void HideAddEnergyPanel()
    {
        if (adInProgress) return;
        addEnergyPanel.SetActive(false);
        ShowTip(string.Empty);
        if (!requestInProgress && !transitionRequested) startButton.interactable = true;
    }

    private async void RefreshAdStatusForOpenPanel()
    {
        if (adStatusRefreshInProgress) return;
        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId)) return;

        adStatusRefreshInProgress = true;
        try
        {
            DailyRewardStatus status = await energyGateway.GetRewardedAdStatusAsync(
                playerId,
                RewardedAdDailyLimit,
                lifetimeCancellation.Token);
            RefreshAdStatus(status);
            if (status.CanClaim) adDailyLimitReached = false;
            ShowTip(adDailyLimitReached ? "Not enough" : string.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            adStatusRefreshInProgress = false;
        }
    }

    private async void OnVideoClicked()
    {
        if (rewardedAdClaimsUsed >= RewardedAdDailyLimit)
        {
            adDailyLimitReached = true;
            ShowTip("Not enough");
            UpdateEnergyPanelButtons();
            return;
        }

        if (adInProgress || requestInProgress || transitionRequested || currentEnergyState == null ||
            currentEnergyState.Current >= currentEnergyState.Maximum)
        {
            return;
        }

        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("Cannot grant an ad reward without a player session.");
            return;
        }

        adInProgress = true;
        requestInProgress = true;
        SetAdControlsInteractable(false);
        string transactionId = Guid.NewGuid().ToString("N");

        try
        {
            RewardedAdResult result = await rewardedAdService.ShowAsync(
                EnergyRewardPlacement, lifetimeCancellation.Token);
            if (result != RewardedAdResult.Completed) return;

            RewardedAdEnergyClaimResult claim = await energyGateway.ClaimRewardedAdEnergyAsync(
                playerId,
                RewardedAdEnergy,
                RewardedAdDailyLimit,
                transactionId,
                lifetimeCancellation.Token);
            rewardedAdClaimsUsed = claim.ClaimsUsed;
            RefreshEnergy(claim.State);
            if (!claim.Succeeded)
            {
                adDailyLimitReached = claim.LimitReached;
                ShowTip(adDailyLimitReached ? "Not enough" : string.Empty);
                return;
            }

            // Keep one final click available so the fourth attempt can explain
            // the daily limit without opening the ad.
            adDailyLimitReached = false;
            addEnergyPanel.SetActive(false);
            ShowTip(string.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            requestInProgress = false;
            adInProgress = false;
            SetAdControlsInteractable(true);
            if (!transitionRequested && startButton != null)
            {
                startButton.interactable = !addEnergyPanel.activeSelf;
            }
        }
    }

    private bool ResolveView()
    {
        startButton = transform.Find("StartBtn")?.GetComponent<Button>();
        addEnergyButton = transform.Find("EnergyBg/AddBtn")?.GetComponent<Button>();
        addEnergyPanel = transform.Find("AddEnergyPanel")?.gameObject;
        Transform addEnergyRoot = addEnergyPanel != null ? addEnergyPanel.transform : null;
        closeEnergyPanelButton = addEnergyRoot?.Find("BG/CloseBtn")?.GetComponent<Button>();
        videoButton = addEnergyRoot?.Find("BG/VideoBtn")?.GetComponent<Button>();
        rewardAmountText = addEnergyRoot?.Find("BG/Text/Image/REnergy")?.GetComponent<TMP_Text>();
        currentAmountText = transform.Find("EnergyBg/RAmount")?.GetComponent<TMP_Text>();
        maximumAmountText = transform.Find("EnergyBg/MaxAmount")?.GetComponent<TMP_Text>();
        tipText = FindDescendant(addEnergyRoot, "TipText")?.GetComponent<TMP_Text>();
        if (tipText == null)
        {
            tipText = FindDescendant(transform, "TipText")?.GetComponent<TMP_Text>();
        }

        if (tipText == null && currentAmountText != null)
        {
            tipText = CreateTipText(currentAmountText);
        }

        bool complete = startButton != null && addEnergyButton != null && addEnergyPanel != null &&
                        closeEnergyPanelButton != null && videoButton != null && rewardAmountText != null &&
                        currentAmountText != null && maximumAmountText != null && tipText != null;
        if (!complete)
        {
            Debug.LogError("MainEnergyController is missing energy, rewarded-ad, or TipText UI.");
        }
        return complete;
    }

    private TMP_Text CreateTipText(TMP_Text styleSource)
    {
        GameObject tipObject = new GameObject("TipText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tipObject.layer = gameObject.layer;
        RectTransform rect = tipObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -190f);
        rect.sizeDelta = new Vector2(600f, 80f);

        TextMeshProUGUI createdText = tipObject.GetComponent<TextMeshProUGUI>();
        createdText.font = styleSource.font;
        createdText.fontSize = styleSource.fontSize;
        createdText.alignment = TextAlignmentOptions.Center;
        createdText.color = new Color32(220, 65, 65, 255);
        createdText.raycastTarget = false;
        return createdText;
    }

    private void RefreshEnergy(PlayerEnergyState state)
    {
        if (state == null) return;
        currentEnergyState = state;
        currentAmountText.text = state.Current.ToString();
        maximumAmountText.text = "/" + state.Maximum;
        if (!adDailyLimitReached &&
            state.Current >= LocalPlayerEnergyGateway.GameStartCost &&
            tipText.text == "Not enough")
        {
            ShowTip(string.Empty);
        }
        UpdateEnergyPanelButtons();
    }

    private void RefreshAdStatus(DailyRewardStatus status)
    {
        if (status == null) return;
        rewardedAdClaimsUsed = status.ClaimsUsed;
        UpdateEnergyPanelButtons();
    }

    private void SetAdControlsInteractable(bool interactable)
    {
        if (addEnergyButton != null) addEnergyButton.interactable = interactable;
        if (closeEnergyPanelButton != null) closeEnergyPanelButton.interactable = interactable;
        UpdateEnergyPanelButtons();
    }

    private void UpdateEnergyPanelButtons()
    {
        if (videoButton == null) return;
        bool canReceiveEnergy = currentEnergyState != null &&
                                currentEnergyState.Current < currentEnergyState.Maximum;
        bool canShowLimitFeedback = rewardedAdClaimsUsed >= RewardedAdDailyLimit &&
                                    !adDailyLimitReached;
        videoButton.interactable = (canReceiveEnergy || canShowLimitFeedback) &&
                                   !adDailyLimitReached && !adInProgress &&
                                   !requestInProgress;
    }

    private void ShowTip(string message)
    {
        if (tipText != null) tipText.text = message;
    }

    private static string GetPlayerId()
    {
        AuthSession session = AuthSessionStore.Current;
        return session != null ? session.PlayerId : string.Empty;
    }

    private static Transform FindDescendant(Transform parent, string objectName)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, objectName, StringComparison.Ordinal)) return child;
            Transform nested = FindDescendant(child, objectName);
            if (nested != null) return nested;
        }
        return null;
    }
}
