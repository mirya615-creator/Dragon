using System;
using System.Collections.Generic;
using DragonBound.Items;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>Editable Item loadout surface for the development client boundary.</summary>
    public sealed class ItemLoadoutView : MonoBehaviour
    {
        private readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Button> itemButtons = new List<Button>();
        private readonly List<Text> itemLabels = new List<Text>();
        private DevelopmentItemRunSnapshotProvider provider;
        private Func<bool> canEdit;
        [SerializeField] private Text stateLabel;
        [SerializeField] private Text countLabel;
        [SerializeField] private Transform itemList;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button applyButton;
        private bool initialized;

        public bool IsOpen => gameObject.activeSelf;

        public static ItemLoadoutView CreateRuntime(Transform parent)
        {
            var root = new GameObject("ART_ItemLoadout", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.10f);
            rect.anchorMax = new Vector2(0.92f, 0.90f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.12f, 0.98f);

            var view = root.AddComponent<ItemLoadoutView>();
            view.BuildRuntimeUi();
            root.SetActive(false);
            return view;
        }

        public void Initialize(DevelopmentItemRunSnapshotProvider value, Func<bool> editGate)
        {
            BindAuthoredControls();
            provider = value;
            canEdit = editGate ?? (() => true);
            initialized = provider != null;
            Refresh();
        }

        public void Open()
        {
            if (!initialized) return;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        public void Refresh()
        {
            if (!initialized) return;
            provider.TryGetValidatedSnapshots(out var snapshot, out _, out _);
            selected.Clear();
            foreach (var itemId in snapshot.ActiveItems) selected.Add(itemId);
            foreach (var itemId in snapshot.PassiveItems) selected.Add(itemId);
            var active = snapshot.ActiveItems.Count;
            var passive = snapshot.PassiveItems.Count;
            var editable = canEdit();
            if (stateLabel != null)
            {
                stateLabel.text = editable
                    ? "LOADOUT EDITING | RUN START LOCKS ITEMS"
                    : "RUN STARTED | LOADOUT LOCKED";
            }
            if (countLabel != null)
            {
                countLabel.text = $"ACTIVE {active}/2     PASSIVE {passive}/6";
            }
            for (var index = 0; index < itemButtons.Count; index++)
            {
                var itemId = itemButtons[index].name.Substring("Item_".Length);
                var definition = ItemCatalog.Get(itemId);
                var isSelected = selected.Contains(itemId);
                itemButtons[index].interactable = editable && (isSelected || HasRoom(definition));
                itemLabels[index].text = (isSelected ? "[EQUIPPED] " : "") +
                                         FriendlyName(itemId) + "\n" + definition.Rarity;
            }
        }

        private void BuildRuntimeUi()
        {
            var title = CreateText("TITLE", transform, "ITEM LOADOUT", 30, new Vector2(0.06f, 0.90f), new Vector2(0.60f, 0.98f));
            title.alignment = TextAnchor.MiddleLeft;
            stateLabel = CreateText("STATE", transform, "", 16, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.90f));
            countLabel = CreateText("COUNT", transform, "", 18, new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.84f));
            itemList = new GameObject("ItemList", typeof(RectTransform)).transform;
            itemList.SetParent(transform, false);
            var listRect = itemList.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.05f, 0.16f);
            listRect.anchorMax = new Vector2(0.95f, 0.77f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;

            var index = 0;
            foreach (var definition in ItemCatalog.FormalCandidates)
            {
                var buttonObject = new GameObject("Item_" + definition.ItemId, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(itemList, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                var column = index % 2;
                var row = index / 2;
                rect.anchorMin = new Vector2(column == 0 ? 0f : 0.51f, 1f - (row + 1) * 0.105f);
                rect.anchorMax = new Vector2(column == 0 ? 0.49f : 1f, 1f - row * 0.105f - 0.01f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var image = buttonObject.GetComponent<Image>();
                image.color = definition.Category == ItemCategory.Active
                    ? new Color(0.18f, 0.31f, 0.38f, 1f)
                    : new Color(0.30f, 0.27f, 0.35f, 1f);
                var button = buttonObject.GetComponent<Button>();
                button.targetGraphic = image;
                var label = CreateText("Label", buttonObject.transform, "", 15, Vector2.zero, Vector2.one);
                label.alignment = TextAnchor.MiddleCenter;
                button.onClick.AddListener(() => ToggleItem(definition.ItemId));
                itemButtons.Add(button);
                itemLabels.Add(label);
                index++;
            }

            closeButton = CreateButton("CLOSE", transform, "CLOSE", new Vector2(0.72f, 0.90f), new Vector2(0.94f, 0.98f));
            closeButton.onClick.AddListener(Close);
            applyButton = CreateButton("APPLY", transform, "APPLY", new Vector2(0.36f, 0.05f), new Vector2(0.64f, 0.13f));
            applyButton.onClick.AddListener(Apply);
        }

        private void BindAuthoredControls()
        {
            if (stateLabel == null) stateLabel = transform.Find("STATE")?.GetComponent<Text>();
            if (countLabel == null) countLabel = transform.Find("COUNT")?.GetComponent<Text>();
            if (itemList == null) itemList = transform.Find("ItemList");
            if (closeButton == null) closeButton = transform.Find("CLOSE")?.GetComponent<Button>();
            if (applyButton == null) applyButton = transform.Find("APPLY")?.GetComponent<Button>();
            if (stateLabel == null || countLabel == null || itemList == null || closeButton == null || applyButton == null)
            {
                throw new InvalidOperationException("The authored Item loadout hierarchy is incomplete.");
            }

            itemButtons.Clear();
            itemLabels.Clear();
            foreach (Transform child in itemList)
            {
                if (child == null || !child.name.StartsWith("Item_", StringComparison.Ordinal)) continue;
                var button = child.GetComponent<Button>();
                var label = child.Find("Label")?.GetComponent<Text>();
                if (button == null || label == null) continue;
                var itemId = child.name.Substring("Item_".Length);
                button.onClick.AddListener(() => ToggleItem(itemId));
                itemButtons.Add(button);
                itemLabels.Add(label);
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
            applyButton.onClick.RemoveListener(Apply);
            applyButton.onClick.AddListener(Apply);
        }

        private void ToggleItem(string itemId)
        {
            if (!canEdit()) return;
            var definition = ItemCatalog.Get(itemId);
            if (selected.Contains(itemId)) selected.Remove(itemId);
            else if (HasRoom(definition)) selected.Add(itemId);
            RefreshSelectionLabels();
        }

        private void Apply()
        {
            if (!canEdit()) return;
            var ids = new List<string>(selected);
            if (!provider.TryConfigure(ids, false, out _)) return;
            Refresh();
        }

        private void RefreshSelectionLabels()
        {
            for (var index = 0; index < itemButtons.Count; index++)
            {
                var itemId = itemButtons[index].name.Substring("Item_".Length);
                itemLabels[index].text = (selected.Contains(itemId) ? "[SELECTED] " : "") + FriendlyName(itemId);
            }
        }

        private bool HasRoom(ItemDefinition definition)
        {
            if (definition == null) return false;
            var count = 0;
            foreach (var itemId in selected)
            {
                if (ItemCatalog.Get(itemId)?.Category == definition.Category) count++;
            }
            var limit = definition.Category == ItemCategory.Active ? ItemLoadout.MaxActiveItems : ItemLoadout.MaxPassiveItems;
            return count < limit;
        }

        private static string FriendlyName(string itemId) => itemId.Replace("ITEM_", string.Empty).Replace('_', ' ');

        private static Text CreateText(string name, Transform parent, string text, int size, Vector2 min, Vector2 max)
        {
            var objectValue = new GameObject(name, typeof(RectTransform), typeof(Text));
            objectValue.transform.SetParent(parent, false);
            var rect = objectValue.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var label = objectValue.GetComponent<Text>();
            label.text = text; label.fontSize = size; label.color = Color.white; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return label;
        }

        private static Button CreateButton(string name, Transform parent, string text, Vector2 min, Vector2 max)
        {
            var objectValue = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            objectValue.transform.SetParent(parent, false);
            var rect = objectValue.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = objectValue.GetComponent<Image>(); image.color = new Color(0.25f, 0.42f, 0.45f, 1f);
            var button = objectValue.GetComponent<Button>(); button.targetGraphic = image;
            var label = CreateText("Label", objectValue.transform, text, 18, Vector2.zero, Vector2.one); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
