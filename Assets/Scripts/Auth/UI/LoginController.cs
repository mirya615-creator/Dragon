using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the Login scene UI flow. All references are resolved from the established hierarchy.
/// </summary>
public sealed class LoginController : MonoBehaviour
{
    private enum EmailLoginMode
    {
        Password,
        VerificationCode
    }

    private GameObject loginPanel;
    private GameObject signUpPanel;
    private TMP_InputField loginEmailInput;
    private TMP_InputField loginPasswordInput;
    private TMP_InputField signUpEmailInput;
    private TMP_InputField signUpPasswordInput;
    private TMP_InputField signUpCodeInput;
    private TMP_Text errorText;
    private Button openSignUpButton;
    private Button signUpButton;
    private Button cancelSignUpButton;
    private Button loginButton;
    private Button guestLoginButton;
    private Button verificationCodeButton;
    private Button passwordModeButton;
    private Button signUpVerificationCodeButton;
    private TMP_Text verificationCodeButtonLabel;
    private TMP_Text signUpVerificationCodeButtonLabel;
    private IAuthGateway authGateway;
    private GuestIdentityService guestIdentityService;
    private CancellationTokenSource lifetimeCancellation;
    private bool requestInProgress;
    private bool codeCooldownActive;
    private string verificationEmail = string.Empty;
    private string verificationCodeButtonDefaultText = string.Empty;
    private Coroutine codeCooldownCoroutine;
    private Coroutine signUpCodeCooldownCoroutine;
    private bool signUpCodeCooldownActive;
    private bool signUpCodeControlsAvailable;
    private string signUpVerificationEmail = string.Empty;
    private string signUpVerificationCodeButtonDefaultText = string.Empty;
    private EmailLoginMode loginMode = EmailLoginMode.Password;

    private void Awake()
    {
        lifetimeCancellation = new CancellationTokenSource();
        authGateway = new LocalAuthGateway();
        guestIdentityService = new GuestIdentityService();

        if (!ResolveView())
        {
            enabled = false;
            return;
        }

        ConfigureInputs();
        BindButtons();
        ShowLoginPanel();
    }

    private void OnDestroy()
    {
        if (openSignUpButton != null) openSignUpButton.onClick.RemoveListener(ShowSignUpPanel);
        if (signUpButton != null) signUpButton.onClick.RemoveListener(OnSignUpClicked);
        if (cancelSignUpButton != null) cancelSignUpButton.onClick.RemoveListener(ShowLoginPanel);
        if (loginButton != null) loginButton.onClick.RemoveListener(OnLoginClicked);
        if (guestLoginButton != null) guestLoginButton.onClick.RemoveListener(OnGuestLoginClicked);
        if (verificationCodeButton != null) verificationCodeButton.onClick.RemoveListener(OnSendCodeClicked);
        if (passwordModeButton != null) passwordModeButton.onClick.RemoveListener(SetPasswordMode);
        if (signUpVerificationCodeButton != null)
        {
            signUpVerificationCodeButton.onClick.RemoveListener(OnSendSignUpCodeClicked);
        }
        if (codeCooldownCoroutine != null) StopCoroutine(codeCooldownCoroutine);
        if (signUpCodeCooldownCoroutine != null) StopCoroutine(signUpCodeCooldownCoroutine);

        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
    }

