using System;
using DragonBound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Applies the signed-in player's persisted avatar to an authored Image.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAvatarView : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    private bool hasExplicitAvatar;
    private string explicitAvatarId;

    private void Awake()
    {
        if (avatarImage == null) avatarImage = GetComponent<Image>();
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (hasExplicitAvatar)
        {
            PlayerAvatarProfile.Apply(avatarImage, explicitAvatarId);
            return;
        }

        AuthSession session = ClientCompositionRoot.Current.AuthSession.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId)) return;
        PlayerAvatarProfile.Apply(
            avatarImage,
            PlayerAvatarProfile.GetOrCreateAvatarId(session.PlayerId));
    }

    public void SetAvatar(string avatarId)
    {
        if (avatarImage == null) avatarImage = GetComponent<Image>();
        explicitAvatarId = avatarId;
        hasExplicitAvatar = true;
        PlayerAvatarProfile.Apply(avatarImage, explicitAvatarId);
    }
}

/// <summary>Mounts the complete ProfileImg prefab inside an existing avatar slot.</summary>
public static class PlayerAvatarPrefabPresenter
{
    private const string PrefabResourcePath = "prefabs/ProfileImg";
    private const string InstanceName = "ProfileImg";
    private static GameObject profilePrefab;
    private static bool missingPrefabLogged;

    public static PlayerAvatarView Mount(RectTransform slot, string avatarId)
    {
        if (slot == null) return null;

        PlayerAvatarView view = FindMountedView(slot);
        if (view == null)
        {
            GameObject prefab = LoadPrefab();
            if (prefab == null) return null;

            GameObject instance = UnityEngine.Object.Instantiate(prefab, slot, false);
            instance.name = InstanceName;
            view = instance.GetComponent<PlayerAvatarView>();
            if (view == null)
            {
                Debug.LogError("ProfileImg.prefab requires PlayerAvatarView on its root.", instance);
                UnityEngine.Object.Destroy(instance);
                return null;
            }
        }

        Image slotImage = slot.GetComponent<Image>();
        if (slotImage != null) slotImage.enabled = false;

        FitCompletePrefab(view.transform as RectTransform, slot);
        foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
        view.SetAvatar(avatarId);
        return view;
    }

    private static PlayerAvatarView FindMountedView(RectTransform slot)
    {
        for (int index = 0; index < slot.childCount; index++)
        {
            PlayerAvatarView view = slot.GetChild(index).GetComponent<PlayerAvatarView>();
            if (view != null) return view;
        }
        return null;
    }

    private static GameObject LoadPrefab()
    {
        if (profilePrefab != null) return profilePrefab;
        profilePrefab = DragonBound.Presentation.UiAssets.Load<GameObject>(PrefabResourcePath);
        if (profilePrefab == null && !missingPrefabLogged)
        {
            missingPrefabLogged = true;
            Debug.LogError("Player avatar prefab is missing at Resources/" + PrefabResourcePath + ".");
        }
        return profilePrefab;
    }

    private static void FitCompletePrefab(RectTransform profileRect, RectTransform slot)
    {
        if (profileRect == null) return;

        profileRect.anchorMin = new Vector2(0.5f, 0.5f);
        profileRect.anchorMax = new Vector2(0.5f, 0.5f);
        profileRect.pivot = new Vector2(0.5f, 0.5f);
        profileRect.anchoredPosition = Vector2.zero;
        profileRect.localRotation = Quaternion.identity;

        float slotSide = Mathf.Min(Mathf.Abs(slot.rect.width), Mathf.Abs(slot.rect.height));
        RectTransform frameRect = profileRect.FindUi("ProfileFire") as RectTransform;
        float authoredWidth = Mathf.Abs(profileRect.rect.width);
        float authoredHeight = Mathf.Abs(profileRect.rect.height);
        if (frameRect != null)
        {
            authoredWidth = Mathf.Max(authoredWidth, Mathf.Abs(frameRect.rect.width));
            authoredHeight = Mathf.Max(authoredHeight, Mathf.Abs(frameRect.rect.height));
        }

        float authoredSide = Mathf.Max(authoredWidth, authoredHeight);
        float scale = slotSide > 0.01f && authoredSide > 0.01f
            ? slotSide / authoredSide
            : 1f;
        profileRect.localScale = Vector3.one * scale;
        profileRect.SetAsLastSibling();
    }
}

/// <summary>Feeds the local avatar into the gameplay loading panel without scene references.</summary>
public static class PlayerAvatarSceneInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AuthSession session = ClientCompositionRoot.Current.AuthSession.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId)) return;

        string avatarId = PlayerAvatarProfile.GetOrCreateAvatarId(session.PlayerId);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform loadingPanel = FindDescendant(root.transform, "LoadingPanel");
            if (loadingPanel == null) continue;

            // BG/Image is the requested hierarchy. Greybox_Main's authored equivalent is
            // currently BG/MyPart/Image, so keep both layouts supported during migration.
            Transform target = loadingPanel.FindUi("BG/Image") ?? loadingPanel.FindUi("BG/MyPart/Image");
            if (target != null)
            {
                PlayerAvatarPrefabPresenter.Mount(target as RectTransform, avatarId);
            }

            Transform enemyTarget = loadingPanel.FindUi("BG/EnemyPart/Image");
            if (enemyTarget != null)
            {
                PlayerAvatarPrefabPresenter.Mount(
                    enemyTarget as RectTransform,
                    PlayerAvatarProfile.CreateRandomAvatarId());
            }
        }
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (string.Equals(root.name, objectName, StringComparison.Ordinal)) return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform match = FindDescendant(root.GetChild(index), objectName);
            if (match != null) return match;
        }
        return null;
    }
}
