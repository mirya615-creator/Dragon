using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Version-specific authored slots used by the Merchant owned-item view.
    /// A scene without this component keeps the legacy dynamic-column layout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MerchantOwnedItemSlotLayout : MonoBehaviour
    {
        [SerializeField] private Image[] activeSlots = Array.Empty<Image>();
        [SerializeField] private Image[] passiveSlots = Array.Empty<Image>();

        public bool IsConfigured =>
            activeSlots != null && activeSlots.Length > 0 &&
            passiveSlots != null && passiveSlots.Length > 0;

        public bool ConfigureFromColumns(Transform activeColumn, Transform passiveColumn)
        {
            Image[] resolvedActiveSlots = CollectDirectImageChildren(activeColumn);
            Image[] resolvedPassiveSlots = CollectDirectImageChildren(passiveColumn);
            if (resolvedActiveSlots.Length == 0 || resolvedPassiveSlots.Length == 0) return false;

            activeSlots = resolvedActiveSlots;
            passiveSlots = resolvedPassiveSlots;
            return true;
        }

        public Image GetFirstAvailableSlot(bool active)
        {
            Image[] slots = GetSlots(active);
            if (slots == null) return null;

            for (int index = 0; index < slots.Length; index++)
            {
                Image slot = slots[index];
                if (slot != null && !slot.gameObject.activeSelf) return slot;
            }

            return null;
        }

        public int CountOccupiedSlots(bool active)
        {
            Image[] slots = GetSlots(active);
            if (slots == null) return 0;

            int count = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                Image slot = slots[index];
                if (slot != null && slot.gameObject.activeSelf) count++;
            }

            return count;
        }

        public void ResetSlots()
        {
            ResetSlots(activeSlots);
            ResetSlots(passiveSlots);
        }

        private Image[] GetSlots(bool active)
        {
            return active ? activeSlots : passiveSlots;
        }

        private static Image[] CollectDirectImageChildren(Transform column)
        {
            if (column == null) return Array.Empty<Image>();

            var slots = new List<Image>(column.childCount);
            for (int index = 0; index < column.childCount; index++)
            {
                Image slot = column.GetChild(index).GetComponent<Image>();
                if (slot != null) slots.Add(slot);
            }

            return slots.ToArray();
        }

        private static void ResetSlots(Image[] slots)
        {
            if (slots == null) return;

            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                Image slot = slots[slotIndex];
                if (slot == null) continue;

                for (int childIndex = slot.transform.childCount - 1; childIndex >= 0; childIndex--)
                {
                    GameObject child = slot.transform.GetChild(childIndex).gameObject;
                    if (!child.name.StartsWith("Item_", StringComparison.Ordinal)) continue;
                    child.SetActive(false);
                    Destroy(child);
                }

                slot.gameObject.SetActive(false);
            }
        }
    }
}
