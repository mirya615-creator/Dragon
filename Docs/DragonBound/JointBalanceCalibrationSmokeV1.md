# Joint Item + Rune + Boss Balance Calibration Smoke V1

- Seed set: `1..50`.
- Full pressure: real W1-W20 `BARE + AI_V0` diagnostic with W6/W12/W16/W20 candidate HP `600/1200/2400/5000`.
- W12 Item fixture: existing two-item diagnostic; Rune snapshot remains empty because no authoritative standard Rune build exists.
- No Production HP or gameplay rule is written by this batch.

## Full W1-W20 BARE / AI_V0

JointBalanceCalibration Build=BARE Seeds=50
[Player] ReachedW20=0.00 % EndWaveP50=7.00
[Player] W6 Spawn=72.00 % Kill=28.00 % Goal=4.00 % TTK_P50=23.00s
[Player] W12 Spawn=20.00 % Kill=12.00 % Goal=0.00 % TTK_P50=29.00s
[Player] W16 Spawn=4.00 % Kill=2.00 % Goal=0.00 % TTK_P50=24.89s
[Player] W20 Spawn=0.00 % Kill=0.00 % Goal=0.00 % TTK_P50=-1.00s
[AI] ReachedW20=0.00 % EndWaveP50=7.00
[AI] W6 Spawn=72.00 % Kill=46.00 % Goal=0.00 % TTK_P50=27.30s
[AI] W12 Spawn=20.00 % Kill=12.00 % Goal=0.00 % TTK_P50=32.30s
[AI] W16 Spawn=4.00 % Kill=2.00 % Goal=0.00 % TTK_P50=28.19s
[AI] W20 Spawn=0.00 % Kill=0.00 % Goal=0.00 % TTK_P50=-1.00s

## W6 BARE / AI_V0

- Player: spawn=72.00 %, kill=28.00 %, leak=0.00 %, TTK P50=22.80s.
- AI: spawn=72.00 %, kill=46.00 %, leak=0.00 %, TTK P50=27.30s.

## W12 STANDARD fixture / AI_V0

| HP | Side | Spawn | Kill | Goal | TTK n | P50 | Window32-36 | Item activations | W13 residual |
| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1100 | Player | 20.00 % | 14.00 % | 0.00 % | 7 | 28.10 | 0.00 % | 0.20 | 0.26 |
| 1100 | AI | 20.00 % | 14.00 % | 0.00 % | 7 | 28.90 | 14.29 % | 0.20 | 0.58 |
| 1200 | Player | 20.00 % | 12.00 % | 0.00 % | 6 | 29.10 | 0.00 % | 0.20 | 0.34 |
| 1200 | AI | 20.00 % | 12.00 % | 0.00 % | 6 | 30.60 | 0.00 % | 0.20 | 0.64 |
| 1300 | Player | 20.00 % | 12.00 % | 0.00 % | 6 | 29.90 | 0.00 % | 0.20 | 0.34 |
| 1300 | AI | 20.00 % | 12.00 % | 0.00 % | 6 | 32.70 | 16.67 % | 0.20 | 0.62 |

## Scope boundary

This smoke result is a baseline artifact, not a Production promote. STANDARD/FULL Rune builds, AI levels 1-3 and loss-streak downgrades remain separate implementation work.
