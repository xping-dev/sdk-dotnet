---
uid: getting-started-quickstart-xunit
title: "Quick start: xUnit"
---

# Quick start: xUnit

This page shows how to add the Xping SDK to an xUnit test project and read your first report. It
is for developers who use xUnit v2 (2.9.2 or later) or xUnit v3 (4.0.2 or later).

---

## Prerequisites

- A test project that uses **xUnit v2** 2.9.2 or later, or **xUnit v3** 4.0.2 or later.
  `dotnet new xunit -n MyTestProject` creates an xUnit v2 project. For xUnit v3, run
  `dotnet new install xunit.v3.templates`, then `dotnet new xunit3 -n MyTestProject`.
- A test project that targets .NET Framework 4.6.2 or later, or .NET 8 or later. xUnit v3 itself
  needs .NET Framework 4.7.2 or later.
- The .NET 10 SDK or runtime, for the `xping` CLI.

You do not need an Xping account. Without an API key, the SDK runs in
[local-only mode](local-first.md): it writes every run to `.xping/` in your repository and makes no
network calls.

---

## Which package

Xping has one adapter for each xUnit version. Choose the one that matches the xUnit package your
test project references:

| Your project references | Install | Test runner |
|---|---|---|
| `xunit` (2.x) | `Xping.Sdk.XUnit` | vstest |
| `xunit.v3`, `xunit.v3.mtp-off` or another `xunit.v3.*` package | `Xping.Sdk.XUnit.V3` | Microsoft.Testing.Platform or vstest |

To check, look for `<PackageReference Include="xunit"` or `Include="xunit.v3` in the `.csproj`
file, or run:

```bash
dotnet list package | grep -i xunit
```

