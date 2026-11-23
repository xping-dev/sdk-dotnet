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

### xUnit v3

Skipped tests are tracked, including a test that skips itself while running (`Assert.Skip`,
`Assert.SkipWhen`). An explicit test (`[Fact(Explicit = true)]`) that the run leaves out is not
recorded, because it did not run.

#### xUnit v3 Records Only The Last Retry Attempt

**Affected Versions**: All versions, with xRetry.v3 1.0
**Impact**: A test retried by xRetry.v3 is recorded once, as the attempt it ended on. `AttemptNumber`
and `PassedOnRetry` are right, so a test that passed on its second attempt shows as a pass on retry.
The attempts before it are not recorded: their failure messages and stack traces are lost, and the
recorded duration is that of all attempts together.

**Reason**: xRetry.v3 runs each attempt behind a message bus of its own and discards every attempt
but the last, as its v2 version does. Unlike its v2 version, it exposes no hook to run an attempt
through, so Xping cannot sit inside the loop. It does announce each attempt (`Running test "…"
attempt (2/3)`) on the run's own bus, which is where the attempt number comes from.

#### vstest Needs `xunit.v3.mtp-off` On The .NET 10 SDK

**Impact**: `dotnet test` without a Microsoft.Testing.Platform `global.json` fails the build of an
`xunit.v3` project with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform".

**Reason**: `xunit.v3` makes the test project a Microsoft.Testing.Platform app, and the .NET 10 SDK's
vstest refuses one. This is xUnit's and the SDK's behavior, not Xping's. Reference `xunit.v3.mtp-off`
instead of `xunit.v3` to stay on vstest, or move `dotnet test` to Microsoft.Testing.Platform.

#### A Timed-Out Test Can Crash The TRX Report On Microsoft.Testing.Platform

**Affected Versions**: xunit.v3 4.0.2 with Microsoft.Testing.Extensions.TrxReport 2.5.1
**Impact**: Now and then, a run with `--report-trx` exits 7 ("test host crashed") after a
`[Fact(Timeout = …)]` test times out. The output shows an extra `[Test Cleanup Failure (…)]` result
for that test and an `InvalidOperationException` from `TrxReportEngine.GetClassAndMethodName`, and
the TRX is empty. Xping still records the test as timed out. It happens more often on a loaded
machine, such as CI.

