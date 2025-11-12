# Xping SDK Logging Strategy

## Executive Summary

This document outlines the logging and user feedback strategy for the Xping SDK. Currently, the SDK operates silently with no visibility into its status, errors, or upload progress. This creates a poor user experience, especially when issues occur (e.g., authentication failures, network problems, offline queue usage).

## Current State Analysis

### What We Found

1. **No Logging Infrastructure**: The SDK has no logging abstraction or implementation
2. **Silent Failures**: Upload failures, authentication errors, and network issues occur without user notification
3. **No Statistics Output**: Users cannot see how many tests were recorded, uploaded, failed, or sampled
4. **Limited Visibility**: TestExecutionCollector tracks stats internally but never exposes them to users
5. **Configuration Issues**: Users don't know if their API keys or configuration are incorrect until they check the dashboard

### Existing Components

- **TestExecutionCollector**: Tracks internal stats (recorded, uploaded, failed, sampled, flushes)
- **XpingApiClient**: Handles uploads with detailed error information in UploadResult
- **UploadResult**: Contains Success, ErrorMessage, ExecutionCount, ReceiptId
- **XpingConfiguration**: Has Enabled flag, sampling rate, batch size, retry settings

## Test Framework Logging Considerations

### Framework-Specific Mechanisms

#### MSTest
- **TestContext.WriteLine()**: Outputs to test results, visible in Test Explorer and CI logs
- **Console.WriteLine()**: Works in local runs, captured in CI/CD pipelines
- Best for: Test-level diagnostics

#### XUnit
- **ITestOutputHelper**: Tied to specific test execution, appears in test results
- **Console.WriteLine()**: Works globally but may not appear in all runners
- Best for: Test-level diagnostics

#### NUnit
- **TestContext.WriteLine()**: Outputs to test results and console
- **Console.Out/Console.Error**: Always available, works in all contexts
- Best for: Assembly-level and test-level diagnostics

### CI/CD Environment Behavior

All frameworks properly capture:
- **Console.WriteLine()** → Standard output (stdout)
- **Console.Error.WriteLine()** → Standard error (stderr)

CI systems (GitHub Actions, Azure DevOps, Jenkins, etc.) capture both streams and include them in build logs.

### Recommendation: Console + Diagnostic Output

**Primary**: `Console.WriteLine()` / `Console.Error.WriteLine()`
- ✅ Works in all test frameworks
- ✅ Captured by all CI/CD systems
- ✅ Visible in local terminal output
- ✅ No framework dependencies
- ✅ Works at assembly initialization level (before tests run)

**Secondary**: Framework-specific diagnostic output (TestContext, ITestOutputHelper)
- For test-level detailed diagnostics
- Optional enhancement in future versions

## Critical User Feedback Points

### 1. Initialization Events

#### Success Scenario
```
[Xping SDK] Initialized successfully
[Xping SDK] Project: abc123-def456 | Environment: CI | Endpoint: https://api.xping.io
[Xping SDK] Batch Size: 100 | Sampling Rate: 100% | Offline Queue: Enabled
```

#### Configuration Issues
```
[Xping SDK] WARNING: SDK is disabled (Enabled=false)
[Xping SDK] WARNING: API Key not configured - uploads will be skipped
[Xping SDK] WARNING: Project ID not configured - uploads will be skipped
[Xping SDK] ERROR: Invalid configuration:
  - ApiEndpoint must be a valid HTTP or HTTPS URL
  - BatchSize must be greater than zero
```

### 2. Upload Events

#### Success
```
[Xping SDK] ✓ Uploaded 47 test executions (Receipt: rx_2h4j8k9l0m1n2o3p)
```

#### Authentication Failure
```
[Xping SDK] ✗ Upload failed: Authentication error (401)
  Cause: Invalid API Key or Project ID
  Action: Verify credentials at https://app.xping.io
  Status: 47 executions queued for retry
```

#### Network Failure
```
[Xping SDK] ✗ Upload failed: Network error
  Cause: HTTP request failed: Connection timeout
  Status: 47 executions queued for retry
  Queue Size: 147 executions pending
```

#### Circuit Breaker Open
```
[Xping SDK] ✗ Upload skipped: Circuit breaker is open
  Cause: Too many consecutive failures (50% failure rate exceeded)
  Status: Will retry in 30 seconds
  Queue Size: 247 executions pending
```

### 3. Flush/Cleanup Events

#### Assembly Cleanup
```
[Xping SDK] Flushing test executions...
[Xping SDK] ✓ Final upload: 23 executions
[Xping SDK] Session Statistics:
  - Total Recorded: 150 tests
  - Total Sampled: 150 tests (100%)
  - Total Uploaded: 150 tests
  - Total Failed: 0 tests
  - Total Flushes: 2
  - Offline Queue: 0 pending
```

