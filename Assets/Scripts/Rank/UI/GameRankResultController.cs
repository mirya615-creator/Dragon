using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameRankResultController : MonoBehaviour
{
    private Button victoryButton;
    private Button defeatButton;
    private Button returnButton;
    private IPlayerRankGateway rankGateway;
    private CancellationTokenSource lifetimeCancellation;
    private string matchId;
    private bool isFinishing;

    private void Awake()
    {
        victoryButton = FindButton("VictoryBtn");
        defeatButton = FindButton("DefaltBtn");
        returnButton = FindButton("ReturnBtn");
        rankGateway = new LocalPlayerRankGateway();
        lifetimeCancellation = new CancellationTokenSource();
        matchId = Guid.NewGuid().ToString("N");
        GameRuneDropSession.Begin(matchId);

        if (victoryButton != null) victoryButton.onClick.AddListener(OnVictoryClicked);
        if (defeatButton != null) defeatButton.onClick.AddListener(OnDefeatClicked);

        if (victoryButton == null || defeatButton == null)
        {
            Debug.LogError("GameRankResultController expects VictoryBtn and DefaltBtn under Game/MainPanel.");
        }
    }

    private void OnDestroy()
    {
        if (victoryButton != null) victoryButton.onClick.RemoveListener(OnVictoryClicked);
        if (defeatButton != null) defeatButton.onClick.RemoveListener(OnDefeatClicked);
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
            SetBusy(false);
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
            LoadMainScene();
        }
        catch (OperationCanceledException)
        {
            // Scene was unloaded during settlement.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to record victory: {exception.Message}");
            SetBusy(false);
        }
    }

    private async void OnDefeatClicked()
    {
        if (!TryBeginFinish()) return;

        AuthSession session = AuthSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Defeat cannot be recorded without an authenticated PlayerId.");
            SetBusy(false);
            return;
        }

        try
        {
            await rankGateway.RecordDefeatAsync(
                session.PlayerId,
                matchId,
                lifetimeCancellation.Token);
            GameRuneDropSession.Settle(session.PlayerId, matchId);
            LoadMainScene();
        }
        catch (OperationCanceledException)
        {
            // Scene was unloaded during settlement.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to record defeat: {exception.Message}");
            SetBusy(false);
        }
    }

    private bool TryBeginFinish()
    {
        if (isFinishing) return false;
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("SceneLoader is unavailable. Start Play Mode from Bootstrap.");
            return false;
        }

        SetBusy(true);
        return true;
    }

    private void SetBusy(bool busy)
    {
        isFinishing = busy;
        if (victoryButton != null) victoryButton.interactable = !busy;
        if (defeatButton != null) defeatButton.interactable = !busy;
        if (returnButton != null) returnButton.interactable = !busy;
    }

    private void LoadMainScene()
    {
        SceneLoader.Instance.LoadSceneAsync("Main");
    }
}
