# Rune Combat Runtime Boundary V1

Date: 2026-08-19

## Delivered slice

`RuneCombat.cs` and its Unity `.meta` moved together from the monolithic Runtime folder to
`Assets/DragonBound/Runtime/Runes/Runtime`. `DragonBound.Runes.Runtime` now references only
`DragonBound.Foundation.Contracts` and `DragonBound.Combat.Contracts` for this boundary.

The moved code contains deterministic per-hero combat event/state primitives:

- `RuneCombatEvent` and `RuneCombatEventType`
- `RuneDamageContext` and source classification
- `RuneRuntimeState` counters, cooldown, summon and temporary-buff state
- `RuneEventLayer` per-runtime-hero event isolation

No scene, prefab, ProjectSettings, server authority, Rune values, Item values, Boss values or
Production behavior were changed.

## Deliberate remaining boundary

`RuneEffectExecutor`, `RuneDrops`, `RunePersistence`, and profile mutation remain in the existing
`DragonBound.Runtime` assembly. `RuneEffectExecutor` directly consumes `EnemyRuntime` and
`EnemyRegistry`; moving it before a typed target/damage adapter exists would create a reverse
dependency from `Runes.Runtime` into Core and break the intended DAG. The next Rune migration must
first extract an enemy target snapshot and damage-application port from `Combat.Contracts` or a
Rune-specific contract assembly.

## Verification

- Rune targeted EditMode: **45/45**, XML `Logs/RuneCombatBoundary-Targeted.xml`, log `Logs/RuneCombatBoundary-Targeted.log`.
- Fast EditMode: **572/572**, XML `Logs/RuneCombatBoundary-Fast.xml`, log `Logs/RuneCombatBoundary-Fast.log`.
- PlayMode: **29/29**, XML `Logs/RuneCombatBoundary-Play.xml`, log `Logs/RuneCombatBoundary-Play.log`.
- `git diff --check`: passed.

## Ownership and rollback

Rune Runtime owns event/state primitives. Combat/Enemies integration owns effect execution until the
typed target/damage port is published. Rollback is a single commit revert; source and `.meta` stay
paired.
