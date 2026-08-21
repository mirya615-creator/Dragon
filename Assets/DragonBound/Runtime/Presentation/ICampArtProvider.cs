using UnityEngine;

namespace DragonBound.Presentation
{
    public interface ICampArtProvider
    {
        bool TryGetBasicUnitSprite(string unitId, out Sprite sprite);
        bool TryGetHeroComponentSprite(string componentId, out Sprite sprite);
        bool TryGetHeroSprite(string heroId, out Sprite sprite);
    }
}
