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

### Amendment 1 — from the P0 inventory

P0 read this document against source and found four conflicts, all in how §3 and D7/D10 describe
the rendering. Resolved here and committed before P1.

| # | Was | Now | Changed |
|---|---|---|---|
| A | §3: the assembly and fingerprint sit in the `TEST` heading annotation, a `window` line follows the name, and nothing is printed outside the fence | The view keeps `report`'s frame (`TextReportRenderer.Render`, `:128-156`): the two header lines (`WriteHeader`, `:167`), the ```` ``` ```` fence, the legend and the footer. The header already names the assembly, run count, period and revision, so the `window` line is dropped. The heading annotation is `fp <fp>`. The identity is the detail view's labelled pairs, `test` and `source`. The footer is always printed and reads `N findings about this test · all: <ForFullReport>`, with `no findings` for zero | §3.1, §3.2, D10 |
| B | D10: a finding renders "as `report` renders a row, with the subject name omitted" | No such row exists. Each finding renders as `WriteFinding` does (`:~940`), keeping its **full-report row number** (`1.  HIGH  flaky`) so the number matches `report` and `--id`, and keeping the evidence line (marker, id, location). Only the name line and the `same test as #N` annotation are dropped: the view is the subject, and every sibling is on screen | D10, §3.1 |
| C | D7: outcome glyphs "colored as the report colors pass and fail" | `OutputCapabilities` has no pass/fail colours, only severity (`Colorize`, `:108`) and `Dim` (`:88`). Glyphs are not coloured; only the `partial` tag is dimmed. New colours are a UI change outside this spec | D7 |
| D | D10: "whose `FindingOrder.Test` is this fingerprint", with clusters listed too | `FindingOrder.Test` returns null for a cluster (`FindingOrder.cs:124-131`). A single-test finding matches through `FindingOrder.Test`; a cluster matches when `FindingSubject.Group.Members` contains the fingerprint. Behaviour unchanged | D10 |

Under `Store`, every command `DrillDown.Scoped` builds carries `--runs <window session count>`, the
footer's `--all` command included. `DrillDown.ForTest` is the one exception (D11).

### Amendment 2 — from the P1 review

| # | Was | Now | Changed |
|---|---|---|---|
| A | D10: each finding's `drillDown` "must reproduce the window it was found in" | Under `Store`, the drill-down keeps the window's **size**, not its sessions. `--runs N` takes the newest N sessions when the command runs. When a run is stored in between, the window includes it and drops the oldest, and a finding can change or disappear. This is expected, and it is how `--runs` already behaves in every drill-down `report` prints | D10 |

**Rejected:** pinning the exact sessions. `report` has no flag that names a set of sessions.
`--since <oldest session's timestamp>` would keep every original run, but the window would grow with
each new one, so the header's run count would stop matching the view it came from.

### Amendment 3 — from P2

| # | Was | Now | Changed |
|---|---|---|---|
| A | D2 tier 3 is a pure prefix match | A query equal to a whole fingerprint matches that fingerprint alone. Otherwise a fingerprint that is a prefix of another (a pinned `login` that later became parameterised, `login:3f2a…`) matches both, and D3's short fingerprint of `login`, which is the whole fingerprint, could never name it | D2, D3 |
| B | D2 tier 2 matches when the FQN ends with `.` + `<name>` | It also matches when the whole FQN equals `<name>`, after the same `+` and `(args)` normalisation. Otherwise the full name of a nested type typed with dots (`Ns.Outer.Inner.Method` for `Ns.Outer+Inner.Method`), or of a parameterised test without its arguments (`Ns.Calc.Add` for `Ns.Calc.Add(1,2)`), matches nothing, though D2 says tier 2 covers `Namespace.Class.Method` | D2 |

### Amendment 4 — from P3

| # | Was | Now | Changed |
|---|---|---|---|
| A | §4.2 has no field for a finding's full-report row, which D10 and §3.1 print (`1.`, `2.`). Under `--test`, `findings` is filtered, so a finding's index there is not its row | `TestHistoryDto` gains `FindingRows`, one `SameSubjectDto` (id, kind, row) per entry of `findings`, in the same order, as `SelectionDto.SameSubject` carries rows. `FindingDto` is unchanged, so `report` and `--id` do not move | D10, §4.2 |

### Amendment 5 — from P4

