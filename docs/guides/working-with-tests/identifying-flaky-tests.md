# Identifying Flaky Tests

Flaky tests—tests that sometimes pass and sometimes fail without code changes—are one of the most frustrating problems in software testing. They erode trust in your test suite, waste developer time, and can mask real issues. Xping helps you identify and understand flaky tests through intelligent detection and confidence scoring.

---

## What Makes a Test Flaky?

Flaky tests fail intermittently for reasons unrelated to code correctness. Common causes include:

- **Timing Issues**: Race conditions, timeouts, or slow external services
- **Environment Dependencies**: File system state, network conditions, or resource availability
- **Non-Deterministic Logic**: Random data, date/time dependencies, or unordered collections
- **Test Isolation Problems**: Shared state between tests or improper cleanup
- **External Service Flakiness**: Third-party APIs, databases, or infrastructure instability

---

## How Xping Detects Flakiness

Xping doesn't just track pass/fail results—it analyzes multiple dimensions of test behavior to identify flakiness patterns. By collecting rich telemetry data across test runs, Xping builds a comprehensive understanding of each test's reliability.

The confidence score combines six key factors to give you a complete picture of test reliability. Tests with low confidence scores (below 0.60) exhibit patterns that indicate flakiness rather than consistent failures.

> **Related**: For details on how confidence scores are calculated, see [Understanding Confidence Scores](../getting-started/understanding-confidence-scores.md).

---

## Using the Flaky Tests Tab

The easiest way to find flaky tests is through the **Flaky Tests** tab in the dashboard:

1. Navigate to the Xping Dashboard
2. Click on the **Flaky Tests** tab
3. View tests filtered to show only those with confidence scores below 0.60
4. Sort by confidence score (ascending) to see the most problematic tests first

This filtered view helps you prioritize reliability improvements by showing only tests that need attention.

> **Related**: For navigation details, see [Navigating the Dashboard](../getting-started/navigating-the-dashboard.md).

---

## Investigating a Flaky Test

When you identify a test with a low confidence score, follow this investigation process:

### 1. Review the Test Detail Page

Navigate to the test detail page to understand:
- **Which factors are pulling the score down?** Look at the factor breakdown
- **What's the confidence level?** If it's Low or Medium, you may need more data
- **What's the trend?** Is it getting worse or has it always been flaky?

### 2. Check the Confidence Score Breakdown

The breakdown shows individual scores for each of the six factors:
- **Historical Pass Rate**: Is the test failing frequently?
- **Execution Stability**: Are execution times varying wildly?
- **Retry Behavior**: Is it failing then passing on retry?
- **Environment Consistency**: Does it behave differently in different environments?
- **Failure Pattern Analysis**: Are failures random or predictable?
- **Dependency Impact**: Does it fail when other tests fail?

Low scores in specific factors point to the type of flakiness.

### 3. Examine Environment Analysis

If "Environment Consistency" has a low score:
- Compare pass rates across Local vs. CI
- Look for environment-specific failures
- Common causes:
  - File path differences (Windows backslashes vs. Unix forward slashes)
  - Environment variables not set in CI
  - Network connectivity differences
  - Timing differences (CI machines may be slower)

### 4. Review Execution History

Look for patterns in recent executions:
- Do failures happen at specific times of day?
- Do they occur during parallel execution?
- Do they correlate with high system load?
- Are certain environments more problematic?

### 5. Check the Flaky Detection Analysis

If the test is flagged as flaky, the Flaky Detection Analysis section provides:
- **Flakiness Category**: Type of flakiness detected (Timing, Environment, Concurrency, etc.)
- **Root Cause Analysis**: Specific indicators found
- **Recommendations**: Actionable suggestions for fixing the issue
- **Confidence**: How certain the system is about the categorization

---

## Common Flakiness Indicators

### Retry Behavior (Low Score)
- The test is failing initially but passing on retry
- Classic sign of timing issues or race conditions
- Example: Timeout on first attempt, success on second attempt

### Environment Consistency (Low Score)
- Works locally, fails in CI (or vice versa)
- Different pass rates across operating systems
- Example: 100% pass on macOS, 60% pass on Linux

### Execution Stability (Low Score)
- Wide variance in execution times
- Occasional performance spikes
- Example: Usually 120ms, occasionally 2400ms

### Failure Pattern (Low Score)
- Random, unpredictable failures
- No correlation with code changes
- Example: Fails 3 times, passes 47 times with no pattern

### Dependency Impact (Low Score)
- Fails when other tests fail
- Shared resource contention
- Example: 80% of failures occur when another specific test also fails

---

## False Positives: When Low Scores Don't Mean Flakiness

A low confidence score doesn't automatically mean flakiness. Consider these scenarios:

**Consistently Broken Test:**
- Test fails 100% of the time due to a real bug
- Low score because it's failing
- **Not flaky** - just broken

**Recently Introduced Test:**
- Fewer than 25 executions
- Confidence level is "Low"
- **May not be flaky** - just needs more data

**Environment-Specific Bug:**
- Fails consistently in one environment
- Passes consistently in another
- **May not be flaky** - could be environment-specific bug

Xping's Flaky Detection Analysis helps distinguish between these cases and true flakiness.

---

## See Also

- [Common Flaky Patterns](./common-flaky-patterns.md) - Catalog of flakiness patterns and detection signatures
- [Fixing Flaky Tests](./fixing-flaky-tests.md) - Strategies for addressing flaky tests
- [Understanding Confidence Scores](../getting-started/understanding-confidence-scores.md) - How scores are calculated
- [Navigating the Dashboard](../getting-started/navigating-the-dashboard.md) - Using the dashboard to find tests
