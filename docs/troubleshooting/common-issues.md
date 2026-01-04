---
uid: troubleshooting-common-issues
title: Common Issues
---

# Common Issues

This guide covers the most frequently encountered issues when using the Xping SDK and their solutions.

---

## Connection & Network Issues

### Tests Not Appearing in Dashboard

**Symptoms:**
- Tests run successfully but don't appear in Xping dashboard
- No error messages in test output
- SDK appears to be working

**Common Causes & Solutions:**

#### 1. Missing or Invalid Credentials

Check that both `ApiKey` and `ProjectId` are configured:

```bash
# Verify environment variables are set
echo $XPING_APIKEY
echo $XPING_PROJECTID
```

Or check your `appsettings.json`:

```json
{
  "Xping": {
    "ApiKey": "xpg_live_your_key_here",
    "ProjectId": "your-project-id"
  }
}
```

**Solution:** Set both values. See [Configuration Reference](../configuration/configuration-reference.md) for details.

---

#### 2. SDK Disabled

The SDK may be disabled via configuration:

```bash
# Check if SDK is disabled
echo $XPING_ENABLED
```

**Solution:** Ensure `Enabled` is `true` (default) or remove the setting entirely.

---

#### 3. Network Connectivity Issues

The SDK requires outbound HTTPS access to `upload.xping.io`.

**Test connectivity:**

```bash
# Test HTTPS connection
curl -v https://upload.xping.io

# Or with PowerShell (Windows)
Test-NetConnection -ComputerName upload.xping.io -Port 443
```

**Common blockers:**
- Corporate firewall blocking outbound HTTPS
- Proxy server requiring authentication
- Network security policies blocking SDK traffic
- DNS resolution issues

**Solution:** Work with your network team to allowlist `upload.xping.io` on port 443 (HTTPS).

---

#### 4. Missing Test Teardown (NUnit & MSTest)

> **Note:** This issue is specific to **NUnit** and **MSTest**. xUnit automatically handles teardown through its custom test framework—no manual teardown required.

Test results are uploaded in batches automatically when:
- The batch size is reached (default: 100 tests collected), OR
- The flush interval timer fires (default: every 30 seconds), OR
- **The test session completes** and teardown flushes remaining tests

**For small test suites:**

If you run 10 tests with proper teardown, all 10 tests will upload immediately when the test suite completes, regardless of batch size or flush interval.

**Common mistake (NUnit/MSTest only):** Missing or incorrect teardown:

❌ **Missing teardown (NUnit):**
```csharp
// No [OneTimeTearDown] - tests may remain in buffer
[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void Setup() => XpingContext.Initialize();
    
    // Missing teardown!
}
```

❌ **Missing teardown (MSTest):**
```csharp
// No [AssemblyCleanup] - tests may remain in buffer
[TestClass]
public static class XpingSetup
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context) 
        => XpingContext.Initialize();
    
    // Missing cleanup!
}
```

✅ **Correct teardown (NUnit):**
```csharp
[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void Setup() => XpingContext.Initialize();
    
    [OneTimeTearDown]
    public async Task Teardown()
    {
        await XpingContext.FlushAsync();     // Flush remaining tests
        await XpingContext.DisposeAsync();   // Cleanup resources
    }
}
```

✅ **Correct teardown (MSTest):**
```csharp
[TestClass]
public static class XpingSetup
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
        => XpingContext.Initialize();
    
    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FlushAsync();     // Flush remaining tests
        await XpingContext.DisposeAsync();   // Cleanup resources
    }
}
```

✅ **xUnit - No manual teardown needed:**
```csharp
// xUnit's XpingTestFramework handles initialization and disposal automatically
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]

// That's it! No manual teardown required for xUnit.
```

**Solution:** 
- **NUnit/MSTest:** Ensure proper test teardown is implemented as shown above or in the [Getting Started guides](../getting-started/quickstart-nunit.md)
- **xUnit:** Verify the `[assembly: TestFramework(...)]` attribute is configured in `AssemblyInfo.cs` (see [xUnit Getting Started](../getting-started/quickstart-xunit.md))

