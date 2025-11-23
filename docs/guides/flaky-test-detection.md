# Flaky Test Detection

Flaky tests—tests that sometimes pass and sometimes fail without code changes—are one of the most frustrating problems in software testing. They erode trust in your test suite, waste developer time, and can mask real issues. Xping helps you identify, understand, and address flaky tests through intelligent detection and confidence scoring.

## What Makes a Test Flaky?

Flaky tests fail intermittently for reasons unrelated to code correctness. Common causes include:

- **Timing Issues**: Race conditions, timeouts, or slow external services
- **Environment Dependencies**: File system state, network conditions, or resource availability
- **Non-Deterministic Logic**: Random data, date/time dependencies, or unordered collections
- **Test Isolation Problems**: Shared state between tests or improper cleanup
- **External Service Flakiness**: Third-party APIs, databases, or infrastructure instability

## How Xping Detects Flaky Tests

Xping doesn't just track pass/fail results—it analyzes multiple dimensions of test behavior to identify flakiness patterns. By collecting rich telemetry data across test runs, Xping builds a comprehensive understanding of each test's reliability.

### The Confidence Score

Every test gets a **confidence score** between 0 and 1, where:
- **1.0** = Highly trustworthy test with consistent, reliable behavior
- **0.5-0.9** = Moderate confidence with some inconsistencies
- **Below 0.5** = Low confidence indicating likely flakiness

This score combines six key factors to give you a complete picture of test reliability:

#### 1. Historical Pass Rate (35% weight)
The most direct indicator of reliability—how often does this test pass?

**What Xping looks for:**
- Consistent pass rates near 100% indicate reliable tests
- Pass rates between 20-80% suggest classic flakiness
- Very low pass rates might indicate broken tests rather than flakiness

**Example pattern:**
```
Test: UserLoginTest
Last 50 runs: 47 passed, 3 failed
Pass rate: 94% → Suggests occasional flakiness
```

#### 2. Execution Stability (20% weight)
How consistent are the test's execution characteristics?

**What Xping looks for:**
- Stable execution times across runs
- Consistent resource usage patterns
- Predictable behavior in test setup and teardown

**Example pattern:**
```
Test: DatabaseQueryTest
Run 1: 120ms
Run 2: 125ms
Run 3: 2400ms ← Unusual spike
Run 4: 118ms
→ High variance indicates instability
```

#### 3. Retry Behavior (15% weight)
Does the test pass when retried after initial failure?

**What Xping looks for:**
- Tests that fail then pass on retry are highly suspect
- Consistent pass-after-retry patterns indicate transient issues
- Tests requiring multiple retries signal deeper problems

**Example pattern:**
```
Test: ApiIntegrationTest
Initial run: Failed (timeout)
Retry 1: Passed
→ Classic flaky test signature
```

#### 4. Environment Consistency (15% weight)
Does the test behave differently across environments?

**What Xping looks for:**
- Pass rates varying between CI/local environments
- Differences across operating systems or runtime versions
- Environment-specific failure modes

**Example pattern:**
```
Test: FileSystemTest
Local (macOS): 100% pass rate
CI (Linux): 60% pass rate
→ Environment-dependent behavior
```

#### 5. Failure Pattern Analysis (10% weight)
Are failures predictable or random?

**What Xping looks for:**
- Recurring error messages or exception types
- Time-based failure patterns (e.g., only fails on Mondays)
- Correlation with other test failures

**Example pattern:**
```
Test: CacheTest
Failures: All occur during parallel test runs
→ Suggests shared state or race condition
```

#### 6. Dependency Impact (5% weight)
How do related tests and dependencies affect reliability?

**What Xping looks for:**
- Tests that fail together (cascade failures)
- Dependency on external services or shared resources
- Impact from setup/teardown in other tests

**Example pattern:**
```
Test: OrderProcessingTest
Failures: 80% occur when PaymentServiceTest also fails
→ Indicates external service dependency
```

## Interpreting Your Confidence Scores

### High Confidence (0.8 - 1.0)
**What it means:** These tests are reliable and trustworthy. Failures likely indicate real issues.

**Action:** Investigate failures seriously. These are your most valuable tests.

### Moderate Confidence (0.5 - 0.8)
**What it means:** Some inconsistency detected. The test works most of the time but has occasional issues.

**Action:** Monitor these tests. Consider investigating if the score trends downward.

### Low Confidence (0.0 - 0.5)
**What it means:** Significant flakiness detected. These tests cannot be trusted.

**Action:** Prioritize fixing these tests or temporarily disable them. They're creating more confusion than value.

## Common Flaky Test Patterns and Detection

### Pattern 1: Race Conditions
**Symptoms:**
- Intermittent failures with no code changes
- Different results when running in isolation vs. parallel
- Timing-related error messages ("timeout", "was not ready")

**How Xping identifies it:**
- Low execution stability score (high variance in timing)
- Failures correlated with parallel test runs
- Retry behavior shows passes after failures

**Strategies:**
- Add explicit waits or synchronization
- Use polling with timeouts instead of fixed delays
- Ensure proper test isolation with setup/teardown

### Pattern 2: External Service Dependencies
**Symptoms:**
- Failures during network issues or service outages
- Different behavior between local and CI environments
- Correlated failures across multiple tests

**How Xping identifies it:**
- Low environment consistency score
- High dependency impact score
- Failure pattern analysis shows clustering

