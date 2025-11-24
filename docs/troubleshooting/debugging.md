---
uid: troubleshooting-debugging
title: Debugging Guide
---

# Debugging Guide

This guide explains how to diagnose and troubleshoot issues with the Xping SDK using logging, diagnostics, and inspection techniques.

---

## Enabling SDK Logging

The Xping SDK includes built-in diagnostic logging to help troubleshoot integration issues. By default, logging outputs to the console.

### Console Logging (Default)

Enable logging programmatically during SDK initialization:

```csharp
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Diagnostics;

var config = new XpingConfiguration
{
    ApiKey = "your-api-key",
    ProjectId = "your-project",
    LogLevel = XpingLogLevel.Debug  // Enable debug logging
};

XpingContext.Initialize(config);
```

### Log Levels

The SDK supports four log levels, each including all levels below it:

| Level | Description | Use Case |
|-------|-------------|----------|
| `XpingLogLevel.None` | No logging output | Production when logs aren't needed |
| `XpingLogLevel.Error` | Errors and critical failures only | Production when you only want failure notifications |
| `XpingLogLevel.Warning` | Warnings and errors | Production to track potential issues |
| `XpingLogLevel.Info` | Informational, warnings, errors (default) | Development and CI/CD to track activity |
| `XpingLogLevel.Debug` | All messages including detailed diagnostics | Troubleshooting and debugging SDK issues |

**Recommended settings:**
- **Development/Local:** `XpingLogLevel.Debug` or `XpingLogLevel.Info`
- **CI/CD:** `XpingLogLevel.Info`
- **Production:** `XpingLogLevel.Warning` or `XpingLogLevel.Error`

---

## Custom Logger Implementation

For integration with your existing logging infrastructure (Serilog, NLog, log4net, etc.), implement the `IXpingLogger` interface:

### Example: Serilog Integration

```csharp
using Xping.Sdk.Core.Diagnostics;
using Serilog;

public class SerilogXpingLogger : IXpingLogger
{
    private readonly ILogger _logger;

    public SerilogXpingLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void LogError(string message) => _logger.Error("[Xping] {Message}", message);

    public void LogWarning(string message) => _logger.Warning("[Xping] {Message}", message);

    public void LogInfo(string message) => _logger.Information("[Xping] {Message}", message);

    public void LogDebug(string message) => _logger.Debug("[Xping] {Message}", message);

    public bool IsEnabled(XpingLogLevel level)
    {
        return level switch
        {
            XpingLogLevel.Error => _logger.IsEnabled(Serilog.Events.LogEventLevel.Error),
            XpingLogLevel.Warning => _logger.IsEnabled(Serilog.Events.LogEventLevel.Warning),
            XpingLogLevel.Info => _logger.IsEnabled(Serilog.Events.LogEventLevel.Information),
            XpingLogLevel.Debug => _logger.IsEnabled(Serilog.Events.LogEventLevel.Debug),
            _ => false
        };
    }
}

// Usage
var config = new XpingConfiguration
{
    ApiKey = "your-api-key",
    ProjectId = "your-project",
    Logger = new SerilogXpingLogger(Log.Logger)
};

XpingContext.Initialize(config);
```

### Example: NLog Integration

```csharp
using Xping.Sdk.Core.Diagnostics;
using NLog;

public class NLogXpingLogger : IXpingLogger
{
    private readonly ILogger _logger;

    public NLogXpingLogger()
    {
        _logger = LogManager.GetLogger("Xping");
    }

    public void LogError(string message) => _logger.Error(message);

    public void LogWarning(string message) => _logger.Warn(message);

    public void LogInfo(string message) => _logger.Info(message);

    public void LogDebug(string message) => _logger.Debug(message);

    public bool IsEnabled(XpingLogLevel level)
    {
        return level switch
        {
            XpingLogLevel.Error => _logger.IsErrorEnabled,
            XpingLogLevel.Warning => _logger.IsWarnEnabled,
            XpingLogLevel.Info => _logger.IsInfoEnabled,
            XpingLogLevel.Debug => _logger.IsDebugEnabled,
            _ => false
        };
    }
}
```

---

## Understanding Log Messages

### Informational Messages

These indicate normal SDK operation:

```
[Info] Xping SDK initialized (v1.0.4)
[Info] Configuration loaded: BatchSize=100, FlushInterval=30s
[Info] Uploaded 45 test executions (ReceiptId: abc123)
[Info] Environment detected: CI (GitHub Actions)
```

**What it means:** SDK is working correctly, uploads are successful.

---

### Warning Messages

These indicate potential issues that don't prevent operation:

