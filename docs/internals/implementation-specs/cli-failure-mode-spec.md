# CLI Report — Failure Modes (`FAILURE MODES` in `--id` and `--test`)

**Status:** Draft, binding once merged
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/**`
**Schema:** `1.24` → `1.25`
**Depends on:** `cli-report-format-spec.md` D5 (heading machinery), D10 (example/rule authority,
width and charset discipline). `cli-finding-detail-spec.md` D4 (the text detail view) and D4.1
(`Flaky` metrics, superseded here by its Amendment 7). `cli-test-history-spec.md` D5 (window) and
D10 (findings block), as amended by its Amendment 10, committed with this document. Issue #270.
**Blocks:** nothing.

---

## Amendment protocol

Identical to `cli-report-format-spec.md`. This document is ground truth. An implementer who finds a
conflict stops and reports rather than adapting. Decisions and the data contract change by
amendment only, committed before dependent code. **OQ** sections are gates.

---

## 1. Ground truth

### 1.1 What exists

Every claim here was read in source at the cited line.

- **A failure mode is a `SignatureView`** (`src/Xping.Cli/Report/Providers/FailureModeProvider.cs:37-49`):
  hash, exception type, normalised message, frames, `Degraded`, `Unavailable`, `Occurrences`,
  `FirstSeenAt`, `FirstSeenSha`, `FirstSeenSessionsAgo`, `FirstSeenInLatestSession`,
  `FirstSeenAfterWindowStart`. It is built only by `ToView` (`:1094-1119`), which has three call sites:
  `DistinctSignatures` (`:1053-1092`, for `Flaky` and `AlwaysFailing`), and the broken-fixture and
  shared-failure evidence builders (`:625`, `:647`).
- **Four evidence kinds carry one**, all in `FailureModeProvider.cs`:
  - `FlakyEvidence.DistinctSignatures` (`:171`): every mode, most frequent first.
  - `AlwaysFailingEvidence.Signature` (`:227`): the modal mode only, with `ModalSignatureShare`.
  - `BrokenFixtureEvidence.Signature` (`:317`) and `SharedFailureEvidence.Signature` (`:342`).

  `TimingOutEvidence` carries none, on purpose: a killed test leaves no signature worth grouping
  (`:743-747`). The retry, duration, time, concurrency and vanished kinds carry none.
- **Frames.** `StackFrameExtractor` (`src/Xping.Sdk.Core/Services/Diagnostics/StackFrameExtractor.cs`)
  keeps at most `MaxFrames` = 5 (`:45`). The extracted frames are user frames when there are any.
  Otherwise they are framework frames with `Degraded` set. Each is a method signature with file and
  line removed, e.g. `MyApp.Services.OrderService.Place(Order order)`, innermost first.
- **Messages.** `SignatureView.Message` is `MessageNormaliser.Normalise`'s output
  (`MessageNormaliser.cs:107-177`): one line, whitespace collapsed (`:123`), run-varying values
  replaced by tokens such as `<num>`, lowercased (`:177`), at most `MaxLength` = 4096 characters
  (`:46`). The raw message in `FailureExemplar.ErrorMessage` is a different value: multi-line,
  elided at `ExemplarCharBudget` = 500 (`EvidenceText.cs:26-34`). It is not shown here.
- **First seen and count are counted over different failures** for `Flaky` and `AlwaysFailing`.
  `Occurrences` counts the failures that survived discounting: environmental sessions and
  clustered failures are removed (`FailureModeProvider.cs:1056-1067`). `FirstSeen*` is copied
  from the index's `SignatureOccurrence`, which `SignatureIndex.BuildPerTest`
  (`src/Xping.Cli/Report/Indexes/SignatureIndex.cs:202-248`) counts over every failed attempt with
  nothing discounted. A mode can therefore show a first-seen run whose failures its count excludes.
  For clusters, both the count and first seen come from the same `SignatureGroup`, environmental runs
  included (`FindClusters` remark, `FailureModeProvider.cs:446-450`).
- **The text views print part of it today.** `EvidenceHeadline.Flaky` adds up to 5 `failure mode N`
  pairs, `type: message` cut to 100 characters, then `more failure modes`
  (`src/Xping.Cli/Report/Contract/EvidenceHeadline.cs:225-245`, `FailureMode()` at `:261-274`).
  `AlwaysFailing` adds `failure mode` = the type alone (`:301`). Broken-fixture and shared-failure
  findings print no signature fields beyond their headline and cause label. No view prints frames,
  occurrences or first seen. `--test` prints no failure mode.
- **JSON carries everything** under `evidence` (`EnvelopeBuilder.cs:540-542`), but no test pins those
  field names. The renderer may not read `evidence`: "every value it prints was resolved before it
  was called" (`src/Xping.Cli/Report/Rendering/IReportRenderer.cs:15-18`).
- **Per-test signatures for `--test`.** `TestHistoryBuilder.Found` receives the `AnalysisContext`
  over the D5 window (`src/Xping.Cli/Commands/ReportCommand.cs:178`, `:318-323`).
  `context.Signatures.SignaturesOf(fingerprint)` (`SignatureIndex.cs:128-131`) returns that test's
  signatures over every failed attempt in the window, nothing discounted, ordered by count, then
  recency, then hash (`:232-245`). The hashes are the ones the providers use.
- **Nothing sanitises recorded text in report output.** The renderer prints test names, FQNs,
  locations, exception types, messages, retry reasons, branch names and the `--test` query as
  recorded. The only control-character filters in the CLI are in auth (`Auth/Flows/FlowFailures.cs:65-66`,
  `Auth/OAuthClient.cs:142`) and on `--project` (`Program.cs:599`). The normaliser collapses
  whitespace but keeps `ESC` and other non-whitespace controls. `--ascii` switches the glyph set
  only. `TheAsciiReportCarriesNothingButPrintableAscii`
  (`tests/Xping.Cli.Tests/Report/GoldenReportTests.cs:162-171`) checks for `< 0x80` and no `ESC`,
  and passes only because no fixture holds non-ASCII or control characters in recorded data.
- **Goldens:** 55 fixtures × 2 glyph sets = 110 files (`ReportFixtures.Keys`,
  `tests/Xping.Cli.Tests/Report/ReportFixtures.cs:43-88`). Documented samples that render `--id` or
  `--test`: `docs/cli/command-reference.md` markers `detail-test`, `detail-repro-flaky`,
  `history-flaky`, `history-repro`.

### 1.2 Opened in this document

- `FailureModeDto`, `FindingDto.FailureModes`, `TestHistoryDto.FailureModes` (§4).
- `TerminalText` in `Report/Rendering/` (D7).
- Fixtures `detail-failure-modes`, `detail-unavailable`, `history-failure-modes`, `hostile-text` (§3.5).

### 1.3 The question this block answers

*How does this test fail, and since when?* Today the reader of `3 failure modes` gets each mode's
type and the first 100 characters of its message. Telling a new regression from an old flake also
needs the stack frames, how often each mode happened, and when it first appeared.

---

## 2. Decisions

### D1 — Shape

One resolved record per failure mode, `FailureModeDto` (§4.2), built by `EnvelopeBuilder` from a
`SignatureView` or a `SignatureOccurrence`. The renderer prints it without reading `evidence`.

- `FindingDto.FailureModes`: the finding's modes per D2. Empty for every other kind. Never null.
- `TestHistoryDto.FailureModes`: the test's own modes per D4.

The hash is published so a consumer can join a mode to `evidence.exemplars[].signatureHash`. Dates
stay `DateTime`, which the renderer formats as it already formats `TestSessionDto.StartedAt`.

**Rejected:** one `ExemplarDto` across the six exemplar records. The exemplars are sample runs, and
three of their kinds have no failure mode at all. That is a different block, out of scope (§7).

### D2 — Which kinds

| Kind | Modes |
|---|---|
| `Flaky` | `DistinctSignatures`, all of them, in evidence order |
| `AlwaysFailing` | `Signature`, the modal mode |
| `BrokenFixture`, `SharedFailure` | `Signature` |
| every other kind | none |

`AlwaysFailing` publishes the modal mode only. Its other modes, when `ModalSignatureShare < 1`,
are not in its evidence, and adding them is out of scope (§7). The existing `dominant mode` pair
already says the modal mode does not cover every failure.

### D3 — First seen is counted over the same failures as the count

For `Flaky` and `AlwaysFailing`, `DistinctSignatures` derives `FirstSeenAt`, `FirstSeenSha` and
`FirstSeenSessionsAgo`, and through them the two `FirstSeen*` flags, from the failures it counted.
That is the oldest surviving failure per hash: `Max(SessionIndex)`, its session's `StartedAt`, and
`RevisionContext.ReadSha` of that session, as `BuildPerTest` computes them for the whole set. The
index's undiscounted `SignatureOccurrence` remains the source of the signature itself and of the
tie-break order.

The two fields sit on one printed line (D5). If the first-seen date pointed at a run the count
excludes, the line would contradict itself. Clusters are unchanged. Their count and first seen
already come from one population.

This moves `evidence` for those two kinds wherever an environmental or clustered failure was a
mode's oldest. It is a fix, and it gets a regression test: a flaky test whose oldest failure of a
mode is in an environmental session reports the first seen of its oldest non-environmental failure.

### D4 — `--test` builds its block from the test's own failures

`TestHistoryBuilder` maps `context.Signatures.SignaturesOf(fingerprint)` into `FailureModes`:

- **Population:** every failed attempt of this test over the D5 window, nothing discounted. That is
  the population the RUNS timeline draws. A failed first attempt that passed on retry is in it, as
  it is in the timeline's `✗→✓`.
- **Order:** the index's order: count, then recency, then hash.
- **Count and first seen** come from `SignatureOccurrence` (`Occurrences`, `OldestSessionIndex`,
  `FirstSeenAt`, `FirstSeenSha`). `FirstSeenAfterWindowStart` is
  `OldestSessionIndex < Window.SessionCount - 1`, as `ToView` computes it.
- **Not from findings.** A test that failed without reaching any finding's threshold still shows
  how it failed. When it is also flaky, the counts here and in its `--id` view may differ, because
  `--id` discounts. Each view states its own population: the `--test` summary line counts runs
  without discounting, and the finding's headline names the executions it considered.
- **Absent block.** When the test never failed in the window, `FailureModes` is empty and the block
  is not printed. The summary line above already says `0 failed`.

### D5 — The text block

One writer, `WriteFailureModes`, used by both views.

**Placement.** Inside the fence:

- `--id`: last, after the same-subject line.
- `--test`: after RUNS, before FINDINGS.

**Heading.** `FAILURE MODES` through `WriteHeading`, annotation `1 failure mode` or
`N failure modes`, where N is the total, not the number listed.

**Each mode.** Numbered `1.`, `2.`, … at indent 0 in the row-number column (`RowLabelWidth` = 4),
as findings are numbered:

```
1.  4 failures · first seen 08-12 09:40 at b71e004, 6 runs ago
    System.InvalidOperationException
    expected <num> but was <num>
      at MyApp.Tests.CheckoutTests.Completes()
      at MyApp.Checkout.Basket.Total()
```

1. **Count line.** `N failure`/`N failures`, the glyph separator, then when it was first seen:
   - `FirstSeenSessionsAgo == 0`: `first seen in the latest run`.
   - Otherwise, `FirstSeenAfterWindowStart`: `first seen <MM-dd HH:mm> at <sha>, <n> runs ago`
     (`1 run ago` for one).
   - Otherwise: `since the window opened, <MM-dd HH:mm> at <sha>`. The mode was already there in
     the window's oldest run, and the report cannot see further back. This is the
     `FirstSeenAfterWindowStart` contract (`FailureModeProvider.cs:33-36`).
   - No sha recorded: drop ` at <sha>`.

   The line wraps with `Wrap` to `ContinuationBudget` (68), continuing at indent 4.
2. **Type line**, indent 4: `ExceptionType`, or `exception type not recorded` when null. A type
   with no break in reach overflows as one token (detail spec Amendment 2D).
3. **Message**, indent 4, wrapped to `ContinuationBudget` (68) with `Wrap`. At most **6** lines. If
   it is cut, the sixth line ends in `...` within the 68 columns. Omitted when the message is
   empty.
4. **Frames**, indent 6, each `at <frame>`, every frame (at most 5). A frame that does not fit
   breaks once, after the last `.` before its first `(` that fits, and continues at indent 9, so
   the method name and its arguments stay together. Only a remainder still too wide wraps
   further, at spaces. No such `.` in reach: the frame overflows as one token. When `Degraded` and there are
   frames, one more dim line at indent 6: `(no test code on the stack; framework frames shown)`.
   No frames: no frame lines and no note.
5. **Unavailable.** When `Unavailable`, the mode prints its count line and one line at indent 4,
   `the adapter recorded no failure detail`, and nothing else.

One blank line between modes.

**Cap.** At most **5** modes are printed, the same cap D4.1 used. Past that, one line at indent 4:
`N more failure modes in --format json`.

**Charset.** The separator is `ReportGlyphs.Separator`. Every other character the block adds is
ASCII. Recorded text goes through D7.

**Rejected:** labelled pairs (`type`, `message`, `frames`) for each mode. With five modes that is
twenty pairs in one aligned column, and the frames column would start 13 characters in, leaving 55
for a frame.

### D6 — D4.1's pairs go

`EvidenceHeadline`:

- `Flaky` drops the `failure mode N` and `more failure modes` pairs. It keeps
  `failure modes  <count>`.
- `AlwaysFailing` drops `failure mode` and keeps `dominant mode`. Its headline keeps its `: <type>`.
- `FailureModesListed`, `FailureModeMessageLength` and `FailureMode()` are deleted.

The block replaces them: the same information, plus the parts the pairs could not hold. JSON's
`metrics` lose the same pairs, and `failureModes` carries the data.

### D7 — Recorded text is made safe for the terminal

`TerminalText.Printable(string text, ReportGlyphs glyphs)` in `Report/Rendering/` returns the text
with every unsafe character replaced by `?`. It works per `Rune`, so an astral character becomes one
`?`.

- **Always unsafe:** C0 controls (U+0000–U+001F, which includes `ESC`, `CR`, `LF` and tab), `DEL`
  (U+007F), C1 controls (U+0080–U+009F), bidi embeddings and overrides (U+202A–U+202E), and bidi
  isolates (U+2066–U+2069).
- **Unsafe under `ReportGlyphs.Ascii`:** every rune ≥ U+0080. Under `--ascii` or piped output the
  report is ASCII end to end, which is what `APipedReportCarriesNothingButTheReport` already
  asserts.

**Where it applies.** Every value from a DTO that came from a recorded session, a store file or
Xping Cloud, at the point a text renderer appends it:

- the header: assembly, branch, sha;
- subject names, cause labels and member names;
- locations and the trailer's path;
- metric values;
- headlines (they embed types and member names);
- annotations;
- latest-run failure summaries;
- the `--test` query and FQN, and `FindingsWithheld`;
- every `FailureModeDto` field;
- `CloudText` values;
- `SummaryReportRenderer`'s assembly.

`ReportCommand`'s stderr messages that echo the query, candidate FQNs or assemblies
(`TestResolver.NotFoundMessage`, `WriteAmbiguous`, the not-reported message) apply it too. Commands
(`DrillDown`, `Repro.Command`) are left alone. They are shell-quoted already, and rewriting a
character would break the command.

Text output only. JSON is unchanged: `ReportJsonOptions.Default` uses `JavaScriptEncoder.Default`,
which escapes controls and bidi characters, and a consumer needs the recorded value.

Replacement is one-for-one, so a cleaned value is never wider than the original. The width rules
stay as they are.

**Rejected:** cleaning in `EnvelopeBuilder`. JSON would then lose the recorded value, and the
choice depends on the glyph set, which only the renderer knows.

---

## 3. Target output

These blocks illustrate §2. They are not normative (format spec D10). The goldens are generated
in P5, and this section is regenerated from them.

### 3.1 `--id`, a flaky test with three modes

````
```
FINDING                                                       row 1 of 1
────────────────────────────────────────────────────────────────────────

1.  MED   flaky
    CheckoutTests.Completes
    failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes
    evidence moderate | -env-cluster | f_89134598 | ...eckoutTests.cs:42

    test      MyApp.Tests.CheckoutTests.Completes
    assembly  MyApp.Tests
    source    tests/MyApp.Tests/CheckoutTests.cs:42

    failed         7 of 20 executions (35%)
    runs affected  5 of 20
    failure modes  3
    repro          run 11 times: at 5 of 20 runs failing (25%), 11 runs
                   show a failure 95% of the time

FAILURE MODES                                            3 failure modes
────────────────────────────────────────────────────────────────────────

1.  4 failures · first seen 08-12 09:40 at b71e004, 6 runs ago
    System.InvalidOperationException
    expected <num> but was <num>
      at MyApp.Checkout.Basket.Total()
      at MyApp.Tests.CheckoutTests.Completes()

2.  2 failures · since the window opened, 08-05 10:12 at a3f9c2e
    System.TimeoutException
    timed out after <num>ms
      at System.Net.Http.HttpConnectionPool.
         SendAsync(HttpRequestMessage request)
      (no test code on the stack; framework frames shown)

3.  1 failure · first seen in the latest run
    the adapter recorded no failure detail
```
````

### 3.2 `--test`

The RUNS block is unchanged and is followed by:

````
FAILURE MODES                                             1 failure mode
────────────────────────────────────────────────────────────────────────

1.  6 failures · since the window opened, 08-20 13:19 at c670fc3
    Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException
    assert.areequal failed. expected:<<num>>. actual:<<num>>.
      at SampleApp.MSTest.CalculatorTests.FlakyTest_PassesOnRetry()

FINDINGS                                                      2 findings
────────────────────────────────────────────────────────────────────────
````

### 3.3 A long message

A 900-character message prints 6 lines of up to 68 columns. The sixth ends in `...`.

### 3.4 Hostile text

A test whose recorded name, type, message and frame hold `ESC[31m`, a newline, `é` and U+202E
prints, under `--ascii`, `?[31m`, `?`, `?` and `?` at those positions. Under Unicode glyphs it
prints `?[31m`, `?`, `é` and `?`. No line inside the fence exceeds the width rule.

### 3.5 Goldens to add

| Fixture | Covers |
|---|---|
| `detail-failure-modes` | §3.1: three modes, a degraded one, an unavailable one, all three first-seen forms |
| `detail-unavailable` | `AlwaysFailing` whose adapter recorded nothing |
| `history-failure-modes` | §3.2, plus a test with 7 modes (cap line) and a 900-character message (§3.3) |
| `hostile-text` | §3.4, through `--id`, `--test` and the full report |

The existing `detail-flaky`, `detail-always-failing`, `detail-broken-fixture`,
`detail-shared-failure`, `detail-store`, `detail-repro-*` and `history-*` goldens change: D6's pairs
go and the block arrives. Regenerate them all and review the diff. It must consist of those two
changes and nothing else. 59 fixtures, 118 files.

---

## 4. Data contract

### 4.1 `ReportEnvelope`

`CurrentSchemaVersion` → `"1.25"`. The history comment gains: *1.25 adds `failureModes` to each
finding and to `test`, drops the `failure mode N`, `more failure modes` and `AlwaysFailing`
`failure mode` metrics, and counts `Flaky` and `AlwaysFailing` first-seen fields over the failures
their counts cover.*

### 4.2 `FailureModeDto`

```csharp
internal sealed record FailureModeDto(
    string Hash,
    string? ExceptionType,
    string Message,
    IReadOnlyList<string> Frames,
    bool FramesDegraded,
    bool Unavailable,
    int Occurrences,
    DateTime FirstSeenAt,
    string? FirstSeenSha,
    int FirstSeenSessionsAgo,
    bool FirstSeenAfterWindowStart);
```

`FirstSeenInLatestSession` is not repeated: it is `FirstSeenSessionsAgo == 0`.

### 4.3 Changed records

- `FindingDto` gains `IReadOnlyList<FailureModeDto> FailureModes`, after `Metrics`.
- `TestHistoryDto` gains `IReadOnlyList<FailureModeDto> FailureModes`, after `Duration`. It is
  empty for a not-found or ambiguous history.
- `EnvelopeBuilder.BuildFinding` resolves `FailureModes` from the evidence per D2.
  `TestHistoryBuilder.Found` resolves the history's modes per D4. A single
  `FailureModeDto From(SignatureView)` and a `From(SignatureOccurrence, int sessionCount)` live
  beside the DTO, so the two views cannot drift.

### 4.4 Unchanged

The evidence payloads, apart from D3's first-seen values. Exemplars. `MetricDto`. The full report's
rows, which print no block. Cloud DTOs.

---

## 5. Open questions — gates

None. The numbers in D5 (5 modes, 6 message lines) are decided, not open. Changing them is an
amendment.

---

## 6. Implementation sequence

Feature branch **`feat/270-failure-modes`**, phase branches `feat/270-p<N>-<slug>`, one PR per phase
into the feature branch, then one PR into `main` with `Closes #270`. Same steps as
`cli-test-history-spec.md` §6.1. Each phase ends green: `dotnet build -c Release` is clean and
`tests/Xping.Cli.Tests` passes.

### P0 — Inventory. Read-only. No code.

1. Confirm every citation in §1.1 against source. Report any divergence as a spec conflict.
2. Read `ReportEnvelope.CurrentSchemaVersion`. If it is not `"1.24"`, this document needs an
   amendment before P1.
3. List every test that asserts a `failure mode N`, `more failure modes` or `AlwaysFailing`
   `failure mode` metric, or a `firstSeen*` value.
4. List every text-renderer site that appends recorded data, against D7's list. Report any site the
   list misses.
5. Confirm the golden count is 110.

**Stop and report.** Write nothing.

### P1 — Contract and D3

`FailureModeDto`, the two fields, the two `From` mappers, D2's resolution in `BuildFinding`, D4's in
`TestHistoryBuilder`. D3 in `DistinctSignatures`. D6 in `EvidenceHeadline`. Schema → `1.25`.

Text output changes only where D6 removes pairs. Regenerate the goldens and confirm that is the
whole diff. Tests:

- each D2 kind's `failureModes`, in order;
- an empty list for the other kinds;
- D3's regression test (§2 D3);
- `--test` modes equal `SignaturesOf` and share hashes with the finding's evidence;
- a never-failing test has an empty list;
- the version pins at `EnvelopeBuilderTests.cs:202` and `ReportEnrichmentTests.cs:89`.

### P2 — Terminal text, D7

`TerminalText` with unit tests for each character class in both glyph sets, plus the astral-rune
case. Apply it at every site in D7's list and at those P0 found. Tighten
`TheAsciiReportCarriesNothingButPrintableAscii` to reject every control character but `\n` and
`\r`, which the renderer writes itself. Add the `hostile-text` fixture.
At this phase it covers the existing views; P3 and P4 extend it to the block.

### P3 — `--id` block

D5's writer and its `--id` placement. Fixtures `detail-failure-modes` and `detail-unavailable`.
Assert:

- message lines cap at 6;
- the cap line appears at 6 modes;
- each first-seen form;
- no `at` when there is no sha;
- the degraded note;
- the unavailable form;
- the width rule across every new fixture.

### P4 — `--test` block

D5's placement in `WriteHistory`, and the absent block for a test with no failures. Fixture
`history-failure-modes`. A real-store test through `Program.Run`: a test failing two ways shows both
modes, with hashes equal to the `--id` evidence.

### P5 — Goldens and documented samples

Regenerate all goldens with `XPING_UPDATE_GOLDENS` and review the diff against §3.5. Regenerate the
documented samples. The affected markers are in `docs/cli/command-reference.md`: `detail-test`,
`detail-repro-flaky`, `history-flaky`, `history-repro`. Regenerate §3 of this document from the
goldens.

### P6 — Docs

`docs/cli/command-reference.md`:

- the `--id` and `--test` sections describe the block;
- the JSON sample shows `failureModes` and `1.25`;
- a sentence says text output replaces control characters, and non-ASCII under `--ascii`.

`nuspec/README.Cli.md` gets the same if it describes either view.

---

## 7. Out of scope

- **Exemplars in text** (the sample runs of every kind, raw messages, contrast executions). JSON
  carries them. This needs its own issue if wanted.
- **`AlwaysFailing`'s non-modal modes.** Its evidence carries only the modal signature.
- **`TimingOut` modes.** It has none, by design (§1.1).
- **The full report's rows.** They print no block. `--id` and `--test` are where detail goes.

## 8. Reserved for Cloud

Xping Cloud's test data carries no signatures (`Cloud/CloudTest.cs:21-30`). A cloud-enriched
finding shows its local modes. Nothing here is reserved.
