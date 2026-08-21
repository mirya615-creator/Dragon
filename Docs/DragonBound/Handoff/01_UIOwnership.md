# UI Ownership

| Area | Primary role | Approval / integration boundary |
|---|---|---|
| UI states, layout, Prefabs, responsive behavior | Unity frontend lead | Owns `Assets/DragonBound/UI/Handoff` and `DragonBound.HandoffUi`; integrates only snapshot adapters after gameplay review. |
| Rules, unlock eligibility, merchant availability | Gameplay lead | Produces immutable snapshots and consumes commands; retains all mutation of Item, Gold, Energy, Ledger, ads, and match state. |
| Sprites, materials, TMP font assets, animation, VFX, SFX | Art lead | Assigns assets to serialized Inspector hooks; no code change is needed for normal replacements. |
| Scope, release acceptance, cross-discipline conflicts | Project lead | Approves state matrix and live integration timing. |

The preview scene and the handoff Prefabs are frontend-owned. Existing `Greybox_Main`, `HeroSlice_Main`, all existing main Prefabs, and Bootstrap are out of scope for this phase.
