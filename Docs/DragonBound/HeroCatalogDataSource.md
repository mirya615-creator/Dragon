# DragonBound Hero Catalog Data Source

The formal source is `FrozenHeroConfigurationCatalog`. `HeroComponentCatalog`,
`HeroDefinitionCatalog`, and `HeroRecipeCatalog` are read-only facades over it.
The workshop, gallery, HeroSlice adapter, and fixed-direction matcher must not
maintain their own component or recipe lists.

## Components

| Canonical ID | Display name | Copies | Role | Art slot |
| --- | --- | ---: | --- | --- |
| CMP_CONTRACT_HATCHLING | 契约幼龙 | 3 | DragonCore | ART_Component_CMP_CONTRACT_HATCHLING |
| CMP_RUNE_STAFF | 符文法杖 | 3 | Focus | ART_Component_CMP_RUNE_STAFF |
| CMP_ANCESTRAL_WAR_CROWN | 先祖战冠 | 3 | Crown | ART_Component_CMP_ANCESTRAL_WAR_CROWN |
| CMP_SKY_RANGER | 天空游侠 | 1 | Person | ART_Component_CMP_SKY_RANGER |
| CMP_FLAME_SHAMAN | 火焰萨满 | 1 | Person | ART_Component_CMP_FLAME_SHAMAN |
| CMP_DRAGON_KNIGHT | 龙骑士 | 1 | Person | ART_Component_CMP_DRAGON_KNIGHT |
| CMP_RUNE_APPRENTICE | 符文学徒 | 1 | Person | ART_Component_CMP_RUNE_APPRENTICE |
| CMP_STONE_SCHOLAR | 石像学者 | 1 | Person | ART_Component_CMP_STONE_SCHOLAR |
| CMP_ASTRAL_MAGE | 星界术师 | 1 | Person | ART_Component_CMP_ASTRAL_MAGE |
| CMP_WANDERING_SWORDSMAN | 流浪剑士 | 1 | Person | ART_Component_CMP_WANDERING_SWORDSMAN |
| CMP_NORTHLAND_SCOUT | 北境斥候 | 1 | Person | ART_Component_CMP_NORTHLAND_SCOUT |
| CMP_STORM_WARRIOR | 风暴勇士 | 1 | Person | ART_Component_CMP_STORM_WARRIOR |
| CMP_SHADOW_WALKER | 暗影行者 | 1 | Person | ART_Component_CMP_SHADOW_WALKER |
| CMP_RUNE_DAGGER | 符文匕首 | 1 | Weapon | ART_Component_CMP_RUNE_DAGGER |
| CMP_DEEPSEA_HARPOONER | 深海鱼叉手 | 1 | Person | ART_Component_CMP_DEEPSEA_HARPOONER |
| CMP_ANCIENT_HARPOON | 远古鱼叉 | 1 | Weapon | ART_Component_CMP_ANCIENT_HARPOON |
| CMP_VALKYRIE_ACOLYTE | 女武神侍从 | 1 | Person | ART_Component_CMP_VALKYRIE_ACOLYTE |
| CMP_DRAGONBONE_LONGBOW | 龙骨长弓 | 1 | Weapon | ART_Component_CMP_DRAGONBONE_LONGBOW |

The run manifest is 24 instances: the first three public cores have three
copies each; every other component has one. Existing `CORE_*` and `PART_*`
references resolve through `DragonBoundLegacyAliases` and never become runtime
display names.

## Heroes And Recipes

| Recipe ID | Hero ID | Hero name state | Direction | Progress owner |
| --- | --- | --- | --- | --- |
| RECIPE_WINDCLAW_RANGER | HERO_WINDCLAW_RANGER | 风爪游侠, Frozen | 天空游侠上 / 契约幼龙下 | CMP_SKY_RANGER |
| RECIPE_EMBER_SHAMAN | HERO_EMBER_SHAMAN | 余烬萨满, Frozen | 火焰萨满上 / 契约幼龙下 | CMP_FLAME_SHAMAN |
| RECIPE_DRAGON_RIDER | HERO_DRAGON_RIDER | 烈焰龙骑, Frozen | 龙骑士上 / 契约幼龙下 | CMP_DRAGON_KNIGHT |
| RECIPE_RUNEBOLT_MAGE | HERO_RUNEBOLT_MAGE | 符文雷矢法师, Frozen | 符文法杖左 / 符文学徒右 | CMP_RUNE_APPRENTICE |
| RECIPE_STONEBINDER | HERO_STONEBINDER | 岩缚术士, Frozen | 符文法杖左 / 石像学者右 | CMP_STONE_SCHOLAR |
| RECIPE_STARFALL_ARCHMAGE | HERO_STARFALL_ARCHMAGE | 星陨大法师, Frozen | 符文法杖左 / 星界术师右 | CMP_ASTRAL_MAGE |
| RECIPE_CROWN_SWORD_LEADER | HERO_CROWN_SWORD_LEADER | 名称待定, Pending | 先祖战冠上 / 流浪剑士下 | CMP_WANDERING_SWORDSMAN |
| RECIPE_CROWN_HUNTER_LEADER | HERO_CROWN_HUNTER_LEADER | 名称待定, Pending | 先祖战冠上 / 北境斥候下 | CMP_NORTHLAND_SCOUT |
| RECIPE_THUNDER_JARL | HERO_THUNDER_JARL | 雷霆领主, Frozen | 先祖战冠上 / 风暴勇士下 | CMP_STORM_WARRIOR |
| RECIPE_NIGHTFANG_ASSASSIN | HERO_NIGHTFANG_ASSASSIN | 夜牙刺客, Frozen | 符文匕首左 / 暗影行者右 | CMP_SHADOW_WALKER |
| RECIPE_LEVIATHAN_HUNTER | HERO_LEVIATHAN_HUNTER | 海兽猎手, Frozen | 远古鱼叉左 / 深海鱼叉手右 | CMP_DEEPSEA_HARPOONER |
| RECIPE_SKYHUNTER_VALKYRIE | HERO_SKYHUNTER_VALKYRIE | 天穹女武神, Frozen | 龙骨长弓左 / 女武神侍从右 | CMP_VALKYRIE_ACOLYTE |

Six entries are Purple and six are Gold. Only `HERO_WINDCLAW_RANGER` and
`HERO_DRAGON_RIDER` are currently `Implemented`; all other entries remain
visible in the gallery but are `NotImplemented` and cannot receive fallback
combat behavior.

## PairLink And Presentation

`HeroPairLink` is temporary and stores both `RecipeId` and `HeroId`. It owns no
cells and consumes no components. Component runtime IDs, cells, input areas,
and lifecycle remain independent. Progress is keyed by the person component
runtime ID plus `RecipeId`, so changing a public core preserves progress while
changing to another recipe does not share it.

The available art replacement points are `HeroComponentDefinition.ArtSlotId`
and `HeroCatalogMetadata.ArtSlotId`. Replacing those presentation nodes must
not change board cells, runtime IDs, recipe IDs, PairLink matching, or combat
anchors.
