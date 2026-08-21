# Drakeforge Analytics Integration Map V1

Status: integration map for future gameplay adapters. This task deliberately does not wire gameplay systems.

## Current Code Audit

| Code area | Current seam | What is already test-covered | V2 integration gap |
| --- | --- | --- | --- |
| `Assets/GameShared/Runtime/Telemetry/AnalyticsEventSchemaV1.cs` | V1 schema, event names, sink and recorder live in GameShared. | V1 schema validation, clone sink, duplicate/order/seed recorder behavior. | V2 needs DragonBound-specific context, rank/AI, recruit V3, formation, Boss windows, economy and ledger safety. |
| `Assets/GameShared/Runtime/Telemetry/JsonlTelemetry.cs` | Legacy JSONL writer and V1 top-level writer. | `JsonlTelemetryTests`, V1 JSON output test. | V2 file sink is not yet required; add only when local V2 artifacts are needed. |
| `Assets/DragonBound/Tests/EditMode/AnalyticsEventSchemaV1Tests.cs` | 8 V1 tests. | Registry, serialization, required fields, typed IDs, heart loss, recorder semantics. | Superseded for V2 coverage, but retained as compatibility evidence. |
| `Assets/DragonBound/Runtime/Analytics/AnalyticsEventSchemaV2.cs` | New V2 schema/recorder contract. | `AnalyticsEventSchemaV2Tests`. | Gameplay adapters still pending. |

## Adapter Ownership Map

| Event(s) | Future insertion point | Notes | Status |
| --- | --- | --- | --- |
| `run_start`, `match_finish` | Bootstrap/run session adapter around `TwentyWavePressureRuntime.StartRun` and terminal settlement. | Allocate `run_id`, seed, config/build, context and sequence once. Do not put energy authority here. | Pending adapter. |
| `wave_start`, `wave_finish` | Public run lifecycle observer around `BeginWave` and `EndCurrentWave`. | Use typed observer rather than parsing `Debug.Log`. | Pending adapter. |
| `enemy_spawn`, `enemy_goal`, `heart_lost`, `death_wave` | `PressureRaceSideRuntime` enemy lifecycle and leak resolution. | Normal leaks produce `heart_lost.count=1`; Boss goals use `boss_goal`. | Partial lifecycle seam exists. |
| `boss_spawn`, `boss_kill`, `boss_goal` | Boss lifecycle observer around W6 Boss runtime and future W12/W16/W20 bosses. | W6 Boss HP is current greybox baseline only; formal production HP pending. | Partial W6 seam exists. |
| `boss_skill` | `SoulchainBinderRuntime.SoulChain.CastEvent`. | Emit stable skill ID and cast ordinal. | Typed seam exists. |
| `boss_summon` | Future Boss summon runtime spawn event. | Classify as `boss_summon`, not regular enemy. | Pending feature. |
| `boss_damage_window` | Boss telemetry aggregate adapter. | Emit `spawn_to_3s`, `spawn_to_5s`, `spawn_to_kill`; never every frame. | Diagnostic data exists; adapter pending. |
| `recruit_result` | `RecruitDeck.LastFiniteBatchTelemetry` after committed `TryRecruit`. | Record actual V3 counts and remaining finite bag. Do not share player/AI results. | Existing telemetry seam, adapter pending. |
| `formation_snapshot` | Board/recruit destination observer at approved bookmarks. | Aggregate Basic/Hero/Component counts, Board/Bench occupancy and hittable count only. | Pending adapter. |
| `hero_formed` | `BoardRecruitDestination` post-drop recipe resolution / `HeroPairLinked`. | Stable hero IDs only. | Existing seam, adapter pending. |
| `last_hit`, `hero_xp`, `hero_level_up` | Combat settlement and `HeroXpSettlement`. | Use typed last damage owner, not logs. | Existing tests cover settlement; adapter pending. |
| `item_grant`, `item_equip`, `item_use` | Item profile operation success and `ItemRunRuntime.TryUse`. | Durable grants/equips that affect inventory must be paired with server result when applicable. | Foundation exists, durable economy pending. |
| `rune_grant`, `rune_equip` | `RuneRunRewardService` and rune profile operations. | Use stable rune IDs. | Partial seam exists. |
| `rune_loadout_assign`, `rune_loadout_unequip`, `rune_craft` | Typed results from `RuneLoadoutService.TryEquip`, `TryUnequip` and `TryCraft`, forwarded through the optional `RuneLoadoutAnalyticsBridge`. | Pass the returned success flag and reason into `RuneAnalyticsAdapterV2`; do not call the service from Analytics. | Wired at the service boundary; absent adapter leaves the legacy path unchanged. |
| `rune_gate_rejection` | Typed `out reason` from the Day 3 gate on loadout/craft/reward operations. | Emit `gate_state=locked`, `account_day` and stable reason `RuneSystemLockedUntilDay3`; never infer account progression. | Loadout service and reward service emit the gate event through the optional adapter; adapter remains absent-safe. |
| `rune_reward_pending`, `rune_reward_granted`, `rune_reward_rejected` | `RuneRunRewardService.CompleteWaveResult` typed result boundary. | Pending is a pre-result observation; granted/rejected reflects the typed result. Analytics never calls `GrantToInventory` or mutates inventory. | Wired through optional reward adapter; legacy `CompleteWave` return remains compatible. |
| `energy_spend`, `energy_grant`, `settlement_gold` | Server ledger response adapter. | Client analytics records amount and server status only after response. | Backend pending. |
| `ad_request`, `ad_result` | Ad service wrapper. | Ad completion does not imply reward. Server verification must produce ledger result. | Backend/ad wrapper pending. |
| `merchant_open`, `merchant_offer`, `merchant_purchase` | Merchant service/UI adapter. | Server owns eligibility, offer pool and purchase result. | Pending feature. |
| `ledger_result` | Single server ledger/idempotency response adapter. | Only `transaction_ref_hash` or `idempotency_key_hash`; no token/key/raw transaction ID. | Backend pending. |
| `rank_snapshot`, `rank_change`, `leaderboard_snapshot` | Rank/leaderboard service adapter. | Server authoritative; no display names/account IDs. | Backend pending. |
| `emergency_save` | Future rescue feature resolution. | Record action result; any reward separately needs ledger. | Pending feature. |

