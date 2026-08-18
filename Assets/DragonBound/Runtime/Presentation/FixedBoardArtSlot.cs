using DragonBound.Grid;
using UnityEngine;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Semantic handoff point for a fixed-board cell. Artists can replace the ART_* children
    /// without changing coordinates, input, occupancy, or runtime ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FixedBoardArtSlot : MonoBehaviour
    {
        [SerializeField] private string artSlotId;
        [SerializeField] private FixedBoardCellRole role;
        [SerializeField] private FixedBoardCellOwner owner;
        [SerializeField] private FixedBoardDeployState deployState;
        [SerializeField] private string surfaceArtSlotId;
        [SerializeField] private bool hasBorder;
        [SerializeField] private bool hasLockMarker;

        public string ArtSlotId => artSlotId;
        public FixedBoardCellRole Role => role;
        public FixedBoardCellOwner Owner => owner;
        public FixedBoardDeployState DeployState => deployState;
        /// <summary>
        /// ART_* identifier for the independently replaceable visible surface of this cell.
        /// It is allowed to change presentation only; the root binding remains gameplay-owned.
        /// </summary>
        public string SurfaceArtSlotId => surfaceArtSlotId;
        public bool HasBorder => hasBorder;
        public bool HasLockMarker => hasLockMarker;

        public void Bind(FixedBoardCellDefinition definition)
        {
            artSlotId = definition.ArtSlotId;
            role = definition.Role;
            owner = definition.Owner;
            deployState = definition.DeployState;
        }

        public void BindPresentationContract(string surfaceSlot, bool border, bool lockMarker)
        {
            surfaceArtSlotId = surfaceSlot;
            hasBorder = border;
            hasLockMarker = lockMarker;
        }
    }
}
