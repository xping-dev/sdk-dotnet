# CLI Report — Finding Detail (`xping report --id <finding-id>`)

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/**`
**Schema:** `1.20` → `1.21`
**Depends on:** `cli-report-format-spec.md` **as amended by its Amendment 2**, which supersedes its
D7's rejection of `--id` and extends D8's below-fence order. Also its D1 (`ShortName`), D3
(`CauseLabel`, members), D5 (heading machinery), D6/D7 (sibling adjacency and row numbering), D10
(example/rule authority, width and charset discipline). `cli-latest-run-spec.md` D2 and D15
(`LatestRun` on the envelope, schema `1.20`). **Do not start P1 of this spec until the format spec's
Amendment 2 is committed.**
**Blocks:** nothing

---

## Amendment protocol

Identical to `cli-report-format-spec.md`. This document is ground truth. An implementer who finds a
conflict stops and reports rather than adapting. Decisions and the data contract change by amendment
only, committed before dependent code. **OQ** sections are gates.

### Inventory corrections carried into this document

The Phase A inventory this spec was authored from stated two counts that source contradicts. Both
are corrected here so that no phase below inherits them.

| Was | Now | Where established |
|---|---|---|
| "28 golden files" | **50** files: 25 fixtures × 2 glyph sets | `ReportFixtures.Keys` (`tests/Xping.Cli.Tests/Report/ReportFixtures.cs:39-66`), `GoldenReportTests.Modes` (`GoldenReportTests.cs:41-45`), `EveryGoldenOnDiskBelongsToAFixture` (`GoldenReportTests.cs:89-106`) |
| documented samples in "README, nuspec, command-reference, four quickstart pages, known-limitations" | **ten** documents: `README.md`, `nuspec/README.Cli.md`, `docs/index.md`, `docs/cli/command-reference.md`, three quickstarts, `docs/getting-started/local-first.md`, `docs/known-limitations.md`, `docs/guides/working-with-tests/common-flaky-patterns.md` | `DocumentedSampleTests.Documents` (`tests/Xping.Cli.Tests/Report/DocumentedSampleTests.cs:61-73`) |

### Amendment 1 — from the P0 inventory

P0 read this document against source and found four conflicts. All four are resolved here, and this
amendment is committed before any code depending on them.

| # | Was | Now | Changed |
|---|---|---|---|
| A | thirteen provider call sites for `DrillDown` | **twelve**: `SharedFailure` and `BrokenFixture` share one, `FailureModeProvider.cs:508`. Thirteen kinds is unchanged | §1.1, D7, P2 |
| B | "no index exposes" a test's name from its fingerprint | `TestIndex.ReferenceFor(fingerprint)` does (`src/Xping.Cli/Report/Indexes/TestIndex.cs:168-169`). The third not-found tier stays out of scope, now as a scope decision rather than a missing capability | D3, §7 |
| C | five provider tests "deleted with the member they read" | three are: the retry tests read `FindingCandidate.DrillDownCommand`. `VanishedProviderTests.cs:337` and `TimeSensitiveProviderTests.cs:773` read `Finding.DrillDownCommand`, which D7 keeps; their assertions change to `--id`. `FindingCoordinatorTests.cs:658, 697, 706` name the deleted member and drop the argument | §1.2, D7, P2 |
| D | D7 cited `FailureModeProvider.cs:483` for the cluster's assembly, and took `subject.Tests[0]` | `FailureModeProvider.cs:490`, which guards an empty member list; the coordinator uses `subject.Tests.FirstOrDefault()?.Assembly` for the same reason | D7 |

The inventory table above also moves `DocumentedSampleTests.Documents` from `:23-34` to `:61-73`.

---

## 1. Ground truth

### 1.1 What exists

Every claim here was read in source at the cited line.

- **Verbs and flags.** `report`, `where`, `clear` and nothing else (`src/Xping.Cli/Program.cs:121-123`).
  `report` declares thirteen options at `Program.cs:134-231`, validates four exclusions at
  `Program.cs:242-265`, and binds into `ReportOptions` at `Program.cs:267-290`. There is no `--id`
  and no positional argument.
- **The finding id** is `f_` plus the first eight hex characters of SHA-256 over `"{kind} {subjectKey}"`
  (`src/Xping.Cli/Report/Model/FindingId.cs:39, 51-64`). Its only caller is
  `FindingCoordinator.cs:192`, passing `candidate.Kind` and `candidate.Subject.SortKey`. For a
  `SingleTest` the key is the SDK's `TestFingerprint` (`Finding.cs:54`); for a `Group` it is
  `sig_<signature hash>` (`Finding.cs:66`, `FailureModeProvider.cs:486`). Neither the window nor the
  assembly is an input.
- **Where the id moves.** The kind is half the hash, so the same subject earns a new id when its
  claim changes kind. Two sites do that: a cluster promoted from `SharedFailure` to `BrokenFixture`
  (`FailureModeProvider.cs:481-486`, which says so), and the `DurationRegression` → `DurationUnstable`
  `Instead` handover (`DurationProvider.cs:554-556`, resolved at `FindingCoordinator.cs:275-285`).
- **`--kind` and the handover.** The kind filter is applied before correction at
  `FindingCoordinator.cs:109-110, 130-134, 144-148, 150-153`. A regression candidate carries its
  unstable alternative in `Instead`; when the primary is silenced, `Reported` returns the alternative
  and the coordinator emits it with `candidate.Kind` and no further kind check
  (`FindingCoordinator.cs:179-205`). So `--kind DurationRegression` can emit a `DurationUnstable`
  finding, and `--kind DurationUnstable` drops the regression candidate at line 152 before its
  alternative is consulted. This is why D1 forbids implementing `--id` through `--kind`.
- **The exit-code contract** is `0` / `1` / `2` (`src/Xping.Cli/Report/ExitCodes.cs:21-27`), judged
  over every finding produced (`ExitCodes.cs:43-56`, `ReportCommand.cs:138`). Parse errors return
  `2` from `Program.cs:64-74`. Documented at `docs/cli/command-reference.md:74`.
- **Metrics** are built once, in `EnvelopeBuilder.BuildFinding` from `EvidenceHeadline.For`
  (`src/Xping.Cli/Report/Contract/EnvelopeBuilder.cs:309-325`), from provider-rounded evidence
  (`FindingOrder.Round`, three decimals, `FindingOrder.cs:138-139`) formatted by the helpers at
  `EvidenceHeadline.cs:619-724`. No renderer reads them: `TextReportRenderer` prints `Headline` only
  (`TextReportRenderer.cs:740-741`).
- **What text never shows.** `FindingDto.EvidenceSessions`, `Metrics`, `Evidence`, `DrillDown`
  (`ReportEnvelope.cs:316-328`); `SubjectDto.Fingerprint`, `FullyQualifiedName`, `DisplayName`,
  `Assembly`, `GroupId` (`ReportEnvelope.cs:381-393`); members past the third and every member's
  source location (`TextReportRenderer.cs:110, 841-859`).
- **DrillDown** has two shapes: `xping report --kind {kind} --format json[ --assembly X]` from
  `ForTest` and `ForGroup` (`src/Xping.Cli/Report/DrillDown.cs:33-60`) and `xping report --all` from
  `ForFullReport` (`DrillDown.cs:72`). Twelve provider call sites hand the string to
  `FindingCandidate.DrillDownCommand` (`IFindingProvider.cs:95`); the coordinator copies it to
  `Finding` (`FindingCoordinator.cs:203`); the builder copies it to `FindingDto.DrillDown`
  (`EnvelopeBuilder.cs:324`). It is serialised by `JsonReportRenderer` and rendered by no text path.
- **Dangling promises.** `DrillDown.cs:20-22` and `FindingId.cs:21` name a future `xping test`
  verb. `cli-report-format-spec.md` D7 rejected `--id` on the strength of that plan. Both are
  resolved by that spec's Amendment 2, which this document depends on: `xping test` is retired and
  `xping report --id` is the drill-down target.

### 1.2 Opened in this document

The inventory left three items unread. All three are now read; none gates anything.

1. **What a group id hashes.** `FailureSignatureFactory.Create` builds the canonical string as
   `exceptionType`, `FieldSeparator`, the normalised message, `FieldSeparator`, then the extracted
   frames joined by `FrameSeparator`, and hashes it with SHA-256
   (`src/Xping.Cli/Report/Signatures/FailureSignatureFactory.cs:49-82`). A failure with no type, no
   message and no frames hashes `UnsignablePrefix` plus the test's own fingerprint instead
   (`FailureSignatureFactory.cs:89-97`), so blank failures never cluster across tests. Consequence for
   D3: a group id is stable while the failure's normalised text is; a changed error message is a new
   cluster with a new id, and no reverse lookup can connect them. That is correct — it is a different
   claim — and the spec does not try.
2. **The retry drill-down assertions.** `RetryProviderTests.cs:535-542, 1097-1104, 1338-1346` each
   assert the full string `xping report --kind {Kind} --format json --assembly MyApp.Tests`, with
   `TestSessionFactory.DefaultAssembly = "MyApp.Tests"` (`TestSessionFactory.cs:33`). Together with
   `VanishedProviderTests.cs:337` and `TimeSensitiveProviderTests.cs:773`, five tests pin the old
   shape. D7 deletes the three retry tests with the member they read and rewrites the other two
   assertions, which read `Finding.DrillDownCommand` (Amendment 1C).
3. **Whether `--id` changes any parse-error test.** No existing test passes `--id`. The parse-error
   tests use `--nonsense` (`ReportCommandTests.cs:206-213`), `--runs banana` (`216-222`), a bare
   `--assembly` (`224-231`), `--dry-run` on `clear` (`ReviewFixesTests.cs:117-126`), and `--nonsense`
   on `where` (`129-135`). The help tests assert only `Usage:` and the root description
   (`ReviewFixesTests.cs:139-153`, `ProgramTests.cs:21-41`). Declaring `--id` changes none of them.

### 1.3 The question this flag answers

The report row is a summary: one headline sentence, one trailer. Everything the analysis resolved
about the finding — the labelled metrics, the p-value and its family size, the full identity, every
member of a cluster and where each lives — is in the JSON envelope and nowhere a person looks. The id
on every trailer exists to be pasted, and today there is nothing to paste it into.

`--id` is the answer to *tell me more about this one*. It is not a search, not a filter over kinds,
and not a gate.

---

## 2. Decisions

Numbered, immutable, amendment-only.

### D1 — Selection is post-coordinator, and never through `--kind`

`--id` selects from the coordinator's completed output. `ReportCommand` runs the coordinator with
`kinds: null` — the unfiltered set — and only then picks the finding whose `Id` equals the requested
id. `LatestRunAnalyzer` receives the same unfiltered findings it receives today when no `--kind` is
given (`ReportCommand.cs:79, 90-91`).

Selection is a pure function, `FindingSelector.Select(ordered, id)`, over
`FindingOrder.WithSiblingsAdjacent(analysis.Findings)` — the same call `EnvelopeBuilder.Build` makes
at `EnvelopeBuilder.cs:52`, so the row number it reports is the row number the full report prints.
Its result is handed to `EnvelopeBuilder.Build` (§4.4), which projects it into the envelope.

**Implementing `--id` by narrowing `--kind` is forbidden**, even though the id encodes a kind and
narrowing to it would look like an optimisation. The `Instead` handover crosses the kind filter in
both directions (§1.1): narrowing to `DurationRegression` can surface a `DurationUnstable` finding
the caller never asked for, and narrowing to `DurationUnstable` suppresses one the unfiltered report
would show. A detail view that shows a finding the full report does not, or fails to find one it
does, is wrong in the one way a drill-down must never be. The selection therefore sees exactly what
the full report sees.

**Rejected:** running only the provider that owns the id's kind. The id does not reveal its kind
without a reverse lookup (D3), and the saving is one provider pass on a store the full report reads
in well under a second. Not worth a second code path through the coordinator.

**Rejected:** selecting inside the coordinator. The coordinator judges candidates; which of its
findings a reader asked to see is not a judgement about the suite.

### D2 — Exit code `3`: the report ran, and the finding is not in it

A well-formed id that names no finding in the resolved window exits **`3`**,
`ExitCodes.FindingNotReported`, with one line on standard error (D3 gives the forms). It is the
fourth outcome, and it is not a failure of the tool:

| Code | Meaning | Source |
|---|---|---|
| `0` | report produced; with `--id`, the finding is reported | `ExitCodes.Success` |
| `1` | a finding reached `--fail-on` (never with `--id`, D6) | `ExitCodes.FindingsAtThreshold` |
| `2` | no report could be produced, or the command line was rejected | `ExitCodes.InsufficientData`, `Program.cs:73` |
| `3` | **new** — the report was produced and the requested finding is not in it | `ExitCodes.FindingNotReported` |

**Why not `0` with a message.** Exit codes are the API a shell reads; standard error is not. A
script asking *is f_x still reported* — the exact question `docs/cli/command-reference.md:352-357`
says the id exists to answer — would have to parse stdout to learn the answer, and for `--format text`
there is no stdout to parse. `grep` returns `1` for *no match* and nobody reads that as `grep`
failing; the code answers the question asked.

**Why not `2`.** `2` is *I could not look* (`ExitCodes.cs:14-16`, `ReportCommand.cs:152-155`).
Here the tool looked, resolved a window, ran every provider and produced a report. Collapsing the
two would make a missing store indistinguishable from a healed test, which is the distinction the
existing three codes were built to keep.

**Why not `1`.** `1` is a severity threshold, and D6 rejects `--fail-on` with `--id` precisely so
that `1` keeps one meaning.

**Reads as "no longer reported", never as failure.** The stderr line says *not reported in this
window*, names the window, and — when D3 finds the subject under another kind — names where the
claim went. It never says *error*, *failed* or *not found*: an id that was on screen yesterday and is
absent today is the report working.

**The window is the honest boundary.** `--runs 5 --id f_x` for a finding that needs twenty runs of
evidence exits `3`. The finding is not in that report, and the message names the window so the
reader can widen it. No attempt is made to search other windows.

### D2a — A malformed id is rejected at parse time, exit `2`

The shape is `f_` followed by exactly eight hexadecimal digits, matching what `FindingId.Compute`
emits (`FindingId.cs:39, 59-63`). The `--id` option carries a `CustomParser`, following the
unknown-kind parser at `Program.cs:168-183`, that rejects anything else with:

```
--id expects a finding id of the form f_ followed by 8 hex digits, got 'f_2a91'.
```

Hex digits are accepted in either case and normalised to lower case before selection; the id is a
hash rendered with `x2` (`FindingId.cs:61`) and nothing in the tool is case-sensitive about it
(`--kind` parses `ignoreCase: true`, `Program.cs:174`).

**Why parse-time and not not-found.** A typo in the shape and an id that has moved are different
user errors with different fixes: the first is corrected by looking at the trailer again, the second
by reading D3's message. One message for both teaches the reader that a healed finding and a
mistyped one look alike. The parse error also fires before the store is opened, which is where every
other rejected command line fails (`Program.cs:62-74`).

Six-character ids such as the fixtures' `f_2a91` (`ReportFixtures.cs:774`) are malformed by this
rule. They never reach the parser — fixtures are envelopes, not command lines — but any test that
drives `--id` through `Program.Run` must use an id the coordinator computed (P3).

### D3 — Not-found does a reverse lookup by subject key and points at the current id

When the id matches no finding, the selector asks, for every finding in hand and every `FindingKind`
other than that finding's own, whether `FindingId.Compute(kind, finding.Subject.SortKey)` equals the
requested id. Thirteen kinds times a few dozen findings is a few hundred short hashes; every input is
already in memory.

A hit establishes two things the message can say: the **kind the id named** and the **subject it
named**, whose current findings are then listed. Three message forms, all on standard error:

```
Finding f_66c10ec3 is not reported in this window (SampleApp.XUnit, 20 runs).
```

```
Finding f_66c10ec3 (slower) is not reported in this window (SampleApp.XUnit,
20 runs). Its subject is reported as f_445c562e (unstable timing, row 4).
```

```
Finding f_7c905f05 (shared failure) is not reported in this window
(SampleApp.XUnit, 20 runs). Its subject is reported as f_3ea537e4
(broken fixture, row 3).
```

Where the store holds other assemblies, one more sentence follows, on the model of the scope notice
(`ReportCommand.cs:250-265`): `1 other assembly in this store (use --assembly to switch).` The most
common reason an id is absent is that the report auto-scoped to a different suite.

Kind labels are `ReportVocabulary.LabelFor` (`ReportVocabulary.cs:41-53`); row numbers are positions
in the ordered list, the same numbers the full report prints. The message is composed in
`ReportCommand` from the `FindingSelection` result, in ASCII, and wrapped only by the terminal — it is
on standard error, outside the fence and its width rule.

**The tool does not redirect.** It could render the current finding under the old id, and must not:
a promoted cluster is a different claim (`FailureModeProvider.cs:481-486`), and
`docs/cli/command-reference.md:359-360` promises that the id names the claim. Showing a `BrokenFixture`
finding to someone who asked for a `SharedFailure` one, silently, breaks that promise. The message
names the new id and the reader types it.

**In JSON,** the not-found envelope is still emitted on stdout (D5), with `findings` empty and
`selection` carrying the same facts: `reported: false`, the kind reverse-looked-up when there was one,
and `sameSubject` listing where the claim went. Exit code `3` either way.

**The same lookup serves the found case.** When the id is reported, `sameSubject` lists the other
findings about the same subject — the D6 siblings, by the same key `FindingOrder.Test` reads
(`FindingOrder.cs:124`) — and the detail view prints them as one closing line (D4). This is the
question the format spec's D7 said `--id` could not answer, *what else is there about this test*,
answered on the same screen.

**Rejected:** a third tier that says *the test is still in the window and carries no finding*, by
hashing every kind against every fingerprint in `context.Tests.Fingerprints`
(`EnvelopeBuilder.cs:60`). Cheap, and honest, but it needs a test's name from its fingerprint, and
`TestIndex.ReferenceFor` supplies the name (Amendment 1B); it is left out to keep this spec to the
findings the coordinator produced. Recorded in §7 as the first thing to add.

### D4 — What the text detail view renders

The detail view is the finding's own row, exactly as the full report prints it, followed by what
the row omits. Nothing in it is phrased for the first time: every string is one the envelope already
carries or one `EvidenceHeadline` already resolves.

**Above the fence:** the two header lines and the caveat line, unchanged. The reader still needs to
know what was analysed.

**Inside the fence, in order:**

1. A heading in the D5 shape: label `FINDING`, right-hand annotation `row N of M` where `N` is the
   finding's position in the full ordered list and `M` is `Summary.Findings`. Wording is **OQ-1**.
2. The row, rendered by the same `WriteFinding` as the full report — number `N.`, marker, kind
   label, annotation, name, headline, trailer — with one difference for group subjects: the member
   list is **uncapped**, and each member is followed by a dim line carrying its source location when
   one was recorded. The row already lists three members; a detail view that listed three and then
   listed all of them again would print the cause's members twice.
3. A blank line, then the **subject block**, for single-test subjects only: labelled pairs `test`
   (the `FullyQualifiedName`), `assembly`, and `source` (`SourceFile:SourceLineNumber`, the whole path,
   never `FitPath`-elided). A pair whose value is null is omitted. Group subjects have no block: the
   cause is the subject line, the members are listed above with their locations, and a group's
   `SubjectDto.Assembly` is null (`EnvelopeBuilder.cs:345-352`).
4. A blank line, then the **metrics block**: one labelled pair per `FindingDto.Metrics` entry, in
   order. Labels are lower case as `MetricDto` documents (`ReportEnvelope.cs:339`).
5. A blank line, then the **same-subject line**, only when `Selection.SameSubject` is non-empty:
   `Also about this test: #4 unstable timing (f_66c10ec3)`, entries joined by `, `, wrapped like
   every other line. Composed in the renderer from resolved row numbers, kinds and ids, on the
   precedent of the latest-run section's *also failing* line (`TextReportRenderer.cs:602-635`).

**Labelled-pair layout.** Indent 4. The label column is the longest label in that block plus two
spaces, computed per block; values start after it and are wrapped to the remaining budget with
continuations aligned under the value. A value with no spaces — a qualified name, a path — wraps at
a `.` or `/` boundary, the separator ending the line; a single segment wider than the budget is
emitted whole and overflows, which is D10.3's existing exemption. `FitName` and `FitPath` are not
used in the blocks: their job is to cut, and the detail view's job is to not.

**What is deliberately not printed as text.** `Fingerprint` and `GroupId` are hashes a reader does
not act on and are longer than the value column; they stay in JSON. `DisplayName` is prose the
format spec's OQ-1 has not admitted. `EvidenceSessions` is already the denominator every
`significance` metric states.

**Exemplars, stack frames, signature messages: deferred, and not because of the fence.** The prompt
that led to this spec framed the width and charset rules as the obstacle. They are real —
`ExemplarCharBudget` (`LocalAnalysisConstants.cs:448`) bounds a message at 500 characters, which is
seven fenced lines, and a user's error text is the one input the ASCII rule cannot be asserted over —
but they are solvable with a wrap and a charset pass. The actual reason is architectural: the
evidence payloads are thirteen records (§1.1) whose exemplar shapes differ per kind
(`FailureExemplar`, `DurationExemplar`, `RetryAttemptExemplar`, `RetryMaskedExemplar`,
`ConcurrencyExemplar`, `TimeExemplar`), and rendering them means either a renderer that walks
`JsonNode` and phrases as it goes — forbidden by the renderer contract (`IReportRenderer.cs:15-18`) —
or a builder that resolves them into a new, uniform `ExemplarDto` with its own lines. That is a data
contract change with its own decisions about which fields each kind shows, and it belongs in its own
spec, where those decisions get argued rather than inherited. §7 reserves it.

**Against my own framing: the tight scope is thin for the commonest kind.** A `Flaky` finding's
three metrics restate its headline (`EvidenceHeadline.cs:204-218`), and the one thing a reader of
`3 failure modes` wants — *which three* — is in `FlakyEvidence.DistinctSignatures`
(`FailureModeProvider.cs:107`) and not in the metrics. So the scope is widened by exactly one pair
per signature, resolved where every other metric is:

> **D4.1 — `Flaky` metrics name each failure mode.** `EvidenceHeadline.Flaky` appends one pair per
> entry of `DistinctSignatures`, labelled `failure mode 1`, `failure mode 2`, …, whose value is the
> signature's `ExceptionType` or `not recorded by the adapter` — the same fallback
> `AlwaysFailing` already uses (`EvidenceHeadline.cs:245`). Messages and frames are not included;
> they are the deferred payload above. This changes the `metrics` array of every `Flaky` finding in
> JSON, which the schema bump (D9) covers, and changes no text output of the full report.

**Below the fence:** the population legend as today — the row printed a marker, so the legend
explains it — then the truncation line, which under `--id` always fires (D5) and is the way back to
the full report. The D8 detail line (D7 here) is **not** printed under `--id`; it would name the
command the reader just ran.

**The latest-run section is suppressed in text under `--id`.** It is an account of the newest run;
the reader asked about one finding. In JSON it is unchanged (D5).

### D5 — JSON is the full envelope with one finding, plus `selection`

`--id --format json` emits the whole `ReportEnvelope`: `schemaVersion`, `window`, `context`,
`summary`, `latestRun` and `truncated` exactly as the unfiltered report would produce them, and
`findings` holding the one selected finding. A new `selection` member, positioned before
`findings`, carries what `--id` resolved (§4.2).

`summary.findings` is the count produced and `findings.length` is `1`, which is the relationship
`--top` already establishes (`EnvelopeBuilder.cs:74-77`, `ReportEnvelopeTests.cs:745-761`).
`truncated` is `{ shown: 1, total: M, command: "xping report --all" }` — literally true, and the
same string the text footer prints. `latestRun` is computed from the unfiltered findings and is
byte-identical to the full report's; an agent reading a finding gets the run it came from beside it.

**Not found:** the same envelope with `findings: []`, `truncated.shown: 0`, and
`selection.reported: false`. Emitted on stdout, exit `3`, the D3 message on stderr. A consumer keyed
on `selection.reported` never has to parse stderr.

**Rejected: a bare `FindingDto`.** It would be a second output contract with no `schemaVersion`, no
window to say which runs the numbers came from, and nowhere for `selection` to live. The envelope's
own remarks make the case (`ReportEnvelope.cs:14-18`): one shape both renderers consume.

**Rejected: `summary` and `latestRun` narrowed to the selection.** Both are facts about the run,
not about the finding. A summary that said `findings: 1` because one was selected would disagree
with the summary the same store printed a second ago.

### D6 — Flag interactions

Rejected combinations are parse errors from the validator at `Program.cs:242-265`, exit `2`, with
the existing `--runs and --since are mutually exclusive.` phrasing. Presence is tested with
`GetResult`, for the reason the validator's comment gives (`Program.cs:239-241`).

| With | Rule | Reasoning |
|---|---|---|
| `--kind` | **rejected** | D1. The id already names a kind, and the filter changes which findings exist. Pinned today by `TheKindFilterNarrowsTheFindings` (`ReportEnvelopeTests.cs:932-940`); under `--id` there is nothing for it to narrow. |
| `--top`, `--all` | **rejected** | `--id` is its own row selection. `--all` is the truncation command the detail view prints as the way back, and accepting it here would make one flag mean *everything* and *this one*. `TruncationIsAccurateAndTheFullListIsReachable` (`ReportEnvelopeTests.cs:745-761`) stays as it is. |
| `--fail-on` | **rejected** | Two reasons. `1` keeps one meaning (D2). And an id is not a stable CI key: it moves on reclassification (§1.1), so a gate written as *fail while f_x is high* would pass silently the day the claim was promoted. A build gate belongs on the full report, where `ExitCodes.ForReport` already judges every finding (`ExitCodes.cs:39-42`). `TruncationDoesNotChangeTheExitCode` (`ReportEnvelopeTests.cs:764-776`) is unaffected. |
| `--summary`, `--format summary` | **rejected** | The one-liner states counts and nothing else, by its own remarks (`SummaryReportRenderer.cs:15-18`): a line that named a test would be the one place the report's phrasing is not the fence's. A one-line finding is a different feature. |
| `--json`, `--format json` | permitted | D5. |
| `--assembly` | permitted | The id is a fact about a window, and the window is scoped to one assembly (`ReportCommand.cs:45-65`). An id from another suite is not reported here, and the D3 message says so and names the flag. |
| `--runs`, `--since` | permitted | Same reasoning: the window decides what is reported, and a finding that needs twenty runs of evidence is honestly absent from five (D2). |
| `--ascii`, `--no-color`, `--directory` | permitted | Orthogonal. |

**The latest-run section** is suppressed in text and unchanged in JSON (D4, D5). Its analysis runs
over the unfiltered findings, as it does today without `--kind` (`ReportCommand.cs:90-91`).

**Scope notice and cloud invitation** follow the existing rule — text format, terminal only
(`ReportCommand.cs:119-136`) — and are unchanged. The scope notice matters more here than in the
full report: it is the first thing to read when an id is not found.

### D7 — DrillDown emits `--id`, and the coordinator assigns it

`DrillDown.ForTest` and `DrillDown.ForGroup` are **deleted**, replaced by one method:

```csharp
public static string ForFinding(string id, string? assembly)
// "xping report --id f_445c562e"                             assembly empty
// "xping report --id f_445c562e --assembly SampleApp.XUnit"   otherwise, Quote() as today
```

**The string is assigned where the id is.** `FindingCandidate.DrillDownCommand`
(`IFindingProvider.cs:95`) and its twelve provider call sites (§1.1) are deleted. The coordinator,
which computes the id at `FindingCoordinator.cs:192`, computes the command beside it and sets
`Finding.DrillDownCommand` from `ForFinding(id, subject.Tests.FirstOrDefault()?.Assembly)` — the first test's
assembly for both subject types, which is what `FailureModeProvider.cs:490` already does for a
cluster. A provider was carrying the command only because it was the one place with the kind in
hand; the id is the coordinator's to assign, and so is the command that names it.

**No `--format json`.** Today's per-finding command carries it and `ForFullReport` does not, for
the reason `DrillDown.cs:66-71` gives: a caller that wants the envelope adds the flag, having just
used it. The same reasoning applies to a per-finding command now that a text detail view exists —
a human who copies the command from a pasted JSON wants the fenced view — and it makes the one string
serve both the envelope and the below-fence line without a second variant. This is the one behaviour
`DrillDown.ForFullReport` and `ForFinding` share, and it is now stated once.

**The three retry tests** that assert the old string (§1.2 item 2) are deleted with the member
they read; the Vanished and TimeSensitive assertions, which read `Finding.DrillDownCommand`, assert
`--id` instead (Amendment 1C). Their replacement is one coordinator test: every finding's `DrillDownCommand` equals
`ForFinding(finding.Id, firstTestAssembly)`.

**Text gains one line below the fence, only in the full report:**

```
Detail of row 1: xping report --id f_c7b87f12 --assembly SampleApp.XUnit
```

Dim, after the legend and the truncation line, emitted only when at least one finding was printed.
It is `Selection`-free output: under `--id` it is not printed (D4). The string is
`Findings[0].DrillDown` — a real command for the most severe row, which is where a reader starts —
and never a template. Four alternatives, three rejected on the format spec's own D7 grounds:

- **A line per finding.** Ten near-identical commands in a pasted block, the argument
  `TextReportRenderer.cs:808-810` makes against exactly this.
- **A template**, `xping report --id <id>`. A command the reader must edit before running, which
  the format spec's D7 held to be below `DrillDown`'s standard. It still is.
- **Nothing.** The feature is then discoverable only from `--help`, and a report that prints ten
  ids and never says what to do with one is a report that trained its readers to ignore them.
- **One real command for row 1** — chosen. It runs as printed, demonstrates the syntax with a value
  the reader can see two lines up, and the substitution for row 4 is obvious.

The line may exceed 72 columns when an assembly name forces it. It is outside the fence, where the
header already runs to 75 (`cli-report-format-spec.md` §3), and a command cut to fit would be a
command that does not run.

**The format spec's D8 below-fence order** becomes legend, truncation line, detail line — its
Amendment 2 records this. **`ShareableOutputTests.TheRestOfTheFindingsAreOfferedOnlyWhenSomeWereWithheld`**
asserts that a complete report ends on the legend's last line (`ShareableOutputTests.cs:1086-1091`)
and moves with it. Every golden whose fixture has a finding gains one line; P5 regenerates them.

**Code comments move with the code.** `DrillDown.cs:13-24` and `FindingId.cs:15-22` stop naming
`xping test` and name `xping report --id` (format spec Amendment 2).

### D8 — Exit-code and stream discipline, restated for the new paths

- Standard output carries the report and nothing else, on every format, as today. Under `--id` with
  `--format text` and a not-reported id, stdout is empty.
- Every message this spec adds goes to standard error: the D3 not-reported forms, and nothing else.
  `WarningsGoToStandardErrorSoJsonStaysParsable` (`ReportEnvelopeTests.cs:894-907`) is the rule and
  is extended to the not-found envelope.
- Precedence: parse errors (`2`, including D2a and D6) before the store is opened; unavailable-report
  paths (`2`, `ReportCommand.cs:156-190, 231-241`) before selection; not reported (`3`); else `0`.
  `1` is unreachable under `--id` (D6).

### D9 — Schema bumps to 1.21

`ReportEnvelope.CurrentSchemaVersion` = `"1.21"`, read from `ReportEnvelope.cs:69` as `"1.20"` at the
time of writing and bumped from there. Changed shapes: `ReportEnvelope` gains `Selection` (§4.1);
`FindingDto.DrillDown` changes from `--kind {kind} --format json` to `--id {id}` (D7); every `Flaky`
finding's `metrics` gains one pair per distinct signature (D4.1).

Three places pin the literal and move together, as the format spec's D9 recorded for `1.19`:
`ReportEnvelope.CurrentSchemaVersion`, `TheEnvelopeCarriesEveryDocumentedSection`
(`ReportEnvelopeTests.cs:329`), and the `--format json` sample in `docs/cli/command-reference.md`
(`:518` region). `CliSurfaceTests.cs:390, 406` pin it too.

### D10 — The rules are authoritative; goldens are generated

`cli-report-format-spec.md` D10 applies here unchanged and is restated because this spec adds a
second layout under the same renderer:

1. §2 outranks §3. A conflict between a decision and an example is a defect in the example.
2. Every detail golden is generated from a fixture whose envelope carries `Selection`, through the
   existing `GoldenReportTests` harness, and never hand-typed. Detail goldens whose metrics have to be
   real are built from evidence through `EvidenceHeadline` — `ShareableOutputTests.EvidenceFor`
   already constructs every one of the thirteen shapes (`ShareableOutputTests.cs:56-198`) — or from
   a store (P5).
3. Every line inside the fence is width-asserted across every fixture, the D10.3 single-token
   exemption included. The labelled-pair wrap rule (D4) exists so that the exemption is reached only
   by a single segment, never by a whole qualified name.
4. Non-ASCII appears only where a `ReportGlyphs` pair exists (`src/Xping.Cli/Reporting/ReportGlyphs.cs:21-78`).
   The detail view introduces no new glyph. User-recorded text — an exception type, a retry
   attribute's declared reason, a time zone id — passes through the metrics as it already passes
   through headlines (`EvidenceHeadline.cs:238-239`); the charset assertion is over the renderer's
   own output on ASCII fixtures, as it is today.

---

## 3. Target output

Illustrative of the rules. **Where a block and §2 disagree, §2 wins** (D10). Blocks are measured to
72 columns inside the fence; the header line is the existing `WriteHeader` output and outside the
rule.

### 3.1 A single-test finding

`xping report --id f_445c562e`, on the store behind the format spec's §3:

```
Xping · SampleApp.XUnit · 20 runs · 2026-09-05 09:00 → 16:21 · main@eab9867
16 tests · 10 healthy · 6 flagged

FINDING                                                       row 5 of 6
────────────────────────────────────────────────────────────────────────

5.  HIGH  flaky                                          same test as #4
    SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState
    failed 6 of 20 executions (30%) in 6 of 20 runs, 1 failure mode
    evidence high | -env-cluster | f_445c562e | .../SampleTests.cs:83

    test      SampleApp.XUnit.SampleTests.
              FlakyTest_EnvironmentState_FailsBasedOnSystemState
    assembly  SampleApp.XUnit
    source    tests/SampleApp.XUnit/SampleTests.cs:83

    failed          6 of 20 executions (30%)
    runs affected   6 of 20
    failure modes   1
    failure mode 1  System.InvalidOperationException

    Also about this test: #4 unstable timing (f_66c10ec3)
```

Below the fence: the three legend lines, then `Showing 1 of 6 · all: xping report --all`. No
detail line (D7).

Measured against the rules: the heading annotation is right-aligned to 72; the row is byte-identical
to row 5 of the format spec's §3; the `test` value wrapped at its last dot because the whole name is
84 columns from its column; the label columns are `assembly` + 2 and `failure mode 1` + 2 within
their blocks; the closing line is the D3 lookup, naming row 4 by the number the full report gives it.

### 3.2 A group finding

`xping report --id f_7c905f05`:

```
FINDING                                                       row 3 of 6
────────────────────────────────────────────────────────────────────────

3.  HIGH  broken fixture
    UnprovisionedDatabase..ctor (3 tests)
      FixtureTests.FirstTestNeedingTheDatabase
        tests/SampleApp.XUnit/FixtureTests.cs:12
      FixtureTests.SecondTestNeedingTheDatabase
        tests/SampleApp.XUnit/FixtureTests.cs:19
      FixtureTests.ThirdTestNeedingTheDatabase
        tests/SampleApp.XUnit/FixtureTests.cs:26
    UnprovisionedDatabase..ctor failed, blocking 3 tests in 20 of 20
    runs
    evidence high | all runs | f_7c905f05

    failing member  UnprovisionedDatabase..ctor
    where           fixture setup
    tests blocked   3
    failures        60
    runs affected   20 of 20
    worst run       3 tests
```

No subject block (D4 item 3), members uncapped with locations (D4 item 2), no same-subject line
because a cluster has no siblings by key. The member location lines are dim, at the member indent
plus two, and go through `FitPath` only in the sense that they never do: a location that does not
fit overflows as one token.

### 3.3 The full report's new last line

The format spec's §3 report, unchanged inside the fence, now ends:

```
all runs: every execution in the window; -env: environmental runs set
aside; -env-cluster: those and clustered failures; -partial: runs that
covered part of the suite. Compare two only where the markers match.
https://docs.xping.io/cli/command-reference.html#the-population-marker

Detail of row 1: xping report --id f_c7b87f12 --assembly SampleApp.XUnit
```

The legend text above is whatever `ReportVocabulary.PopulationLegend` holds and is not specified
here; it is shown so the position of the new line is unambiguous. On a truncated report the
truncation line sits between the two.

### 3.4 Not reported

`xping report --id f_66c10ec3` after the unstable finding was superseded by a regression. Standard
output is empty; standard error:

```
Finding f_66c10ec3 (unstable timing) is not reported in this window (SampleApp.XUnit, 20 runs). Its subject is reported as f_c1d2e3f4 (slower, row 2).
```

Exit `3`. With `--format json`, stdout carries the envelope with `findings: []` and
`selection.reported: false` (§4.2) and the same line goes to stderr.

### 3.5 What the goldens say

Per D10 the goldens are authority. P5 adds fixtures `detail-test`, `detail-group`,
`detail-<kind>` for each of the thirteen kinds, and `detail-store` from a real store; §3.1 and §3.2
are checked against `detail-test` and `detail-group`, not the reverse. Every existing fixture with a
finding regenerates for §3.3.

---

## 4. Data contract

### 4.1 `ReportEnvelope`

```csharp
internal sealed record ReportEnvelope(
    string SchemaVersion,
    WindowDto Window,
    ContextDto? Context,
    SummaryDto Summary,
    LatestRunDto? LatestRun,
    SelectionDto? Selection,   // NEW - null unless --id was given
    IReadOnlyList<FindingDto> Findings,
    TruncationDto Truncated);
```

Positioned before `Findings`, which it qualifies. Property declaration order is serialised order and
is part of the contract. Written as `null` rather than omitted, per `ReportJsonOptions`'s rule that a
consumer can tell *no value* from *field removed* (`ReportJsonOptions.cs:23-24`).

### 4.2 `SelectionDto` and `SameSubjectDto`

```csharp
/// <param name="Id">The id as requested, normalised to lower case (D2a).</param>
/// <param name="Reported">Whether a finding with this id is in the report (D2).</param>
/// <param name="Kind">
/// The kind the id names: the finding's own when reported; the kind the reverse lookup
/// established when not (D3); null when the lookup found nothing.
/// </param>
/// <param name="Row">
/// The finding's position, from 1, in the full ordered list — the number the full report prints
/// for it. Null when not reported.
/// </param>
/// <param name="SameSubject">
/// Every other finding about the subject this id names, in envelope order. When reported, its
/// siblings; when not, where the claim went. Empty when there are none or the subject is unknown.
/// </param>
internal sealed record SelectionDto(
    string Id,
    bool Reported,
    string? Kind,
    int? Row,
    IReadOnlyList<SameSubjectDto> SameSubject);

/// <param name="Id">The finding's id.</param>
/// <param name="Kind">Its kind, spelled as findings[].kind.</param>
/// <param name="Row">Its position in the full ordered list, from 1.</param>
internal sealed record SameSubjectDto(string Id, string Kind, int Row);
```

`Kind` is spelled as `FindingDto.Kind` is — the enum name, `DurationRegression` — for the reason
`EnvelopeBuilder.NotMeasured` gives about matching `--kind` (`EnvelopeBuilder.cs:242-247`).

### 4.3 `FindingSelection` (analysis-side, not serialised)

```csharp
/// <summary>What --id resolved to, over the ordered finding list.</summary>
internal sealed record FindingSelection(
    string Id,
    Finding? Finding,                // null when not reported
    int? Row,
    FindingKind? Kind,
    IReadOnlyList<(Finding Finding, int Row)> SameSubject);
```

Produced by `FindingSelector.Select(IReadOnlyList<Finding> ordered, string id)`, a pure static in
`src/Xping.Cli/Report/`. `ordered` is `FindingOrder.WithSiblingsAdjacent(analysis.Findings)`.

### 4.4 `EnvelopeBuilder.Build`

Gains a trailing `FindingSelection? selection = null` parameter. When non-null, `shown` is the
selected finding alone (or empty), `Selection` is projected from it, and everything else is built
exactly as today from the full `AnalysisResult`. Eight call sites: `ReportCommand.cs:93`,
`ReportFixtures.cs:387`, and six provider test files (`ParallelSensitiveProviderTests.cs:802`,
`TimeSensitiveProviderTests.cs:1240`, `DurationProviderTests.cs:1771`,
`FailureModeProviderTests.cs:1541, 1557`, `RetryProviderTests.cs:716`). A defaulted parameter leaves
all seven test call sites untouched.

### 4.5 `ReportOptions`, `ExitCodes`, `DrillDown`, `Finding`

- `ReportOptions` gains `string? Id`.
- `ExitCodes` gains `public const int FindingNotReported = 3;` (D2).
- `DrillDown` loses `ForTest` and `ForGroup`, gains `ForFinding(string id, string? assembly)` (D7).
  `ForFullReport` is unchanged.
- `FindingCandidate` loses `DrillDownCommand`; `Finding.DrillDownCommand` stays and is set by the
  coordinator (D7).

### 4.6 Unchanged

`WindowDto`, `ContextDto`, `SummaryDto`, `LatestRunDto`, `LatestRunFailureDto`, `SubjectDto`,
`MetricDto`, `TruncationDto`, `SeverityCountsDto`, `NotMeasuredDto`, `ReportJsonOptions`, every
evidence payload's shape, every provider's analysis, every constant in `LocalAnalysisConstants`,
`FindingId.Compute`, `FindingOrder`, `LatestRunAnalyzer`, `SummaryReportRenderer`.

**No analysis behaviour changes in this spec.** No finding is emitted, suppressed, reranked or
rescored. `--id` reads the coordinator's output and adds nothing to its inputs.

---

## 5. Open questions — gates

### OQ-1 — Detail heading label. **Closed**

**Resolved: `FINDING` with annotation `row N of M`**, as recommended. The
shape — uppercase label, right-aligned annotation, rule — is the format spec's D5 and is not in
question.

### OQ-2 — Below-fence detail line wording. **Closed**

**Resolved: `Detail of row 1: `**, as recommended. The rule that it is one
real command for row 1, dim, last, and absent under `--id`, is D7 and is not in question.

---

## 6. Implementation sequence

One prompt per phase. Each phase ends green: builds, tests pass, `xping report` runs.

### P0 — Inventory. Read-only. No code.

1. Confirm every citation in §1.1 and §1.2 against source. Report any divergence as a spec conflict.
2. Confirm `cli-report-format-spec.md` Amendment 2 is committed.
3. Read `ReportEnvelope.CurrentSchemaVersion`. If it is not `"1.20"`, stop: D9's number is derived
   from it and this document needs an amendment before P2.
4. List every test that reads `FindingCandidate.DrillDownCommand` or asserts a `drillDown` string,
   beyond the five named in §1.2 and `ReportEnvelopeTests.cs:699`.
5. List every test that asserts on the last line of a rendered report or on the count of lines
   below the fence, beyond `ShareableOutputTests.cs:1086-1091`.
6. Confirm `TestIndex` exposes no lookup from fingerprint to `TestReference`. If it does, note it
   against §7's first item; it does not change this spec.
7. Confirm the golden count is 50 and the documented-sample document count is ten.

**Stop and report.** Write nothing.

### P1 — `FindingSelector`

Pure component: an ordered finding list and an id in, a `FindingSelection` out (§4.3). No CLI, no
envelope, no rendering.

Unit tests: id reported; id absent with no subject match; id absent and reverse-looked-up to a
promoted cluster (`SharedFailure` id, `BrokenFixture` current); id absent and reverse-looked-up
across the duration handover; reported finding with two siblings, rows in envelope order; uppercase
hex input matches; row numbers agree with `EnvelopeBuilder`'s numbering on a list that
`WithSiblingsAdjacent` reorders. Findings in these tests are built directly, as
`FindingCoordinatorTests` builds them, with ids from `FindingId.Compute`.

### P2 — Contract

`SelectionDto`, `SameSubjectDto`, `ReportEnvelope.Selection`, the `Build` parameter (§4.4). D7: the
coordinator assigns `DrillDownCommand`; `FindingCandidate.DrillDownCommand` and the twelve call
sites go; `DrillDown.ForTest`/`ForGroup` go; the three retry tests go, the two coordinator-output
assertions move to `--id`, and the coordinator test arrives. D4.1: `Flaky` metrics. Comments at `DrillDown.cs:13-24` and `FindingId.cs:15-22` name
`xping report --id`. Schema → `1.21`.

**Text output is byte-identical after this phase.** JSON: `selection: null` on every report, new
`drillDown` strings, extra `Flaky` metrics, `1.21`. Update `TheEnvelopeCarriesEveryDocumentedSection`
and `CliSurfaceTests` for the number, and add `selection` to the former's key sweep.

### P3 — CLI surface and exit code

`--id` option with the D2a parser; D6 validators; `ReportOptions.Id`; `ExitCodes.FindingNotReported`;
`ReportCommand` runs the coordinator with `kinds: null` when `Id` is set, selects, builds with the
selection, writes the D3 message, returns `3` or `0`. JSON not-found envelope (D5).

Tests drive `Program.Run` against a **real store** seeded as `ReportEnvelopeTests.SeedVanishing`
seeds one (`ReportEnvelopeTests.cs:73-91`): ids are ten characters and are read back from a first
`--format json` run, never typed. The fixtures' six-character ids cannot be used here (D2a). Cover:
found (exit `0`, `selection.row`, `findings.length == 1`, `summary` equal to the unfiltered run's);
absent (exit `3`, empty `findings`, message on stderr, stdout parsable); malformed (exit `2`, shape
hint); each D6 rejection (exit `2`, message); `--assembly` scoping an id out and the message naming
the other assembly; `--runs` narrowing an id out; `--kind` under `--id` rejected before the store
is touched.

### P4 — Text detail renderer

D4 in `TextReportRenderer`: the heading, the row via the existing `WriteFinding`, uncapped members
with locations, the labelled-pair blocks and their wrap rule, the same-subject line, latest-run
suppression, no detail line. D7's below-fence detail line on the full report. Gate on **OQ-1** and
**OQ-2**.

Assert: no line inside the fence exceeds `FenceWidth` except a single unbreakable token; a value
with dots wraps at a dot and not mid-segment; a 40-member cluster prints 40 members; colour changes
only escape sequences (the heading annotation is padded from the plain label).
`TheRestOfTheFindingsAreOfferedOnlyWhenSomeWereWithheld` moves to the new last line.

### P5 — Goldens and documented samples

Add the fixtures named in §3.5. `detail-<kind>` fixtures take their evidence from a builder like
`ShareableOutputTests.EvidenceFor` so every kind's metrics block is pinned; `detail-store` runs the
whole pipeline as `ReportFixtures.LatestRunFromStore` does (`ReportFixtures.cs:336-395`) and selects
a real id from its output. Regenerate all 50 existing goldens with `XPING_UPDATE_GOLDENS`: every
fixture with a finding gains the D7 line. Review the diff; it must be that line and nothing else.

Regenerate the documented samples. Only whole-report extents change — the `rows`, `trailer`,
`summary` and `latest-run` extents stop above or beside the fence (`DocumentedSampleTests.cs:130-151`).
Affected markers: `README.md:132`, `docs/index.md:163`, `docs/cli/command-reference.md:90`, the three
quickstarts (`quickstart-xunit.md:359`, `quickstart-mstest.md:383`, `quickstart-nunit.md:370`),
`local-first.md:82`, `nuspec/README.Cli.md:33`.

### P6 — Docs

`docs/cli/command-reference.md`: the option table (`:62-72`) gains `--id`; the exit-code line
(`:74`) gains `3`; `### Finding ids` (`:347-364`) gains the drill-down sentence and the
reclassification caveat; the JSON sample (`:518-540`) shows `selection`, the new `drillDown` shape
and `1.21`. `README.md:183-193` gains one line. `nuspec/README.Cli.md:83-85` gains one row.
`report --help` is the option description and needs no separate step.

---

## 7. Out of scope

- **Exemplars, stack frames and signature messages in the detail view.** Deferred by D4, for the
  architectural reason given there: they need a builder-resolved `ExemplarDto` and per-kind
  decisions about which fields to show, in their own spec. That spec starts from this one's §4.2.
- **The third not-found tier**, *the test is in the window and carries no finding*, via
  `context.Tests.Fingerprints` and `TestIndex.ReferenceFor`. Both exist; it is a scope decision,
  and the first thing to add. D3 records it.
- **`xping test`.** Retired by the format spec's Amendment 2. Not stubbed, not reserved.
- **Multiple ids** in one invocation, `--id` on `where` or `clear`, and `--fail-on` with `--id`
  (D6).
- **`xping login` and `--source local|cloud`**, promised at `README.md:327-328, 456`. Unrelated to
  this spec and untouched by it; noted so the next inventory does not rediscover them.
- Any change to finding emission, thresholds, severity, or `LocalAnalysisConstants`.

---

## 8. Reserved for Cloud

Not implemented. Recorded so the detail layout does not have to change when it is.

- **The metrics block is the annotation slot.** A Cloud-side `confidence` or `seen on N branches`
  is one more labelled pair, resolved in the builder, and the block's column arithmetic already
  absorbs a longer label.
- **`selection.sameSubject` is the cross-branch slot.** A finding about the same subject on another
  branch is one more `SameSubjectDto` with a branch field added by amendment; the same-subject line
  already lists entries by row and id.
- **The row is identical whether or not authenticated**, for the reason the format spec's §8 gives:
  nobody learns two layouts.
