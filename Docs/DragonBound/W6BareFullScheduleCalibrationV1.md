# W6 Bare Full-Schedule Calibration V1

- Schedule: real W1-W6 `CoreLoopRhythmDiagnostics` run from seeds 1-1000.
- Item/Rune: disabled by the normal bare diagnostics constructor.
- Boss: fixed Soulchain Binder mechanics; HP is an analysis input only.
- This is a historical calibration report. Production now uses the user-approved shared fixed W6 Boss HP `600` [FROZEN].
- Qualified baseline sample: Boss spawned, the run remained active through its spawn, and at least one deployed Basic or active Hero existed at that instant.
- Power proxy: deployed Basic count plus active Hero level sum. It is not a combat rating.
- Quality strata are fixed before analysis from that spawn-time proxy: lower, middle, and upper thirds of qualified samples per side.

| HP | Side | Qualified | Kill | Leak | TTK Mean | P10 | P25 | P50 | P75 | P90 |
| ---: | :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 350 | Player | 6.00 % | 3.40 % | 0.00 % | 14.73 | 6.50 | 9.00 | 13.65 | 18.30 | 25.41 |
| 350 | AI | 6.00 % | 1.90 % | 0.00 % | 23.59 | 14.96 | 21.25 | 24.30 | 27.65 | 31.52 |

- HP 350 / Player: SpawnBasic=6.50, SpawnHero=1.35, Damage Basic/Hero=17.19 %/82.81 %, Cast S/F=0.06/0.06/0.00, ControlUnitSeconds=0.12, BossAliveAtW7=0.20 %, W6Heart=1.57, ResidualW1-W6=0.00/0.00/0.01/0.00/0.00/0.00, Strata=Low=n=20,Proxy=7.45,Kill=65.00 %,TTKP50=14.10; Normal=n=20,Proxy=8.65,Kill=50.00 %,TTKP50=14.25; High=n=20,Proxy=10.40,Kill=55.00 %,TTKP50=11.00.

- HP 350 / AI: SpawnBasic=6.88, SpawnHero=1.13, Damage Basic/Hero=20.96 %/79.04 %, Cast S/F=0.07/0.07/0.00, ControlUnitSeconds=0.15, BossAliveAtW7=0.70 %, W6Heart=1.80, ResidualW1-W6=0.00/0.01/0.02/0.02/0.01/0.02, Strata=Low=n=20,Proxy=6.85,Kill=50.00 %,TTKP50=23.95; Normal=n=20,Proxy=8.65,Kill=20.00 %,TTKP50=27.35; High=n=20,Proxy=10.40,Kill=25.00 %,TTKP50=24.40.
| 400 | Player | 6.00 % | 3.30 % | 0.00 % | 16.32 | 7.30 | 10.10 | 16.60 | 19.80 | 26.64 |
| 400 | AI | 6.00 % | 1.80 % | 0.00 % | 24.07 | 15.66 | 21.68 | 24.80 | 27.58 | 33.01 |

- HP 400 / Player: SpawnBasic=6.50, SpawnHero=1.35, Damage Basic/Hero=15.45 %/84.55 %, Cast S/F=0.06/0.06/0.00, ControlUnitSeconds=0.12, BossAliveAtW7=0.20 %, W6Heart=1.55, ResidualW1-W6=0.00/0.00/0.01/0.00/0.00/0.00, Strata=Low=n=20,Proxy=7.45,Kill=65.00 %,TTKP50=16.60; Normal=n=20,Proxy=8.65,Kill=45.00 %,TTKP50=17.30; High=n=20,Proxy=10.40,Kill=55.00 %,TTKP50=14.60.

- HP 400 / AI: SpawnBasic=6.88, SpawnHero=1.13, Damage Basic/Hero=19.99 %/80.01 %, Cast S/F=0.07/0.07/0.00, ControlUnitSeconds=0.15, BossAliveAtW7=0.70 %, W6Heart=1.78, ResidualW1-W6=0.00/0.01/0.02/0.02/0.01/0.02, Strata=Low=n=20,Proxy=6.85,Kill=50.00 %,TTKP50=24.70; Normal=n=20,Proxy=8.65,Kill=15.00 %,TTKP50=24.70; High=n=20,Proxy=10.40,Kill=25.00 %,TTKP50=25.70.
| 450 | Player | 6.00 % | 3.10 % | 0.00 % | 17.02 | 7.60 | 10.65 | 17.80 | 21.55 | 25.10 |
| 450 | AI | 6.00 % | 1.80 % | 0.00 % | 25.13 | 16.67 | 23.25 | 25.95 | 28.33 | 34.16 |

