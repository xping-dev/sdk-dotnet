<div id="top"></div>

<div align="center">
  <img src="docs/media/logo.svg" width="50" alt="Xping Logo" />
  
  <h1>Xping SDK for .NET</h1>
  
  <p align="center">
    <strong>Observability for your test suite. Know which tests you can trust.</strong>
    <br />
    Stop wasting time on flaky tests. Get actionable insights that improve reliability and confidence.
  </p>
</div>

<p align="center">
  <a href="https://www.nuget.org/packages/Xping.Sdk.Core/"><img src="https://img.shields.io/nuget/v/Xping.Sdk.Core?label=Xping.Sdk.Core" alt="NuGet"></a>
  <a href="https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml"><img src="https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml/badge.svg" alt="Build Status"></a>
  <a href="https://codecov.io/gh/xping-dev/sdk-dotnet"><img src="https://codecov.io/gh/xping-dev/sdk-dotnet/graph/badge.svg?token=VUOVI3YUTO" alt="codecov"></a>
  <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
</p>

<div align="center">
  <p align="center">
    <a href="#-quick-start"><strong>Quick Start</strong></a> •
    <a href="#-why-xping"><strong>Why Xping?</strong></a> •
    <a href="#-features"><strong>Features</strong></a> •
    <a href="https://docs.xping.io"><strong>Documentation</strong></a> •
    <a href="https://docs.xping.io/known-limitations.html"><strong>Known Limitations</strong></a> •
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
- 💪 **Resilient by design** - Retry logic, circuit breakers
- 📦 **Minimal overhead** - <5 ms per test, <1 KB memory footprint

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🔍 **Test Execution Tracking** | Automatically collect test results, duration, outcomes, and environment metadata |
| 📊 **Flaky Test Detection** | Identify unreliable tests with confidence scoring |
| 🌐 **CI/CD Auto-detection** | Works seamlessly with GitHub Actions, Azure DevOps, Jenkins, GitLab, and more |
| 💪 **Resilient Upload** | Retry policies and circuit breakers for reliable data delivery |
| ⚡ **Low Overhead** | <5 ms overhead per test, minimal memory footprint |
| 🎯 **Multi-Framework** | Support for NUnit, xUnit, and MSTest |
| 🔧 **Flexible Configuration** | JSON, environment variables, or programmatic setup |

---

## ⚡ Quick Start

Get started in under 2 minutes:

```bash
# 1. Install the SDK for your test framework
dotnet add package Xping.Sdk.NUnit    # or Xping.Sdk.XUnit / Xping.Sdk.MSTest

# 2. Configure with environment variables
export XPING_API_KEY="your-api-key"
export XPING_PROJECT_ID="your-project-id"
```

**NUnit** - Add one attribute to track all tests:
```csharp
[assembly: XpingTrack]
```

**xUnit** - Add one line to AssemblyInfo.cs:
```csharp
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

**MSTest** - Inherit from base class:
```csharp
[TestClass]
public class MyTests : XpingTestBase { }
```

Run your tests and view insights at [app.xping.io](https://app.xping.io) 🚀

### 📖 Framework-Specific Guides

- **[NUnit Setup Guide →](https://docs.xping.io/getting-started/quickstart-nunit.html)** - Detailed setup, attributes, and best practices
- **[xUnit Setup Guide →](https://docs.xping.io/getting-started/quickstart-xunit.html)** - Custom framework configuration and examples
- **[MSTest Setup Guide →](https://docs.xping.io/getting-started/quickstart-mstest.html)** - Base class usage and TestContext integration

---

## 🏗️ Architecture 


### Three-Layer Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Your Test Project                          │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐          │
│  │ NUnit Tests  │  │ xUnit Tests  │  │ MSTest Tests  │          │
│  └──────┬───────┘  └───────┬──────┘  └────────┬──────┘          │
└─────────┼──────────────────┼──────────────────┼─────────────────┘
          │                  │                  │
          ▼                  ▼                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Test Framework Adapters                      │
│  ┌──────────────┐  ┌───────────────┐  ┌──────────────┐          │
│  │ Xping.NUnit  │  │ Xping.XUnit   │  │ Xping.MSTest │          │
│  └──────┬───────┘  └───────┬───────┘  └───────┬──────┘          │
└─────────┼──────────────────┼──────────────────┼─────────────────┘
          │                  │                  │
          └──────────────────┼──────────────────┘
                             ▼
          ┌─────────────────────────────────────┐
          │         Xping.Sdk.Core              │
          │                                     │
          │  • Test Execution Tracking          │
          │  • Environment Detection            │
          │  • Configuration Management         │
          │  • Resilient Upload                 │
          └──────────────────┬──────────────────┘
                             ▼
                    Xping Platform API
```

---

## 🔧 Configuration

Xping SDK can be configured via **environment variables**, **appsettings.json**, or **programmatically**.

### Quick Configuration (Environment Variables)

```bash
# Required
export XPING_API_KEY="your-api-key"
export XPING_PROJECT_ID="your-project-id"

# Optional
export XPING_ENABLED="true"
export XPING_BATCH_SIZE="100"
```

For complete configuration reference including JSON config, CI/CD integration examples (GitHub Actions, Azure DevOps, Jenkins), and advanced options, see the [Configuration Guide](https://docs.xping.io/configuration/configuration-reference.html).

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

---

## 🔒 Privacy & Security

We take data privacy seriously. Here's exactly what we collect and don't collect:

### What We DON'T Collect
- ✅ No source code
- ✅ No sensitive data from test assertions
- ✅ No credentials or secrets
- ✅ No personally identifiable information (PII)

### Security Measures
- 🔐 **Encryption**: All data transmitted over HTTPS
- 🔑 **API Key Security**: Environment variable support (never commit keys to source control)
- 🗄️ **Data Retention**: Configurable retention policies
- 📖 **Open Source**: Full transparency - [review the code yourself](https://github.com/xping-dev/sdk-dotnet)
- ⚙️ **Configurable Collection**: Stack traces and sampling are optional

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
⚠️ DatabaseIntegrationTest: avg 45 s (up from 12 s last week)
✓ UnitTests: avg 150 ms (stable)
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

## 📚 Documentation

**Essential Resources:**
- 🚀 [Getting Started Guide](https://docs.xping.io/index.html#quick-start)
- ⚠️ [Known Limitations](https://docs.xping.io/known-limitations.html)
- 🔧 [Troubleshooting](https://docs.xping.io/troubleshooting/common-issues.html)
- 📖 [API Reference](https://docs.xping.io/api/Xping.Sdk.Core.Collection.html)

**Complete documentation available at [docs.xping.io](https://docs.xping.io)**

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