    private bool ResolveView()
    {
        Transform mainPanel = FindInScene("Canvas/MainPanel");
        if (mainPanel == null)
        {
            Debug.LogError("LoginController could not find Canvas/MainPanel.");
            return false;
        }

        loginPanel = FindRequired(mainPanel, "LoginPanel")?.gameObject;
        signUpPanel = FindRequired(mainPanel, "SignUpPanel")?.gameObject;
        errorText = FindRequired(mainPanel, "ErrorText")?.GetComponent<TMP_Text>();

        Transform loginRoot = loginPanel != null ? loginPanel.transform : null;
        Transform signUpRoot = signUpPanel != null ? signUpPanel.transform : null;
        if (loginRoot == null || signUpRoot == null) return false;

        loginEmailInput = GetRequired<TMP_InputField>(loginRoot, "LoginEmailInput");
        loginPasswordInput = GetRequired<TMP_InputField>(loginRoot, "LoginPasswordInput");
        openSignUpButton = GetRequired<Button>(loginRoot, "SignUp");
        loginButton = GetRequired<Button>(loginRoot, "LoginBtn");
        guestLoginButton = GetRequired<Button>(loginRoot, "GuestLoginBtn");
        verificationCodeButton = GetRequired<Button>(loginRoot, "Vcode");
        passwordModeButton = GetRequired<Button>(loginRoot, "Pcode");
        verificationCodeButtonLabel = verificationCodeButton != null
            ? verificationCodeButton.GetComponentInChildren<TMP_Text>(true)
            : null;

        signUpEmailInput = GetRequired<TMP_InputField>(signUpRoot, "SignUpEmailInput");
        signUpPasswordInput = GetRequired<TMP_InputField>(signUpRoot, "SignUpPasswordInput");
        signUpButton = GetRequired<Button>(signUpRoot, "SignUpBtn");
        cancelSignUpButton = GetRequired<Button>(signUpRoot, "CancleSignUp");
        Transform signUpCodeObject = signUpRoot.Find("Vcodeinput");
        Transform signUpCodeButtonObject = signUpRoot.Find("SignVcode");
        signUpCodeInput = signUpCodeObject != null ? signUpCodeObject.GetComponent<TMP_InputField>() : null;
        signUpVerificationCodeButton = signUpCodeButtonObject != null
            ? signUpCodeButtonObject.GetComponent<Button>()
            : null;
        signUpVerificationCodeButtonLabel = signUpVerificationCodeButton != null
            ? signUpVerificationCodeButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        signUpCodeControlsAvailable = signUpCodeInput != null && signUpVerificationCodeButton != null &&
                                      signUpVerificationCodeButtonLabel != null;

        bool complete = loginEmailInput != null && loginPasswordInput != null &&
                        signUpEmailInput != null && signUpPasswordInput != null &&
                        errorText != null && openSignUpButton != null && signUpButton != null &&
                        cancelSignUpButton != null && loginButton != null && guestLoginButton != null &&
                        verificationCodeButton != null && passwordModeButton != null &&
                        verificationCodeButtonLabel != null;
        if (!complete) Debug.LogError("LoginController is missing one or more required UI components.");
        return complete;
    }

    private void ConfigureInputs()
    {
        loginEmailInput.contentType = TMP_InputField.ContentType.EmailAddress;
        signUpEmailInput.contentType = TMP_InputField.ContentType.EmailAddress;
        loginEmailInput.characterLimit = 254;
        signUpEmailInput.characterLimit = 254;

        loginPasswordInput.contentType = TMP_InputField.ContentType.Password;
        signUpPasswordInput.contentType = TMP_InputField.ContentType.Password;
        loginPasswordInput.characterLimit = 72;
        signUpPasswordInput.characterLimit = 72;
        if (signUpCodeControlsAvailable)
        {
            signUpCodeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            signUpCodeInput.characterLimit = 6;
            signUpCodeInput.ForceLabelUpdate();
        }
        loginPasswordInput.ForceLabelUpdate();
        signUpPasswordInput.ForceLabelUpdate();
    }

    private void BindButtons()
    {
        openSignUpButton.onClick.AddListener(ShowSignUpPanel);
        signUpButton.onClick.AddListener(OnSignUpClicked);
        cancelSignUpButton.onClick.AddListener(ShowLoginPanel);
        loginButton.onClick.AddListener(OnLoginClicked);
        guestLoginButton.onClick.AddListener(OnGuestLoginClicked);
        verificationCodeButton.onClick.AddListener(OnSendCodeClicked);
        passwordModeButton.onClick.AddListener(SetPasswordMode);
        verificationCodeButtonDefaultText = verificationCodeButtonLabel.text;
        if (signUpCodeControlsAvailable)
        {
            signUpVerificationCodeButton.onClick.AddListener(OnSendSignUpCodeClicked);
            signUpVerificationCodeButtonDefaultText = signUpVerificationCodeButtonLabel.text;
        }
    }

    private void ShowSignUpPanel()
    {
        if (requestInProgress) return;
        ClearError();
        loginPasswordInput.text = string.Empty;
        signUpVerificationEmail = string.Empty;
        if (signUpCodeInput != null) signUpCodeInput.text = string.Empty;
        loginPanel.SetActive(false);
        signUpPanel.SetActive(true);
        signUpEmailInput.ActivateInputField();
    }

    private void ShowLoginPanel()
    {
        if (requestInProgress) return;
        ClearError();
        signUpPasswordInput.text = string.Empty;
        if (signUpCodeInput != null) signUpCodeInput.text = string.Empty;
        signUpVerificationEmail = string.Empty;
        signUpPanel.SetActive(false);
        loginPanel.SetActive(true);
        SetPasswordMode();
        loginEmailInput.ActivateInputField();
    }

