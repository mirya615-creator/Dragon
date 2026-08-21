using System;
using System.Collections.Generic;
using DragonBound.Bootstrap;
using DragonBound.Core;
using DragonBound.Items;
using UnityEngine;

namespace DragonBound.Presentation
{
    /// <summary>Editor/development-build controls for manually exercising real Item and Rune paths.</summary>
    public sealed class DevelopmentGameplayTestPanel : MonoBehaviour
    {
        private readonly HashSet<string> selectedItems = new HashSet<string>(StringComparer.Ordinal);
        private DragonBoundBootstrap bootstrap;
        private DevelopmentItemRunSnapshotProvider itemProvider;
        private Rect windowRect;
        private Vector2 scroll;
        private bool isOpen = true;
        private bool mirrorItemsToAi;
        private string status = "Select up to 2 Active and 6 Passive Items.";

        public bool IsOpen => isOpen;

        public static DevelopmentGameplayTestPanel Create(
            DragonBoundBootstrap owner,
            DevelopmentItemRunSnapshotProvider provider)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            var panelObject = new GameObject("DEV_GameplayTestPanel");
            panelObject.transform.SetParent(owner.transform, false);
            var panel = panelObject.AddComponent<DevelopmentGameplayTestPanel>();
            panel.bootstrap = owner;
            panel.itemProvider = provider;
            return panel;
        }

        private void Update()
        {
            if (bootstrap?.Match == null || bootstrap.Match.State != MatchState.Ready)
            {
                isOpen = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                isOpen = !isOpen;
            }
        }

        private void OnGUI()
        {
            if (bootstrap?.Match == null || bootstrap.Match.State != MatchState.Ready)
            {
                return;
            }

            if (!isOpen)
            {
                if (GUI.Button(new Rect(12f, 12f, 150f, 42f), "DEV LOADOUT (F8)"))
                {
                    isOpen = true;
                }
                return;
            }

            var margin = 18f;
            var width = Mathf.Min(920f, Screen.width - margin * 2f);
            var height = Mathf.Min(1040f, Screen.height - margin * 2f);
            if (windowRect.width <= 0f || Math.Abs(windowRect.width - width) > 0.5f ||
                Math.Abs(windowRect.height - height) > 0.5f)
            {
                windowRect = new Rect((Screen.width - width) * 0.5f, margin, width, height);
            }

            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "DEV GAMEPLAY QA");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(6f);
            GUILayout.Label("ITEM LOADOUT");
            GUILayout.Label($"Selected: Active {SelectedCount(ItemCategory.Active)}/2 | Passive {SelectedCount(ItemCategory.Passive)}/6");
            mirrorItemsToAi = GUILayout.Toggle(mirrorItemsToAi, "Mirror Player Items to AI");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CLEAR ITEMS", GUILayout.Height(34f)))
            {
                selectedItems.Clear();
                status = "Item selection cleared.";
            }
            if (GUILayout.Button("FILL 2 ACTIVE + 6 PASSIVE", GUILayout.Height(34f)))
            {
                FillMaximumLoadout();
                status = "Maximum QA loadout selected. This is not a Production build definition.";
            }
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            DrawItemGroup(ItemCategory.Active);
            GUILayout.Space(8f);
            DrawItemGroup(ItemCategory.Passive);
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label("RUNE PROFILE");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("UNLOCK DAY 3 + GRANT ALL", GUILayout.Height(38f)))
            {
                PrepareRunes();
            }
            if (GUILayout.Button("OPEN RUNE LOADOUT", GUILayout.Height(38f)))
            {
                if (PrepareRunes())
                {
                    bootstrap.OpenDevelopmentRuneLoadout();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label(status);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("APPLY ITEMS", GUILayout.Height(42f)))
            {
                ApplyItems();
            }
            if (GUILayout.Button("APPLY + START RUN", GUILayout.Height(42f)) && ApplyItems())
            {
                isOpen = false;
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 28f));
        }

        private void DrawItemGroup(ItemCategory category)
        {
            GUILayout.Label(category == ItemCategory.Active ? "ACTIVE ITEMS" : "PASSIVE ITEMS");
            foreach (var definition in ItemCatalog.FormalCandidates)
            {
                if (definition.Category != category)
                {
                    continue;
                }

                var selected = selectedItems.Contains(definition.ItemId);
                var next = GUILayout.Toggle(
                    selected,
                    $"{FriendlyItemName(definition.ItemId)} | {definition.Rarity}",
                    GUILayout.Height(28f));
                if (next != selected)
                {
                    SetSelected(definition, next);
                }
            }
        }

        private void SetSelected(ItemDefinition definition, bool selected)
        {
            if (!selected)
            {
                selectedItems.Remove(definition.ItemId);
                status = "Selection updated.";
                return;
            }

            var limit = definition.Category == ItemCategory.Active
                ? ItemLoadout.MaxActiveItems
                : ItemLoadout.MaxPassiveItems;
            if (SelectedCount(definition.Category) >= limit)
            {
                status = definition.Category + " Item slot limit reached.";
                return;
            }

            selectedItems.Add(definition.ItemId);
            status = "Selection updated.";
        }

        private int SelectedCount(ItemCategory category)
        {
            var count = 0;
            foreach (var itemId in selectedItems)
            {
                if (ItemCatalog.Get(itemId)?.Category == category)
                {
                    count++;
                }
            }
            return count;
        }

        private void FillMaximumLoadout()
        {
            selectedItems.Clear();
            foreach (var definition in ItemCatalog.FormalCandidates)
            {
                var limit = definition.Category == ItemCategory.Active
                    ? ItemLoadout.MaxActiveItems
                    : ItemLoadout.MaxPassiveItems;
                if (SelectedCount(definition.Category) < limit)
                {
                    selectedItems.Add(definition.ItemId);
                }
            }
        }

        private bool PrepareRunes()
        {
            if (bootstrap.TryPrepareDevelopmentRuneProfile(out var reason))
            {
                status = "Rune profile unlocked at Day 3; all Runes granted for manual loadout testing.";
                return true;
            }

            status = "Rune setup rejected: " + reason;
            return false;
        }

        private bool ApplyItems()
        {
            var ordered = new List<string>();
            foreach (var definition in ItemCatalog.FormalCandidates)
            {
                if (selectedItems.Contains(definition.ItemId))
                {
                    ordered.Add(definition.ItemId);
                }
            }

            if (!itemProvider.TryConfigure(ordered, mirrorItemsToAi, out var reason))
            {
                status = "Item loadout rejected: " + reason;
                return false;
            }

            status = $"Item loadout applied: {ordered.Count} Player Items; AI " +
                     (mirrorItemsToAi ? "mirrors Player." : "uses no Items.");
            return true;
        }

        private static string FriendlyItemName(string itemId)
        {
            const string prefix = "ITEM_";
            return (itemId.StartsWith(prefix, StringComparison.Ordinal)
                    ? itemId.Substring(prefix.Length)
                    : itemId)
                .Replace('_', ' ');
        }
    }
}
