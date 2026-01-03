# Quick Start: xUnit

Get started with Xping SDK in your xUnit test projects in less than 5 minutes. This guide will walk you through installation, configuration, and your first tracked test.

---

## What You'll Learn

- How to install Xping SDK for xUnit
- How to configure your API credentials
- How to enable automatic test tracking
- How to verify results in the Xping dashboard

---

## Prerequisites

Before you begin, make sure you have:

- **.NET Framework 4.6.1+**, **.NET Core 2.0+**, or **.NET 5+** installed ([Download](https://dotnet.microsoft.com/download))
  - Xping SDK targets .NET Standard 2.0 for broad compatibility
- An **Xping account** with API credentials ([Sign up](https://app.xping.io))
- An existing **xUnit test project** or create a new one

> **New to xUnit?** Create a test project with: `dotnet new xunit -n MyTestProject`

---

## Step 1: Installation

Install the Xping SDK xUnit adapter package in your test project:

### Using .NET CLI

```bash
dotnet add package Xping.Sdk.XUnit
```

### Using Package Manager Console

```powershell
Install-Package Xping.Sdk.XUnit
```

### Using PackageReference

Add to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.XUnit" Version="1.0.*" />
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

Configure in code using a module initializer:

```csharp
using System.Runtime.CompilerServices;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;

namespace MyTestProject;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
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

## Step 3: Enable Xping Test Framework

To enable automatic test tracking, configure your test project to use the Xping test framework. Add this to an `AssemblyInfo.cs` file in your test project:

```csharp
using Xunit;

[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

> **What does this do?** This tells xUnit to use the Xping test framework instead of the default one. The Xping framework wraps xUnit and automatically tracks all test executions.

**Creating AssemblyInfo.cs:**

If you don't have an `AssemblyInfo.cs` file, create one at the root of your test project:

```csharp
/*
 * AssemblyInfo.cs - xUnit Configuration for Xping SDK
 */

using Xunit;

// Configure xUnit to use Xping test framework for automatic tracking
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

---

## Step 4: Write Your Tests

With Xping configured, all your xUnit tests are automatically tracked—no attributes required!

### Basic Tests

```csharp
using Xunit;

namespace MyTestProject;

public class CalculatorTests
{
    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(2, 3);

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
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

### Theory Tests

Xping automatically tracks parameterized theory tests:

```csharp
public class MathTests
{
    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 3, 5)]
    [InlineData(-1, 1, 0)]
    [InlineData(100, -50, 50)]
    public void Add_VariousInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.Equal(expected, result);
    }
}
```

Each theory iteration is tracked separately with its parameter values captured.

### Async Tests

Xping fully supports async test methods:

```csharp
public class ApiTests
{
    [Fact]
    public async Task FetchData_ValidEndpoint_ReturnsData()
    {
        var client = new HttpClient();
        var response = await client.GetAsync("https://api.example.com/data");
        
        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData("https://api.example.com/users/1")]
    [InlineData("https://api.example.com/users/2")]
    public async Task FetchUser_ValidId_ReturnsUser(string url)
    {
        var client = new HttpClient();
        var response = await client.GetStringAsync(url);
        
        Assert.NotEmpty(response);
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

Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 156 ms
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

### Using Traits for Categorization

Use xUnit's `[Trait]` attribute to organize tests:

```csharp
public class DatabaseTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void Connection_DatabaseAvailable_Connects()
    {
        // Test implementation
        Assert.True(true);
    }

    [Theory]
    [InlineData("SELECT * FROM Users")]
    [InlineData("SELECT * FROM Orders")]
    [Trait("Category", "Integration")]
    [Trait("Type", "Query")]
    public void ExecuteQuery_ValidSql_ReturnsResults(string sql)
    {
        // Test implementation
        Assert.NotNull(sql);
    }
}
```

Traits are automatically captured and visible in the Xping dashboard for filtering and analysis.

### Collection Fixtures

Xping supports xUnit's collection fixtures for shared context:

```csharp
// Define a collection
[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

// Use the collection
[Collection("Database collection")]
public class DatabaseTests1
{
    private readonly DatabaseFixture _fixture;

    public DatabaseTests1(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test1()
    {
        // Use _fixture
        Assert.NotNull(_fixture);
    }
}

[Collection("Database collection")]
public class DatabaseTests2
{
    private readonly DatabaseFixture _fixture;

    public DatabaseTests2(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test2()
    {
        // Use _fixture
        Assert.NotNull(_fixture);
    }
}
```

All tests using the same collection are tracked with their collection context.

### Skip Tests

Xping tracks skipped tests as well:

```csharp
public class ExperimentalTests
{
    [Fact(Skip = "Not yet implemented")]
    public void NewFeature_WhenEnabled_Works()
    {
        // This test will be tracked as skipped
    }

    [Theory(Skip = "Waiting for API v2")]
    [InlineData(1)]
    [InlineData(2)]
    public void ApiV2Feature_ValidInput_ReturnsResult(int input)
    {
        // This test will be tracked as skipped
    }
}
```

---

## Known Limitations

> **✅ Good News:** Unlike NUnit and MSTest, xUnit's skipped tests (using `Skip` parameter) **are properly tracked** by Xping.
>
> The xUnit adapter uses a message sink pattern that captures all test lifecycle events, including skipped tests.
> No workarounds needed!
>
> For framework comparison and other limitations, see [Known Limitations](../known-limitations.md).

---

## Troubleshooting

If you encounter issues while integrating or using the Xping SDK with xUnit, we have comprehensive troubleshooting resources available:

### Common Issues

- **Tests not appearing in dashboard** - Configuration, test framework setup, and connectivity checks
- **"Could not find test framework" error** - Package installation and AssemblyInfo configuration
- **Data looks incomplete** - Network stability and flush timing
- **Performance concerns** - Impact measurement and optimization

### Get Help

For detailed troubleshooting steps and solutions:

- **[Common Issues](../troubleshooting/common-issues.md)** - Frequently encountered problems and solutions
- **[Debugging Guide](../troubleshooting/debugging.md)** - Enable logging and diagnose SDK behavior

Still stuck? Reach out through our support channels listed in the "Need Help?" section below.

---

## Next Steps

🎉 **Congratulations!** You've successfully integrated Xping SDK with xUnit.

Now explore more features:

- **[CI/CD Integration](ci-cd-setup.md)** - Integrate with GitHub Actions, Azure DevOps, and more
- **[Configuration Reference](../configuration/configuration-reference.md)** - Advanced configuration options
- **[Understanding Confidence Scores](../guides/getting-started/understanding-confidence-scores.md)** - Learn about test reliability scoring
- **[Performance Overview](../guides/optimization/performance-overview.md)** - Understanding performance, optimization, and tuning settings
- **[Known Limitations](../known-limitations.md)** - Framework-specific constraints and comparisons

---

## Sample Project

For a complete working example, check out our sample project:

📂 [samples/SampleApp.XUnit](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnit)

---

## Need Help?

- 📚 [Documentation](https://docs.xping.io)
- 💬 [Community Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)
- 🐛 [Report an Issue](https://github.com/xping-dev/sdk-dotnet/issues)
- 📧 [Email Support](mailto:support@xping.io)

---

**Happy Testing!** 🚀
