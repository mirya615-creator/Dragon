# DragonBound Hero Slice Acceptance

Date: 2026-08-04

## Scope

Implemented only the first hero vertical slice:

- Purple: Windclaw Ranger
- Gold: Dragon Rider
- Components: Dragon Sigil x2, Sky Ranger x1 UNIQUE, Dragon Knight x1 UNIQUE

The legacy 24-component deck remains available behind its original mode. `Greybox_Main` remains basic-only (`EnableHeroComponents=false`, `HeroSliceMode=false`). `HeroSlice_Main` is an independent scene with both flags enabled. No weapons, other ten heroes, full 24-component runtime mode, Boss expansion, or 15-wave work was added.

## Recruitment

HeroSlice recruitment is deterministic for a fixed RunSeed:

| Recruitment | Components | Basic cards |
|---|---|---|
| 1 | Dragon Sigil x1 | 4 seeded basics |
| 2 | Sky Ranger x1, Dragon Sigil x1 | 3 seeded basics |
| 3 | Dragon Knight x1 | 4 seeded basics |
| 4+ | none | 5 seeded basics |

Drawn finite components are removed permanently. Bench refresh removes only bench cards; deployed cards and heroes remain. Refresh logs `ComponentDiscardedByRefresh`, and UNIQUE cards cause the `REFRESH WILL LOSE UNIQUE` button warning. Both sides use independent, equal finite bags and the same pricing rules. HeroSlice starts both teams at 36 supplies so three legal recruitments (10 + 12 + 14) are reachable without granting AI an advantage.

## Formation and board

Correct components form automatically after the second component is placed in orthogonally adjacent battle cells. Diagonal and separated cells do not form. A formed hero has one stable RuntimeId and an atomic two-cell `BoardFootprint`; both cells resolve to the same occupant. The footprint moves and rolls back as one object, cannot be moved to the bench, and cannot be merged with a basic unit. A hero can be formed only once per recipe per side.

Formation visuals are authored through `HeroFormation.prefab`: connector line, two flashes, rarity-colored two-cell border, and hero name. The card prefab has editable hero border, level and XP hooks. Formation progress is 0.6 seconds and advances during `Ready` as well as combat; combat remains disabled until it completes.

## Hero combat

Windclaw Ranger uses 14 base attack, 1.80 attack speed, 4.25-cell range, elite-first targeting, and a fifth-attack Power Shot (180%, plus 25% against elites). Dragon Rider uses 13 base attack, 1.70 attack speed, 3.75-cell range, four-target 0.65-cell area attacks, a six-second 2x dive corridor, and a three-second flame zone at 25% base attack per second. Both heroes share side kill experience, with elite kills worth 3 and all other enemies worth 1. Formation-time kills are not replayed. Windclaw caps at Lv3 and Dragon Rider at Lv5.

## Verification

- EditMode XML: `Logs/DragonBoundHeroSlice-EditMode.xml`
  - 106 passed, 0 failed
- PlayMode XML: `Logs/DragonBoundHeroSlice-PlayMode.xml`
  - 8 passed, 0 failed
- Compile log: `Logs/DragonBoundHeroSlice-Compile.log`
- Runtime and recruitment evidence: `Logs/DragonBoundHeroSlice-EditMode.log`, `Logs/DragonBoundHeroSlice-PlayMode.log`
- Attempted runtime UI capture: `Logs/DragonBoundHeroSlice_720x1280.png`. In `-nographics`, Unity reports a uniform frame; the capture is retained for reference and does not affect functional test results.

The EditMode suite includes all named hero-slice tests plus the existing basic-unit tests. PlayMode covers both the original `Greybox_Main` scene and `HeroSlice_Main`, including AI formation, two-cell rendering, range center, and Ready-phase formation timing.

## Changed files

Runtime: `Runtime/AI/BasicUnitAiController.cs`, `Runtime/Bootstrap/DragonBoundBootstrap.cs`, `Runtime/Combat/HeroCombatState.cs`, `Runtime/Combat/HeroSliceCatalog.cs`, `Runtime/Combat/TargetingSystem.cs`, `Runtime/Core/EnemyRuntime.cs`, `Runtime/Core/ThreeWaveSliceRuntime.cs`, `Runtime/Grid/BoardGrid.cs`, `Runtime/Grid/DragPlacementController.cs`, `Runtime/Presentation/DraggableUnitView.cs`, `Runtime/Presentation/DragonBoundScreenView.cs`, `Runtime/Presentation/GreyboxBoardView.cs`, `Runtime/Presentation/GreyboxHudView.cs`, `Runtime/Presentation/GreyboxRecruitmentPanel.cs`, `Runtime/Presentation/HeroFormationView.cs`, `Runtime/Presentation/HeroSliceCardPresentation.cs`, `Runtime/Recruitment/BoardRecruitDestination.cs`, `Runtime/Recruitment/HeroSliceRecruitmentConfig.cs`, `Runtime/Recruitment/RecruitDeck.cs`, `Runtime/Recruitment/RecruitmentDefinitions.cs`, `Runtime/Recruitment/RecruitmentService.cs`.

Editor/UI: `Editor/DragonBoundHeroSliceSceneBuilder.cs`, `Editor/DragonBoundPortraitUiBuilder.cs`, `UI/Prefabs/Components/HeroFormation.prefab`, `UI/Prefabs/Components/UnitCard.prefab`, generated UI module/screen prefabs, `Scenes/HeroSlice_Main.unity`, and the preserved basic `Scenes/Greybox_Main.unity`.

Tests: `Tests/EditMode/BoardGridTests.cs`, `Tests/EditMode/HeroSliceTests.cs`, `Tests/EditMode/PortraitUiPrefabTests.cs`, `Tests/EditMode/RecruitDeckTests.cs`, `Tests/EditMode/RecruitmentServiceTests.cs`, `Tests/PlayMode/HeroSlicePlayModeTests.cs`.
