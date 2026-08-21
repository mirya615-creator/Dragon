# Drakeforge Full Configuration Baseline V2

Date: 2026-08-14

This is the complete external-sendable baseline, not a delta. It uses verified runtime values where implemented and frozen design rules elsewhere. Missing values are stated as PENDING rather than invented.

## Authority

1. `Drakeforge_Energy_Ads_Item_Design_Spec_2026-08-14.md` overrides older conflicting Energy, Merchant, Lottery, ad and Item-price rules.
2. Verified production code controls implemented combat, Recruit, Rune and R1 pressure values.
3. `Docs/R1ProductionVerifiedBaseline.md`, `Docs/RuneArchitectureAlphaVerified.md` and `Docs/RuneV1ProductClosureVerified.md` record verified state.
4. `Drakeforge_Game_Design_Spec_2026-08-14.md` controls frozen but unimplemented content.
5. `Drakeforge_配置表模板.md` is a legacy schema reference; conflicting values are obsolete.

| Domain | Stable ID / version | Owner | Authority |
| --- | --- | --- | --- |
| Pressure | `PressureRaceGreyboxV2` | Client simulation | `TwentyWavePressureConfiguration.CreateCoreLoopV2()` |
| Enemy | `EnemyArchetype`, base HP 30 | Client simulation | `EnemyRuntime.cs` |
| Recruit V3 | `DynamicComponentCatchupV3.diagnostic.1` | Client; future server Run validation | Recruit runtime/V3 policy |
| Forge Pick | `ITEM_SHOVEL`, `RecruitShovel.v2` | Client simulation | `ShovelRecruitment.cs` |
| Hero content | `CMP_*`, `RECIPE_*`, `HERO_*` | Client simulation | `FrozenHeroConfiguration.cs` |
| Rune content/profile | `RuneContent.V1`, schema 2 | Client local now; future Both | Rune runtime/persistence |
| Item/Energy/ads | Item Foundation IDs/schema/runtime seam verified; ad schema uses `content_version` | Both; server settlement | latest Energy/Ads/Item design |
| DayKey/Rank | IDs PENDING | Server authority | design specification |

Stable runtime IDs are ASCII and never derive from display names. Durable ownership, daily reset, rewarded-ad settlement and rank require server authority.

## Board and Basic Units

### Board Fact [IMPLEMENTED]

The current greybox has 24 deployable-or-locked positions per side: 6 open Battle cells and 18 Locked cells. A Forge Pick opens one selected own Locked cell for the Run. Player and AI are independent.

This conflicts with the earlier 40-cell-per-side design statement. It does not block R1/Rune verification, but blocks new capacity configuration that assumes 40 live cells until design and layout are reconciled.

### Basic Catalog [IMPLEMENTED]

Basics merge only when config ID and level match; levels run Lv1-Lv5. Basics cannot equip Runes and Basic last hits grant Hero XP zero.

| Config ID | Archetype / attack kind | Attack L1-L5 | Attack speed L1-L5 | Range |
| --- | --- | --- | --- | ---: |
| `basic.axe_raider` | Axe / `Single` | 3.00, 4.50, 6.30, 8.19, 10.24 | 1.25, 1.88, 2.62, 3.41, 4.27 | 1.5 |
| `basic.longbow_hunter` | Bow / `BowProjectile` | 2.00, 3.00, 4.20, 5.46, 6.82 | 1.25, 1.88, 2.62, 3.41, 4.27 | 3.5 |
| `basic.spear_raider` | Spear / `SpearPierce` | 2.00, 3.00, 4.20, 5.46, 6.82 | 1.38, 2.06, 2.89, 3.75, 4.69 | 2.5 |
| `basic.twinaxe_berserker` | Rider / `RiderSweep` | 2.00, 3.00, 4.20, 5.46, 6.82 | 1.25, 1.88, 2.62, 3.41, 4.27 | 2.0 |

## Components, Recruit and Forge Pick

### Finite Bag [IMPLEMENTED]

There are 18 component definitions and 24 runtime instances per side per Run. The three public cores have three copies; the other 15 components have one. Drawn instances never return. Camp overwrite permanently discards an unplaced Component. New Runs rebuild bags; Player and AI bags are independent and deterministic.

