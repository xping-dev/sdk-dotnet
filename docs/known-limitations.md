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

## General Limitations

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
