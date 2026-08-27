using UnityEngine;

namespace DragonBound.Presentation
{
    public enum CombatFxPlacementMode
    {
        TemplateSizeAtTarget = 0,
        StretchBetweenPoints = 1,
        Projectile = 2,
        GameplayRadius = 3
    }

    // Optional per-template settings. Add this component to an ART_* effect template
    // when its runtime clone needs artist-authored layout or timing overrides.
    [DisallowMultipleComponent]
    public sealed class CombatFxAuthoring : MonoBehaviour
    {
        [SerializeField] private CombatFxPlacementMode placement = CombatFxPlacementMode.TemplateSizeAtTarget;
        [SerializeField] private Vector2 positionOffset;
        [SerializeField] private bool orientToAttackDirection;
        [SerializeField, Min(0.01f)] private float duration = 0.28f;
        [SerializeField] private bool fade = true;
        [SerializeField, Min(0.01f)] private float lengthScale = 1f;
        [SerializeField, Min(0.01f)] private float radiusScale = 1f;

        public CombatFxPlacementMode Placement => placement;
        public Vector2 PositionOffset => positionOffset;
        public bool OrientToAttackDirection => orientToAttackDirection;
        public float Duration => Mathf.Max(0.01f, duration);
        public bool Fade => fade;
        public float LengthScale => Mathf.Max(0.01f, lengthScale);
        public float RadiusScale => Mathf.Max(0.01f, radiusScale);

        public void Configure(
            CombatFxPlacementMode placementMode,
            float lifetime,
            bool shouldFade,
            bool shouldOrientToAttackDirection = false)
        {
            placement = placementMode;
            duration = Mathf.Max(0.01f, lifetime);
            fade = shouldFade;
            orientToAttackDirection = shouldOrientToAttackDirection;
        }
    }
}
