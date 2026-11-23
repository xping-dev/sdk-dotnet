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
- [ ] Link to Xping Cloud showing test results
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
- Show Xping Cloud during standups
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

> **Related**: For fixing strategies, see [Fixing Flaky Tests](patterns-and-fixes.md).

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
- **QA**: Monitor Xping Cloud and report degradations
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

Questions about confidence scores are answered in [Confidence scores](../cloud/confidence-scores.md#common-questions).

---

## See Also

- [Identifying Flaky Tests](finding-flaky-tests.md) - Find unreliable tests
- [Fixing Flaky Tests](patterns-and-fixes.md) - Strategies for addressing issues
- [Monitoring Test Health](monitoring-test-health.md) - Regular review workflows
- [Common Flaky Patterns](common-flaky-patterns.md) - Pattern catalog