| Component ID | Name | Category | Copies | Compatible heroes |
| --- | --- | --- | ---: | --- |
| `CMP_CONTRACT_HATCHLING` | Contract Dragonling | PublicCore | 3 | Windclaw, Ember, Flame Drake Rider |
| `CMP_RUNE_STAFF` | Rune Staff | PublicCore | 3 | Runebolt, Stonebound, Starfall |
| `CMP_ANCESTRAL_WAR_CROWN` | Ancestral War Crown | PublicCore | 3 | Oathcrown, Frostcrown, Thunderlord |
| `CMP_SKY_RANGER` | Sky Ranger | PurplePartner | 1 | Windclaw |
| `CMP_FLAME_SHAMAN` | Flame Shaman | PurplePartner | 1 | Ember |
| `CMP_RUNE_APPRENTICE` | Rune Apprentice | PurplePartner | 1 | Runebolt |
| `CMP_STONE_SCHOLAR` | Stone Scholar | PurplePartner | 1 | Stonebound |
| `CMP_WANDERING_SWORDSMAN` | Wandering Sword | PurplePartner | 1 | Oathcrown |
| `CMP_NORTHLAND_SCOUT` | Northland Scout | PurplePartner | 1 | Frostcrown |
| `CMP_DRAGON_KNIGHT` | Dragon Knight | SharedRouteGoldPartner | 1 | Flame Drake Rider |
| `CMP_ASTRAL_MAGE` | Astral Arcanist | SharedRouteGoldPartner | 1 | Starfall |
| `CMP_STORM_WARRIOR` | Storm Warrior | SharedRouteGoldPartner | 1 | Thunderlord |
| `CMP_SHADOW_WALKER` | Shadow Walker | DedicatedGold | 1 | Nightfang |
| `CMP_RUNE_DAGGER` | Rune Dagger | DedicatedGold | 1 | Nightfang |
| `CMP_DEEPSEA_HARPOONER` | Deepsea Harpooner | DedicatedGold | 1 | Abyssal Harpooner |
| `CMP_ANCIENT_HARPOON` | Ancient Harpoon | DedicatedGold | 1 | Abyssal Harpooner |
| `CMP_VALKYRIE_ACOLYTE` | Valkyrie Attendant | DedicatedGold | 1 | Skyborne Valkyrie |
| `CMP_DRAGONBONE_LONGBOW` | Dragonbone Bow | DedicatedGold | 1 | Skyborne Valkyrie |

### Recruit V3 [IMPLEMENTED]

Every successful Recruit produces five results. Production V3 permits 0-3 Components and preserves at least one Basic. R1-R3 use Normal; from R4 onward tier selection compares delivered Components with a wave target. A Forge Pick never consumes a finite Component.

| Tier | 0 Component | 1 | 2 | 3 |
| --- | ---: | ---: | ---: | ---: |
| Normal | 50% | 20% | 20% | 10% |
| Light | 40% | 15% | 25% | 20% |
| Medium | 30% | 10% | 25% | 35% |
| Strong | 20% | 5% | 20% | 55% |
| Severe | 10% | 0% | 15% | 75% |

V3 delivered targets by W1-W13 are `0.8, 1.6, 2.4, 4.0, 6.0, 8.5, 11.0, 13.5, 16.0, 18.5, 21.0, 22.5, 24.0`; after W13 the target remains 24. Legacy V2 allows four Components, but production V3 maximum is three.

### Forge Pick [IMPLEMENTED]

| Field | Value |
| --- | --- |
| Result / random ID | `ITEM_SHOVEL`; `RecruitShovel` / `RecruitShovel.v2` |
| Cap / placement | At most one per successful Recruit. Roll first, reserve a non-Component slot, then fill remaining results. |
| Basic guarantee | One Basic remains; Pick never replaces or discards a finite Component. |
| Eligible | Successful Recruit with at least one own Locked cell. |
| Miss chances | Miss 0 = 20%; Miss 1 = 35%; Miss 2 = 50%; Miss 3+ = 100% hard pity. |
| Ineligible | No roll, no stream consumption and no pity advance. |
| Unlock | One selected own Locked cell. Invalid target/cancel consumes nothing. |
| Future seam | `GrantShovel(int count)` uses the same unlock path as Recruit rewards. |

## Twelve Heroes and PairLink [IMPLEMENTED]

PairLink is a temporary relation between two independent Component runtimes. Moving either Component breaks it immediately. Recipe progression retains earned XP and reforms through the same progression key.

