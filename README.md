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
    <strong>Stop guessing. Start knowing which tests you can trust.</strong>
    <br />
    Test reliability analysis for .NET — accumulates history across <code>dotnet test</code> runs
    so flaky tests stop being anecdotes.
    <br />
    <sub>Local mode requires no account and no network access.</sub>
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

## Two ways to run Xping

|                | **Local** | **Cloud** |
| -------------- | --------- | --------- |
| **Setup**      | SDK package + CLI tool | + an API key |
| **Account**    | Not required | Required ([invite-only](#connecting-to-xping-cloud)) |
| **Your data**  | Stays in `.xping/` in your repo | Uploaded to Xping Cloud |
| **History**    | Your machine, your runs | Every machine, every branch, CI included |
| **You get**    | Observations and evidence from local runs | Confidence scores, evidence sufficiency, trends, PR comments |

**Start local.** Nothing leaves your machine, and you can add an API key later without
changing a line of test code.

---

## Quick Start

### 1. Install both pieces

```bash
dotnet add package Xping.Sdk.XUnit    # or Xping.Sdk.NUnit / Xping.Sdk.MSTest

dotnet new tool-manifest              # skip if you already have one
dotnet tool install Xping.Cli
```

The SDK records what happens during each run; the CLI reads what it recorded. You need
both — the SDK alone writes history nothing reads.

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

One run tells you nothing about reliability. A dozen tells you plenty. Run them the way
you normally would and let the history build up.

### 4. Ask what happened

```bash
dotnet xping report
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

No API key, no signup, no network calls. Everything lives in `.xping/` in your machine.

[Running Without an Account →](https://docs.xping.io/getting-started/local-first.html)

### Framework-specific guides

- [NUnit Setup Guide](https://docs.xping.io/getting-started/quickstart-nunit.html) — attributes, filtering, best practices
- [xUnit Setup Guide](https://docs.xping.io/getting-started/quickstart-xunit.html) — custom framework configuration and examples
- [MSTest Setup Guide](https://docs.xping.io/getting-started/quickstart-mstest.html) — base class usage and TestContext integration

---

## Why this exists

`dotnet test` is amnesiac. Every run starts from zero and discards everything the last one
knew. A test that failed on Tuesday and passed on Wednesday leaves no trace that anyone can
point at on Thursday, so "is that one flaky?" gets answered from memory and vibes — and
usually settled by re-running until it goes green.

Xping is the accumulation layer underneath that. The SDK records each execution with its
outcome, duration, and environment; the CLI reads that history back and tells you which
tests behaved consistently and which didn't. Nothing about your test code changes.

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
| Per-test confidence score               | — | ✓ |
| Evidence sufficiency (ESS)              | — | ✓ |
| Root-cause categorisation               | — | ✓ |
| Reliability trends over time            | — | ✓ |
| Cross-environment comparison            | — | ✓ |
| History across CI, branches, teammates  | — | ✓ |
| GitHub PR comments                      | — | ✓ |

---

## Connecting to Xping Cloud

Set two environment variables. Nothing else changes — same packages, same attributes.

```bash
export XPING_APIKEY="your-api-key"
export XPING_PROJECTID="your-project-id"
```

Runs are uploaded as they finish and analysed at [app.xping.io](https://app.xping.io):
confidence scores per test, evidence sufficiency, root-cause categorisation, trends across
environments, and PR comments on GitHub.

> **Xping Cloud is currently invite-only.** We're running a small, high-touch pilot while
> the scoring model settles. [Request access](https://xping.io) — or keep working locally,
> which stays free and account-free regardless.

For CI setup (GitHub Actions, Azure DevOps, Jenkins, GitLab), see the
[Configuration Reference](https://docs.xping.io/configuration/configuration-reference.html).

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
              │  no API key                       │  API key set
              ▼                                   ▼
       .xping/ (local store)                 Xping Cloud
              │                                   │
              ▼                                   ▼
      dotnet xping report            scores · trends · PR comments
```

Adapters are thin — they hook the framework's execution pipeline and hand results to
`Xping.Sdk.Core`, which owns collection, environment detection, and delivery. Overhead is
under 5 ms per test.

---

## Configuration

Configure via **environment variables**, **appsettings.json**, or **programmatically**.

```bash
# Cloud only
export XPING_APIKEY="your-api-key"
export XPING_PROJECTID="your-project-id"

# Optional, either way
export XPING_ENABLED="true"
export XPING_BATCHSIZE="100"
```

Without `XPING_APIKEY`, the SDK writes to `.xping/` and makes no network calls. Full
options in the [Configuration Reference](https://docs.xping.io/configuration/configuration-reference.html).

---

## Your Data

### What gets recorded

**Per test execution** — name and fully qualified name, outcome, duration, start and end
timestamps (UTC), error message and stack trace on failure, categories and traits.

**Per environment** — OS and version, .NET runtime version, machine name, CI platform
detection, build and branch information from the CI environment, network metrics.

### What never gets recorded

No source code. No assertion values. No credentials or secrets. No personally identifiable
information.

### Local

Everything is written to `.xping/` in your repository and stays there. No network calls are
made without an API key. Add `.xping/` to your `.gitignore` — the history is machine-local
and isn't meant to be shared through version control. To start over, delete the folder.

### Cloud

Data is transmitted over HTTPS. Keep API keys in environment variables or CI secrets, never
in source control. Stack trace capture and sampling are configurable, and retention is set
per workspace. The SDK is MIT-licensed and open source — [read exactly what it
sends](https://github.com/xping-dev/sdk-dotnet).

---

## What Xping Helps You Find

Patterns that show up in accumulated execution history:

- **Race conditions** — intermittent failures with no code change between runs
- **External dependencies** — failures that track network or service availability
- **Shared state** — tests that pass alone and fail in a suite
- **Time-based flakiness** — failures clustered around dates, times, or timezones
- **Resource exhaustion** — degradation over the course of a long run
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
| `src/Xping.Cli` | `dotnet xping` — local analysis and reporting |
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

- Quarantine — mark known-flaky tests so CI stops failing on them
- Richer local analysis: duration regression, failure signature grouping
- `xping mcp` — expose local history to AI coding agents
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