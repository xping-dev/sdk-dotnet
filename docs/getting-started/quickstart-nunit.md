# Quick Start: NUnit

Get started with Xping SDK in your NUnit test projects in less than 5 minutes. This guide will walk you through installation, configuration, and your first tracked test.

---

## What You'll Learn

- How to install Xping SDK for NUnit
- How to run local-only, and how to add cloud upload later
- How to track your first test
- How to verify results in Xping Cloud

---

## Prerequisites

Before you begin, make sure you have:

- **.NET Framework 4.6.1+**, **.NET Core 2.0+**, or **.NET 5+** installed ([Download](https://dotnet.microsoft.com/download))
  - Xping SDK targets .NET Standard 2.0 for broad compatibility
- **No Xping account needed.** Without an API key the SDK runs [local-only](local-first.md): every run is recorded to `.xping/` in your repo and no network call is made. An account is required only to upload to Xping Cloud, which is currently invite-only.
- An existing **NUnit test project** or create a new one

> **New to NUnit?** Create a test project with: `dotnet new nunit -n MyTestProject`

---

## Step 1: Installation

Install the Xping SDK NUnit adapter package in your test project:

### Using .NET CLI

```bash
dotnet add package Xping.Sdk.NUnit
```

### Using Package Manager Console

```powershell
Install-Package Xping.Sdk.NUnit
```

### Using PackageReference

Add to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.NUnit" Version="1.0.*" />
</ItemGroup>
```

---

## Step 2: Configuration

**Skip this step if you are starting local-only.** With no API key configured, the SDK records
every run to `.xping/` in your repository and makes no network calls — see
[Running Without an Account](local-first.md). Come back here when you want to upload to Xping Cloud.

### Option A: Environment Variable (Recommended)

The API key is a credential. Keep it out of your repository — set it in your shell on a dev
machine, and as a secret in CI:

```bash
# Linux/macOS
export XPING_APIKEY="your-api-key-here"

# Windows (PowerShell)
$env:XPING_APIKEY="your-api-key-here"

# Windows (Command Prompt)
set XPING_APIKEY=your-api-key-here
```

That is the whole setup. Every other setting has a working default.

> **Getting Your API Key:**
> 1. Log in to [Xping Cloud](https://app.xping.io)
> 2. Navigate to **Account** → **Settings** → **API & Integration**
> 3. Click **Create API Key** and copy it
>
> Xping Cloud is currently invite-only. [Request access](https://xping.io/contact?pilot=True), or
> keep working locally — that stays free and account-free.

For CI, store it as a pipeline secret rather than a plain variable. See
[CI/CD Integration](ci-cd-setup.md).

### Option B: appsettings.json (Non-Secret Settings Only)

`appsettings.json` is the right place for tuning — batch size, flush interval, environment name —
and the wrong place for the API key:

```json
{
  "Xping": {
    "Enabled": true,
    "BatchSize": 100,
    "CaptureStackTraces": true
  }
}
```

> **⚠️ Do not put `ApiKey` here.** `appsettings.json` is committed to source control and copied
> into your build output, so a key placed in it leaks to everyone with repository access and into
> every build artifact. The SDK *will* read it from there — it just should not have to. Use
> `XPING_APIKEY` instead.

Make sure the file is copied to output directory by adding this to your `.csproj`:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Settings resolve in this order, highest first: `XPING_*` environment variables →
`Xping__*` environment variables → `appsettings.{Environment}.json` → `appsettings.json`.

### About your project

You do not name it. Xping derives the project from the test assembly each execution belongs to, so
a test project called `PaymentService.Tests` reports into a project of that name, created
automatically on the first run. A solution with several test projects gets one Xping project each.

`ProjectId` is **optional** and exists only to override that — set it when several test assemblies
should report into a single project, for example in a monorepo. It is a hard pin: every execution
in the session lands in that project regardless of which assembly it came from.

```bash
export XPING_PROJECTID="payment-platform"   # optional; only to merge assemblies
```

See [ProjectId](../configuration/configuration-reference.md#projectid) in the Configuration
Reference.

### Option C: Programmatic Configuration

Pass a configuration object to `Initialize` from your setup fixture. Read the key from the
environment — never a literal:

```csharp
[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        var config = new XpingConfiguration
        {
            ApiKey = Environment.GetEnvironmentVariable("XPING_APIKEY"),
            BatchSize = 200
        };

        XpingContext.Initialize(config);
    }
}
```

> Passing a configuration object replaces the file-and-environment pipeline for that session — the
> SDK uses exactly what you hand it. Only reach for this when you need settings computed at
> runtime; `XPING_APIKEY` alone covers the normal case.

---

## Step 3: Set Up Global Tracking

Create a setup fixture to initialize and dispose of the Xping context. This ensures proper resource management:

```csharp
using NUnit.Framework;
using Xping.Sdk.Core;

namespace MyTestProject;

[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void BeforeAllTests()
    {
        // Initialize Xping SDK
        XpingContext.Initialize();
    }

    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        // Finalize the session (flushes and uploads what is left), then release resources
        await XpingContext.FinalizeAsync().ConfigureAwait(false);
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
    }
}
```

> **Important:** Place this file at the root of your test namespace to ensure it runs once per test assembly.

`FinalizeAsync` closes the session and delivers everything still buffered; `ShutdownAsync` disposes
the host. Both are required — skipping `FinalizeAsync` loses the last batch. (`FlushAsync` also
exists, but it flushes mid-session and does not close the run; you rarely need it.)

---

## Step 4: Track Your Tests

Apply the `[XpingTrack]` attribute at the assembly, class, or method level depending on your needs:

### Assembly-Level Tracking (Most Convenient)

Track all tests in your entire test project by adding the attribute to an `AssemblyInfo.cs` file:

```csharp
using Xping.Sdk.NUnit;

