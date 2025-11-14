# Configuration Reference

Complete reference guide for configuring Xping SDK. This document covers all available configuration options, their purposes, valid values, and best practices.

---

## Configuration Methods

Xping SDK supports multiple configuration methods with the following priority order (highest to lowest):

1. **Programmatic Configuration** - Pass configuration to `XpingContext.Initialize()`
2. **Environment Variables** - System or process environment variables
3. **JSON Configuration Files** - `appsettings.json` or custom files
4. **Default Values** - Built-in defaults when no explicit configuration provided

---

## Quick Reference Table

| Setting | Type | Default | Environment Variable | Description |
|---------|------|---------|---------------------|-------------|
| `ApiEndpoint` | string | `https://api.xping.io` | `XPING_APIENDPOINT` | Xping API base URL |
| `ApiKey` | string | **(required)** | `XPING_APIKEY` | Authentication API key |
| `ProjectId` | string | **(required)** | `XPING_PROJECTID` | User-defined project identifier |
| `BatchSize` | int | `100` | `XPING_BATCHSIZE` | Tests per upload batch |
| `FlushInterval` | TimeSpan | `30s` | `XPING_FLUSHINTERVAL` | Auto-flush interval |
| `Environment` | string | `Local` | `XPING_ENVIRONMENT` | Environment name |
| `AutoDetectCIEnvironment` | bool | `true` | `XPING_AUTODETECTCIENVIRONMENT` | Auto-detect CI/CD |
| `Enabled` | bool | `true` | `XPING_ENABLED` | SDK enabled/disabled |
| `CaptureStackTraces` | bool | `true` | `XPING_CAPTURESTACKTRACES` | Include stack traces |
| `EnableCompression` | bool | `true` | `XPING_ENABLECOMPRESSION` | Compress uploads |
| `MaxRetries` | int | `3` | `XPING_MAXRETRIES` | Upload retry attempts |
| `RetryDelay` | TimeSpan | `2s` | `XPING_RETRYDELAY` | Delay between retries |
| `SamplingRate` | double | `1.0` | `XPING_SAMPLINGRATE` | Percentage of tests tracked |
| `UploadTimeout` | TimeSpan | `30s` | `XPING_UPLOADTIMEOUT` | HTTP request timeout |
| `CollectNetworkMetrics` | bool | `true` | `XPING_COLLECTNETWORKMETRICS` | Network metrics collection |
| `LogLevel` | enum | `Info` | N/A | SDK diagnostic log level |
| `Logger` | IXpingLogger | `null` | N/A | Custom logger implementation |

---

## Core Settings

### ApiEndpoint

**Type:** `string`  
**Default:** `https://api.xping.io`  
**Required:** No (uses default if not specified)  
**Environment Variable:** `XPING_APIENDPOINT`

The base URL for the Xping API. Change this only if you're using a self-hosted or regional instance.

**Valid values:**
- Must be a valid HTTP or HTTPS URL
- Must not include trailing slash

**Example:**

```json
{
  "Xping": {
    "ApiEndpoint": "https://api.xping.io"
  }
}
```

```bash
export XPING_APIENDPOINT="https://api.xping.io"
```

```csharp
var config = new XpingConfiguration
{
    ApiEndpoint = "https://api.xping.io"
};
XpingContext.Initialize(config);
```

---

### ApiKey

**Type:** `string`  
**Default:** *None*  
**Required:** Yes  
**Environment Variable:** `XPING_APIKEY`

Your Xping authentication API key. This credential identifies your account and authorizes SDK operations.

