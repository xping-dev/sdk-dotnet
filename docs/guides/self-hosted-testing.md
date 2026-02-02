---
uid: guides.self-hosted-testing
title: Self-Hosted Testing with Xping Cloud Platform
---

# Self-Hosted Testing with Xping Cloud Platform

## Overview

This guide explains how to configure the Xping SDK to test itself using the Xping observability platform. By enabling self-hosted testing, the SDK's own test suite uploads execution results to Xping Cloud, providing:

- **Real-world validation**: The SDK uses itself in production-like scenarios
- **Flaky test detection**: Identify unreliable tests in the SDK's own test suite
- **CI/CD observability**: Monitor test reliability across different environments and .NET versions
- **Confidence building**: Demonstrate SDK capabilities to potential users

## Architecture

The self-hosted testing setup follows this architecture:

```
┌───────────────────────────────────────────────────────────┐
│                    GitHub Actions CI/CD                   │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐  │
│  │ NUnit Tests   │  │ xUnit Tests   │  │ MSTest Tests  │  │
│  │ + Xping.Sdk   │  │ + Xping.Sdk   │  │ + Xping.Sdk   │  │
│  └───────┬───────┘  └───────┬───────┘  └───────┬───────┘  │
│          │                  │                  │          │
│          └──────────────────┼──────────────────┘          │
│                             ▼                             │
│                ┌────────────────────────┐                 │
│                │  Xping Cloud Platform  │                 │
│                │     upload.xping.io    │                 │
│                └────────────────────────┘                 │
└───────────────────────────────────────────────────────────┘
```

Each test project in the SDK:
1. References to the appropriate Xping adapter (NUnit/xUnit/MSTest)
2. Configures API credentials via environment variables
3. Applies tracking attributes to capture test executions
4. Uploads results during CI/CD runs

## Benefits

### For SDK Development
- **Real-time validation**: Test the SDK against its own codebase
- **Early bug detection**: Catch issues in upload, serialization, and configuration
- **Performance monitoring**: Track SDK overhead in actual test execution
- **Breaking change detection**: Identify regressions immediately

### For SDK Users
- **Proof of concept**: Demonstrates real-world SDK usage
- **Confidence**: Shows the SDK team uses their own product
- **Best practices**: Provides reference implementation for integration

## Configuration Strategy

### Environment-Based Configuration

The SDK supports multiple configuration sources with this priority order:

1. **Environment variables** (the highest priority) – Used in CI/CD
2. **appsettings.json** – Used for local development
3. **Default values** (lowest priority)

This allows:
- Developers to test locally without uploading to Xping Cloud
- CI/CD to automatically upload results using secrets
- Sample apps to use separate projects from test projects

### Project ID Naming Convention

To organize test results in Xping Cloud, use this naming pattern:

```
sdk-dotnet-<test-suite>-<framework>
```

Examples:
- `sdk-dotnet-core-tests` - Core SDK unit tests
- `sdk-dotnet-nunit-adapter-tests` - NUnit adapter tests
- `sdk-dotnet-xunit-adapter-tests` - xUnit adapter tests
- `sdk-dotnet-mstest-adapter-tests` - MSTest adapter tests
- `sdk-dotnet-integration-tests` - Integration tests

Benefits:
- Clear organization in Xping dashboard
- Easy filtering by test suite type
- Consistent naming across environments

## Implementation Guide

### Step 1: Install the SDK (Already Complete)

Each test project already references the appropriate Xping SDK adapter that corresponds to its testing framework. Since this project uses **xUnit** as the unit testing framework, all test projects must reference the **Xping.Sdk.XUnit** adapter to enable Xping's observability and reliability tracking capabilities.

**xUnit Tests:**
```xml
<ProjectReference Include="..\..\src\Xping.Sdk.XUnit\Xping.Sdk.XUnit.csproj" />
```

### Step 2: Add Tracking to Test Projects

#### For xUnit Tests

Add to `AssemblyInfo.cs`:

```csharp
using Xunit;

[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

### Step 3: Configure Local Development

Create `appsettings.test.json` in test project directories:

```json
{
  "Xping": {
    "ProjectId": "sdk-dotnet-core-tests",
    "Enabled": false
  }
}
```

**Note**: `Enabled` is set to `false` by default to prevent local development from uploading to Xping Cloud Platform. CI/CD will override this via environment variables.

Add to `.csproj` to copy the configuration file:

```xml
<ItemGroup>
  <None Update="appsettings.test.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Step 4: Configure CI/CD Pipeline

Update `.github/workflows/ci.yml` to enable Xping tracking:

