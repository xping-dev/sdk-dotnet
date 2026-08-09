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

### As a local tool (recommended)

```bash
dotnet new tool-manifest        # if your repo has no manifest yet
dotnet tool install Xping.Cli
```

Invoke it as `dotnet xping`. The version is pinned in `.config/dotnet-tools.json`, so everyone on the team gets identical output. Teammates run `dotnet tool restore` once.

### As a global tool

```bash
dotnet tool install -g Xping.Cli
```

Invoke it as `xping`. Note that `dotnet xping` only works for tools installed into a manifest.

---

## `xping report`

Reports flakiness from recent local runs.

```bash
dotnet xping report [options]
```

| Option | Default | Description |
|---|---|---|
| `--last <n>` | `12` | Recent runs to analyse **per assembly** |
| `--assembly <name>` | newest | Restrict the report to one test assembly |
| `--all` | off | Report across every assembly in the store |
| `--directory <path>` | working directory | Resolve the store starting from this directory |
| `--details` | off | Print per-test run history |
| `--json` | off | Emit JSON instead of a rendered report |
| `--ascii` | auto | Force ASCII output |

`--all` and `--assembly` are mutually exclusive.

**Exit codes:** `0` success · `1` no store or no runs · `2` invalid arguments.

Note that the exit code reports whether the **command** succeeded, not whether tests are flaky. A clean report and a report full of findings both exit `0`.

### Scoping

Every test project in a solution shares one store. Without `--all` or `--assembly`, the report covers the assembly that ran most recently and says so when others exist:

```
Reporting on Checkout.Tests · 2 other assemblies in this store (use --assembly to switch).
```

### `--all`

Each assembly is analysed against **its own** runs, and the findings are merged. A suite with 3 runs is never described against another suite's 12-run window.

Because there is no single "latest run" across assemblies, the aggregate report omits the pass/fail counts of the scoped report and labels each finding with its assembly:

```
──────────────────────────────────────────────────────────────────────────
  Xping · local summary                            3 assemblies · 36 runs
──────────────────────────────────────────────────────────────────────────

  ⚠  2 unstable tests across 3 assemblies

     ●●○●●●○●●●●○   Checkout.AppliesDiscount                        9/12
                    Checkout.Tests · passed 9 of 12 runs · inconsistent
──────────────────────────────────────────────────────────────────────────
```

### `--details`

Per-test history, including the full fingerprint you can use to correlate with the dashboard:

```
Details

  Checkout.AppliesDiscount_WhenCouponValid
    fingerprint  90a41028c37a83516e2f22f6de78ae42225cc7cde60353b747902bdd645460e4
    passed       9 of 12 runs
      2026-08-08 09:02  pass  120ms  main
      2026-08-08 09:07  FAIL  2400ms  main
      2026-08-08 09:14  pass  118ms  feat/coupons
```

### `--json`

For scripts and CI. Emits a versioned document and nothing else — no rendered block to strip.

```bash
dotnet xping report --all --json > flakiness.json
```

```json
{
  "schemaVersion": 1,
  "storePath": "/Users/you/src/my-repo/.xping",
  "runsAnalysed": 36,
  "assembliesAnalysed": 3,
  "hasSufficientHistory": true,
  "minimumRunsForHistory": 3,
  "generatedAtUtc": "2026-08-08T12:35:40.6663810Z",
  "runs": [
    {
      "sessionId": "08ffa28a39a34abfa91b781e4a44d788",
      "startedAtUtc": "2026-08-08T12:34:22.2985860Z",
      "durationMs": 38214,
      "assembly": "Checkout.Tests",
      "environment": "Local",
      "branch": "feat/coupons",
      "commitSha": "9d3f1a2...",
      "isCi": false,
      "testCount": 412
    }
  ],
  "unstableTests": [
    {
      "fingerprint": "90a41028c37a8351...",
      "name": "Checkout.AppliesDiscount_WhenCouponValid",
      "assembly": "Checkout.Tests",
      "kind": "FlakyAcrossRuns",
      "passCount": 9,
      "runCount": 12,
      "history": [true, true, false, true, true, true],
      "passedOnAttempt": null
    }
  ],
  "consistentFailures": []
}
```

`kind` is one of `FlakedInRun`, `FlakyAcrossRuns`, `NewlyFailing`, `ConsistentlyFailing`.

`history` is ordered oldest first; `true` is a pass. Per-run detail is included deliberately so a consumer can compute its own view without re-reading the store.

**Check `schemaVersion` before parsing.** Fields will be added over time; existing fields will not be renamed or repurposed.

---

## `xping where`

Prints where the local store lives and what it holds.

```bash
dotnet xping where [--directory <path>]
```

```
/Users/you/src/my-repo/.xping
  36 runs · 1.2 MB on disk
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
dotnet xping clear [options]
```

| Option | Description |
|---|---|
| `--assembly <name>` | Only delete runs for one test assembly |
| `--force` | Skip the confirmation prompt |
| `--directory <path>` | Resolve the store from this directory |

Local history is not recoverable — it is not in version control, and in local-only mode it exists nowhere else. So `clear` confirms first:

```
Delete all 36 runs from /Users/you/src/my-repo/.xping? [y/N]
```

When stdin is not interactive (a script, a CI step, a pipeline), `clear` **refuses** rather than assuming consent:

```
Refusing to delete all 36 runs without confirmation. Re-run with --force to proceed non-interactively.
```

Pass `--force` to delete without prompting.

**Exit codes:** `0` success · `1` refused, cancelled, or some runs could not be deleted.

---

## `xping version`

Prints the tool version.

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