#### With Failures
```
[Xping SDK] Session Statistics:
  - Total Recorded: 150 tests
  - Total Sampled: 150 tests (100%)
  - Total Uploaded: 103 tests
  - Total Failed: 47 tests
  - Total Flushes: 3
  - Offline Queue: 47 pending
[Xping SDK] WARNING: Some test executions could not be uploaded
  Queued items will be retried in the next test session
```

### 4. Sampling Information

When sampling rate < 100%:
```
[Xping SDK] Sampling Rate: 25% (approximately 1 in 4 tests will be tracked)
```

### 5. Offline Queue Events

When queue is being processed:
```
[Xping SDK] Processing offline queue (147 pending executions)...
[Xping SDK] ✓ Uploaded 100 queued executions
[Xping SDK] Offline queue: 47 executions remaining
```

## Proposed Logging Architecture

### Phase 1: Core Logging Infrastructure (MVP)

```csharp
namespace Xping.Sdk.Core.Diagnostics;

/// <summary>
/// Log levels for SDK diagnostics.
/// </summary>
public enum XpingLogLevel
{
    /// <summary>No logging.</summary>
    None = 0,
    
    /// <summary>Errors and critical issues only.</summary>
    Error = 1,
    
    /// <summary>Warnings and errors.</summary>
    Warning = 2,
    
    /// <summary>Informational messages, warnings, and errors (default).</summary>
    Info = 3,
    
    /// <summary>Detailed diagnostic information (for debugging).</summary>
    Debug = 4
}

/// <summary>
/// Simple logger interface for Xping SDK diagnostics.
/// </summary>
public interface IXpingLogger
{
    void LogError(string message);
    void LogWarning(string message);
    void LogInfo(string message);
    void LogDebug(string message);
    bool IsEnabled(XpingLogLevel level);
}

/// <summary>
/// Default console-based logger implementation.
/// </summary>
public sealed class XpingConsoleLogger : IXpingLogger
{
    private readonly XpingLogLevel _minLevel;
    private const string Prefix = "[Xping SDK]";

    public XpingConsoleLogger(XpingLogLevel minLevel = XpingLogLevel.Info)
    {
        _minLevel = minLevel;
    }

    public void LogError(string message)
    {
        if (IsEnabled(XpingLogLevel.Error))
        {
            Console.Error.WriteLine($"{Prefix} ERROR: {message}");
        }
    }

    public void LogWarning(string message)
    {
        if (IsEnabled(XpingLogLevel.Warning))
        {
            Console.WriteLine($"{Prefix} WARNING: {message}");
        }
    }

    public void LogInfo(string message)
    {
        if (IsEnabled(XpingLogLevel.Info))
        {
            Console.WriteLine($"{Prefix} {message}");
        }
    }

    public void LogDebug(string message)
    {
        if (IsEnabled(XpingLogLevel.Debug))
        {
            Console.WriteLine($"{Prefix} DEBUG: {message}");
        }
    }

    public bool IsEnabled(XpingLogLevel level) => level <= _minLevel;
}

/// <summary>
/// Null logger that discards all log messages.
/// </summary>
public sealed class XpingNullLogger : IXpingLogger
{
    public static readonly XpingNullLogger Instance = new();
    
    public void LogError(string message) { }
    public void LogWarning(string message) { }
    public void LogInfo(string message) { }
    public void LogDebug(string message) { }
    public bool IsEnabled(XpingLogLevel level) => false;
}
```

### Configuration Integration

```csharp
public sealed class XpingConfiguration
{
    // ... existing properties ...

    /// <summary>
    /// Gets or sets the minimum log level for SDK diagnostics.
    /// Default is Info. Set to None to disable all logging.
    /// </summary>
    public XpingLogLevel LogLevel { get; set; } = XpingLogLevel.Info;

    /// <summary>
    /// Gets or sets a custom logger implementation.
    /// If null, uses default console logger.
    /// </summary>
    public IXpingLogger? Logger { get; set; }
}
```

### Integration Points

#### 1. XpingContext (MSTest/NUnit/XUnit)

```csharp
public static TestExecutionCollector Initialize(XpingConfiguration configuration)
{
    var logger = configuration.Logger ?? new XpingConsoleLogger(configuration.LogLevel);
    
    // Validate configuration
    var errors = configuration.Validate();
    if (errors.Count > 0)
    {
        logger.LogError("Invalid configuration:");
        foreach (var error in errors)
        {
            logger.LogError($"  - {error}");
        }
    }
    
    // Check for common misconfigurations
    if (!configuration.Enabled)
    {
        logger.LogWarning("SDK is disabled (Enabled=false)");
    }
    
    if (string.IsNullOrWhiteSpace(configuration.ApiKey))
    {
        logger.LogWarning("API Key not configured - uploads will be skipped");
    }
    
    if (string.IsNullOrWhiteSpace(configuration.ProjectId))
    {
        logger.LogWarning("Project ID not configured - uploads will be skipped");
    }
    
    // Log initialization success
    logger.LogInfo("Initialized successfully");
    logger.LogInfo($"Project: {configuration.ProjectId} | Environment: {configuration.Environment}");
    logger.LogDebug($"Endpoint: {configuration.ApiEndpoint}");
    logger.LogDebug($"Batch Size: {configuration.BatchSize} | Sampling: {configuration.SamplingRate:P0}");
    
    return collector;
}
```