```
[Warning] Upload retry attempt 2/3 after network timeout
[Warning] Stack trace capture failed for test 'MyTest': Stack trace unavailable
[Warning] Status: Upload attempts will resume after circuit breaker resets (30 seconds)
```

**What to do:**
- **Retries:** Monitor if retries succeed. Persistent retries may indicate network issues.
- **Stack trace failures:** Non-critical; test data still uploads without stack traces.
- **Circuit breaker warnings:** Indicates repeated failures; check underlying error logs.

---

### Error Messages

These indicate failures that prevent SDK functionality:

#### Authentication Errors

```
[Error] Upload failed: Authentication failed (401): Invalid API Key or Project ID
  Action: Verify credentials at https://app.xping.io
```

**Solution:** Verify your API key and Project ID are correct.

---

#### Configuration Errors

```
[Error] Upload skipped: API Key and Project ID are required but not configured
[Info] Action: Configure credentials in appsettings.json or environment variables
[Info]   - Set XPING__APIKEY and XPING__PROJECTID environment variables
[Info]   - Or add 'Xping' section to appsettings.json
```

**Solution:** Configure required credentials.

---

#### Network Errors

```
[Error] Network error: Unable to connect to upload.xping.io
[Error] HTTP request failed: The remote name could not be resolved: 'upload.xping.io'
```

**Solution:** Check network connectivity, firewall rules, DNS resolution.

---

#### Server Errors

```
[Error] Upload failed: Server error (503): API temporarily unavailable
  Status: Uploads will be retried automatically
```

**Solution:** Wait for automatic retry. If persistent, check Xping status page or contact support.

---

#### Circuit Breaker Errors

```
[Error] Circuit breaker is open: Too many consecutive failures
[Warning] Status: Upload attempts will resume after circuit breaker resets (30 seconds)
```

**What it means:** The SDK has detected repeated failures (50% failure rate over 10+ attempts) and temporarily stopped upload attempts.

**Solution:** 
1. Fix the underlying issue (network, credentials, API availability)
2. Wait 30 seconds for automatic reset
3. Enable debug logging to see what's causing failures

---

### Debug Messages

These provide detailed diagnostic information:

```
[Debug] Collecting test execution: TestName='MyTest', Outcome=Passed, Duration=125ms
[Debug] Batch size reached (100 tests), uploading to API
[Debug] HTTP POST to https://upload.xping.io?sessionId=abc123-def456
[Debug] Compression enabled: Original=45KB, Compressed=12KB (73% reduction)
[Debug] Upload completed: StatusCode=200, ReceiptId=xyz789
```

**Use case:** Troubleshooting SDK behavior, verifying configuration, tracking upload timing.

---

## Verifying Configuration at Runtime

To verify what configuration values the SDK is using:

### Using Debug Logging

Enable `XpingLogLevel.Debug` and check the initialization messages:

```
[Info] Xping SDK initialized (v1.0.4)
[Debug] Configuration loaded:
  - ApiEndpoint: https://upload.xping.io
  - ProjectId: my-project
  - BatchSize: 100
  - FlushInterval: 00:00:30
  - SamplingRate: 1.0
  - Enabled: True
  - Environment: CI
```

### Programmatic Inspection

Access the current configuration via `XpingContext`:

```csharp
// After initialization
var config = XpingContext.Configuration;

Console.WriteLine($"API Endpoint: {config.ApiEndpoint}");
Console.WriteLine($"Project ID: {config.ProjectId}");
Console.WriteLine($"Batch Size: {config.BatchSize}");
Console.WriteLine($"SDK Enabled: {config.Enabled}");
Console.WriteLine($"Sampling Rate: {config.SamplingRate}");
```

---

## Testing API Connectivity Independently

### Command Line Testing

Test HTTPS connectivity to Xping API:

**Linux/macOS:**
```bash
# Test basic connectivity
curl -v https://upload.xping.io

# Test with authentication (replace with your credentials)
curl -X POST https://upload.xping.io \
  -H "X-API-Key: your-api-key" \
  -H "X-Project-Id: your-project-id" \
  -H "Content-Type: application/json" \
  -d '{"executions":[]}'
```

**Windows PowerShell:**
```powershell
# Test connectivity
Test-NetConnection -ComputerName upload.xping.io -Port 443

# Test with authentication
$headers = @{
    "X-API-Key" = "your-api-key"
    "X-Project-Id" = "your-project-id"
    "Content-Type" = "application/json"
}
Invoke-RestMethod -Uri "https://upload.xping.io" -Method Post -Headers $headers -Body '{"executions":[]}'
```

**Expected responses:**
- **200 OK:** Connection successful, credentials valid
- **401 Unauthorized:** Invalid API key or Project ID
- **403 Forbidden:** Valid credentials but insufficient permissions
- **Connection refused/timeout:** Network connectivity issue

