using System;
using UnityEngine;

namespace DragonBound.Presentation
{
    [CreateAssetMenu(
        menuName = "DragonBound/UI/Camp Art Catalog",
        fileName = "CampArtCatalog")]
    public sealed class CampArtCatalog : ScriptableObject, ICampArtProvider
    {
        [Serializable]
        private struct SpriteEntry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private SpriteEntry[] basicUnits = new SpriteEntry[0];
        [SerializeField] private SpriteEntry[] heroComponents = new SpriteEntry[0];
        [SerializeField] private SpriteEntry[] heroes = new SpriteEntry[0];

        public bool TryGetBasicUnitSprite(string unitId, out Sprite sprite)
        {
            return TryGet(basicUnits, unitId, out sprite);
        }

        public bool TryGetHeroComponentSprite(string componentId, out Sprite sprite)
        {
            return TryGet(heroComponents, componentId, out sprite);
        }

        public bool TryGetHeroSprite(string heroId, out Sprite sprite)
        {
            return TryGet(heroes, heroId, out sprite);
        }

        private static bool TryGet(SpriteEntry[] entries, string id, out Sprite sprite)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    if (string.Equals(entries[i].id, id, StringComparison.Ordinal) && entries[i].sprite != null)
                    {
                        sprite = entries[i].sprite;
                        return true;
                    }
                }
            }

            sprite = null;
            return false;
        }
    }
}
