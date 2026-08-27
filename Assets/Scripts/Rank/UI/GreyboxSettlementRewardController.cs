using System;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GreyboxSettlementRewardController : MonoBehaviour
{
    private const string DoubleGoldPlacement = "game_gold_double";

    private TMP_Text resultText;
    private TMP_Text goldText;
    private Button receiveButton;
    private Button doubleButton;
    private DragonBoundBootstrap bootstrap;
    private GameSettlementCoordinator settlementCoordinator;
    private IRewardedAdService rewardedAdService;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;
    private string matchId;
    private MatchOutcome pendingOutcome;
    private bool preparing;
    private bool readyToClaim;
    private bool claimInProgress;

    private void Awake()
    {
        resultText = transform.Find("Text")?.GetComponent<TMP_Text>();
        goldText = transform.Find("GoldText")?.GetComponent<TMP_Text>();
        receiveButton = transform.Find("ReciveBtn")?.GetComponent<Button>();
        doubleButton = transform.Find("DoubleBtn")?.GetComponent<Button>();
        if (resultText == null || goldText == null || receiveButton == null || doubleButton == null)
        {
            Debug.LogError(
                "Greybox SettlementPanel requires Text, GoldText, ReciveBtn, and DoubleBtn.");
            enabled = false;
            return;
        }

        IClientServices services = ClientCompositionRoot.Current;
        settlementCoordinator = new GameSettlementCoordinator(services);
        rewardedAdService = services.RewardedAds;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();
        receiveButton.onClick.AddListener(OnReceiveClicked);
        doubleButton.onClick.AddListener(OnDoubleClicked);
        SetClaimBusy(true);
    }

    private void Start()
    {
        if (!enabled) return;
        bootstrap = FindObjectOfType<DragonBoundBootstrap>();
        if (bootstrap?.Match == null)
        {
            Debug.LogError("Greybox settlement cannot find DragonBoundBootstrap.");
            return;
        }

        matchId = bootstrap.GameplayRunId;
        if (string.IsNullOrWhiteSpace(matchId))
        {
            Debug.LogError("Greybox settlement requires the gameplay RunId.");
            enabled = false;
            return;
        }
        GameRuneDropSession.Begin(matchId);

        bootstrap.Match.StateChanged -= HandleMatchStateChanged;
        bootstrap.Match.StateChanged += HandleMatchStateChanged;
        ImportCompletedWaveRuneRewards();
        HandleMatchStateChanged(bootstrap.Match.State);
    }

    private void OnDestroy()
    {
        if (bootstrap?.Match != null)
        {
            bootstrap.Match.StateChanged -= HandleMatchStateChanged;
        }
        if (receiveButton != null) receiveButton.onClick.RemoveListener(OnReceiveClicked);
        if (doubleButton != null) doubleButton.onClick.RemoveListener(OnDoubleClicked);
        if (lifetimeCancellation == null) return;
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        lifetimeCancellation = null;
    }

    private void ImportCompletedWaveRuneRewards()
    {
        if (bootstrap?.PlayerRuneRewards?.GrantedRewards == null) return;
        foreach (DragonBound.Runes.RuneReward reward in
                 bootstrap.PlayerRuneRewards.GrantedRewards)
        {
            GameRuneDropSession.RecordCompletedWaveReward(reward);
        }
    }

    private async void HandleMatchStateChanged(MatchState state)
    {
        if (state != MatchState.Victory && state != MatchState.Defeat || preparing || readyToClaim)
        {
            return;
        }

        preparing = true;
        ImportCompletedWaveRuneRewards();
        pendingOutcome = state == MatchState.Victory
            ? MatchOutcome.Victory
            : MatchOutcome.Defeat;
        resultText.text = pendingOutcome == MatchOutcome.Victory ? "Victory" : "Defeat";
        long baseReward = pendingOutcome == MatchOutcome.Victory
            ? LocalPlayerGoldGateway.VictoryReward
            : LocalPlayerGoldGateway.DefeatReward;
        goldText.text = "+" + baseReward;
        SetClaimBusy(true);

        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Greybox settlement requires an authenticated PlayerId.");
            preparing = false;
            return;
        }

        try
        {
            GameSettlementPreparation preparation = await settlementCoordinator.PrepareAsync(
                session.PlayerId,
                matchId,
                pendingOutcome == MatchOutcome.Victory
                    ? ServerMatchResult.Victory
                    : ServerMatchResult.Defeat,
                GameplayTerminationReason.Natural,
                GameplayFaultAttribution.None,
                bootstrap.Match.CurrentWave,
                bootstrap.Match.Player.Resources,
                bootstrap.Recruitment != null ? bootstrap.Recruitment.CompletedRecruitments : 0,
                lifetimeCancellation.Token);
            ApplyPreparation(preparation);
            readyToClaim = true;
            SetClaimBusy(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to prepare Greybox settlement: {exception.Message}");
            preparing = false;
        }
    }

    private async void OnReceiveClicked()
    {
        if (!TryBeginClaim()) return;
        try
        {
            await SettleGoldAndReturnAsync(GoldClaimType.Standard, string.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to receive Greybox match gold: {exception.Message}");
            SetClaimBusy(false);
        }
    }

    private async void OnDoubleClicked()
    {
        if (!TryBeginClaim()) return;
        try
        {
            RewardedAdResult result = await rewardedAdService.ShowAsync(
                DoubleGoldPlacement,
                lifetimeCancellation.Token);
            if (result != RewardedAdResult.Completed)
            {
                SetClaimBusy(false);
                return;
            }

            await SettleGoldAndReturnAsync(
                GoldClaimType.RewardedAd,
                Guid.NewGuid().ToString("N"));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to receive doubled Greybox match gold: {exception.Message}");
            SetClaimBusy(false);
        }
    }

    private bool TryBeginClaim()
    {
        if (!readyToClaim || claimInProgress) return false;
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("SceneLoader is unavailable.");
            return false;
        }

        SetClaimBusy(true);
        return true;
    }

    private async Task SettleGoldAndReturnAsync(
        GoldClaimType claimType,
        string adVerificationId)
    {
        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            throw new InvalidOperationException("Gold cannot be settled without an authenticated PlayerId.");
        }

        await settlementCoordinator.ClaimGoldAsync(
            session.PlayerId,
            matchId,
            claimType,
            adVerificationId,
            lifetimeCancellation.Token);
        SceneLoader.Instance.LoadSceneAsync("Main");
    }

    private void ApplyPreparation(GameSettlementPreparation preparation)
    {
        if (preparation?.Result == null) return;
        if (preparation.Result.SettlementType == GameplaySettlementType.Compensation)
        {
            resultText.text = "Compensation";
        }
        else if (preparation.Result.SettlementType == GameplaySettlementType.Retry)
        {
            resultText.text = "Settlement Retry";
        }
        else
        {
            switch (preparation.Result.Result)
            {
                case ServerMatchResult.Victory:
                    resultText.text = "Victory";
                    break;
                case ServerMatchResult.Defeat:
                    resultText.text = "Defeat";
                    break;
                case ServerMatchResult.NoContest:
                    resultText.text = "No Contest";
                    goldText.text = "+0";
                    break;
                default:
                    resultText.text = "Settlement Pending";
                    goldText.text = "+0";
                    break;
            }
        }
        if (doubleButton != null) doubleButton.gameObject.SetActive(preparation.CanClaimGold);
    }

    private void SetClaimBusy(bool busy)
    {
        claimInProgress = busy;
        if (receiveButton != null) receiveButton.interactable = readyToClaim && !busy;
        if (doubleButton != null) doubleButton.interactable = readyToClaim && !busy;
    }
}
