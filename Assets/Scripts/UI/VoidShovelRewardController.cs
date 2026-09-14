using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Grid;
using DragonBound.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Presents the server-authoritative, once-per-run Forge Pick rewarded-ad flow.
/// The server owns eligibility, ad verification, idempotency and stable reward IDs;
/// this component only pauses gameplay and materializes accepted rewards on the bench.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidShovelRewardController : MonoBehaviour
{
    private const string GameplaySceneName = "Greybox_Main";
    private const int RewardShovelCount = 2;
    private const int RuntimeWaitFrames = 300;
    private static readonly int[] VerificationRetryDelaysSeconds = { 1, 2, 3, 5, 8 };

    private readonly List<string> pendingGrantedUnitIds = new List<string>();
    private Button rewardButton;
    private DragonBoundBootstrap bootstrap;
    private IRewardedAdService rewardedAds;
    private IRunForgePickGateway runForgePick;
    private CancellationTokenSource lifetimeCancellation;
    private bool requestPending;
    private bool rewardClaimed;
    private bool verificationPending;
    private bool statusLoaded;
    private bool authorityCanClaim;
    private bool pauseApplied;
    private bool ownsWavePause;
    private float timeScaleBeforeAd;
    private float nextPlacementRetryAt;

    public bool RewardClaimed => rewardClaimed;
    public bool RequestPending => requestPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, GameplaySceneName, StringComparison.Ordinal)) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform buttonTransform = FindDescendant(root.transform, "VoidShovel");
            if (buttonTransform == null) continue;
            if (buttonTransform.GetComponent<VoidShovelRewardController>() == null)
                buttonTransform.gameObject.AddComponent<VoidShovelRewardController>();
            return;
        }

        Debug.LogError(
            "Greybox_Main requires ART_ScreenBackground/VoidShovel with a Button component.");
    }

    private void Awake()
    {
        rewardButton = GetComponent<Button>();
        bootstrap = FindObjectOfType<DragonBoundBootstrap>();
        lifetimeCancellation = new CancellationTokenSource();
        try
        {
            IClientServices services = ClientCompositionRoot.Current;
            rewardedAds = services.RewardedAds;
            runForgePick = services.RunForgePick;
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError("VoidShovel could not resolve client services: " + exception.Message, this);
        }

        if (rewardButton == null)
        {
            Debug.LogError("VoidShovel requires a Button component.", this);
            enabled = false;
            return;
        }

        rewardButton.onClick.RemoveListener(OnRewardButtonClicked);
        rewardButton.onClick.AddListener(OnRewardButtonClicked);
        ApplyButtonState();
    }

    private async void Start()
    {
        await RefreshAuthorityStateAsync(false);
    }

    private void Update()
    {
        if (!rewardClaimed || pendingGrantedUnitIds.Count == 0 ||
            Time.unscaledTime < nextPlacementRetryAt)
        {
            return;
        }

        nextPlacementRetryAt = Time.unscaledTime + 0.5f;
        TryMaterializeGrantedReward();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && (verificationPending || !statusLoaded) && !requestPending)
            _ = RefreshAuthorityStateAsync(true);
    }

    private void OnDestroy()
    {
        if (rewardButton != null)
            rewardButton.onClick.RemoveListener(OnRewardButtonClicked);

        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
        RestoreGameplayAfterAd();
    }

    private async void OnRewardButtonClicked()
    {
        if (!statusLoaded || requestPending || rewardClaimed || verificationPending) return;
        if (!TryGetAuthoritativeRunId(out string runId) ||
            rewardedAds == null || runForgePick == null)
        {
            TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
            return;
        }
        if (CountEmptyBenchSlots() < RewardShovelCount)
        {
            TipTextService.Show("Two empty recruit slots are required.", 3f);
            return;
        }

        requestPending = true;
        ApplyButtonState();
        PauseGameplayForAd();
        bool adCompleted = false;
        try
        {
            string clientEventId = Guid.NewGuid().ToString("N");
            string intentKey = "run." + runId + ".forge-pick.intent." + clientEventId;
            RunForgePickIntent intent = await runForgePick.CreateIntentAsync(
                runId,
                clientEventId,
                ResolvePlatform(),
                intentKey,
                lifetimeCancellation.Token);
            if (intent.AlreadyClaimed)
            {
                await RecoverGrantedStatusAsync(runId);
                return;
            }

            string claimKey = "run." + runId + ".forge-pick.claim." + intent.ClaimId;
            var pending = new PendingRunForgePickClaim
            {
                RunId = runId,
                ClaimId = intent.ClaimId,
                ClientEventId = clientEventId,
                IntentIdempotencyKey = intentKey,
                ClaimIdempotencyKey = claimKey
            };
            PendingRunForgePickClaimStore.Save(pending);

            RewardedAdResult adResult = await rewardedAds.ShowAsync(
                new RewardedAdPlaybackRequest
                {
                    PlacementId = intent.PlacementId,
                    CustomData = intent.AdCustomData,
                    ClaimId = intent.ClaimId
                },
                lifetimeCancellation.Token);
            if (adResult != RewardedAdResult.Completed)
            {
                PendingRunForgePickClaimStore.Clear(runId);
                return;
            }

            adCompleted = true;
            RunForgePickClaimResult claim = await ClaimWithVerificationRetryAsync(pending);
            if (claim == null)
            {
                verificationPending = true;
                TipTextService.Show("Reward will be restored after reconnecting.", 3f);
                return;
            }
            AcceptGrantedReward(runId, claim.State, claim.RewardCount, claim.UnitRuntimeIds);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ClientServiceException exception)
        {
            if (exception.Code == "RUN_REWARD_ALREADY_CLAIMED")
            {
                await RecoverGrantedStatusAsync(runId);
            }
            else if (exception.Code == "AD_VERIFICATION_PENDING")
            {
                verificationPending = true;
                TipTextService.Show("Reward will be restored after reconnecting.", 3f);
            }
            else if (exception.Code == "AD_VERIFICATION_FAILED" ||
                     exception.Code == "AD_INTENT_EXPIRED")
            {
                PendingRunForgePickClaimStore.Clear(runId);
                verificationPending = false;
                TipTextService.Show("Reward verification failed.", 3f);
            }
            else if (IsTerminalRunRewardError(exception.Code))
            {
                PendingRunForgePickClaimStore.Clear(runId);
                verificationPending = false;
                authorityCanClaim = false;
                TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
            }
            else
            {
                verificationPending = adCompleted;
                if (!adCompleted) PendingRunForgePickClaimStore.Clear(runId);
                TipTextService.Show(
                    adCompleted
                        ? "Reward will be restored after reconnecting."
                        : "Rewarded video is temporarily unavailable.",
                    3f);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (!adCompleted) PendingRunForgePickClaimStore.Clear(runId);
            TipTextService.Show(
                adCompleted
                    ? "Reward will be restored after reconnecting."
                    : "Rewarded video is temporarily unavailable.",
                3f);
            verificationPending = adCompleted;
        }
        finally
        {
            RestoreGameplayAfterAd();
            requestPending = false;
            ApplyButtonState();
        }
    }

    private async Task RefreshAuthorityStateAsync(bool quiet)
    {
        if (requestPending || lifetimeCancellation == null) return;
        requestPending = true;
        statusLoaded = false;
        ApplyButtonState();
        try
        {
            if (!await WaitForRuntimeAsync() || !TryGetAuthoritativeRunId(out string runId) ||
                runForgePick == null)
            {
                if (!quiet)
                    TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
                return;
            }

            RunForgePickStatus status = await runForgePick.GetStatusAsync(
                runId,
                lifetimeCancellation.Token);
            if (status.State == RunForgePickState.Granted)
            {
                AcceptGrantedReward(runId, status.State, status.RewardCount, status.UnitRuntimeIds);
                return;
            }

            rewardClaimed = false;
            verificationPending = status.State == RunForgePickState.VerificationPending;
            authorityCanClaim = status.State == RunForgePickState.Available && status.CanClaim;
            if (PendingRunForgePickClaimStore.TryGet(runId, out PendingRunForgePickClaim pending))
            {
                RunForgePickClaimResult claim = await ClaimWithVerificationRetryAsync(pending);
                if (claim != null)
                {
                    AcceptGrantedReward(runId, claim.State, claim.RewardCount, claim.UnitRuntimeIds);
                    return;
                }
                verificationPending = true;
            }
            statusLoaded = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ClientServiceException exception)
        {
            if (exception.Code == "AD_VERIFICATION_PENDING")
                verificationPending = true;
            else
            {
                authorityCanClaim = false;
                if (!quiet)
                    TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (!quiet)
                TipTextService.Show("Rewarded video is temporarily unavailable.", 3f);
        }
        finally
        {
            requestPending = false;
            ApplyButtonState();
        }
    }

    private async Task<RunForgePickClaimResult> ClaimWithVerificationRetryAsync(
        PendingRunForgePickClaim pending)
    {
        for (int attempt = 0; attempt <= VerificationRetryDelaysSeconds.Length; attempt++)
        {
            try
            {
                RunForgePickClaimResult result = await runForgePick.ClaimAsync(
                    pending.RunId,
                    pending.ClaimId,
                    pending.ClaimIdempotencyKey,
                    lifetimeCancellation.Token);
                if (result.State == RunForgePickState.VerificationPending)
                {
                    if (attempt >= VerificationRetryDelaysSeconds.Length) return null;
                    await Task.Delay(
                        TimeSpan.FromSeconds(VerificationRetryDelaysSeconds[attempt]),
                        lifetimeCancellation.Token);
                    continue;
                }
                if (result.State != RunForgePickState.Granted)
                    throw new ClientServiceException(
                        "INVALID_RESPONSE",
                        "Forge Pick claim did not return a granted reward.");
                return result;
            }
            catch (ClientServiceException exception)
                when (exception.Code == "AD_VERIFICATION_PENDING")
            {
                if (attempt >= VerificationRetryDelaysSeconds.Length) return null;
                await Task.Delay(
                    TimeSpan.FromSeconds(VerificationRetryDelaysSeconds[attempt]),
                    lifetimeCancellation.Token);
            }
        }
        return null;
    }

    private async Task RecoverGrantedStatusAsync(string runId)
    {
        RunForgePickStatus status = await runForgePick.GetStatusAsync(
            runId,
            lifetimeCancellation.Token);
        if (status.State != RunForgePickState.Granted)
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Already-claimed Forge Pick status did not contain its reward snapshot.");
        }
        AcceptGrantedReward(runId, status.State, status.RewardCount, status.UnitRuntimeIds);
    }

    private void AcceptGrantedReward(
        string runId,
        RunForgePickState state,
        int rewardCount,
        IList<string> unitRuntimeIds)
    {
        if (state != RunForgePickState.Granted || rewardCount != RewardShovelCount ||
            unitRuntimeIds == null || unitRuntimeIds.Count != RewardShovelCount ||
            string.IsNullOrWhiteSpace(unitRuntimeIds[0]) ||
            string.IsNullOrWhiteSpace(unitRuntimeIds[1]) ||
            string.Equals(unitRuntimeIds[0], unitRuntimeIds[1], StringComparison.Ordinal))
        {
            throw new ClientServiceException(
                "INVALID_RESPONSE",
                "Forge Pick grant requires exactly two distinct server unit IDs.");
        }

        rewardClaimed = true;
        verificationPending = false;
        authorityCanClaim = false;
        statusLoaded = true;
        pendingGrantedUnitIds.Clear();
        pendingGrantedUnitIds.Add(unitRuntimeIds[0]);
        pendingGrantedUnitIds.Add(unitRuntimeIds[1]);
        PendingRunForgePickClaimStore.Clear(runId);
        TryMaterializeGrantedReward();
        ApplyButtonState();
    }

    private bool TryMaterializeGrantedReward()
    {
        if (!ResolveRuntime() || pendingGrantedUnitIds.Count == 0) return false;

        var missing = new List<string>();
        foreach (string runtimeId in pendingGrantedUnitIds)
        {
            if (!bootstrap.RecruitDestination.TryGetCard(runtimeId, out _))
                missing.Add(runtimeId);
        }
        if (missing.Count == 0)
        {
            pendingGrantedUnitIds.Clear();
            return true;
        }
        if (CountEmptyBenchSlots() < missing.Count) return false;

        foreach (string runtimeId in missing)
        {
            if (!bootstrap.RecruitDestination.TryAddRewardShovelToFirstEmptyBench(runtimeId) &&
                !bootstrap.RecruitDestination.TryGetCard(runtimeId, out _))
            {
                return false;
            }
        }

        bootstrap.BoardView?.RefreshUnits();
        pendingGrantedUnitIds.Clear();
        return true;
    }

    private async Task<bool> WaitForRuntimeAsync()
    {
        for (int frame = 0; frame < RuntimeWaitFrames; frame++)
        {
            lifetimeCancellation.Token.ThrowIfCancellationRequested();
            if (ResolveRuntime() && TryGetAuthoritativeRunId(out _)) return true;
            await Task.Yield();
        }
        return false;
    }

    private bool TryGetAuthoritativeRunId(out string runId)
    {
        runId = bootstrap?.GameplayRunId;
        return !string.IsNullOrWhiteSpace(runId) && Guid.TryParse(runId, out _);
    }

    private int CountEmptyBenchSlots()
    {
        if (bootstrap?.PlayerBoard == null) return 0;
        int count = 0;
        foreach (var position in bootstrap.PlayerBoard.GetPositions(CellType.Bench))
        {
            if (!bootstrap.PlayerBoard.IsOccupied(position) &&
                bootstrap.PlayerBoard.IsPlaceable(position))
            {
                count++;
            }
        }
        return count;
    }

    private bool ResolveRuntime()
    {
        if (bootstrap == null) bootstrap = FindObjectOfType<DragonBoundBootstrap>();
        return bootstrap != null && bootstrap.IsInitialized &&
               bootstrap.PlayerBoard != null && bootstrap.RecruitDestination != null;
    }

    private void PauseGameplayForAd()
    {
        if (pauseApplied) return;
        pauseApplied = true;
        timeScaleBeforeAd = Time.timeScale;
        ownsWavePause = bootstrap?.TwentyWave != null &&
                        bootstrap.Match != null &&
                        bootstrap.Match.State == MatchState.Running &&
                        bootstrap.TwentyWave.PauseWave();
        Time.timeScale = 0f;
    }

    private void RestoreGameplayAfterAd()
    {
        if (!pauseApplied) return;
        if (ownsWavePause && bootstrap?.TwentyWave != null &&
            bootstrap.Match != null && bootstrap.Match.State == MatchState.Paused)
        {
            bootstrap.TwentyWave.ResumeWave();
        }
        Time.timeScale = timeScaleBeforeAd;
        ownsWavePause = false;
        pauseApplied = false;
    }

    private void ApplyButtonState()
    {
        if (rewardButton != null)
        {
            rewardButton.interactable = statusLoaded && !requestPending &&
                                        authorityCanClaim && !rewardClaimed &&
                                        !verificationPending;
        }
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

    private static bool IsTerminalRunRewardError(string code)
    {
        return code == "RUN_NOT_ACTIVE" || code == "RUN_NOT_FOUND" ||
               code == "RUN_NOT_OWNED" || code == "INVALID_ARGUMENT" ||
               code == "IDEMPOTENCY_CONFLICT";
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        if (string.Equals(root.name, objectName, StringComparison.Ordinal)) return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform result = FindDescendant(root.GetChild(index), objectName);
            if (result != null) return result;
        }
        return null;
    }
}
