<!-- 
  This README is specifically designed for NuGet package display.
  It uses absolute URLs and formatting optimized for NuGet.org rendering.
  For the GitHub repository README, see /README.md in the root directory.
-->

# Xping SDK for .NET

**Xping** brings observability to testing. We help developers and teams understand not just whether tests pass, but whether they can be trusted. Our mission is to eliminate wasted time on flaky tests and provide actionable insights that improve test reliability and confidence.

## Features

- 🔍 **Test Execution Tracking** - Automatic collection of test results, duration, and outcomes
- 📊 **Flaky Test Detection** - Identify unreliable tests that waste development time
- 🌐 **CI/CD Integration** - Automatic environment detection for GitHub Actions, Azure DevOps, Jenkins, and more
- 💪 **Resilient Upload** - Retry logic with exponential backoff and circuit breaker for reliable data delivery
- ⚡ **Low Overhead** - Minimal performance impact on your test execution
- 🎯 **Multi-Framework Support** - Works with NUnit, xUnit, and MSTest

## Installation

Choose the package for your test framework:

### NUnit
```bash
dotnet add package Xping.Sdk.NUnit
```

### xUnit
```bash
dotnet add package Xping.Sdk.XUnit
```

### MSTest
```bash
dotnet add package Xping.Sdk.MSTest
```

## Quick Start

### NUnit

```csharp
using NUnit.Framework;
using Xping.Sdk.NUnit;

// Apply at assembly level for all tests
[assembly: XpingTrack]

// Or apply to specific test fixtures
[TestFixture]
[XpingTrack]
public class CalculatorTests
{
    [Test]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = Calculator.Add(2, 3);
        Assert.AreEqual(5, result);
    }
}
```

### xUnit

```csharp
using Xunit;

// Add to AssemblyInfo.cs or as assembly attribute
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]

public class CalculatorTests
{
    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = Calculator.Add(2, 3);
        Assert.Equal(5, result);
    }
}
```

### MSTest

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.MSTest;

[TestClass]
public class CalculatorTests : XpingTestBase
{
    [TestMethod]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = Calculator.Add(2, 3);
        Assert.AreEqual(5, result);
    }
}
```

## Configuration

Configure Xping SDK using `appsettings.json` or environment variables:

### appsettings.json

```json
{
  "Xping": {
    "ApiKey": "your-api-key",
    "ProjectId": "your-project-id",
    "Enabled": true,
    "ApiEndpoint": "https://upload.xping.io/v1",
    "BatchSize": 100,
    "FlushInterval": "00:00:30"
  }
}
```

### Environment Variables (Recommended for CI/CD)

```bash
export XPING_APIKEY="your-api-key"
export XPING_PROJECTID="your-project-id"
export XPING_ENABLED="true"
```

## Key Benefits

### Detect Flaky Tests
Automatically identify tests that pass and fail intermittently, helping you focus on real issues rather than debugging unreliable tests.

### Track Test History
Monitor test execution trends over time to understand test suite health and identify patterns in failures.

### CI/CD Insights
Get detailed insights into test performance across different environments, branches, and pull requests.

### Performance Analysis
Track test execution duration to identify slow tests and optimize your test suite.

## Requirements

- .NET Standard 2.0 or higher
- .NET Framework 4.6.1+ / .NET Core 2.0+ / .NET 5+
- NUnit 3.14+, xUnit 2.9+, or MSTest 3.2+

## Documentation

For detailed documentation, tutorials, and examples, visit:

- **GitHub Repository**: https://github.com/xping-dev/sdk-dotnet
- **Getting Started Guide**: https://github.com/xping-dev/sdk-dotnet/blob/main/docs/docs/getting-started.md
- **API Documentation**: https://docs.xping.io

## Support

- **Issues**: https://github.com/xping-dev/sdk-dotnet/issues
- **Discussions**: https://github.com/xping-dev/sdk-dotnet/discussions```c#

## License

Distributed under the MIT License. See `LICENSE` file for more information.
