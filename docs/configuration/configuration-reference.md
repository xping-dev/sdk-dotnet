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
| `ApiEndpoint` | string | `https://upload.xping.io/v1` | `XPING_APIENDPOINT` | Xping API base URL |
| `ApiKey` | string | *(none)* | `XPING_APIKEY` | Authentication API key. Required in Cloud mode; omit it to run [local-only](local-store.md). |
| `ProjectId` | string | *(none)* | `XPING_PROJECTID` | Optional. Pins the whole session to one project; omit it to get one project per test assembly. |
| `Mode` | XpingMode | `Auto` | `XPING_MODE` | `Auto`, `LocalOnly`, `Cloud`, or `Disabled` |
| `BatchSize` | int | `100` | `XPING_BATCHSIZE` | Tests per upload batch |
| `FlushInterval` | TimeSpan | `30s` | `XPING_FLUSHINTERVAL` | Auto-flush interval |
| `Environment` | string | `Local` | `XPING_ENVIRONMENT` | Environment name |
| `AutoDetectCIEnvironment` | bool | `true` | `XPING_AUTODETECTCIENVIRONMENT` | Auto-detect CI/CD |
| `CiEnvironmentName` | string | `CI` | `XPING_CIENVIRONMENTNAME` | Label used for auto-detected CI executions |
| `Enabled` | bool | `true` | `XPING_ENABLED` | SDK enabled/disabled |
| `CaptureStackTraces` | bool | `true` | `XPING_CAPTURESTACKTRACES` | Include stack traces |
| `EnableCompression` | bool | `true` | `XPING_ENABLECOMPRESSION` | Compress uploads |
| `MaxRetries` | int | `3` | `XPING_MAXRETRIES` | Upload retry attempts |
| `RetryDelay` | TimeSpan | `2s` | `XPING_RETRYDELAY` | Delay between retries |
| `UploadTimeout` | TimeSpan | `30s` | `XPING_UPLOADTIMEOUT` | HTTP request timeout |
| `EnablePullRequestDetection` | bool | `true` | `XPING_ENABLEPULLREQUESTDETECTION` | Detect PR context for CI/CD comment posting |
| `CollectLocalGitAuthor` | bool | `false` | `XPING_COLLECTLOCALGITAUTHOR` | Include git author name in local-run metadata (opt-in to avoid PII collection) |
| `StrictMode` | bool | `false` | `XPING_STRICTMODE` | Throw on configuration errors instead of silently disabling |
| *(store path)* | string | repository root | `XPING_LOCAL_STORE` | Overrides the [local store](local-store.md) location |
| *(banner)* | string | *(unset)* | `XPING_NO_BANNER` | Suppresses the SDK's retry hint and the CLI's invitation |

---

## Operating Mode

Xping runs in one of three modes. The mode is resolved once at startup and determines whether the SDK uploads, stores locally, or does nothing.

### Mode

**Type:** `XpingMode`
**Default:** `Auto`
**Environment Variable:** `XPING_MODE`

| Value | Behaviour |
|---|---|
| `Auto` | Resolves to `Cloud` when credentials are present, otherwise `LocalOnly`. |
| `LocalOnly` | Collects and writes the [local store](local-store.md). No network calls at all. |
| `Cloud` | Collects, uploads, **and** writes the local store. |
| `Disabled` | Collects nothing. Equivalent to `Enabled = false`. |

### How `Auto` resolves

| Condition | Resolved mode |
|---|---|
| `Enabled = false` | `Disabled` |
| `StrictMode = true` | `Cloud` |
| `ApiKey` present | `Cloud` |
| Otherwise | `LocalOnly` |

### Strict mode still requires credentials

`StrictMode` exists to guarantee observability in CI, so it forces `Cloud`. A missing API key under strict mode remains a hard configuration error and will **not** silently degrade to local-only collection.

This is the intended split: local-only is the default for an unconfigured developer, never a silent downgrade for a configured pipeline.

