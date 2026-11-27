# Xping.Sdk.XUnit.V3

**For xUnit v3 (the `xunit.v3` package, 4.0.2 or later).** Using xUnit v2 (`xunit` 2.x)? Use
[`Xping.Sdk.XUnit`](../Xping.Sdk.XUnit/README.md) instead. A build that pairs this package with
xUnit v2 stops with `XPING002`.

xUnit v3 adapter for the Xping SDK. It records each test execution, on Microsoft.Testing.Platform
and on vstest, to the local store and, with an API key, uploads it to Xping Cloud. The test project
must target .NET Framework 4.7.2 or later, or .NET 8 or later.

## Installation

```bash
dotnet add package Xping.Sdk.XUnit.V3 --prerelease
```

All Xping packages are prereleases, so the command needs `--prerelease`.

## Quick start

Add one line to `AssemblyInfo.cs`, or to any file in the test project:

```csharp
[assembly: Xunit.TestFramework(typeof(Xping.Sdk.XUnit.V3.XpingTestFramework))]
```

Every test in the assembly is then recorded, and the session ends when the assembly finishes. Write
tests as usual: `[Fact]`, `[Theory]`, traits, fixtures and `Assert.Skip` all work.

`dotnet test` does not show the SDK's log lines for an xUnit v3 project. To see them, run the test
project itself with `dotnet run`.

The full guide is [Quick start: xUnit](https://docs.xping.io/sdk/getting-started/quickstart-xunit.html).

## Running

An xUnit v3 test project is an executable. Xping records it however it runs:

| How | Setup |
|-----|-------|
| `dotnet test` on Microsoft.Testing.Platform | `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` |
| `dotnet run` / the test executable | `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` |
| `dotnet test` on vstest | `xunit.runner.visualstudio`, and `xunit.v3.mtp-off` instead of `xunit.v3` on the .NET 10 SDK |

Each session records which runner it ran on (`Xping.Runner`) and `Xping.TestFramework = xUnit.v3`.

## What is recorded

- Outcome: passed, failed, skipped (including `Assert.Skip` at run time), and timeout for a test that
  overran its `[Fact(Timeout = …)]`, with the declared budget
- Duration, start and end time, from xUnit's own messages
- Failure message, exception type and stack trace (uploaded only with `UploadStackTraces`), and where in the
  lifecycle the test failed: a fixture of any scope, the constructor, `InitializeAsync`, the body, or
  `Dispose`
- Traits: `Category` as categories, the first `Description` as the description, the rest as tags
- Each theory row as its own test, identified by the arguments in its display name
- Source file and line, and a fingerprint pinned with `[XpingFingerprint]`
- Retries by xRetry.v3: the attempt the test ended on, with its number
- Discovered and selected test case counts, which tell a filtered run from one after tests were deleted

Explicit tests the run leaves out are not recorded.

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

To build the configuration in code, call `Initialize` from a module initializer (.NET 5 or later):

```csharp
using System.Runtime.CompilerServices;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.XUnit.V3;

internal static class XpingModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize() =>
        XpingContext.Initialize(new XpingConfiguration { BatchSize = 200 });
}
```

`XPING_*` environment variables still win over the values you pass. With a configuration object,
the SDK reads no `appsettings.json` file. A later `XpingContext.Initialize()` in the same process
reuses this configuration, so a long-lived process, such as an IDE's test server, keeps it across
runs. `ShutdownAsync()` forgets it.

## Error handling

An SDK failure is logged and the tests run untracked. In `StrictMode` a configuration error ends the
process before any test runs, and an upload error fails the run while keeping every result: as an
error message on vstest, and through the exit code on Microsoft.Testing.Platform.

## Known limitations

- xRetry.v3 reports only the attempt a retried test ended on: the attempts before it are not
  recorded, and the duration covers all of them.
- On Microsoft.Testing.Platform with `--report-trx`, a timed-out test that passed
  `TestContext.Current.CancellationToken` to its hung call can crash the TRX report (exit 7). This is
  an xUnit v3 bug ([xunit/xunit#3669](https://github.com/xunit/xunit/issues/3669)).
- Two theory rows whose arguments display the same way share one identity.
- The runner is not recorded under xUnit's own console runner.

See [Known Limitations](https://docs.xping.io/sdk/troubleshooting/known-limitations.html#xunit-v3).

## Sample project

`samples/SampleApp.XUnitV3` runs on vstest by default and on Microsoft.Testing.Platform with
`-p:XpingTestRunner=MTP`.

## License

MIT License - Copyright (c) 2026 Xping.io
