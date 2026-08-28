---
uid: index
title: Xping for .NET
---

# Xping for .NET

**Your coding agent can't tell a flaky test from a broken one.**

Xping records every `dotnet test` run — local ones included — so "is this test flaky?" gets
answered from history instead of from one red run.

> Recording and local reports are **free**, need **no account**, and make **no network calls**.

---

## Why this exists

`dotnet test` is amnesiac. Every run starts from zero and discards everything the last one knew.
A test that failed on Tuesday and passed on Wednesday leaves no trace anyone can point at on
Thursday, so "is that one flaky?" gets answered from memory and settled by re-running until it
goes green.

That was already expensive when a human was doing it. Now an agent is. Hand an agent a failing
test and a single run to learn from, and it will make the test pass. With one data point, a real
regression and a flake look identical — so it fixes what it can see:

| It does this | And you get |
| ------------ | ----------- |
| Loosens the assertion | A test that still runs, but stopped checking the thing that broke |
| Adds a retry | A real, reproducible failure that now passes on attempt three |
| Bumps the timeout | The same race condition, just harder to hit in CI |
| Moves the test into its own `[Collection]` | A green suite that now runs those tests serially — the shared-state bug is untouched, and every build is slower |
| Mocks the dependency that failed | A test that passes deterministically because it no longer touches the code that broke |

Every one of those is a reasonable move on one data point. Nobody notices until the suite stops
meaning anything.

Xping is the accumulation layer underneath. Nothing about your test code changes.

---

## What is Xping?

**Xping SDK** — a lightweight library that hooks your test framework (NUnit, xUnit, MSTest) and
records each execution: outcome, duration, environment. Under 5 ms per test. Targets
`netstandard2.0`, so it runs on any supported .NET version.

**Xping CLI** — the `xping` global tool. Reads the recorded history back and tells you which
tests behaved consistently and which didn't — to you in the terminal, and to your agent as
structured JSON. Targets `net10.0`.

