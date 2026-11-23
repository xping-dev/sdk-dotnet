---
uid: cloud-session-reliability-score
title: Session reliability score
---

# Session reliability score

The **session reliability score** is one number from 0.00 to 1.00 for one test run. It combines the
[confidence scores](confidence-scores.md) of the tests that ran in it. It answers the question:
"How much can I trust this run as a whole?"

---

## The formula

```text
session reliability = 0.6 × mean + 0.4 × weakest
```

- **mean** is the average confidence score of the tests in the run. A test with more evidence counts
  more: a `Robust` test counts three times as much as a `Sparse` test, and an `Adequate` test twice as
  much.
- **weakest** is the lowest confidence score of any test in the run.

The weights 0.6 and 0.4 are fixed. They are the same for every project, so you can compare the
scores of different projects. You cannot change them.

The weakest test always moves the score. A run with one broken test never looks as healthy as a run
with none, however large the suite.

Tests without a confidence score are left out of both terms. A run in which no test has a score has
no session score.

The run's page shows the calculation, for example:

```text
0.60 × 0.94 mean + 0.40 × 0.20 weakest = 0.64
```

The total is the stored score. The two rounded values next to it do not always add up to it exactly.

---

## Example

A run has 50 tests that score 0.95 and 1 test that scores 0.20. All 51 tests have the same evidence
level.

| Term | Value |
|---|---|
| mean | 0.935 |
| weakest | 0.20 |
| **session reliability** | **0.6 × 0.935 + 0.4 × 0.20 = 0.64** |

When every test has the same score, the mean and the weakest are equal, and the session score is
that score.

---

## What the session score reads

The session score reads each test's current confidence score. It is not a separate measurement of
the run. Two results follow:

- **Pull-request runs and default-branch runs use the same formula.** The confidence score already
  counts pull-request runs less than default-branch runs. The session score adds no weighting of its
  own.
- **Two session scores are not always comparable.** Two runs can contain different tests. A
  pull-request run often runs a part of the suite, and the weakest term reacts strongly to which tests
  ran. Xping Cloud shows no trend of session scores for this reason.

The run's page compares the score with one earlier run from the last 30 days. That run has the same
project, environment, framework, and CI or local origin. It is the previous run of the same branch.
When a pull-request run has none, Xping Cloud compares it with the branch the pull request targets.
When no earlier run matches, the page shows no comparison.

---

## Evidence

A session also has an evidence level: `Sparse`, `Adequate` or `Robust`. It is the lower of two
measurements:

- the share of the run's tests that have a confidence score: below 30% is `Sparse`, 30% to below
  70% is `Adequate`, and 70% or more is `Robust`;
- the weakest evidence level of the tests that have one.

A run with full coverage can still have `Sparse` evidence when one of its tests has `Sparse`
evidence.

---

## What the session score does not change

The session score does not change any test's confidence score. It does not change how many of a
project's tests fall into each [score category](confidence-scores.md#score-categories).

---

## See also

- [Confidence scores](confidence-scores.md)
- [Reading the PR comment](reading-the-pr-comment.md#reliability): the PR comment compares the
  session score with your target branch.
