using System;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Items;
using DragonBound.UI;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed class ForgekeepersGiftPanelController : MonoBehaviour, IItemForgePickPort
{
    private const int RuntimeWaitFrames = 300;
    private const float StatusRetryDelaySeconds = 1f;
    private static readonly int[] VerificationRetryDelaysSeconds = { 1, 2, 3, 5, 8 };

    [SerializeField] private GameObject forgePanel;
    [SerializeField] private Button videoRewardButton;
    [SerializeField] private Button closeButton;

    private DragonBoundBootstrap bootstrap;
    private IRewardedAdService rewardedAds;
    private IForgekeepersGiftGateway gateway;
    private CancellationTokenSource lifetimeCancellation;
    private Action<ItemForgePickClaimResult> pendingCompletion;
    private PendingForgekeepersGiftClaim activeClaim;
    private ForgekeepersGiftIntent activeIntent;
    private bool promptOpen;
    private bool operationPending;
    private bool adPlaying;
    private bool verificationPending;
    private bool statusLoaded;
    private bool statusRefreshRequested;
    private bool authorityEquipped;
    private bool authorityCanOpen;
    private bool ownsPause;
    private float timeScaleBeforePause = 1f;
    private float nextStatusRefreshAt;
    private float nextRecoveryAt;

    private void Awake()
    {
        ResolveAuthoredUi();
        if (forgePanel != null) forgePanel.SetActive(false);
        lifetimeCancellation = new CancellationTokenSource();
        bootstrap = FindObjectOfType<DragonBoundBootstrap>();
        bootstrap?.BindPlayerForgePickPort(this);

        try
        {
            IClientServices services = ClientCompositionRoot.Current;
            rewardedAds = services?.RewardedAds;
            gateway = services?.ForgekeepersGift;
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(
                "Forgekeeper's Gift could not resolve client services: " + exception.Message,
                this);
        }

        if (videoRewardButton != null)
        {
            videoRewardButton.onClick.RemoveListener(OnVideoRewardClicked);
            videoRewardButton.onClick.AddListener(OnVideoRewardClicked);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }
        ApplyPanelState();
    }

    private async void Start()
    {
        try
        {
            if (await WaitForRuntimeAsync()) await RefreshAuthorityStateAsync(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Update()
    {
        if (statusRefreshRequested && !operationPending && !promptOpen && !adPlaying)
        {
            statusRefreshRequested = false;
            _ = RefreshAuthorityStateAsync(true);
        }

        if (activeClaim != null &&
            !string.IsNullOrWhiteSpace(activeClaim.GrantedUnitRuntimeId))
        {
            TryMaterializeGrantedReward();
        }

        if (verificationPending && !operationPending &&
            Time.unscaledTime >= nextRecoveryAt)
        {
            nextRecoveryAt = Time.unscaledTime + 5f;
            _ = RecoverPendingClaimAsync(true);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) BeginRateLimitedStatusRefresh(true);
    }

    private void OnDestroy()
    {
        if (videoRewardButton != null)
            videoRewardButton.onClick.RemoveListener(OnVideoRewardClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
        bootstrap?.UnbindPlayerForgePickPort(this);
        pendingCompletion = null;
        promptOpen = false;
        RestoreGameplay();
    }

    public ItemForgePickRequestResult TryBeginForgePickClaim(
        Action<ItemForgePickClaimResult> completed)
    {
        if (completed == null || promptOpen || operationPending || adPlaying)
        {
            return new ItemForgePickRequestResult(
                ItemForgePickRequestKind.Rejected,
                "ForgePickClaimAlreadyPending");
        }

        if (!ResolveRuntime() || forgePanel == null || videoRewardButton == null ||
            closeButton == null || rewardedAds == null || gateway == null ||
            !TryGetRunId(out _))
        {
            return new ItemForgePickRequestResult(
                ItemForgePickRequestKind.TemporarilyUnavailable,
                "ForgePickUiUnavailable");
        }

        if (statusLoaded && !authorityEquipped)
        {
            return new ItemForgePickRequestResult(
                ItemForgePickRequestKind.Rejected,
                "ForgePickItemNotEquipped");
        }

        if (!statusLoaded || !authorityCanOpen || verificationPending)
        {
            BeginRateLimitedStatusRefresh();
            return new ItemForgePickRequestResult(
                ItemForgePickRequestKind.TemporarilyUnavailable,
                "ForgePickAuthorityNotReady");
        }

        if (!bootstrap.TwentyWave.PauseWave())
        {
            return new ItemForgePickRequestResult(
                ItemForgePickRequestKind.TemporarilyUnavailable,
                "GameplayNotRunning");
        }

        promptOpen = true;
        operationPending = true;
        ownsPause = true;
        pendingCompletion = completed;
        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        forgePanel.SetActive(true);
        forgePanel.transform.SetAsLastSibling();
        ApplyPanelState();
        _ = CreateIntentAsync();
        return new ItemForgePickRequestResult(ItemForgePickRequestKind.PromptOpened);
    }

    private async Task CreateIntentAsync()
    {
        if (!TryGetRunId(out string runId))
        {
            operationPending = false;
            FinishClaim(new ItemForgePickClaimResult(
                ItemForgePickClaimKind.Failed,
                "RunUnavailable"));
            return;
        }

        try
        {
            string clientEventId = Guid.NewGuid().ToString("N");
            string intentKey =
                "run." + runId + ".forgekeepers-gift.intent." + clientEventId;
            activeIntent = await gateway.CreateIntentAsync(
                runId,
                clientEventId,
                ResolvePlatform(),
                intentKey,
                lifetimeCancellation.Token);
            EnsureCurrentRun(activeIntent.RunId);
            activeClaim = new PendingForgekeepersGiftClaim
            {
                RunId = runId,
                ClaimId = activeIntent.ClaimId,
                ClientEventId = clientEventId,
                IntentIdempotencyKey = intentKey,
                ClaimIdempotencyKey =
                    "run." + runId + ".forgekeepers-gift.claim." + activeIntent.ClaimId,
                OpportunityIndex = activeIntent.OpportunityIndex
            };
            ForgekeepersGiftClaimStore.SavePending(activeClaim);
            authorityCanOpen = false;
            SynchronizeCooldownFromTimestamp(activeIntent.NextAvailableAt, 90f);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ClientServiceException exception)
        {
            HandleIntentFailure(exception);
            return;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
            FinishClaim(new ItemForgePickClaimResult(
                ItemForgePickClaimKind.Failed,
                "IntentFailed"));
            BeginRateLimitedStatusRefresh(true);
            return;
        }
        finally
        {
            operationPending = false;
            ApplyPanelState();
        }
    }

    private async void OnVideoRewardClicked()
    {
        if (!promptOpen || operationPending || adPlaying || activeClaim == null ||
            activeIntent == null || rewardedAds == null)
        {
            return;
        }

        if (!ResolveRuntime() || !bootstrap.RecruitDestination.HasEmptyBenchSlot())
        {
            TipTextService.Show("Keep one deployment slot empty to receive the Shovel.", 3f);
            return;
        }

        adPlaying = true;
        ApplyPanelState();
        bool adCompleted = false;
        try
        {
            RewardedAdResult result = await rewardedAds.ShowAsync(
                new RewardedAdPlaybackRequest
                {
                    PlacementId = activeIntent.PlacementId,
                    CustomData = activeIntent.AdCustomData,
                    ClaimId = activeIntent.ClaimId
                },
                lifetimeCancellation.Token);
            if (result != RewardedAdResult.Completed)
            {
                ClearActiveClaim();
                FinishClaim(new ItemForgePickClaimResult(
                    result == RewardedAdResult.Skipped
                        ? ItemForgePickClaimKind.Declined
                        : ItemForgePickClaimKind.Failed,
                    result.ToString()));
                BeginRateLimitedStatusRefresh(true);
                return;
            }

            adCompleted = true;
            activeClaim.AdCompleted = true;
            ForgekeepersGiftClaimStore.SavePending(activeClaim);
            operationPending = true;
            ApplyPanelState();
            ForgekeepersGiftClaimResult claim =
                await ClaimWithVerificationRetryAsync(activeClaim);
            if (claim == null)
            {
                verificationPending = true;
                nextRecoveryAt = Time.unscaledTime + 5f;
                TipTextService.Show("Reward will be restored after verification.", 3f);
                FinishClaim(new ItemForgePickClaimResult(
                    ItemForgePickClaimKind.Failed,
                    "VerificationPending"));
                return;
            }

            AcceptGrantedReward(claim);
            FinishClaim(new ItemForgePickClaimResult(ItemForgePickClaimKind.Granted));
            BeginRateLimitedStatusRefresh(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ClientServiceException exception)
        {
            HandleClaimFailure(exception, adCompleted);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            verificationPending = adCompleted;
            if (!adCompleted) ClearActiveClaim();
            TipTextService.Show(
                adCompleted
                    ? "Reward will be restored after reconnecting."
                    : "Rewarded video is temporarily unavailable.",
                3f);
            FinishClaim(new ItemForgePickClaimResult(
                ItemForgePickClaimKind.Failed,
                adCompleted ? "ClaimDeferred" : "AdFailed"));
        }
        finally
        {
            operationPending = false;
            adPlaying = false;
            ApplyPanelState();
        }
    }

    private void OnCloseClicked()
    {
        if (!promptOpen || operationPending || adPlaying) return;
        ClearActiveClaim();
        FinishClaim(new ItemForgePickClaimResult(ItemForgePickClaimKind.Declined, "Closed"));
        BeginRateLimitedStatusRefresh(true);
    }

    private async Task<ForgekeepersGiftClaimResult> ClaimWithVerificationRetryAsync(
        PendingForgekeepersGiftClaim pending)
    {
        for (int attempt = 0; attempt <= VerificationRetryDelaysSeconds.Length; attempt++)
        {
            try
            {
                ForgekeepersGiftClaimResult result = await gateway.ClaimAsync(
                    pending.RunId,
                    pending.ClaimId,
                    pending.ClaimIdempotencyKey,
                    lifetimeCancellation.Token);
                if (result.State == ForgekeepersGiftState.VerificationPending)
                {
                    if (attempt >= VerificationRetryDelaysSeconds.Length) return null;
                    await DelayVerificationRetryAsync(attempt);
                    continue;
                }
                if (result.State != ForgekeepersGiftState.Granted)
                {
                    throw new ClientServiceException(
                        "INVALID_RESPONSE",
                        "Forgekeeper's Gift claim was not granted.");
                }
                return result;
            }
            catch (ClientServiceException exception)
                when (exception.Code == "AD_VERIFICATION_PENDING")
            {
                if (attempt >= VerificationRetryDelaysSeconds.Length) return null;
                await DelayVerificationRetryAsync(attempt);
            }
        }
        return null;
    }

    private Task DelayVerificationRetryAsync(int attempt)
    {
        return Task.Delay(
            TimeSpan.FromSeconds(VerificationRetryDelaysSeconds[attempt]),
            lifetimeCancellation.Token);
    }

    private async Task RecoverPendingClaimAsync(bool quiet)
    {
        if (operationPending || gateway == null || !TryGetRunId(out string runId)) return;
        if (activeClaim == null &&
            !ForgekeepersGiftClaimStore.TryGetPending(runId, out activeClaim))
        {
            verificationPending = false;
            return;
        }
        if (!string.Equals(activeClaim.RunId, runId, StringComparison.Ordinal)) return;
        if (!string.IsNullOrWhiteSpace(activeClaim.GrantedUnitRuntimeId))
        {
            TryMaterializeGrantedReward();
            return;
        }

        operationPending = true;
        ApplyPanelState();
        try
        {
            ForgekeepersGiftClaimResult result =
                await ClaimWithVerificationRetryAsync(activeClaim);
            if (result == null)
            {
                verificationPending = true;
                nextRecoveryAt = Time.unscaledTime + 5f;
                return;
            }
            AcceptGrantedReward(result);
            verificationPending = false;
            if (!quiet) TipTextService.Show("Shovel reward restored.", 3f);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ClientServiceException exception)
        {
            if (exception.Code == "AD_VERIFICATION_PENDING")
            {
                verificationPending = true;
                nextRecoveryAt = Time.unscaledTime + 5f;
            }
            else if (exception.Code == "AD_VERIFICATION_FAILED" ||
                     exception.Code == "AD_INTENT_EXPIRED")
            {
                ClearActiveClaim();
                verificationPending = false;
            }
            else if (!quiet)
            {
                TipTextService.Show("Reward will be restored after reconnecting.", 3f);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            verificationPending = true;
            nextRecoveryAt = Time.unscaledTime + 5f;
        }
        finally
        {
            operationPending = false;
            ApplyPanelState();
        }
    }

    private async Task RefreshAuthorityStateAsync(bool quiet)
    {
        if (operationPending || gateway == null || lifetimeCancellation == null) return;
        if (!ResolveRuntime() || !TryGetRunId(out string runId)) return;

        operationPending = true;
        statusRefreshRequested = false;
        ApplyPanelState();
        try
        {
            ForgekeepersGiftStatus status = await gateway.GetStatusAsync(
                runId,
                lifetimeCancellation.Token);
            EnsureCurrentRun(status.RunId);
            statusLoaded = true;
            authorityEquipped = status.Equipped;
            authorityCanOpen = status.Equipped && status.CanOpen &&
                               status.State == ForgekeepersGiftState.Available;
            verificationPending = status.State == ForgekeepersGiftState.VerificationPending;
            SynchronizeCooldown(status.RemainingSeconds);
            nextStatusRefreshAt = Time.unscaledTime +
                                  Mathf.Max(StatusRetryDelaySeconds, status.RemainingSeconds);

            if (ForgekeepersGiftClaimStore.TryGetPending(runId, out activeClaim))
            {
                if (!string.IsNullOrWhiteSpace(activeClaim.GrantedUnitRuntimeId))
                {
                    TryMaterializeGrantedReward();
                }
                else if (status.State == ForgekeepersGiftState.Granted &&
                         !string.IsNullOrWhiteSpace(status.ClaimId))
                {
                    AcceptGrantedReward(
                        status.RunId,
                        status.ClaimId,
                        status.State,
                        status.RewardCount,
                        status.UnitRuntimeIds,
                        status.OpportunityIndex);
                }
                else if (activeClaim.AdCompleted ||
                         status.State == ForgekeepersGiftState.VerificationPending)
                {
                    verificationPending = true;
                }
                else
                {
                    ClearActiveClaim();
                    verificationPending = false;
                }
            }
            else if (status.State == ForgekeepersGiftState.VerificationPending &&
                     !string.IsNullOrWhiteSpace(status.ClaimId))
            {
                activeClaim = new PendingForgekeepersGiftClaim
                {
                    RunId = runId,
                    ClaimId = status.ClaimId,
                    ClaimIdempotencyKey =
                        "run." + runId + ".forgekeepers-gift.claim." + status.ClaimId,
                    OpportunityIndex = status.OpportunityIndex
                };
                ForgekeepersGiftClaimStore.SavePending(activeClaim);
                verificationPending = true;
            }
            else if (status.State == ForgekeepersGiftState.Granted &&
                     !string.IsNullOrWhiteSpace(status.ClaimId) &&
                     !ForgekeepersGiftClaimStore.IsHandled(runId, status.ClaimId))
            {
                AcceptGrantedReward(
                    status.RunId,
                    status.ClaimId,
                    status.State,
                    status.RewardCount,
                    status.UnitRuntimeIds,
                    status.OpportunityIndex);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ClientServiceException exception)
        {
            statusLoaded = exception.Code == "ITEM_NOT_EQUIPPED";
            authorityEquipped = false;
            authorityCanOpen = false;
            if (!quiet && exception.Code != "ITEM_NOT_EQUIPPED")
                TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            authorityCanOpen = false;
            if (!quiet)
                TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
        }
        finally
        {
            operationPending = false;
            ApplyPanelState();
        }

        if (verificationPending) _ = RecoverPendingClaimAsync(quiet);
    }

    private void AcceptGrantedReward(ForgekeepersGiftClaimResult result)
    {
        AcceptGrantedReward(
            result.RunId,
            result.ClaimId,
            result.State,
            result.RewardCount,
            result.UnitRuntimeIds,
            result.OpportunityIndex);
    }

    private void AcceptGrantedReward(
        string runId,
        string claimId,
        ForgekeepersGiftState state,
        int rewardCount,
        System.Collections.Generic.IList<string> unitRuntimeIds,
        int opportunityIndex)
    {
        EnsureCurrentRun(runId);
        if (state != ForgekeepersGiftState.Granted ||
            string.IsNullOrWhiteSpace(claimId) || rewardCount != 1 ||
            unitRuntimeIds == null || unitRuntimeIds.Count != 1 ||
            string.IsNullOrWhiteSpace(unitRuntimeIds[0]))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Forgekeeper's Gift requires exactly one server shovel ID.");
        }

        if (ForgekeepersGiftClaimStore.IsHandled(runId, claimId))
        {
            ForgekeepersGiftClaimStore.ClearPending(runId);
            activeClaim = null;
            return;
        }

        activeClaim = new PendingForgekeepersGiftClaim
        {
            RunId = runId,
            ClaimId = claimId,
            ClaimIdempotencyKey =
                "run." + runId + ".forgekeepers-gift.claim." + claimId,
            OpportunityIndex = opportunityIndex,
            GrantedUnitRuntimeId = unitRuntimeIds[0]
        };
        ForgekeepersGiftClaimStore.SavePending(activeClaim);
        verificationPending = false;
        authorityCanOpen = false;
        TryMaterializeGrantedReward();
    }

    private bool TryMaterializeGrantedReward()
    {
        if (activeClaim == null ||
            string.IsNullOrWhiteSpace(activeClaim.GrantedUnitRuntimeId) ||
            !ResolveRuntime())
        {
            return false;
        }

        string runtimeId = activeClaim.GrantedUnitRuntimeId;
        bool exists = bootstrap.RecruitDestination.TryGetCard(runtimeId, out _);
        if (!exists && !bootstrap.RecruitDestination.HasEmptyBenchSlot()) return false;
        if (!exists &&
            !bootstrap.RecruitDestination.TryAddRewardShovelToFirstEmptyBench(runtimeId) &&
            !bootstrap.RecruitDestination.TryGetCard(runtimeId, out _))
        {
            return false;
        }

        bootstrap.BoardView?.RefreshUnits();
        ForgekeepersGiftClaimStore.MarkHandled(activeClaim.RunId, activeClaim.ClaimId);
        ForgekeepersGiftClaimStore.ClearPending(activeClaim.RunId);
        activeClaim = null;
        return true;
    }

    private void HandleIntentFailure(ClientServiceException exception)
    {
        bool terminal = exception.Code == "ITEM_NOT_EQUIPPED" ||
                        exception.Code == "RUN_NOT_ACTIVE" ||
                        exception.Code == "RUN_NOT_FOUND" ||
                        exception.Code == "RUN_NOT_OWNED";
        if (terminal)
        {
            authorityEquipped = false;
            authorityCanOpen = false;
        }
        if (exception.Code != "REWARD_NOT_READY" && !terminal)
            TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
        FinishClaim(new ItemForgePickClaimResult(
            ItemForgePickClaimKind.Failed,
            exception.Code));
        BeginRateLimitedStatusRefresh(true);
    }

    private void HandleClaimFailure(ClientServiceException exception, bool adCompleted)
    {
        if (exception.Code == "AD_VERIFICATION_PENDING")
        {
            verificationPending = true;
            nextRecoveryAt = Time.unscaledTime + 5f;
            TipTextService.Show("Reward will be restored after verification.", 3f);
        }
        else if (exception.Code == "AD_VERIFICATION_FAILED" ||
                 exception.Code == "AD_INTENT_EXPIRED")
        {
            ClearActiveClaim();
            verificationPending = false;
            TipTextService.Show("Reward verification failed.", 3f);
        }
        else
        {
            verificationPending = adCompleted;
            if (!adCompleted) ClearActiveClaim();
            TipTextService.Show(
                adCompleted
                    ? "Reward will be restored after reconnecting."
                    : "Rewarded video is temporarily unavailable.",
                3f);
        }
        FinishClaim(new ItemForgePickClaimResult(
            ItemForgePickClaimKind.Failed,
            exception.Code));
    }

    private void ClearActiveClaim()
    {
        if (activeClaim != null)
            ForgekeepersGiftClaimStore.ClearPending(activeClaim.RunId);
        activeClaim = null;
        activeIntent = null;
    }

    private void FinishClaim(ItemForgePickClaimResult result)
    {
        if (!promptOpen) return;
        promptOpen = false;
        adPlaying = false;
        activeIntent = null;
        if (forgePanel != null) forgePanel.SetActive(false);
        RestoreGameplay();
        Action<ItemForgePickClaimResult> completion = pendingCompletion;
        pendingCompletion = null;
        completion?.Invoke(result);
        ApplyPanelState();
    }

    private void RestoreGameplay()
    {
        if (ownsPause && bootstrap?.TwentyWave != null && bootstrap.Match != null &&
            bootstrap.Match.State == MatchState.Paused)
        {
            bootstrap.TwentyWave.ResumeWave();
        }
        if (ownsPause) Time.timeScale = timeScaleBeforePause;
        ownsPause = false;
    }

    private void BeginRateLimitedStatusRefresh(bool force = false)
    {
        if (!force && Time.unscaledTime < nextStatusRefreshAt) return;
        nextStatusRefreshAt = Time.unscaledTime + StatusRetryDelaySeconds;
        if (operationPending || adPlaying || promptOpen)
        {
            statusRefreshRequested = true;
            return;
        }
        _ = RefreshAuthorityStateAsync(true);
    }

    private void SynchronizeCooldown(float remainingSeconds)
    {
        if (!ResolveRuntime()) return;
        bootstrap.TwentyWave.PlayerItems?.SynchronizeForgekeepersGiftCooldown(
            Mathf.Max(0f, remainingSeconds));
    }

    private void SynchronizeCooldownFromTimestamp(string timestamp, float fallbackSeconds)
    {
        if (DateTimeOffset.TryParse(timestamp, out DateTimeOffset availableAt))
        {
            SynchronizeCooldown((float)Math.Max(
                0d,
                (availableAt - DateTimeOffset.UtcNow).TotalSeconds));
            return;
        }
        SynchronizeCooldown(fallbackSeconds);
    }

    private async Task<bool> WaitForRuntimeAsync()
    {
        for (int frame = 0; frame < RuntimeWaitFrames; frame++)
        {
            lifetimeCancellation.Token.ThrowIfCancellationRequested();
            if (ResolveRuntime() && TryGetRunId(out _)) return true;
            await Task.Yield();
        }
        return false;
    }

    private bool ResolveRuntime()
    {
        if (bootstrap == null)
        {
            bootstrap = FindObjectOfType<DragonBoundBootstrap>();
            bootstrap?.BindPlayerForgePickPort(this);
        }
        return bootstrap != null && bootstrap.IsInitialized &&
               bootstrap.TwentyWave != null && bootstrap.Match != null &&
               bootstrap.PlayerBoard != null && bootstrap.RecruitDestination != null;
    }

    private bool TryGetRunId(out string runId)
    {
        runId = bootstrap?.GameplayRunId;
        return !string.IsNullOrWhiteSpace(runId) && Guid.TryParse(runId, out _);
    }

    private void EnsureCurrentRun(string responseRunId)
    {
        if (!TryGetRunId(out string runId) ||
            !string.Equals(responseRunId, runId, StringComparison.Ordinal))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Forgekeeper's Gift response belongs to another run.");
        }
    }

    private void ApplyPanelState()
    {
        if (videoRewardButton != null)
        {
            videoRewardButton.interactable = promptOpen && !operationPending &&
                                             !adPlaying && activeClaim != null &&
                                             activeIntent != null;
        }
        if (closeButton != null)
            closeButton.interactable = promptOpen && !operationPending && !adPlaying;
    }

    private void ResolveAuthoredUi()
    {
        if (forgePanel == null)
        {
            Transform panel = transform.Find("ForgePanel");
            if (panel != null) forgePanel = panel.gameObject;
        }
        Transform root = forgePanel != null ? forgePanel.transform : null;
        if (videoRewardButton == null)
            videoRewardButton = root?.Find("Bg/VideoReward")?.GetComponent<Button>();
        if (closeButton == null)
            closeButton = root?.Find("Bg/CloseBtn")?.GetComponent<Button>();
    }

    private static string ResolvePlatform()
    {
#if UNITY_ANDROID
        return "android";
#elif UNITY_IOS
        return "ios";
#else
        return "editor";
#endif
    }
}
