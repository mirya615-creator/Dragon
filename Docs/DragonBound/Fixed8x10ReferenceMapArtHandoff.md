# Fixed8x10 Reference Map Art Handoff

`Fixed8x10_ReferenceMap01` owns one continuous 8 by 10 board. Gameplay coordinates,
cell roles, input receivers, unit anchors, effects, and enemy waypoints remain runtime-owned.
Art may replace the `ART_*` nodes listed below without moving or deleting those anchors.

## Board Nodes

`ART_MapBackground`, `ART_MapFrame`, `ART_AiHalfBackground`, `ART_PlayerHalfBackground`,
`ART_CenterDivider`, and `ART_ForegroundDecoration` are independent map-level art slots.
`ART_CenterDivider` is a presentation seam between rows 4 and 5 only.

## Cell Nodes

Every map cell has a `FixedBoardArtSlot` root binding and an `ART_Cell_Border` child.
Deployment surfaces are `ART_Cell_Unlocked` or `ART_Cell_Locked`. Locked cells additionally
own an `ART_LockMarker`, which replaces the former repeated `LOCK` text.

Lane surfaces are determined from the explicit ordered path and use one of
`ART_LaneStraightHorizontal`, `ART_LaneStraightVertical`, `ART_LaneCornerLeftUp`,
`ART_LaneCornerLeftDown`, `ART_LaneCornerRightUp`, or `ART_LaneCornerRightDown`.
Endpoints use `ART_PlayerSpawn`, `ART_PlayerGoal`, `ART_AiSpawn`, and `ART_AiGoal`.

## Replacement Boundary

Art may replace sprites, images, and decorative children below these `ART_*` nodes. It must
not modify `CellRoot`, `ContentAnchor`, grid coordinates, cell role, input receiver, drop
target, unit anchor, effect anchor, waypoint, path progress, or PairLink hit areas.

`DEV_BoardDebugOverlay` is a disabled development-only, non-raycast layer and is not part of
the final UI hierarchy. Its `ShowAttackRange` toggle reuses the existing selected-unit range
preview; it does not select units, change targeting, or alter drag interaction.
