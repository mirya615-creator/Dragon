# Drakeforge Release Smoke Checklist V1

Status: release smoke checklist. This is intentionally shorter than the master QA matrix and should be run after targeted and Fast EditMode gates pass.

## Preflight

| Check | Pass criteria |
| --- | --- |
| Git baseline | HEAD contains `923d12c5ba44b9a02d55d08a6ec86117b01bcd5d` or later mainline. |
| Worktree | No unrelated changes in forbidden directories before the smoke starts. |
| Build config | Android build uses the release task's Firebase/ad configuration; this QA task does not edit SDK or `google-services.json`. |
| Analytics schema | `AnalyticsEventSchemaV2Tests` passes and event count remains documented. |
| Context routing | Live, diagnostic and HeroSlice smoke runs produce distinct `execution_context` values. |

## Android Smoke

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Fresh install and first launch. | App opens without analytics exception. | `run_start` only after a run begins, no PII in local logs. |
| 2 | Start `Greybox_Main`. | Energy spend waits for server result when connected. | `energy_spend` and `ledger_result` with hashed reference. |
| 3 | Play through W1 first spawn. | First regular enemy timing and speed match configuration. | `wave_start`, `enemy_spawn.normal`. |
| 4 | Reach or jump to W6 in QA build. | Boss spawn uses V1 speed `0.20`; W6 HP marked as current greybox baseline, not final production HP. | `boss_spawn`, `boss_skill`, `boss_damage_window`. |
| 5 | Recruit several times on player side while AI acts. | Player and AI recruit results differ only by actual independent RNG results. | Side-separated `recruit_result`; no shared payload. |
| 6 | Form one hero if possible. | Stable hero ID and formation snapshot are emitted. | `hero_formed`, `formation_snapshot`. |
| 7 | Kill enemies with hero involvement. | Last-hit and XP events have stable IDs. | `last_hit`, `hero_xp`, optional `hero_level_up`. |
| 8 | Complete victory or defeat. | Match emits terminal result once. | `death_wave` before defeat `match_finish`; `settlement_gold` only after server result. |

## Network And Ads

| Step | Action | Expected result | Evidence |
| --- | --- | --- | --- |
| 1 | Disable network before run-start energy spend. | Client does not grant local authority. | `ledger_result.ledger_status=offline_queued` or no accepted spend. |
| 2 | Restore network and retry. | One accepted transaction; retries are duplicate/idempotent. | Same hashed idempotency reference, no double spend/grant. |
| 3 | Trigger rewarded ad with no-fill/failure simulator. | No reward is granted. | `ad_result.result=no_fill/failed`, no accepted reward ledger. |
| 4 | Trigger rewarded ad success. | Reward appears only after server verification. | `ad_result.result=completed`, followed by accepted `ledger_result`. |
| 5 | Inspect analytics JSON. | No token, secret, raw transaction ID, account ID, device ID, ad ID or localized text. | Manual log review plus schema tests. |

## Exit Criteria

- Targeted Analytics V2 EditMode passes.
- Fast EditMode passes when only Analytics/doc changes were made.
- Android smoke completes without crash, duplicated reward, context mixing or PII leakage.
- Any pending feature area is recorded as "schema ready, gameplay/backend wiring pending" rather than treated as shipped.
