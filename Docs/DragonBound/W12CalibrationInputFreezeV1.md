# W12 Calibration Input Freeze V1

Status: diagnostic input freeze for the bounded W12 Stormcaller Priest Build Envelope. This document does not freeze Production Boss HP.

## Scope

The calibration uses the same deterministic RunSeed set `1..50` for both cohorts and all candidates `1000 / 1100 / 1200 / 1300 / 1400`:

- `Direct-W12`: a diagnostic `CALIBRATION_FIXTURE` uses 10x the frozen initial Heart only to prevent unrelated W12 normal leaks from ending the Boss mechanics sample, adds a fixed `120` setup resources to each side, runs 24 existing AI recruitment/deployment decision cycles, then jumps to W12. It is used to measure Storm Call, shield/body damage, Spellbreaker and Boss TTK without treating early W1-W11 settlement as a Boss sample.
- `End-to-end`: the existing Production W1-W12 schedule, recruitment, merge, AI and combat path. Runs that settle before W12 remain in the 50-run denominator and are reported separately from killed-Boss TTK samples.

Both cohorts use the same Item and Rune assumptions. The Direct-W12 setup is not a new Production entry point and does not change the default runtime constructor.

The Direct-W12 Heart allowance, setup resource grant and decision count are calibration fixture inputs only. They are not a change to Match starting Heart, Match starting resources, AI strategy, economy, or Production balance.

## Item Fixture

The diagnostic Item snapshot contains the two currently implemented Item IDs:

| ItemId | Category | Implemented effect | Source |
| --- | --- | --- | --- |
| `ITEM_DRAKEHEART_RELIC` | Passive | `+3` Max Heart and `+3` Current Heart at Run start | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs`, `DrakeheartRelicEffect.HeartBonus` |
| `ITEM_WINTERVEIL_RUNE` | Active | `10%` movement slow for `5s`, `30s` cooldown | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs`, `WinterveilRuneEffect` |

The fixture grants and equips these IDs through the existing `ItemProfile`, `ItemInventory`, `ItemLoadout` and `ItemRunSnapshot` path. Winterveil is attempted once at W12 start by the diagnostic harness. No other Item ID or effect is introduced.

## Rune Assumption

The repository has no authoritative Production W12 Rune Build. Both sides therefore use `RuneLoadoutSnapshot.Empty` for this calibration. This is a **Calibration Assumption**, not a product rule, balance freeze or claim that the Production Rune Build is empty. The result must be revisited after an authoritative Rune Build is supplied.

## HP Boundary

`1200` remains the Stormcaller Priest Greybox input from `StormcallerPriestConfiguration.GreyboxMaxHitPoints`. Production HP remains **PENDING**. Candidate HP is passed only through the existing diagnostic constructor override; the Production default is not modified and no candidate is promoted by this task.

## Reproducibility

Raw rows are written to `Logs/W12StormcallerPriestBuildEnvelope.csv`. Each row includes `candidateHp`, `cohort`, `runSeed` and `side`, plus the observed Boss HP and lifecycle/combat/cast telemetry. Direct fixture board counts are recorded at the jump boundary so an empty-board diagnostic cannot be mistaken for a valid sample.
