# Drakeforge QA Master Test Matrix V2

Status: active QA planning contract for Analytics V2 and release readiness. This document is a test matrix, not a gameplay change request.

Baseline audited in this worktree: `923d12c5ba44b9a02d55d08a6ec86117b01bcd5d`.

## Frozen Rules Covered

- Enemy taxonomy is limited to `normal`, `boss`, and `boss_summon`. Existing code still contains legacy archetype enum members, but current twenty-wave production weights emit only Normal regular enemies.
- Normal enemy movement speed is `0.60` cells/s.
- W6 Boss V1 baseline movement speed is `0.20` cells/s.
- Player and AI hero component acquisition remain independent random streams with Recruit Component Policy V3 and each side's own finite component bag.
- Analytics records actual results only. It must not share, predict, align, or alter both sides' recruit result counts or component identities.
- Live `Greybox_Main`, diagnostic AI-vs-AI, and `HeroSlice_Main` showcase data are separate environments and must not be aggregated together without filtering `execution_context`.
- W6 production Boss HP remains `PENDING`; use the current greybox value only as V1 baseline evidence.

## Existing Analytics V1 Audit

| Area | Existing coverage | Gap for V2 |
| --- | --- | --- |
| `AnalyticsEventNames` | 17 registered names: run, wave, death, Boss, recruit, hero, item, rune, heart events. | Missing context isolation, formation snapshots, rank, AI difficulty, ledger, ads, merchant, energy, settlement gold, emergency save, and explicit Boss damage window. |
| `AnalyticsSchemaV1` | Common envelope validation for event name/version/id/run/seed/build/config/side/wave/sequence/timestamp and some event-specific IDs. | No `execution_context`, `rank_tier`, `ai_difficulty`, V3 recruit counts, finite bag remaining count, Board/Bench counts, hit target counts, authority boundaries or PII hash constraints. |
| sink/recorder | `InMemoryAnalyticsSink` clones accepted events. `AnalyticsRecorder` dedupes `event_id`, rejects out-of-order sequence and run-seed drift. `JsonlTelemetry` writes V1 top-level JSONL. | No context drift guard, no V2 sink type, no ledger-safe reference validation, no environment partition contract. |
| `AnalyticsEventSchemaV1Tests` | 8 tests cover event registry, serialization, missing common fields, typed IDs, heart loss, recorder sequencing/dedupe/seed, sink clone, JSONL. | Does not cover V2 event set, recruit V3, formation snapshots, Boss damage window, ledger safety, context isolation, rank/AI fields or economy events. |

## Execution Environments

| Environment | `execution_context` | Use | Must not contain |
| --- | --- | --- | --- |
| Live `Greybox_Main` | `live_player_vs_ai` | Player manual input versus AI automation, release and balance funnels. | Diagnostic-only auto player actions. |
| Offline diagnostic | `diagnostic_ai_vs_ai` | Deterministic AI controller versus AI controller runs, batch QA, fairness metrics. | Live player economy, ads or player input facts. |
| `HeroSlice_Main` | `hero_slice_showcase` | Showcase and visual proof for hero formation/presentation. | Live funnel or diagnostic survival claims. |

## Master Matrix