```bash
# CI: fail the build if observability is misconfigured
export XPING_STRICTMODE=true
export XPING_APIKEY="..."
export XPING_PROJECTID="..."
```

### Local-only makes no network calls

In `LocalOnly` mode the SDK does not create an HTTP client, retry pipeline, or circuit breaker. Nothing in the local path opens a socket.

### Staying local with credentials present

Setting `Mode` explicitly overrides credential detection:

```bash
export XPING_MODE=LocalOnly
```

Useful when a key is present in your environment for other reasons but you want a particular run kept off the platform.

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
**Required:** In Cloud mode only  
**Environment Variable:** `XPING_APIKEY`

Your Xping authentication API key. This credential identifies your account and authorizes SDK operations.

Omitting it is a supported configuration: the SDK resolves to [`LocalOnly`](#operating-mode) and records test history to disk instead of uploading. See [Running Without an Account](../getting-started/local-first.md).

**Getting your API key:**
1. Log in to [Xping Cloud](https://app.xping.io)
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
    "ApiKey": "xpg_live_productionkey"
  }
}
```

```bash
# Recommended: Use environment variables
export XPING_APIKEY="xpg_live_productionkey"
```

```csharp
// Not recommended: Hard-coding in source
var config = new XpingConfiguration
{
    ApiKey = "xpg_live_productionkey"
};
XpingContext.Initialize(config);
```

---

### ProjectId

**Type:** `string`
**Default:** *None — the project is derived from the test assembly*
**Required:** No
**Environment Variable:** `XPING_PROJECTID`

**You usually do not need to set this.** When `ProjectId` is unset, Xping derives the project from the test assembly each execution belongs to. A test project called `PaymentService.Tests` reports into a project of that name, created automatically the first time your tests run.

That means a solution-wide `dotnet test` produces **one project per test project**:

| Test assembly | Project |
|---|---|
| `Billing.Tests` | `billing-tests` |
| `Api.Tests` | `api-tests` |
| `Web.Tests` | `web-tests` |

This mirrors how the [`xping` CLI](../guides/local-store.md) already scopes local reports — `xping report --assembly Billing.Tests` — so local and cloud views agree on what a project is.

**Set `ProjectId` only to override that.** It is a hard pin: every execution in the session lands in that one project regardless of which assembly it came from. The case for it is a monorepo where several test assemblies should report as a single project:

```json
{
  "Xping": {
    "ProjectId": "payment-service"
  }
}
```

**Validation Rules** (applied by the platform to a pinned value):
- **Character Set:** ASCII alphanumeric characters, hyphens (`-`), and underscores (`_`) only
- **Format:** Must start with an alphanumeric character (a-z, 0-9), followed by any combination of alphanumeric, hyphens, or underscores
- **Whitespace:** No spaces or whitespace characters allowed (including internal whitespace)
- **Maximum Length:** 128 characters
- **Normalization:** Automatically converted to lowercase, so `"my-app"` and `"My-App"` are the same project
- A blank value (`""`) is treated as unset, not as a pin

> **Important:** A project is identified by its name, and there is no automated migration between projects. Renaming a test assembly therefore starts a *new* project and leaves the old history behind — as does changing a pinned `ProjectId`. Pin a value if you expect to rename the assembly.

> **Note:** Because a solution-wide run can create several projects at once, a first run on a plan with a low project limit may hit that limit. Pin a `ProjectId` to report into a single project instead.

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

Number of test executions to accumulate before automatically uploading to Xping.

**Upload triggers:**
1. When `BatchSize` test executions are collected (e.g., 100 tests)
2. When `FlushInterval` timer fires (e.g., every 30 seconds)
3. **When test session completes** (via `FlushAsync()` or `DisposeAsync()`)

This means even small test suites (e.g., 10 tests) will upload immediately when the test session ends, regardless of batch size.

**Performance considerations:**
- **Small batches (10-50):** Faster mid-run visibility, more API calls, higher overhead
- **Medium batches (100-200):** Balanced for most scenarios (recommended)
- **Large batches (500-1000):** Fewer API calls during execution, higher memory usage

**When to adjust:**
- **Increase** for large test suites (1000+ tests) to reduce API call frequency during test execution
- **Decrease** for real-time monitoring during development (see results before suite completes)

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

Maximum time to wait before uploading accumulated test executions, even if `BatchSize` hasn't been reached. This is a timer-based flush that runs periodically during test execution.

**Important:** At the end of your test session, `FlushAsync()` or `DisposeAsync()` will upload any remaining tests immediately, regardless of this interval.

**Format:**
- JSON: `"HH:MM:SS"` format (e.g., `"00:01:00"` for 1 minute)
- Environment variable: Seconds as integer (e.g., `"60"`) or TimeSpan string
- Programmatic: `TimeSpan` object

**Usage scenarios:**
- **Short intervals (5-15s):** See results quickly during development (uploads every 5-15s)
- **Medium intervals (30-60s):** Standard CI/CD pipelines (balanced)
- **Long intervals (2-5m):** Large batch jobs where you only need final results

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

Descriptive name for the execution environment. Used for filtering and analysis in Xping Cloud.

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

**Priority Order for Environment Detection:**

The SDK determines the environment name using the following priority (highest to lowest):

1. **`XPING_ENVIRONMENT` environment variable** - Explicit Xping-specific setting (highest priority)
2. **Auto-detected CI** - Returns `CiEnvironmentName` (default `"CI"`) when `AutoDetectCIEnvironment=true` and running in a detected CI/CD platform
3. **`Environment` configuration property** - Value set programmatically or in configuration files
4. **Framework environment variables** - `ASPNETCORE_ENVIRONMENT`, then `DOTNET_ENVIRONMENT`
5. **Default** - Returns `"Local"` when none of the above are set

**Example:** If you don't specify `Environment` and `AutoDetectCIEnvironment=false`, Xping will use `DOTNET_ENVIRONMENT`/`ASPNETCORE_ENVIRONMENT` when available, and otherwise fall back to `"Local"`. Setting `XPING_ENVIRONMENT=Staging` still overrides everything and uses `"Staging"` instead.

---

### AutoDetectCIEnvironment

**Type:** `bool`  
**Default:** `true`  
**Environment Variable:** `XPING_AUTODETECTCIENVIRONMENT`

Automatically detect when running in CI/CD environments and set `Environment` to `CiEnvironmentName` (default `"CI"`). Also captures CI-specific metadata (build numbers, commit SHAs, branch names, etc.).

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

### CiEnvironmentName

**Type:** `string`  
**Default:** `"CI"`  
**Environment Variable:** `XPING_CIENVIRONMENTNAME`

Overrides the label used when CI/CD is auto-detected. This is useful when you want CI executions grouped under a more specific environment name such as `"BuildPipeline"` or `"PullRequestValidation"` without disabling auto-detection.

**Example:**

```json
{
  "Xping": {
    "AutoDetectCIEnvironment": true,
    "CiEnvironmentName": "BuildPipeline"
  }
}
```

```bash
export XPING_CIENVIRONMENTNAME="BuildPipeline"
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

