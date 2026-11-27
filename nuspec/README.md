<!-- 
  This README is specifically designed for NuGet package display.
  It uses absolute URLs and formatting optimized for NuGet.org rendering.
  For the GitHub repository README, see /README.md in the root directory.
-->

# Xping SDK for .NET

The Xping SDK records every `dotnet test` run, including the runs on your own machine. With that
history, the `xping` CLI tells you which tests behave consistently and which are flaky or broken.
Recording and local reports are free, need no account and make no network calls.

## Features

- 🔍 **Test execution tracking**: records the outcome, duration and environment of each test
- 📊 **Local reports**: the `xping` CLI reports flaky, failing, slow and stopped tests from the run history
- 🌐 **CI detection**: detects the CI platform, build, branch and pull request
- ☁️ **Xping Cloud (optional)**: confidence scores, flaky detection and pull request comments
- 🎯 **Frameworks**: NUnit, xUnit v2, xUnit v3 and MSTest

## Installation

All Xping packages are prereleases, so each install command needs `--prerelease`. Choose the
package for your test framework:

### NUnit

```bash
dotnet add package Xping.Sdk.NUnit --prerelease
```

### xUnit

Match the xUnit package that your project references:

```bash
dotnet add package Xping.Sdk.XUnit --prerelease      # xUnit v2: the project references xunit
dotnet add package Xping.Sdk.XUnit.V3 --prerelease   # xUnit v3: the project references xunit.v3
```

### MSTest

```bash
dotnet add package Xping.Sdk.MSTest --prerelease
```

To read the history, install the CLI:

```bash
dotnet tool install -g Xping.Cli --prerelease
```

## Quick start

### NUnit

Add one file. It tracks every test in the assembly, and its setup fixture starts and ends the Xping
session. Without the `[OneTimeTearDown]` method, the session never ends and nothing is written.

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

### xUnit

Add one line to `AssemblyInfo.cs`:

```csharp
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")] // xUnit v2
// [assembly: TestFramework(typeof(Xping.Sdk.XUnit.V3.XpingTestFramework))]       // xUnit v3
```

### MSTest

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
public class CalculatorTests : XpingTestBase
{
    [TestMethod]
    public void Add_TwoNumbers_ReturnsSum()
    {
        Assert.AreEqual(5, 2 + 3);
    }
}
```

### Read the report

Run your tests at least 5 times, then:

```bash
xping report
```

## Configuration

Without an API key, the SDK writes the history to `.xping/` in your repository and makes no network
calls. To upload to Xping Cloud, set an API key as an environment variable. Create the key in
Xping Cloud under **Settings → API keys**, with only the Upload scope. Never put it in
`appsettings.json`, which is committed and copied into the build output.

```bash
export XPING_APIKEY="your-api-key"
```

Other settings can go in `appsettings.json`. `XPING_*` environment variables win over it:

```json
{
  "Xping": {
    "Enabled": true,
    "BatchSize": 100,
    "FlushInterval": "00:00:30"
  }
}
```

For every option, see the
[Configuration reference](https://docs.xping.io/sdk/configuration/configuration-reference.html).

## Requirements

- .NET Framework 4.6.2 or later, or .NET 8 or later (the packages target .NET Standard 2.0)
- NUnit 3.14 or later, xUnit 2.9.2 or later (`Xping.Sdk.XUnit`), xUnit v3 4.0.2 or later
  (`Xping.Sdk.XUnit.V3`), or MSTest 3.8 or later
- The .NET 10 SDK or runtime, for the `xping` CLI

## Documentation

- **Documentation**: https://docs.xping.io
- **Get started**: https://docs.xping.io/sdk/index.html#quick-start
- **Run without an account**: https://docs.xping.io/sdk/getting-started/local-first.html
- **GitHub repository**: https://github.com/xping-dev/sdk-dotnet

## Support

- **Issues**: https://github.com/xping-dev/sdk-dotnet/issues
- **Discussions**: https://github.com/xping-dev/sdk-dotnet/discussions

## License

Distributed under the MIT License. See the `LICENSE` file for more information.
