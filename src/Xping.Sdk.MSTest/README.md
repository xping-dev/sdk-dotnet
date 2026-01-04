# Xping.Sdk.MSTest

MSTest test framework adapter for Xping SDK. Automatically tracks MSTest test executions and uploads results to the Xping platform.

## Features

- **Automatic Test Tracking**: Inherit from `XpingTestBase` for automatic tracking
- **Assembly-Level Initialization**: Simple setup with `XpingAssemblyInitialize`
- **Rich Metadata Extraction**: Captures test categories, properties, and data rows
- **Outcome Mapping**: Maps all MSTest outcomes to Xping test outcomes
- **DataTestMethod Support**: Tracks each data row execution separately
- **Thread-Safe**: Fully safe for parallel test execution

## Installation

Add the NuGet package reference to your MSTest test project:

```xml
<ItemGroup>
  <PackageReference Include="Xping.Sdk.MSTest" Version="1.0.0" />
</ItemGroup>
```

## Quick Start

### 1. Inherit from XpingTestBase

```csharp
[TestClass]
public class CalculatorTests : XpingTestBase
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Add_ReturnsSum()
    {
        var result = Calculator.Add(2, 3);
        Assert.AreEqual(5, result);
    }
    
    [DataTestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(5, 5, 10)]
    [TestCategory("Integration")]
    public void Add_MultipleInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.AreEqual(expected, result);
    }
}
```

### 2. Run Tests

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
    "ApiEndpoint": "https://upload.xping.io/api/v1",
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
- `ApiEndpoint` - API server URL (default: `https://upload.xping.io/api/v1`)
- `Environment` - Environment name (default: `Local`)
- `EnableOfflineQueue` - Queue failed uploads for retry (default: `true`)
- `MaxRetries` - Maximum retry attempts (default: `3`)

Or use environment variables:
- `XPING_API_KEY` (required)
- `XPING_PROJECT_ID` (required)
- `XPING_API_ENDPOINT`
- `XPING_ENVIRONMENT`

## Usage Patterns

### Option 1: Base Class (Recommended)

Inherit from `XpingTestBase` for automatic tracking of all test methods:

```csharp
[TestClass]
public class MyTests : XpingTestBase
{
    [TestMethod]
    public void TestMethod()
    {
        // Test implementation
        // Automatically tracked!
    }
}
```

**Advantages:**
- Zero configuration per test
- Automatic tracking of all test methods in the class
- Simplest approach for most scenarios

### Option 2: Assembly-Level Initialization

Use `XpingAssemblyInitialize` for explicit SDK lifecycle management without inheriting from base class:

```csharp
[TestClass]
public static class TestSetup
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

[TestClass]
public class MyTests
{
    public TestContext? TestContext { get; set; }
    
    [TestMethod]
    public void TestMethod()
    {
        // Manually record test if needed
        // More control, but requires manual tracking
    }
}
```

**Advantages:**
- More control over SDK lifecycle
- Can be combined with custom base classes
- Useful for advanced scenarios

## Metadata Extraction

The adapter automatically extracts rich metadata from your MSTest tests:

```csharp
[TestClass]
public class ApiTests : XpingTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("API")]
    [TestProperty("Team", "BackendTeam")]
    [TestProperty("Importance", "High")]
    public void ApiAuthenticationTest()
    {
        // Test implementation
    }
}
```

Extracted metadata:
- **TestCategory**: Captured as categories
- **TestProperty**: Captured as custom attributes and tags
- **Data Rows**: Arguments captured for data-driven tests
- **Test Context**: Results directory and other context information
- **Framework**: mstest (automatically added)

## DataTestMethod Support

Data-driven tests are fully supported:

```csharp
[TestClass]
public class StringTests : XpingTestBase
{
    [DataTestMethod]
    [DataRow("hello", 5)]
    [DataRow("world", 5)]
    [DataRow("", 0)]
    [TestCategory("Unit")]
    public void String_Length_ReturnsExpected(string input, int expectedLength)
    {
        Assert.AreEqual(expectedLength, input.Length);
    }
}
```

Each data row is tracked as a separate test execution with its arguments stored in metadata.

