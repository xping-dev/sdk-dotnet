---
uid: getting-started-local-first
title: Run without an account
---

# Run without an account

This page shows how to record your test runs and read a reliability report with no Xping account,
no API key and no network access. It is for anyone who wants to try Xping on their own machine first.

Without an API key, the SDK runs in **local-only mode**. Everything stays on your machine.

---

## What you will learn

- How to record test history with no Xping account
- How to read the local report
- Where the local data is stored and how to delete it
- What local-only mode can and cannot tell you

---

## Prerequisites

- A test project that uses **NUnit**, **xUnit** (v2 or v3) or **MSTest** 3.8 or later
- A test project that targets .NET Framework 4.6.2 or later, or .NET 8 or later
- The .NET 10 SDK or runtime, for the CLI

No account. No API key.

---

## Step 1: Add the SDK

Install the adapter for your test framework:

```bash
dotnet add package Xping.Sdk.NUnit --prerelease   # or Xping.Sdk.XUnit (xUnit v2), Xping.Sdk.XUnit.V3 (xUnit v3), Xping.Sdk.MSTest
```

All Xping packages are prereleases, so the command needs `--prerelease`.

Set up the adapter as the quick start for your framework describes
([NUnit](quickstart-nunit.md), [xUnit](quickstart-xunit.md), [MSTest](quickstart-mstest.md)).
Skip the API key step.

With no API key, the SDK starts in local-only mode. When you run `dotnet test` in a terminal, the
SDK prints this line:

```
[Xping 17:28:04 INF] Initialized in local-only mode. SessionId: "6bec32b0-922b-4b9b-bcb9-fb4e1056180f", Environment: Default. No API key configured - results stay on this machine.
```

When the output goes to a file or a pipe instead of a terminal, for example in a CI log,
`dotnet test` shows this line only with `--logger "console;verbosity=normal"`.

---

## Step 2: Run your tests several times

```bash
dotnet test
```

