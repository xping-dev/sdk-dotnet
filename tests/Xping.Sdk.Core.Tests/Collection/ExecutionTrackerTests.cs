/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Collector.Internals;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Collection;

public sealed class ExecutionTrackerTests
{
    private static IExecutionTracker BuildTracker() => ServiceHelper.BuildTracker();

    // ---------------------------------------------------------------------------
    // CreateExecutionContext — first attempt
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateExecutionContext_ShouldReturnPositionOne_ForFirstTest()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "worker-1";

        // Act
        var record = tracker.CreateExecutionContext(workerId);

        // Assert
        Assert.Equal(1, record.PositionInSuite);
        Assert.Equal(1, record.GlobalPosition);
    }

    [Fact]
    public void CreateExecutionContext_ShouldIncrementWorkerPosition_ForSubsequentTests()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "worker-1";

        // Act
        tracker.CreateExecutionContext(workerId);
        tracker.CreateExecutionContext(workerId);
        var third = tracker.CreateExecutionContext(workerId);

        // Assert
        Assert.Equal(3, third.PositionInSuite);
    }

    [Fact]
    public void CreateExecutionContext_ShouldIncrementGlobalPosition_AcrossWorkers()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act
        tracker.CreateExecutionContext("worker-a");
        tracker.CreateExecutionContext("worker-b");
        var third = tracker.CreateExecutionContext("worker-a");

        // Assert
        Assert.Equal(3, tracker.GlobalPosition);
        // Worker-a is on its 2nd test
        Assert.Equal(2, third.PositionInSuite);
    }

    [Fact]
    public void CreateExecutionContext_RetryAttempt_ShouldReusePosition()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "worker-retry";

        // Act — first attempt claims position 1
        var firstAttempt = tracker.CreateExecutionContext(workerId, attemptNumber: 1);
        // Retry should reuse position 1, not claim position 2
        var retryAttempt = tracker.CreateExecutionContext(workerId, attemptNumber: 2);

        // Assert
        Assert.Equal(1, firstAttempt.PositionInSuite);
        Assert.Equal(1, retryAttempt.PositionInSuite);
    }

    [Fact]
    public void CreateExecutionContext_RetryAttempt_ShouldNotIncrementGlobalPosition()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "w";

        tracker.CreateExecutionContext(workerId, attemptNumber: 1);
        int globalAfterFirst = tracker.GlobalPosition;

        // Act
        tracker.CreateExecutionContext(workerId, attemptNumber: 2);

        // Assert
        Assert.Equal(globalAfterFirst, tracker.GlobalPosition);
    }

    [Fact]
    public void CreateExecutionContext_ShouldIncludeNoPreviousTest_OnFirstExecution()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act
        var record = tracker.CreateExecutionContext("w");

        // Assert
        Assert.Null(record.PreviousTestId);
        Assert.Null(record.PreviousTestName);
        Assert.Null(record.PreviousTestOutcome);
    }

    [Fact]
    public void CreateExecutionContext_ShouldIncludePreviousTest_AfterCompletion()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "w";

        tracker.CreateExecutionContext(workerId);
        tracker.RecordTestCompletion(workerId, "test-id-1", "Test_One", TestOutcome.Passed);

        // Act
        var second = tracker.CreateExecutionContext(workerId);

        // Assert
        Assert.Equal("test-id-1", second.PreviousTestId);
        Assert.Equal("Test_One", second.PreviousTestName);
        Assert.Equal(TestOutcome.Passed, second.PreviousTestOutcome);
    }

    [Fact]
    public void CreateExecutionContext_ShouldUseCollectionName_WhenProvided()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act
        var record = tracker.CreateExecutionContext("w", collectionName: "MyCollection");

        // Assert
        Assert.Equal("MyCollection", record.CollectionName);
    }

    [Fact]
    public void CreateExecutionContext_NullWorkerId_ShouldFallBackToThreadId()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act — calling without explicit workerId; falls back to current thread ID
        var record = tracker.CreateExecutionContext(workerId: null);

        // Assert
        Assert.NotEmpty(record.WorkerId);
        Assert.Equal(record.ThreadId, record.WorkerId);
    }

    // ---------------------------------------------------------------------------
    // RecordTestCompletion / GetPreviousTest
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetPreviousTest_ShouldReturnNull_ForUnknownWorker()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act
        var prev = tracker.GetPreviousTest("never-used-worker");

        // Assert
        Assert.Null(prev);
    }

    [Fact]
    public void GetPreviousTest_ShouldReturnRecord_AfterCompletion()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "w";

        // Act
        tracker.RecordTestCompletion(workerId, "id-42", "My_Test", TestOutcome.Failed);
        var prev = tracker.GetPreviousTest(workerId);

        // Assert
        Assert.NotNull(prev);
        Assert.Equal("id-42", prev.TestFingerprint);
        Assert.Equal("My_Test", prev.TestName);
        Assert.Equal(TestOutcome.Failed, prev.Outcome);
    }

    [Fact]
    public void GetPreviousTest_ShouldReturnLatestCompletion_WhenCalledMultipleTimes()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "w";

        tracker.RecordTestCompletion(workerId, "id-1", "First", TestOutcome.Passed);
        tracker.RecordTestCompletion(workerId, "id-2", "Second", TestOutcome.Failed);

        // Act
        var prev = tracker.GetPreviousTest(workerId);

        // Assert
        Assert.Equal("id-2", prev!.TestFingerprint);
        Assert.Equal("Second", prev.TestName);
    }

    // ---------------------------------------------------------------------------
    // GetWorkerPosition
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetWorkerPosition_ShouldReturnZero_ForUnknownWorker()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act & Assert
        Assert.Equal(0, tracker.GetWorkerPosition("unknown"));
    }

    [Fact]
    public void GetWorkerPosition_ShouldReturnCurrentPosition_AfterExecution()
    {
        // Arrange
        var tracker = BuildTracker();
        const string workerId = "w";

        tracker.CreateExecutionContext(workerId);
        tracker.CreateExecutionContext(workerId);

        // Act
        var pos = tracker.GetWorkerPosition(workerId);

        // Assert
        Assert.Equal(2, pos);
    }

    // ---------------------------------------------------------------------------
    // Parallelization (issue #120)
    //
    // WasParallelized/ConcurrentTestCount used to be derived from the number of distinct
    // worker keys ever seen, which is monotonically non-decreasing and says nothing about
    // concurrency. They are now measured from RecordTestStart/RecordTestEnd notifications.
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateExecutionContext_SerialExecutionAcrossWorkers_ShouldNotReportParallelization()
    {
        // Regression for #120: a serial run that visits several workers (xUnit reports one
        // collection per test class) must not be reported as parallel.
        // Arrange
        var tracker = BuildTracker();

        // Act — one test at a time, each fully finished before the next starts
        tracker.RecordTestStart("worker-a");
        var first = tracker.CreateExecutionContext("worker-a");
        tracker.RecordTestEnd("worker-a");

        tracker.RecordTestStart("worker-b");
        var second = tracker.CreateExecutionContext("worker-b");
        tracker.RecordTestEnd("worker-b");

        // Assert
        Assert.Equal(1, first.ConcurrentTestCount);
        Assert.False(first.WasParallelized);
        Assert.Equal(1, second.ConcurrentTestCount);
        Assert.False(second.WasParallelized);
    }

    [Fact]
    public void CreateExecutionContext_OverlappingTests_ShouldReportBothAsParallel()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act — both tests are in flight at the same time
        tracker.RecordTestStart("worker-a");
        tracker.RecordTestStart("worker-b");

        var recordA = tracker.CreateExecutionContext("worker-a");
        var recordB = tracker.CreateExecutionContext("worker-b");

        // Assert — overlapping tests agree on the count, so a session's records stay consistent
        Assert.Equal(2, recordA.ConcurrentTestCount);
        Assert.True(recordA.WasParallelized);
        Assert.Equal(2, recordB.ConcurrentTestCount);
        Assert.True(recordB.WasParallelized);
    }

    [Fact]
    public void CreateExecutionContext_OverlapEndedBeforeRecordCreated_ShouldReportPeak()
    {
        // ConcurrentTestCount is the peak overlap during the test, not a snapshot taken when
        // the record is built — worker-b has already finished by the time worker-a reports.
        // Arrange
        var tracker = BuildTracker();

        // Act
        tracker.RecordTestStart("worker-a");
        tracker.RecordTestStart("worker-b");
        tracker.RecordTestEnd("worker-b");

        var record = tracker.CreateExecutionContext("worker-a");

        // Assert
        Assert.Equal(2, record.ConcurrentTestCount);
        Assert.True(record.WasParallelized);
    }

    [Fact]
    public void CreateExecutionContext_WithoutRecordedStart_ShouldReportSingleTest()
    {
        // Callers that never report test starts (third-party integrations) get the honest
        // default rather than a fabricated concurrency value.
        // Arrange
        var tracker = BuildTracker();

        // Act
        var record = tracker.CreateExecutionContext("worker-a");

        // Assert
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.False(record.WasParallelized);
    }

    [Fact]
    public void CreateExecutionContext_WithoutRecordedStart_ShouldReportSingleTestWhileOthersAreInFlight()
    {
        // A caller that does not report starts has no measurable overlap window of its own, so
        // tests in flight on other workers must not be attributed to it.
        // Arrange
        var tracker = BuildTracker();
        tracker.RecordTestStart("worker-a");
        tracker.RecordTestStart("worker-b");

        // Act
        var record = tracker.CreateExecutionContext("worker-untracked");

        // Assert
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.False(record.WasParallelized);
    }

    [Fact]
    public void RecordTestStart_CalledTwiceForSameWorker_ShouldNotDoubleCount()
    {
        // [XpingTrack] can be applied at assembly, class and method scope at once, in which case
        // NUnit invokes BeforeTest once per attribute instance for the same test.
        // Arrange
        var tracker = BuildTracker();

        // Act
        tracker.RecordTestStart("worker-a");
        tracker.RecordTestStart("worker-a");
        var record = tracker.CreateExecutionContext("worker-a");

        // Assert
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.False(record.WasParallelized);
    }

    [Fact]
    public void RecordTestEnd_WithoutMatchingStart_ShouldBeNoOp()
    {
        // Adapters call RecordTestEnd from a finally block, which can run without a matching start
        // (e.g. when initialization failed before the test was registered).
        // Arrange
        var tracker = BuildTracker();

        // Act
        tracker.RecordTestEnd("worker-a");
        tracker.RecordTestEnd("worker-a");
        tracker.RecordTestEnd("worker-b");

        tracker.RecordTestStart("worker-a");
        var record = tracker.CreateExecutionContext("worker-a");

        // Assert — no underflow leaked into the next test
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.False(record.WasParallelized);
    }

    [Fact]
    public void RecordTestStart_AfterAbandonedTest_ShouldSelfHealOnSameWorker()
    {
        // A test whose end is never reported (framework abort) must not leave the worker
        // permanently occupied — otherwise every later test looks parallel, which is #120 again.
        // Arrange
        var tracker = BuildTracker();
        tracker.RecordTestStart("worker-a"); // abandoned: no RecordTestEnd

        // Act
        tracker.RecordTestStart("worker-a");
        var record = tracker.CreateExecutionContext("worker-a");

        // Assert
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.False(record.WasParallelized);
    }

    [Fact]
    public void RecordTestStart_AbandonedOtherWorker_ShouldStillCount()
    {
        // Accepted residual behavior: a lost end on a worker that never runs another test keeps
        // its slot for the rest of the run, so tests on other workers see it as an overlap.
        // Encoded here so the trade-off cannot drift silently.
        // Arrange
        var tracker = BuildTracker();
        tracker.RecordTestStart("worker-a"); // abandoned: no RecordTestEnd

        // Act
        tracker.RecordTestStart("worker-b");
        var record = tracker.CreateExecutionContext("worker-b");

        // Assert
        Assert.Equal(2, record.ConcurrentTestCount);
        Assert.True(record.WasParallelized);
    }

    [Fact]
    public void RecordTestStart_NullWorkerId_ShouldPairWithThreadIdFallback()
    {
        // Arrange — serial NUnit runs report a null WorkerId, so both sides fall back to the thread id
        var tracker = BuildTracker();

        // Act
        tracker.RecordTestStart();
        var record = tracker.CreateExecutionContext();
        tracker.RecordTestEnd();

        // Assert
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.False(record.WasParallelized);
    }

    [Fact]
    public void RecordTestStart_ShouldReturnNumberOfTestsInFlight()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act & Assert
        Assert.Equal(1, tracker.RecordTestStart("worker-a"));
        Assert.Equal(2, tracker.RecordTestStart("worker-b"));

        tracker.RecordTestEnd("worker-a");

        Assert.Equal(2, tracker.RecordTestStart("worker-c"));
    }

    [Fact]
    public void CreateExecutionContext_ConcurrentCalls_ShouldReportEveryTestAsParallel()
    {
        // Arrange
        const int workerCount = 8;
        var tracker = BuildTracker();
        var records = new ConcurrentBag<TestOrchestrationRecord>();
        using var allStarted = new Barrier(workerCount);

        // Act — every worker registers its start, then all of them build their records
        Parallel.For(0, workerCount, index =>
        {
            string workerId = $"worker-{index}";

            tracker.RecordTestStart(workerId);
            allStarted.SignalAndWait();

            records.Add(tracker.CreateExecutionContext(workerId));
            tracker.RecordTestEnd(workerId);
        });

        // Assert — all eight overlapped, so all eight report the full peak
        Assert.Equal(workerCount, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(workerCount, record.ConcurrentTestCount);
            Assert.True(record.WasParallelized);
        });
    }

    [Fact]
    public void CreateExecutionContext_ConcurrentCalls_ShouldNotProduceMixedRecords()
    {
        // Regression for the shared TestOrchestrationBuilder: concurrent callers used to interleave
        // their Reset()/With...() calls on one instance and emit records mixing two tests' fields.
        // This is a probabilistic detector — intermittent against the old code, always green now.
        // Arrange
        const int workerCount = 16;
        var tracker = BuildTracker();
        var records = new ConcurrentBag<TestOrchestrationRecord>();

        // Act — worker and collection names are paired, so a mixed record shows up as a mismatch
        Parallel.For(0, workerCount, index =>
        {
            var record = tracker.CreateExecutionContext($"worker-{index}", $"collection-{index}");
            records.Add(record);
        });

        // Assert
        Assert.Equal(workerCount, records.Count);
        Assert.All(records, record =>
        {
            string index = record.WorkerId.Substring("worker-".Length);
            Assert.Equal($"collection-{index}", record.CollectionName);
            Assert.Equal(1, record.PositionInSuite); // each worker ran exactly one test
        });
    }

    // ---------------------------------------------------------------------------
    // ActiveWorkerCount
    // ---------------------------------------------------------------------------

    [Fact]
    public void ActiveWorkerCount_ShouldReflectDistinctWorkersWithTests()
    {
        // Arrange
        var tracker = BuildTracker();

        // Act
        tracker.CreateExecutionContext("w1");
        tracker.CreateExecutionContext("w2");
        tracker.CreateExecutionContext("w3");
        tracker.CreateExecutionContext("w1"); // same worker, second test

        // Assert
        Assert.Equal(3, tracker.ActiveWorkerCount);
    }

    // ---------------------------------------------------------------------------
    // Clear
    // ---------------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldResetAllState()
    {
        // Arrange
        var tracker = BuildTracker();

        tracker.CreateExecutionContext("w");
        tracker.RecordTestCompletion("w", "id", "Name", TestOutcome.Passed);

        // Act
        tracker.Clear();

        // Assert
        Assert.Equal(0, tracker.GlobalPosition);
        Assert.Equal(0, tracker.ActiveWorkerCount);
        Assert.Null(tracker.GetPreviousTest("w"));
        Assert.Equal(0, tracker.GetWorkerPosition("w"));
    }

    [Fact]
    public void Clear_ShouldAllowFreshTracking_AfterReset()
    {
        // Arrange
        var tracker = BuildTracker();
        tracker.CreateExecutionContext("w");
        tracker.Clear();

        // Act
        var record = tracker.CreateExecutionContext("w");

        // Assert
        Assert.Equal(1, record.PositionInSuite);
        Assert.Equal(1, tracker.GlobalPosition);
    }

    [Fact]
    public void Clear_ShouldResetInFlightState()
    {
        // Arrange — two tests left in flight when the suite state is released
        var tracker = BuildTracker();
        tracker.RecordTestStart("w1");
        tracker.RecordTestStart("w2");

        // Act
        tracker.Clear();

        tracker.RecordTestStart("w3");
        var record = tracker.CreateExecutionContext("w3");

        // Assert — the abandoned slots do not survive the reset
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.False(record.WasParallelized);
    }

    // ---------------------------------------------------------------------------
    // SuiteElapsedTime
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateExecutionContext_FirstCall_ShouldReturnNearZeroSuiteElapsedTime()
    {
        // Arrange — fake time: first GetTimestamp call captures suite start, second computes delta.
        // Both calls happen at the same tick, so elapsed should be zero.
        var fake = new FakeTimeProvider(frequency: 1_000_000_000L);
        fake.CurrentTimestamp = 1_000_000_000L; // 1 second into process lifetime (avoids sentinel 0)
        IExecutionTracker tracker = new ExecutionTracker(fake);

        // Act
        var record = tracker.CreateExecutionContext("w");

        // Assert — start was captured at the same tick as the elapsed read, so delta is zero
        Assert.Equal(TimeSpan.Zero, record.SuiteElapsedTime);
    }

    [Fact]
    public void CreateExecutionContext_SubsequentCall_ShouldReturnAccumulatedSuiteElapsedTime()
    {
        // Arrange
        var fake = new FakeTimeProvider(frequency: 1_000_000_000L); // 1 GHz → 1 tick = 1 ns
        fake.CurrentTimestamp = 1_000_000_000L;
        IExecutionTracker tracker = new ExecutionTracker(fake);

        // First call: captures suite start at tick 1_000_000_000
        tracker.CreateExecutionContext("w");

        // Advance time by exactly 5 seconds
        fake.CurrentTimestamp = 6_000_000_000L;

        // Act
        var record = tracker.CreateExecutionContext("w");

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), record.SuiteElapsedTime);
    }

    [Fact]
    public void CreateExecutionContext_RetryAttempt_ShouldReflectRealElapsedTime()
    {
        // Retried tests should still report when they actually ran (not when attempt 1 ran).
        var fake = new FakeTimeProvider(frequency: 1_000_000_000L);
        fake.CurrentTimestamp = 1_000_000_000L;
        IExecutionTracker tracker = new ExecutionTracker(fake);

        // First attempt at t=0 relative to suite start
        tracker.CreateExecutionContext("w", attemptNumber: 1);

        // Retry occurs 3 seconds later
        fake.CurrentTimestamp = 4_000_000_000L;
        var retryRecord = tracker.CreateExecutionContext("w", attemptNumber: 2);

        Assert.Equal(TimeSpan.FromSeconds(3), retryRecord.SuiteElapsedTime);
    }

    [Fact]
    public void Clear_ShouldResetSuiteClock_AllowingFreshTrackingFromZero()
    {
        // Arrange
        var fake = new FakeTimeProvider(frequency: 1_000_000_000L);
        fake.CurrentTimestamp = 1_000_000_000L;
        IExecutionTracker tracker = new ExecutionTracker(fake);

        // Establish a suite start, then advance time significantly
        tracker.CreateExecutionContext("w");
        fake.CurrentTimestamp = 100_000_000_000L; // 99 seconds later

        tracker.Clear();

        // After Clear(), the next call should establish a new suite start at t=100s
        // so elapsed should be zero again (start captured and read at same tick)
        var record = tracker.CreateExecutionContext("w");

        Assert.Equal(TimeSpan.Zero, record.SuiteElapsedTime);
    }

    [Fact]
    public void CreateExecutionContext_MultipleWorkers_EachReflectsElapsedFromSameSuiteStart()
    {
        // Arrange — two workers starting at different times, but suite clock is shared
        var fake = new FakeTimeProvider(frequency: 1_000_000_000L);
        fake.CurrentTimestamp = 1_000_000_000L;
        IExecutionTracker tracker = new ExecutionTracker(fake);

        // Worker A starts first — establishes suite start at tick 1_000_000_000
        tracker.CreateExecutionContext("worker-a");

        // 2 seconds later, worker B starts
        fake.CurrentTimestamp = 3_000_000_000L;
        var recordB = tracker.CreateExecutionContext("worker-b");

        // Assert — worker B's elapsed should be measured from the shared suite start, not its own start
        Assert.Equal(TimeSpan.FromSeconds(2), recordB.SuiteElapsedTime);
    }

    /// <summary>
    /// Test double for <see cref="ITimeProvider"/> that allows manual control of the current timestamp.
    /// </summary>
    private sealed class FakeTimeProvider : ITimeProvider
    {
        public long CurrentTimestamp { get; set; }
        public long Frequency { get; }

        public FakeTimeProvider(long frequency = 10_000_000L) // 10 MHz default
        {
            Frequency = frequency;
        }

        public long GetTimestamp() => CurrentTimestamp;
    }
}
