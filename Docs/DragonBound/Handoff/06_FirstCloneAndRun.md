# First Clone And Run Acceptance

This checklist decides whether a frontend developer can receive the project from Git alone.

## Clean checkout gate

1. Clone into an empty directory; do not reuse another developer's `Library`.
2. Confirm `ProjectSettings/ProjectVersion.txt` reports Unity `2022.3.62f3c1`.
3. Open the project in the Unity Editor GUI and allow Package Manager and the Asset Database to
   finish before using `-batchmode`. On the current local Unity `2022.3.62f3c1` China build,
   a brand-new `Library` can make a first batch-only launch fail with `The "path" argument must
   be of type string. Received undefined`; this is a local UPM initialization issue and is not
   fixed by committing `Library`. If it occurs, close the failed editor, reopen the checkout in
   the GUI, wait for package resolution to finish, then rerun the automated gate.
4. Confirm the Console has no compile, missing script, missing GUID or package resolution errors.
5. Open `Greybox_Main`, enter Play Mode and complete the startup/recruitment flow.
6. Open `HeroSlice_Main` and verify its focused component flow.
7. Open `UI_Handoff` manually and verify phone/tablet layout previews; this scene is not a live
   gameplay or build scene.

## Automated gate

- Target the directly changed module first.
- Run Fast EditMode.
- Run complete PlayMode.
- Run Full EditMode only at the release/architecture checkpoints defined in `TestLanes.md`.
- Run `git diff --check` and confirm the checkout remains clean after Unity closes.

## Asset gate

- Every Unity asset has its `.meta` file.
- Moved source and asset GUIDs match the pre-move values.
- `Greybox_Main`, `HeroSlice_Main`, Bootstrap and shared Prefabs changed only with Integration
  Owner review.
- No machine-local credentials, generated Logs or local package caches are tracked.

## Acceptance result

The handoff is accepted only when the fresh checkout, not the developer's original working copy,
passes compilation, Fast EditMode, PlayMode and the manual `Greybox_Main` smoke test.