The SDK writes each run to a local store. `xping report` shows no findings until the store holds at
least **5 runs**, and each test needs at least 5 runs of its own. With fewer runs, the report shows
the counts but no findings. Fewer runs cannot tell a flaky test from a test that just broke.
From the second run on, the report does show the failures of the latest run. See
[The latest run](../cli/reading-the-report.md#the-latest-run).

---

## Step 3: Install the CLI

> **The CLI needs the .NET 10 SDK or runtime.** The tool targets `net10.0` and does not start on
> older runtimes. The SDK packages target `netstandard2.0`, so your test projects do not need
> .NET 10.

```bash
dotnet tool install -g Xping.Cli --prerelease
```

This adds the `xping` command to your `PATH`, for every repository. If your shell answers
`command not found: xping`, add `~/.dotnet/tools` to your `PATH` and open a new terminal.

To pin the CLI version in your repository instead, install it into a tool manifest. See
[Installation](../cli/command-reference.md#installation) in the command reference.

---

## Step 4: Read the report

```bash
xping report
```

<!-- xping:sample docs-checkout -->
````
Xping · Checkout.Tests · 20 runs · 2026-08-05 → 2026-08-19 · main@a3f9c2e
412 tests · 409 healthy · 3 flagged · 41 awaiting more runs · 6 not significant
◷ nothing to measure: concurrency 108 · slower 32

```
NEEDS ATTENTION (1 high, 1 medium, 1 low)              most severe first
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    SummaryTests.GenerateMonthlySummary
    failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes
    evidence adequate | -env-cluster | f_2a91c0de | ...ummaryTests.cs:88

2.  MED   slower
    FlowTests.CheckoutFlow_Completes
    3.51x slower (95% CI 1.94-5.87x), 340ms -> 1.2s on the clock
    evidence robust | -env | f_8c04b71a | .../Checkout/FlowTests.cs:214

3.  LOW   stopped running
    LegacyImport.Roundtrip
    ran in 12 of 17 earlier runs, absent from the last 3
    evidence adequate | -partial | f_1d77e3f5 | .../ImportTests.cs:41
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/reading-the-report.html#the-population-marker

Detail of row 1: xping report --id f_2a91c0de --assembly Checkout.Tests
````

### How to read it

Each finding has four parts: a **severity**, a **kind**, the **test**, and the **evidence** behind it.

| Part | Meaning |
|---|---|
| `HIGH` / `MED` / `LOW` | The severity. The report lists the most severe findings first. |
| `flaky`, `slower`, `stopped running` | The *kind*: what the report observed. There are 13 kinds. The [command reference](../cli/reading-the-report.md#finding-kinds) lists them all. |
| The counts line | The measurement behind the finding, in plain numbers. |
| `evidence sparse\|adequate\|robust` | How many runs support *this finding*. A finding can count only the runs it could measure, so this is not always every run of the test. Treat a finding with `sparse` evidence as a lead, not a verdict. |
| `f_2a91c0de` | The finding id. It stays the same between reports, so you can use it in a ticket or compare two reports. |

The report sits inside a fenced code block and is at most 72 columns wide. When you paste it into
Slack, a pull request or a ticket, the columns stay aligned. The report shows the top ten findings.
Add `--all` to see the rest.

The report keeps **flaky** and **always failing** apart on purpose. A test that never passes is
not unreliable. It is broken, and it needs a different fix.

---

## What gets flagged

The report has 13 finding kinds. These are the ones you see most often:

| Printed as | `--kind` value | What the report observed |
|---|---|---|
| masked by retry | `RetryMasked` | The test failed, then passed on a retry, so the run's outcome hid the failure. |
| flaky | `Flaky` | The test both passes and fails, or fails in different ways. |
| always failing | `AlwaysFailing` | The test fails in almost every run, in one main way. This is more likely a real bug than a flaky test. |
| timing out | `TimingOut` | The framework stops the test for running past its timeout in most runs. |
| slower | `DurationRegression` | The recent runs of the test are slower than its earlier runs, by more than its usual variation. The headline gives the factor and its interval, and the evidence gives the spread on each side. See [Known limitations](../troubleshooting/known-limitations.md). |
| stopped running | `Vanished` | The test ran regularly in the earlier runs and has stopped. |

The other kinds are deeper retries (`RetryDeepening`), out of retries (`RetryExhausted`), unstable
timing (`DurationUnstable`), concurrency (`ParallelSensitive`), broken fixture (`BrokenFixture`),
shared failure (`SharedFailure`) and time sensitive (`TimeSensitive`). The
[command reference](../cli/reading-the-report.md#finding-kinds) describes each one.

Every kind needs the 5-run minimum. Before that, the SDK still tells you about a retry in the run
that just finished. Outside CI, it writes this line to your terminal when the run ends:

```
[Xping] 1 test flaked on retry this run - run `xping report` for local history
```

To show only some kinds, use `--kind`. With a store that holds one test assembly:

```bash
xping report --kind Flaky --kind AlwaysFailing
```

The report ignores skipped tests and tests that did not run. They say nothing about reliability.

---

## Multiple test projects

All test projects in a repository share one store. When the store holds more than one assembly,
`xping report` lists them, one row each, most severe first:

```
Xping · 3 assemblies

Checkout.Tests                               4 findings (1 high, 3 medium)
  412 tests · 4 flagged · 20 runs · last 2026-11-08 16:45
Billing.Tests                                                  no findings
  96 tests · 0 flagged · 20 runs · last 2026-11-08 16:44
Search.Tests                                                   no findings
  57 tests · 0 flagged · 6 runs · last 2026-11-02 09:12

Details: xping report --assembly Checkout.Tests
```

To see the findings of one assembly:

```bash
xping report --assembly Billing.Tests
```

`--kind`, `--top` and `--all` select findings from one assembly, so they need `--assembly` when the
store holds several. Without it, the CLI writes `--kind needs --assembly` to stderr and exits with
code 2.

`--fail-on` checks every assembly. A CI step fails when any assembly has a finding at the
threshold.

The CLI analyses each assembly against **its own** run history. An assembly with 3 runs is never
compared with the 12-run window of another assembly.

---

## Where your data lives

```bash
xping where
```

```
/Users/you/src/my-repo/.xping
  36 runs · 1.2 MB on disk
  Checkout.Tests                              12 runs  last 2026-08-08 09:14 UTC
  Billing.Tests                               12 runs  last 2026-08-08 09:13 UTC
  Api.Tests                                   12 runs  last 2026-08-08 09:11 UTC
```

The store is the `.xping` folder at the root of your repository. It **ignores itself**: it contains
a `.gitignore` that hides the whole folder. Nothing appears in `git status`, and the SDK does not
change your own `.gitignore`.

The store keeps at most 50 runs of each test assembly, runs up to 30 days old, and 100 MB in
total. It deletes older runs automatically. These limits are fixed. To move the store, see
[Local store](../configuration/local-store.md).

To delete history:

```bash
xping clear                              # asks before it deletes
xping clear --assembly Billing.Tests     # only one assembly
```

---

## What local-only mode cannot tell you

The local history holds only **your** runs, on **your** machine. It cannot include:

- CI runs
- the runs of your teammates
- differences between operating systems or environments
- more history than the store keeps

This is why the local report shows no confidence score. The
[confidence score](../cloud/confidence-scores.md) of Xping Cloud uses data that one machine does not
have, such as how a test behaves in different environments. A local number that looked like the
Xping Cloud score but did not match it would mislead you.

Local-only mode answers the question *"what is unstable on my machine, in my last runs?"*. Xping
Cloud answers *"what is unstable across CI, every branch and every machine, over months?"*.

---

## Connect later

You lose nothing by starting locally. Add an API key when you want to. It is the only variable you
need, and your test code stays the same:

```bash
export XPING_APIKEY="your-key"
```

Create the key in Xping Cloud under **Settings → API keys**, with only the Upload scope.

You do not name a project. Xping Cloud creates one project for each test assembly, on the first
upload. `XPING_PROJECTID` is optional. It sends several assemblies to one project. See
[ProjectId](../configuration/configuration-reference.md#projectid).

Keep the key out of your repository. Set it in your shell or in a secret store, never in
`appsettings.json`. That file is committed and copied into the build output.

With the key, the SDK switches to Cloud mode and uploads each run. It **keeps writing the local
store**, so `xping report` still works offline on your own machine.

To see the Xping Cloud data in the same report, sign in once on your machine:

```bash
xping login
```

`xping report` then adds the Xping Cloud confidence score to the rows it prints, with a label so you
cannot mistake it for a local number. Under `FLAGGED IN CLOUD`, it lists the tests that Xping Cloud
calls flaky, broken or unstable but that passed here. Neither changes the local counts or the exit
code. When you are offline, it prints the local report. See
[Cloud data](../cli/cloud-data.md).

To stop the SDK from uploading when credentials are present, set the mode:

```bash
export XPING_MODE=LocalOnly
```

`XPING_MODE` is an SDK setting. The CLI does not read it. Without a sign-in, `xping report` tries
the `XPING_APIKEY` in your shell. A key with only the Upload scope cannot read Xping Cloud data, so
the report stays local. In a terminal, the report says so in one line. After `xping login`, the
report shows the Xping Cloud data instead.

---

## See also

- [Command reference](../cli/command-reference.md): every command and option
- [Local store](../configuration/local-store.md): the location and the limits
- [Configuration reference](../configuration/configuration-reference.md): every SDK setting
- [Finding flaky tests](../flaky-tests/finding-flaky-tests.md)
