# W6 Board-Quality Shared HP V1 Diagnostic

Historical diagnostic status: the dynamic BoardQuality schemes were not promoted. Production
now uses the shared fixed W6 Boss HP `600` [FROZEN].

- Fixed 500 baseline plus exactly two dynamic schemes, each using the same Seed set `1..1000`.
- BoardQuality is snapshotted once immediately before W6 Boss generation and uses only deployed Basic units and completed Hero pairs.
- Formula: `sum(Basic Attack*AttackSpeed) + sum(Hero Attack*AttackSpeed)` at current configured level. No range, position, Bench, unpaired Component, temporary effect, resource, Item, or Rune state is included.
- ReferenceQuality is `max(PlayerQuality, AIQuality)`. Equal values deterministically select Player as ReferenceSide for paired diagnostics; both sides always receive the same shared HP.
- Quality thresholds are derived from the fixed-500 Seed distribution, not hand-authored.

Raw fixed-500 side baseline from the same repaired W1-W6 path: Player killed `438/769` with TTK P25/P50/P75 `13.88/21.75/28.48s`, AI killed `462/769` with `16.12/23.10/30.40s`; 20-25s hit rates are `18.26%` and `17.53%`. The Reference/Weak rows below re-label each seed by the higher BoardQuality, so their percentiles are intentionally different from the raw side rows.

## Derived quality distribution

- Qualified ReferenceQuality samples=769/1000
- Q25/Q50/Q75=41.61/49.69/63.58
- Tier mapping: T1 < Q25, T2 < Q50, T3 < Q75, T4 >= Q75. HP mapping is monotonic in every scheme.

## Overall comparison

| Scheme | Shared HP rule | Reference TTK P25/P50/P75 | Weak TTK P25/P50/P75 | Reference 20-25 | Weak 20-25 | Reference quality delta | TTK delta |
| :--- | :--- | :--- | :--- | ---: | ---: | ---: | ---: |
| Fixed500 | 500/500/500/500 | 13.10/20.65/27.10s | 17.08/26.50/33.40s | 20.19 % | 14.44 % | 15.34 | 4.61 |
| Tight | 450/500/550/600 | 14.10/21.80/27.40s | 18.00/27.10/33.85s | 20.07 % | 12.10 % | 15.34 | 4.34 |
| Broad | 400/500/600/700 | 15.50/22.40/28.30s | 18.90/27.15/34.93s | 21.12 % | 14.71 % | 15.34 | 4.46 |

### Fixed500 per tier

| Tier | HP | Samples | Reference Kill | Weak Kill | Reference P25/P50/P75 | Weak P25/P50/P75 | Reference 20-25 | Weak 20-25 | Early End |
| :--- | ---: | ---: | ---: | ---: | :--- | :--- | ---: | ---: | ---: |
| T1 | 500.00 | 423 | 16.31 % | 11.82 % | 16.20/21.20/29.10s | 22.08/30.45/35.95s | 13.04 % | 14.00 % | 231 |
| T2 | 500.00 | 192 | 68.75 % | 41.67 % | 14.18/22.20/31.10s | 17.10/26.40/35.40s | 15.15 % | 11.25 % | 0 |
| T3 | 500.00 | 192 | 81.77 % | 54.69 % | 14.90/21.90/26.60s | 16.90/27.20/32.80s | 20.38 % | 11.43 % | 0 |
| T4 | 500.00 | 193 | 94.30 % | 64.77 % | 11.10/18.00/23.00s | 16.20/23.90/31.30s | 26.37 % | 19.20 % | 0 |
### Tight per tier

| Tier | HP | Samples | Reference Kill | Weak Kill | Reference P25/P50/P75 | Weak P25/P50/P75 | Reference 20-25 | Weak 20-25 | Early End |
| :--- | ---: | ---: | ---: | ---: | :--- | :--- | ---: | ---: | ---: |
| T1 | 450.00 | 423 | 17.26 % | 11.82 % | 14.10/21.30/28.90s | 21.40/28.60/34.75s | 10.96 % | 12.00 % | 231 |
| T2 | 500.00 | 192 | 68.75 % | 41.67 % | 14.18/22.20/31.10s | 17.10/26.40/35.40s | 15.15 % | 11.25 % | 0 |
| T3 | 550.00 | 192 | 80.21 % | 52.08 % | 16.05/22.65/27.35s | 18.53/27.90/33.25s | 18.83 % | 9.00 % | 0 |
| T4 | 600.00 | 193 | 92.75 % | 60.62 % | 13.30/20.00/24.60s | 18.00/25.40/33.40s | 28.49 % | 15.38 % | 0 |
### Broad per tier

| Tier | HP | Samples | Reference Kill | Weak Kill | Reference P25/P50/P75 | Weak P25/P50/P75 | Reference 20-25 | Weak 20-25 | Early End |
| :--- | ---: | ---: | ---: | ---: | :--- | :--- | ---: | ---: | ---: |
| T1 | 400.00 | 423 | 18.44 % | 12.06 % | 12.68/22.15/30.30s | 20.10/27.70/34.45s | 17.95 % | 9.80 % | 231 |
| T2 | 500.00 | 192 | 68.75 % | 41.67 % | 14.18/22.20/31.10s | 17.10/26.40/35.40s | 15.15 % | 11.25 % | 0 |
| T3 | 600.00 | 192 | 78.13 % | 50.00 % | 17.03/23.35/28.28s | 19.48/27.80/33.63s | 20.67 % | 13.54 % | 0 |
| T4 | 700.00 | 193 | 90.67 % | 58.55 % | 15.65/21.40/25.10s | 19.80/27.00/36.20s | 27.43 % | 20.35 % | 0 |

## Decision

Fixed500 remains the best baseline for this rule set. Tight changes ReferenceSide P50 from 20.65s to 21.80s and 20-25s hit rate from 20.19% to 20.07%; Broad changes them to 22.40s and 21.12%. The maximum window gain is only 0.93 percentage points, while WeakSide P50 moves from 26.50s to 27.10-27.15s and P75 remains above 33s.

The dynamic schemes did not significantly narrow the distribution or improve the strong-side outcome. They were therefore not added as a runtime system. Production uses the shared fixed `600` HP decision recorded in the current configuration baseline; the fixed-500 rows above remain historical comparison data.

Raw metrics: `Logs/W6BoardQualitySharedHpComparison.csv`.
