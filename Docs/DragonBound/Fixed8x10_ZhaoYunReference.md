# Fixed8x10 ReferenceMap01

## Frozen Default Map

`Fixed8x10_ReferenceMap01` is the default fixed board. It renders one 8 x 10
rectangle: AI owns ConfigRows 0 to 4 at the screen top and Player owns
ConfigRows 5 to 9 at the screen bottom. Every displayed coordinate is one
standard square cell.

The authored map convention is top-down. Runtime Unity anchors use bottom-up
coordinates, so the conversion is fixed and explicit:

```text
runtimeX = ConfigColumn
runtimeY = 9 - ConfigRow
```

The five bench slots are outside the map at runtime `y = -1`; they remain in
the authored `BenchWorkshopArea` and are never map cells.

## Exact Role Mask

```text
       C0 C1 C2 C3 C4 C5 C6 C7
R0:    G  L  L  L  L  L  L  S
R1:    R  L  L  U  U  U  L  R
R2:    R  L  L  U  U  U  L  R
R3:    R  L  L  R  R  R  R  R
R4:    R  R  R  R  L  L  L  L
R5:    L  L  L  L  R  R  R  R
R6:    R  R  R  R  R  L  L  R
R7:    R  L  U  U  U  L  L  R
R8:    R  L  U  U  U  L  L  R
R9:    S  L  L  L  L  L  L  G
```

`U` is an unlocked deployment cell, `L` is a locked expandable deployment
cell, `R` is a road cell, `S` is a spawn cell, and `G` is a goal cell. The
mask has 12 U, 36 L, 28 R, 2 S, and 2 G cells. It intentionally has no
undefined, generic environment, or generic terrain role.

AI and Player both have 6 unlocked deployment cells, 18 locked deployment
cells, one spawn, and one goal. The entire role table is rotationally
symmetric around the center of the 8 x 10 rectangle.

## Explicit Lanes

The route is ordered data, not inferred from the road-tile set.

```text
Player config path:
R9C0 -> R8C0 -> R7C0 -> R6C0 -> R6C1 -> R6C2 -> R6C3 -> R6C4
      -> R5C4 -> R5C5 -> R5C6 -> R5C7 -> R6C7 -> R7C7 -> R8C7 -> R9C7

AI config path:
R0C7 -> R1C7 -> R2C7 -> R3C7 -> R3C6 -> R3C5 -> R3C4 -> R3C3
      -> R4C3 -> R4C2 -> R4C1 -> R4C0 -> R3C0 -> R2C0 -> R1C0 -> R0C0
```

Each ordered path begins on S, ends on G, and visits every R tile belonging
to its side exactly once. Adjacent waypoints are orthogonal.

## Presentation And Art Handoff

```text
ART_MapBackground
ART_MapFrame
ART_CenterDivider
ART_AiHalfBackground
ART_PlayerHalfBackground
ART_AiSpawnGate / ART_PlayerSpawnGate
ART_AiGoal / ART_PlayerGoal
ART_Cell_Unlocked
ART_Cell_Locked
ART_Cell_Border
ART_Cell_Decoration
ART_LockMarker
ART_LaneBase
ART_LaneEdge
ART_LaneDirection
```

`FixedBoardArtSlot` records each cell's authored role and `ART_*` id. Art may
replace those visual children without moving a cell's `ContentAnchor`, input
receiver, drop target, unit anchor, effect anchor, or lane waypoint. Roads,
spawn, and goal cells are presentation-only: their graphics have raycasts
disabled and they are never registered with `BoardGrid` as placement targets.

`ART_LaneBase` is instantiated and bound one `R` cell at a time. It is never
stretched across adjacent cells or represented as a detached road object.
`S` and `G` retain their own full-cell gate and goal slots.

`ART_CenterDivider` is an overlay along the R4/R5 boundary only. It does not
add a row or modify either row's cell centers.
