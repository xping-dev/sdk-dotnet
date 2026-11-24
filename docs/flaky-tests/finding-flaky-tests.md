---
uid: flaky-tests-finding-flaky-tests
title: Finding flaky tests
---

# Finding flaky tests

A **flaky** test passes and fails on the same code. It costs time, because each failure makes you
check whether the code is broken, and it hides real failures among the false ones.

You can find flaky tests in two places:

- **On your machine**, with `xping report`. It reads the runs that the SDK keeps on your machine. It
  needs no account.
- **In Xping Cloud**, which reads the runs that every machine and CI job uploads. It decides whether
  each test is flaky, and why.

To fix a flaky test, see [Flaky patterns and fixes](patterns-and-fixes.md).

---

## On your machine

Run your tests a few times, then run `xping report`. The report needs at least 5 runs that contain
the test. See [Get started locally](../getting-started/local-first.md).

These [finding kinds](../cli/reading-the-report.md#finding-kinds) point at a flaky test:

| Printed as | What the report observed |
|---|---|
| `flaky` | The test both passes and fails, or fails in different ways. |
| `masked by retry` | The test failed and then passed on a retry, so the build stayed green. |
| `deeper retries` | The test needs more attempts to pass than it did before. |
| `out of retries` | The retries ran out, and the test failed the run. |
| `concurrency` | The test fails more, or less, when more tests run at the same time. |
| `time sensitive` | The failures gather at one time of day, at weekends, or on one side of a daylight saving change. |

Two kinds describe a test that fails, but not a flaky one:

- `always failing`: the test fails almost every run, mostly in one way. It is broken, not flaky.
- `timing out`: the test mostly hangs until its timeout.

A test can have more than one finding. To see all of a test's findings and runs, use
`xping report --test <name>`. See [Test history](../cli/reading-the-report.md#test-history).

### Fail a CI step on findings

`xping report --fail-on <severity>` exits with code `1` when a finding reaches that severity:
`high`, `medium` or `low`. Add it as a step after the tests. It reads only the local findings on
that CI agent. See [Fail the build on findings](../getting-started/ci-cd-setup.md#fail-the-build-on-findings).

---

## In Xping Cloud

Xping Cloud decides for each test whether it is flaky. This decision is the **flaky verdict**. It is
separate from the [confidence score](../cloud/confidence-scores.md): no score makes a test flaky.

### Which tests Cloud examines

Cloud examines tests that score below Reliable, are already flagged, or recovered on a retry.

A test that recovered on a retry is examined even when it scores well. The confidence score reads
each run by its last attempt, so a retried test can end most runs green and score Reliable. Flaky
detection reads the first attempt of each run, so it sees the failures.

See [Score and flaky are independent](../cloud/confidence-scores.md#score-and-flaky-are-independent).

### When Cloud can decide

Xping Cloud reads the test's runs from the last 14 days. It reads the first attempt of each run, and
leaves out runs with no result, such as skipped runs.

- It needs at least 10 runs.
- The runs must show clearly whether the test is **broken** (passes 10% of runs or less),
  **intermittent** (between 10% and 95%) or **stable** (95% or more). With few runs, or with a pass
  rate close to 10% or 95%, the runs do not show this yet. For example, ten passing runs in a row are
  not enough to call a test stable. Sixteen are.

When the runs are enough, several signals vote on the verdict: the pass rate, the variation in
duration, recoveries on a retry, the gap between environments, the exception types of the failures, and
whether the
result follows the test that ran before it. When the votes do not agree enough, Cloud gives no
verdict.

A test that failed from one point on and kept failing is not flaky. Its code changed at that point.
The same is true of a test that fails on one branch only. Cloud keeps a flaky verdict in these cases
only when the runs since the change still pass and fail, or when one commit both passed and failed.

When Cloud cannot decide, the test keeps its earlier verdict.

### Categories

A flaky test gets one **category**: the most likely cause.

| Category | What Cloud saw |
|---|---|
| Timing | The duration varies a lot, and runs time out or fail with a timeout or cancellation exception. |
| Network | Failures with network exceptions, such as `SocketException` or `HttpRequestException`. |
| External dependency | Failures with database or file-system exceptions, such as `DbException` or `IOException`. |
| Environment specific | The test passes in one environment much more often than in another. |
| Order dependency | The result follows the test that ran just before it. |
| Concurrency | The pass rate differs between runs with more and fewer tests running at the same time. |
| Unknown | No other category matched. |

Environment specific compares the environment names that the SDK records. An environment name is the
deployment that the tests run against, such as `Staging`, set with the
[`Environment` option](../configuration/configuration-reference.md#environment). Use one name for
all runs against the same deployment. A new name splits the test's history.

### Severity

A flaky test also gets a **severity**, from how often its first attempt fails:

| Severity | Fails |
|---|---|
| `minor` | up to about 10% of runs |
| `moderate` | about 10% to 30% of runs |
| `major` | about 30% to 50% of runs |
| `critical` | more than about 50% of runs |

The rates are approximate. Cloud moves a test to another severity only when the runs show clearly
that its failure rate crossed an edge.

### What Cloud reports about a flaky test

For a flaky test, Xping Cloud gives:

- the category and the severity;
- the root causes it found, and how far the signals agreed;
- the environments the test ran in, and which of them use retries;
- recommended actions for the category;
- on the Pro, Team and Enterprise plans, an AI summary of why the test is flaky. See
  [Plans and retention](../cloud/plans-and-retention.md#features-that-need-a-plan).

### When the verdict changes

Flaky detection runs after Xping Cloud scores an upload, for each test in it that Cloud examines. It
does not run on a schedule.

- A test's first verdict applies at once.
- After that, a test becomes flaky, or stops being flaky, when two detections in a row agree. One
  detection alone does not change the verdict.
- A detection that cannot decide does not change the verdict.
- A flagged test that stops running stays flagged, because no new detection runs for it.

Xping Cloud lists the tests that became flaky or recovered, with the time of the change.

### Needs attention

Xping Cloud lists a test as **needs attention** when it is flaky, or when its score is Moderately
Reliable or lower. A flaky test with a high score needs attention. A test with a low score needs
attention even when it is not flaky.

---

## Cloud's verdict in the CLI

When the CLI has a Xping Cloud credential, for example after `xping login`, `xping report` adds Xping Cloud's verdict to the local report:

- A `flaky` or `always failing` finding takes Cloud's verdict as its label, such as
  `cloud known-flaky` or `cloud broken`.
- Every other finding keeps its local label, and Cloud's flaky state goes on a `cloud:` line, such as
  `flaky (cause: timing, major)`.
- A `FLAGGED IN CLOUD` section lists the tests that Cloud flags and that have no local finding.

Cloud's verdict never changes the local findings, `--fail-on` or the exit code. See
[Xping Cloud data in the CLI](../cli/cloud-data.md).

---

## See also

- [Flaky patterns and fixes](patterns-and-fixes.md)
- [Reading the report](../cli/reading-the-report.md)
- [Confidence scores](../cloud/confidence-scores.md)
- [Xping Cloud data in the CLI](../cli/cloud-data.md)