- HP 450 / Player: SpawnBasic=6.50, SpawnHero=1.35, Damage Basic/Hero=14.57 %/85.43 %, Cast S/F=0.06/0.06/0.00, ControlUnitSeconds=0.12, BossAliveAtW7=0.30 %, W6Heart=1.55, ResidualW1-W6=0.00/0.00/0.01/0.00/0.00/0.00, Strata=Low=n=20,Proxy=7.45,Kill=60.00 %,TTKP50=17.20; Normal=n=20,Proxy=8.65,Kill=40.00 %,TTKP50=19.40; High=n=20,Proxy=10.40,Kill=55.00 %,TTKP50=16.70.

- HP 450 / AI: SpawnBasic=6.88, SpawnHero=1.13, Damage Basic/Hero=19.51 %/80.49 %, Cast S/F=0.08/0.07/0.00, ControlUnitSeconds=0.16, BossAliveAtW7=0.70 %, W6Heart=1.78, ResidualW1-W6=0.00/0.01/0.02/0.02/0.01/0.02, Strata=Low=n=20,Proxy=6.85,Kill=50.00 %,TTKP50=25.75; Normal=n=20,Proxy=8.65,Kill=15.00 %,TTKP50=26.30; High=n=20,Proxy=10.40,Kill=25.00 %,TTKP50=26.00.
| 500 | Player | 6.00 % | 2.80 % | 0.00 % | 18.60 | 9.60 | 13.75 | 19.20 | 22.38 | 27.29 |
| 500 | AI | 6.00 % | 1.80 % | 0.00 % | 26.13 | 17.19 | 23.98 | 26.70 | 29.50 | 35.73 |

- HP 500 / Player: SpawnBasic=6.50, SpawnHero=1.35, Damage Basic/Hero=13.65 %/86.35 %, Cast S/F=0.07/0.06/0.00, ControlUnitSeconds=0.13, BossAliveAtW7=0.30 %, W6Heart=1.55, ResidualW1-W6=0.00/0.00/0.01/0.00/0.00/0.01, Strata=Low=n=20,Proxy=7.45,Kill=55.00 %,TTKP50=19.00; Normal=n=20,Proxy=8.65,Kill=30.00 %,TTKP50=20.30; High=n=20,Proxy=10.40,Kill=55.00 %,TTKP50=17.10.

- HP 500 / AI: SpawnBasic=6.88, SpawnHero=1.13, Damage Basic/Hero=18.69 %/81.31 %, Cast S/F=0.08/0.07/0.00, ControlUnitSeconds=0.16, BossAliveAtW7=0.80 %, W6Heart=1.78, ResidualW1-W6=0.00/0.01/0.02/0.02/0.01/0.02, Strata=Low=n=20,Proxy=6.85,Kill=50.00 %,TTKP50=26.55; Normal=n=20,Proxy=8.65,Kill=15.00 %,TTKP50=26.90; High=n=20,Proxy=10.40,Kill=25.00 %,TTKP50=27.70.
| 550 | Player | 6.00 % | 2.50 % | 0.00 % | 19.58 | 10.88 | 14.10 | 20.00 | 23.50 | 28.54 |
| 550 | AI | 6.00 % | 1.80 % | 0.00 % | 27.14 | 17.54 | 25.68 | 27.55 | 30.43 | 37.14 |

- HP 550 / Player: SpawnBasic=6.50, SpawnHero=1.35, Damage Basic/Hero=12.97 %/87.03 %, Cast S/F=0.07/0.06/0.00, ControlUnitSeconds=0.13, BossAliveAtW7=0.30 %, W6Heart=1.55, ResidualW1-W6=0.00/0.00/0.01/0.00/0.00/0.01, Strata=Low=n=20,Proxy=7.45,Kill=55.00 %,TTKP50=20.50; Normal=n=20,Proxy=8.65,Kill=20.00 %,TTKP50=18.80; High=n=20,Proxy=10.40,Kill=50.00 %,TTKP50=19.75.

- HP 550 / AI: SpawnBasic=6.88, SpawnHero=1.13, Damage Basic/Hero=17.78 %/82.22 %, Cast S/F=0.08/0.07/0.00, ControlUnitSeconds=0.16, BossAliveAtW7=0.80 %, W6Heart=1.78, ResidualW1-W6=0.00/0.01/0.02/0.02/0.01/0.02, Strata=Low=n=20,Proxy=6.85,Kill=50.00 %,TTKP50=27.40; Normal=n=20,Proxy=8.65,Kill=15.00 %,TTKP50=27.50; High=n=20,Proxy=10.40,Kill=25.00 %,TTKP50=28.70.
| 600 | Player | 6.00 % | 2.50 % | 0.00 % | 20.47 | 11.28 | 15.60 | 20.70 | 24.30 | 29.28 |
| 600 | AI | 6.00 % | 1.60 % | 0.00 % | 27.24 | 18.00 | 24.65 | 28.30 | 29.65 | 36.35 |

