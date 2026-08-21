# R1 Production Verified Baseline

Verified on 2026-08-14 for the twenty-wave pressure runtime.

## Production Authority

The sole production HP authority is `TwentyWavePressureConfiguration.CreateCoreLoopV2()`.
`CreateGreyboxV1()`, the default `TwentyWavePressureRuntime` constructor, the bootstrap,
and `EnemyHpCurveCandidate.CurrentProduction` all resolve through that configuration.
The development candidate tables do not select production at runtime.

R1 effective maximum HP by wave:

`25.5, 26.1, 26.7, 35, 45, 63, 95, 120, 145, 175, 205, 240, 275, 315, 360, 410, 465, 525, 590, 660`

R1 is the full LargeScaleModerate table with W5/W6 relieved from `50/70` to `45/63`.
No non-HP wave property changed in this promotion.

## Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Unity compile | Passed | `Logs/R1ProductionPromote_Artifacts/unity-compile.log` |
| Default-runtime R1 tests | 11/11 passed | `Targeted-TwentyWave.xml` |
| Candidate field-freeze test | 1/1 passed | `Targeted-EnemyHpCandidate.xml` |
| Same RunSeed 9191 production/R1 simulation | 1/1 passed | `Targeted-SameSeed-Rerun.xml` |
| Full EditMode | 410/410 passed | `Full-EditMode.xml`, duration `1959.9985706s` |
| Full PlayMode | 26/26 passed | `Full-PlayMode.xml`, duration `16.5738491s` |

The same-seed test compares all wave multipliers and the complete simulation report after
normalizing the diagnostic-only candidate label. Gameplay data is identical.

## Rollback

`Logs/R1ProductionPromote_Artifacts/ROLLBACK.ps1` restores only the four source files
touched by this promotion. It validates their post-promote SHA256 values before it writes,
so it refuses to overwrite later changes.

No Git commit was created because the working tree contains unrelated existing changes.
