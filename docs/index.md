# Xping SDK for .NET

**Observability for your test suite. Know which tests you can trust.**

---

## Welcome

Welcome to the **Xping SDK** documentation! Xping brings observability to testing, helping developers and teams understand not just whether tests pass, but whether they can be trusted. Our mission is to eliminate wasted time on flaky tests and provide actionable insights that improve test reliability and confidence.

---

## What is Xping SDK?

**Xping SDK** is a lightweight library that integrates seamlessly with your existing .NET test frameworks (NUnit, xUnit, MSTest) to automatically track test executions, detect flaky tests, and provide confidence scores based on historical data.

### Key Features

✅ **Automatic Test Tracking** - Zero-touch integration with your existing tests  
✅ **Flaky Test Detection** - Identify unreliable tests across multiple runs  
✅ **Confidence Scores** - Know which test results you can trust  
✅ **Environment Context** - Track tests across CI/CD, local, and production environments  
✅ **Performance Insights** - Monitor test execution times and trends  
✅ **Zero Impact** - <5ms overhead per test, runs silently in background

---

## Quick Start

Get started with Xping in less than 5 minutes:

### Choose Your Test Framework

<table>
<tr>
<td width="33%">

**NUnit**

```bash
dotnet add package Xping.Sdk.NUnit
```

[Quick Start Guide →](getting-started/quickstart-nunit.md)

</td>
<td width="33%">

**xUnit**

```bash
dotnet add package Xping.Sdk.XUnit
```

[Quick Start Guide →](getting-started/quickstart-xunit.md)

</td>
<td width="33%">

**MSTest**

```bash
dotnet add package Xping.Sdk.MSTest
```

[Quick Start Guide →](getting-started/quickstart-mstest.md)

</td>
</tr>
</table>

### Basic Setup

1. **Install the package** for your test framework
2. **Get your API key** from [Xping Dashboard](https://app.xping.io): **Account** → **Settings** → **API & Integration**
3. **Configure with your credentials**:
   ```json
   {
     "Xping": {
       "ApiKey": "your-api-key",
       "ProjectId": "your-project-name"
     }
   }
   ```
  > **Note:** `ProjectId` is any meaningful identifier you choose (e.g., `"my-app"`). Xping automatically creates the project when tests first run to help you organize your tests. You might have multiple projects to better organize your tests across different applications or components.
  4. **Apply tracking** to your tests (framework-specific)
5. **Run tests** as normal - Xping works silently in the background

---

## Documentation Structure

#### [Getting Started](getting-started/quickstart-nunit.md)
Quick start guides for each test framework and CI/CD integration

- [NUnit Quick Start](getting-started/quickstart-nunit.md)
- [xUnit Quick Start](getting-started/quickstart-xunit.md)
- [MSTest Quick Start](getting-started/quickstart-mstest.md)
- [CI/CD Integration](getting-started/ci-cd-setup.md)

#### [Configuration](configuration/configuration-reference.md)
Comprehensive configuration options and environment setup

#### [Guides](guides/flaky-test-detection.md)
Practical guides for using Xping SDK features

- [Flaky Test Detection](guides/flaky-test-detection.md)
- [Performance Guide](guides/performance.md)

#### [Performance](performance/overview.md)
Performance benchmarks and optimization guidelines

- [Performance Overview](performance/overview.md)
- [Benchmark Results](performance/benchmark-results.md)

#### [Troubleshooting](troubleshooting/common-issues.md)
Common issues and solutions

- [Common Issues](troubleshooting/common-issues.md)
- [Debugging Guide](troubleshooting/debugging.md)

#### API Reference
Complete API documentation for all SDK components

---

## Why Xping?

### The Problem

Traditional test frameworks tell you if a test passed or failed—but they don't tell you if you can **trust** that result. Teams waste countless hours:

- 🔄 Re-running tests hoping they'll pass
- 🐛 Debugging tests that fail intermittently  
- 🤔 Wondering if failures are real bugs or environmental issues
- 📊 Lacking visibility into test reliability across environments

### The Solution

Xping provides:

- **Confidence Scores**: Multi-factor analysis combining pass rates, execution stability, retry behavior, environment consistency, failure patterns, and dependency impact
- **Flaky Test Detection**: Automatically identify unreliable tests using statistical analysis
- **Trend Analysis**: Visualize test reliability over time
- **Environment Context**: Understand where tests fail most
- **Actionable Insights**: Know which tests need attention

> **How Confidence Scores Work**: Xping analyzes test execution history across six weighted factors: pass rate (35%), execution stability (20%), retry behavior (15%), environment consistency (15%), failure patterns (10%), and dependency impact (5%). This produces a 0-1 score that distinguishes truly flaky tests from consistently passing or failing ones. Learn more in the [Flaky Test Detection Guide](guides/flaky-test-detection.md).

---

## How It Works

1. **Track**: Xping SDK captures test execution data (duration, outcome, environment)
2. **Analyze**: Our platform analyzes patterns across thousands of test runs
3. **Detect**: Statistical analysis identifies flaky tests and anomalies
4. **Alert**: Get notified when test reliability drops
5. **Improve**: Use insights to fix flaky tests and improve suite health

---

## Sample Projects

Explore complete working examples:

- 📂 [NUnit Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.NUnit)
- 📂 [xUnit Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnit)
- 📂 [MSTest Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.MSTest)

---

## Support & Community

We're here to help!

- 📚 **[Documentation](https://docs.xping.io)** - Comprehensive guides and references
- 💬 **[GitHub Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)** - Ask questions and share feedback
- 🐛 **[Issue Tracker](https://github.com/xping-dev/sdk-dotnet/issues)** - Report bugs and request features
- 📧 **[Email Support](mailto:support@xping.io)** - Direct support from our team
- 🌐 **[Official Website](https://xping.io)** - Learn more about Xping

---

## Contributing

Xping SDK is open source! We welcome contributions from the community.

- View our [Code of Conduct](https://github.com/xping-dev/sdk-dotnet/blob/main/CODE_OF_CONDUCT.md)
- Read the [Contributing Guide](https://github.com/xping-dev/sdk-dotnet/blob/main/CONTRIBUTING.md)
- Check out [open issues](https://github.com/xping-dev/sdk-dotnet/issues)

---

## License

Xping SDK is licensed under the [MIT License](https://github.com/xping-dev/sdk-dotnet/blob/main/LICENSE).

---

**Ready to improve your test reliability?** [Get Started →](getting-started/quickstart-nunit.md)