> When `CaptureStackTraces` is set to `false`, the uploaded payload for failed tests will include
> `stackTraceOmitted: true`. This allows Xping Cloud to distinguish between a test
> that had no stack trace and one where the user explicitly disabled stack trace collection.

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

### EnablePullRequestDetection

**Type:** `bool`
**Default:** `true`
**Environment Variable:** `XPING_ENABLEPULLREQUESTDETECTION`

Detect pull request context from CI/CD environment variables and include it in session uploads. When enabled, Xping reads PR metadata (PR number, branch, platform) from the CI environment to enable automatic PR comment posting with test results.

**Supported platforms:**
- GitHub Actions (via `GITHUB_EVENT_NAME`, `GITHUB_REF`, etc.)

**When to disable:**
- You don't use PR comment posting and want to skip the detection overhead
- You're running in a non-PR build and want to suppress any PR context association

**Example:**

```json
{
  "Xping": {
    "EnablePullRequestDetection": false
  }
}
```

```bash
export XPING_ENABLEPULLREQUESTDETECTION="false"
```

```csharp
var config = new XpingConfigurationBuilder()
    .WithPullRequestDetection(false)
    .Build();
XpingContext.Initialize(config);
```

---

### CollectLocalGitAuthor

**Type:** `bool`  
**Default:** `false`  
**Environment Variable:** `XPING_COLLECTLOCALGITAUTHOR`