| # | Was | Now | Changed |
|---|---|---|---|
| A | D3 and D4 write an envelope under `--format json` when the name resolves to no test, but no test chose its assembly or window | That envelope is the report over `report`'s own scope, `--assembly` or else the newest assembly, and the `--test` window: `Store`, unless `--runs` or `--since` replaces it. When the name matched a test but the narrowed window holds none of its runs (D4 b), the envelope is over that test's assembly and that window. In text, only stderr is written and nothing is analysed. The answer is about the name, so a window that cannot be opened for the envelope never replaces it: stderr and the exit code stand, and stdout is empty. A `--since` later than every run of the matched test's assembly opens no window at all, and is D4 (b), not an empty store | D3, D4 |
| B | `ReportScope.AssemblyGiven` is whether the reader typed `--assembly` | Under `--test` it is always true. The assembly came from the name, and a bare `xping report --all` scopes itself to whichever assembly ran last, so every command the view prints names it, under `--runs` and `--since` too | D10, D11 |

### Amendment 6 — from P5

| # | Was | Now | Changed |
|---|---|---|---|
| A | The `TEST` heading prints `fp <fp>`, but §4.2 carries only the full fingerprint, and the short one depends on every other fingerprint in the assembly | `TestHistoryDto` gains `ShortFingerprint` (`string?`, null when the name resolved to no test), the `<fp>` D3 defines | §3.1, §4.2 |
| B | D11: `--id`'s `History:` line follows "Also about this test", inside the fence | It is printed dimmed **below the fence**, after the footer, as `Detail of row 1:` is. Inside the fence a command with a long assembly or `--directory` would wrap across lines or overflow the 72 columns, and every other command the report prints sits below the fence so it is never cut. `SelectionDto` gains `History` (`string?`): `DrillDown.ForTest` for a reported single-test finding, and for D11's third not-found tier; null otherwise. Its `<fp>` is the short fingerprint over the assembly's whole-store read, so it is the one `--test` accepts | D11, §4.2 |
| C | §3.1: the line below the footer reads `Detail of row 1:` | `Detail of row N:`, where N is the first finding's full-report row. Under `--test` the first finding shown is often not row 1, and D10 keeps the full report's numbers | D10, §3.1 |
| D | D10 drops each finding's name line | Only a single-test finding's. A cluster keeps its name line, which is its cause and member count, and its member list: the cluster is not the view's subject | D10 |
| E | D11: the third not-found tier "adds" its clause to the message | It is a second stderr line, after the whole message, ending on the command. Appended mid-message, the next clause (`1 other assembly in this store…`) ran on after the command, and copying the command copied it as arguments | D11 |
| F | D11: the third tier hits whenever no finding's subject is the test | A hit on a test that a reported cluster contains is not "no findings": the cluster is about it (D10). The message names the cluster as where the subject is reported, as the second tier does, and prints no history line | D11 |

The schema stays `1.23`, which is unreleased; its remarks entry names both new fields.

### Amendment 7 — from #281

| # | Was | Now | Changed |
|---|---|---|---|
| A | `LocalStoreOptions` keeps 50 runs, 50 MB, 30 days, the run limit counted across the whole store | 50 runs **per assembly**, 100 MB and 30 days store-wide. A run covering several assemblies counts towards each and is pruned only once none of them needs it. The size limit removes runs outside every assembly's newest 20 first. A whole-store read for one assembly is therefore at most ~50 sessions | §1.1 |
| B | Lookup across every assembly reads a store capped at ~50 sessions | It reads up to `UnboundedReadCeiling` (1000) sessions. That covers twenty assemblies at full retention, and the 100 MB limit binds well before that on any suite large enough to matter. The read stays one pass, so D2's rejection of auto-scoping stands | D2 |
| C | OQ-2: revisit the timeline's row cap if retention changes | Revisited: still no cap. A run limit counted per assembly keeps one test's timeline at ~50 rows, as before | §3.2, OQ-2 |

The report's default window (20 runs / 14 days) is unchanged. It now comes from `ReportWindow` in Core, which retention is tested against, so the store always holds at least one full default window per assembly.

### Amendment 8 — from #283