```yaml
name: Test Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

env:
  XPING_ENABLED: true
  XPING_APIKEY: ${{ secrets.XPING_API_KEY }}

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        dotnet-version: ['8.0.x', '9.0.x']
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: ${{ matrix.dotnet-version }}
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore --configuration Release
      
      - name: Run Core Tests
        env:
          XPING_PROJECTID: sdk-dotnet-core-tests
        run: dotnet test tests/Xping.Sdk.Core.Tests/ --no-build --configuration Release
      
      - name: Run NUnit Adapter Tests
        env:
          XPING_PROJECTID: sdk-dotnet-nunit-adapter-tests
        run: dotnet test tests/Xping.Sdk.NUnit.Tests/ --no-build --configuration Release
      
      - name: Run xUnit Adapter Tests
        env:
          XPING_PROJECTID: sdk-dotnet-xunit-adapter-tests
        run: dotnet test tests/Xping.Sdk.XUnit.Tests/ --no-build --configuration Release
      
      - name: Run MSTest Adapter Tests
        env:
          XPING_PROJECTID: sdk-dotnet-mstest-adapter-tests
        run: dotnet test tests/Xping.Sdk.MSTest.Tests/ --no-build --configuration Release
      
      - name: Run Integration Tests
        env:
          XPING_PROJECTID: sdk-dotnet-integration-tests
        run: dotnet test tests/Xping.Sdk.Integration.Tests/ --no-build --configuration Release
```

### Step 5: Add GitHub Secrets

In GitHub repository settings, add the secret:

1. Navigate to **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret**
3. Add:
   - Name: `XPING_API_KEY`
   - Value: Your Xping API key from [Xping Dashboard](https://app.xping.io)

### Step 6: Verify Setup

1. **Local verification** (no upload):
   ```bash
   dotnet test tests/Xping.Sdk.Core.Tests/
   ```
   
   Verify SDK is disabled by default (check logs).

2. **CI/CD verification** (with upload):
   - Push changes to trigger the CI pipeline
   - Monitor the GitHub Actions workflow
   - Check Xping Dashboard for test results

3. **Manual upload test** (local with environment variables):
   ```bash
   export XPING_ENABLED=true
   export XPING_APIKEY="your-api-key"
   export XPING_PROJECTID="sdk-dotnet-test-local"
   dotnet test tests/Xping.Sdk.Core.Tests/
   ```

## Monitoring and Insights

### Xping Cloud Dashboard

After successful setup, view test results at [Xping Dashboard](https://app.xping.io):

1. **Test Execution Tracking**: See all test runs from CI/CD
2. **Flaky Test Detection**: Identify unreliable tests automatically
3. **Environment Comparison**: Compare test reliability between .NET 8 and .NET 9
4. **Historical Trends**: Track test suite health over time
5. **Failure Analysis**: Investigate stack traces and error patterns

### Key Metrics to Monitor

- **Pass rate**: Percentage of tests passing consistently
- **Flaky rate**: Tests with intermittent failures
- **Execution time**: Test duration trends
- **Environment differences**: Reliability variations across .NET versions

## Troubleshooting

### Tests Not Uploading

**Symptom**: No results appear in Xping Dashboard

**Causes and Solutions**:

1. **SDK disabled in configuration**
   - Check `XPING_ENABLED=true` in CI/CD environment
   - Verify environment variable override works

2. **Missing API key**
   - Verify `XPING_API_KEY` secret exists in GitHub
   - Check secret is not expired

3. **Network issues**
   - Verify `ApiEndpoint` is `https://upload.xping.io/v1`
   - Check GitHub Actions runner has internet access

4. **Tracking not applied**
   - Confirm `XpingContext.Initialize()` is called

**Debug Steps**:

```bash
# Enable verbose SDK logging
export XPING_LOGLEVEL=Debug

# Run tests and check output
dotnet test tests/Xping.Sdk.Core.Tests/ --logger "console;verbosity=detailed"
```

### Configuration Not Loading

**Symptom**: SDK uses default configuration instead of test configuration

**Causes**:
- `appsettings.test.json` not copied to output directory
- File isn’t named exactly `appsettings.json` (SDK looks for this filename)
- Environment variables are not overriding correctly

**Solution**:

Rename `appsettings.test.json` to `appsettings.json` in test projects, or use environment variables for all configuration.

### Duplicate Uploads

**Symptom**: Same test execution uploaded multiple times

**Causes**:
- Multiple `XpingContext.Initialize()` calls
- Missing `FlushAsync()` or `DisposeAsync()` causing retries

**Solution**:

Ensure setup fixtures follow the patterns shown in Step 2, with a single initialization and proper cleanup.

## Best Practices

### 1. Separate Projects for Different Test Suites

Use distinct `ProjectId` values for each test project:
- Core SDK tests
- Framework adapter tests
- Integration tests
- Performance benchmarks

This enables:
- Granular analysis per test suite
- Independent monitoring
- Clear failure attribution

### 2. Disable Local Uploads

Keep `Enabled: false` in `appsettings.json` to prevent:
- Local development interfering with CI/CD metrics
- Accidental uploads during debugging
- Credential exposure

### 3. Use Sampling in High-Volume Scenarios

For large test suites (>1000 tests), consider sampling:

```json
{
  "Xping": {
    "SamplingRate": 0.1
  }
}
```

This uploads 10% of test executions while maintaining statistical significance.

### 4. Monitor SDK Overhead

Use benchmarks to ensure SDK doesn't significantly impact test performance:

```bash
dotnet run --project tests/Xping.Sdk.Benchmarks/
```

Target: <5 ms overhead per test execution.

### 5. Test Xping Integration in Integration Tests

Create integration tests specifically for Xping upload functionality:

```csharp
[Fact]
public async Task Xping_UploadsSuccessfully_WhenEnabled()
{
    // Arrange
    var config = new XpingConfiguration
    {
        ApiEndpoint = "https://upload.xping.io/v1",
        ApiKey = Environment.GetEnvironmentVariable("XPING_APIKEY"),
        ProjectId = "sdk-dotnet-integration-test",
        Enabled = true
    };
    
    // Act & Assert
    // Test upload logic
}
```

## Security Considerations

### API Key Protection

1. **Never commit API keys**: Use environment variables and secrets
2. **Rotate keys regularly**: Update GitHub secrets periodically
3. **Use read-only keys**: If available, prefer keys with upload-only permissions
4. **Audit access**: Monitor who has access to GitHub secrets

### Data Privacy

1. **Review test data**: Ensure no PII or sensitive data in test names or stack traces
2. **Configure stack trace capture**: Disable if tests contain sensitive logic
   ```json
   {
     "Xping": {
       "CaptureStackTraces": false
     }
   }
   ```
3. **Use sampling**: Reduce data volume while maintaining insights

## Advanced Configuration

### Per-Environment Configuration

Use different configurations for different branches:

```yaml
# Production branch
- name: Run Tests (Production)
  if: github.ref == 'refs/heads/main'
  env:
    XPING_ENVIRONMENT: Production
    XPING_SAMPLINGRATE: 1.0

# Development branch
- name: Run Tests (Development)
  if: github.ref == 'refs/heads/develop'
  env:
    XPING_ENVIRONMENT: Development
    XPING_SAMPLINGRATE: 0.5
```

### Custom Retry Configuration

Adjust retry behavior for CI/CD:

```yaml
env:
  XPING_MAXRETRIES: 5
  XPING_RETRYDELAY: 00:00:05
```

### Compression and Batching

Optimize for large test suites:

```yaml
env:
  XPING_BATCHSIZE: 500
  XPING_ENABLECOMPRESSION: true
  XPING_FLUSHINTERVAL: 00:01:00
```

## Continuous Improvement

### Metrics to Track

1. **SDK test reliability**: Monitor flaky tests in the SDK itself
2. **Upload success rate**: Track successful vs. failed uploads
3. **Performance impact**: Measure SDK overhead on test execution time
4. **Coverage completeness**: Ensure all test suites are tracked

### Regular Reviews

Schedule regular reviews of:
- Xping Cloud dashboard for SDK tests
- Flaky test reports
- Test execution trends
- Configuration optimization opportunities

## Conclusion

Self-hosted testing with Xping Cloud provides:
- **Real-world validation**: The SDK uses itself
- **Continuous monitoring**: Track test reliability over time
- **Confidence**: Demonstrate SDK quality to users
- **Best practices**: Reference implementation for integration

By following this guide, the Xping SDK's test suite will automatically upload results to Xping Cloud Platform during CI/CD runs, providing comprehensive observability into the SDK's own test health.

## Related Documentation

- [Configuration Reference](../configuration/configuration-reference.md)
- [Getting Started](../index.md#getting-started)
- [Troubleshooting](../troubleshooting/common-issues.md)

## Support

For issues or questions:
- GitHub Issues: [https://github.com/xping-dev/sdk-dotnet/issues](https://github.com/xping-dev/sdk-dotnet/issues)
- Documentation: [docs.xping.io](https://docs.xping.io)
