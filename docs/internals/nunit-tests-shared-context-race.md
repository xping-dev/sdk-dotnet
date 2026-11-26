# The deliberate race in `Xping.Sdk.NUnit.Tests.XpingContextTests`

This note is for contributors. It explains why three tests in the NUnit adapter's test project sometimes fail in CI, and why the race is kept.

**Affected Tests**: `Xping.Sdk.NUnit.Tests.XpingContextTests` — `RecordTest_AfterInitialize_DoesNotThrow`, `FlushAsync_AfterInitialize_DoesNotThrow`, `IsInitialized_AfterInitialize_ReturnsTrue`
**Impact**: Intermittent failure in CI when `XPING_ENABLED=true`. The tests carry `[RetryFact(3)]`, so the race does not fail the build. It shows as a **RetryMasked** finding instead.
**Status**: The race is left in place on purpose. It is a real flaky test that Xping is expected to detect and flag.

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

**Why CI-specific**: With `XPING_ENABLED=true` in CI (`XPING_APIKEY` is set), `ShutdownAsync()` triggers real network I/O (session finalization + upload), significantly widening the race window. Locally, with no API key, the SDK runs in `LocalOnly` mode and makes no upload, so disposal is quick and the window is near zero.

**Why these tests exist as-is**: A deliberate example of a flaky test caused by a legitimate environmental and concurrency issue. Its purpose is to demonstrate Xping's ability to detect, correlate, and report flaky tests automatically across CI runs. Removing the race would eliminate a valuable real-world validation case for the SDK's own flaky test detection pipeline.

**Why `[RetryFact(3)]`**: Retrying does not fix the race and is not meant to — it changes what the flake produces. Without it the test either goes green, in which case nobody learns anything, or goes red and blocks a build over a defect in the test harness rather than in the SDK. With it the build stays green *and* Xping records both attempts, because xRetry is the retry library Xping instruments from inside the retry loop (see [Known limitations](../troubleshooting/known-limitations.md#xunit)). Attempt 1 is persisted as `Failed` with its real `InvalidOperationException`, attempt 2 as `Passed` with `PassedOnRetry = true`, and the pair is reported as a `RetryMasked` finding — the one flakiness signal that needs no history at all and is otherwise invisible in a green build.

So Xping catches the flake every time it happens, not only on the runs where it turns CI red.

All three affected tests take the same path — `Initialize()` followed by a call that dereferences the static instance — so all three carry the attribute. Only `RecordTest_AfterInitialize_DoesNotThrow` has been observed failing so far; the other two are the same race waiting for a wider window.
