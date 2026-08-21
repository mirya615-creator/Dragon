# Drakeforge Module Boundary Audit V1

## Audit basis

- Repository: `F:/unity文件/Dragon`
- Audited HEAD: `691f21d9afd1813c63ad700d393dbd7e4d296e5d`
- Worktree exception: `Assets/google-services.json` and its `.meta` are untracked user-provided release files. They were not read, modified, or staged.
- This phase is read-only for code and Unity assets. No asmdef, Scene, Prefab, ProjectSettings, or Production value was changed.
- Inventory: 111 Runtime `.cs` files, 17 Editor `.cs` files, 52 EditMode test `.cs` files, 5 PlayMode test `.cs` files, 4 Scenes, 14 Prefabs, 6 asmdefs.

## Current assembly reality

| Assembly | Current references | Actual boundary | Finding |
| --- | --- | --- | --- |
| `GameShared.Runtime` | none | Seed/random primitives | Cleanest foundation; Unity-free intent is not enforced by a separate contract assembly. |
| `DragonBound.Runtime` | `GameShared.Runtime`, `Unity.UGUI` | All gameplay, AI, Items, Runes, Bootstrap, Presentation, Analytics | Monolith. Namespace folders do not create dependency boundaries. |
| `DragonBound.HandoffUi` | `Unity.UGUI`, `Unity.TextMeshPro` | Handoff preview views and UI contracts | Technically isolated, but its preview is mock-only and its Prefab is consumed by Integration tests/scenes. |
| `DragonBound.Editor` | Runtime, HandoffUi, Unity UI/TMP | Builders, diagnostics, capture, Android builder | Integration/editor owner; several diagnostics instantiate Runtime directly. |
| `DragonBound.Tests.EditMode` | GameShared, Runtime, HandoffUi, Unity UI | Whole-runtime tests | Broad compile dependency prevents module-level ownership today. |
| `DragonBound.Tests.PlayMode` | GameShared, Runtime, HandoffUi, Unity UI | Scene/bootstrap/UI tests | Scene-level integration suite, not independently movable. |

## Module findings

