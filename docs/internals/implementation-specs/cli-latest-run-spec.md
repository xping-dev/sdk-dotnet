# CLI Report — `LATEST RUN` Section

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/Report/**`
**Schema:** `1.19` → `1.20`
**Depends on:** `cli-report-format-spec.md`, **as amended by its Amendment 1**. D1
(`SubjectDto.ShortName`), D3 (`SubjectDto.CauseLabel` and the `BuildSubject` signature), D5 (section
heading machinery and the `ReportGlyphs` rule character) and D10 (example/rule authority, width and
charset discipline). **Do not start P1 of this spec until P6 of that one is merged.**

---

## Amendment protocol

Identical to `cli-report-format-spec.md`. This document is ground truth. An implementer who finds a
conflict stops and reports rather than adapting. Decisions and the data contract change by amendment
only, committed before dependent code. **OQ** sections are gates.

### Amendment 1 — following the format spec's Amendment 1

This spec is downstream of `cli-report-format-spec.md` and moves with it. That document's Amendment 1
reached three things here:

| From | Was | Now | Changed |
|---|---|---|---|
| its A | this spec chained `1.12` → `1.13` | chains `1.19` → `1.20` | header, D15, P2 |
| its C | `SubjectDto` gained one new member | it gained two, and `BuildSubject` gained a parameter | §4.3 |
| its D | §3.1 showed `NEEDS ATTENTION (5)` | the heading carries severity bands | D8, §3.1 |

Nothing in §2 is reversed. The decisions here are unchanged in substance; what moved is the version
number, one constructor's shape, and one line of an example.

### Amendment 2 — from the P0 inventory

P0 read this document against source and found two places where the spec and the code disagree.

| Item | Was | Now | Changed |
|---|---|---|---|
| A | "appeared" was defined by the spec and not exposed by any index | `TestIndex` gains a verdict-only view; the analyzer reads that and nothing else | D3, §4.2, §4.5, OQ-1 |
| B | D7 named `xping report --latest-run --all` | `xping report --all`, and `--all` lifts this section's cap as well as the findings cap | D7, §4.2 |

**A.** D3 says a skipped test is not an appearance, and §4.2 says `TestsExecuted` counts tests with
a verdict. Every existing index says otherwise: `TestIndex.RunsOf`, `TestIndex.SessionsRunIn` and
`SessionView.Tests` all count a `Skipped` execution as a run, and the adapters do record skipped
tests. Built on those, a test skipped nineteen times and failed today would read `passed the
previous 19 runs`, which is false. The spec's claim is the right one, so the index moves to it: the
verdict-only view lives in `TestIndex`, per `AnalysisContext`'s rule, and the analyzer never
filters outcomes itself. `SessionView.Tests` is unchanged — it feeds the environmental heuristic
and the header, neither of which this spec touches — so `TestsExecuted` is counted by the
analyzer from the verdict-only view, not copied from `SessionView.Tests`.

**B.** No `--latest-run` flag exists, and §7 rules a latest-run mode out. The overflow line names
the flag that already means "show everything": `xping report --all`. `--all` lifts both caps. One
flag meaning *everything* in both sections is the same argument D7 makes against `--top` meaning
two things.

OQ-1 is resolved by the same inventory and is recorded as such below.

### Amendment 3 — P3 gates

| Item | Decision | Changed |
|---|---|---|
| A | OQ-3: `FailureSummary` is the exception type with its namespace stripped, or null | OQ-3, §4.3 |
| B | OQ-4: status words render undecorated except `new`, which is bold; no colour | OQ-4, D9 |
| C | `LatestRunDto` gains `NewFailures`, the count the heading annotation and the one-liner read | §4.2 |
| D | The heading annotation and the "also failing" line are specified, not only illustrated | D4, D8 |
| E | OQ-5: the one-liner appends `, N new failure(s)` only when non-zero | OQ-5, P6 |
| F | Explaining finding ids are published in envelope order, resolved in the builder | §4.2 |

**A.** `NullReferenceException`, not `System.NullReferenceException`. The row's job is the
contrast; the trailer identifies the failure and the runner's output diagnoses it. The findings'
headline keeps the full type — it is the failure *mode* and a reader groups on it — and the two
are not in tension, because a trailer is not a headline. Resolved in the builder like every other
display string. Null when the adapter recorded no type; the trailer then carries the location
alone, or is omitted.

**B.** Colour is what the report uses for severity, and a coloured status word would read as one.
Bold is not in the severity vocabulary. `new` is the regression signal and is the one word
emphasised; `new test` and `seen before` are not. `OutputCapabilities` gains `Emphasis(text)`,
which is the identity function when colour is off, so `--no-color` and piped output are unchanged.

**C.** The annotation says `2 new failures`, and the DTO before this amendment could not supply
that number: `Failures` is the capped list and `new` rows sort first, so a run with twelve new
failures would have shown ten and counted ten. `NewFailures` counts rows whose status is `new` or
`newTest`, before the cap, and is positioned after `TestsFailed`. It is also what OQ-5 reads in P6.

**E.** The spec's own recommendation, applied. Suppressed and environmental runs append nothing:
the first has no news and the second's news is not a count of tests.

**F.** The analyzer meets findings in the order of the tests they explain, and the builder is where
envelope order is known — `FindingOrder.WithSiblingsAdjacent` runs there — so the ids are sorted
there, by position in the ordered list. The store-driven golden in P5 is what caught `(#2, #1)`.

**D.** The heading's right-hand annotation is `{NewFailures} new failure(s)` when the count is
non-zero, `no new failures` when it is zero, and `looks environmental` under D6. The "also failing"
line references findings by the row numbers they hold in the rendered list; a finding cut by
`--top` has no row and is not referenced. When every explaining finding was cut, the line reads
`Also failing: N tests explained by findings not shown.` — the truncation footer already names the
command that shows them.

### Amendment 4 — from the P6 review

Six defects, four of which are decisions §2 never made and which the implementation therefore had
to invent.

| Item | Was | Now | Changed |
|---|---|---|---|
| A | the "also failing" line was composed by concatenation | wrapped like every other line in the section | D4 |
| B | an empty finding list printed `No findings.` under a listed regression | the clean-bill line is suppressed when the section reported failures | D13 |
| C | a test's prior failures counted runs flagged environmental | they are excluded, as every provider excludes them | D3, D6 |
| D | `--kind` silently changed which rows appear | stated: the section defers only to the kinds asked for | D4 |
| E | a `--top` cut left the count and the row numbers disagreeing | the line says which findings are not shown | D4 |
| F | `seenBefore` asserted *too few to classify yet* | it states the count and claims no reason | D3 |

**A.** Five explaining findings produce a 75-column line, three past `FenceWidth`. Every other line
in the section goes through `Wrap`, `Fit` or `FitPath`; this one went through none. The golden that
would have caught it has exactly three.

**B.** `EmptyReport`'s `No findings.` carries the pass glyph, and it answers *why is this block
empty*. With rows above it the block is not empty and the suite is not clean, so a green tick under
a regression is the exact misreading `#185` was filed about. The line is withheld only in that
form: `Nothing reportable yet: 4 need more runs` stays, because it explains candidates the report
withheld and is true either way.

**C.** Every provider drops a session `SessionView.IsLikelyEnvironmental` flags before reading a
test's history — six call sites — and D6 says why: one broken dependency must not be counted
against each test it took down. The analyzer counted them, so one outage in the window demoted a
genuine regression from `new` to `seenBefore`, zeroed `NewFailures`, and made the heading read
`no new failures`. **`PriorSessions` and `PriorFailures` are counted over the prior runs that were
not flagged environmental**, which also means a test seen only in such runs is a `newTest`. D6's
rule was written for the newest session; it holds for the rest of the window for the same reason.

**D.** `analysis.Findings` is already narrowed by `--kind`, so a test whose only finding was
filtered out stops being *explained* and becomes a row. That is not wrong — with F applied the row
states a true thing — but it was undocumented, and a section billed as a complete account of the
run must say what it defers to. **`--kind` narrows what the section defers to, not what it
reports.** Running the providers twice to recover the unfiltered set would cost the whole analysis
a second time to change one line of prose.

**E.** `ExplainedByFindings` is counted over every finding produced and the row numbers resolve
against the findings shown, so a partial cut printed `2 tests explained by finding below (#1)`.
Three forms now, and the count is always over every finding:

```
    Also failing: 2 tests explained by findings below (#1, #2).
    Also failing: 2 tests explained by findings not shown.
    Also failing: 2 tests explained by findings below (#1) and others not shown.
```

**F.** *Too few to classify yet* names an evidence gate, and a test with twenty runs and eight
failures whose hypothesis lost the Benjamini-Hochberg correction — or whose provider threw — has
cleared every gate there is. The row cannot know why no finding exists and must not guess:
`failed 9 of 21 runs` and nothing more. Counted inclusive of this run, because that is the history
the reader is being handed.

### Amendment 5 — the cap line carries no command

Superseded by `cli-finding-detail-spec.md` Amendment 3. The cap line inside the fence is
`Showing 10 of 23`; the command that lifts the cap is the report's one truncation line below the
fence, which now counts the failures it withheld. `LatestRunDto.OverflowCommand` is removed. D7's
block and §4's `OverflowCommand` parameter are read accordingly.

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
is a pattern claim. It needs evidence, thresholds, and, via the coordinator's Benjamini-Hochberg
pass over `ProviderReport.HypothesesTested`, multiplicity correction.

*What just broke?* is a different question. It is a fact about one session plus a count of prior
ones. No inference, no threshold, no p-value.

Lowering the gates to admit the second question would corrupt the first. A separate section is what
keeps both honest, and it is why this is not a provider (D1).

### 1.3 What earns the lines

Not "a test failed". `dotnet test` said that thirty seconds ago, and restating it is noise. The
value is entirely the **contrast with history**, which `dotnet test` cannot produce:

> passed the previous 19 runs, failed just now

This yields the governing rule of the section (D5): **no row without a contrast.**

### 1.4 Honest scope of the benefit

On a store containing exactly one session, every test is "first seen this run" and the section adds
nothing over the test runner's own output. It is suppressed there (D5).

The section therefore makes `xping report` useful from **run 2**, not run 1: four runs earlier than
today, not five. Stated here so nobody discovers it in review.

---

## 2. Decisions

### D1 — This is not an `IFindingProvider`

The coordinator applies Benjamini-Hochberg across every fingerprint reported in
`ProviderReport.HypothesesTested`. These rows are not hypotheses. Routing them through a provider
would place them in the ranked finding list, subject them to multiplicity correction, and give them
severities. All three wrong.

Implementation is a standalone component, `LatestRunAnalyzer`, reading `AnalysisContext` and
returning a value object. It is invoked from the report pipeline alongside the coordinator, not
inside it. It is pure: no disk, no clock, no configuration.

### D2 — Envelope gains a `LatestRun` member

See §4. Built by `EnvelopeBuilder` from `LatestRunAnalyzer`'s output, so text and JSON cannot
disagree. The same rule that governs every other display string.

### D3 — Row classification

For each test with a failing **final outcome** in the newest session, its status is decided by its
prior history within the analysis window:

| Status | Condition | Contrast line |
|---|---|---|
| `new` | appeared in a prior session, never failed in one | `passed the previous N runs, failed just now` |
| `newTest` | no prior appearance in the window | `first seen this run, failed` |
| `seenBefore` | failed before, carries no finding | `failed N of M runs` (Amendment 4F) |

"Appeared in a session" means recorded at least one execution with a verdict in it — `Passed`,
`Failed` or `Timeout`. Skipped tests do not count as appearances and never produce rows.

**Amendment 4C.** A prior run the environmental heuristic flagged is not counted, in either
column. The analyzer asks `AnalysisContext.SessionViewFor` exactly as the providers do, so one bad
afternoon cannot turn a regression into a test that has failed before.

**Amendment 2A.** That definition is read from `TestIndex`, not applied in the analyzer.
`TestIndex.RunsOf` and `SessionsRunIn` count every recorded execution, skipped included, and stay
that way; the index gains a verdict-only sibling — one entry per session in which the test recorded
a verdict, represented by its deciding attempt — and this section reads only that. An analyzer that
filtered `RunsOf` by outcome itself would be a second definition of "appeared", which is what
`AnalysisContext`'s rule exists to prevent.

`new` is the regression signal and is why the section exists. `seenBefore` exists so the section is
a complete account of the run rather than a filtered one.

Contrast strings are ASCII (§4.3). Commas, not dashes.

### D4 — Tests already carrying a finding are suppressed, and counted

A test whose fingerprint appears in any emitted finding produces no row. Instead, one line closes
the section:

```
    Also failing: 3 tests explained by findings below (#1, #2, #4).
```

Findings are referenced by the row numbers assigned in `cli-report-format-spec.md` D7. This makes
the section a complete account of the latest run at the cost of one line, with no duplication. The
line is omitted when the count is zero.

### D5 — No row without a contrast; single-session suppression

If the analysis window contains exactly one session, the whole section is suppressed: heading, rule
and the "also failing" line included. There is no history to contrast against, so every row would
restate the test runner.

`LatestRunDto` is still emitted in JSON in that case, with `Suppressed = true` and an empty list, so
a consumer can distinguish *suppressed* from *nothing failed*.

### D6 — Environmental collapse

If the newest session's `SessionView.IsLikelyEnvironmental` is true, the section emits no rows.
Instead, one line:

```
    Looks environmental: 187 of 210 tests failed. Not itemised.
```

Rationale: the existing heuristic (`EnvironmentalSessionFailureRate = 0.30`,
`EnvironmentalSessionMinFailures = 10`) exists precisely to stop one broken dependency from
poisoning the report. A section that itemised 187 rows would defeat it.

The JSON payload still carries the counts and `IsLikelyEnvironmental = true`, and an empty
`Failures` list. It does not carry 187 suppressed rows.

### D7 — Cap and overflow

At most **10** rows. Overflow is stated on one line:

```
    Showing 10 of 23 · all: xping report --all
```

The cap is a named constant in `LocalAnalysisConstants`, not a literal.

`--top` does not apply to this section. `--top` governs findings; conflating the two would make one
flag mean two things.

`--all` applies to both. **Amendment 2B**: an earlier draft printed `xping report --latest-run
--all`, naming a flag that does not exist and a mode §7 rules out. `--all` already means *show
everything the report withheld*, and that is one meaning whether the withholding was a finding cap
or this one. The overflow line is `DrillDown.ForFullReport()`, the same string the findings
truncation line prints, so the two cannot drift.

### D8 — Placement, above the findings

The section renders above `NEEDS ATTENTION`, inside the same fence, immediately after the header and
caveat lines.

Rationale: the reader ran the command because something recent happened. The section is empty in the
healthy case, so the top slot costs nothing when there is nothing to say.

**What "the same heading machinery" means, after the format spec's Amendment 1.** That amendment put
the severity bands inside `NEEDS ATTENTION`'s parenthesis. The shared machinery is the *shape* — an
uppercase label, a right-aligned annotation at `FenceWidth`, a `ReportGlyphs.HorizontalRule` rule
beneath — and not the content of either annotation. This heading's label carries the session's time
and sha, and its right-hand annotation is a failure count (`2 new failures`). It has no parenthesis
and never calls `ReportVocabulary.SeverityBands`: these rows are not graded, which is D9.

### D9 — No severity markers

Rows carry a status word (`new`, `new test`, `seen before`), not `HIGH`/`MED`/`LOW`. These are
observations of one session, not graded claims, and borrowing the severity vocabulary would imply
the same kind of judgement the findings carry.

Colour: the status word may be coloured via `OutputCapabilities`, using a distinct palette from
severity. `new` warrants emphasis; `newTest` and `seenBefore` do not. Exact palette is **OQ-4**.

### D10 — Rows carry no population marker

`ReportVocabulary.PopulationTokenFor` puts a marker on every finding, including `all runs`, because
a reader comparing two rates needs to know which runs each was counted out of, and a marker printed
only where something was discounted cannot distinguish *counted everything* from *not stated*.

That argument does not reach here. These rows publish no rate and no denominator: a row states a
single session's outcome and a count of prior sessions. There is nothing to compare and nothing to
discount, so a marker would assert a discounting decision that was never made.

An implementer adding one for visual consistency with the findings block is making the report claim
something false. `PopulationRules.For` is not called from this section.

Correspondingly, this spec adds no `FindingKind`, so the exhaustiveness guard
`EveryKindRecordsWhichPopulationItsRatesAreTakenOver` is unaffected.

### D11 — Ordering

1. `new`, ordered by prior-appearance count **descending**. A test that passed nineteen times and
   just failed outranks one that passed six times.
2. `newTest`, ordered by `ShortName` ordinal. Under the format spec's D1 as amended, `ShortName`
   may carry an argument list, so two cases of one parameterised method sort by their arguments as
   the adapter spelled them. That is deterministic, which is all this rule needs.
3. `seenBefore`, ordered by prior failure count descending.

Ties break on fingerprint, ordinal. Two runs over one store produce identical output.

### D12 — `--fail-on` is unaffected

A `new` row emits no finding and therefore cannot fail the command. This is deliberate: `dotnet
test` has already failed the build, and overloading a flag whose current meaning is *severity* with
a second meaning of *novelty* would make it unpredictable.

`--fail-on-new` is explicitly **out of scope** and is not stubbed, documented, or reserved.

### D13 — The section works below the finding gates

`MinimumSessionsToReport` does not gate this section. That is the point of it. From two sessions
onward the section emits; findings continue to require five.

When the window has 2 to 4 sessions, the report renders the `LATEST RUN` section followed by the
existing `EmptyReport()` line, which already explains why nothing else is shown.

### D14 — Retries settle on the final outcome

A test that failed then passed on retry within the newest session has a final outcome of pass and
produces **no row**. Retry-masked failures over the window are already `FindingKind.RetryMasked`;
reporting one run's masked failure here would duplicate that kind at a lower evidential standard.

`SessionView` documents `Failures` as counting tests judged on their last attempt, so a section that
counted a retry-rescued test would disagree with the header line directly above it.

### D15 — Schema bumps to 1.20

`ReportEnvelope.CurrentSchemaVersion` = `"1.20"`. Added: `LatestRun`.

**Amended from `1.13`.** Both specs were drafted against `1.11`; the envelope had shipped `1.18`
before either was read against source. The format spec's Amendment 1 takes it to `1.19`, so this
one is `1.20`. The chain, not the numbers, is what this decision fixes: this spec's bump is always
the one after the format spec's, and it is applied in P2 after that spec's P1 has landed.

---

## 3. Target output

**`cli-report-format-spec.md` D10 applies here unchanged**: §2 outranks §3, goldens are generated
rather than authored, every fenced line is width-asserted, and non-ASCII characters appear only
where a `ReportGlyphs` pair already exists. The blocks below are measured to 72 columns; the header
line is the existing `WriteHeader` output and is outside that rule.

### 3.1 Normal case

```
Xping · SampleApp.XUnit · 20 runs · 2026-09-05 09:00 → 16:21 · main@eab9867
16 tests · 10 healthy · 6 flagged

LATEST RUN  16:21 · eab9867                               2 new failures
────────────────────────────────────────────────────────────────────────

    new          CartTests.Checkout_AppliesDiscount
                 passed the previous 19 runs, failed just now
                 NullReferenceException | CartTests.cs:112

    new test     CartTests.Checkout_RejectsExpiredCoupon
                 first seen this run, failed
                 Assert.Equal() Failure | CartTests.cs:140

    Also failing: 3 tests explained by findings below (#1, #2, #4).

NEEDS ATTENTION (5 high)                               most severe first
────────────────────────────────────────────────────────────────────────

1.  HIGH  always failing
    ...
```

Layout: status word left-aligned in a 13-column field at indent 4; name, contrast and trailer at
indent 17. Trailer is dim and joined by `" | "`, matching the findings block rather than the header:
the header's `·` separates provenance facts, and reusing it here would make the segments read as
prose.

The trailer carries **failure summary, then location**, and nothing else. There is no evidence
level, no id and no population marker. Evidence level qualifies a windowed estimate, an id addresses
a finding, and a population marker asserts a discounting decision, none of which a single-session
observation has. Location stays last so its left-truncated form never opens a line, for the reason
`cli-report-format-spec.md` D8 gives.

### 3.2 Environmental

```
LATEST RUN  16:21 · eab9867                          looks environmental
────────────────────────────────────────────────────────────────────────

    Looks environmental: 187 of 210 tests failed. Not itemised.
```

### 3.3 Only known failures

```
LATEST RUN  16:21 · eab9867                              no new failures
────────────────────────────────────────────────────────────────────────

    Also failing: 3 tests explained by findings below (#1, #2, #4).
```

### 3.4 Clean run

Section absent entirely. No heading, no rule, no line.

### 3.5 What the goldens say

Per D10 the goldens are authority. `latest-run`, `latest-run-known-only`, `latest-run-environmental`,
`latest-run-overflow`, `latest-run-suppressed` and `latest-run-store` under
`tests/Xping.Cli.Tests/Report/Goldens/` are the rendering of §2, and §3.1 differs from
`latest-run.unicode.txt` in two places, both by decision: the second trailer reads
`EqualException` and not `Assert.Equal() Failure` (Amendment 3A), and the closing line reads
`5 tests explained by findings below (#1, #2, #3)` because the block beneath it is the format
spec's §3, whose third row is the cluster. §3.2 and §3.3 render as shown; §3.4 is
`latest-run-suppressed`, byte-identical to `spec-section-3`.

`latest-run-store` is the section as the whole pipeline produces it from twenty sessions. It shows
one thing §1.1 does not: a test that passed nineteen times and failed once is a `Flaky` finding on a
twenty-run window, so it appears under *also failing* and not as a `new` row. D4 is working as
written; whether one failure in twenty should be *flaky* is a question for the provider and outside
this spec (§7).

---

## 4. Data contract

### 4.1 `ReportEnvelope`

```csharp
internal sealed record ReportEnvelope(
    string SchemaVersion,
    WindowDto Window,
    ContextDto? Context,
    SummaryDto Summary,
    LatestRunDto? LatestRun,   // NEW - null only if no sessions
    IReadOnlyList<FindingDto> Findings,
    TruncationDto Truncated);
```

Positioned before `Findings`, matching render order.

### 4.2 `LatestRunDto`

```csharp
/// <param name="SessionId">The newest analysed session.</param>
/// <param name="StartedAt">When it started. Renders so a stale store is visible.</param>
/// <param name="Sha">Commit it ran at, when one was recorded. Never invented.</param>
/// <param name="IsLikelyEnvironmental">Whether the session itself is suspect (D6).</param>
/// <param name="Suppressed">True when the window holds one session (D5).</param>
/// <param name="TestsExecuted">Tests with a verdict in this session (Amendment 2A: counted from the verdict-only view, not SessionView.Tests).</param>
/// <param name="TestsFailed">Tests whose final outcome was a failure.</param>
/// <param name="NewFailures">Rows whose status is new or newTest, before the cap (Amendment 3C).</param>
/// <param name="ExplainedByFindings">Failing tests that carry a finding (D4).</param>
/// <param name="ExplainedByFindingIds">Those findings' ids, in envelope order.</param>
/// <param name="Failures">Rows, ordered per D11, after the D7 cap.</param>
/// <param name="FailuresShown">Rows in <paramref name="Failures"/>.</param>
/// <param name="FailuresTotal">Rows before the cap.</param>
/// <param name="OverflowCommand">Invocation showing all of them, when capped: DrillDown.ForFullReport() (Amendment 2B).</param>
internal sealed record LatestRunDto(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    bool IsLikelyEnvironmental,
    bool Suppressed,
    int TestsExecuted,
    int TestsFailed,
    int NewFailures,
    int ExplainedByFindings,
    IReadOnlyList<string> ExplainedByFindingIds,
    IReadOnlyList<LatestRunFailureDto> Failures,
    int FailuresShown,
    int FailuresTotal,
    string? OverflowCommand);
```

### 4.3 `LatestRunFailureDto`

```csharp
/// <param name="Status">"new", "newTest" or "seenBefore" (D3).</param>
/// <param name="Subject">Reuses SubjectDto. ShortName comes from the format spec's D1.</param>
/// <param name="Contrast">The already-resolved contrast sentence (D3).</param>
/// <param name="FailureSummary">Exception type, namespace stripped, or null (Amendment 3A).</param>
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

**`Subject` is always a single-test `SubjectDto`.** Per the format spec's D3 as amended, `ShortName`
is populated and `CauseLabel` is null — no latest-run row has a group subject, because a row is one
test's outcome in one session. That amendment also gave `EnvelopeBuilder.BuildSubject` a
`FindingEvidence` parameter so a group's cause can be resolved; these rows have no evidence to pass
and the parameter is null here. An implementer who finds that awkward should note that the
alternative — resolving `CauseLabel` inside `BuildSubject` — is the conflict that amendment exists
to fix, and must not reintroduce it.

`Contrast` is resolved in the builder, never in a renderer. Same rule as `EvidenceHeadline`, and for
the same reason: two renderers phrasing one measurement is how they end up disagreeing.

`Contrast` is ASCII, matching the `EvidenceHeadline` constraint, because the glyph set is chosen
after this runs and never applies to it. That rules out the em dash, which earlier drafts of D3
used: piped output is asserted to be entirely below `0x80` by
`APipedReportCarriesNothingButTheReport`, and a contrast string resolved in the builder cannot be
glyph-switched later.

### 4.4 `LocalAnalysisConstants`

```csharp
/// <summary>Rows the latest-run section shows before it says how many it withheld (10).</summary>
public const int LatestRunMaxRows = 10;
```

No other constant is added, changed, or read differently. **This spec changes no threshold and no
finding behaviour.**

### 4.5 Data availability

No SDK change and no new collection. Every input is confirmed present in source:

| Member | Gives |
|---|---|
| `AnalysisContext.SessionViews[0]` | the newest session; `SessionView.Index` is 0 for newest |
| `SessionView.IsLikelyEnvironmental` | D6, already computed |
| `SessionView.Tests` / `.Failures` | D6's counts, already computed |
| `AnalysisContext.SessionViewFor(sessionId)` | lookup by id |
| `AnalysisContext.Tests.RunsOf(fingerprint)` | one `ExecutionRef` per session, the deciding attempt, newest first — counts skipped runs |
| `AnalysisContext.Tests` verdict-only view (**new, Amendment 2A**) | the same, restricted to sessions in which the test recorded a verdict |
| `SessionOutcomes.Tally(session)` | session tally, judged on last attempt |

The emitted finding list supplies the D4 suppression set. Last-attempt resolution is per-test
already (**OQ-1**, resolved); the one addition is the verdict-only view, which is the P1 change to
`TestIndex`.

`AnalysisContext.SessionViewFor` takes a `Guid`, not a string. `LatestRunDto.SessionId` is the
serialised form.

---

## 5. Open questions — gates

### OQ-1 — **RESOLVED** by P0. No gate.

Asked whether last-attempt resolution is reachable per-test. It is: `TestIndex.RunsOf(fingerprint)`
returns one `ExecutionRef` per session, the deciding attempt, and its documentation binds it to
`SessionOutcomes` by contract. `SessionOutcomes.FinalOutcomes` also exists but is private and is
not needed. Nothing is extracted.

What P0 did find is that `RunsOf` counts skipped runs, which is Amendment 2A: the verdict-only
sibling is the one addition to `TestIndex`, and it is where D3's definition of "appeared" lives.

### OQ-2 — **RESOLVED.** No gate.

Asked how a retried test's session outcome is recorded. `SessionView` documents `Failures` as
counting tests judged on their last attempt, so last-attempt resolution already exists and is
already the definition the environmental heuristic runs on. D14 stands, with the stronger
justification now folded into it. The plumbing half is OQ-1.

### OQ-3 — **RESOLVED** (Amendment 3A). No gate.

The exception type with its namespace stripped, or null. The narrowest option that identifies the
failure, and the one that leaves the location room in the 55 columns a trailer has.

### OQ-4 — **RESOLVED** (Amendment 3B). No gate.

Undecorated, except `new` in bold. No colour, so nothing here can read as a severity.

### OQ-5 — **RESOLVED** (Amendment 3E). No gate.

Appended only when non-zero: `Xping: 5 findings (5 high) in 20 runs of SampleApp.XUnit, 2 new
failures`. Pinned by the `latest-run summary` documented sample.

---

## 6. Implementation sequence

### P0 — Inventory. Read-only. No code.

1. Answer **OQ-1**: is last-attempt resolution reachable per-test, or only via
   `SessionOutcomes.Tally`'s aggregate? Name the member, or state that extraction is required.
2. Confirm the §4.5 table against source. Report any divergence as a spec conflict.
3. Confirm `SessionViews` ordering (`AnalysisContext` documents newest-first; verify).
4. Confirm `cli-report-format-spec.md` P6 is merged and the heading machinery exists.
5. List every test that would break under the §4.1 envelope change.
6. Report the shape of the developer fixture store: session count, and whether it contains a session
   with a genuinely new failure. If it does not, note that P5 must construct one.

**Stop and report.** Write nothing.

### P1 — `LatestRunAnalyzer`

Pure component: `AnalysisContext` plus the emitted finding list in, a value object out. No envelope
wiring, no rendering.

Unit tests, one per branch of D3, D5, D6, D11, D14: never-failed-before, first-appearance,
failed-before-below-gates, already-has-a-finding, single-session window, environmental newest
session, retry-rescued, skipped test, more than `LatestRunMaxRows` failures, ordering ties.

### P2 — Envelope

`LatestRunDto`, `LatestRunFailureDto`, `ReportEnvelope.LatestRun`. Contrast strings resolved in the
builder. Schema to `1.20` — confirm the format spec's P1 has already taken it to `1.19` rather than
assuming the number.

JSON output gains the member. **Text output is byte-identical after this phase.** Verify against the
P7 golden files from the format spec before proceeding.

### P3 — Text rendering, normal case

Heading, rule, rows, "also failing" line. Reuse the D5 heading machinery from the format spec; do
not add a second heading writer. Gate on **OQ-3** and **OQ-4**.

### P4 — Environmental, suppression, overflow

D5, D6, D7 render paths. Each gets a golden file.

### P5 — Golden files and fixtures

Generate goldens from a fixture store and check §3.1 to §3.4 against them, not the reverse (format
spec D10.2). Construct a fixture store containing a genuine new failure if P0 found none. Add width
and charset assertions across every fixture, plus ASCII-glyph and `--no-color` variants of §3.1.

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
- A `--latest-run-only` mode. `--runs 1` already produces a report whose only content would be this
  section, except that D5 suppresses it, which is correct. Revisit only if asked for.
- Cloud (§8).

---

## 8. Reserved for Cloud

Not implemented. Recorded because this section is where Cloud adds the most value per line, and the
row shape should not have to change to accommodate it.

This is the natural home for caught-divergence. The contrast line is the slot:

```
    new          CartTests.Checkout_AppliesDiscount
                 confidence 0.94 over 812 runs, failed on this branch
                 NullReferenceException | CartTests.cs:112
```

The local contrast (`passed the previous 19 runs`) is replaced, not supplemented: the Cloud
statement is strictly stronger and says the same thing better. Row geometry is unchanged; only the
`Contrast` string differs, and it is already resolved in the builder, so no renderer changes.

Consequence for P2: `LatestRunFailureDto.Contrast` must be the **only** place the contrast sentence
is composed. An implementer who phrases it in the renderer instead will make the Cloud work a
renderer change.
