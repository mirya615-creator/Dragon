using System;
using DragonBound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Controls bottom-navigation panel switching in the Main scene.
/// Visual state is owned by the version-specific authored UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainNavTabController : MonoBehaviour
{
    private const string NavigationPath = "ButtonNavigation";
    private Image bagImage;
    private Image rankingImage;
    private Image mainImage;
    private Button mainButton;
    private Button rankingButton;
    private GameObject weaponPanel;
    private GameObject leaderPanel;

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
        // 只关闭场景持久化的 onClick，不能整体重建事件：那会清掉
        // BottomNavigationSelection 已注册的选中态监听。
        DisablePersistentCalls(rankingButton.onClick);
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

    /// <summary>Turns off scene-persistent OnClick calls without rebuilding the event.</summary>
    private static void DisablePersistentCalls(Button.ButtonClickedEvent clickedEvent)
    {
        for (int index = 0; index < clickedEvent.GetPersistentEventCount(); index++)
        {
            clickedEvent.SetPersistentListenerState(index, UnityEventCallState.Off);
        }
    }

}
