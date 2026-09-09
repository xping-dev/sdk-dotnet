# CLI Report Format Redesign

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/Report/**`
**Schema:** `1.11` → `1.12`
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
  `excludesEnvironmental` → `-env`, `excludesEnvironmentalAndClustered` → `-env-cluster`.
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

1. If `FullyQualifiedName` is present, `ShortName` = the last two dot-separated segments —
   `Class.Method`. Namespace is dropped.
2. If `DisplayName` carries an argument list (contains `(` and the text before it matches the method
   segment), append the argument list verbatim: `Class.Method(x: 1, y: 2)`.
3. If `FullyQualifiedName` is absent, `ShortName` = `DisplayName`.
4. If both are absent, `ShortName` = `"(unnamed)"`.

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

`SubjectDto` gains `CauseLabel` — resolved in `EnvelopeBuilder` from the provider's evidence. Where
`BrokenFixtureEvidence` names a lifecycle member, that member is the label. Where
`SharedFailureEvidence` has no agreed member, the label is the signature's exception type. Where
neither is available, the label is `GroupId` and the members are still listed.

### D4 — Header split into two questions, and `Flagged` is published

Line 1 answers *what was analysed*: `Xping · assembly · N runs · period · branch@sha`. Unchanged.

Line 2 answers *what state the suite is in*: `N tests · N healthy · N flagged`.

`SummaryDto` gains `Flagged`. `EnvelopeBuilder` already computes it as `flagged.Count` and currently
discards it into the `Healthy` subtraction. Publishing it makes the line reconcile without the
reader reconstructing the deduplication.

`ExcludedLowEvidence` and `ExcludedNotSignificant` continue to append to line 2 as today.

The caveat line (`WriteCaveats`) is unchanged and stays above the fence.

### D5 — Section heading

```
NEEDS ATTENTION (5)                                          most severe first
────────────────────────────────────────────────────────────────────────────
```

The heading is uppercase, the count is parenthesised, the ordering note is right-aligned to
`FenceWidth`, and a rule follows at `FenceWidth`.

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
5. HIGH  flaky                                                 same test as #4
```

Ordering rule: findings are sorted by their existing rank. Then, walking the list top to bottom,
any finding whose subject fingerprint has already appeared is moved to immediately after the last
finding sharing that fingerprint. The first occurrence keeps its rank position, so severity ranking
is preserved for every distinct test.

Applies to `SingleTest` subjects only. Group subjects are not deduplicated against their members.

This is a **ranking-layer** change, applied in `EnvelopeBuilder` before truncation, so `--top`
cannot separate a sibling pair from its anchor and the JSON consumer sees the same order.

### D7 — Row numbering and the details line

Rows are numbered from 1, in envelope order, so a reader can reference a finding out loud and so
`--top` truncation is visibly a truncation.

One footer line, below the fence, alongside the existing truncation line:

```
Details: xping report --id <id>
```

This replaces the per-finding `DrillDown` never being surfaced in text output. `FindingDto.DrillDown`
remains in the JSON contract and is unchanged.

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

The separator stays `" | "` rather than moving to `ReportGlyphs.Separator`. The header uses `·` for
a different job — separating provenance facts — and collapsing the two would make the trailer's
segments read as continuous prose.

### D9 — Schema bumps to 1.12

`ReportEnvelope.CurrentSchemaVersion` = `"1.12"`. Changed shapes: `SubjectDto` gains `ShortName` and
`CauseLabel`; `SummaryDto` gains `Flagged`; finding order changes per D6.

---

## 3. Target output

Verbatim. Implementation is complete when a fixture reproducing the sample store renders this,
modulo names.

```
Xping  SampleApp.XUnit  ·  20 runs  ·  2026-09-05 09:00→16:21  ·  main@eab9867
16 tests  ·  10 healthy  ·  6 flagged

NEEDS ATTENTION (5)                                          most severe first
────────────────────────────────────────────────────────────────────────────

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
    UnprovisionedDatabase — 3 tests
      FixtureTests.FirstTestNeedingTheDatabase
      FixtureTests.SecondTestNeedingTheDatabase
      FixtureTests.ThirdTestNeedingTheDatabase
    UnprovisionedDatabase..ctor failed, blocking 3 tests in 20 of 20 runs
    evidence high | all runs | f_7c905f05

4.  HIGH  unstable timing
    SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState
    p50 73ms, ranging 18ms to 129ms, dispersion 0.73 over 20 executions
    evidence high | -env | f_66c10ec3 | .../SampleTests.cs:83

5.  HIGH  flaky                                                same test as #4
    SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState
    failed 6 of 20 executions (30%) in 6 of 20 runs, 1 failure mode
    evidence high | -env-cluster | f_445c562e | .../SampleTests.cs:83

Details: xping report --id <id>

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/command-reference.html#the-population-marker
```

The block from the heading through the last trailer is fenced, as today. The details line and the
legend sit below the fence, in that order.

Note that finding 3 carries `all runs`: `BrokenFixture` maps to `PopulationRule.AllExecutions`,
because a shared cause is what an environmental session looks like from underneath and discounting
those sessions would silence the finding that explains them. Findings 1, 2 and 5 carry
`-env-cluster`; finding 4 carries `-env`. An implementer must not "correct" these — they are
`PopulationRules.For(kind)` output and the golden file pins them.

---

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
`ReportVocabulary.MarkerFor`, `ReportVocabulary.FindingsPhrase`, and the segment order, separator
and budget arithmetic inside `WriteTrailer`.

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

**Stop and report.** Write nothing.

### P1 — Contract: name resolution

`SubjectDto` gains `ShortName` and `CauseLabel`. `EnvelopeBuilder.ForTest` and `BuildSubject`
resolve them per D1 and D3. Schema → `1.12`.

Renderers are **not** touched. Text output is byte-identical after this phase. JSON output gains
fields.

Unit tests: FQN with namespace, nested class, parameterised display name, prose display name, null
FQN, null both, group with lifecycle member, group without.

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

D4 and D5. `ReportGlyphs` gains the rule character. Gate on **OQ-3**.

### P7 — Footer

D7 details line; footnote reduced to one line and repositioned.

### P8 — Golden files

Regenerate every pinned text fixture. Add a golden file reproducing §3 exactly. Add width assertions
across every fixture. Add an ASCII-glyph variant and a `--no-color` variant.

### P9 — Consistency and docs

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