| # | Was | Now | Changed |
|---|---|---|---|
| A | Lookup across every assembly reads the store once, unscoped, up to `UnboundedReadCeiling` (Amendment 7B) | One pass over every file, newest first, holding one session at a time and keeping only identities, so nothing is truncated and no run is read twice. The clock-skew drop applies per assembly, as it already does under `--assembly`, so `--test Foo` and `--test Foo --assembly X` agree: an assembly's runs ahead of the clock are left out while it has a datable run, and kept when it has none. Such an assembly is named in its own warning (`every run of <assembly> is stamped ahead of this machine's clock`). "Every recorded run is ahead" is said only when no run in the store, attributed or not, can be dated. Candidates are ordered by their newest run (start time descending, then session id ascending), then assembly, FQN and fingerprint. "N runs" in D4(a) counts distinct runs, so a run covering two assemblies counts once. Each unreadable file is counted once, including when it is the only thing the store holds | D2, D4 |
| B | The `--test` window is read up to `UnboundedReadCeiling` | The ceiling is gone. Every unbounded read is scoped to one assembly, which the writer's retention bounds | §1.1, D5 |
| C | Amendment 7B | Replaced by A | D2 |

`report`'s auto-scoping, which this spec does not use, now picks the newest assembly from each file's header without loading any session.

---

## 1. Ground truth

### 1.1 What exists

Every claim here was read in source at the cited line.

- **Three window modes, unbounded reads scoped.** `WindowResolver.Resolve` (`Report/Windowing/WindowResolver.cs:150-163`)
  picks `--since`, then `--runs`, then the default (20 sessions, kept to 14 days). Every read goes
  through `Read(source, maxSessions, assembly)` (`:383`). The unbounded modes read every session
  of one assembly; retention, not the CLI, bounds them (Amendment 8B). `WindowResolution` has `Default`, `Runs`, `SinceSha`,
  `SinceDate` (`AnalysisWindow.cs:13-26`).
- **Retention bounds the store, not the CLI.** `LocalStoreOptions` keeps 50 runs per assembly, and
  100 MB and 30 days store-wide (`src/Xping.Sdk.Core/Services/LocalStore/LocalStoreOptions.cs`;
  Amendment 7A). A whole-store read for one assembly is therefore at most ~50 sessions in the default
  configuration.
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
2. **Segment suffix.** Ordinal, case-sensitive. Before comparing, `+` is read as `.` on both sides:
   NUnit and MSTest write a nested type as `Outer+Inner`, so `--test Inner.Method` matches it. The test
   matches when its FQN ends with `.` + `<name>`, or equals `<name>` (Amendment 3B), either as stored
   or with a trailing `(args)` removed.
   So `Add` matches every case of `Add(…)`, and `Add(1,2)` matches that one case. This covers `Method`,
   `Class.Method` and `Namespace.Class.Method` without reading `ClassName`. Generic arity markers
   (`` `1 ``) are not normalised; the bare method name or the full FQN still matches.
3. **Fingerprint prefix.** `<name>` is a prefix of the fingerprint, ordinal and case-sensitive, at
   least 8 characters long or the whole fingerprint when it is shorter. A query equal to a whole
   fingerprint matches that fingerprint alone (Amendment 3A). When nothing matches and
   `<name>` is all hex, it is retried lower-cased, since a computed fingerprint is lower-case SHA-256 hex.
   A fingerprint pinned with `[XpingFingerprint]` is any `[a-zA-Z0-9_-]{1,100}`, plus `:<paramHash>`
   for each parameterised case (`TestIdentityGenerator.cs:61-63`,
   `XpingFingerprintAttribute.cs:69-71`). That is why this tier compares text rather than hex.

Matching uses each fingerprint's **newest** identity, so a renamed test is found under its current name.

**Where it looks.** With `--assembly`, only that assembly. Without it, every assembly in the store:
`ResolveTest` reads every file once, one session at a time, applies the clock-skew drop per assembly,
and groups executions by `(Assembly, TestFingerprint)` (Amendment 8A). A single match scopes the
rest of the command to that test's assembly, and its window is then resolved as D5 describes. Nothing
is ever joined across assemblies.

**Rejected:** case-insensitive matching, globs and fuzzy matching. Each makes "matched one test" depend
on what else happens to be in the store, and the ambiguity path (D3) already handles typing less.
**Rejected:** using `report`'s auto-scoping (newest assembly only). A name is a stronger scope than
"whatever ran last", and lookup across every assembly costs one pass over the store, holding one session at a time (Amendment 8A).

### D3 — Ambiguity

When the winning tier matches two or more fingerprints, nothing is rendered. Instead:

- stderr lists the candidates, newest run first, up to 10, then `+N more` (§3.3);
- each candidate gives its fingerprint prefix, its assembly and its FQN; the FQN is broken with the
  existing `Break` rules (`TextReportRenderer.cs:556-580`) and fits the fence;
