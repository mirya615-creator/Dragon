# W6 Combat Reach Side Symmetry Audit V1

- Offline mode: real W1-W6 schedule, seeds 1..1000, both sides driven by `BasicUnitAiController`.
- Boss HP: `500` Greybox analysis input only; formal W6 Boss HP remains **PENDING**.
- This pass is not an HP sweep and does not modify production balance.
- Production normal speed remains `0.60 cells/s`; W1-W20 regular spawns remain `EnemyArchetype.Normal`.

## Player

- BossSpawn=76.90 %
- QualifiedBaseline=76.90 %
- PredictedSingleTargetDps is recorded per seed in the existing W6 calibration stream.
- ActualBossDamage0To3MeanAllSeeds=36.04
- ActualBossDamage0To5MeanAllSeeds=59.66
- ActualBossDamage0To3MeanBossSpawned=46.86
- ActualBossDamage0To5MeanBossSpawned=77.58
- BasicDamage0To3MeanBossSpawned=6.76
- HeroDamage0To3MeanBossSpawned=40.10
- BasicDamage0To5MeanBossSpawned=14.84
- HeroDamage0To5MeanBossSpawned=62.74
- BossTTKP50=21.75

## AI

- BossSpawn=76.90 %
- QualifiedBaseline=76.90 %
- PredictedSingleTargetDps is recorded per seed in the existing W6 calibration stream.
- ActualBossDamage0To3MeanAllSeeds=28.95
- ActualBossDamage0To5MeanAllSeeds=44.68
- ActualBossDamage0To3MeanBossSpawned=37.65
- ActualBossDamage0To5MeanBossSpawned=58.10
- BasicDamage0To3MeanBossSpawned=0.42
- HeroDamage0To3MeanBossSpawned=37.23
- BasicDamage0To5MeanBossSpawned=0.66
- HeroDamage0To5MeanBossSpawned=57.45
- BossTTKP50=23.10


## Live scene contract

`Greybox_Main` and `HeroSlice_Main` use a manual Player side and an automatic AI side. The observed empty Player board versus roughly 3-4 AI Heroes is therefore a live-scene initialization contract, not evidence about the offline diagnostics sample.
`CoreLoopRhythmDiagnostics` creates an automatic `BasicUnitAiController` for both Player and AI. The offline Player/AI DPS difference must therefore be evaluated with coordinate, target eligibility, and actual combat telemetry, not with the live Player empty-board observation.

## Mirror fixture

The deterministic mirror fixture is covered by `W6CombatReachSideSymmetryTests`. It uses identical Basic/Hero cards and levels, horizontally mirrored deployment cells, one fixed Soulchain Binder per side, and compares target eligibility, distance, first attack time, damage, and attack-event counts through five simulated seconds.

## Findings

- The pre-fix fixed-side gap was not caused by live Player empty-board state. Offline diagnostics drive both sides with `BasicUnitAiController`.
- Same-input deployment replay now reaches `FirstDivergenceCycle=-1` across 8 cycles after side-local ordering and side-aware recipe formation fixes. The remaining real-schedule composition difference follows the documented `player`/`ai` runtime prefixes and deck/bag streams.
- Post-fix Boss-spawned first-5-second damage is `77.58` Player versus `58.10` AI, with TTK P50 `21.75s` versus `23.10s`; pre-fix values were `77.93` / `16.72` and `21.60s` / `29.55s`.
- The CSV records each unit's side-local combat position, Boss position/path progress, distance, range, hit eligibility, and predicted DPS.

## Decision

- Do not freeze or sweep W6 Boss HP from this telemetry alone. The next W6 HP calibration is allowed only with the side-specific input streams and Player/AI distributions reported separately; `500` remains Greybox and formal Boss HP remains **PENDING**.

Raw per-seed telemetry: `Logs/W6CombatReachSideSymmetry-500.csv`.
