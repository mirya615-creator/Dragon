# Runes Module Boundary V1

Date: 2026-08-18
Phase: Drakeforge Phase C-Rune-1
Status: Contract boundary only; no server implementation

## Purpose

`DragonBound.Runes.Contracts` is the future seam for authoritative Rune profile,
run-start snapshot and reward integration. It allows a later client adapter or
server-backed Integration module to replace the provider implementations while the
verified local Rune V1 loop remains in `DragonBound.Runtime` unchanged.

The contract assembly is Unity-free (`noEngineReferences`) and depends only on
`DragonBound.Foundation.Contracts` for `RunId` and `WaveNumber`. It has no dependency
on `Items.Contracts`, `DragonBound.Runtime`, Scenes, Prefabs, Bootstrap or Presentation.
Heroes and Combat contracts are not referenced because this boundary carries stable
HeroId strings and does not need their behavior interfaces yet.

## Contract surface

| Type | Boundary role |
| --- | --- |
| `RuneId`, `RuneRarity` | Stable Rune identity and the existing four rarity labels; no effect values. |
| `AccountDay`, `DayKey` | Typed values supplied by an external authority; this module never calculates either one. |
| `RuneProfileSnapshot` | Immutable read DTO for account day, content version, inventory counts and editable loadout assignments. |
| `LoadoutAssignment` | One stable HeroId to RuneId association. It does not implement ownership, copy limits or Basic/Hero policy. |
| `RunSnapshot` | Immutable run-start assignment DTO keyed by Foundation `RunId`; it is isolated from `DragonBound.Core.RunSnapshot`. |
| `RewardGrant` | Typed reward result DTO carrying an externally supplied grant identity and existing Rune identity fields. It has no drop algorithm or ledger behavior. |
| `FeatureGateResult` | Typed gate state for locked/unlocked/pending/not-configured consumers. |
| `IRuneProfileProvider` | Replaceable profile read port. |
| `IRuneSnapshotProvider` | Replaceable run-start snapshot port. |
| `IRuneRewardProvider` | Replaceable reward request port. |

`RuneContractStatus.Pending` and `RuneContractStatus.NotConfigured`, together with
the typed FeatureGate and RewardGrant states, make an unavailable authority explicit.
Callers must not interpret these states as a locally computed unlock, reward, inventory
mutation or successful claim.

## Dependency direction

```text
DragonBound.Foundation.Contracts
                ^
                |
DragonBound.Runes.Contracts
                ^
       future Integration adapter
```

The existing `DragonBound.Runtime` Rune implementation is not changed or made to
reference this assembly in this phase. The local `RunePersistence`, `RuneInventory`,
`RuneEffectExecutor`, `HeroRuneLoadout`, Bootstrap and Presentation behavior remain
the V1 production path. A future integration change must be separately reviewed and
must preserve that path until an adapter and regression tests exist.

## Explicit non-goals

This commit does not implement server code, API endpoints, database or persistence
services, authentication, authoritative Ledger logic, ad verification, server DayKey
calculation or deployment. It also does not implement Rune effects, formal Rune Builds,
drop probabilities, reward selection, inventory settlement, account state transitions,
Merchant or any Items integration.

The provider interfaces are ports only. Authority, validation, idempotency, awarding,
inventory mutation and account state remain owned by the future server/Integration
implementation.

## Shared seams still awaiting integration

- A trusted account/profile transport must map server data into `RuneProfileSnapshot`.
- An Integration owner must validate ownership and lock the `RunSnapshot` at RunStart.
- Reward settlement must supply a grant identity and authoritative `RewardGrant`; the
  client must not infer success from a local drop or ad callback.
- A future adapter must define how the existing local V1 profile is read or migrated
  without changing its schema or local offline behavior.
- Heroes/Combat adapters may later carry typed identity or combat ports if Rune runtime
  extraction requires them; this contract phase intentionally leaves those references out.
