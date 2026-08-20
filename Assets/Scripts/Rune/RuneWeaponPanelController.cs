using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RuneWeaponPanelController : MonoBehaviour
{
    private const string WeaponPrefabPath = "prefabs/Weapon";
    private const string Weapon0PrefabPath = "prefabs/Weapon0";
    private const int PageSize = 25;

    private Transform weaponContainer;
    private Transform heroContainer;
    private Button pageLeftButton;
    private Button pageRightButton;
    private TMP_Text pageText;
    private GameObject weaponPrefab;
    private GameObject weapon0Prefab;
    private string playerId;
    private RuneProfile currentProfile;
    private IRuneProfileGateway runeGateway;
    private IAuthSessionStore authSessionStore;
    private CancellationTokenSource lifetimeCancellation;
    private Coroutine pendingInventoryChange;
    private bool runeOperationInProgress;
    private readonly List<InventoryDisplayEntry> displayEntries = new List<InventoryDisplayEntry>();
    private int currentPageIndex;

    private sealed class InventoryDisplayEntry
    {
        public RuneInventoryEntry Inventory;
        public RuneDefinition Definition;
        public int AvailableCompleteRunes;
        public bool IsFragmentProgress;
    }

    private void Awake()
    {
        IClientServices services = ClientCompositionRoot.Current;
        runeGateway = services.Runes;
        authSessionStore = services.AuthSession;
        lifetimeCancellation = new CancellationTokenSource();
        weaponContainer = transform.Find("WeaponContainer");
        heroContainer = transform.Find("MyHeroBg/HeroContainer");
        pageLeftButton = GetButton(transform.Find("PageLeft"));
        pageRightButton = GetButton(transform.Find("PageRight"));
        pageText = GetText(transform.Find("page"));
        weaponPrefab = Resources.Load<GameObject>(WeaponPrefabPath);
        weapon0Prefab = Resources.Load<GameObject>(Weapon0PrefabPath);

        if (weaponContainer == null || heroContainer == null ||
            weaponPrefab == null || weapon0Prefab == null ||
            pageLeftButton == null || pageRightButton == null || pageText == null)
        {
            Debug.LogError(
                "RuneWeaponPanelController requires WeaponContainer, MyHeroBg/HeroContainer " +
                "PageLeft, PageRight, page and Resources/prefabs/Weapon(0).");
        }

        if (pageLeftButton != null) pageLeftButton.onClick.AddListener(ShowPreviousPage);
        if (pageRightButton != null) pageRightButton.onClick.AddListener(ShowNextPage);

        if (weaponContainer != null)
        {
            RuneUnequipDropZone unequipZone = weaponContainer.GetComponent<RuneUnequipDropZone>();
            if (unequipZone == null) unequipZone = weaponContainer.gameObject.AddComponent<RuneUnequipDropZone>();
            unequipZone.Initialize(this);
        }
    }

    private void OnDestroy()
    {
        if (pageLeftButton != null) pageLeftButton.onClick.RemoveListener(ShowPreviousPage);
        if (pageRightButton != null) pageRightButton.onClick.RemoveListener(ShowNextPage);
        if (lifetimeCancellation == null) return;
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        lifetimeCancellation = null;
    }

    private void OnEnable()
    {
        _ = RenderAllAsync();
    }

    private async Task RenderAllAsync()
    {
        if (weaponContainer == null || heroContainer == null ||
            weaponPrefab == null || weapon0Prefab == null) return;

        ClearContainer();
        displayEntries.Clear();
        AuthSession session = authSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Rune inventory cannot be displayed without an authenticated PlayerId.");
            currentPageIndex = 0;
            UpdatePageControls();
            return;
        }

        playerId = session.PlayerId;
        try
        {
            currentProfile = await runeGateway.GetProfileAsync(
                playerId,
                lifetimeCancellation.Token);
            if (!isActiveAndEnabled) return;
            SetupHeroDropZones();
            RefreshHeroRuneNames();
            RefreshInventoryPages();
        }
        catch (OperationCanceledException)
        {
            // The scene was unloaded while the profile request was in progress.
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to load rune inventory: {exception.Message}");
        }
    }

    private void RefreshInventoryPages()
    {
        displayEntries.Clear();
        for (int index = 0; index < currentProfile.Inventory.Count; index++)
        {
            RuneInventoryEntry inventory = currentProfile.Inventory[index];
            RuneDefinition definition = RuneCatalog.Find(inventory.RuneId);
            if (definition == null) continue;

            bool requiresFragments = definition.Rarity == RuneRarity.Epic ||
                                     definition.Rarity == RuneRarity.Legendary;
            int availableCompleteRunes = inventory.OwnedCount -
                                         CountEquippedRunes(definition.RuneId);

            if (availableCompleteRunes > 0)
            {
                displayEntries.Add(new InventoryDisplayEntry
                {
                    Inventory = inventory,
                    Definition = definition,
                    AvailableCompleteRunes = availableCompleteRunes,
                    IsFragmentProgress = false
                });
            }

            if (requiresFragments && inventory.FragmentCount > 0)
            {
                displayEntries.Add(new InventoryDisplayEntry
                {
                    Inventory = inventory,
                    Definition = definition,
                    AvailableCompleteRunes = 0,
                    IsFragmentProgress = true
                });
            }
        }

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, TotalPages - 1);
        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        ClearContainer();

        int startIndex = currentPageIndex * PageSize;
        int endIndex = Mathf.Min(startIndex + PageSize, displayEntries.Count);
        for (int index = startIndex; index < endIndex; index++)
        {
            CreateInventoryCard(displayEntries[index]);
        }

        UpdatePageControls();
    }

    private void CreateInventoryCard(InventoryDisplayEntry entry)
    {
        GameObject instance = Instantiate(
            entry.IsFragmentProgress ? weaponPrefab : weapon0Prefab,
            weaponContainer,
            false);
        instance.name = entry.IsFragmentProgress
            ? $"Rune_{entry.Definition.RuneId}_Fragments"
            : $"Rune_{entry.Definition.RuneId}_Complete";
        SetText(instance.transform.Find("Name"), entry.Definition.DisplayName);

        if (entry.IsFragmentProgress)
        {
            SetText(
                instance.transform.Find("count"),
                $"{entry.Inventory.FragmentCount}/{entry.Definition.RequiredFragments}");
            return;
        }

        SetAvailableCount(instance.transform, entry.AvailableCompleteRunes);
        RuneDragItem dragItem = instance.AddComponent<RuneDragItem>();
        dragItem.Initialize(entry.Definition.RuneId, entry.AvailableCompleteRunes);
    }

    private int TotalPages => Mathf.Max(1, Mathf.CeilToInt(displayEntries.Count / (float)PageSize));

    private void ShowPreviousPage()
    {
        if (currentPageIndex <= 0) return;
        currentPageIndex--;
        RenderCurrentPage();
    }

    private void ShowNextPage()
    {
        if (currentPageIndex >= TotalPages - 1) return;
        currentPageIndex++;
        RenderCurrentPage();
    }

    private void UpdatePageControls()
    {
        int totalPages = TotalPages;
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);
        if (pageText != null) pageText.text = $"{currentPageIndex + 1}/{totalPages}";
        if (pageLeftButton != null) pageLeftButton.interactable = true;
        if (pageRightButton != null) pageRightButton.interactable = true;
    }

    private static void SetAvailableCount(Transform item, int availableCompleteRunes)
    {
        Transform amountRoot = item.Find("AcText");
        if (amountRoot == null)
        {
            Debug.LogError($"{item.name} requires AcText/Text (TMP).");
            return;
        }

        bool hasCompleteRune = availableCompleteRunes > 0;
        amountRoot.gameObject.SetActive(hasCompleteRune);
        if (hasCompleteRune)
        {
            SetText(amountRoot.Find("Text (TMP)"), availableCompleteRunes.ToString());
        }
    }

    private async Task<bool> TryEquipRuneAsync(string heroId, string runeId)
    {
        if (string.IsNullOrEmpty(playerId) || runeOperationInProgress) return false;

        runeOperationInProgress = true;
        try
        {
            RuneProfileMutationResult result = await runeGateway.EquipRuneAsync(
                playerId,
                heroId,
                runeId,
                lifetimeCancellation.Token);
            if (result == null || !result.Succeeded || result.Profile == null) return false;

            currentProfile = result.Profile;
            if (isActiveAndEnabled)
            {
                RefreshHeroRuneNames();
                RefreshInventoryPages();
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to equip rune: {exception.Message}");
            return false;
        }
        finally
        {
            runeOperationInProgress = false;
        }
    }

    private async Task<bool> TryUnequipRuneAsync(string heroId)
    {
        if (string.IsNullOrEmpty(playerId) || runeOperationInProgress) return false;

        runeOperationInProgress = true;
        try
        {
            RuneProfileMutationResult result = await runeGateway.UnequipRuneAsync(
                playerId,
                heroId,
                lifetimeCancellation.Token);
            if (result == null || !result.Succeeded || result.Profile == null) return false;

            currentProfile = result.Profile;
            if (isActiveAndEnabled)
            {
                RefreshHeroRuneNames();
                RefreshInventoryPages();
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Unable to unequip rune: {exception.Message}");
            return false;
        }
        finally
        {
            runeOperationInProgress = false;
        }
    }

    public bool RequestUnequipRune(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(playerId) ||
            pendingInventoryChange != null || runeOperationInProgress)
        {
            return false;
        }

        pendingInventoryChange = StartCoroutine(UnequipAfterPointerEvent(heroId));
        return true;
    }

    public bool RequestEquipRune(string heroId, string runeId)
    {
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(runeId) ||
            string.IsNullOrEmpty(playerId) || pendingInventoryChange != null ||
            runeOperationInProgress)
        {
            return false;
        }

        pendingInventoryChange = StartCoroutine(EquipAfterPointerEvent(heroId, runeId));
        return true;
    }

    private IEnumerator EquipAfterPointerEvent(string heroId, string runeId)
    {
        // Let OnDrop and OnEndDrag complete before rebuilding WeaponContainer.
        yield return null;
        pendingInventoryChange = null;
        _ = CompleteEquipAsync(heroId, runeId);
    }

    private IEnumerator UnequipAfterPointerEvent(string heroId)
    {
        // Do not destroy and rebuild Canvas children while EventSystem is dispatching OnDrop/OnEndDrag.
        yield return null;
        pendingInventoryChange = null;
        _ = CompleteUnequipAsync(heroId);
    }

    private async Task CompleteEquipAsync(string heroId, string runeId)
    {
        if (await TryEquipRuneAsync(heroId, runeId)) return;
        if (!isActiveAndEnabled || currentProfile == null) return;
        RefreshHeroRuneNames();
        RefreshInventoryPages();
    }

    private async Task CompleteUnequipAsync(string heroId)
    {
        if (await TryUnequipRuneAsync(heroId)) return;
        if (isActiveAndEnabled) RestoreHeroRuneNames();
    }

    public RectTransform SpawnUnequipProxy(string runeId)
    {
        RuneDefinition definition = RuneCatalog.Find(runeId);
        if (definition == null) return null;

        // A lightweight proxy avoids cloning a complete inventory hierarchy into the root Canvas
        // during an active pointer/render event.
        var proxy = new GameObject(
            $"UnequipProxy_{runeId}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        RectTransform proxyRect = (RectTransform)proxy.transform;
        proxyRect.SetParent(GetDragRoot(), false);
        proxyRect.sizeDelta = new Vector2(240f, 120f);
        proxy.name = $"UnequipProxy_{runeId}";

        Image background = proxy.GetComponent<Image>();
        background.color = new Color(0.12f, 0.16f, 0.2f, 0.9f);
        background.raycastTarget = false;

        var labelObject = new GameObject(
            "Name",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(proxyRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = definition.DisplayName;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        label.color = Color.white;
        label.raycastTarget = false;

        CanvasGroup group = proxy.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        return proxyRect;
    }

    public void RestoreHeroRuneNames()
    {
        RefreshHeroRuneNames();
    }

    private Transform GetDragRoot()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.transform : transform.root;
    }

    private void SetupHeroDropZones()
    {
        for (int index = 0; index < heroContainer.childCount; index++)
        {
            Transform hero = heroContainer.GetChild(index);
            Transform weapon = hero.Find("weapon");
            if (weapon == null) continue;

            TMP_Text heroName = GetText(hero.Find("Name"));
            string heroId = heroName != null && !string.IsNullOrWhiteSpace(heroName.text)
                ? heroName.text.Trim()
                : $"HERO_SLOT_{index + 1:00}";
            TMP_Text runeName = GetText(weapon.Find("Text (TMP)"));
            RuneDropZone zone = weapon.GetComponent<RuneDropZone>();
            if (zone == null) zone = weapon.gameObject.AddComponent<RuneDropZone>();
            zone.Initialize(this, heroId, runeName);

            RuneEquippedDragItem equippedItem = weapon.GetComponent<RuneEquippedDragItem>();
            if (equippedItem == null) equippedItem = weapon.gameObject.AddComponent<RuneEquippedDragItem>();
            equippedItem.Initialize(this, heroId, zone);
        }
    }

    private void RefreshHeroRuneNames()
    {
        RuneDropZone[] zones = heroContainer.GetComponentsInChildren<RuneDropZone>(true);
        for (int index = 0; index < zones.Length; index++)
        {
            string runeId = FindEquippedRuneId(zones[index].HeroId);
            RuneDefinition definition = RuneCatalog.Find(runeId);
            zones[index].SetRuneName(definition != null ? definition.DisplayName : string.Empty);

            RuneEquippedDragItem equippedItem = zones[index].GetComponent<RuneEquippedDragItem>();
            if (equippedItem != null) equippedItem.SetRuneId(runeId);
        }
    }

    private string FindEquippedRuneId(string heroId)
    {
        for (int index = 0; index < currentProfile.Loadouts.Count; index++)
        {
            HeroRuneLoadoutEntry loadout = currentProfile.Loadouts[index];
            if (loadout.HeroId == heroId) return loadout.RuneId;
        }
        return null;
    }

    private int CountEquippedRunes(string runeId)
    {
        int count = 0;
        for (int index = 0; index < currentProfile.Loadouts.Count; index++)
        {
            if (currentProfile.Loadouts[index].RuneId == runeId) count++;
        }
        return count;
    }

    private void ClearContainer()
    {
        for (int index = weaponContainer.childCount - 1; index >= 0; index--)
        {
            GameObject child = weaponContainer.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static void SetText(Transform target, string value)
    {
        TMP_Text text = GetText(target);
        if (text != null) text.text = value;
    }

    private static TMP_Text GetText(Transform target)
    {
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static Button GetButton(Transform target)
    {
        return target != null ? target.GetComponent<Button>() : null;
    }
}