When running on a developer machine (not a CI environment) inside a git repository, controls whether the SDK reads the author name from `.git/config [user] name` and includes it as the `Git.Actor` custom property in environment metadata.

This setting is **disabled by default** because `user.name` in `.git/config` is typically a developer's real full name — collecting and uploading it without explicit consent is a PII concern. Enable it only when your team is aware and has agreed to share this information.

**When enabled, the following custom property is populated:**
- `Git.Actor` — value of `[user] name` from the local `.git/config`

**This setting has no effect when:**
- Running in a CI environment (`IsCIEnvironment = true`) — CI actor comes from the CI provider's environment variables instead
- Running outside a git repository

**Example:**

```json
{
  "Xping": {
    "CollectLocalGitAuthor": true
  }
}
```

```bash
export XPING_COLLECTLOCALGITAUTHOR="true"
```

```csharp
var config = new XpingConfiguration
{
    CollectLocalGitAuthor = true
};
XpingContext.Initialize(config);
```

---

## Advanced Settings

> **Logging:** The SDK uses `Microsoft.Extensions.Logging.ILogger` for diagnostics. Configure log verbosity through your host's standard logging configuration (e.g., `appsettings.json` `Logging` section or `ILoggingBuilder`). There are no SDK-specific `LogLevel` or `Logger` configuration properties.

### StrictMode

**Type:** `bool`  
**Default:** `false`  
**Environment Variable:** `XPING_STRICTMODE`

Controls how the SDK responds to errors that prevent proper test observability.

- **`false` (default — resilient mode):** Configuration and network errors are logged and the SDK silently continues or is disabled. Tests always run without interruption.
- **`true` (strict mode):** In the core SDK, any error that prevents test data from being collected or uploaded is surfaced by throwing `XpingConfigurationException` or `XpingNetworkException`. When you use the provided NUnit, xUnit, or MSTest adapters, these exceptions are treated as fatal and translated into `Environment.FailFast`, ensuring CI pipelines fail fast when observability cannot be guaranteed.

Strict mode is recommended for production CI/CD pipelines where you want to guarantee observability is always active, rather than letting it silently degrade.

> **Note:** The distinction between core behavior (throwing `XpingConfigurationException` or `XpingNetworkException`) and adapter behavior (`Environment.FailFast`) means that in most test runs you will observe process termination rather than a catchable exception. This is by design to ensure failed observability causes a visible CI failure.

**When to use strict mode:**
- Production CI/CD pipelines where missing Xping configuration or network failures should be a build failure
- Teams that have fully adopted Xping and want to enforce observability mandatorily
- Security-sensitive environments where untracked test runs are unacceptable

**When to use resilient mode (default):**
- Local development and developer onboarding
- Environments where tests must always run regardless of observability status
- Gradual SDK adoption across a codebase

**Behavior comparison:**

| Scenario | Resilient Mode (default) | Strict Mode |
|----------|--------------------------|-------------|
| Missing `ApiKey` | Error logged, SDK disabled, tests run | `XpingConfigurationException` thrown, test run fails |
| Invalid `ApiEndpoint` | Error logged, SDK disabled, tests run | `XpingConfigurationException` thrown, test run fails |
| Network error during upload | Error logged, tests complete without observability data | `XpingNetworkException` thrown, test run fails |
| Valid configuration + upload succeeds | SDK active, tests tracked | SDK active, tests tracked |

