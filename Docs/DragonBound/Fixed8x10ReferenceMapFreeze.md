# Fixed8x10 Reference Map Freeze

## Scope

This document freezes the gameplay structure of `Fixed8x10_ReferenceMap01`.
It is a handoff reference, not a runtime source of truth. Runtime cells and ordered
lanes remain authored in `BattlefieldLayoutDefinitions.Fixed8x10ReferenceMap01`.

## Coordinates

- Config: `R0` is the screen top and `R9` is the screen bottom.
- Runtime: `Y0` is the screen bottom and `Y9` is the screen top.
- Conversion is centralized: `RuntimeX = ConfigColumn`, `RuntimeY = 9 - ConfigRow`.
- Presentation, drag, AI, pairing, enemy paths, and tests consume runtime coordinates.

## Frozen 8 by 10 Role Table

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

`U` is unlocked deployment, `L` is locked deployment, `R` is lane, `S` is spawn,
and `G` is goal. The counts are fixed at `80 / 12 / 36 / 28 / 2 / 2`.

## Ordered Lanes

Player:

```text
R9C0 R8C0 R7C0 R6C0 R6C1 R6C2 R6C3 R6C4
R5C4 R5C5 R5C6 R5C7 R6C7 R7C7 R8C7 R9C7
```

AI:

```text
R0C7 R1C7 R2C7 R3C7 R3C6 R3C5 R3C4 R3C3
R4C3 R4C2 R4C1 R4C0 R3C0 R2C0 R1C0 R0C0
```

Both lists are explicit, contain 16 nodes, are orthogonally adjacent, have no
duplicate nodes, and are 180-degree rotational counterparts at the same index.

## Runtime Guarantees

- Enemy `PathProgress` is normalized cumulative route distance from `0` to `1`.
- `SegmentProgress` is only local interpolation within the active segment.
- Frontmost target selection uses `PathProgress`, never world X/Y or node order alone.
- Lane tile shape is derived from the previous and next node in the explicit lane.
- Only the 12 `U` cells are initial battle cells. `L`, `R`, `S`, and `G` reject input.
- Initial player and AI masks each contain a 3 by 2 connected deployment region.
- The center divider is presentation-only between config rows 4 and 5.

## Art Boundary

Art may replace children below the documented `ART_*` nodes from
`Fixed8x10ReferenceMapArtHandoff.md`. It must not move or remove `CellRoot`,
cell coordinates, roles, input/drop receivers, unit/effect anchors, waypoints,
or PairLink hit areas. `DEV_BoardDebugOverlay` is a disabled non-raycast
development layer and is excluded from the production map hierarchy.
