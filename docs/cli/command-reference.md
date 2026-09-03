---
uid: cli-command-reference
title: Xping CLI Reference
---

# Xping CLI Reference

`Xping.Cli` reads the local run store written by the Xping SDK and reports on it. It needs no account, no API key, and no network access.

The SDK records runs; the CLI interprets them. Keeping analysis out of the test host means your test runs pay no analysis cost and the report never has to compete with the test runner for the terminal.

---

## Installation

> **Requires the .NET 10 SDK or runtime.** The tool targets `net10.0` and will not launch on
> older runtimes. The Xping SDK packages themselves target `netstandard2.0` and are unaffected —
> your test projects can stay on any supported .NET version.

### As a global tool (recommended)

```bash
dotnet tool install -g Xping.Cli
```

Invoke it as `xping`, from any directory. This is how the tool refers to itself: every command it
prints — in help text, in error hints, in the `drillDown` field of `--format json` — is a bare
`xping` command you can paste back into the shell.

> Global tools install to `~/.dotnet/tools` (`%USERPROFILE%\.dotnet\tools` on Windows), which the
> .NET SDK normally adds to your `PATH`. If your shell answers `command not found: xping`, add that
> directory to `PATH` and reopen the terminal.

### As a local tool

```bash
dotnet new tool-manifest        # if your repo has no manifest yet
dotnet tool install Xping.Cli
```

Invoke it as `dotnet xping`. The version is pinned in `.config/dotnet-tools.json`, so everyone on
the team gets identical output; teammates run `dotnet tool restore` once. Choose this if pinning
the CLI version alongside your repo matters more than the shorter command — the rest of this page
writes `xping`, and each of those becomes `dotnet xping` for a manifest install.

---

## `xping report`

Reports test reliability findings from recent local runs.

```bash
xping report [options]
```

| Option | Default | Description |
|---|---|---|
| `--runs <n>` | 20 runs / 14 days | Recent runs to analyse. Alias: `--last` |
| `--since <sha\|date>` | — | Analyse from a commit or date instead. Excludes `--runs` |
| `--top <n>` | `10` | Findings to show. Excludes `--all` |
| `--all` | off | Show **every finding** rather than the top ones |
| `--kind <Kind>...` | all | Restrict to one or more finding kinds |
| `--assembly <name>` | newest | Scope the report to one test assembly |
| `--directory <path>` | working directory | Resolve the store starting from this directory |
| `--format <f>` | `text` | `text`, `json` or `summary` |
| `--json` | off | Alias for `--format json` |
| `--summary` | off | Alias for `--format summary` |
| `--fail-on <s>` | `none` | Exit non-zero when a finding reaches `high`, `medium` or `low` |
| `--ascii` | auto | Force ASCII output |
| `--no-color` | auto | Never emit ANSI colour. `NO_COLOR` is honoured too |

> `--all` means *every finding*, not *every assembly*. Scoping is `--assembly`.

**Exit codes:** `0` a report was produced and nothing reached `--fail-on` · `1` a finding reached `--fail-on` · `2` no report could be produced (no store, no readable runs, or a parse error).

The 1/2 distinction is what lets a build step tell "I looked and found problems" apart from "I could not look". Warnings go to stderr, so `--format json` on stdout stays parsable.

### The report

The default output is built to be shared. The findings sit inside a fenced code block, so selecting the report and pasting it into Slack, a pull request or a ticket renders it in monospace with its columns intact:

````
Xping · Checkout.Tests · 20 runs · 2026-08-05 → 2026-08-19 · main@a3f9c2e
3 findings (1 high, 2 medium) · 412 tests · 409 healthy

```
HIGH  flaky            GenerateMonthlySummary
      failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes
      evidence moderate | f_2a91c0de | tests/Billing/SummaryTests.cs:88

MED   slower           CheckoutFlow_Completes
      3.51x slower (95% CI 1.94-5.87x), 340ms -> 1.2s on the clock
      evidence high | f_8c04b71a | tests/Checkout/FlowTests.cs:214

LOW   stopped running  LegacyImport.Roundtrip
      ran in 12 of 17 earlier runs, absent from the last 3
      evidence moderate | f_1d77e3f5 | tests/Legacy/ImportTests.cs:41
```
````