| ID | Area | Scenario | Expected result | Analytics V2 evidence | Gate |
| --- | --- | --- | --- | --- | --- |
| QA-001 | Startup timing | Start live run, observe preparation before first regular enemy. | W1 begins after configured preparation; first regular spawn timing matches schedule. | `run_start`, `wave_start`, `enemy_spawn`, `formation_snapshot(startup)` with `execution_context=live_player_vs_ai`. | Targeted plus Fast EditMode when instrumented. |
| QA-002 | Normal movement | Inspect W1 regular enemy movement. | Normal enemies move at `0.60` cells/s. | `enemy_spawn.enemy_type=normal`, `move_speed_cells_per_second=0.60`. | Existing `TwentyWavePressureTests`; future analytics hook test. |
| QA-003 | Boss movement | Spawn W6 Boss. | Boss moves at `0.20` cells/s V1 baseline. | `boss_spawn.move_speed_cells_per_second=0.20`. | Existing W6 tests plus analytics hook test. |
| QA-004 | Enemy taxonomy | Run W1-W20 spawn plan. | Formal production analytics classifies only normal, boss, boss_summon. | `enemy_spawn`, `boss_spawn`, `boss_summon`; no fast/elite/swarm event type. | Targeted schema plus future integration test. |
| QA-005 | W1-W20 progression | Complete scheduled waves with residual enemies allowed. | Wave index remains 1-20; residual enemies can cross wave boundaries without creating fake wave resets. | `wave_finish` residual fields through formation snapshots and `enemy_goal`/`last_hit`. | Fast EditMode; seed gate before RC. |
| QA-006 | Heart baseline | New match starts at Heart=3. | Both sides start with 3 hearts. | `run_start` or first `formation_snapshot` includes side context; `heart_lost` starts from 3. | Existing W6 settlement test. |
| QA-007 | Normal leak | Normal enemy reaches goal. | Heart decreases by 1, no instant defeat. | `enemy_goal.enemy_type=normal`, `heart_lost.count=1`, `reason=normal_goal`. | Future integration test. |
| QA-008 | Boss goal | Boss reaches goal. | Side is instantly defeated; heart becomes 0. | `boss_goal`, `death_wave`, `match_finish`; `heart_after=0`. | Existing W6 settlement test plus future analytics hook. |
| QA-009 | Recruit V3 result | Perform player recruit under V3. | Result has 5 cards, 0-3 components, at least 1 Basic, independent actual counts. | `recruit_result` fields: `recruitment_number`, `component_count`, `basic_count`, `forge_pick_count`, `component_policy`, `remaining_component_bag`. | `AnalyticsEventSchemaV2Tests`; future recruitment adapter test. |
| QA-010 | Recruit independence | Same run observes player and AI recruit actions. | Each side uses independent finite bag and random stream; no shared result count or mirrored component list. | Separate `recruit_result` events keyed by side/run/sequence. | Recruitment targeted plus future adapter test. |
| QA-011 | Component bag | Draw until finite bag changes. | Remaining bag decreases only by actual delivered components; Forge Pick does not consume component. | `remaining_component_bag`, `component_count`, `forge_pick_count`. | Recruitment targeted. |
| QA-012 | Hero formation | Components form a hero. | Stable hero ID is emitted, display names are not. | `hero_formed.hero_id`; nearby `formation_snapshot`. | Hero targeted plus analytics adapter test. |
| QA-013 | Hero XP | Enemy death awards XP. | XP goes to the recorded last-hit owner; level transitions follow configured thresholds. | `last_hit`, `hero_xp`, `hero_level_up`. | `HeroXpLastHitSettlementTests`; future hook test. |
| QA-014 | W6 Boss skill | Soulchain cast starts and resolves. | Cast ordinal and affected count are observed without gameplay mutation. | `boss_skill.count`, `skill_id`, optional affected count in `count`/`damage`. | Existing W6 tests plus future hook. |
| QA-015 | Boss summon rules | Future Boss summon is spawned. | Spawned unit is classified as `boss_summon`, not normal. | `boss_summon.enemy_type=boss_summon`, `summon_id`. | Pending feature; schema ready. |
| QA-016 | Boss damage windows | Sample W6 damage from spawn to 3s and 5s. | Window aggregates are emitted only at configured boundaries, not every frame. | `boss_damage_window.damage_window_id=spawn_to_3s` and `spawn_to_5s`. | Existing W6 calibration plus future hook. |
| QA-017 | Item grant/equip/use | Item foundation grants/equips/uses stable item IDs. | No Merchant or ledger side effect inferred from client-only events. | `item_grant`, `item_equip`, `item_use`, plus `ledger_result` only after server response. | Item targeted when wired. |
| QA-018 | Rune grant/equip | Wave reward or profile operation grants/equips rune. | Stable rune ID only; no localized text. | `rune_grant`, `rune_equip`. | Rune targeted when wired. |
| QA-019 | Energy spend/grant | Run entry spends energy, reserve/ad/share grants energy. | Client records server result only; no local authority. | `energy_spend`, `energy_grant`, paired `ledger_result`. | Server integration pending. |
| QA-020 | Ad points | Request, no-fill, failure, complete, reward. | Ad result is separated from ledger reward confirmation. | `ad_request`, `ad_result`, `ledger_result`. | Android smoke and server test when available. |
| QA-021 | Merchant | Merchant opens every 2 normally completed runs. | Offers are stable IDs; selected offer invalidates alternatives server-side. | `merchant_open`, `merchant_offer`, `merchant_purchase`, `ledger_result`. | Server integration pending. |
| QA-022 | Emergency save | Trigger rescue surface. | Stable result and reason are emitted once per action. | `emergency_save`, optional `ledger_result` for reward. | Pending feature. |
| QA-023 | AI rank tier | Match starts with rank tier and AI difficulty snapshot. | AI difficulty and rank tier are present on every event. | Common `rank_tier`, `ai_difficulty`; `rank_snapshot`. | Schema test now; backend pending. |
| QA-024 | Losing streak demotion | Simulate loss streak. | Rank change is server-authoritative; client records result. | `rank_change`, `ledger_result`/server reference where applicable. | Pending server rules. |
| QA-025 | Formation fairness | Compare player and AI board operation availability. | No event reveals opponent recruit result before that side acts. | Side-specific `formation_snapshot`; no cross-side payload. | Diagnostic fairness suite when wired. |
| QA-026 | Rank leaderboard | Weekly and monthly leaderboard fetch. | Periods are explicit and separated. | `leaderboard_snapshot.leaderboard_period=weekly/monthly`. | Backend pending. |
| QA-027 | Settlement Gold | Finish victory/defeat/timeout/quit. | Gold amount comes from server settlement; client event records result only. | `settlement_gold`, `ledger_result`. | Server integration pending. |
| QA-028 | Live vs diagnostic isolation | Run live and offline batch with same seed. | Events remain separated by `execution_context` and must not share one `run_id`. | Common envelope; recorder rejects context drift per `run_id`. | `AnalyticsEventSchemaV2Tests`. |
| QA-029 | Hero slice isolation | Open HeroSlice showcase. | Showcase events use `hero_slice_showcase` only. | `run_start`, `formation_snapshot`, `hero_formed` with showcase context. | Future scene adapter test. |
| QA-030 | Android launch smoke | Fresh install on Android, start run, background/resume. | No analytics crash; offline queue obeys ledger boundaries. | `run_start`, `ad_result` where available, `ledger_result` statuses. | Release smoke. |
| QA-031 | Network loss/recovery | Disconnect before ledger/ad result, then restore. | No duplicate grant; idempotency hash is stable. | `ledger_result=offline_queued/restored/duplicate`. | Release smoke plus backend sandbox. |
| QA-032 | Ad failure | Ad SDK returns fail/no-fill. | No reward is granted without server verification. | `ad_result.result=failed/no_fill`, no accepted reward `ledger_result`. | Release smoke. |
| QA-033 | Idempotency | Retry same transaction. | Duplicate is recorded as duplicate result, not a second reward. | `ledger_result.ledger_status=duplicate`, same hash reference. | Backend sandbox. |
| QA-034 | Ledger boundary | Inspect emitted JSON. | No token, secret, bearer string, account ID, device ID or ad ID. | Only `transaction_ref_hash` or `idempotency_key_hash`. | Targeted schema plus log review. |

## Sampling Policy

- `formation_snapshot` is sampled at startup, wave start, post recruit result, post hero formation, Boss spawn, match finish, and diagnostic failure bookmarks.
- `boss_damage_window` uses fixed aggregate windows such as `spawn_to_3s`, `spawn_to_5s`, `spawn_to_kill`, not per-hit spam unless a targeted diagnostic explicitly opts in.
- Combat, damage and path telemetry must aggregate by event boundary. No per-frame analytics event is allowed for movement or target scanning.
- Batch diagnostics may write local CSV/JSON artifacts, but Analytics V2 event streams must still use `execution_context=diagnostic_ai_vs_ai`.
