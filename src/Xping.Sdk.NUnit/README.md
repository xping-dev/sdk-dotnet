# Xping.Sdk.NUnit

NUnit test framework adapter for Xping SDK. Automatically tracks NUnit test executions and uploads results to the Xping platform.

## Features

- **Automatic Test Tracking**: Uses NUnit's `ITestAction` to automatically track test execution
- **Global Setup/Teardown**: `SetUpFixture` ensures proper SDK initialization and cleanup
- **Flexible Attribution**: Apply `[XpingTrack]` at method, class, or assembly level
- **Rich Metadata Extraction**: Captures categories, descriptions, authors, and custom attributes
- **Outcome Mapping**: Maps NUnit result states to Xping test outcomes
- **Parameterized Test Support**: Tracks each test case with its arguments

## Installation

Add the NuGet package reference to your NUnit test project:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.NUnit" Version="1.0.0" />
</ItemGroup>
```

## Quick Start

### 1. Create Global Setup Fixture

Create a `SetUpFixture` in your test project to initialize and cleanup the Xping SDK:

```csharp
using NUnit.Framework;
using Xping.Sdk.NUnit;

[SetUpFixture]
public class XpingSetup
{
    [OneTimeSetUp]
    public void BeforeAllTests()
    {
        XpingContext.Initialize();
    }

    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        await XpingContext.FlushAsync();
        await XpingContext.DisposeAsync();
    }
}
```

### 2. Apply XpingTrack Attribute

Apply the `[XpingTrack]` attribute to track tests:

#### Class-Level Tracking (tracks all tests in the class)

```csharp
[TestFixture]
[XpingTrack]
public class MyTests
{
    [Test]
    public void Test1() => Assert.Pass();

    [Test]
    public void Test2() => Assert.Pass();
}
```

#### Method-Level Tracking (tracks specific tests only)

```csharp
[TestFixture]
public class MyTests
{
    [Test]
    [XpingTrack]
    public void TrackedTest() => Assert.Pass();

    [Test]
    public void UntrackedTest() => Assert.Pass();
}
```

#### Assembly-Level Tracking (tracks all tests)

Add to `AssemblyInfo.cs` or any file:

```csharp
[assembly: XpingTrack]
```

## Configuration

The adapter uses the standard Xping configuration. Add a `"Xping"` section to your `appsettings.json` file:

```json
{
  "Xping": {
    "ApiKey": "your-api-key-here",
    "ProjectId": "your-project-id-here",
    "ApiEndpoint": "https://api.xping.io",
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
- `ApiEndpoint` - API server URL (default: `https://api.xping.io`)
- `Environment` - Environment name (default: `Local`)
- `EnableOfflineQueue` - Queue failed uploads for retry (default: `true`)
- `MaxRetries` - Maximum retry attempts (default: `3`)

Or use environment variables:
- `XPING_API_KEY` (required)
- `XPING_PROJECT_ID` (required)
- `XPING_API_ENDPOINT`
- `XPING_ENVIRONMENT`

## Metadata Extraction

The adapter automatically extracts rich metadata from your NUnit tests:

```csharp
[Test]
[Category("Integration")]
[Category("Slow")]
[Description("Verifies API authentication flow")]
[Author("John Doe")]
public void ApiAuthenticationTest()
{
    // Test implementation
}
```

Extracted metadata:
- **Categories**: Integration, Slow
- **Description**: Verifies API authentication flow
- **Author**: John Doe (as tag)
- **Framework**: nunit (automatically added)

## Parameterized Tests

Parameterized tests are fully supported with argument tracking:

```csharp
[Test]
[TestCase(1, 2, 3)]
[TestCase(5, 5, 10)]
public void AddTest(int a, int b, int expected)
{
    Assert.That(a + b, Is.EqualTo(expected));
}
```

Each test case is tracked separately with its arguments stored in metadata.

## Outcome Mapping

NUnit result states are mapped to Xping outcomes:

| NUnit State | Xping Outcome |
|------------|---------------|
| Success | Passed |
| Failure, Error, SetUpFailure, SetUpError, TearDownError, ChildFailure | Failed |
| Skipped, Ignored, Explicit | Skipped |
| Inconclusive | Inconclusive |
| NotRunnable, Cancelled | NotExecuted |

## Advanced Usage

### Manual Test Recording

You can manually record test executions if needed:

```csharp
var execution = new TestExecution
{
    TestId = "custom-test-id",
    Name = "ManualTest",
    Outcome = TestOutcome.Passed,
    StartTime = DateTime.UtcNow.AddSeconds(-5),
    EndTime = DateTime.UtcNow,
    Duration = TimeSpan.FromSeconds(5)
};

XpingContext.RecordTest(execution);
```

### Custom Initialization

Provide custom configuration at initialization:

```csharp
var config = new XpingConfiguration
{
    ApiKey = "custom-key",
    ProjectId = "custom-project-id",
    ApiEndpoint = "https://custom.api.com",
    Environment = "Production"
};

XpingContext.Initialize(config);
```

## Error Handling

The `AfterTest` hook includes error handling to ensure test execution is never interrupted:

- Exceptions during test tracking are silently caught and logged
- Test results remain unaffected even if tracking fails
- Failed uploads are queued for retry when offline queue is enabled

## Thread Safety

The adapter is fully thread-safe for parallel test execution:

**XpingContext:**
- Uses thread-safe singleton initialization with locking
- First call to `Initialize()` creates the instance
- Subsequent calls return the same instance
- Safe for parallel test execution

**XpingTrackAttribute:**
- Stores timing data in test properties (per-test storage) instead of instance fields
- Uses `Stopwatch.GetTimestamp()` for high-resolution, thread-safe timing
- Attribute instances can be safely reused across multiple tests
- No shared mutable state between test executions

This design ensures safe execution when NUnit runs tests in parallel (`ParallelScope.All` or `ParallelScope.Fixtures`).

## Troubleshooting

### Tests not being tracked

1. Ensure `XpingSetup` fixture exists and calls `Initialize()`
2. Verify `[XpingTrack]` attribute is applied
3. Check `appsettings.json` has a `"Xping"` section with configuration, or set environment variables
4. Verify API key is valid

### Configuration not found

If no configuration is found, the adapter will:
1. Look for `appsettings.json` with a `"Xping"` section in the current directory
2. Check environment variables (with `XPING_` prefix)
3. Fall back to default offline-only mode

### Offline mode

When API is unavailable:
- Tests are queued locally in `~/.xping/queue`
- Automatic retry on next successful connection
- No test execution impact

## Sample Project

See the complete sample project at `samples/SampleApp.NUnit` for working examples of:
- Global setup fixture
- Class-level and method-level tracking
- Parameterized tests
- Category and metadata usage
- Skipped test tracking

## License

MIT License - Copyright (c) 2025 Xping.io
