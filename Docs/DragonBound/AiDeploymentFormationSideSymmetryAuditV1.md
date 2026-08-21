# AI Deployment / Formation Side Symmetry Audit V1

- Same-input replay uses the same RunSeed, runtime prefix, finite component bag, RecruitBatch sequence, resources, unlock state, and decision cycles on both sides.
- Both sides are driven by `BasicUnitAiController`; AI-side positions are converted to the Player-local rotational counterpart before comparison.
- SameInputReplay RunSeed=1701 Cycles=8 FirstDivergenceCycle=-1 Reason=

## Root cause and fix

- The original divergence was caused by world-coordinate enumeration in `BasicUnitAiController`: board cells, occupants, merge candidates, parking targets, and recipe candidates were ordered in global coordinates, so the rotationally mirrored side made different tie-break choices.
- After side-local ordering, `HeroRecipeDefinition` formation rules still ran against AI world coordinates and reversed vertical component orientation. Formation checks and target positions now convert through the side-local rotational transform in `BasicUnitAiController` and `BoardRecruitDestination`.
- Fixed-board conversion for intermediate recipe coordinates is total (8x10 rotation); normal board validation rejects candidates outside the deployment mask.
- No attack, range, HP, speed, count, recruit probability, component probability, or hero rule changed.

## Production input boundary

- Offline `CoreLoopRhythmDiagnostics` is AI-versus-AI, but its production-style streams are intentionally side-specific: runtime prefixes are `player` and `ai`, deck salts differ, and bag seeds differ.
- `RecruitDeck.DeriveSeed` hashes `runtimePrefix` into each finite-batch stream, so swapping salts does not make inputs identical while the prefixes remain different. Residual composition differences therefore follow the input-stream boundary, not a remaining side-local controller branch.
- Live `Greybox_Main`/`HeroSlice_Main` remains manual Player versus automatic/preset AI and is not an offline AI-versus-AI fairness sample.

## Post-fix fixed-500 verification

- One post-fix real W1-W6 run was executed for seeds `1..1000` with Soulchain Binder `500` Greybox HP; this was not an HP sweep.
- BossSpawn was `76.90%` for both Player and AI.
- Boss-spawned first-5-second Boss damage was Player `77.58` and AI `58.10` (pre-fix `77.93` / `16.72`).
- Boss TTK P50 was Player `21.75s` and AI `23.10s` (pre-fix `21.60s` / `29.55s`).
- Full per-seed telemetry: `Logs/W6CombatReachSideSymmetry-500.csv`; formal W6 Boss HP remains **PENDING**.

## Cycle detail

- Cycle 1: Symmetric=True, PlayerRecipeAttempt/Success=0/0, AIRecipeAttempt/Success=0/0, Difference=None
- Cycle 2: Symmetric=True, PlayerRecipeAttempt/Success=1/1, AIRecipeAttempt/Success=1/1, Difference=None
- Cycle 3: Symmetric=True, PlayerRecipeAttempt/Success=1/1, AIRecipeAttempt/Success=1/1, Difference=None
- Cycle 4: Symmetric=True, PlayerRecipeAttempt/Success=1/1, AIRecipeAttempt/Success=1/1, Difference=None
- Cycle 5: Symmetric=True, PlayerRecipeAttempt/Success=1/1, AIRecipeAttempt/Success=1/1, Difference=None
- Cycle 6: Symmetric=True, PlayerRecipeAttempt/Success=1/1, AIRecipeAttempt/Success=1/1, Difference=None
- Cycle 7: Symmetric=True, PlayerRecipeAttempt/Success=1/1, AIRecipeAttempt/Success=1/1, Difference=None
- Cycle 8: Symmetric=True, PlayerRecipeAttempt/Success=1/1, AIRecipeAttempt/Success=1/1, Difference=None

## Live scene boundary

Live `Greybox_Main`/`HeroSlice_Main` remains manual Player versus automatic AI. This replay is intentionally AI-versus-AI and must not be interpreted as a human-player fairness sample.

Raw trace: `Logs/AiDeploymentFormationSymmetryReplay.csv`.
