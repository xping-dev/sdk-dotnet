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

### Amendment 1 — from the P0 inventory

P0 checked every citation against source and listed the tests and output sites the phases touch.
These changes are applied in place in the sections named.

| # | Was | Now | Changed |
|---|---|---|---|
| A | D7: `FlowFailures.Printable` is replaced by `TerminalText.Printable`, so one definition serves the whole CLI | Auth is out of D7. `FlowFailures.Printable` drops and trims rather than replacing, `DeviceFlowTests.cs:162-163` and `LoginDeviceTests.cs:119` pin that, and its result reaches auth's `--json` output as `oauthError`. Replacing it would change JSON, which D7 rules out. Auth is not report text and keeps its own tested rule | D7, P2 |
| B | D7's covered values and stderr list | Added, in the text renderer: `Summary.FailedProviders` (`TextReportRenderer.cs:404`), metric **labels** as well as values (`:518`), `Repro.Expectation` (`:519`, `:1411`), the latest run's local `Contrast` (`:1160`), `SameSubject` ids (`:526`). Added, on stderr in `ReportCommand`: `TestResolver.OutsideWindowMessage` (`:200-202`), `CloudHint.Text` (`:472`), the window resolver's `FailureDetail` (`:533`, `:541-542`), the assembly and store path (`:538-539`, `:635`), `LookupWarnings.AheadAssemblies` (`:577-579`). On stdout, `WriteScopeNotice`'s assembly (`:752`). Outside `report`: the rejected `--project` echo (`Program.cs:601`) and `xping where`'s assembly names and store path (`WhereCommand.cs:37`, `:86-90`), with the `Unicode` or `Ascii` charset their output would use | D7, P2 |
| C | D7: `EvidenceLevel` is a vocabulary value the hostile-envelope test keeps | It is printed raw (`TextReportRenderer.cs:1367`, `$"evidence {finding.EvidenceLevel}"`), so it reads through `Recorded` and the test rewrites it. Only kind, severity, population and status keep their values | D7 |
| D | D7: the hostile-envelope test rewrites string properties and list elements | It also rewrites dictionary **keys** and values of string type. `Summary.NotMeasured` is keyed by kind and printed through `LabelFor` (`:335`), which echoes an unknown key as-is. That site reads the label through `Recorded`, so `LabelFor`'s fallback can't pass raw text through | D7 |
| E | D4: the population is every failed attempt | Stated: a failed attempt includes a timed-out one (`TestOutcomeExtensions.IsFailure`). `--test` lists modes from timed-out attempts, which `--id` never does, because `TimingOut` carries no signature (`FailureModeProvider.cs:743-747`). The `--test` block describes how the test failed, and a timeout is one way | D4 |
| F | §3.5 / P1: the D6 golden diff is "the pairs removed" | In every Flaky detail golden, `failure mode 1` (14 characters) was the longest label. Without it, the block's column narrows by one, and every pair in it moves one column left. That is part of the expected diff. AlwaysFailing does not shift, because `runs affected` (13) is longer than `failure mode` (12). No `history-*` golden carries a D6 pair | §3.5, P1 |
| G | P1's test list | Adds: `FailureModeProviderTests.cs:423`, `:456` and `:475` (they assert the D6 pairs) are rewritten to assert `failureModes`. The positional `FindingDto` in `ReportFixtures.cs:1565-1581` and the positional `TestHistoryDto`s in `ReportFixtures.cs:1017-1026` and `HistoryRenderingTests.cs:363-372` gain the new field. The JSON sample at `docs/cli/command-reference.md:957-959` and `:889` move in P6 | P1, P6 |
| H | §1.1: the normalised message is one line, lowercased, at most 4096 characters, `Normalise` at `:107-177` | One line. Lowercased outside quoted literals, which keep their case (`MessageNormaliser.cs:131-144`). Cut to 4096 **before** token substitution (`:119-120`), so the result can exceed 4096 slightly. `Normalise` is `:107-150`, `Substitute` `:161-178`. D5's 6-line cap is unaffected | §1.1 |
| I | §1.1: `ReportCommand.cs:318-323` | The context is built at `:329` and passed to the history callback at `:376` | §1.1 |
| J | — | `StackFrameExtractor.cs:14` documents frames as "outermost first". They are kept in trace order, which puts the throw site first. P1 corrects the comment to "innermost first, as the trace lists them" | §1.1, P1 |

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
  (`MessageNormaliser.cs:107-150`): one line, whitespace collapsed (`:123`), run-varying values
  replaced by tokens such as `<num>`, lowercased outside quoted literals (`:131-144`), cut to `MaxLength` =
  4096 characters before substitution (`:46`, `:119-120`). The raw message in `FailureExemplar.ErrorMessage` is a different value: multi-line,
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
  over the D5 window (`src/Xping.Cli/Commands/ReportCommand.cs:178`, `:329`, `:376`).
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
- `TerminalText` and `TerminalCharset` in `src/Xping.Cli/Reporting/`, and the renderer helpers `Recorded` and `Command` (D7).
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