Only the top ten findings are shown by default. When some are withheld, one line follows the fence — `Showing 10 of 21 · all: xping report --all` — and a report showing everything ends at the fence.

Nothing inside the fence exceeds 72 columns, so it survives a phone and a quoted reply. Findings are ordered by impact, most severe first — the severity column carries the ranking, so the top of the block is the part worth reading.

### Finding kinds

Every value `--kind` accepts. A kind is what a finding *claims*; the words in brackets are how the
report prints it.

| Kind | Printed as | Claims |
|---|---|---|
| `RetryMasked` | masked by retry | The test failed and passed on retry, never reaching the run's outcome |
| `RetryDeepening` | deeper retries | The test used to pass on fewer attempts than it now needs |
| `RetryExhausted` | out of retries | The retries ran out and the test still failed the run |
| `Flaky` | flaky | The test both passes and fails, or fails in varying ways |
| `AlwaysFailing` | always failing | The test fails almost always, in one dominant way |
| `TimingOut` | timing out | The test is mostly killed for overrunning its timeout rather than failing |
| `BrokenFixture` | broken fixture | Several tests fail alike because one shared lifecycle member is broken, and that member is named |
| `SharedFailure` | shared failure | Several tests fail with one signature in one run — one cause, not many |
| `DurationRegression` | slower | The test's recent runs are slower than its earlier ones by more than the variation it already had |
| `DurationUnstable` | unstable timing | The test's duration varies too much for anyone to predict what it will cost |
| `ParallelSensitive` | concurrency | The test's failure rate moves with how many tests ran alongside it |
| `TimeSensitive` | time sensitive | The test's failures cluster at one local time of day, day group, or UTC offset |
| `Vanished` | stopped running | The test appeared throughout the baseline and has stopped running |

The three retry kinds are one judgement about one mechanism, and a test gets at most one of them:
`RetryExhausted` first, then `RetryDeepening`, then `RetryMasked` — red beats worsening beats
standing. Each is decided on the attempts an adapter actually recorded. A run counts as out of
retries when its last recorded attempt failed and an earlier attempt exists — never by comparing an
attempt number against the configured limit. That limit is published beside the finding as
`maxRetriesAsDeclared` and nothing is decided by it: retry attributes disagree about whether the
number counts total attempts or retries after the first, so the report records it the way the
attribute spelled it rather than guessing. `RetryDeepening` compares the recent runs against the
earlier ones and needs five earlier passing runs to compare against, so it stays quiet on a short
history. A test can still be reported as both out of retries and flaky: those are different claims
about the same red run, and they carry different ids.

`TimeSensitive` reads three axes: the local six-hour quarter of the day, weekend against weekday,
and — when the window contains two UTC offsets for one time zone, which is what a daylight-saving
change looks like — one side of the change against the other. Each side is counted in runs rather
than attempts, since every attempt of a run is read on the same clock. The failing side must span at
least three separate local days, so a bad afternoon is not reported as an afternoon pattern.

Trying several splits and keeping the best of them is a search, and the report is charged for it.
Every split with runs enough on both sides is tested exactly, and its result is multiplied by the
number of **distinct** ways the window was divided — two quarters that separate the same runs are
one division, not two. The split with the least probable result wins, and it is reported only if it
survives that multiplication. The finding publishes the probability it survived on and the number of
divisions charged for, so how wide a search found it is visible rather than implied. The practical
effect, on an even split of a fortnight of runs with a clean other side: five failures of six are
reported and four are not. How much it takes is not one number — it falls as the clean side grows
and rises with the width of the search — so the finding publishes both figures rather than asking
you to remember a rule.

`ParallelSensitive` reads the concurrency an execution ran at as an ordered dose and asks whether the
test's failures track it, across **every** level it was observed at. There is no split point, which is
what makes the finding reachable on the commonest .NET configuration: a suite pinned at a fixed
`maxParallelThreads` puts almost every execution on one level, and dividing that distribution in two
leaves one half empty. The report needs only that the concurrency varied at all.

