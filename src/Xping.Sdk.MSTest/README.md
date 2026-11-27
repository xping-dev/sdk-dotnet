# Xping.Sdk.MSTest

MSTest adapter for the Xping SDK, for MSTest 3.8 or later. It records each test execution to the
local store and, with an API key, uploads it to Xping Cloud. The test project must target .NET
Framework 4.6.2 or later, or .NET 8 or later.

## Installation

```bash
dotnet add package Xping.Sdk.MSTest --prerelease
```

All Xping packages are prereleases, so the command needs `--prerelease`.

The adapter needs **MSTest 3.8 or later** (`MSTest.TestFramework` and `MSTest.TestAdapter`). MSTest
3.7 skips `[AssemblyCleanup]` when tests run in parallel at method level, and that method ends the
Xping session.

## Quick start

Add one class that starts and ends the session, and make each test class inherit from
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
public class CalculatorTests : XpingTestBase
{
    [TestMethod]
    public void Add_TwoNumbers_ReturnsSum() => Assert.AreEqual(5, 2 + 3);
}
```

- MSTest runs assembly hooks only from the test assembly itself, so `XpingSetup` must be in your
  test project.
- `FinalizeAndShutdownAsync` ends the session, writes it to the local store and uploads what is
  left.
- `XpingTestBase` records each test in its `[TestInitialize]` and `[TestCleanup]` methods. The SDK
  records only the tests of classes that inherit from it.

When you run `dotnet test` in a terminal, the SDK prints a line when the session starts, and with
an API key one line for each upload that Xping Cloud accepts:

```
[Xping 17:28:04 INF] Initialized in local-only mode. SessionId: "6bec32b0-922b-4b9b-bcb9-fb4e1056180f", Environment: Default. No API key configured - results stay on this machine.
```

When the output goes to a file or a pipe, for example in a CI log, `dotnet test` shows these lines
only with `--logger "console;verbosity=normal"`.

The full guide is [Quick start: MSTest](https://docs.xping.io/sdk/getting-started/quickstart-mstest.html).

## Running on Microsoft.Testing.Platform

The adapter hooks into MSTest, not the test runner, so it works on vstest and on
Microsoft.Testing.Platform (MTP) alike. To use MTP, enable MSTest's runner in the test project:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <EnableMSTestRunner>true</EnableMSTestRunner>
</PropertyGroup>
```

Run the test executable directly, or `dotnet test` in the .NET 10 SDK's MTP mode
(`global.json`: `{ "test": { "runner": "Microsoft.Testing.Platform" } }`). For TRX, reference
`Microsoft.Testing.Extensions.TrxReport` and pass `--report-trx`.

Strict mode fails the run through `[AssemblyCleanup]` on both runners. On MTP the
[process-exit safety net](#session-finalized-by-the-process-exit-safety-net) completes its upload
without `VSTEST_TESTHOST_SHUTDOWN_TIMEOUT`.

## What is recorded

- The outcome, mapped from MSTest's outcome:

  | MSTest outcome | Xping outcome |
  |---|---|
  | Passed | Passed |
  | Failed | Failed |
  | Inconclusive | Inconclusive |
  | Stopped by `[Timeout]` | Timeout |
  | Aborted | Failed |
  | Any other outcome | NotExecuted |

  A test stopped by `[Timeout]` is recorded as `Timeout`, with the budget it declared. A test
  stopped by a run-wide `TestTimeout` is recorded as `Timeout` without a budget. With cooperative
  cancellation (`CooperativeCancellation = true`, or turned on for the whole run), a timed-out test
  is recorded as `Failed` if it observes its token, or `Passed` if it ignores the token, as MSTest
  reports it. See
  [Known limitations](https://docs.xping.io/sdk/troubleshooting/known-limitations.html#timeouts).
- The duration, the start and end times, and the error message and stack trace of a failure. The
  stack trace text is uploaded only with `UploadStackTraces`.
- Each `[DataRow]` as a separate test, with its arguments
- `[TestProperty]` values, and the other entries of `TestContext.Properties`, as custom attributes
- A fingerprint pinned with `[XpingFingerprint("...")]`, which keeps a test's history when you
  rename it

MSTest skips a test with `[Ignore]` before its `[TestInitialize]` method runs, so the SDK does not
record it.

## Configuration

Without an API key, the SDK runs in local-only mode: it writes every run to `.xping/` in your
repository and makes no network calls. Read the history with the `xping` CLI
(`dotnet tool install -g Xping.Cli --prerelease`, then `xping report`).

To upload to Xping Cloud, create a key under **Settings → API keys**, with only the Upload scope,
and set it as an environment variable:

```bash
export XPING_APIKEY="your-api-key"
```

Do not put the key in `appsettings.json`: that file is committed and copied into the build output.
Other settings can go in the `"Xping"` section of `appsettings.json`, or in `XPING_*` environment
variables, which win over the file. See the
[Configuration reference](https://docs.xping.io/sdk/configuration/configuration-reference.html).

To build the configuration in code, pass it to `Initialize` in the `[AssemblyInitialize]` method:

```csharp
using Xping.Sdk.Core.Configuration;

[AssemblyInitialize]
public static void AssemblyInit(TestContext context) =>
    XpingContext.Initialize(new XpingConfiguration { BatchSize = 200 });
```

`XPING_*` environment variables still win over the values you pass. With a configuration object,
the SDK reads no `appsettings.json` file. A later `XpingContext.Initialize()` in the same process
reuses this configuration, so a long-lived process, such as an IDE's test server, keeps it across
runs. `ShutdownAsync()` forgets it.

## Error handling

An SDK failure is logged, and the tests run as usual. In `StrictMode`, a failed upload makes
`FinalizeAndShutdownAsync` throw. MSTest then fails the run and reports the error on the last test
result.

## Troubleshooting

### Session finalized by the process-exit safety net

If the log shows `Process exiting with session ... still active`, no `[AssemblyCleanup]` method
ended the session, and the SDK ended it when the test process exited. Make sure your test project
declares the `XpingSetup` class from the [quick start](#quick-start), and that nothing in its
`[AssemblyCleanup]` method throws before the `FinalizeAndShutdownAsync` call.

The SDK writes the local store before it uploads, so `xping report` still sees the run. On vstest,
the upload to Xping Cloud usually does not finish: vstest stops the test process 100 ms after the
run, which is less than one network round trip. To give the upload time, set this timeout in the
environment that runs `dotnet test`. The parent process reads it, so the SDK cannot set it for
you. On Microsoft.Testing.Platform, the upload finishes without it.

```bash
VSTEST_TESTHOST_SHUTDOWN_TIMEOUT=5000 dotnet test
```

## Known limitations

See [Known limitations](https://docs.xping.io/sdk/troubleshooting/known-limitations.html#mstest).

## Sample project

`samples/SampleApp.MSTest`.

## License

MIT License - Copyright (c) 2025 Xping.io
