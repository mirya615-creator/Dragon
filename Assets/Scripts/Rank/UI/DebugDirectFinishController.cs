#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Debug helper for Greybox_Main: press F9 after the run starts to call /finish directly,
/// then enter Victory only after the server accepts the settlement.
/// </summary>
[DisallowMultipleComponent]
public sealed class DebugDirectFinishController : MonoBehaviour
{
    private const string GameScene = "Greybox_Main";

    private DragonBoundBootstrap bootstrap;
    private bool triggered;

    private void Awake()
    {
        Debug.Log("[DirectFinish] Mounted. Press F9 (or click the on-screen button) to call /finish directly.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForGameScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != GameScene) return;
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        for (int i = 0; i < scene.rootCount; i++)
        {
            if (scene.GetRootGameObjects()[i]
                .GetComponentInChildren<DebugDirectFinishController>(true) != null)
            {
                return;
            }
        }

        var owner = new GameObject("DebugDirectFinishController");
        owner.AddComponent<DebugDirectFinishController>();
    }

    private void Update()
    {
        if (triggered) return;
        if (Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.F10))
        {
            triggered = true;
            StartCoroutine(SubmitVictoryCoroutine());
        }
    }

    private void OnGUI()
    {
        if (triggered) return;
        // Draw a fallback button in the top-left corner so F9 can be bypassed
        // if the key is intercepted by the OS / input method.
        if (GUI.Button(new Rect(10, 10, 160, 40), "DIRECT FINISH (F9)"))
        {
            triggered = true;
            StartCoroutine(SubmitVictoryCoroutine());
        }
    }

    private IEnumerator SubmitVictoryCoroutine()
    {
        bootstrap = FindObjectOfType<DragonBoundBootstrap>();
        if (bootstrap == null)
        {
            Debug.LogError("[DirectFinish] DragonBoundBootstrap not found in Greybox_Main.", this);
            triggered = false;
            yield break;
        }

        // Wait until the bootstrap has finished initializing the match.
        int wait = 0;
        while (!bootstrap.IsInitialized && wait < 300)
        {
            yield return null;
            wait++;
        }

        if (!bootstrap.IsInitialized)
        {
            Debug.LogError("[DirectFinish] Bootstrap did not initialize in time.", this);
            triggered = false;
            yield break;
        }

        MatchController match = bootstrap.Match;
        if (match == null)
        {
            Debug.LogError("[DirectFinish] Match runtime is not available.", this);
            triggered = false;
            yield break;
        }

        // Wait until the match is actually running (so CurrentWave etc. are valid).
        wait = 0;
        while (match.State != MatchState.Running && wait < 600)
        {
            yield return null;
            wait++;
        }

        if (match.State == MatchState.Victory || match.State == MatchState.Defeat)
        {
            Debug.Log("[DirectFinish] Match is already terminal: " + match.State);
            yield break;
        }

        if (match.CurrentWave < 1)
        {
            Debug.LogWarning(
                "[DirectFinish] Direct finish requires an active wave.",
                this);
            triggered = false;
            yield break;
        }

        var onlineGateway = ClientCompositionRoot.Current.Gameplay as GoUnaryGameplayRunGateway;
        if (onlineGateway != null)
        {
            AuthSession session = ClientCompositionRoot.Current.AuthSession.Current;
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
            {
                Debug.LogError(
                    "[DirectFinish] Direct finish requires an authenticated PlayerId.",
                    this);
                triggered = false;
                yield break;
            }

            Task<GameSettlementPreparation> finishTask =
                new GameSettlementCoordinator(ClientCompositionRoot.Current).PrepareAsync(
                    session.PlayerId,
                    bootstrap.GameplayRunId,
                    ServerMatchResult.Victory,
                    GameplayTerminationReason.Natural,
                    GameplayFaultAttribution.None,
                    match.CurrentWave,
                    match.Player.HatchlingHealth,
                    bootstrap.Recruitment != null
                        ? bootstrap.Recruitment.CompletedRecruitments
                        : 0,
                    CancellationToken.None);
            while (!finishTask.IsCompleted) yield return null;
            if (finishTask.IsCanceled)
            {
                triggered = false;
                yield break;
            }
            if (finishTask.IsFaulted)
            {
                Exception failure = finishTask.Exception?.GetBaseException();
                if (failure is ClientServiceException serviceFailure)
                {
                    Debug.LogError(
                        "[DirectFinish] Direct /finish failed." +
                        " RunId=" + bootstrap.GameplayRunId +
                        " Wave=" + match.CurrentWave +
                        " MatchState=" + match.State +
                        " HttpStatus=" + serviceFailure.HttpStatus +
                        " Code=" + serviceFailure.Code +
                        " TraceId=" + serviceFailure.TraceId +
                        " SnapshotPending=" +
                        onlineGateway.HasPendingFinishRequest(bootstrap.GameplayRunId) +
                        " Message=" + serviceFailure.Message,
                        this);
                }
                else
                {
                    Debug.LogError(
                        "[DirectFinish] Direct /finish failed: " +
                        (failure?.Message ?? "Unknown error"),
                        this);
                }
                triggered = false;
                yield break;
            }
        }

        // The settlement ledger is already saved, so the normal state listener only opens
        // SettlementPanel and reuses the accepted result without sending another request.
        bool ok = match.TryTransition(MatchState.Victory);
        Debug.Log(ok
            ? "[DirectFinish] Direct finish accepted -> Victory transitioned."
            : "[DirectFinish] TryTransition(Victory) returned false. State=" + match.State);
    }
}
#endif
