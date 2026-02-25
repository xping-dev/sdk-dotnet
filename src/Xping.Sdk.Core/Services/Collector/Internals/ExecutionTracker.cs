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
    private readonly TestOrchestrationBuilder _builder = new();
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
        string workerKey = workerId ?? threadId;

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

        // Count active workers (approximation of concurrency)
        int concurrentCount = _workerPositions.Count;

        TestOrchestrationRecord testOrchestrationRecord = _builder
            .Reset()
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
    void IExecutionTracker.RecordTestCompletion(string? workerId, string testFingerprint, string testName, TestOutcome outcome)
    {
        string threadId = System.Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        string workerKey = workerId ?? threadId;

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
        string threadId = System.Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        string workerKey = workerId ?? threadId;

        return _workerPositions.TryGetValue(workerKey, out int position) ? position : 0;
    }

    /// <inheritdoc/>
    PrecedingTestRecord? IExecutionTracker.GetPreviousTest(string? workerId)
    {
        string threadId = System.Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        string workerKey = workerId ?? threadId;

        return _previousTests.TryGetValue(workerKey, out PrecedingTestRecord? previousTest) ? previousTest : null;
    }

    /// <inheritdoc/>
    void IExecutionTracker.Clear()
    {
        _previousTests.Clear();
        _workerPositions.Clear();
        _globalPosition = 0;
        _suiteStartTimestamp = 0;
        _builder.Reset();
    }
}
