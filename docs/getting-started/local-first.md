---
uid: getting-started-local-first
title: Running Without an Account
---

# Running Without an Account

Xping works with no API key, no signup, and no network access. Add the SDK to your test project, run your tests a few times, and `dotnet xping report` will tell you which of your tests are unreliable.

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
dotnet new tool-manifest        # if your repo has no manifest yet
dotnet tool install Xping.Cli
```

This makes `dotnet xping` available in your repository, pinned to a version your whole team shares. To install it for your user account instead, use `dotnet tool install -g Xping.Cli` and invoke it as `xping`.

---

## Step 4: Read the report

```bash
dotnet xping report
```

```
──────────────────────────────────────────────────────────────────────────
  Xping · local run summary                             412 tests · 38.2s
──────────────────────────────────────────────────────────────────────────
  ✓ 405 passed     ✗ 4 failed     ○ 3 skipped

  ⚠  2 unstable tests · last 12 local runs

     ●●○●●●○●●●●○   Checkout.AppliesDiscount_WhenCouponValid         9/12
                    passed 9 of 12 runs · inconsistent

     ●●●●●●●●●●●○   Db.MigratesSchema_OnStartup                     11/12
                    newly failing · first failure in this window

  ✗  1 test failed in all 12 runs - not flaky, likely real bugs
     Auth.RejectsExpiredToken
──────────────────────────────────────────────────────────────────────────
```

### How to read it

The sparkline runs **left to right, oldest to newest**. `●` is a pass, `○` a failure. The pattern is the point:

| Pattern | What Xping calls it |
|---|---|
| `●●○●●●○●●●●○` | **Flaky across runs** — passes and fails without a clear cause |
| `●●●●●●●●●●●○` | **Newly failing** — passed every earlier run, failed the most recent |
| `●●●●●●●●○○○○` | **Flaky across runs** — broke recently and stayed broken, but it has both passed and failed in the window |
| `○○○○○○○○○○○○` | **Consistently failing** — never passed; listed separately as a likely real bug |

Separating "flaky" from "consistently failing" is deliberate. A test that never passes is not unreliable, it is broken, and the fix is completely different.

---

## What gets flagged

Four signals, in order of how strongly they imply flakiness:

1. **Flaked on retry** — failed and then passed within a single run. The strongest signal, and the only one available on your very first run.
2. **Flaky across runs** — both passed and failed within the analysed window.
3. **Newly failing** — passed in every earlier run, failed in the most recent.
4. **Consistently failing** — never passed. Reported separately as a likely real bug.

Skipped and not-executed tests are ignored; they carry no reliability signal.

---

## Multiple test projects

Every test project in a solution shares one store. By default the report covers the assembly that ran most recently and tells you when others exist:

```
Reporting on Checkout.Tests · 2 other assemblies in this store (use --assembly to switch).
```

To see one suite, or all of them:

```bash
dotnet xping report --assembly Billing.Tests
dotnet xping report --all
```

`--all` analyses each assembly against **its own** run history and merges the findings, so a suite that has run 3 times is never described against another suite's 12-run window.

---

## Where your data lives

```bash
dotnet xping where
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
dotnet xping clear                              # prompts before deleting
dotnet xping clear --assembly Billing.Tests     # only one suite
```

---

## What local-only mode cannot tell you

Local history contains only **your** runs, on **your** machine. It structurally cannot include:

- CI runs
- Your teammates' runs
- Behaviour differences across operating systems or environments
- More history than your retention window holds

This is why the local report does not show a confidence score. The [confidence score](../guides/getting-started/understanding-confidence-scores.md) weighs six factors, several of which — environment consistency in particular — need data that does not exist on one developer's machine. Showing a number here that looked like the dashboard's but disagreed with it would be worse than showing none.

Local-only mode answers *"what is unstable on my machine, in my last N runs"*. The dashboard answers *"what is unstable across CI, every branch, and everyone's machines, over months"*.

---

## Connecting later

Nothing is lost by starting local. Add an API key whenever you want:

```bash
export XPING_APIKEY="your-key"
export XPING_PROJECTID="your-project"
```

The SDK switches to connected mode and uploads as normal. It **keeps writing the local store**, so `dotnet xping report` continues to work offline and on your own machine.

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
