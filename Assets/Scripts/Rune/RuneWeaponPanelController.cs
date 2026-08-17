using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuneWeaponPanelController : MonoBehaviour
{
    private const string WeaponPrefabPath = "prefabs/Weapon";
    private const string Weapon0PrefabPath = "prefabs/Weapon0";

    private Transform weaponContainer;
    private GameObject weaponPrefab;
    private GameObject weapon0Prefab;

    private void Awake()
    {
        weaponContainer = transform.Find("WeaponContainer");
        weaponPrefab = Resources.Load<GameObject>(WeaponPrefabPath);
        weapon0Prefab = Resources.Load<GameObject>(Weapon0PrefabPath);

        if (weaponContainer == null || weaponPrefab == null || weapon0Prefab == null)
        {
            Debug.LogError(
                "RuneWeaponPanelController requires WeaponContainer and Resources/prefabs/Weapon(0).");
        }
    }

    private void OnEnable()
    {
        RenderLastRunRewards();
    }

    private void RenderLastRunRewards()
    {
        if (weaponContainer == null || weaponPrefab == null || weapon0Prefab == null) return;

        ClearContainer();
        AuthSession session = AuthSessionStore.Current;
        if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
        {
            Debug.LogError("Rune inventory cannot be displayed without an authenticated PlayerId.");
            return;
        }

        RuneProfile profile = new LocalRuneRewardService().GetProfile(session.PlayerId);
        for (int index = 0; index < profile.LastRunRewards.Count; index++)
        {
            RuneReward reward = profile.LastRunRewards[index];
            RuneDefinition definition = RuneCatalog.Find(reward.RuneId);
            if (definition == null) continue;

            bool usesFragmentCard = reward.Rarity == RuneRarity.Epic ||
                                    reward.Rarity == RuneRarity.Legendary;
            GameObject instance = Instantiate(
                usesFragmentCard ? weaponPrefab : weapon0Prefab,
                weaponContainer,
                false);
            instance.name = $"Rune_{reward.RuneId}";
            SetText(instance.transform.Find("Name"), reward.DisplayName);

            if (usesFragmentCard)
            {
                RuneInventoryEntry inventory = FindInventory(profile.Inventory, reward.RuneId);
                int fragments = inventory != null ? inventory.FragmentCount : 0;
                SetText(instance.transform.Find("count"), $"{fragments}/{definition.RequiredFragments}");
            }
        }
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

    private static RuneInventoryEntry FindInventory(
        List<RuneInventoryEntry> inventory,
        string runeId)
    {
        for (int index = 0; index < inventory.Count; index++)
        {
            if (inventory[index].RuneId == runeId) return inventory[index];
        }
        return null;
    }

    private static void SetText(Transform target, string value)
    {
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value;
    }
}
