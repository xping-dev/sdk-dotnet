<div id="top"></div>

<div align="center">
  <p align="center">
    <a href="https://www.nuget.org/packages/Xping.Sdk.Core/"><img src="https://img.shields.io/nuget/v/Xping.Sdk.Core?label=Xping.Sdk.Core" alt="NuGet"></a>
    <a href="https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml"><img src="https://github.com/xping-dev/sdk-dotnet/actions/workflows/ci.yml/badge.svg" alt="Build Status"></a>
    <a href="https://codecov.io/gh/xping-dev/sdk-dotnet"><img src="https://codecov.io/gh/xping-dev/sdk-dotnet/graph/badge.svg?token=VUOVI3YUTO" alt="codecov"></a>
    <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  </p>
  <img src="https://raw.githubusercontent.com/xping-dev/sdk-dotnet/main/docs/media/logo.svg" width="50" alt="Xping Logo" />
  <h1>Xping for .NET</h1>
  <p align="center">
    <strong>Stop guessing. Start knowing which tests you can trust.</strong>
    <br />
    Automatic flaky test detection and test reliability insights for .NET teams.
  </p>
</div>


<div align="center">
  <p align="center">
    <a href="#quick-start"><strong>Quick Start</strong></a> •
    <a href="#the-problem"><strong>The Problem</strong></a> •
    <a href="#what-you-get"><strong>What You Get</strong></a> •
    <a href="https://docs.xping.io"><strong>Documentation</strong></a> •
    <a href="https://docs.xping.io/known-limitations.html"><strong>Known Limitations</strong></a> •
    <a href="https://github.com/xping-dev/sdk-dotnet/issues"><strong>Report Bug</strong></a>
  </p>
</div>

<br />

---

## The Problem

**How much time did your team waste debugging flaky tests last week?**

We've all been there. Your test passes locally but fails in CI. You re-run it and it passes. You waste hours investigating only to find out the test itself is unreliable, not your code.

Traditional test frameworks tell you if a test passed or failed—but they don't tell you if you can **trust** that result. Teams spend countless hours:

- Re-running tests hoping they'll pass
- Debugging tests that fail intermittently
- Wondering if failures are real bugs or environmental issues
- Lacking visibility into test reliability across environments

**Xping solves this.** We bring observability to testing, giving you confidence scores, flaky test detection, and actionable insights—all with minimal setup.

---

## What You Get

### For Everyone
- **Automatic Flaky Test Detection** - Statistical analysis identifies unreliable tests before they become a problem
- **Test Reliability Insights** - See which tests you can trust and which need attention
- **Zero-Config Setup** - Add one attribute or line of code, start tracking immediately
- **Minimal Overhead** - Less than 5ms per test, sub-1KB memory footprint

### For Developers
- **Focus on Real Bugs** - Stop chasing flaky tests, focus on actual issues
- **Environment Comparison** - Understand how tests behave locally vs. CI/CD
- **Historical Trends** - See test reliability over time

### For QA & Engineering Leaders
- **Data-Driven Decisions** - Quantify test suite quality with hard metrics
- **Problem Identification** - Automatically spot flaky tests across your entire suite
- **CI/CD Intelligence** - Works seamlessly with GitHub Actions, Azure DevOps, Jenkins, GitLab, and more

### Technical Features
- **Test Execution Tracking** - Automatically collect test results, duration, outcomes, and environment metadata
- **Resilient Upload** - Retry policies and circuit breakers for reliable data delivery
- **Multi-Framework Support** - NUnit, xUnit, and MSTest
- **Flexible Configuration** - JSON, environment variables, or programmatic setup

---

## Quick Start

Get started in under 5 minutes:

### 1. Install the SDK

```bash
dotnet add package Xping.Sdk.NUnit    # or Xping.Sdk.XUnit / Xping.Sdk.MSTest
```

### 2. Configure with environment variables

```bash
export XPING_APIKEY="your-api-key"
export XPING_PROJECTID="your-project-id"
```

### 3. Add tracking to your tests

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

### 4. Run your tests

That's it! Run your tests normally and view insights at [app.xping.io](https://app.xping.io)

### Framework-Specific Guides

