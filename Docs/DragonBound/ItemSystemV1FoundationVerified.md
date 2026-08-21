# Item Gameplay Vertical Slice Verified

Date: 2026-08-17

## Scope

This slice connects the Item Foundation to the live greybox twenty-wave pressure race. It does not add Item state to Hero or Rune, and it does not implement Merchant, Lottery, ads, Gold/Energy, cloud ledger, W12/W16/W20 Bosses, or any server-authoritative settlement.

## Data flow

`authoritative account progress + authoritative DayKey -> daily inventory/loadout -> validated match-start snapshots -> independent player/AI ItemRunRuntime -> pressure race command/effects -> greybox HUD`

`IItemAccountProgressProvider` supplies the already-authoritatively-counted normal completion total. Item code does not increment it. The fifth normal completion permanently unlocks Items; both victory and defeat count, `AbnormalExit` does not. `IItemDayKeyProvider` supplies DayKey; device time is not read. DayKey changes clear daily inventory/loadout but never re-lock the system.

`ItemProfileData` is schema version `2`. Version `1` is explicitly rejected with `IncompatibleSchema`: its `DayNumber` has no safe conversion to normal completed-match count. A backend/profile migration must refresh both the authoritative DayKey and normal completion count before supplying a V2 record.

Each daily ItemId has at most one owned copy. A Run loadout accepts at most 2 Active and 6 Passive Items, rejects unknown, pending, unowned, duplicate and category-invalid entries, and creates an immutable snapshot. Later loadout edits do not affect that snapshot.

## Catalog status

There are 20 unique stable ItemIds. All 20 are formal candidates with typed runtime effects. Economy/flow effects delegate authority-sensitive operations to explicit integration ports:

| Item | Category | Effect |
| --- | --- | --- |
| `ITEM_DRAKEHEART_RELIC` | Passive/Rare | RunStart own Max Heart +3 and Current Heart +3 |
| `ITEM_WINTERVEIL_RUNE` | Active/Rare | Use: all currently alive own-route enemies, including Boss, MoveSpeed -10% for 5s; CD30s |
| `ITEM_WYRMFANG_SNARE` | Active/Rare | Route single target: Normal/SmallEnemy 40% MaxHP; Boss `min(120,5% MaxHP)`; CD45s |
| `ITEM_RUNEBURST_MINE` | Active/Excellent | 1.25-cell AoE; Normal/SmallEnemy 80 damage; Boss `min(80,3% MaxHP)`; CD60s |
| `ITEM_FRENZY_RUNE` | Active/Epic | Selected Basic/Hero attack speed x1.4, at most twice per unit; CD60s |
| `ITEM_RUNE_OF_TEMPERING` | Active/Epic | Selected Basic/Hero 50% +1 or 50% -1 level with boundary clamp; CD45s |
| `ITEM_WARFORGE_SIGIL` | Active/Legendary | Selected Basic/Hero +1 level; Hero uses typed next-level progression; CD90s |
| `ITEM_DRAGONFALL_JUDGMENT` | Passive/Legendary | One eligible final-three-cell trigger; Normal/SmallEnemy 80% MaxHP; Boss `min(200,8% MaxHP)`; Worldeater Minion interaction PENDING |
| `ITEM_PACT_OF_ENDURANCE` | Passive/Rare | RunStart own Max/Current Heart +5; opponent +3 |
| `ITEM_FARWATCH_CREST` | Passive/Rare | Eligible Valkyrie/Ranger/Basic archer Range x2 for the run |
| `ITEM_FROST_MIRE` | Passive/Rare | Own-route enemies MoveSpeed -10% for the run |
| `ITEM_WAR_TEMPO` | Passive/Excellent | Basic and Hero AttackSpeed +10% for both sides |
| `ITEM_VETERANS_MARK` | Passive/Excellent | Recruit Lv1 Basic has deterministic 5% direct Lv2 promotion |
| `ITEM_QUARTERMASTERS_SATCHEL` | Passive/Excellent | Bench capacity +1, non-stacking |
| `ITEM_SPELLBREAKER_SEAL` | Passive/Epic | Boss cast 50% block port; blocked cast reflects 10% Boss MaxHP |
| `ITEM_RIVALRY_OATH` | Passive/Epic | Own Basic/Hero AttackSpeed +50%; opponent +30% |
| `ITEM_DRACONIC_PRESENCE` | Passive/Legendary | Each own Hero slows own-route enemies by 2%, capped at 10% |
| `ITEM_FORGE_TREASURY` | Passive/Epic | Every 10 explicitly legal kills requests +3 local Run Resource through `IItemRunResourcePort` |
| `ITEM_BATTLEFIELD_COMMAND` | Passive/Epic | First Hero formation requests one free Recruit through `IItemFreeRecruitPort`; no local success is fabricated |
| `ITEM_FORGEKEEPERS_GIFT` | Passive/Legendary | At 90s and every 90s requests an ad-gated Forge Pick through `IItemForgePickPort`; `NoLockedCell` stops future requests |

Forge Treasury has a local run-resource adapter only. Battlefield Command and Forgekeeper's Gift are not connected to a server, advertising, Ledger, Merchant, or live Recruit authority: absent or rejected ports never report a grant. Dragonfall Judgment's Worldeater Minion interaction remains `PENDING`. Effect creation uses a typed `ItemEffectKind` registry, not string reflection or a monolithic effect switch.

## Verification record

`TwentyWavePressureRuntime` requests `IItemRunSnapshotProvider` once during `StartRun`, locks one snapshot for each side and creates isolated runtimes. It never polls a service while attacking or ticking. `TryUseItem(TeamSide, itemId, out reason)` is the common player/AI active command entry. Failed activation does not start cooldown. The player HUD binds two greybox active slots to that same command; an unconfigured HUD generates placeholder controls at runtime, so no scene or formal art asset changes are required.

EditMode coverage in `Assets/DragonBound/Tests/EditMode/ItemSystemV1FoundationTests.cs` and `ItemGameplayIntegrationTests.cs` covers fifth-match unlock, result classification, DayKey reset persistence, schema rejection, immutable loadouts, player/AI snapshot and cooldown isolation, one-time Drakeheart application, Winterveil failure/no-CD, normal/Boss targeting, restoration, cooldown, and HUD slot click binding. Targeted EditMode, Fast EditMode, and PlayMode are required for this runtime/bootstrap/UI change; Full EditMode is intentionally excluded.

Item effect verification on 2026-08-18: A-group Targeted EditMode `22/22` passed (`Logs/ItemActiveA-Targeted.xml`) and Fast EditMode `555/555` passed (`Logs/ItemActiveA-Fast.xml`). B-group rerun Targeted EditMode `8/8` passed in `0.187s` (`Logs/ItemPassiveB-Targeted-rerun.xml`) and Fast EditMode `563/563` passed in `10.692s` (`Logs/ItemPassiveB-Fast-rerun.xml`). C-group full Item Targeted EditMode `42/42` passed in `0.379s` (`Logs/ItemC-Targeted-All-rerun.xml`) and Fast EditMode `567/567` passed in `10.746s` (`Logs/ItemC-Fast.xml`). Full EditMode was not run.

## Art and UI seam

Every definition exposes `IconKey` and `ArtAssetKey` placeholders. The two active slots use the current UGUI Button/Text system and placeholder labels only. No final art, Merchant UI or production Item screen is included.

## Next step

The A, B, and C Item V1 slices are implemented. Remaining work is integration ownership for the C-group authority ports and the explicitly pending Dragonfall Judgment/Worldeater Minion interaction; no client result may be presented as authoritative server, advertising, Ledger, or reward success.
