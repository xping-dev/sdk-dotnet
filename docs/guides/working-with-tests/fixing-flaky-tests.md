# Fixing Flaky Tests

Once you've identified a flaky test, it's time to fix it. This guide provides a systematic approach to investigating and fixing flaky tests, with specific strategies for each common flakiness pattern.

---

## Investigation Workflow

Follow this six-step workflow to systematically address flaky tests:

### 1. Review Xping Telemetry

Check the test detail page in the Xping dashboard:
- Execution times across runs
- Environment pass rates (Local vs. CI)
- Failure messages and patterns
- Which confidence score factors are low

### 2. Look for Patterns

Use the six confidence score factors to hypothesize the cause:
- **Execution Stability (Low)**: Likely timing or race condition issues
- **Retry Behavior (Low)**: Test passes after retry - transient failure
- **Environment Consistency (Low)**: Environment-specific configuration or dependencies
- **Dependency Impact (Low)**: Shared state or external service issues
- **Failure Pattern (Low)**: Random failures suggest non-deterministic data
- **Historical Pass Rate (Low)**: Could be consistently broken, not flaky

> **Related**: For detailed pattern recognition, see [Common Flaky Patterns](./common-flaky-patterns.md).

### 3. Reproduce Locally

Try to reproduce the flakiness:
```bash
# Run the test multiple times in isolation
dotnet test --filter "FullyQualifiedName~FlakyTestName" --logger "console;verbosity=detailed"

# Run it 10 times to see if it fails
for i in {1..10}; do dotnet test --filter "FullyQualifiedName~FlakyTestName"; done

# Run with the full test suite (check for shared state issues)
dotnet test
```

### 4. Check Environment Differences

Compare behavior between environments:
- Environment variables set in CI but not locally
- File path differences (Windows vs. Linux)
- Network connectivity and timeouts
- Resource limits (memory, file handles)
- Timing (CI machines may be slower)

### 5. Review Recent Changes

Use Xping's historical data to identify when flakiness started:
- Check the confidence score trend
- Look at the execution history timeline
- Correlate with recent code changes or deployments

### 6. Monitor After Fixes

After implementing a fix:
1. Let the test run at least 20-30 more times
2. Watch the confidence score trend in Xping
3. Verify the score moves toward Reliable or Highly Reliable
4. Check that the problematic factor scores improve

**Expected timeline:**
- **Immediate**: Recent executions show fewer failures
- **3-5 days**: Pass rate improves noticeably
- **1-2 weeks**: Confidence score reflects new stability
- **2-4 weeks**: Confidence level increases as more data is collected

---

## Fix Strategies by Pattern

### Race Conditions

**Indicators:**
- Low Execution Stability (high timing variance)
- Passes on retry
- Failures during parallel execution

**Fixes:**

1. **Add Explicit Waits**
   ```csharp
   // BAD: Arbitrary delay
   await Task.Delay(1000);

   // GOOD: Wait for specific condition
   await WaitForConditionAsync(
       () => order.Status == OrderStatus.Completed,
       timeout: TimeSpan.FromSeconds(10)
   );
   ```

2. **Use Proper Synchronization**
   ```csharp
   // BAD: No synchronization
   processor.ProcessAsync(order); // Fire and forget
   Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));

   // GOOD: Await completion
   await processor.ProcessAsync(order);
   Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
   ```

3. **Polling with Timeout**
   ```csharp
   public async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
   {
       var stopwatch = Stopwatch.StartNew();
       while (!condition() && stopwatch.Elapsed < timeout)
       {
           await Task.Delay(100);
       }
       if (!condition())
           throw new TimeoutException($"Condition not met within {timeout}");
   }
   ```

---

### External Service Dependencies

**Indicators:**
- Low Environment Consistency
- High Dependency Impact
- Clustered failures (multiple tests fail together)

**Fixes:**

1. **Mock External Services**
   ```csharp
   // BAD: Real HTTP call
   var client = new HttpClient();
   var response = await client.GetAsync("https://api.example.com/user/123");

   // GOOD: Mock the HTTP client
   var mockClient = new Mock<IHttpClient>();
   mockClient.Setup(c => c.GetAsync(It.IsAny<string>()))
       .ReturnsAsync(new HttpResponseMessage
       {
           StatusCode = HttpStatusCode.OK,
           Content = new StringContent("{\"name\":\"John Doe\"}")
       });
   ```

