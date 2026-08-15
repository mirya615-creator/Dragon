using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps the Bootstrap loading UI alive and owns every scene transition.
/// </summary>
public sealed class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Image progressFill;

    [Header("Bootstrap")]
    [SerializeField] private string firstScene = "Login";

    private bool isLoading;

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

        if (progressFill != null)
        {
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;
        }
    }

    private void Start()
    {
        // Bootstrap is the only entry scene. Its loading panel immediately
        // displays the asynchronous load progress of Login.
        if (SceneManager.GetActiveScene().name == "Bootstrap")
        {
            LoadSceneAsync(firstScene);
        }
        else
        {
            BindSceneButton(SceneManager.GetActiveScene().name);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    /// <summary>
    /// Starts a guarded asynchronous scene transition.
    /// This public method can also be selected from a Button OnClick event.
    /// </summary>
    public void LoadSceneAsync(string sceneName)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;
        SetLoadingVisible(true);
        SetProgress(0f);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"Unable to load scene '{sceneName}'. Make sure it is in Build Settings.");
            SetLoadingVisible(false);
            isLoading = false;
            yield break;
        }

        // Unity stops at 0.9 until scene activation is permitted.
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            SetProgress(operation.progress / 0.9f);
            yield return null;
        }

        SetProgress(1f);
        yield return null; // Ensure the completed bar is rendered for one frame.

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Bootstrap")
        {
            return;
        }

        BindSceneButton(scene.name);
        SetLoadingVisible(false);
        isLoading = false;
    }

    private void BindSceneButton(string sceneName)
    {
        switch (sceneName)
        {
            case "Game":
                BindButton("ReturnBtn", "Main");
                break;
        }
    }

    private void BindButton(string buttonObjectName, string targetScene)
    {
        GameObject buttonObject = GameObject.Find(buttonObjectName);
        Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;

        if (button == null)
        {
            Debug.LogError($"Button '{buttonObjectName}' was not found in scene '{SceneManager.GetActiveScene().name}'.");
            return;
        }

        button.onClick.AddListener(() => LoadSceneAsync(targetScene));
    }

    private void SetLoadingVisible(bool visible)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(visible);
        }
    }

    private void SetProgress(float value)
    {
        if (progressFill != null)
        {
            progressFill.fillAmount = Mathf.Clamp01(value);
        }
    }
}
