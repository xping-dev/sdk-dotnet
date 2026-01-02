# Xping SDK Configuration

This directory contains sample configuration files for the Xping SDK.

## Configuration via appsettings.json

```json
{
  "Xping": {
    "ApiKey": "your-api-key-here",
    "ProjectId": "your-project-id-here",
    "ApiEndpoint": "https://api.xping.io",
    "BatchSize": 100,
    "FlushInterval": "00:00:30",
    "Environment": "Local",
    "AutoDetectCIEnvironment": true,
    "Enabled": true,
    "CaptureStackTraces": true,
    "EnableCompression": true,
    "EnableOfflineQueue": true,
    "MaxRetries": 3,
    "RetryDelay": "00:00:02",
    "SamplingRate": 1.0,
    "UploadTimeout": "00:00:30",
    "CollectNetworkMetrics" : true
  }
}
```

## Configuration via Environment Variables

All configuration values can be set via environment variables with the `XPING_` prefix:

```bash
export XPING_APIKEY="your-api-key"
export XPING_PROJECTID="your-project-id"
export XPING_BATCHSIZE="200"
export XPING_ENVIRONMENT="Production"
export XPING_ENABLED="true"
export XPING_SAMPLINGRATE="0.5"
```

## Configuration via Code (Programmatic)

```csharp
using Xping.Sdk.Core.Configuration;

// Using the builder pattern
var config = new XpingConfigurationBuilder()
    .WithApiKey("your-api-key")
    .WithProjectId("your-project-id")
    .WithBatchSize(200)
    .WithEnvironment("Production")
    .WithSamplingRate(0.5)
    .Build();

// Direct instantiation
var config = new XpingConfiguration
{
    ApiKey = "your-api-key",
    ProjectId = "your-project-id",
    BatchSize = 200,
    Environment = "Production",
    SamplingRate = 0.5
};
```

## Configuration Priority

Configuration sources are loaded in the following order (later sources override earlier ones):

1. Default values (lowest priority)
2. appsettings.json
3. appsettings.{Environment}.json
4. Environment variables (highest priority)

## Required Configuration

The following configuration values are **required**:

- `ApiKey` - Your Xping API key
- `ProjectId` - Your Xping project ID

## Configuration Reference

### API Configuration

- **ApiEndpoint** (string): The Xping API endpoint URL
  - Default: `"https://api.xping.io"`
  
- **ApiKey** (string): Your API key for authentication
  - **Required**
  
- **ProjectId** (string): Your project identifier
  - **Required**

### Batch Configuration

- **BatchSize** (int): Number of test executions to batch before uploading
  - Default: `100`
  - Valid range: 1-1000
  
- **FlushInterval** (TimeSpan): Time interval for automatic batch uploads
  - Default: `00:00:30` (30 seconds)

### Environment Configuration

- **Environment** (string): Environment name (e.g., "Local", "CI", "Staging", "Production")
  - Default: `"Local"`
  
- **AutoDetectCIEnvironment** (bool): Automatically detect CI/CD environments
  - Default: `true`

### Feature Flags

- **Enabled** (bool): Enable/disable the SDK
  - Default: `true`
  
- **CaptureStackTraces** (bool): Capture stack traces for failed tests
  - Default: `true`
  
- **EnableCompression** (bool): Enable compression for uploads
  - Default: `true`
  
- **EnableOfflineQueue** (bool): Queue test executions when offline
  - Default: `true`

### Retry Configuration

- **MaxRetries** (int): Maximum number of retry attempts
  - Default: `3`
  - Valid range: 0-10
  
- **RetryDelay** (TimeSpan): Delay between retry attempts
  - Default: `00:00:02` (2 seconds)

### Sampling Configuration

- **SamplingRate** (double): Percentage of tests to track (0.0 to 1.0)
  - Default: `1.0` (100%)
  - Valid range: 0.0-1.0

### Timeout Configuration

- **UploadTimeout** (TimeSpan): Timeout for upload operations
  - Default: `00:00:30` (30 seconds)