- HP 600 / Player: SpawnBasic=6.50, SpawnHero=1.35, Damage Basic/Hero=12.42 %/87.58 %, Cast S/F=0.07/0.06/0.00, ControlUnitSeconds=0.13, BossAliveAtW7=0.50 %, W6Heart=1.53, ResidualW1-W6=0.00/0.00/0.01/0.00/0.00/0.01, Strata=Low=n=20,Proxy=7.45,Kill=55.00 %,TTKP50=22.40; Normal=n=20,Proxy=8.65,Kill=20.00 %,TTKP50=19.30; High=n=20,Proxy=10.40,Kill=50.00 %,TTKP50=20.45.

- HP 600 / AI: SpawnBasic=6.88, SpawnHero=1.13, Damage Basic/Hero=17.63 %/82.37 %, Cast S/F=0.08/0.08/0.00, ControlUnitSeconds=0.16, BossAliveAtW7=0.80 %, W6Heart=1.78, ResidualW1-W6=0.00/0.01/0.02/0.02/0.01/0.02, Strata=Low=n=20,Proxy=6.85,Kill=40.00 %,TTKP50=27.30; Normal=n=20,Proxy=8.65,Kill=15.00 %,TTKP50=28.30; High=n=20,Proxy=10.40,Kill=25.00 %,TTKP50=28.80.
| 650 | Player | 6.00 % | 2.40 % | 0.00 % | 21.86 | 13.27 | 17.08 | 21.90 | 25.68 | 31.26 |
| 650 | AI | 6.00 % | 1.50 % | 0.00 % | 27.17 | 19.14 | 24.00 | 29.00 | 29.45 | 35.28 |

- HP 650 / Player: SpawnBasic=6.50, SpawnHero=1.35, Damage Basic/Hero=11.94 %/88.06 %, Cast S/F=0.07/0.07/0.00, ControlUnitSeconds=0.13, BossAliveAtW7=0.50 %, W6Heart=1.53, ResidualW1-W6=0.00/0.00/0.01/0.00/0.00/0.01, Strata=Low=n=20,Proxy=7.45,Kill=55.00 %,TTKP50=23.70; Normal=n=20,Proxy=8.65,Kill=15.00 %,TTKP50=17.90; High=n=20,Proxy=10.40,Kill=50.00 %,TTKP50=21.90.

- HP 650 / AI: SpawnBasic=6.88, SpawnHero=1.13, Damage Basic/Hero=17.21 %/82.79 %, Cast S/F=0.08/0.08/0.00, ControlUnitSeconds=0.16, BossAliveAtW7=1.10 %, W6Heart=1.78, ResidualW1-W6=0.00/0.01/0.02/0.02/0.01/0.02, Strata=Low=n=20,Proxy=6.85,Kill=40.00 %,TTKP50=28.10; Normal=n=20,Proxy=8.65,Kill=15.00 %,TTKP50=29.10; High=n=20,Proxy=10.40,Kill=20.00 %,TTKP50=28.70.

## Recommendation

No candidate satisfied the 28-32s qualified-sample median target on both Player and AI. Production remains unchanged; the side bias and low W6 qualification rate must be reviewed before formal HP selection.

Raw per-seed telemetry is written to `Logs/W6BareFullScheduleCalibration.csv`.

## Runtime Boundary

- This report uses `CoreLoopRhythmDiagnostics.RunW6BareCalibration`, beginning at W1 with the production R1 normal-enemy HP schedule, Recruit V3, finite component bag, Forge Pick, merge, Basic/Hero combat, and AI V0.
- The diagnostics use `EmptyItemRunSnapshotProvider`; Item and Rune loadouts are not present.
- Soulchain Binder is spawned only by the W6 production boss slot. The legacy `W6SoulChainTelemetryRunner` static board fixture is not used for this report.
- The observation continues through W7 only to observe the W6 Boss residual/settlement. It never treats W7 enemies as a W6 Boss fixture.
- A Boss reaching the goal follows the production InstantDefeat rule. The historical candidates were not promoted; the current Production source is `SoulchainBinderConfiguration.GreyboxMaxHitPoints = 600`.

## Interpretation

The same 1..1000 Seed set produces only 60 qualified Boss-spawn samples per side (6.00%). The other runs are legitimate pre-W6 Bare failures, not discarded rerolls. This makes a formal W6 HP freeze unsound: Player qualified-sample medians remain below the 28-32 second target throughout this 350-650 sweep, while AI reaches that window only at the high end. The next decision must address this build/side qualification bias before another HP promote attempt.

## Verification

- Targeted EditMode: `W6BareFullScheduleCalibrationTests`, 2/2 passed, 0.61s. XML: `Logs/W6BareCalibration-Targeted-v2.xml`.
- Fast EditMode: 454/454 passed, 3.46s. XML: `Logs/W6BareCalibration-FastEditMode.xml`.
- Full PlayMode: 27/27 passed, 25.77s. XML: `Logs/W6BareCalibration-PlayMode.xml`.
- Full EditMode was intentionally not run for this calibration task.