---

## Inspecting Network Traffic

For advanced troubleshooting, inspect HTTP requests and responses:

### Using Fiddler (Windows/macOS)

1. Install [Fiddler](https://www.telerik.com/fiddler)
2. Configure Fiddler to decrypt HTTPS traffic (Tools → Options → HTTPS)
3. Run your tests
4. Filter for `upload.xping.io` in Fiddler
5. Inspect request headers, body, and response

**What to check:**
- Request headers include `X-API-Key` and `X-Project-Id`
- Request body contains test execution data
- Response status code and body

---

### Using Wireshark (Advanced)

For network-level inspection:

1. Install [Wireshark](https://www.wireshark.org/)
2. Start capture on your network interface
3. Apply filter: `http.host contains "upload.xping.io"`
4. Run your tests
5. Analyze packets

**Note:** HTTPS traffic is encrypted; Wireshark shows TCP/TLS handshake but not decrypted payload.

---

### Using .NET HTTP Logging

Enable .NET HTTP client logging in your test project:

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "System.Net.Http.HttpClient": "Trace"  // Enable HTTP logging
    }
  }
}
```

Or via environment variable:

```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
export DOTNET_SYSTEM_NET_HTTP_LOGGING_LEVEL=Trace
```

This logs all HTTP requests and responses, including Xping SDK uploads.

---

## Common Error Codes

### HTTP Status Codes

| Code | Message | Meaning | Solution |
|------|---------|---------|----------|
| **200** | OK | Upload successful | None needed |
| **400** | Bad Request | Invalid request payload | Check SDK version, report bug if persistent |
| **401** | Unauthorized | Invalid API key or Project ID | Verify credentials at https://app.xping.io |
| **403** | Forbidden | Valid credentials, insufficient permissions | Check project access in dashboard |
| **429** | Too Many Requests | Rate limit exceeded | Reduce frequency, increase batch size |
| **500** | Internal Server Error | Xping API error | Retry automatically handled; contact support if persistent |
| **503** | Service Unavailable | API temporarily down | Retry automatically handled; check status page |

---

### SDK-Specific Errors

| Error Message | Meaning | Solution |
|---------------|---------|----------|
| `API Key and Project ID are required` | Missing credentials | Configure `ApiKey` and `ProjectId` |
| `Circuit breaker is open` | Too many consecutive failures | Fix underlying issue, wait for reset |
| `Network error: Unable to connect` | Network connectivity issue | Check firewall, DNS, proxy |
| `HTTP request failed: Timeout` | Request took too long | Increase `UploadTimeout` or reduce `BatchSize` |
| `Configuration validation failed` | Invalid configuration value | Check value ranges in configuration |

---

## Collecting Diagnostic Information for Support

If you need to contact support, collect the following information:

### 1. SDK Version

```bash
# Check installed package version
dotnet list package | grep Xping
```

Or check in your `.csproj`:
```xml
<PackageReference Include="Xping.Sdk.NUnit" Version="1.0.4" />
```

---

### 2. Environment Information

```bash
# .NET version
dotnet --version

# OS information
uname -a  # Linux/macOS
systeminfo  # Windows
```

---

### 3. Configuration (Redact Secrets!)

Share your configuration **without exposing your API key**:

```json
{
  "Xping": {
    "ApiEndpoint": "https://upload.xping.io",
    "ApiKey": "xpg_***_REDACTED_***",  // ⚠️ Redact this!
    "ProjectId": "my-project",
    "BatchSize": 100,
    "FlushInterval": "00:00:30"
  }
}
```

---

### 4. SDK Logs

Enable `XpingLogLevel.Debug` and capture logs:

**Option 1: Redirect console output to file:**
```bash
dotnet test > test-output.log 2>&1
```

**Option 2: Use custom logger to write to file**

---

### 5. Test Framework and Version

```bash
# Check test framework package version
dotnet list package | grep -E "(NUnit|xunit|MSTest)"
```

---

### 6. Minimal Reproducible Example

Create a minimal test project that reproduces the issue:

```csharp
using NUnit.Framework;
using Xping.Sdk.Core.Configuration;

[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void GlobalSetup()
    {
        var config = new XpingConfiguration
        {
            ApiKey = "xpg_test_key",
            ProjectId = "test-project",
            LogLevel = Xping.Sdk.Core.Diagnostics.XpingLogLevel.Debug
        };
        XpingContext.Initialize(config);
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        await XpingContext.FlushAsync();
    }
}

public class SampleTests
{
    [Test]
    public void Test1()
    {
        Assert.Pass();
    }