**Getting your API key:**
1. Log in to [Xping Dashboard](https://app.xping.io)
2. Navigate to **Account** → **Settings** → **API & Integration**
3. Click **Create API Key** and copy it

**Security considerations:**
- Never commit API keys to source control
- Use environment variables or secret management in CI/CD
- Rotate keys regularly as a security best practice
- Each team member can use their own key for attribution

**Example:**

```json
{
  "Xping": {
    "ApiKey": "xpk_live_a1b2c3d4e5f6g7h8i9j0"
  }
}
```

```bash
# Recommended: Use environment variables
export XPING_APIKEY="xpk_live_a1b2c3d4e5f6g7h8i9j0"
```

```csharp
// Not recommended: Hard-coding in source
var config = new XpingConfiguration
{
    ApiKey = "xpk_live_a1b2c3d4e5f6g7h8i9j0"
};
XpingContext.Initialize(config);
```

---

### ProjectId

**Type:** `string`  
**Default:** *None*  
**Required:** Yes  
**Environment Variable:** `XPING_PROJECTID`

A user-defined identifier for your project. Choose any meaningful name—Xping automatically creates the project when your tests first run.

**Characteristics:**
- User-defined, not retrieved from the platform
- Must be unique within your workspace
- Can be any string (alphanumeric, hyphens, underscores recommended)
- Case-sensitive
- Commonly follows naming conventions: `"my-app"`, `"payment-service"`, `"frontend"`

**Example:**

```json
{
  "Xping": {
    "ProjectId": "payment-service"
  }
}
```

```bash
export XPING_PROJECTID="payment-service"
```

```csharp
var config = new XpingConfiguration
{
    ProjectId = "payment-service"
};
XpingContext.Initialize(config);
```

**Project organization strategies:**
- **Monorepo:** Use separate project IDs per component (`"web-api"`, `"web-ui"`, `"worker"`)
- **Multi-repo:** Use repository name or service name
- **Environment separation:** Include environment suffix (`"myapp-prod"`, `"myapp-staging"`)

---

## Batching & Upload Settings

### BatchSize

**Type:** `int`  
**Default:** `100`  
**Valid Range:** `1` to `1000`  
**Environment Variable:** `XPING_BATCHSIZE`

Number of test executions to accumulate before uploading to Xping. Larger batches reduce API calls but increase memory usage and delay visibility.

**Performance considerations:**
- **Small batches (10-50):** Faster visibility, more API calls, higher overhead
- **Medium batches (100-200):** Balanced for most scenarios
- **Large batches (500-1000):** Fewer API calls, higher memory, delayed visibility

**When to adjust:**
- **Increase** for large test suites (1000+ tests) to reduce API overhead
- **Decrease** for real-time monitoring or small test suites (<100 tests)

**Example:**

```json
{
  "Xping": {
    "BatchSize": 200
  }
}
```

```bash
export XPING_BATCHSIZE="200"
```

---

### FlushInterval

**Type:** `TimeSpan`  
**Default:** `00:00:30` (30 seconds)  
**Valid Range:** Must be greater than zero  
**Environment Variable:** `XPING_FLUSHINTERVAL`

Maximum time to wait before uploading accumulated test executions, even if `BatchSize` hasn't been reached.

**Format:**
- JSON: `"HH:MM:SS"` format (e.g., `"00:01:00"` for 1 minute)
- Environment variable: Seconds as integer (e.g., `"60"`) or TimeSpan string
- Programmatic: `TimeSpan` object

**Usage scenarios:**
- **Short intervals (5-15s):** Real-time monitoring during development
- **Medium intervals (30-60s):** Standard CI/CD pipelines
- **Long intervals (2-5m):** Large batch jobs with thousands of tests

**Example:**

```json
{
  "Xping": {
    "FlushInterval": "00:01:00"
  }
}
```

```bash
# As seconds
export XPING_FLUSHINTERVAL="60"

# As TimeSpan
export XPING_FLUSHINTERVAL="00:01:00"
```

```csharp
var config = new XpingConfiguration
{
    FlushInterval = TimeSpan.FromMinutes(1)
};
XpingContext.Initialize(config);
```

---

### UploadTimeout

**Type:** `TimeSpan`  
**Default:** `00:00:30` (30 seconds)  
**Valid Range:** Must be greater than zero  
**Environment Variable:** `XPING_UPLOADTIMEOUT`

HTTP request timeout for upload operations. If uploads don't complete within this time, they're retried according to `MaxRetries` and `RetryDelay`.

**When to adjust:**
- **Increase** for slow network connections or large batches
- **Decrease** for fast failure detection in reliable networks

**Example:**

```json
{
  "Xping": {
    "UploadTimeout": "00:01:00"
  }
}
```

```bash
export XPING_UPLOADTIMEOUT="60"
```

---

## Reliability & Retry Settings

### MaxRetries

**Type:** `int`  
**Default:** `3`  
**Valid Range:** `0` to `10`  
**Environment Variable:** `XPING_MAXRETRIES`

Maximum number of retry attempts for failed upload operations. Retries use exponential backoff based on `RetryDelay`.

**Retry behavior:**
- **0 retries:** Fail immediately on first error
- **1-3 retries:** Standard resilience (recommended)
- **4-10 retries:** High resilience for unreliable networks

**Retried errors:**
- Network timeouts
- HTTP 5xx errors (server errors)
- HTTP 429 (rate limiting)
- Transient network failures

**Not retried:**
- HTTP 4xx errors (except 429) - indicates client error
- Authentication failures (401, 403)
- Validation errors (400)

**Example:**

```json
{
  "Xping": {
    "MaxRetries": 5
  }
}
```

```bash
export XPING_MAXRETRIES="5"
```

---

### RetryDelay

**Type:** `TimeSpan`  
**Default:** `00:00:02` (2 seconds)  
**Valid Range:** Cannot be negative  
**Environment Variable:** `XPING_RETRYDELAY`

Base delay between retry attempts. Actual delay uses exponential backoff:
- 1st retry: `RetryDelay`
- 2nd retry: `RetryDelay * 2`
- 3rd retry: `RetryDelay * 4`
- And so on...

**Example:**
With `RetryDelay = 2s` and `MaxRetries = 3`:
- Initial attempt: fails at t=0s
- 1st retry: after 2s (at t=2s)
- 2nd retry: after 4s more (at t=6s)
- 3rd retry: after 8s more (at t=14s)

**Example:**

```json
{
  "Xping": {
    "RetryDelay": "00:00:05"
  }
}
```

```bash
export XPING_RETRYDELAY="5"
```

---

## Environment Settings

### Environment

**Type:** `string`  
**Default:** `"Local"`  
**Environment Variable:** `XPING_ENVIRONMENT`

Descriptive name for the execution environment. Used for filtering and analysis in the Xping dashboard.

**Common values:**
- `"Local"` - Developer workstation
- `"CI"` - Continuous integration
- `"Staging"` - Staging environment
- `"Production"` - Production environment
- `"QA"` - QA/testing environment

**Example:**

```json
{
  "Xping": {
    "Environment": "Staging"
  }
}
```

```bash
export XPING_ENVIRONMENT="Staging"
```

**Note:** If `AutoDetectCIEnvironment` is enabled (default), the SDK automatically sets this to `"CI"` when running in detected CI/CD platforms.

---

### AutoDetectCIEnvironment

**Type:** `bool`  
**Default:** `true`  
**Environment Variable:** `XPING_AUTODETECTCIENVIRONMENT`

Automatically detect when running in CI/CD environments and set `Environment` to `"CI"`. Also captures CI-specific metadata (build numbers, commit SHAs, etc.).

**Supported CI/CD platforms:**
- GitHub Actions
- Azure DevOps
- GitLab CI/CD
- Jenkins
- CircleCI
- Travis CI
- TeamCity
- Generic CI (via `CI` environment variable)

**When to disable:**
- You want explicit control over environment naming
- Custom CI platform not auto-detected
- Running in CI but want to track as different environment

**Example:**

```json
{
  "Xping": {
    "AutoDetectCIEnvironment": false,
    "Environment": "CustomCI"
  }
}
```

```bash
export XPING_AUTODETECTCIENVIRONMENT="false"
```

---

## Feature Flags

### Enabled

**Type:** `bool`  
**Default:** `true`  
**Environment Variable:** `XPING_ENABLED`

Master switch to enable or disable the entire SDK. When disabled, Xping operates as a no-op—tests run normally but no tracking occurs.

**Use cases:**
- Temporarily disable tracking without removing SDK code
- Feature flags for gradual rollout
- Debugging test failures potentially caused by SDK
- Conditional enabling based on environment or configuration

**Example:**

```json
{
  "Xping": {
    "Enabled": false
  }
}
```

```bash
export XPING_ENABLED="false"
```

```csharp
// Conditional configuration based on environment
var config = new XpingConfiguration
{
    // Disable in development, enable in CI
    Enabled = Environment.GetEnvironmentVariable("CI") != null
};
XpingContext.Initialize(config);
```

---

### CaptureStackTraces

**Type:** `bool`  
**Default:** `true`  
**Environment Variable:** `XPING_CAPTURESTACKTRACES`

Include full stack traces for failed tests in uploaded data. Stack traces help diagnose test failures but increase payload size.

**Trade-offs:**
- **Enabled:** Better debugging, larger payloads, slightly higher overhead
- **Disabled:** Smaller payloads, faster uploads, less diagnostic info

**When to disable:**
- Very large test suites with frequent failures
- Network bandwidth concerns
- Privacy/security requirements (stack traces may contain sensitive paths)

**Example:**

```json
{
  "Xping": {
    "CaptureStackTraces": false
  }
}
```

```bash
export XPING_CAPTURESTACKTRACES="false"
```

---

### EnableCompression

**Type:** `bool`  
**Default:** `true`  
**Environment Variable:** `XPING_ENABLECOMPRESSION`

Compress upload payloads using gzip compression. Significantly reduces bandwidth usage with minimal CPU overhead.

**Typical compression ratios:**
- JSON test data: 60-80% size reduction
- With stack traces: 70-85% size reduction

**When to disable:**
- Debugging network issues (inspect uncompressed payloads)
- Very constrained CPU environments (rare)
- Proxy/firewall issues with compressed content

**Example:**

```json
{
  "Xping": {
    "EnableCompression": false
  }
}
```

```bash
export XPING_ENABLECOMPRESSION="false"
```

---

### CollectNetworkMetrics

**Type:** `bool`  
**Default:** `true`  
**Environment Variable:** `XPING_COLLECTNETWORKMETRICS`

Collect network reliability metrics including latency, connection type, and online status. Helps identify network-related test flakiness.

**Collected metrics:**
- Network latency measurements
- Connection type (WiFi, Ethernet, Cellular)
- Online/offline status changes
- DNS resolution times

**Privacy note:** Only connection metadata is collected—no personal data or network content.

**Example:**

```json
{
  "Xping": {
    "CollectNetworkMetrics": false
  }
}
```

```bash
export XPING_COLLECTNETWORKMETRICS="false"
```

---

## Advanced Settings

### SamplingRate

**Type:** `double`  
**Default:** `1.0` (100%)  
**Valid Range:** `0.0` to `1.0`  
**Environment Variable:** `XPING_SAMPLINGRATE`

Percentage of tests to track. Use sampling for very large test suites to reduce overhead and API usage.

**Values:**
- `1.0` - Track all tests (100%)
- `0.5` - Track half of tests (50%)
- `0.1` - Track 10% of tests
- `0.0` - Track no tests (equivalent to `Enabled = false`)

**Sampling behavior:**
- Random: Each test is randomly sampled with the specified probability
- Statistically representative: Distributed evenly across test suite over multiple runs
- Non-deterministic: Different tests may be sampled in each test run

**Use cases:**
- Large suites (10,000+ tests) where full tracking is costly
- Initial deployment to assess SDK impact
- Cost optimization while maintaining statistical significance

**Example:**

```json
{
  "Xping": {
    "SamplingRate": 0.1
  }
}
```

```bash
export XPING_SAMPLINGRATE="0.1"
```

**Statistical considerations:**
- 10% sampling: ~1,000 samples needed for 95% confidence
- 50% sampling: ~384 samples needed for 95% confidence
- 100% sampling: Complete data, no statistical uncertainty

---

### LogLevel

**Type:** `XpingLogLevel` enum  
**Default:** `Info`  
**Values:** `None`, `Error`, `Warning`, `Info`, `Debug`  
**Environment Variable:** Not supported (programmatic only)

Minimum severity level for SDK diagnostic logging. Controls verbosity of internal SDK operations.

**Log levels:**
- `None` - No logging output
- `Error` - Only errors (authentication failures, network errors)
- `Warning` - Errors + warnings (configuration issues, retry attempts)
- `Info` - Errors + warnings + info (initialization, batch uploads, shutdown)
- `Debug` - All messages including detailed diagnostics

**Example:**

```csharp
var config = new XpingConfiguration
{
    LogLevel = XpingLogLevel.Debug
};
XpingContext.Initialize(config);
```

**Output destination:** By default, logs are written to `Console.WriteLine`. Set a custom `Logger` for different output.

---

### Logger

**Type:** `IXpingLogger`  
**Default:** `null` (uses built-in console logger)  
**Environment Variable:** Not supported (programmatic only)

Custom logger implementation for SDK diagnostics. Allows integration with your application's logging framework.

**Example with custom logger:**

```csharp
public class MyCustomLogger : IXpingLogger
{
    private readonly ILogger _logger;
    
    public MyCustomLogger(ILogger logger)
    {
        _logger = logger;
    }
    
    public void Log(XpingLogLevel level, string message, Exception? exception = null)
    {
        var logLevel = MapToLogLevel(level);
        _logger.Log(logLevel, exception, message);
    }
}

// Configuration
var config = new XpingConfiguration
{
    Logger = new MyCustomLogger(myLoggerInstance)
};
XpingContext.Initialize(config);
```

**Disable all logging:**

```csharp
var config = new XpingConfiguration
{
    Logger = XpingNullLogger.Instance
};
XpingContext.Initialize(config);
```

---

## Configuration Examples

### Complete JSON Configuration

```json
{
  "Xping": {
    "ApiKey": "xpk_live_a1b2c3d4e5f6g7h8i9j0",
    "ProjectId": "my-application",
    "ApiEndpoint": "https://api.xping.io",
    "BatchSize": 100,
    "FlushInterval": "00:00:30",
    "Environment": "Local",
    "AutoDetectCIEnvironment": true,
    "Enabled": true,
    "CaptureStackTraces": true,
    "EnableCompression": true,
    "MaxRetries": 3,
    "RetryDelay": "00:00:02",
    "SamplingRate": 1.0,
    "UploadTimeout": "00:00:30",
    "CollectNetworkMetrics": true
  }
}
```

### Environment-Specific Configuration

**appsettings.Development.json:**
```json
{
  "Xping": {
    "ApiKey": "xpk_test_developmentkey",
    "ProjectId": "my-app-dev",
    "Environment": "Development",
    "Enabled": true,
    "BatchSize": 50,
    "FlushInterval": "00:00:10"
  }
}
```

**appsettings.Production.json:**
```json
{
  "Xping": {
    "ApiKey": "xpk_live_productionkey",
    "ProjectId": "my-app-prod",
    "Environment": "Production",
    "Enabled": true,
    "BatchSize": 200,
    "FlushInterval": "00:01:00"
  }
}
```

### CI/CD Environment Variables (GitHub Actions)

```yaml
env:
  XPING_APIKEY: ${{ secrets.XPING_API_KEY }}
  XPING_PROJECTID: "my-app"
  XPING_ENVIRONMENT: "CI"
  XPING_ENABLED: "true"
  XPING_BATCHSIZE: "200"
  XPING_FLUSHINTERVAL: "60"
```

### Programmatic Configuration

```csharp
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;

// Fluent builder pattern
var config = new XpingConfigurationBuilder()
    .WithApiKey(Environment.GetEnvironmentVariable("XPING_API_KEY"))
    .WithProjectId("my-application")
    .WithBatchSize(200)
    .WithFlushInterval(TimeSpan.FromMinutes(1))
    .WithEnvironment("Staging")
    .WithMaxRetries(5)
    .WithSamplingRate(0.5)
    .Build();

XpingContext.Initialize(config);

// Direct configuration
var directConfig = new XpingConfiguration
{
    ApiKey = "xpk_live_key",
    ProjectId = "my-app",
    BatchSize = 150,
    EnableCompression = true
};
XpingContext.Initialize(directConfig);
```

### Minimal Configuration

```json
{
  "Xping": {
    "ApiKey": "xpk_live_a1b2c3d4e5f6g7h8i9j0",
    "ProjectId": "my-app"
  }
}
```

All other settings use default values.

---

## Configuration Loading Order

Configuration values are merged from multiple sources in this priority order:

1. **Programmatic configuration** (highest priority)
   ```csharp
   var config = new XpingConfiguration { ApiKey = "key" };
   XpingContext.Initialize(config);
   ```

2. **Environment variables**
   ```bash
   export XPING_APIKEY="key"
   ```

3. **Environment-specific JSON**
   ```
   appsettings.Development.json
   appsettings.Production.json
   ```

4. **Base JSON configuration**
   ```
   appsettings.json
   ```

5. **Default values** (lowest priority)

**Example resolution:**
```
ApiKey:
  - Default: null
  - appsettings.json: "xpk_test_key"
  - Environment variable: "xpk_live_key"  ← Wins
  - Programmatic: Not set

BatchSize:
  - Default: 100
  - appsettings.json: 200  ← Wins
  - Environment variable: Not set
  - Programmatic: Not set
```

---

## Configuration Validation

Xping SDK validates configuration on initialization and provides clear error messages for invalid settings.

### Validation Rules

| Setting | Validation |
|---------|------------|
| `ApiKey` | Must not be empty or whitespace |
| `ProjectId` | Must not be empty or whitespace |
| `ApiEndpoint` | Must be valid HTTP/HTTPS URL |
| `BatchSize` | Must be between 1 and 1000 |
| `FlushInterval` | Must be greater than zero |
| `MaxRetries` | Must be between 0 and 10 |
| `RetryDelay` | Cannot be negative |
| `SamplingRate` | Must be between 0.0 and 1.0 |
| `UploadTimeout` | Must be greater than zero |

### Handling Validation Errors

```csharp
var builder = new XpingConfigurationBuilder()
    .WithApiKey("")  // Invalid
    .WithBatchSize(2000);  // Invalid

if (builder.TryBuild(out var config, out var errors))
{
    XpingContext.Initialize(config);
}
else
{
    Console.WriteLine("Configuration errors:");
    foreach (var error in errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
}

// Output:
// Configuration errors:
//   - ApiKey is required.
//   - BatchSize cannot exceed 1000.
```

---

## Best Practices

### Security

1. **Never commit API keys** to source control
2. **Use environment variables** in CI/CD pipelines
3. **Rotate API keys** regularly
4. **Use different keys** per environment
5. **Restrict key permissions** when available

### Performance

1. **Adjust batch size** based on test suite size:
   - Small suites (<100 tests): `BatchSize = 50`
   - Medium suites (100-1000 tests): `BatchSize = 100-200`
   - Large suites (>1000 tests): `BatchSize = 500-1000`

2. **Balance flush interval** with visibility needs:
   - Real-time monitoring: `10-15 seconds`
   - Standard CI: `30-60 seconds`
   - Batch jobs: `2-5 minutes`

3. **Enable compression** unless debugging network issues

4. **Use sampling** for very large suites (>10,000 tests)

### Reliability

1. **Keep default retry settings** unless you have specific requirements
2. **Monitor retry rates** in Xping dashboard
3. **Increase timeouts** for slow networks
4. **Enable auto-detect CI** for consistent environment tracking

### Development Workflow

1. **Local development:**
   ```json
   {
     "Xping": {
       "Enabled": true,
       "BatchSize": 50,
       "FlushInterval": "00:00:10"
     }
   }
   ```

2. **CI/CD pipelines:**
   ```yaml
   env:
     XPING_BATCHSIZE: "200"
     XPING_FLUSHINTERVAL: "60"
   ```

3. **Production monitoring:**
   ```json
   {
     "Xping": {
       "BatchSize": 500,
       "FlushInterval": "00:02:00",
       "SamplingRate": 0.1
     }
   }
   ```

---

## Troubleshooting

### Configuration not loading

**Problem:** Settings from `appsettings.json` are ignored.

**Solutions:**
1. Ensure file is copied to output directory:
   ```xml
   <ItemGroup>
     <None Update="appsettings.json">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </None>
   </ItemGroup>
   ```

2. Verify JSON syntax is valid

3. Check section name is exactly `"Xping"` (case-sensitive)

### Environment variables not working

**Problem:** Environment variables don't override JSON configuration.

**Solutions:**
1. Verify variable name uses `XPING_` prefix (not `XPING__`)

2. Check variable is set in the correct scope:
   ```bash
   # System-wide
   export XPING_APIKEY="key"
   
   # Process-specific
   XPING_APIKEY="key" dotnet test
   ```

3. Restart IDE/terminal after setting environment variables

### Validation errors on startup

**Problem:** SDK fails to initialize with validation errors.

**Solutions:**
1. Check error messages for specific issues

2. Verify required settings (`ApiKey`, `ProjectId`) are provided

3. Ensure numeric values are within valid ranges

4. Validate TimeSpan format: `"HH:MM:SS"`

---

## See Also

- **[Environment Variables](environment-variables.md)** - Detailed environment variable reference
- **[Advanced Configuration](advanced-configuration.md)** - Advanced scenarios and patterns
- **[Quick Start Guides](../getting-started/quickstart-nunit.md)** - Framework-specific setup
- **[CI/CD Integration](../getting-started/ci-cd-setup.md)** - Pipeline configuration examples
- **[Troubleshooting](../troubleshooting/common-issues.md)** - Common configuration issues

---

**Need help?** 
- 📚 [Documentation](https://docs.xping.io)
- 💬 [Community Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)
- 📧 [Email Support](mailto:support@xping.io)