### D3 — First seen: the date from the counted failures, the flags from all of them

For `Flaky` and `AlwaysFailing`, `DistinctSignatures` derives `FirstSeenAt`, `FirstSeenSha` and
`FirstSeenSessionsAgo` from the failures it counted: the oldest surviving failure per hash, i.e.
`Max(SessionIndex)`, its session's `StartedAt`, and `RevisionContext.ReadSha` of that session, as
`BuildPerTest` computes them for the whole set. The count and the date on D5's count line then
describe the same failures, and the date never points at a run the count excludes.

The two flags stay on the index's undiscounted `SignatureOccurrence.OldestSessionIndex`, as
`ToView` computes them today:

- `FirstSeenInLatestSession`: the mode appeared nowhere in the window before the newest run.
- `FirstSeenAfterWindowStart`: the mode was absent from the window's oldest run.

They answer *is this mode new?*, and discounting must not change that answer. A mode whose older
failures all fell in environmental runs is not new because those runs were set aside. Deriving the
flags from the counted failures would make a discounted oldest run turn every mode into "first seen
… N runs ago", and a mode with only one counted failure into "first seen in the latest run": a
false regression signal. D5 prints a date only when the flags say the mode is new in the window.

The index's `SignatureOccurrence` also remains the source of the signature itself and of the
tie-break order. Clusters are unchanged: their count and first seen already come from one
population.

This changes `evidence` for those two kinds wherever an environmental or clustered failure was a
mode's oldest. It is a fix, with two regression tests:

- A flaky test whose oldest failure of a mode is in an environmental session, after the window's
  oldest run, reports the date and sha of its oldest counted failure.
- A flaky test whose mode appears in the window's oldest run, which is environmental, still has
  `FirstSeenAfterWindowStart = false`.

### D4 — `--test` builds its block from the test's own failures

`TestHistoryBuilder` maps `context.Signatures.SignaturesOf(fingerprint)` into `FailureModes`:

- **Population:** every failed attempt of this test over the D5 window, nothing discounted. That is
  the population the RUNS timeline draws: a failed first attempt that passed on retry is in it, as
  it is in the timeline's `✗→✓`. A timed-out attempt is a failed one (`IsFailure`), so `--test`
  lists modes `--id` never does: `TimingOut` carries none (Amendment 1E).
- **Order:** the index's order: count, then recency, then hash.
- **Count, first seen and flags** come from `SignatureOccurrence` (`Occurrences`,
  `OldestSessionIndex`, `FirstSeenAt`, `FirstSeenSha`). The flags are computed as `ToView` computes
  them.
- **Not from findings.** A test that failed without reaching any finding's threshold still shows
  how it failed.
- **The unit is the attempt.** The summary line above counts runs by their deciding attempt
  (`TestHistoryBuilder.cs:153-179`). A test that always passed on retry reads `0 failed` there and
  has failed attempts here. The count line therefore names its unit, `failed attempts` (D5), in both
  views. When the test is also flaky, the count here and the count in its `--id` view may differ,
  because `--id` discounts. Each block counts the attempts of its own view's population.
