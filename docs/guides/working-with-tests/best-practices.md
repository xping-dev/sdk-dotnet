# Best Practices

This guide consolidates best practices for maintaining test reliability using Xping. Follow these guidelines to build and maintain a trustworthy test suite.

---

## Making Confidence Scores Visible

### Include in PR Reviews

Make reliability part of your code review process:

```markdown
## PR Checklist
- [ ] All new tests have run at least 20 times
- [ ] New tests have confidence scores > 0.60
- [ ] No existing tests show declining trends due to these changes
- [ ] Link to Xping dashboard showing test results
```

### Set Minimum Thresholds

Define quality gates based on test importance:

**Recommended Thresholds:**
- **Critical path tests**: 0.85+ (Highly Reliable)
- **Integration tests**: 0.75+ (Reliable)
- **All tests**: 0.60+ (above Flaky threshold)

**Enforcement:**
- Don't merge code that introduces low-confidence tests
- Block PRs if existing tests degrade below thresholds
- Require investigation before merging tests with Medium or Low confidence levels

### Display in Team Areas

Keep reliability top-of-mind:
- Show Xping dashboard during standups
- Display flaky test count on team information radiators
- Include reliability metrics in sprint reviews
- Share weekly summaries in team channels

---

## Don't Ignore Low Scores

### Why Flaky Tests Are Expensive

Flaky tests cost more than they save:
- **Time Waste**: Team investigates failures that aren't real bugs
- **Eroded Trust**: Developers start ignoring all test failures
- **Hidden Bugs**: Real issues get dismissed as "probably just flaky"
- **CI/CD Delays**: Pipelines get stuck or require manual overrides
- **Productivity Loss**: Context switching to investigate intermittent failures

### Take Action Immediately

When you see low scores:
1. **Acknowledge**: Don't pretend it's not a problem
2. **Investigate**: Use Xping's telemetry to understand why
3. **Fix or Disable**: Either fix the root cause or temporarily disable
4. **Track**: Create tickets to ensure disabled tests get re-enabled

> **Related**: For fixing strategies, see [Fixing Flaky Tests](./fixing-flaky-tests.md).

---

## Fix at the Source

### Don't Mask Flakiness with Retries

**Problem Approach:**
```csharp
[Test]
[Retry(3)] // Hiding the problem!
public void FlakyTest()
{
    // Test with race condition
}
```

**Better Approach:**
```csharp
[Test]
public async Task ReliableTest()
{
    // Fixed the race condition with proper synchronization
    await WaitForConditionAsync(...);
}
```

### Identify Root Causes

Use Xping's factor breakdown to identify the real issue:
- **Low Retry Behavior**: Fix timing/synchronization
- **Low Environment Consistency**: Fix environment-specific dependencies
- **Low Execution Stability**: Fix resource contention or performance issues

### Use Retries Appropriately

Retries are acceptable for:
- **Temporary measure**: While investigating a fix
- **External services**: When testing real external APIs (with appropriate tags)
- **Infrastructure flakiness**: Network hiccups beyond your control

Retries are NOT acceptable for:
- **Hiding test design problems**: Race conditions, shared state
- **Permanent solution**: If you need retries long-term, the test is broken
- **All tests**: Blanket retry policies mask real issues

---

## Document Learnings

### Build Institutional Knowledge

When you fix a flaky test:

**Record What You Learned:**
```markdown
## Flaky Test Fix: UserLoginTest

**Problem**: Test failed intermittently with "element not found" error

**Root Cause**: Race condition - clicked login before page fully loaded

**Solution**: Added explicit wait for login button to be clickable

**Pattern**: Timing/Race Condition (see Common Flaky Patterns guide)

**Prevention**: Always wait for explicit conditions, never use fixed delays

**Xping Indicators**:
- Execution Stability: 0.42 (high variance)
- Retry Behavior: 0.38 (often passed on retry)
```

### Share with the Team

- Add to team wiki or documentation
- Present in engineering meetings
- Create runbooks for common patterns
- Mentor new team members on reliability

### Build a Pattern Library

Create a team reference:
- Common flakiness patterns you've seen
- Solutions that worked
- Anti-patterns to avoid
- Code snippets for reliable test patterns

---

## Set Quality Standards

### Define Minimum Scores by Test Type

**Critical Path Tests** (e.g., login, checkout, payment):
- Minimum confidence: 0.85 (Highly Reliable)
- Rationale: These must be rock-solid; failures indicate serious bugs
- Action: Investigate immediately if score drops below threshold

**Integration Tests** (e.g., API calls, database operations):
- Minimum confidence: 0.75 (Reliable)
- Rationale: Some environmental variance acceptable, but should be stable
- Action: Monitor and fix if degrading

**Unit Tests**:
- Minimum confidence: 0.75 (Reliable)
- Rationale: Should be completely deterministic
- Action: Very low scores indicate test design problems

**End-to-End Tests**:
- Minimum confidence: 0.60 (Moderately Reliable)
- Rationale: External dependencies may introduce some flakiness
- Action: Strive for higher but accept some environmental variance

### Enforce Standards

**In CI/CD:**
- Block PRs introducing tests below thresholds
- Fail builds if critical tests degrade
- Require manual approval for low-confidence tests

**In Code Review:**
- Check new tests in Xping before approving
- Require explanations for tests with low scores
- Suggest improvements based on Xping's analysis

---

## Test Design Principles

### Write Deterministic Tests

**Good:**
```csharp
// Fixed seed for reproducibility
var random = new Random(12345);
var testData = random.Next(1, 100);
```

**Bad:**
```csharp
// Non-deterministic
var random = new Random();
var testData = random.Next(1, 100);
```

