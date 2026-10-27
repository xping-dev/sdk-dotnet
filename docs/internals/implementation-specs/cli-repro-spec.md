# CLI Report — Repro Command (`repro` on each finding)

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/**`
**Schema:** `1.23` → `1.24`
**Depends on:** `cli-report-format-spec.md` D10 (example/rule authority, width and charset discipline).
`cli-finding-detail-spec.md` D4 (the text detail view) and its Amendments 3 and 4 (commands below the
fence, shell quoting). `cli-test-history-spec.md` Amendment 6B (`History:` below the fence). Issue
#139.
**Blocks:** #271 (the repro line in `report --test`)

---

## Amendment protocol

Identical to `cli-report-format-spec.md`. This document is ground truth. An implementer who finds a
conflict stops and reports rather than adapting. Decisions and the data contract change by amendment
only, committed before dependent code. **OQ** sections are gates.

### Amendment 1 — from the P0 runner checks

P0 re-read §1.1 against source and found no conflict. It ran OQ-1 by hand (results in §5) and found
one conflict, in D3. Resolved here and committed before P1.

| # | Was | Now | Changed |
|---|---|---|---|
| A | D3 escapes `\ ( ) & \| = ! ~` | D3 also escapes `"`, as `\"`. NUnit's stored FQN quotes a string argument (`Echo("x")`). `dotnet test` hands `--filter` to MSBuild as a property, and a bare `"` there fails before VSTest runs: `MSB4177: Invalid property`. `\"` and `%22` both reach VSTest as `"`; `\"` keeps D3 to one escape rule. **Narrowed by Amendment 2B:** VSTest only | D3, §3.2 |

### Amendment 2 — from #287 (runner and project on each session)

#287 records `Xping.Runner` (`VSTest`, `MTP`), `Xping.TestFramework` and `Xping.ProjectPath` on each
local session (`LocalSessionProperties`, `src/Xping.Sdk.Core/Services/LocalStore/`). Its scope says
the D4 amendment belongs to this issue's P0. P0 was rerun for it (OQ-2, §5). Resolved here and
committed before P1 is reworked.

| # | Was | Now | Changed |
|---|---|---|---|
| A | D4: one `dotnet test` command, VSTest only, no project path | The command depends on the newest session in the window. **VSTest:** `dotnet test <path> --no-build -e XPING_ENABLED=false --filter <filter>`. **MTP:** `dotnet run --project <path> --no-build -e XPING_ENABLED=false -- --filter <filter>` (**`--no-build` dropped by Amendment 4D**). `<path>` is `Xping.ProjectPath`, and the command runs from the repository root. With no recorded path, the VSTest form drops `<path>` and the MTP form drops `--project <path>`, and the reader runs it from the project directory, as before. With no recorded runner (the xUnit console runner, a custom host), `repro` is null: a guessed runner prints a command that fails | D4, §1.1, §3, §4.3, §7 |
| B | D3: one escape set | Shared by both runners: `\ ( ) & \| = ! ~` get a backslash. **VSTest adds** `"` as `\"` (Amendment 1A) and `%` as `%25`: `dotnet test` passes the filter through MSBuild, which decodes `%XX`, so `S("50%20off")` became `S("50 off")` and matched nothing. **MTP adds nothing.** `dotnet run` hands the filter straight to the test app: `\"` there fails to parse and throws, and a bare `"` or `%` is read as written | D3, §3.2 |
| C | — | **Rejected for MTP: `dotnet test --project`** (the .NET 10 SDK's MTP mode). On SDK 10.0.201, `-e` did not reach the test app: each filtered run with `-e XPING_ENABLED=false` (and with `-e XPING_MODE=Disabled`) still wrote a session. The mode also needs a `global.json` the CLI cannot see, and without one the same command fails with `MSB1001: Unknown switch`. `dotnet run` starts the app directly, whichever way the suite usually runs | D4 |
| D | — | `Xping.TestFramework` is not read. NUnit, MSTest and xUnit v2 take the same filter on both runners. xUnit v3 (#284) will need it (`--filter-method`) | D4, §7 |

### Amendment 3 — from the P1 review

D2 and D5 sized the count from the wrong runs. Both conflicts lower `p` or raise it past what the
command reruns, so the printed count is wrong in a direction the reader can't see. Resolved here and
committed on the P1 branch before its code.

| # | Was | Now | Changed |
|---|---|---|---|
| A | Single-test kinds: `p` over `SessionsConsidered` | **Replaced by Amendment 4A.** Over `Finding.EvidenceSessions`, the runs the test ran in (environmental runs set aside). `SessionsConsidered` is the window's runs minus environmental ones (`FailureModeProvider.cs:680`), not the test's: a test in 5 of 20 runs that failed in 2 read as 2 of 20, capped, when it is 2 of 5 (6 runs). For a test in every run the two agree, so §3.1 is unchanged. D6's counts are these | D2, D5, D6 |
| B | Cluster kinds: `p` from `SessionsAffected / Sessions`, the whole cluster's runs | From the chosen members' own runs. `ClusterMember` gains `FailingSessions` (runs it failed in with the cluster's signature) and `Sessions` (runs it ran in, `TestIndex.SessionsRunIn`). **Recounted by Amendment 4B.** A subset of the cluster fails in fewer runs than the cluster, so the old count was too low: a member with 5 failures in a cluster affecting 10 of 20 runs printed 5 runs for a rate of at most 25% (11+ runs) | D2, D5, §4.4 |
| C | Members chosen by most failures | By the share of their own runs they failed in, then most failures, then ordinal order. That is the member most likely to fail on a rerun, which is what the command is for. `BrokenFixture` takes the first; `SharedFailure` the first 3, and `p` is the highest of their rates. That is a lower bound on the chance that at least one of them fails, so the count errs towards more runs | D2 |
| D | §4.4: no change to any finding | The evidence payload changes: `members[]` in the envelope gains `failingSessions` and `sessions`. Ranking, emission and every other field are unchanged. Schema 1.24 covers it (§4.2). **Amended by 4A, 4B:** the fields are `runsFailed` and `runsJudged`, and the three single-test evidence records gain them too | §4.2, §4.4 |

---

### Amendment 4 — from the second P1 review

Amendment 3 counted the right test's runs in the wrong way. A run a retry turned green counted as
failing, and a run the test was skipped in counted as a run. Both move `p` away from what the
command will see. And `--no-build` only works in the configuration the suite was built in. Resolved
here and committed on the P1 branch before its code.

| # | Was | Now | Changed |
|---|---|---|---|
| A | Single-test `p` = `SessionsWithFailures` (or `SessionsWithTimeouts`) / `EvidenceSessions` | `RunsFailed / RunsJudged`, two new fields on `FlakyEvidence`, `AlwaysFailingEvidence` and `TimingOutEvidence`. `RunsJudged` is the test's runs with a verdict (`TestIndex.VerdictRunsOf`), environmental runs set aside. `RunsFailed` is those whose deciding attempt failed on the test's own account (a timeout, for `TimingOut`), not in a cluster. `SessionsWithFailures` counts a run where attempt 1 failed and a retry passed; the rerun keeps the retry attribute, so that run comes back green. `EvidenceSessions` counts runs the test was skipped in. A test whose every failure a retry hid gets no `repro`, as D2 says of the retry kinds | D2, D5, D6 |
| B | `ClusterMember.FailingSessions` / `Sessions` (Amendment 3B) | Renamed `RunsFailed` / `RunsJudged`, counted the same way: verdict runs, and those whose deciding attempt failed with the cluster's signature. Clusters still set nothing aside (environmental runs stay in) | D2, §4.2 |
| C | Members chosen per member (Amendment 3C) | Per distinct FQN. MSTest and xUnit data rows share one FQN, so two members could print the same filter clause twice and leave out a third test. The first member of each FQN, in 3C's order, stands for it | D2 |
| D | D4: `--no-build` | Dropped. The store doesn't record the build configuration, and `--no-build` without `-c` fails on a suite built in Release (`test source not found`). An incremental build costs 1–2 s per run. Recording the configuration so the command can carry `-c` is an SDK change, out of scope | D4, D6, §3 |

### Amendment 5 — from P2

D6 and §3.1 printed `44%` for 4 of 9 runs. D6 also says percentages round as the headline rounds
them, and the headline prints a rate at one decimal (`FindingOrder.Round`, then `0.#`): `44.4%`.
Resolved here and committed on the P2 branch before its code.

| # | Was | Now | Changed |
|---|---|---|---|
| A | D6's first row and §3.1 read `44%` | `44.4%`, as the headline prints it. §3.1's headline lines read `(44.4%)` too | D6, §3.1 |

---

### Amendment 6 — from the P2 review

D5 says the counts are the ones D6 prints, so the reader can check them. D6's `runs > 1` sentence
printed only the rate, and `ReproDto` only the capped count. A cluster's rate is its top member's,
not the cluster's: a shared failure read `runs affected 3 of 20` above `at 60% of runs failing`, with
nothing saying the 60% was one member's 3 of 5. And a capped repro carried `runs: null`, leaving the
uncapped count only in an English sentence. Resolved here and committed before the fix.

| # | Was | Now | Changed |
|---|---|---|---|
| A | `runs > 1`: `at 44.4% of runs failing` | `at 4 of 9 runs failing (44.4%)`. The counts are the rerun test's own (D5), so a cluster's line names the member's runs, not the cluster's | D6, §3.1 |
| B | `ReproDto(Command, Runs, Rate, Expectation)` | Adds `RunsNeeded` (D5's count, uncapped), `RunsFailed` and `RunsJudged` (D5's `p`), so a JSON reader never parses the sentence | §4.2 |

---

## 1. Ground truth

### 1.1 What exists

Every claim here was read in source at the cited line.

- **Drill-downs.** `DrillDown` (`src/Xping.Cli/Report/DrillDown.cs`) builds `xping report --id`,
  `--all` and `--test` commands. Every one is a command the CLI accepts today. Values are shell-quoted
  by `DrillDown.Quote` (`:135`): single quotes, left bare when every character is plain.
- **Findings on the envelope.** `FindingDto` (`Report/Contract/ReportEnvelope.cs:355`) carries
  `DrillDown`, built in `EnvelopeBuilder.BuildFinding` (`EnvelopeBuilder.cs:430`). Schema `1.23`
  (`ReportEnvelope.cs:92`).
- **Commands below the fence.** `TextReportRenderer.Render` (`Report/Rendering/TextReportRenderer.cs:177`)
  prints every command after the closing fence, dimmed: `Detail of row N:` and `History:`. Inside the
  fence a long command would wrap or overflow 72 columns, and a wrapped command does not run.
- **Kinds.** 13 (`Report/Model/FindingKind.cs`): `RetryMasked`, `RetryDeepening`, `RetryExhausted`,
  `Flaky`, `AlwaysFailing`, `TimingOut`, `BrokenFixture`, `SharedFailure`, `DurationRegression`,
  `DurationUnstable`, `ParallelSensitive`, `TimeSensitive`, `Vanished`.
- **Rates per kind** (`Report/Providers/FailureModeProvider.cs`):
  - `FlakyEvidence` (`:139`): `SessionsWithFailures` of `SessionsConsidered`, and `FailureRate` over
    **executions**, retry attempts included.
  - `AlwaysFailingEvidence` (`:186`): same fields; the kind needs `FailureRate ≥ 0.90`.
  - `TimingOutEvidence` (`:238`): `SessionsWithTimeouts` of `SessionsConsidered`.
  - `BrokenFixtureEvidence` (`:271`) and `SharedFailureEvidence` (`:298`): `SessionsAffected` of
    `Sessions`, and `Members` as `ClusterMember(Fingerprint, FullyQualifiedName, Failures)` (`:107`).
- **Stored FQN per adapter.** What `TestReference.FullyQualifiedName` holds:
  - MSTest: `{FullyQualifiedTestClassName}.{TestName}`, no arguments (`src/Xping.Sdk.MSTest/XpingTestBase.cs:180`).
  - NUnit: `test.FullName`, **with** arguments for a parameterised case (`src/Xping.Sdk.NUnit/XpingTrackAttribute.cs:230`).
  - xUnit: `{Class.Name}.{Method.Name}`, no arguments (`src/Xping.Sdk.XUnit/XpingMessageSink.cs:317`).
  - xUnit v3: `{TestClassName}.{TestMethodName}`, no arguments (`src/Xping.Sdk.XUnit.V3/XpingMessageSink.cs:343`).
    Its sessions record `Xping.TestFramework = xUnit.v3`.
- **Runner and project per session** (Amendment 2, #287). `EnvironmentInfo.CustomProperties` carries
  `LocalSessionProperties.Runner` (`VSTest` or `MTP`, absent when the SDK could not tell),
  `TestFramework` and `ProjectPath` (repository-relative, `/` separators, absent outside a repository
  or with `GenerateAssemblyInfo=false`). The CLI already reads this dictionary for `ProjectId`
  (`ReportCommand.cs:491`). `EnvironmentInfo.Framework` is the .NET runtime, not the test framework.
- **Turning the SDK off.** `XPING_ENABLED=false` disables collection
  (`docs/configuration/configuration-reference.md:48`). `dotnet test -e NAME=VALUE` and
  `dotnet run -e NAME=VALUE` set a variable for the test process, without shell syntax.
- **No retry switch.** Retries come from framework attributes (`RetryAttributeRegistry`, the three
  `*RetryDetector`s). Nothing turns them off from the command line.

### 1.2 Opened in this document

1. Nothing tells the reader how to make a reported failure happen again.
2. Nothing in the CLI builds a `dotnet test` command.

### 1.3 The question this field answers

`drillDown` answers *show me more about this claim*. `repro` answers *show me it fail*. A single
rerun of a 30% flake passes 70% of the time, and the reader concludes the report is wrong. The report
is the only thing that knows the failure rate, so it is the only thing that can say how many reruns
it takes.

---

## 2. Decisions

Numbered, immutable, amendment-only.

### D1 — Shape

Each finding gains a `repro` field: one `dotnet test` command and how many times to run it.
`drillDown` is unchanged.

The CLI **prints** the command and never runs it. `report` stays read-only over the store: running
tests would write sessions into the window it just analysed, and the CLI cannot know the project,
configuration or environment the suite needs.

**Rejected:** replacing `drillDown`. The two answer different questions, and `drillDown` is how an
agent navigates the report.

### D2 — Which kinds

A kind gets a `repro` only when rerunning the test can show the failure it reports. Otherwise
`repro` is null. A command that cannot show the failure discredits the report more than no command.

| Kind | Filter | Runs |
|---|---|---|
| `Flaky` | the test | D5, from `RunsFailed / RunsJudged` (Amendment 4A) |
| `AlwaysFailing` | the test | 1 |
| `TimingOut` | the test | D5, from `RunsFailed / RunsJudged`, timeouts only (Amendment 4A) |
| `BrokenFixture` | the member that failed in the largest share of its runs (Amendment 3C) | D5, from that member's `RunsFailed / RunsJudged` (Amendments 3B, 4B) |
| `SharedFailure` | up to 3 members with distinct FQNs (Amendment 4C), in that order | D5, from the highest of their `RunsFailed / RunsJudged` |
| `RetryMasked`, `RetryDeepening`, `RetryExhausted` | — | — |
| `DurationRegression`, `DurationUnstable` | — | — |
| `ParallelSensitive`, `TimeSensitive` | — | — |
| `Vanished` | — | — |

Why the dashes:
- **Retry kinds.** The retry attribute stays on, so the loop stays green. No command-line switch
  turns it off (§1.1).
- **Duration kinds.** A loop prints pass/fail, not a spread against the recorded p50.
- **`ParallelSensitive`.** The finding is the difference between two runs (isolated, full
  concurrency), and each framework sets parallelism differently.
- **`TimeSensitive`.** It depends on the time of day, not on running again.
- **`Vanished`.** The useful command is `dotnet test --list-tests`, and VSTest ignores `--filter` when
  listing.

`BrokenFixture` takes one member because a broken fixture fails every test that uses it.
`SharedFailure` takes three because its members share a cause, and three keep the command short.

### D3 — The filter

`FullyQualifiedName=<FQN>`, the stored FQN of D2's test, with the filter's reserved characters
`\ ( ) & | = ! ~` escaped by a backslash. On VSTest, `"` also becomes `\"` and `%` becomes `%25`
(Amendments 1A, 2B). Several members join with `|`.

What this matches, per adapter (§1.1):
- **NUnit:** exactly the stored case. The stored FQN carries the arguments, as NUnit's own does.
- **MSTest, xUnit:** every data row of a data-driven test. The stored FQN has no arguments, and
  neither does the runner's. The failing row runs among them.

**Rejected:**
- `~` (contains). `Calc.Add` also matches `Calc.AddRange`.
- Picking `=` or `~` by whether the name has `(`. The stored FQN already has the runner's shape.

### D4 — The command

By the newest session's `Xping.Runner` (Amendment 2A):

```
VSTest:  dotnet test <path> -e XPING_ENABLED=false --filter <filter>
MTP:     dotnet run --project <path> -e XPING_ENABLED=false -- --filter <filter>
```

No runner recorded: no command, and `repro` is null.

- **`-e XPING_ENABLED=false`.** Repro runs are back-to-back filtered runs of one test. Recorded, they
  would push real runs out of a `--runs` window and skew every session count. `-e` works in every
  shell, where a `VAR=value` prefix does not.
- **No `--no-build`** (Amendment 4D). It assumes the Debug configuration, and the store doesn't
  record which one the suite was built in. An up-to-date build costs 1–2 s per run.
- **`<path>`** is `Xping.ProjectPath`, relative to the repository root, so the command runs from
  there. No recorded path: the VSTest form drops it and the MTP form drops `--project <path>`. The
  reader runs it from the project directory.
- **Newest session.** Its runner and path describe the project as it is now. An older session may
  predate a switch of runner.
- **Quoting.** `<path>` and `<filter>` go through `ShellQuote`, which `DrillDown.Quote` moves into
  so both builders share it.

The command is one line of plain text, with no loop. A loop is shell-specific, and `runs` carries the
count.

### D5 — Repeat count

The runs needed to see at least one failure with probability `ReproConfidence` (0.95), at observed
per-run rate `p`:

```
runs = ceil(ln(1 - 0.95) / ln(1 - p))
```

- **`p` is per session, not per execution.** One `dotnet test` is one session, and the reader judges
  each by its result. `FlakyEvidence.FailureRate` counts retry attempts and would understate the
  count needed.
- **`p` is the point estimate**, not a Wilson bound. On a 20-run window the bound puts 2 failures in
  20 at 107 runs, past the cap. The counts are the ones D6 prints, so the reader can check them.
- **`p` is over the runs the rerun test ran in with a verdict** (Amendments 3, 4A), not the
  window's. A run fails when its deciding attempt failed: a retry that passed leaves the run green,
  as it will on the rerun.
- **Capped at `ReproMaxRuns` (50).** Above it (`p` below ~5.8%), `runs` is null. The command is still
  printed, for a reader who wants to debug a single run.
- `AlwaysFailing` is 1 (D2). Otherwise `runs ≥ 1`.

Both constants go in `LocalAnalysisConstants`. Neither is a user setting.

| `p` | 4 of 9 | 6 of 20 | 10 of 20 | 2 of 20 | 1 of 20 |
|---|---|---|---|---|---|
| `runs` | 6 | 9 | 5 | 29 | null (59) |

**Rejected:**
- A user-set confidence. It would be a public contract, and the report ships no tuning knobs.
- The Wilson lower bound. See above.

### D6 — The expectation line

One sentence, resolved in the envelope like every other display string:

| Case | Text |
|---|---|
| `runs > 1` | `run 6 times: at 4 of 9 runs failing (44.4%), 6 runs show a failure 95% of the time` |
| `runs = 1` | `run once: it failed in 9 of 9 runs` |
| `runs` null | `failed in 1 of 20 runs: too rare to reproduce by rerunning (59 runs needed)` |

Percentages round as the headline rounds them.

### D7 — Where it shows

- **JSON:** on every finding, null for D2's dashes.
- **Text, `--id` view:** a `repro` labelled line in the detail block with D6's sentence, wrapped like
  any value. Below the fence, after `History:`, a dimmed `Repro: <command>`. Below the fence, never
  wrapped, for the same reason as `History:`.
- **Text, `report` list:** unchanged. A finding is one row there, and the reader reaches `--id` in one
  command.
- **`--test` view:** #271.

---

## 3. Target output

Illustrative. Where a block and §2 disagree, §2 wins.

### 3.1 A flaky test

`xping report --id f_1491bf4c`:

````
Xping · SampleApp.MSTest · 9 runs · 2026-08-20 → 2026-08-21 · main@c670fc3
16 tests · 10 healthy · 6 flagged

```
FINDING                                                       row 2 of 6
────────────────────────────────────────────────────────────────────────

2.  HIGH  flaky
    CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently
    failed 4 of 9 executions (44.4%) in 4 of 9 runs, 1 failure mode
    evidence low | f_1491bf4c | .../CalculatorTests.cs:57

    test      SampleApp.MSTest.CalculatorTests.
              FlakyTest_RaceCondition_FailsIntermittently
    assembly  SampleApp.MSTest
    source    tests/SampleApp.MSTest/CalculatorTests.cs:57

    failed          4 of 9 executions (44.4%)
    runs affected   4 of 9
    failure modes   1
    repro           run 6 times: at 4 of 9 runs failing (44.4%), 6 runs
                    show a failure 95% of the time
```

<legend>
Showing 1 of 6 · all: xping report --all
History: xping report --test 5d1e2a0c --assembly SampleApp.MSTest
Repro: dotnet test tests/SampleApp.MSTest/SampleApp.MSTest.csproj -e XPING_ENABLED=false --filter FullyQualifiedName=SampleApp.MSTest.CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently
````

The filter needs no quotes: every character in it is plain (D4).

### 3.2 A parameterised NUnit case

```
Repro: dotnet test tests/SampleApp.NUnit/SampleApp.NUnit.csproj -e XPING_ENABLED=false --filter 'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Divide\(10,0\)'
```

With a string argument (Amendments 1A, 2B), on VSTest and on MTP:

```
Repro: dotnet test tests/SampleApp.NUnit/SampleApp.NUnit.csproj -e XPING_ENABLED=false --filter 'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Echo\(\"50%2520off\"\)'
Repro: dotnet run --project tests/SampleApp.NUnit/SampleApp.NUnit.csproj -e XPING_ENABLED=false -- --filter 'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Echo\("50%20off"\)'
```

No project path recorded:

```
Repro: dotnet test -e XPING_ENABLED=false --filter 'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Divide\(10,0\)'
```

### 3.3 A shared failure

```
Repro: dotnet test tests/Ns.Tests/Ns.Tests.csproj -e XPING_ENABLED=false --filter 'FullyQualifiedName=Ns.A.One|FullyQualifiedName=Ns.B.Two|FullyQualifiedName=Ns.C.Three'
```

### 3.4 Goldens to add

Each in both glyph sets, built through `ReportFixtures`:

- `detail-repro-flaky`: §3.1
- `detail-repro-capped`: `runs` null
- `detail-repro-cluster`: §3.3
- `detail-repro-none`: a retry kind, with no `repro` line and no `Repro:` line
- `detail-repro-mtp`: §3.1 on MTP, the `dotnet run` form
- `detail-repro-no-runner`: §3.1 with no recorded runner, so no `repro` line and no `Repro:` line

Every existing golden whose finding gains a `repro` is regenerated.

---

## 4. Data contract

### 4.1 `FindingDto`

```csharp
internal sealed record FindingDto(
    ...,
    string DrillDown,
    ReproDto? Repro,               // NEW, null for D2's dashes
    CloudTestDto? Cloud = null);
```

### 4.2 `ReproDto`

```csharp
internal sealed record ReproDto(   // NEW
    string Command,                // D4, one line
    int? Runs,                     // D5; null above ReproMaxRuns
    int RunsNeeded,                // D5, uncapped (Amendment 6B)
    int RunsFailed,                // D5's p: runs failed (Amendment 6B)
    int RunsJudged,                //   of runs judged (Amendment 6B)
    double Rate,                   // D5's p, at published precision
    string Expectation);           // D6
```

`CurrentSchemaVersion` = `"1.24"`, with a remarks entry: *1.24 adds `repro` to each finding, and
`runsFailed` and `runsJudged` to each cluster member and to the `Flaky`, `AlwaysFailing` and
`TimingOut` evidence* (Amendments 3D, 4A, 4B).

### 4.3 Small changes

- `ReproCommand.For(Finding, ReproTarget)` in `src/Xping.Cli/Report/`, beside `DrillDown`. Pure, no
  I/O. `ReproTarget(string? Runner, string? ProjectPath)` is read once per report from the newest
  session's `CustomProperties` (Amendment 2A).
- `ShellQuote`, moved out of `DrillDown`.
- `LocalAnalysisConstants.ReproConfidence` (0.95) and `ReproMaxRuns` (50).

### 4.4 Unchanged

Every ranking and every emission rule. **No analysis behaviour changes.** The evidence changes are
Amendments 3D, 4A and 4B.

---

## 5. Open questions — gates

### OQ-1 — Runner behaviour. **Closed by P0** (Amendment 1)

P0 confirms, against the three sample projects under VSTest, before any code:

1. The escape set in D3 is VSTest's, and an escaped NUnit case runs exactly that case.
2. An MSTest `[DataRow]` and an xUnit `[Theory]` filter runs every row.
3. `-e XPING_ENABLED=false` reaches the test host: the run writes no session to `.xping`.
4. A nested class (`Outer+Inner`) matches as stored.

A failure here is an amendment to D3 or D4, not a workaround in code.

**Results.** Run on .NET SDK 10.0.201 with the samples' package versions (MSTest 3.11.1, NUnit 4.2.2 with
NUnit3TestAdapter 4.6.0, xUnit 2.9.2 with xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk
17.12.0). The samples have no `[DataRow]` and no nested class, so the checks used throwaway projects
with the same packages and setup. Each project had `Add` and `AddRange`, two data rows each of
`Divide(int, int)` and `Echo(string)`, and `Outer+Inner.Nested`. A full run with `XPING_LOCAL_STORE`
pointing at a scratch store recorded the FQNs. Each command was built from those FQNs as D3 and D4
say, then run from the project directory in zsh, and once in bash.

| # | Result |
|---|---|
| 1 | NUnit `Divide\(10,0\)` ran that case alone. `Echo("a=b\|c")` **failed** with D3's original set (MSB4177) and passed with `"` escaped (Amendment 1A). With that set, string arguments holding `; % ' $( ) @( ) , \` and a space, a `char` argument and `null` each ran exactly one case. `=` matched `Add` and never `AddRange`, in all three frameworks |
| 2 | MSTest `Calc.Divide` and `Calc.Echo` each ran both rows. Same for xUnit |
| 3 | No filtered run with `-e XPING_ENABLED=false` added a session, in any of the three frameworks. A control run without it added one in each |
| 4 | `Outer+Inner.Nested` is stored with `+` by all three adapters and matched as stored, unquoted |
| — | Three-member `\|` filters (§3.3 shape) ran exactly their members, NUnit and xUnit |

### OQ-2 — Runner behaviour on MTP, with a project path. **Closed by P0** (Amendment 2)

Rerun after #287. Same SDK and packages as OQ-1, except NUnit3TestAdapter 5.2.0 (the samples'
version since #290). The harness was a scratch Git repository importing the shipped
`Xping.Sdk.Core.targets`, with VSTest projects for all three frameworks and MTP projects
(`EnableNUnitRunner`, `EnableMSTestRunner`, `OutputType=Exe`) for NUnit and MSTest. Every run had
`XPING_MODE=LocalOnly`. Commands ran from the repository root.

| Check | Result |
|---|---|
| Properties | Each full run recorded `Xping.Runner` (`VSTest` for `dotnet test`, `MTP` for the executable through `dotnet run`), `Xping.TestFramework`, and `Xping.ProjectPath` as `tests/<fw>/<name>.csproj` |
| VSTest with a path | `dotnet test tests/nunit/Oq2.nunit.csproj --no-build -e … --filter …` ran the selected case from the repository root |
| VSTest `%` | `S("50%20off")` with `%` as written: `No test matches … S("50 off")`. With `%25`: ran that case (Amendment 2B) |
| MTP `dotnet run` | `Divide\(10,0\)`, `Echo\("a\=b\|c"\)`, `S\("50%20off"\)`, `S\("c:\\x"\)`, `S\("it's"\)`, `S\(null\)` each ran exactly one case. MSTest `Calc.Divide` ran both rows. `Outer+Inner.Nested` matched. A two-member `\|` filter ran both |
| MTP `\"` | `Echo\(\"a\=b\|c\"\)` threw from `TestApplication.RunAsync` (Amendment 2B) |
| MTP `-e` | `dotnet run … -e XPING_ENABLED=false` wrote no session, with and without an MTP `global.json`. A control run without `-e` wrote one |
| `dotnet test --project`, MTP mode | With an MTP `global.json`: filters ran, but every run wrote a session despite `-e XPING_ENABLED=false` or `-e XPING_MODE=Disabled`. Without one: `MSB1001: Unknown switch` (Amendment 2C) |

---

## 6. Implementation sequence

Feature branch **`feat/139-repro`**, phase branches `feat/139-p<N>-<slug>`, one PR per phase into the
feature branch, then one PR into `main` with `Closes #139`. Same steps as
`cli-test-history-spec.md` §6.1. Each phase ends green: `dotnet build -c Release` is clean and
`dotnet test tests/Xping.Cli.Tests -c Release` passes.

- **P0 — Inventory, OQ-1 and OQ-2.** Done; see Amendments 1 and 2 and the results in §5. Re-verify
  §1.1. Run the checks by hand against the samples and record the results. Any conflict becomes an
  amendment, committed to the feature branch. No PR.
- **P1 — Builder.** `ShellQuote`, `ReproTarget`, `ReproCommand`, the two constants. Unit tests:
  escaping per runner (`"` and `%` on VSTest only), quoting, both command forms, with and without a
  path, no runner, each D2 row, D5's table, the cap, `AlwaysFailing`. Amendment 3: the test's own
  runs as `p`'s denominator, `ClusterMember`'s two counts in `FailureModeProvider` (provider tests),
  member choice and the cluster rate. Amendment 4: verdict runs and deciding attempts for both
  counts, members by distinct FQN, no `--no-build`.
- **P2 — Contract and text.** `ReproDto`, schema 1.24, `EnvelopeBuilder` (reads `ReproTarget`), the `repro` line and the
  `Repro:` line. Envelope tests, §3.4's goldens, and the existing goldens regenerated.
- **P3 — Runner test.** In `tests/Xping.Sdk.Integration.Tests`, through `SampleRunner`: for one test
  per sample project, on VSTest and, for NUnit and MSTest, on MTP, including a parameterised one each
  and an NUnit string case, run the emitted command once from the repository root and assert the
  runner selected the expected tests and wrote no session. This pins OQ-1 and OQ-2 against framework
  and SDK upgrades.
- **P4 — Docs.**
  - `docs/cli/command-reference.md`: the `repro` field, the two command forms, running from the
    repository root (or the project directory when no path was recorded), and no repro without a
    recorded runner.
  - The CLI README (`nuspec/README.Cli.md`).
  - Any documented sample that `DocumentedSampleTests` checks.

---

## 7. Out of scope

- An executing verb (`xping repro <id>`). If ever built, it sets `XPING_ENABLED=false` for its own
  runs.
- Repro for the retry, duration, parallel, time and vanished kinds (D2).
- xUnit v3 (#284) and its `--filter-method`. Uses `Xping.TestFramework` when it lands (Amendment 2D).
- `dotnet test` in the SDK's MTP mode (Amendment 2C).
- The repro line in `report --test`: #271.

---

## 8. Reserved for Cloud

Nothing.
