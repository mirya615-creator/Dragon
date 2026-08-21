# Pressure Race Art Handoff

The twenty-wave pressure runtime owns scheduling, enemy archetypes, combat sides, path
progress, movement speed, hit points, and leak resolution. Presentation may only replace
sprites through `PressureRaceArtCatalog` and the semantic slots below.

| Slot | Use |
| --- | --- |
| `ART_Enemy_Normal` | Normal enemy body |
| `ART_Enemy_Fast` | Fast enemy body |
| `ART_Enemy_Swarm` | Swarm enemy body, reserved for the existing archetype |
| `ART_Enemy_Elite` | Elite enemy body |
| `ART_Enemy_BossReserved` | Future Boss placeholder only; this V2 runtime does not spawn Bosses |
| `ART_Enemy_HealthBar` | Enemy health-bar treatment |
| `ART_Enemy_HitFlash` | Enemy hit feedback treatment |

`EnemyView.ArtSlotId` exposes the active enemy body slot at runtime. If no catalog or sprite
is assigned, the existing greybox body is retained. Art must not rename runtime IDs, change
the `EnemyView` root, modify waypoint anchors, alter the enemy registry, or write combat state.