#### 2. TestExecutionCollector

```csharp
public sealed class TestExecutionCollector : ITestExecutionCollector
{
    private readonly IXpingLogger _logger;
    
    public TestExecutionCollector(
        ITestResultUploader uploader, 
        XpingConfiguration config,
        IXpingLogger? logger = null)
    {
        _logger = logger ?? XpingNullLogger.Instance;
        // ... existing initialization ...
    }
    
    private async Task FlushInternalAsync(CancellationToken cancellationToken)
    {
        // ... existing flush logic ...
        
        var result = await _uploader.UploadAsync(batch, cancellationToken);
        
        if (result.Success)
        {
            Interlocked.Add(ref _totalUploaded, batch.Count);
            _logger.LogInfo($"✓ Uploaded {batch.Count} test executions" +
                (string.IsNullOrEmpty(result.ReceiptId) ? "" : $" (Receipt: {result.ReceiptId})"));
        }
        else
        {
            Interlocked.Add(ref _totalFailed, batch.Count);
            _logger.LogError($"✗ Upload failed: {result.ErrorMessage}");
            
            if (_config.EnableOfflineQueue)
            {
                _logger.LogWarning($"Status: {batch.Count} executions queued for retry");
            }
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        // ... flush logic ...
        
        // Log final statistics
        var stats = await GetStatsAsync();
        _logger.LogInfo("Session Statistics:");
        _logger.LogInfo($"  - Total Recorded: {stats.TotalRecorded} tests");
        _logger.LogInfo($"  - Total Sampled: {stats.TotalSampled} tests ({_config.SamplingRate:P0})");
        _logger.LogInfo($"  - Total Uploaded: {stats.TotalUploaded} tests");
        
        if (stats.TotalFailed > 0)
        {
            _logger.LogWarning($"  - Total Failed: {stats.TotalFailed} tests");
        }
        
        _logger.LogDebug($"  - Total Flushes: {stats.TotalFlushes}");
        _logger.LogDebug($"  - Buffer Count: {stats.BufferCount}");
    }
}
```

#### 3. XpingApiClient

```csharp
public sealed class XpingApiClient : ITestResultUploader
{
    private readonly IXpingLogger _logger;
    
    public XpingApiClient(
        HttpClient httpClient,
        XpingConfiguration config,
        IOfflineQueue? offlineQueue = null,
        IXpingSerializer? serializer = null,
        IXpingLogger? logger = null)
    {
        _logger = logger ?? XpingNullLogger.Instance;
        // ... existing initialization ...
    }
    
    public async Task<UploadResult> UploadAsync(
        IEnumerable<TestExecution> executions,
        CancellationToken cancellationToken = default)
    {
        // Enhanced error messaging
        if (string.IsNullOrWhiteSpace(_config.ApiKey) || string.IsNullOrWhiteSpace(_config.ProjectId))
        {
            var errorMsg = "Upload skipped: API Key and Project ID are required but not configured";
            _logger.LogError(errorMsg);
            _logger.LogInfo("Action: Configure credentials in appsettings.json or environment variables");
            
            return new UploadResult
            {
                Success = false,
                ErrorMessage = errorMsg
            };
        }
        
        // Process offline queue first
        if (_offlineQueue != null)
        {
            var queueSize = await _offlineQueue.GetQueueSizeAsync(cancellationToken);
            if (queueSize > 0)
            {
                _logger.LogInfo($"Processing offline queue ({queueSize} pending executions)...");
                await ProcessQueuedExecutionsAsync(cancellationToken);
            }
        }
        
        // ... upload logic ...
    }
    
    private async Task<UploadResult> UploadBatchAsync(...)
    {
        try
        {
            // ... existing upload logic ...
        }
        catch (BrokenCircuitException ex)
        {
            var errorMsg = "Circuit breaker is open: Too many consecutive failures";
            _logger.LogError(errorMsg);
            _logger.LogWarning("Status: Will retry when circuit breaker resets (30 seconds)");
            
            return new UploadResult
            {
                Success = false,
                ErrorMessage = errorMsg
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Network error: {ex.Message}");
            // ... existing error handling ...
        }
        // ... other exception handlers ...
    }
    
    private static async Task<UploadResult> ProcessResponseAsync(
        HttpResponseMessage response, 
        int executionCount)
    {
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var errorContent = await response.Content.ReadAsStringAsync();
            
            // Enhanced error messages with actionable guidance
            var errorMsg = statusCode switch
            {
                401 => "Authentication failed: Invalid API Key or Project ID\n" +
                       "  Action: Verify credentials at https://app.xping.io",
                403 => "Authorization failed: Insufficient permissions\n" +
                       "  Action: Check project access at https://app.xping.io",
                429 => "Rate limit exceeded: Too many requests\n" +
                       "  Action: Reduce test execution frequency or contact support",
                >= 500 => $"Server error ({statusCode}): API temporarily unavailable\n" +
                          "  Action: Uploads will be retried automatically",
                _ => $"API returned {statusCode}: {errorContent}"
            };
            
            return new UploadResult
            {
                Success = false,
                ErrorMessage = errorMsg
            };
        }
        
        // ... success handling ...
    }
}
```

