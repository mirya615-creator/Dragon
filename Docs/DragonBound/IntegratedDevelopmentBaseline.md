# Integrated Development Baseline

Date: 2026-08-14
Status: integration baseline. No gameplay, economy, HP, Rune-value or content expansion was
performed while creating this document.

## Git Authority and Safety

| Field | Value |
| --- | --- |
| Repository | `DragonBound` |
| Branch | `main` |
| HEAD | `3f7b7adfe233a345cb6792cc64bed06eec136cfe` |
| Decorated HEAD | `3f7b7ad (HEAD -> main, origin/main) Initial DragonBound project baseline` |
| Remote | `origin https://github.com/danti961224-coder/DragonBound.git` |
| Push/rebase/reset/clean | not performed |

The audit started with 130 worktree entries: 32 modified tracked files and 98 untracked files.
They predate this baseline document and are mixed with shared scene, Bootstrap and presentation
files. They must be treated as user-owned until separated in an isolated worktree.

| Audit bucket | Evidence and examples | Commit safety |
| --- | --- | --- |
| Existing user/history changes | portrait UI builder, presentation views, scenes, prefabs and pre-existing edited tests | Not safe to include without file ownership confirmation. |
| Core loop / Recruit / Hero XP | TwentyWave runtime/configuration, pressure diagnostics, AI controller/diagnostics, finite component recruitment, shovel and `HeroXpSettlement` | Functionally related, but shares Bootstrap and test files with other work. |
| R1 production HP | `TwentyWavePressureConfiguration.cs`, `EnemyHpCurveCandidate.cs`, R1 tests and `R1ProductionVerifiedBaseline.md` | Verified but not safely separable from current untracked core-loop tree. |
| Rune Architecture Alpha | `Runtime/Runes`, Rune effect tests, presentation art contracts | Frozen architecture baseline; shared Bootstrap/UI wiring prevents a safe isolated commit here. |
| Rune V1 product closure | persistence/profile operations, loadout view/prefab, Bootstrap/scene wiring and closure tests | Verified local product loop, but intersects shared UI and Bootstrap changes. |
| Tests, docs and diagnostic runners | EditMode/PlayMode additions, Editor batch runners, verification docs and Logs | Some can be isolated later; Logs are evidence, not source-history cleanup candidates. |
| Uncertain provenance | any remaining edited shared asset, scene, prefab or presentation file not covered by a feature manifest | Leave untouched; inspect against a known snapshot before assigning ownership. |

No local commit was created. Creating a commit now would either omit dependencies or capture changes
with unknown ownership. No remote state was modified.

## Verified Module Baseline

| Module | State | Authority / evidence |
| --- | --- | --- |
| Twenty-wave pressure | R1 Production frozen | `TwentyWavePressureConfiguration.CreateCoreLoopV2()`; W5=45, W6=63; `Docs/R1ProductionVerifiedBaseline.md`. |
| Core Loop V2 | integrated greybox | 20 waves, independent side state, deterministic streams and diagnostics are present in the working tree. |
| Recruit V3 / finite bag / Forge Pick | integrated greybox | 24-instance bag, five results, dynamic catch-up and miss-pity implementation. |
| Hero XP | verified last-hit | Hero final hit receives full formal XP; Basic/system/leak kills award Hero XP zero. |
| Rune Architecture | Architecture Alpha frozen | 14 runes, effects/drop/snapshot seam; do not expand during R1 stabilization. |
| Rune product closure | verified local profile/UI loop | schema v2, Day 3 gate, durable loadout and locked Run snapshot. |
| Item, Boss, Energy/ads, Rank backend | design/configuration only | No implementation was started by this baseline task. |

## Verification Record

The full baseline immediately before this integration task is taken from final NUnit XML:

| Gate | Result | Artifact |
| --- | ---: | --- |
| Unity compile | passed | `Logs/RuneV1Closure-Compile-02.log` |
| Full EditMode including diagnostics | 416 / 416 | `Logs/RuneV1Closure-FullEditMode.xml`, 1994.1455457s, SHA-256 prefix `8C59AE0B610ACAEA` |
| Full PlayMode | 27 / 27 | `Logs/RuneV1Closure-FullPlayMode-02.xml`, 24.6819879s, SHA-256 prefix `2D542F11AF8D6E6A` |

This baseline task adds diagnostic category metadata and the test-lane launcher only. Its fresh
verification results are read from final XML where XML exists:

| Gate | Result | Duration | Artifact / SHA-256 prefix |
| --- | ---: | ---: | --- |
| Unity compile | passed, exit 0 | batch completed | `Logs/IntegrationBaseline-Compile.log`, `AE43F841F87FAF91` |
| Fast EditMode, excluding diagnostics | 398 / 398 | 7.3044694s | `Logs/IntegrationBaseline-FastEditMode.xml`, `87B1A7C1DFE433C4` |
| Complete PlayMode | 27 / 27 | 25.2622715s | `Logs/IntegrationBaseline-PlayMode.xml`, `CD7F047418396D0A` |

The Fast XML contains none of the named 10k/100k or 1000-seed diagnostics sampled during the
lane audit. Full EditMode was intentionally not rerun: the prior final XML remains the required
416 / 416 full-gate baseline and no diagnostic assertion, sample size or gameplay code changed.

## Recovery

1. Preserve the current dirty tree before performing any source rollback.
2. Use an isolated worktree from `3f7b7adfe233a345cb6792cc64bed06eec136cfe` to inspect a module
   manifest and recover only the explicitly owned files.
3. For R1 HP, use `Logs/R1ProductionPromote_Artifacts/ROLLBACK.ps1`; it checks promoted hashes
   before changing the promotion's four recorded files.
4. For Rune local saves, the repository uses `dragonbound-runes-v1.json` plus `.bak`; corrupt
   primaries are rejected and a valid backup is recovered.
5. Do not use `git reset`, `git checkout --`, `git restore .`, `git clean`, `rebase`, force push
   or a broad commit to make this working tree look clean.

## Next Decision

The single recommended next task is **Item System V1** only after the user confirms it. Keep R1
Production and Rune V1 frozen until then.

## Continuation Verification (2026-08-17)

The project was reopened in the Dragon workspace and the verification lanes were rerun. The
standalone compile used the normal package-loading path (without `-noUpm`); the earlier
`-noUpm` attempt was invalid because it suppressed the UGUI package and produced misleading
`UnityEngine.UI` compiler errors.

| Gate | Result | Duration / evidence |
| --- | ---: | --- |
| Unity compile | passed, exit 0 | `Logs/IntegrationCheckpoint-Compile.log`; script compilation 41.237820s |
| Fast EditMode, excluding diagnostics | 438 / 438 | `Logs/IntegrationCheckpoint-FastEditMode.xml`; 3.6451641s |
| Complete PlayMode | 27 / 27 | `Logs/IntegrationCheckpoint-PlayMode.xml`; 26.8676759s |

The approximately 33-minute Full EditMode lane was not rerun. No source or gameplay values were
changed during this continuation. The worktree remains uncommitted because the existing source,
scene, prefab, test, diagnostic, and documentation files are mixed across prior tasks and cannot
be safely separated by ownership from this checkpoint alone.