Concurrency genuinely differs between attempts within a run, so every attempt is a real reading and
the per-level rates are over executions. What the probability behind the finding is computed over is
runs: a run's attempts are correlated, and a heavily retried afternoon would otherwise buy
significance with repetition. The finding publishes both denominators at every level, so a rate over
twelve executions can be read against how many separate occasions supplied them.

Two figures are published and neither implies the other. The probability says whether the failures
track concurrency more than they would have anyway; **tau**, a rank correlation, says how strongly.
Both must clear their bar. Tau is discounted by how tied the exposure is, so it is not comparable
between suites with different parallelism settings — the level table and the observed range are
published beside it so the dose-response can be read directly rather than inferred from one number.

The direction is two-sided: a test that fails more when it runs *nearly alone* is as real a defect as
one that fails under contention, and each is reported against the end of the range holding its
failures. What the report cannot do is separate concurrency from duration — a slow test overlaps more
neighbours by construction — which is in [known limitations](../known-limitations.md).

`BrokenFixture` and `SharedFailure` describe the same measurement and differ only in what can be said
about its cause. A cluster is reported as a broken fixture when **every** failure in it was recorded
in the same lifecycle member — a `[SetUp]`, a `[TestInitialize]`, a class fixture constructor — and
stays a shared failure otherwise. Which failures an adapter can place, and which it cannot, is in
[known limitations](../known-limitations.md).

### Finding ids

The `f_…` on each finding is a short, stable identity for that finding — a hash of what the
finding claims (`flaky`, `slower`, `stopped running`, …) and the test or group it claims it
about. Nothing else goes into it.

It is stable **across runs**. Run `dotnet test` again, run `xping report` again, and a
finding that is still there keeps the id it had — even though the run count moved and the
numbers behind it changed. That is what makes it useful: paste a report into a pull request
today and another next week, and the ids say which findings are the same ones and which are
new.

The id names the *claim*, not the measurement. Two reports can quote different failure rates
under one id; the rate moved as runs accumulated, and it is the same finding observed again.
For the numbers themselves, read the `headline` and `metrics` — not the id.

Two findings about the same test carry different ids when they are different claims: a test
that is both flaky and masked by a retry produces two findings and two ids.

Copy the block, or pipe it:

```bash
xping report | pbcopy          # macOS
xping report | clip            # Windows
xping report | wl-copy         # Linux (Wayland)
```

When stdout is not a terminal the report drops everything that is not the report: no colour, no Unicode glyphs, no scope notice, no cloud invitation, and no blank lines around the block.


### Source locations

The last segment of a finding's trailer is where the test is declared:

```
      evidence moderate | f_2a91c0de | tests/Billing/SummaryTests.cs:88
```

It is the file and the line the test's body starts on, made relative to the repository root, and it
is printed whenever the SDK captured one rather than being reserved for a verbose mode — knowing a
test is flaky is only half of what you need to fix it. Whether that line is the opening brace or the
first statement depends on how the assembly was built; see
[known limitations](../known-limitations.md).

None of NUnit, MSTest or xUnit reports this, so the SDK reads it from the test assembly's Portable
PDB. That means a build with `DebugType=none` has no location to report, and the trailer ends at
the finding id instead. A long path is shortened from the left at a directory boundary
(`.../Billing/SummaryTests.cs:88`) so the rest of the trailer stays readable inside the fence.

A group finding — one covering several tests at once, like a broken fixture — names no single file,
because its members are declared in different places. The members and their own locations are in
the JSON output.

See [known limitations](../known-limitations.md) for what the line number can and cannot tell you.

### `--summary`

One line, for a chat message, a commit trailer or a CI step title:

```bash
$ xping report --summary
Xping: 3 findings (1 high, 2 medium) in 20 runs of Checkout.Tests
```

### Scoping

Every test project in a solution shares one store. Without `--assembly`, the report covers one assembly and says so when others exist:

```
Reporting on Checkout.Tests · 2 other assemblies in this store (use --assembly to switch).
```

Analysing them together would pool unrelated suites: one assembly's history would contain another's tests, and its run count would be the solution-wide total.