// Track all tests in this assembly
[assembly: XpingTrack]
```

Create this file at the root of your test project. Make sure it's included in your `.csproj`:

```xml
<ItemGroup>
  <Compile Include="AssemblyInfo.cs" />
</ItemGroup>
```

> **💡 Tip:** This is the most convenient option for comprehensive test tracking. All current and future tests in the project will be automatically tracked without additional attributes.

### Class-Level Tracking

Track all tests in a specific fixture:

```csharp
using NUnit.Framework;
using Xping.Sdk.NUnit;

namespace MyTestProject;

[TestFixture]
[XpingTrack] // Tracks all tests in this class
public class CalculatorTests
{
    [Test]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(2, 3);

        // Assert
        Assert.That(result, Is.EqualTo(5));
    }

    [Test]
    public void Divide_ByZero_ThrowsException()
    {
        // Arrange
        var calculator = new Calculator();

        // Act & Assert
        Assert.Throws<DivideByZeroException>(() => 
            calculator.Divide(10, 0));
    }
}
```

### Method-Level Tracking

Track specific tests:

```csharp
[TestFixture]
public class UserServiceTests
{
    [Test]
    [XpingTrack] // Track only this test
    [Category("Integration")]
    public async Task GetUser_ValidId_ReturnsUser()
    {
        // Arrange
        var service = new UserService();

        // Act
        var user = await service.GetUserAsync(123);

        // Assert
        Assert.That(user, Is.Not.Null);
        Assert.That(user.Id, Is.EqualTo(123));
    }

    [Test]
    public void SomeOtherTest_NotTracked()
    {
        // This test won't be tracked
        Assert.Pass();
    }
}
```

### Pinning a Stable Test Fingerprint

By default, Xping identifies each test by a SHA256 hash of its fully qualified name. Renaming a method, class, or namespace causes the platform to treat it as a brand-new test, losing all historical trend data.

`XpingFingerprintAttribute` lets you pin a stable identifier to a test method. When present, Xping uses that value instead of computing a hash. The fingerprint value must use only URL-safe characters (`[a-zA-Z0-9_-]`):

```csharp
using Xping.Sdk.Core.Attributes;

[XpingFingerprint("checkout-happy-path-v1")]
[Test]
public void PlaceOrder_WithValidCart_Succeeds() { ... }
```

For parameterized tests, the pinned value is automatically combined with a value of the case parameters, keeping each variant distinct on the platform:

```csharp
[XpingFingerprint("login-v1")]
[TestCase("admin", true)]
[TestCase("user", false)]
public void Login_ShouldSucceed(string role, bool expected) { ... }
// Automatically produces: "login-v1:admin,true" and "login-v1:user,false"
```

> **💡 Tip:** Once you publish a run with a pinned fingerprint, treat that value as permanent. Renaming the method is safe—that is the whole point—but changing the attribute value itself severs the link to all historical data for that test.

---

## Step 5: Run Your Tests

Run your tests as you normally would:

```bash
dotnet test
```

You should see output similar to:

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 123 ms
```

Xping SDK runs silently in the background, tracking execution data without affecting your test results.

---

## Step 6: Read the Results

Run your suite **more than once** before reading anything. One run tells you nothing about
reliability; cross-run flakiness detection needs at least three.

### Locally — `xping report`

Install the CLI once, then ask what happened:

```bash
dotnet tool install -g Xping.Cli      # puts `xping` on your PATH; needs .NET 10
xping report
```

