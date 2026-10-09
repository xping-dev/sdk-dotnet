# AGENTS.md

Instructions for AI coding agents working in this repo. Keep it short: only what an agent can't
infer from the code. If something here goes stale, fix it in the same change.

## What this is

Xping SDK records .NET test executions (NUnit, xUnit, MSTest) and either keeps them in a local
store or uploads them to Xping Cloud. The `xping` CLI analyzes the local store and reports
flaky, slow, and newly broken tests.

Nothing is released yet. There is no compatibility contract: rename, reshape, or delete public
APIs and stored formats freely. No `[Obsolete]` shims, legacy overloads, or migration code.

## Commands

```sh
dotnet build -c Release                              # warnings are errors
dotnet test tests/Xping.Sdk.Core.Tests -c Release    # one project; see ci.yml for the full list
dotnet test --filter "FullyQualifiedName~Uploader"   # one area
dotnet format --verify-no-changes                    # style check
dotnet run --project src/Xping.Cli -- report         # run the CLI against the local store
```

Before calling a change done: build is clean and the affected test projects pass.

## Layout

| Path | Target | Purpose |
|------|--------|---------|
| `src/Xping.Sdk.Core` | netstandard2.0 | Config, collection, environment/PR detection, local store, upload |
| `src/Xping.Sdk.NUnit` | netstandard2.0 | `[XpingTrack]` attribute + `XpingContext` |
| `src/Xping.Sdk.XUnit` | netstandard2.0 | `XpingTestFramework` (registered via `[assembly: TestFramework]`) |
| `src/Xping.Sdk.MSTest` | netstandard2.0 | `XpingAssemblyInitialize` + `XpingTestBase` (MSTest ≥ 3.8) |
| `src/Xping.Sdk.Shared` | netstandard2.0 | Internal helpers and polyfills; not a public package |
| `src/Xping.Cli` | net10.0 | `xping report / where / clear` (System.CommandLine) |
| `tests/*` | net10.0 | xUnit tests per project, plus integration tests and benchmarks |
| `samples/*` | | One sample app per framework |
| `docs/` | | DocFX site; `docs/internals/implementation-specs/` holds CLI specs |

## Architecture rules

- **`XpingContextOrchestrator`** (Core) owns the session lifecycle: initialize → record → flush
  → finalize. Each adapter's `XpingContext` derives from it and only maps framework events.
  Framework-agnostic logic belongs in Core, not in an adapter.
- **Never break the user's test run.** SDK failures are logged and the SDK degrades to no-op
  (`NoOp*` services). The only exception is `StrictMode`, which must fail the run visibly.
- **Keep the per-test hot path cheap.** `RecordTestExecution` does no I/O, no locking beyond
  the collector, and no allocation-heavy work. Batch and defer everything else.
- **Modes** (`XpingMode`): `Auto` resolves to `Cloud` with an API key (or strict mode), else
  `LocalOnly`. Local history is written before upload, in every collecting mode.
- **Configuration**: `XPING_*` env vars win on every path, including `Initialize(config)`.
  Then `Xping__*` / `appsettings*.json` (or the passed object), then defaults. Every new option needs an env var, a default, validation in `XpingConfigurationValidator`, and
  a row in `docs/configuration/configuration-reference.md`.
- **DI**: services register in `XpingServiceCollectionExtensions`. Public surface is interfaces
  (`IXpingUploader`, `IEnvironmentDetector`, …); implementations live in `Internals/` folders
  and are `internal sealed`.
- **HTTP resilience** uses `Microsoft.Extensions.Http.Resilience`, not raw Polly.
- **Serialization** goes through `IXpingSerializer` (System.Text.Json, camelCase, enums as
  strings). Don't call `JsonSerializer` directly. Exception: the CLI's documented `--json` output
  (`ReportJsonOptions`, `AuthJson`) uses its own options, because it writes nulls and must not move
  with the SDK wire format.
- The product is **Xping Cloud**, never "Dashboard".

## Code style

Enforced by `.editorconfig` and `Directory.Build.props` (`AnalysisMode=All`,
`TreatWarningsAsErrors`, nullable on). Beyond what the analyzers catch:

- Every `.cs` file starts with the header below. `<YEAR>` is the current year when the file is
  created (e.g. `2027`). Don't change the year in existing files.
  ```csharp
  /*
   * © <YEAR> Xping.io. All Rights Reserved.
   * License: [MIT]
   */
  ```
- File-scoped namespaces, `sealed` by default, collection expressions, target-typed `new`.
- Library code targets netstandard2.0: use `ConfigureAwait(false)`, no `.Result`/`.Wait()`, and
  check `Polyfills/` before reaching for a newer BCL API.
- Public members need XML docs (the build enforces it).
- Comments explain *why* — invariants, races, framework quirks. Don't narrate *what*.
- Suppress an analyzer only with a comment saying why.
- Adding a package dependency to a shipped project needs a reason in the PR.

## Tests

- xUnit (`[Fact]`/`[Theory]`) with Moq; one test class per production class, mirroring folders.
- Name tests for the behavior: `Method_Scenario_Expected` or a plain sentence
  (`TheRunnersOwnSourceInformationIsUsedWhenItSuppliedAny`). Both are used.
- Every bug fix gets a regression test that fails without the fix.
- Test time and environment through `ITimeProvider` / `TimeProvider` and
  `IEnvironmentVariableProvider` — no `Thread.Sleep`, no real env vars, no network.
- Adapter behavior that depends on a test runner (vstest shutdown, MSTest cleanup, xUnit
  discovery) needs a test against that framework, not just a mock.

## Workflow

- Branch off `main`; one issue per PR.
- Conventional commits with scope: `fix(sdk): …`, `feat(cli): …`, `docs: …`. Subject says what
  the user sees change, and references the issue: `(#123)`.
- CLI report work follows the specs in `docs/internals/implementation-specs/`. If the code
  needs to diverge, amend the spec first, in the same PR.
- User-visible changes update `docs/` and the relevant adapter `README.md` in the same PR.
- Never commit secrets, `bin/`, `obj/`, `coverage/`, or `docs/_site/`.
