using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameRankResultController : MonoBehaviour
{
    private const string DoubleGoldPlacement = "game_gold_double";
    private const int MinimumLoadingMilliseconds = 2000;

    private Button victoryButton;
    private Button defeatButton;
    private Button returnButton;
    private GameObject loadingPanel;
    private GameObject settlementPanel;
    private TMP_Text settlementResultText;
    private TMP_Text goldText;
    private Button receiveButton;
    private Button doubleButton;
    private IPlayerRankGateway rankGateway;
    private IPlayerGoldGateway goldGateway;
    private IMerchantGateway merchantGateway;
    private IRewardedAdService rewardedAdService;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;
    private string matchId;
    private MatchOutcome pendingOutcome;
    private bool isFinishing;
    private bool gameReady;
    private bool hasPendingOutcome;
    private bool claimInProgress;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        victoryButton = FindButton("VictoryBtn");
        defeatButton = FindButton("DefaltBtn");
        returnButton = FindButton("ReturnBtn");
        loadingPanel = transform.Find("LoadingPanel")?.gameObject;
        rankGateway = services.Rank;
        goldGateway = services.Gold;
        merchantGateway = services.Merchant;
        rewardedAdService = services.RewardedAds;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();
        matchId = Guid.NewGuid().ToString("N");

        if (loadingPanel == null)
        {
            Debug.LogError("GameRankResultController expects LoadingPanel under Game/MainPanel.");
            enabled = false;
            return;
        }
        if (!ResolveSettlementView())
        {
            enabled = false;
            return;
        }

        loadingPanel.SetActive(true);
        loadingPanel.transform.SetAsLastSibling();
        SetGameControlsInteractable(false);
        settlementPanel.SetActive(false);
        GameRuneDropSession.Begin(matchId);

        if (victoryButton != null) victoryButton.onClick.AddListener(OnVictoryClicked);
        if (defeatButton != null) defeatButton.onClick.AddListener(OnDefeatClicked);
        receiveButton.onClick.AddListener(OnReceiveClicked);
        doubleButton.onClick.AddListener(OnDoubleClicked);

        if (victoryButton == null || defeatButton == null)
        {
            Debug.LogError("GameRankResultController expects VictoryBtn and DefaltBtn under Game/MainPanel.");
        }
    }

    private async void Start()
    {
        if (!enabled || loadingPanel == null) return;

        try
        {
            Task minimumDisplay = Task.Delay(
                MinimumLoadingMilliseconds,
                lifetimeCancellation.Token);
            Task initialization = InitializeGameAsync(lifetimeCancellation.Token);
            await Task.WhenAll(minimumDisplay, initialization);

            gameReady = true;
            loadingPanel.SetActive(false);
            SetGameControlsInteractable(true);
        }
        catch (OperationCanceledException)
        {
            // Game scene was unloaded while initialization was running.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to initialize Game scene: {exception.Message}");
            // Keep LoadingPanel visible and gameplay disabled on initialization failure.
        }
    }

    private static async Task InitializeGameAsync(CancellationToken cancellationToken)
    {
        // Awake has created the match session and initialized the Game services.
        // Yield once so every Game object can finish its first-frame setup.
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Canvas.ForceUpdateCanvases();
    }

    private void OnDestroy()
    {
        if (victoryButton != null) victoryButton.onClick.RemoveListener(OnVictoryClicked);
        if (defeatButton != null) defeatButton.onClick.RemoveListener(OnDefeatClicked);
        if (receiveButton != null) receiveButton.onClick.RemoveListener(OnReceiveClicked);
        if (doubleButton != null) doubleButton.onClick.RemoveListener(OnDoubleClicked);
        if (lifetimeCancellation == null) return;
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        lifetimeCancellation = null;
    }

    private Button FindButton(string objectName)
    {
        Transform buttonTransform = transform.Find(objectName);
        return buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
    }

    private async void OnVictoryClicked()
    {
        if (!TryBeginFinish()) return;

        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Victory cannot be recorded without an authenticated PlayerId.");
            SetGameResultBusy(false);
            return;
        }

        try
        {
            RankProgressResult result = await rankGateway.RecordVictoryAsync(
                session.PlayerId,
                matchId,
                lifetimeCancellation.Token);
            RankPromotionStore.Set(session.PlayerId, result);
            await GameRuneDropSession.SettleAsync(
                session.PlayerId,
                matchId,
                lifetimeCancellation.Token);
            ShowSettlement(MatchOutcome.Victory);
        }
        catch (OperationCanceledException)
        {
            // Scene was unloaded during settlement.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to record victory: {exception.Message}");
            SetGameResultBusy(false);
        }
    }

    private async void OnDefeatClicked()
    {
        if (!TryBeginFinish()) return;

        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Defeat cannot be recorded without an authenticated PlayerId.");
            SetGameResultBusy(false);
            return;
        }

        try
        {
            await rankGateway.RecordDefeatAsync(
                session.PlayerId,
                matchId,
                lifetimeCancellation.Token);
            await GameRuneDropSession.SettleAsync(
                session.PlayerId,
                matchId,
                lifetimeCancellation.Token);
            ShowSettlement(MatchOutcome.Defeat);
        }
        catch (OperationCanceledException)
        {
            // Scene was unloaded during settlement.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to record defeat: {exception.Message}");
            SetGameResultBusy(false);
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
            Debug.LogError($"Unable to receive match gold: {exception.Message}");
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

            string adVerificationId = Guid.NewGuid().ToString("N");
            await SettleGoldAndReturnAsync(GoldClaimType.RewardedAd, adVerificationId);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to receive doubled match gold: {exception.Message}");
            SetClaimBusy(false);
        }
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

        await goldGateway.SettleMatchAsync(
            session.PlayerId,
            matchId,
            pendingOutcome,
            claimType,
            adVerificationId,
            lifetimeCancellation.Token);
        MerchantRunResult merchantResult = await merchantGateway.RecordCompletedRunAsync(
            session.PlayerId,
            matchId,
            lifetimeCancellation.Token);
        if (merchantResult.Offer != null)
        {
            MerchantPresentationStore.MarkPending(session.PlayerId);
        }
        LoadMainScene();
    }

    private bool TryBeginClaim()
    {
        if (!hasPendingOutcome || claimInProgress) return false;
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("SceneLoader is unavailable.");
            return false;
        }

        SetClaimBusy(true);
        return true;
    }

    private void ShowSettlement(MatchOutcome outcome)
    {
        pendingOutcome = outcome;
        hasPendingOutcome = true;
        settlementResultText.text = outcome == MatchOutcome.Victory ? "Victory" : "Defalt";
        long baseReward = outcome == MatchOutcome.Victory
            ? LocalPlayerGoldGateway.VictoryReward
            : LocalPlayerGoldGateway.DefeatReward;
        goldText.text = "+" + baseReward;
        settlementPanel.SetActive(true);
        settlementPanel.transform.SetAsLastSibling();
        SetClaimBusy(false);
    }

    private void SetClaimBusy(bool busy)
    {
        claimInProgress = busy;
        if (receiveButton != null) receiveButton.interactable = !busy;
        if (doubleButton != null) doubleButton.interactable = !busy;
    }

    private bool TryBeginFinish()
    {
        if (!gameReady || isFinishing || hasPendingOutcome) return false;
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("SceneLoader is unavailable.");
            return false;
        }

        SetGameResultBusy(true);
        return true;
    }

    private void SetGameResultBusy(bool busy)
    {
        isFinishing = busy;
        if (victoryButton != null) victoryButton.interactable = !busy;
        if (defeatButton != null) defeatButton.interactable = !busy;
        if (returnButton != null) returnButton.interactable = !busy;
    }

    private void SetGameControlsInteractable(bool interactable)
    {
        if (victoryButton != null) victoryButton.interactable = interactable;
        if (defeatButton != null) defeatButton.interactable = interactable;
        if (returnButton != null) returnButton.interactable = interactable;
    }

    private bool ResolveSettlementView()
    {
        Transform panelTransform = transform.Find("SettlementPanel");
        if (panelTransform == null)
        {
            Debug.LogError("GameRankResultController expects SettlementPanel under Game/MainPanel.");
            return false;
        }

        Transform resultTransform = panelTransform.Find("Text");
        Transform goldTransform = panelTransform.Find("GoldText");
        Transform receiveTransform = panelTransform.Find("ReciveBtn");
        Transform doubleTransform = panelTransform.Find("DoubleBtn");
        if (resultTransform == null || goldTransform == null || receiveTransform == null || doubleTransform == null)
        {
            Debug.LogError("SettlementPanel requires Text, GoldText, ReciveBtn, and DoubleBtn children.");
            return false;
        }

        settlementPanel = panelTransform.gameObject;
        settlementResultText = resultTransform.GetComponent<TMP_Text>();
        goldText = goldTransform.GetComponent<TMP_Text>();
        receiveButton = receiveTransform.GetComponent<Button>();
        doubleButton = doubleTransform.GetComponent<Button>();
        bool complete = settlementResultText != null && goldText != null &&
                        receiveButton != null && doubleButton != null;
        if (!complete)
        {
            Debug.LogError("SettlementPanel children are missing TMP text or Button components.");
        }
        return complete;
    }

    private void LoadMainScene()
    {
        SceneLoader.Instance.LoadSceneAsync("Main");
    }
}
