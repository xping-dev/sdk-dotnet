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

Reports test reliability findings from recent local runs.

```bash
dotnet xping report [options]
```

| Option | Default | Description |
|---|---|---|
| `--runs <n>` | 20 runs / 14 days | Recent runs to analyse. Alias: `--last` |
| `--since <sha\|date>` | — | Analyse from a commit or date instead. Excludes `--runs` |
| `--top <n>` | `10` | Findings to show. Excludes `--all` |
| `--all` | off | Show **every finding** rather than the top ones |
| `--kind <Kind>...` | all | Restrict to one or more finding kinds |
| `--assembly <name>` | newest | Restrict the report to one test assembly |
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
      evidence moderate | f_2a91

MED   slower           CheckoutFlow_Completes
      p50 340ms -> 1.2s (+264.7%), normalised +251.2%
      evidence high | f_8c04

LOW   stopped running  LegacyImport.Roundtrip
      ran in 12 of 17 earlier runs, absent from the last 3
      evidence moderate | f_1d77
```
````

Only the top ten findings are shown by default. When some are withheld, one line follows the fence — `Showing 10 of 21 · all: xping report --all` — and a report showing everything ends at the fence.

Nothing inside the fence exceeds 72 columns, so it survives a phone and a quoted reply. Findings are ordered by impact, most severe first — the severity column carries the ranking, so the top of the block is the part worth reading.

Copy the block, or pipe it:

```bash
dotnet xping report | pbcopy          # macOS
dotnet xping report | clip            # Windows
dotnet xping report | wl-copy         # Linux (Wayland)
```

When stdout is not a terminal the report drops everything that is not the report: no colour, no Unicode glyphs, no scope notice, no cloud invitation, and no blank lines around the block.

### `--summary`

One line, for a chat message, a commit trailer or a CI step title:

```bash
$ dotnet xping report --summary
Xping: 3 findings (1 high, 2 medium) in 20 runs of Checkout.Tests
```

### Scoping

Every test project in a solution shares one store. Without `--assembly`, the report covers the assembly that ran most recently and says so when others exist:

```
Reporting on Checkout.Tests · 2 other assemblies in this store (use --assembly to switch).
```

Analysing them together would pool unrelated suites: one assembly's history would contain another's tests, and its run count would be the solution-wide total.

### `--format json`

For scripts and agents. Emits a versioned envelope and nothing else — no rendered block to strip.

```bash
dotnet xping report --all --format json > findings.json
```

Every finding carries a `headline` — the same sentence the rendered report prints — plus `metrics`, the labelled pairs behind it, and the raw `evidence` the two were resolved from:

```json
{
  "schemaVersion": "1.1",
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
      "id": "f_2a91",
      "kind": "Flaky",
      "severity": "high",
      "evidenceLevel": "moderate",
      "subject": { "type": "test", "fullyQualifiedName": "…", "assembly": "Checkout.Tests" },
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
dotnet xping where [--directory <path>]
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