2. **Use Contract Testing**
   ```csharp
   // Instead of calling real API, verify your code works with contract
   [Test]
   public void ProcessUser_WithValidResponse_ParsesCorrectly()
   {
       var contractJson = File.ReadAllText("contracts/user-response.json");
       var user = JsonSerializer.Deserialize<User>(contractJson);

       Assert.That(user.Name, Is.EqualTo("John Doe"));
   }
   ```

3. **Tag Tests That Need External Services**
   ```csharp
   [Test]
   [Category("external-service")]
   [Category("integration")]
   [Category("slow")]
   public async Task RealApiIntegration_Works()
   {
       // Only run these occasionally or in specific CI jobs
   }
   ```

---

### Shared State

**Indicators:**
- Pass when run alone, fail in suite
- High Dependency Impact
- Order-dependent failures

**Fixes:**

1. **Independent Setup/Teardown**
   ```csharp
   // BAD: Shared static state
   private static Database _database = new Database();

   // GOOD: Instance per test
   private Database _database;

   [SetUp]
   public void SetUp()
   {
       _database = new Database();
   }

   [TearDown]
   public void TearDown()
   {
       _database.Dispose();
   }
   ```

2. **Use Unique Resources Per Test**
   ```csharp
   [Test]
   public void CreateFile_Succeeds()
   {
       // BAD: Fixed filename (collides in parallel execution)
       var filename = "test.txt";

       // GOOD: Unique filename per test
       var filename = $"test-{Guid.NewGuid()}.txt";

       File.WriteAllText(filename, "content");
       try
       {
           Assert.That(File.Exists(filename), Is.True);
       }
       finally
       {
           File.Delete(filename);
       }
   }
   ```

3. **Reset Static State**
   ```csharp
   public class ConfigurationCache
   {
       private static Dictionary<string, string> _cache = new();

       // Provide a way to reset for testing
       public static void ResetForTesting()
       {
           _cache.Clear();
       }
   }

   [TearDown]
   public void TearDown()
   {
       ConfigurationCache.ResetForTesting();
   }
   ```

---

### Time-Based Flakiness

**Indicators:**
- Temporal clustering in failure pattern
- Fails at specific times or dates
- Inconsistent with no clear environmental cause

**Fixes:**

1. **Mock System Time**
   ```csharp
   // Create a time provider abstraction
   public interface ITimeProvider
   {
       DateTime Now { get; }
       DateTime UtcNow { get; }
   }

   // Use it in your code
   public class EventScheduler
   {
       private readonly ITimeProvider _timeProvider;

       public EventScheduler(ITimeProvider timeProvider)
       {
           _timeProvider = timeProvider;
       }

       public Event CreateEvent(string name)
       {
           return new Event
           {
               Name = name,
               CreatedAt = _timeProvider.UtcNow
           };
       }
   }

   // Test with fixed time
   [Test]
   public void ScheduleEvent_CreatesEventWithCurrentTime()
   {
       var mockTime = new Mock<ITimeProvider>();
       var fixedTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
       mockTime.Setup(t => t.UtcNow).Returns(fixedTime);

       var scheduler = new EventScheduler(mockTime.Object);
       var event = scheduler.CreateEvent("Meeting");

       Assert.That(event.CreatedAt, Is.EqualTo(fixedTime));
   }
   ```

2. **Use Relative Time Comparisons**
   ```csharp
   // BAD: Absolute time comparison
   Assert.That(event.CreatedAt, Is.EqualTo(DateTime.Now));

   // GOOD: Relative comparison with tolerance
   Assert.That(event.CreatedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
   ```

---

### Resource Exhaustion

**Indicators:**
- Pass rate degrades over time within a test run
- Execution stability decreases
- "Out of memory" or "too many open files" errors

**Fixes:**

