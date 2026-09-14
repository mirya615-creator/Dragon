using System.Collections.Generic;
using DragonBound.Runes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>Builds the run-local Rune reward rows whenever the pause panel opens.</summary>
    [DisallowMultipleComponent]
    public sealed class PauseRuneRewardPresenter : MonoBehaviour
    {
        private const string NoRewardPrefabPath = "prefabs/NoReward";
        // The newly authored reward prefab currently has the name "Item 1" because
        // prefabs/Item is already occupied by the merchant inventory prefab.
        private const string AuthoredRewardPrefabPath = "prefabs/Item 1";
        private const string FallbackRewardPrefabPath = "prefabs/Item";

        private readonly List<GameObject> spawnedViews = new List<GameObject>();
        private RuneRunRewardService playerRewards;
        private RuneRunRewardService aiRewards;
        private GameObject noRewardPrefab;
        private GameObject rewardItemPrefab;

        public void Bind(RuneRunRewardService player, RuneRunRewardService ai)
        {
            playerRewards = player;
            aiRewards = ai;
        }

        public void RefreshRewards()
        {
            ClearSpawnedViews();
            ResolvePrefabs();

            var background = transform.Find("Bg");
            if (background == null)
            {
                Debug.LogWarning("Pause Rune rewards require PausePanel/Bg.", this);
                return;
            }

            PopulateSide(
                background,
                "AiPart",
                "AiReward",
                aiRewards != null ? aiRewards.GrantedRewards : null);
            PopulateSide(
                background,
                "PlayerPart",
                "PlayerReward",
                playerRewards != null ? playerRewards.GrantedRewards : null);
        }

        private void PopulateSide(
            Transform background,
            string partName,
            string rewardName,
            IReadOnlyList<RuneReward> rewards)
        {
            // New hierarchy: Bg/{Part}/{Reward}. The direct-child fallback keeps the
            // currently saved scene operational until its authored hierarchy is saved.
            var part = background.Find(partName);
            var rewardContainer = part != null
                ? part.Find(rewardName)
                : background.Find(rewardName);
            var noRewardParent = part != null ? part : background;

            if (rewardContainer == null)
            {
                Debug.LogWarning($"Pause Rune rewards cannot find {partName}/{rewardName}.", this);
                return;
            }

            if (rewards == null || rewards.Count == 0)
            {
                SpawnNoReward(noRewardParent);
                return;
            }

            if (rewardItemPrefab == null)
            {
                Debug.LogWarning(
                    $"Pause Rune reward prefab is missing at Resources/{AuthoredRewardPrefabPath} " +
                    $"or Resources/{FallbackRewardPrefabPath}.",
                    this);
                return;
            }

            for (var index = 0; index < rewards.Count; index++)
            {
                SpawnReward(rewardContainer, rewards[index], index);
            }
        }

        private void SpawnNoReward(Transform parent)
        {
            if (noRewardPrefab == null)
            {
                Debug.LogWarning(
                    $"Pause Rune no-reward prefab is missing at Resources/{NoRewardPrefabPath}.",
                    this);
                return;
            }

            var instance = Instantiate(noRewardPrefab, parent, false);
            instance.name = "NoReward_Runtime";
            if (instance.transform is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            instance.SetActive(true);
            spawnedViews.Add(instance);
        }

        private void SpawnReward(Transform parent, RuneReward reward, int index)
        {
            if (reward == null)
            {
                return;
            }

            var instance = Instantiate(rewardItemPrefab, parent, false);
            instance.name = $"Item_Runtime_{index + 1}";

            var icon = instance.transform.Find("ItemImg")?.GetComponent<Image>() ??
                       instance.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = RuneUiSpriteCatalog.Load(reward.RuneId);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            foreach (var label in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                label.gameObject.SetActive(false);
            }
            HideOptionalChildRecursive(instance.transform, "DelBtn");
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            instance.SetActive(true);
            spawnedViews.Add(instance);
        }

        private void ResolvePrefabs()
        {
            if (noRewardPrefab == null)
            {
                noRewardPrefab = Resources.Load<GameObject>(NoRewardPrefabPath);
            }

            if (rewardItemPrefab == null)
            {
                rewardItemPrefab = Resources.Load<GameObject>(AuthoredRewardPrefabPath) ??
                                   Resources.Load<GameObject>(FallbackRewardPrefabPath);
            }
        }

        private void ClearSpawnedViews()
        {
            foreach (var view in spawnedViews)
            {
                if (view == null)
                {
                    continue;
                }

                view.SetActive(false);
                Destroy(view);
            }
            spawnedViews.Clear();
        }

        private static void HideOptionalChildRecursive(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == childName)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            ClearSpawnedViews();
        }
    }
}
