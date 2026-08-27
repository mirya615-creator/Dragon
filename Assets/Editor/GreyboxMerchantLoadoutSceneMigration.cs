using UnityEditor;
using UnityEditor.SceneManagement;
using DragonBound.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class GreyboxMerchantLoadoutSceneMigration
{
    private const string ScenePath = "Assets/Scenes/Greybox_Main.unity";
    private const string SessionKey = "dragonbound.greyboxMerchantLoadoutSceneMigration.v5";

    static GreyboxMerchantLoadoutSceneMigration()
    {
        EditorApplication.delayCall += TryRunAutomatically;
    }

    [MenuItem("Tools/DragonBound/Repair Greybox Merchant Loadout UI")]
    private static void RunFromMenu()
    {
        ApplyMigration();
    }

    private static void TryRunAutomatically()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            return;
        }

        ApplyMigration();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.delayCall += TryRunAutomatically;
    }

    private static void ApplyMigration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForMigration = !scene.IsValid() || !scene.isLoaded;
        if (openedForMigration)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        Transform itemContainer = FindItemContainer(scene);
        if (itemContainer == null)
        {
            Debug.LogError("Greybox_Main requires ItemContainer with Active and Passtive children.");
            if (openedForMigration) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        bool changed = false;
        DragonBoundBootstrap bootstrap = FindInScene<DragonBoundBootstrap>(scene);
        if (bootstrap == null)
        {
            Debug.LogError("Greybox_Main requires DragonBoundBootstrap.");
            if (openedForMigration) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        var bootstrapObject = new SerializedObject(bootstrap);
        SerializedProperty deferProperty = bootstrapObject.FindProperty(
            "deferInitializationUntilItemSnapshotReady");
        if (deferProperty != null && !deferProperty.boolValue)
        {
            deferProperty.boolValue = true;
            bootstrapObject.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (itemContainer.GetComponent<GreyboxMerchantLoadoutController>() == null)
        {
            Undo.AddComponent<GreyboxMerchantLoadoutController>(itemContainer.gameObject);
            changed = true;
        }

        Transform activeContainer = itemContainer.Find("Active");
        Transform passiveContainer = itemContainer.Find("Passtive");

        if (activeContainer == null || passiveContainer == null)
        {
            Debug.LogError(
                "Greybox Merchant migration requires Active and Passtive containers.");
            if (openedForMigration) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        for (int index = 0; index < 2; index++)
        {
            Transform slot = activeContainer.Find("Active" + index);
            if (slot != null)
            {
                if (EnsureCooldownMask(slot)) changed = true;
                if (slot.gameObject.activeSelf)
                {
                    slot.gameObject.SetActive(false);
                    changed = true;
                }
            }
        }

        for (int index = 0; index < 6; index++)
        {
            Transform slot = passiveContainer.Find("Passtive" + index);
            if (slot != null)
            {
                if (EnsureCooldownMask(slot)) changed = true;
                if (slot.gameObject.activeSelf)
                {
                    slot.gameObject.SetActive(false);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "Greybox Merchant loadout UI repaired: empty item slots hidden and equipped slots runtime-driven.");
        }

        SessionState.SetBool(SessionKey, true);
        if (openedForMigration) EditorSceneManager.CloseScene(scene, true);
    }

    private static Transform FindItemContainer(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "ItemContainer") continue;
                if (candidate.Find("Active") != null && candidate.Find("Passtive") != null)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }

        return null;
    }

    private static bool EnsureCooldownMask(Transform slot)
    {
        Transform existing = slot.Find("CooldownMask");
        bool created = existing == null;
        GameObject maskObject;
        if (created)
        {
            maskObject = new GameObject("CooldownMask", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(maskObject, "Add item cooldown mask");
            maskObject.transform.SetParent(slot, false);
        }
        else
        {
            maskObject = existing.gameObject;
        }

        var image = maskObject.GetComponent<Image>();
        bool changed = created ||
                       !image.preserveAspect ||
                       image.type != Image.Type.Filled ||
                       image.fillMethod != Image.FillMethod.Radial360 ||
                       image.raycastTarget;
        var rect = maskObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
        image.fillAmount = 0f;
        image.preserveAspect = true;
        image.color = new Color(0f, 0f, 0f, 0.58f);
        image.raycastTarget = false;
        maskObject.SetActive(false);

        maskObject.transform.SetAsLastSibling();
        return changed;
    }
}