    private async void OnSignUpClicked()
    {
        if (requestInProgress) return;

        try
        {
            if (!signUpCodeControlsAvailable)
            {
                throw new AuthException("UI_NOT_READY", "Save Vcodeinput and SignVcode in the Login scene.");
            }
            string email = AuthInputValidator.NormalizeEmail(signUpEmailInput.text);
            AuthInputValidator.ValidatePassword(signUpPasswordInput.text);
            ValidateVerificationCode(signUpCodeInput.text);
            if (!string.Equals(email, signUpVerificationEmail, StringComparison.Ordinal))
            {
                throw new AuthException("EMAIL_CHANGED", "Email changed. Request a new code.");
            }
            SetBusy(true);
            ClearError();

            await authGateway.RegisterWithEmailCodeAsync(
                email,
                signUpPasswordInput.text,
                signUpCodeInput.text,
                guestIdentityService.CreateDeviceInfo(),
                lifetimeCancellation.Token);

            signUpPasswordInput.text = string.Empty;
            signUpCodeInput.text = string.Empty;
            signUpVerificationEmail = string.Empty;
            loginEmailInput.text = email;
            signUpPanel.SetActive(false);
            loginPanel.SetActive(true);
            SetPasswordMode();
            ShowMessage("Registration successful. Please log in.");
            loginPasswordInput.ActivateInputField();
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
            ShowMessage("Registration failed. Please try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSendSignUpCodeClicked()
    {
        if (requestInProgress || signUpCodeCooldownActive || !signUpCodeControlsAvailable) return;

        try
        {
            string email = AuthInputValidator.NormalizeEmail(signUpEmailInput.text);
            AuthInputValidator.ValidatePassword(signUpPasswordInput.text);
            SetBusy(true);
            ClearError();
            await authGateway.SendEmailCodeAsync(
                email, EmailCodePurpose.Register, lifetimeCancellation.Token);
            signUpVerificationEmail = email;
            signUpCodeInput.text = string.Empty;
            signUpCodeInput.ActivateInputField();
            StartSignUpCodeCooldown();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ShowMessage("Code sent. Check the Unity Console.");
#else
            ShowMessage("Code sent. Check your email.");
#endif
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
            ShowMessage("Unable to send code. Please try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnLoginClicked()
    {
        if (requestInProgress) return;

        try
        {
            string email = AuthInputValidator.NormalizeEmail(loginEmailInput.text);
            if (loginMode == EmailLoginMode.Password)
            {
                AuthInputValidator.ValidatePassword(loginPasswordInput.text);
            }
            else
            {
                ValidateVerificationCode(loginPasswordInput.text);
                if (!string.Equals(email, verificationEmail, StringComparison.Ordinal))
                {
                    throw new AuthException("EMAIL_CHANGED", "Email changed. Request a new code.");
                }
            }
            SetBusy(true);
            ClearError();

            AuthSession session;
            if (loginMode == EmailLoginMode.Password)
            {
                session = await authGateway.LoginAsync(
                    email, loginPasswordInput.text, lifetimeCancellation.Token);
            }
            else
            {
                session = await authGateway.VerifyEmailCodeAsync(
                    email,
                    loginPasswordInput.text,
                    guestIdentityService.CreateDeviceInfo(),
                    lifetimeCancellation.Token);
            }
            AuthSessionStore.Set(session);
            loginPasswordInput.text = string.Empty;

            if (SceneLoader.Instance == null)
            {
                throw new InvalidOperationException("SceneLoader is not available.");
            }

            SceneLoader.Instance.LoadSceneAsync("Main");
        }
        catch (OperationCanceledException)
        {
        }
        catch (AuthException exception)
        {
            loginPasswordInput.text = string.Empty;
            ShowMessage(exception.Message);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowMessage("Login failed. Please try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSendCodeClicked()
    {
        if (requestInProgress || codeCooldownActive) return;

        try
        {
            string email = AuthInputValidator.NormalizeEmail(loginEmailInput.text);
            SetBusy(true);
            ClearError();
            await authGateway.SendEmailCodeAsync(
                email, EmailCodePurpose.Login, lifetimeCancellation.Token);
            SetVerificationCodeMode(email);
            StartCodeCooldown();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ShowMessage("Code sent. Check the Unity Console.");
#else
            ShowMessage("Code sent. Check your email.");
#endif
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
            ShowMessage("Unable to send code. Please try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetPasswordMode()
    {
        if (requestInProgress) return;
        loginMode = EmailLoginMode.Password;
        verificationEmail = string.Empty;
        loginPasswordInput.text = string.Empty;
        loginPasswordInput.contentType = TMP_InputField.ContentType.Password;
        loginPasswordInput.characterLimit = 72;
        SetInputPlaceholder(loginPasswordInput, "PASSWORD...");
        loginPasswordInput.ForceLabelUpdate();
        ClearError();
    }

    private void SetVerificationCodeMode(string email)
    {
        loginMode = EmailLoginMode.VerificationCode;
        verificationEmail = email;
        loginPasswordInput.text = string.Empty;
        loginPasswordInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        loginPasswordInput.characterLimit = 6;
        SetInputPlaceholder(loginPasswordInput, "Verification code");
        loginPasswordInput.ForceLabelUpdate();
        loginPasswordInput.ActivateInputField();
    }

    private void StartCodeCooldown()
    {
        if (codeCooldownCoroutine != null) StopCoroutine(codeCooldownCoroutine);
        codeCooldownCoroutine = StartCoroutine(CodeCooldownRoutine(60));
    }

    private IEnumerator CodeCooldownRoutine(int seconds)
    {
        codeCooldownActive = true;
        if (verificationCodeButton != null) verificationCodeButton.interactable = false;
        for (int remaining = seconds; remaining > 0; remaining--)
        {
            if (verificationCodeButtonLabel != null) verificationCodeButtonLabel.text = $"{remaining}s";
            yield return new WaitForSecondsRealtime(1f);
        }
        codeCooldownActive = false;
        codeCooldownCoroutine = null;
        if (verificationCodeButtonLabel != null)
        {
            verificationCodeButtonLabel.text = verificationCodeButtonDefaultText;
        }
        if (verificationCodeButton != null) verificationCodeButton.interactable = !requestInProgress;
    }

    private void StartSignUpCodeCooldown()
    {
        if (signUpCodeCooldownCoroutine != null) StopCoroutine(signUpCodeCooldownCoroutine);
        signUpCodeCooldownCoroutine = StartCoroutine(SignUpCodeCooldownRoutine(60));
    }

    private IEnumerator SignUpCodeCooldownRoutine(int seconds)
    {
        signUpCodeCooldownActive = true;
        if (signUpVerificationCodeButton != null) signUpVerificationCodeButton.interactable = false;
        for (int remaining = seconds; remaining > 0; remaining--)
        {
            if (signUpVerificationCodeButtonLabel != null)
            {
                signUpVerificationCodeButtonLabel.text = $"{remaining}s";
            }
            yield return new WaitForSecondsRealtime(1f);
        }
        signUpCodeCooldownActive = false;
        signUpCodeCooldownCoroutine = null;
        if (signUpVerificationCodeButtonLabel != null)
        {
            signUpVerificationCodeButtonLabel.text = signUpVerificationCodeButtonDefaultText;
        }
        if (signUpVerificationCodeButton != null)
        {
            signUpVerificationCodeButton.interactable = !requestInProgress;
        }
    }

    private static void ValidateVerificationCode(string code)
    {
        if (code == null || code.Length != 6)
        {
            throw new AuthException("INVALID_CODE", "Enter the 6-digit verification code.");
        }
        for (int index = 0; index < code.Length; index++)
        {
            if (code[index] < '0' || code[index] > '9')
            {
                throw new AuthException("INVALID_CODE", "Enter the 6-digit verification code.");
            }
        }
    }

    private static void SetInputPlaceholder(TMP_InputField input, string value)
    {
        TMP_Text placeholder = input != null ? input.placeholder as TMP_Text : null;
        if (placeholder != null) placeholder.text = value;
    }

    private async void OnGuestLoginClicked()
    {
        if (requestInProgress) return;

        try
        {
            SetBusy(true);
            ClearError();
            GuestLoginRequest request = guestIdentityService.CreateRequest();
            AuthSession session = await authGateway.GuestLoginAsync(request, lifetimeCancellation.Token);
            AuthSessionStore.Set(session);

            if (SceneLoader.Instance == null)
            {
                throw new InvalidOperationException("SceneLoader is not available.");
            }

            SceneLoader.Instance.LoadSceneAsync("Main");
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

    private void SetBusy(bool busy)
    {
        requestInProgress = busy;
        if (openSignUpButton != null) openSignUpButton.interactable = !busy;
        if (signUpButton != null) signUpButton.interactable = !busy;
        if (cancelSignUpButton != null) cancelSignUpButton.interactable = !busy;
        if (loginButton != null) loginButton.interactable = !busy;
        if (guestLoginButton != null) guestLoginButton.interactable = !busy;
        if (verificationCodeButton != null)
        {
            verificationCodeButton.interactable = !busy && !codeCooldownActive;
        }
        if (passwordModeButton != null) passwordModeButton.interactable = !busy;
        if (signUpVerificationCodeButton != null)
        {
            signUpVerificationCodeButton.interactable = !busy && !signUpCodeCooldownActive;
        }
    }

    private void ClearError()
    {
        ShowMessage(string.Empty);
    }

    private void ShowMessage(string message)
    {
        if (errorText != null) errorText.text = message;
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
