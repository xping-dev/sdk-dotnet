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
