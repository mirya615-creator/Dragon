# Module Migration Sequence V1

The sequence below is the only recommended path. Each commit is independently revertible and must keep the default scenes and Production values unchanged.

## Rules for every commit

- Move source and `.meta` together with `git mv`; never recreate a Unity asset.
- Run `git diff --check`, Unity compile, targeted tests for the moved module, Fast EditMode, and affected PlayMode before merge.
- Do not move a serialized Scene/Prefab until the target asmdef compiles and GUID references are checked.
- Long-running Diagnostics remain excluded from the fast gate and run only in the existing Full Gate.
- No commit may include `Assets/google-services.json` or its `.meta`.

## Ordered commits

| Commit | Change | Verification | Parallelism |
| --- | --- | --- | --- |
| 0 | This audit and ownership docs only. | `git diff --check`; Markdown/CSV review. | Complete. |
| 1 | Add `Foundation.Contracts` asmdef and move only Unity-free contracts: seed abstraction, clock/event interfaces, immutable IDs, side/wave/settlement contracts. No call-site changes yet; keep compatibility types in Runtime. | Compile; Foundation contract tests. | Must be first. |
| 2 | Add `Board.Contracts` and `Enemies.Contracts`; extract `GridPosition`, side transform/value objects, enemy/path/goal interfaces. Keep adapters in existing Runtime. | Board, targeting and enemy targeted tests; PlayMode smoke. | Serial after 1. |
| 3 | Add `Combat.Contracts` and `Heroes.Contracts`; extract `ICombatTarget`, damage result/event ports, Hero identity/progression/skill/Xp contracts. Keep `HeroCombatState` implementation in Runtime. | Hero XP, targeting, skill scaling and Basic tests. | Serial after 2; Combat and Heroes can be developed in parallel only after contracts land. |
| 4 | Add `Recruitment.Runtime` using Board/Hero contracts. **Slice 4A complete:** pure recruitment config and Forge Pick state moved to `DragonBound.Recruitment.Runtime`; deck, finite bag, recruitment transaction and PairLink remain in the monolith until ports land. | Slice 4A: static/GUID checks. Full step: Recruitment, merge, finite bag, Hero recipe tests; HeroSlice PlayMode. | Serial after 3. |
| 5 | Move `Combat.Runtime` and `Enemies.Runtime` behind the new contracts. Replace direct Core references in HeroCombatState/EnemyRuntime with ports. | Combat/Enemy targeted; full PlayMode. | Serial; do not parallelize with Match. |
| 6 | Add `Items.Runtime` and `Runes.Runtime` references to contracts only. **Item 6A complete:** pure Item definitions/profile moved to `DragonBound.Items.Runtime`; **Rune 6A complete:** pure Rune definitions, inventory, hero loadout, presentation and modifier input moved to `DragonBound.Runes.Runtime`; combat/economy effects, Rune combat/drops/persistence remain integrated. Move remaining implementations without changing 20-item status, Rune gate, persistence schema, or effects. | Item 6A: Item profile targeted `17/17`, Fast EditMode `572/572`, PlayMode `29/29`. Rune 6A: Rune targeted `21/21`, Fast EditMode `572/572`, PlayMode `29/29`. Full step: Item/Rune targeted, Fast EditMode, PlayMode loadout/snapshot. | Items and Runes can proceed in parallel after 5, but merge serially. |
| 7 | Add `AI.Runtime` as an adapter over Board/Recruitment/Combat/Match contracts. Move `BasicUnitAiController`; keep diagnostics in QA assembly. | AI symmetry and survival targeted; full PlayMode. | After 4 and 5. |
| 8 | Add `Match.Runtime`; move TwentyWave, PressureRaceSide, MatchSettlement, TeamState and WaveSystem. Inject recruitment/combat/items/runes/AI instead of constructing them. | W1-W20, settlement, W6 Soulchain, HP 600 regression; full PlayMode. | Serial and highest gameplay risk. |
| 9 | Add `Integration.Runtime`; reduce Bootstrap to composition only. Move Editor builders/diagnostics to owning Editor assemblies. | Bootstrap PlayMode, scene/prefab binding tests, Android editor compile. | After all runtime modules. |
| 10 | Add `Presentation.Runtime` and connect read-only snapshots/command ports. Keep `HandoffUi` isolated and connect through an adapter. | UI EditMode/PlayMode, portrait prefab tests, visual capture. | After Match and Integration. |
| 11 | Split EditMode tests by module and keep one Integration test assembly; split PlayMode into Integration-only scene tests. | Fast Gate plus affected PlayMode; Full Gate at release checkpoints. | Final serial cleanup. |
| 12 | Add optional `Meta.Contracts`, `Analytics.Contracts`, CODEOWNERS and PR checks. Do not implement server authority in this migration. | Schema tests, contract compile, repository checks. | After runtime DAG is stable. |

## Safe parallel work

After commits 1-3, feature owners may prepare Item, Rune, AI, Combat and UI adapter changes on separate branches, but only against published contract assemblies. Scene/Prefab, Bootstrap, Match and ProjectSettings changes remain serialized under Integration Owner review.

## Rollback

Revert the latest migration commit only after its direct dependents are reverted. Never use `git reset`, `git clean`, or GUID regeneration. A failed move is recovered by restoring the paired source and `.meta` paths from the same commit and rerunning the affected test lane.