---

#### 5. Silent Upload Failures

If uploads fail (network issues, invalid credentials), the SDK logs errors but doesn't throw exceptions — your tests continue running normally.

**Solution:** Enable logging to see what's happening. See [Debugging Guide](debugging.md#enabling-sdk-logging).

---

### Connection Timeout Errors

**Symptoms:**
- Slow test execution
- Timeout messages in logs
- Intermittent upload failures

**Common Causes:**

1. **Slow Network Connection**
   - Default upload timeout is 30 seconds
   - Large batches may take longer on slow connections

   **Solution:** Increase `UploadTimeout`:
   ```json
   {
     "Xping": {
       "UploadTimeout": "00:01:00"  // 60 seconds
     }
   }
   ```

2. **Large Batch Sizes**
   - Batches with stack traces can be large
   - Compression helps but may not be enough

   **Solution:** Reduce `BatchSize` or disable stack trace capture:
   ```json
   {
     "Xping": {
       "BatchSize": 50,
       "CaptureStackTraces": false
     }
   }
   ```

3. **Proxy Interference**
   - Corporate proxies may interfere with HTTPS connections
   - Proxy authentication may be required

   **Solution:** Currently, the SDK doesn't have built-in proxy support. However, you can configure a local proxy that forwards to `upload.xping.io` and set `ApiEndpoint` to your proxy URL:
   ```json
   {
     "Xping": {
       "ApiEndpoint": "http://localhost:8888"  // Your local proxy
     }
   }
   ```

  > This functionality has not been tested in production environments. 
  > It is provided as a troubleshooting option for evaluation purposes only.

---

### Circuit Breaker Open

**Symptoms:**
- Error message: "Circuit breaker is open: Too many consecutive failures"
- Uploads stopped temporarily
- Message indicates automatic reset after 30 seconds

**What This Means:**

The SDK's resilience system has detected repeated upload failures (50% failure rate over 10+ attempts) and has temporarily stopped attempting uploads to prevent cascading failures.

**Common Causes:**
- Xping API temporarily unavailable
- Network issues causing consistent failures
- Invalid credentials causing repeated 401/403 errors

**Solution:**

1. **Wait for automatic reset:** The circuit breaker reopens after 30 seconds
2. **Check network connectivity:** Verify connection to `upload.xping.io`
3. **Review logs:** Enable debug logging to see underlying failure reasons
4. **Verify credentials:** Ensure API key and project ID are valid

**Prevention:** Address the root cause (network, credentials, API issues) before the circuit breaker triggers.

---

## Authentication & Authorization

### HTTP 401: Authentication Failed

**Error Message:**
```
Authentication failed (401): Invalid API Key or Project ID
Action: Verify credentials at https://app.xping.io
```

**Causes:**
1. API key is incorrect or expired
2. API key doesn't exist in the Xping platform
3. Typo in API key (extra spaces, wrong characters)

**Solution:**