    [Test]
    public void Test2()
    {
        Assert.Fail("Intentional failure");
    }
}
```

---

## Troubleshooting Workflow

Follow this systematic approach when debugging issues:

### Step 1: Enable Debug Logging

```csharp
var config = new XpingConfiguration
{
    ApiKey = "your-api-key",
    ProjectId = "your-project",
    LogLevel = XpingLogLevel.Debug
};
XpingContext.Initialize(config);
```

### Step 2: Run Tests and Capture Output

```bash
dotnet test --logger "console;verbosity=detailed" > test-output.log 2>&1
```

### Step 3: Review Logs

Look for:
- **Errors** (red flags indicating failures)
- **Warnings** (yellow flags indicating potential issues)
- **Upload messages** (verify uploads are happening)
- **Configuration values** (verify settings are correct)

### Step 4: Test Connectivity

```bash
curl -v https://upload.xping.io
```

### Step 5: Verify Configuration

Check:
- API Key and Project ID are set
- Values are not empty or placeholder text
- No typos or extra spaces
- Environment variables are set correctly (double underscores!)

### Step 6: Check Common Issues

Review [Common Issues](common-issues.md) for known problems and solutions.

### Step 7: Contact Support

If still unresolved, contact support with collected diagnostic information.

---

## Advanced Diagnostics

### Memory Profiling

If you suspect memory leaks or high memory usage:

**Using dotMemory (JetBrains):**
1. Install [dotMemory](https://www.jetbrains.com/dotmemory/)
2. Profile your test run
3. Look for retained `TestExecution` objects
4. Check if `XpingContext` is properly disposed

**Using Visual Studio Diagnostic Tools:**
1. Debug → Performance Profiler
2. Select "Memory Usage"
3. Run tests
4. Take memory snapshots before/after
5. Compare snapshots to identify growth

---

### Performance Profiling

To measure SDK overhead:

**Using BenchmarkDotNet:**

See [Performance Testing](../performance/overview.md) for detailed benchmarking instructions.

**Using dotnet-trace:**

```bash
# Collect trace
dotnet-trace collect --process-id <test-process-id> --providers Microsoft-Windows-DotNETRuntime

# Analyze with PerfView (Windows) or speedscope.app
```

---

### Concurrency Debugging

If you suspect thread safety issues:

1. **Enable debug logging** to see thread IDs (if logged)
2. **Add thread identifiers** to your custom logger:
   ```csharp
   public void LogDebug(string message)
   {
       var threadId = Thread.CurrentThread.ManagedThreadId;
       _logger.Debug($"[Thread {threadId}] {message}");
   }
   ```
3. **Look for race conditions** or lock contention in logs

---

## Best Practices for Debugging

### 1. Use Appropriate Log Levels

- **Development:** `Debug` or `Info` to see everything
- **CI/CD:** `Info` to track activity without overwhelming logs
- **Production:** `Warning` or `Error` to minimize noise

### 2. Log to Files in CI/CD

Redirect logs to files for persistence:

```bash
dotnet test > test-output.log 2>&1
```

Or configure your CI/CD to capture test output.

### 3. Use Custom Loggers

Integrate with your existing logging infrastructure:

```csharp
var config = new XpingConfiguration
{
    Logger = new SerilogXpingLogger(Log.Logger),
    // ... other settings
};
```

### 4. Test Locally First

Before debugging in CI/CD:
1. Reproduce the issue locally
2. Enable debug logging
3. Verify configuration
4. Test connectivity

### 5. Isolate the Problem

Create a minimal test project that reproduces the issue:
- Remove unnecessary dependencies
- Use simple test cases
- Simplify configuration

### 6. Check SDK Version

Ensure you're using the latest SDK version:

```bash
dotnet list package --outdated
```

Update if needed:

```bash
dotnet add package Xping.Sdk.NUnit --version 1.0.4
```

---

## Related Documentation

- [Common Issues](common-issues.md) - Solutions to frequently encountered problems
- [Configuration Reference](../configuration/configuration-reference.md) - Complete configuration guide
- [Performance Testing](../performance/overview.md) - Performance benchmarking and optimization
- [CI/CD Setup Guide](../guides/cicd-setup.md) - CI/CD integration instructions

---

## Getting Help

If you need additional support:

1. **Check documentation:** Review guides and reference materials
2. **Search issues:** Look for similar problems in GitHub issues
3. **Contact support:**
   - Email: support@xping.io
   - Include: SDK version, .NET version, logs, configuration (redact API keys!)
4. **Report bugs:** Open an issue at https://github.com/xping-dev/sdk-dotnet/issues

When reporting issues, always include:
- SDK version
- .NET version
- Test framework and version
- Operating system
- Configuration (redacted)
- Debug logs showing the problem
- Minimal reproducible example
