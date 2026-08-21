# Boss Full Regression Plan V1

Status: `READY FOR EXECUTION`

This plan covers the four fixed Boss identities currently wired into the Unity
pressure runtime. It verifies runtime contracts and wave integration first; it
does not promote Greybox HP values to Production balance.

## Fixed Boss Coverage

| Wave | Boss | Integration fixture | Runtime/contract fixtures | Current scope |
| --- | --- | --- | --- | --- |
| W6 | `Soulchain Binder` | `W6SoulChainV1Tests` | `BossesContractsTests` | 2x2 Basic control, duration/merge inheritance, empty target cooldown, Spellbreaker reflection, XP and slot/count rules |
| W12 | `Stormcaller Priest` | `W12StormcallerPriestV1Tests` | `BossesContractsTests` | cast snapshot, shield/overflow, speed effect expiry/refresh, Spellbreaker, XP and slot/count rules |
| W16 | `Bloodcrown Tyrant` | `W16BloodcrownIntegrationTests` | `BloodcrownTyrantRuntimeTests`, `BossesContractsTests` | Decree Basic policy, future Basic coverage, merge gate, Spellbreaker, death restore, XP and slot/count rules |
| W20 | `Worldeater Wyrm` | `W20WorldeaterIntegrationTests` | `WorldeaterWyrmRuntimeTests`, `BossesContractsTests` | Devour target lock/invalid target, linear HP growth, fixed four-minion summon, Spellbreaker, persistence, InstantDefeat and XP |

The four Boss integration fixtures currently pass on this branch:

| Fixture | Result | Artifact |
| --- | ---: | --- |
| `W6SoulChainV1Tests` | 12/12 | `Logs/TestLane-Targeted-20260818-202509.xml` |
| `W12StormcallerPriestV1Tests` | 9/9 | `Logs/TestLane-Targeted-20260818-202534.xml` |
| `W16BloodcrownIntegrationTests` | 4/4 | `Logs/TestLane-Targeted-20260818-202557.xml` |
| `W20WorldeaterIntegrationTests` | 4/4 | `Logs/TestLane-Targeted-20260818-202621.xml` |

## Repeatable Gate Sequence

Run from `F:\unity文件\Dragon`:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane Targeted -TestFilter "W6SoulChainV1Tests"
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane Targeted -TestFilter "W12StormcallerPriestV1Tests"
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane Targeted -TestFilter "W16BloodcrownIntegrationTests"
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane Targeted -TestFilter "W20WorldeaterIntegrationTests"
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane Targeted -TestFilter "BossesContractsTests"
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane FastEditMode
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane PlayMode
```

The last two gates are required because Bosses are constructed by the shared
pressure composition root. The current verified baseline after W20 integration
is Fast EditMode `532/532` and PlayMode `29/29`.

For a release candidate or any change to RNG, diagnostics, AI fairness, wave
composition, or balance sampling, append:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane FullEditMode
```

Full EditMode includes diagnostic/long-running suites and is intentionally not
an everyday gate.

## Assertions Required Before Balance Sign-off

1. Each fixed Boss is spawned in its independent slot and does not increase the
   Normal count for its wave.
2. Boss movement remains `0.20 cells/s`; Boss goal handling remains
   `InstantDefeat`.
3. Boss summons remain separate from the Normal wave budget. W20 creates four
   minions per successful summon, with no XP/resource reward and persistence
   after Boss death.
4. Spellbreaker is evaluated only for a legal cast attempt. Invalid/empty
   targets and the W20 fixed summon behavior must keep their specified cooldown
   and reflection semantics.
5. Boss XP is awarded only for a valid Hero last hit using the frozen map
   W6=6, W12=10, W16=15, W20=20.
6. Wave schedule and residual enemies remain independent of Boss death or summon
   cleanup.

## Known Gaps

- W6 `600` is the current user-approved Production value. W12 `1200`, W16
  `2400`, and W20 `5000` remain Greybox/candidate values in their verification
  records and must not be called final balance.
- No combined four-Boss, multi-seed balance run is currently a production gate.
  That run belongs after Item and Rune mechanics are closed and their standard
  builds are enabled.
- Boss analytics and UI/Prefab presentation are covered only where existing
  typed seams/tests exist; they are not substituted for runtime regression.
- Full EditMode has not been rerun by this plan; use the release-candidate gate
  above when the balance or diagnostics inputs change.

## Handoff Rule

Mechanism regressions may be fixed without touching Boss HP. Production HP and
TTK are calibrated only after Boss, Item and Rune behavior are integrated, using
the same seeds and build envelopes for Player and AI. A failed mechanism test is
an implementation defect; a TTK/kill-rate miss is a separate balance result.