1. Verify your API key at [Xping Dashboard](https://app.xping.io)
2. Navigate to **Account** → **Settings** → **API & Integration**
3. Copy the correct API key
4. Update your configuration:

```bash
export XPING_APIKEY="xpg_live_correct_key_here"
```

**Common mistakes:**
- Using test/development key in production
- API key rotation without updating configuration
- Hardcoded keys in source control (security risk!)

---

### HTTP 403: Authorization Failed

**Error Message:**
```
Authorization failed (403): Insufficient permissions
Action: Check project access at https://app.xping.io
```

**Causes:**
1. API key is valid but doesn't have permission to create or access projects
2. API key has been revoked or disabled
3. Account-level permissions issue

**Solution:**

1. Verify your API key status in [Xping Dashboard](https://app.xping.io)
2. Navigate to **Account** → **Settings** → **API & Integration**
3. Check that the API key has appropriate permissions
4. If needed, create a new API key with full permissions

> **Note:** The `ProjectId` is a user-defined identifier that Xping automatically creates when your tests first run. You don't need to create the project manually in the dashboard—just choose a meaningful name and Xping will handle the rest.

---

### HTTP 429: Rate Limit Exceeded

**Error Message:**
```
Rate limit exceeded (429): Too many requests
Action: Reduce test execution frequency or contact support
```

**Causes:**
1. Executing tests too frequently
2. Multiple test runners uploading simultaneously
3. Small batch sizes causing too many API calls
4. Running large test suites continuously

**Solution:**

1. **Increase batch size** to reduce API call frequency:
   ```json
   {
     "Xping": {
       "BatchSize": 500  // Larger batches = fewer API calls
     }
   }
   ```

2. **Increase flush interval** to batch more tests:
   ```json
   {
     "Xping": {
       "FlushInterval": "00:02:00"  // 2 minutes
     }
   }
   ```

3. **Use sampling** for very large test suites:
   ```json
   {
     "Xping": {
       "SamplingRate": 0.5  // Track 50% of tests
     }
   }
   ```

4. **Coordinate with team:** Ensure multiple team members aren't running tests against the same project simultaneously

5. **Contact support** if you need higher rate limits for legitimate use cases

---

## Configuration Issues

### Configuration Not Loading

**Symptoms:**
- SDK uses default values instead of your configuration
- Environment variables ignored
- `appsettings.json` not being read

**Common Causes:**

#### 1. Wrong Environment Variable Names

Environment variables must use double underscores for nested properties:

❌ **Incorrect:**
```bash
export XPING.APIKEY="key"           # Wrong: uses dot
export XPING_APIKEY="key"          # Wrong: snake_case
```

✅ **Correct:**
```bash
export XPING__APIKEY="key"          # Correct: double underscore
export XPING__PROJECTID="project"   # Correct: double underscore
```

#### 2. JSON File Not Found

The SDK looks for `appsettings.json` in the working directory or alongside the test assembly.

**Solution:** 
- Ensure `appsettings.json` is in the correct location
- Set `Copy to Output Directory` to `Copy if newer` in Visual Studio
- Or use absolute paths for configuration files

#### 3. Invalid JSON Syntax

**Solution:** Validate your JSON:
```bash
# Validate JSON syntax (requires jq)
cat appsettings.json | jq .

# Or use online JSON validators
```

#### 4. Case Sensitivity

Configuration property names are **case-sensitive** in JSON:

❌ **Incorrect:**
```json
{
  "Xping": {
    "apikey": "key",      // Wrong: lowercase
    "PROJECTID": "id"     // Wrong: uppercase
  }
}
```

✅ **Correct:**
```json
{
  "Xping": {
    "ApiKey": "key",      // Correct: PascalCase
    "ProjectId": "id"     // Correct: PascalCase
  }
}
```

---

### Configuration Validation Errors

**Symptoms:**
- SDK initialization fails
- Error messages about invalid configuration values

**Common Issues:**

1. **Invalid Value Ranges:**
   - `BatchSize`: Must be between 1 and 1000
   - `SamplingRate`: Must be between 0.0 and 1.0
   - `MaxRetries`: Must be between 0 and 10
   - `FlushInterval`: Must be greater than zero

2. **Invalid TimeSpan Format:**
   ```json
   {
     "Xping": {
       "FlushInterval": "30",         // ❌ Wrong: not TimeSpan format
       "FlushInterval": "00:00:30"    // ✅ Correct: HH:MM:SS format
     }
   }
   ```

3. **Invalid URLs:**
   - `ApiEndpoint` must be a valid HTTP or HTTPS URL
   - Don't include trailing slashes

**Solution:** Review [Configuration Reference](../configuration/configuration-reference.md) for valid values and formats.

---

## Performance Issues

### Test Suite Running Slowly

**Symptoms:**
- Tests take longer to complete than expected
- Performance degradation after adding Xping SDK

**Expected Overhead:**

The Xping SDK is designed to add minimal overhead:
- **<5ms per test** on average
- **<1KB memory per test execution**
- **<5% CPU overhead**

See [Performance Testing Overview](../performance/overview.md) for detailed benchmarks.

**If you're seeing higher overhead:**

#### 1. Synchronous Flushing

Avoid calling `FlushAsync()` after every test—this blocks test execution:

❌ **Bad:**
```csharp
[Test]
public async Task MyTest()
{
    // Test code
    await XpingContext.FlushAsync();  // Don't do this!
}
```

✅ **Good:**
```csharp
[OneTimeTearDown]  // Flush once after all tests
public async Task Cleanup()
{
    await XpingContext.FlushAsync();
}
```

#### 2. Stack Trace Capture

Capturing stack traces adds overhead for failed tests:

**Solution:** Disable if you don't need stack traces:
```json
{
  "Xping": {
    "CaptureStackTraces": false
  }
}
```

#### 3. Network Issues

Slow network connections can impact performance if batches fill up frequently.

**Solution:**
- Increase `BatchSize` to batch more tests
- Increase `FlushInterval` to reduce upload frequency

#### 4. Compression Overhead

Compression reduces payload size but adds CPU overhead.

**Solution:** Disable compression if network bandwidth is not an issue:
```json
{
  "Xping": {
    "EnableCompression": false
  }
}
```

---

### High Memory Usage

**Symptoms:**
- Increased memory consumption during test execution
- Out of memory errors in large test suites

**Expected Memory Usage:**

- **<1KB per test execution** for metadata
- **<50MB for 10,000 tests** buffered in memory

**If memory usage is higher:**

#### 1. Large Batch Sizes

Larger batches keep more data in memory before uploading:

**Solution:** Reduce `BatchSize`:
```json
{
  "Xping": {
    "BatchSize": 50  // Smaller batches = less memory
  }
}
```

#### 2. Stack Traces

Stack traces consume significant memory, especially for deep call stacks:

**Solution:** Disable stack trace capture:
```json
{
  "Xping": {
    "CaptureStackTraces": false
  }
}
```

#### 3. Upload Failures

If uploads fail repeatedly, tests accumulate in memory:

**Solution:**
- Fix upload issues (network, credentials)
- Monitor logs for upload errors

---

## CI/CD Specific Issues

### Environment Not Detected

**Symptoms:**
- Tests run in CI but environment shows as "Local" instead of "CI"
- CI-specific metadata (build number, commit SHA) not captured

**Solution:**

The SDK auto-detects most CI/CD platforms via environment variables. Verify detection is enabled:

```json
{
  "Xping": {
    "AutoDetectCIEnvironment": true  // Default
  }
}
```

**Supported CI/CD platforms:**
- GitHub Actions
- Azure DevOps
- GitLab CI/CD
- Jenkins
- CircleCI
- Travis CI
- TeamCity
- Generic CI (via `CI=true` environment variable)

**For unsupported platforms:** Explicitly set the environment:

```json
{
  "Xping": {
    "Environment": "CI",
    "AutoDetectCIEnvironment": false
  }
}
```

Or via environment variable:
```bash
export XPING__ENVIRONMENT="CI"
```

---

### Secrets & API Keys in CI/CD

**Symptoms:**
- API key exposed in logs
- Build fails due to missing credentials
- Security scanning flags hardcoded keys

**Solution:**

**Never hardcode API keys in source code or configuration files.**

Use CI/CD secret management:

**GitHub Actions:**
```yaml
- name: Run Tests
  env:
    XPING__APIKEY: ${{ secrets.XPING_APIKEY }}
    XPING__PROJECTID: ${{ secrets.XPING_PROJECTID }}
  run: dotnet test
```

**Azure DevOps:**
```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'test'
  env:
    XPING__APIKEY: $(XpingApiKey)      # From pipeline variables
    XPING__PROJECTID: $(XpingProjectId)
```

**GitLab CI:**
```yaml
test:
  script:
    - dotnet test
  variables:
    XPING__APIKEY: $XPING_APIKEY      # From GitLab CI/CD variables
    XPING__PROJECTID: $XPING_PROJECTID
```

See [CI/CD Setup Guide](../getting-started/ci-cd-setup.md) for platform-specific instructions.

---

### Parallel Test Execution Issues

**Symptoms:**
- Tests fail when running in parallel
- Inconsistent results between parallel and sequential runs
- Thread safety warnings

**Solution:**

The Xping SDK is designed to be thread-safe and supports parallel test execution. However, proper initialization and cleanup are essential.

> **Note:** Setup and teardown patterns vary by test framework. See examples below for your framework.

**Key requirements (all frameworks):**
- Initialize once per test session (not per test)
- Ensure proper cleanup/teardown
- Avoid calling `Initialize()` in individual test methods

**NUnit:**
```csharp
[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        XpingContext.Initialize();
    }

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        await XpingContext.FlushAsync();
        await XpingContext.DisposeAsync();
    }
}
```

**MSTest:**
```csharp
[TestClass]
public static class XpingSetup
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        XpingContext.Initialize();
    }

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FlushAsync();
        await XpingContext.DisposeAsync();
    }
}
```

**xUnit:**
```csharp
// Add to AssemblyInfo.cs - initialization and cleanup are automatic
[assembly: TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

For detailed setup instructions, see the [Getting Started guides](../getting-started/quickstart-nunit.md) for your test framework.

---

## Data & Metrics Issues

### Missing Test Results

**Symptoms:**
- Some tests appear in dashboard, others don't
- Inconsistent test result uploads

**Common Causes:**

#### 1. Sampling Enabled

If sampling is configured, only a percentage of tests are tracked:

```json
{
  "Xping": {
    "SamplingRate": 0.5  // Only 50% of tests tracked
  }
}
```

**Solution:** Set `SamplingRate` to `1.0` (100%) to track all tests.

---

#### 2. Test Name Issues

Very long test names may be truncated or rejected.

**Solution:** Keep test names reasonable (<500 characters).

---

#### 3. Upload Failures

Individual upload failures may result in lost test data.

**Solution:** Enable logging to identify upload issues. See [Debugging Guide](debugging.md).

---

### Flaky Test Detection Not Working

**Symptoms:**
- Tests marked as flaky in your suite aren't detected by Xping
- Confidence scores don't reflect test instability

**Important:** Flaky test detection requires **statistical data over time**.

**Requirements:**
- **Minimum 10 test executions** for statistical analysis
- **Consistent test names** across runs
- **Time to process** uploaded data (asynchronous processing)

**What to expect:**
1. **First run:** Tests upload, no flakiness data yet
2. **Runs 2-9:** Data accumulates, analysis begins
3. **Run 10+:** Flakiness detection becomes reliable

**If detection still isn't working after 10+ runs:**

1. Verify tests are actually flaky (not environment issues)
2. Check that test names are consistent across runs
3. Ensure sufficient pass/fail variation for detection
4. Review dashboard filters—flaky tests may be filtered out

---

## Troubleshooting Checklist

Before seeking support, verify:

- ✅ API Key and Project ID are configured correctly
- ✅ Network connectivity to `upload.xping.io` is working
- ✅ SDK is enabled (`Enabled = true`)
- ✅ Configuration is valid (proper JSON syntax, value ranges)
- ✅ Logs are enabled to see detailed diagnostics
- ✅ Latest SDK version is installed
- ✅ Test framework integration is set up correctly

---

## Getting Help

If you're still experiencing issues after trying these solutions:

1. **Enable debug logging** to capture detailed diagnostics (see [Debugging Guide](debugging.md))
2. **Check the documentation:**
   - [Configuration Reference](../configuration/configuration-reference.md)
   - [Getting Started Guides](../getting-started/quickstart-nunit.md)
   - [CI/CD Setup Guide](../getting-started/ci-cd-setup.md)
3. **Contact support:**
   - Email: support@xping.io
   - Include: SDK version, .NET version, test framework, logs, and configuration (redact API keys!)

---

## Related Documentation

- [Debugging Guide](debugging.md) - Enable logging and diagnose SDK issues
- [Configuration Reference](../configuration/configuration-reference.md) - Complete configuration guide
- [Performance Overview](../guides/optimization/performance-overview.md) - Optimize SDK performance
- [CI/CD Setup Guide](../getting-started/ci-cd-setup.md) - Platform-specific CI/CD instructions
