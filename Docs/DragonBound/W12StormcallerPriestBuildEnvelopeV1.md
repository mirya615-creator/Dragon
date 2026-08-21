# W12 Stormcaller Priest Build Envelope V1

- Same RunSeed set `1..50` for every candidate and cohort.
- Calibration fixture: both sides receive the two currently Implemented Item candidates: Passive `ITEM_DRAKEHEART_RELIC` and Active `ITEM_WINTERVEIL_RUNE`; Winterveil is attempted once when W12 starts. This fixture is diagnostic-only and is not a server-authoritative Build definition.
- Rune snapshot is empty because the repository has no authoritative standard W12 Rune loadout; existing Rune rules remain available but are not silently invented for this calibration.
- Candidates are bounded around the current Greybox `1200`: `1000 / 1100 / 1200 / 1300 / 1400`. No Production HP is written.
- W12 target window from the Boss System document: killed-Boss TTK `32-36s`; percentiles use actual killed samples only.

## Direct-W12 cohort (CALIBRATION_FIXTURE)

A fixed 10x-Heart diagnostic allowance, 120-resource/24-decision recruitment setup and one Dragon Rider development pair is built, then the runtime jumps to W12. This cohort is the Boss-mechanics sample and is not a Production flow.
| HP | Cohort | Side | Sample n | Spawn | Kill | Goal | W13 Residual | TTK n | P25 | P50 | P75 | Window32-36 | First/Second Cast Success | Avg Affected 1/2 | Shield/Body Damage | Spellbreaker Failures |
| ---: | :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | :--- | :--- | :--- | ---: |
| 1000 | Direct-W12 | Player | 50 | 100.00 % | 16.00 % | 0.00 % | 88.00 % | 8 | 32.60 | 38.40 | 38.40 | 12.50 % | 100.00 %/100.00 % | 5.36/6.20 | 810.61/4203.37 | 0 |
| 1000 | Direct-W12 | AI | 50 | 100.00 % | 24.00 % | 0.00 % | 76.00 % | 12 | 31.90 | 37.70 | 38.40 | 8.33 % | 100.00 %/96.00 % | 5.50/5.70 | 753.00/4620.54 | 0 |
| 1100 | Direct-W12 | Player | 50 | 100.00 % | 12.00 % | 0.00 % | 88.00 % | 6 | 31.30 | 40.40 | 41.00 | 16.67 % | 100.00 %/100.00 % | 5.36/6.20 | 820.36/4198.08 | 0 |
| 1100 | Direct-W12 | AI | 50 | 100.00 % | 24.00 % | 0.00 % | 78.00 % | 12 | 33.40 | 39.80 | 41.00 | 16.67 % | 100.00 %/96.00 % | 5.50/5.70 | 752.71/4633.34 | 0 |
| 1200 | Direct-W12 | Player | 50 | 100.00 % | 12.00 % | 0.00 % | 88.00 % | 6 | 32.10 | 43.20 | 43.90 | 33.33 % | 100.00 %/100.00 % | 5.36/6.20 | 820.36/4202.65 | 0 |
| 1200 | Direct-W12 | AI | 50 | 100.00 % | 24.00 % | 0.00 % | 80.00 % | 12 | 34.20 | 42.50 | 43.90 | 25.00 % | 100.00 %/96.00 % | 5.50/5.70 | 757.42/4641.21 | 0 |
| 1300 | Direct-W12 | Player | 50 | 100.00 % | 8.00 % | 0.00 % | 94.00 % | 4 | 33.30 | 34.80 | 34.80 | 75.00 % | 100.00 %/100.00 % | 5.36/6.20 | 828.27/4221.81 | 0 |
| 1300 | Direct-W12 | AI | 50 | 100.00 % | 18.00 % | 0.00 % | 86.00 % | 9 | 33.30 | 34.80 | 45.30 | 33.33 % | 100.00 %/100.00 % | 5.50/5.94 | 782.78/4667.73 | 0 |
| 1400 | Direct-W12 | Player | 50 | 100.00 % | 6.00 % | 0.00 % | 94.00 % | 3 | 34.00 | 34.00 | 35.70 | 100.00 % | 100.00 %/100.00 % | 5.36/6.20 | 831.86/4230.10 | 0 |
| 1400 | Direct-W12 | AI | 50 | 100.00 % | 14.00 % | 0.00 % | 88.00 % | 7 | 34.00 | 35.30 | 43.40 | 28.57 % | 100.00 %/100.00 % | 5.50/5.94 | 788.66/4683.59 | 0 |

## End-to-end cohort

