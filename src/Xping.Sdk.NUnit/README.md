# Xping.Sdk.NUnit

NUnit adapter for the Xping SDK, for NUnit 3.14 or later. It records each test execution to the
local store and, with an API key, uploads it to Xping Cloud. The test project must target .NET
Framework 4.6.2 or later, or .NET 8 or later.

## Installation

```bash
dotnet add package Xping.Sdk.NUnit --prerelease
```

All Xping packages are prereleases, so the command needs `--prerelease`.

## Quick start

Add one file to the root of your test project:

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

- `[assembly: XpingTrack]` tracks every test in the assembly. To track less, put `[XpingTrack]` on
  a test class or a test method instead.
- The setup fixture has no namespace, so it runs once for the whole assembly.
- `FinalizeAndShutdownAsync` ends the session, writes it to the local store and uploads what is
  left. Without the `[OneTimeTearDown]` method, the SDK records nothing.

When you run `dotnet test` in a terminal, the SDK prints a line when the session starts, and with
an API key one line for each upload that Xping Cloud accepts:

```
[Xping 17:28:04 INF] Initialized in local-only mode. SessionId: "6bec32b0-922b-4b9b-bcb9-fb4e1056180f", Environment: Default. No API key configured - results stay on this machine.
```

When the output goes to a file or a pipe, for example in a CI log, `dotnet test` shows these lines
only with `--logger "console;verbosity=normal"`.

The full guide is [Quick start: NUnit](https://docs.xping.io/getting-started/quickstart-nunit.html).

## Running on Microsoft.Testing.Platform

The adapter hooks into NUnit, not into the test runner, so it works on vstest and on
Microsoft.Testing.Platform. To use Microsoft.Testing.Platform, turn on NUnit's runner
(NUnit3TestAdapter 5.0 or later) in the test project:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <EnableNUnitRunner>true</EnableNUnitRunner>
</PropertyGroup>
```

Run the test executable, or run `dotnet test` with `{ "test": { "runner": "Microsoft.Testing.Platform" } }`
in `global.json` (.NET 10 SDK). For a TRX report, reference `Microsoft.Testing.Extensions.TrxReport`
and pass `--report-trx`.

## What is recorded

- The outcome, mapped from NUnit's result state:

  | NUnit result state | Xping outcome |
  |---|---|
  | Success | Passed |
  | Failure, Error, SetUpFailure, SetUpError, TearDownError, ChildFailure | Failed |
  | Failure from a `[Timeout]` or `[CancelAfter]` budget, Cancelled | Timeout |
  | Skipped, Ignored, Explicit | Skipped |
  | Inconclusive | Inconclusive |
  | Any other state | NotExecuted |

- The duration, the start and end times, and the error message and stack trace of a failure. The
  stack trace text is uploaded only with `UploadStackTraces`.
- Each `[TestCase]` as a separate test, with its arguments
- `[Category]` values as categories, `[Description]` as the description, and `[Author]` as an
  `author:` tag
- A fingerprint pinned with `[XpingFingerprint("...")]`, which keeps a test's history when you
  rename it

NUnit skips a test with `[Ignore]` before it runs, so the SDK does not record it.

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
[Configuration reference](https://docs.xping.io/configuration/configuration-reference.html).

To build the configuration in code, pass it to `Initialize` in the setup fixture:

```csharp
[OneTimeSetUp]
public void BeforeAllTests() => XpingContext.Initialize(new XpingConfiguration { BatchSize = 200 });
```

`XPING_*` environment variables still win over the values you pass. With a configuration object,
the SDK reads no `appsettings.json` file. A later `XpingContext.Initialize()` in the same process
reuses this configuration, so a long-lived process, such as an IDE's test server, keeps it across
runs. `ShutdownAsync()` forgets it.

## Error handling

An SDK failure is logged, and the tests run as usual. In `StrictMode`, a failed upload fails the
run. NUnit reports an exception from `[OneTimeTearDown]` only as a warning and exits with code 0,
so on vstest the SDK ends the test process about two seconds later, after every result has reached
`dotnet test`. On Microsoft.Testing.Platform, it sets exit code 1 after the platform writes its
reports.

## Known limitations

See [Known limitations](https://docs.xping.io/troubleshooting/known-limitations.html#nunit).

## Sample project

`samples/SampleApp.NUnit`.

## License

MIT License - Copyright (c) 2025 Xping.io
