using System;
using DragonBound.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates an explicit Firebase Analytics allow/deny prompt on first launch and a persistent
/// entry in Main/SetupPanel. The UI is installed at runtime so scene serialization is untouched.
/// </summary>
public sealed class AnalyticsConsentUiController : MonoBehaviour
{
    private const string ControllerName = "AnalyticsConsentUiController";
    private const string PromptName = "AnalyticsConsentPrompt";
    private const string SettingsButtonName = "AnalyticsConsentSettingsBtn";

    private GameObject prompt;
    private TMP_Text settingsButtonLabel;

    public static bool HasResolvedConsent =>
        FirebaseAnalyticsBootstrap.ConsentState != DragonBound.Analytics.AnalyticsConsentStateV2.Unknown;

    public static void ShowRequiredPrompt()
    {
        EnsureForActiveScene().ShowPrompt(true);
    }

    private static AnalyticsConsentUiController EnsureForActiveScene()
    {
        var current = FindObjectOfType<AnalyticsConsentUiController>();
        if (current != null) return current;

        var host = new GameObject(ControllerName);
        current = host.AddComponent<AnalyticsConsentUiController>();
        current.InstallForScene(SceneManager.GetActiveScene());
        return current;
    }

    internal void InstallForScene(Scene scene)
    {
        if (scene.name == "Login" && !HasResolvedConsent)
        {
            ShowPrompt(true);
        }
        else if (scene.name == "Main")
        {
            InstallSettingsButton(scene);
        }
    }

    private void InstallSettingsButton(Scene scene)
    {
        var setupPanel = FindInScene(scene, "SetupPanel");
        if (setupPanel == null)
        {
            Debug.LogWarning("[Analytics] SetupPanel was not found; consent can still be changed on Login.");
            return;
        }

        var existing = setupPanel.transform.FindUi(SettingsButtonName);
        if (existing != null)
        {
            settingsButtonLabel = existing.GetComponentInChildren<TMP_Text>(true);
            RefreshSettingsLabel();
            return;
        }

        var button = CreateButton(
            setupPanel.transform,
            SettingsButtonName,
            Vector2.zero,
            new Vector2(520f, 92f),
            "数据分析设置",
            new Color32(64, 102, 139, 255));
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 72f);
        settingsButtonLabel = button.GetComponentInChildren<TMP_Text>(true);
        button.onClick.AddListener(() => ShowPrompt(false));
        RefreshSettingsLabel();
    }

    private void ShowPrompt(bool requiredChoice)
    {
        if (prompt != null)
        {
            prompt.SetActive(true);
            prompt.transform.SetAsLastSibling();
            return;
        }

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Analytics] Cannot display consent prompt because no Canvas is available.");
            return;
        }

        prompt = CreatePanel(canvas.transform, PromptName, new Color32(0, 0, 0, 205));
        Stretch(prompt.GetComponent<RectTransform>());
        prompt.transform.SetAsLastSibling();

        var card = CreatePanel(prompt.transform, "Card", new Color32(247, 249, 252, 255));
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(900f, 650f);

        CreateText(card.transform, "Title", new Vector2(0f, 238f), new Vector2(790f, 80f),
            "数据分析选择", 44f, FontStyles.Bold, new Color32(26, 39, 56, 255));
        CreateText(card.transform, "Description", new Vector2(0f, 55f), new Vector2(760f, 270f),
            "我们使用 Firebase Analytics 收集应用版本、设备和系统信息以及游戏事件，" +
            "用于分析稳定性、关卡表现和改进游戏体验。\n\n" +
            "我们不会在分析事件中记录姓名、邮箱、聊天内容、登录令牌或原始交易号。" +
            "你可以拒绝，且不会影响登录和游戏；之后可在设置中随时更改。",
            29f, FontStyles.Normal, new Color32(55, 68, 83, 255));

        var allowButton = CreateButton(card.transform, "AllowBtn", new Vector2(-205f, -220f),
            new Vector2(330f, 104f), "同意数据分析", new Color32(38, 117, 196, 255));
        var denyButton = CreateButton(card.transform, "DenyBtn", new Vector2(205f, -220f),
            new Vector2(330f, 104f), "暂不同意", new Color32(103, 113, 125, 255));
        allowButton.onClick.AddListener(() => ApplyChoice(true));
        denyButton.onClick.AddListener(() => ApplyChoice(false));

        if (!requiredChoice)
        {
            var closeButton = CreateButton(card.transform, "CloseBtn", new Vector2(385f, 275f),
                new Vector2(70f, 70f), "×", new Color32(220, 225, 231, 255));
            closeButton.GetComponentInChildren<TMP_Text>().color = new Color32(55, 68, 83, 255);
            closeButton.onClick.AddListener(() => prompt.SetActive(false));
        }
    }

    private void ApplyChoice(bool enabled)
    {
        FirebaseAnalyticsBootstrap.SetCollectionEnabled(enabled);
        RefreshSettingsLabel();
        if (prompt != null) prompt.SetActive(false);
        Debug.Log(enabled
            ? "[Analytics] User granted Analytics collection consent."
            : "[Analytics] User denied or withdrew Analytics collection consent.");
    }

    private void RefreshSettingsLabel()
    {
        if (settingsButtonLabel == null) return;
        switch (FirebaseAnalyticsBootstrap.ConsentState)
        {
            case DragonBound.Analytics.AnalyticsConsentStateV2.Granted:
                settingsButtonLabel.text = "数据分析：已开启";
                break;
            case DragonBound.Analytics.AnalyticsConsentStateV2.Denied:
                settingsButtonLabel.text = "数据分析：已关闭";
                break;
            default:
                settingsButtonLabel.text = "数据分析：待选择";
                break;
        }
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        value.layer = parent.gameObject.layer;
        value.transform.SetParent(parent, false);
        value.GetComponent<Image>().color = color;
        return value;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        string label,
        Color color)
    {
        var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        value.layer = parent.gameObject.layer;
        value.transform.SetParent(parent, false);
        var rect = value.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        value.GetComponent<Image>().color = color;
        var button = value.GetComponent<Button>();
        button.targetGraphic = value.GetComponent<Image>();
        CreateText(value.transform, "Label", Vector2.zero, size - new Vector2(24f, 16f), label,
            30f, FontStyles.Bold, Color.white);
        return button;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        string value,
        float fontSize,
        FontStyles fontStyle,
        Color color)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        host.layer = parent.gameObject.layer;
        host.transform.SetParent(parent, false);
        var rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        var text = host.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindRecursive(root.transform, objectName);
            if (found != null) return found.gameObject;
        }

        return null;
    }

    private static Transform FindRecursive(Transform parent, string objectName)
    {
        if (string.Equals(parent.name, objectName, StringComparison.Ordinal)) return parent;
        for (var index = 0; index < parent.childCount; index++)
        {
            var found = FindRecursive(parent.GetChild(index), objectName);
            if (found != null) return found;
        }

        return null;
    }
}

public static class AnalyticsConsentUiInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Login" && scene.name != "Main") return;
        var controller = UnityEngine.Object.FindObjectOfType<AnalyticsConsentUiController>();
        if (controller == null)
        {
            controller = new GameObject("AnalyticsConsentUiController")
                .AddComponent<AnalyticsConsentUiController>();
        }
        controller.InstallForScene(scene);
    }
}