- **Absent block.** When the test has no failed attempt in the window, `FailureModes` is empty and
  the block is not printed.

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
1.  4 failed attempts · first seen 08-12 09:40 at b71e004, 6 runs ago
    System.InvalidOperationException
    expected <num> but was <num>
      at MyApp.Checkout.Basket.Total()
      at MyApp.Tests.CheckoutTests.Completes()
```

1. **Count line.** `1 failed attempt` or `N failed attempts`, the glyph separator, then:
   - `FirstSeenInLatestSession`: `first seen in the latest run`.
   - Otherwise, `FirstSeenAfterWindowStart`: `first seen <MM-dd HH:mm> at <sha>, <when>`, where
     `<when>` is `<n> runs ago` (`1 run ago`), or `in the latest run` when `FirstSeenSessionsAgo`
     is 0. That case happens only when D3's discounting set aside every older failure.
   - Otherwise: `since the window opened`, with no date. The mode was already there in the window's
     oldest run, the header names that run's date, and the report cannot see further back. This is
     the `FirstSeenAfterWindowStart` contract (`FailureModeProvider.cs:33-36`).
   - The sha is cut to `ShaWidth` (7), as the RUNS rows cut it (`TextReportRenderer.cs:711`). No
     sha recorded: drop ` at <sha>`.

   The line wraps with `Wrap` to `ContinuationBudget` (68), continuing at indent 4.
2. **Type line**, indent 4: `ExceptionType`, or `exception type not recorded` when null. A type
   with no break in reach overflows as one token (detail spec Amendment 2D).
3. **Message**, indent 4, wrapped to `ContinuationBudget` (68) with `Wrap`. At most **6** lines.
   When more would follow, the sixth line is the rest of the message cut hard to 65 columns, a word
   or a token split if need be, followed by `...`. The marker always fits in the 68 columns,
   including when the sixth line would be one long token. Omitted when the message is empty.
4. **Frames**, innermost first as recorded, every frame (at most 5). Each is `at <frame>` at indent
   6, a budget of 66 columns. A frame that does not fit breaks once, after the last `.` before its
   first `(` that fits, and continues at indent 9 (budget 63), so the method name and its
   arguments stay together. A remainder still wider than 63 wraps further at spaces, at indent 9. No
   such `.` in reach: the frame overflows as one token. When `Degraded` and there are frames, one
   more dim line at indent 6: `(no test code on the stack; framework frames shown)`. No frames: no
   frame lines and no note.
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

**One function.** `TerminalText.Printable(string text, TerminalCharset charset)` in
`src/Xping.Cli/Reporting/` (beside `ReportGlyphs`) returns the text with every unsafe rune replaced
by `?`. It works per `Rune`, so an astral character becomes one `?`. `TerminalCharset` is
`Unicode`, `Ascii` or `Command` (below). The text renderer derives the charset from its glyphs.

- **Always unsafe:**
  - C0 controls U+0000–U+001F (including `ESC`, `CR`, `LF`, tab), `DEL` U+007F, C1 controls
    U+0080–U+009F;
  - the line and paragraph separators U+2028 and U+2029;
  - bidi marks U+200E, U+200F and U+061C, bidi embeddings and overrides U+202A–U+202E, bidi
    isolates U+2066–U+2069;
  - zero-width and invisible format characters: U+200B–U+200D, U+2060–U+2064, U+FEFF, and U+00AD.
- **`Ascii`:** additionally every rune ≥ U+0080.
- **`Command`:** the always-unsafe set only, in every glyph set. A command is printed to be run,
  and replacing an `é` in an FQN would make it select nothing. A control or bidi character is never
  part of a command that works, so replacing it costs nothing.

Auth's `FlowFailures.Printable` (`Auth/Flows/FlowFailures.cs:63-66`) is left alone (Amendment 1A).

**One choke point in the renderer.** No text renderer appends a DTO string directly.
`TextReportRenderer`, `SummaryReportRenderer` and `CloudText` read every recorded value through one
helper, `Recorded(string?)`, or through `Command(string?)` for a command. Each applies `Printable`
with the right charset. Renderer-authored text (labels, vocabulary, glyphs, numbers it formats)
does not go through them. A new field is safe only if it is read through one of the two, so the
rule is enforced by a test, not by the list below:

- **The hostile-envelope test** (P2) takes every fixture envelope. It rewrites, by reflection over
  the DTO graph, every `string` property, every `string` element of a list, and every `string` key and value
  of a dictionary to append
  `\u001b[31m\u202e\u2028é`. Values the renderer maps through `ReportVocabulary` (kind,
  severity, population, status) keep their own, because a fixture with an
  unknown kind tests nothing about recorded text. It renders the result in both glyph sets with
  colour off, and asserts:
  - no output character is in the always-unsafe set, apart from the renderer's own `\n`/`\r`;
  - under `Ascii`, no character ≥ U+0080 outside the command strings the envelope carries.

  A renderer site that bypasses the helpers fails it.

The values this covers today, for P0 to check against:

- the header: assembly, branch, sha;
- subject names, cause labels, member names;
- locations and the trailer's path;
- metric labels and values, including retry mechanisms and declared reasons;
- headlines, annotations, `repro` sentences, the evidence level;
- the finding id, the selection id, same-subject ids;
- `Summary.FailedProviders` and the not-measured kinds;
- latest-run failure summaries, contrasts and shas;
- the `--test` query, FQN, short fingerprint, `FindingsWithheld`, and every RUNS row's sha
  (`TextReportRenderer.cs:711`);
- every `FailureModeDto` field;
- `CloudText` values;
- `SummaryReportRenderer`'s assembly.

Commands go through `Command`: `DrillDown`, `Repro.Command`, `Selection.History`,
`Truncated.Command`, and the footer and below-fence lines built from them.

**ASCII end to end, except inside commands.** Under `--ascii` or piped output, every character is
ASCII except a non-ASCII character a command carries from a recorded name or path.
`APipedReportCarriesNothingButTheReport` and `TheAsciiReportCarriesNothingButPrintableAscii` keep
their assertion over every existing fixture, none of which has a non-ASCII command. The second
test also rejects every control character but the renderer's `\n` and `\r`. The hostile-envelope
test is where the command exception is checked.

**Messages outside the renderer.** These apply `Printable` with the charset the report would use,
and any command in them applies `Command`:

- `ReportCommand`'s messages that echo the query, candidates, assemblies, the store path, a
  `--since` value or a sha: `TestResolver.NotFoundMessage`, `OutsideWindowMessage`,
  `WriteAmbiguous`, the not-reported message, `CloudHint.Text`, the window resolver's
  `FailureDetail`, `LookupWarnings.AheadAssemblies`, `WriteScopeNotice`;
- `Program.cs:601`'s echo of a rejected `--project`;
- `xping where`'s assembly names and store path.

**Text output only.** JSON is unchanged: `ReportJsonOptions.Default` uses `JavaScriptEncoder.Default`,
which escapes controls and non-ASCII, and a consumer needs the recorded value.

Replacement is one-for-one per rune, so a cleaned value is never wider than the original. The
width rules stay as they are.

**Rejected:**

- **Cleaning in `EnvelopeBuilder`.** JSON would then lose the recorded value, and the choice depends
  on the glyph set, which only the renderer knows.
- **A list of call sites without the test.** The first review of this spec found a site the list
  missed.

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

1.  4 failed attempts · first seen 08-12 09:40 at b71e004, 6 runs ago
    System.InvalidOperationException
    expected <num> but was <num>
      at MyApp.Checkout.Basket.Total()
      at MyApp.Tests.CheckoutTests.Completes()

2.  2 failed attempts · since the window opened
    System.TimeoutException
    timed out after <num>ms
      at System.Net.Http.HttpConnectionPool.
         SendAsync(HttpRequestMessage request)
      (no test code on the stack; framework frames shown)

3.  1 failed attempt · first seen in the latest run
    the adapter recorded no failure detail
```
````

### 3.2 `--test`

§3.1 of the test-history spec, after the RUNS block. The summary above it reads
`0 passed, 6 passed on retry, 0 failed`: the six failed attempts are the first attempts of runs
that passed on retry (D4).

````
FAILURE MODES                                             1 failure mode
────────────────────────────────────────────────────────────────────────

1.  6 failed attempts · since the window opened
    Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException
    assert.areequal failed. expected:<<num>>. actual:<<num>>.
      at SampleApp.MSTest.CalculatorTests.FlakyTest_PassesOnRetry()

FINDINGS                                                      2 findings
────────────────────────────────────────────────────────────────────────
````

### 3.3 A long message

A 900-character message prints 6 lines of up to 68 columns. The sixth ends in `...`.

### 3.4 Hostile text

The normaliser collapses whitespace in a signature's message, and the frame extractor splits the
stack on line breaks. So a newline in those two fields reaches the output as a space or as two
frames, never as `?`. The other characters survive both, and so does a newline in a test name,
exception type, member name or path.

A test whose recorded FQN holds `ESC[31m`, a newline, `é` and U+202E prints, under `--ascii`,
`?[31m`, `?`, `?` and `?` at those positions in its name, its `test` pair and every failure-mode
field that carries them. Under Unicode glyphs it prints `?[31m`, `?`, `é` and `?`. In the
`Repro:` and `History:` commands, under both glyph sets, it prints `?[31m`, `?`, `é` and `?`. No
line inside the fence exceeds the width rule.

### 3.5 Goldens to add

| Fixture | Covers |
|---|---|
| `detail-failure-modes` | §3.1: three modes, a degraded one, an unavailable one, all three first-seen forms |
| `detail-unavailable` | `AlwaysFailing` whose adapter recorded nothing |
| `history-failure-modes` | §3.2, plus a test with 7 modes (cap line) and a 900-character message (§3.3) |
| `hostile-text` | §3.4, through `--id`, `--test` and the full report. The golden pins one store; the hostile-envelope test (D7) covers every field |

The existing `detail-flaky`, `detail-always-failing`, `detail-broken-fixture`,
`detail-shared-failure`, `detail-store`, `detail-repro-*` and `history-*` goldens change: D6's pairs
go and the block arrives. Regenerate them all and review the diff. It must consist of those two
changes and nothing else. 59 fixtures, 118 files.

---

## 4. Data contract

### 4.1 `ReportEnvelope`

`CurrentSchemaVersion` → `"1.25"`. The history comment gains: *1.25 adds `failureModes` to each
finding and to `test`, drops the `failure mode N`, `more failure modes` and `AlwaysFailing`
`failure mode` metrics, and takes `Flaky` and `AlwaysFailing` first-seen dates from the failures
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
    bool FirstSeenInLatestSession,
    bool FirstSeenAfterWindowStart);
```

`FirstSeenInLatestSession` is carried rather than read off `FirstSeenSessionsAgo == 0`: under D3
the two can differ.

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
   list misses. List every site that appends a command.
5. Confirm the golden count is 110.

**Stop and report.** Write nothing.

### P1 — Contract and D3

`FailureModeDto`, the two fields, the two `From` mappers, D2's resolution in `BuildFinding`, D4's in
`TestHistoryBuilder`. D3 in `DistinctSignatures`. D6 in `EvidenceHeadline`. Schema → `1.25`.

Text output changes only where D6 removes pairs. Regenerate the goldens and confirm that is the
whole diff. Tests:

- each D2 kind's `failureModes`, in order;
- an empty list for the other kinds;
- D3's two regression tests;
- `--test` modes equal `SignaturesOf` and share hashes with the finding's evidence;
- a never-failing test has an empty list;
- the six `"1.24"` pins: `EnvelopeBuilderTests.cs:202`, `ReportEnvelopeTests.cs:329` and `:916`,
  `CliSurfaceTests.cs:393` and `:437`, `ReportEnrichmentTests.cs:89`.

### P2 — Terminal text, D7

`TerminalText` and `TerminalCharset`, with unit tests for each character class under each charset
and for an astral rune. Then:

- `Recorded` and `Command` in the text renderers, and every recorded or command read routed through
  them;
- the stderr messages;
- the hostile-envelope test;
- `TheAsciiReportCarriesNothingButPrintableAscii` tightened;
- the `hostile-text` fixture.

At this phase the fixture covers the existing views. P3 and P4 add the block to it, and the
hostile-envelope test covers `FailureModeDto` as soon as P1's field exists.

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
