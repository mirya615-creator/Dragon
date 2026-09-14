using System.Threading;
using System.Threading.Tasks;
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

    public static async Task LogoutAndReturnToLoginAsync(
        CancellationToken cancellationToken = default)
    {
        IClientServices services = ClientCompositionRoot.Current;
        try
        {
            await services.Auth.LogoutAsync(cancellationToken);
        }
        finally
        {
            services.AuthSession.Clear();
            EnsureCreated();
            instance.RedirectToLogin();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // A duplicate scene instance can still receive Start/Update until Destroy is applied
            // at the end of the frame. Disable it immediately so its uninitialized store is never used.
            enabled = false;
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
        if (instance != this || sessionStore == null) return;
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
