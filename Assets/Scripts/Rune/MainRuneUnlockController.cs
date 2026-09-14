using System;
using DragonBound.Presentation;
using System.Threading;
using System.Threading.Tasks;
using DragonBound.Runes;
using DragonBound.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainRuneUnlockController : MonoBehaviour
{
    private const string BagButtonPath = "ButtonNavigation/BagBtn";
    private const float TipDurationSeconds = 3f;
    private static readonly string LockedTip =
        $"Unlocks on Day {RuneFeatureGate.UnlockAccountDay}";

    private Button bagButton;
    private CanvasGroup bagCanvasGroup;
    private GameObject weaponPanel;
    private GameObject leaderPanel;
    private IAuthSessionStore authSessionStore;
    private IRuneProfileGateway runeGateway;
    private CancellationTokenSource lifetimeCancellation;
    private bool profileLoaded;
    private bool isUnlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForMainScene(scene);
    }

    private static void InstallForMainScene(Scene scene)
    {
        if (!string.Equals(scene.name, "Main", StringComparison.Ordinal)) return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform mainPanel = FindDescendant(roots[index].transform, "MainPanel");
            if (mainPanel == null || mainPanel.FindUi(BagButtonPath) == null) continue;
            if (mainPanel.GetComponent<MainRuneUnlockController>() == null)
            {
                mainPanel.gameObject.AddComponent<MainRuneUnlockController>();
            }
            return;
        }
    }

    private void Awake()
    {
        bagButton = transform.FindUi(BagButtonPath)?.GetComponent<Button>();
        weaponPanel = FindSceneObject(gameObject.scene, "WeaponPanel")?.gameObject;
        leaderPanel = FindSceneObject(gameObject.scene, "LeaderPanel")?.gameObject;

        if (bagButton == null || weaponPanel == null || leaderPanel == null)
        {
            Debug.LogError(
                "MainRuneUnlockController requires MainPanel/ButtonNavigation/BagBtn, " +
                "WeaponPanel and LeaderPanel.",
                this);
            enabled = false;
            return;
        }

        // The authored BagBtn used to open WeaponPanel directly. Replace that persistent action
        // so every entry path passes through the trusted AccountDay gate.
        bagButton.onClick = new Button.ButtonClickedEvent();
        bagButton.onClick.AddListener(HandleBagClicked);
        bagButton.interactable = true;
        bagCanvasGroup = bagButton.GetComponent<CanvasGroup>();
        if (bagCanvasGroup == null) bagCanvasGroup = bagButton.gameObject.AddComponent<CanvasGroup>();

        IClientServices services = ClientCompositionRoot.Current;
        authSessionStore = services.AuthSession;
        runeGateway = services.Runes;
        lifetimeCancellation = new CancellationTokenSource();
        ApplyGateVisual();
    }

    private void OnEnable()
    {
        LocalRuneProgressionSettings.AccountDayChanged += HandleAccountDayChanged;
        if (enabled) _ = RefreshGateAsync();
    }

    private void OnDisable()
    {
        LocalRuneProgressionSettings.AccountDayChanged -= HandleAccountDayChanged;
    }

    private void OnDestroy()
    {
        if (bagButton != null) bagButton.onClick.RemoveListener(HandleBagClicked);
        TipTextService.Hide();
        lifetimeCancellation?.Cancel();
        lifetimeCancellation?.Dispose();
        lifetimeCancellation = null;
    }

    private async Task RefreshGateAsync()
    {
        profileLoaded = false;
        isUnlocked = false;
        ApplyGateVisual();

        try
        {
            AuthSession session = authSessionStore?.Current;
            while ((session == null || string.IsNullOrWhiteSpace(session.PlayerId)) &&
                   lifetimeCancellation != null && !lifetimeCancellation.IsCancellationRequested)
            {
                await Task.Delay(100, lifetimeCancellation.Token);
                session = authSessionStore?.Current;
            }
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerId)) return;

            RuneProfile profile = await runeGateway.GetProfileAsync(
                session.PlayerId,
                lifetimeCancellation.Token);
            if (lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested) return;

            profileLoaded = profile != null;
            isUnlocked = profileLoaded &&
                         profile.AccountDay >= RuneFeatureGate.UnlockAccountDay;
            ApplyGateVisual();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to load Rune AccountDay: {exception.Message}", this);
        }
    }

    private void HandleAccountDayChanged()
    {
        if (isActiveAndEnabled) _ = RefreshGateAsync();
    }

    private void HandleBagClicked()
    {
        // Toggle: clicking BagBtn while the bag panel is open closes it.
        if (weaponPanel.activeSelf)
        {
            weaponPanel.SetActive(false);
            return;
        }

        if (!profileLoaded || !isUnlocked)
        {
            ShowTip(LockedTip);
            return;
        }

        if (leaderPanel.activeSelf)
        {
            leaderPanel.SetActive(false);
        }

        weaponPanel.SetActive(true);
    }

    private void ApplyGateVisual()
    {
        if (bagButton != null) bagButton.interactable = true;
        if (bagCanvasGroup != null) bagCanvasGroup.alpha = isUnlocked ? 1f : 0.55f;
    }

    private void ShowTip(string message)
    {
        TipTextService.Show(message, TipDurationSeconds);
    }

    private static Transform FindSceneObject(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform found = FindDescendant(roots[index].transform, objectName);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendant(root.GetChild(index), objectName);
            if (found != null) return found;
        }
        return null;
    }
}
