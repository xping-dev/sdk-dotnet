# Xping.Sdk.XUnit.V3

**For xUnit v3 (the `xunit.v3` package).** Using xUnit v2 (`xunit` 2.x)? Use
[`Xping.Sdk.XUnit`](../Xping.Sdk.XUnit/README.md) instead; a build that pairs this package with
xUnit v2 stops with `XPING002`.

xUnit v3 test framework adapter for the Xping SDK. Records every test execution, on
Microsoft.Testing.Platform and on vstest, to the local store and, with an API key, to Xping Cloud.

## Installation

```bash
dotnet add package Xping.Sdk.XUnit.V3
```

## Quick Start

Add to `AssemblyInfo.cs` (or any file in the test project):

```csharp
[assembly: Xunit.TestFramework(typeof(Xping.Sdk.XUnit.V3.XpingTestFramework))]
```

That's it: every test in the assembly is recorded, and the session ends when the assembly finishes.
Write tests as usual — `[Fact]`, `[Theory]`, traits, fixtures, `Assert.Skip`.

## Running

An xUnit v3 test project is an executable. Xping records it however it runs:

| How | Setup |
|-----|-------|
| `dotnet test` on Microsoft.Testing.Platform | `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` |
| `dotnet run` / the test executable | `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` |
| `dotnet test` on vstest | `xunit.runner.visualstudio`, and `xunit.v3.mtp-off` instead of `xunit.v3` on the .NET 10 SDK |

Each session records which runner it ran on (`Xping.Runner`) and `Xping.TestFramework = xUnit.v3`.

## What Is Recorded

- Outcome: passed, failed, skipped (including `Assert.Skip` at run time), and timeout for a test that
  overran its `[Fact(Timeout = …)]`, with the declared budget
- Duration, start and end time, from xUnit's own messages
- Failure message, exception type and stack trace (see `CaptureStackTraces`), and where in the
  lifecycle the test failed: a fixture of any scope, the constructor, `InitializeAsync`, the body, or
  `Dispose`
- Traits: `Category` as categories, the first `Description` as the description, the rest as tags
- Each theory row as its own test, identified by the arguments in its display name
- Source file and line, and a fingerprint pinned with `[XpingFingerprint]`
- Retries by xRetry.v3: the attempt the test ended on, with its number
- Discovered and selected test case counts, which tell a filtered run from one after tests were deleted

Explicit tests the run leaves out are not recorded.

## Configuration

Configuration is the same as for every Xping adapter: `XPING_*` environment variables,
`appsettings.json`, or `XpingContext.Initialize(config)` from a module initializer. See the
[Configuration Reference](https://docs.xping.io/configuration/configuration-reference.html).

```csharp
using System.Runtime.CompilerServices;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.XUnit.V3;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize() =>
        XpingContext.Initialize(new XpingConfiguration { ApiKey = Environment.GetEnvironmentVariable("XPING_APIKEY") });
}
```

## Error Handling

An SDK failure is logged and the tests run untracked. In `StrictMode` a configuration error ends the
process before any test runs, and an upload error fails the run while keeping every result: as an
error message on vstest, and through the exit code on Microsoft.Testing.Platform.

## Known Limitations

- xRetry.v3 reports only the attempt a retried test ended on: the attempts before it are not
  recorded, and the duration covers all of them.
- Two theory rows whose arguments display the same way share one identity.
- The runner is not recorded under xUnit's own console runner.

See [Known Limitations](https://docs.xping.io/known-limitations.html#xunit-v3).

## Sample Project

`samples/SampleApp.XUnitV3` runs on vstest by default and on Microsoft.Testing.Platform with
`-p:XpingTestRunner=MTP`.

## License

MIT License - Copyright (c) 2026 Xping.io