## Outcome Mapping

MSTest test outcomes are mapped to Xping outcomes:

| MSTest Outcome | Xping Outcome |
|---------------|---------------|
| Passed | Passed |
| Failed | Failed |
| Inconclusive | Inconclusive |
| Timeout | Failed |
| Aborted | Failed |
| Unknown | NotExecuted |
| NotRunnable | NotExecuted |
| InProgress | NotExecuted |

## Skipped Tests

Tests with the `[Ignore]` attribute are tracked as skipped:

```csharp
[TestClass]
public class SlowTests : XpingTestBase
{
    [TestMethod]
    [Ignore("Takes too long to run regularly")]
    public void SlowIntegrationTest()
    {
        // Test implementation
    }
}
```

## Advanced Usage

### Programmatic Configuration

You can initialize with custom configuration:

```csharp
[TestClass]
public static class CustomTestSetup
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
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

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await XpingContext.FlushAsync();
        await XpingContext.DisposeAsync();
    }
}
```

### TestContext Access

The base class provides access to MSTest's `TestContext`:

```csharp
[TestClass]
public class ContextTests : XpingTestBase
{
    [TestMethod]
    public void TestUsingContext()
    {
        TestContext.WriteLine("This message appears in test output");
        Assert.IsNotNull(TestContext);
        Assert.IsNotNull(TestContext.TestName);
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

The MSTest adapter uses inheritance and attributes for test tracking:

1. **XpingTestBase** - Optional base class with `[TestInitialize]` and `[TestCleanup]` methods
2. **XpingAssemblyInitialize** - Static class with `[AssemblyInitialize]` and `[AssemblyCleanup]` for SDK lifecycle
3. **XpingContext** - Global context managing SDK lifecycle and test recording
4. **TestContext** - MSTest's context providing test metadata and outcome

Test timing uses high-resolution `Stopwatch.GetTimestamp()` for accurate duration measurement.

## Thread Safety

The adapter is fully thread-safe for parallel test execution:

- **XpingContext**: Thread-safe singleton initialization with locking
- **TestContext**: Per-test instance provided by MSTest framework
- **Timing**: Uses per-test properties for thread-safe timing

This design ensures safe execution when MSTest runs tests in parallel.

## Error Handling

The adapter includes comprehensive error handling:

- Exceptions during test tracking are silently caught
- Test results remain unaffected even if tracking fails
- Failed uploads are retried automatically with exponential backoff
- No impact on test execution or outcomes

## Troubleshooting

### Tests not being tracked

1. Verify your test class inherits from `XpingTestBase`
2. Check `appsettings.json` has a `"Xping"` section with configuration, or set environment variables
3. Verify API key and project ID are valid
4. Check console output for Xping initialization messages

### TestContext is null

If `TestContext` is null in your test:
1. Ensure you're using MSTest (not another framework)
2. Verify the property is named exactly `TestContext`
3. Check that it has a public setter

### Configuration not found

If no configuration is found, the adapter will:
1. Look for `appsettings.json` with a `"Xping"` section in the current directory
2. Check environment variables (with `XPING_` prefix)
3. Fall back to default offline-only mode

### Network Issues

When API is unavailable:
- Tests are queued locally in `~/.xping/queue`
- Automatic retry on next successful connection
- No test execution impact

## Sample Project

See the complete sample project at `samples/SampleApp.MSTest` for working examples of:
- Test class inheriting from XpingTestBase
- TestMethod and DataTestMethod usage
- TestCategory and TestProperty attributes
- Skipped test tracking with [Ignore]
- ExpectedException handling

## Comparison with Other Frameworks

| Feature | MSTest | NUnit | xUnit |
|---------|--------|-------|-------|
| Setup | Base class | Attribute | AssemblyInfo |
| Tracking | Automatic (base class) | Opt-in (attribute) | Automatic (all tests) |
| Configuration | Inherit once | Per fixture/test | One-time |
| Data Tests | DataTestMethod | TestCase | Theory |
| Overhead | Minimal | Minimal | Minimal |

## License

MIT License - Copyright (c) 2025 Xping.io
