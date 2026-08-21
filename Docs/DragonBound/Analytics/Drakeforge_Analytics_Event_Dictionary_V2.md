# Drakeforge Analytics Event Dictionary V2

Status: V2 event contract for client QA and future service ingestion. The client analytics layer observes gameplay and service responses; it is not the authority for Energy, Gold, Item ownership, ad rewards or rank.

Code contract: `Assets/DragonBound/Runtime/Analytics/AnalyticsEventSchemaV2.cs`.

Event count: 45.

## Common Envelope

| Field | Required | Rule |
| --- | --- | --- |
| `event_name` | yes | One registered V2 event name. |
| `event_version` | yes | `2`. |
| `event_id` | yes | Unique retry/dedupe key for this event. |
| `client_timestamp` | yes | ISO-8601 UTC; sequence is authoritative for client order. |
| `run_id` | yes | Opaque per-run ID, never an account or device ID. |
| `run_seed` | yes | Exact run seed used by simulation. |
| `execution_context` | yes | `live_player_vs_ai`, `diagnostic_ai_vs_ai`, `hero_slice_showcase`. |
| `side` | yes | `player`, `ai`, `system`. |
| `wave` | yes | `0` before wave start, otherwise `1` to `20`. |
| `rank_tier` | yes | `unranked`, `bronze`, `silver`, `gold`, `platinum`, `diamond`, `master`, `legend`. |
| `ai_difficulty` | yes | `none`, `easy`, `standard`, `hard`, `elite`. |
| `sequence` | yes | Starts at 1 per `run_id` and increments by one. |
| `config_version` | yes | Stable config/content snapshot. |
| `build_version` | yes | Client build identifier. |

PII rule: do not include display names, localized text, account IDs, platform IDs, device IDs, ad IDs, IP addresses, tokens, secrets, raw transaction IDs, input traces or chat text. Ledger references must be hashes or opaque non-sensitive references.

## Event Dictionary

