# Drakeforge Analytics Integration Gaps V1

Status: Analytics/QA acceptance-layer audit on W12 calibration baseline `fc78175`. This document records seams that are not publicly exposed; it does not authorize gameplay changes.

## Connected in the Analytics adapter

- `TwentyWavePressureRuntime.PlayerEnemyLifecycleEmitted` and `AiEnemyLifecycleEmitted` map W12 Boss spawn, kill, and goal to `boss_spawn`, `boss_kill`, and `boss_goal`.
- `TwentyWavePressureRuntime.StormcallerCastEmitted` maps typed cast start, effect resolve, and spellbreaker block to existing `boss_skill` events with stable `result` values `started`, `resolved`, and `blocked`.
- `TwentyWavePressureRuntime.CombatEmitted` is accumulated by side and emitted only at explicit `boss_damage_window` boundaries. The window `result` values `shield_damage` and `health_damage` carry aggregates; no per-frame or per-hit event is emitted.
- `TwentyWavePressureRuntime.PlayerRuneRewardGranted` maps to existing `rune_grant` with `result=rune_reward` and a complete/fragment reason.
- Item run snapshots, item commands/results, cooldown observations, rune loadout snapshots, and rejected rune gates have explicit Analytics-side recording methods that map to the existing `item_equip`, `item_use`, `rune_equip`, and `rune_grant` events with stable result/reason values. They remain caller-driven because the gameplay classes do not expose lifecycle events for those operations.
- Diagnostic calibration samples map to an aggregate `boss_damage_window` record (`result=cohort`, `max_hit_points=candidate_hp`, `reason=early_end_reason`) and are rejected unless `execution_context=diagnostic_ai_vs_ai`.

## Missing public seams

| Area | Missing seam | Owner action required |
| --- | --- | --- |
| Run lifecycle | No typed `run_start`, `wave_start`, `wave_finish`, or `match_finish` event on `TwentyWavePressureRuntime`; only log text and state methods are public. | Gameplay/bootstrap owner should expose typed lifecycle callbacks; Analytics must not parse logs. |
| Item snapshot | `ItemRunRuntime.StartRun` has no event and `TwentyWavePressureRuntime` does not expose snapshot-lock success/rejection. | Item/runtime owner should publish a result event with side and stable snapshot reason. |
| Item command | `TryUseItem` returns a bool/reason but has no command/result event and no cooldown-changed event. | Item/runtime owner should publish command accepted/rejected and cooldown observation callbacks. |
| Rune loadout | `RuneLoadoutService.LockForRunStart` and profile operations return bool/reason but do not publish a result event. | Rune/profile owner should publish lock success, assignment mutation, and gate rejection callbacks. |
| Rune gate | `RuneRunRewardService.CompleteWave` silently returns null while the Day 3 gate is closed. | Rune owner should expose a typed gate rejection reason. |
| Server economy | No client/server adapter exists for Energy, Gold, ads, Merchant, rank, or ledger responses. | Service owner must provide status plus non-sensitive `transaction_ref_hash` or `idempotency_key_hash`; never tokens, secrets, raw transaction IDs, account/device IDs, or ad IDs. |
| Boss calibration | Calibration reports are local aggregate objects and do not publish a stable cohort/sample callback. | Diagnostics owner should call the adapter at sample close with cohort, candidate HP, and early-end reason. |

## Context isolation

`live_player_vs_ai`, `diagnostic_ai_vs_ai`, and `hero_slice_showcase` are separate `execution_context` values. The recorder rejects context changes within one `run_id`; diagnostic calibration is only accepted in the diagnostic context. No Analytics adapter promotes diagnostic or HeroSlice evidence into live funnel metrics.
