---
uid: cloud-confidence-scores
title: Confidence scores
---

# Confidence scores

This page explains how Xping Cloud scores each test, and how to read the score. It is for anyone
who reads scores in Xping Cloud, in the CLI or in a pull request comment.

A **confidence score** is Xping Cloud's estimate of how much you can trust a test's result. It is a
number from 0.00 to 1.00. A high score means the test gives the same answer on the same code. A low
score means its result carries less signal.

Xping Cloud scores each test from its uploaded runs. The score is not a flaky verdict. See
[Score and flaky are independent](#score-and-flaky-are-independent).

These values belong to the current scoring version and can change.

---

## Score categories

Each score falls into one **score category**:

| Score | Category |
|---|---|
| 0.90 to 1.00 | Highly Reliable |
| 0.75 to 0.89 | Reliable |
| 0.60 to 0.74 | Moderately Reliable |
| 0.40 to 0.59 | Unreliable |
| 0.00 to 0.39 | Highly Unreliable |
| no score | Insufficient Data |

A test with a score in a lower category needs more of your attention when it fails, because its
failures are harder to tell apart from noise.

---

## When a test gets a score

A test gets a score when it has enough recent evidence. Xping Cloud does not count runs for this.
It measures the **effective sample size** (ESS): a weighted count of the test's runs.

- A run in CI on your default branch, outside a pull request, counts in full.
- A run in a pull request, or on another branch, counts much less. Its code can still change.
- A run outside CI, or a run with no commit, counts more than a pull-request run but less than a
  default-branch run.

A test needs an ESS of at least 10 within the last two weeks. Below that, its category is
**Insufficient Data** and it has no score. Ten default-branch runs are enough. Ten pull-request runs
are not. The age of a run within the two weeks does not change the ESS.

The score itself reads about the last five weeks of runs. In the factors, recent runs count more than
older runs: a run's weight halves about every week.

> [!NOTE]
> The SDK records the branch, the commit and the pull request when your CI provides them. See
> [Data Xping collects](../configuration/collected-data.md). A CI run that Xping cannot place on a
> branch counts as a run outside CI.

---

## Evidence

Next to the score, Xping Cloud shows how much evidence supports it. The **evidence level** comes from
the ESS:

| Evidence level | ESS |
|---|---|
| Sparse | below 15 |
| Adequate | 15 to below 40 |
| Robust | 40 or more |

A test without a score shows **No Data**.

Treat a score with `Sparse` evidence as a first reading. It can still move a lot as more runs arrive.
A score with `Robust` evidence moves slowly.

The score also has a **range**: the values the score is likely to have, given the runs so far. Few
runs give a wide range. Many runs give a narrow one.

---

## How the score is calculated

The score has six inputs. Each one is a **factor**. One factor sets the level of the score. Five
factors can only lower it.

```text
score = level × (discount from each of the five other factors)
```

### The six factors

| Factor | Role | What it measures |
|---|---|---|
| Pass Rate | level | How often the test passes. Xping Cloud pulls the rate toward what a typical test in your project does, so a test with four passing runs does not score 1.00. |
| Execution Stability | discount | How much the test's duration varies from run to run. |
| Environment Consistency | discount | The gap in pass rate between the test's environments. |
| Concurrency Sensitivity | discount | The gap in pass rate between runs with more and fewer tests running at the same time. |
| Retry Behavior | discount | How often a run needed a retry to pass. |
| Failure Pattern | discount | Whether the failures share one error, and whether that error is transient. |

Each discount factor has a maximum effect. Execution Stability can lower the score the most. Then
come Environment Consistency, Concurrency Sensitivity, and Retry Behavior and Failure Pattern, which
have the same, smallest effect. No discount factor can bring a score to zero.

Environment Consistency compares the environment names that the SDK records, for example `Staging`
and `Production`. You set the name with the
[`Environment` option](../configuration/configuration-reference.md#environment). It needs runs in at
least two environments.

Environment Consistency and Concurrency Sensitivity score the gap they *estimate*. A small gap seen in
few runs counts for less than the same gap seen in many runs.

### A factor that cannot be measured

Most tests have too little data for some factors. For example, a test that runs in one environment
has no environment gap to measure. A factor that cannot be measured does not lower the score.

When Environment Consistency or Concurrency Sensitivity could not be measured for want of evidence,
Xping Cloud also gives the lowest value the score could have if that factor had been measured. It
says, for example:
`Environment Consistency could not be measured. Allowing for what it might have found, the score could be as low as 0.73.`
A factor that does not apply to the test, such as Retry Behavior for a test with no retries, does not
count here.

### Why this score

The score breakdown, **Why this score**, shows where the score lost value:

```text
1 − score = (1 − level) + the loss from each discount factor
```

The first line is the loss from the pass rate. The other lines show what each factor took off. The
factor with the largest loss is the first place to look.

### Scores per environment

Each environment of a test also gets its own score. Xping Cloud pulls an environment's pass rate
toward the same test's runs in its other environments, not toward the project. A test that runs in
only one environment has one score.

---

## Trends

Xping Cloud fits a straight line through each test's recent scores and reads its slope per day:

- **Significant Improvement** and **Minor Improvement**: the scores rise.
- **Stable**: the slope is small, or the scores move too irregularly for a line to describe them.
- **Minor Degradation** and **Significant Degradation**: the scores fall.

A test needs at least 5 scores before it shows a trend. Until then, it has no trend.

---

## Score and flaky are independent

A **flaky** test passes and fails without a change to its code. Xping Cloud's flaky detection
decides this as its own verdict. It does not read the score to decide.

Cloud examines tests that score below Reliable, are already flagged, or recovered on a retry.

This has two results:

- **A low score does not make a test flaky.** A test that fails every run because of a real bug has a
  low score. It is not flaky, because its result does not change.
- **A high score does not clear a test.** The score reads each run by its last attempt. A test that
  fails often but passes on a retry ends most runs green and can score Reliable. Flaky detection
  reads the first attempt of each run, so it can still flag that test.

Read the score and the flaky verdict side by side. See
[Finding flaky tests](../flaky-tests/finding-flaky-tests.md) for how detection works.

---

## Fixed-window values

The score, the factor values, the ESS and the evidence level each describe a fixed window of recent
runs. Xping Cloud calculates them again on a schedule, and after an upload that contains the test.
Within an hour of the last calculation, an upload starts another one only when it has a failure or a
retry.

You cannot add or average these values over a date range you choose. For example, the mean of a
test's daily scores over a month is not a monthly score. To compare two points in time, compare two
scores.

---

## Common questions

### Why does a test that passes every run have a low score?

The pass rate sets only the level. A test that passes, but needs retries or varies a lot in
duration, loses part of its score to those factors. Open **Why this score** to see
which factor lowered it.

A test with few runs also has a level below its raw pass rate. Xping Cloud pulls the pass rate toward
your project's typical test until the test has more runs.

### Why does a test show Insufficient Data?

Its ESS in the last two weeks is below 10. This happens when the test has run only a few times, when it ran mostly in
pull requests, or when it has not run for a while. Run it on your default branch in CI to give it a
score.

### Does a low score mean a test is flaky?

No. No score threshold makes a test flaky. Check the flaky verdict. See
[Score and flaky are independent](#score-and-flaky-are-independent).

### Can I change a test's score?

No. Xping Cloud calculates the score from the test's runs. To raise it, fix what **Why this score**
shows.

### Can Xping Cloud notify me when a score drops?

No. Score notifications are not available.

### The score looks wrong. What do I check?

1. The evidence level. A `Sparse` score can move a lot.
2. **Why this score**, to see which factor lowered it.
3. The test's run history, to check that the runs match what you expect.
4. Whether the runs come from your default branch. Pull-request runs count less.

---

## See also

- [Session reliability score](session-reliability-score.md): how Xping Cloud combines the scores of
  the tests in one run.
- [Reading the PR comment](reading-the-pr-comment.md)
- [Finding flaky tests](../flaky-tests/finding-flaky-tests.md)
