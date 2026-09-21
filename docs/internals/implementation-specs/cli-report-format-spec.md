# CLI Report Format Redesign

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/Report/**`
**Schema:** `1.18` → `1.19`
**Depends on:** nothing
**Blocks:** `cli-latest-run-spec.md` (reuses D1 and D5 of this document)

---

## Amendment protocol

This document is ground truth. An implementation session reads it before writing anything.

If an implementer finds a conflict between this spec and the code, or believes a decision here is
wrong, they **stop and report**. They do not adapt silently, do not "improve" a decision inline, and
do not proceed past a blocked open question. Changing anything in **Decisions** or **Data contract**
requires an amendment to this document first, committed before the code that depends on it.

Sections marked **OQ** are gates. Code that depends on an unresolved OQ is not written.

### Amendment 1 — from the P0 inventory

P0 read this document against source and found four conflicts, plus one stale statement of fact in
§1.1. All five are resolved here, and this amendment is committed before any code depending on them.

| # | Was | Now | Changed |
|---|---|---|---|
| A | schema `1.11` → `1.12` | schema `1.18` → `1.19` | header, D9, P1 |
| B | D1's resolution rules matched only NUnit's recorded names | rules 1 and 2 restated per adapter | D1, P1 |
| C | D3 resolved `CauseLabel` in `BuildSubject`, which cannot see evidence, and fell back to the group id | resolution moves to `BuildFinding`; the hash is never a label | D3, P1 |
| D | D4 silently deleted the severity breakdown from text output | the breakdown moves to the D5 heading | D4, D5, §4.3, P6 |
| E | §1.1 listed three population tokens | there are four; `excludesPartialRuns` → `-partial` | §1.1 |

§3 is updated where these reach it. It stays illustrative and is still regenerated in P7 per D10.2.

---

## 1. Ground truth

`xping report --format text` renders through `TextReportRenderer`. The envelope contract in
`Report/Contract/ReportEnvelope.cs` states the governing rule:

> Every display string is already resolved by the time an envelope exists. Renderers choose layout
> and nothing else — no thresholds, no formatting decisions, no arithmetic.

Current layout constants in `TextReportRenderer`:

| Constant | Value | Meaning |
|---|---|---|
| `FenceWidth` | 72 | widest line the fence may contain |
| `MarkerWidth` | 4 | severity marker (`HIGH`, `MED `, `LOW `) |
| `Indent` | 6 | `MarkerWidth + 2` |
| `SubjectColumn` | 23 | `Indent + ReportVocabulary.LongestLabel + 2` |
| `SeparatorWidth` | 3 | width of `" | "` |

Current per-finding shape is three logical lines: a subject line carrying marker + kind label +
subject, a wrapped headline, and a dim trailer.

### 1.1 Machinery that is already correct and is not touched

Verified in source. Listed because an implementer reading §1.2 could reasonably conclude these share
the same defect. They do not.

- **`FindingDto.Population`** is resolved on the envelope. `PopulationRules.For(kind)` is a single
  exhaustive table keyed on `FindingKind` that throws on an unrecorded kind, guarded by
  `EveryKindRecordsWhichPopulationItsRatesAreTakenOver`. This is already the correct architecture —
  the renderer reads a resolved value and decides nothing.
- **`ReportVocabulary.PopulationTokenFor`** maps `allExecutions` → `all runs`,
  `excludesEnvironmental` → `-env`, `excludesEnvironmentalAndClustered` → `-env-cluster`, and
  `excludesPartialRuns` → `-partial`. **Four, not three** — an earlier draft of this list stopped at
  three, which was true of `1.11` and stopped being true at `1.18`, where `Vanished` moved onto the
  fourth rule. `EveryPopulationRuleHasAMarkerOfItsOwn` and
  `EveryFindingSaysWhichPopulationItsRateWasTakenOver` both pin it.
- **Every finding carries a token, including `all runs`.** This is argued in the source: a marker
  printed only where something was set aside leaves a reader unable to distinguish *counted
  everything* from *this build did not say*.
- **`ReportVocabulary.PopulationLegend`** is three lines below the fence, printed once per report,
  suppressed on an empty report, and carries the docs URL on a line of its own.
- **`WriteTrailer`'s budget arithmetic**, including the separator-counting comment explaining why
  the joined line lands on the budget rather than three columns over it.

None of the above changes in this spec.

### 1.2 Observed defect

```
HIGH  always failing   ... which throws an exception is properly tracked
      failed 20 of 20 executions (100%), one failure mode:
      System.InvalidOperationException
      evidence high | -env-cluster | f_c7b87f12 | .../SampleTests.cs:66
```

The subject is not a test identity. Three causes, all in `TextReportRenderer`:

1. **`Subject()` prefers the wrong string.**
   ```csharp
   return subject.DisplayName ?? subject.FullyQualifiedName ?? subject.GroupId ?? "(unnamed)";
   ```
   `DisplayName` is the *runner-facing* name. For xUnit that is `[Fact(DisplayName = "…")]` prose or
   a parameterised case string. It is not an identifier, is not greppable, and is not usable as a
   `dotnet test --filter` argument. It is preferred over `FullyQualifiedName`, so a test with a
   prose display name loses its identity outright.

2. **The subject budget is 49 columns.** `FenceWidth - SubjectColumn` = `72 - 23`. The marker and
   the kind label consume a third of the fence on the one line that must never be truncated.

3. **`Fit()` truncates from the left.** Correct for an FQN, destructive for a sentence. Applied to
   prose it produces the leading `...` seen above.

Two further defects of the same family:

4. **Group findings name an arbitrary member.** `Subject()` takes `Members[0]`, left-truncates it,
   and appends `+N more` — producing `...xtureTests.ThirdTestNeedingTheDatabase +2 more` for a
   finding whose provider (`FailureModeProvider`) already identified the shared lifecycle member.

5. **`Subject()` performs string resolution inside a renderer**, contradicting the envelope contract
   quoted above. This is the root cause of (1)–(4) being renderer bugs at all.

### 1.3 Reader-facing defects

6. **The finding list has no heading.** `5 findings (5 high)` states a count in an internal
   vocabulary word. Nothing tells the reader whether the list is informational or actionable.

7. **The healthy count does not reconcile.** `16 tests · 10 healthy` with `5 findings` reads as
   arithmetic error. It is correct — `EnvelopeBuilder` deduplicates the flagged fingerprint set, and
   one test carries two findings while one finding covers three tests — but the intermediate count
   is computed and then discarded.

8. **Sibling findings are separated.** The same test appears at rank 4 and rank 5 with nothing
   connecting them.

---

## 2. Decisions

Numbered, immutable, amendment-only.

### D1 — Name resolution moves into `EnvelopeBuilder`

`SubjectDto` gains a pre-resolved `ShortName`. Renderers print it and perform no string surgery on
names.

Resolution order, applied in `EnvelopeBuilder.ForTest`:

1. If `FullyQualifiedName` is present, split it at its **first** `(`. The part before is the *name*;
   the part from `(` to the end, if there is one, is the *argument list*. `ShortName` = the last two
   dot-separated segments of the name — `Class.Method` — with the argument list re-appended
   unchanged. Namespace is dropped.
2. If rule 1 produced no argument list and `DisplayName` carries one, append `DisplayName`'s
   verbatim: `Class.Method(a: 1, b: 2)`.
3. If `FullyQualifiedName` is absent, `ShortName` = `DisplayName`.
4. If both are absent, `ShortName` = `"(unnamed)"`.

**Rule 1 splits at the paren before it splits on dots**, because one adapter's fully qualified name
already carries its arguments and one argument value in ten contains a decimal point. NUnit records
`test.FullName`, which for a parameterised case is `SampleApp.NUnit.SampleTests.Add(1.5, 2)`.
Splitting that on `.` first yields `5, 2)` as the final segment and loses the class outright.

**Rule 2's condition is a per-adapter fact, not a guess.** An argument list counts as one only when
the text before the first `(` — trailing whitespace removed — equals **either** the method segment
**or** the whole `FullyQualifiedName`. Both forms are required, and a third adapter puts a space in
front of the paren:

| Adapter | `DisplayName` from | Parameterised case | Prefix before `(` |
|---|---|---|---|
| xUnit | `test.DisplayName` | `SampleApp.XUnit.SampleTests.Add(a: 1, b: 2)` | the full FQN |
| NUnit | `test.Name` | `Add(1,2)` | the method segment |
| MSTest | `FormatTestNameWithParameters` | `Add (1,2)` | the method segment, then a space |

An earlier draft of this rule required the prefix to match the method segment alone. That is true of
NUnit and of nothing else: every parameterised xUnit test would have failed the test and lost its
arguments, and every MSTest one would have failed it on the space.

The argument list is appended **exactly as the adapter wrote it**. The three render arguments
differently — named in xUnit, positional in the other two — and normalising them here would invent a
spelling no runner emits and no `--filter` accepts.

A `[Theory]` given an explicit prose `DisplayName` produces `some prose(a: 1, b: 2)`, whose prefix
matches neither form, so rule 2 does not fire and the arguments are not shown. That is the correct
outcome: the alternative is appending an argument list to a name that is not the method's.

**Note, not a gate.** MSTest builds its `FullyQualifiedName` as
`$"{context.FullyQualifiedTestClassName}.{context.TestName}"`, and `TestName` may be the explicit
`[TestMethod("…")]` display name rather than the method's. Where it is, the FQN itself carries prose
and rule 1 yields `Class.some prose`. That is an SDK-side question about what the adapter records,
out of scope here, and rule 1 degrades to something honest either way — it does not block P1.

`FullyQualifiedName` and `DisplayName` remain on the DTO unchanged. Nothing is removed.

**Rationale.** `Class.Method` is unambiguous within a suite, short enough to never truncate in
practice, greppable, and directly pasteable into `dotnet test --filter FullyQualifiedName~`. Nested
classes produce `Outer+Inner.Method` from the FQN's own encoding, which is correct.

**Prose display names** — a `DisplayName` that matches neither rule 2 nor the method segment — are
**not** shown on the subject line. Whether they appear at all is **OQ-1**.

### D2 — The subject gets its own line

The severity marker and kind label move to a header line. The name occupies a full-width line
beneath it and is never competed with for horizontal space.

New layout constants:

| Constant | Value | Meaning |
|---|---|---|
| `FenceWidth` | 72 | unchanged |
| `RowLabelWidth` | 4 | `"1. "` padded — `$"{n}."` right-padded to 3, plus one space |
| `ContinuationIndent` | 4 | equals `RowLabelWidth`; aligns continuations under the marker |

Budgets: name and headline and trailer all get `FenceWidth - ContinuationIndent` = **68 columns**,
up from 49 for the name.

Truncation of `ShortName`, when it happens at all, protects the method segment: cut from the left at
the dot boundary, yielding `...Tests.VeryLongMethodName`. A method segment longer than the budget on
its own is emitted whole rather than split — half an identifier is unsearchable, and overflowing the
fence by a few columns is the smaller harm. This mirrors the existing `Wrap()` policy for long words
and `FitPath()` for paths.

**Cost:** one additional line per finding. That line is the fix.

### D3 — Group findings name the shared cause

For `SubjectDto.Type == "group"`, the subject line is the shared cause, not a member. Members are
listed beneath at indent 6, capped at 3, with `+N more` when the cluster is wider.

`SubjectDto` gains `CauseLabel`, resolved in **`EnvelopeBuilder.BuildFinding`** and passed down.
It cannot be resolved where an earlier draft of this decision put it: `BuildSubject` takes a
`FindingSubject` and nothing else, while every value below comes from the finding's
`FindingEvidence`, which only `BuildFinding` holds. `BuildSubject` gains a `FindingEvidence`
parameter for this.

Resolution order:

1. `BrokenFixtureEvidence.Member`, **verbatim** — `UnprovisionedDatabase..ctor`.
2. `BrokenFixtureEvidence` whose failures agreed on no member: the site, phrased — `fixture setup`,
   `per-test setup`, and so on.
3. `SharedFailureEvidence.Signature.ExceptionType`.
4. Anything else: `"(cause not recorded)"`.

Rules 1 and 2 are `EvidenceHeadline`'s own `e.Member ?? Phrase(e.Site)`, and the phrasing table is
shared rather than copied. The subject line and the headline directly beneath it then name the same
thing in the same words — which is why rule 1 keeps `..ctor` instead of trimming to the declaring
type. `UnprovisionedDatabase` above a headline reading `UnprovisionedDatabase..ctor failed` is two
names for one member, and the member is what a reader opens.

**`GroupId` is never the label.** It is `sig_<signature hash>` — `FailureModeProvider` keys the
cluster on its signature so that the subject survives a promotion between kinds — and a hash
presented as a cause is worse than saying the cause was not recorded. The members are listed
beneath in every case, rule 4 included, so a cluster nobody can name is still navigable.

`CauseLabel` is null for a single-test subject and `ShortName` is null for a group subject. No
subject carries both.

### D4 — Header split into two questions, and `Flagged` is published

Line 1 answers *what was analysed*: `Xping · assembly · N runs · period · branch@sha`. Unchanged.

Line 2 answers *what state the suite is in*: `N tests · N healthy · N flagged`.

`SummaryDto` gains `Flagged`. `EnvelopeBuilder` already computes it as `flagged.Count` and currently
discards it into the `Healthy` subtraction. Publishing it makes the line reconcile without the
reader reconstructing the deduplication.

`ExcludedLowEvidence` and `ExcludedNotSignificant` continue to append to line 2 as today.

**`ReportVocabulary.FindingsPhrase` leaves line 2.** Today the line opens with it —
`5 findings (5 high, 2 medium) · 412 tests · 409 healthy` — and D4 replaces that opening with the
three suite-state counts. Dropping the phrase outright would delete the severity breakdown from
text output altogether: the row markers each state one severity, and after this nothing totals them.
So the breakdown moves to the D5 heading, beside the list it describes. Nothing is printed twice —
the heading carries the bands, the rows carry the ranking, line 2 carries the suite.

`FindingsPhrase` itself is unchanged and `SummaryReportRenderer` keeps calling it; that renderer is
one line and has no heading to move anything into. `WriteHeader` stops calling it.

The caveat line (`WriteCaveats`) is unchanged and stays above the fence.

### D5 — Section heading

```
NEEDS ATTENTION (5 high, 2 medium)                     most severe first
────────────────────────────────────────────────────────────────────────
```

The heading is uppercase, the severity breakdown is parenthesised, the ordering note is
right-aligned to `FenceWidth`, and a rule follows at `FenceWidth`.

**The parenthesis carries the bands, not a bare total.** This is where D4's breakdown lands. Empty
bands are omitted rather than printed as zero, exactly as `FindingsPhrase` already does, so the
heading reads `(5 high)`, `(5 high, 2 medium)` or `(1 low)`. `ReportVocabulary` gains
`SeverityBands(summary)` returning the bands **unparenthesised** — `5 high, 2 medium` — and each
caller adds its own punctuation: the heading wraps it in parentheses, `FindingsPhrase` keeps its
existing `$"{count} ({bands})"`. One producer, so the two cannot drift, and `FindingsPhrase`'s own
output does not change — which is what §4.3 records about it.

The bands count every finding **produced**, not the rows shown: `SummaryDto.Counts` is already
computed before truncation, for the reason `EnvelopeBuilder` states there. A `--top 3` report can
therefore head three rows with `NEEDS ATTENTION (5 high, 2 medium)`. That is correct, and the
truncation line below the fence is what explains it.

The rule is drawn with `─` when `capabilities.Glyphs` is the Unicode set and `-` when ASCII. It is
part of `ReportGlyphs`, not a literal in the renderer.

`NEEDS ATTENTION` chosen over `PROBLEMS FOUND` (claims causality, which the evidence rules forbid)
and over `RELIABILITY ISSUES` (jargon). It is imperative, unambiguous for non-native readers, and
does not assert a verdict. Final wording is **OQ-3**.

A heading is emitted **only when there is at least one finding**. The empty-report path
(`EmptyReport()`) is unchanged and emits no heading — a clean report and a full one should not
require the reader to parse a heading to discover there is nothing under it.

### D6 — Sibling findings are adjacent

When one test carries more than one finding, the second and subsequent findings are pulled up to
immediately follow the first, and annotated on the header line, right-aligned:

```
5.  HIGH  flaky                                          same test as #4
```

Ordering rule: findings are sorted by their existing rank. Then, walking the list top to bottom,
any finding whose subject fingerprint has already appeared is moved to immediately after the last
finding sharing that fingerprint. The first occurrence keeps its rank position, so severity ranking
is preserved for every distinct test.

Applies to `SingleTest` subjects only. Group subjects are not deduplicated against their members.

This is a **ranking-layer** change, applied in `EnvelopeBuilder` before truncation, so `--top`
cannot separate a sibling pair from its anchor and the JSON consumer sees the same order.

### D7 — Row numbering

Rows are numbered from 1, in envelope order. D6's `same test as #N` annotation refers to these
numbers, and `--top` truncation reads as a truncation only when the rows are numbered.

**No details line is added.** An earlier draft of this decision emitted
`Details: xping report --id <id>` below the fence. There is no `--id` flag; the command does not
exist. Emitting it would violate `DrillDown`'s own stated rule that every command it produces is one
the tool accepts today, in the field a reader is most likely to run.

`--id` is not reserved for later either. A finding id is the wrong key for the affordance: the
reader's question is *what else is there about this test*, which is why `DrillDown` names a per-test
`xping test` verb as the planned target. Adding `--id` would be a second, worse route to the same
place, and a new CLI verb belongs in a CLI-surface spec rather than a rendering one.

Two alternatives were considered and rejected:

- **Print each finding's real `DrillDown`.** `xping report --kind UnstableTiming --format json
  --assembly SampleApp.XUnit` is roughly sixty columns and five more lines per report, and in a
  single-assembly report the only part that varies is the kind, which is printed on the row two
  lines above it.
- **A template footer**, `xping report --kind <kind> --format json`. Honest about the flags, but it
  is a command the reader must edit before running, which is below the standard `DrillDown` sets.

`FindingDto.DrillDown` is unchanged and continues to carry the per-finding command in JSON, where an
agent reads it. Text output continues not to surface it. The truncation line continues to carry
`DrillDown.ForFullReport()`, which is a real command.

### D8 — Trailer composition is unchanged except for its indent

One dim line per finding, joined by `" | "`:

```
evidence high | -env-cluster | f_c7b87f12 | .../SampleTests.cs:66
```

**The only change is the indent**, which moves from `Indent` (6) to `ContinuationIndent` (4),
widening the budget from 66 to 68. Segment order, separator, tokens, the every-finding population
rule, `FitPath` truncation and the budget arithmetic all stay exactly as they are.

Four things this decision deliberately does **not** do, each having been proposed and withdrawn:

- **Not reordered to put location first.** `FitPath` cuts from the left, so a truncated path reads
  `.../SampleTests.cs:66`. Leading a line with an ellipsis is the exact visual defect D1 and D2
  exist to remove. Location stays last, where the existing budget arithmetic already places it and
  where its truncation is invisible.
- **Not conditionally emitted.** The population token appears on every finding including
  `all runs`, per the argued rule in §1.1.
- **Not retokenised.** `-env`, `-env-cluster` and `all runs` stay. They are terse by design, the
  legend is what makes them readable, and the two columns this redesign recovers do not buy enough
  room for prose.
- **Not accompanied by a legend change.** `PopulationLegend` is already one block, already below the
  fence, already printed once, already suppressed on an empty report.

**Below-fence order is legend, then truncation line** — what the code already emits. The legend
explains what the reader is looking at; the truncation line is the next thing they might do, and an
action belongs after its explanation. This is stated here and nowhere else: an earlier draft
restated it in D7 and again in a phase description, and the three statements disagreed. Where this
spec and the code already agree, the spec says so once.

The separator stays `" | "` rather than moving to `ReportGlyphs.Separator`. The header uses `·` for
a different job — separating provenance facts — and collapsing the two would make the trailer's
segments read as continuous prose.

### D10 — The rules are authoritative; the example is a rendering of them

§3 was hand-typed in an earlier draft and contained four lines over 72 columns and one non-ASCII
character with no glyph pair behind it. The block is corrected, but the lesson is structural, so it
is a decision rather than a fix:

1. **§2 outranks §3.** Where the example and a decision disagree, the decision is the spec and the
   example is a defect to be regenerated. An implementer who finds a conflict reports it; they do
   not implement the example.
2. **The golden file is generated, never authored.** P7 produces it from a fixture store. §3 is
   then checked against it. Hand-editing a golden to match a spec inverts the relationship and is
   how a width violation gets pinned as correct.
3. **Every rendered line inside the fence is width-asserted**, not spot-checked. The assertion runs
   across every fixture, not only the one reproducing §3.
4. **Non-ASCII characters appear only where a `ReportGlyphs` pair already exists.** Today that is
   the separator, the arrow, and the D5 heading rule. Introducing a bare literal breaks
   `APipedReportCarriesNothingButTheReport`, which asserts every character in piped output is below
   `0x80`. A new decorative character requires a new glyph pair and an ASCII counterpart, in the
   same change.

Applying (4): D3's group line uses **parentheses**, `UnprovisionedDatabase..ctor (3 tests)`, not an
em dash. Parentheses are ASCII in both modes and need no glyph pair, which is the right trade for one
use. This supersedes the em dash shown in earlier drafts.

### D9 — Schema bumps to 1.19

`ReportEnvelope.CurrentSchemaVersion` = `"1.19"`. Changed shapes: `SubjectDto` gains `ShortName` and
`CauseLabel`; `SummaryDto` gains `Flagged`; finding order changes per D6.

**Amended from `1.12`.** This document was drafted against `1.11`, and the envelope shipped `1.18`
before P0 read it — seven revisions underneath the draft, the last of them `1.18`, where
`population` gained `excludesPartialRuns`. `1.19` is the next number. Three places pin the current
value as a literal and move with it: `ReportEnvelope.CurrentSchemaVersion`,
`ReportEnvelopeTests.TheEnvelopeCarriesEveryDocumentedSection`, and the sample in
`docs/cli/command-reference.md`. Nothing else in this spec depends on the number.

---

## 3. Target output

Illustrative of the rules, not a byte-exact contract. **Where this block and §2 disagree, §2 wins
and this block is wrong** — see D10. The golden file in P7 is generated, never hand-typed, and is
what acceptance is measured against.

```
Xping · SampleApp.XUnit · 20 runs · 2026-09-05 09:00 → 16:21 · main@eab9867
16 tests · 10 healthy · 6 flagged

NEEDS ATTENTION (5 high)                               most severe first
────────────────────────────────────────────────────────────────────────

1.  HIGH  always failing
    SampleTests.ExceptionTest_ThrowsOnMissingDependency
    failed 20 of 20 executions (100%), one failure mode:
    System.InvalidOperationException
    evidence high | -env-cluster | f_c7b87f12 | .../SampleTests.cs:66

2.  HIGH  timing out
    SampleTests.TimeoutTest_UnresponsiveDependency_AwaitsForever
    timed out 20 of 20 executions (100%) in 20 of 20 runs, killed at its
    500ms limit
    evidence high | -env-cluster | f_3ea537e4 | .../SampleTests.cs:43

3.  HIGH  broken fixture
    UnprovisionedDatabase..ctor (3 tests)
      FixtureTests.FirstTestNeedingTheDatabase
      FixtureTests.SecondTestNeedingTheDatabase
      FixtureTests.ThirdTestNeedingTheDatabase
    UnprovisionedDatabase..ctor failed, blocking 3 tests in 20 of 20
    runs
    evidence high | all runs | f_7c905f05

4.  HIGH  unstable timing
    SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState
    p50 73ms, ranging 18ms to 129ms, dispersion 0.73 over 20 executions
    evidence high | -env | f_66c10ec3 | .../SampleTests.cs:83

5.  HIGH  flaky                                          same test as #4
    SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState
    failed 6 of 20 executions (30%) in 6 of 20 runs, 1 failure mode
    evidence high | -env-cluster | f_445c562e | .../SampleTests.cs:83
```

The block from the heading through the last trailer is fenced. The legend and, when the report was
truncated, the truncation line sit below it, in that order - D8. There is no details line - D7.

Measured against the rules: no line inside the fence exceeds 72 columns; the only non-ASCII
characters are the header's separator and arrow and the heading rule, each of which has an existing
`ReportGlyphs` pair; the heading annotation and the sibling annotation are right-aligned to 72; the
fixture headline wraps at 68 because it is 69 columns.

Finding 3 carries `all runs`: `BrokenFixture` maps to `PopulationRule.AllExecutions`, because a
shared cause is what an environmental session looks like from underneath and discounting those
sessions would silence the finding that explains them. Findings 1, 2 and 5 carry `-env-cluster`;
finding 4 carries `-env`. An implementer must not "correct" these - they are
`PopulationRules.For(kind)` output and the golden file pins them.

The two header lines are the existing `WriteHeader` output, with D4's `flagged` count added to
line 2. Line 1 measures 75 columns in shipped output, so either `FenceWidth` does not govern the
header or the header already overflows it. Either way it is pre-existing and this spec does not
touch it - P0 item 8 establishes which, and the answer changes nothing here.

## 4. Data contract

### 4.1 `SubjectDto`

```csharp
internal sealed record SubjectDto(
    string Type,
    string? Fingerprint,
    string? FullyQualifiedName,
    string? DisplayName,
    string? ShortName,      // NEW - D1
    string? CauseLabel,     // NEW - D3, group subjects only
    string? SourceFile,
    int? SourceLineNumber,
    string? Assembly,
    string? GroupId,
    int? MemberCount,
    IReadOnlyList<SubjectDto>? Members);
```

Property declaration order is serialised order and is part of the contract. New members are inserted
where shown, not appended — this is a schema bump, and grouping the name fields is worth the churn.

### 4.2 `SummaryDto`

Gains `int Flagged`, positioned immediately before `Healthy`.

### 4.3 Unchanged

`FindingDto`, `WindowDto`, `ContextDto`, `MetricDto`, `TruncationDto`, `SeverityCountsDto`,
`ReportJsonOptions`, every `FindingEvidence` payload, every provider, every threshold in
`LocalAnalysisConstants`.

Also explicitly unchanged, per §1.1 and D8: `PopulationRule`, `PopulationRules`,
`ReportVocabulary.PopulationTokenFor`, `ReportVocabulary.PopulationLegend`,
`ReportVocabulary.MarkerFor`, and the segment order, separator and budget arithmetic inside
`WriteTrailer`.

`ReportVocabulary.FindingsPhrase` keeps its output and its `SummaryReportRenderer` caller. What
changes around it is not the method: D4 stops `WriteHeader` calling it, and D5 adds a
`SeverityBands` sibling that it is refactored to call.

**No analysis behaviour changes in this spec.** No finding is emitted, suppressed, or reranked by
severity. D6 reorders equal-rank output only.

---

## 5. Open questions — gates

### OQ-1 — What produces the prose display name? **Blocks D1 rule 4**

The sample's `"… which throws an exception is properly tracked"` is prose. Determine whether it
originates from an explicit `[Fact(DisplayName = …)]`, an `[Description]` attribute, or something
the SDK adapter synthesises.

- If explicit and common: emit it dimmed on a line beneath the name, quoted, truncated from the
  **right** at 68 columns.
- If synthesised or rare: drop it from text output entirely; it remains in JSON.

Do not implement the dimmed line until this is answered.

### OQ-2 — **RESOLVED.** No gate.

Asked whether the rate-population token existed and whether it was derived in the renderer. It
exists, and it is carried on the envelope as `FindingDto.Population`, resolved by the exhaustive
`PopulationRules` table. It is already the architecture D1 argues for.

Retained rather than deleted so that D8's four withdrawals have something to point at. Nothing in
this spec is gated on it.

### OQ-3 — Section heading wording. **Blocks D5**

`NEEDS ATTENTION` is the recommendation. Confirm or replace before P6.

### OQ-4 — Does severity keep its colour? **Blocks P3**

`OutputCapabilities.Colorize(severity, text)` currently wraps the marker. Confirm the marker remains
the coloured element under the new layout, rather than the name or the whole header line.

---

## 6. Implementation sequence

One prompt per phase. Each phase ends green: builds, tests pass, `xping report` runs.

### P0 — Inventory. Read-only. No code.

Produce a written report covering:

1. Current `TextReportRenderer` in full — confirm or correct every constant in §1.
2. Confirm §1.1 against source: `FindingDto.Population`, `PopulationRules.For`,
   `PopulationTokenFor`, `PopulationLegend`, `WriteLegend`, and the `WriteTrailer` budget
   arithmetic. Report any divergence from §1.1 as a spec conflict. Do not modify any of them.
3. Every call site of `Subject()`, `Fit()`, `FitPath()`, `Wrap()`.
4. Every test asserting on text output — file, name, and what it pins.
5. Whether `SummaryReportRenderer` or any JSON test would break under the D9 field additions.
6. Sample `DisplayName` values from the fixture suite, to answer **OQ-1**.
7. Confirmation that `ReportVocabulary.LongestLabel` is still 15 and which kind label sets it.
8. Whether `FenceWidth` governs the two header lines. Shipped line 1 measures 75 columns. Report
   which it is; do not change it either way.
9. Every `ReportGlyphs` pair that exists today, and every test asserting on output charset -
   `APipedReportCarriesNothingButTheReport` and any sibling.

**Stop and report.** Write nothing.

### P1 — Contract: name resolution

`SubjectDto` gains `ShortName` and `CauseLabel`. `EnvelopeBuilder.ForTest` resolves `ShortName` per
D1. `BuildFinding` resolves `CauseLabel` per D3 and passes it into `BuildSubject`, which gains a
`FindingEvidence` parameter. Schema → `1.19`.

Renderers are **not** touched. Text output is byte-identical after this phase. JSON output gains
fields.

Unit tests, one per rule and per adapter shape: FQN with namespace; nested class; an FQN that
already carries its argument list, with a decimal point inside it (NUnit); an FQN-prefixed
parameterised display name (xUnit); a space before the argument list (MSTest); a prose display name
on a parameterised case; a prose display name alone; null FQN; null both. And for D3: group with a
lifecycle member, group with a site but no member, group with an exception type only, group with
none of the three.

Both positional-record insertions break their call sites silently rather than loudly —
`ShareableOutputTests` builds a `SubjectDto` with the name in two adjacent positional slots and a
`SummaryDto` with thirteen. Update those in this phase.

### P2 — Contract: `Flagged`

`SummaryDto` gains `Flagged`; `EnvelopeBuilder` publishes the count it already computes. Text output
unchanged.

### P3 — Renderer: row layout

Rewrite `WriteFinding` per D2, D7, D8. Delete `SubjectColumn`. Add `RowLabelWidth`,
`ContinuationIndent`. Replace `Subject()` with a read of `ShortName` — the method is deleted, not
rewritten.

Add `FitName()` protecting the method segment. `Fit()` remains for other uses.

Assert: no rendered line inside the fence exceeds `FenceWidth`, except a single unbreakable token.

### P4 — Renderer: group subjects

Member list per D3. Cap 3, `+N more`, indent 6.

### P5 — Ranking: sibling adjacency

D6, in `EnvelopeBuilder`, before truncation. The annotation string (`same test as #N`) is resolved
in the builder, not the renderer.

Test: two findings one test, three findings one test, sibling separated by four ranks, sibling
spanning a `--top` boundary.

### P6 — Header and heading

D4 and D5. `ReportVocabulary` gains `SeverityBands`; `WriteHeader` stops calling `FindingsPhrase`.
`ReportGlyphs.HorizontalRule` already exists as a pair and is unused — wire it up rather than adding
one. Gate on **OQ-3**.

Nine tests in `ShareableOutputTests` read the header counts line and move with D4.

### P7 — Golden files

Regenerate every pinned text fixture. Generate a golden from a fixture store reproducing §3 and
check §3 against it - not the reverse (D10.2). Any divergence is reported: it is either a defect in
§3 or a defect in the implementation, and which one is a judgement for the author, not the
implementer.

Add, across every fixture and not only the §3 one: a width assertion on every line inside the fence,
and a charset assertion for piped output. Add an ASCII-glyph variant and a `--no-color` variant.

### P8 — Consistency and docs

Confirm `SummaryReportRenderer` still agrees with the fenced report on counts. Update the output
contract document and the CLI README sample. Confirm the sample in the NuGet description still
renders.

---

## 7. Out of scope

- Any change to finding emission, thresholds, severity assignment, or `LocalAnalysisConstants`.
- The `LATEST RUN` section — `cli-latest-run-spec.md`.
- Cloud annotation. §8 reserves the slot; nothing is built.
- Configuration surface for layout.
- `--fail-on` semantics.

---

## 8. Reserved for Cloud

Not implemented. Recorded so the layout does not have to change when it is.

- **Row trailer is the annotation slot.** `confidence 0.62 · moderate` inserts between the evidence
  level and the id. No line above it reflows.
- **Header line 1 gains one provenance segment** (`· cloud`) when authenticated.
- **The name and headline lines are identical whether or not authenticated.** A local report and an
  annotated one are the same shape; nobody learns two layouts.
- **Ranking stays a single function.** If Cloud later reranks by confidence rather than local
  severity, that swaps a comparator. It must not reach the renderer.
