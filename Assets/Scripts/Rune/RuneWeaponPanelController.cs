using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuneWeaponPanelController : MonoBehaviour
{
    private const string WeaponPrefabPath = "prefabs/Weapon";
    private const string Weapon0PrefabPath = "prefabs/Weapon0";

    private Transform weaponContainer;
    private Transform heroContainer;
    private GameObject weaponPrefab;
    private GameObject weapon0Prefab;
    private string playerId;
    private RuneProfile currentProfile;

    private void Awake()
    {
        weaponContainer = transform.Find("WeaponContainer");
        heroContainer = transform.Find("MyHeroBg/HeroContainer");
        weaponPrefab = Resources.Load<GameObject>(WeaponPrefabPath);
        weapon0Prefab = Resources.Load<GameObject>(Weapon0PrefabPath);

        if (weaponContainer == null || heroContainer == null ||
            weaponPrefab == null || weapon0Prefab == null)
        {
            Debug.LogError(
                "RuneWeaponPanelController requires WeaponContainer, MyHeroBg/HeroContainer " +
                "and Resources/prefabs/Weapon(0).");
        }
    }

    private void OnEnable()
    {
        RenderAll();
    }

    private void RenderAll()
    {
        if (weaponContainer == null || heroContainer == null ||
            weaponPrefab == null || weapon0Prefab == null) return;

        ClearContainer();
        AuthSession session = AuthSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Rune inventory cannot be displayed without an authenticated PlayerId.");
            return;
        }

        playerId = session.PlayerId;
        currentProfile = new LocalRuneRewardService().GetProfile(playerId);
        SetupHeroDropZones();
        RefreshHeroRuneNames();
        RenderInventory();
    }

    private void RenderInventory()
    {
        for (int index = 0; index < currentProfile.Inventory.Count; index++)
        {
            RuneInventoryEntry inventory = currentProfile.Inventory[index];
            RuneDefinition definition = RuneCatalog.Find(inventory.RuneId);
            if (definition == null) continue;

            bool usesFragmentCard = definition.Rarity == RuneRarity.Epic ||
                                    definition.Rarity == RuneRarity.Legendary;
            int availableCompleteRunes = inventory.OwnedCount -
                                         CountEquippedRunes(definition.RuneId);
            bool hasDisplayableInventory = usesFragmentCard
                ? availableCompleteRunes > 0 || inventory.FragmentCount > 0
                : availableCompleteRunes > 0;
            if (!hasDisplayableInventory) continue;

            GameObject instance = Instantiate(
                usesFragmentCard ? weaponPrefab : weapon0Prefab,
                weaponContainer,
                false);
            instance.name = $"Rune_{definition.RuneId}";
            SetText(instance.transform.Find("Name"), definition.DisplayName);

            if (availableCompleteRunes > 0)
            {
                RuneDragItem dragItem = instance.AddComponent<RuneDragItem>();
                dragItem.Initialize(definition.RuneId);
            }

            if (usesFragmentCard)
            {
                SetText(
                    instance.transform.Find("count"),
                    $"{inventory.FragmentCount}/{definition.RequiredFragments}");
            }
        }
    }

    public bool TryEquipRune(string heroId, string runeId)
    {
        if (string.IsNullOrEmpty(playerId)) return false;

        var service = new LocalRuneRewardService();
        if (!service.TryEquipRune(playerId, heroId, runeId, out RuneProfile updatedProfile))
        {
            Debug.LogWarning($"Rune '{runeId}' is not available for hero '{heroId}'.");
            return false;
        }

        currentProfile = updatedProfile;
        ClearContainer();
        RefreshHeroRuneNames();
        RenderInventory();
        return true;
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
}
