using System;
using System.Threading;
using DragonBound.Bootstrap;
using DragonBound.Presentation;
using DragonBound.Services;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GreyboxPauseExitRewardController : MonoBehaviour
{
    private GreyboxHudView hudView;
    private DragonBoundBootstrap bootstrap;
    private GameSettlementCoordinator settlementCoordinator;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;
    private bool exitInProgress;
    private bool pauseExitBound;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        settlementCoordinator = new GameSettlementCoordinator(services);
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();
    }

    private void Start()
    {
        hudView = FindObjectOfType<GreyboxHudView>();
        bootstrap = FindObjectOfType<DragonBoundBootstrap>();
        if (hudView == null || bootstrap == null)
        {
            Debug.LogError("Pause exit requires GreyboxHudView and DragonBoundBootstrap.");
            enabled = false;
            return;
        }

        if (bootstrap.IsInitialized)
        {
            BindPauseExit();
            return;
        }
        if (bootstrap.InitializationFailed)
        {
            HandleInitializationFailed(bootstrap.InitializationException);
            return;
        }

        bootstrap.Initialized += BindPauseExit;
        bootstrap.InitializationFailedEvent += HandleInitializationFailed;
    }

    private void BindPauseExit()
    {
        if (pauseExitBound) return;
        if (bootstrap?.Match == null || string.IsNullOrWhiteSpace(bootstrap.GameplayRunId))
        {
            Debug.LogError("Pause exit requires initialized match state and gameplay RunId.");
            enabled = false;
            return;
        }

        hudView.PauseExitRequested -= HandlePauseExitRequested;
        hudView.PauseExitRequested += HandlePauseExitRequested;
        pauseExitBound = true;
    }

    private void HandleInitializationFailed(Exception exception)
    {
        Debug.LogError(
            "Pause exit is unavailable because gameplay initialization failed: " +
            (exception?.Message ?? "Unknown error"));
        enabled = false;
    }

    private void OnDestroy()
    {
        if (hudView != null)
        {
            hudView.PauseExitRequested -= HandlePauseExitRequested;
        }
        if (bootstrap != null)
        {
            bootstrap.Initialized -= BindPauseExit;
            bootstrap.InitializationFailedEvent -= HandleInitializationFailed;
        }

        if (lifetimeCancellation == null)
        {
            return;
        }

        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        lifetimeCancellation = null;
    }

    private async void HandlePauseExitRequested()
    {
        if (exitInProgress)
        {
            return;
        }

        exitInProgress = true;
        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Pause exit reward requires an authenticated PlayerId.");
            ResetExitRequest();
            return;
        }

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("SceneLoader is unavailable.");
            ResetExitRequest();
            return;
        }

        try
        {
            RecordCurrentRuneRewards();
            await settlementCoordinator.PrepareAsync(
                session.PlayerId,
                bootstrap.GameplayRunId,
                ServerMatchResult.Defeat,
                GameplayTerminationReason.PlayerSurrender,
                GameplayFaultAttribution.Player,
                bootstrap.Match.CurrentWave,
                bootstrap.Match.Player.HatchlingHealth,
                bootstrap.Recruitment != null ? bootstrap.Recruitment.CompletedRecruitments : 0,
                lifetimeCancellation.Token);
            await settlementCoordinator.ClaimGoldAsync(
                session.PlayerId,
                bootstrap.GameplayRunId,
                GoldClaimType.Standard,
                string.Empty,
                lifetimeCancellation.Token);
            SceneLoader.Instance.LoadSceneAsync("Main");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to grant pause exit gold: {exception.Message}");
            ResetExitRequest();
        }
    }

    private void RecordCurrentRuneRewards()
    {
        if (bootstrap?.PlayerRuneRewards?.GrantedRewards == null) return;
        foreach (DragonBound.Runes.RuneReward reward in bootstrap.PlayerRuneRewards.GrantedRewards)
            GameRuneDropSession.RecordCompletedWaveReward(reward);
    }

    private void ResetExitRequest()
    {
        exitInProgress = false;
        hudView?.CancelPauseExitRequest();
    }
}
