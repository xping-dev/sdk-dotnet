# Xping for .NET

**Stop guessing. Start knowing which tests you can trust.**

---

## Welcome

Welcome to the Xping documentation for .NET! Xping is a test reliability platform that helps you identify flaky tests, understand test quality, and build confidence in your test suite.

---

## What is Xping?

**Xping** is a complete test reliability solution consisting of two components that work together:

### Xping SDK
A lightweight library that integrates with your test framework (NUnit, xUnit, MSTest) to automatically track test executions. It collects test results, timing, environment data, and outcomes with minimal overhead (less than 5ms per test).

### Xping Dashboard
A cloud platform that analyzes your test data to identify flaky tests, calculate confidence scores, and provide actionable insights. Access it at [app.xping.io](https://app.xping.io).

**How they work together:** The SDK collects test execution data and sends it to the Dashboard, where statistical analysis identifies patterns, detects flaky tests, and helps you understand your test suite's reliability.

---

## What You Get

**Automatic Flaky Test Detection** - Statistical analysis identifies unreliable tests before they become a problem

**Test Reliability Insights** - See which tests you can trust and which need attention

**Zero-Config Setup** - Add one attribute or line of code, start tracking immediately

**Minimal Overhead** - Less than 5ms per test, sub-1KB memory footprint

**Environment Comparison** - Understand how tests behave locally vs. CI/CD

**Historical Trends** - See test reliability over time across all environments

---

## Quick Start

Get started in under 5 minutes:

### 1. Choose Your Test Framework

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

### 2. Get Your API Key

Sign up at [app.xping.io](https://app.xping.io) and get your API key from **Account** → **Settings** → **API & Integration**

### 3. Configure Your Project

```json
{
  "Xping": {
    "ApiKey": "your-api-key",
    "ProjectId": "your-project-name"
  }
}
```

> **Note:** `ProjectId` is any meaningful identifier you choose (e.g., `"my-app"`). Xping automatically creates the project when tests first run. You can have multiple projects to organize tests across different applications or components.

### 4. Add Tracking to Your Tests

Add one attribute or line of code (framework-specific - see guides above)

### 5. Run Your Tests

That's it! Run your tests normally and view insights in the [Xping Dashboard](https://app.xping.io)

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

#### [Guides](guides/getting-started/understanding-confidence-scores.md)
Practical guides for using Xping SDK features

**Getting Started:**
- [Understanding Confidence Scores](guides/getting-started/understanding-confidence-scores.md)
- [Navigating the Dashboard](guides/getting-started/navigating-the-dashboard.md)
- [Interpreting Test Results](guides/getting-started/interpreting-test-results.md)

**Working with Tests:**
- [Identifying Flaky Tests](guides/working-with-tests/identifying-flaky-tests.md)
- [Common Flaky Patterns](guides/working-with-tests/common-flaky-patterns.md)
- [Fixing Flaky Tests](guides/working-with-tests/fixing-flaky-tests.md)
- [Monitoring Test Health](guides/working-with-tests/monitoring-test-health.md)
- [Best Practices](guides/working-with-tests/best-practices.md)

**Optimization:**
- [Performance Overview](guides/optimization/performance-overview.md)
- [Performance Configuration](guides/optimization/performance-configuration.md)
- [Performance Troubleshooting](guides/optimization/performance-troubleshooting.md)

#### [Performance](performance/overview.md)
Performance benchmarks and optimization guidelines

- [Performance Overview](performance/overview.md)
- [Benchmark Results](performance/benchmark-results.md)

#### [Troubleshooting](troubleshooting/common-issues.md)
Common issues and solutions

- [Common Issues](troubleshooting/common-issues.md)
- [Debugging Guide](troubleshooting/debugging.md)

#### [API Reference](https://docs.xping.io/api/Xping.Sdk.Core.Collection.html)
Complete API documentation for all SDK components

---

## Why Xping?

### The Problem

**How much time did your team waste debugging flaky tests last week?**

We've all been there. Your test passes locally but fails in CI. You re-run it and it passes. You waste hours investigating only to find out the test itself is unreliable, not your code.

Traditional test frameworks tell you if a test passed or failed—but they don't tell you if you can **trust** that result. Teams waste countless hours:

- Re-running tests hoping they'll pass
- Debugging tests that fail intermittently
- Wondering if failures are real bugs or environmental issues
- Lacking visibility into test reliability across environments

### The Solution

Xping provides:

- **Confidence Scores** - Multi-factor analysis combining pass rates, execution stability, retry behavior, environment consistency, failure patterns, and dependency impact
- **Flaky Test Detection** - Automatically identify unreliable tests using statistical analysis
- **Trend Analysis** - Visualize test reliability over time
- **Environment Context** - Understand where tests fail most
- **Actionable Insights** - Know which tests need attention

> **How Confidence Scores Work**: Xping analyzes test execution history across six weighted factors: pass rate (35%), execution stability (20%), retry behavior (15%), environment consistency (15%), failure patterns (10%), and dependency impact (5%). This produces a 0-1 score that distinguishes truly flaky tests from consistently passing or failing ones. Learn more in the [Understanding Confidence Scores](guides/getting-started/understanding-confidence-scores.md) guide.

---

## How It Works

1. **Track** - Xping SDK captures test execution data (duration, outcome, environment)
2. **Send** - SDK securely transmits data to Xping Dashboard
3. **Analyze** - Dashboard analyzes patterns across thousands of test runs
4. **Detect** - Statistical analysis identifies flaky tests and anomalies
5. **Alert** - Get notified when test reliability drops
6. **Improve** - Use insights to fix flaky tests and improve suite health

---

## Sample Projects

Explore complete working examples:

- [NUnit Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.NUnit)
- [xUnit Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.XUnit)
- [MSTest Sample](https://github.com/xping-dev/sdk-dotnet/tree/main/samples/SampleApp.MSTest)

---

## Support & Community

- [Documentation](https://docs.xping.io) - Comprehensive guides and references
- [GitHub Discussions](https://github.com/xping-dev/sdk-dotnet/discussions) - Ask questions and share feedback
- [Issue Tracker](https://github.com/xping-dev/sdk-dotnet/issues) - Report bugs and request features
- [Email Support](mailto:support@xping.io) - Direct support from our team
- [Xping Dashboard](https://app.xping.io) - View your test insights

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