| Hero ID | Display | Rarity | Recipe | Legal formation | Atk / AS / Range | Skill |
| --- | --- | --- | --- | --- | --- | --- |
| `HERO_WINDCLAW_RANGER` | Windclaw Ranger | Purple | `RECIPE_WINDCLAW_RANGER` | Vertical: Sky Ranger above Contract Dragonling | 14 / 1.80 / 3.25 | `SKILL_POWER_SHOT` |
| `HERO_EMBER_SHAMAN` | Ember Shaman | Purple | `RECIPE_EMBER_SHAMAN` | Vertical: Flame Shaman above Contract Dragonling | 8 / 1.70 / 3.00 | `SKILL_EXPLOSIVE_FIREBALL` |
| `HERO_RUNEBOLT_MAGE` | Runebolt Mage | Purple | `RECIPE_RUNEBOLT_MAGE` | Horizontal: Rune Staff left of Rune Apprentice | 8 / 1.75 / 3.00 | `SKILL_RUNEBOLT_MAGE` |
| `HERO_STONEBINDER` | Stonebound Warlock | Purple | `RECIPE_STONEBINDER` | Horizontal: Rune Staff left of Stone Scholar | 10 / 1.45 / 2.75 | `SKILL_STONE_BIND` |
| `HERO_CROWN_SWORD_LEADER` | Oathcrown Swordsman | Purple | `RECIPE_CROWN_SWORD_LEADER` | Vertical: War Crown above Wandering Sword | 18 / 1.50 / 1.75 | `SKILL_DUEL_MOMENTUM` |
| `HERO_CROWN_HUNTER_LEADER` | Frostcrown Hunter | Purple | `RECIPE_CROWN_HUNTER_LEADER` | Vertical: War Crown above Northland Scout | 16 / 1.45 / 3.25 | `SKILL_HUNT_MARK` |
| `HERO_DRAGON_RIDER` | Flame Drake Rider | Gold | `RECIPE_DRAGON_RIDER` | Vertical: Dragon Knight above Contract Dragonling | 13 / 1.70 / 3.00 | `SKILL_FLAME_DIVE` |
| `HERO_STARFALL_ARCHMAGE` | Starfall Archmage | Gold | `RECIPE_STARFALL_ARCHMAGE` | Horizontal: Rune Staff left of Astral Mage | 12 / 1.75 / 3.25 | `SKILL_STARFALL` |
| `HERO_THUNDER_JARL` | Thunderlord | Gold | `RECIPE_THUNDER_JARL` | Vertical: War Crown above Storm Warrior | 11 / 1.55 / 3.00 | `SKILL_THUNDER_DOMINION` |
| `HERO_NIGHTFANG_ASSASSIN` | Nightfang Assassin | Gold | `RECIPE_NIGHTFANG_ASSASSIN` | Horizontal: Rune Dagger left of Shadow Walker | 30 / 1.50 / 2.25 | `SKILL_NIGHTFANG_EXECUTION` |
| `HERO_LEVIATHAN_HUNTER` | Abyssal Harpooner | Gold | `RECIPE_LEVIATHAN_HUNTER` | Horizontal: Ancient Harpoon left of Deepsea Harpooner | 15 / 1.85 / 3.50 | `SKILL_ABYSS_HARPOON` |
| `HERO_SKYHUNTER_VALKYRIE` | Skyborne Valkyrie | Gold | `RECIPE_SKYHUNTER_VALKYRIE` | Horizontal: Dragonbone Bow left of Valkyrie Acolyte | 24 / 1.80 / 3.50 | `SKILL_SKY_HUNT` |

The frozen design names the Crown Sword entry Oathcrown Blademaster, while runtime says Oathcrown Swordsman. The stable ID is unchanged. This is non-blocking localization work before art lock.

### Hero SkillMultiplier V1 [IMPLEMENTED]

`SkillMultiplier` is deliberately scoped to the fields below. It does not implicitly alter a
Hero's normal attack, attack speed, cooldown, range, search range, line length, width, radius,
target count, execute threshold, displacement, or control rules.

