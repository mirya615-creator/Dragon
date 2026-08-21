# W6 Soulchain Binder Formal HP Calibration V1

## Superseded status

This historical calibration report records pre-promotion candidates. Production now uses the
user-approved shared fixed W6 HP `600` [FROZEN]. The BoardQuality dynamic scheme is diagnostic
only and is not active in Production.

- Real Production W1-W6 schedule, Recruit V3, normal-only enemies at 0.60 cells/s, current AI, Item/Rune disabled.
- Same RunSeed set `1..1000` for every candidate; Player and AI are reported independently.
- Soulchain mechanics are unchanged. HP is an analysis input; this batch never writes Production HP.
- Greybox reference: `500` post-fix BossSpawn `76.90%` both sides, TTK P50 `21.75s` Player / `23.10s` AI.
- Candidate bracket rationale: existing 500/650 evidence places the post-fix 28-32s target above 650; bounded candidates are 700/800/900.

## Candidate results

### 700.00 HP / Player

- BossSpawned=769/1000 (76.90 %)
- BossKilled=377, BossToGoal=0, BossAliveAtW7=362, BossNotGenerated=231, EarlyMatchEndBeforeBoss=231, BossSpawnedUnresolved=392
- TTK kill samples=377, P25/P50/P75=18.00/23.60/30.60s, Window28To32=56/377 (14.85 %)
- Spawned-sample Damage0-3/Damage0-5=46.86/77.58, HittableBasic/Hero=0.89/0.55, PredictedDPS=20.33
- Residual W1-W6 total average=7.75, BossAliveAtW7=36.20 %

### 700.00 HP / AI

- BossSpawned=769/1000 (76.90 %)
- BossKilled=413, BossToGoal=0, BossAliveAtW7=375, BossNotGenerated=231, EarlyMatchEndBeforeBoss=231, BossSpawnedUnresolved=356
- TTK kill samples=413, P25/P50/P75=19.70/25.30/32.80s, Window28To32=57/413 (13.80 %)
- Spawned-sample Damage0-3/Damage0-5=37.65/58.10, HittableBasic/Hero=0.15/0.55, PredictedDPS=14.24
- Residual W1-W6 total average=7.58, BossAliveAtW7=37.50 %

### 800.00 HP / Player

- BossSpawned=769/1000 (76.90 %)
- BossKilled=347, BossToGoal=0, BossAliveAtW7=391, BossNotGenerated=231, EarlyMatchEndBeforeBoss=231, BossSpawnedUnresolved=422
- TTK kill samples=347, P25/P50/P75=19.60/24.90/31.10s, Window28To32=48/347 (13.83 %)
- Spawned-sample Damage0-3/Damage0-5=46.86/77.58, HittableBasic/Hero=0.89/0.55, PredictedDPS=20.33
- Residual W1-W6 total average=7.86, BossAliveAtW7=39.10 %

### 800.00 HP / AI

- BossSpawned=769/1000 (76.90 %)
- BossKilled=376, BossToGoal=0, BossAliveAtW7=401, BossNotGenerated=231, EarlyMatchEndBeforeBoss=231, BossSpawnedUnresolved=393
- TTK kill samples=376, P25/P50/P75=20.80/26.45/33.20s, Window28To32=47/376 (12.50 %)
- Spawned-sample Damage0-3/Damage0-5=37.65/58.10, HittableBasic/Hero=0.15/0.55, PredictedDPS=14.24
- Residual W1-W6 total average=7.66, BossAliveAtW7=40.10 %

### 900.00 HP / Player

- BossSpawned=769/1000 (76.90 %)
- BossKilled=319, BossToGoal=0, BossAliveAtW7=418, BossNotGenerated=231, EarlyMatchEndBeforeBoss=231, BossSpawnedUnresolved=450
- TTK kill samples=319, P25/P50/P75=21.00/25.80/32.05s, Window28To32=47/319 (14.73 %)
- Spawned-sample Damage0-3/Damage0-5=46.86/77.58, HittableBasic/Hero=0.89/0.55, PredictedDPS=20.33
- Residual W1-W6 total average=8.00, BossAliveAtW7=41.80 %

### 900.00 HP / AI

- BossSpawned=769/1000 (76.90 %)
- BossKilled=346, BossToGoal=0, BossAliveAtW7=423, BossNotGenerated=231, EarlyMatchEndBeforeBoss=231, BossSpawnedUnresolved=423
- TTK kill samples=346, P25/P50/P75=21.93/27.65/33.98s, Window28To32=56/346 (16.18 %)
- Spawned-sample Damage0-3/Damage0-5=37.65/58.10, HittableBasic/Hero=0.15/0.55, PredictedDPS=14.24
- Residual W1-W6 total average=7.77, BossAliveAtW7=42.30 %


## Decision

No candidate satisfied the historical Player and AI kill-sample P50 28-32s window simultaneously. This report is superseded by the user-approved shared fixed Production W6 HP `600` [FROZEN]; Boss speed, Soulchain mechanics, enemies, Hero/Basic values, AI, and wave schedule were not changed.

## Outcome definitions

`BossNotGenerated` means the shared run ended before the W6 Boss spawn node. `EarlyMatchEndBeforeBoss` is that subset with `MatchEndWave < 6`; the runtime records `-1` when the match ends before W6 is entered. `BossSpawnedUnresolved` means the Boss spawned but was neither killed nor leaked in the recorded W6/W7 resolution window. TTK percentiles and 28-32s hit rate use killed Boss samples only.

Raw metrics: `Logs/W6SoulchainFormalHpCalibration.csv`.
