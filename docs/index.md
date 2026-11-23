---
uid: index
title: Xping for .NET
---

# Xping for .NET

This page is for .NET developers who want to know which of their tests they can trust. It explains
what Xping records, how to start without an account, and what Xping Cloud adds.

Xping records every `dotnet test` run, including the runs on your own machine. With that history,
you can tell a flaky test from a broken one.

> Recording and local reports are **free**. They need **no account** and make **no network calls**.

---

## Why history matters

A single test run cannot tell a flaky test from a real failure. Both look the same: one red result.

When you, or a coding agent, fix a test from one red run, the usual fixes hide the problem:

| The fix | The result |
| ------- | ---------- |
| Loosen the assertion | The test runs, but it no longer checks the thing that broke. |
| Add a retry | A real failure that you can reproduce passes on the third attempt. |
| Increase the timeout | The race condition is still there. It happens less often, so it is harder to find in CI. |
| Move the test into its own `[Collection]` | The tests run one after another. The shared-state bug is still there, and every build is slower. |
| Mock the dependency that failed | The test always passes, because it no longer runs the code that broke. |

Each of these fixes makes sense when you have only one run. Xping keeps the history of every run,
so you can decide from many runs instead. Your test code does not change.

---

## What is Xping?

**Xping SDK**: a library that connects to your test framework (NUnit, xUnit v2 and v3, MSTest)
and records each test execution: the outcome, the duration and the environment. The packages target
`netstandard2.0`. They run on .NET Framework 4.6.2 or later and on .NET 8 or later.

**Xping CLI**: the `xping` .NET tool. It reads the recorded history and reports which tests
behaved consistently and which did not. It writes text for you and JSON for scripts and agents.
It needs the .NET 10 runtime.