1. **Proper Resource Disposal**
   ```csharp
   // BAD: Resource leak
   [Test]
   public async Task ProcessFile_Succeeds()
   {
       var stream = File.OpenRead("large-file.txt");
       await processor.ProcessAsync(stream);
       // Stream never disposed!
   }

   // GOOD: Using statement
   [Test]
   public async Task ProcessFile_Succeeds()
   {
       using var stream = File.OpenRead("large-file.txt");
       await processor.ProcessAsync(stream);
   } // Stream automatically disposed
   ```

2. **Reset Connection Pools**
   ```csharp
   [TearDown]
   public void TearDown()
   {
       // Reset database connection pool
       SqlConnection.ClearAllPools();
   }
   ```

3. **Limit Parallelism If Needed**
   ```csharp
   // In your test project file (.csproj) for resource-intensive tests
   <PropertyGroup>
       <MaxParallelThreads>4</MaxParallelThreads>
   </PropertyGroup>
   ```

---

### Non-Deterministic Data

**Indicators:**
- Random failure patterns
- No correlation with environment or timing
- Truly unpredictable failures

**Fixes:**

1. **Use Fixed Seeds for Random Data**
   ```csharp
   // BAD: Random seed
   var random = new Random();
   var testData = Enumerable.Range(0, 100).Select(_ => random.Next()).ToList();

   // GOOD: Fixed seed
   var random = new Random(12345); // Always generates same sequence
   var testData = Enumerable.Range(0, 100).Select(_ => random.Next()).ToList();
   ```

2. **Avoid Non-Deterministic Functions in Assertions**
   ```csharp
   // BAD: GUID comparison
   var userId = Guid.NewGuid();
   var user = service.GetUser(userId);
   Assert.That(user.Id, Is.EqualTo(Guid.NewGuid())); // Different GUID!

   // GOOD: Use the same GUID
   var userId = Guid.Parse("12345678-1234-1234-1234-123456789012");
   var user = service.GetUser(userId);
   Assert.That(user.Id, Is.EqualTo(userId));
   ```

3. **Be Explicit About Collection Order**
   ```csharp
   // BAD: Assumes order
   var users = service.GetUsers();
   Assert.That(users[0].Name, Is.EqualTo("Alice"));

   // GOOD: Sort explicitly or check without order
   var users = service.GetUsers().OrderBy(u => u.Name).ToList();
   Assert.That(users[0].Name, Is.EqualTo("Alice"));

   // Or check for presence without order
   Assert.That(users.Select(u => u.Name), Contains.Item("Alice"));
   ```

---

## Using Metadata to Track Flaky Tests

While investigating, tag tests with metadata to help with analysis:

**NUnit:**
```csharp
[Test]
[Category("flaky")]
[Category("under-investigation")]
[Description("Intermittent timeout - investigating race condition")]
public void ProcessOrder_CompletesSuccessfully()
{
    // Test implementation
}
```

**xUnit:**
```csharp
[Fact]
[Trait("Status", "flaky")]
[Trait("Issue", "race-condition")]
[Trait("InvestigatedBy", "alice")]
public void ProcessOrder_CompletesSuccessfully()
{
    // Test implementation
}
```

**MSTest:**
```csharp
[TestMethod]
[TestCategory("flaky")]
[TestCategory("under-investigation")]
[Description("Intermittent timeout - investigating race condition")]
public void ProcessOrder_CompletesSuccessfully()
{
    // Test implementation
}
```

---

## When to Disable a Flaky Test

Temporarily disable highly flaky tests (confidence < 0.30) if:
- Investigation will take time and the test is blocking CI
- The test is not critical to current development
- You need to unblock the team while fixing

```csharp
[Test]
[Ignore("Flaky test - tracked in JIRA-12 3")]
public void HighlyFlakyTest()
{
    // Will be re-enabled after fix
}
```

**Important**: Always track disabled tests and re-enable them after fixing!

---

## See Also

- [Common Flaky Patterns](./common-flaky-patterns.md) - Pattern catalog for diagnosis
- [Identifying Flaky Tests](./identifying-flaky-tests.md) - How to find flaky tests
- [Monitoring Test Health](./monitoring-test-health.md) - Track improvements over time
- [Best Practices](./best-practices.md) - Prevent flakiness from the start
