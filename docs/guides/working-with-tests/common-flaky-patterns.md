# Common Flaky Test Patterns

Understanding common patterns of test flakiness helps you quickly diagnose and categorize unreliable tests. This guide catalogs the six most common flaky test patterns, their symptoms, and how Xping detects each one.

---

## Pattern 1: Race Conditions

### Symptoms
- Intermittent failures with no code changes
- Different results when running in isolation vs. parallel
- Timing-related error messages ("timeout", "was not ready")
- Works when debugged but fails in normal execution

### How Xping Identifies It
- **Low Execution Stability Score**: High variance in execution timing (e.g., usually 120ms but occasionally 2400ms)
- **Failures Correlated with Parallel Runs**: Tests fail more often during parallel execution
- **Retry Behavior**: Shows passes after initial failures
- **Failure Pattern**: Random, unpredictable timing

### Example

```csharp
[Test]
public async Task ProcessOrder_CompletesSuccessfully()
{
    var order = CreateTestOrder();
    var processor = new OrderProcessor();

    // Race condition: Order status might not be updated yet
    processor.ProcessAsync(order); // Missing await!

    Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
    // Sometimes fails because ProcessAsync hasn't finished yet
}
```

**Xping Detection:**
- Execution Stability: 0.45 (high variance in timing)
- Retry Behavior: 0.52 (often passes on second attempt)
- Overall Confidence: 0.48 (Unreliable)

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md#race-conditions).

---

## Pattern 2: External Service Dependencies

### Symptoms
- Failures during network issues or service outages
- Different behavior between local and CI environments
- Correlated failures across multiple tests
- Timeouts or connection errors

### How Xping Identifies It
- **Low Environment Consistency Score**: Different pass rates across environments
- **High Dependency Impact Score**: Failures correlate with other tests using the same service
- **Failure Pattern Analysis**: Shows clustering of failures (multiple tests fail together)
- **Network Metrics**: Correlation with network latency or availability

### Example

```csharp
[Test]
public async Task GetUser_ReturnsUserData()
{
    var client = new HttpClient();

    // Calls real API - fails when API is down or slow
    var response = await client.GetAsync("https://api.example.com/user/123");
    var user = await response.Content.ReadAsAsync<User>();

    Assert.That(user.Name, Is.EqualTo("John Doe"));
}
```

**Xping Detection:**
- Environment Consistency: 0.38 (works locally, fails in CI)
- Dependency Impact: 0.42 (fails when PaymentServiceTests also fail)
- Overall Confidence: 0.41 (Unreliable)

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md#external-service-dependencies).

---

## Pattern 3: Shared State and Test Order Dependency

### Symptoms
- Pass when run alone, fail when run with other tests
- Different results based on test execution order
- "Setup already completed" or "resource in use" errors
- Static state pollution

### How Xping Identifies It
- **Pass Rate Varies**: Significantly different between isolated and suite runs
- **High Dependency Impact Score**: Failures correlate with specific test combinations
- **Failure Pattern**: Failures during parallel execution
- **Execution Context**: Correlation with worker ID or position in suite

### Example

```csharp
public class UserServiceTests
{
    // Shared static state - BAD!
    private static Database _database = new Database();

    [Test]
    public void Test1_CreateUser()
    {
        _database.Insert(new User { Id = 1, Name = "Alice" });
        Assert.That(_database.Count(), Is.EqualTo(1));
    }

    [Test]
    public void Test2_CreateUser()
    {
        // Fails if Test1 ran first - database already has data!
        _database.Insert(new User { Id = 1, Name = "Bob" });
        Assert.That(_database.Count(), Is.EqualTo(1)); // Actually 2!
    }
}
```

**Xping Detection:**
- Dependency Impact: 0.35 (fails when other tests in class fail)
- Execution Stability: 0.48 (varies based on test order)
- Overall Confidence: 0.39 (Highly Unreliable)

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md#shared-state).

---

## Pattern 4: Time-Based Flakiness

### Symptoms
- Failures at specific times (end of month, weekends, timezone changes)
- Date/time comparison failures
- Intermittent failures that aren't immediately reproducible
- Daylight saving time issues

### How Xping Identifies It
- **Failure Pattern Analysis**: Shows temporal clustering (failures at specific times)
- **Low Execution Stability**: No clear cause for variance
- **Historical Pass Rate**: Periodic dips corresponding to time periods
- **Environment Consistency**: Different behavior across timezones

### Example

