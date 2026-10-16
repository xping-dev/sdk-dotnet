# CLI Report — Test History (`xping report --test <name>`)

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/**`
**Schema:** `1.22` → `1.23`
**Depends on:** `cli-report-format-spec.md` D1 (`ShortName`), D5 (heading machinery), D10 (example/rule
authority, width and charset discipline). `cli-finding-detail-spec.md` D3 (not-found tiers), D6 (flag
interactions), §4 (`SelectionDto`, `FindingSelection`), and its Amendment 6, committed with this
document. Issue #141.
**Blocks:** #270 (failure-mode exemplars, adds a block to this view), #271 (repro line, adds a line to
this view)

---

## Amendment protocol

Identical to `cli-report-format-spec.md`. This document is ground truth. An implementer who finds a
conflict stops and reports rather than adapting. Decisions and the data contract change by amendment
only, committed before dependent code. **OQ** sections are gates.

### Inventory corrections carried into this document

`cli-finding-detail-spec.md` was written against an earlier tree. Where it and source disagree, source
is what this document builds on, and every phase below reads source, not that spec's line numbers.

| Was (detail spec) | Now | Where established |
|---|---|---|
| Schema `1.21` | `1.22`; this spec bumps to `1.23` | `ReportEnvelope.CurrentSchemaVersion` (`src/Xping.Cli/Report/Contract/ReportEnvelope.cs:81`) |
| `--id` validators at `Program.cs:242-265` | `Program.cs:399-448` | `src/Xping.Cli/Program.cs` |
| Selection in `ReportCommand` | Inside `EnvelopeBuilder.Build(..., id, scope)` (its Amendment 2F) | `EnvelopeBuilder.cs:49-75` |
| `DrillDown.ForFinding(id, assembly)` | `DrillDown.ForFinding(id, ReportScope)` | `src/Xping.Cli/Report/DrillDown.cs:44-49` |
| 50 golden files | 82: 41 fixtures × 2 glyph sets | `ReportFixtures.Keys`, `GoldenReportTests.Modes` |

---

## 1. Ground truth

### 1.1 What exists

Every claim here was read in source at the cited line.

- **Three window modes, one read ceiling.** `WindowResolver.Resolve` (`Report/Windowing/WindowResolver.cs:136-147`)
  picks `--since`, then `--runs`, then the default (20 sessions, kept to 14 days). Every read goes
  through `Read(source, maxSessions, assembly)` (`:346`), and the unbounded modes read up to
  `UnboundedReadCeiling` = 1000 (`:121`). `WindowResolution` has `Default`, `Runs`, `SinceSha`,
  `SinceDate` (`AnalysisWindow.cs:13-26`).
- **Retention bounds the store, not the CLI.** `LocalStoreOptions` keeps 50 runs, 50 MB, 30 days
  (`src/Xping.Sdk.Core/Services/LocalStore/LocalStoreOptions.cs:19,24,29`). A whole-store read is
  therefore at most ~50 sessions in the default configuration.
- **Per-test index.** `TestIndex` (`Report/Indexes/TestIndex.cs`) gives `ExecutionsOf` (every attempt,
  newest first, `:96`), `RunsOf` (one deciding attempt per session, `:119`), `SessionsRunIn` (`:161`),
  `ReferenceFor` (newest identity, `:168`), `PositionOf` (`:176`). An execution with no retry metadata
  is attempt 1 (`:473`).
- **Partial runs.** `AnalysisContext` (`Report/AnalysisContext.cs:47-64`) marks a session partial from
  `ReportedPartial` when the framework reported discovered vs. selected counts, else when it holds
  fewer than `PartialSessionShare` (0.5) of the largest session **in the window**. The fallback is
  window-relative.
- **Sha.** `RevisionContext.ReadSha(session)` (`Report/RevisionContext.cs:71`) reads
  `EnvironmentInfo.CustomProperties["Git.SHA"]`. CI runs carry none. The renderer shortens to 7
  characters inline (`TextReportRenderer.cs:721`, `:1234`); there is no shared helper.
- **Identity.** `TestIdentity` carries `Namespace`, `ClassName`, `MethodName` (`src/Xping.Sdk.Core/Models/Executions/TestIdentity.cs:97-113`),
  parsed from the FQN by splitting on `.` (`TestIdentityGenerator.cs:152-176`). `TestReference`
  (`Report/Model/Finding.cs:22-28`) carries the FQN, display name, fingerprint, assembly and source
  location, not the class or method.
- **Store-wide assembly list.** `ISessionSource.KnownAssemblies()` (`Report/Store/SessionSource.cs:49`).
- **Floors are per finding, not per command.** `EvidenceLevelResolver.MeetsReportingFloor`
  (`Report/Scoring/EvidenceLevelResolver.cs:106-108`) needs 5 sessions in the window and 5 for the
  subject. `FindingCoordinator` drops candidates below it (`FindingCoordinator.cs:161-168`). Nothing
  refuses a report for a small window.
- **Duration helpers.** `Quantile.Interpolated` (`Report/Scoring/Quantile.cs:60`) and
  `RobustDispersion.Of` (`Report/Scoring/RobustDispersion.cs:124`) are internal and reusable.
  `DurationProvider`'s profile code is private.
- **Glyphs.** `ReportGlyphs` (`src/Xping.Cli/Reporting/ReportGlyphs.cs:21-48`) has `Pass ✓/+`,
  `Fail ✗/x`, `Skip ○/o`, `Arrow →/->`, `Separator ·/|`, `HorizontalRule ─/-`. No timeout or retry
  glyph.
- **Finding ids.** `FindingId.Compute(kind, subjectKey)` (`Report/Model/FindingId.cs:51`); a single-test
  subject's key is its fingerprint (`Finding.cs:54`).
- **Selection plumbing.** `--id` flows through `ReportOptions.Id`, the validators, `EnvelopeBuilder`,
  `SelectionDto` (`ReportEnvelope.cs:445`), `TextReportRenderer.WriteDetail` (`:416`) and
  `ExitCodes.FindingNotReported` = 3 (`Report/ExitCodes.cs:31`).

### 1.2 Opened in this document

1. Nothing resolves a test by name. There is no FQN, class or method lookup, and no fingerprint-prefix
   lookup.
2. Nothing renders sessions as rows.
3. Nothing reads the whole store as a window.
4. Nothing computes a duration summary for a test that has no duration finding.

### 1.3 The question this flag answers

`report` answers *what is wrong here?* `--id` answers *tell me about this claim*. `--test` answers
*tell me about this test*: every run the store holds for it, what it did in each, and what the report
concludes from them. It works for any test, including one with no findings, and it shows the runs
behind a finding, which is the evidence that would have exposed #140 at a glance.

---

## 2. Decisions

Numbered, immutable, amendment-only.

### D1 — Shape

`xping report --test <name>`, a flag on `report`, keyed on the test. It is a sibling of `--id`, not a
mode of it. A healthy test has no finding id, and `--id` answers a different question.

Flag interactions:

| With | Rule | Reasoning |
|---|---|---|
| `--id` | Parse error | Two selections; neither wins |
| `--kind`, `--top`, `--all` | Parse error | Each narrows or widens a ranked list; this view isn't one |
| `--fail-on` (explicit) | Parse error | A history never fails a build; same rule as `--id` |
| `--summary`, `--format summary` | Parse error | Same as `--id` |
| `--runs`, `--since` | Narrow the window (D5) | Same meaning as in `report` |
| `--assembly` | Restricts lookup to that assembly (D2) | |
| `--directory`, `--project` | Unchanged | Where the store is |
| `--format json`, `--json` | Envelope (§4) | |
| `--ascii`, `--no-color`, `NO_COLOR` | Unchanged | Through `OutputCapabilities.Resolve` |

The error text follows the existing form: `--test and --kind are mutually exclusive.`

**Rejected:** a `test` verb. It was retired by the format spec's Amendment 2, and a verb would duplicate
every window and store flag `report` already parses.

### D2 — Name resolution

`<name>` is matched in three tiers, in order. **The first tier that matches anything wins**, and the
later tiers are not tried.

1. **Exact FQN.** Ordinal, case-sensitive, against `FullyQualifiedName`.
2. **Segment suffix.** The FQN, with any trailing `(args)` removed, ends with `.` + `<name>`, ordinal,
   case-sensitive. This covers `Method`, `Class.Method` and `Namespace.Class.Method` without reading
   `ClassName`. A nested or generic class therefore needs no special case.
3. **Fingerprint prefix.** `<name>` is 8 to 64 hex characters and prefixes the fingerprint,
   case-insensitive.

Matching uses each fingerprint's **newest** identity, so a renamed test is found under its current name.

**Where it looks.** With `--assembly`, only that assembly. Without it, every assembly in the store:
`ResolveTest` reads the store once with no assembly filter, up to `UnboundedReadCeiling` (subject to
the clock-skew drop), and groups executions by `(Assembly, TestFingerprint)`. A single match scopes the
rest of the command to that test's assembly, and its window is then resolved as D5 describes. Nothing
is ever joined across assemblies.

**Rejected:** case-insensitive matching, globs and fuzzy matching. Each makes "matched one test" depend
on what else happens to be in the store, and the ambiguity path (D3) already handles typing less.
**Rejected:** using `report`'s auto-scoping (newest assembly only). A name is a stronger scope than
"whatever ran last", and lookup across every assembly costs one read of a store capped at ~50 sessions.

### D3 — Ambiguity

When the winning tier matches two or more fingerprints, nothing is rendered. Instead:

- stderr lists the candidates, newest run first, up to 10, then `+N more` (§3.3);
- each candidate gives its fingerprint prefix, its assembly and its FQN; the FQN is broken with the
  existing `Break` rules (`TextReportRenderer.cs:556-580`) and fits the fence;
- one ready-to-run line closes the list: `xping report --test <fp8> --assembly <assembly>` for the
  first candidate;
- `--format json` writes the envelope to stdout with `test.candidates` populated and `test.sessions`
  empty;
- the command exits **`4`, `ExitCodes.TestAmbiguous`**.

Parameterised tests are the common case. `--test Add` matches `Add(1,2)` and `Add(2,3)`, by design. The
fingerprint prefix is how a reader names one case.

The prefix shown is 8 characters. If two candidates share 8, both are widened to the shortest length
that tells them apart.

**Rejected:** picking the newest match. The command would silently answer about a different test on
another day.

### D4 — Not found

Exit **`3`**. `ExitCodes.FindingNotReported` is renamed **`ExitCodes.NotFound`**, since it now covers
both selections. One line on stderr, in one of two tiers:

- **(a) No match in the store:**
  `No test matching 'Foo' in this store (3 assemblies, 48 runs).`
  With `--assembly`: `No test matching 'Foo' in SampleApp.MSTest (48 runs).`
- **(b) Matched only outside the narrowed window:** the tiers run against the unwindowed read too, so
  this tier can name the flag:
  `No run of 'Foo' in this window (--runs 5). It ran in 12 earlier runs; drop --runs to see them.`

With `--format json`, stdout carries the envelope with `test: null` and `selection: null`, as `--id`'s
not-reported path writes an envelope (`ReportCommand.cs:127-135`).

### D5 — Window

A new **`WindowResolution.Store`**, the default under `--test`: every session the store holds for the
assembly, read up to `UnboundedReadCeiling`. `--runs N` and `--since` behave exactly as in `report` and
replace it. The clock-skew drop still applies.

The findings block (D10) and the partial marker (D8) are computed over this same window. A narrower
window can change both, which is intended: a window's partial fallback measures against that window's
largest session, and `--runs 3` asks about three runs.

**Rejected:** `report`'s default of 20 runs and 14 days. Once a test is named, the reader wants its
history, and retention has already capped it.

### D6 — Floors

The timeline renders from one session. Nothing about `--test` gates the command on window size.
Findings keep their own floors (§1.1). When the window holds fewer sessions than
`MinimumSessionsToReport`, or the test has run in fewer than `MinimumSessionsPerTestToReport`, the
findings block says why it is empty instead of `no findings` (§3.2):
`too little history to judge: findings need 5 runs, this test has 3`.

`ExitCodes.InsufficientData` (2) is kept for an empty store or an unreadable one, exactly as `report`
uses it.

### D7 — Timeline row

One row per session the test appeared in, newest first, under a `RUNS` heading (format spec D5). The
columns, with widths in display columns:

| Col | Width | Content |
|---|---|---|
| indent | 2 | |
| when | 11 | `MM-dd HH:mm`, UTC, as stored. Same convention as the rest of the report, no label |
| gap | 2 | |
| outcome | 4 | The first attempt's glyph, then the deciding attempt's glyph if they differ: `✗→✓` (ASCII `x->+`). One glyph when they agree |
| gap | 2 | |
| detail | 12 | `attempt N` when the deciding attempt is > 1. Otherwise the outcome word for a deciding outcome other than Passed or Failed: `timeout`, `skipped`, `not run`, `inconclusive`. Both: `timeout @2`. Else blank |
| gap | 2 | |
| duration | 8, right-aligned | Deciding attempt, integer `ms`, as `EvidenceHeadline` prints durations. A longer value widens the row; it still fits (below) |
| gap | 2 | |
| sha | 7 | First 7 characters, or blank |
| gap | 2 | |
| tag | 7 | `partial` when the session view is partial, else nothing |

That is 61 columns, which leaves 11 for a duration overflow inside the 72-column fence. Trailing blanks
are trimmed. Outcome glyphs are colored as the report colors pass and fail. The tag is dimmed.

The year is omitted. Retention caps the store at 30 days, and the heading's period line (§3.1) carries
the full dates.

**Rejected:** a column per attempt. Retry counts are unbounded, and the deciding attempt plus "where it
started" is what a reader acts on. The JSON carries every attempt.

### D8 — Absences

Sessions in the window that do not contain the test are never rows. Each maximal run of consecutive
absent sessions collapses into one line, in its place in the timeline:

```
               ─     absent from 16 runs, 16 partial
```

The `─` (ASCII `-`) sits in the outcome column. The count clause `, N partial` appears only when N > 0.
When N equals the run count, the reader has their answer to #140: the test wasn't deleted, the runs were
filtered.

The JSON lists every session in the window in one `sessions` array, each with `appeared` (§4).

**Rejected:** a row per absent session. On a 50-run window, most of the screen is blank rows.

### D9 — Summary lines

Two lines under the identity, before the timeline:

```
ran in 4 of 20 runs: 0 passed, 4 passed on retry, 0 failed
duration p50 12ms · p95 13ms · range 11-13ms
```

- Counts come from each run's deciding attempt (`TestIndex.RunsOf`). `passed on retry` is a deciding
  Passed with attempt > 1. `failed` covers Failed and Timeout. A run whose deciding attempt is
  Skipped, NotExecuted or Inconclusive still counts in `ran in` and is never an absence, because the
  framework saw the test (OQ-1). Such runs add a `, N other` clause only when N > 0.
- Duration is over the deciding attempt of each run with a verdict, using `Quantile.Interpolated` on
  raw milliseconds. The range is min to max. The duration line is omitted when there are no such runs.
- `RobustDispersion.Of` goes into the JSON as `dispersion` only. It is unitless and reads as noise in
  text.

**Rejected:** reusing `DurationProvider`'s normalised profile. It answers "slow relative to its
session", which is a finding's question, not a history's.

### D10 — Findings block

Under a `FINDINGS` heading, every finding the coordinator produced over the D5 window whose
`FindingOrder.Test` is this fingerprint (`FindingOrder.cs:124`), in report order. Each is rendered as
`report` renders a row, with the subject name omitted, since the view is the subject. The id stays on
the evidence line.

Each finding's `drillDown` must reproduce the window it was found in. `ReportScope` gains the window
resolution, and when it is `Store`, `DrillDown.ForFinding` appends `--runs <window session count>`.
Clusters (`SubjectGroup`) that contain the test are listed too, after single-test findings, with their
member count. Their id resolves under `--id` like any other.

The block's heading annotation reads `N findings`. When there are none, the block reads `no findings`,
or D6's reason when the floor explains it.

**Rejected:** repeating `--id`'s full detail blocks. The view would be several screens long, and each
row's id is one command away.

### D11 — Links between the two views

- `--id`'s detail view gains one line after "Also about this test", for single-test subjects:
  `History: xping report --test <fp8> --assembly <assembly>`, built by a new `DrillDown.ForTest`.
  It repeats `--assembly` and `--directory`, not `--runs` or `--since`: the history view's default
  window is already what a reader following this link wants.
- `--id`'s not-found message gains the third tier that the detail spec's D3 deferred. When no finding
  matches and no same-subject finding is reported, recompute `FindingId.Compute(kind, fingerprint)`
  for every fingerprint in the window and every kind. On a hit the message adds:
  `Its test has no findings in this window: xping report --test <fp8> --assembly <assembly>`.
  `cli-finding-detail-spec.md` Amendment 6 records this.

---

## 3. Target output

Illustrative. Where a block and §2 disagree, §2 wins.

### 3.1 A flaky test with a filtered gap

```
$ xping report --test FlakyTest_PassesOnRetry

TEST                                      SampleApp.MSTest · fp 3a597eb2
────────────────────────────────────────────────────────────────────────

SampleApp.MSTest.CalculatorTests.FlakyTest_PassesOnRetry
source  tests/SampleApp.MSTest/CalculatorTests.cs:42
window  20 runs, 2026-08-20 → 2026-08-21

ran in 4 of 20 runs: 0 passed, 4 passed on retry, 0 failed
duration p50 12ms · p95 13ms · range 11-13ms

RUNS                                                        newest first
────────────────────────────────────────────────────────────────────────

               ─     absent from 16 runs, 16 partial
  08-20 15:35  ✗→✓   attempt 2         12ms  c670fc3
  08-20 15:34  ✗→✓   attempt 2         11ms  c670fc3
  08-20 14:02  ✗→✓   attempt 2         13ms  c670fc3
  08-20 14:02  ✗→✓   attempt 2         11ms  c670fc3

FINDINGS                                                      2 findings
────────────────────────────────────────────────────────────────────────

HIGH  flaky
      failed 4 of 8 executions (50%) in 4 of 20 runs, 1 failure mode
      evidence low | f_8f042eab

MED   masked by retry
      passed on retry 4 times in 4 of 20 runs, up to attempt 2
      evidence low | f_a2b61c2f

Detail of row 1: xping report --id f_8f042eab --assembly SampleApp.MSTest --runs 20
```

**Measured against the rules.**
- **Width:** the widest line is the rule (72).
- **Absences:** the absent line is D8. All 16 runs were partial, so the `stopped running` finding that
  #140 produced is gone. `Vanished` excludes partial runs (`excludesPartialRuns`), and the view shows
  why.
- **Drill-down:** the closing `Detail of row 1:` line is row 1's `drillDown` (D10). It carries
  `--runs 20` because the window is `Store`. It sits below the fence, like `report`'s
  (`TextReportRenderer.cs:1103-1112`), so the 72-column rule doesn't apply to it.

### 3.2 A healthy test

```
$ xping report --test Add_ReturnsSum

TEST                                      SampleApp.MSTest · fp 91c0de44
────────────────────────────────────────────────────────────────────────

SampleApp.MSTest.CalculatorTests.Add_ReturnsSum
source  tests/SampleApp.MSTest/CalculatorTests.cs:18
window  20 runs, 2026-08-20 → 2026-08-21

ran in 20 of 20 runs: 20 passed, 0 passed on retry, 0 failed
duration p50 4ms · p95 6ms · range 3-9ms

RUNS                                                        newest first
────────────────────────────────────────────────────────────────────────

  08-21 09:12  ✓                        4ms  c670fc3
  08-21 09:03  ✓                        5ms  c670fc3
  (18 more rows, elided here only)

FINDINGS
────────────────────────────────────────────────────────────────────────

no findings
```

Every row is rendered; nothing is truncated. A store capped at ~50 runs fits on a scrolled terminal,
and a truncated history would hide the run a reader came for.

### 3.3 An ambiguous name

stderr, exit `4`:

```
'Add' matches 3 tests. Name one by its fingerprint:
  3a597eb2  SampleApp.MSTest
    SampleApp.MSTest.ParameterizedTests.Add(1,2)
  5b1e09fd  SampleApp.MSTest
    SampleApp.MSTest.ParameterizedTests.Add(2,3)
  c4d2aa10  SampleApp.NUnit
    SampleApp.NUnit.CalculatorTests.Add
xping report --test 3a597eb2 --assembly SampleApp.MSTest
```

### 3.4 Not found

stderr, exit `3`:

```
No test matching 'Ad' in this store (3 assemblies, 48 runs).
```

### 3.5 ASCII

§3.1 under `--ascii` or redirected output: `─` becomes `-`, `✗→✓` becomes `x->+`, `·` becomes `|`,
`→` in the period becomes `->`, and the range already uses `-`. Column widths are display columns, so
`x->+` uses the full 4-wide outcome column and the rows stay aligned.

### 3.6 Goldens to add

Each in both glyph sets, built from fixtures through `ReportFixtures`:

- `history-flaky`: §3.1
- `history-healthy`: §3.2
- `history-timeout-retry`: `timeout @2`
- `history-no-sha`: CI runs
- `history-below-floor`: D6
- `history-cluster-member`: D10 clusters
- `history-store`: the real pipeline, as `detail-store` does

The fence sweep in `DetailRenderingTests` covers them.

---

## 4. Data contract

### 4.1 `ReportEnvelope`

```csharp
internal sealed record ReportEnvelope(
    ...,
    SelectionDto? Selection,
    TestHistoryDto? Test,          // NEW, null unless --test
    IReadOnlyList<FindingDto> Findings,
    ...);
```

`CurrentSchemaVersion` = `"1.23"`, with a remarks entry: *1.23 adds `test`, written by `--test`.*

### 4.2 `TestHistoryDto` and its parts

```csharp
internal sealed record TestHistoryDto(                    // NEW
    string Query,                                         // what the reader typed
    SubjectDto? Test,                                     // null when ambiguous or not found
    IReadOnlyList<TestSessionDto> Sessions,               // every session in the window, newest first
    TestRunCountsDto? Counts,
    TestDurationDto? Duration,                            // null when no run has a verdict
    string? FindingsWithheld,                             // D6 reason, else null
    IReadOnlyList<TestCandidateDto> Candidates);          // D3; empty unless ambiguous

internal sealed record TestSessionDto(                    // NEW
    Guid SessionId,
    DateTime StartedAt,
    string? Sha,
    bool Appeared,
    bool Partial,
    TestOutcome? Outcome,                                 // deciding attempt; null when absent
    int? DecidingAttempt,
    long? DurationMs,
    IReadOnlyList<TestAttemptDto> Attempts);              // empty when absent

internal sealed record TestAttemptDto(int Attempt, TestOutcome Outcome, long DurationMs);   // NEW

internal sealed record TestRunCountsDto(                  // NEW
    int Ran, int WindowRuns, int Passed, int PassedOnRetry, int Failed, int Other);

internal sealed record TestDurationDto(                   // NEW
    long P50Ms, long P95Ms, long MinMs, long MaxMs, double Dispersion, int Samples);

internal sealed record TestCandidateDto(                  // NEW
    string FingerprintPrefix, string Assembly, string FullyQualifiedName, DateTime LastSeen, string Command);
```

`SubjectDto` (`ReportEnvelope.cs:410`) is reused, with `Type` set as for a single-test subject, so the
test reads the same here as under a finding.

Under `--test`, `Findings` holds D10's findings, and `Truncation` and `Summary` describe the full report,
as they do under `--id`.

### 4.3 Small changes

- `ReportOptions.Test` (`string?`).
- `ExitCodes`: rename `FindingNotReported` to `NotFound` (3); add `TestAmbiguous` = 4.
- `WindowResolution.Store`; `WindowRequest` gains `bool WholeStore`.
- `ReportScope` gains `WindowResolution Resolution` and `int WindowRuns`.
- `DrillDown.ForTest(fingerprint, scope)`.
- `DrillDown.ForFinding` appends `--runs` under `Store` (D10).

### 4.4 Unchanged

`report` without `--test`, `--id`'s found path and every provider. **No analysis behaviour changes.**
The findings under `--test` are the coordinator's output over a different window, not new analysis.

---

## 5. Open questions — gates

### OQ-1 — Deciding outcome Skipped or NotExecuted. **Closed**

When a run's deciding attempt is `Skipped` or `NotExecuted`, does the run count as `ran in`, with
`Other` incremented, or as an absence?

**Resolved:** it counts as ran. The row shows `skipped` or `not run` in the detail column, and the run
adds to `Other`. A test the framework saw is never an absence, even when it did not run: calling it
one would hide an `[Ignore]` someone left on. D9 records the rule.

### OQ-2 — Timeline length. **Closed**

With a raised `MaxRuns`, a store can hold hundreds of runs. Should the text view cap its rows, with a
`(N older runs)` line and `--runs` to see them, or keep printing every row?

**Resolved:** no cap. Every row in the window is printed, as §3.2 states, and `--runs`/`--since` are
how a reader shortens it. Revisit only by amendment, if the retention defaults change.

---

## 6. Implementation sequence

One prompt per phase. Each phase ends green: `dotnet build -c Release` is clean and
`dotnet test tests/Xping.Cli.Tests -c Release` passes.

- **P0 — Inventory.** Read-only. Re-verify every §1.1 citation. Confirm:
  1. That `SubjectDto` for a single test carries everything §3.1's identity block prints.
  2. How `report` renders a finding row without its subject.
  3. The pass/fail colors in `OutputCapabilities`.

  List any conflict with this document. **Stop and report.** Write nothing.
- **P1 — Window.** `WindowResolution.Store`, `WindowRequest.WholeStore`, and `ReportScope.Resolution`
  and `WindowRuns`. Unit tests in `WindowResolverTests`.
- **P2 — `TestResolver`.** D2's three tiers, the unscoped read, ambiguity with widening prefixes (D3),
  and the not-found tiers (D4). Pure and unit-tested in a new `TestResolverTests`, including
  parameterised FQNs, renamed tests and hex-looking method names.
- **P3 — Contract.** `TestHistoryBuilder` builds §4.2 from `AnalysisContext`, and `EnvelopeBuilder`
  takes it. Schema 1.23. D10's findings filter and `DrillDown` changes. Envelope tests.
- **P4 — CLI surface.** The `--test` option and its validators (D1), the `ExitCodes` changes, and the
  `ReportCommand` paths for found, ambiguous and not found. Command tests against a real temp store, in
  the style of `FindingDetailCommandTests`.
- **P5 — Text.** `TextReportRenderer.WriteHistory` (D7–D10), and `--id`'s history line and third
  not-found tier (D11).
- **P6 — Goldens.** §3.6, in both glyph sets, plus the fence sweep.
- **P7 — Docs.**
  - `docs/cli/command-reference.md`: the flag, its exclusions, and the exit-code table (3 renamed, 4
    added).
  - The CLI README (`nuspec/README.Cli.md`).
  - Any documented sample that `DocumentedSampleTests` checks.

---

## 7. Out of scope

- Failure-mode exemplars (message, frames, first seen, counts): #270, which adds a block to this view.
- The repro line: #271, after #139.
- Executing tests (#139).
- Joining across assemblies. Lookup spans assemblies; the view never does.
- More than one test per invocation, or a filter expression.
- `--new` and delta framing (#143).

---

## 8. Reserved for Cloud

- History beyond local retention. Cloud keeps more than 30 days and 50 runs; a Cloud-backed `--test`
  could extend the timeline past what the local store holds. Not specified here.
