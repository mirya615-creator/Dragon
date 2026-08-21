# Rune V1 Product Closure - Verified Baseline

Date: 2026-08-14  
Status: verified product closure for the local-profile/UI loop. Rune content, values, R1
Production HP, Item, Boss, Energy/ads, and AI fairness remain frozen.

## Scope and Day 3 Authority

`Drakeforge_Game_Design_Spec_2026-08-14.md` states only that the Rune System opens on
Day 3. It does not define an on-device clock or an account-day calculation. The runtime
therefore reads `AccountDay` through `IRuneProgressionProvider`; the local profile keeps a
last trusted value for offline display and does **not** derive progression from device time.
A future account/backend implementation replaces this provider.

`RuneFeatureGate` is the single product gate:

- Before Day 3, the Loadout entry opens a locked view and all entry/filter/action controls are
  non-interactable.
- `RuneLoadoutService` rejects equipment, removal and crafting; `RuneRunRewardService`
  rejects wave drops. These service boundaries cover button, deep-link and runtime product
  calls with `RuneSystemLockedUntilDay3`.
- At Run start, Day 1 creates and seals an empty snapshot. At Day 3+, a validated snapshot is
  sealed. No route bypasses the gate through the Bootstrap flow.

## Durable Profile Schema

`RuneSaveData.CurrentSchemaVersion = 2`; content version is `RuneContent.V1`.

Persisted fields:

- `AccountDay`
- `InventoryEntries`: RuneId, configured rarity, OwnedCount, FragmentCount
- `LoadoutAssignments`: HeroId -> RuneId

Runtime-only dictionaries and `RuneLoadoutSnapshot` are deliberately excluded. The snapshot is
created at Run start from the validated loadout and passed to `BoardRecruitDestination`; it is
then sealed. An out-of-run profile reload restores editable inventory/loadout only, never a
previous Run snapshot.

`LocalRuneProfileRepository` writes JSON to `Application.persistentDataPath` as
`dragonbound-runes-v1.json`. It writes a temporary file and uses `File.Replace` with a `.bak`
backup; the fallback first copies the previous primary to `.bak`, then replaces the primary.
Loading accepts schema 0/1 migration to schema 2, rejects unsupported schema/content versions,
validates all inventory and HeroId/RuneId references, recovers from a valid backup, or returns a
new `CorruptFallback` profile without overwriting corrupt source data. No cloud save is claimed:
`IRuneProfileRepository` is the backend replacement seam.

## Product Data Flow

1. Bootstrap loads the repository, constructs `RuneFeatureGate`, `RuneLoadoutService` and the
   Player-only `RuneRunRewardService`.
2. Wave completion calls the existing Rune reward runtime. Day 3 gate, deterministic drop rules,
   inventory grant and profile save occur through this route.
3. The greybox Loadout exposes 12 Hero entries and 14 Rune entries. It shows rarity, OwnedCount,
   FragmentCount and equipped hero; it supports hero selection, rarity filtering, equip/replace,
   unequip and craft, including displayed validation failures.
4. Each hero has at most one assignment; `AssignedCopies(RuneId) <= OwnedCount(RuneId)` remains
   enforced. A HeroId can create multiple combat runtimes without consuming extra owned copies.
5. At Run start the immutable HeroId -> RuneId snapshot is passed to formation/combat. Further
   profile/UI changes cannot alter that Run.

## Art Contract

The greybox UI intentionally consumes `RunePresentationData` resource keys such as `ArtAssetKey`,
frame and rarity-theme keys. Prefab nodes use `ART_` names and are placeholders only. Final icons,
frames, backgrounds, localized display text and layout polish are still art/UI production work;
the presentation contract is ready for those assets.

## Files Changed for Closure

