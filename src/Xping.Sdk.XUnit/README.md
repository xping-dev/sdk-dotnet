# Xping.Sdk.XUnit

xUnit test framework adapter for Xping SDK. Automatically tracks xUnit test executions and uploads results to the Xping platform.

## Features

- **Automatic Test Tracking**: Custom test framework automatically tracks all test execution
- **Seamless Integration**: One-line setup in AssemblyInfo.cs
- **Rich Metadata Extraction**: Captures traits, display names, and test arguments
- **Outcome Mapping**: Maps xUnit test results to Xping test outcomes
- **Theory Support**: Tracks each theory test case with its arguments
- **Thread-Safe**: Fully safe for parallel test execution

## Installation

Add the NuGet package reference to your xUnit test project:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.XUnit" Version="1.0.0" />
</ItemGroup>
```

## Quick Start

### 1. Configure Test Framework

Add to `AssemblyInfo.cs` (or create it in your test project root):

```csharp
[assembly: Xunit.TestFramework("Xping.Sdk.XUnit.XpingTestFramework", "Xping.Sdk.XUnit")]
```

That's it! All tests will now be automatically tracked.

### 2. Write Tests Normally

```csharp
public class CalculatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Add_ReturnsSum()
    {
        var result = Calculator.Add(2, 3);
        Assert.Equal(5, result);
    }
    
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [Trait("Category", "Integration")]
    public void Add_MultipleInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.Equal(expected, result);
    }
}
```

### 3. Run Tests

```bash
dotnet test
```

All test executions are automatically tracked and uploaded to Xping!

## Configuration

The adapter uses the standard Xping configuration. Add a `"Xping"` section to your `appsettings.json` file:

```json
{
  "Xping": {
    "ApiKey": "your-api-key-here",
    "ProjectId": "your-project-id-here",
    "ApiEndpoint": "https://upload.xping.io/v1",
    "Environment": "Development",
    "EnableOfflineQueue": true,
    "MaxRetries": 3
  }
}
```

**Required Fields:**
- `ApiKey` - Your Xping API authentication key (mandatory)
- `ProjectId` - Your Xping project identifier (mandatory)

**Optional Fields:**
- `ApiEndpoint` - API server URL (default: `https://upload.xping.io/v1`)
- `Environment` - Environment name (default: `Local`)
- `EnableOfflineQueue` - Queue failed uploads for retry (default: `true`)
- `MaxRetries` - Maximum retry attempts (default: `3`)

Or use environment variables:
- `XPING_APIKEY` (required)
- `XPING_PROJECTID` (required)
- `XPING_API_ENDPOINT`
- `XPING_ENVIRONMENT`

## Metadata Extraction

The adapter automatically extracts rich metadata from your xUnit tests:

```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Priority", "High")]
[Trait("Owner", "TeamA")]
public void ApiAuthenticationTest()
{
    // Test implementation
}
```

Extracted metadata:
- **Traits**: All traits are captured (Category as categories, others as tags)
- **Display Name**: Test display name
- **Arguments**: Theory test arguments (as custom attributes)
- **Source Location**: Source file and line number (if available)
- **Framework**: xunit (automatically added)

## Theory Tests

Theory tests with data are fully supported:

```csharp
[Theory]
[InlineData("hello", 5)]
[InlineData("world", 5)]
[MemberData(nameof(GetTestData))]
public void StringLengthTest(string input, int expectedLength)
{
    Assert.Equal(expectedLength, input.Length);
}

public static IEnumerable<object[]> GetTestData()
{
    yield return new object[] { "test", 4 };
    yield return new object[] { "data", 4 };
}
```

Each theory test case is tracked separately with its arguments stored in metadata.

## Outcome Mapping

xUnit test results are mapped to Xping outcomes:

| xUnit Result | Xping Outcome |
|-------------|---------------|
| Passed | Passed |
| Failed | Failed |
| Skipped | Skipped |

## Test Collections

Tests in collections are tracked normally:

```csharp
[Collection("Integration Tests")]
public class DatabaseTests
{
    [Fact]
    public void ConnectToDatabase()
    {
        // Test implementation
    }
}

[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<DatabaseFixture>
{
}
```

## Advanced Usage

### Programmatic Configuration

You can initialize with custom configuration in a constructor:

```csharp
public class TestBase
{
    static TestBase()
    {
        var config = new XpingConfiguration
        {
            ApiKey = "custom-key",
            ProjectId = "custom-project-id",
            ApiEndpoint = "https://custom.api.com",
            Environment = "Production"
        };
        
        XpingContext.Initialize(config);
    }
}
```

### Manual Test Recording

While not typically needed (automatic tracking handles this), you can manually record tests:

```csharp
var execution = new TestExecution
{
    ExecutionId = Guid.NewGuid(),
    TestId = "custom-test-id",
    TestName = "ManualTest",
    Outcome = TestOutcome.Passed,
    StartTimeUtc = DateTime.UtcNow.AddSeconds(-5),
    EndTimeUtc = DateTime.UtcNow,
    Duration = TimeSpan.FromSeconds(5)
};

XpingContext.RecordTest(execution);
```

## How It Works

The xUnit adapter uses a custom test framework to intercept test execution:

1. **XpingTestFramework** - Replaces the default xUnit test framework
2. **XpingTestFrameworkExecutor** - Wraps test case execution
3. **XpingMessageSink** - Intercepts test lifecycle messages (Starting, Passed, Failed, Skipped)
4. **XpingContext** - Manages SDK lifecycle and test recording

Test timing uses high-resolution `Stopwatch.GetTimestamp()` for accurate duration measurement.

## Thread Safety

The adapter is fully thread-safe for parallel test execution:

- **XpingContext**: Thread-safe singleton initialization with locking
- **XpingMessageSink**: Uses `ConcurrentDictionary` for tracking test state
- **Timing**: Uses per-test timestamp storage for thread safety

This design ensures safe execution when xUnit runs tests in parallel.

## Error Handling

The adapter includes comprehensive error handling:

- Exceptions during test tracking are silently caught and logged
- Test results remain unaffected even if tracking fails
- Failed uploads are retried automatically with exponential backoff
- No impact on test execution or outcomes

## Troubleshooting

### Tests not being tracked

1. Verify `AssemblyInfo.cs` contains the `[assembly: TestFramework(...)]` attribute
2. Check `appsettings.json` has a `"Xping"` section with configuration, or set environment variables
3. Verify API key and project ID are valid
4. Check console output for Xping initialization messages

### Configuration not found

If no configuration is found, the adapter will:
1. Look for `appsettings.json` with a `"Xping"` section in the current directory
2. Check environment variables (with `XPING_` prefix)
3. Fall back to default offline-only mode

### Network Issues

**Verify API Connectivity**

Check if the Xping API endpoint is accessible from your environment:

```bash
# Test connectivity to the API endpoint
curl -I https://upload.xping.io/v1/health

# Or using PowerShell
Invoke-WebRequest -Uri https://upload.xping.io/v1/health -Method Head
```

If the endpoint is unreachable, check:
- Network connectivity and firewall rules
- Corporate proxy settings
- DNS resolution for `upload.xping.io`

## Sample Project

See the complete sample project at `samples/SampleApp.XUnit` for working examples of:
- Assembly-level test framework configuration
- Fact and Theory tests
- Trait usage for categorization
- Test collections
- Skipped test tracking
- Member data and inline data

## Comparison with Other Frameworks

| Feature | xUnit | NUnit | MSTest |
|---------|-------|-------|--------|
| Setup | AssemblyInfo.cs | SetUpFixture + Attribute | Base class |
| Tracking | Automatic (all tests) | Opt-in (attribute) | Opt-in (base class) |
| Configuration | One-time | Per fixture/test | Per test class |
| Overhead | Minimal | Minimal | Minimal |

## License

MIT License - Copyright (c) 2025 Xping.io