| Hero ID | SkillMultiplierTarget | Code status |
| --- | --- | --- |
| `HERO_WINDCLAW_RANGER` | Fifth-hit power shot: `CurrentAttack x 1.80 x SkillMultiplier`; Elite then `x1.25`. | Aligned |
| `HERO_EMBER_SHAMAN` | Secondary splash only: `CurrentAttack x 0.75 x SkillMultiplier`; primary remains `CurrentAttack`. | Aligned |
| `HERO_RUNEBOLT_MAGE` | Pierce targets two and later: `CurrentAttack x SkillMultiplier`; first target remains `CurrentAttack`; cap stays 4/5/6. | Aligned |
| `HERO_STONEBINDER` | Stun duration: `1.20s x SkillMultiplier x EnemyStunMultiplier`; damage remains `CurrentAttack`. | Aligned |
| `HERO_CROWN_SWORD_LEADER` | Duel Momentum final bonus per stack: `0.08 x SkillMultiplier`; cap stays 5/6/7. | Aligned |
| `HERO_CROWN_HUNTER_LEADER` | Marked attack: `CurrentAttack x 1.25 x SkillMultiplier`; a mark persists until death or range exit before selecting highest current HP. | Aligned |
| `HERO_DRAGON_RIDER` | Dive: `CurrentAttack x 2.00 x SkillMultiplier`; fire tick: rune-modified BaseAttack `x0.25 x SkillMultiplier`. Dive remains ready without a legal target inside its 6-cell skill length. | Aligned |
| `HERO_STARFALL_ARCHMAGE` | Starfall impact: `CurrentAttack x 2.80 x SkillMultiplier`. | Aligned |
| `HERO_THUNDER_JARL` | Dominion stun: `0.90s x SkillMultiplier x EnemyStunMultiplier`; chain and Dominion damage remain fixed at their configured attack factors. | Aligned |
| `HERO_NIGHTFANG_ASSASSIN` | Shadow Execution's three segments only: `70%/70%/160% x SkillMultiplier`; normal strike and fixed execute rules remain unchanged. | Aligned |
| `HERO_LEVIATHAN_HUNTER` | Abyss Harpoon's six skill hits only: `150%/138%/126%/114%/102%/90% x SkillMultiplier`; normal harpoon and control remain unchanged. | Aligned |
| `HERO_SKYHUNTER_VALKYRIE` | Radiance primary: `CurrentAttack x SkillMultiplier`; two secondaries: `CurrentAttack x0.40 x SkillMultiplier`; normal attacks and stack rules remain unchanged. | Aligned |

### Hero XP and Levels [IMPLEMENTED]

| Rule | Behavior |
| --- | --- |
| Settlement | Full XP goes only to the Hero applying final formal combat damage. No shared, assist, proximity or damage-share XP. |
| Reward | Production Normal = 1. Boss design rewards are W6/W12/W16/W20 = 6/10/15/20, last-hit Hero only; W6/W12 Boss reward mapping is implemented, W16/W20 remain pending until their entities exist. |
| No XP | Basic last hit, leak, system destroy, debug cleanup, non-combat despawn and invalid owner award zero. |
| Attribution | AoE, chain, penetration, execute, DoT, delayed skill and ground hazard retain creating Hero ownership; each enemy settles independently. |
| Side/link | Player and AI never cross-award; PairLink break never splits or clears progression. |

| Rarity | Max | Total XP thresholds | Attack multiplier | Attack speed multiplier | Skill multiplier |
| --- | ---: | --- | --- | --- | --- |
| Purple | 3 | L1 0; L2 20; L3 60 | 1.00; 1.05; 1.10 | 1.00; 1.25; 1.56 | 1.00; 1.10; 1.25 |
| Gold | 5 | L1 0; L2 20; L3 55; L4 105; L5 175 | 1.00; 1.12; 1.25; 1.40; 1.57 | 1.00; 1.10; 1.21; 1.33; 1.46 | 1.00; 1.10; 1.25; 1.45; 1.70 |

Gold Lv5 requires 175 total XP. Purple has no Lv5 in verified code: cap is Lv3 / 60 XP. A Purple Lv5 requirement is PENDING design work and cannot be inferred from Gold.

## Twenty-Wave R1 [IMPLEMENTED]

The sole production HP authority is `TwentyWavePressureConfiguration.CreateCoreLoopV2()`. R1 is the full LargeScaleModerate curve with W5/W6 relieved to 45/63. No candidate selector is active in default runtime.

| W | Count / side | Effective Max HP | Boss slot |
| ---: | ---: | ---: | --- |
| 1 | 10 | 25.5 | No |
| 2 | 11 | 26.1 | No |
| 3 | 12 | 26.7 | No |
| 4 | 13 | 35 | No |
| 5 | 15 | 45 | No |
| 6 | 16 | 63 | Yes |
| 7 | 18 | 95 | No |
| 8 | 19 | 120 | No |
| 9 | 21 | 145 | No |
| 10 | 23 | 175 | No |
| 11 | 25 | 205 | No |
| 12 | 27 | 240 | Yes |
| 13 | 29 | 275 | No |
| 14 | 31 | 315 | No |
| 15 | 33 | 360 | No |
| 16 | 35 | 410 | Yes |
| 17 | 37 | 465 | No |
| 18 | 39 | 525 | No |
| 19 | 41 | 590 | No |
| 20 | 43 | 660 | Yes |