- one ready-to-run line closes the list: `xping report --test <fp> --assembly <assembly>` for the
  first candidate;
- `--format json` writes the envelope to stdout with `test.query` set, `test.candidates` populated,
  `test.test` null and `test.sessions` empty;
- the command exits **`4`, `ExitCodes.TestAmbiguous`**.

Parameterised tests are the common case. `--test Add` matches `Add(1,2)` and `Add(2,3)`, by design. The
fingerprint prefix is how a reader names one case.

**Short fingerprint.** Every command this spec prints names a test by its short fingerprint, written
`<fp>`. It is the shortest prefix of at least 8 characters that matches exactly one fingerprint in the
assembly; the whole fingerprint when it is shorter than 8, or when no shorter prefix is unique, which
still names it alone (Amendment 3A). A computed fingerprint almost always stops at 8. The parameterised cases of a pinned test share the pin, so their short fingerprints run past the `:`
into the parameter hash. That is what lets `--test <fp>` name one case. Candidates in D3 print it, and
`DrillDown.ForTest` (D11) emits it.

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

With `--format json`, stdout carries the envelope, as `--id`'s not-reported path does
(`ReportCommand.cs:97-107`). `selection` is null. `test` is present with `query` set, `test.test` and
`counts` and `duration` null, and `sessions` and `candidates` empty, so a script can see what was asked
for. Not found and ambiguous differ by `candidates` and by the exit code.

### D5 — Window

A new **`WindowResolution.Store`**, the default under `--test`: every session the store holds for the
assembly. `--runs N` and `--since` behave exactly as in `report` and
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
| detail | 12 | `attempt N` when the deciding attempt is > 1. Otherwise the outcome word for a deciding outcome other than Passed or Failed: `timeout`, `skipped`, `not run`, `inconcl.` (Inconclusive, abbreviated so every word fits). Both: `<word> @N`, e.g. `timeout @2`. Else blank. The longest value at attempt 99 is `inconcl. @99`, 12 columns. A higher attempt widens the row, like a long duration |
| gap | 2 | |
| duration | 8, right-aligned | Deciding attempt, formatted by `DurationFormatter.Format` (`src/Xping.Sdk.Shared/DurationFormatter.cs`) as `EvidenceHeadline` formats every duration (`EvidenceHeadline.cs:780`): `12ms`, `1.2s`, and so on. A longer value widens the row; it still fits (below) |
| gap | 2 | |
| sha | 7 | First 7 characters, or blank |
| gap | 2 | |
| tag | 7 | `partial` when the session view is partial, else nothing |

That is 61 columns, which leaves 11 for a duration overflow inside the 72-column fence. Trailing blanks
are trimmed. Glyphs are not coloured (Amendment 1C). The tag is dimmed.

The year is omitted. Retention caps the store at 30 days, and the heading's period line (§3.1) carries
the full dates.

**Rejected:** a column per attempt. Retry counts are unbounded, and the deciding attempt plus "where it
started" is what a reader acts on. The JSON carries every attempt.

### D8 — Absences

Sessions in the window that do not contain the test are never rows. Each maximal run of consecutive
absent sessions collapses into one line, in its place in the timeline:

```
               ─     absent from 14 runs, 14 partial
```

The `─` (ASCII `-`) sits in the outcome column. The count clause `, N partial` appears only when N > 0.
When N equals the run count, the reader has their answer to #140: the test wasn't deleted, the runs were
filtered.

The JSON lists every session in the window in one `sessions` array, each with `appeared` (§4).

**Rejected:** a row per absent session. On a 50-run window, most of the screen is blank rows.

### D9 — Summary lines

Two lines under the identity, before the timeline:

```
ran in 6 of 20 runs: 0 passed, 6 passed on retry, 0 failed
duration p50 12ms · p95 13ms · range 11-13ms
```

- Counts come from each run's deciding attempt (`TestIndex.RunsOf`). `passed on retry` is a deciding
  Passed with attempt > 1. `failed` covers Failed and Timeout. A run whose deciding attempt is
  Skipped, NotExecuted or Inconclusive still counts in `ran in` and is never an absence, because the
  framework saw the test (OQ-1). Such runs add a `, N other` clause only when N > 0.
- Duration is over the deciding attempt of each run with a verdict, using `Quantile.Interpolated` on
  raw milliseconds, printed with `DurationFormatter.Format` as D7's column is. The range is min to max. The duration line is omitted when there are no such runs.
