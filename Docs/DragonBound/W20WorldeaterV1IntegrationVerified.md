# W20 Worldeater Wyrm V1 Integration

Status: runtime mechanism integrated; Production HP remains `PENDING`. The configured `5000`
value is Greybox only.

`TwentyWavePressureRuntime.BeginWave(20)` uses the existing wave queue and creates one
independent `BOSS_WORLDEATER_WYRM` slot per side. The Boss is not part of the regular enemy
count and remains in the shared side registry across the W20 schedule.

`WorldeaterWyrmRuntime` is engine-independent and receives typed ports for the Boss target,
Devour targets, summons and Spellbreaker. Devour locks the lowest StoredLevel Basic first, then
Worldeater Minion, with stable RuntimeId tie-breaking. Invalid or absent targets consume a full
15-second cooldown without Spellbreaker. Valid targets are consumed at windup resolution and
grow Boss Max/Current HP by a fixed fraction of the original Boss MaxHP, never compounding.

Summon casts resolve independently at 12.0s + 0.75s and each successful cast adds exactly four
330 HP, 0.75 cells/s `EnemyArchetype.Swarm` entities. They have no XP or resource reward, do not
block wave schedule completion, persist after Boss death, and reaching the goal uses
`InstantDefeat`.

SubBoss remains an interface-only target class and is not produced. W6/W12/W16 behavior and
values remain unchanged.

Verification fixture files:

- `Assets/DragonBound/Tests/EditMode/WorldeaterWyrmRuntimeTests.cs`
- `Assets/DragonBound/Tests/EditMode/W20WorldeaterIntegrationTests.cs`

Verification completed on 2026-08-18:

| Lane | Result | Artifact |
| --- | --- | --- |
| W20 integration targeted EditMode | 4/4 passed | `Logs/TestLane-Targeted-20260818-200955.xml` |
| W20 runtime targeted EditMode | 7/7 passed | `Logs/TestLane-Targeted-20260818-201036.xml` |
| Fast EditMode | 532/532 passed | `Logs/TestLane-FastEditMode-20260818-201117.xml` |
| Full PlayMode | 29/29 passed | `Logs/TestLane-PlayMode-20260818-201155.xml` |

`git diff --check` passed. `Assets/google-services.json` and its `.meta` file are local
release artifacts and are intentionally excluded from this integration change.
