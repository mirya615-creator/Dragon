using System;
using System.Collections;
using System.Threading;
using DragonBound.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the Google and guest login flow for the Login scene.
/// </summary>
public sealed class LoginController : MonoBehaviour
{
    private const float StartupLoadingDurationSeconds = 3f;
    private const float LoginTipDurationSeconds = 3f;
    private const string TipTextPrefabPath = "prefabs/TipText";

    private GameObject loginPanel;
    private GameObject signUpPanel;
    private GameObject googleConfirmPanel;
    private GameObject startupLoadingImage;
    private TipTextController googleConfirmationTip;
    private TMP_Text googleAccountText;
    private Image googleAvatarImage;
    private Image startupLoadingFill;
    private RectTransform startupLoadingFillRect;
    private Button guestLoginButton;
    private Button googleButton;
    private Button googleConfirmButton;
    private Button googleCancelButton;
    private IAuthGateway authGateway;
    private IAuthSessionStore authSessionStore;
    private IGoogleOAuthProvider googleOAuthProvider;
    private IGuestIdentityProvider guestIdentityService;
    private CancellationTokenSource lifetimeCancellation;
    private PendingGoogleIdentity pendingGoogleIdentity;
    private Sprite defaultGoogleAvatarSprite;
    private Coroutine startupLoadingCoroutine;
    private bool requestInProgress;

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        lifetimeCancellation = new CancellationTokenSource();
        authGateway = services.Auth;
        authSessionStore = services.AuthSession;
        googleOAuthProvider = services.GoogleOAuth;
        guestIdentityService = services.GuestIdentity;

        if (!ResolveView())
        {
            enabled = false;
            return;
        }

