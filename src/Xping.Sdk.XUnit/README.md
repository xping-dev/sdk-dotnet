# Xping.Sdk.XUnit

**For xUnit v2 (the `xunit` package, 2.9.2 or later).** Using xUnit v3 (`xunit.v3`)? Use
[`Xping.Sdk.XUnit.V3`](../Xping.Sdk.XUnit.V3/README.md) instead. A build that pairs this package with
xUnit v3 stops with `XPING001`.

xUnit v2 adapter for the Xping SDK. It records each test execution to the local store and, with an
API key, uploads it to Xping Cloud. The test project must target .NET Framework 4.6.2 or later, or
.NET 8 or later.

## Installation

```bash
dotnet add package Xping.Sdk.XUnit --prerelease
```

All Xping packages are prereleases, so the command needs `--prerelease`.

## Quick start

Add one line to `AssemblyInfo.cs`, or to any file in the test project:

```csharp
[assembly: Xunit.TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

xUnit then runs your tests through the Xping test framework, which records every test in the
assembly. You do not add attributes to your tests, and you do not write a setup class: the
framework starts the session before the first test and ends it after the last one.

When you run `dotnet test` in a terminal, the SDK prints a line when the session starts, and with
an API key one line for each upload that Xping Cloud accepts:

```
[Xping 17:28:04 INF] Initialized in local-only mode. SessionId: "6bec32b0-922b-4b9b-bcb9-fb4e1056180f", Environment: Default. No API key configured - results stay on this machine.
```

When the output goes to a file or a pipe, for example in a CI log, `dotnet test` shows these lines
only with `--logger "console;verbosity=normal"`.

The full guide is [Quick start: xUnit](https://docs.xping.io/sdk/getting-started/quickstart-xunit.html).

## What is recorded

- The outcome: passed, failed or skipped, and timeout for a test that ran past its
  `[Fact(Timeout = ...)]`, with the declared budget
- The duration, the start and end times, and the error message and stack trace of a failure. The
  stack trace text is uploaded only with `UploadStackTraces`.
- Each theory row as a separate test, with its arguments
- Traits: `Category` as categories, the first `Description` as the description, and every other
  trait as a `key:value` tag. Later `Description` values are dropped.
- The name of the test collection
- The output the test wrote to `ITestOutputHelper`
- The source file and line of the test
- A fingerprint pinned with `[XpingFingerprint("...")]`, which keeps a test's history when you
  rename it

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

To build the configuration in code, call `Initialize` from a module initializer (.NET 5 or later).
The Xping test framework runs the test assembly's module initializers before it starts the session:

```csharp
using System.Runtime.CompilerServices;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.XUnit;

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

An SDK failure is logged, and the tests run untracked. In `StrictMode`, a configuration error ends
the process before any test runs, and a failed upload fails the run.

## Known limitations

See [Known limitations](https://docs.xping.io/sdk/troubleshooting/known-limitations.html#xunit).

## Sample project

`samples/SampleApp.XUnit`.

## License

MIT License - Copyright (c) 2025 Xping.io