- `RobustDispersion.Of` goes into the JSON as `dispersion` only. It is unitless and reads as noise in
  text.

**Rejected:** reusing `DurationProvider`'s normalised profile. It answers "slow relative to its
session", which is a finding's question, not a history's.

### D10 — Findings block

Under a `FINDINGS` heading, every finding the coordinator produced over the D5 window that is about
this test, in report order: a single-test finding whose `FindingOrder.Test` is this fingerprint
(`FindingOrder.cs:124`), or a cluster whose members include it (Amendment 1D). Each is rendered as
`WriteFinding` renders it, keeping its full-report row number and evidence line. Only the name line
and the `same test as #N` annotation are dropped (Amendment 1B).

Each finding's `drillDown` must reproduce the window it was found in: under `Store`, its size, not
its sessions (Amendment 2A). `ReportScope` gains the window resolution, and when it is `Store`,
`DrillDown.ForFinding` appends `--runs <window session count>`.
Clusters (`SubjectGroup`) that contain the test are listed too, after single-test findings, with their
member count. Their id resolves under `--id` like any other.

The block's heading annotation reads `N findings`. When there are none, the block reads `no findings`,
or D6's reason when the floor explains it.

**Rejected:** repeating `--id`'s full detail blocks. The view would be several screens long, and each
row's id is one command away.

### D11 — Links between the two views

- `--id`'s detail view gains one line after "Also about this test", for single-test subjects:
  `History: xping report --test <fp> --assembly <assembly>`, built by a new `DrillDown.ForTest`.
  It repeats `--assembly` and `--directory`, not `--runs` or `--since`: the history view's default
  window is already what a reader following this link wants.
- `--id`'s not-found message gains the third tier that the detail spec's D3 deferred. When no finding
  matches and no same-subject finding is reported, recompute `FindingId.Compute(kind, fingerprint)`
  for every fingerprint in the window and every kind. On a hit the message adds:
  `Its test has no findings in this window: xping report --test <fp> --assembly <assembly>`.
  `cli-finding-detail-spec.md` Amendment 6 records this.

---

## 3. Target output

Illustrative. Where a block and §2 disagree, §2 wins.

### 3.1 A flaky test with a filtered gap

````
$ xping report --test FlakyTest_PassesOnRetry
Xping · SampleApp.MSTest · 20 runs · 2026-08-20 → 2026-08-21 · main@c670fc3
16 tests · 10 healthy · 6 flagged

```
TEST                                                         fp 3a597eb2
────────────────────────────────────────────────────────────────────────

    test    SampleApp.MSTest.CalculatorTests.FlakyTest_PassesOnRetry
    source  tests/SampleApp.MSTest/CalculatorTests.cs:42

ran in 6 of 20 runs: 0 passed, 6 passed on retry, 0 failed
duration p50 12ms · p95 13ms · range 11-13ms

RUNS                                                        newest first
────────────────────────────────────────────────────────────────────────

               ─     absent from 14 runs, 14 partial
  08-20 15:35  ✗→✓   attempt 2         12ms  c670fc3
  08-20 15:34  ✗→✓   attempt 2         11ms  c670fc3
  08-20 14:02  ✗→✓   attempt 2         13ms  c670fc3
  08-20 14:02  ✗→✓   attempt 2         11ms  c670fc3
  08-20 13:20  ✗→✓   attempt 2         12ms  c670fc3
  08-20 13:19  ✗→✓   attempt 2         12ms  c670fc3

FINDINGS                                                      2 findings
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    failed 6 of 12 executions (50%) in 6 of 20 runs, 1 failure mode
    evidence low | f_8f042eab | .../CalculatorTests.cs:42

2.  MED   masked by retry
    passed on retry 6 times in 6 of 20 runs, up to attempt 2
    evidence low | f_a2b61c2f | .../CalculatorTests.cs:42
```

2 findings about this test · all: xping report --all --assembly SampleApp.MSTest --runs 20
Detail of row 1: xping report --id f_8f042eab --assembly SampleApp.MSTest --runs 20
````

**Measured against the rules.**
- **Frame:** the two header lines, the fence, the footer and the `Detail of row 1` line are
  `report`'s (Amendment 1A). Only the lines between the fences are held to 72 columns.
- **Rows:** `1.` and `2.` are the findings' row numbers in the full report, where siblings are
  adjacent (`FindingOrder.WithSiblingsAdjacent`) (Amendment 1B).
