using UnityEngine;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Identifies an independently replaceable map-wide art node. It deliberately has no
    /// reference to game state, ensuring art replacement cannot alter board geometry or input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FixedBoardMapArtSlot : MonoBehaviour
    {
        [SerializeField] private string artSlotId;

        public string ArtSlotId => artSlotId;

        public void Bind(string value)
        {
            artSlotId = value;
        }
    }
}