| Pacing / composition | Value |
| --- | --- |
| Preparation / interval / gap | 4.0s / 1.50s / 6.50s |
| Normal / Fast / Elite speed | 2.40 / 0.80 / 0.58 cells per second |
| W1 weights | 80% / 20% / 0% |
| W2 weights | 30% / 70% / 0% |
| W3 weights | 45% / 40% / 15% |
| W4-W20 weights | `Normal=0.55-0.30n`; `Fast=0.30+0.05n`; `Elite=0.15+0.25n`; `n=(W-4)/16` |
| Semantics | Timed waves, no normal intermission, residual enemies persist, W20 schedule completion does not settle a match. |

| Enemy | HP | XP | Speed | Status |
| --- | --- | ---: | --- | --- |
| Normal | Shared R1 wave HP | 1 | 2.40 | Active |
| Fast | Same shared R1 wave HP | 1 | 0.80 | Active |
| Elite | Same shared R1 wave HP | 3 | 0.58 | Active |
| Swarm | PENDING | PENDING | PENDING | Enum only, not production composition |
| Boss | PENDING | PENDING | PENDING | Slot only, no production entity |

Each side starts with 3 Hatchling HP and 20 Run Resource. Each Normal leak deals 1; a Boss reaching the goal causes instant defeat. Boss Summon goal behavior follows Boss Summon Rules V1. Base death decides the match; W20 schedule completion does not.

## Boss [FIXED V1 SELECTION, W6 IMPLEMENTED]

| Slot | Fixed V1 Boss | Status |
| --- | --- | --- |
| W6 | Soulchain Binder | Implemented and frozen. |
| W12 | Stormcaller Priest | Runtime and skill lifecycle implemented/verified; 1200 greybox HP, Production HP pending. |
| W16 | Bloodcrown Tyrant | Skill values frozen; 2400 greybox HP, Production HP/runtime pending. |
| W20 | Worldeater Wyrm | Skill values frozen; 5000 greybox HP, Production HP/runtime pending. |

V1 uses fixed Boss selection and no random pool or fallback. All other previously listed Bosses are Future Candidates. The authoritative behavior specification is `Docs/Drakeforge_Boss_System_V1_2026-08-17.md`.

W6 Soulchain Binder mechanism is implemented and verified in the Production W6 slot:
`BOSS_SOULCHAIN_BINDER`, Boss speed `0.20 cells/s`, FirstCastDelay `8.0s`, Windup `0.5s`,
Effect `2.0s`, Cooldown `15.0s`, max affected Basic `2`, and shared fixed W6 HP `600` [FROZEN].
The Boss is a separate W6 slot and does not increase the 16 Normal count. W6 HP is frozen at
600; W6/W12 Boss XP mapping and final-Hero attribution are implemented; W16/W20 entities remain
PENDING. W12/W16/W20 skill values are frozen in the Boss System V1 document; all W12/W16/W20
Production HP values remain PENDING. The
BoardQuality dynamic HP diagnostic was rejected for Production; both sides receive fixed 600 HP.

Spellbreaker windup failure is exposed through `ISoulChainSpellbreakerResolver`; a blocked
cast reflects 10% Boss MaxHP and enters cooldown without rewards. `ITEM_SPELLBREAKER_SEAL`
is implemented through the typed Boss-cast evaluation port; individual Boss adapters opt in.

## Twenty Items [FOUNDATION IMPLEMENTED]

Items are daily Build assets: reset at an injected authoritative DayKey, one owned copy per ItemId per day, no stacking. Each Run can equip up to 2 Active and 6 Passive Items. Item System permanently unlocks after the account normally completes its fifth Run; wins and losses count, abnormal exits do not. Unlock progress does not reset with DayKey. Item Foundation schema version 1, stable IDs, inventory/loadout validation, immutable Run Snapshot and the effect seam are implemented. The local repository and server ledger remain interfaces; no client clock is authoritative.

All 20 stable IDs are formal candidates and can enter a validated loadout. Forge Treasury uses a
local Run Resource port. Battlefield Command and Forgekeeper's Gift use authority-sensitive typed
ports: a missing/rejected Recruit, ad, Ledger, or Forge Pick authority never becomes a fabricated
client-side success.

