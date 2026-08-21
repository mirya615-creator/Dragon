# Recruitment Runtime Boundary V1

## Status

The first migration slice is complete as a source move. `FiniteComponentRecruitmentConfig`
and `ShovelRecruitment` now compile under `DragonBound.Recruitment.Runtime`, a Unity-free
assembly that references only `GameShared.Runtime`.

## Included

- `FiniteComponentRecruitmentConfig`: V3/V2 recruitment balancing constants.
- `ShovelRecruitment`: Forge Pick eligibility, pity state, capture and restore data.
- Paired Unity `.meta` files moved with the source files; GUIDs were preserved.

## Excluded

`RecruitDeck`, `RecruitmentService`, `BoardRecruitDestination`, hero catalogs, diagnostics,
scene/prefab wiring and production values remain in `DragonBound.Runtime`. They still depend
on Core, Board, Combat, Runes or Unity and require adapter seams before moving.

## Dependency direction

`GameShared.Runtime <- DragonBound.Recruitment.Runtime <- DragonBound.Runtime`

The new assembly has `noEngineReferences: true` and `autoReferenced: false`. The monolithic
runtime and EditMode test assembly reference it explicitly. No server authority is introduced.

## Handoff rules

1. Recruitment owners may edit the two moved files without changing scene or prefab assets.
2. Changes to recruitment transaction, finite bag, PairLink or Board destination stay in the
   monolithic runtime until `Board.Contracts`, `Heroes.Contracts` and the recruitment ports are
   ready.
3. Unity `.meta` files and GUIDs must remain paired. Do not regenerate them.
4. The next slice is `Recruitment.Contracts`/ports, followed by the finite bag and deck; do not
   move `BoardRecruitDestination` in the same change.

## Verification scope

Static checks and `git diff --check` are required in this change. Targeted EditMode, Fast
EditMode and affected PlayMode must run after the Unity editor releases the project lock.
