using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Inspector-owned workshop art bindings. Art slot IDs stay in the frozen content catalog,
    /// so changing a sprite can never change a component, hero, recipe, or runtime save key.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HeroWorkshopArtCatalog",
        menuName = "DragonBound/Presentation/Hero Workshop Art Catalog")]
    public sealed class HeroWorkshopArtCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "DragonBound/HeroWorkshopArtCatalog";

        [SerializeField] private List<ArtSlot> componentSlots = new List<ArtSlot>();
        [SerializeField] private List<ArtSlot> heroSlots = new List<ArtSlot>();

        public int ComponentSlotCount => componentSlots.Count;
        public int HeroSlotCount => heroSlots.Count;

        public Sprite GetComponentSprite(string artSlotId)
        {
            return GetSprite(componentSlots, artSlotId);
        }

        public Sprite GetHeroSprite(string artSlotId)
        {
            return GetSprite(heroSlots, artSlotId);
        }

        public bool HasComponentSlot(string artSlotId)
        {
            return HasSlot(componentSlots, artSlotId);
        }

        public bool HasHeroSlot(string artSlotId)
        {
            return HasSlot(heroSlots, artSlotId);
        }

        private static Sprite GetSprite(IReadOnlyList<ArtSlot> slots, string artSlotId)
        {
            if (slots == null || string.IsNullOrWhiteSpace(artSlotId))
            {
                return null;
            }

            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                if (slot != null && string.Equals(slot.ArtSlotId, artSlotId, StringComparison.Ordinal))
                {
                    return slot.Sprite;
                }
            }

            return null;
        }

        private static bool HasSlot(IReadOnlyList<ArtSlot> slots, string artSlotId)
        {
            if (slots == null || string.IsNullOrWhiteSpace(artSlotId))
            {
                return false;
            }

            for (var index = 0; index < slots.Count; index++)
            {
                if (slots[index] != null &&
                    string.Equals(slots[index].ArtSlotId, artSlotId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        public sealed class ArtSlot
        {
            [SerializeField] private string artSlotId;
            [SerializeField] private Sprite sprite;

            public string ArtSlotId => artSlotId;
            public Sprite Sprite => sprite;
        }
    }
}
