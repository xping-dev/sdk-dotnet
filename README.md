<div id="top"></div>

<div align="center">
  <img src="docs/docs/media/logo.svg" width="400" alt="Xping Logo" />
  
  <h1>Xping SDK for .NET</h1>
  
  <p align="center">
    <strong>Observability for your test suite. Know which tests you can trust.</strong>
    <br />
    Stop wasting time on flaky tests. Get actionable insights that improve reliability and confidence.
  </p>
  
  <p align="center">
    [![NuGet](https://img.shields.io/nuget/v/Xping.Sdk.Core?label=Xping.Sdk.Core)](https://www.nuget.org/packages/Xping.Sdk.Core/)
    [![Build Status](https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml)
    [![codecov](https://codecov.io/gh/xping-dev/sdk-dotnet/graph/badge.svg?token=VUOVI3YUTO)](https://codecov.io/gh/xping-dev/sdk-dotnet)
    [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
  </p>

  <p align="center">
    <a href="#-quick-start"><strong>Quick Start</strong></a> •
    <a href="#-why-xping"><strong>Why Xping?</strong></a> •
    <a href="#-features"><strong>Features</strong></a> •
    <a href="docs/"><strong>Documentation</strong></a> •
    <a href="https://github.com/xping-dev/sdk-dotnet/issues"><strong>Report Bug</strong></a>
  </p>
</div>

<br />

---

## 🎯 The Problem

**How much time did your team waste debugging flaky tests last week?**

Traditional test frameworks tell you if a test passed or failed—but they don't tell you if you can **trust** that result. Developers spend countless hours:

- 🔄 Re-running tests hoping they'll pass
- 🐛 Debugging tests that fail intermittently
- 🤔 Wondering if failures are real bugs or environmental issues
- 📊 Lacking visibility into test reliability across environments

**Xping solves this.** We bring observability to testing, giving you confidence scores, flaky test detection, and actionable insights—all with minimal setup.

---

## 🚀 Why Xping?

### For Developers
- ⚡ **Zero-config setup** - Add one attribute, start tracking
- 🎯 **Focus on real bugs** - Stop chasing flaky tests
- 📈 **Understand test health** - See reliability trends over time

### For QA Leaders
- 📊 **Data-driven decisions** - Quantify test suite reliability
- 🔍 **Identify problem areas** - Spot flaky tests automatically
- 🌐 **Environment insights** - Compare local vs. CI/CD test behavior

### For DevOps Engineers
- 🔧 **CI/CD integration** - Automatic environment detection (GitHub Actions, Azure DevOps, Jenkins, GitLab)
- 💪 **Resilient by design** - Retry logic, circuit breakers, offline queue
- 📦 **Minimal overhead** - <5ms per test, <100 bytes memory footprint

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🔍 **Test Execution Tracking** | Automatically collect test results, duration, outcomes, and environment metadata |
| 📊 **Flaky Test Detection** | Identify unreliable tests with confidence scoring |
| 🌐 **CI/CD Auto-detection** | Works seamlessly with GitHub Actions, Azure DevOps, Jenkins, GitLab, and more |
| 💪 **Resilient Upload** | Retry policies, circuit breakers, and offline queuing for reliable data delivery |
| ⚡ **Low Overhead** | <5ms overhead per test, minimal memory footprint |
| 🎯 **Multi-Framework** | Support for NUnit, xUnit, and MSTest |
| 🔧 **Flexible Configuration** | JSON, environment variables, or programmatic setup |
| 📦 **Offline Mode** | Queue results when network is unavailable, upload when reconnected |

---

## 📦 Installation

Choose the package for your test framework:

```bash
# For NUnit projects
dotnet add package Xping.Sdk.NUnit

# For xUnit projects
dotnet add package Xping.Sdk.XUnit

# For MSTest projects
dotnet add package Xping.Sdk.MSTest
```

---

## ⚡ Quick Start

### NUnit

```csharp
using NUnit.Framework;
using Xping.Sdk.NUnit;

// Option 1: Track all tests in the assembly
[assembly: XpingTrack]

// Option 2: Track specific test fixtures
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

    [Test]
    public void Divide_ByZero_ThrowsException()
    {
        Assert.Throws<DivideByZeroException>(() => Calculator.Divide(10, 0));
    }
}
```

### xUnit

```csharp
using Xunit;

// Add to AssemblyInfo.cs or any file with [assembly:] attributes
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]

public class CalculatorTests
{
    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = Calculator.Add(2, 3);
        Assert.Equal(5, result);
    }

    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(10, -5, 5)]
    public void Add_MultipleInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.Equal(expected, result);
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

    [DataTestMethod]
    [DataRow(2, 3, 5)]
    [DataRow(10, -5, 5)]
    public void Add_MultipleInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.AreEqual(expected, result);
    }
}
```

### Configuration

Add `appsettings.json` to your test project:

```json
{
  "Xping": {
    "ApiKey": "your-api-key",
    "ProjectId": "your-project-id",
    "Enabled": true,
    "ApiEndpoint": "https://api.xping.io",
    "BatchSize": 100,
    "FlushInterval": "00:00:30",
    "MaxRetries": 3,
    "EnableOfflineQueue": true
  }
}
```

Or use environment variables (recommended for CI/CD):

```bash
export XPING_API_KEY="your-api-key"
export XPING_PROJECT_ID="your-project-id"
```

That's it! Run your tests and view results at [app.xping.io](https://app.xping.io)

---

## 🏗️ Architecture 


### Three-Layer Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Your Test Project                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ NUnit Tests  │  │ xUnit Tests  │  │ MSTest Tests │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
└─────────┼──────────────────┼──────────────────┼─────────────────┘
          │                  │                  │
          ▼                  ▼                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Test Framework Adapters                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Xping.NUnit  │  │ Xping.XUnit  │  │ Xping.MSTest │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
└─────────┼──────────────────┼──────────────────┼─────────────────┘
          │                  │                  │
          └──────────────────┼──────────────────┘
                             ▼
          ┌─────────────────────────────────────┐
          │         Xping.Sdk.Core              │
          │                                     │
          │  • Test Execution Collector        │
          │  • Environment Detection           │
          │  • In-Memory Buffer (Thread-Safe)  │
          │  • Configuration Management        │
          │  • Offline Queue                   │
          └──────────────┬──────────────────────┘
                         ▼
          ┌─────────────────────────────────────┐
          │      API Client (Resilient)         │
          │                                     │
          │  • Retry Logic (Exponential)       │
          │  • Circuit Breaker                 │
          │  • Gzip Compression                │
          │  • Batch Upload                    │
          └──────────────┬──────────────────────┘
                         ▼
                  Xping Platform API
```

### Technology Stack

#### Core Dependencies
- **.NET Standard 2.0** - Broad compatibility (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- **System.Text.Json** - High-performance JSON serialization
- **Microsoft.Extensions.Configuration** - Flexible configuration system
- **Polly 8.x** - Resilience policies (retry, circuit breaker)

#### Test Framework Integration
- **NUnit 3.14+** - `ITestAction` for per-test hooks, `[SetUpFixture]` for global setup
- **xUnit 2.9+** - Custom `ITestFramework` and message sink for test tracking
- **MSTest 3.2+** - Base class pattern with `TestContext` integration

#### Performance Characteristics
- **Overhead**: <5ms per test execution
- **Memory**: <100 bytes per test execution record
- **Throughput**: >10,000 tests/second collection capacity
- **Network**: Configurable batch size (default: 100 tests per upload)

---

## 🔧 Configuration

### Configuration Sources (Priority Order)

1. **Programmatic** (highest priority)
2. **Environment Variables**
3. **appsettings.json**
4. **Default Values** (lowest priority)

### Complete Configuration Reference

```json
{
  "Xping": {
    // Authentication (Required)
    "ApiKey": "your-api-key",
    "ProjectId": "your-project-id",
    
    // API Configuration
    "ApiEndpoint": "https://api.xping.io",
    "UploadTimeout": "00:00:30",
    
    // Feature Flags
    "Enabled": true,
    "CaptureStackTraces": true,
    "EnableCompression": true,
    "EnableOfflineQueue": true,
    "AutoDetectCIEnvironment": true,
    "CollectNetworkMetrics": true,
    
    // Batching & Performance
    "BatchSize": 100,
    "FlushInterval": "00:00:30",
    
    // Retry Configuration
    "MaxRetries": 3,
    "RetryDelay": "00:00:02",
    
    // Sampling (1.0 = 100%, 0.5 = 50%)
    "SamplingRate": 1.0
  }
}    
```

### Environment Variables

```bash
# Required
export XPING_API_KEY="your-api-key"
export XPING_PROJECT_ID="your-project-id"

# Optional
export XPING_ENABLED="true"
export XPING_API_ENDPOINT="https://api.xping.io"
export XPING_BATCH_SIZE="100"
export XPING_MAX_RETRIES="3"
```

### CI/CD Configuration Examples

<details>
<summary><strong>GitHub Actions</strong></summary>

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run Tests with Xping
        env:
          XPING_API_KEY: ${{ secrets.XPING_API_KEY }}
          XPING_PROJECT_ID: ${{ secrets.XPING_PROJECT_ID }}
        run: dotnet test
```

</details>

<details>
<summary><strong>Azure DevOps</strong></summary>

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

variables:
  XPING_API_KEY: $(XpingApiKey)
  XPING_PROJECT_ID: $(XpingProjectId)

steps:
- task: UseDotNet@2
  inputs:
    version: '8.0.x'

- task: DotNetCoreCLI@2
  displayName: 'Run Tests'
  inputs:
    command: 'test'
    projects: '**/*Tests.csproj'
```

</details>

<details>
<summary><strong>Jenkins</strong></summary>

```groovy
pipeline {
    agent any
    
    environment {
        XPING_API_KEY = credentials('xping-api-key')
        XPING_PROJECT_ID = credentials('xping-project-id')
    }
    
    stages {
        stage('Test') {
            steps {
                sh 'dotnet test'
            }
        }
    }
}
```

</details>

---

## 📊 What Gets Collected?

### Test Execution Data
- Test name and fully qualified name
- Outcome (Passed, Failed, Skipped, etc.)
- Duration (milliseconds)
- Start and end timestamps (UTC)
- Error message and stack trace (for failures)
- Test categories/traits

### Environment Metadata
- Operating system and version
- .NET runtime version
- Machine name
- Network metrics (latency, packet loss)
- CI/CD platform detection
- Build/branch information (from CI environment)

### Privacy & Security
- ✅ No source code is collected
- ✅ No sensitive data from test assertions
- ✅ Stack traces are optional (configurable)
- ✅ All data transmitted over HTTPS
- ✅ Configurable sampling for large test suites

---

## 🎯 Use Cases

### 1. Identify Flaky Tests
Track test outcomes over time to identify tests with inconsistent results:
```
✓ LoginTest: 95% reliability (190/200 passed)
✗ SearchTest: 60% reliability (120/200 passed) ⚠️ FLAKY
```

### 2. Monitor Test Duration
Identify slow tests that impact CI/CD pipeline performance:
```
⚠️ DatabaseIntegrationTest: avg 45s (up from 12s last week)
✓ UnitTests: avg 150ms (stable)
```

### 3. Environment Comparison
Compare test behavior across environments:
```
Production Warmup: 98% reliability
Staging: 95% reliability  
Local Development: 85% reliability ⚠️
```

### 4. CI/CD Pipeline Insights
Understand test reliability across different build configurations:
```
main branch: 97% reliability
feature branches: 89% reliability
PR builds: 92% reliability
```

---

## 🔒 Security & Compliance

- **Data Encryption**: All data transmitted over HTTPS/TLS 1.3
- **API Key Security**: Support for environment variables (never commit keys)
- **Data Retention**: Configurable retention policies
- **Open Source**: Full transparency - review the code yourself
- **No Dependencies on External Services**: Works offline with queue mode

---

## 📚 Documentation

- [Getting Started Guide](docs/docs/overview.md)
- [NUnit Integration](docs/docs/tutorial-unittests.md)
- [xUnit Integration](docs/docs/tutorial-unittests.md)
- [MSTest Integration](docs/docs/tutorial-unittests.md)
- [Configuration Reference](docs/docs/overview.md)
- [API Documentation](https://xping-dev.github.io/sdk-dotnet/)
- [Troubleshooting](docs/docs/overview.md)

---

## 🤝 Contributing

We welcome contributions! Whether it's:

- 🐛 Bug reports
- 💡 Feature requests  
- 📖 Documentation improvements
- 🔧 Code contributions

### Development Setup

```bash
# Clone the repository
git clone https://github.com/xping-dev/sdk-dotnet.git
cd sdk-dotnet

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

---

## 📈 Roadmap

Check our [Milestones](https://github.com/xping-dev/sdk-dotnet/milestones) for planned features:

- **[Working Set](https://github.com/xping-dev/sdk-dotnet/milestone/1)** - Currently in progress
- **[Backlog](https://github.com/xping-dev/sdk-dotnet/milestone/2)** - Future considerations

### Planned Features
- 📊 Enhanced flaky test analytics
- 🔍 Test failure categorization (infrastructure vs. code)
- 🌐 Multi-language support (Java, Python, JavaScript)
- 📦 Self-hosted deployment option

---

## 💬 Support & Community

- 💬 [GitHub Discussions](https://github.com/xping-dev/sdk-dotnet/discussions) - Ask questions, share ideas
- 🐛 [Issue Tracker](https://github.com/xping-dev/sdk-dotnet/issues) - Report bugs, request features
- 📧 [Email Support](mailto:support@xping.io) - Direct support
- 📖 [Documentation](https://docs.xping.io) - Comprehensive guides

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## ⭐ Show Your Support

If Xping helps you build better software, give us a ⭐️ on GitHub!

<div align="center">
  <p>
    <strong>Built with ❤️ by developers who hate flaky tests</strong>
  </p>
  <p>
    <sub>Made by <a href="https://xping.io">Xping</a> • Follow us on <a href="https://github.com/xping-dev">GitHub</a></sub>
  </p>
</div>

<p align="right">(<a href="#top">back to top</a>)</p>
