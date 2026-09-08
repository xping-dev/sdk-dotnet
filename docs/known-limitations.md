# Known Limitations

This document outlines known limitations and edge cases in the Xping SDK. Understanding these constraints will help you make informed decisions when implementing test observability in your projects.

## Framework-Specific Limitations

### NUnit

#### Ignored Tests Are Not Tracked

**Affected Versions**: All versions
**Impact**: Tests marked with `[Ignore]` attribute will not appear in Xping reporting

**Reason**: The `XpingTrackAttribute` uses NUnit's `ITestAction` interface, which only intercepts tests during the execution phase. Tests with the `[Ignore]` attribute are filtered out by NUnit before execution begins, so the tracking hooks (`BeforeTest` and `AfterTest`) are never invoked.

**Note**: Currently, there is no workaround for tracking ignored tests in NUnit. Only tests that actually execute will be tracked by Xping.

---

### MSTest

#### Ignored Tests Are Not Tracked

**Affected Versions**: All versions
**Impact**: Tests marked with `[Ignore]` attribute will not appear in Xping reporting

**Reason**: The `XpingTestBase` class uses `[TestInitialize]` and `[TestCleanup]` lifecycle hooks, which only execute for tests that run. Tests with the `[Ignore]` attribute are skipped by MSTest before these hooks are invoked.

**Note**: Currently, there is no workaround for tracking ignored tests in MSTest. Only tests that actually execute will be tracked by Xping.

#### Retry Attempts Are Numbered By Counting, Not Reported By The Framework

**Affected Versions**: All versions
**Impact**: Two runs of the same test identity within one session are recorded as attempt 1 and attempt 2 of a retry, whether or not a retry is what produced them

**What works**: MSTest re-runs the whole per-test lifecycle for every retried attempt — a retry attribute derived from `TestMethodAttribute` invokes the test method again, which builds a fresh test class instance and runs `[TestInitialize]`, the method, and `[TestCleanup]` once more. Xping therefore records **every attempt** as its own `TestExecution`, each with its own outcome, duration and failure text, all sharing one position in the suite. A test that fails and then passes carries `AttemptNumber = 2` and `PassedOnRetry = true` on its final execution, so the masked failure is visible even though the build is green.

```csharp
// Both attempts recorded: attempt 1 Failed with its error intact, attempt 2 Passed
[Retry(3)]
public void FlakyTest()
{
    // ...
}
```

**Reason for the limitation**: nothing in `TestContext` says which attempt is running, so the adapter derives the number by counting the executions already recorded for the same test fingerprint. A test identity that passed starts a fresh chain, since a retry only ever follows an attempt that did not pass — but two `[DataRow]` rows carrying *identical* values share a fingerprint, and a failing one followed by a repeat of itself is indistinguishable from a retry.

**Where attempts are not tracked**: a retry helper that re-runs only the test method body without going through `ITestMethod.Invoke` bypasses `[TestInitialize]` / `[TestCleanup]` entirely, and Xping sees a single execution for the whole retry loop.

**Note**: `AttemptNumber` is taken from a `RetryAttempt` or `RetryCount` test property, or an attempt marker in the test name (`(Retry 2)`, `[Attempt 2]`), when a retry helper publishes one of those; the counted value is used only as the floor.

---

### xUnit

**No known limitations** for skipped test tracking.

Tests marked with the `Skip` parameter are properly tracked by Xping:

```csharp
[Fact(Skip = "Temporarily disabled - ticket #123")]
public void MyTest()
{
    // This test WILL be tracked with Outcome = Skipped
}
```

The xUnit adapter uses a message sink pattern that intercepts all test lifecycle events, including skipped tests.

#### Retry Attempts Are Only Tracked For Retry Libraries That Expose A Single-Attempt Hook

**Affected Versions**: All versions
**Impact**: With some retry libraries, a test that fails and then passes on retry is recorded as a single passing execution — `AttemptNumber` stays `1`, `PassedOnRetry` stays `false`, the hidden failure is not persisted, and the recorded duration is the library's cumulative time across all attempts

