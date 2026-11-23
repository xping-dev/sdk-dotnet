---
uid: cli-command-reference
title: Xping CLI Reference
---

# Xping CLI Reference

The `xping` command reads the local run store that the Xping SDK writes, and reports on it. The
report needs no account, no API key and no network access.

This page lists every command, option, exit code and output stream. Two other pages explain the
output:

- [Reading the report](reading-the-report.md): the findings, evidence, population markers, failure
  modes, repro commands and test history.
- [Xping Cloud data in the CLI](cloud-data.md): what the report adds when you sign in or set an API
  key.

---

## Installation

The CLI needs the .NET 10 runtime or SDK. The tool targets `net10.0`. Your test projects can stay
on any .NET version that the SDK packages support.

The packages are prereleases, so each install command needs `--prerelease`.

### As a global tool

```bash
dotnet tool install -g Xping.Cli --prerelease
```

Run it as `xping`, from any folder. Global tools go to `~/.dotnet/tools`
(`%USERPROFILE%\.dotnet\tools` on Windows). The .NET SDK usually adds that folder to your `PATH`. If
your shell says `command not found: xping`, add the folder to `PATH` and open a new terminal.

### As a local tool

```bash
dotnet new tool-manifest        # if the repository has no manifest yet
dotnet tool install Xping.Cli --prerelease
```

Run it as `dotnet xping`. The version is pinned in `.config/dotnet-tools.json`, so everyone on the
team gets the same output. Each teammate runs `dotnet tool restore` once. This page writes `xping`.
With a local tool, write `dotnet xping` instead.

### Printed commands

Every command the CLI prints, in help text, in hints and in the `drillDown` field of
`--format json`, starts with `xping` and runs as printed.

