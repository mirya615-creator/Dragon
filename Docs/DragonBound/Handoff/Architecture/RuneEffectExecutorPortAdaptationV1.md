# Rune Effect Executor Port Adaptation V1

## Scope

This slice adapts `RuneEffectExecutor` to the typed target seam introduced by
`9984936`, without changing the public Hero combat contract. `HeroCombatState` and
`HeroPairCombatProxy` continue to call the existing `RuneCombatContext` and
`RuneDamageResult` methods.

The executor now has parallel port-based overloads using:

- `RuneTargetCombatContext`
- `IRuneCombatTarget`
- `IRuneCombatTargetRegistry`
- `RuneTargetDamageResult`

Target selection, Frostbite classification, damage application, shield/health result
reporting, and Warcry commands are executed through the port path. The legacy overloads
wrap `EnemyRuntime`/`EnemyRegistry` with the Core-owned adapters and map results back by
stable `RuntimeId`. This keeps the existing Hero result shape and does not require a
`HeroCombatState` change.

## Compatibility Guarantees

- Rune effect parameters and numeric values are unchanged.
- Random event names and `randomPrefix` construction are unchanged, so deterministic
  stream keys and call order remain owned by the existing executor.
- Target ordering remains the existing ordinal `RuntimeId` ordering, with the same
  frontmost/path-progress and distance tie-breaks.
- The legacy API remains available for current production callers.
- No service authority, Scene, Prefab, Bootstrap, production balance, or Google
  configuration was changed.

## Assembly Boundary

Superseded by `RuneEffectExecutionEngineRuntimeBoundaryV1.md`. The pure engine now lives
in `DragonBound.Runes.Runtime`; `RuneEffectExecutor` is retained in the monolithic
runtime as the legacy `EnemyRuntime` facade for current HeroCombatState callers.

## Verification

- Executor and port targeted: 14/14 passed, 0.154 s.
  XML: `Logs/RuneExecutorPort-Targeted.xml`
  Log: `Logs/RuneExecutorPort-Targeted.log`
- Full Rune targeted: 57/57 passed, 0.368 s.
  XML: `Logs/RuneExecutorPort-RuneTargeted.xml`
  Log: `Logs/RuneExecutorPort-RuneTargeted.log`
- Fast EditMode: 576/576 passed, 15.531 s.
  XML: `Logs/RuneExecutorPort-Fast.xml`
  Log: `Logs/RuneExecutorPort-Fast.log`
- PlayMode: 29/29 passed, 25.531 s.
  XML: `Logs/RuneExecutorPort-Play.xml`
  Log: `Logs/RuneExecutorPort-Play.log`