```
Xping · MyTestProject · 9 runs · 2026-08-20 07:52 → 09:02 · main@bdbafba
2 findings (2 high) · 17 tests · 15 healthy

HIGH  flaky            FlakyTest_PassesOnRetry
      failed 9 of 18 executions (50%) in 9 of 9 runs, 1 failure mode
      evidence moderate | f_2b84a621

HIGH  always failing   ThrowingTestIsTracked
      failed 9 of 9 executions (100%), one failure mode:
      System.InvalidOperationException
      evidence low | f_c1774d82
```

`xping report --format json` emits the same findings as a versioned envelope, which is what you
hand an agent. See the [CLI Command Reference](../cli/command-reference.md) for every flag and
finding kind.

### In Xping Cloud — with an API key

1. Open [Xping Cloud](https://app.xping.io)
2. Explore your test data across multiple tabs:
   - **Test Sessions** - View uploaded test runs with execution statistics, environment details, and duration
   - **Tests** - Browse all tests with confidence scores, success rates, and execution history
   - **Flaky Tests** - Identify unreliable tests that need attention

Cloud adds what a single machine cannot supply: confidence scores, evidence sufficiency,
root-cause categorisation, and trends across CI, branches, and teammates.

> **Learn More:** For detailed information about navigating Xping Cloud, filtering tests, and understanding the test detail view, see [Navigating Xping Cloud](../guides/getting-started/navigating-xping-cloud.md).

---

## Common Patterns

### Parameterized Tests

Xping automatically tracks parameterized tests with their arguments:

```csharp
[TestFixture]
[XpingTrack]
public class MathTests
{
    [Test]
    [TestCase(1, 1, 2)]
    [TestCase(2, 3, 5)]
    [TestCase(-1, 1, 0)]
    public void Add_VariousInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.That(result, Is.EqualTo(expected));
    }
}
```

Each test case is tracked separately with its parameter values captured in metadata.

### Async Tests

Xping fully supports async test methods:

```csharp
[TestFixture]
[XpingTrack]
public class ApiTests
{
    [Test]
    public async Task FetchData_ValidEndpoint_ReturnsData()
    {
        var client = new HttpClient();
        var response = await client.GetAsync("https://api.example.com/data");
        
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }
}
```

### Custom Metadata with Categories

Use NUnit's `[Category]` attribute to organize tests:

```csharp
[TestFixture]
[XpingTrack]
[Category("Integration")]
public class DatabaseTests
{
    [Test]
    [Category("Smoke")]
    [Category("Critical")]
    public void Connection_DatabaseAvailable_Connects()
    {
        // Test implementation
        Assert.Pass();
    }
}
```

Categories are automatically captured and visible in Xping Cloud for filtering and analysis.

---

## Known Limitations

> **⚠️ Important:** Tests marked with `[Ignore]` attribute are not tracked by Xping.
>
> This is because NUnit skips ignored tests before execution begins, and the tracking hooks are never invoked.
> Only tests that actually execute will be tracked.
>
> For more details, see [Known Limitations](../known-limitations.md#nunit).

---

## Troubleshooting

If you encounter issues while integrating or using the Xping SDK with NUnit, we have comprehensive troubleshooting resources available:

### Common Issues

- **Tests not appearing in Xping Cloud** - Configuration, attribute placement, and connectivity checks
- **Data looks incomplete** - Setup fixture and flush timing
- **Attribute not working** - Proper placement of `[XpingTrack]` attribute
- **Performance concerns** - Impact measurement and optimization

### Get Help

For detailed troubleshooting steps and solutions:

- **[Common Issues](../troubleshooting/common-issues.md)** - Frequently encountered problems and solutions
- **[Debugging Guide](../troubleshooting/debugging.md)** - Enable logging and diagnose SDK behavior

Still stuck? Reach out through our support channels listed in the "Need Help?" section below.

---

## Next Steps

🎉 **Congratulations!** You've successfully integrated Xping SDK with NUnit.

Now explore more features:

- **[CI/CD Integration](ci-cd-setup.md)** - Integrate with GitHub Actions, Azure DevOps, and more
- **[Configuration Reference](../configuration/configuration-reference.md)** - Advanced configuration options
- **[Understanding Confidence Scores](../guides/getting-started/understanding-confidence-scores.md)** - Learn about test reliability scoring
- **[Performance Overview](../guides/optimization/performance-overview.md)** - Understanding performance, optimization, and tuning settings
- **[Known Limitations](../known-limitations.md)** - Framework-specific constraints and workarounds

---

## Sample Project

For a complete working example, check out our sample project:

📂 [samples/SampleApp.NUnit](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.NUnit)

---

## Need Help?

- 📚 [Documentation](https://docs.xping.io)
- 💬 [Community Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)
- 🐛 [Report an Issue](https://github.com/xping-dev/sdk-dotnet/issues)
- 📧 [Email Support](mailto:support@xping.io)

---

**Happy Testing!** 🚀
