# Item + Rune + Boss Joint Regression V1

Date: 2026-08-18
Baseline HEAD: `78428a1419e991d8fa37433aad23f6feefb1004c` (`feat: implement item economy flow seams`)

## Scope

This pass is verification-only. It does not migrate assemblies, change gameplay values, change
Boss HP, change Item power, alter Rune drops, or read the untracked `Assets/google-services.json`
files. Full EditMode was not run.

## Targeted EditMode

All commands used `Scripts\\TestLanes.ps1 -Lane Targeted -TestFilter ...` with Unity EditMode.

| Fixture filter | Result | XML | Log |
| --- | ---: | --- | --- |
| `DragonBound.Tests.EditMode.Item` | 42/42 | `Logs/JointRegression-Item-Targeted.xml` | `Logs/JointRegression-Item-Targeted.log` |
| `DragonBound.Tests.EditMode.Rune` | 45/45 | `Logs/JointRegression-Rune-Targeted.xml` | `Logs/JointRegression-Rune-Targeted.log` |
| `DragonBound.Tests.EditMode.W6SoulChainV1Tests` | 12/12 | `Logs/JointRegression-W6-Targeted.xml` | `Logs/JointRegression-W6-Targeted.log` |
| `DragonBound.Tests.EditMode.W12StormcallerPriestV1Tests` | 9/9 | `Logs/JointRegression-W12-Targeted.xml` | `Logs/JointRegression-W12-Targeted.log` |
| `DragonBound.Tests.EditMode.W16BloodcrownIntegrationTests` | 4/4 | `Logs/JointRegression-W16-Targeted.xml` | `Logs/JointRegression-W16-Targeted.log` |
| `DragonBound.Tests.EditMode.W20WorldeaterIntegrationTests` | 4/4 | `Logs/JointRegression-W20-Targeted.xml` | `Logs/JointRegression-W20-Targeted.log` |
| `DragonBound.Tests.EditMode.BossesContractsTests` | 6/6 | `Logs/JointRegression-BossContracts-Targeted.xml` | `Logs/JointRegression-BossContracts-Targeted.log` |
| `DragonBound.Tests.EditMode.BloodcrownTyrantRuntimeTests` | 8/8 | `Logs/JointRegression-Bloodcrown-Targeted.xml` | `Logs/JointRegression-Bloodcrown-Targeted.log` |
| `DragonBound.Tests.EditMode.WorldeaterWyrmRuntimeTests` | 7/7 | `Logs/JointRegression-Worldeater-Targeted.xml` | `Logs/JointRegression-Worldeater-Targeted.log` |

Targeted total: **137/137 passed**.

## Fast and PlayMode

- Fast EditMode (Diagnostics/LongRunning excluded): **567/567 passed**, `18.084s` test duration. XML: `Logs/JointRegression-FastEditMode.xml`; log: `Logs/JointRegression-FastEditMode.log`.
- Full PlayMode: **29/29 passed**, `33.646s` test duration. XML: `Logs/JointRegression-PlayMode.xml`; log: `Logs/JointRegression-PlayMode.log`.

## Cross-system checks

- Item snapshots are copied and isolated per side; out-of-run loadout edits do not mutate a started Run.
- Winterveil affects only the owning route, includes that route's Boss, and recovers after its duration; the opposing Boss remains unchanged.
- Rune loadout assign/unequip/craft, Day3 rejection, reward pending/granted/rejected, effect execution, and analytics adapter calls remain green.
- W6 Soulchain, W12 Storm Call/shield/Spellbreaker, W16 Decree, and W20 Devour/minion/goal settlement remain green.
- Boss XP mappings remain Hero-last-hit-only; Basic, Item, invalid attribution, and W20 minions remain excluded.

## Residual gaps

This is not a server-authority test. C-group Item ports for free Recruit, ad-gated Forge Pick,
Ledger, and account settlement still require their owning integration services. The current tests
use typed fixtures and verify that missing authority does not fabricate success. No formal multi-Run
Rune+Item+Boss production balance sample was generated; the next stage is the separately requested
multi-Seed balance/AI/Boss pressure calibration. Production HP and other numeric candidates remain
unchanged.

## Worktree

`git diff --check` passes. The only remaining worktree entries are the untracked user-provided
`Assets/google-services.json` and `Assets/google-services.json.meta`; they were not read, modified,
staged, or committed.
