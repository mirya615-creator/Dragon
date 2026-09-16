using System.Collections;
using System.Threading;
using DragonBound.Presentation;
using DragonBound.Bootstrap;
using DragonBound.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gameplay loading curtain for Greybox_Main.
///
/// Flow: the Main scene asks SceneLoader to load Greybox_Main. As soon as the
/// gameplay scene is activated this controller opens the already-authored
/// Canvas/LoadingPanel at the top of the Canvas and keeps it on screen until the
/// gameplay board has finished initializing (DragonBoundBootstrap.IsInitialized).
///
/// The curtain is released at initialization completion - deliberately NOT at
/// MatchState.Running. In Greybox_Main the first enemy only spawns ~6s into the
/// scene (1s Ready prompt + 4s wave-1 first-spawn preparation), and that opening
/// window is part of the visible match: the player sees the entrance prompt and
/// places/recruits before wave 1 arrives. Waiting for Running would leave the
/// curtain over the entire opening, which is why the mask is dropped as soon as
/// the board is presentable instead.
///
/// The component installs itself; it does not need to be authored in the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayLoadingPanelController : MonoBehaviour
{
    private const string GameScene = "Greybox_Main";
    private const string CanvasRootName = "Canvas";
    private const string LoadingPanelNode = "LoadingPanel";
    private const int ReadyTimeoutSeconds = 20;

    private GameObject loadingPanel;
    private LoadingPanelEntranceAnimator entranceAnimator;
    private DragonBoundBootstrap bootstrap;
    private TMP_Text playerRateText;
    private TMP_Text aiRateText;
    private TMP_Text playerRankText;
    private TMP_Text aiRankText;
    private CancellationTokenSource lifetimeCancellation;
    private MatchController trackedMatch;
    private string trackedPlayerId;
    private bool matchResultRecorded;
    private bool hidden;
    private bool cancelled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        // RuntimeInitializeOnLoadMethod(AfterSceneLoad) only covers the initial
        // startup scene. Register once so Login -> Main -> Greybox_Main is also
        // observed when domain reload is enabled or disabled.
        SceneManager.sceneLoaded -= InstallForGameplayScene;
        SceneManager.sceneLoaded += InstallForGameplayScene;
    }

    private static void InstallForGameplayScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameScene) return;

        // Avoid double mounting when the scene is entered again or when a manual
        // instance already exists after a domain reload.
        for (int i = 0; i < scene.rootCount; i++)
        {
            if (scene.GetRootGameObjects()[i]
                .GetComponentInChildren<GameplayLoadingPanelController>(true) != null)
            {
                return;
            }
        }

        var owner = new GameObject("GameplayLoadingPanelController");
        SceneManager.MoveGameObjectToScene(owner, scene);
        owner.AddComponent<GameplayLoadingPanelController>();
    }

    private void Awake()
    {
        lifetimeCancellation = new CancellationTokenSource();
        ResolvePanel();
        if (loadingPanel == null)
        {
            Debug.LogError(
                "GameplayLoadingPanelController requires a Canvas root containing " +
                "Canvas/" + LoadingPanelNode + " in " + GameScene + ".",
                this);
            enabled = false;
            return;
        }

        entranceAnimator = loadingPanel.GetComponent<LoadingPanelEntranceAnimator>();
        if (entranceAnimator == null)
        {
            Debug.LogError(
                "GameplayLoadingPanelController requires LoadingPanelEntranceAnimator " +
                "on Canvas/LoadingPanel.",
                loadingPanel);
            enabled = false;
            return;
        }

        // Show the curtain from the very first rendered frame and keep it above
        // every other Canvas child so no half-initialized board can flash through.
        loadingPanel.transform.SetAsLastSibling();
        loadingPanel.SetActive(true);
        ResolvePresentationTexts();
        RefreshRateTexts();
        RefreshRankTexts();
    }

    private void Start()
    {
        StartCoroutine(WaitUntilBoardPresentable());
    }

    private void OnDestroy()
    {
        cancelled = true;
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
        if (trackedMatch != null)
        {
            trackedMatch.StateChanged -= HandleTrackedMatchStateChanged;
        }
    }

    /// <summary>Locates the authored Canvas/LoadingPanel in this controller's scene.</summary>
    private void ResolvePanel()
    {
        Scene scene = gameObject.scene.IsValid()
            ? gameObject.scene
            : SceneManager.GetActiveScene();
        for (int i = 0; i < scene.rootCount; i++)
        {
            Transform root = scene.GetRootGameObjects()[i].transform;
            if (root.name != CanvasRootName) continue;
            loadingPanel = root.FindUi(LoadingPanelNode)?.gameObject;
            if (loadingPanel != null) return;
        }
    }

    private IEnumerator WaitUntilBoardPresentable()
    {
        float deadline = Time.unscaledTime + ReadyTimeoutSeconds;

        // 1) DragonBoundBootstrap may be created late when the scene defers
        //    initialization until an external run snapshot has been injected.
        while (bootstrap == null && !cancelled && Time.unscaledTime < deadline)
        {
            bootstrap = FindObjectOfType<DragonBoundBootstrap>();
            yield return null;
        }

        if (cancelled) yield break;
        if (bootstrap == null)
        {
            Debug.LogError(
                "GameplayLoadingPanelController: DragonBoundBootstrap was not found in " +
                GameScene + ".", this);
            yield break;
        }

        // 2) Wait for initialization to finish (board presentable). Do NOT wait
        //    for MatchState.Running: that only happens right before wave 1 spawns,
        //    ~6s in, and the opening seconds must stay visible.
        while (!cancelled && Time.unscaledTime < deadline &&
               !bootstrap.IsInitialized && !bootstrap.InitializationFailed)
        {
            yield return null;
        }

        if (cancelled) yield break;

        // 3) If initialization failed, keep the curtain up so the player cannot
        //    interact with a broken board (Esc still exits via SceneLoader).
        if (bootstrap.InitializationFailed)
        {
            Debug.LogError(
                "GameplayLoadingPanelController keeps the curtain up because bootstrap " +
                "initialization failed: " + bootstrap.InitializationException?.Message,
                this);
            yield break;
        }

        BindMatchResultTracking();
        RefreshRateTexts();

        // Fast initialization must not cut the entrance motion short.
        while (!cancelled && Time.unscaledTime < deadline &&
               entranceAnimator != null && !entranceAnimator.IsEntranceComplete)
        {
            yield return null;
        }

        // Once the entrance finishes, hold the completed composition for exactly
        // one second. There is intentionally no separate minimum display timer.
        // Realtime keeps this presentation stable if gameplay time is paused.
        yield return new WaitForSecondsRealtime(1f);

        if (cancelled) yield break;
        if (hidden) yield break;

        // 4) Board is initialized: lift the curtain immediately so the Ready
        //    prompt and the whole pre-wave opening (~6s) are visible.
        Hide();
    }

    private void Hide()
    {
        hidden = true;
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    private void ResolvePresentationTexts()
    {
        if (loadingPanel == null)
        {
            return;
        }

        Transform root = loadingPanel.transform;
        playerRateText = (root.FindUi("BG/MyPart/MyItem/Rate/RateText") ??
                          root.FindUi("MyPart/MyItem/Rate/RateText") ??
                          root.FindUi("BG/MyPart/MyItem/Text (TMP)/RateText") ??
                          root.FindUi("MyPart/MyItem/Text (TMP)/RateText") ??
                          root.FindUi("BG/PlayerPart/PlayerItem/Text (TMP)/RateText"))?
            .GetComponent<TMP_Text>();
        aiRateText = (root.FindUi("BG/EnemyPart/EnemyItem/Rate/RateText") ??
                      root.FindUi("EnemyPart/EnemyItem/Rate/RateText") ??
                      root.FindUi("BG/EnemyPart/EnemyItem/Text (TMP)/RateText") ??
                      root.FindUi("EnemyPart/EnemyItem/Text (TMP)/RateText"))?
            .GetComponent<TMP_Text>();
        playerRankText = (root.FindUi("BG/MyPart/MyItem/TextBg/Rank") ??
                          root.FindUi("MyPart/MyItem/TextBg/Rank"))?
            .GetComponent<TMP_Text>();
        aiRankText = (root.FindUi("BG/EnemyPart/EnemyItem/TextBg/Rank") ??
                      root.FindUi("EnemyPart/EnemyItem/TextBg/Rank"))?
            .GetComponent<TMP_Text>();

        if (playerRateText == null || aiRateText == null)
        {
            Debug.LogWarning(
                "Gameplay LoadingPanel requires player and EnemyPart RateText nodes.",
                loadingPanel);
        }
    }

    private async void RefreshRankTexts()
    {
        // Rank labels only exist in the V2 composition. V1 therefore keeps its
        // existing presentation without performing an extra rank request.
        if (playerRankText == null && aiRankText == null)
        {
            return;
        }

        AuthSession session = ClientCompositionRoot.Current.AuthSession.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            ApplyRankTexts(RankProgressionRules.Calculate(0));
            return;
        }

        try
        {
            PlayerRankState rank = await ClientCompositionRoot.Current.Rank.GetRankAsync(
                session.PlayerId,
                lifetimeCancellation.Token);
            if (!cancelled && rank != null)
            {
                ApplyRankTexts(rank);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Scene was unloaded while the rank request was in flight.
        }
        catch (System.Exception exception)
        {
            if (!cancelled)
            {
                Debug.LogWarning(
                    "Gameplay LoadingPanel could not load rank data: " + exception.Message,
                    loadingPanel);
                ApplyRankTexts(RankProgressionRules.Calculate(0));
            }
        }
    }

    private void ApplyRankTexts(PlayerRankState playerRank)
    {
        string displayName = RankProgressionRules.GetDisplayName(playerRank);
        if (playerRankText != null)
        {
            playerRankText.text = displayName;
        }

        // Gameplay currently matches the AI profile from the player's rank band.
        // Until the run contract exposes a separate AI rank snapshot, present the
        // matched rank rather than inventing an unrelated opponent rank.
        if (aiRankText != null)
        {
            aiRankText.text = displayName;
        }
    }

    private void RefreshRateTexts()
    {
        AuthSession session = ClientCompositionRoot.Current.AuthSession.Current;
        trackedPlayerId = session != null && !string.IsNullOrWhiteSpace(session.PlayerId)
            ? session.PlayerId
            : string.Empty;
        GameplayWinRateProfile.Snapshot snapshot =
            GameplayWinRateProfile.Load(trackedPlayerId);

        if (playerRateText != null)
        {
            playerRateText.text = GameplayWinRateProfile.FormatRate(snapshot.PlayerRatePercent);
        }
        if (aiRateText != null)
        {
            aiRateText.text = GameplayWinRateProfile.FormatRate(snapshot.AiRatePercent);
        }
    }

    private void BindMatchResultTracking()
    {
        MatchController match = bootstrap != null ? bootstrap.Match : null;
        if (ReferenceEquals(trackedMatch, match))
        {
            return;
        }

        if (trackedMatch != null)
        {
            trackedMatch.StateChanged -= HandleTrackedMatchStateChanged;
        }

        trackedMatch = match;
        matchResultRecorded = false;
        if (trackedMatch != null)
        {
            trackedMatch.StateChanged += HandleTrackedMatchStateChanged;
        }
    }

    private void HandleTrackedMatchStateChanged(MatchState state)
    {
        if (matchResultRecorded ||
            (state != MatchState.Victory && state != MatchState.Defeat) ||
            string.IsNullOrWhiteSpace(trackedPlayerId))
        {
            return;
        }

        matchResultRecorded = true;
        GameplayWinRateProfile.Snapshot snapshot =
            GameplayWinRateProfile.RecordCompletedMatch(
                trackedPlayerId,
                state == MatchState.Victory);
        if (playerRateText != null)
        {
            playerRateText.text = GameplayWinRateProfile.FormatRate(snapshot.PlayerRatePercent);
        }
        if (aiRateText != null)
        {
            aiRateText.text = GameplayWinRateProfile.FormatRate(snapshot.AiRatePercent);
        }
    }
}