**Reason**: xUnit has no native retry support. Retry libraries implement it by running each attempt against a message bus of their own that *discards* the messages of any attempt they intend to retry, then flushing a single synthesised result. A message sink — which is where Xping observes test execution — sits outside that bus, so the discarded attempts never reach it.

**Where retries are fully tracked**: [xRetry](https://github.com/JoshKeegan/xRetry) exposes `RetryTestCaseRunner.RunAsync` publicly so other xUnit extensions can supply the delegate that runs one attempt. Xping uses it, which places it *inside* the retry loop: every attempt is recorded as its own `TestExecution` with the correct `AttemptNumber`, its own duration, and — for the attempts the retry hid — the failure message, stack trace and exception type. The retry library keeps full control of the retry count, delays, skip-on-exception handling and what the runner is told, so test behavior is unchanged.

**Where they are not**: libraries that inline the retry loop inside their test case's `RunAsync` behind a private delayed or blocking bus — including the retry sample in xUnit's own documentation — expose no such hook. Xping leaves those test cases untouched and records only the attempt the library reports.

```csharp
// Fully tracked: two executions recorded — attempt 1 Failed, attempt 2 Passed
[RetryFact(3)]
public void FlakyTest()
{
    // ...
}
```

**Note**: `AttemptNumber` is also read from a `RetryAttempt` trait or an attempt number in the display name (`(attempt 2)`, `[Retry 2]`) when a library publishes one of those.

---

### Timeouts

Xping records a test the framework killed for overrunning its timeout as `Outcome = Timeout` rather
than `Failed`, and captures the budget the test declared alongside it. Two cases are not observable.

#### A Hang That Takes Down The Test Host Is Not Recorded

Every adapter is driven by a callback the framework raises after a test finishes — MSTest's
`[TestCleanup]`, NUnit's `AfterTest`, xUnit's result message. If a hang brings down the whole test
host, no callback runs and the execution is not recorded at all. Each framework's own `[Timeout]`
handles the ordinary case, which is what Xping observes.

#### NUnit's Blocking `[Timeout]` Is Not Tracked

NUnit's `[Timeout]` on a synchronous test abandons the test thread without invoking
`ITestAction.AfterTest`, so Xping never sees the result and records nothing for that test. Use
`[CancelAfter]` instead, which cancels cooperatively and is tracked normally:

```csharp
[Test, CancelAfter(500)]
public async Task MyTest(CancellationToken cancellationToken)
{
    // Tracked with Outcome = Timeout when it overruns
    await Task.Delay(5000, cancellationToken);
}
```

#### xUnit Applies A Timeout Only To Async Tests

`[Fact(Timeout = ...)]` on a synchronous test is rejected by xUnit itself, which fails the test with
"Tests marked with Timeout are only supported for async tests". Xping records that as `Failed`, not
`Timeout` — it is a misconfigured test, not a hanging one.

## Fixture Failures

Xping records **where** a failing test failed — the test body, per-test setup or teardown, or a
one-time fixture — in `TestExecution.Site`, and names the member in `FailureSiteMember`. When every
failure in a cluster agrees on one member, `xping report` reports a **broken fixture** naming it
instead of N separate failing tests.

What is reachable differs by framework, and none of the three reports a failure site of its own. The
tables below record what was observed by running each package, not what its documentation says.

**Affected Versions**: NUnit 3.14 and 4.2, MSTest 3.7, xUnit 2.9

### What is recorded

| Lifecycle member | NUnit | MSTest | xUnit |
|---|---|---|---|
| Test body | `TestBody` | `TestBody` | `TestBody` |
| Per-test setup | `TestSetup` (`[SetUp]`) | `TestSetup` (`[TestInitialize]`) | `TestSetup` (constructor, `InitializeAsync`) |
| Per-test teardown | `TestTeardown` (`[TearDown]`) | **not recorded** | `TestTeardown` (`Dispose`, `DisposeAsync`) |
| One-time fixture setup | **not recorded** | **not recorded** | `FixtureSetup` (`IClassFixture<T>`, `ICollectionFixture<T>`) |
| One-time fixture teardown | **not recorded** | **not recorded** | **not recorded** |
| Assembly setup / teardown | **not recorded** | **not recorded** | **not recorded** |

A failure Xping cannot place is recorded as `Unknown` rather than assumed to be in the test body, and
a cluster containing one is reported as a plain shared failure. The report never names a member it did
not observe.

### NUnit: `[OneTimeSetUp]` And `[OneTimeTearDown]` Are Not Observable

**Impact**: A broken `[OneTimeSetUp]` produces **no Xping records at all**, not failing ones

**Reason**: `XpingTrackAttribute` is an `ITestAction`, and the hook only fires for tests that execute.
When `[OneTimeSetUp]` throws, NUnit marks the fixture's children failed without running them, so
neither `BeforeTest` nor `AfterTest` is invoked — for the child tests or for the fixture itself. The
runner reports N failing tests; Xping sees none of them.

`[OneTimeTearDown]` is the mirror image: it runs after the last test has already been reported, so a
test whose fixture teardown then throws is recorded by Xping as **passed**, while the runner fails it.

**Workaround**: move the work into `[SetUp]`. It runs per test rather than once, which costs time, but
it is recorded — and a `[SetUp]` that breaks for every test in a fixture produces exactly the broken
fixture finding a `[OneTimeSetUp]` would have.

### NUnit: The Framework Reports No Failure Site

**Impact**: none directly; recorded because it explains why the site is derived from the stack trace

**Reason**: `ResultState.Site` is `Test` for every test-level result, including one whose `[SetUp]`
threw. The states that carry a site — `SetUpFailure`, `SetUpError`, `TearDownError` — are recorded on
the enclosing *suite*, which the adapter never sees. Xping therefore matches the stack trace against
the fixture's own lifecycle methods. A failure with no stack trace cannot be placed, which is why
disabling `CaptureStackTraces` does not remove the site (it is resolved first) but a framework that
supplies no trace leaves it `Unknown`.

### MSTest: A Throwing `[TestCleanup]` Discards The Whole Record

**Impact**: The test produces **no `TestExecution` at all**

**Reason**: MSTest runs `[TestCleanup]` methods derived-class first and stops at the first one that
throws. `XpingTestBase.XpingTestCleanup` is declared on the base class, so a `[TestCleanup]` of your
own that throws prevents it from running, and the record is never built. The runner reports the test
as failed; Xping reports nothing.

**Workaround**: wrap the body of your `[TestCleanup]` in a `try`/`catch` and assert the failure inside
the test instead.

### MSTest: `[ClassInitialize]` And `[ClassCleanup]` Are Not Observable

**Impact**: A broken `[ClassInitialize]` produces **no Xping records**; a broken `[ClassCleanup]`
leaves its tests recorded as **passed**

**Reason**: `[ClassInitialize]` failing aborts the class before any `[TestInitialize]` runs, and
`[ClassCleanup]` runs after every test has been recorded. The adapter has no hook at either point.
`[AssemblyInitialize]` and `[AssemblyCleanup]` are unobservable for the same reason.

**Workaround**: move the work into `[TestInitialize]`, which is recorded.

### xUnit: Fixture Disposal Is Not Attributed To A Test

**Impact**: A class or collection fixture whose `Dispose` throws leaves its tests recorded as
**passed**

**Reason**: xUnit reports it as `ITestClassCleanupFailure` / `ITestCollectionCleanupFailure` /
`ITestAssemblyCleanupFailure`, which carry no test and arrive after every test in the class has already
been reported. `XpingMessageSink` forwards these messages to the runner without recording them, since
there is no execution to attach them to.

**What works**: fixture *construction* is fully recorded. xUnit wraps a failing `IClassFixture<T>`
constructor in `Xunit.Sdk.TestClassException` and names the fixture type in the message, which is the
only first-class failure-site signal any of the three frameworks provides. An `ICollectionFixture<T>`
constructor is **not** wrapped — it arrives as the bare exception — and is recognised from its
constructor frame instead.

---

## Local Analysis

### `TimeSensitive` Bins Are Coarse, And Deliberately

**Impact**: a test that fails only on Mondays, or only at month end, is not reported as
time-sensitive. Neither is one whose failures all fall inside a single day.

**Reason**: the default window is twenty runs over fourteen days. That is at most two of any given
weekday and one month boundary, which cannot support a rate. The axes are therefore a six-hour
quarter of the local day, weekend against weekday, and one UTC offset against another — and every
finding additionally requires failures spanning three separate local days, so a single bad evening
is not reported as an evening pattern. A finer bin would fire more often and mean less.

### `TimeSensitive` Charges For Its Own Search, And A Wide Search Is Expensive

**Impact**: a gap that would be reported in a window whose runs all start at one of two times of day
may not be reported in a window whose runs are spread across the clock, even though the second window
holds more evidence. On an even split of a fortnight of runs against a clean other side, five
failures of six are reported and four are not; a window that divided four ways needs one more than
that.

**Reason**: up to six splits are tried and the best is kept, which is a search, and the best of six
noisy splits is systematically wider than any one of them measured alone. Each split is therefore
tested exactly and its result multiplied by the number of distinct divisions the window admitted. A
window whose runs fall in two quarters of the day performed one comparison and pays nothing for it; a
window spread across the whole clock and both day groups performed four or more and needs a
correspondingly wider gap to clear the same bar. That is the correct price — before the charge
existed, a test with no time dependency at all produced a finding in 28% of twenty-run windows — but
it does mean the finding is quieter on a window whose runs are spread thin, and quieter everywhere
than it was before the charge existed. The probability the finding survived on and the number of
divisions charged for are both published with it.

### `ParallelSensitive` Reads A Trend, Not A Split

**Impact**: a suite pinned at a fixed `maxParallelThreads` can now produce a concurrency finding,
where before it structurally could not. In exchange the finding is quieter on very short histories,
and it no longer reports a difference between two halves of a window.

**Reason**: the finding used to divide a test's executions at its own median concurrency and compare
the two halves. On a pinned suite the median *is* the pinned value, every tied execution fell into
the low half, and the high half starved — measured over twenty-run windows, no such suite could
produce a finding at all, however concurrency-sensitive its tests really were. A suite jittering by
one around eight managed it in 38% of windows, and a suite spread evenly over fourteen levels in all
of them, so which tests were reportable depended on the parallelism setting rather than on the tests.

It now reads every level as one point on a dose-response curve and tests the trend across them. Any
variation at all makes a window analysable: the same four distributions read 94%, 100%, 100% and 100%
at twenty runs. What it costs is that the probability behind the finding is computed over runs rather
than over attempts — so a heavily retried afternoon can no longer buy significance with repetition —
and referred to a continuity-corrected normal, which together make short windows quiet. Eight runs,
four of them perfectly separated from the other four, is the smallest window that can produce a
finding at all, and a single quiet run against any number of crowded ones never produces one.

### `ParallelSensitive` Cannot Separate Concurrency From Duration

**Impact**: a test that got slower may be reported as concurrency-sensitive when nothing about
contention is wrong with it.

**Reason**: `ConcurrentTestCount` is observed, not assigned. A slow test overlaps more of its
neighbours than a fast one does, by construction, and where a test is scheduled in a run determines
how crowded the suite was around it. So the concurrency a test ran at is confounded with the test's
own duration and with its position in the run, and a test whose duration crept up — and which
therefore came to run alongside more tests — produces the same rising trend as one that genuinely
fails under contention. The correction is to stratify by duration, and a twenty-run window does not
hold enough executions to estimate a trend inside each stratum. Nothing in the report attempts it.
The observed concurrency range and the per-level table are published so that the dose-response can be
read directly and judged; a rise measured across levels 11 to 13 is a far weaker statement than the
same rise measured across 1 to 14, and only the evidence shows the difference.

---

### `Vanished` Needs A Habit, And Cannot See One In Fewer Than Eight Runs

**Impact**: a test that ran occasionally and then genuinely was deleted is not reported as having
stopped running. Neither is any deleted test, of any history, on a store holding fewer than eight
runs.

**Reason**: absence is only evidence against a habit, and a count of appearances cannot establish
one. Three appearances out of three earlier runs is a habit that stopped; three out of seventeen is a
test that was mostly absent already, and one that runs about a fifth of the time misses three runs in
a row more often than not. So the finding is decided by a test rather than a count — Fisher's exact
test on the earlier and current runs against present and absent, asking how often every one of a
test's appearances would fall among the earlier runs and none among the current ones if appearing had
nothing to do with when the run happened. It is one-sided, legitimately, because the kind only ever
looks at a test already known to be missing, and it reports only at p ≤ 0.05.

Against the default three-run current slice that means a baseline run rate of about 0.71 — twelve of
seventeen earlier runs carries, eight does not. The requirement **eases** as the history lengthens
rather than tightening, towards the 1 − 0.05^(1/3) ≈ 0.632 the conditioning drops away to: fourteen
of twenty, twenty-seven of forty, sixty-five of a hundred, six hundred and thirty-three of a
thousand. Short histories are the expensive ones.

Below eight runs in the window the current slice narrows to a single run, where even perfect
attendance across six earlier runs is one deal in seven, so nothing of this kind is reportable at
all. That is the intended answer rather than an oversight — one run's absence is not evidence that
anything stopped — but it does mean a fresh store says nothing about deleted tests until it has
eight runs in it. The run rate and the p-value are published with every finding that does clear the
bar.

---

### `Vanished` Cannot Tell A Filtered Run From A Deletion, So It Trusts Neither Below Half A Suite

**Impact**: a run that covered less than half the suite is counted on neither side of an absence, so
a deletion that removed more than half a suite is never reported. A `--filter` selecting *more* than
half the suite still produces one false `stopped running` per unselected test.

**Reason**: a run under a `dotnet test --filter` did not fail to see the tests it excluded — it never
looked for them. Counting its silence makes every unselected test look deleted, which is what an
ordinary inner loop produces in about ten minutes: a handful of full runs, then a stream of filtered
ones, then a report claiming most of the suite has stopped running. Every statement in it is true of
the data and false about the world.

So the report classifies each run by how much of the suite it covered — its distinct tests against
the largest run in the window, which is the best evidence the window holds of how big the suite is,
and the only anchor a store of mostly filtered runs does not corrupt. The median does not work: four
full runs and sixteen filtered ones has a median of one test, and every run in it measures as
typical. A report is scoped to exactly one assembly, so the runs being compared are always runs of
the same suite. Runs covering less than half are set aside, the remaining runs are re-split into
their own "now" and "before", and the counts on the finding are of those runs alone — `full runs` in
the sentence, with a `set aside` metric saying how many were left out.

**The trade, stated rather than discovered**: a count cannot separate the two cases, and no threshold
makes it able to. Nine tests missing from a suite of seventeen is the same table whether they were
excluded or removed. The line is placed at a half — the point at which a run stopped being a run of
the suite and became a run of part of it — and it is deliberately biased towards silence: `Vanished`
is capped at `Severity.Low` because a disappearance is usually something the developer did on purpose
a minute ago, so a missed one costs little, whereas the false positive arrives once per unselected
test on every filtered run for as long as it stays in the window.

**Related**: setting runs aside shortens the history this kind measures against, so a store whose
runs are mostly filtered can fall below the eight runs the section above requires and report nothing
at all. Only the kinds that read absence set these runs aside — every other kind still counts them in
full, because a filtered run's *outcomes* are as true as any other run's and it is only its silences
that mean nothing. The summary line says how many runs covered part of the suite, so the distinction
is visible rather than inferred.

### `RetryExhausted` Is Observed, And The Declared Retry Limit Is Not Interpreted

**Impact**: a test whose retry attribute allows three retries but which only ever recorded two
attempts is not reported as out of retries, and a test recorded as having failed a fourth attempt is
— whatever its attribute says.

**Reason**: `MaxRetries` is recorded verbatim by every adapter, and retry attributes disagree about
what it counts. NUnit writes NUnit's `TryCount`, which is total attempts including the first; an
xUnit or MSTest retry library writes whatever its own limit is called, which may or may not include
the first attempt. Comparing an attempt number against it would report identical behaviour as
exhausted on one framework and as fine on another. The report therefore reads only what happened —
the highest attempt recorded for the test in that run failed, and an earlier attempt exists — and
publishes the declared limit beside it as `maxRetriesAsDeclared`, under a name that says whose
number it is.

**Related**: `DelayBetweenRetries` is recorded by the xUnit and MSTest adapters and not by NUnit, so
the `configured wait` metric is absent on NUnit even where the attribute declares a delay. It is
never added to the measured retry time: whether the framework actually waited is not in the session.

---

### `RetryDeepening` Needs A Baseline, And Often Does Not Have One

**Impact**: a test that has plainly started needing more attempts may be reported only as
`RetryMasked`, or not at all.

**Reason**: the finding compares a test's recent runs against its earlier ones, and needs five
earlier runs it settled green in and two recent ones before it will compare them. A test that runs
under a filter, was added this fortnight, or fails outright in most of the window does not reach
that, and a window below eight runs narrows the recent slice to a single run, which is a coin toss
rather than a trend. Both arms also set environmental runs aside, because one outage inside a
three-run "now" fabricates the change outright.

**Related**: the comparison is only as good as the adapter's attempt tracking. Where attempts are
not recorded — see the xUnit and MSTest sections above — a test that needs three attempts looks
exactly like one that needs one.

---

### `DurationRegression` Is Calibrated On Two Arms That Spread Alike, And The Recent One Is Only Three Runs

**Impact**: where a test's three most recent runs vary much more widely than its history does, it can
be reported as `DurationRegression` — "slower" — when its typical duration has not moved. Read at
one in a hundred, that happens on the order of two in a hundred where the recent runs spread twice as
widely as the baseline, and six in a hundred where they spread four times as widely.

**Reason**: the finding asks whether the recent runs are drawn from a slower distribution by
comparing them against every way the runs could have been split between "recent" and "earlier". That
comparison is exact when the two arms are equally dispersed. Brunner–Munzel's own null is weaker — it
asks only that neither arm be slower, and lets the two differ in spread — and what carries the
calibration to that weaker null holds only as the samples grow. The recent arm is three runs, which
is not large enough for it to hold. Both arms centred so that neither is slower, seventeen earlier
runs against three, read at one in a hundred:

| how the two spreads compare | reported as slower |
|---|---|
| earlier runs four times as wide as the recent | 0.02% |
| the two alike | 1.0% |
| recent runs twice as wide as the earlier | 2.0% |
| recent runs four times as wide as the earlier | 6.5% |

The common direction is the safe one: a fortnight of history usually spreads more widely than three
runs do, and there the finding is reported far *less* often than one in a hundred. The liberal
direction is real but needs the recent runs to be much the wilder of the two.

No correction to the comparison fixes this. It is the nonparametric Behrens–Fisher problem, which has
no exact answer at any sample size, and three recent readings leave nothing approximate to fall back
on. Nor does tightening what the finding demands: requiring the confidence interval's lower end to
clear the practical threshold reaches 1.9% and finds only half of all true twofold slowdowns, against
97% today, which is a worse report.

What would move it is more recent runs rather than a better test — most of this is a three-readings
problem. A wider recent slice makes it far less likely that all of its runs land above a steady
baseline by chance, at the cost of a slowdown having to persist longer before it is reported, and of
changing what "recent" means for every other finding that compares two slices. That change is not
made here, and until it is, the figures above are what the finding costs.

**What the finding is really saying when this happens**: not nothing. The recent runs genuinely have
changed — in how much they vary, rather than in how long they take. `DurationUnstable` is the finding
that claim belongs to, and a regression suppresses it for the same test.

**How to tell**: every `DurationRegression` publishes both arms' spread, over exactly the runs the
comparison read. Ask for `--format json` — the terminal report prints each finding's headline and
nothing else, so the per-finding figures live in the JSON evidence, this pair among them:

```json
"evidence": {
  "current":  { "comparedSessions": 3, "comparedDispersion": 1.048 },
  "baseline": { "comparedSessions": 7, "comparedDispersion": 0 }
}
```

A recent figure much larger than the baseline one is the shape described above, and the number to
weigh the "slower" claim against. Both are relative to their own arm's median — a spread of 0.2 means
a typical run fell a fifth away from that arm's own level — which is what makes the two comparable
when one arm is the slower of the two. Measured over four thousand windows of the six-in-a-hundred
cell, 85% of the findings it wrongly produced published the recent arm as the wider one.

---

## General Limitations

### Source Location Comes From The PDB, And Points At The Start Of The Body

**Affected Frameworks**: NUnit, MSTest, xUnit
**Affected Versions**: 1.0.0-rc and later

A finding's trailer ends with the file and line the test is declared at:

```
HIGH  flaky            FlakyTest_PassesOnRetry
      failed 5 of 10 executions (50%) in 5 of 5 runs, 1 failure mode
      evidence low | f_8f042eab | .../SampleApp.MSTest/SampleTests.cs:135
```

None of the three frameworks reports this, so the SDK reads it from the assembly's Portable PDB,
keyed by the test method's metadata token — the same route Test Explorer uses. That brings four
limits worth knowing:

**The line is where the body starts, not the attribute.** A PDB records where *code* is, and an
attribute is not code. For

```csharp
[Test]                       // line 40
public void Checkout()       // line 41
{                            // line 42  <- Debug build
    Assert.That(...);        // line 43  <- Release build
}
```

Which of the two you get depends on the build configuration. A debug build gives the opening brace
its own sequence point, so line 42 is reported. An optimised build has no reason to keep a point for
a brace that generates no code, so the first *executable* statement — line 43 — is the first thing
the PDB can name. The gap is not always one line: a body opening with a comment or a blank line
reports the brace under Debug and the first real statement below them under Release.

Both land inside the method, which is what the location is for; neither ever points at the attribute
or the signature.

**No PDB, no location.** Building with `DebugType=none` strips the symbols, and the trailer simply
omits the location — everything else about the test is still recorded. Both the default portable
PDB (a `.pdb` beside the assembly) and `DebugType=embedded` work. A test assembly shipped to another
machine without its `.pdb` also loses it.

**Paths are made relative to the repository when possible.** A PDB stores the absolute path of the
machine that compiled the assembly, so the SDK trims it against the nearest enclosing `.git` or
solution file, and strips the `/_/` root that a deterministic CI build
(`ContinuousIntegrationBuild=true`) rewrites paths to. When neither applies — an assembly built
somewhere other than where it runs, with no deterministic rewrite — the absolute path is recorded
verbatim.

**MSTest cannot tell overloads apart.** The MSTest adapter resolves a test's `MethodInfo` from
`TestContext.FullyQualifiedTestClassName` and `TestContext.TestName`, which name a method but not its
signature. A test class with two overloads of the same test method name resolves to whichever the
runtime lists first, so the reported line may belong to the other one. This is a pre-existing limit
of the adapter's method resolution (it also affects the pinned fingerprint and the timeout budget);
source location just makes it visible. NUnit and xUnit hand over the method directly and are
unaffected.


### CI Flaky Tests: `XpingContextTests` (NUnit Adapter Tests)

**Affected Tests**: `Xping.Sdk.NUnit.Tests.XpingContextTests` — `RecordTest_AfterInitialize_DoesNotThrow`, `FlushAsync_AfterInitialize_DoesNotThrow`, `IsInitialized_AfterInitialize_ReturnsTrue`
**Affected Versions**: All versions
**Impact**: Intermittent failure in CI when `XPING_ENABLED=true`. Since the tests carry `[RetryFact(3)]` this no longer fails the build; it surfaces as a **RetryMasked** finding instead.
**Status**: The race is intentionally left in place — it is a real-world flaky test that the Xping platform is expected to detect and flag automatically. Only its handling changed.

**Observed failure**:
```
Assert.Null() Failure: Value is not null
Expected: null
Actual:   System.ArgumentNullException: Argument is null. (Parameter '_instance')
   at Xping.Sdk.NUnit.XpingContext.RecordTest(...)
```

**Root cause — race condition between two test framework lifecycles**:

The `Xping.Sdk.NUnit.Tests` project runs tests under **xUnit** as its primary runner, but also contains a NUnit `[SetUpFixture]` (`XpingTestSetup.cs`) for self-hosted telemetry. This creates two independent owners of the same static `XpingContext._instance` field operating concurrently:

1. **NUnit `[SetUpFixture]` teardown** (`AfterAllTests`) calls `XpingContext.ShutdownAsync()`, which atomically sets `_instance = null` via `Interlocked.Exchange`.
2. **xUnit `IAsyncLifetime`** (`InitializeAsync`/`DisposeAsync`) resets `_instance` around each test via `ShutdownAsync`, expecting exclusive ownership.

The race window opens when:

1. NUnit finds no NUnit tests to run and immediately calls `AfterAllTests()`.
2. xUnit is concurrently executing a test inside `[Collection("XpingContext")]`.
3. The xUnit test calls `XpingContext.Initialize()` → `_instance = newLazy`.
4. Before `RecordTest()` is called, NUnit's `AfterAllTests()` fires `ShutdownAsync()` → `_instance = null`.
5. `RecordTest()` calls `_instance.RequireNotNull()` and throws `ArgumentNullException`.

**Why CI-specific**: With `XPING_ENABLED=true` in CI (`XPING_APIKEY` is set), `ShutdownAsync()` triggers real network I/O (session finalization + upload), significantly widening the race window. Locally, the SDK is disabled (no credentials), so disposal is instant and the window is near-zero.

**Why these tests exist as-is**: A deliberate example of a flaky test caused by a legitimate environmental and concurrency issue. Its purpose is to demonstrate Xping's ability to detect, correlate, and report flaky tests automatically across CI runs. Removing the race would eliminate a valuable real-world validation case for the SDK's own flaky test detection pipeline.

**Why `[RetryFact(3)]`**: Retrying does not fix the race and is not meant to — it changes what the flake produces. Without it the test either goes green, in which case nobody learns anything, or goes red and blocks a build over a defect in the test harness rather than in the SDK. With it the build stays green *and* Xping records both attempts, because xRetry is the retry library Xping instruments from inside the retry loop (see the xUnit section above). Attempt 1 is persisted as `Failed` with its real `ArgumentNullException`, attempt 2 as `Passed` with `PassedOnRetry = true`, and the pair is reported as a `RetryMasked` finding — the one flakiness signal that needs no history at all and is otherwise invisible in a green build.

Which is a better demonstration than the original: the flake is now caught every time it occurs, rather than only on the runs where it happened to turn CI red.

All three affected tests take the same path — `Initialize()` followed by a call that dereferences the static instance — so all three carry the attribute. Only `RecordTest_AfterInitialize_DoesNotThrow` has been observed failing so far; the other two are the same race waiting for a wider window.

---

## Reporting Issues

If you encounter a limitation not documented here, please:

1. **Search existing issues**: [GitHub Issues](https://github.com/xping-io/sdk-dotnet/issues)
2. **Report new limitations**: [Create New Issue](https://github.com/xping-io/sdk-dotnet/issues/new)

When reporting, please include:
- Test framework and version
- Xping SDK version
- Minimal reproduction code
- Expected vs actual behavior

---

## Version History

| Version | Changes |
|---------|---------|
| 1.0.0   | Initial documentation - NUnit and MSTest `[Ignore]` limitation |
| 1.1.0   | Added known CI flaky test `RecordTest_AfterInitialize_DoesNotThrow` as intentional flakiness example |
| 1.2.0   | Documented xUnit retry attempt tracking and the retry libraries it covers |
| 1.3.0   | Documented MSTest retry attempt tracking and how attempt numbers are derived |
| 1.4.0   | Documented the binning limits of `TimeSensitive` |
| 1.5.0   | Documented how the retry findings read attempt numbers, and why the declared retry limit is never interpreted |
| 1.6.0   | Documented where source location comes from, and what it cannot answer |
| 1.7.0   | Documented what `TimeSensitive` now charges for searching three axes, and what that costs |
| 1.8.0   | Documented what `ParallelSensitive` now measures, and the duration confound it cannot correct |
| 1.9.0   | Documented the run rate `Vanished` now requires, and the window size below which it is silent |
| 1.10.0  | Documented how `Vanished` treats a run that covered part of the suite, and what that trade costs |
| 1.11.0  | Documented what `DurationRegression`'s calibration is exact for, where it is not, and the two spreads published beside every such finding |
