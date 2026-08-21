# Analytics Event Schema V1

Status: frozen QA contract for local instrumentation. This contract does not select an analytics vendor, send data over the network, or change simulation behavior.

## Scope and privacy

V1 captures run mechanics and stable content identifiers only. Do not send display names, localized text, account identifiers, device identifiers, advertising identifiers, input traces, chat, IP addresses, or other personal data. Stable IDs such as `BOSS_SOULCHAIN_BINDER`, `SOULCHAIN`, item IDs and rune IDs are not display text.

Every emitted event is one JSON object. `JsonlTelemetry.Record(AnalyticsEvent)` writes one top-level JSONL object for local QA; the existing legacy `ITelemetry.Record(string, int, long, string)` remains supported for diagnostic callers.

## Common envelope

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `event_name` | string | yes | One registered stable name below. |
| `event_version` | integer | yes | `1` for this contract. |
| `event_id` | string | yes | Unique client-generated retry/deduplication ID. |
| `client_timestamp` | string | yes | ISO-8601 UTC timestamp; ordering uses `sequence`, not wall clock. |
| `run_id` | string | yes | Opaque per-run ID, not an account ID. |
| `run_seed` | integer | yes | Exact `RunSeed` used by the simulation. |
| `config_version` | string | yes | Content/config snapshot version. |
| `build_version` | string | yes | Client build identifier. |
| `side` | string | yes | `player`, `ai`, or `system`. `player` is used for player-owned action events. |
| `wave` | integer | yes | `0` before a wave; otherwise 1-20. |
| `sequence` | integer | yes | Starts at 1 per `run_id`, strictly increments by one. |

Optional common payload fields are serialized as empty/zero when not applicable: `result`, `elapsed_seconds`, `boss_id`, `skill_id`, `summon_id`, `unit_id`, `hero_id`, `hero_level`, `item_id`, `rune_id`, `count`, `value`, and `reason`.

## Registered events

| Event | Required event fields | Semantics |
| --- | --- | --- |
| `run_start` | common envelope | Run snapshot accepted locally; use `wave=0`. |
| `run_finish` | `result`, `elapsed_seconds`, `reason` | One terminal result for a run. `result` is a stable result code, not UI text. |
| `wave_reached` | `elapsed_seconds` | Scheduler started this wave. |
| `death_wave` | `reason` | Player-side terminal wave; `wave` identifies the loss wave. |
| `boss_spawned` | `boss_id`, `elapsed_seconds` | One event per spawned boss side. |
| `boss_killed` | `boss_id`, `elapsed_seconds` | Boss death, before any rewards are inferred. |
| `boss_ttk` | `boss_id`, `elapsed_seconds` | TTK in seconds from `boss_spawned` to kill. |
| `boss_skill_cast` | `boss_id`, `skill_id`, `count`, `value` | `count` is cast ordinal; `value` is affected-count or another documented numeric outcome. |
| `boss_summon_spawned` | `boss_id`, `summon_id`, `count` | `count` is the spawned amount. |
| `recruit` | `unit_id`, `count` | Stable recruited unit/component ID and quantity. |
| `hero_formed` | `hero_id` | Hero formation completed; never send its display name. |
| `hero_level_up` | `hero_id`, `hero_level` | Level transition after XP resolution. |
| `item_equipped` | `item_id` | Loadout snapshot mutation; slot may be carried in `reason` once a stable slot code is defined. |
| `item_used` | `item_id` | Runtime activation; `reason` may contain a stable trigger code. |
| `rune_equipped` | `rune_id`, `hero_id` | Loadout snapshot mutation. |
| `rune_drop` | `rune_id`, `count` | Reward grant; `reason` distinguishes full-rune and fragment via a stable code. |
| `heart_lost` | `count`, `reason` | Player or AI loses hearts. `reason` is a stable leak/boss-goal code. |

`death_wave` does not replace `run_finish`; a defeat therefore emits `death_wave` before `run_finish`. A boss kill emits `boss_killed` before `boss_ttk`. A missing or non-applicable numerical value must remain `0`, never be encoded as a display string.

## Versioning and delivery rules

- Event names and field meanings are append-only inside V1. Never rename a stable identifier or change a field's unit.
- A breaking change creates event version 2 while old producers may continue to send V1. Consumers branch on `(event_name, event_version)`.
- Adding a new optional field is allowed in V1 after this document is updated. Adding an event requires a new named constant and schema test.
- `event_id` deduplicates retry submissions. `AnalyticsRecorder` retains accepted IDs per process and does not send duplicate IDs to its sink.
- The recorder rejects an out-of-order `sequence` and any changed `run_seed` within one `run_id`. A sink stores an immutable snapshot of an accepted event.
- `client_timestamp` is diagnostic context only. Server receipt time, when a service is later introduced, must be a separate field owned by that service.

## Local integration contract

`AnalyticsRecorder` accepts an `IAnalyticsSink`. `InMemoryAnalyticsSink` is the test/default QA sink. `JsonlTelemetry` is a file sink for local diagnostic artifacts. No cloud SDK belongs in this layer.

Callers must create the `run_id`, snapshot `run_seed/config_version/build_version`, and allocate monotonically increasing sequences once at run bootstrap. The first phase deliberately does not wire gameplay classes. See `QARegressionMatrixV1.md` for the planned event hooks.