- [NUnit Setup Guide](https://docs.xping.io/getting-started/quickstart-nunit.html) - Detailed setup, attributes, and best practices
- [xUnit Setup Guide](https://docs.xping.io/getting-started/quickstart-xunit.html) - Custom framework configuration and examples
- [MSTest Setup Guide](https://docs.xping.io/getting-started/quickstart-mstest.html) - Base class usage and TestContext integration

---

## Architecture

### How It Works

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
                    Xping Cloud Platform
```

---

## Configuration

Xping SDK can be configured via **environment variables**, **appsettings.json**, or **programmatically**.

### Environment Variables

```bash
# Required
export XPING_APIKEY="your-api-key"
export XPING_PROJECTID="your-project-id"

# Optional
export XPING_ENABLED="true"
export XPING_BATCHSIZE="100"
```

See the [Configuration Reference](https://docs.xping.io/configuration/configuration-reference.html) for complete options including JSON configuration, CI/CD integration examples (GitHub Actions, Azure DevOps, Jenkins), and advanced settings.

---

## What Gets Collected?

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

## Privacy & Security

We take data privacy seriously. Here's exactly what we collect and don't collect:

### What We DON'T Collect
- No source code
- No sensitive data from test assertions
- No credentials or secrets
- No personally identifiable information (PII)

### Security Measures
- **Encryption** - All data transmitted over HTTPS
- **API Key Security** - Environment variable support (never commit keys to source control)
- **Data Retention** - Configurable retention policies
- **Open Source** - Full transparency, [review the code yourself](https://github.com/xping-dev/sdk-dotnet)
- **Configurable Collection** - Stack traces and sampling are optional

---

## What Problems Does Xping Solve?

Xping helps you identify and understand common test reliability issues. The SDK collects test execution data, and the Dashboard analyzes it to detect:

- **Race Conditions** - Tests that fail intermittently due to timing issues
- **External Service Dependencies** - Tests affected by network or service availability
- **Shared State Issues** - Tests that interfere with each other
- **Time-Based Flakiness** - Tests that fail at specific times or dates
- **Resource Exhaustion** - Tests that leak resources over time
- **Non-Deterministic Data** - Tests with random or unpredictable data

Once tests are tracked with the SDK, the [Xping Dashboard](https://app.xping.io) provides:
- Reliability scores and confidence metrics for each test
- Automatic flaky test detection with pattern analysis
- Environment comparison (local vs. CI/CD behavior)
- Historical trends and performance insights

For detailed examples of each pattern and how Xping detects them, see the [Common Flaky Patterns Guide](https://docs.xping.io/guides/working-with-tests/common-flaky-patterns.html).

---

## Documentation

**Essential Resources:**
- [Getting Started Guide](https://docs.xping.io/index.html#quick-start)
- [Known Limitations](https://docs.xping.io/known-limitations.html)
- [Troubleshooting](https://docs.xping.io/troubleshooting/common-issues.html)
- [API Reference](https://docs.xping.io/api/Xping.Sdk.Core.Collection.html)

Complete documentation available at [docs.xping.io](https://docs.xping.io)

---

## Contributing

We welcome contributions! Whether it's:

- Bug reports
- Feature requests
- Documentation improvements
- Code contributions

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

## Roadmap

Check our [Milestones](https://github.com/xping-dev/sdk-dotnet/milestones) for planned features:

- [Working Set](https://github.com/xping-dev/sdk-dotnet/milestone/1) - Currently in progress
- [Backlog](https://github.com/xping-dev/sdk-dotnet/milestone/2) - Future considerations

### Planned Features
- Enhanced flaky test analytics
- Test failure categorization (infrastructure vs. code)
- Multi-language support (Java, Python, JavaScript)
- Self-hosted deployment option

---

## Support & Community

- [GitHub Discussions](https://github.com/xping-dev/sdk-dotnet/discussions) - Ask questions, share ideas
- [Issue Tracker](https://github.com/xping-dev/sdk-dotnet/issues) - Report bugs, request features
- [Email Support](mailto:support@xping.io) - Direct support
- [Documentation](https://docs.xping.io) - Comprehensive guides

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

<div align="center">
  <p>
    <strong>Built by developers who hate flaky tests</strong>
  </p>
  <p>
    <sub>Made by <a href="https://xping.io">Xping</a> • Follow us on <a href="https://github.com/xping-dev">GitHub</a></sub>
  </p>
  <br />
  <p>
    If Xping helps you build better software, give us a ⭐ on GitHub!
  </p>
</div>

<p align="right">(<a href="#top">back to top</a>)</p>