| Item | Type | Rarity | Frozen effect |
| --- | --- | --- | --- |
| Wyrmfang Snare | Active | Rare | Route single target: Normal/SmallEnemy 40% MaxHP; Boss `min(120,5% MaxHP)`; CD45s. |
| Winterveil Rune | Active | Rare | All route enemies including Boss -10% speed for 5s; CD30s. |
| Runeburst Mine | Active | Excellent | One-use 1.25-cell AoE; small enemies 80 damage; Boss `min(80,3% MaxHP)`; CD60s. |
| Frenzy Rune | Active | Epic | Selected Basic/Hero attack speed x1.4; at most two multiplicative applications; CD60s. |
| Rune of Tempering | Active | Epic | Selected Basic/Hero 50% +1 or 50% -1 level; clamp bounds; CD45s. |
| Warforge Sigil | Active | Legendary | Selected Basic/Hero +1 level; Hero uses formal next-level XP interface; CD90s. |
| Drakeheart Relic | Passive | Rare | Own Max/Current Heart +3. |
| Pact of Endurance | Passive | Rare | Own Max/Current Heart +5; opponent +3. |
| Farwatch Crest | Passive | Rare | Skyborne, Windclaw and Basic Bow range x2 for the Run. |
| Frost Mire | Passive | Rare | All own-route enemies -10% speed for the Run. |
| War Tempo | Passive | Excellent | Both sides Basic/Hero attack speed +10% for the Run. |
| Veteran's Mark | Passive | Excellent | 5% of recruited Lv1 Basics become Lv2. |
| Quartermaster's Satchel | Passive | Excellent | Bench +1 for the Run; no stack. |
| Spellbreaker Seal | Passive | Epic | Boss skills 50% fail; failure damages Boss 10% MaxHP; unlimited. |
| Rivalry Oath | Passive | Epic | Own Basic/Hero attack speed +50%; opponent +30%. |
| Forge Treasury | Passive | Epic | Every 10 legal kills grants +3 Run Resource. |
| Battlefield Command | Passive | Epic | First Hero formation gives free Recruit; no resource cost or future cost increase. |
| Forgekeeper's Gift | Passive/ad-only | Legendary | At 90s and every 90s grant Forge Pick while Locked cells remain; not Gold-purchasable. |
| Dragonfall Judgment | Passive | Legendary | Once when an eligible enemy enters the final 3 cells: Normal/SmallEnemy 80% MaxHP; Boss `min(200,8% MaxHP)`; Worldeater Minion interaction remains PENDING. |
| Draconic Presence | Passive | Legendary | Each Hero reduces enemy speed 2%, max 10%, multiplicative with other slows. |

### Item Foundation IDs and status

| Stable ItemId | Category | Rarity | Status |
| --- | --- | --- | --- |
| `ITEM_WYRMFANG_SNARE` | Active | Rare | IMPLEMENTED |
| `ITEM_WINTERVEIL_RUNE` | Active | Rare | IMPLEMENTED |
| `ITEM_RUNEBURST_MINE` | Active | Excellent | IMPLEMENTED |
| `ITEM_FRENZY_RUNE` | Active | Epic | IMPLEMENTED |
| `ITEM_RUNE_OF_TEMPERING` | Active | Epic | IMPLEMENTED |
| `ITEM_WARFORGE_SIGIL` | Active | Legendary | IMPLEMENTED |
| `ITEM_DRAKEHEART_RELIC` | Passive | Rare | IMPLEMENTED |
| `ITEM_PACT_OF_ENDURANCE` | Passive | Rare | IMPLEMENTED |
| `ITEM_FARWATCH_CREST` | Passive | Rare | IMPLEMENTED |
| `ITEM_FROST_MIRE` | Passive | Rare | IMPLEMENTED |
| `ITEM_WAR_TEMPO` | Passive | Excellent | IMPLEMENTED |
| `ITEM_VETERANS_MARK` | Passive | Excellent | IMPLEMENTED |
| `ITEM_QUARTERMASTERS_SATCHEL` | Passive | Excellent | IMPLEMENTED |
| `ITEM_SPELLBREAKER_SEAL` | Passive | Epic | IMPLEMENTED (Boss adapters consume typed port) |
| `ITEM_RIVALRY_OATH` | Passive | Epic | IMPLEMENTED |
| `ITEM_FORGE_TREASURY` | Passive | Epic | IMPLEMENTED (typed local Run Resource port) |
| `ITEM_BATTLEFIELD_COMMAND` | Passive | Epic | IMPLEMENTED (typed free Recruit authority port) |
| `ITEM_FORGEKEEPERS_GIFT` | Passive | Legendary | IMPLEMENTED (typed ad-gated Forge Pick authority port) |
| `ITEM_DRAGONFALL_JUDGMENT` | Passive | Legendary | IMPLEMENTED (Worldeater Minion interaction PENDING) |
| `ITEM_DRACONIC_PRESENCE` | Passive | Legendary | IMPLEMENTED |

Item schema version is `1`. `IItemDayKeyProvider` supplies DayKey/day number; `IItemProfileRepository` and `IItemServerLedger` are explicit persistence/authority seams only. `ItemRunSnapshot` copies the validated daily loadout at RunStart and is not mutated by later out-of-run edits.

