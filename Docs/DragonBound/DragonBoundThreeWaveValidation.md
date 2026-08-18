# DragonBound 3-Wave Validation

Date: 2026-08-04

Project: `F:\unity文件\Dragon`

The source project `F:\unity文件\My project` was not modified.

## Scope

- Hero components remain disabled (`EnableHeroComponents = false`).
- Boss and 15-wave expansion remain disabled/out of scope.
- Wave durations are fixed at 24, 26, and 28 seconds.
- Match initialization is `Initializing -> Ready -> Running`.
- Enemy paths are open and terminate at `DragonGoal` without waypoint modulo.
- Enemy leak resolution is guarded by `HasResolved` and removes the matching Runtime/View.
- Enemy counters and debug rows are backed by live `EnemyRuntime` registries.
- Range, enemy HP, combat feedback, death flash, and supplies gain feedback use editable UI templates.

## Automated Results

- EditMode: 48/48 passed
  - XML: `Logs/codex-wave-editmode-final4.xml`
  - Log: `Logs/codex-wave-editmode-final4.log`
- PlayMode: 5/5 passed
  - XML: `Logs/codex-wave-playmode-final2.xml`
  - Log: `Logs/codex-wave-playmode-final2.log`

The EditMode log proves:

- `WaveFinished Wave=1 ElapsedSeconds=24`
- `WaveFinished Wave=2 ElapsedSeconds=26`
- `WaveFinished Wave=3 ElapsedSeconds=28`
- victory with deployed units and defeat with leaks
- residual enemies inherited across wave boundaries
- final leaks reach `RegistryCount=0`
- all four attack kinds execute: Single, BowProjectile, SpearPierce, RiderSweep
- kills increase resources and log `ResourcesAfter`

The PlayMode log proves:

- `Time.timeScale=1`
- Ready occurs before Running
- no wave/enemy tick before Running
- recruit batches contain exactly five basic cards
- full/non-empty bench recruitment refreshes all five camp cards
- AXE range diameter uses `actualCellSizePixels * 1.5 * 2`
- range hides when drag begins and remains hidden after cancel
- both runtime enemy Views follow their authored open routes
- no Runtime/View mismatch error was emitted

The 100-recruit disabled-hero test passed:

`RecruitDeckTests.OneHundredDisabledRecruitmentsNeverExposeHeroComponents`

## Visual Evidence

- `Logs/DragonBoundPortraitPreview_720x1280.png`
- `Logs/DragonBoundPortraitPreview.png` (1080x1920)
- `Logs/DragonBoundPortraitPreview_1080x2280.png`
- Capture log: `Logs/codex-wave-capture-final.log`

The final capture is nonblank. Static prefab state shows `INITIALIZING...`, `ENEMIES 0`, hidden enemy templates, and a hidden Boss bar. Runtime objects are created only from live registries.

## Environment Note

This Codex session exposed zero Unity MCP resources/templates. Validation therefore used Unity 2022.3.62f3c1 batchmode, Unity Test Runner XML, runtime logs, and GPU render capture. The user waived recording delivery.