- `Assets/DragonBound/Runtime/Runes/RunePersistence.cs`
- `Assets/DragonBound/Runtime/Runes/RuneProfileOperations.cs`
- `Assets/DragonBound/Runtime/Runes/RuneInventory.cs`
- `Assets/DragonBound/Runtime/Runes/HeroRuneLoadout.cs`
- `Assets/DragonBound/Runtime/Runes/RuneDrops.cs`
- `Assets/DragonBound/Runtime/Bootstrap/DragonBoundBootstrap.cs`
- `Assets/DragonBound/Runtime/Presentation/DragonBoundScreenView.cs`
- `Assets/DragonBound/Runtime/Presentation/GreyboxRecruitmentPanel.cs`
- `Assets/DragonBound/Runtime/Presentation/RuneLoadoutView.cs`
- `Assets/DragonBound/Runtime/Presentation/RuneLoadoutEntryView.cs`
- `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs`
- `Assets/DragonBound/UI/Prefabs/Modules/RuneLoadout.prefab`
- the paired scene/screen/recruitment prefab wiring and Rune closure EditMode/PlayMode tests.

## Verification

All results below are read from final NUnit XML, not console summaries.

| Verification | Result | Duration | XML/log |
| --- | --- | ---: | --- |
| Unity compile | passed (exit 0) | 20s | `Logs/RuneV1Closure-Compile-02.log` |
| Profile persistence/migration/gate EditMode | 5/5 | 0.155s | `Logs/RuneV1Closure-ProfileEditMode.xml` |
| Loadout prefab/art-contract EditMode | 1/1 | 0.138s | `Logs/RuneV1Closure-PrefabEditMode-02.xml` |
| Loadout Bootstrap/restart/snapshot PlayMode | 1/1 | 3.882s | `Logs/RuneV1Closure-LoadoutPlayMode.xml` |
| Rune Architecture Alpha EditMode | 9/9 | 0.110s | `Logs/RuneV1Closure-AlphaEditMode.xml` |
| Rune effects EditMode | 12/12 | 0.165s | `Logs/RuneV1Closure-EffectsEditMode.xml` |
| Fast UI EditMode | 16/16 | 0.293s | `Logs/RuneV1Closure-FastUiEditMode-02.xml` |
| Full PlayMode | 27/27 | 24.682s | `Logs/RuneV1Closure-FullPlayMode-02.xml` |
| Final full EditMode, including diagnostics | 416/416 | 1994.146s | `Logs/RuneV1Closure-FullEditMode.xml` |

Final artifact SHA-256 prefixes:

- Full EditMode XML: `8C59AE0B610ACAEA`
- Full PlayMode XML: `2D542F11AF8D6E6A`
- Compile log: `28FADFC243880C04`

The full EditMode run includes the existing 1000-Seed board/bench audit and completed it before
producing the final XML. No test run was terminated by a short timeout.

## Warnings and Deferred Work

- Unity emitted startup LicenseClient handshake/access-token warnings, then resolved entitlement;
  final compile exited 0 and all test XML results passed.
- Cloud/account persistence is intentionally deferred. `IRuneProfileRepository` and
  `IRuneProgressionProvider` are the future backend seams.
- A production source for trusted `AccountDay`, final art/localization, analytics event emission,
  and final UX polish remain outside this task.

## Baseline and Rollback

Baseline Git HEAD: `3f7b7adfe233a345cb6792cc64bed06eec136cfe`.

The worktree contains 129 pre-existing mixed changes, including the frozen Rune Architecture Alpha
and R1 work. No commit was created and no generic `git restore`, `git clean`, or reset is safe in
this worktree. Recovery of a damaged local Rune save is automatic through `.bak`; a corrupt
primary is rejected and a valid backup is loaded.

For source rollback, create an isolated worktree from the preserved Alpha baseline/artifact,
restore only the files in **Files Changed for Closure**, then reverse the corresponding Rune
Bootstrap/UI wiring. Do not restore entire shared Bootstrap, scene or prefab files in this dirty
worktree, because that would remove unrelated R1/core-loop changes. This baseline document and
the final XML/log artifacts are the verification record for that scoped rollback.