## Fourteen Runes [ALPHA ARCHITECTURE, PRODUCT CLOSURE VERIFIED]

Runes open Day 3, are permanent, do not level, bind to HeroId and snapshot at Run start. A HeroId equips one Rune. A complete Rune copy assigns to one distinct HeroId; two heroes need two copies. Basic cannot equip Runes.

| Rune ID | Rarity | Effect |
| --- | --- | --- |
| `Might` | Common | Hero attack damage +8%. |
| `Farreach` | Excellent | Hero range +0.75 cells. |
| `Power` | Excellent | Hero attack damage +15%. |
| `Longshot` | Excellent | Distance damage 0 to +20%, full near maximum range. |
| `Frostbite` | Excellent | Basic hit: Normal/Fast/Elite -10% speed 1.5s; Boss -5% 1s; refresh, no stack. |
| `Ricochet` | Epic | 30% successful-hit chance, one different target, 55% attack. |
| `Volley` | Epic | Every 10 successful attacks fires five bolts at 35% attack. |
| `BladeTempest` | Epic | Non-Rune-derived Hero kill: 40% chance to hit up to 3 nearby enemies at 60%. |
| `Ambush` | Epic | First successful attack of instance against enemy: 30% 0.75-cell AoE at 80%. |
| `Windhawk` | Epic | 15% hit chance, ICD2s; non-main highest progress 90%, fallback 60%. |
| `Skybreaker` | Legendary | 10% hit chance; primary 180%, 0.9-cell secondary 80%; no control. |
| `Wyrmguard` | Legendary | Level-up spirit 12s, 1.5 attacks/s, 35% current Hero attack; refresh existing. |
| `Dragonbloom` | Legendary | Non-Rune-derived Hero kill: 30% 4s bloom, 1 attack/s, 40% attack; refresh existing. |
| `Warcry` | Legendary | 12% hit chance, ICD10s; same-side Heroes within 2.5 cells gain 20% attack speed for 6s. |

Rune-derived damage keeps Hero DamageOwner, settles Last-Hit XP normally and cannot recursively trigger Runes. Common/Excellent drops are complete. Epic is 25% complete/75% fragment and crafts at 3. Legendary is fragment-only and crafts at 5. Limit is four successful Rune rewards per Run.

| Completed wave | Drop chance | Common | Excellent | Epic | Legendary fragment |
| --- | ---: | ---: | ---: | ---: | ---: |
| W1-W2 | 0% | - | - | - | - |
| W3-W6 | 12% | 75% | 25% | 0% | 0% |
| W7-W12 | 18% | 45% | 30% | 15% | 10% |
| W13-W16 | 28% | 30% | 30% | 25% | 15% |
| W17-W20 | 40% | 15% | 25% | 40% | 20% |

Rune profile schema 2 persists `AccountDay`, inventory entries (RuneId, rarity, OwnedCount, FragmentCount) and HeroId-to-RuneId assignments. It uses temporary JSON plus replacement/backup, migrates schema 0/1, rejects invalid content and never persists a Run snapshot. Cloud authority remains deferred.

## Economy, Energy, Merchant, Lottery and Ads [FROZEN DESIGN]

| Field | Value | Owner |
| --- | ---: | --- |
| Start Run Resource | 20 | Client simulation |
| Recruit cost | 10 +2 per successful Recruit | Client simulation |
| Normal/Fast/Elite kill resource | +1 | Client simulation |
| Victory Gold / ad double | 20 / 40 | Server settlement and client UI |
| Defeat, timeout, quit Gold / double | 10 / 20 | Server settlement and client UI |
| Settlement ad | One per Run; no daily cap | Both, RunId idempotency |
| Initial / cap Energy | 30 / 30 | Server authority, client display |
| Run-start Energy cost | 5 | Server atomic ledger |
| Natural and offline Energy | 1 per 3 minutes | Server time |
| Ad Energy / daily cap | +10 / 3 | Server verification |
| Share Energy / daily cap | +5 / 4 | Platform callback plus server ledger |

Energy rewards clamp to 30 but consume a valid claim. `Expedition Reserve` is the out-of-run entry. Server DayKey is account-region local midnight; server stores timezone and locks repeated change for 24h. Device time is not authoritative.

