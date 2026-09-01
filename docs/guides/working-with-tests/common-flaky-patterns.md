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
- Failures at specific times (overnight, weekends, timezone changes)
- Date/time comparison failures
- Intermittent failures that aren't immediately reproducible
- Daylight saving time issues

### How Xping Identifies It

`xping report` emits a **`TimeSensitive`** finding when a test's failure rate depends on when
it ran. It takes one observation per run — a retried test is judged on its final attempt, because
every attempt within a run shares that run's clock — and splits those three ways, reporting the
widest gap:

- **Local time of day** — the worst six-hour quarter of the local day against the rest of it. Six
  hours rather than one, because a fortnight of runs cannot fill twenty-four hourly bins.
- **Weekend against weekday** — read on the machine's own clock. Note that a single weekday
  ("only fails on Mondays") is *not* detectable at the default window, which holds at most two of
  any given day.
- **UTC offset** — when the window contains two offsets for one time zone, which is what a daylight
  saving change looks like, the two sides are compared. This is how DST is detected without a
  timezone database.

Two things gate every finding: at least five **runs** on each side, and **failures spanning at
least three separate local days**. The second is what stops one bad evening being reported as an
evening pattern; the first is counted in runs so that one retried evening cannot fill a side on its
own.

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

```
MED   time sensitive   ...ScheduleEvent_CreatesEventForToday
      failed 100% in 00:00-06:00 local against 0% in the rest of the
      day, gap 100 pts across 4 days
```

The finding says *when*, never *why*. It is capped at medium severity for that reason: a clock
reading tells you where to look and nothing about what to fix.

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md#time-based-flakiness).

---

## Pattern 5: Non-Deterministic Test Data

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

## Pattern 6: Retries That Are Papering Over A Regression

### Symptoms
- The build is green, but the suite keeps getting slower
- A test with a retry attribute that "has always been a bit flaky"
- A red build on a test that has a retry attribute, so retries are apparently not the answer
- Nobody can say when it started

### How Xping Identifies It

Retry data answers two questions no other finding can, and `xping report` emits a distinct kind for
each. Both are decided on the attempts an adapter actually recorded, never on the limit the retry
attribute declared.

- **`RetryDeepening`** — the test still passes, but it now needs more attempts to do it than it used
  to. The recent runs are compared against the earlier ones on the attempts a *typical* passing run
  needed, by nearest rank, so one unlucky run cannot invent the trend or hide it. This is the one
  signal that fires while the build is still green, and it is the cheapest possible warning that
  something is decaying.
- **`RetryExhausted`** — the retries ran out and the test failed the run anyway. A run counts when
  its last recorded attempt failed and an earlier attempt exists. The evidence carries how often the
  retries *did* rescue the test, which is what separates a mitigation that is slipping from one that
  has never worked at all.

A test gets at most one retry finding: out of retries, then deeper retries, then masked by retry.
`RetryDeepening` is capped at medium severity, because nothing has failed a build yet — the claim is
that something is about to.

### Example

```csharp
[RetryFact(3)]
public async Task Checkout_CompletesWithinTheServiceBudget()
{
    var checkout = new CheckoutService(_client);

    // The service got slower. The retry still saves the build, so nobody noticed -
    // but where this used to pass first time, it now needs a third attempt.
    var result = await checkout.CompleteAsync(_basket);

    Assert.True(result.Succeeded);
}
```

**Xping Detection:**

```
MED   deeper retries  ...Checkout_CompletesWithinTheServiceBudget
      attempts to pass 1 -> 3 (+2) over 3 runs against 14 before,
      2.4s spent retrying
```

And once the retry budget stops covering it:

```
HIGH  out of retries  ...Checkout_CompletesWithinTheServiceBudget
      gave up after 3 attempts in 6 of 7 retried runs (86%), 41s spent
      retrying
```

The second block is the argument for deleting the retry attribute rather than raising it: the
retries are no longer buying a green build, only wall-clock.

> **Related**: Retry attempt tracking differs by framework — see
> [Known Limitations](../../known-limitations.md). Where an adapter cannot record each attempt,
> silence here is indistinguishable from "no retries happened".

---

## Pattern Recognition Summary

Use this table to quickly identify the pattern based on Xping factor scores:

| Pattern | Execution Stability | Retry Behavior | Environment Consistency | Dependency Impact | Failure Pattern |
|---------|-------------------|----------------|----------------------|------------------|-----------------|
| **Race Conditions** | 🔴 Low | 🔴 Low | ✅ High | ✅ High | Random timing |
| **External Services** | ✅ High | 🟡 Medium | 🔴 Low | 🔴 Low | Clustered |
| **Shared State** | 🟡 Medium | ✅ High | ✅ High | 🔴 Low | Order-dependent |
| **Time-Based** | 🟡 Medium | 🟡 Medium | 🟡 Medium | ✅ High | Temporal |
| **Non-Deterministic Data** | ✅ High | 🟡 Medium | ✅ High | ✅ High | Truly random |
| **Retries Papering Over It** | 🟡 Medium | 🔴 Low | ✅ High | 🟡 Medium | Worsening over time |

---

## See Also

- [Identifying Flaky Tests](./identifying-flaky-tests.md) - How to find flaky tests in your suite
- [Fixing Flaky Tests](./fixing-flaky-tests.md) - Strategies for fixing each pattern
- [Understanding Confidence Scores](../getting-started/understanding-confidence-scores.md) - How scores are calculated
- [Best Practices](./best-practices.md) - Prevent flakiness from the start