- **Width:** inside the fence, the widest line is the rule (72).
- **Floors:** the test ran in 6 runs, which clears `MinimumSessionsPerTestToReport` (5), so findings are
  reported. At 4 runs the block would carry D6's reason instead.
- **Absences:** the absent line is D8. All 14 runs were partial, so the `stopped running` finding that
  #140 produced is gone. `Vanished` excludes partial runs (`excludesPartialRuns`), and the view shows
  why.
- **Drill-down:** the `Detail of row 1:` line is the first finding's `drillDown` (D10), and the
  footer's `--all` is `ForFullReport`. Both carry `--runs 20` because the window is `Store`. Both sit
  below the fence (`TextReportRenderer.cs:1056-1112`).

### 3.2 A healthy test

````
$ xping report --test Add_ReturnsSum
Xping · SampleApp.MSTest · 20 runs · 2026-08-20 → 2026-08-21 · main@c670fc3
16 tests · 10 healthy · 6 flagged

```
TEST                                                         fp 91c0de44
────────────────────────────────────────────────────────────────────────

    test    SampleApp.MSTest.CalculatorTests.Add_ReturnsSum
    source  tests/SampleApp.MSTest/CalculatorTests.cs:18

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

no findings about this test · all: xping report --all --assembly SampleApp.MSTest --runs 20
````

Every row is rendered; nothing is truncated. A store capped at ~50 runs per assembly fits on a
scrolled terminal, and a truncated history would hide the run a reader came for.

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
    IReadOnlyList<SameSubjectDto> FindingRows,            // Amendment 4A; one per findings entry
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

### 6.1 Branches and review

This feature is built on a long-lived feature branch, **`feat/141-report-test`**, and reaches `main`
in one merge at the end. Each phase is reviewed on its own, as the diff of one PR. An agent picking up
a phase in a new session follows these steps exactly:

1. **Start from the feature branch, not `main`.** `git fetch`, check out `feat/141-report-test`,
   and pull it. The previous phase must already be merged into it. If it is not, stop and say so.
2. **Branch per phase:** `feat/141-p<N>-<slug>`, e.g. `feat/141-p1-window`.
3. **Do the phase,** and only that phase. A conflict with this spec is an amendment committed to the
   phase branch first, before the code that needs it (amendment protocol).
4. **Commit:** conventional, with the issue and phase, e.g.
   `feat(cli): add the whole-store window for report --test (#141, P1)`. One or more commits; the
   reviewer sees the PR diff.
5. **Push and open a PR into `feat/141-report-test`**, never into `main`. Title as the commit subject.
   The body lists what the phase did, any amendment it made, and what the next phase starts from.
   CI runs on it (`ci.yml` triggers on PRs into `feat/**`).
6. **Stop.** Do not start the next phase and do not merge. The user reviews, CI passes, and the user
   merges the phase PR into the feature branch.
7. **After the last phase (P7),** open one PR from `feat/141-report-test` into `main`, with
   `Closes #141`. It is merged by the user.

P0 is the exception: it is read-only and opens no PR. Its amendment (Amendment 1) was committed
directly to `feat/141-report-test`.

- **P0 — Inventory.** Done; see Amendment 1. Read-only. Re-verify every §1.1 citation. Confirm:
  1. That `SubjectDto` for a single test carries everything §3.1's identity block prints.
  2. How `report` renders a finding row without its subject.
  3. The pass/fail colors in `OutputCapabilities`.

  List any conflict with this document. **Stop and report.** Write nothing.
- **P1 — Window.** `WindowResolution.Store`, `WindowRequest.WholeStore`, and `ReportScope.Resolution`
  and `WindowRuns`. Unit tests in `WindowResolverTests`.
- **P2 — `TestResolver`.** D2's three tiers, the unscoped read, ambiguity with widening prefixes (D3),
  and the not-found tiers (D4). Pure and unit-tested in a new `TestResolverTests`, including
  parameterised FQNs with and without typed args, `Outer+Inner` nested types, pinned and pinned
  parameterised fingerprints, renamed tests and hex-looking method names.
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
- Executing tests: never. #139 prints the command (`repro`, `cli-repro-spec.md`).
- Joining across assemblies. Lookup spans assemblies; the view never does.
- More than one test per invocation, or a filter expression.
- `--new` and delta framing (#143).

---

## 8. Reserved for Cloud

- History beyond local retention. Cloud keeps more than 30 days and 50 runs; a Cloud-backed `--test`
  could extend the timeline past what the local store holds. Not specified here.
