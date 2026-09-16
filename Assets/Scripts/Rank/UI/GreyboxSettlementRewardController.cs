using System;
using DragonBound.Presentation;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Services;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GreyboxSettlementRewardController : MonoBehaviour
{
    private const string DoubleGoldPlacement = "settle_double";
    private const string V1VictorySpritePath = "GameUI/SettlementUI/Victory";
    private const string V1DefeatSpritePath = "GameUI/SettlementUI/Defeat";
    private const string V2VictorySpritePath = "UIResources/Game/Settlement/图层 70";
    private const string V2DefeatSpritePath = "UIResources/Game/Settlement/图层 71";

    private Image resultImage;
    private TMPro.TMP_Text resultText;
    private TMPro.TMP_Text goldText;
    private Sprite victorySprite;
    private Sprite defeatSprite;
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
    private bool useV2Presentation;

    private void Awake()
    {
        useV2Presentation = string.Equals(
            DragonBound.Presentation.UiAssets.Active?.VariantId,
            "V2",
            StringComparison.Ordinal);
        resultImage = transform.FindUi("SettleImg")?.GetComponent<Image>();
        resultText = transform.FindUi("Text")?.GetComponent<TMPro.TMP_Text>();
        goldText = transform.FindUi("GoldText")?.GetComponent<TMPro.TMP_Text>();
        receiveButton = transform.FindUi("ReciveBtn")?.GetComponent<Button>();
        doubleButton = transform.FindUi("DoubleBtn")?.GetComponent<Button>();
        victorySprite = DragonBound.Presentation.UiAssets.Load<Sprite>(
            useV2Presentation ? V2VictorySpritePath : V1VictorySpritePath);
        defeatSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(
            useV2Presentation ? V2DefeatSpritePath : V1DefeatSpritePath);
        if (resultImage == null || victorySprite == null || defeatSprite == null ||
            goldText == null || receiveButton == null || doubleButton == null ||
            (useV2Presentation && resultText == null))
        {
            Debug.LogError(
                "Greybox SettlementPanel requires SettleImg, Text, GoldText, ReciveBtn, " +
                "DoubleBtn, and the Victory/Defeat settlement sprites.");
            enabled = false;
            return;
        }

        IClientServices services = ClientCompositionRoot.Current;
        settlementCoordinator = new GameSettlementCoordinator(services);
        rewardedAdService = services.RewardedAds;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();
        receiveButton.onClick.AddListener(OnReceiveClicked);
        doubleButton.interactable = false;
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
        ApplyResultImage(pendingOutcome);
        ShowProcessingState();

        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Greybox settlement requires an authenticated PlayerId.");
            preparing = false;
            ShowFailureState();
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
                bootstrap.Match.Player.HatchlingHealth,
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
            ShowFailureState();
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
        string playerId = null;
        string adEventId = null;
        try
        {
            AuthSession session = authSessionStore.Current;
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
                throw new InvalidOperationException(
                    "Gold cannot be doubled without an authenticated PlayerId.");
            playerId = session.PlayerId;

            RewardedAdResult result = await rewardedAdService.ShowAsync(
                DoubleGoldPlacement,
                lifetimeCancellation.Token);
            if (result != RewardedAdResult.Completed)
            {
                SetClaimBusy(false);
                return;
            }

            adEventId = PendingAdEventStore.GetOrCreate(
                playerId, DoubleGoldPlacement, matchId);
            await SettleGoldAndReturnAsync(
                GoldClaimType.RewardedAd,
                adEventId);
            PendingAdEventStore.Complete(
                playerId, DoubleGoldPlacement, matchId, adEventId);
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
        long fallbackReward = preparation.GoldOutcome == MatchOutcome.Victory
            ? LocalPlayerGoldGateway.VictoryReward
            : LocalPlayerGoldGateway.DefeatReward;
        long displayedReward = preparation.CanClaimGold
            ? preparation.Result.GoldReward > 0
                ? preparation.Result.GoldReward
                : fallbackReward
            : 0;
        goldText.text = "+" + displayedReward;
        goldText.gameObject.SetActive(true);
        receiveButton.gameObject.SetActive(true);
        if (doubleButton != null)
        {
            doubleButton.gameObject.SetActive(true);
            doubleButton.interactable = false;
        }
    }

    private void ShowProcessingState()
    {
        readyToClaim = false;
        resultImage.gameObject.SetActive(true);
        goldText.gameObject.SetActive(false);
        receiveButton.gameObject.SetActive(false);
        doubleButton.gameObject.SetActive(false);
        SetClaimBusy(true);
    }

    private void ShowFailureState()
    {
        readyToClaim = false;
        resultImage.gameObject.SetActive(true);
        goldText.gameObject.SetActive(false);
        receiveButton.gameObject.SetActive(false);
        doubleButton.gameObject.SetActive(false);
        SetClaimBusy(true);
    }

    private void ApplyResultImage(MatchOutcome outcome)
    {
        bool victory = outcome == MatchOutcome.Victory;
        resultImage.sprite = victory ? victorySprite : defeatSprite;
        resultImage.preserveAspect = true;
        resultImage.gameObject.SetActive(true);
        if (resultText != null)
        {
            resultText.text = victory ? "Victory" : useV2Presentation ? "Dafalt" : "Defeat";
            resultText.gameObject.SetActive(true);
        }
    }

    private void SetClaimBusy(bool busy)
    {
        claimInProgress = busy;
        if (receiveButton != null) receiveButton.interactable = readyToClaim && !busy;
        if (doubleButton != null) doubleButton.interactable = false;
    }
}