Real W1-W12 schedule. Runs ending before W12 remain in the 50-run denominator and are excluded from TTK percentiles.
| HP | Cohort | Side | Sample n | Spawn | Kill | Goal | W13 Residual | TTK n | P25 | P50 | P75 | Window32-36 | First/Second Cast Success | Avg Affected 1/2 | Shield/Body Damage | Spellbreaker Failures |
| ---: | :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | :--- | :--- | :--- | ---: |
| 1000 | End-to-end | Player | 50 | 20.00 % | 14.00 % | 0.00 % | 0.00 % | 7 | 15.30 | 26.60 | 35.50 | 14.29 % | 100.00 %/30.00 % | 0.90/0.24 | 62.69/1082.86 | 0 |
| 1000 | End-to-end | AI | 50 | 20.00 % | 14.00 % | 0.00 % | 0.00 % | 7 | 21.60 | 27.80 | 36.60 | 14.29 % | 100.00 %/30.00 % | 0.98/0.26 | 71.36/1050.02 | 0 |
| 1100 | End-to-end | Player | 50 | 20.00 % | 14.00 % | 0.00 % | 0.00 % | 7 | 16.90 | 28.10 | 37.00 | 0.00 % | 100.00 %/30.00 % | 0.90/0.24 | 62.69/1093.61 | 0 |
| 1100 | End-to-end | AI | 50 | 20.00 % | 14.00 % | 0.00 % | 0.00 % | 7 | 22.40 | 28.90 | 37.60 | 14.29 % | 100.00 %/40.00 % | 0.98/0.32 | 74.96/1055.42 | 0 |
| 1200 | End-to-end | Player | 50 | 20.00 % | 12.00 % | 0.00 % | 2.00 % | 6 | 17.10 | 29.10 | 39.20 | 0.00 % | 100.00 %/50.00 % | 0.90/0.42 | 73.49/1081.40 | 0 |
| 1200 | End-to-end | AI | 50 | 20.00 % | 12.00 % | 0.00 % | 0.00 % | 6 | 19.30 | 30.60 | 36.40 | 0.00 % | 100.00 %/40.00 % | 0.98/0.32 | 74.96/1050.08 | 0 |
| 1300 | End-to-end | Player | 50 | 20.00 % | 12.00 % | 0.00 % | 2.00 % | 6 | 17.70 | 29.90 | 41.40 | 0.00 % | 100.00 %/50.00 % | 0.90/0.42 | 75.61/1102.76 | 0 |
| 1300 | End-to-end | AI | 50 | 20.00 % | 12.00 % | 0.00 % | 0.00 % | 6 | 21.10 | 32.70 | 38.30 | 16.67 % | 100.00 %/40.00 % | 0.98/0.32 | 74.96/1064.43 | 0 |
| 1400 | End-to-end | Player | 50 | 20.00 % | 10.00 % | 0.00 % | 2.00 % | 5 | 18.90 | 30.00 | 31.60 | 0.00 % | 100.00 %/50.00 % | 0.90/0.42 | 75.21/1131.10 | 0 |
| 1400 | End-to-end | AI | 50 | 20.00 % | 12.00 % | 0.00 % | 0.00 % | 6 | 22.40 | 34.70 | 41.10 | 16.67 % | 100.00 %/40.00 % | 0.98/0.32 | 74.96/1093.67 | 0 |

## Interpretation

This report is an envelope diagnostic, not a Production promote. Direct-W12 is the controlled mechanics cohort; End-to-end reports W12 arrival and early-end denominators separately. Compare Player/AI distributions and W13 residual pressure before selecting a separate formal HP task.

Raw per-seed telemetry: `Logs/W12StormcallerPriestBuildEnvelope.csv`. Every row includes candidateHp, cohort, runSeed and side. The Rune loadout assumption must be resolved before Production HP freeze.

## Calibration conclusion

- Diagnostic recommendation: retain `1200-1300 HP` as the next bounded review interval. Direct-W12 P50 is closest to the 32-36s target at 1300, but killed samples are sparse (4-9 per side) and W13 residual is high; this is evidence for review, not a freeze.
- Direct-W12 has a full 50/50 Boss-spawn denominator. End-to-end reaches W12 in only 10/50 runs per side; the other 40/50 are early-end samples and are excluded from TTK percentiles but remain in the cohort denominator.
- End-to-end P50 remains approximately 29-33s across the bounded range, with 0-16.67% 32-36s window coverage and no Boss goal samples in this seed set. Direct-W12 and end-to-end therefore answer different questions and must not be merged.
- Production Stormcaller HP remains **PENDING**; `1200` remains Greybox. No Storm Call, movement, wave, AI, Hero/Basic, Item or Rune Production value was changed.
