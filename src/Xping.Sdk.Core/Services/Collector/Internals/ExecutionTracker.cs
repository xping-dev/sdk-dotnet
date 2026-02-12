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
    private int _globalPosition;

    /// <inheritdoc/>
    int IExecutionTracker.GlobalPosition => _globalPosition;

    /// <inheritdoc/>
    int IExecutionTracker.ActiveWorkerCount => _workerPositions.Count;

    /// <inheritdoc/>
    TestOrchestrationRecord IExecutionTracker.CreateExecutionContext(string? workerId, string? collectionName)
    {
        string threadId = System.Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        string workerKey = workerId ?? threadId;

        // Get a position for this worker
        int workerPosition = _workerPositions.AddOrUpdate(
            workerKey,
            _ => 1,
            updateValueFactory: (_, current) => current + 1);

        // Get global position (approximate in parallel scenarios)
        int globalPosition = Interlocked.Increment(ref _globalPosition);

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
            .WithPreviousTest(previousTest?.TestId, previousTest?.TestName, previousTest?.Outcome)
            .Build();

        return testOrchestrationRecord;
    }

    /// <inheritdoc/>
    void IExecutionTracker.RecordTestCompletion(string? workerId, string testId, string testName, TestOutcome outcome)
    {
        string threadId = System.Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        string workerKey = workerId ?? threadId;

        _previousTests[workerKey] = new PrecedingTestRecord
        {
            TestId = testId,
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
    }
}
