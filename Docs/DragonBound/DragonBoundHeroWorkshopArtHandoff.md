# DragonBound Hero Workshop Art Handoff

The workshop uses a fixed centered modal. It remains anchored to 60 percent of
the portrait screen height and must not cover the board outside its panel.

## Replaceable art slots

| Node | Purpose | Art constraint |
| --- | --- | --- |
| `ART_WorkshopPanel` | Outer modal frame | Keep its anchors and input hierarchy. |
| `ART_WorkshopBookPage` | Central book-page background | Replace the greybox color or sprite only. |
| `ART_ComponentsTabIcon` | Component-library tab icon | Square icon slot. |
| `ART_GalleryTabIcon` | Hero-gallery tab icon | Square icon slot. |
| `ART_ComponentLibraryPage` | Component page paper | Must stay behind the component grid. |
| `ART_HeroGalleryPage` | Gallery page paper | Must stay behind hero cards and detail strip. |
| `ART_ComponentIcon` | Component square-card art | Does not change runtime bag counts. |
| `ART_HeroPortrait` | Left recipe square | First component/weapon side of a hero recipe card. |
| `ART_HeroRecipePartner` | Right recipe square | Character side of a hero recipe card. |
| `ART_HeroDetailPortrait` | Selected hero detail icon | Decorative small icon only. |
| `ART_HeroDetailInfo` | Selected hero detail paper | Decorative backing only. |

## Layout invariants

- Component library: four square cards per row, five rows for eighteen entries.
- Hero gallery: three recipe cards per row, four rows for twelve heroes.
- Every hero recipe card contains two square art regions and one name region.
- `WorkshopBagStatsLabel` appears only on the component page.
- `ComponentState` and `HeroState` are retained for runtime binding but hidden in
  the greybox layout. Do not enable them as permanent card text.
- Do not rename or move `HeroWorkshopView`, its tab buttons, grids, templates,
  input targets, or their anchors when replacing art.
