using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainRuneUnlockController : MonoBehaviour
{
    private const int UnlockAccountDay = 3;
    private const float TipDurationSeconds = 1.5f;
    private const string LockedTip = "Unlocks on Day 3";

    private Button bagButton;
    private CanvasGroup bagCanvasGroup;
    private TMP_Text tipText;
    private GameObject weaponPanel;
    private IAuthSessionStore authSessionStore;
    private IRuneProfileGateway runeGateway;
    private CancellationTokenSource lifetimeCancellation;
    private Coroutine tipCoroutine;
    private bool profileLoaded;
    private bool isUnlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForMainScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!string.Equals(scene.name, "Main", StringComparison.Ordinal)) return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform mainPanel = FindDescendant(roots[index].transform, "MainPanel");
            if (mainPanel == null || mainPanel.Find("BagBtn") == null) continue;
            if (mainPanel.GetComponent<MainRuneUnlockController>() == null)
            {
                mainPanel.gameObject.AddComponent<MainRuneUnlockController>();
            }
            return;
        }
    }

    private void Awake()
    {
        bagButton = transform.Find("BagBtn")?.GetComponent<Button>();
        tipText = transform.Find("TipText")?.GetComponent<TMP_Text>();
        weaponPanel = FindSceneObject("WeaponPanel")?.gameObject;

        if (bagButton == null || tipText == null || weaponPanel == null)
        {
            Debug.LogError(
                "MainRuneUnlockController requires MainPanel/BagBtn, MainPanel/TipText, " +
                "and WeaponPanel.",
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
        if (enabled) _ = RefreshGateAsync();
    }

    private void OnDestroy()
    {
        if (bagButton != null) bagButton.onClick.RemoveListener(HandleBagClicked);
        if (tipCoroutine != null) StopCoroutine(tipCoroutine);
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
            isUnlocked = profileLoaded && profile.AccountDay >= UnlockAccountDay;
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

    private void HandleBagClicked()
    {
        if (!profileLoaded || !isUnlocked)
        {
            ShowTip(LockedTip);
            return;
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
        if (tipCoroutine != null) StopCoroutine(tipCoroutine);
        tipCoroutine = StartCoroutine(ShowTipRoutine(message));
    }

    private IEnumerator ShowTipRoutine(string message)
    {
        tipText.text = message;
        tipText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(TipDurationSeconds);
        if (tipText.text == message)
        {
            tipText.text = string.Empty;
            tipText.gameObject.SetActive(false);
        }
        tipCoroutine = null;
    }

    private static Transform FindSceneObject(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
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
