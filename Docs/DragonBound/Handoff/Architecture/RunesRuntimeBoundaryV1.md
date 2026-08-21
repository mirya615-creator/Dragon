# Runes Runtime Boundary V1

## Scope

Rune 6A moves only the pure data and out-of-run loadout layer into `DragonBound.Runes.Runtime`:
`RuneDefinitions`, `RuneInventory`, `HeroRuneLoadout`, `RuneLoadoutAssignment`, `RunePresentation`
and `RuneModifiers`. The existing `DragonBound.Runes` namespace and behavior remain unchanged.

## Dependency direction

`DragonBound.Runes.Runtime` has `noEngineReferences: true` and no assembly references. It does not
reference Core, Analytics, Recruitment, Unity, server code or Item runtime. The remaining
`RuneCombat`, `RuneEffectExecutor`, `RuneDrops`, `RunePersistence`, profile operations and
analytics bridge stay in `DragonBound.Runtime` until their ports are extracted.

## Verification

- Rune targeted fixtures: `21/21` passed.
- Fast EditMode: `572/572` passed.
- PlayMode: `29/29` passed.

No Rune values, Day 3 gate, persistence schema, reward behavior, scene or prefab was changed.

## Next slice

Extract Rune combat modifiers behind `Rune.Contracts` and a typed combat modifier port. Keep Rune
drop/reward authority and profile persistence in the integration owner until server adapters exist.