| Event | Trigger point | Required payload | Source environment | Dedupe key | Authority boundary | PII rule | Acceptance method |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `run_start` | Run bootstrap accepts seed/config/build. | Common envelope. | All three contexts. | `run_id:run_start`. | Client observes local run start; energy spend requires ledger result. | No account/device IDs. | Schema validation and first sequence is 1. |
| `wave_start` | Scheduler begins a wave. | Common envelope with `wave`. | Live, diagnostic. | `run_id:wave:N:start`. | Client schedule observation. | Stable IDs only. | Wave order test. |
| `wave_finish` | Scheduler closes wave window. | Optional residual counts via nearby snapshot. | Live, diagnostic. | `run_id:wave:N:finish`. | Client schedule observation. | Stable IDs only. | Residual cross-wave test. |
| `enemy_spawn` | Regular enemy spawn. | `enemy_type`, `enemy_id`, `move_speed_cells_per_second`, optional HP. | Live, diagnostic. | `run_id:side:enemy_id:spawn`. | Client runtime observation. | Runtime ID is opaque. | Verify `normal` at `0.60`. |
| `enemy_goal` | Non-Boss enemy reaches endpoint. | `enemy_type`, `enemy_id`, `reason`. | Live, diagnostic. | `run_id:side:enemy_id:goal`. | Client runtime observation. | Runtime ID is opaque. | Heart loss follows normal goal. |
| `recruit_result` | Recruit batch is committed. | `recruitment_number`, `component_count`, `basic_count`, `forge_pick_count`, `component_policy`, `remaining_component_bag`. | All three contexts when recruiting exists. | `run_id:side:recruitment_number`. | Analytics records actual result only; RNG remains gameplay-owned. | No component display names. | Counts sum to 5, components 0-3, Basic at least 1. |
| `formation_snapshot` | Key state bookmark. | `snapshot_reason`, Basic/Hero/Component counts, Board/Bench occupied, hittable count. | All three contexts. | `run_id:side:sequence` or stable bookmark key. | Client snapshot only. | No position trace beyond aggregate counts. | Verify sampled only at approved nodes. |
| `hero_formed` | Components resolve into a hero. | `hero_id`. | All three contexts. | `run_id:side:hero_runtime_id:formed`. | Client observes formation. | Stable hero ID only. | Hero formation test. |
| `hero_xp` | XP is awarded to a hero. | `hero_id`, `xp_amount`. | Live, diagnostic. | `run_id:side:hero_runtime_id:xp:sequence`. | Client observes combat settlement. | Stable IDs only. | Last-hit XP test. |
| `hero_level_up` | Hero level transition is committed. | `hero_id`, `hero_level`. | Live, diagnostic. | `run_id:side:hero_runtime_id:level:hero_level`. | Client observes XP settlement. | Stable IDs only. | Level threshold test. |
| `last_hit` | Enemy kill owner is resolved. | `enemy_id`, `source_unit_id`, optional `hero_id`. | Live, diagnostic. | `run_id:side:enemy_id:last_hit`. | Client observes combat settlement. | Runtime IDs only. | Last-hit owner test. |
| `boss_spawn` | Boss runtime is spawned. | `boss_id`, `enemy_type=boss`, `move_speed_cells_per_second`, `max_hit_points`. | Live, diagnostic. | `run_id:side:boss_runtime_id:spawn`. | Client runtime observation; W6 production HP remains pending. | Stable Boss ID only. | Verify W6 speed `0.20`. |
| `boss_skill` | Boss skill cast starts or resolves. | `boss_id`, `skill_id`, `count`. | Live, diagnostic. | `run_id:side:boss_id:skill_id:count`. | Client runtime observation. | Stable skill ID only. | W6 Soulchain cast test. |
| `boss_summon` | Boss spawns a summon. | `boss_id`, `summon_id`, `enemy_type=boss_summon`, `count`. | Live, diagnostic. | `run_id:side:boss_id:summon_id:count`. | Client runtime observation. | Stable summon ID only. | Pending Boss summon feature test. |
| `boss_damage_window` | Configured Boss damage window closes. | `boss_id`, `damage_window_id`, `duration_seconds`, optional `damage`. | Live, diagnostic. | `run_id:side:boss_id:window_id`. | Client aggregate observation. | No per-frame traces. | W6 3s/5s aggregate check. |
| `boss_kill` | Boss death resolves. | `boss_id`, `duration_seconds`. | Live, diagnostic. | `run_id:side:boss_runtime_id:kill`. | Client combat observation. | Stable IDs only. | Boss kill before terminal finish. |
| `boss_goal` | Boss reaches endpoint. | `boss_id`, `heart_after=0`. | Live, diagnostic. | `run_id:side:boss_runtime_id:goal`. | Client observes instant defeat; settlement is gameplay-owned. | Stable IDs only. | Boss goal triggers defeat. |
| `heart_lost` | Side loses hearts. | `count`, `reason`, `heart_after`. | Live, diagnostic. | `run_id:side:wave:heart_after:sequence`. | Client observes health delta. | Stable reason code only. | Normal goal loses 1. |
| `death_wave` | Side reaches defeat condition. | `wave`, `reason`. | Live, diagnostic. | `run_id:side:death_wave`. | Client observes terminal wave. | Stable reason code only. | Emitted before defeat finish. |
| `match_finish` | Match reaches terminal result. | `match_result`, `reason`. | Live, diagnostic. | `run_id:match_finish`. | Client observes local terminal state; rewards require ledger. | Stable result code only. | Exactly one per run. |
| `item_grant` | Item grant is acknowledged. | `item_id`. | Live; possibly diagnostic fixtures. | `run_id:item_id:grant:ref`. | Server owns durable item grants. | Stable item ID only. | Paired with accepted ledger where durable. |
| `item_equip` | Item is equipped. | `item_id`. | Live. | `run_id:item_id:equip:slot`. | Server/profile owns durable state when applicable. | Stable item ID only. | Profile operation success. |
| `item_use` | Runtime item activation succeeds. | `item_id`. | Live, diagnostic. | `run_id:side:item_id:use:sequence`. | Client observes runtime activation. | Stable item ID only. | Item runtime targeted test. |
| `rune_grant` | Rune or fragment grant is acknowledged. | `rune_id`, optional `count`. | Live; diagnostic reward fixtures. | `run_id:rune_id:grant:ref`. | Server/profile owns durable grants when applicable. | Stable rune ID only. | Rune reward/loadout tests. |
| `rune_equip` | Rune loadout mutation succeeds. | `rune_id`, optional `hero_id`. | Live, showcase. | `run_id:hero_id:rune_id:equip`. | Profile owns durable loadout. | Stable IDs only. | Rune profile operation test. |
| `rune_loadout_assign` | Typed loadout assign result returns from the profile service. | `hero_id`, `rune_id`, `operation_result`, optional `reason`. | Live, showcase. | `run_id:rune:operation_id`. | Profile/service owns loadout; Analytics records the returned result. | Stable hero/rune IDs and reason codes only. | Assign success and rejected-copy tests. |
| `rune_loadout_unequip` | Typed loadout unequip result returns from the profile service. | `hero_id`, `operation_result`, optional `reason`. | Live, showcase. | `run_id:rune:operation_id`. | Profile/service owns loadout; Analytics records the returned result. | Stable hero ID and reason codes only. | Unequip success and empty-loadout rejection tests. |
| `rune_craft` | Typed craft result returns from the profile service. | `rune_id`, `operation_result`, optional `reason`. | Live, showcase. | `run_id:rune:operation_id`. | Profile/service owns inventory; Analytics records the returned result. | Stable rune ID and reason codes only. | Craft success and insufficient-fragments tests. |
| `rune_gate_rejection` | Rune operation is rejected by the Day 3 feature gate. | `rune_operation`, `gate_state=locked`, `account_day`, `reason`. | Live, showcase. | `run_id:rune:gate:operation_id`. | Account progression/profile boundary owns gate state; client observes the typed rejection. | No account IDs; stable operation/reason codes only. | Day 1/2 rejection asserts `RuneSystemLockedUntilDay3`. |
| `rune_reward_pending` | Completed wave enters reward resolution before a result is available. | `reward_wave`, `reward_state=pending`. | Live, diagnostic fixtures. | `run_id:rune:reward:pending_ref`. | Reward service owns roll and grant; this is an observation only. | No account/device IDs. | Pending precedes exactly one result. |
| `rune_reward_granted` | Typed reward result is granted to the inventory boundary. | `reward_wave`, `rune_id`, `reward_state=granted`, optional `reward_form`. | Live, diagnostic fixtures. | `run_id:rune:reward:grant_ref`. | Profile/inventory owns durable grant; Analytics never grants. | Stable rune ID and form only. | Granted result paired with reward service result. |
| `rune_reward_rejected` | Typed reward result is rejected or blocked. | `reward_wave`, `reward_state=rejected`, `reason`, optional `rune_id`. | Live, diagnostic fixtures. | `run_id:rune:reward:reject_ref`. | Reward/profile boundary owns rejection; Analytics never retries or grants. | Stable reason code only. | Gate/cap/failed grant rejection test. |
| `energy_spend` | Server returns spend result for run start. | `energy_amount`, `reason`. | Live. | Ledger hash reference. | Server ledger authoritative. | No raw token or transaction ID. | Paired `ledger_result`. |
| `energy_grant` | Server returns grant result. | `energy_amount`, `reason`. | Live. | Ledger hash reference. | Server ledger authoritative. | No raw token or transaction ID. | Paired `ledger_result`. |
| `ad_request` | Client requests ad placement. | `ad_point_id`. | Live. | `run_id:ad_point_id:request:sequence`. | Client observes request only. | No ad ID or device ID. | Ad SDK smoke. |
| `ad_result` | Ad SDK returns result. | `ad_point_id`, `result`. | Live. | `run_id:ad_point_id:result:sequence`. | Ad completion is not reward authority. | No ad ID or device ID. | Failure gives no reward. |
| `merchant_open` | Server/client opens merchant event. | `merchant_id`. | Live. | `merchant_id:open:server_ref`. | Server owns eligibility. | Stable merchant ID only. | Merchant counter test. |
| `merchant_offer` | Merchant offer list is shown. | `merchant_id`, `offer_id`. | Live. | `merchant_id:offer_id`. | Server owns offer pool. | Stable offer/item IDs only. | Offer count and uniqueness. |
| `merchant_purchase` | Offer selection is acknowledged. | `merchant_id`, `offer_id`. | Live. | Ledger hash reference. | Server ledger authoritative. | No raw payment/transaction data. | Alternatives invalidated server-side. |
| `ledger_result` | Server ledger or idempotency layer returns status. | `ledger_operation`, `ledger_status`, `transaction_ref_hash` or `idempotency_key_hash`. | Live. | Hash reference plus operation. | Server authoritative for Energy/Gold/Item/ad rewards. | Hash or non-sensitive reference only. | Schema rejects token-like values. |
| `rank_snapshot` | Rank state is fetched or match starts. | `rank_value`, common `rank_tier`. | Live. | `rank_snapshot:server_ref`. | Server rank authoritative. | No account ID. | Rank display/backend test. |
| `rank_change` | Server confirms rank delta. | `rank_value`, common `rank_tier`, `reason`. | Live. | `rank_change:server_ref`. | Server rank authoritative. | No account ID. | Loss/demotion rules test. |
| `leaderboard_snapshot` | Weekly/monthly leaderboard is fetched. | `leaderboard_period`, `rank_value`. | Live. | `leaderboard:period:server_ref`. | Server leaderboard authoritative. | No names or account IDs. | Weekly/monthly separation. |
| `settlement_gold` | Server confirms match settlement Gold. | `gold_amount`, `reason`. | Live. | Ledger hash reference. | Server ledger authoritative. | No raw transaction ID. | Victory/defeat amount check. |
| `emergency_save` | Emergency rescue action resolves. | `result`, `reason`. | Live. | `run_id:side:emergency_save:sequence`. | Reward, if any, requires ledger. | Stable reason code only. | Rescue feature test. |

## Recorder Contract

- `event_id` deduplicates retries.
- `run_seed` must not change within one `run_id`.
- `execution_context` must not change within one `run_id`.
- `sequence` must be contiguous per `run_id`.
- The in-memory QA sink stores clones, so callers cannot mutate recorded evidence after acceptance.
- Rune lifecycle observations are emitted by `RuneAnalyticsAdapterV2` from typed adapter inputs; the adapter does not call Rune services or infer inventory changes.