### Ensure Proper Isolation

**Good:**
```csharp
private Database _database;

[SetUp]
public void SetUp()
{
    _database = new Database(); // Fresh instance per test
}

[TearDown]
public void TearDown()
{
    _database.Dispose(); // Clean up
}
```

**Bad:**
```csharp
private static Database _database = new Database(); // Shared state!
```

### Make Tests Environment-Agnostic

**Good:**
```csharp
[Test]
public void ProcessFile_Succeeds()
{
    var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "test.txt");
    // Works on Windows, Linux, macOS
}
```

**Bad:**
```csharp
[Test]
public void ProcessFile_Succeeds()
{
    var testFile = "C:\\temp\\test.txt"; // Windows-only!
}
```

---

## CI/CD Integration Strategies

### Run Tests Multiple Times

Before merging, ensure new tests are stable:
```yaml
- name: Stability Check for New Tests
  run: |
    # Run new tests 20 times to gather confidence data
    for i in {1..20}; do
      dotnet test --filter "Category=new"
    done
```

### Use Xping Data in Pipelines

Check confidence scores before merging:
```yaml
- name: Check Test Reliability
  run: |
    # Query Xping API for test confidence scores
    # Fail if any test has confidence < 0.60
```

### Separate Flaky Test Runs

Run known-flaky tests separately:
```yaml
- name: Reliable Tests
  run: dotnet test --filter "Category!=flaky"

- name: Flaky Tests (Informational)
  run: dotnet test --filter "Category=flaky"
  continue-on-error: true
```

---

## Team Culture and Shared Responsibility

### Make Reliability Everyone's Job

- **Developers**: Write reliable tests from the start
- **Reviewers**: Check Xping scores during PR review
- **QA**: Monitor dashboard and report degradations
- **Team Leads**: Prioritize reliability in sprint planning

### Celebrate Improvements

Recognize reliability wins:
- Tests fixed and maintaining high scores
- Decrease in flaky test count
- Improvement in team's average confidence score
- Quick turnaround on fixing degradations

### Regular Reviews

Include reliability in regular ceremonies:
- **Daily Standup**: Mention any critical flaky tests
- **Sprint Planning**: Allocate time for fixing flaky tests
- **Sprint Review**: Show reliability metrics trends
- **Retrospectives**: Discuss what caused flakiness and how to prevent it

---

## Common Questions

### Q: Why is my passing test showing a low confidence score?

**A:** Confidence isn't just about pass/fail. A test that passes 100% of the time but requires retries, has wildly varying execution times, or only passes in certain environments will have a lower confidence score. Xping is telling you the test is unreliable even if it technically passes eventually.

### Q: How long does it take to get a confidence score?

**A:** You need at least 10 test executions before a confidence score is calculated. After 10 runs, you'll see a score with "Low Confidence" level. The score becomes more reliable as you accumulate more executions:
- 25+ runs: Medium Confidence
- 50+ runs: High Confidence
- 100+ runs: Very High Confidence

### Q: My test has a low score but I know why it's flaky. Can I override it?

**A:** No, confidence scores are calculated objectively based on actual behavior. However, you can:
- Add tags/categories to provide context (e.g., `[Category("external-service")]`)
- Use filters to exclude these tests from your main view
- Create separate test suites for inherently unstable tests (like external service health checks)
- Focus on improving the test rather than overriding the score

### Q: What's the difference between Success Rate and Confidence Score?

**A:**
- **Success Rate**: Simple percentage of passed executions (e.g., 85 passed out of 100 = 85%)
- **Confidence Score**: Holistic reliability assessment that considers pass rate, execution consistency, retry behavior, environment consistency, and more

A test with 100% success rate can have a low confidence score if it frequently needs retries or shows inconsistent behavior.

### Q: Should I disable all tests below 0.60?

**A:** Not necessarily. First, understand **why** the score is low:
- Check the confidence level - is it based on enough data?
- Review the factor breakdown - which specific factors are problematic?
- Look at the trend - is it improving?

Then decide:
- If it's providing value and failures are legitimate → Fix the flakiness
- If it's flaky and nobody trusts it → Fix or disable
- If it's testing something inherently unstable → Reconsider test design or move to a separate suite

### Q: How do I share a specific test's data with my team?

**A:** Each test has a unique URL (e.g., `/tests/{test-id}`). Copy and share this URL in your issue tracker, pull request, or team chat. Anyone with access to the dashboard can view the full test details.

### Q: Can I get notifications when a test's confidence drops?

**A:** The current implementation calculates scores and flags low-confidence tests in the Flaky Tests tab. Check your dashboard regularly, or consider implementing your own alerting using the Xping API. Future versions may include built-in alerting capabilities.

### Q: Why does a test show "Insufficient Data"?

**A:** The test hasn't reached the minimum 10 executions required for confidence score calculation. Keep running your tests, and once it hits 10 executions, you'll see a confidence score appear.

### Q: The confidence score seems wrong. What should I do?

**A:**
1. Check the **Confidence Level** - if it's Low, the score may change significantly as more data is collected
2. Review the **Factor Breakdown** to see which factors are affecting the score
3. Verify the **Sample Size** - small samples can be less accurate
4. Look at the **Execution History** to verify the data matches reality

If something still seems incorrect, this may indicate an edge case worth investigating further.

---

## See Also

- [Identifying Flaky Tests](./identifying-flaky-tests.md) - Find unreliable tests
- [Fixing Flaky Tests](./fixing-flaky-tests.md) - Strategies for addressing issues
- [Monitoring Test Health](./monitoring-test-health.md) - Regular review workflows
- [Common Flaky Patterns](./common-flaky-patterns.md) - Pattern catalog