## Data Isolation Rules

- `live_player_vs_ai`, `diagnostic_ai_vs_ai`, and `hero_slice_showcase` must use separate `run_id` namespaces.
- The V2 recorder rejects a changed `execution_context` inside one `run_id`.
- Offline diagnostics may use fixed seeds and AI-vs-AI controllers, but their data cannot be promoted to live player funnel metrics.
- HeroSlice showcase may validate formation and presentation instrumentation only; it is not survival or economy evidence.

## High-Frequency Controls

| Candidate signal | Allowed strategy | Forbidden strategy |
| --- | --- | --- |
| Formation state | Snapshot at startup, wave start, post recruit, post hero formation, Boss spawn, match finish and diagnostic bookmarks. | Per-frame board dumps. |
| Boss damage | Aggregate windows and final TTK. | Per-hit or per-frame default analytics. |
| Enemy movement | Spawn metadata, goal/kill, aggregate path diagnostics in local files. | Per-frame position analytics. |
| Combat | Last-hit and XP settlement events. | Full attack stream in live analytics. |

## Implementation Sequence

1. Keep V1 compatibility tests green while V2 schema is introduced.
2. Add one adapter at a time, starting with run lifecycle and recruit result.
3. Add a focused EditMode test for each adapter before touching another gameplay system.
4. Run Targeted Analytics after schema/adapter changes.
5. Run Fast EditMode for Analytics-only and docs changes.
6. Reserve PlayMode/Full EditMode for scene/bootstrap/runtime behavior changes or release candidates.