With the wrong adapter, the build stops with `XPING001` or `XPING002`, and the message names the
package to use. See
[Wrong xUnit adapter](../troubleshooting/common-issues.md#wrong-xunit-adapter-xping001-xping002).

---

## Step 1: Install the package

The tabs on this page stay in sync: choose your xUnit version once.

# [xUnit v2](#tab/xunit-v2)

```bash
dotnet add package Xping.Sdk.XUnit --prerelease
```

Or in the `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.XUnit" Version="1.0.0-rc.*" />
</ItemGroup>
```

# [xUnit v3](#tab/xunit-v3)

```bash
dotnet add package Xping.Sdk.XUnit.V3 --prerelease
```

Or in the `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.XUnit.V3" Version="1.0.0-rc.*" />
</ItemGroup>
```

***

All Xping packages are prereleases, so the command needs `--prerelease`.

---

## Step 2: Turn on the Xping test framework

Add one line to an `AssemblyInfo.cs` file at the root of your test project. Create the file if you
do not have one.

# [xUnit v2](#tab/xunit-v2)

```csharp
using Xunit;

[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

# [xUnit v3](#tab/xunit-v3)

```csharp
using Xunit;

[assembly: TestFramework(typeof(Xping.Sdk.XUnit.V3.XpingTestFramework))]
```

xUnit v3 takes the type of the framework, not its name.

***

xUnit then runs your tests through the Xping test framework, which records every test in the
assembly. You do not add attributes to your tests, and you do not write a setup class: the
framework starts the session before the first test and ends it after the last one.

---

## Step 3: Run your tests

```bash
dotnet test
```

When you run `dotnet test` in a terminal, the SDK prints one line when the session starts:

```
[Xping 17:28:04 INF] Initialized in local-only mode. SessionId: "6bec32b0-922b-4b9b-bcb9-fb4e1056180f", Environment: Default. No API key configured - results stay on this machine.
```

When the output goes to a file or a pipe, for example in a CI log, `dotnet test` shows the SDK's
lines only with `--logger "console;verbosity=normal"`.

On xUnit v3, `dotnet test` does not show the SDK's lines. To see them, run the test project
itself with `dotnet run`.

### xUnit v3 test runners

An xUnit v3 test project is an executable. The SDK records it on each runner:

- **Microsoft.Testing.Platform**: run `dotnet test` with
  `{ "test": { "runner": "Microsoft.Testing.Platform" } }` in `global.json`. Or run the project with
  `dotnet run` when it sets `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`.
- **vstest**: run `dotnet test` without that `global.json` setting, through
  `xunit.runner.visualstudio`. On the .NET 10 SDK, vstest does not run a Microsoft.Testing.Platform
  app, so reference `xunit.v3.mtp-off` instead of `xunit.v3`.

---

## Step 4: Read the report

Run your tests at least **5 times**. Each test also needs at least 5 runs of its own. With fewer
runs, the report shows the counts but no findings.

Install the CLI, then read the report:

```bash
dotnet tool install -g Xping.Cli --prerelease
xping report
```

<!-- xping:sample docs-xunit -->
````
Xping · MyTestProject · 9 runs · 2026-08-20 07:52 → 09:02 · main@bdbafba
17 tests · 15 healthy · 2 flagged

```
NEEDS ATTENTION (2 high)                               most severe first
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    SampleTests.FlakyTest_PassesOnRetry
    failed 9 of 18 executions (50%) in 9 of 9 runs, 1 failure mode
    evidence adequate | -env-cluster | f_2b84a621 | ...SampleTests.cs:96

2.  HIGH  always failing
    SampleTests.ThrowingTestIsTracked
    failed 9 of 9 executions (100%), one failure mode:
    System.InvalidOperationException
    evidence sparse | -env-cluster | f_c1774d82 | .../SampleTests.cs:65
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/command-reference.html#the-population-marker

Detail of row 1: xping report --id f_2b84a621 --assembly MyTestProject
````

[Run without an account](local-first.md#how-to-read-it) explains each part of a finding.
`xping report --format json` gives the same findings as JSON, for a script or a coding agent. The
[command reference](../cli/command-reference.md) lists every option.

---

## Upload to Xping Cloud

Xping Cloud adds confidence scores, flaky-test detection and pull request comments, across CI and
every machine. Access is by invitation.

1. In Xping Cloud, open **Settings → API keys** and create a key with only the Upload scope.
2. Set the key as an environment variable:

   ```bash
   export XPING_APIKEY="your-api-key"        # Linux and macOS
   $env:XPING_APIKEY="your-api-key"          # PowerShell
   set XPING_APIKEY=your-api-key             # Command Prompt
   ```

3. Run `dotnet test`. The start line no longer says local-only:

   ```
   [Xping 09:14:02 INF] Initialized. SessionId: "0f3c9a1e-5b7d-4c2a-9e61-2d8b4f7a3c10", Environment: Default
   ```

   The SDK prints one line for each upload that Xping Cloud accepts. The upload at the end of the
   session prints:

   ```
   [Xping 09:14:05 INF] Finalization receipt 7c1e04b2 (312ms, 4.6 KB)
   ```

   An upload during the session prints `Published 100 tests in 287ms (38.2 KB) · receipt 3a9d51f0`.
   When an upload fails, the SDK prints an error that says why. The test run does not fail, unless
   you turn on [strict mode](../configuration/configuration-reference.md#strictmode).

Keep the key out of your repository. Do not put it in `appsettings.json`: that file is committed
and copied into the build output. In CI, store it as a secret. See [Set up CI](ci-cd-setup.md).

The SDK keeps writing the local store in Xping Cloud mode, so `xping report` still works.

You do not name a project. Xping Cloud creates one project for each test assembly, on the first
upload. To send several assemblies to one project, set
[ProjectId](../configuration/configuration-reference.md#projectid).

### Configure in code

The Xping test framework calls `XpingContext.Initialize()` itself. To pass a configuration object
instead, call `Initialize` first, from a module initializer:

```csharp
using System.Runtime.CompilerServices;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.XUnit; // Xping.Sdk.XUnit.V3 on xUnit v3

internal static class XpingModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize() => XpingContext.Initialize(new XpingConfiguration
    {
        BatchSize = 200,
    });
}
```

`[ModuleInitializer]` needs .NET 5 or later. With a configuration object, the SDK reads no
`appsettings.json` file. `XPING_*` environment variables still win over the values you pass. See
[Configuration methods](../configuration/configuration-reference.md#configuration-methods).

---

## What the adapter records

- **Theories**: each data row is a separate test, with its arguments.
- **Traits**: `[Trait]` values are stored with each execution.
- **Collections**: each execution records the name of its test collection.
- **Skipped tests**: a test with `Skip` is recorded as skipped.
- **xRetry.v3 retries**: xRetry.v3 reports only the attempt that a retried test ended on, so the SDK
  records only that attempt. See
  [Known limitations](../troubleshooting/known-limitations.md#xunit-v3-records-only-the-last-retry-attempt).

### Keep a test's history when you rename it

Xping identifies a test by a hash of its fully qualified name. When you rename the method, the
class or the namespace, Xping sees a new test, and the history of the old name stops.

To keep the history, pin a fingerprint. Use only letters, digits, `_` and `-`, at most 100
characters:

```csharp
using Xping.Sdk.Core.Attributes;

[XpingFingerprint("checkout-happy-path-v1")]
[Fact]
public void PlaceOrder_WithValidCart_Succeeds() { }
```

For a theory, the SDK adds the arguments to the fingerprint, so each data row stays a separate
test. Do not change the value after the first run: a new value starts a new history.

---

## See also

- [Set up CI](ci-cd-setup.md)
- [Configuration reference](../configuration/configuration-reference.md)
- [Known limitations](../troubleshooting/known-limitations.md)
- [Common issues](../troubleshooting/common-issues.md)
- Sample projects: [samples/SampleApp.XUnit](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnit) (v2),
  [samples/SampleApp.XUnitV3](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnitV3) (v3)
