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
            try
            {
                await services.GoogleOAuth.ClearCredentialStateAsync(CancellationToken.None);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Unable to clear Google credential state: " + exception.Message);
            }
            services.AuthSession.Clear();
            EnsureCreated();
            instance.RedirectToLogin();
        }
    }

    public static async Task LinkCurrentPlayerToGoogleAsync(
        CancellationToken cancellationToken = default)
    {
        IClientServices services = ClientCompositionRoot.Current;
        AuthSession session = services.AuthSession.Current;
        if (session == null || !services.AuthSession.IsValid(session))
            throw new AuthException("SESSION_MISSING", "Sign in before linking a Google account.");
        if (!session.IsGuest)
            throw new AuthException("ACCOUNT_ALREADY_LINKED", "This player is already linked.");

        string originalPlayerId = session.PlayerId;
        PendingGoogleIdentity identity = null;
        try
        {
            identity = await services.GoogleOAuth.SignInAsync(cancellationToken);
            if (identity == null || string.IsNullOrWhiteSpace(identity.IdToken))
                throw new AuthException("INVALID_GOOGLE_TOKEN", "Google did not return an ID token.");

            await services.Auth.LinkGoogleAsync(
                identity.IdToken,
                services.GuestIdentity.CreateDeviceInfo(),
                cancellationToken);

            AuthSession current = services.AuthSession.Current;
            if (current == null ||
                !string.Equals(current.PlayerId, originalPlayerId, System.StringComparison.Ordinal))
                throw new AuthException(
                    "PLAYER_ID_MISMATCH",
                    "Google linking changed the active player unexpectedly.");

            current.IsGuest = false;
            current.IsNewPlayer = false;
            services.AuthSession.Set(current);
        }
        finally
        {
            if (identity != null && identity.OwnsAvatarSprite && identity.AvatarSprite != null)
            {
                Texture2D texture = identity.AvatarSprite.texture;
                Destroy(identity.AvatarSprite);
                if (texture != null) Destroy(texture);
            }
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