**A run can belong to more than one assembly.** A *run* is one test host process, and `dotnet test` over a solution puts several test projects in a single host. One run of that kind is one run of **each** assembly it covered, and `--assembly` narrows it to that assembly's tests rather than choosing between runs. So `Checkout.Tests` and `Billing.Tests` can each report 20 runs from the same 20 solution-wide invocations, each seeing only its own tests.

Runs recorded before the SDK could name the assembly belong to no suite. They stay in the store and `xping where` still counts them, but no `--assembly` value reaches them.

### `--format json`

For scripts and agents. Emits a versioned envelope and nothing else — no rendered block to strip.

```bash
xping report --all --format json > findings.json
```

Every finding carries a `headline` — the same sentence the rendered report prints — plus `metrics`, the labelled pairs behind it, and the raw `evidence` the two were resolved from:

```json
{
  "schemaVersion": "1.8",
  "window": { "sessionCount": 20, "resolution": "default", "currentSliceSize": 3 },
  "context": { "sha": "a3f9c2e", "branch": "main", "assembly": "Checkout.Tests" },
  "summary": {
    "tests": 412,
    "findings": 3,
    "counts": { "high": 1, "medium": 2, "low": 0 },
    "healthy": 409,
    "excludedLowEvidence": 41
  },
  "findings": [
    {
      "id": "f_2a91c0de",
      "kind": "Flaky",
      "severity": "high",
      "evidenceLevel": "moderate",
      "subject": { "type": "test", "fullyQualifiedName": "…", "assembly": "Checkout.Tests",
                    "sourceFile": "tests/Billing/SummaryTests.cs", "sourceLineNumber": 88 },
      "headline": "failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes",
      "metrics": [
        { "label": "failed", "value": "7 of 20 executions (35%)" },
        { "label": "runs affected", "value": "5 of 20" },
        { "label": "failure modes", "value": "3" }
      ],
      "evidence": { "…": "…" },
      "drillDown": "xping report --kind Flaky --format json"
    }
  ],
  "truncated": { "shown": 3, "total": 3, "command": "xping report --all" }
}
```

---

## `xping where`

Prints where the local store lives and what it holds.

```bash
xping where [--directory <path>]
```

```
/Users/you/src/my-repo/.xping
  36 runs · 6.1 MB on disk
  Checkout.Tests                              12 runs  last 2026-08-08 09:14 UTC
  Billing.Tests                               12 runs  last 2026-08-08 09:13 UTC
  Api.Tests                                   12 runs  last 2026-08-08 09:11 UTC
```

The store location is discovered by walking up for a repository root, so this is the reliable way to answer "where is my data". It is also the first thing to run when a report looks wrong or empty.

**Exit codes:** `0` success · `1` no store could be resolved.

---

## `xping clear`

Deletes recorded runs.

```bash
xping clear [options]
```

| Option | Description |
|---|---|
| `--assembly <name>` | Only delete one test assembly's history |
| `--force` | Skip the confirmation prompt |
| `--directory <path>` | Resolve the store from this directory |

Local history is not recoverable — it is not in version control, and in local-only mode it exists nowhere else. So `clear` confirms first:

```
Delete all 36 runs from /Users/you/src/my-repo/.xping? [y/N]
```

`--assembly` is exact. A run recorded by a solution-wide `dotnet test` holds several test projects' history, and clearing one of them strips that project out and keeps the run — the other suites keep every run they had.

When stdin is not interactive (a script, a CI step, a pipeline), `clear` **refuses** rather than assuming consent:

```
Refusing to delete all 36 runs without confirmation. Re-run with --force to proceed non-interactively.
```

Pass `--force` to delete without prompting.

**Exit codes:** `0` success · `1` refused, cancelled, or some runs could not be deleted.

---

## `xping --version`

Prints the tool version, e.g. `1.0.0-rc.5`.

---

## Environment variables

| Variable | Effect |
|---|---|
| `XPING_LOCAL_STORE` | Overrides the store location. Must match the value the SDK used. |
| `XPING_NO_BANNER` | Suppresses the cloud invitation. |

See [Local Store](../configuration/local-store.md) for details.

---

## See Also

- [Running Without an Account](../getting-started/local-first.md)
- [Local Store](../configuration/local-store.md)
- [Configuration Reference](../configuration/configuration-reference.md)