The CLI quotes printed commands for one shell: PowerShell 7.3 or later on Windows, and bash or zsh
elsewhere. Set `XPING_SHELL` to `posix` or `powershell` to choose. Git Bash on Windows, and a coding
agent that runs commands through it, needs `posix`. The `--format json` fields carry the same
strings. cmd.exe and Windows PowerShell 5.1 are not supported. See
[known limitations](../troubleshooting/known-limitations.md#printed-commands-are-quoted-for-one-shell-chosen-by-xping_shell-or-the-os).

---

## Commands

| Command | Does |
|---|---|
| [`xping report`](#xping-report) | Reports findings from recent local runs |
| [`xping where`](#xping-where) | Shows where local runs are stored |
| [`xping clear`](#xping-clear) | Deletes recorded runs |
| [`xping login`](#xping-login) | Signs in to Xping Cloud |
| [`xping logout`](#xping-logout) | Signs out of Xping Cloud and removes the stored sign-in |
| [`xping auth status`](#xping-auth-status) | Shows which Xping Cloud credential the CLI would use |
| [`xping --version`](#xping---version) | Prints the version |

### Help

- `xping --help`, `xping -h`, `xping -?` and `xping help` print the commands and exit `0`.
- `xping` with no arguments prints the same help and exits `1`.
- `xping <command> --help` prints the command's options and exits `0`.
- An unknown command or option, or an invalid value, prints the error on stderr, then
  ``Run `xping report --help` for usage.`` (with the command you ran), and exits `2`.

---

## `xping report`

Reports findings from recent local runs.

```bash
xping report [options]
```

| Option | Default | Description |
|---|---|---|
| `--runs <n>` | see below | The number of recent runs to analyse. `--last` is the same option. Under `--test`, the default is every run in the store |
| `--since <sha\|date>` | — | Analyse the runs from a commit or a date (`yyyy-MM-dd`). Cannot be used with `--runs` |
| `--top <n>` | `10` | The number of rows to show. Findings about one test stay together, so a lower-severity finding can come before a higher one about another test. Cannot be used with `--all` |
| `--all` | off | Show every finding, and every row of [the latest run](reading-the-report.md#the-latest-run) |
| `--kind <kind>...` | all | Show only these [finding kinds](reading-the-report.md#finding-kinds) |
| `--assembly <name>` | the only one | Report on one test assembly. Without it, a store with several assemblies gets [one row per assembly](#scoping) |
| `--id <f_…>` | — | Show one finding in detail. See [Finding ids](reading-the-report.md#finding-ids) |
| `--test <name>` | — | Show one test's run-by-run history, by name or fingerprint. See [Test history](reading-the-report.md#test-history) |
| `--directory <path>` | current folder | Find the store starting from this folder |
| `--project <key>` | — | Read [Xping Cloud data](cloud-data.md) from this project. Overrides `XPING_PROJECTID` |
| `--format <f>` | `text` | `text`, `json` or `summary`, in any case |
| `--json` | off | The same as `--format json` |
| `--summary` | off | The same as `--format summary` |
| `--fail-on <s>` | `none` | Exit `1` when a finding reaches this severity: `high`, `medium` or `low`. Without `--assembly`, every assembly is checked |
| `--ascii` | auto | Use ASCII output |
| `--no-color` | auto | Never use ANSI colour. `NO_COLOR` does the same |

`--last`, `--json` and `--summary` are shorter ways to write `--runs`, `--format json` and
`--format summary`.

**The default window** is the last 20 runs, cut to the runs from the last 14 days. When no run is
that recent, the window is the last 20 runs.

**Options that cannot be combined.** The command exits `2` for each of these:

- `--id` with `--test`, `--kind`, `--top`, `--all`, `--fail-on` or `--summary`;
- `--test` with `--kind`, `--top`, `--all`, `--fail-on` or `--summary`;
- `--runs` with `--since`, and `--all` with `--top`;
- `--json` with `--summary`, and either of them with a different `--format`;
- `--format summary` with `--id` or `--test`;
- `--kind`, `--top` or `--all` without `--assembly`, when the store holds several assemblies.

`--all` means every finding, not every assembly. Use `--assembly` to choose an assembly.

### Report exit codes

| Code | Meaning |
|---|---|
| `0` | A report was produced, and no finding reached `--fail-on` |
| `1` | A finding reached `--fail-on`, in any assembly |
| `2` | No report could be produced: an invalid option or combination; no store; no runs, or no runs for `--assembly`; a `--since` commit that no run carries, or a `--since` value that is neither a commit nor a date; no run that names its test assembly |
| `3` | With `--id`, the report was produced and that finding is not in it. With `--test`, no test matches the name, or none of its runs is in the window |
| `4` | With `--test`, the name matches more than one test. With `--id` and no `--assembly`, more than one assembly reports the finding |
| `70` | The CLI crashed |

Ctrl+C ends `report` without an exit code of its own. See [Exit codes](#exit-codes).

Exit `1` means "the report found problems". Exit `2` means "the report could not run". A build step
can tell the two apart. Xping Cloud never changes the exit code.

### Output streams

- stdout carries the report, or the JSON envelope.
- stderr carries every warning, error and hint, so `--format json` on stdout stays valid JSON.
- When no run names its test assembly, stderr says
  `No runs in <store> record which test assembly they belong to.`, and the command exits `2`.

### The output

The default output is the text report. [Reading the report](reading-the-report.md) explains each
part. The other forms are below.

### The Cloud invitation

At a terminal, after a text report with findings, the CLI can print one line on stderr:

```
See flakiness across CI and your whole team → https://xping.io/start
```

It appears at most once every 24 hours per store. The CLI keeps the time in `state.json` in the
store folder. It does not appear when:

- stdout is not a terminal;
- you use `--format json`;
- the report is the overview of several assemblies;
- the report has no findings;
- the newest run was recorded in Cloud mode, or Xping Cloud answered the report;
- `XPING_NO_BANNER` is set to any non-empty value.

### `--summary`

One line, for a chat message, a commit trailer or a CI step title:

```bash
xping report --summary
```

<!-- xping:sample docs-checkout summary -->
```
Xping: 3 findings (1 high, 1 medium, 1 low) in 20 runs of Checkout.Tests
```

When the newest run has new failures, the line ends with them:

<!-- xping:sample latest-run summary -->
```
Xping: 5 findings (5 high) in 20 runs of SampleApp.XUnit, 2 new failures
```

On a store with several assemblies, `--summary` prints one line per assembly. It never asks Xping
Cloud.

### Scoping

Every test project in a solution writes to one store. A report covers one assembly, because
mixing unrelated suites would mix their tests and their run counts.

When the store holds one assembly, `xping report` is that assembly's report. When it holds several,
`xping report` prints one row per assembly, most severe first, and the command that opens the
first:

```
Xping · 3 assemblies

Checkout.Tests                               4 findings (1 high, 3 medium)
  412 tests · 4 flagged · 20 runs · last 2026-11-08 16:45
  cloud: 1 broken · 2 known flaky
Billing.Tests                                                  no findings
  96 tests · 0 flagged · 20 runs · last 2026-11-08 16:44
  cloud: no test flagged
Search.Tests                                                   no findings
  57 tests · 0 flagged · 6 runs · last 2026-11-02 09:12
  cloud: no project in this workspace for Search.Tests · pass --project
         <key>

Details: xping report --assembly Checkout.Tests
```

- Each row repeats the header of that assembly's own report, with the same window and numbers.
- With a Xping Cloud credential, each row also gets its `cloud:` line. A reason that every row
  shares, such as `cloud: login required · run xping login`, is printed once, under the first line.
  See [Xping Cloud data in the CLI](cloud-data.md).
- `--runs` and `--since` set every assembly's window. An assembly with no runs in the window gets a
  row that says so.
- `--fail-on` checks every assembly.
- `--json` writes an envelope with `"scope": "overview"` and an `assemblies` array. An assembly's
  own report has `"scope": "assembly"`. Each row's `cloud` holds its `status`, `reason`,
  `projectKey` and `health` without the list of tests. It is `null` for a row with no window.
- `--id` without `--assembly` looks in every assembly. So does `--test`.

**A run can belong to several assemblies.** A run is one test host process, and `dotnet test` on a
solution can run several test projects in one host. Such a run counts as one run of each assembly
it covered. `--assembly` keeps only that assembly's tests. So two assemblies can each report 20 runs
from the same 20 invocations.

Runs that name no assembly stay in the store, and `xping where` counts them, but no report includes
them.

**Runs recorded in CI have no commit.** The SDK records the commit only outside CI. So the header
shows no commit for a CI run, and `--since <sha>` matches only runs recorded on a developer machine.

### `--format json`

For scripts and agents. The command writes a versioned envelope, and nothing else, to stdout:

```bash
xping report --assembly Checkout.Tests --all --format json > findings.json
```

The top-level keys are `schemaVersion`, `scope`, `window`, `context`, `summary`, `latestRun`,
`selection`, `test`, `findings` and `truncated`.

**Each finding** carries:

- `headline`, the sentence the text report prints, and `metrics`, the labelled values behind it;
- `evidence`, the raw values both were computed from;
- `evidenceLevel` and `evidenceSessions`, the number of runs the level was computed from;
- `population`: `allExecutions`, `excludesEnvironmental`, `excludesEnvironmentalAndClustered` or
  `excludesPartialRuns`. It says what the finding's published rate was counted out of: executions,
  or runs for `excludesPartialRuns`. It applies to that rate only. A `stopped running` finding
  counts its appearances over full runs, while its `executionsInWindow` covers the whole window. See
  [the population marker](reading-the-report.md#the-population-marker);
- `annotation`, on the second and later findings about one test, naming the row of the first. The
  CLI groups them before `--top` cuts the list, so an annotation never points at a row that is not
  there;
- `drillDown`, the `--id` command for the finding;
- `repro` and `failureModes`, described below;
- `cloud`, described in [Xping Cloud data in the CLI](cloud-data.md#cloud-data-in---format-json).

**`latestRun`** is [the latest run](reading-the-report.md#the-latest-run) as data: the newest run,
its counts, and one entry per row with the same `contrast` sentence and the same `subject` a finding
carries. It is present whenever the window holds a run. Use it to tell:

- a clean run (`testsFailed` is `0`);
- a run the report did not list, because it looked environmental (`isLikelyEnvironmental`);
- a list the report cut (`failuresShown` against `failuresTotal`). `truncated.command` shows the
  rest.

`explainedByFindingIds` lists, in rank order, the findings that cover the failures not listed as
rows.

**`test`** is `null` unless you gave [`--test`](reading-the-report.md#test-history). Then
`window.resolution` is `store`, unless `--runs` or `--since` narrowed it. `test` holds:

- `query`, what you typed;
- `test`, the test it matched, as a `subject`, and `shortFingerprint`, the value `--test` accepts;
- `sessions`, one entry per run in the window, newest first, with `sessionId`, `startedAt`, `sha`,
  `appeared`, `partial`, the deciding `outcome`, `decidingAttempt`, `durationMs` and every attempt in
  `attempts`;
- `counts` and `duration`, behind the two summary lines. `duration` also has `dispersion`, which the
  text leaves out;
- `findingsWithheld`, the reason when there was too little history for findings;
- `findingRows`, the full-report row of each entry in `findings`. `findings` holds only the findings
  about this test;
- `candidates`, when the name matched more than one test. Each has `fingerprintPrefix`, `assembly`,
  `fullyQualifiedName`, `lastSeen` and the `command` that names it, and `test.test` is `null`. When
  the name matched nothing, `test.test` is `null` and `candidates` is empty.

When there is no window, for example a `--since` date after every run, stdout is empty. Only the
exit code and stderr answer.

**`repro`** is [the repro command](reading-the-report.md#reproducing-a-finding) as data, or `null`.

- `command` is the `Repro:` line.
- `runs` is the number of runs to make, or `null` above the limit of 50. `runsNeeded` is the same
  number without the limit.
- `rate` is `runsFailed` out of `runsJudged`: the runs the test failed in, out of the runs it ran in
  with a result.
- `expectation` is the sentence the `repro` line prints.

A group's members in `evidence` have their own `runsFailed` and `runsJudged`, and so do the `Flaky`,
`AlwaysFailing` and `TimingOut` evidence records.

**`failureModes`** lists the [failure modes](reading-the-report.md#failure-modes), the most frequent
first: every mode for `Flaky`; the one signature for `AlwaysFailing` (its most common),
`BrokenFixture` and `SharedFailure`; and an empty list for the other kinds. Each mode has:

- `exceptionType` (`null` when the adapter recorded none), the normalised `message`, and up to five
  `frames`, innermost first;
- `occurrences`, the failures the finding counted, and when the mode was first seen among them;
- `firstSeenInLatestSession` and `firstSeenAfterFirstRun`, which say whether the mode is new. Both
  look at every failure in the window, from the test's first run in it with a result (for a group,
  its members' oldest). So `firstSeenSessionsAgo` of `0` alone does not mean new. A test whose only
  run is the newest has neither flag set;
- `hash`, which matches `evidence.exemplars[].signatureHash`;
- `unavailable`, `true` when nothing was recorded to group the failure by;
- `timedOut`, `true` when the mode's attempts timed out. A mode never mixes timed-out and failed
  attempts.

`report --test --format json` carries the same list for the test, over every failed attempt in the
window.

**`summary`** counts the suite:

- `tests` is `healthy` plus `flagged` plus `failedLatestRun`.
- `flagged` counts tests and `findings` counts findings. One test can have several findings, and one
  `broken fixture` finding covers every test the fixture failed, so the two numbers differ.
- `failedLatestRun` counts the latest run's rows: the tests it failed that no finding names.
- `healthy` means no finding and no failure in the newest run. It includes tests that were not
  measured, and a test first seen in a partial run, with one run of history.
- `excludedLowEvidence` and `excludedNotSignificant` are the `awaiting more runs` and
  `not significant` counts.
- `notMeasured` says, per kind, how many tests the kind could not measure: `awaitingRuns` need more
  runs, and `unreadable` have recorded data that cannot answer the question. Do not add the entries
  up: one test is counted once for each kind it could not answer.
- `environmentalSessions`, `partialSessions`, `incompleteSessions`, `unreadableSessions` and
  `skewedSessions` count the runs of each kind in the window. `failedProviders` names any kind
  whose analysis failed.

```json
{
  "schemaVersion": "1.33",
  "scope": "assembly",
  "window": {
    "from": "2026-08-05T09:12:00Z", "to": "2026-08-19T16:21:00Z", "sessionCount": 20,
    "resolution": "default", "resolutionArgument": null, "currentSliceSize": 3,
    "sessionIds": ["6f9a2f1c-…", "…"]
  },
  "context": {
    "sha": "a3f9c2e", "branch": "main", "assembly": "Checkout.Tests",
    "cloud": {
      "status": "ok", "reason": "none", "elapsedMs": 412,
      "cloudUrl": "https://app.xping.io", "credential": "login", "workspaceId": "01J8…", "workspace": "Acme",
      "fetchedAt": "2026-08-19T16:30:00+00:00", "signatureAlgorithm": "emh-1", "retentionDays": 365,
      "requestedTests": 4, "enrichedTests": 4,
      "projects": [
        { "assembly": "Checkout.Tests", "projectKey": "checkout-tests", "source": "name-match", "status": "ok", "reason": "none" }
      ],
      "sessions": [
        { "sessionKey": "6f9a2f1c-…", "projectKey": "checkout-tests", "state": "complete", "scored": true, "flakyExamined": true }
      ],
      "health": {
        "state": "ok", "reason": "none", "totalTests": 412, "unhealthyTests": 3,
        "verdicts": { "broken": 1, "knownFlaky": 2, "unstable": 0, "flagged": 0 }, "shownElsewhere": 2,
        "tests": [ { "subject": { "type": "test", "fingerprint": "…", "fullyQualifiedName": "Checkout.Tests.CartTests.Checkout_Rounds", "…": "…" }, "digest": { "verdict": "known-flaky", "…": "…" } } ],
        "testsShown": 1, "testsTotal": 1
      }
    }
  },
  "summary": {
    "tests": 412,
    "findings": 3,
    "counts": { "high": 1, "medium": 1, "low": 1 },
    "flagged": 3,
    "failedLatestRun": 1,
    "healthy": 408,
    "excludedLowEvidence": 41,
    "excludedNotSignificant": 6,
    "notMeasured": {
      "DurationRegression": { "awaitingRuns": 63, "unreadable": 32 },
      "ParallelSensitive": { "awaitingRuns": 0, "unreadable": 108 },
      "…": "…"
    },
    "environmentalSessions": 0,
    "partialSessions": 0,
    "incompleteSessions": 0,
    "unreadableSessions": 0,
    "skewedSessions": 0,
    "failedProviders": []
  },
  "latestRun": {
    "sessionId": "6f9a2f1c-…",
    "startedAt": "2026-08-19T16:21:00Z",
    "sha": "a3f9c2e",
    "isLikelyEnvironmental": false,
    "testsExecuted": 412,
    "testsFailed": 3,
    "newFailures": 1,
    "explainedByFindings": 2,
    "explainedByFindingIds": ["f_2a91c0de", "f_8c04b71a"],
    "failures": [
      {
        "status": "new",
        "subject": { "type": "test", "shortName": "CartTests.Checkout_AppliesDiscount", "…": "…" },
        "contrast": "passed the previous 19 runs, failed just now",
        "failureSummary": "NullReferenceException",
        "priorSessions": 19,
        "priorFailures": 0,
        "cloud": null
      }
    ],
    "failuresShown": 1,
    "failuresTotal": 1
  },
  "selection": null,
  "test": null,
  "findings": [
    {
      "id": "f_2a91c0de",
      "kind": "Flaky",
      "severity": "high",
      "evidenceLevel": "adequate",
      "evidenceSessions": 12,
      "population": "excludesEnvironmentalAndClustered",
      "subject": { "type": "test", "fullyQualifiedName": "…", "shortName": "SummaryTests.GenerateMonthlySummary",
                    "causeLabel": null, "assembly": "Checkout.Tests",
                    "sourceFile": "tests/Billing/SummaryTests.cs", "sourceLineNumber": 88 },
      "annotation": null,
      "headline": "failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes",
      "metrics": [
        { "label": "failed", "value": "7 of 20 executions (35%)" },
        { "label": "runs affected", "value": "5 of 20" },
        { "label": "failure modes", "value": "3" }
      ],
      "failureModes": [
        {
          "hash": "9c41e07b2d3a", "exceptionType": "System.InvalidOperationException",
          "message": "summary for <date> has no rows", "frames": ["Billing.SummaryTests.GenerateMonthlySummary()"],
          "framesDegraded": false, "unavailable": false, "timedOut": false, "occurrences": 4,
          "firstSeenAt": "2026-08-12T09:14:00Z", "firstSeenSha": "c670fc3d1e2f", "firstSeenSessionsAgo": 14,
          "firstSeenInLatestSession": false, "firstSeenAfterFirstRun": true,
          "errorMessageHash": "ab12…", "cloud": { "match": "matched", "signature": { "errorMessageHash": "ab12…", "…": "…" } }
        },
        { "hash": "…", "exceptionType": "System.TimeoutException", "…": "…" },
        { "hash": "…", "exceptionType": null, "unavailable": true, "…": "…" }
      ],
      "evidence": { "…": "…" },
      "drillDown": "xping report --id f_2a91c0de --assembly Checkout.Tests",
      "repro": {
        "command": "dotnet test tests/Checkout.Tests/Checkout.Tests.csproj -e XPING_ENABLED=false --filter FullyQualifiedName=Billing.SummaryTests.GenerateMonthlySummary",
        "runs": 11, "runsNeeded": 11, "runsFailed": 5, "runsJudged": 20, "rate": 0.25,
        "expectation": "run 11 times: at 5 of 20 runs failing (25%), 11 runs show a failure 95% of the time"
      },
      "cloud": {
        "state": "ok", "fetchedAt": "2026-08-19T16:30:00+00:00", "disagreesWithLocal": false, "identity": null,
        "verdict": "known-flaky", "testName": "Billing.SummaryTests.GenerateMonthlySummary",
        "lastExecutedAt": "2026-08-19T12:00:00+00:00", "totalRuns": 812,
        "confidence": { "state": "scored", "score": 0.62, "category": "moderately-reliable", "trend": "stable",
                        "effectiveSampleSize": 48.0, "evidenceLevel": "robust", "window": { "kind": "days", "length": 35, "…": "…" }, "…": "…" },
        "flaky": { "state": "flaky", "category": "timing", "severity": "moderate", "…": "…" },
        "canonical": { "runs": 10, "failed": 3, "latestOutcome": "passed", "…": "…" },
        "lastTransition": { "type": "flakiness-introduced", "…": "…" },
        "signatures": [ { "errorMessageHash": "ab12…", "runs": 36, "ciRuns": 34, "localRuns": 2, "machines": 6, "…": "…" } ]
      }
    }
  ],
  "truncated": { "shown": 3, "total": 3, "command": "xping report --all --assembly Checkout.Tests" }
}
```

`"…": "…"` marks fields left out of this sample.

---

## `xping where`

Prints where the local store is and what it holds.

```bash
xping where [--directory <path>]
```

```
/Users/you/src/my-repo/.xping
  36 runs · 6.1 MB on disk
  Checkout.Tests                             12 runs  last 2026-08-08 09:14 UTC
  Billing.Tests                              12 runs  last 2026-08-08 09:13 UTC
  Api.Tests                                  12 runs  last 2026-08-08 09:11 UTC
```

Run it first when a report looks wrong or empty. It shows the folder that the SDK and the CLI
resolve. See [Local store](../configuration/local-store.md#location) for the rules.

- The size includes temporary files left by interrupted writes, because retention counts them.
- A run that covered several assemblies is counted under each of them, so the assembly counts can
  add up to more than the total.
- An assembly name longer than 40 characters is cut, and ends in `~`.
- `N runs name no assembly and are not shown` appears when some runs name no assembly.
- `(no runs recorded yet)` follows the path when the store has no runs.

When no store can be resolved, `xping where` prints
`No Xping local store could be resolved from this directory.` on **stdout** and exits `1`. This
happens only when the repository root's `.xping` folder is missing or cannot be written, and the
folder under your user profile is not available either.

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | No store could be resolved |
| `70` | The CLI crashed |

---

## `xping clear`

Deletes recorded runs.

```bash
xping clear [options]
```

| Option | Description |
|---|---|
| `--assembly <name>` | Delete only this test assembly's runs. The name must match exactly |
| `--force` | Do not ask for confirmation |
| `--directory <path>` | Find the store starting from this folder |

You cannot get deleted runs back. They are not in version control, and in local-only mode they are
nowhere else. So `clear` asks first, on stdout:

```
Delete all 36 runs from /Users/you/src/my-repo/.xping? [y/N]
```

Any answer other than `y` prints `Cancelled.` and deletes nothing.

When stdin is not interactive (a script, a CI step or a pipe), `clear` refuses, on stderr:

```
Refusing to delete all 36 runs without confirmation. Re-run with --force to proceed non-interactively.
```

- A run from a solution-wide `dotnet test` holds several assemblies. `--assembly` removes that
  assembly's tests from the run and keeps the run for the others.
- Clearing everything also deletes temporary files left by interrupted writes. `--assembly` leaves
  them, because a temporary file names no assembly.
- On success, stdout says `Deleted N runs.`
- With nothing to delete, stdout says `Nothing to clear.`, or
  `No runs recorded for assembly 'X'.` with `--assembly`, and the command exits `0`.
- When no store can be resolved, stderr says
  `No Xping local store could be resolved from this directory.`

| Code | Meaning |
|---|---|
| `0` | The runs were deleted, or there was nothing to delete |
| `1` | No store could be resolved; you cancelled; `clear` refused without `--force`; or some runs could not be deleted |
| `70` | The CLI crashed |

---

## `xping login`

Signs you in to Xping Cloud, so `xping report` can show [Xping Cloud data](cloud-data.md) for your
tests.

```bash
xping login [--device] [--no-browser] [--workspace <id>] [--json]
```

| Option | Description |
|---|---|
| `--device` | Sign in with a code on any device. Use it when your browser is on another machine |
| `--no-browser` | Print the sign-in link without trying to open a browser |
| `--workspace <id>` | Preselect this workspace on the consent page. The id is the 26-character value that `xping auth status` shows after the workspace name |
| `--json` | Write the result to stdout as JSON |

The [global options](#global-options) apply too. `--cloud-url` signs in to another Xping Cloud.

`login` opens your browser on the Xping Cloud consent page and waits up to 5 minutes for you to
approve. It always prints the link too, in case the browser does not open:

```
Opening your browser to sign in to Xping Cloud (https://app.xping.io).
If it does not open, use this link:

  https://app.xping.io/connect/authorize?response_type=code&client_id=xping-cli&...

Waiting for you to finish in the browser (up to 5 minutes)...
✓ Signed in as jane@example.com
  Workspace  Acme (01J8K2V6XN7Y0Q4R5S6T7U8V9X)
  Session    ...A7F2Q9   (shown on Settings → Security → CLI sessions)
  Stored in  macOS Keychain
```

The browser must run on the same machine as the CLI, because Xping Cloud returns you to a one-time
address on `127.0.0.1`.

The CLI does not try to open a browser over SSH, on Linux without a display, or in a container
(Docker, devcontainers, Codespaces). It prints
`No browser was found on this machine. Open this link in a browser on this machine:` and the link.
With `--no-browser`, the first line is `Open this link in a browser on this machine:`. In both cases
the CLI then prints
``If your browser is on another machine, press Ctrl+C and run `xping login --device`.``

With `--device`, open the page on any device, such as your phone or the laptop you connect from, and
enter the code. The code expires after the time shown:

```
To sign in, open  https://app.xping.io/device
and enter the code  ABCD-EFGH

(or open https://app.xping.io/device?user_code=ABCD-EFGH)

Waiting for you to approve in the browser (up to 10 minutes)...
```

All text for people goes to stderr. stdout carries only the `--json` document.

### Only in your own terminal

`login` refuses to run when stdin or stderr is not a terminal, or when `CI` is `true`, `1` or `yes`:

```
xping login needs an interactive terminal. Run it in your own shell. Coding agents and CI must use an API key or a login stored earlier; see `xping auth status`.
```

It exits `13`. With `--json`, it still writes the failure document. CI uses an API key instead (see
[What you need](cloud-data.md#what-you-need)). A coding agent runs `xping auth status --json` to
learn whether Cloud data is available, and asks you to run `xping login` when it is not.

### Where the sign-in is stored

| OS | Store |
|---|---|
| Windows | Windows Credential Manager |
| macOS | macOS Keychain |
| Linux | Secret Service (GNOME Keyring or another provider), through libsecret |

The entry is named `xping-cli` and is keyed by the Cloud URL, so you can be signed in to two Xping
Clouds. When no OS store can be used, for example over SSH with a locked keychain, on a server
without a D-Bus session, or without libsecret, the sign-in goes to `~/.xping/credentials.json`, and
`login` says so:

```
⚠ Stored credentials in ~/.xping/credentials.json because no OS credential store is available (Secret Service not running). The file is readable only by you.
```

The same happens when the OS store refuses the write. On Windows, a damaged user profile makes every
Credential Manager write fail with error 8 (`cmdkey` fails the same way):

```
⚠ Could not write to Windows Credential Manager (error 8: Not enough memory resources are available to process this command).
⚠ Stored credentials in ~/.xping/credentials.json instead. The file is readable only by you.
```

- The file has mode `0600`, in a folder with mode `0700` (an owner-only ACL on Windows).
- The CLI refuses to read the file when other users can read it, and says how to fix it. `auth status`
  always says so. `report` says so when it has no API key to use instead, at a terminal or with
  `--verbose`:

  ```
  Refusing to read ~/.xping/credentials.json because other users can read it. Run `chmod 600 ~/.xping/credentials.json` (and `chmod 700 ~/.xping`) and try again.
  ```

- A new `login` replaces the file. A sign-in in the file is still found from a desktop session, and
  a later `login` there moves it to the OS store.

> **macOS: one keychain prompt after each upgrade.** Apple does not sign the `xping` tool, so macOS
> identifies it by its hash. After `dotnet tool update`, the first command that reads the sign-in
> shows a dialog: xping wants to use your confidential information stored in `xping-cli`. Choose
> **Always Allow**. The dialog does not appear again until the next upgrade.

### WSL and devcontainers

- **WSL 2.** `login` opens your Windows browser through `wslview` (from wslu) or `rundll32`. WSL 2
  forwards `127.0.0.1` to Windows by default, so the browser can return to the CLI. If the sign-in
  never arrives, because a networking mode turns forwarding off, use `xping login --device`. WSL
  usually has no keyring, so expect the file store.
- **VS Code devcontainers and Codespaces.** The CLI treats them as headless and prints the link. Use
  `xping login --device`. The browser sign-in works only if you forward the port it listens on to
  your machine. The sign-in is stored in the container's `~/.xping/credentials.json` and is lost
  with the container.

### `--json`

```json
{
  "schemaVersion": "1",
  "result": "signed-in",
  "cloudUrl": "https://app.xping.io",
  "flow": "loopback",
  "email": "jane@example.com",
  "sub": "01J8K2V6XN7Y0Q4R5S6T7U8V9W",
  "workspaceId": "01J8K2V6XN7Y0Q4R5S6T7U8V9X",
  "workspaceName": "Acme",
  "sessionId": "01J8K2V6XN7Y0Q4R5S6T7U8VA7F2Q9",
  "store": "keychain",
  "accessTokenExpiresAt": "2026-09-29T10:15:00Z"
}
```

`flow` is `loopback` or `device`. `store` is `keychain` or `file`. `workspaceName` is `null` when
Xping Cloud sent no name. The text then shows the workspace id alone.

Every failure of `login`, `logout` and `auth status` under `--json` writes the same document:

```json
{
  "schemaVersion": "1",
  "result": "failed",
  "cloudUrl": "https://app.xping.io",
  "error": "access_denied",
  "message": "You declined the sign-in request. Nothing was stored.",
  "oauthError": null
}
```

`error` is one of `access_denied`, `timeout`, `interactive_required`, `cloud_unreachable`,
`version_mismatch`, `credential_store`, `oauth_error` (the server's code is in `oauthError`),
`cancelled`, `configuration` or `local_listener`. No document ever contains a token.

| Code | Meaning |
|---|---|
| `0` | Signed in |
| `2` | Invalid option or configuration |
| `11` | You declined |
| `12` | The sign-in timed out, or the device code expired |
| `13` | Not an interactive terminal, or `CI` is set |
| `14` | Another sign-in failure |
| `15` | Xping Cloud could not be reached |
| `16` | Xping Cloud does not accept this CLI version |
| `17` | The credential store failed |
| `70` | The CLI crashed |
| `130` | You pressed Ctrl+C |

---

## `xping logout`

Revokes the session on Xping Cloud and removes the stored sign-in.

```bash
xping logout [--json]
```

There is no prompt.

```
✓ Signed out of https://app.xping.io (jane@example.com).
```

When Xping Cloud cannot be reached, the CLI removes the local sign-in anyway. The session stays valid
on the server until it expires or you revoke it:

```
⚠ Could not reach https://app.xping.io to revoke the session (connection failed).
  Your local credentials were removed. The session may still exist on the server; revoke it
  under Settings → Security → CLI sessions.
✓ Signed out locally.
```

- `logout` also deletes the cached project list and the cached Xping Cloud endpoints for that Cloud
  URL.
- When you are not signed in, it prints `You are not signed in to https://app.xping.io.` and exits
  `0`. If an API key is set, it then says where the key comes from, so you can unset it.
- `logout` never changes an API key.

`--json` writes
`{ "schemaVersion": "1", "result": "signed-out", "cloudUrl": "…", "revoked": true, "warning": null }`.
`result` is `signed-out`, `signed-out-locally` or `not-signed-in`.

| Code | Meaning |
|---|---|
| `0` | Signed out, or not signed in |
| `2` | Invalid configuration |
| `17` | The stored sign-in could not be removed. The message names the store |
| `70` | The CLI crashed |
| `130` | You pressed Ctrl+C |

---

## `xping auth status`

Shows which Xping Cloud credential the CLI would use, without a network call.

```bash
xping auth status [--json]
```

```
Cloud URL     https://app.xping.io
Credential    stored login (macOS Keychain)
Signed in     jane@example.com
Workspace     Acme (01J8K2V6XN7Y0Q4R5S6T7U8V9X)
Session       ...A7F2Q9
Access token  expires in 12 minutes (refreshed automatically)
```

The CLI refreshes the access token when a command needs it. The `Access token` row can also read
`expired (refreshed automatically on next use)` or `none stored (refreshed automatically on next use)`.
Neither is a problem.

Other cases:

- An API key is set too: `API key (XPING_APIKEY) also set; used only when no login is available.`
- `--api-key` was given: `Credential    API key (--api-key)`. When a sign-in is stored too, the CLI
  adds `A stored login also exists and is not used while --api-key is given.`
- Only an API key: `Credential    API key (XPING_APIKEY)`. The CLI cannot check a key without a
  network call, so `auth status` does not.
- No credential: `Credential    none` and ``Run `xping login` to sign in.``

`--verbose` adds the paths in use under `~/.xping`.

`--json`:

```json
{
  "schemaVersion": "1",
  "cloudUrl": "https://app.xping.io",
  "credential": "stored-login",
  "credentialSource": "keychain",
  "loggedIn": true,
  "email": "jane@example.com",
  "sub": "01J8K2V6XN7Y0Q4R5S6T7U8V9W",
  "workspaceId": "01J8K2V6XN7Y0Q4R5S6T7U8V9X",
  "workspaceName": "Acme",
  "sessionId": "01J8K2V6XN7Y0Q4R5S6T7U8VA7F2Q9",
  "accessTokenExpiresAt": "2026-09-29T10:15:00Z",
  "storedAt": "2026-09-01T08:00:00Z",
  "fallbackApiKey": "env",
  "shadowedLogin": false,
  "warnings": []
}
```

- `credential` is `stored-login`, `api-key` or `none`.
- `credentialSource` is `keychain` or `file` for a sign-in, and `flag`, `env` or `config` for a key.
- `fallbackApiKey` is `env` or `config`: a key the CLI uses if the sign-in stops being valid.
- `warnings` holds full sentences, such as the file-store notice or a refused credentials file, for
  an agent to pass on.

| Code | Meaning |
|---|---|
| `0` | A credential is available: a sign-in or a key |
| `2` | Invalid configuration, such as a bad `XPING_CLOUDURL`. With `--json`, a failure document |
| `10` | No credential |
| `17` | No credential, and a credential store could not be read |
| `70` | The CLI crashed |
| `130` | You pressed Ctrl+C |

---

## `xping --version`

Prints the tool version, for example `1.0.0-rc.5`.

---

## Global options

These work on every command, before or after the command name.

| Option | Description |
|---|---|
| `--cloud-url <url>` | The Xping Cloud to sign in to and read from. Default `https://app.xping.io` |
| `--api-key <key>` | An API key for reading Xping Cloud data. Prefer `XPING_APIKEY`, so the key does not go into your shell history |
| `--verbose` | Write diagnostics to stderr: which credential is used, token refreshes, why Cloud data is missing, and the stack trace of a crash. Tokens and keys are never printed |

The Cloud URL comes from the first of these that is set:

1. `--cloud-url`;
2. `XPING_CLOUDURL`, `Xping__CloudUrl` or `XPING__CLOUDURL`;
3. `Xping:CloudUrl` in `appsettings.json` or `appsettings.{environment}.json`, in the current
   folder (for `report`, in the `--directory` folder when you give one). `{environment}` is
   `ASPNETCORE_ENVIRONMENT`, else `DOTNET_ENVIRONMENT`, else `Production`;
4. `https://app.xping.io`.

The URL must use `https`. `http` is accepted only for `localhost` and `127.0.0.1`. Each Cloud URL has
its own sign-in.

---

## Exit codes

Every code any command returns:

| Code | Meaning | Commands |
|---|---|---|
| `0` | Success | all |
| `1` | A finding reached `--fail-on`; `where` or `clear` failed; `xping` was run with no arguments | `report`, `where`, `clear`, `xping` |
| `2` | An invalid option or value (including `--cloud-url`, `--project` and `--workspace`); `report` could not produce a report; for `login`, `logout` and `auth status`, an invalid `XPING_CLOUDURL` or settings file (`report` prints a hint instead) | all |
| `3` | The `--id` finding is not in the report; no test matches `--test`, or none of its runs is in the window | `report` |
| `4` | `--test` matches more than one test; `--id` without `--assembly` is reported by more than one assembly | `report` |
| `10` | No credential | `auth status` |
| `11` | You declined the sign-in | `login` |
| `12` | The sign-in timed out, or the device code expired | `login` |
| `13` | Not an interactive terminal, or `CI` is set | `login` |
| `14` | Another sign-in failure | `login` |
| `15` | Xping Cloud could not be reached | `login` |
| `16` | Xping Cloud does not accept this CLI version | `login` |
| `17` | The credential store failed | `login`, `logout`, `auth status` |
| `70` | The CLI crashed. stderr says `xping: <message>`. Add `--verbose` for the stack trace | all |
| `130` | You pressed Ctrl+C | `login`, `logout`, `auth status` |

`login`, `logout` and `auth status` stop on Ctrl+C, print `Cancelled.` and exit `130`. `report`,
`where` and `clear` do not handle Ctrl+C themselves. The operating system ends them, and the exit
code is the one your shell reports for an interrupted process.

---

## Environment variables

| Variable | Effect |
|---|---|
| `XPING_LOCAL_STORE` | Moves the local store. Set the same value the SDK used. See [LocalStorePath](../configuration/configuration-reference.md#localstorepath) |
| `XPING_NO_BANNER` | Any non-empty value hides the [Cloud invitation](#the-cloud-invitation) |
| `XPING_SHELL` | The shell printed commands are quoted for: `posix` or `powershell`. Default: `powershell` on Windows, `posix` elsewhere. Any other value prints a warning, and the default applies |
| `XPING_APIKEY` | An API key for Xping Cloud data when you are not signed in. `Xping__ApiKey` and `XPING__APIKEY` work too. See [What you need](cloud-data.md#what-you-need) |
| `XPING_PROJECTID` | The Xping Cloud project to read. `--project` overrides it. `Xping__ProjectId` and `XPING__PROJECTID` work too |
| `XPING_CLOUDURL` | The Xping Cloud to use. `--cloud-url` overrides it. `Xping__CloudUrl` and `XPING__CLOUDURL` work too |
| `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT` | Choose `appsettings.{environment}.json`. The default is `Production` |
| `NO_COLOR` | Any value turns off colour. The same as `--no-color` |
| `FORCE_COLOR` | Any value turns on colour when stdout is not a terminal. `NO_COLOR` and `--no-color` win over it. It does not turn on the Cloud invitation |
| `CI` | When `true`, `1` or `yes`, `xping login` refuses to run |

### Files under `~/.xping`

| Path | Content |
|---|---|
| `~/.xping/credentials.json` | Sign-ins, only when no OS credential store is available. Mode `0600` |
| `~/.xping/cache/discovery/` | Xping Cloud's endpoints, kept for 24 hours |
| `~/.xping/cache/projects/` | Xping Cloud projects per workspace, kept for 24 hours |
| `~/.xping/locks/` | Lock files that stop two commands from refreshing a sign-in at the same time |

`xping logout` removes the sign-in and the caches for that Cloud URL. You can delete the caches at
any time. This folder is not the local run store. See [Local store](../configuration/local-store.md).

---

## See also

- [Reading the report](reading-the-report.md)
- [Xping Cloud data in the CLI](cloud-data.md)
- [Run without an account](../getting-started/local-first.md)
- [Local store](../configuration/local-store.md)
- [Configuration reference](../configuration/configuration-reference.md)
