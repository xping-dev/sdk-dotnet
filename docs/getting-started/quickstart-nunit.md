# Quick Start: NUnit

Get started with Xping SDK in your NUnit test projects in less than 5 minutes. This guide will walk you through installation, configuration, and your first tracked test.

---

## What You'll Learn

- How to install Xping SDK for NUnit
- How to configure your API credentials
- How to track your first test
- How to verify results in the Xping dashboard

---

## Prerequisites

Before you begin, make sure you have:

- **.NET Framework 4.6.1+**, **.NET Core 2.0+**, or **.NET 5+** installed ([Download](https://dotnet.microsoft.com/download))
  - Xping SDK targets .NET Standard 2.0 for broad compatibility
- An **Xping account** with API credentials ([Sign up](https://app.xping.io))
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

Configure Xping with your API credentials. There are three ways to configure:

### Option A: Configuration File (Recommended)

Create or update `appsettings.json` in your test project:

```json
{
  "Xping": {
    "ApiKey": "your-api-key-here",
    "ProjectId": "your-project-id-here",
    "Enabled": true
  }
}
```

> **Getting Your API Key:**
> 1. Log in to [Xping Dashboard](https://app.xping.io)
> 2. Navigate to **Account** → **Settings** → **API & Integration**
> 3. Click **Create API Key** and copy it
>
> **About Project ID:**
> The `ProjectId` is a user-defined identifier for your project (e.g., `"my-app"`, `"payment-service"`). 
> Choose any meaningful name—Xping will automatically create the project in your workspace when your tests first run. 
> The platform requires that project names are unique within your workspace. Check [Configuration Reference](../configuration/configuration-reference.md#projectid) for more information.

Make sure the file is copied to output directory by adding this to your `.csproj`:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Option B: Environment Variables

Set environment variables (useful for CI/CD):

```bash
# Linux/macOS
export XPING__APIKEY="your-api-key-here"
export XPING__PROJECTID="your-project-id-here"
export XPING__ENABLED="true"

# Windows (PowerShell)
$env:XPING__APIKEY="your-api-key-here"
$env:XPING__PROJECTID="your-project-id-here"
$env:XPING__ENABLED="true"

# Windows (Command Prompt)
set XPING__APIKEY=your-api-key-here
set XPING__PROJECTID=your-project-id-here
set XPING__ENABLED=true
```

### Option C: Programmatic Configuration

Configure in code using a setup fixture:

```csharp
[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        var config = new XpingConfiguration
        {
            ApiKey = "your-api-key-here",
            ProjectId = "your-project-id-here",
            Enabled = true
        };
        
        XpingContext.Initialize(config);
    }
}
```

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
        // Flush any remaining test data and dispose
        await XpingContext.FlushAsync();
        await XpingContext.DisposeAsync();
    }
}
```

> **Important:** Place this file at the root of your test namespace to ensure it runs once per test assembly.

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

## Step 6: View Results in Xping Dashboard

1. Open the [Xping Dashboard](https://app.xping.io)
2. Explore your test data across multiple tabs:
   - **Test Sessions** - View uploaded test runs with execution statistics, environment details, and duration
   - **Tests** - Browse all tests with confidence scores, success rates, and execution history
   - **Flaky Tests** - Identify unreliable tests that need attention

Each test execution includes comprehensive tracking of pass/fail status, duration, confidence scores, environment information (OS, .NET version, CI/CD context), and trends over time.

> **Learn More:** For detailed information about navigating the dashboard, filtering tests, and understanding the test detail view, see [Navigating the Dashboard](../guides/getting-started/navigating-the-dashboard.md).

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

Categories are automatically captured and visible in the Xping dashboard for filtering and analysis.

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

- **Tests not appearing in dashboard** - Configuration, attribute placement, and connectivity checks
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
