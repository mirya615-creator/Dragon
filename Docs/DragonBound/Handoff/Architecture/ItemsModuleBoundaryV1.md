# Items Module Boundary V1

## Scope

Phase C-Item-1 established the Unity-free contract assembly. Phase C-Item-2 now moves the pure `ItemDefinitions` and `ItemProfile` data/profile layer into `DragonBound.Items.Runtime`; the live combat/economy effects, Scene/Prefab wiring and ProjectSettings remain unchanged.

The existing `DragonBound.Items` namespace remains the production compatibility surface. `DragonBound.Items.Runtime` keeps that namespace while owning only the pure data/profile layer; the remaining effect runtime continues in `DragonBound.Runtime` until its ports are extracted.

## Assembly and dependency direction

`DragonBound.Items.Contracts` has `noEngineReferences: true` and references only `DragonBound.Foundation.Contracts`, for `RunId`. `DragonBound.Items.Runtime` has `noEngineReferences: true` and currently has no assembly dependencies because the moved data/profile layer uses only .NET types. Neither assembly references `DragonBound.Runtime`, Core, Runes, or any service/server assembly.

The intended direction is:

`Foundation.Contracts <- Items.Contracts <- Items.Runtime <- future effect adapters`

The current effect runtime remains unchanged and is not moved in this Item phase. The EditMode and PlayMode test assemblies reference the new data/runtime assembly explicitly because they contain integration tests and the live loadout path.

## Contract inventory

| Contract | Boundary role |
| --- | --- |
| `ItemId`, `ItemIds`, `ItemCategory` | Stable ASCII identity and Active/Passive classification for all 20 IDs. |
| `ItemCatalogEntry`, `ItemCatalog` | Typed catalog state for all 20 stable Item IDs. The contract catalog currently marks all IDs `Configured`; authority-sensitive economy wiring and final production validation remain separate concerns. |
| `ItemSnapshot` | Immutable match-start active/passive selection with `Ready`, `Pending` and `NotConfigured` state. |
| `ItemCommand`, `ItemCommandKind` | Client commands for activation, merchant selection and ad reward claim. |
| `ItemResult`, `ItemResultState`, `Cooldown` | Typed command outcome and cooldown view; no effect implementation or authoritative settlement. |
| `MerchantOffer`, `MerchantSelection` | Offer/selection DTOs with no Gold/Energy price or server behavior. |
| `AdRewardClaim` | Client claim metadata only. It is not ad verification or reward authority. |
| `LedgerReference` | Opaque reference returned by a replaceable authority adapter; it has no ledger logic. |
| `AccountProgress`, `IItemAccountProgressProvider` | Supplied normal completion count; Item code does not increment or derive it. |
| `DayKey`, `IItemDayKeyProvider` | Supplied opaque DayKey; Item code does not calculate it from device time. |
| `IItemSnapshotProvider`, `IItemCommandPort`, `IMerchantOfferProvider`, `IAdRewardClaimPort` | Client-replaceable ports for later composition. |

## Explicit non-goals

This commit contains no server code, API endpoint, database or persistence service, authentication, authoritative Ledger/Profile/reward/idempotency/account-state logic, ad validation, server DayKey calculation, deployment configuration, Merchant backend, Lottery, Gold/Energy implementation, or remaining Item effects.

`ITEM_WINTERVEIL_RUNE` is represented as a stable Item ID and category only in this assembly. There is no Rune module reference and no Item-to-Rune code dependency.

## Integration seams still required

1. The next Item slice must move the effect runtime behind typed ports without changing the current Item effects or 20-item status. `ItemRunSnapshot`/`ItemRuntime` remain in the monolith for that slice.
2. A server-owned adapter must supply authoritative account progress, DayKey, profile/reward settlement, idempotency and any LedgerReference. The client ports must remain replaceable and must not infer authority locally.
3. A Merchant/Ads integration owner must define transport and verification outside this assembly, then map verified responses into these DTOs.
4. Integration/Bootstrap owns composition, Scene/Prefab wiring and any migration of the legacy test assembly. This phase does not touch those assets.

## Verification

The Phase C-Item-2 targeted lane covers `ItemProfileSnapshotProviderTests`, `ItemSystemV1FoundationTests` and `DevelopmentItemRunSnapshotProviderTests` (`17/17` passed). Fast EditMode (`572/572`) and PlayMode (`29/29`) also pass. Full EditMode and long seed/diagnostic lanes remain excluded from this focused migration.
