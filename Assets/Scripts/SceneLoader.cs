using System.Collections;
using DragonBound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent asynchronous scene loader. It is created automatically before Login starts.
/// </summary>
public sealed class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Image progressFill;

    private bool isLoading;
    private GameObject sceneLoadingPanel;
    private Image sceneProgressFill;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null || FindObjectOfType<SceneLoader>() != null)
        {
            return;
        }

        new GameObject("SceneLoader").AddComponent<SceneLoader>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        ConfigureProgressFill(progressFill);
    }

    private void Start()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        ResolveSceneLoadingUi(activeScene);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Update()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        if ((activeScene == "Greybox_Main" || activeScene == "HeroSlice_Main") &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            LoadSceneAsync("Main");
        }
    }

    /// <summary>
    /// Starts a guarded asynchronous scene transition.
    /// This public method can also be selected from a Button OnClick event.
    /// </summary>
    public void LoadSceneAsync(string sceneName)
    {
        StartSceneLoad(sceneName, true);
    }

    /// <summary>
    /// Starts a guarded asynchronous transition without displaying the source scene's loading UI.
    /// Login uses this after its one-time startup loading presentation has completed.
    /// </summary>
    public void LoadSceneAsyncWithoutLoadingUi(string sceneName)
    {
        StartSceneLoad(sceneName, false);
    }

    private void StartSceneLoad(string sceneName, bool showLoadingUi)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName, showLoadingUi));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, bool showLoadingUi)
    {
        isLoading = true;
        if (showLoadingUi)
        {
            ResolveSceneLoadingUi(SceneManager.GetActiveScene());
            SetLoadingVisible(true);
            SetProgress(0f);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"Unable to load scene '{sceneName}'. Make sure it is in Build Settings.");
            if (showLoadingUi)
            {
                SetLoadingVisible(false);
            }
            isLoading = false;
            yield break;
        }

        // Unity stops at 0.9 until scene activation is permitted.
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            if (showLoadingUi)
            {
                SetProgress(operation.progress / 0.9f);
            }
            yield return null;
        }

        if (showLoadingUi)
        {
            SetProgress(1f);
            yield return null; // Ensure the completed bar is rendered for one frame.
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        ResolveSceneLoadingUi(scene);
        if (isLoading)
        {
            SetLoadingVisible(false);
        }
        isLoading = false;
    }

    private void SetLoadingVisible(bool visible)
    {
        GameObject activePanel = sceneLoadingPanel != null ? sceneLoadingPanel : loadingPanel;
        if (activePanel != null)
        {
            activePanel.SetActive(visible);
        }
    }

    private void SetProgress(float value)
    {
        Image activeFill = sceneProgressFill != null ? sceneProgressFill : progressFill;
        if (activeFill != null)
        {
            float progress = Mathf.Clamp01(value);
            activeFill.fillAmount = progress;
            activeFill.rectTransform.anchorMax = new Vector2(progress, 1f);
        }
    }

    private void ResolveSceneLoadingUi(Scene scene)
    {
        sceneLoadingPanel = null;
        sceneProgressFill = null;

        if (!scene.IsValid() || !scene.isLoaded || scene.name != "Login")
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform loadingTransform = root.transform.FindUi(
                "SafeArea/MainPanel/LoginPanel/LoadingImg");
            if (loadingTransform == null)
            {
                continue;
            }

            Transform fillTransform = loadingTransform.FindUi("FillImg");
            Image fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (fill == null)
            {
                Debug.LogError("Login loading UI requires LoadingImg/FillImg with an Image component.");
                return;
            }

            sceneLoadingPanel = loadingTransform.gameObject;
            sceneProgressFill = fill;
            ConfigureProgressFill(sceneProgressFill);
            return;
        }

        Debug.LogError(
            "Login scene is missing Canvas/SafeArea/MainPanel/LoginPanel/LoadingImg.");
    }

    private static void ConfigureProgressFill(Image fill)
    {
        if (fill == null)
        {
            return;
        }

        fill.type = Image.Type.Filled ;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
    }
}
