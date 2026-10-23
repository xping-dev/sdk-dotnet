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
| A | D3 escapes `\ ( ) & \| = ! ~` | D3 also escapes `"`, as `\"`. NUnit's stored FQN quotes a string argument (`Echo("x")`). `dotnet test` hands `--filter` to MSBuild as a property, and a bare `"` there fails before VSTest runs: `MSB4177: Invalid property`. `\"` and `%22` both reach VSTest as `"`; `\"` keeps D3 to one escape rule | D3, §3.2 |

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
- **Not recorded.** The store holds no project path and no runner (VSTest or
  Microsoft.Testing.Platform). `EnvironmentInfo.Framework` is the .NET runtime, not the test framework.
- **Turning the SDK off.** `XPING_ENABLED=false` disables collection
  (`docs/configuration/configuration-reference.md:48`). `dotnet test -e NAME=VALUE` sets a variable for
  the test host, without shell syntax.
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
| `Flaky` | the test | D5, from `SessionsWithFailures / SessionsConsidered` |
| `AlwaysFailing` | the test | 1 |
| `TimingOut` | the test | D5, from `SessionsWithTimeouts / SessionsConsidered` |
| `BrokenFixture` | the member with the most failures | D5, from `SessionsAffected / Sessions` |
| `SharedFailure` | up to 3 members, most failures first | D5, from `SessionsAffected / Sessions` |
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

`FullyQualifiedName=<FQN>`, the stored FQN of D2's test, with VSTest's reserved characters
`\ ( ) & | = ! ~` and `"` (Amendment 1A) escaped by a backslash. Several members join with `|`.

What this matches, per adapter (§1.1):
- **NUnit:** exactly the stored case. The stored FQN carries the arguments, as NUnit's own does.
- **MSTest, xUnit:** every data row of a data-driven test. The stored FQN has no arguments, and
  neither does the runner's. The failing row runs among them.

**Rejected:**
- `~` (contains). `Calc.Add` also matches `Calc.AddRange`.
- Picking `=` or `~` by whether the name has `(`. The stored FQN already has the runner's shape.

### D4 — The command

```
dotnet test --no-build -e XPING_ENABLED=false --filter <filter>
```

- **`-e XPING_ENABLED=false`.** Repro runs are back-to-back filtered runs of one test. Recorded, they
  would push real runs out of a `--runs` window and skew every session count. `-e` works in every
  shell, where a `VAR=value` prefix does not.
- **`--no-build`.** About 0.7 s per run instead of a build each time. The expectation line (D6) tells
  the reader to build first.
- **No project path.** The store has none. Run from where `dotnet test` finds the project. From a
  solution root, other test projects match nothing and finish at once.
- **VSTest only.** The store does not record the runner. MTP projects can need different filter flags
  (xUnit v3: `--filter-method`). The docs say so.
- **Quoting.** `<filter>` goes through `DrillDown.Quote`. `Quote` moves to a shared internal
  `ShellQuote` used by both builders.

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
  20 at 107 runs, past the cap. The rate a reader sees in the headline is the rate the count uses.
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
| `runs > 1` | `run 6 times after a build: at 44% of runs failing, 6 runs show a failure 95% of the time` |
| `runs = 1` | `run once after a build: it failed in 9 of 9 runs` |
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
    failed 4 of 9 executions (44%) in 4 of 9 runs, 1 failure mode
    evidence low | f_1491bf4c | .../CalculatorTests.cs:57

    test      SampleApp.MSTest.CalculatorTests.
              FlakyTest_RaceCondition_FailsIntermittently
    assembly  SampleApp.MSTest
    source    tests/SampleApp.MSTest/CalculatorTests.cs:57

    failed          4 of 9 executions (44%)
    runs affected   4 of 9
    failure modes   1
    repro           run 6 times after a build: at 44% of runs failing,
                    6 runs show a failure 95% of the time
