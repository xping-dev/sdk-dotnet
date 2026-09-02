---
uid: getting-started-local-first
title: Running Without an Account
---

# Running Without an Account

Xping works with no API key, no signup, and no network access. Add the SDK to your test project, run your tests a few times, and `xping report` will tell you which of your tests are unreliable.

This is **local-only mode**. Everything stays on your machine.

---

## What You'll Learn

- How to collect test history with no Xping account
- How to read the local flakiness report
- Where local data is stored and how to remove it
- What local-only mode can and cannot tell you

---

## Prerequisites

- An existing test project using **NUnit**, **xUnit**, or **MSTest**
- The .NET SDK

No account. No API key.

---

## Step 1: Add the SDK

Install the adapter for your test framework:

```bash
dotnet add package Xping.Sdk.NUnit     # or Xping.Sdk.XUnit / Xping.Sdk.MSTest
```

Wire it up exactly as the framework quickstart describes ([NUnit](quickstart-nunit.md), [xUnit](quickstart-xunit.md), [MSTest](quickstart-mstest.md)) — but skip the API key step.

With no `ApiKey` configured, the SDK starts in local-only mode:

```
[Xping] Initialized in local-only mode. No API key configured - results stay on this machine.
```

---

## Step 2: Run your tests a few times

```bash
dotnet test
```

Each run is recorded to a local store. Cross-run flakiness detection needs at least **three runs**, because two runs cannot distinguish a flaky test from a test that just broke.

---

## Step 3: Install the CLI

> **Requires the .NET 10 SDK or runtime.** The tool targets `net10.0` and will not launch on
> older runtimes. The Xping SDK packages themselves target `netstandard2.0` and are unaffected —
> your test projects can stay on any supported .NET version.

```bash
dotnet tool install -g Xping.Cli
```

This puts an `xping` command on your `PATH`, usable from any repository. If your shell answers `command not found: xping`, add `~/.dotnet/tools` to your `PATH` and reopen the terminal.