**Reason**: the test passed `TestContext.Current.CancellationToken` to the call that hung. xUnit
cancels that token at the timeout, so the abandoned task fails afterwards. xUnit v3 sometimes reports
that failure as a separate cleanup failure, which carries no class or method name, and the TRX
extension crashes on it. This happens without Xping too: see
[xunit/xunit#3669](https://github.com/xunit/xunit/issues/3669). Until it is fixed, drop the token
from a call the timeout is meant to abandon (and suppress `xUnit1069` there), or run without
`--report-trx`.

#### A Theory Row Is Identified By The Arguments In Its Display Name

**Impact**: Two rows of one theory whose arguments display the same way, such as two different
objects with the same `ToString()`, share one identity and one history. A row's identity also changes
when its display text does.

**Reason**: a theory row xUnit v3 did not pre-enumerate reaches Xping without its arguments: only
its display name carries them. vstest and Microsoft.Testing.Platform pre-enumerate; xUnit's own console
runner, and a runner configured with `preEnumerateTheories: false`, do not. Reading the arguments from
the display name every time keeps a row's identity the same however it was run.

The exception is a value xUnit shortens in the display name (it ends with `···`): a string over
50 characters, a collection over 5 items, or a deeply nested object. A pre-enumerated row with such a
value is identified by its real arguments instead, so rows that differ past the cut stay apart. A row
xUnit did not pre-enumerate still uses the shortened text, so such a row has a different identity
under xUnit's console runner than under vstest or Microsoft.Testing.Platform.

#### The Runner Is Unknown Under xUnit's Own Console Runner

**Impact**: A session recorded by `xunit.v3.runner.console`, or by running the test executable by
hand without Microsoft.Testing.Platform, has no `Xping.Runner` property, so `xping report` prints no
command to run a test again.

**Reason**: an xUnit v3 test assembly is its own executable on every runner, so the entry assembly
says nothing about the runner. Xping reads it from the runner's code on the call stack and from
vstest's environment, and leaves it out rather than guess.

---

### Timeouts

Xping records a test the framework killed for overrunning its timeout as `Outcome = Timeout` rather
than `Failed`, and captures the budget the test declared alongside it. The cases below are recorded
differently or not at all.

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

#### MSTest's Cooperative `[Timeout]` Is Recorded As Failed Or Passed

With `[Timeout(..., CooperativeCancellation = true)]`, or with cooperative cancellation turned on for
every test (`CooperativeCancellationTimeout` in `.runsettings` or `testconfig.json`), MSTest cancels
the test's token instead of abandoning the test. By the time `[TestCleanup]` runs, MSTest reports nothing that marks the test as
timed out:

- A test that observes the token ends with an `OperationCanceledException`. Xping records it as
  `Failed`.
- A test that ignores the token runs to completion. MSTest passes it, and Xping records `Passed`.

A non-cooperative timeout, which is MSTest's default, is recorded as `Timeout`, for sync and async
tests alike.

**Affected Versions**: MSTest 3.8 and later

#### MSTest's Run-Wide Timeout Is Recorded Without A Budget

A test stopped by a timeout set for the whole run (`TestTimeout` in `.runsettings` or
`testconfig.json`) is recorded as `Timeout`. Xping reads the budget only from the test's own
`[Timeout]`, so that execution has no budget.

**Affected Versions**: MSTest 3.8 and later

#### xUnit v2 Applies A Timeout Only To Async Tests

`[Fact(Timeout = ...)]` on a synchronous test is rejected by xUnit v2 itself, which fails the test with
"Tests marked with Timeout are only supported for async tests". Xping records that as `Failed`, not
`Timeout` — it is a misconfigured test, not a hanging one.

## Fixture Failures

Xping records **where** a failing test failed — the test body, per-test setup or teardown, or a
one-time fixture — in `TestExecution.Site`, and names the member in `FailureSiteMember`. When every
failure in a cluster agrees on one member, `xping report` reports a **broken fixture** naming it
instead of N separate failing tests.

What is reachable differs by framework, and none of the three reports a failure site of its own. The
tables below record what was observed by running each package, not what its documentation says.

**Affected Versions**: NUnit 3.14 and 4.2, MSTest 3.8 and later, xUnit 2.9

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
the fixture's own lifecycle methods. A failure with no stack trace cannot be placed, so a framework
that supplies no trace leaves it `Unknown`. `UploadStackTraces` does not change the site: it is
resolved from the trace before the upload leaves the trace out.

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

### MSTest: A Session Finalized By The Process-Exit Safety Net Loses Its Upload On vstest

**Impact**: When no `[AssemblyCleanup]` finalizes the session, the SDK finalizes it from a
process-exit safety net. Local history is written and `xping report` sees the run, but on vstest the
**cloud upload is usually lost**. On Microsoft.Testing.Platform the upload completes.

**Reason**: The safety net runs inside `AppDomain.ProcessExit`, and vstest terminates the test host
100 ms after the run completes. Microsoft.Testing.Platform runs in the test process and lets the
handler finish. A local file write fits in that window; a
network round trip does not. The hook goes missing when the test assembly declares none (MSTest only
runs assembly hooks declared in the test assembly itself), when it throws before finalizing, or, on
MSTest 3.7.x, under method-level parallelization (fixed in 3.8, the package's minimum).

**Workaround**: declare an `[AssemblyCleanup]` in the test assembly that calls
`XpingContext.FinalizeAndShutdownAsync()`. If the safety net still fires on vstest, set
`VSTEST_TESTHOST_SHUTDOWN_TIMEOUT=5000` in the environment that runs `dotnet test` so the upload has
time to complete.

### xUnit: Fixture Disposal Is Not Attributed To A Test

**Impact**: A class or collection fixture whose `Dispose` throws leaves its tests recorded as
**passed**

**Reason**: xUnit reports it as `ITestClassCleanupFailure` / `ITestCollectionCleanupFailure` /
`ITestAssemblyCleanupFailure`, which carry no test and arrive after every test in the class has already
been reported. `XpingMessageSink` forwards these messages to the runner without recording them, since
there is no execution to attach them to.

**What works**: fixture *construction* is fully recorded. xUnit v2 wraps a failing `IClassFixture<T>`
constructor in `Xunit.Sdk.TestClassException` and names the fixture type in the message, which is the
only first-class failure-site signal any of the three frameworks provides. An `ICollectionFixture<T>`
constructor is **not** wrapped — it arrives as the bare exception — and is recognised from its
constructor frame instead. xUnit v3 wraps a failing fixture of every scope, assembly, collection and
class, in `Xunit.Sdk.TestPipelineException` and names its type.

---

## Local Analysis

The limits of the CLI's local analysis are on
[Reading the report](../cli/reading-the-report.md#limits-of-local-analysis).

---

## General Limitations

### Printed Commands Are Quoted For One Shell, Chosen By `XPING_SHELL` Or The OS

**Affected**: `xping report` drill-down, `History:` and repro commands, and their `--format json` fields

The CLI can't see which shell started it. It quotes for the shell `XPING_SHELL` names (`posix` or
`powershell`), and without it by OS: PowerShell 7.3+ on Windows, POSIX everywhere else. The two
styles differ for a value holding an apostrophe or a curly single quote (`‘ ’ ‚ ‛`), which PowerShell
doubles, and for a `\`, which PowerShell leaves bare and POSIX quotes.

- **Git Bash on Windows**, where coding agents usually run commands, gets PowerShell quoting unless
  you set `XPING_SHELL=posix`.
- **cmd.exe** isn't supported. It has no single quotes, so a quoted value is split at `|`, `&` or a
  space, and keeps its quotes.
- **Windows PowerShell 5.1** isn't supported. It passes a `"` inside an argument to `dotnet` without
  escaping it, so a repro filter naming an NUnit test with a string argument, like `Echo("x")`,
  breaks. Use PowerShell 7.3 or later.

### Source Location Comes From The PDB, And Points At The Start Of The Body

**Affected Frameworks**: NUnit, MSTest, xUnit
**Affected Versions**: 1.0.0-rc and later

A finding's trailer ends with the file and line the test is declared at:

<!-- xping:sample docs-location rows -->
```
1.  HIGH  flaky
    SampleTests.FlakyTest_PassesOnRetry
    failed 5 of 10 executions (50%) in 5 of 5 runs, 1 failure mode
    evidence sparse | -env-cluster | f_8f042eab | .../SampleTests.cs:135
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
Actual:   System.InvalidOperationException: XpingContext is not initialized. Call XpingContext.Initialize() first.
   at Xping.Sdk.NUnit.XpingContext.RecordTest(...)
```

**Root cause — race condition between two test framework lifecycles**:

The `Xping.Sdk.NUnit.Tests` project runs tests under **xUnit** as its primary runner, but also contains a NUnit `[SetUpFixture]` (`XpingTestSetup.cs`) for self-hosted telemetry. This creates two independent owners of the same static context (the `XpingContextSingleton` behind `XpingContext`) operating concurrently:

1. **NUnit `[SetUpFixture]` teardown** (`AfterAllTests`) calls `XpingContext.FinalizeAsync()` and then `XpingContext.ShutdownAsync()`, which atomically uninstalls the context via `Interlocked.Exchange`. It deliberately avoids `FinalizeAndShutdownAsync()`: the context it finalizes may be a strict-mode one an xUnit test installed, aimed at an unreachable endpoint, and the resulting `Environment.FailFast` would crash the test host.
2. **xUnit `IAsyncLifetime`** (`InitializeAsync`/`DisposeAsync`) resets the context around each test via `ShutdownAsync`, expecting exclusive ownership.

The race window opens when:

1. NUnit finds no NUnit tests to run and immediately calls `AfterAllTests()`.
2. xUnit is concurrently executing a test inside `[Collection("XpingContext")]`.
3. The xUnit test calls `XpingContext.Initialize()`, which installs a new context.
4. Before `RecordTest()` is called, NUnit's `AfterAllTests()` fires `ShutdownAsync()`, which uninstalls it.
5. `RecordTest()` finds no context and throws `InvalidOperationException`.

**Why CI-specific**: With `XPING_ENABLED=true` in CI (`XPING_APIKEY` is set), `ShutdownAsync()` triggers real network I/O (session finalization + upload), significantly widening the race window. Locally, the SDK is disabled (no credentials), so disposal is instant and the window is near-zero.

**Why these tests exist as-is**: A deliberate example of a flaky test caused by a legitimate environmental and concurrency issue. Its purpose is to demonstrate Xping's ability to detect, correlate, and report flaky tests automatically across CI runs. Removing the race would eliminate a valuable real-world validation case for the SDK's own flaky test detection pipeline.

**Why `[RetryFact(3)]`**: Retrying does not fix the race and is not meant to — it changes what the flake produces. Without it the test either goes green, in which case nobody learns anything, or goes red and blocks a build over a defect in the test harness rather than in the SDK. With it the build stays green *and* Xping records both attempts, because xRetry is the retry library Xping instruments from inside the retry loop (see the xUnit section above). Attempt 1 is persisted as `Failed` with its real `InvalidOperationException`, attempt 2 as `Passed` with `PassedOnRetry = true`, and the pair is reported as a `RetryMasked` finding — the one flakiness signal that needs no history at all and is otherwise invisible in a green build.

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
