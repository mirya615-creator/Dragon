using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps the Login loading bar inside the device safe area on portrait screens.
/// The scene artwork remains full-screen; only interactive/loading content is fitted.
/// </summary>
public static class LoginSafeAreaLayoutAdapter
{
    private const string LoginSceneName = "Login";
    private const float HorizontalMargin = 40f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyToInitialScene()
    {
        Apply(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(scene);
    }

    private static void Apply(Scene scene)
    {
        if (!scene.IsValid() || scene.name != LoginSceneName)
        {
            return;
        }

        Canvas canvas = FindCanvas(scene);
        if (canvas == null)
        {
            Debug.LogWarning("Login safe-area adaptation requires Canvas.");
            return;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }

        Transform loadingTransform = canvas.transform.Find(
            "SafeArea/MainPanel/LoginPanel/LoadingImg");
        RectTransform loadingRect = loadingTransform as RectTransform;
        if (loadingRect == null)
        {
            Debug.LogWarning(
                "Login safe-area adaptation requires " +
                "Canvas/SafeArea/MainPanel/LoginPanel/LoadingImg.");
            return;
        }

        loadingRect.anchorMin = new Vector2(0f, loadingRect.anchorMin.y);
        loadingRect.anchorMax = new Vector2(1f, loadingRect.anchorMax.y);
        loadingRect.anchoredPosition = new Vector2(0f, loadingRect.anchoredPosition.y);
        loadingRect.sizeDelta = new Vector2(
            -(HorizontalMargin * 2f),
            loadingRect.sizeDelta.y);
    }

    private static Canvas FindCanvas(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index].name == "Canvas")
            {
                return roots[index].GetComponent<Canvas>();
            }
        }

        return null;
    }
}