**Example:**

```json
{
  "Xping": {
    "StrictMode": true
  }
}
```

```bash
# Enable via environment variable
export XPING_STRICTMODE="true"
```

```bash
# Inline for a single test run
XPING_STRICTMODE=true dotnet test
```

```csharp
var config = new XpingConfigurationBuilder()
    .WithApiKey("your-api-key")
    .WithProjectId("your-project")
    .WithStrictMode(true)
    .Build();
XpingContext.Initialize(config);
```

**GitHub Actions example:**

```yaml
- name: Run Tests
  env:
    XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
    XPING_PROJECTID: ${{ vars.XPING_PROJECTID }}
    XPING_STRICTMODE: "true"
  run: dotnet test
```

**Expected output when strict mode triggers:**

```
# Resilient mode (default) — configuration error
[Error] Configuration validation failed: ApiKey is required.
        SDK disabled - tests will run without observability tracking.

# Strict mode — configuration error
[Xping] Strict mode configuration error: Xping configuration invalid: ApiKey is required.

# Resilient mode (default) — network error
[Error] Network error occurred
        HTTP request failed: ...

# Strict mode — network error
[Xping] Strict mode network error: Xping network error in strict mode: HTTP request failed: ...
```

---

## Configuration Examples

### Complete JSON Configuration

```json
{
  "Xping": {
    "ApiKey": "xpg_live_productionkey",
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
    "UploadTimeout": "00:00:30",
    "EnablePullRequestDetection": true,
    "CollectLocalGitAuthor": false
  }
}
```

### Environment-Specific Configuration

**appsettings.Development.json:**
```json
{
  "Xping": {
    "ApiKey": "xpg_test_developmentkey",
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
    "ApiKey": "xpg_live_productionkey",
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
  XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
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
    .WithApiKey(Environment.GetEnvironmentVariable("XPING_APIKEY"))
    .WithProjectId("my-application")
    .WithBatchSize(200)
    .WithFlushInterval(TimeSpan.FromMinutes(1))
    .WithEnvironment("Staging")
    .WithMaxRetries(5)
    .Build();

XpingContext.Initialize(config);

// Direct configuration
var directConfig = new XpingConfiguration
{
    ApiKey = "xpg_live_productionkey",
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
    "ApiKey": "xpg_live_productionkey",
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

**Note:** The `Environment` property has special detection logic that considers multiple sources beyond just configuration values. See the [Environment](#environment) section for the complete priority order used for environment name detection.

**Example resolution:**
```
ApiKey:
  - Default: null
  - appsettings.json: "xpg_test_key"
  - Environment variable: "xpg_live_key"  ← Wins
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
| `ApiEndpoint` | Must be valid HTTP/HTTPS URL |
| `BatchSize` | Must be between 1 and 1000 |
| `FlushInterval` | Must be greater than zero |
| `MaxRetries` | Must be between 0 and 10 |
| `RetryDelay` | Cannot be negative |
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

### Reliability

1. **Keep default retry settings** unless you have specific requirements
2. **Monitor retry rates** in Xping Cloud
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
       "FlushInterval": "00:02:00"
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

2. Verify the required setting (`ApiKey`) is provided

3. Ensure numeric values are within valid ranges

4. Validate TimeSpan format: `"HH:MM:SS"`

---

## See Also

- **[Quick Start Guides](../getting-started/quickstart-nunit.md)** - Framework-specific setup
- **[CI/CD Integration](../getting-started/ci-cd-setup.md)** - Pipeline configuration examples
- **[Troubleshooting](../troubleshooting/common-issues.md)** - Common configuration issues

---

**Need help?** 
- 📚 [Documentation](https://docs.xping.io)
- 💬 [Community Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)
- 📧 [Email Support](mailto:support@xping.io)
