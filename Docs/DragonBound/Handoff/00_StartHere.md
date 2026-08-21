# Drakeforge UI Handoff

This handoff introduces an isolated `UI_Handoff` preview for the first production-facing UI slice: Item HUD and a three-choice Merchant. It is a prefab-led presentation foundation only. No item effects, Gold, advertising, Ledger, shop cadence, Energy, Rank, or Rune behavior is included.

Open `Assets/DragonBound/Scenes/UI_Handoff.unity` to inspect the mock state. The scene is intentionally independent of the server, match bootstrap, and a real battle. Select `DragonBound/Handoff/Create UI Handoff Assets` only in an empty checkout; it refuses to rebuild or overwrite the handoff assets once they exist.

Read next: `01_UIOwnership.md`, then `02_UIStateMatrix.md` and `04_DataBindingContracts.md` before integrating a live source.

For a new repository checkout, read `06_FirstCloneAndRun.md` before opening or rebuilding any
handoff asset.

Validated previews:

- `Previews/UI_Handoff_1080x1920.png` (phone portrait)
- `Previews/UI_Handoff_2048x1536.png` (tablet with centered 9:16 fixed-format content)

## Audit Boundary

Existing code-created or greybox UI retained as-is:

- `DragonBoundPortraitUiBuilder` owns the current `DragonBoundPortraitScreen`, `Greybox_Main`, and associated modules. It has explicit rebuild commands and remains a debug/greybox path.
- `RuneLoadoutView`, `HeroWorkshopView`, recruitment and battlefield views dynamically create their existing greybox entries. They remain pending future prefab conversion.
- `DragonBoundScreenView.CreateRangeDismissSurface` creates a transparent debug interaction surface at runtime.

Formal UI pending prefab conversion is limited to those existing greybox modules. This phase does not refactor them or run any existing rebuild command.
