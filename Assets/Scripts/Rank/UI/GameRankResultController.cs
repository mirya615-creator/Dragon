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

    private Button victoryButton;
    private Button defeatButton;
    private Button returnButton;
    private GameObject settlementPanel;
    private TMP_Text settlementResultText;
    private TMP_Text goldText;
    private Button receiveButton;
    private Button doubleButton;
    private IPlayerRankGateway rankGateway;
    private IPlayerGoldGateway goldGateway;
    private IMerchantGateway merchantGateway;
    private IRewardedAdService rewardedAdService;
    private CancellationTokenSource lifetimeCancellation;
    private string matchId;
    private MatchOutcome pendingOutcome;
    private bool isFinishing;
    private bool hasPendingOutcome;
    private bool claimInProgress;

    private void Awake()
    {
        victoryButton = FindButton("VictoryBtn");
        defeatButton = FindButton("DefaltBtn");
        returnButton = FindButton("ReturnBtn");
        rankGateway = new LocalPlayerRankGateway();
        goldGateway = new LocalPlayerGoldGateway();
        merchantGateway = new LocalMerchantGateway();
        lifetimeCancellation = new CancellationTokenSource();
        matchId = Guid.NewGuid().ToString("N");

        if (!ResolveSettlementView())
        {
            enabled = false;
            return;
        }

        rewardedAdService = new MockRewardedAdService(transform, goldText.font);
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

        AuthSession session = AuthSessionStore.Current;
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
            GameRuneDropSession.Settle(session.PlayerId, matchId);
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

        AuthSession session = AuthSessionStore.Current;
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
            GameRuneDropSession.Settle(session.PlayerId, matchId);
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
        AuthSession session = AuthSessionStore.Current;
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
        if (isFinishing || hasPendingOutcome) return false;
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
