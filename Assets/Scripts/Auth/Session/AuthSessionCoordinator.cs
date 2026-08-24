using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Guards authenticated scenes and provides one place for future Go unary authentication failures
/// to invalidate the local session and return to Login.
/// </summary>
public sealed class AuthSessionCoordinator : MonoBehaviour
{
    private const float ValidationIntervalSeconds = 1f;
    private static AuthSessionCoordinator instance;

    private IAuthSessionStore sessionStore;
    private float nextValidationTime;
    private bool redirectInProgress;

    public static void EnsureCreated()
    {
        if (instance != null || FindObjectOfType<AuthSessionCoordinator>() != null) return;
        new GameObject("AuthSessionCoordinator").AddComponent<AuthSessionCoordinator>();
    }

    public static void InvalidateAndReturnToLogin()
    {
        IAuthSessionStore store = ClientCompositionRoot.Current.AuthSession;
        store.Clear();
        EnsureCreated();
        instance.RedirectToLogin();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        sessionStore = ClientCompositionRoot.Current.AuthSession;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ValidateActiveScene(SceneManager.GetActiveScene());
    }

    private void Update()
    {
        if (Time.unscaledTime < nextValidationTime) return;
        nextValidationTime = Time.unscaledTime + ValidationIntervalSeconds;
        ValidateActiveScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Login") redirectInProgress = false;
        ValidateActiveScene(scene);
    }

    private void ValidateActiveScene(Scene scene)
    {
        if (!IsProtectedScene(scene.name) || sessionStore.IsValid(sessionStore.Current)) return;
        sessionStore.Clear();
        RedirectToLogin();
    }

    private void RedirectToLogin()
    {
        if (redirectInProgress || SceneManager.GetActiveScene().name == "Login") return;
        redirectInProgress = true;
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadSceneAsync("Login");
        }
        else
        {
            SceneManager.LoadSceneAsync("Login");
        }
    }

    private static bool IsProtectedScene(string sceneName)
    {
        return sceneName == "Main" || sceneName == "Greybox_Main" ||
               sceneName == "HeroSlice_Main";
    }
}
