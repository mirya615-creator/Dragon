# W6 Soulchain Binder V1 Telemetry Verification

Status: implemented and verified as a greybox W6 vertical slice. This document is an
engineering verification record, not a production Boss balance approval.

## Scope and authority

- Design authority: `Drakeforge_完整策划主文档_V9_2026-08-15.md`, including BattleSettlementDefinition V1 and the W1-W6 Bare Lane calibration boundary.
- W6 generates only `BOSS_SOULCHAIN_BINDER`.
- The Boss occupies a separate Boss slot and does not increase the W6 Normal count of 16.
- Boss and W6 Normal #1 are spawned by the same `BeginWave(6)` simulation event.
- Shared fixed Production W6 Boss HP is `600` [FROZEN]. The former `500` value was Greybox-only.
- Boss move speed is `0.20 cells/s`.
- W6 calibration uses the V9 `NormalMoveSpeed = 2.40 cells/s`; Fast/Elite greybox speeds remain `0.80/0.58 cells/s`.
- W7-W20 Bosses are not implemented by this slice.
- The 1000 Seed fixture below is for same-Seed A/B mechanism comparison only. It is not a formal W6 HP calibration and must not be used to promote Boss HP.

## Runtime behavior

`SoulChainController` uses an independent `RunRandom` stream derived from RunSeed and side.
The first cast starts at 8.0s, has 0.5s windup, applies a 2.0s Basic-only attack disable,
and starts the 15.0s cooldown after effect end. The normal successful cycle is 17.5s.
The controller keeps the selected 2x2 anchor fixed through windup, reads current Basic
occupants at resolution, affects at most two with uniform deterministic selection, and does
not append units entering the area later. Empty resolution still consumes the cast and CD.
Merge inherits the maximum remaining control duration. Boss death clears active controls.

`ISoulChainSpellbreakerResolver` is a single windup-time failure seam. A blocked cast deals
10% of Boss MaxHP as reflected damage, grants no kill/resource/XP/Rune reward, and starts the
15s cooldown. `ITEM_SPELLBREAKER_SEAL` remains `PENDING` in the Item catalog; only the Boss
interaction seam is implemented here.

## V9 settlement seam

`BattleSettlementDefinition` exposes the frozen baseline: InitialMaxHeart 3,
InitialCurrentHeart 3, NormalGoalDamage 1, BossGoalEffect InstantDefeat, MaxScheduledWave 20,
and no W21 generation. `TeamState.IsInstantDefeated` is set when a Boss reaches the goal;
the existing match runtime settles the player/AI side immediately. W20 residual continuation
and the final primary/secondary comparison remain outside this W6 slice.

## Explicit test fixture

`TEST_FIXTURE_W6_BASIC4_HERO1_PAIR1_LV1`:

| Field | Value |
|---|---:|
| W6 Normal count | 16 |
| Normal HP | 63 |
| Basic | 4, Lv1 |
| Hero | 1, Lv1 |
| Component/PairLink | 1 |
| Item | 0 |
| Rune-derived source | deterministic fixture damage only |
| Boss HP | 500 explicit fixture override; Production W6 fixed HP 600 |
| W7 capture time | 29.0s |

## 1000 Seed A/B result

Seed set: `1..1000`, same fixture and same damage streams. Full machine-readable output:

- [W6SoulChainTelemetry.json](/F:/unity文件/Dragon/Logs/W6SoulChainTelemetry.json)
- [W6SoulChainTelemetry.csv](/F:/unity文件/Dragon/Logs/W6SoulChainTelemetry.csv)

| Metric | SoulChain off | SoulChain on |
|---|---:|---:|
| Avg Boss TTK | 27.100s | 28.057s |
| Avg Boss TTK delta | - | +0.957s |
| Boss alive at W7 start | 0.0% | 0.0% |
| Avg casts started | 0.000 | 2.000 |
| Avg casts succeeded | 0.000 | 2.000 |
| Avg casts failed | 0.000 | 0.000 |
| Avg second cast started/applied | - | 1.000 / 1.000 |
| Avg total control unit-seconds | 0.000 | 7.044 |
| Avg cast 1 targets / seconds | - | 1.758 / 3.516 |
| Avg cast 2 targets / seconds | - | 1.764 / 3.528 |

Damage source shares, SoulChain off/on:

| Source | Off | On |
|---|---:|---:|
| Basic | 54.078% | 53.009% |
| Hero | 24.321% | 24.884% |
| Component/PairLink | 18.903% | 19.346% |
| RuneDerived | 2.699% | 2.763% |
| Item | 0% | 0% |
| Other/System | 0% | 0% |

The bucket sum equals the recorded total within floating point accumulation tolerance;
Boss and Normal damage are also emitted separately.

## Verification artifacts

| Lane | Result | XML | Log |
|---|---|---|---|
| Unity compile | Passed, exit 0 | - | [W6SoulChain-Compile.log](/F:/unity文件/Dragon/Logs/W6SoulChain-Compile.log) |
| Targeted EditMode | 12/12 passed | [W6SoulChain-Targeted.xml](/F:/unity文件/Dragon/Logs/W6SoulChain-Targeted.xml) | [W6SoulChain-Targeted.log](/F:/unity文件/Dragon/Logs/W6SoulChain-Targeted.log) |
| Fast EditMode | 438/438 passed | [W6SoulChain-FastEditMode.xml](/F:/unity文件/Dragon/Logs/W6SoulChain-FastEditMode.xml) | [W6SoulChain-FastEditMode.log](/F:/unity文件/Dragon/Logs/W6SoulChain-FastEditMode.log) |
| Full PlayMode | 27/27 passed | [W6SoulChain-PlayMode.xml](/F:/unity文件/Dragon/Logs/W6SoulChain-PlayMode.xml) | [W6SoulChain-PlayMode.log](/F:/unity文件/Dragon/Logs/W6SoulChain-PlayMode.log) |
| 1000 Seed batch | Completed | - | [W6SoulChain-TelemetryBatch.log](/F:/unity文件/Dragon/Logs/W6SoulChain-TelemetryBatch.log) |

The approximately 33-minute Full EditMode lane was intentionally not rerun; the requested
verification order used targeted EditMode, Fast EditMode, and full PlayMode.

## Remaining blockers

- Formal W6 Boss HP and any production Boss balance require a Bare Lane full-schedule multi-Seed calibration under V9; the fixture cannot freeze them.
- Spellbreaker Seal remains Item `PENDING`; no formal Item candidate status changed.
- W12/W16/W20 Boss entities and skills remain outside this slice.
- No UI, art, audio, or Item implementation was added.
