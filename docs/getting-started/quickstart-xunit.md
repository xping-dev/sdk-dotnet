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
- An **Xping account** with API credentials ([Sign up](https://app.xping.io/signup))
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
> The platform only requires that project names are unique within your workspace.

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
2. Navigate to your project
3. View your test executions, including:
   - **Pass/Fail Status** with execution time
   - **Flaky Test Detection** across multiple runs
   - **Confidence Scores** based on historical data
   - **Environment Information** (OS, .NET version, CI/CD context)
   - **Trends and Analytics** over time

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

## Troubleshooting

### Tests aren't appearing in Xping Dashboard

**Check the following:**

1. **Configuration**: Verify your `ApiKey` and `ProjectId` are correct
2. **Enabled flag**: Ensure `Xping:Enabled` is `true`
3. **Test Framework**: Verify `[assembly: TestFramework(...)]` is in `AssemblyInfo.cs`
4. **Network connectivity**: Check that your environment can reach `https://api.xping.io`
5. **Build output**: Ensure `appsettings.json` is being copied to output directory

### "Could not find test framework" error

If you see an error like:

```
Could not find type 'Xping.Sdk.XUnit.XpingTestFramework' in Xping.Sdk.XUnit
```

**Possible causes:**

- Package not properly installed
- Project not restored after adding the package
- Typo in `AssemblyInfo.cs`

**Solution:**

1. Clean and restore:
   ```bash
   dotnet clean
   dotnet restore
   dotnet build
   ```

2. Verify the package reference exists in your `.csproj`:
   ```xml
   <PackageReference Include="Xping.Sdk.XUnit" Version="1.0.*" />
   ```

3. Check `AssemblyInfo.cs` has the correct namespace:
   ```csharp
   [assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
   ```

### Tests are tracked but data looks incomplete

**Possible causes:**

- The test process exited before flushing completed
- Network issues during upload (check logs for retry attempts)


**Solution:** The Xping test framework handles cleanup automatically. If you're experiencing issues, check:

1. Test runner isn't force-killed
2. Network is stable during test execution
3. Increase flush interval if needed (see [Configuration Reference](../configuration/configuration-reference.md))

### Performance overhead concerns

Xping SDK is designed for minimal impact:

- **Tracking overhead**: <5ms per test
- **Memory usage**: ~100 bytes per test execution
- **Background processing**: Async batching with configurable intervals

To verify, compare test execution times with and without Xping by temporarily removing the `[assembly: TestFramework(...)]` attribute.

---

## Next Steps

🎉 **Congratulations!** You've successfully integrated Xping SDK with xUnit.

Now explore more features:

- **[CI/CD Integration](ci-cd-setup.md)** - Integrate with GitHub Actions, Azure DevOps, and more
- **[Configuration Reference](../configuration/configuration-reference.md)** - Advanced configuration options
- **[Flaky Test Detection](../guides/flaky-test-detection.md)** - Understanding confidence scores
- **[Performance Guide](~/guides/performance.md)** - Understanding performance, optimization, and tuning settings

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
