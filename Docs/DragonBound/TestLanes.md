# Test Lanes

Status: active from the 2026-08-14 integrated baseline.

The project keeps all diagnostic assertions and sample sizes. The fast lane excludes tests
explicitly marked with NUnit `Diagnostics` or `LongRunning`; it does not alter any test body,
Monte Carlo sample count, or production configuration.

## Commands

Run from the repository root. `TestLanes.ps1` writes a fresh NUnit XML and Unity log for every
invocation. Supply explicit output paths when a verification record needs stable artifact names.

| Lane | Command | Purpose |
| --- | --- | --- |
| Targeted | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane Targeted -TestFilter "DragonBound.Tests.EditMode.TwentyWavePressureTests"` | A touched fixture or fully-qualified test before broader validation. |
| Fast EditMode | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane FastEditMode` | Everyday model/configuration gate; excludes `Diagnostics` and `LongRunning`. |
| PlayMode | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane PlayMode` | Complete PlayMode gate. It is always part of the fast gate. |
| Full EditMode | `powershell -ExecutionPolicy Bypass -File .\Scripts\TestLanes.ps1 -Lane FullEditMode` | All EditMode tests, including all diagnostic/Monte Carlo runs. |

Unity Test Framework category negation is provided by `com.unity.test-framework@1.1.33`:
`-testCategory "!Diagnostics;!LongRunning"`.

## Required Gates

| Change type | Minimum verification |
| --- | --- |
| Isolated unit/configuration edit | Targeted plus Fast EditMode. |
| Runtime, bootstrap, UI, persistence or scene edit | Targeted, Fast EditMode, and complete PlayMode. |
| Recruit RNG, pressure balance, AI, Rune rewards, diagnostics, save schema or release candidate | Targeted, Fast EditMode, complete PlayMode, then Full EditMode. |
| Any changed diagnostic assertion or sample count | Full EditMode is mandatory. |

## Diagnostic Classification

The following classes now carry `Diagnostics` on their large-sample or search tests:

- finite component recruitment completion and Monte Carlo audits;
- Dynamic Component Catch-up V3, Forge Pick and shovel 10k/100k diagnostics;
- 1000-seed pressure, HP curve, capacity and recipe-coverage audits;
- the 100-seed AI survival report;
- existing bare-run 1000-seed diagnostics (already classified).

Ordinary deterministic unit tests remain in the fast lane. A test can use large fixture values
without becoming diagnostic when it does not execute a large simulation.

## Full-Gate Reference

The latest full EditMode baseline was intentionally not rerun for this documentation/category
change because it is approximately 33 minutes and no assertion, sample size, gameplay code or
production configuration changed.

| Artifact | Result | Duration | SHA-256 |
| --- | ---: | ---: | --- |
| `Logs/RuneV1Closure-FullEditMode.xml` | 416 / 416 passed | 1994.1455457s | `8C59AE0B610ACAEA4D1AB42403DAC350FA912289EB68516749520EA1B71...` |

Fresh Fast EditMode and PlayMode artifacts from this integration pass are recorded in
`Docs/IntegratedDevelopmentBaseline.md`.

The first verified fast gate after classification completed `398 / 398` in `7.3044694s`; complete
PlayMode completed `27 / 27` in `25.2622715s`. The full baseline remains `416 / 416` because all
18 diagnostic methods stay present for the Full EditMode lane.
