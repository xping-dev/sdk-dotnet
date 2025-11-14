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
- An **Xping account** with API credentials ([Sign up](https://app.xping.io/signup))
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

Apply the `[XpingTrack]` attribute to your test fixtures or individual test methods:

### Class-Level Tracking (Recommended)

Track all tests in a fixture:

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
2. Navigate to your project
3. View your test executions, including:
   - **Pass/Fail Status** with execution time
   - **Flaky Test Detection** across multiple runs
   - **Confidence Scores** based on historical data
   - **Environment Information** (OS, .NET version, CI/CD context)
   - **Trends and Analytics** over time

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

## Troubleshooting

### Tests aren't appearing in Xping Dashboard

**Check the following:**

1. **Configuration**: Verify your `ApiKey` and `ProjectId` are correct
2. **Enabled flag**: Ensure `Xping:Enabled` is `true`
3. **Network connectivity**: Check that your environment can reach `https://api.xping.io`
4. **Setup fixture**: Verify `[SetUpFixture]` is being executed (add logging to confirm)
5. **Attribute placement**: Ensure `[XpingTrack]` is applied to your test fixture or methods

### Tests are tracked but data looks incomplete

**Possible causes:**

- The test process exited before `XpingContext.FlushAsync()` completed
- Network issues during upload (check logs for retry attempts)
- Offline queue is full (default max: 10,000 tests)

**Solution:** Ensure `[OneTimeTearDown]` in your setup fixture includes:

```csharp
[OneTimeTearDown]
public async Task AfterAllTests()
{
    await XpingContext.FlushAsync();
    await XpingContext.DisposeAsync();
}
```

### Performance overhead concerns

Xping SDK is designed for minimal impact:

- **Tracking overhead**: <5ms per test
- **Memory usage**: ~100 bytes per test execution
- **Background processing**: Async batching with configurable intervals

To verify, compare test execution times with and without Xping:

```bash
# Without tracking
dotnet test

# With tracking
dotnet test
```

Overhead should be negligible (<1% of total test time).

---

## Next Steps

🎉 **Congratulations!** You've successfully integrated Xping SDK with NUnit.

Now explore more features:

- **[CI/CD Integration](ci-cd-setup.md)** - Integrate with GitHub Actions, Azure DevOps, and more
- **[Configuration Reference](../configuration/configuration-reference.md)** - Advanced configuration options
- **[Flaky Test Detection](../guides/flaky-test-detection.md)** - Understanding confidence scores
- **[Performance Tuning](../guides/performance-tuning.md)** - Optimize for large test suites

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
