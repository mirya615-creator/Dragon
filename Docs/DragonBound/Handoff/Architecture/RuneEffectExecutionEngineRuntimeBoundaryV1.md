# Rune Effect Execution Engine Runtime Boundary V1

## Completed Boundary

`RuneEffectExecutionEngine` now lives in `DragonBound.Runes.Runtime` with its paired
Unity `.meta`. It executes only through `RuneTargetCombatContext`,
`IRuneCombatTarget`, `IRuneCombatTargetRegistry`, and `RuneTargetDamageResult`.
The assembly references only `GameShared.Runtime`, Foundation.Contracts, and
Combat.Contracts. It has no dependency on `EnemyRuntime`, `EnemyRegistry`, or the
monolithic `DragonBound.Runtime` assembly.

The old `RuneEffectExecutor`, `RuneCombatContext`, and `RuneDamageResult` remain in
`DragonBound.Runtime` as a thin compatibility facade. It creates Core-owned target
adapters and maps engine results back to the existing HeroCombatState-facing result type
by stable runtime id. HeroCombatState therefore retains its existing public behaviour.

## Determinism

`RuneCombatDeterminism.AlgorithmVersion` is Rune-owned and has the existing value
`RuneDrop.V1`. `RuneDropRules.AlgorithmVersion` now aliases that constant. The engine
therefore preserves the exact existing prefix:

```
RuneDrop.V1.Combat.{RuneId}.{HeroRuntimeId}.{EventName}.{Ordinal}
```

The event names and per-engine `randomCall` increment placement are unchanged.

## Regression Coverage

The boundary regression compares legacy facade and direct engine execution for:

- Skybreaker target ids, damage, kill state, shield damage, health damage, and random key;
- Frostbite normal and Boss slow parameters;
- Warcry command fields and random call order.

Existing executor and port tests continue to cover Ricochet, Volley, Longshot, Ambush,
Windhawk, Wyrmguard, Dragonbloom, and cooldown behaviour.

## Rune Ownership After This Slice

Independent in `DragonBound.Runes.Runtime`:

- Definitions, inventory, loadout, presentation and modifier data;
- Rune combat events, typed port context/result, runtime state and pure execution engine.

Still in monolithic `DragonBound.Runtime`:

- `RuneEffectExecutor` Legacy facade and Core enemy adapters;
- Reward/drop rules (`RuneDrops.cs`), profile operations, persistence and analytics/UI
  integration. These retain product/meta authority and are intentionally outside the
  combat-engine boundary.

## Next Boundary

Stop Rune migration here. The precise next Item effect boundary begins at
`Assets/DragonBound/Runtime/Items/ItemRuntime.cs` (`ItemRunContext`,
`IItemEffectRuntime`, `ItemEffectRuntimeFactory`) and
`Assets/DragonBound/Runtime/Items/ItemActiveCombatEffects.cs`
(`ItemCombatEffectTargeting`). These currently expose `EnemyRegistry` and
`EnemyRuntime` directly. Reuse the existing Combat target/damage port pattern through a
Core adapter before moving pure Item effect execution into `DragonBound.Items.Runtime`.
Do not migrate Item Economy/authority effects in that slice.

## Verification

- Executor/engine boundary targeted: 17/17 passed, 0.225 s.
  XML: `Logs/RuneEngineBoundary-Targeted.xml`
  Log: `Logs/RuneEngineBoundary-Targeted.log`
