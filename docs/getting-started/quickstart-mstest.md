# Quick Start: MSTest

Get started with Xping SDK in your MSTest test projects in less than 5 minutes. This guide will walk you through installation, configuration, and your first tracked test.

---

## What You'll Learn

- How to install Xping SDK for MSTest
- How to configure your API credentials
- How to track your first test
- How to verify results in the Xping dashboard

---

## Prerequisites

Before you begin, make sure you have:

- **.NET Framework 4.6.1+**, **.NET Core 2.0+**, or **.NET 5+** installed ([Download](https://dotnet.microsoft.com/download))
  - Xping SDK targets .NET Standard 2.0 for broad compatibility
- An **Xping account** with API credentials ([Sign up](https://app.xping.io))
- An existing **MSTest test project** or create a new one

> **New to MSTest?** Create a test project with: `dotnet new mstest -n MyTestProject`

---

## Step 1: Installation

Install the Xping SDK MSTest adapter package in your test project:

### Using .NET CLI

```bash
dotnet add package Xping.Sdk.MSTest
```

### Using Package Manager Console

```powershell
Install-Package Xping.Sdk.MSTest
```

### Using PackageReference

Add to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.MSTest" Version="1.0.*" />
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

Configure in code using assembly-level initialization:

```csharp
[TestClass]
public static class XpingInitializer
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
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

## Step 3: Set Up Assembly-Level Tracking

Create an assembly initialization class to initialize and dispose of the Xping context. This ensures proper resource management:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core;

namespace MyTestProject;

[TestClass]
public static class XpingSetup
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        // Initialize Xping SDK
        XpingContext.Initialize();
    }

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        // Flush any remaining test data and dispose
        await XpingContext.FlushAsync();
        await XpingContext.DisposeAsync();
    }
}
```

> **Important:** Place this class in your test project. MSTest will automatically discover and execute these methods once per test assembly.

---

## Step 4: Track Your Tests

There are two approaches to tracking tests with MSTest:

### Approach A: Using Base Class (Recommended)

Inherit from `XpingTestBase` to automatically track all tests in your class:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.MSTest;

namespace MyTestProject;

[TestClass]
public class CalculatorTests : XpingTestBase
{
    [TestMethod]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(2, 3);

        // Assert
        Assert.AreEqual(5, result);
    }

    [TestMethod]
    public void Divide_ByZero_ThrowsException()
    {
        // Arrange
        var calculator = new Calculator();

        // Act & Assert
        Assert.ThrowsException<DivideByZeroException>(() => 
            calculator.Divide(10, 0));
    }
}
```

The base class automatically handles test tracking via `[TestInitialize]` and `[TestCleanup]` hooks.

### Approach B: Manual Tracking

If you can't use inheritance, manually implement test tracking:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core;
using System.Diagnostics;

namespace MyTestProject;

[TestClass]
public class UserServiceTests
{
    private Stopwatch _stopwatch = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void TestInit()
    {
        _stopwatch = Stopwatch.StartNew();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _stopwatch.Stop();

        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = TestContext.TestName,
            FullyQualifiedName = $"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}",
            Outcome = MapOutcome(TestContext.CurrentTestOutcome),
            Duration = _stopwatch.Elapsed,
            StartTimeUtc = DateTime.UtcNow - _stopwatch.Elapsed,
            EndTimeUtc = DateTime.UtcNow,
            Environment = EnvironmentDetector.Detect()
        };

        XpingContext.RecordTest(execution);
    }

    private static TestOutcome MapOutcome(UnitTestOutcome outcome)
    {
        return outcome switch
        {
            UnitTestOutcome.Passed => TestOutcome.Passed,
            UnitTestOutcome.Failed => TestOutcome.Failed,
            UnitTestOutcome.Inconclusive => TestOutcome.NotExecuted,
            _ => TestOutcome.Failed
        };
    }

    [TestMethod]
    public async Task GetUser_ValidId_ReturnsUser()
    {
        // Test implementation
        Assert.IsTrue(true);
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

Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 134 ms
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

### Data-Driven Tests

Xping automatically tracks data-driven tests with their data rows:

```csharp
[TestClass]
public class MathTests : XpingTestBase
{
    [DataTestMethod]
    [DataRow(1, 1, 2)]
    [DataRow(2, 3, 5)]
    [DataRow(-1, 1, 0)]
    [DataRow(100, -50, 50)]
    public void Add_VariousInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.AreEqual(expected, result);
    }
}
```

Each data row is tracked separately with its parameter values captured in metadata.

### Dynamic Data Sources

Use dynamic data sources like CSV files or databases:

```csharp
[TestClass]
public class DataSourceTests : XpingTestBase
{
    [DataTestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method)]
    public void ProcessData_ValidInput_ReturnsExpected(int input, int expected)
    {
        var result = input * 2;
        Assert.AreEqual(expected, result);
    }

    public static IEnumerable<object[]> GetTestData()
    {
        yield return new object[] { 1, 2 };
        yield return new object[] { 5, 10 };
        yield return new object[] { 10, 20 };
    }
}
```

### Async Tests

Xping fully supports async test methods:

```csharp
[TestClass]
public class ApiTests : XpingTestBase
{
    [TestMethod]
    public async Task FetchData_ValidEndpoint_ReturnsData()
    {
        var client = new HttpClient();
        var response = await client.GetAsync("https://api.example.com/data");
        
        Assert.IsTrue(response.IsSuccessStatusCode);
    }
}
```

### Test Categories

Use MSTest's `[TestCategory]` attribute to organize tests:

```csharp
[TestClass]
public class DatabaseTests : XpingTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Smoke")]
    public void Connection_DatabaseAvailable_Connects()
    {
        // Test implementation
        Assert.IsTrue(true);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Performance")]
    public void Query_LargeDataset_CompletesQuickly()
    {
        // Test implementation
        Assert.IsTrue(true);
    }
}
```

Categories are automatically captured and visible in the Xping dashboard for filtering and analysis.

### Test Priorities

MSTest priorities are also tracked:

```csharp
[TestClass]
public class CriticalTests : XpingTestBase
{
    [TestMethod]
    [Priority(1)]
    [TestCategory("Critical")]
    public void CriticalFeature_Works()
    {
        Assert.IsTrue(true);
    }

    [TestMethod]
    [Priority(2)]
    public void ImportantFeature_Works()
    {
        Assert.IsTrue(true);
    }
}
```

### Accessing TestContext

The `TestContext` property provides useful information:

```csharp
[TestClass]
public class ContextTests : XpingTestBase
{
    [TestMethod]
    public void AccessTestContext_HasValidInformation()
    {
        // Access test context
        var testName = TestContext.TestName;
        var className = TestContext.FullyQualifiedTestClassName;
        
        // Write output (visible in test results)
        TestContext.WriteLine($"Running test: {testName}");
        
        Assert.IsNotNull(testName);
    }
}
```

---

## Known Limitations

> **⚠️ Important:** Tests marked with `[Ignore]` attribute are not tracked by Xping.
>
> This is because MSTest skips ignored tests before execution begins, and the `[TestInitialize]` and `[TestCleanup]` hooks are never invoked.
> Only tests that actually execute will be tracked.
>
> For more details, see [Known Limitations](../known-limitations.md#mstest).

---

## Troubleshooting

If you encounter issues while integrating or using the Xping SDK with MSTest, we have comprehensive troubleshooting resources available:

### Common Issues

- **Tests not appearing in dashboard** - Configuration, credentials, and connectivity checks
- **TestContext is null** - Property visibility and initialization
- **Data looks incomplete** - Flush and disposal timing
- **Performance concerns** - Impact measurement and optimization

### Get Help

For detailed troubleshooting steps and solutions:

- **[Common Issues](../troubleshooting/common-issues.md)** - Frequently encountered problems and solutions
- **[Debugging Guide](../troubleshooting/debugging.md)** - Enable logging and diagnose SDK behavior

Still stuck? Reach out through our support channels listed in the "Need Help?" section below.

---

## Next Steps

🎉 **Congratulations!** You've successfully integrated Xping SDK with MSTest.

Now explore more features:

- **[CI/CD Integration](ci-cd-setup.md)** - Integrate with GitHub Actions, Azure DevOps, and more
- **[Configuration Reference](../configuration/configuration-reference.md)** - Advanced configuration options
- **[Understanding Confidence Scores](../guides/getting-started/understanding-confidence-scores.md)** - Learn about test reliability scoring
- **[Performance Overview](../guides/optimization/performance-overview.md)** - Understanding performance, optimization, and tuning settings
- **[Known Limitations](../known-limitations.md)** - Framework-specific constraints and workarounds

---

## Sample Project

For a complete working example, check out our sample project:

📂 [samples/SampleApp.MSTest](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.MSTest)

---

## Need Help?

- 📚 [Documentation](https://docs.xping.io)
- 💬 [Community Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)
- 🐛 [Report an Issue](https://github.com/xping-dev/sdk-dotnet/issues)
- 📧 [Email Support](mailto:support@xping.io)

---

**Happy Testing!** 🚀
