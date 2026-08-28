using System;
using System.Collections;
using System.Threading;
using DragonBound.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the Main scene energy display and guarded Start button flow.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainEnergyController : MonoBehaviour
{
    private const string GameplaySceneName = "Greybox_Main";
    private const int RewardedAdEnergy = 10;
    private const int RewardedAdDailyLimit = 3;
    private const string EnergyRewardPlacement = "energy_reward";
    private const int ShareRewardEnergy = 5;
    private const int ShareDailyLimit = 4;
    private const string EnergySharePlacement = "energy_share";

    private GameObject addEnergyPanel;
    private Button startButton;
    private Button addEnergyButton;
    private Button closeEnergyPanelButton;
    private Button videoButton;
    private Button shareButton;
    private TMP_Text currentAmountText;
    private TMP_Text maximumAmountText;
    private TMP_Text mainTipText;
    private TMP_Text tipText;
    private TMP_Text rewardAmountText;
    private IPlayerEnergyGateway energyGateway;
    private IPlayerRankGateway rankGateway;
    private IAuthSessionStore authSessionStore;
    private IRewardedAdService rewardedAdService;
    private IShareService shareService;
    private CancellationTokenSource lifetimeCancellation;
    private Coroutine recoveryRefreshCoroutine;
    private Coroutine tipHideCoroutine;
    private PlayerEnergyState currentEnergyState;
    private bool requestInProgress;
    private bool refreshInProgress;
    private bool adInProgress;
    private bool adStatusRefreshInProgress;
    private bool adDailyLimitReached;
    private bool shareInProgress;
    private bool shareStatusRefreshInProgress;
    private bool shareDailyLimitReached;
    private bool transitionRequested;
    private int rewardedAdClaimsUsed;
    private int sharesUsed;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        energyGateway = services.Energy;
        rankGateway = services.Rank;
        authSessionStore = services.AuthSession;
        rewardedAdService = services.RewardedAds;
        shareService = services.Share;
        lifetimeCancellation = new CancellationTokenSource();

        if (!ResolveView())
        {
            enabled = false;
            return;
        }

        mainTipText.text = string.Empty;
        ShowTip(string.Empty);
        rewardAmountText.text = "+" + RewardedAdEnergy;
        maximumAmountText.text = "/" + LocalPlayerEnergyGateway.MaximumEnergy;
        addEnergyPanel.SetActive(false);
        startButton.onClick.AddListener(OnStartClicked);
        addEnergyButton.onClick.AddListener(ShowAddEnergyPanel);
        closeEnergyPanelButton.onClick.AddListener(HideAddEnergyPanel);
        videoButton.onClick.AddListener(OnVideoClicked);
        shareButton.onClick.AddListener(OnShareClicked);
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
            DailyShareStatus shareStatus = await energyGateway.GetShareStatusAsync(
                playerId,
                ShareDailyLimit,
                lifetimeCancellation.Token);
            RefreshShareStatus(shareStatus);
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
        if (shareButton != null) shareButton.onClick.RemoveListener(OnShareClicked);
        if (recoveryRefreshCoroutine != null) StopCoroutine(recoveryRefreshCoroutine);
        if (tipHideCoroutine != null) StopCoroutine(tipHideCoroutine);
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
        if (requestInProgress || adInProgress || shareInProgress ||
            transitionRequested || addEnergyPanel.activeSelf) return;

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
            PlayerRankState rank = await rankGateway.GetRankAsync(
                playerId,
                lifetimeCancellation.Token);
            int rankLevel = rank != null ? rank.Level : 1;
            string launchNonce = GameplayLaunchContext.GetOrCreateNonce(playerId, rankLevel);
            EnergyConsumeResult result = await energyGateway.ConsumeEnergyAsync(
                playerId,
                LocalPlayerEnergyGateway.GameStartCost,
                launchNonce,
                lifetimeCancellation.Token);

            RefreshEnergy(result.State);
            if (!result.Succeeded)
            {
                GameplayLaunchContext.Complete(launchNonce);
                ShowMainTip("Not enough");
                return;
            }

            ShowMainTip(string.Empty);
            transitionRequested = true;
            SceneLoader.Instance.LoadSceneAsync(GameplaySceneName);
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
        if (requestInProgress || adInProgress || shareInProgress || transitionRequested) return;
        ShowMainTip(string.Empty);
        ShowTip(string.Empty);
        addEnergyPanel.SetActive(true);
        startButton.interactable = false;
        UpdateEnergyPanelButtons();
        RefreshAdStatusForOpenPanel();
        RefreshShareStatusForOpenPanel();
    }

    private void HideAddEnergyPanel()
    {
        if (adInProgress || shareInProgress) return;
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

    private async void RefreshShareStatusForOpenPanel()
    {
        if (shareStatusRefreshInProgress) return;
        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId)) return;

        shareStatusRefreshInProgress = true;
        try
        {
            DailyShareStatus status = await energyGateway.GetShareStatusAsync(
                playerId,
                ShareDailyLimit,
                lifetimeCancellation.Token);
            RefreshShareStatus(status);
            if (status.CanShare) shareDailyLimitReached = false;
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
            shareStatusRefreshInProgress = false;
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

        if (adInProgress || shareInProgress || requestInProgress || transitionRequested || currentEnergyState == null ||
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
        SetRewardControlsInteractable(false);
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
            SetRewardControlsInteractable(true);
            if (!transitionRequested && startButton != null)
            {
                startButton.interactable = !addEnergyPanel.activeSelf;
            }
        }
    }

    private async void OnShareClicked()
    {
        if (sharesUsed >= ShareDailyLimit)
        {
            shareDailyLimitReached = true;
            ShowTip("Not enough");
            UpdateEnergyPanelButtons();
            return;
        }

        if (shareInProgress || adInProgress || requestInProgress || transitionRequested ||
            currentEnergyState == null || currentEnergyState.Current >= currentEnergyState.Maximum)
        {
            return;
        }

        string playerId = GetPlayerId();
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("Cannot grant a share reward without a player session.");
            return;
        }

        shareInProgress = true;
        requestInProgress = true;
        SetRewardControlsInteractable(false);
        string transactionId = Guid.NewGuid().ToString("N");

        try
        {
            ShareResult result = await shareService.ShareAsync(
                new ShareRequest
                {
                    PlacementId = EnergySharePlacement,
                    Message = "Play DragonBound with me!"
                },
                lifetimeCancellation.Token);
            if (result != ShareResult.Completed) return;

            ShareEnergyClaimResult claim = await energyGateway.ClaimShareEnergyAsync(
                playerId,
                ShareRewardEnergy,
                ShareDailyLimit,
                transactionId,
                lifetimeCancellation.Token);
            sharesUsed = claim.SharesUsed;
            RefreshEnergy(claim.State);
            if (!claim.Succeeded)
            {
                shareDailyLimitReached = claim.LimitReached;
                ShowTip(shareDailyLimitReached ? "Not enough" : string.Empty);
                return;
            }

            // Keep one final click available so the fifth attempt can explain
            // the daily limit and then disable the button.
            shareDailyLimitReached = false;
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
            shareInProgress = false;
            SetRewardControlsInteractable(true);
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
        shareButton = addEnergyRoot?.Find("BG/ShareBtn")?.GetComponent<Button>();
        rewardAmountText = addEnergyRoot?.Find("BG/Text/Image/REnergy")?.GetComponent<TMP_Text>();
        currentAmountText = transform.Find("EnergyBg/RAmount")?.GetComponent<TMP_Text>();
        maximumAmountText = transform.Find("EnergyBg/MaxAmount")?.GetComponent<TMP_Text>();
        mainTipText = transform.Find("TipText")?.GetComponent<TMP_Text>();
        tipText = FindDescendant(addEnergyRoot, "TipText")?.GetComponent<TMP_Text>();

        if (mainTipText == null && currentAmountText != null)
        {
            mainTipText = CreateTipText(currentAmountText);
        }

        bool complete = startButton != null && addEnergyButton != null && addEnergyPanel != null &&
                        closeEnergyPanelButton != null && videoButton != null && shareButton != null &&
                        rewardAmountText != null &&
                        currentAmountText != null && maximumAmountText != null &&
                        mainTipText != null && tipText != null;
        if (!complete)
        {
            Debug.LogError("MainEnergyController is missing energy, reward, share, or TipText UI.");
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
        if (state.Current >= LocalPlayerEnergyGateway.GameStartCost &&
            mainTipText.text == "Not enough")
        {
            ShowMainTip(string.Empty);
        }
        UpdateEnergyPanelButtons();
    }

    private void RefreshAdStatus(DailyRewardStatus status)
    {
        if (status == null) return;
        rewardedAdClaimsUsed = status.ClaimsUsed;
        adDailyLimitReached = status.LimitFeedbackConsumed;
        UpdateEnergyPanelButtons();
    }

    private void RefreshShareStatus(DailyShareStatus status)
    {
        if (status == null) return;
        sharesUsed = status.SharesUsed;
        shareDailyLimitReached = status.LimitFeedbackConsumed;
        UpdateEnergyPanelButtons();
    }

    private void SetRewardControlsInteractable(bool interactable)
    {
        if (addEnergyButton != null) addEnergyButton.interactable = interactable;
        if (closeEnergyPanelButton != null) closeEnergyPanelButton.interactable = interactable;
        UpdateEnergyPanelButtons();
    }

    private void UpdateEnergyPanelButtons()
    {
        if (videoButton == null || shareButton == null) return;
        bool canReceiveEnergy = currentEnergyState != null &&
                                currentEnergyState.Current < currentEnergyState.Maximum;
        bool canShowAdLimitFeedback = rewardedAdClaimsUsed >= RewardedAdDailyLimit;
        bool canShowShareLimitFeedback = sharesUsed >= ShareDailyLimit;
        bool rewardRequestIdle = !adInProgress && !shareInProgress && !requestInProgress;
        videoButton.interactable = (canReceiveEnergy || canShowAdLimitFeedback) &&
                                   !adInProgress && rewardRequestIdle;
        shareButton.interactable = (canReceiveEnergy || canShowShareLimitFeedback) &&
                                   rewardRequestIdle;
    }

    private void ShowTip(string message)
    {
        if (tipText == null) return;
        if (tipHideCoroutine != null)
        {
            StopCoroutine(tipHideCoroutine);
            tipHideCoroutine = null;
        }

        if (string.IsNullOrEmpty(message))
        {
            tipText.text = string.Empty;
            tipText.gameObject.SetActive(false);
            return;
        }

        tipText.text = message;
        tipText.gameObject.SetActive(true);
        tipHideCoroutine = StartCoroutine(HideTipAfterDelay());
    }

    private IEnumerator HideTipAfterDelay()
    {
        yield return new WaitForSecondsRealtime(3f);
        tipHideCoroutine = null;
        if (tipText == null) yield break;
        tipText.text = string.Empty;
        tipText.gameObject.SetActive(false);
    }

    private void ShowMainTip(string message)
    {
        if (mainTipText != null) mainTipText.text = message;
    }

    private string GetPlayerId()
    {
        AuthSession session = authSessionStore.Current;
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
