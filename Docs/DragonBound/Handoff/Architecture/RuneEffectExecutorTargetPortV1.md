# Rune Effect Executor Target Port V1

## Decision

`RuneEffectExecutor` remains in the monolithic `DragonBound.Runtime` assembly for this
slice. It still owns `RuneCombatContext`, `RuneDamageResult`, deterministic target
selection, and the runtime Rune effect implementations, each of which currently exposes
`EnemyRuntime` or `EnemyRegistry` directly. Moving it now would make
`DragonBound.Runes.Runtime` depend on Core and violate the intended dependency direction.

This slice introduces the typed, read-only-at-the-boundary Combat target port needed for
an adapter-based migration:

```
DragonBound.Runes.Runtime -> DragonBound.Combat.Contracts
DragonBound.Core -> DragonBound.Combat.Contracts
DragonBound.Core: EnemyRuntime / EnemyRegistry adapters -> IRuneCombatTarget
```

`DragonBound.Runes.Runtime` has no reference to `EnemyRuntime` or `EnemyRegistry`.
`EnemyRuntimeRuneCombatTarget` and `EnemyRegistryRuneCombatTargetRegistry` are Core-owned
adapters, so Core implementation details do not enter the Rune assembly.

## Port Surface

`IRuneCombatTarget` exposes a stable runtime id, life/boss/path/position inspection,
typed Rune damage application (requested, shield, health, killed), and a typed movement
slow operation. `IRuneCombatTargetRegistry` returns a snapshot of those targets.

The port deliberately does not expose mutable enemy state, registry mutation, rewards,
or global runtime state. Existing production behaviour and Rune numbers are unchanged;
the port is not injected into `RuneEffectExecutor` in this slice.

## Remaining Migration Boundary

The next Rune combat slice must introduce Rune-owned context/result representations over
the port and an adapter at the Core composition root. It must also remove the executor's
direct use of `RuneDropRules.AlgorithmVersion` without changing deterministic stream
keys. Only after those changes can `RuneEffectExecutor` move with its `.meta` into
`DragonBound.Runes.Runtime` without an assembly cycle.

No server authority, scene, prefab, bootstrap, or production-balance setting changed.

## Verification

- `RuneCombatTargetPortTests`: 2/2 passed; 0.45 s.
  XML: `Logs/RuneTargetPort-Targeted.xml`
  Log: `Logs/RuneTargetPort-Targeted.log`
- Rune targeted suite: 47/47 passed; 0.42 s.
  XML: `Logs/RuneTargetPort-RuneTargeted.xml`
  Log: `Logs/RuneTargetPort-RuneTargeted.log`
- Fast EditMode: 574/574 passed; 15.20 s.
  XML: `Logs/RuneTargetPort-Fast.xml`
  Log: `Logs/RuneTargetPort-Fast.log`
- PlayMode: 29/29 passed; 26.23 s.
  XML: `Logs/RuneTargetPort-Play.xml`
  Log: `Logs/RuneTargetPort-Play.log`