| Proposed module | Current files / classes | Current dependencies | Reverse or boundary violations | Move now? | Required seam before move | Owner | Risk |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Foundation/Core | `Runtime/Core/*`: MatchController, TeamState, RunSnapshot, WaveSystem, settlement, diagnostics (27 files) | Combat, Grid, Recruitment, AI, Items, Runes | Core owns diagnostics and reaches into every gameplay subsystem; `TwentyWavePressureRuntime` directly constructs Item/Rune runtime. | No | `Foundation.Contracts`, `IMatchClock`, `IRunRandom`, `ICombatEventSink`, `IWaveSchedule` | Core Loop | Very high; largest fan-in/fan-out. |
| Match/Waves | `TwentyWavePressureRuntime`, `PressureRaceSideRuntime`, `WaveSystem`, `TwentyWavePressureConfiguration`, `TeamState`, `MatchSettlement` | Core, Grid, Combat, Recruitment, Items, Runes | Match creates concrete recruitment/combat paths and owns both Player/AI sides. | No | `IMatchSide`, `IWaveSpawner`, `IEnemyGoalSettlement`, `IRunSnapshotProvider` | Core Loop | High; changing this changes production pacing and settlement. |
| Board/Deployment | `Grid/*`, `BoardGrid`, `BattlefieldLayoutDefinition`, drag contracts | Core, Combat | Board data uses `CombatPoint` and `TeamState`; UI drag objects live in the same Runtime assembly. | Partial | `Board.Contracts` with `GridPosition`, `BoardOccupant`, side transform and mutation events | Board | High; serialized layout and side-mirror behavior. |
| Recruitment/Merge | `Recruitment/*`, BoardRecruitDestination, RecruitmentService, HeroRecipeValidation | Grid, Core, Combat, Runes | Recruitment creates `HeroCombatState`; destination resolves pair links and Rune-aware operations. | No | `IRecruitDestination`, `IHeroPairFactory`, `IRecruitDeck`, `IRecruitmentEventSink` | Economy/Recruitment | Very high; finite bag and PairLink invariants. |
| Combat/Targeting | `Combat/*`, HeroCombatState, TargetingSystem, GroundHazardSystem, BasicUnitCatalog | Core, Grid, Recruitment, Runes | Hero combat owns progression and Rune modifiers; `EnemyRuntime` is a Core type. | No | `Combat.Contracts`, `ICombatTarget`, `IDamageSink`, `ICombatModifierPipeline` | Combat | Very high; 12 Hero skill rules and XP attribution. |
| Heroes/XP/Skills | Hero definitions in `Combat/HeroSliceCatalog`, `Recruitment/FrozenHeroConfiguration`, `HeroXpSettlement`, PairLink types | Combat, Recruitment, Core | Hero identity/configuration is split across Combat, Recruitment and Core; XP settlement is Core but consumes Combat ownership. | No | `Heroes.Contracts`, `HeroDefinition`, `HeroProgression`, `IHeroSkillExecutor`, `IHeroXpAwarder` | Heroes | Very high; split source of truth and skill scaling. |
| Enemies/Path | `EnemyRuntime`, `EnemyPath`, `PathDisplacementSystem`, `TargetingSystem` portions, Soulchain Boss | Core, Combat, Grid | Enemy lifecycle, path movement, Boss and damage resolution are mixed in Core; targeting is Combat-owned. | No | `Enemies.Contracts`, `IPathProgress`, `IEnemyLifecycle`, `IEnemyDamageReceiver` | Enemies/Bosses | High; goal settlement and residual enemies. |
| Bosses/Summons | `SoulchainBinderRuntime`, W6 telemetry/calibration, Boss branch in `TwentyWavePressureRuntime` | Core, Combat, Recruitment/Grid indirectly | Boss configuration is embedded in Core and W6 schedule; no Boss assembly or Boss interface. | No | `IBossDefinition`, `IBossRuntime`, `IBossCastEvent`, `IBossSpawnPolicy` | Bosses | High; W6 HP/settlement frozen and must remain one source. |
| AI | `BasicUnitAiController`, survival and symmetry diagnostics | Core, Grid, Combat, Recruitment | AI directly mutates BoardRecruitDestination and knows concrete Hero/Basic rules; diagnostics are production-adjacent. | No | `IAiDecisionContext`, `IAiDeploymentPolicy`, `IAiRecruitPolicy`, side-local board view | AI | High; deterministic side symmetry and tick ordering. |
| Items/Merchant | `Items/*`, two implemented effects, 18 pending definitions | Core (`TeamState`, `EnemyRegistry`) | Item runtime is in monolith; Merchant is only Handoff mock/contracts, no authoritative ledger. | Partial | `Item.Contracts`, `IItemRunSnapshotProvider`, `IItemEffectRuntime`, `IItemServerLedger` | Items/Meta | Medium-high; snapshot authority and pending catalog status. |
| Runes | `Runes/*`, persistence, loadout, combat effects, presentation data | Core, Recruitment, Combat | Rune combat is called from HeroCombatState; persistence directly captures Core RunSnapshot; UI view remains Presentation. | No | `Rune.Contracts`, `IRuneModifier`, `IRuneProfileRepository`, `IRuneRunSnapshot` | Runes | High; Day 3 gate and persistence compatibility. |
| Meta | No dedicated runtime module; Rune progression and Item interfaces are the only partial implementation | Recruitment, Core, Items, Runes | Energy, Gold, Rank, Unlock and server authority are absent or represented only by interfaces/docs. | No implementation to move | `IMetaAccount`, `IDayKeyProvider`, `IEnergyLedger`, `IRankService` | Meta/Backend | High; do not fake client authority. |
| Ads/Server contracts | `IItemServerLedger`, analytics payloads, Android builder; no backend implementation | Items, Bootstrap, Editor | UI mock can display offers without authoritative server path; Firebase/AdMob files are outside this audit and untracked. | Contracts only | `IAdRewardVerifier`, `IMerchantService`, `IAccountLedger` | Backend/Monetization | High; security and release identity. |
| UI/Presentation | `Presentation/*`, `HandoffUi/*`, 14 Prefabs | Core, Grid, Combat, Recruitment, Items, Runes; HandoffUi is separate | `DragonBoundScreenView` initializes gameplay services and Bootstrap owns its lifecycle. | Handoff preview yes; in-run UI no | `IPresentationSnapshot`, command ports, read-only view models | UI/Presentation | High for in-run UI; medium for Handoff preview. |
| Analytics/QA | `AnalyticsEventSchemaV2`, Editor diagnostic batches, 57 tests | Runtime and all gameplay domains | Analytics schema is Runtime; QA tests compile against entire Runtime and some diagnostics run production paths. | No | `Telemetry.Contracts`, test fixtures per module, diagnostics category boundary | QA/Analytics | Medium; broad compile coupling and long-running tests. |
| Integration/Bootstrap | `DragonBoundBootstrap`, Editor builders, Scenes | Every gameplay/UI module | Bootstrap is the composition root but also owns defaults, persistence fallback, AI tick, diagnostics and UI wiring. | No | `IntegrationCompositionRoot`, explicit adapters/config object | Integration Owner | Critical; only owner allowed to edit main Scenes/Prefabs. |

## Concrete dependency cycles

The current namespace graph is not a DAG:

- `Core -> Combat -> Core`: `TwentyWavePressureRuntime` consumes combat results; `HeroCombatState` consumes `EnemyRuntime` and Core team/run types.
- `Core -> Grid -> Core`: `BattlefieldLayoutDefinition` and `BoardGrid` use `TeamSide`/Core runtime state; Core constructs boards.
- `Core -> Recruitment -> Core`: Core creates recruitment services; recruitment consumes `TeamState`, `RunSnapshot` and Core events.
- `Core -> Runes -> Core`: Core constructs Rune runtime; Rune persistence and combat use Core `RunSnapshot`, `TeamState`, and enemy registry.
- `Recruitment -> Combat -> Recruitment`: Recruitment creates Hero combat states; Hero combat reads Hero/PairLink definitions.
- `Presentation -> Core/Grid/Recruitment/Items/Runes`: `DragonBoundScreenView` and `GreyboxHudView` are not passive views; they initialize and invoke gameplay services.
- `Bootstrap -> every domain`: expected for a composition root, but currently also contains gameplay policy and test/debug entry points.

## Asset and scene ownership

| Asset | Current role | Owner now | Safe handoff |
| --- | --- | --- | --- |
| `Assets/DragonBound/Scenes/Greybox_Main.unity` | Production greybox composition root, Player/AI battlefield and UI | Integration Owner | No; only after composition-root adapters and PlayMode gate. |
| `Assets/DragonBound/Scenes/HeroSlice_Main.unity` | Hero showcase with development economy and debug setup | Integration + Heroes | No; Hero/Recruitment owners may change scripts, not scene wiring. |
| `Assets/DragonBound/Scenes/UI_Handoff.unity` | Preview-only merchant/item handoff | UI/Handoff owner | Yes after HandoffUi tests; scene remains Integration-owned for build inclusion. |
| `Assets/Scenes/SampleScene.unity` | Unity template/default scene, not in build settings | Integration Owner | Do not use as product entry without explicit decision. |
| `UI/Prefabs/Components/*` | Board cells, unit cards, hero formation, bench slots | UI/Presentation | Yes with Presentation tests; GUIDs must remain. |
| `UI/Prefabs/Modules/Battlefield.prefab` | Board/lane/combat FX composition | UI/Presentation + Integration | Conditional; runtime binding remains Integration-owned. |
| `UI/Prefabs/Modules/HUD.prefab`, `Recruitment.prefab`, `HeroWorkshop.prefab`, `RuneLoadout.prefab` | Runtime controls and read models | UI/Presentation with domain owners | Conditional; commands must be ports, not direct services. |
| `UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` | Full in-run screen composition | Integration Owner | No until screen adapter boundary exists. |
| `UI/Handoff/Prefabs/*` | Isolated preview UI | HandoffUi owner | Yes; preserve prefab GUIDs and test scene. |
| `ProjectSettings/EditorBuildSettings.asset` | Build scene list | Integration Owner | No; any scene list change is release coordination. |

## Answers to handoff questions

1. `DragonBound.Runtime` is a single monolithic runtime assembly. The real cycles are the Core/Combat/Grid/Recruitment/Runes cycles above; folder names do not constrain them.
2. `GameShared.Runtime` and the HandoffUi preview contracts/views are closest to independent. Items have explicit contracts but still depend on Core concrete types. Runes, AI, Bosses and in-run UI remain Bootstrap/Core-bound.
3. UI_Handoff can have an independent owner now. Items can have an owner for catalog/effect work behind existing contracts. Runes and AI require contract extraction first. Bosses cannot be independently handed off until spawn/cast/settlement interfaces exist.
4. `Greybox_Main`, `HeroSlice_Main`, `DragonBoundPortraitScreen.prefab`, `Battlefield.prefab`, and `EditorBuildSettings.asset` are Integration Owner assets. Feature owners submit adapter/script changes; they do not directly rewire these assets.
5. Unity `.meta` files and GUIDs must move together. Use `git mv` for source + `.meta`, never recreate assets, and verify `guid` and all scene/prefab `m_Script` references before and after each move.
6. Existing `.gitignore` excludes Unity caches, Logs and local settings but has no LFS policy, CODEOWNERS, or PR checks. The GitHub additions are specified in `GitHubHandoffChecklistV1.md`; they are not configured in this phase.

## Maximum risks

1. Bootstrap is both composition root and policy owner, so moving any domain without first extracting ports can change default runtime behavior.
2. Hero/XP/Skill truth is split across Combat, Recruitment and Core, making duplicate configuration likely during extraction.
3. Scene/Prefab bindings and serialized GUIDs are the release surface; an asmdef split can compile while silently breaking authored references or PlayMode initialization.
