<div id="top"></div>

<div align="center">
  <p align="center">
    <a href="https://www.nuget.org/packages/Xping.Sdk.Core/"><img src="https://img.shields.io/nuget/v/Xping.Sdk.Core?label=Xping.Sdk.Core" alt="NuGet"></a>
    <a href="https://www.nuget.org/packages/Xping.Cli/"><img src="https://img.shields.io/nuget/v/Xping.Cli?label=Xping.Cli" alt="NuGet"></a>
    <a href="https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml"><img src="https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml/badge.svg" alt="Build Status"></a>
    <a href="https://codecov.io/gh/xping-dev/sdk-dotnet"><img src="https://codecov.io/gh/xping-dev/sdk-dotnet/graph/badge.svg?token=VUOVI3YUTO" alt="codecov"></a>
    <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  </p>
  <img src="docs/media/logo.svg" width="50" alt="Xping Logo" />
  <h1>Xping for .NET</h1>
  <p align="center">
    <strong>Your coding agent can't tell a flaky test from a broken one.</strong>
    <br />
    Xping records every <code>dotnet test</code> run — local ones included — so
    "is this test flaky?" gets answered from history instead of from one red run.
    <br />
    <sub>Recording and local reports are free, need no account, and make no network calls.</sub>
  </p>
</div>

<div align="center">
  <p align="center">
    <a href="#quick-start"><strong>Quick Start</strong></a> •
    <a href="#local-vs-cloud"><strong>Local vs. Cloud</strong></a> •
    <a href="#how-it-works"><strong>How It Works</strong></a> •
    <a href="https://docs.xping.io"><strong>Documentation</strong></a> •
    <a href="https://docs.xping.io/known-limitations.html"><strong>Known Limitations</strong></a> •
    <a href="https://github.com/xping-dev/sdk-dotnet/issues"><strong>Report Bug</strong></a>
  </p>
</div>

<br />

---

## Why this exists

`dotnet test` is amnesiac. Every run starts from zero and discards everything the last one
knew. A test that failed on Tuesday and passed on Wednesday leaves no trace anyone can point
at on Thursday, so "is that one flaky?" gets answered from memory and settled by re-running
until it goes green.

That was already expensive when a human was doing it. Now an agent is. Hand Claude a failing 
test and a single run to learn from, and it will make the test pass. With one data point, a 
real regression and a flake look identical — so it fixes what it can see:

| It does this | And you get |
| ------------ | ----------- |
| Loosens the assertion | A test that still runs, but stopped checking the thing that broke |
| Adds a retry | A real, reproducible failure that now passes on attempt three |
| Bumps the timeout | The same race condition, just harder to hit in CI |
| Moves the test into its own [Collection] | A green suite that now runs those tests serially — the shared-state bug is untouched, and every build is slower |
| Mocks the dependency that failed | A test that passes deterministically because it no longer touches the code that broke. |

Every one of those is a reasonable move on one data point. Nobody notices until the suite
stops meaning anything.

Xping is the accumulation layer underneath. The SDK records each execution with its outcome,
duration, and environment; the CLI reads that history back and says which tests behaved
consistently and which didn't — to you in the terminal, and to your agent as structured JSON.
Nothing about your test code changes.

---

## Two ways to run Xping

