# Reading the Xping Comment

When your CI runs tests on a pull request, Xping posts **one comment** on that pull request and keeps it up to date. Every new commit, every new test run and every finished analysis updates the same comment, so you never have to scroll through a stack of bot messages.

The comment answers three questions, in this order:

1. Did the tests pass?
2. If a test failed, is it likely to be caused by this pull request?
3. Did the change make the test suite more or less reliable?

PR comments are available on the **Team** and **Enterprise** plans, for GitHub, GitLab and Azure DevOps.

---

## The Comment at a Glance

From top to bottom:

| Part | What it tells you |
|------|-------------------|
| **Headline** | The verdict for the whole run, in one line |
| **Counts line** | Tests, passed, failed, timed out, skipped, passed after retry, duration, projects and commit |
| **Status line** | What is still being analysed; it is gone once the analysis is finished |
| **Failed tests** | One card per failed test, with a verdict, the reason and a next step |
| **Became flaky during this PR** | Tests that started to pass and fail on the same code while this PR ran |
| **Reliability** | How the suite's reliability score compares with the base branch |
| **Projects** (collapsed) | A table per project, tests whose reliability dropped, and earlier commits |
| **Footer** | Links to Xping, this page, and the time of the last update |

A part that has nothing to say is left out.

---

## The Headline

The headline starts with an emoji and says what happened:

| Headline | Meaning |
|----------|---------|
| ✅ `all 1,248 tests passed` | Every test passed. `· 3 passed after retry` is added when some needed a retry. |
| ⚠️ `all 1,248 tests passed · 1 test became flaky during this PR` | Nothing failed, but a test started to behave inconsistently. |
| ❌ `27 of 1,248 tests failed · 24 likely caused by this PR · 3 pre-existing` | At least one failure points at this PR, or Xping cannot tell yet. |
| ⚠️ `1 of 1,248 tests failed · pre-existing` | Tests failed, but none of the failures looks introduced by this PR. |
| ⚠️ `… · analysis unavailable` | The test results are complete, but Xping could not finish the reliability and flaky analysis. |
| ⚠️ `run did not finish` | The test run stopped sending results. Counts are partial. |

Failures are counted in three groups:

- **likely caused by this PR**: start here.
- **unclear**: Xping does not have enough to say either way.
- **pre-existing**: the failure is not introduced by this PR. It still needs a fix, just not necessarily in this PR.

---

## The Status Line

Xping posts the comment as soon as the test results arrive, then finishes the analysis in the background:

- ⏳ *Reliability and flaky analysis pending*: the results are in, scores are being computed.
- ⏳ *2 of 3 projects reported*: some projects of this repository have not sent results for this commit yet.
- ⏳ *Flaky analysis pending*: scores are in, flaky detection is still running.

The comment updates itself when each step finishes. You do not need to reload or re-run anything.

---

## Failed Tests

The first five failures are shown as full cards. Further failures are listed in a collapsed **more failed tests** section, and a very long list ends with a link to all failures in Xping.

Failures are sorted so that what you must act on comes first: *likely caused by this PR*, then *unclear*, then *pre-existing*.

A full card looks like this:

> ⚠️ **OrderServiceTests.Submit_SendsConfirmation** · Shop.Api.Tests<br>
> **Unstable on `main`** · `main` failed this test 1 of its last 10 runs (last on 2026-10-29); its newest run passed<br>
> `AssertionException` · `Expected 3 recorded orders but found 2` · 41s (typically 12s)<br>
> Next: rerun the job; if it fails again with the same error, compare with the failing run on `main` → compare<br>
> ✨ The confirmation is sent before the order is saved, so the assertion can run before the third order exists.

Line by line:

1. **The test and its project.** The name links to the test's [failure page](#the-failure-page). The project is left out when the repository has only one.
2. **The verdict and the reason for it.** See [Verdicts](#verdicts) below.
3. **What happened:** the exception type, the first line of the error message, where it failed (for example `in test setup`), `failed on all 3 attempts` when retries did not help, `transient error` or `network error` when the SDK recognised one, and the duration. *(typically 12s)* appears when this run took at least twice as long, or half as long, as usual.
4. **Next:** a concrete first step, with a link to the part of the failure page that helps.
5. **✨ line:** an AI explanation, when there is one. See [AI Explanations](#ai-explanations).

The error message is redacted before it is shown: passwords, tokens, connection strings, email addresses and similar values are replaced with `<redacted>`.

### Verdicts

Each failure gets exactly one verdict. Xping decides it from the test's history on your default branch (shown by its name, usually `main`), on other branches, earlier commits of this PR and flaky detection.

| Verdict | Group | What it means | What to do |
|---------|-------|---------------|------------|
| **Likely caused by this PR** | caused | The test passes on `main` (every one of its recent runs) and is not failing anywhere else. *failing since commit … in this PR* says when it started. | Look at the changes in this PR that affect the test, and compare with the last passing run on `main`. |
| **New test, failing** | caused | The test first appeared in this PR and has never passed in any run Xping recorded. | Run the test locally. |
| **Became flaky during this PR** | unclear | Flaky detection found the test passing and failing on the same code while this PR ran. | Check whether this PR changed the test's timing, data or dependencies. |
| **Not enough history** | unclear | `main` has fewer than 3 runs of this test within your plan's data retention window, so there is nothing to compare with. | Read the error. Xping cannot say yet whether `main` has the same failure. |
| **Known flaky test** | pre-existing | The test was detected as flaky before this PR started. The reason shows since when, and the kind of flakiness (for example timing) and its severity. | Rerun the job. If it fails again with a *different* error, treat it as a real failure. |
| **Also failing on `main`** | pre-existing | The newest run on `main` fails too. *with the same error* is added when the error message matches. | It is not caused by this PR; it needs a fix on `main`. |
| **Fails on other branches too** | pre-existing | Other branches had this test fail in the last 7 days. | The cause is probably shared (environment, data or a dependency). Compare the failures. |
| **Unstable on `main`** | pre-existing | `main` fails this test now and then, but its newest run passed. | Rerun the job. If it fails again with the same error, compare with the failing run on `main`. |

The checks run in the order of the table rows from *Known flaky test* down, and the first one that matches decides. For example, a test that is known to be flaky gets *Known flaky test* even if `main` also fails it. Timeouts count as failures throughout.

**Pre-existing does not mean ignore.** It means only that this PR is not the likely cause. A pre-existing failure can still block your merge, and the next step tells you where the fix belongs.

### Test names

The comment shows the class and method name, with its arguments for parameterised tests (`ParserTests.Parse_Rejects("abc", +2 more)`). The namespace is left out; the project name next to it gives the context, and the failure page shows the full name. Long argument lists drop arguments from the end, never the first one, so two cases of the same test stay apart.

---

## AI Explanations

On the **Team** and **Enterprise** plans, the first five failures of each commit get a short AI explanation, marked with ✨. It reads the same facts as the verdict, plus the error, and suggests a likely cause.

- The explanation is written once per failure and commit. When the same test fails with the same error and verdict on a later commit, the earlier explanation is reused.
- The comment never waits for it. The ✨ lines appear a little after the rest of the comment, as an update.
- It never contradicts the verdict. If it is not sure, it says so.
- It is a suggestion, not a diagnosis. The verdict, the reason and the next step are worked out from your test history, without AI; the ✨ line is the only AI-written part of the comment.
- The full answer (the summary, the likely cause and a first step, with the model name) is on the failure page.

The error message and stack trace are sent to the AI model only when your workspace allows it: **Settings → Integrations → Share error text with AI analysis**. Without it, the explanation uses the test's facts and history only. Either way the text is redacted first.

Each explanation counts toward your workspace's monthly AI analysis limit, shared with confidence analyses and flaky summaries. When the limit is reached, failures are shown without a ✨ line until the next month.

---

## Became Flaky During This PR

This list names tests that flaky detection flagged while this PR ran but that did **not** fail at the head commit (for example, they passed after a retry). Each row shows the test, its project, the kind of flakiness and its severity. A flagged test that did fail is shown in **Failed tests** instead, with the verdict *Became flaky during this PR*.

See [Identifying Flaky Tests](../working-with-tests/identifying-flaky-tests.md) for how detection works.

---

## Reliability

The reliability line compares each project's [session reliability score](../getting-started/session-reliability-score.md) at the head commit with the newest run of your PR's target branch (shown by its name, for example `main`) in the last 30 days:

> **Reliability** · Reliable 0.86 · −0.03 vs `main` (2026-11-08)

- The category (*Highly Reliable*, *Reliable*, *Moderately Reliable*, *Unreliable* or *Highly Unreliable*) comes from the score.
- `+0.02` or `−0.03` is the change from the target branch, rounded to two decimals. *no change* means the rounded change is zero.
- With several projects, only those that changed category, or moved by 0.05 or more, get their own row. The others are summed up as *other projects without notable change*, with the range of their scores.
- `*` after a score means it is based on few runs and may change.
- *no run on `main` in the last … days* means there is nothing to compare with yet.

---

## The Projects Section

The collapsed **Projects** section holds the details:

- **A table per project**: tests, failed, skipped, retried, duration, reliability and the change compared with the target branch. `–` means zero or not available; `pending` means the value is still being computed.
- **Tests that dropped a reliability category during this PR**: each test's own rolling score went down a category. This is a change over time, not a comparison between branches.
- **Earlier commits**: the last three commits of this PR, with their failures and reliability, so you can see whether things are getting better.

---

## The Failure Page

Every test name in the comment links to a page in Xping about that test **in this pull request**. It shows:

1. **Verdict**: the same verdict, reason and next step as the comment, the full ✨ answer when there is one, and what the verdict is based on.
2. **This run**: the full redacted error message, the stack trace, every attempt, the duration compared with usual, and the environment.
3. **Timeline**: the test's outcome on each commit of this PR, next to its last runs on `main`.
4. **Compared with the last passing run on `main`**: commit, date, duration and environment side by side, with the differences highlighted.
5. **Related failures**: other branches where this test failed recently, flaky detection details, and the test's current confidence score.

The comment's *compare* and *see the detection* links jump straight to the matching section. When the PR has moved on to a newer commit, the page says so and links to the current one.

---

## Common Questions

**The comment did not update after my last push.**
The comment updates when results for the new commit arrive. If your CI has not run the tests yet, the comment still shows the previous commit (its commit is in the counts line and in the footer).

**Why is a failure "likely caused by this PR" when I did not touch that code?**
The verdict says the test passes on `main` and fails here, and nowhere else. Indirect changes count: a dependency update, a configuration change or a changed test fixture can break a test whose code you did not touch. The failure page's comparison shows what differs from the last passing run.

**Why is there no ✨ line on a failure?**
Only the first five failures of a commit get one, on the Team and Enterprise plans, while the workspace is under its monthly AI limit. It also appears a little later than the rest of the comment.

**Can I turn the comment off?**
Yes. An Owner or Admin can pause comments for the whole workspace (**Settings → Integrations**) or turn them off for one project (the project's settings).

---

## See Also

- [Understanding Confidence Scores](../getting-started/understanding-confidence-scores.md)
- [Session Reliability Score](../getting-started/session-reliability-score.md)
- [Identifying Flaky Tests](../working-with-tests/identifying-flaky-tests.md)
- [Fixing Flaky Tests](../working-with-tests/fixing-flaky-tests.md)