## Configuration Examples

### appsettings.json

```json
{
  "Xping": {
    "ApiKey": "your-api-key",
    "ProjectId": "your-project-id",
    "LogLevel": "Info",  // None, Error, Warning, Info, Debug
    "Enabled": true,
    "BatchSize": 100,
    "SamplingRate": 1.0
  }
}
```

### Environment Variables

```bash
# Core settings
XPING__APIKEY=your-api-key
XPING__PROJECTID=your-project-id
XPING__LOGLEVEL=Info  # None, Error, Warning, Info, Debug

# CI-specific settings
XPING__ENABLED=true
XPING__ENVIRONMENT=CI
```

### Programmatic Configuration

```csharp
XpingContext.Initialize(new XpingConfiguration
{
    ApiKey = "your-api-key",
    ProjectId = "your-project-id",
    LogLevel = XpingLogLevel.Debug,  // Enable detailed logging
    Logger = new CustomLogger()      // Or use custom logger
});
```

## Verbosity Levels

| Level | When to Use | What Gets Logged |
|-------|-------------|------------------|
| **None** | Production, minimal overhead | Nothing |
| **Error** | Production, failures only | Authentication errors, network failures, critical issues |
| **Warning** | Production, issues that need attention | Configuration warnings, upload retries, queue growth |
| **Info** | Development, CI/CD (default) | Initialization, upload success/failure, statistics |
| **Debug** | Troubleshooting | Configuration details, batch sizes, queue operations |

## Phase 2: Future Enhancements (Post-MVP)

1. **Structured Logging**: Support for structured log formats (JSON)
2. **Custom Log Providers**: Integration with Microsoft.Extensions.Logging
3. **Test Framework Integration**: Use TestContext.WriteLine() / ITestOutputHelper for test-specific logs
4. **Log Filtering**: Allow filtering by component (collector, uploader, queue)
5. **Performance Metrics**: Log upload duration, compression ratios, retry attempts
6. **Dashboard Integration**: Send diagnostic events to Xping dashboard for monitoring

## Migration Path

### For Existing Users

1. **Default Behavior**: Logging enabled at Info level by default
2. **Opt-Out**: Set `LogLevel = None` to disable
3. **Backward Compatible**: No breaking changes to existing APIs

### For New Users

1. **Quick Start**: Works out of the box with sensible defaults
2. **Clear Feedback**: Immediate visibility into SDK status
3. **Troubleshooting**: Error messages include actionable steps

## Success Criteria

1. ✅ Users can see when SDK is initialized and with what configuration
2. ✅ Upload failures clearly indicate the cause (auth, network, config)
3. ✅ Users know how many tests were recorded, uploaded, and failed
4. ✅ Offline queue status is visible (size, retry attempts)
5. ✅ Logs work in both local development and CI/CD environments
6. ✅ Performance impact is minimal (< 1ms per log statement)
7. ✅ Users can disable logging if desired
8. ✅ Error messages are actionable (tell users what to do)

## Implementation Priority

### Must Have (MVP)
- [ ] Core logging infrastructure (IXpingLogger, XpingConsoleLogger)
- [ ] Configuration integration (LogLevel property)
- [ ] Initialization logging (success, warnings, errors)
- [ ] Upload event logging (success/failure with details)
- [ ] Statistics logging at cleanup
- [ ] Enhanced error messages with guidance

### Should Have (Post-MVP)
- [ ] Debug-level logging for troubleshooting
- [ ] Offline queue status logging
- [ ] Circuit breaker status logging
- [ ] Sampling information logging

### Nice to Have (Future)
- [ ] Custom logger support (bring your own logger)
- [ ] Test framework-specific integration
- [ ] Structured logging support
- [ ] Performance metrics logging