| Merchant / Lottery rule | Value | Owner |
| --- | --- | --- |
| Merchant trigger | Every 2 normally completed Runs; win/defeat count, abnormal exit does not. | Server counter |
| Merchant offer | 3 distinct unowned-today Items, or 1-2 if fewer; select at most one. | Server pool/client UI |
| Lifecycle | Choice invalidates others; closing consumes event on next Run; no daily popup cap. | Both |
| Ad offer | Max one per Merchant; Rare 10%, Excellent 20%, Epic 30%, Legendary 40%. | Server config |
| Lottery | One rewarded-ad attempt per Merchant from unowned-today Item pool. | Server verification |
| Failure | No verification/no-fill/interruption grants no reward and consumes no Lottery attempt. | Server |
| Item prices | Rare 40, Excellent 60, Epic 80, Legendary 120 Gold. | Server ledger |

`Forgekeeper's Gift` is rewarded-ad only. Superseded values include initial Energy 5, recovery 1/20 minutes, one daily Energy ad, six daily Merchant popups and six daily Lottery rolls.

### Rewarded-Ad Points

All ads are voluntary and server-verified. Analytics records request, display, complete, reward, failure and no-fill separately. Mandatory interstitials are excluded.

| Required future point ID | Trigger / reward | Limit |
| --- | --- | --- |
| PENDING `settlement_double` | Settlement Gold x2 | 1 / Run |
| PENDING `run_forge_pick` | Normal state: Forge Pick x2 | 1 successful claim / Run |
| PENDING `run_knockback` | Threat within final 2 cells: move all own-route enemies back 7 cells | 1 successful use / Run |
| PENDING `merchant_item` | Merchant ad Item | max 1 / Merchant |
| PENDING `merchant_lottery` | Lottery Item draw | 1 / Merchant event |
| PENDING `energy_reserve` | Energy +10 | 3 / DayKey |

The in-run button sits beside the five-result Recruit panel. It pauses all simulation, validates the live Run, locks selected reward type, then resumes after completion/failure/exit. Forge Pick and Knockback use independent quotas.

Required ad schema: `ad_point_id:string`, `enabled:bool`, `trigger_type:enum`, `trigger_value:int`, `reward_type:enum`, `reward_value:float`, `daily_limit:int`, `per_run_limit:int`, `cooldown_seconds:int`, `server_verify_required:bool`, `content_version:string`. Limit convention: -1 unlimited, 0 disabled, positive cap.

## DayKey and Rank [FROZEN DESIGN, PENDING IMPLEMENTATION]

| Domain | Rule | Owner/status |
| --- | --- | --- |
| DayKey | Server account-region midnight controls Energy and daily Item state. | Server, PENDING |
| Timezone safety | Account timezone stored server-side; 24h change lock. | Server, PENDING |
| Rank | 10 major ranks; ranks 1-9 have 3 segments, top rank uses cumulative stars. | Server rules/client UI, PENDING |
| Star/loss | Win +1; lower three ranks protect loss/demotion. Mid/high loss rule PENDING. | Server |
| Season | Season/reset; weekly leaderboard only; ties favor earlier achiever. | Server, PENDING |

Legacy display names Private, Corporal, Sergeant, Lieutenant, Captain, Major, Colonel, General, Marshal and Grand Marshal are reference-only. They have no authoritative persistent IDs or season formula and must not become identity data.

## Explicit Open Decisions

| Decision | Blocks |
| --- | --- |
| 24 current cells vs historical 40-cell board | Board/capacity redesign only; not R1/Rune baseline. |
| Oathcrown display label | Final localization/art lock only. |
| Trusted account-day/cloud Item ledger | Live account economy; Item Foundation uses interfaces only. |
| Boss entities, stats, XP and skills | Boss system. |
| Trusted account-day/cloud Rune profile | Live account economy; not local Rune closure. |
| Rank IDs, season reset and high-rank loss formula | Rank backend/UI. |

## Verification Reference

| Gate | Result | Evidence |
| --- | ---: | --- |
| R1 targeted production checks | 11/11 plus 1/1 candidate and 1/1 same-seed | `Docs/R1ProductionVerifiedBaseline.md` |
| Rune closure full EditMode | 416/416 | `Logs/RuneV1Closure-FullEditMode.xml` |
| Rune closure full PlayMode | 27/27 | `Logs/RuneV1Closure-FullPlayMode-02.xml` |
| Item Foundation targeted EditMode | 8/8 | `Logs/ItemFoundation-Targeted.xml` |
| Item Foundation Fast EditMode | 426/426 | `Logs/ItemFoundation-FastEditMode.xml` |
| Item Foundation full PlayMode | 27/27 | `Logs/ItemFoundation-PlayMode.xml` |

Use `Docs/TestLanes.md` for Targeted, Fast and Full gates. This document does not authorize Item, Boss, Energy/ads, Rank or balance implementation by itself.
