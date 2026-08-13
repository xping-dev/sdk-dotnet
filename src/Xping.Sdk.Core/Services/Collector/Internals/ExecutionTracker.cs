/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Globalization;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Collector.Internals;

/// <summary>
/// Tracks test execution order, parallelization, and suite state across test runs.
/// Thread-safe service enabling detection of order-dependent failures and parallel execution issues.
/// </summary>
internal sealed class ExecutionTracker : IExecutionTracker
{
    private readonly ConcurrentDictionary<string, PrecedingTestRecord> _previousTests = new();
    private readonly ConcurrentDictionary<string, int> _workerPositions = new();

    // In-flight tests, keyed by worker. Every supported framework runs at most one test per worker
    // at a time, so the worker key doubles as the in-flight slot: a repeated start replaces the
    // slot instead of double counting, and a missing end self-heals on the worker's next test.
    private readonly ConcurrentDictionary<string, InFlightTest> _inFlightTests = new();

    private readonly ITimeProvider _timeProvider;
    private int _globalPosition;
    private long _suiteStartTimestamp; // 0 = not yet captured; Stopwatch timestamps are always positive

    public ExecutionTracker(ITimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    int IExecutionTracker.GlobalPosition => _globalPosition;

    /// <inheritdoc/>
    int IExecutionTracker.ActiveWorkerCount => _workerPositions.Count;

    private static string ResolveWorkerKey(string? workerId) =>
        workerId ?? System.Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);

    private TimeSpan ComputeSuiteElapsedTime()
    {
        long start = Volatile.Read(ref _suiteStartTimestamp);
        long elapsed = _timeProvider.GetTimestamp() - start;
        return TimeSpan.FromTicks(elapsed * TimeSpan.TicksPerSecond / _timeProvider.Frequency);
    }

    /// <inheritdoc/>
    TestOrchestrationRecord IExecutionTracker.CreateExecutionContext(string? workerId, string? collectionName, int attemptNumber)
    {
        // Lazy-capture suite start on the very first call. Stopwatch timestamps are always > 0,
        // so 0 is a safe sentinel for "not yet started". CompareExchange ensures exactly-once
        // initialization even under concurrent entry by multiple workers.
        if (Volatile.Read(ref _suiteStartTimestamp) == 0)
        {
            long captured = _timeProvider.GetTimestamp();
            Interlocked.CompareExchange(ref _suiteStartTimestamp, captured, 0);
        }

        string threadId = System.Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        string workerKey = ResolveWorkerKey(workerId);

        int workerPosition;
        int globalPosition;

        if (attemptNumber > 1)
        {
            // Retry attempt: reuse the position claimed by attempt 1 so that retried tests
            // do not displace the positions of subsequent distinct tests in the suite.
            workerPosition = _workerPositions.TryGetValue(workerKey, out int existing) ? existing : 1;
            globalPosition = Volatile.Read(ref _globalPosition);
        }
        else
        {
            // First attempt: claim a new position slot on this worker and globally.
            workerPosition = _workerPositions.AddOrUpdate(
                workerKey,
                _ => 1,
                updateValueFactory: (_, current) => current + 1);

            globalPosition = Interlocked.Increment(ref _globalPosition);
        }

        // Get a previous test for this worker
        _previousTests.TryGetValue(workerKey, out PrecedingTestRecord? previousTest);

        // Measure actual concurrency. The adapter reports the test end only after this record has
        // been built, so this test is still in flight and counts itself. The slot carries the peak
        // overlap observed during the test, which covers tests that overlapped and already finished.
        // Without a slot for this worker the test's own overlap window is unknown, so report that it
        // ran alone rather than attributing unrelated in-flight tests to it.
        int concurrentCount = _inFlightTests.TryGetValue(workerKey, out InFlightTest? inFlightTest)
            ? Math.Max(1, Volatile.Read(ref inFlightTest.ObservedConcurrency))
            : 1;

        // A local builder: a shared instance would let concurrent workers interleave their
        // Reset()/With...() calls and emit records mixing fields from two different tests.
        TestOrchestrationRecord testOrchestrationRecord = new TestOrchestrationBuilder()
            .WithThreadId(threadId)
            .WithWorkerId(workerKey)
            .WithCollectionName(collectionName)
            .WithParallelization(concurrentCount > 1, concurrentCount)
            .WithPositionInSuite(workerPosition)
            .WithGlobalPosition(globalPosition)
            .WithPreviousTest(previousTest?.TestFingerprint, previousTest?.TestName, previousTest?.Outcome)
            .WithSuiteElapsedTime(ComputeSuiteElapsedTime())
            .Build();

        return testOrchestrationRecord;
    }

    /// <inheritdoc/>
    int IExecutionTracker.RecordTestStart(string? workerId)
    {
        string workerKey = ResolveWorkerKey(workerId);

        InFlightTest inFlightTest = new();
        _inFlightTests[workerKey] = inFlightTest;

        int inFlightCount = _inFlightTests.Count;

        // Publish the new concurrency level to every test currently in flight, including this one,
        // so each test reports the peak overlap it actually experienced rather than the overlap that
        // happened to exist at the moment its record was built.
        foreach (KeyValuePair<string, InFlightTest> entry in _inFlightTests)
        {
            ObserveConcurrency(entry.Value, inFlightCount);
        }

        return inFlightCount;
    }

    /// <inheritdoc/>
    void IExecutionTracker.RecordTestEnd(string? workerId)
    {
        _inFlightTests.TryRemove(ResolveWorkerKey(workerId), out _);
    }

    private static void ObserveConcurrency(InFlightTest inFlightTest, int concurrency)
    {
        int current = Volatile.Read(ref inFlightTest.ObservedConcurrency);

        while (current < concurrency)
        {
            int previous = Interlocked.CompareExchange(ref inFlightTest.ObservedConcurrency, concurrency, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    /// <inheritdoc/>
    void IExecutionTracker.RecordTestCompletion(string? workerId, string testFingerprint, string testName, TestOutcome outcome)
    {
        string workerKey = ResolveWorkerKey(workerId);

        _previousTests[workerKey] = new PrecedingTestRecord
        {
            TestFingerprint = testFingerprint,
            TestName = testName,
            Outcome = outcome
        };
    }

    /// <inheritdoc/>
    int IExecutionTracker.GetWorkerPosition(string? workerId)
    {
        string workerKey = ResolveWorkerKey(workerId);

        return _workerPositions.TryGetValue(workerKey, out int position) ? position : 0;
    }

    /// <inheritdoc/>
    PrecedingTestRecord? IExecutionTracker.GetPreviousTest(string? workerId)
    {
        string workerKey = ResolveWorkerKey(workerId);

        return _previousTests.TryGetValue(workerKey, out PrecedingTestRecord? previousTest) ? previousTest : null;
    }

    /// <inheritdoc/>
    void IExecutionTracker.Clear()
    {
        _previousTests.Clear();
        _workerPositions.Clear();
        _inFlightTests.Clear();
        _globalPosition = 0;
        _suiteStartTimestamp = 0;
    }

    /// <summary>
    /// A test occupying a worker's in-flight slot, carrying the peak concurrency observed while it ran.
    /// </summary>
    private sealed class InFlightTest
    {
        /// <summary>
        /// The highest number of tests seen in flight at any point while this test was running.
        /// Mutated through <see cref="Interlocked"/>, so it cannot be a property.
        /// </summary>
        public int ObservedConcurrency;
    }
}
