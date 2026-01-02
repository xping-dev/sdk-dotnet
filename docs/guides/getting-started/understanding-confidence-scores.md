# Understanding Confidence Scores

The **confidence score** is Xping's primary metric for test reliability. It's a value between 0.0 and 1.0 that answers the question: *"Can I trust this test?"*

Every test that has at least 10 executions receives a confidence score. This score is calculated by analyzing multiple factors about test behavior and aggregating them into a single, easy-to-understand number.

---

## Score Ranges & Categories

Xping automatically categorizes tests based on their confidence score:

| Score Range | Category | Badge Color | Meaning |
|-------------|----------|-------------|---------|
| **0.90 - 1.00** | **Highly Reliable** | 🟢 Green | Excellent - Highly consistent behavior. Failures are almost certainly real bugs. |
| **0.75 - 0.89** | **Reliable** | 🟢 Light Green | Good - Mostly reliable with minor inconsistencies. Generally trustworthy. |
| **0.60 - 0.74** | **Moderately Reliable** | 🟡 Yellow | Fair - Shows some flakiness. Monitor for patterns. |
| **0.40 - 0.59** | **Unreliable** | 🟠 Orange | Poor - Significant flakiness detected. Investigate and fix. |
| **0.00 - 0.39** | **Highly Unreliable** | 🔴 Red | Critical - Severe flakiness. Fix urgently or disable. |

---

## Confidence Level: How Reliable is the Score?

In addition to the score itself, Xping shows a **Confidence Level** that indicates how statistically reliable the score calculation is. This is based on sample size:

| Confidence Level | Runs Required | Meaning |
|-----------------|---------------|---------|
| **No Data** | < 10 runs | Insufficient data - need more test executions |
| **Low** | 10-24 runs | Tentative determination - score may fluctuate |
| **Medium** | 25-49 runs | Reasonably reliable - recommended minimum for production |
| **High** | 50-99 runs | Very reliable determination - suitable for critical tests |
| **Very High** | 100+ runs | Extremely reliable - ideal for comprehensive analysis |

**Example:** A test showing "0.85 (High Confidence)" means:
- The test has a reliability score of 0.85 (Reliable category)
- This score is based on 50-99 test executions
- The score is statistically reliable and can be trusted

**Example:** A test showing "0.92 (Low Confidence)" means:
- The test has a high score of 0.92 (Highly Reliable category)
- But it's only based on 10-24 executions
- The score may change significantly as more data is collected

---

## Score Trends

Xping tracks how confidence scores change over time and displays trend indicators:

- **Significant Improvement** ⬆️ Green: Score increased by +10% or more
- **Minor Improvement** ↗️ Light Green: Score increased by +5% to +10%
- **Stable** ➡️ Gray: Score changed less than ±5%
- **Minor Degradation** ↘️ Orange: Score decreased by -5% to -10%
- **Significant Degradation** ⬇️ Red: Score decreased by -10% or more

Trends help you quickly spot tests that are getting better or worse over time.

---

## The 6 Scoring Factors

The confidence score combines six key factors that examine different aspects of test behavior. Each factor has a specific weight that reflects its importance in determining overall reliability.

### 1. Historical Pass Rate (35% weight)

The most direct indicator of reliability—how often does this test pass?

**What Xping looks for:**
- Consistent pass rates near 100% indicate reliable tests
- Pass rates between 20-80% suggest classic flakiness
- Very low pass rates might indicate broken tests rather than flakiness

**How it affects the score:**
- 100% pass rate = maximum contribution
- <80% pass rate = significant penalty

**Example pattern:**
```
Test: UserLoginTest
Last 50 runs: 47 passed, 3 failed
Pass rate: 94% → Suggests occasional flakiness
```

### 2. Execution Stability (20% weight)

How consistent are the test's execution characteristics?

**What Xping looks for:**
- Stable execution times across runs
- Consistent resource usage patterns
- Predictable behavior in test setup and teardown
- High variance in execution time reduces the score

**Example pattern:**
```
Test: DatabaseQueryTest
Run 1: 120ms
Run 2: 125ms
Run 3: 2400ms ← Unusual spike
Run 4: 118ms
→ High variance indicates instability
```

### 3. Retry Behavior (15% weight)

Does the test pass when retried after initial failure?

**What Xping looks for:**
- Tests that fail then pass on retry are highly suspect
- Consistent pass-after-retry patterns indicate transient issues
- Tests requiring multiple retries signal deeper problems

**How it affects the score:**
- Frequent retry success significantly lowers the score
- Classic sign of flakiness

**Example pattern:**
```
Test: ApiIntegrationTest
Initial run: Failed (timeout)
Retry 1: Passed
→ Classic flaky test signature
```

### 4. Environment Consistency (15% weight)

Does the test behave differently across environments?

**What Xping looks for:**
- Pass rates varying between CI/local environments
- Differences across operating systems or runtime versions
- Environment-specific failure modes
- Environment-dependent failures indicate flakiness

**Example pattern:**
```
Test: FileSystemTest
Local (macOS): 100% pass rate
CI (Linux): 60% pass rate
→ Environment-dependent behavior
```

### 5. Failure Pattern Analysis (10% weight)

Are failures predictable or random?

**What Xping looks for:**
- Recurring error messages or exception types
- Time-based failure patterns (e.g., only fails on Mondays)
- Correlation with other test failures
- Random failures are harder to fix and score lower

**Example pattern:**
```
Test: CacheTest
Failures: All occur during parallel test runs
→ Suggests shared state or race condition
```

### 6. Dependency Impact (5% weight)

How do related tests and dependencies affect reliability?

**What Xping looks for:**
- Tests that fail together (cascade failures)
- Dependency on external services or shared resources
- Impact from setup/teardown in other tests
- Test isolation issues

**Example pattern:**
```
Test: OrderProcessingTest
Failures: 80% occur when PaymentServiceTest also fails
→ Indicates external service dependency
```

---

## When Are Confidence Scores Calculated?

Confidence scores are calculated automatically after new test executions are uploaded:

1. **Initial Calculation**: After a test reaches 10 executions
2. **Recalculation Triggers**:
   - New test executions are uploaded (threshold-based)
   - Tests with changing behavior are prioritized
   - Scheduled recalculation for all active tests

3. **Minimum Data Requirements**:
   - **Overall**: At least 10 test executions
   - **Per Factor**: Some factors need more data (20+ runs for environment analysis)
   - **Insufficient Data**: Tests show "Insufficient Data" badge until minimum is met

---

## See Also

- [Navigating the Dashboard](./navigating-the-dashboard.md) - Learn how to view confidence scores in the Xping dashboard
- [Interpreting Test Results](./interpreting-test-results.md) - How to use confidence scores to make decisions about your tests
- [Identifying Flaky Tests](../working-with-tests/identifying-flaky-tests.md) - Use confidence scores to find unreliable tests