To pin the CLI version alongside your repo instead, install it into a tool manifest — see [Installation](../cli/command-reference.md#installation) in the CLI reference.

---

## Step 4: Read the report

```bash
xping report
```

````
Xping · Checkout.Tests · 20 runs · 2026-08-05 → 2026-08-19 · main@a3f9c2e
3 findings (1 high, 2 medium) · 412 tests · 409 healthy

```
HIGH  flaky            GenerateMonthlySummary
      failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes
      evidence moderate | f_2a91c0de | tests/Billing/SummaryTests.cs:88

MED   slower           CheckoutFlow_Completes
      p50 +251.2% normalised (+854ms), 340ms -> 1.2s on the clock
      evidence high | f_8c04b71a | tests/Checkout/FlowTests.cs:214

LOW   stopped running  LegacyImport.Roundtrip
      ran in 12 of 17 earlier runs, absent from the last 3
      evidence moderate | f_1d77e3f5 | tests/Legacy/ImportTests.cs:41
```
````

### How to read it

Each finding is four things: a **severity**, a **kind**, the **test**, and the **evidence** behind
the claim.

| Part | Means |
|---|---|
| `HIGH` / `MED` / `LOW` | Impact ranking. Findings are ordered most severe first, so the top of the block is the part worth reading. |
| `flaky`, `slower`, `stopped running` | The *kind* — what the finding claims. There are sixteen; the [CLI reference](../cli/command-reference.md#finding-kinds) lists them all. |
| The counts line | The measurement the claim rests on, in plain numbers. |
| `evidence low\|moderate\|high` | How much history stands behind it. A `low`-evidence finding is a lead, not a verdict. |
| `f_2a91c0de` | A stable id for that finding, so you can refer to it in a ticket or diff two reports. |

The report body sits inside a fenced code block and stays under 72 columns, so pasting it into
Slack, a PR, or a ticket keeps its columns. Only the top ten findings show by default —
`--all` shows the rest.

Separating **flaky** from **always failing** is deliberate. A test that never passes is not
unreliable, it is broken, and the fix is completely different.

---

## What gets flagged

The report distinguishes sixteen finding kinds. The ones you meet first:

| Printed as | `--kind` value | Claims |
|---|---|---|
| masked by retry | `RetryMasked` | The test failed and passed on retry, never reaching the run's outcome. The strongest flakiness signal, and the only one available from a single run. |
| flaky | `Flaky` | The test both passes and fails, or fails in varying ways. |
| always failing | `AlwaysFailing` | The test fails almost always, in one dominant way — a likely real bug, not flake. |
| timing out | `TimingOut` | The test is mostly killed for overrunning its timeout rather than failing. |
| slower | `DurationRegression` | The test's median duration has regressed against its own baseline. |
| stopped running | `Vanished` | The test appeared throughout the baseline and has stopped running. |

The rest — order dependence, concurrency sensitivity, broken fixtures, shared failures, time-of-day
and network clustering — are in the [CLI reference](../cli/command-reference.md#finding-kinds).
Restrict the report to one or more with `--kind`:

```bash
xping report --kind Flaky --kind AlwaysFailing
```

Skipped and not-executed tests are ignored; they carry no reliability signal.

---

## Multiple test projects

Every test project in a solution shares one store. By default the report covers the assembly that ran most recently and tells you when others exist:

```
Reporting on Checkout.Tests · 2 other assemblies in this store (use --assembly to switch).
```

To see one suite, or all of them:

```bash
xping report --assembly Billing.Tests
xping report --all
```

`--all` analyses each assembly against **its own** run history and merges the findings, so a suite that has run 3 times is never described against another suite's 12-run window.

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
```

The store lives in a `.xping` folder at your repository root and **ignores itself** — it contains a `.gitignore` that hides the whole directory, so nothing appears in `git status` and your own `.gitignore` is never modified.

It keeps the last 50 runs and prunes automatically. See [Local Store](../configuration/local-store.md) for retention settings and how to relocate it.

To delete history:

```bash
xping clear                              # prompts before deleting
xping clear --assembly Billing.Tests     # only one suite
```

---

## What local-only mode cannot tell you

Local history contains only **your** runs, on **your** machine. It structurally cannot include:

- CI runs
- Your teammates' runs
- Behaviour differences across operating systems or environments
- More history than your retention window holds

This is why the local report does not show a confidence score. The [confidence score](../guides/getting-started/understanding-confidence-scores.md) weighs six factors, several of which — environment consistency in particular — need data that does not exist on one developer's machine. Showing a number here that looked like Xping Cloud's but disagreed with it would be worse than showing none.

Local-only mode answers *"what is unstable on my machine, in my last N runs"*. Xping Cloud answers *"what is unstable across CI, every branch, and everyone's machines, over months"*.

---

## Connecting later

Nothing is lost by starting local. Add an API key whenever you want — that is the only variable
you need, and no test code changes:

```bash
export XPING_APIKEY="your-key"
```

You do not name a project. Xping derives one per test assembly, created on first upload.
`XPING_PROJECTID` is optional and only pins several assemblies into a single project — see
[ProjectId](../configuration/configuration-reference.md#projectid).

Keep the key out of your repository: export it from your shell or a secret store, never
`appsettings.json`, which is committed and copied into build output.

The SDK switches to Cloud mode and uploads as normal. It **keeps writing the local store**, so `xping report` continues to work offline and on your own machine.

To stay local even with credentials present, set the mode explicitly:

```bash
export XPING_MODE=LocalOnly
```

---

## See Also

- [Xping CLI Reference](../cli/command-reference.md) — every command and option
- [Local Store](../configuration/local-store.md) — location, retention, and settings
- [Configuration Reference](../configuration/configuration-reference.md) — all SDK settings
- [Identifying Flaky Tests](../guides/working-with-tests/identifying-flaky-tests.md)