**Xping Cloud** — the hosted platform at [app.xping.io](https://app.xping.io). Pools history
across every machine, branch, and CI job, then scores it: confidence per test, evidence
sufficiency, root-cause categorisation, trends, and PR comments on GitHub. Currently invite-only.

The SDK and CLI are two halves of one thing — the SDK alone writes history nothing reads.
Cloud is optional and additive.

---

## Local vs. Cloud

|                | **Local** | **Cloud** |
| -------------- | --------- | --------- |
| **Setup**      | SDK package + CLI tool | + an upload key |
| **Account**    | Not required | Required (invite-only) |
| **Your data**  | Stays in `.xping/` in your repo | Uploaded to Xping Cloud |
| **History**    | Your machine, your runs | Every machine, every branch, CI included |
| **You get**    | Observations and evidence from local runs | Confidence scores, evidence sufficiency, trends, PR comments |

The local CLI reports **what it observed**. It does not assign confidence scores or name causes —
a handful of runs on one machine isn't an evidence base strong enough to support that kind of
claim, and we'd rather show you the runs than a number that implies more certainty than the data
has. Scoring lives in Xping Cloud, where there's enough history across machines, branches, and CI
to justify it.

|                                        | Local | Cloud |
| -------------------------------------- | :---: | :---: |
| Run-by-run pass/fail history            | ✓ | ✓ |
| Inconsistent and newly-failing tests    | ✓ | ✓ |
| Consistently-failing tests (real bugs)  | ✓ | ✓ |
| Duration and environment capture        | ✓ | ✓ |
| JSON envelope for agents and scripts    | ✓ | ✓ |
| Per-test confidence score               | — | ✓ |
| Evidence sufficiency (ESS)              | — | ✓ |
| Root-cause categorisation               | — | ✓ |
| Reliability trends over time            | — | ✓ |
| Cross-environment comparison            | — | ✓ |
| History across CI, branches, teammates  | — | ✓ |
| GitHub PR comments                      | — | ✓ |

**Start local.** Nothing leaves your machine, and you can add an upload key later without
changing a line of test code.

---

## Quick Start

### 1. Install both pieces

```bash
dotnet add package Xping.Sdk.XUnit    # or Xping.Sdk.NUnit / Xping.Sdk.MSTest

dotnet tool install -g Xping.Cli      # puts `xping` on your PATH
```

> The CLI needs the .NET 10 SDK or runtime. The SDK packages target `netstandard2.0`, so your
> test projects can stay on any supported .NET version.

### 2. Turn on tracking

# [xUnit](#tab/xunit)

Add one line to `AssemblyInfo.cs`:

```csharp
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

[xUnit Quick Start →](getting-started/quickstart-xunit.md)

# [NUnit](#tab/nunit)

Add one assembly-level attribute:

```csharp
[assembly: XpingTrack]
```

[NUnit Quick Start →](getting-started/quickstart-nunit.md)

# [MSTest](#tab/mstest)

Inherit from the base class:

```csharp
[TestClass]
public class MyTests : XpingTestBase { }
```

[MSTest Quick Start →](getting-started/quickstart-mstest.md)

***

### 3. Run your tests — more than once

```bash
dotnet test
```

One run tells you nothing about reliability. A dozen tells you plenty. Run them the way you
normally would — including the local runs that never reach CI, which is where most of the
evidence has always been thrown away — and let the history build up. Cross-run flakiness
detection needs at least three runs.

### 4. Ask what happened

```bash
xping report
```

```
Xping · SampleApp.MSTest · 9 runs · 2026-08-20 07:52 → 09:02 · main@bdbafba
4 findings (4 high) · 17 tests · 14 healthy

HIGH  flaky            FlakyTest_PassesOnRetry
      failed 9 of 18 executions (50%) in 9 of 9 runs, 1 failure mode
      evidence moderate | f_2b84a621

HIGH  always failing   ThrowingTestIsTracked
      failed 9 of 9 executions (100%), one failure mode:
      System.InvalidOperationException
      evidence low | f_c1774d82

HIGH  flaky            FlakyTest_RaceCondition_FailsIntermittently
      failed 4 of 9 executions (44.4%) in 4 of 9 runs, 1 failure mode
      evidence low | f_d24c5aa9

HIGH  masked by retry  FlakyTest_PassesOnRetry
      passed on retry 9 times in 9 of 9 runs, up to attempt 2
      evidence moderate | f_e98db1e6
```

No API key, no signup, no network calls. Everything lives in `.xping/` on your machine.

[Running Without an Account →](getting-started/local-first.md)

---

## The CLI surface

```bash
xping report                    # findings from recent runs
xping report --format json      # the versioned envelope, for a script or an agent
xping report --summary          # one line, for a chat message or a CI step title
xping report --fail-on high     # exit non-zero when a high finding shows up
xping report --runs 50          # widen the window (or --since <sha|yyyy-MM-dd>)
xping report --kind flaky       # restrict to one or more finding kinds
xping where                     # show where local runs are stored
xping clear                     # delete recorded runs
```

Full flags, finding kinds, and the JSON schema: [CLI Command Reference](cli/command-reference.md).

---

## Feeding it to your agent

`xping report --format json` emits a versioned envelope — findings, evidence levels, run counts,
failure signatures — which is what makes the difference between an agent guessing and an agent
knowing:

1. **The agent hits a failing test.** Instead of rewriting the assertion on one red run, it asks
   Xping what this test's history looks like.
2. **Xping answers with evidence.** Failed 34 of 34 runs, one failure signature, evidence high —
   or failed 4 of 40, three signatures, only ever in parallel.
3. **The agent acts on it.** A failure that consistent isn't flake, it's a bug, and the test was
   right to catch it. A single local failure against a long clean history is noise: run it again,
   don't touch the test.

What comes back is evidence, not a verdict. The agent still decides.

---

## Connecting to Xping Cloud

Set one environment variable. Nothing else changes — same packages, same attributes.

```bash
export XPING_APIKEY="your-api-key"
```

Your project is named after your test assembly, so a solution with several test projects gets one
Xping project each — created automatically when tests first run. Set `XPING_PROJECTID` (or
`ProjectId` in `appsettings.json`) to report them all as a single project instead; see the
[configuration reference](configuration/configuration-reference.md#projectid).

Runs upload as they finish, so history pools across every machine, branch, and CI job — a test
that only fails on a teammate's laptop still counts.

> **Xping Cloud is currently invite-only.** We're running a small, high-touch pilot while the
> scoring model settles. [Request access](https://xping.io/contact?pilot=True) — or keep working
> locally, which stays free and account-free regardless.

`XPING_APIKEY` is an **upload-only** key. Export it from the environment on every dev machine and
in CI; never commit it. It can write test runs and nothing else, so the key sitting in your CI
secrets is not a way into your data.

For CI setup (GitHub Actions, Azure DevOps, Jenkins, GitLab), see
[CI/CD Integration](getting-started/ci-cd-setup.md).

---

## How It Works

```
        Your test project  (xUnit · NUnit · MSTest)
                        │
                        ▼
            Xping.Sdk.<framework> adapter
                        │
                        ▼
                  Xping.Sdk.Core
         tracking · environment detection
                        │
      ┌─────────────────┴─────────────────┐
      │  no upload key                    │  XPING_APIKEY set
      ▼                                   ▼
.xping/ (local store) ─ batched upload ─▶ Xping Cloud
      │                                   │  scoring · root cause · trends
      │                                   │
      │                                   ▼
      │                              app.xping.io
      ▼                                   ▼
      └──────────────▶ xping report ◀─────┘
                            │
              ┌─────────────┴─────────────┐
              ▼                           ▼
        you, in a terminal        your agent, via --format json
```

Adapters are thin — they hook the framework's execution pipeline and hand results to
`Xping.Sdk.Core`, which owns collection, environment detection, and delivery.

Two things are worth reading off that diagram:

- **The CLI is the only reader of the local store.** `.xping/` is an implementation detail;
  `xping report` is the interface. See [Local Store](configuration/local-store.md).
- **The terminal isn't the only surface.** Not everyone who cares about the suite runs it.
  Developers get the report one command away; QA, leads, and product open
  [app.xping.io](https://app.xping.io) and see the same evidence with nothing installed.

---

## Configuration

Configure via **environment variables**, **appsettings.json**, or **programmatically**.
Environment variables are recommended for the API key — it should never reach source control.

```bash
# Cloud only
export XPING_APIKEY="your-api-key"

# Optional: pin every test assembly to one project instead of one project each
export XPING_PROJECTID="your-project-id"

# Optional, either way
export XPING_ENABLED="true"
export XPING_BATCHSIZE="100"
export XPING_CAPTURESTACKTRACES="true"
```

Without `XPING_APIKEY`, the SDK writes to `.xping/` and makes no network calls. Full options in
the [Configuration Reference](configuration/configuration-reference.md).

---

## Your Data

**Per test execution** — name and fully qualified name, outcome, duration, start and end
timestamps (UTC), error message and stack trace on failure, the declared timeout budget when the
test sets one, categories and traits. A test the framework killed for overrunning its timeout is
recorded as a timeout, not folded into ordinary failures.

**Per environment** — OS and version, .NET runtime version, machine name, CI platform detection,
build and branch information from the CI environment, network metrics, and the machine's time
zone and UTC offset at the start of the run — which is what lets a failure be placed at a local
time of day rather than only at a UTC instant.

**What never gets recorded** — no source code, no assertion values, no credentials or secrets, no
personally identifiable information.

Locally, everything is written to `.xping/` in your repository and stays there. Add `.xping/` to
your `.gitignore` — the history is machine-local and isn't meant to be shared through version
control. To start over, run `xping clear`. For Cloud, data is transmitted over HTTPS; stack trace
capture is configurable and retention is set per workspace.

---

## What Xping Helps You Find

Patterns that show up in accumulated execution history:

- **Race conditions** — intermittent failures with no code change between runs
- **External dependencies** — failures that track network or service availability
- **Shared state** — tests that pass alone and fail in a suite
- **Time-based flakiness** — failures clustered in one local time of day, at weekends, or on one
  side of a daylight-saving change
- **Non-deterministic data** — random inputs that occasionally hit an edge case

Working locally surfaces the *behaviour*; Xping Cloud attributes the *cause*. See
[Common Flaky Patterns](guides/working-with-tests/common-flaky-patterns.md) for worked examples.

> **How Cloud confidence scores work**: Xping analyses test execution history across six weighted
> factors — pass rate (35%), execution stability (20%), retry behaviour (15%), environment
> consistency (15%), failure patterns (10%), and dependency impact (5%) — producing a 0–1 score.
> ESS is the effective sample size: how many independent runs stand behind that number. See
> [Understanding Confidence Scores](guides/getting-started/understanding-confidence-scores.md).

---

## Documentation Structure

#### [Getting Started](getting-started/local-first.md)

- [Running Without an Account](getting-started/local-first.md) — the local-only path, start to finish
- [NUnit Quick Start](getting-started/quickstart-nunit.md)
- [xUnit Quick Start](getting-started/quickstart-xunit.md)
- [MSTest Quick Start](getting-started/quickstart-mstest.md)
- [CI/CD Integration](getting-started/ci-cd-setup.md)

#### [Configuration](configuration/configuration-reference.md)

- [Configuration Reference](configuration/configuration-reference.md) — every option, all three sources
- [Local Store](configuration/local-store.md) — what `.xping/` holds and how it's managed

#### [CLI](cli/command-reference.md)

- [Command Reference](cli/command-reference.md) — `report`, `where`, `clear`, flags, JSON envelope

#### [Guides](guides/getting-started/understanding-confidence-scores.md)

**Getting Started:**
- [Understanding Confidence Scores](guides/getting-started/understanding-confidence-scores.md)
- [Session Reliability Score](guides/getting-started/session-reliability-score.md)
- [Navigating Xping Cloud](guides/getting-started/navigating-xping-cloud.md)
- [Interpreting Test Results](guides/getting-started/interpreting-test-results.md)

**Working with Tests:**
- [Identifying Flaky Tests](guides/working-with-tests/identifying-flaky-tests.md)
- [Common Flaky Patterns](guides/working-with-tests/common-flaky-patterns.md)
- [Fixing Flaky Tests](guides/working-with-tests/fixing-flaky-tests.md)
- [Monitoring Test Health](guides/working-with-tests/monitoring-test-health.md)
- [Best Practices](guides/working-with-tests/best-practices.md)

**Optimization:**
- [Performance Overview](guides/optimization/performance-overview.md)
- [Performance Configuration](guides/optimization/performance-configuration.md)
- [Performance Troubleshooting](guides/optimization/performance-troubleshooting.md)

#### [Performance](performance/overview.md)

- [Performance Overview](performance/overview.md)
- [Benchmark Results](performance/benchmark-results.md)

#### [Troubleshooting](troubleshooting/common-issues.md)

- [Common Issues](troubleshooting/common-issues.md)
- [Debugging Guide](troubleshooting/debugging.md)
- [Known Limitations](known-limitations.md) — what Xping does not do yet

#### [API Reference](https://docs.xping.io/api/Xping.Sdk.Core.Collection.html)

Complete API documentation for all SDK components.

---

## Sample Projects

Runnable examples per framework:

- [NUnit Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.NUnit)
- [xUnit Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnit)
- [MSTest Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.MSTest)

---

## Roadmap

Tracked in [Milestones](https://github.com/xping-dev/sdk-dotnet/milestones). Currently on the
list:

- `xping login` and `xping report --source local|cloud` — one command, both histories
- Agent integration: a Claude skill and an MCP server over `xping report`
- Quarantine — mark known-flaky tests so CI stops failing on them
- `xping watch` — stream new runs as they land, beside `dotnet watch test`
- Richer local analysis: duration regression, failure signature grouping
- Azure DevOps and GitLab integration

---

## Support & Community

- [GitHub Discussions](https://github.com/xping-dev/sdk-dotnet/discussions) — questions and ideas
- [Issue Tracker](https://github.com/xping-dev/sdk-dotnet/issues) — bugs and feature requests
- [support@xping.io](mailto:support@xping.io) — direct support
- [Xping Cloud](https://app.xping.io) — your scored test history

---

## Contributing

Xping SDK is open source and MIT-licensed. Contributions are welcome.

- [Contributing Guide](https://github.com/xping-dev/sdk-dotnet/blob/main/CONTRIBUTING.md)
- [Code of Conduct](https://github.com/xping-dev/sdk-dotnet/blob/main/CODE_OF_CONDUCT.md)
- [Open issues](https://github.com/xping-dev/sdk-dotnet/issues)
- [MIT License](https://github.com/xping-dev/sdk-dotnet/blob/main/LICENSE)

---

**Ready to see which of your tests you can trust?**
[Start locally, no account →](getting-started/local-first.md)
