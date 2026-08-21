# GitHub Handoff Checklist V1

This is a repository policy proposal only. No remote, branch protection, CODEOWNERS file, LFS configuration, or PR rule was changed in this phase.

## Repository hygiene

- Keep the existing Unity `.gitignore` rules for `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/` and local IDE files.
- Add an explicit policy for generated diagnostic CSV/JSON/XML under `Logs/`; commit only deliberately reviewed fixtures under `Docs/Fixtures/`.
- Keep `Assets/google-services.json` and machine-local SDK credentials out of commits unless the release owner explicitly adds an encrypted/approved artifact path.
- Do not use `git clean`, force pushes, history rewrites, or mixed feature commits during handoff.

## Git LFS decision

Use Git LFS only for repository-owned large binary art/audio/video assets after an inventory. The current checked-in PNG range art is small and does not justify automatic LFS conversion. If future assets exceed the repository threshold, add explicit patterns such as `*.psd`, `*.wav`, `*.mp4`, and source art files, then migrate only in a dedicated reviewed commit. Never LFS-track Unity `.meta`, `.asmdef`, `.unity`, `.prefab`, JSON contracts, or test fixtures.

## CODEOWNERS proposal

The following ownership paths should be added after module asmdefs land:

```text
/Assets/GameShared/                         @foundation-owner
/Assets/DragonBound/Runtime/Core/           @match-owner
/Assets/DragonBound/Runtime/Grid/           @board-owner
/Assets/DragonBound/Runtime/Recruitment/    @recruitment-owner
/Assets/DragonBound/Runtime/Combat/         @combat-owner
/Assets/DragonBound/Runtime/Runes/          @rune-owner
/Assets/DragonBound/Runtime/Items/          @item-owner
/Assets/DragonBound/Runtime/AI/             @ai-owner
/Assets/DragonBound/Runtime/Presentation/   @ui-owner
/Assets/DragonBound/Runtime/HandoffUi/      @ui-owner
/Assets/DragonBound/Runtime/Bootstrap/      @integration-owner
/Assets/DragonBound/Scenes/                 @integration-owner
/Assets/DragonBound/UI/Prefabs/             @integration-owner @ui-owner
/ProjectSettings/                           @release-owner @integration-owner
/Assets/DragonBound/Tests/                  @qa-owner
/Docs/Handoff/                              @architecture-owner
```

Until the split, `DragonBound.Runtime`, all production Scenes/Prefabs and `DragonBound.Editor` require Integration Owner review because the current assembly does not enforce these boundaries.

## Branch and PR policy

- Protected `main`; no direct pushes; no force push or rebase of shared branches.
- Feature branches use `module/<owner>/<short-change>` and contain one migration or feature boundary.
- Required checks: Unity compile, `Targeted`, Fast EditMode excluding Diagnostics/LongRunning, affected PlayMode, `git diff --check`, asmdef dependency validation, and GUID/reference validation for asset moves.
- Full EditMode is required before release baselines and after asmdef/Scene/Prefab migrations that affect shared runtime; it is not required for docs-only changes.
- PR description must list changed module, public interfaces, serialized asset paths, Production-value impact, tests/XML paths, rollback commit, and whether any pending design value changed.
- CODEOWNERS approval is required for `ProjectSettings`, build scenes, main Prefabs, Bootstrap, match/settlement, and any HP/XP/skill configuration.
- Generated logs, local credentials, and `Assets/google-services.json` are never accepted as incidental PR files.

## Handoff acceptance

1. The target DAG compiles with no upward or back-edge references.
2. Each moved Unity source retains its `.meta` GUID and all Scene/Prefab script references resolve.
3. Module test assemblies reference contracts plus their implementation, not the monolithic Runtime.
4. Integration PlayMode passes for `Greybox_Main`, `HeroSlice_Main`, and `UI_Handoff`.
5. Current frozen behavior remains explicit: W6 shared fixed HP `600`, Last-Hit XP, R1 HP curve, Rune Alpha/Product Closure status, and Item 2 implemented + 18 pending.
