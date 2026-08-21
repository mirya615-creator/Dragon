# Joint Item + Rune + Boss Balance Calibration Formal V1

- Seed set: `1..1000`; every original early failure remains in the denominator.
- Build: `BARE_FORMAL + AI_V0`; Item and Rune are disabled because no authoritative standard Rune build exists in the client diagnostic API.
- Candidate Boss HP: W6/W12/W16/W20 = `600/1200/2400/5000`.
- This report is a pressure baseline, not a Production HP promote.

JointBalanceCalibration Build=BARE_FORMAL Seeds=1000
[Player] ReachedW20=0.00 % EndWaveP50=7.00
[Player] W6 Spawn=76.90 % Kill=40.60 % Goal=2.30 % TTK_P50=22.90s
[Player] W12 Spawn=15.20 % Kill=10.10 % Goal=0.10 % TTK_P50=27.20s
[Player] W16 Spawn=5.20 % Kill=2.20 % Goal=0.10 % TTK_P50=31.09s
[Player] W20 Spawn=0.00 % Kill=0.00 % Goal=0.00 % TTK_P50=-1.00s
[AI] ReachedW20=0.00 % EndWaveP50=7.00
[AI] W6 Spawn=76.90 % Kill=44.10 % Goal=0.80 % TTK_P50=24.80s
[AI] W12 Spawn=15.20 % Kill=11.10 % Goal=0.10 % TTK_P50=25.20s
[AI] W16 Spawn=5.20 % Kill=3.00 % Goal=0.20 % TTK_P50=30.19s
[AI] W20 Spawn=0.00 % Kill=0.00 % Goal=0.00 % TTK_P50=-1.00s


## Interpretation

W16/W20 Boss TTK percentiles use only actually spawned and killed samples. A low spawn rate is retained as pressure evidence and is not replaced by direct-Boss rerolls.
Item/Rune STANDARD and FULL cohorts remain pending until an authoritative Rune build fixture is provided by the owning configuration boundary.