        DisableRetiredEmailLoginUi();
        BindButtons();
        ShowLoginPanel();
        PrepareStartupLoading();
    }

    private void OnDestroy()
    {
        if (guestLoginButton != null) guestLoginButton.onClick.RemoveListener(OnGuestLoginClicked);
        if (googleButton != null) googleButton.onClick.RemoveListener(OnGoogleClicked);
        if (googleConfirmButton != null) googleConfirmButton.onClick.RemoveListener(OnGoogleConfirmClicked);
        if (googleCancelButton != null) googleCancelButton.onClick.RemoveListener(CancelGoogleConfirmation);
        if (startupLoadingCoroutine != null) StopCoroutine(startupLoadingCoroutine);
        HideAllTips();

        googleOAuthProvider?.CancelPendingSignIn();
        DisposePendingGoogleIdentity();
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }

    private bool ResolveView()
    {
        Transform mainPanel = FindInScene("Canvas/SafeArea/MainPanel");
        if (mainPanel == null)
        {
            Debug.LogError("LoginController could not find Canvas/SafeArea/MainPanel.");
            return false;
        }

        loginPanel = FindRequired(mainPanel, "LoginPanel")?.gameObject;
        signUpPanel = mainPanel.Find("SignUpPanel")?.gameObject;
        googleConfirmPanel = FindRequired(mainPanel, "GoogleConfirmPanel")?.gameObject;
        Transform loginRoot = loginPanel != null ? loginPanel.transform : null;
        guestLoginButton = GetRequired<Button>(loginRoot, "GuestLoginBtn");
        googleButton = GetRequired<Button>(loginRoot, "GoogleBtn");
        Transform loadingRoot = FindRequired(loginRoot, "LoadingImg");
        startupLoadingImage = loadingRoot != null ? loadingRoot.gameObject : null;
        startupLoadingFill = GetRequired<Image>(loadingRoot, "FillImg");
        startupLoadingFillRect = startupLoadingFill != null ? startupLoadingFill.rectTransform : null;

        Transform googleRoot = googleConfirmPanel != null ? googleConfirmPanel.transform : null;
        googleAvatarImage = GetRequired<Image>(googleRoot, "GoogleInform/GoogleImg");
        googleAccountText = GetRequired<TMP_Text>(googleRoot, "GoogleInform/GoogleAccount");
        googleConfirmationTip = ResolveConfirmationTip(googleRoot);
        googleConfirmButton = GetRequired<Button>(googleRoot, "ConfirmBtn");
        googleCancelButton = GetRequired<Button>(googleRoot, "CancleBtn");
        defaultGoogleAvatarSprite = googleAvatarImage != null ? googleAvatarImage.sprite : null;

        bool complete = loginPanel != null && googleConfirmPanel != null &&
                        guestLoginButton != null && googleButton != null &&
                        startupLoadingImage != null && startupLoadingFill != null &&
                        startupLoadingFillRect != null && googleAvatarImage != null &&
                        googleAccountText != null && googleConfirmationTip != null &&
                        googleConfirmButton != null && googleCancelButton != null;
        if (!complete) Debug.LogError("LoginController is missing Google or guest login UI components.");
        return complete;
    }

    private void DisableRetiredEmailLoginUi()
    {
        if (signUpPanel != null) signUpPanel.SetActive(false);
        SetChildActive(loginPanel.transform, "LoginEmailInput", false);
        SetChildActive(loginPanel.transform, "LoginPasswordInput", false);
        SetChildActive(loginPanel.transform, "SignUp", false);
        SetChildActive(loginPanel.transform, "LoginBtn", false);
        SetChildActive(loginPanel.transform, "Vcode", false);
        SetChildActive(loginPanel.transform, "Pcode", false);
    }

    private void BindButtons()
    {
        guestLoginButton.onClick.AddListener(OnGuestLoginClicked);
        googleButton.onClick.AddListener(OnGoogleClicked);
        googleConfirmButton.onClick.AddListener(OnGoogleConfirmClicked);
        googleCancelButton.onClick.AddListener(CancelGoogleConfirmation);
    }

    private void PrepareStartupLoading()
    {
        guestLoginButton.gameObject.SetActive(false);
        googleButton.gameObject.SetActive(false);
        startupLoadingImage.SetActive(true);
        startupLoadingFill.type = Image.Type.Simple;
        startupLoadingFillRect.anchorMin = new Vector2(0f, 0f);
        startupLoadingFillRect.anchorMax = new Vector2(0f, 1f);
        startupLoadingFillRect.pivot = new Vector2(0f, 0.5f);
        startupLoadingFillRect.anchoredPosition = Vector2.zero;
        startupLoadingFillRect.sizeDelta = Vector2.zero;
        SetStartupLoadingProgress(0f);
        SetBusy(true);
        startupLoadingCoroutine = StartCoroutine(RestoreSessionOrShowLogin());
    }

    private IEnumerator RestoreSessionOrShowLogin()
    {
        // Allow all BeforeSceneLoad and sceneLoaded initialization to finish first.
        yield return null;

        if (!AnalyticsConsentUiController.HasResolvedConsent)
        {
            AnalyticsConsentUiController.ShowRequiredPrompt();
            while (!AnalyticsConsentUiController.HasResolvedConsent)
            {
                yield return null;
            }
        }

        float elapsed = 0f;
        while (elapsed < StartupLoadingDurationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetStartupLoadingProgress(elapsed / StartupLoadingDurationSeconds);
            yield return null;
        }

        SetStartupLoadingProgress(1f);
        yield return null;
        startupLoadingImage.SetActive(false);

        if (authSessionStore.TryRestore(out AuthSession restored) &&
            authSessionStore.IsValid(restored))
        {
            PlayerAvatarProfile.GetOrCreateAvatarId(restored.PlayerId);
            startupLoadingCoroutine = null;
            LoadMainScene();
            yield break;
        }

        authSessionStore.Clear();
        guestLoginButton.gameObject.SetActive(true);
        googleButton.gameObject.SetActive(true);
        startupLoadingCoroutine = null;
        SetBusy(false);
    }

    private void SetStartupLoadingProgress(float value)
    {
        float progress = Mathf.Clamp01(value);
        startupLoadingFill.fillAmount = progress;
        startupLoadingFillRect.anchorMax = new Vector2(progress, 1f);
    }

    private async void OnGuestLoginClicked()
    {
        if (requestInProgress) return;

        bool sceneTransitionStarted = false;
        try
        {
            SetBusy(true);
            ClearError();
            GuestLoginRequest request = guestIdentityService.CreateRequest();
            AuthSession session = await authGateway.GuestLoginAsync(
                request,
                lifetimeCancellation.Token);
            PlayerAvatarProfile.GetOrCreateAvatarId(session.PlayerId);
            authSessionStore.Set(session);
            LoadMainScene();
            sceneTransitionStarted = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (AuthException exception)
        {
            ShowMessage(exception.Message);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowMessage("Guest login failed. Please try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnGoogleClicked()
    {
        if (requestInProgress) return;

        try
        {
            SetBusy(true);
            ClearError();
            DisposePendingGoogleIdentity();
            pendingGoogleIdentity = await googleOAuthProvider.SignInAsync(lifetimeCancellation.Token);
            if (pendingGoogleIdentity == null ||
                string.IsNullOrWhiteSpace(pendingGoogleIdentity.IdToken) ||
                string.IsNullOrWhiteSpace(pendingGoogleIdentity.Email) ||
                !pendingGoogleIdentity.EmailVerified)
            {
                DisposePendingGoogleIdentity();
                throw new AuthException("INVALID_CREDENTIALS", "Google authentication failed.");
            }

            googleAccountText.text = pendingGoogleIdentity.Email;
            googleAvatarImage.sprite = pendingGoogleIdentity.AvatarSprite != null
                ? pendingGoogleIdentity.AvatarSprite
                : defaultGoogleAvatarSprite;
            googleAvatarImage.preserveAspect = true;
            loginPanel.SetActive(false);
            googleConfirmPanel.SetActive(true);
            googleConfirmationTip.Show("Confirm to login ?", LoginTipDurationSeconds);
        }
        catch (OperationCanceledException)
        {
        }
        catch (AuthException exception)
        {
            ShowMessage(exception.Message);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowMessage("Google sign-in failed. Please try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnGoogleConfirmClicked()
    {
        if (requestInProgress || pendingGoogleIdentity == null) return;

        try
        {
            SetBusy(true);
            ClearError();
            AuthSession session = await authGateway.GoogleLoginAsync(
                pendingGoogleIdentity.IdToken,
                guestIdentityService.CreateDeviceInfo(),
                lifetimeCancellation.Token);
            PlayerAvatarProfile.GetOrCreateAvatarId(session.PlayerId);
            authSessionStore.Set(session);
            googleConfirmPanel.SetActive(false);
            loginPanel.SetActive(true);
            DisposePendingGoogleIdentity();
            LoadMainScene();
        }
        catch (OperationCanceledException)
        {
        }
        catch (AuthException exception)
        {
            ShowMessage(exception.Message);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowMessage("Google login failed. Please try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void CancelGoogleConfirmation()
    {
        if (requestInProgress) return;
        googleOAuthProvider.CancelPendingSignIn();
        DisposePendingGoogleIdentity();
        googleConfirmPanel.SetActive(false);
        loginPanel.SetActive(true);
        HideAllTips();
    }

    private void ShowLoginPanel()
    {
        if (signUpPanel != null) signUpPanel.SetActive(false);
        googleConfirmPanel.SetActive(false);
        loginPanel.SetActive(true);
        HideAllTips();
    }

    private void LoadMainScene()
    {
        if (SceneLoader.Instance == null)
        {
            throw new InvalidOperationException("SceneLoader is not available.");
        }
        HideAllTips();
        // LoadingImg belongs exclusively to the three-second startup presentation.
        // Authentication and the subsequent scene transition run without reopening it.
        startupLoadingImage.SetActive(false);
        SceneLoader.Instance.LoadSceneAsyncWithoutLoadingUi("Main");
    }

    private void DisposePendingGoogleIdentity()
    {
        if (pendingGoogleIdentity != null && pendingGoogleIdentity.OwnsAvatarSprite &&
            pendingGoogleIdentity.AvatarSprite != null)
        {
            Texture2D texture = pendingGoogleIdentity.AvatarSprite.texture;
            Destroy(pendingGoogleIdentity.AvatarSprite);
            if (texture != null) Destroy(texture);
        }
        pendingGoogleIdentity = null;
        if (googleAvatarImage != null) googleAvatarImage.sprite = defaultGoogleAvatarSprite;
        if (googleAccountText != null) googleAccountText.text = string.Empty;
    }

    private void SetBusy(bool busy)
    {
        requestInProgress = busy;
        if (guestLoginButton != null) guestLoginButton.interactable = !busy;
        if (googleButton != null) googleButton.interactable = !busy;
        if (googleConfirmButton != null) googleConfirmButton.interactable = !busy;
        if (googleCancelButton != null) googleCancelButton.interactable = !busy;
    }

    private void ClearError()
    {
        TipTextService.Hide();
        // Unity keeps a managed wrapper after a component is destroyed. The null
        // conditional operator only checks the wrapper and can therefore invoke
        // Hide on an already-destroyed TipTextController during scene teardown.
        if (googleConfirmationTip != null) googleConfirmationTip.Hide();
    }

    private void ShowMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            ClearError();
            return;
        }

        if (googleConfirmPanel != null && googleConfirmPanel.activeInHierarchy &&
            googleConfirmationTip != null)
        {
            TipTextService.Hide();
            googleConfirmationTip.Show(message, LoginTipDurationSeconds);
            return;
        }

        TipTextService.Show(message, LoginTipDurationSeconds);
    }

    private void HideAllTips()
    {
        if (googleConfirmationTip != null) googleConfirmationTip.Hide();
        TipTextService.Hide();
    }

    private static TipTextController ResolveConfirmationTip(Transform googleRoot)
    {
        var authoredTip = googleRoot != null ? googleRoot.Find("TipText") : null;
        var existingController = authoredTip != null
            ? authoredTip.GetComponent<TipTextController>()
            : null;
        if (existingController != null)
        {
            existingController.Hide();
            return existingController;
        }

        var prefab = Resources.Load<GameObject>(TipTextPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Login requires Resources/" + TipTextPrefabPath + ".prefab.");
            return null;
        }

        var instance = Instantiate(prefab, googleRoot, false);
        instance.name = "TipText";
        var instanceRect = instance.transform as RectTransform;
        var authoredRect = authoredTip as RectTransform;
        if (instanceRect != null && authoredRect != null)
        {
            instanceRect.position = authoredRect.position;
            instanceRect.SetSiblingIndex(authoredRect.GetSiblingIndex());
        }

        instance.SetActive(false);
        if (authoredTip != null)
        {
            Destroy(authoredTip.gameObject);
        }

        var controller = instance.GetComponent<TipTextController>();
        if (controller == null)
        {
            Debug.LogError("TipText.prefab requires TipTextController on its root.", instance);
        }
        return controller;
    }

    private static void SetChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        if (child != null) child.gameObject.SetActive(active);
    }

    private static Transform FindInScene(string path)
    {
        string[] segments = path.Split('/');
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!string.Equals(root.name, segments[0], StringComparison.Ordinal)) continue;
            Transform current = root.transform;
            for (int index = 1; index < segments.Length && current != null; index++)
            {
                current = current.Find(segments[index]);
            }
            return current;
        }
        return null;
    }

    private static Transform FindRequired(Transform parent, string childName)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        if (child == null) Debug.LogError($"Missing UI object '{childName}' under '{parent?.name}'.");
        return child;
    }

    private static T GetRequired<T>(Transform parent, string childName) where T : Component
    {
        Transform child = FindRequired(parent, childName);
        T component = child != null ? child.GetComponent<T>() : null;
        if (child != null && component == null) Debug.LogError($"'{childName}' is missing {typeof(T).Name}.");
        return component;
    }
}

/// <summary>
/// Installs the controller without requiring a serialized scene script reference.
/// </summary>
public static class LoginSceneInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Login" || UnityEngine.Object.FindObjectOfType<LoginController>() != null) return;
        new GameObject("LoginController").AddComponent<LoginController>();
    }
}
