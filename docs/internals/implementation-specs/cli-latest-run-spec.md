# CLI Report — `LATEST RUN` Section

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/Report/**`
**Schema:** `1.12` → `1.13`
**Depends on:** `cli-report-format-spec.md` — D1 (`SubjectDto.ShortName`) and D5 (section heading
machinery, `ReportGlyphs` rule character). **Do not start P1 of this spec until P6 of that one is
merged.**

---

## Amendment protocol

Identical to `cli-report-format-spec.md` §Amendment protocol. This document is ground truth. An
implementer who finds a conflict stops and reports rather than adapting. Decisions and the data
contract change by amendment only, committed before dependent code. **OQ** sections are gates.

---

## 1. Ground truth and problem

### 1.1 What the report cannot currently say

`LocalAnalysisConstants` gates every finding:

```csharp
public const int MinimumSessionsToReport = 5;        // sessions in the window
public const int MinimumSessionsPerTestToReport = 5; // sessions the subject test ran in
```

Both are correct and stay. Their consequence is that a test which passed nineteen times and failed
on the newest run produces **no output at all**, and a developer's first four `xping report`
invocations print `Nothing reportable yet`.

### 1.2 Why this is not a threshold-tuning problem

The report currently answers one question: *what is chronically unreliable over this window?* That
is a pattern claim. It requires evidence, thresholds, and — via the coordinator's
Benjamini-Hochberg pass over `ProviderReport.HypothesesTested` — multiplicity correction.

*What just broke?* is a different question. It is a fact about one session plus a count of prior
ones. No inference, no threshold, no p-value.

Lowering the gates to admit the second question would corrupt the first. A separate section is what
keeps both honest, and it is why this is not a provider (see D1).

### 1.3 What actually earns the lines

Not "a test failed" — `dotnet test` said that thirty seconds ago, and restating it is noise. The
value is entirely the **contrast with history**, which `dotnet test` cannot produce:

> passed the previous 19 runs, failed just now

This yields the governing rule of the whole section (D5): **no row without a contrast.**

### 1.4 Honest scope of the benefit

On a store containing exactly one session, every test is "first seen this run" and the section adds
nothing over the test runner's own output. It is suppressed there (D5).

The section therefore makes `xping report` useful from **run 2**, not run 1 — four runs earlier than
today, not five. Stated here so nobody discovers it in review.

---

## 2. Decisions

### D1 — This is not an `IFindingProvider`

The coordinator applies Benjamini-Hochberg across every fingerprint reported in
`ProviderReport.HypothesesTested`. These rows are not hypotheses. Routing them through a provider
would place them in the ranked finding list, subject them to multiplicity correction, and give them
severities — all three wrong.

Implementation is a standalone component, `LatestRunAnalyzer`, reading `AnalysisContext` and
returning a value object. It is invoked from the report pipeline alongside the coordinator, not
inside it. It is pure: no disk, no clock, no configuration.

### D2 — Envelope gains a `LatestRun` member

See §4. Built by `EnvelopeBuilder` from `LatestRunAnalyzer`'s output, so text and JSON cannot
disagree — the same rule that governs every other display string.

### D3 — Row classification

For each test with a failing **final outcome** in the newest session, its status is decided by its
prior history within the analysis window:

| Status | Condition | Contrast line |
|---|---|---|
| `new` | appeared in ≥1 prior session, never failed in any | `passed the previous N runs, failed just now` |
| `newTest` | no prior appearance in the window | `first seen this run, failed` |
| `seenBefore` | failed in ≥1 prior session, carries no finding | `failed N of M runs — too few to classify yet` |

"Appeared in a session" means recorded at least one execution with a verdict in it. Skipped tests do
not count as appearances and never produce rows.

`new` is the regression signal and is the reason the section exists. `seenBefore` exists so the
section is a complete account of the run rather than a filtered one.

### D4 — Tests already carrying a finding are suppressed, and counted

A test whose fingerprint appears in any emitted finding produces no row. Instead, one line closes
the section:

```
Also failing: 3 tests explained by findings below (#1, #2, #4).
```

Findings are referenced by the row numbers assigned in `cli-report-format-spec.md` D7. This makes
the section a complete account of the latest run at the cost of one line, with no duplication.

The line is omitted when the count is zero.

### D5 — No row without a contrast; single-session suppression

If the analysis window contains exactly one session, the whole section is suppressed — including
the heading and the "also failing" line. There is no history to contrast against, so every row would
restate the test runner.

`LatestRunDto` is still emitted in JSON in that case, with `Suppressed = true` and an empty list, so
a consumer can distinguish "suppressed" from "nothing failed".

### D6 — Environmental collapse

If the newest session's `SessionView.IsLikelyEnvironmental` is true, the section emits no rows.
Instead, one line:

```
Latest run looks environmental: 187 of 210 tests failed. Not itemised.
```

Rationale: the existing environmental heuristic (`EnvironmentalSessionFailureRate = 0.30`,
`EnvironmentalSessionMinFailures = 10`) exists precisely to stop one broken dependency from
poisoning the report. A section that itemised 187 rows would defeat it.

The JSON payload still carries the counts and `IsLikelyEnvironmental = true`, and still carries an
empty `Failures` list. It does not carry 187 suppressed rows.

### D7 — Cap and overflow

At most **10** rows. Overflow is stated on one line:

```
Showing 10 of 23 · all: xping report --latest-run --all
```

The cap is a named constant in `LocalAnalysisConstants`, not a literal.

`--top` does not apply to this section. `--top` governs findings; conflating the two would make one
flag mean two things.

### D8 — Placement, above the findings

The section renders above `NEEDS ATTENTION`, inside the same fence, immediately after the header and
caveat lines.

Rationale: the reader ran the command because something recent happened. The section is empty in the
healthy case, so the top slot costs nothing when there is nothing to say.

### D9 — No severity markers

Rows carry a status word (`new`, `new test`, `seen before`), not `HIGH`/`MED`/`LOW`. These are
observations of one session, not graded claims, and borrowing the severity vocabulary would imply
the same kind of judgement the findings carry.

Colour: the status word may be coloured via `OutputCapabilities`, using a distinct palette from
severity. `new` warrants emphasis; `newTest` and `seenBefore` do not. Exact palette is **OQ-4**.

### D10 — Rows carry no population marker

`ReportVocabulary.PopulationTokenFor` puts a marker on every finding — including `all runs` —
because a reader comparing two rates needs to know which runs each was counted out of, and a marker
printed only where something was discounted cannot distinguish *counted everything* from *not
stated*.

That argument does not reach here. These rows publish no rate and no denominator: a row states a
single session's outcome and a count of prior sessions. There is nothing to compare and nothing to
discount, so a marker would assert a discounting decision that was never made.

An implementer adding one for visual consistency with the findings block is making the report claim
something false. `PopulationRules.For` is not called from this section.

Correspondingly, this spec adds no `FindingKind`, so the exhaustiveness guard
`EveryKindRecordsWhichPopulationItsRatesAreTakenOver` is unaffected.

### D11 — Ordering

1. `new`, ordered by prior-appearance count **descending** — a test that passed nineteen times and
   just failed outranks one that passed six times.
2. `newTest`, ordered by `ShortName` ordinal.
3. `seenBefore`, ordered by prior failure count descending.

Ties break on fingerprint, ordinal. Two runs over one store produce identical output.

### D12 — `--fail-on` is unaffected

A `new` row emits no finding and therefore cannot fail the command. This is deliberate:
`dotnet test` has already failed the build, and overloading a flag whose current meaning is
*severity* with a second meaning of *novelty* would make it unpredictable.

`--fail-on-new` is explicitly **out of scope** and is not stubbed, documented, or reserved.

### D13 — The section works below the finding gates

`MinimumSessionsToReport` does not gate this section. That is the point of it. From two sessions
onward the section emits; findings continue to require five.

When the window has 2–4 sessions, the report renders the `LATEST RUN` section followed by the
existing `EmptyReport()` line, which already explains why nothing else is shown.

### D14 — Retries settle on the final outcome

A test that failed then passed on retry within the newest session has a final outcome of pass and
produces **no row**. Retry-masked failures over the window are already `FindingKind.RetryMasked`;
reporting one run's masked failure here would duplicate that kind at a lower evidential standard.

Confirmed against SDK retry semantics in **OQ-2**.

### D15 — Schema bumps to 1.13

`ReportEnvelope.CurrentSchemaVersion` = `"1.13"`. Added: `LatestRun`.

---

## 3. Target output

### 3.1 Normal case

```
Xping  SampleApp.XUnit  ·  20 runs  ·  2026-09-05 09:00→16:21  ·  main@eab9867
16 tests  ·  10 healthy  ·  6 flagged

LATEST RUN  16:21 · eab9867                                   2 new failures
────────────────────────────────────────────────────────────────────────────

    new          CartTests.Checkout_AppliesDiscount
                 passed the previous 19 runs, failed just now
                 NullReferenceException | CartTests.cs:112

    new test     CartTests.Checkout_RejectsExpiredCoupon
                 first seen this run, failed
                 Assert.Equal() Failure | CartTests.cs:140

    Also failing: 3 tests explained by findings below (#1, #2, #4).

NEEDS ATTENTION (5)                                        most severe first
────────────────────────────────────────────────────────────────────────────

1.  HIGH  always failing
    ...
```

Layout: status word left-aligned in a 13-column field at indent 4; name, contrast, and trailer at
indent 17. Trailer is dim and joined by `" | "`, matching the findings block rather than the header
— the header's `·` separates provenance facts, and reusing it here would make the segments read as
prose.

The trailer carries **failure summary, then location**, and nothing else. There is no evidence
level, no id and no population marker: evidence level qualifies a windowed estimate, an id addresses
a finding, and a population marker asserts a discounting decision — none of which a single-session
observation has. Location stays last so its left-truncated form never opens a line, for the reason
`cli-report-format-spec.md` D8 gives.

### 3.2 Environmental

```
LATEST RUN  16:21 · eab9867                              looks environmental
────────────────────────────────────────────────────────────────────────────

    Latest run looks environmental: 187 of 210 tests failed. Not itemised.
```

### 3.3 Only known failures

```
LATEST RUN  16:21 · eab9867                                  no new failures
────────────────────────────────────────────────────────────────────────────

    Also failing: 3 tests explained by findings below (#1, #2, #4).
```

### 3.4 Clean run

Section absent entirely. No heading, no rule, no line.

---

## 4. Data contract

### 4.1 `ReportEnvelope`

```csharp
internal sealed record ReportEnvelope(
    string SchemaVersion,
    WindowDto Window,
    ContextDto? Context,
    SummaryDto Summary,
    LatestRunDto? LatestRun,   // NEW - null only when the window has no sessions
    IReadOnlyList<FindingDto> Findings,
    TruncationDto Truncated);
```

Positioned before `Findings`, matching render order.

### 4.2 `LatestRunDto`

```csharp
/// <param name="SessionId">The newest analysed session.</param>
/// <param name="StartedAt">When it started. Renders so a stale store is visible.</param>
/// <param name="Sha">Commit it ran at, when one was recorded. Never invented.</param>
/// <param name="IsLikelyEnvironmental">Whether the session itself is suspect - D6.</param>
/// <param name="Suppressed">True when the window holds one session - D5.</param>
/// <param name="TestsExecuted">Tests with a verdict in this session.</param>
/// <param name="TestsFailed">Tests whose final outcome was a failure.</param>
/// <param name="ExplainedByFindings">Failing tests that carry a finding - D4.</param>
/// <param name="ExplainedByFindingIds">Those findings' ids, in envelope order.</param>
/// <param name="Failures">Rows, ordered per D11, after the D7 cap.</param>
/// <param name="FailuresShown">Rows in <paramref name="Failures"/>.</param>
/// <param name="FailuresTotal">Rows before the cap.</param>
/// <param name="OverflowCommand">Invocation showing all of them, when capped.</param>
internal sealed record LatestRunDto(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    bool IsLikelyEnvironmental,
    bool Suppressed,
    int TestsExecuted,
    int TestsFailed,
    int ExplainedByFindings,
    IReadOnlyList<string> ExplainedByFindingIds,
    IReadOnlyList<LatestRunFailureDto> Failures,
    int FailuresShown,
    int FailuresTotal,
    string? OverflowCommand);
```

### 4.3 `LatestRunFailureDto`

```csharp
/// <param name="Status">"new", "newTest" or "seenBefore" - D3.</param>
/// <param name="Subject">Reuses SubjectDto. ShortName comes from the format spec's D1.</param>
/// <param name="Contrast">The already-resolved contrast sentence - D3.</param>
/// <param name="FailureSummary">Exception type, or the runner's assertion header.</param>
/// <param name="PriorSessions">Sessions before this one in which the test appeared.</param>
/// <param name="PriorFailures">Of those, how many it failed in.</param>
internal sealed record LatestRunFailureDto(
    string Status,
    SubjectDto Subject,
    string Contrast,
    string? FailureSummary,
    int PriorSessions,
    int PriorFailures);
```

`Contrast` is resolved in the builder, never in a renderer — same rule as `EvidenceHeadline`, and
for the same reason: two renderers phrasing one measurement is how they end up disagreeing.

`Contrast` is ASCII, matching the `EvidenceHeadline` constraint. The glyph set is chosen after this
runs and never applies to it.

### 4.4 `LocalAnalysisConstants`

```csharp
/// <summary>Rows the latest-run section shows before it says how many it withheld (10).</summary>
public const int LatestRunMaxRows = 10;
```

No other constant is added, changed, or read differently. **This spec changes no threshold and no
finding behaviour.**

### 4.5 Data availability

No SDK change and no new collection. Every input is confirmed present in source:

- `AnalysisContext.SessionViews[0]` — the newest session. `SessionView.Index` is 0 for newest, and
  `IsLikelyEnvironmental`, `Tests` and `Failures` are already computed on it, which is D6 in full.
- `AnalysisContext.Tests.ExecutionsOf(fingerprint)` — every `ExecutionRef` for a test, each carrying
  `.Session` and `.Execution`, which gives prior appearances and prior failures.
- `AnalysisContext.SessionViewFor(sessionId)` — lookup by id.
- The emitted finding list — the D4 suppression set.

The single remaining unknown is whether last-attempt resolution is reachable per-test rather than
only per-session. **OQ-1.**

---

## 5. Open questions — gates

### OQ-1 — Per-test final outcome within one session. **Blocks P1. Narrowed.**

Confirmed present in source, and to be used rather than reinvented:

| Member | Gives |
|---|---|
| `AnalysisContext.SessionViews[0]` | the newest session — `SessionView.Index` is 0 for newest |
| `AnalysisContext.SessionViewFor(sessionId)` | lookup by id |
| `SessionView.IsLikelyEnvironmental` | D6, already computed |
| `SessionView.Tests` / `.Failures` | D6's counts, already computed |
| `AnalysisContext.Tests.ExecutionsOf(fingerprint)` | every `ExecutionRef` for a test, each carrying `.Session` and `.Execution` |
| `SessionOutcomes.Tally(session)` | session-level tally, judged **on last attempt** |

The one remaining unknown: `SessionOutcomes.Tally` returns a session-level `(tests, failures)` pair,
not a per-test verdict. Establish whether the last-attempt resolution inside it is exposed as a
reusable per-test member, or whether only the aggregate is public.

- If a per-test member exists, use it.
- If only the aggregate exists, **extract** the last-attempt resolution into a member both callers
  use. Do not reimplement it in `LatestRunAnalyzer`. Two places deciding what "the test failed in
  this session" means is how they come to disagree, and `SessionView.Failures` is already published
  on that definition.

Any new index belongs in `TestIndex`, per `AnalysisContext`'s rule that anything more than one
provider would derive lives there.

### OQ-2 — **RESOLVED.** No gate.

`SessionView` documents `Failures` as *"how many of them ended it as a failure, judged on their last
attempt"*. Last-attempt resolution therefore already exists and is already the definition the
environmental heuristic runs on.

D14 stands as written, and gains a stronger justification than it had: a retry-rescued test produces
no row not merely because that is tidier, but because `SessionView.Failures` does not count it
either. A section that counted it would disagree with the header line directly above it.

What remains is a plumbing question — whether that resolution is reachable per-test — and it is
folded into OQ-1.

### OQ-3 — What `FailureSummary` should carry. **Blocks P3**

Candidates: exception type alone; exception type plus elided message; the runner's assertion header.
It must fit 51 columns (68 minus the 17-column indent) minus the location segment. Prefer the
narrowest option that identifies the failure — the row's job is the contrast, not the diagnosis.

### OQ-4 — Status-word palette. **Blocks P3**

`OutputCapabilities.Colorize` is keyed on severity strings. Decide whether it gains a second keyed
method or the status words render undecorated. Undecorated is acceptable; a second severity-shaped
palette is not, because it would read as a severity.

### OQ-5 — Does the one-line summary mention new failures? **Blocks P6**

`SummaryReportRenderer` currently emits `Xping: N findings (…) in N runs`. Adding new-failure counts
makes the one-liner more useful in a CI step title, and makes it longer. Recommendation: append
`, 2 new failures` only when non-zero. Confirm.

---

## 6. Implementation sequence

### P0 — Inventory. Read-only. No code.

1. Answer the narrowed **OQ-1**: is last-attempt resolution reachable per-test, or only via
   `SessionOutcomes.Tally`'s aggregate? Name the member, or state that extraction is required.
2. Confirm the §4.5 table against source. Report any divergence as a spec conflict.
3. Confirm `SessionViews` ordering (`AnalysisContext` documents newest-first — verify).
4. Confirm `cli-report-format-spec.md` P6 is merged and the heading machinery exists.
5. List every test that would break under the §4.1 envelope change.
6. Report the shape of the developer fixture store: session count, whether it contains a session
   with a genuinely new failure. If it does not, note that P5 must construct one.

**Stop and report.** Write nothing.

### P1 — `LatestRunAnalyzer`

Pure component: `AnalysisContext` + the emitted finding list in, a value object out. No envelope
wiring, no rendering.

Unit tests, one per branch of D3, D5, D6, D11, D14:
never-failed-before, first-appearance, failed-before-below-gates, already-has-a-finding,
single-session window, environmental newest session, retry-rescued, skipped test,
more than `LatestRunMaxRows` failures, ordering ties.

### P2 — Envelope

`LatestRunDto`, `LatestRunFailureDto`, `ReportEnvelope.LatestRun`. Contrast strings resolved in the
builder. Schema → `1.13`.

JSON output gains the member. **Text output is byte-identical after this phase** — verify against
the P8 golden files from the format spec before proceeding.

### P3 — Text rendering, normal case

Heading, rule, rows, "also failing" line. Reuse the D5 heading machinery from the format spec; do
not add a second heading writer.

Gate on **OQ-3** and **OQ-4**.

Assert: no line exceeds `FenceWidth`.

### P4 — Environmental, suppression, overflow

D5, D6, D7 render paths. Each gets a golden file.

### P5 — Golden files and fixtures

Reproduce §3.1–§3.4 exactly. Construct a fixture store containing a genuine new failure if P0 found
none. Add ASCII-glyph and `--no-color` variants of §3.1.

### P6 — Summary renderer and docs

**OQ-5**. Update the output contract document, the CLI README sample, and the NuGet description
sample if it shows a report.

---

## 7. Out of scope

- `--fail-on-new` or any exit-code change (D12).
- Turning `summary.ExcludedLowEvidence` into a browsable list. That is a list of non-answers: long,
  low-signal, and it invites the reader to treat unsupported rates as findings, which is what the
  evidence gates exist to prevent. It stays a count. If it is ever wanted, it is
  `xping report --watchlist` and its own spec.
- Any change to finding emission, thresholds, or severity.
- A `--latest-run-only` mode. `--runs 1` already produces a report whose only content is this
  section — except that D5 suppresses it, which is correct. Revisit only if asked for.
- Cloud. §8.

---

## 8. Reserved for Cloud

Not implemented. Recorded because this section is where Cloud adds the most value per line, and the
row shape should not have to change to accommodate it.

This is the natural home for caught-divergence. The contrast line is the slot:

```
    new          CartTests.Checkout_AppliesDiscount
                 confidence 0.94 across 812 canonical runs — failed on this branch
                 NullReferenceException | CartTests.cs:112
```

The local contrast (`passed the previous 19 runs`) is replaced, not supplemented — the Cloud
statement is strictly stronger and says the same thing better. Row geometry is unchanged; only the
`Contrast` string differs, and it is already resolved in the builder, so no renderer changes.

Consequence for P2: `LatestRunFailureDto.Contrast` must be the **only** place the contrast sentence
is composed. An implementer who phrases it in the renderer instead will make the Cloud work a
renderer change.
