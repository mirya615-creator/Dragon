# QA Regression Matrix V1

Status: active QA execution contract. Commands use `Scripts/TestLanes.ps1`; the lane implementation and current baseline remain in `Docs/TestLanes.md`.

## Everyday commands

| Gate | Command | Expected use |
| --- | --- | --- |
| Targeted | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane Targeted -TestFilter "DragonBound.Tests.EditMode.AnalyticsEventSchemaV1Tests"` | Before and after a telemetry/schema change. |
| Fast EditMode | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane FastEditMode` | Every ordinary code, config, schema or test change. Excludes `Diagnostics` and `LongRunning`. |
| PlayMode | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane PlayMode` | Required where runtime/bootstrap/UI wiring changes. |
| Full EditMode / Seed Gate | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane FullEditMode` | Do not run as an everyday gate; includes diagnostic and seed suites and is about 33 minutes. |

## Change-to-gate matrix

| Work area | Targeted fixture | Fast EditMode | PlayMode | Full/Seed Gate trigger |
| --- | --- | --- | --- | --- |
| Analytics schema, serializer, sink, recorder | `AnalyticsEventSchemaV1Tests` and `JsonlTelemetryTests` | required | not required unless a MonoBehaviour/bootstrap adapter changes | Event order semantics, RNG context behavior, or diagnostic output format changes. |
| 20-wave schedule / pressure runtime | `TwentyWavePressureTests` plus affected telemetry test | required | required | Spawn composition, wave timing, pressure balance, sample count, or `RunSeed` derivation changes. |
| W6 Boss / SoulChain | `W6SoulChainV1Tests` plus affected telemetry test | required | required when presentation/runtime wiring changes | Boss timing, targeting, damage composition, cast sequence, or W6 multi-seed evidence changes. |
| Recruit / finite bag / Forge Pick | affected recruitment fixture plus telemetry test | required | required when UI/bootstrap changes | RNG, pity/catch-up, sample count, or distribution behavior changes. |
| Hero formation / XP | `HeroSliceTests`, `HeroXpLastHitSettlementTests`, affected telemetry test | required | required when runtime/UI wiring changes | Hero formation distribution, XP progression or diagnostic counters change. |
| Rune profile, rewards, loadout | Rune fixture plus affected telemetry test | required | required | Reward/drop distribution, persistence schema, or stochastic diagnostics change. |
| Item Foundation / later Item runtime | `ItemSystemV1FoundationTests` plus affected telemetry test | required | required when runtime/UI wiring changes | Item randomization, balance, or batch diagnostics change. |
| AI / Boss fairness work | affected AI/Boss fixture plus telemetry test | required | required | AI random streams, multi-seed survival, Boss fairness/matrix evidence or sample count changes. |

## Required assertions for a newly wired event

1. Exact `event_name`, version and required stable IDs are emitted.
2. One run maintains a stable `run_id`, `run_seed`, config/build snapshot and contiguous sequence.
3. Terminal and dependent ordering is asserted: `run_start` first; `death_wave` before defeat `run_finish`; `boss_killed` before `boss_ttk`.
4. Replaying the same `event_id` is ignored by the recorder; a new ID with a stale sequence is rejected.
5. No display/localized text or account/device identifier appears in emitted JSON.

## Hook gap register (phase 1: do not modify gameplay code)

| Event(s) | Existing seam | Recommended future adapter insertion | State |
| --- | --- | --- | --- |
| `run_start`, `run_finish`, `wave_reached`, `death_wave` | `TwentyWavePressureRuntime.StartRun`, `BeginWave`, `SettleRun` | Bootstrap-owned observer that receives a public run lifecycle event. Avoid parsing `Debug.Log`. | Gap: lifecycle event is not exposed. |
| `boss_spawned`, `boss_killed`, `boss_ttk` | W6 spawn occurs in `TwentyWavePressureRuntime.BeginWave`; enemy lifecycle stream is already forwarded | Subscribe at a future telemetry adapter to typed boss/lifecycle events; preserve spawn time per boss runtime ID. | Partial seam; W6 only and no typed public boss lifecycle. |
| `boss_skill_cast` | `SoulChainController.CastEvent` | Subscribe when the run constructs the W6 runtime; emit only stable `SoulChainCastEventKind` codes. | Existing typed seam. |
| `boss_summon_spawned` | No V1 BossSummon runtime yet | Add a typed spawn event when BossSummon is implemented. | Pending feature. |
| `recruit`, `hero_formed` | `BoardRecruitDestination` recruitment and `HeroPairLinked` events | Bootstrap-owned adapter should translate stable unit/hero IDs. | Existing typed seams, not centrally wired. |
| `hero_level_up` | `PressureRaceSideRuntime` has level-up resolution/logging | Expose a typed level-up event; do not scrape the log string. | Gap: only log seam is currently visible. |
| `item_equipped`, `item_used` | Item Foundation/runtime boundary | Add after Item loadout and runtime activation are productized. | Pending feature. |
| `rune_equipped` | `RuneProfileOperations` / loadout UI | Emit from profile-operation success result, not button click. | Adapter not wired. |
| `rune_drop` | `TwentyWavePressureRuntime.PlayerRuneRewardGranted` | Subscribe in the future run telemetry adapter. | Existing typed seam. |
| `heart_lost` | `PressureRaceSideRuntime.ResolveLeak` and `CombatEmitted` | Translate typed leak combat/lifecycle event at the adapter boundary. | Partial seam; direct heart delta is not exposed. |

## Full-gate policy

The prior Full EditMode baseline is deliberately retained for schema-only work. Run it before a release candidate and whenever a task changes diagnostics, Monte Carlo sample sizes, run-seed derivation, pressure/RNG balance, Boss/AI fairness evidence, reward distributions or any category marked `Diagnostics`/`LongRunning`. Do not replace it with a shorter test or silently reduce sampling.