**Xping Cloud**: the hosted service at [app.xping.io](https://app.xping.io). It combines the
history from every machine, branch and CI job. It gives each test a confidence score, says how
much evidence stands behind the score, detects flaky tests and their likely cause, shows trends,
and comments on pull requests in GitHub, GitLab and Azure DevOps. Access is by invitation.

The SDK writes the history and the CLI reads it, so a local report needs both. Xping Cloud is
optional.

---

## Local and Xping Cloud

|                | **Local** | **Xping Cloud** |
| -------------- | --------- | --------------- |
| **Setup**      | SDK package and CLI tool | Also an API key with the Upload scope |
| **Account**    | Not needed | Needed (by invitation) |
| **Your data**  | Stays in `.xping/` in your repository | Uploaded to Xping Cloud |
| **History**    | Your machine, your runs | Every machine and branch, CI included |
| **You get**    | Observations and evidence from local runs | Confidence scores, evidence levels, flaky detection, trends, PR comments |

The local CLI reports **what it observed**. It does not give a test a score, and it does not name
a cause. A few runs on one machine are not enough evidence for that. Xping Cloud scores tests,
because it has the history of many machines, branches and CI jobs.

|                                        | Local | Xping Cloud |
| -------------------------------------- | :---: | :---: |
| Pass and fail history, run by run       | ✓ | ✓ |
| Tests that pass and fail inconsistently | ✓ | ✓ |
| Tests that fail in every run            | ✓ | ✓ |
| Duration and environment of each run    | ✓ | ✓ |
| JSON output for agents and scripts      | ✓ | ✓ |
| Confidence score per test               | — | ✓ |
| Effective sample size (ESS) and evidence level | — | ✓ |
| Flaky verdict and cause category        | — | ✓ |
| Reliability trends                      | — | ✓ |
| Comparison across environments          | — | ✓ |
| History from CI, branches and teammates | — | ✓ |
| Pull request comments                   | — | ✓ |

**Start locally.** Nothing leaves your machine. You can add an API key later without changing
your test code.

---

## Quick start

### 1. Install the SDK and the CLI

```bash
dotnet add package Xping.Sdk.XUnit --prerelease   # xUnit v2. Or Xping.Sdk.XUnit.V3, Xping.Sdk.NUnit, Xping.Sdk.MSTest
dotnet tool install -g Xping.Cli --prerelease     # adds the xping command to your PATH
```

> All Xping packages are prereleases, so each install command needs `--prerelease`.
> The CLI needs the .NET 10 SDK or runtime. Your test projects can target .NET Framework 4.6.2 or
> later, or .NET 8 or later. The MSTest adapter needs MSTest 3.8 or later.

### 2. Turn on tracking

# [xUnit](#tab/xunit)

Add one line to `AssemblyInfo.cs`. The line depends on the xUnit package that your project
references:

```csharp
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")] // xunit (v2): Xping.Sdk.XUnit
// [assembly: TestFramework(typeof(Xping.Sdk.XUnit.V3.XpingTestFramework))]       // xunit.v3: Xping.Sdk.XUnit.V3
```

[Quick start: xUnit →](getting-started/quickstart-xunit.md)

# [NUnit](#tab/nunit)

Add one file. It tracks every test in the assembly, and its setup fixture starts and ends the Xping
session:

```csharp
using System.Threading.Tasks;
using NUnit.Framework;
using Xping.Sdk.NUnit;

[assembly: XpingTrack]

[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void BeforeAllTests() => XpingContext.Initialize();

    [OneTimeTearDown]
    public async Task AfterAllTests() => await XpingContext.FinalizeAndShutdownAsync();
}
```

Keep the `[OneTimeTearDown]` method. The SDK writes the run to the local store when the session
ends, and without this method the session never ends.

[Quick start: NUnit →](getting-started/quickstart-nunit.md)

# [MSTest](#tab/mstest)

Add one class that starts and ends the Xping session, and make your test classes inherit from
`XpingTestBase`:

```csharp
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.MSTest;

[TestClass]
public static class XpingSetup
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context) => XpingContext.Initialize();

    [AssemblyCleanup]
    public static async Task AssemblyCleanup() => await XpingContext.FinalizeAndShutdownAsync();
}

[TestClass]
public class MyTests : XpingTestBase { }
```

The `[AssemblyCleanup]` method lets the upload to Xping Cloud finish before the test process
exits.

[Quick start: MSTest →](getting-started/quickstart-mstest.md)

***

### 3. Run your tests several times

```bash
dotnet test
```

Each run adds to the history. One run says nothing about reliability, so `xping report` shows no
findings until it has at least **5 runs**, and each test needs at least 5 runs of its own. Run your
tests as you normally do. The runs on your own machine count too.

### 4. Read the report

```bash
xping report
```

<!-- xping:sample docs-mstest -->
````
Xping · SampleApp.MSTest · 9 runs · 2026-08-20 07:52 → 09:02 · main@bdbafba
17 tests · 14 healthy · 3 flagged

```
NEEDS ATTENTION (4 high)                               most severe first
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    SampleTests.FlakyTest_PassesOnRetry
    failed 9 of 18 executions (50%) in 9 of 9 runs, 1 failure mode
    evidence adequate | -env-cluster | f_2b84a621 | ...SampleTests.cs:96

2.  HIGH  masked by retry                                same test as #1
    SampleTests.FlakyTest_PassesOnRetry
    passed on retry 9 times in 9 of 9 runs, up to attempt 2
    evidence adequate | -env | f_e98db1e6 | .../SampleTests.cs:96

3.  HIGH  always failing
    SampleTests.ThrowingTestIsTracked
    failed 9 of 9 executions (100%), one failure mode:
    System.InvalidOperationException
    evidence sparse | -env-cluster | f_c1774d82 | .../SampleTests.cs:65

4.  HIGH  flaky
    SampleTests.FlakyTest_RaceCondition_FailsIntermittently
    failed 4 of 9 executions (44.4%) in 4 of 9 runs, 1 failure mode
    evidence sparse | -env-cluster | f_d24c5aa9 | .../SampleTests.cs:118
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/reading-the-report.html#the-population-marker

Detail of row 1: xping report --id f_2b84a621 --assembly SampleApp.MSTest
````

No API key, no account and no network calls. The history stays in `.xping/` on your machine.

[Run without an account →](getting-started/local-first.md)

---

## CLI commands

```bash
xping report                    # findings from recent runs
xping report --format json      # versioned JSON, for a script or an agent
xping report --summary          # one line per assembly, for a chat message or a CI step title
xping report --fail-on high     # exit with code 1 when any assembly has a high finding
xping report --runs 50          # a larger window (or --since <sha|yyyy-MM-dd>)
xping report --assembly Shop.Tests --kind flaky   # one assembly, one or more finding kinds
xping where                     # where the local runs are stored
xping clear                     # delete recorded runs
xping login                     # sign in to Xping Cloud from your browser
xping logout                    # sign out and remove the stored sign-in
xping auth status               # which Xping Cloud credential the CLI uses
```

With several test projects, `xping report` prints one row per assembly. Add `--assembly` to see
the findings of one assembly. When the store holds several assemblies, `--kind`, `--top` and `--all` need `--assembly`. See
[Scoping](cli/command-reference.md#scoping).

For every flag, finding kind and the JSON schema, see the
[Command reference](cli/command-reference.md).

---

## Use with a coding agent

`xping report --format json` returns versioned JSON: the findings, their evidence levels, the run
counts and the failure modes. An agent can use it like this:

1. **The agent finds a failing test.** Before it changes the assertion, it asks Xping for the
   history of that test.
2. **Xping returns the evidence.** For example: failed in 34 of 34 runs, one failure mode,
   evidence robust. Or: failed in 4 of 40 runs, three failure modes.
3. **The agent decides.** A test that fails the same way in every run probably found a real bug,
   so the agent fixes the code and keeps the test. One local failure after a long series of passes
   may be noise, so the agent runs the test again before it changes anything.

The report gives evidence, not a verdict. The agent still makes the decision.

---

## Connect to Xping Cloud

Set one environment variable. Your packages and test code stay the same.

```bash
export XPING_APIKEY="your-api-key"
```

Create the key in Xping Cloud under **Settings → API keys**. Create it with only the Upload scope.
Set it as an environment variable on each machine and in CI. Never commit it.

Each test assembly becomes its own Xping Cloud project. Xping Cloud creates the project the first
time the tests upload. To send several assemblies to one project, set `XPING_PROJECTID` (or
`ProjectId` in `appsettings.json`). See [ProjectId](configuration/configuration-reference.md#projectid).

The SDK uploads the results while the tests run and at the end of each run. It also keeps writing
the local store. Xping Cloud combines the runs from every machine, branch and CI job, so a test
that fails only on a teammate's laptop is part of the history.

> **Xping Cloud access is by invitation.** [Request access](https://xping.io/contact?pilot=True).
> The local report stays free and needs no account.

To see Xping Cloud data in your terminal, sign in once on each machine:

```bash
xping login
xping report
```

`xping report` then adds the Xping Cloud confidence score to the rows it prints. A key with only
the Upload scope cannot read Xping Cloud data, so the CLI uses your sign-in instead. See
[`xping login`](cli/command-reference.md#xping-login) and
[Cloud data](cli/cloud-data.md).

To set up CI, see [Set up CI](getting-started/ci-cd-setup.md).

---

## How it works

```
        Your test project  (xUnit · NUnit · MSTest)
                        │
                        ▼
            Xping.Sdk.<framework> adapter
                        │
                        ▼
                  Xping.Sdk.Core
         recording · environment detection
                        │
          ┌─────────────┴──────────────┐
          │ always                     │ with XPING_APIKEY
          ▼                            ▼
   .xping/ (local store)          Xping Cloud
          │                            │ scores · flaky detection · trends
          │                            ├────────────▶ app.xping.io
          ▼                            ▼
          └───────▶ xping report ◀─────┘  (after xping login)
                         │
           ┌─────────────┴─────────────┐
           ▼                           ▼
     you, in a terminal       your agent, with --format json
```

Each adapter connects to the execution pipeline of its framework and passes the results to
`Xping.Sdk.Core`. Core records the results, detects the environment, writes the local store and
uploads to Xping Cloud.

- **Only the CLI reads the local store.** Use `xping report` to read it, not the files in
  `.xping/`. See [Local store](configuration/local-store.md).
- **Not everyone uses a terminal.** People who do not run the tests, such as QA, team leads and
  product managers, can open [app.xping.io](https://app.xping.io) and see the same history.

---

## Configuration

You can configure the SDK with **environment variables**, **appsettings.json** or **code**.
`XPING_*` environment variables win over the other sources. Keep the API key in an environment
variable, so it never reaches source control.

```bash
# Xping Cloud
export XPING_APIKEY="your-api-key"
export XPING_PROJECTID="your-project-id"   # optional: one project for every test assembly
export XPING_BATCHSIZE="100"               # optional: results per upload batch
export XPING_UPLOADSTACKTRACES="true"      # optional: also upload the stack trace text (off by default)

# Local and Xping Cloud
export XPING_ENABLED="true"
export XPING_LOGLEVEL="Information"
```

Without an API key from any source, and without StrictMode, the SDK writes to `.xping/` and makes
no network calls. For every option,
see the [Configuration reference](configuration/configuration-reference.md).

---

## Your data

For each test execution, Xping records the test's name, outcome, duration and times, the error
message and stack trace of a failure, its categories and traits, and its source file and line. For
each run, it records the machine, the operating system, the .NET runtime, the time zone, and the CI
build, branch, commit and the person behind the run. Outside CI, it records the branch and commit of
your local git repository.

**Error text**: Xping stores the error message word for word, and uploads it word for word to
Xping Cloud. It contains whatever your test or your code put in it. Xping does not read your source
code.

[Data Xping collects](configuration/collected-data.md) lists every field, the personal data among them, and the switch for
each one. The CI email and the local git author are off by default.

**Locally**, the history stays in `.xping/`. The folder contains its own `.gitignore`, so git
ignores it. To delete the history, run `xping clear`.

**In Xping Cloud**, the SDK uploads over HTTPS. It does not upload the stack trace text unless you
set [UploadStackTraces](configuration/configuration-reference.md#uploadstacktraces). Xping Cloud
always receives a hash of the methods in the trace, without file paths or line numbers. The local
store keeps the full trace. How long Xping Cloud keeps your data depends on your plan; see
[xping.io/pricing](https://xping.io/pricing).

---

## What Xping helps you find

Patterns that show up in the run history:

- **Intermittent failures**: the test passes and fails with no code change between runs.
- **Parallel runs**: the test fails only when it runs at the same time as other tests.
- **Shared state**: tests that fail together because they share a fixture or a resource.
- **Time-based failures**: failures at one local time of day, at weekends, or around a
  daylight-saving change.
- **Random data**: random inputs that sometimes hit an edge case.

The local report shows the behaviour. Xping Cloud also gives a cause category. For worked
examples, see [Common flaky patterns](flaky-tests/common-flaky-patterns.md).

> Xping Cloud gives each test a confidence score from 0 to 1, based on its run history.
> See [Confidence scores](cloud/confidence-scores.md).

---

## Documentation structure

#### [Get started](getting-started/local-first.md)

- [Run without an account](getting-started/local-first.md): the local-only path, from start to finish
- [Quick start: NUnit](getting-started/quickstart-nunit.md)
- [Quick start: xUnit (v2 and v3)](getting-started/quickstart-xunit.md)
- [Quick start: MSTest](getting-started/quickstart-mstest.md)
- [Set up CI](getting-started/ci-cd-setup.md)

#### [Configure](configuration/configuration-reference.md)

- [Configuration reference](configuration/configuration-reference.md): every option, from all three sources
- [Local store](configuration/local-store.md): what `.xping/` holds and how it is managed

#### [CLI](cli/command-reference.md)

- [Command reference](cli/command-reference.md): `report`, `where`, `clear`, `login`, the flags and the JSON output

#### [Xping Cloud](cloud/confidence-scores.md)

- [Confidence scores](cloud/confidence-scores.md)
- [Session reliability score](cloud/session-reliability-score.md)
- [Reading the PR comment](cloud/reading-the-pr-comment.md)
- [Interpreting test results](cloud/interpreting-test-results.md)

#### [Flaky tests](flaky-tests/finding-flaky-tests.md)

- [Finding flaky tests](flaky-tests/finding-flaky-tests.md)
- [Flaky patterns and fixes](flaky-tests/patterns-and-fixes.md)
- [Monitoring test health](flaky-tests/monitoring-test-health.md)
- [Common flaky patterns](flaky-tests/common-flaky-patterns.md)
- [Best practices](flaky-tests/best-practices.md)

#### [Performance](performance/performance.md)

- [Performance](performance/performance.md)
- [Performance configuration](performance/performance-configuration.md)
- [Performance troubleshooting](performance/performance-troubleshooting.md)
- [Benchmark overview](performance/overview.md)
- [Benchmark results](performance/benchmark-results.md)

#### [Troubleshooting](troubleshooting/common-issues.md)

- [Common issues](troubleshooting/common-issues.md)
- [Debugging](troubleshooting/debugging.md)
- [Known limitations](troubleshooting/known-limitations.md): what Xping does not do

#### [API reference](xref:Xping.Sdk.Core)

The API documentation for the SDK packages.

---

## Sample projects

A runnable example for each framework:

- [NUnit sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.NUnit)
- [xUnit v2 sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnit)
- [xUnit v3 sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnitV3)
- [MSTest sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.MSTest)

---

## Roadmap

Planned work is tracked in [Milestones](https://github.com/xping-dev/sdk-dotnet/milestones). It
includes:

- Agent integration: a Claude skill and an MCP server over `xping report`
- Quarantine: mark known flaky tests so that CI does not fail on them
- `xping watch`: show new runs as they finish, next to `dotnet watch test`

---

## Support and community

- [GitHub Discussions](https://github.com/xping-dev/sdk-dotnet/discussions): questions and ideas
- [Issue tracker](https://github.com/xping-dev/sdk-dotnet/issues): bugs and feature requests
- [support@xping.io](mailto:support@xping.io): direct support
- [Xping Cloud](https://app.xping.io): your scored test history

---

## Contributing

The Xping SDK is open source under the MIT license. Contributions are welcome.

- [Contributing guide](https://github.com/xping-dev/sdk-dotnet/blob/main/CONTRIBUTING.md)
- [Code of conduct](https://github.com/xping-dev/sdk-dotnet/blob/main/CODE_OF_CONDUCT.md)
- [Open issues](https://github.com/xping-dev/sdk-dotnet/issues)
- [MIT license](https://github.com/xping-dev/sdk-dotnet/blob/main/LICENSE)

---

**Find out which of your tests you can trust.**
[Start locally, with no account →](getting-started/local-first.md)
