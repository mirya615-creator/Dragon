using System;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class BottomNavigationSelection : MonoBehaviour
    {
        public enum NavigationItem
        {
            Ranking,
            Main,
            Bag
        }

        [SerializeField] private Button rankingButton;
        [SerializeField] private Image rankingImage;
        [SerializeField] private Button mainButton;
        [SerializeField] private Image mainImage;
        [SerializeField] private Button bagButton;
        [SerializeField] private Image bagImage;
        [SerializeField] private NavigationItem initialSelection = NavigationItem.Main;

        private static readonly Color SelectedColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color UnselectedColor = new Color(1f, 1f, 1f, 0f);
        private bool listenersAdded;

        [SerializeField] private GameObject leaderPanel;
        [SerializeField] private GameObject weaponPanel;

        private bool syncWithPanels;
        private NavigationItem currentSelection = NavigationItem.Main;
        private bool hasApplied;

        private void Awake()
        {
            ResolvePanels();
            ApplySelection(initialSelection);   // 首帧就对，不等 Start
        }

        private void Start()
        {
            Initialize();                        // sceneLoaded 的 onClick 重建已完成
        }

        private void Update()
        {
            if (!syncWithPanels) return;
            NavigationItem resolved = weaponPanel.activeSelf ? NavigationItem.Bag
                : leaderPanel.activeSelf ? NavigationItem.Ranking
                : NavigationItem.Main;
            if (resolved != currentSelection) ApplySelection(resolved);
        }

        private void ResolvePanels()
        {
            if (leaderPanel == null) leaderPanel = FindSceneObject("LeaderPanel");
            if (weaponPanel == null) weaponPanel = FindSceneObject("WeaponPanel");
            syncWithPanels = leaderPanel != null && weaponPanel != null;
        }

        private GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                Transform found = FindDescendant(root.transform, objectName);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindDescendant(Transform current, string objectName)
        {
            if (current == null) return null;
            if (current.name == objectName) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform found = FindDescendant(current.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }
        public void Initialize()
        {
            if (rankingButton == null || rankingImage == null ||
                mainButton == null || mainImage == null ||
                bagButton == null || bagImage == null)
            {
                throw new InvalidOperationException("Bottom navigation button and image references must all be assigned.");
            }

            if (listenersAdded) return;
            rankingButton.onClick.AddListener(SelectRanking);
            mainButton.onClick.AddListener(SelectMain);
            bagButton.onClick.AddListener(SelectBag);
            listenersAdded = true;
        }

        private void OnDestroy()
        {
            if (rankingButton != null) rankingButton.onClick.RemoveListener(SelectRanking);
            if (mainButton != null) mainButton.onClick.RemoveListener(SelectMain);
            if (bagButton != null) bagButton.onClick.RemoveListener(SelectBag);
            listenersAdded = false;
        }

        public void Configure(
            Button ranking,
            Image rankingStateImage,
            Button main,
            Image mainStateImage,
            Button bag,
            Image bagStateImage,
            NavigationItem selected = NavigationItem.Main)
        {
            rankingButton = ranking;
            rankingImage = rankingStateImage;
            mainButton = main;
            mainImage = mainStateImage;
            bagButton = bag;
            bagImage = bagStateImage;
            initialSelection = selected;

            DisableColorTint(rankingButton);
            DisableColorTint(mainButton);
            DisableColorTint(bagButton);
            hasApplied = false;
            ApplySelection(initialSelection);
        }

        public void ApplySelection(NavigationItem selected)
        {
            if (hasApplied && selected == currentSelection) return;
            SetColor(rankingImage, selected == NavigationItem.Ranking);
            SetColor(mainImage, selected == NavigationItem.Main);
            SetColor(bagImage, selected == NavigationItem.Bag);
            currentSelection = selected;
            hasApplied = true;
        }

        private void SelectRanking() => ApplySelection(NavigationItem.Ranking);

        private void SelectMain() => ApplySelection(NavigationItem.Main);

        private void SelectBag() => ApplySelection(NavigationItem.Bag);

        private static void SetColor(Graphic graphic, bool selected)
        {
            if (graphic != null) graphic.color = selected ? SelectedColor : UnselectedColor;
        }

        private static void DisableColorTint(Selectable selectable)
        {
            if (selectable != null) selectable.transition = Selectable.Transition.None;
        }
    }
}
