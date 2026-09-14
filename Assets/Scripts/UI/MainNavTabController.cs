using System;
using DragonBound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the selected/unselected sprites of the bottom navigation buttons
/// (BagBtn / RankingBtn / MainBtn) in the Main scene, based on which panel
/// is currently open. Auto-installs onto MainPanel on scene load.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainNavTabController : MonoBehaviour
{
    private const string NavigationPath = "ButtonNavigation";
    private const string SelectSpritePath = "Main/select";
    private const string NoSelectSpritePath = "Main/Noselect";

    private Image bagImage;
    private Image rankingImage;
    private Image mainImage;
    private Button mainButton;
    private Button rankingButton;
    private GameObject weaponPanel;
    private GameObject leaderPanel;
    private Sprite selectSprite;
    private Sprite noSelectSprite;
    private int lastState = -1; // 0 = home, 1 = bag, 2 = ranking

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
            if (mainPanel == null || mainPanel.FindUi($"{NavigationPath}/BagBtn") == null) continue;
            if (mainPanel.GetComponent<MainNavTabController>() == null)
            {
                mainPanel.gameObject.AddComponent<MainNavTabController>();
            }
            return;
        }
    }

    private void Awake()
    {
        Transform navigation = transform.FindUi(NavigationPath);
        bagImage = navigation?.FindUi("BagBtn")?.GetComponent<Image>();
        rankingImage = navigation?.FindUi("RankingBtn")?.GetComponent<Image>();
        mainImage = navigation?.FindUi("MainBtn")?.GetComponent<Image>();
        Scene ownerScene = gameObject.scene;
        weaponPanel = FindSceneObject(ownerScene, "WeaponPanel")?.gameObject;
        leaderPanel = FindSceneObject(ownerScene, "LeaderPanel")?.gameObject;

        if (bagImage == null || rankingImage == null || mainImage == null ||
            weaponPanel == null || leaderPanel == null)
        {
            Debug.LogError(
                "MainNavTabController requires MainPanel/ButtonNavigation/BagBtn, " +
                "RankingBtn, MainBtn, " +
                "WeaponPanel and LeaderPanel.",
                this);
            enabled = false;
            return;
        }

        selectSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(SelectSpritePath);
        noSelectSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(NoSelectSpritePath);
        if (selectSprite == null || noSelectSprite == null)
        {
            Debug.LogError(
                "MainNavTabController unable to load sprites from Resources/Main/select " +
                "and Resources/Main/Noselect.",
                this);
            enabled = false;
            return;
        }

        // MainBtn was given a Button component in the scene but has no persistent
        // OnClick calls wired; hook up the close-panels behavior here instead of
        // touching the scene. If the Button already existed, reuse it as-is.
        mainButton = mainImage.GetComponent<Button>();
        if (mainButton == null)
        {
            mainButton = mainImage.gameObject.AddComponent<Button>();
            // Pure sprite-driven highlight; avoid ColorTint tinting the select sprite.
            mainButton.transition = Selectable.Transition.None;
        }
        mainButton.onClick.AddListener(HandleMainClicked);
        // RankingBtn keeps a scene-persistent onClick that only opens LeaderPanel,
        // and persistent calls run BEFORE runtime listeners. Rebuild the event so a
        // single handler can toggle the panel open/closed.
        rankingButton = rankingImage != null ? rankingImage.GetComponent<Button>() : null;
        if (rankingButton == null)
        {
            rankingButton = rankingImage.gameObject.AddComponent<Button>();
            rankingButton.transition = Selectable.Transition.None;
        }
        rankingButton.onClick = new Button.ButtonClickedEvent();
        rankingButton.onClick.AddListener(HandleRankingClicked);

    }

    private void Update()
    {
        // Defensive normalization for panels opened by another entry point. The
        // navigation handlers below never leave both panels active, but keeping
        // this guard here prevents an invalid authored/runtime state persisting.
        if (weaponPanel.activeSelf && leaderPanel.activeSelf)
        {
            leaderPanel.SetActive(false);
        }

        // Poll panel active states instead of hooking button clicks:
        // BagBtn's onClick is rebuilt at runtime by MainRuneUnlockController,
        // and panels can be closed from several entry points.
        int state = weaponPanel.activeSelf ? 1
                  : leaderPanel.activeSelf ? 2
                  : 0;
        if (state == lastState) return;

        lastState = state;
        bagImage.sprite = state == 1 ? selectSprite : noSelectSprite;
        rankingImage.sprite = state == 2 ? selectSprite : noSelectSprite;
        mainImage.sprite = state == 0 ? selectSprite : noSelectSprite;
    }

    /// <summary>MainBtn click: close every overlay panel and return home.</summary>
    private void HandleMainClicked()
    {
        // Identical to the ReturnBtn/CloseBtn persistent SetActive(false) calls;
        // both panel controllers clean themselves up in OnDisable, so no extra
        // teardown is required here. No-op when already on the home screen.
        if (weaponPanel.activeSelf)
        {
            weaponPanel.SetActive(false);
        }

        if (leaderPanel.activeSelf)
        {
            leaderPanel.SetActive(false);
        }
    }

    /// <summary>RankingBtn click: toggle the leaderboard panel open/closed.</summary>
    private void HandleRankingClicked()
    {
        if (leaderPanel.activeSelf)
        {
            leaderPanel.SetActive(false);
        }
        else
        {
            if (weaponPanel.activeSelf)
            {
                weaponPanel.SetActive(false);
            }

            leaderPanel.SetActive(true);
        }
    }


    private void OnDestroy()
    {
        if (mainButton != null) mainButton.onClick.RemoveListener(HandleMainClicked);
        if (rankingButton != null) rankingButton.onClick.RemoveListener(HandleRankingClicked);
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
