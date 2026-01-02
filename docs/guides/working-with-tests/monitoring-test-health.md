# Monitoring Test Health

Regular monitoring of test reliability helps you maintain a trustworthy test suite. This guide provides practical workflows for daily, weekly, and monthly health checks using the Xping dashboard.

---

## Daily Quick Check (2-3 minutes)

Start your day with a quick reliability check:

### 1. Check the Flaky Tests Tab

- Navigate to the **Flaky Tests** tab in the Xping dashboard
- Look for any new entries that appeared since yesterday
- These are tests with confidence scores below 0.60

### 2. Review Declining Scores

- Sort by trend indicator
- Look for tests with red arrows (↘️ ⬇️ indicating degradation)
- These tests were previously reliable but are getting worse

### 3. Acknowledge Critical Issues

- Identify tests with scores below 0.40 (Highly Unreliable)
- These are urgent and should be addressed immediately
- Consider temporarily disabling them if they're blocking CI

### Quick Action Checklist

- [ ] Checked Flaky Tests tab for new entries
- [ ] Noted any tests with declining trends
- [ ] Created tickets for critical issues (< 0.40)
- [ ] Communicated urgent issues to the team

---

## Weekly Review (15-20 minutes)

Dedicate time each week to assess test suite health:

### 1. Review Low-Confidence Tests

Navigate to the **Tests** tab and:
- Sort by confidence score (ascending) to see worst tests first
- Review the bottom 10-20 tests
- Filter to your team's test projects if working in a monorepo

### 2. Identify Top Priorities

Select the top 3-5 lowest-scoring tests to focus on. Prioritize based on:

**Impact Factors:**
- **Criticality**: Does this test cover a critical user journey?
- **Frequency**: How often does it run (every PR vs. nightly)?
- **Disruption**: How much time does the team waste investigating it?

**Improvement Potential:**
- **Trend**: Is it degrading (⬇️) or stable but low (➡️)?
- **Confidence Level**: High confidence in the score means clear evidence
- **Pattern**: Does Xping's Flaky Detection Analysis provide clear recommendations?

### 3. Create Action Items

For each prioritized test:
1. Create a ticket with details from Xping (confidence breakdown, patterns detected)
2. Assign to the team member who knows that code best
3. Include link to the test detail page in Xping
4. Set a reasonable deadline based on priority

### 4. Check Previous Fixes

Review tests you fixed in previous weeks:
- Are confidence scores improving (↗️ ⬆️)?
- Has the pass rate stabilized?
- Have problematic factors (retry, environment consistency) improved?

### Weekly Review Checklist

- [ ] Sorted tests by confidence score
- [ ] Identified top 3-5 priorities
- [ ] Created tickets with Xping details
- [ ] Reviewed previous fixes for improvement
- [ ] Shared findings with the team

---

## Monthly Health Assessment (30-45 minutes)

Once a month, take a broader view of test suite health:

### 1. Generate Overview Metrics

Create a snapshot of your test suite:

**Distribution Analysis:**
- How many tests in each category?
  - Highly Reliable (0.90-1.00): ___ tests
  - Reliable (0.75-0.89): ___ tests
  - Moderately Reliable (0.60-0.74): ___ tests
  - Unreliable (0.40-0.59): ___ tests
  - Highly Unreliable (0.00-0.39): ___ tests

**Trend Analysis:**
- How has the distribution changed from last month?
- What percentage of tests are in the Flaky Tests tab?
- Overall average confidence score for the test suite

**Velocity Tracking:**
- How many flaky tests were fixed this month?
- How many new flaky tests appeared?
- Net change in test reliability

### 2. Identify Systemic Issues

Look for patterns across multiple tests:

**Common Failure Patterns:**
- Are multiple tests showing the same flakiness pattern (e.g., all environment consistency issues)?
- Is there a spike in a particular factor across many tests?

**Environmental Issues:**
- Are environment-specific problems widespread (e.g., all Linux CI tests failing)?
- Do you need infrastructure improvements?

**Organizational Patterns:**
- Is there a particular project/namespace with many flaky tests?
- Does one team or component have reliability issues?
- Are new tests more reliable than old ones?

### 3. Celebrate Wins

Recognition drives improvement:

**Individual Test Improvements:**
- Tests that moved from Unreliable to Reliable
- Tests with significant improvement trends (20+ point score increase)
- Tests that were fixed and maintained high scores

**Team Achievements:**
- Decrease in overall flaky test count
- Improvement in average suite confidence score
- Reduction in CI pipeline failures due to flaky tests

**Share Success Stories:**
- Document what was learned from fixing difficult flaky tests
- Highlight teams with the best reliability improvements
- Share patterns and solutions across teams

### Monthly Assessment Checklist

- [ ] Generated distribution metrics
- [ ] Compared to previous month
- [ ] Identified systemic issues
- [ ] Created improvement initiatives
- [ ] Celebrated and shared wins
- [ ] Updated team on overall health trend

---

## Setting Up Regular Review Cycles

### Scheduling

**Daily Check:**
- First thing in the morning or before standup
- Takes 2-3 minutes
- Can be done by on-call developer or team lead

**Weekly Review:**
- Same day/time each week (e.g., Friday afternoon)
- Takes 15-20 minutes
- Should involve whole team or rotate responsibility

**Monthly Assessment:**
- First week of each month
- Takes 30-45 minutes
- Should include team leads and stakeholders

### Automation Opportunities

Consider automating notifications:
- Alert when a test's confidence drops below threshold
- Daily summary of new flaky tests
- Weekly report of top 10 problem tests
- Monthly trend report

> **Note**: Xping can integrate with your notification systems to provide automated alerts.

---

## Tracking Improvement Over Time

### Create a Health Dashboard

Track these metrics month-over-month:
- Average confidence score for the suite
- Percentage of tests in each reliability category
- Number of tests in Flaky Tests tab
- Time wasted on flaky test failures (if measurable)

### Set Team Goals

Example goals:
- Reduce flaky test count by 50% in Q1
- Achieve 90%+ of tests with confidence > 0.75
- Fix all critical flaky tests (< 0.40) within 1 week of detection
- Prevent new flaky tests (block PRs with low scores)

### Review Progress

In team retrospectives:
- Discuss reliability improvements
- Share learnings from fixes
- Adjust goals based on progress
- Identify remaining challenges

---

## See Also

- [Identifying Flaky Tests](./identifying-flaky-tests.md) - How to find problematic tests
- [Fixing Flaky Tests](./fixing-flaky-tests.md) - Strategies for addressing issues
- [Best Practices](./best-practices.md) - Team-level practices for reliability
- [Navigating the Dashboard](../getting-started/navigating-the-dashboard.md) - Using the dashboard effectively