|                | **Local** | **Cloud** |
| -------------- | --------- | --------- |
| **Setup**      | SDK package + CLI tool | + an upload key |
| **Account**    | Not required | Required ([invite-only](#connecting-to-xping-cloud)) |
| **Your data**  | Stays in `.xping/` in your repo | Uploaded to Xping Cloud |
| **History**    | Your machine, your runs | Every machine, every branch, CI included |
| **You get**    | Observations and evidence from local runs | Confidence scores, evidence sufficiency, trends, PR comments |

**Start local.** Nothing leaves your machine, and you can add an upload key later without
changing a line of test code.

---

## Quick Start

### 1. Install both pieces

```bash
dotnet add package Xping.Sdk.XUnit    # or Xping.Sdk.NUnit / Xping.Sdk.MSTest

dotnet tool install -g Xping.Cli      # puts `xping` on your PATH
```

The SDK records what happens during each run; the CLI reads what it recorded. You need
both — the SDK alone writes history nothing reads.

> The CLI targets `net10.0` and needs the .NET 10 SDK or runtime. The SDK packages target
> `netstandard2.0`, so your test projects can stay on any supported .NET version.

### 2. Turn on tracking

**xUnit** — add one line to `AssemblyInfo.cs`:
```csharp
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

**NUnit** — add one assembly-level attribute:
```csharp
[assembly: XpingTrack]
```

**MSTest** — inherit from the base class:
```csharp
[TestClass]
public class MyTests : XpingTestBase { }
```

### 3. Run your tests — more than once

```bash
dotnet test
```

One run tells you nothing about reliability. A dozen tells you plenty. Run them the way you
normally would — including the local runs that never reach CI, which is where most of the
evidence has always been thrown away — and let the history build up.

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

[Running Without an Account →](https://docs.xping.io/getting-started/local-first.html)

### Framework-specific guides

- [NUnit Setup Guide](https://docs.xping.io/getting-started/quickstart-nunit.html) — attributes, filtering, best practices
- [xUnit Setup Guide](https://docs.xping.io/getting-started/quickstart-xunit.html) — custom framework configuration and examples
- [MSTest Setup Guide](https://docs.xping.io/getting-started/quickstart-mstest.html) — base class usage and TestContext integration

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

`xping report --help` lists the rest: `--top`/`--all`, `--assembly`, `--directory`,
`--ascii`, `--no-color`.

---

## Feeding it to your agent

`xping report --format json` emits a versioned envelope — findings, evidence levels, run
counts, failure signatures — which is what makes the difference between an agent guessing and
an agent knowing:

1. **Claude hits a failing test.** Instead of rewriting the assertion on one red run, it asks
   Xping what this test's history looks like.
2. **Xping answers with evidence.** Failed 34 of 34 runs, one failure signature, evidence
   high — or failed 4 of 40, three signatures, only ever in parallel.
3. **Claude acts on it.** A failure that consistent isn't flake, it's a bug, and the test was
   right to catch it. A single local failure against a long clean history is noise: run it
   again, don't touch the test.

What comes back is evidence, not a verdict. The agent still decides.

Today that means piping `--format json` into your agent's context. A dedicated skill and an
MCP server are [on the roadmap](#roadmap).

---

## Local vs. Cloud

The local CLI reports **what it observed**. It does not assign confidence scores or name
causes — a handful of runs on one machine isn't an evidence base strong enough to support
that kind of claim, and we'd rather show you the runs than a number that implies more
certainty than the data has. Scoring lives in Xping Cloud, where there's enough history
across machines, branches, and CI to justify it.

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

Confidence runs 0–1. ESS is the effective sample size — how many independent runs stand
behind that number.

---

## Connecting to Xping Cloud

Set one environment variable. Nothing else changes — same packages, same attributes.

```bash
export XPING_APIKEY="your-api-key"
```

Your project is named after your test assembly, so a solution with several test projects gets
one Xping project each. Set `XPING_PROJECTID` or add `ProjectId` to `appsettings.json` to report them all as a single project instead.

Runs upload as they finish, so history pools across every machine, branch, and CI job — a
test that only fails on a teammate's laptop still counts. Analysis happens at
[app.xping.io](https://app.xping.io): confidence scores per test, evidence sufficiency,
root-cause categorisation, trends across environments, and PR comments on GitHub.

> **Xping Cloud is currently invite-only.** We're running a small, high-touch pilot while
> the scoring model settles. [Request access](https://xping.io/contact?pilot=True) — or keep working locally,
> which stays free and account-free regardless.

For CI setup (GitHub Actions, Azure DevOps, Jenkins, GitLab), see the
[Configuration Reference](https://docs.xping.io/configuration/configuration-reference.html).

### Two credentials, on purpose

`XPING_APIKEY` is an **upload-only** key. Export it from the environment on every dev machine
and in CI; never commit it. It can write test runs and nothing else, so the key sitting in
your CI secrets is not a way into your data.

Reading back the team's scored history is a **person's** action, authenticated as you rather
than as a shared machine credential ([`xping login`](#roadmap) — landing next). Machines
write, people read: that's how history pools across a team without anyone sharing a
credential, and why a contractor's laptop can contribute runs without getting read access to
your suite.

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
      tracking · environment detection · batching
                        │
      ┌─────────────────┴─────────────────┐
      │  no upload key                    │  XPING_APIKEY set
      ▼                                   ▼
.xping/ (local store)   ─── upload ───▶  Xping Cloud
      │                                   │  scoring · root cause · trends
      │                                   │
      │                                   ├──────────────────────┐
      │                                   │                      │
      │                            (read: xping login)           ▼
      │                                   │              app.xping.io
      ▼                                   ▼            the shared view —
      └──────────────▶ xping report ◀─────┘            QA, leads, product,
                            │                          nothing installed
              ┌─────────────┴─────────────┐
              ▼                           ▼
        you, in a terminal        your agent, via --format json
```

Adapters are thin — they hook the framework's execution pipeline and hand results to
`Xping.Sdk.Core`, which owns collection, environment detection, and delivery. Overhead is
under 5 ms per test.

Two things are worth reading off that diagram:

- **The CLI is the only reader of the local store.** `.xping/` is an implementation detail;
  `xping report` is the interface.
- **The terminal isn't the only surface.** Not everyone who cares about the suite runs it.
  Developers get the report one command away; QA, leads, and product open
  [app.xping.io](https://app.xping.io) and see the same evidence with nothing installed. This
  repo is the developer half — the portal is documented at [docs.xping.io](https://docs.xping.io).

> **Reading cloud history from the CLI** (`xping login`, `xping report --source local|cloud`)
> is the next thing landing here. Today `xping report` reads the local store; cloud history is
> read in the portal.

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

Without `XPING_APIKEY`, the SDK writes to `.xping/` and makes no network calls. Full
options in the [Configuration Reference](https://docs.xping.io/configuration/configuration-reference.html).

---

## Your Data

### What gets recorded

**Per test execution** — name and fully qualified name, outcome, duration, start and end
timestamps (UTC), error message and stack trace on failure, the declared timeout budget when
the test sets one, categories and traits. A test the framework killed for overrunning its
timeout is recorded as a timeout, not folded into ordinary failures.

**Per environment** — OS and version, .NET runtime version, machine name, CI platform
detection, build and branch information from the CI environment, network metrics, and the
machine's time zone and UTC offset at the start of the run — which is what lets a failure be
placed at a local time of day rather than only at a UTC instant.

### What never gets recorded

No source code. No assertion values. No credentials or secrets. No personally identifiable
information.

### Local

Everything is written to `.xping/` in your repository and stays there. No network calls are
made without an API key. Add `.xping/` to your `.gitignore` — the history is machine-local
and isn't meant to be shared through version control. To start over, run `xping clear` (or
delete the folder).

### Cloud

Data is transmitted over HTTPS. Keep API keys in environment variables or CI secrets, never
in source control. Stack trace capture is configurable, and retention is set
per workspace. The SDK is MIT-licensed and open source — [read exactly what it
sends](https://github.com/xping-dev/sdk-dotnet).

---

## What Xping Helps You Find

Patterns that show up in accumulated execution history:

- **Race conditions** — intermittent failures with no code change between runs
- **External dependencies** — failures that track network or service availability
- **Shared state** — tests that pass alone and fail in a suite
- **Time-based flakiness** — failures clustered in one local time of day, at weekends, or on
  one side of a daylight-saving change
- **Non-deterministic data** — random inputs that occasionally hit an edge case

Working locally surfaces the *behaviour*; Xping Cloud attributes the *cause*. See the
[Common Flaky Patterns Guide](https://docs.xping.io/guides/working-with-tests/common-flaky-patterns.html)
for worked examples.

---

## Repository Layout

| Path | Contents |
| ---- | -------- |
| `src/Xping.Sdk.Core` | Collection, environment detection, configuration, delivery |
| `src/Xping.Sdk.XUnit` · `.NUnit` · `.MSTest` | Framework adapters |
| `src/Xping.Cli` | `xping` — local analysis and reporting |
| `samples/` | Runnable examples per framework |
| `docs/` | Source for [docs.xping.io](https://docs.xping.io) |

---

## Documentation

- [Getting Started](https://docs.xping.io/index.html#quick-start)
- [Running Without an Account](https://docs.xping.io/getting-started/local-first.html)
- [Known Limitations](https://docs.xping.io/known-limitations.html)
- [Troubleshooting](https://docs.xping.io/troubleshooting/common-issues.html)
- [API Reference](https://docs.xping.io/api/Xping.Sdk.Core.Collection.html)

Full documentation at [docs.xping.io](https://docs.xping.io).

---

## Contributing

Bug reports, feature requests, documentation fixes, and code contributions are all welcome.

```bash
git clone https://github.com/xping-dev/sdk-dotnet.git
cd sdk-dotnet
dotnet restore
dotnet build
dotnet test
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## Roadmap

Tracked in [Milestones](https://github.com/xping-dev/sdk-dotnet/milestones):
[Working Set](https://github.com/xping-dev/sdk-dotnet/milestone/1) ·
[Backlog](https://github.com/xping-dev/sdk-dotnet/milestone/2)

Currently on the list:

- `xping login` and `xping report --source local|cloud` — one command, both histories
- Agent integration: a Claude skill and an MCP server over `xping report`
- Quarantine — mark known-flaky tests so CI stops failing on them
- `xping watch` — stream new runs as they land, beside `dotnet watch test`
- Richer local analysis: duration regression, failure signature grouping
- Azure DevOps and GitLab integration

---

## Support

- [GitHub Discussions](https://github.com/xping-dev/sdk-dotnet/discussions) — questions and ideas
- [Issue Tracker](https://github.com/xping-dev/sdk-dotnet/issues) — bugs and feature requests
- [support@xping.io](mailto:support@xping.io) — direct support

---

## License

MIT — see [LICENSE](LICENSE).

---

<div align="center">
  <p><strong>Built by developers who hate flaky tests</strong></p>
  <p><sub>Made by <a href="https://xping.io">Xping</a> • Follow us on <a href="https://github.com/xping-dev">GitHub</a></sub></p>
  <br />
  <p>If Xping helps you build better software, give us a ⭐ on GitHub!</p>
</div>

<p align="right">(<a href="#top">back to top</a>)</p>