**Strategies:**
- Mock external services for unit/integration tests
- Use contract testing instead of live service calls
- Add circuit breakers and proper timeout handling
- Tag tests that require external services

### Pattern 3: Shared State and Test Order Dependency
**Symptoms:**
- Pass when run alone, fail when run with other tests
- Different results based on test execution order
- "Setup already completed" or "resource in use" errors

**How Xping identifies it:**
- Pass rate varies significantly between isolated and suite runs
- High dependency impact score
- Failure pattern correlates with specific test combinations

**Strategies:**
- Ensure each test has independent setup/teardown
- Use test fixtures properly (NUnit SetUpFixture, xUnit CollectionFixture)
- Avoid static state or properly reset it between tests
- Consider test parallelization groups

### Pattern 4: Time-Based Flakiness
**Symptoms:**
- Failures at specific times (end of month, weekends, timezone changes)
- Date/time comparison failures
- Intermittent failures that aren't immediately reproducible

**How Xping identifies it:**
- Failure pattern analysis shows temporal clustering
- Low execution stability with no clear cause
- Historical pass rate with periodic dips

**Strategies:**
- Mock system time in tests
- Use relative time comparisons instead of absolute
- Make tests timezone-agnostic
- Set explicit date/time values in test setup

### Pattern 5: Resource Exhaustion
**Symptoms:**
- Failures increase as test suite runs longer
- "Out of memory", "too many open files", or connection pool errors
- First runs pass, later runs fail

**How Xping identifies it:**
- Pass rate degrades within a single test run
- Execution stability decreases over time
- Dependency impact shows cascade patterns

**Strategies:**
- Properly dispose of resources (use `using` statements in C#)
- Reset connection pools between tests
- Limit parallelism if needed
- Monitor resource usage in CI

### Pattern 6: Non-Deterministic Test Data
**Symptoms:**
- Failures related to unexpected data values
- Random assertion failures
- Intermittent null reference exceptions

**How Xping identifies it:**
- Random failure patterns with no clear correlation
- Low historical pass rate with scattered failures
- Retry behavior inconsistent

**Strategies:**
- Use fixed seed values for random data generators
- Avoid `DateTime.Now` or `Guid.NewGuid()` in assertions
- Use deterministic test data factories
- Be explicit about sort orders when checking collections

## Taking Action on Flaky Tests

### Immediate Actions
1. **Identify:** Check your Xping dashboard for tests with confidence scores below 0.7
2. **Prioritize:** Focus on frequently-run tests with low confidence—they cause the most disruption
3. **Document:** Add metadata tags to known flaky tests while investigating
4. **Isolate:** Temporarily disable highly flaky tests (< 0.3) until fixed

### Investigation Workflow
1. **Review Xping telemetry:** Check execution times, environments, and failure messages
2. **Look for patterns:** Use the six factors to hypothesize the flakiness cause
3. **Reproduce locally:** Try running the test multiple times in isolation and in suite
4. **Check environment differences:** Compare behavior between local and CI
5. **Review recent changes:** Use Xping's historical data to see when flakiness started

### Long-Term Strategies
- **Test Design:** Write tests that are deterministic and isolated by default
- **CI/CD Integration:** Use Xping's confidence scores to prevent merging flaky tests
- **Team Culture:** Make test reliability a shared responsibility
- **Continuous Monitoring:** Track confidence score trends over time
- **Automation:** Set up alerts for tests whose confidence drops below thresholds

## Using Xping Effectively

### Metadata for Flaky Tests
Tag tests with relevant metadata to help with analysis using your test framework's native attributes:

**NUnit:**
```csharp
[Test]
[Category("external-service")]
[Category("integration")]
[Description("Payment API integration test with 30s timeout")]
public void ProcessPayment_Should_Succeed()
{
    // Test implementation
}
```

**xUnit:**
```csharp
[Fact]
[Trait("Category", "external-service")]
[Trait("Category", "integration")]
[Trait("Service", "payment-api")]
[Trait("Timeout", "30s")]
public void ProcessPayment_Should_Succeed()
{
    // Test implementation
}
```

**MSTest:**
```csharp
[TestMethod]
[TestCategory("external-service")]
[TestCategory("integration")]
[Description("Payment API integration test with 30s timeout")]
public void ProcessPayment_Should_Succeed()
{
    // Test implementation
}
```

### Setting Confidence Thresholds
Configure your CI pipeline to block PRs with low-confidence tests:

```yaml
- name: Check Test Confidence
  run: |
    # Xping can fail the build if new tests have low confidence
    # This prevents introducing flaky tests into your codebase
```

### Regular Review Cycles
Schedule weekly or sprint-based reviews:
- Identify tests with declining confidence scores
- Celebrate teams that improve test reliability
- Share learnings about flakiness patterns across teams

## Next Steps

- **Configuration:** See [Environment Variables](../configuration/configuration-reference.md) to configure detection sensitivity
- **CI/CD Integration:** Check [CI/CD Setup](../getting-started/ci-cd-setup.md) for pipeline configuration
- **Troubleshooting:** Visit [Common Issues](../troubleshooting/common-issues.md) if you need help

---

**Remember:** Flaky tests are a symptom, not the disease. Xping helps you identify them, but fixing the underlying issues—race conditions, external dependencies, poor isolation—is what builds a reliable test suite you can trust.