```

<legend>
Showing 1 of 6 · all: xping report --all
History: xping report --test 5d1e2a0c --assembly SampleApp.MSTest
Repro: dotnet test --no-build -e XPING_ENABLED=false --filter FullyQualifiedName=SampleApp.MSTest.CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently
````

The filter needs no quotes: every character in it is plain (D4).

### 3.2 A parameterised NUnit case

```
Repro: dotnet test --no-build -e XPING_ENABLED=false --filter 'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Divide\(10,0\)'
```

With a string argument (Amendment 1A):

```
Repro: dotnet test --no-build -e XPING_ENABLED=false --filter 'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Echo\(\"a\=b\|c\"\)'
```

### 3.3 A shared failure

```
Repro: dotnet test --no-build -e XPING_ENABLED=false --filter 'FullyQualifiedName=Ns.A.One|FullyQualifiedName=Ns.B.Two|FullyQualifiedName=Ns.C.Three'
```

### 3.4 Goldens to add

Each in both glyph sets, built through `ReportFixtures`:

- `detail-repro-flaky`: §3.1
- `detail-repro-capped`: `runs` null
- `detail-repro-cluster`: §3.3
- `detail-repro-none`: a retry kind, with no `repro` line and no `Repro:` line

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
    double Rate,                   // D5's p, at published precision
    string Expectation);           // D6
```

`CurrentSchemaVersion` = `"1.24"`, with a remarks entry: *1.24 adds `repro` to each finding.*

### 4.3 Small changes

- `ReproCommand.For(Finding)` in `src/Xping.Cli/Report/`, beside `DrillDown`. Pure, no I/O.
- `ShellQuote`, moved out of `DrillDown`.
- `LocalAnalysisConstants.ReproConfidence` (0.95) and `ReproMaxRuns` (50).

### 4.4 Unchanged

Every provider, every finding, every ranking. **No analysis behaviour changes.**

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

---

## 6. Implementation sequence

Feature branch **`feat/139-repro`**, phase branches `feat/139-p<N>-<slug>`, one PR per phase into the
feature branch, then one PR into `main` with `Closes #139`. Same steps as
`cli-test-history-spec.md` §6.1. Each phase ends green: `dotnet build -c Release` is clean and
`dotnet test tests/Xping.Cli.Tests -c Release` passes.

- **P0 — Inventory and OQ-1.** Done; see Amendment 1 and OQ-1's results. Re-verify §1.1. Run OQ-1's
  checks by hand against the samples and record the results. Any conflict becomes Amendment 1,
  committed to the feature branch. No PR.
- **P1 — Builder.** `ShellQuote`, `ReproCommand`, the two constants. Unit tests: escaping (`"`
  included), quoting, each D2 row, D5's table, the cap, `AlwaysFailing`.
- **P2 — Contract and text.** `ReproDto`, schema 1.24, `EnvelopeBuilder`, the `repro` line and the
  `Repro:` line. Envelope tests, §3.4's goldens, and the existing goldens regenerated.
- **P3 — Runner test.** In `tests/Xping.Sdk.Integration.Tests`: for one test per sample project,
  including a parameterised one each and an NUnit string case, run the emitted command once and assert the runner selected the
  expected tests and wrote no session. This pins OQ-1 against framework upgrades.
- **P4 — Docs.**
  - `docs/cli/command-reference.md`: the `repro` field, the VSTest-only limit, running from the
    project or solution directory.
  - The CLI README (`nuspec/README.Cli.md`).
  - Any documented sample that `DocumentedSampleTests` checks.

---

## 7. Out of scope

- An executing verb (`xping repro <id>`). If ever built, it sets `XPING_ENABLED=false` for its own
  runs.
- Repro for the retry, duration, parallel, time and vanished kinds (D2).
- Microsoft.Testing.Platform filters. Needs the SDK to record the runner: a separate issue.
- A project path in the command. Needs the SDK to record it: a separate issue.
- The repro line in `report --test`: #271.

---

## 8. Reserved for Cloud

Nothing.