```csharp
[Test]
public void ScheduleEvent_CreatesEventForToday()
{
    var scheduler = new EventScheduler();

    // Flaky: Depends on current date/time
    var startOfDay = DateTime.Now.Date;
    var event = scheduler.CreateEvent("Meeting", startOfDay);

    // Fails when test runs around midnight (date changes)
    Assert.That(event.Date, Is.EqualTo(DateTime.Now.Date));
}
```

**Xping Detection:**
- Failure Pattern: 0.44 (temporal clustering around specific times)
- Execution Stability: 0.51 (inconsistent but no clear reason)
- Overall Confidence: 0.47 (Unreliable)

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md#time-based-flakiness).

---

## Pattern 5: Resource Exhaustion

### Symptoms
- Failures increase as test suite runs longer
- "Out of memory", "too many open files", or connection pool errors
- First runs pass, later runs fail
- Performance degrades over time

### How Xping Identifies It
- **Pass Rate Degrades**: Within a single test session over time
- **Execution Stability Decreases**: Later tests take longer
- **Dependency Impact**: Shows cascade patterns (failures spread to more tests)
- **Position in Suite**: Tests later in execution order fail more often

### Example

```csharp
[Test]
public async Task ProcessLargeFile_Succeeds()
{
    // File handle never disposed - leak!
    var stream = File.OpenRead("large-file.txt");
    var processor = new FileProcessor();

    await processor.ProcessAsync(stream);
    // Missing stream.Dispose() or 'using' statement

    Assert.That(processor.Status, Is.EqualTo(ProcessStatus.Success));
}

// After 100 runs, OS runs out of file handles
```

**Xping Detection:**
- Execution Stability: 0.36 (degrades over time)
- Historical Pass Rate: 0.44 (first runs pass, later fail)
- Overall Confidence: 0.38 (Highly Unreliable)

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md#resource-exhaustion).

---

## Pattern 6: Non-Deterministic Test Data

### Symptoms
- Failures related to unexpected data values
- Random assertion failures
- Intermittent null reference exceptions
- Unordered collection comparison failures

### How Xping Identifies It
- **Random Failure Patterns**: No clear correlation with any factor
- **Low Historical Pass Rate**: Scattered failures across all executions
- **Retry Behavior**: Inconsistent (sometimes passes on retry, sometimes doesn't)
- **No Environmental or Timing Correlation**: Truly random

### Example

```csharp
[Test]
public void ProcessUsers_SortsCorrectly()
{
    var users = new List<User>
    {
        new User { Id = Guid.NewGuid(), Name = "Alice" }, // Random GUID
        new User { Id = Guid.NewGuid(), Name = "Bob" }
    };

    var sorted = UserService.SortById(users);

    // Flaky: GUID order is random!
    Assert.That(sorted[0].Name, Is.EqualTo("Alice"));
}
```

**Xping Detection:**
- Failure Pattern: 0.40 (random, no correlation)
- Historical Pass Rate: 0.55 (intermittent failures)
- Overall Confidence: 0.45 (Unreliable)

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md#non-deterministic-data).

---

## Pattern Recognition Summary

Use this table to quickly identify the pattern based on Xping factor scores:

| Pattern | Execution Stability | Retry Behavior | Environment Consistency | Dependency Impact | Failure Pattern |
|---------|-------------------|----------------|----------------------|------------------|-----------------|
| **Race Conditions** | 🔴 Low | 🔴 Low | ✅ High | ✅ High | Random timing |
| **External Services** | ✅ High | 🟡 Medium | 🔴 Low | 🔴 Low | Clustered |
| **Shared State** | 🟡 Medium | ✅ High | ✅ High | 🔴 Low | Order-dependent |
| **Time-Based** | 🟡 Medium | 🟡 Medium | 🟡 Medium | ✅ High | Temporal |
| **Resource Exhaustion** | 🔴 Low | 🟡 Medium | 🟡 Medium | 🔴 Low | Degrades over time |
| **Non-Deterministic Data** | ✅ High | 🟡 Medium | ✅ High | ✅ High | Truly random |

---

## See Also

- [Identifying Flaky Tests](./identifying-flaky-tests.md) - How to find flaky tests in your suite
- [Fixing Flaky Tests](./fixing-flaky-tests.md) - Strategies for fixing each pattern
- [Understanding Confidence Scores](../getting-started/understanding-confidence-scores.md) - How scores are calculated
- [Best Practices](./best-practices.md) - Prevent flakiness from the start
