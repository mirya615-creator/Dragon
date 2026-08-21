# W16 Bloodcrown Tyrant Integration

Status: implementation complete in the main pressure composition root; `2400` remains a
Greybox input and is not a Production-frozen HP value.

## Runtime path

`TwentyWavePressureRuntime.BeginWave(16)` queues the configured W16 regular wave through the
existing `PressureRaceSideRuntime` and then creates one independent `BloodcrownTyrantRuntime`
per side via `BloodcrownIntegrationAdapter`. The Boss is registered through `SpawnBoss`, so it
is not part of the regular wave spawn plan and can remain in the registry across W17.

The adapter supplies the typed Boss target, Spellbreaker and Basic policy ports. During Decree,
Basic combat uses the configured level-1 attack and attack speed while retaining the stored-level
range. `BoardRecruitDestination` exposes the same policy to DragDrop and automatic merge paths;
Recruitment and future Item callers share the destination's occupied-drop gate, so no alternate
merge implementation is introduced. Stored card levels are never mutated.

Boss XP continues through `PressureRaceSideRuntime.ResolveKill` and the existing formal Hero
last-hit settlement. W6 (HP 600) and W12 behavior are not changed.

## Verification

The new integration fixture is
`Assets/DragonBound/Tests/EditMode/W16BloodcrownIntegrationTests.cs`; isolated runtime coverage
remains in `BloodcrownTyrantRuntimeTests.cs` and `BossesContractsTests.cs`.

Verification completed with the repository `TestLanes.ps1` entry point:

- W16 integration Targeted EditMode: `4/4` passed.
- Fast EditMode: `521/521` passed.
- PlayMode: `29/29` passed.
- `git diff --check`: passed after metadata cleanup.

The Unity test XML and logs are kept under `Logs/W16Integration-*.xml` and
`Logs/W16Integration-*.log`; they are generated verification artifacts and are not part of the
production commit.

## Not in scope

W20 Worldeater, W16 Production HP calibration, new Item/Rune effects, AI strategy, UI, scenes,
prefabs, server contracts and ProjectSettings remain untouched.
