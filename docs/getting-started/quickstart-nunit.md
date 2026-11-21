---
uid: getting-started-quickstart-nunit
title: "Quick start: NUnit"
---

# Quick start: NUnit

This page shows how to add the Xping SDK to an NUnit test project and read your first report. It
is for developers who use NUnit 3.14 or later.

---

## Prerequisites

- A test project that uses **NUnit 3.14** or later. To create one, run
  `dotnet new nunit -n MyTestProject`.
- A test project that targets .NET Framework 4.6.2 or later, or .NET 8 or later.
- The .NET 10 SDK or runtime, for the `xping` CLI.

You do not need an Xping account. Without an API key, the SDK runs in
[local-only mode](local-first.md): it writes every run to `.xping/` in your repository and makes no
network calls.

---

## Step 1: Install the package

```bash
dotnet add package Xping.Sdk.NUnit --prerelease
```

All Xping packages are prereleases, so the command needs `--prerelease`. To add the package to the
`.csproj` file instead:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.NUnit" Version="1.0.0-rc.*" />
</ItemGroup>
```

---

## Step 2: Start and end the session

Add one file to the root of your test project, for example `XpingSetup.cs`:

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

- `[assembly: XpingTrack]` tracks every test in the assembly.
- The setup fixture has no namespace, so it runs once for the whole assembly.
- `FinalizeAndShutdownAsync` ends the session, writes it to the local store and uploads what is
  left. **Without the `[OneTimeTearDown]` method, the session never ends:** the local store gets
  nothing, and Xping Cloud gets only the batches the SDK uploaded during the run.

The project includes the file by default. Do not add a `<Compile Include="..." />` item for it: an
SDK-style project already compiles every `.cs` file, and a second item stops the build with
`NETSDK1022`.

### Track only some tests

To track less than the whole assembly, leave out `[assembly: XpingTrack]` and put `[XpingTrack]`
on a test class or a test method:

```csharp
using NUnit.Framework;
using Xping.Sdk.NUnit;

[TestFixture]
[XpingTrack] // tracks every test in this class
public class CalculatorTests
{
    [Test]
    public void Add_TwoNumbers_ReturnsSum() => Assert.That(2 + 3, Is.EqualTo(5));
}
```

The setup fixture is still needed.

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

### On Microsoft.Testing.Platform

The adapter hooks into NUnit, not into the test runner, so it works the same on
[Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro).
Turn on NUnit's runner (NUnit3TestAdapter 5.0 or later) in the test project:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <EnableNUnitRunner>true</EnableNUnitRunner>
</PropertyGroup>
```

Then run the test executable, or run `dotnet test` with this `global.json` next to your solution
(.NET 10 SDK):

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

For a TRX report, reference `Microsoft.Testing.Extensions.TrxReport` and pass `--report-trx`.

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
   you turn on [strict mode](#strict-mode).

Keep the key out of your repository. Do not put it in `appsettings.json`: that file is committed
and copied into the build output. In CI, store it as a secret. See [Set up CI](ci-cd-setup.md).

The SDK keeps writing the local store in Xping Cloud mode, so `xping report` still works.

You do not name a project. Xping Cloud creates one project for each test assembly, on the first
upload. To send several assemblies to one project, set
[ProjectId](../configuration/configuration-reference.md#projectid).

### Configure in code

To build the configuration at run time, pass it to `Initialize` in the setup fixture:

```csharp
[OneTimeSetUp]
public void BeforeAllTests() => XpingContext.Initialize(new XpingConfiguration
{
    BatchSize = 200,
});
```

`XpingConfiguration` is in the `Xping.Sdk.Core.Configuration` namespace. With a configuration
object, the SDK reads no `appsettings.json` file. `XPING_*` environment variables still win over
the values you pass. See [Configuration methods](../configuration/configuration-reference.md#configuration-methods).

---

## What the adapter records

- **Parameterized tests**: each `[TestCase]` is a separate test, with its arguments.
- **Categories**: `[Category]` values are stored with each execution.
- **Ignored tests**: NUnit skips a test with `[Ignore]` before it runs, so the SDK does not record
  it. See [Known limitations](../troubleshooting/known-limitations.md#nunit).

### Keep a test's history when you rename it

Xping identifies a test by a hash of its fully qualified name. When you rename the method, the
class or the namespace, Xping sees a new test, and the history of the old name stops.

To keep the history, pin a fingerprint. Use only letters, digits, `_` and `-`, at most 100
characters:

```csharp
using Xping.Sdk.Core.Attributes;

[XpingFingerprint("checkout-happy-path-v1")]
[Test]
public void PlaceOrder_WithValidCart_Succeeds() { }
```

For a parameterized test, the SDK adds the arguments to the fingerprint, so each case stays a
separate test. Do not change the value after the first run: a new value starts a new history.

### Strict mode

In [strict mode](../configuration/configuration-reference.md#strictmode), a failed upload fails the
test run. NUnit reports an exception from `[OneTimeTearDown]` only as a warning and exits with
code 0. So on vstest the SDK ends the test process about two seconds later, after every result has
reached `dotnet test`. On Microsoft.Testing.Platform, it sets exit code 1 after the platform writes
its reports.

---

## See also

- [Set up CI](ci-cd-setup.md)
- [Configuration reference](../configuration/configuration-reference.md)
- [Known limitations](../troubleshooting/known-limitations.md#nunit)
- [Common issues](../troubleshooting/common-issues.md)
- Sample project: [samples/SampleApp.NUnit](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.NUnit)
