# Direct W16 / W20 Boss Envelope Calibration V1

- Seed set: `1..50` for every candidate.
- Cohort: `DIRECT_BOSS + AI_V0`, fixed development pair and diagnostic resources; this is not a Production run entry.
- Item/Rune: disabled. Rune standard build is not authoritative in the current client diagnostic API.
- TTK percentiles use killed samples only; early failure is retained in the spawn denominator.

## W16 Bloodcrown Tyrant

| HP | Side | Spawn | Kill | Goal | TTK P25 | TTK P50 | TTK P75 | Damage P50 |
| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 2000 | Player | 100.00 % | 6.00 % | 0.00 % | 38.70 | 38.70 | 40.80 | 2003.00 |
| 2000 | AI | 100.00 % | 10.00 % | 0.00 % | 35.90 | 38.70 | 41.50 | 2027.40 |
| 2400 | Player | 100.00 % | 6.00 % | 0.00 % | 42.10 | 42.10 | 51.70 | 2448.60 |
| 2400 | AI | 100.00 % | 6.00 % | 0.00 % | 39.90 | 39.90 | 42.10 | 2445.40 |
| 2800 | Player | 100.00 % | 2.00 % | 0.00 % | 43.40 | 43.40 | 43.40 | 2841.72 |
| 2800 | AI | 100.00 % | 4.00 % | 0.00 % | 43.10 | 43.40 | 43.40 | 2898.96 |
| 3200 | Player | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 |
| 3200 | AI | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 |

## W20 Worldeater Wyrm

| HP | Side | Spawn | Kill | Goal | TTK P25 | TTK P50 | TTK P75 | Damage P50 | Summons P50 |
| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 4000 | Player | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |
| 4000 | AI | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |
| 5000 | Player | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |
| 5000 | AI | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |
| 6000 | Player | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |
| 6000 | AI | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |
| 7000 | Player | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |
| 7000 | AI | 100.00 % | 0.00 % | 0.00 % | -1.00 | -1.00 | -1.00 | -1.00 | 8.00 |

## Decision boundary

These are mechanics-envelope measurements only. No HP is promoted until Item + Rune standard/full cohorts, Spellbreaker cohorts, and end-to-end pressure results are available.
