/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Collection;

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using Xping.Sdk.Core.Models;

/// <summary>
/// Tracks test execution order, parallelization, and suite state across test runs.
/// Thread-safe service enabling detection of order-dependent failures and parallel execution issues.
/// </summary>
public sealed class ExecutionTracker
{
    private readonly ConcurrentDictionary<string, TestExecutionInfo> _previousTests;
    private readonly ConcurrentDictionary<string, int> _workerPositions;
    private int _globalPosition;
    private readonly DateTime _suiteStartTime;
    private readonly string _suiteId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionTracker"/> class.
    /// </summary>
    public ExecutionTracker()
    {
        _previousTests = new ConcurrentDictionary<string, TestExecutionInfo>();
        _workerPositions = new ConcurrentDictionary<string, int>();
        _globalPosition = 0;
        _suiteStartTime = DateTime.UtcNow;
        _suiteId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Gets the unique identifier for this test suite execution session.
    /// </summary>
    public string SuiteId => _suiteId;

    /// <summary>
    /// Gets the start time of the test suite execution.
    /// </summary>
    public DateTime SuiteStartTime => _suiteStartTime;

    /// <summary>
    /// Creates an execution context for a test, tracking its position, previous test, and parallelization state.
    /// </summary>
    /// <param name="workerId">Optional worker identifier (e.g., NUnit WorkerId, XUnit Collection). Falls back to thread ID if null.</param>
    /// <param name="collectionName">Optional collection or fixture name for framework-specific grouping.</param>
    /// <returns>ExecutionContext containing order, parallelization, and suite state information.</returns>
    public Models.ExecutionContext CreateContext(string? workerId = null, string? collectionName = null)
    {
        var threadId = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        var workerKey = workerId ?? threadId;

        // Get position for this worker
        var workerPosition = _workerPositions.AddOrUpdate(
            workerKey,
            _ => 1,
            (_, current) => current + 1);

        // Get global position (approximate in parallel scenarios)
        var globalPos = Interlocked.Increment(ref _globalPosition);

        // Get previous test for this worker
        _previousTests.TryGetValue(workerKey, out var previousTest);

        // Count active workers (approximation of concurrency)
        var concurrentCount = _workerPositions.Count;

        return new Models.ExecutionContext
        {
            PositionInSuite = workerPosition,
            GlobalPosition = globalPos,
            PreviousTestId = previousTest?.TestId,
            PreviousTestName = previousTest?.TestName,
            PreviousTestOutcome = previousTest?.Outcome,
            WasParallelized = concurrentCount > 1,
            ConcurrentTestCount = concurrentCount,
            ThreadId = threadId,
            WorkerId = workerId,
            TestSuiteId = _suiteId,
            SuiteElapsedTime = DateTime.UtcNow - _suiteStartTime,
            CollectionName = collectionName
        };
    }

    /// <summary>
    /// Records a test completion for tracking as the previous test in subsequent executions.
    /// </summary>
    /// <param name="workerId">Optional worker identifier. Falls back to thread ID if null.</param>
    /// <param name="testId">Stable test identifier from TestIdentity.</param>
    /// <param name="testName">Display name of the test.</param>
    /// <param name="outcome">Test outcome (Passed, Failed, Skipped, etc.).</param>
    public void RecordTestCompletion(string? workerId, string testId, string testName, TestOutcome outcome)
    {
        var threadId = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        var workerKey = workerId ?? threadId;

        _previousTests[workerKey] = new TestExecutionInfo
        {
            TestId = testId,
            TestName = testName,
            Outcome = outcome
        };
    }

    /// <summary>
    /// Gets the current global position counter (approximate in parallel scenarios).
    /// </summary>
    public int GlobalPosition => _globalPosition;

    /// <summary>
    /// Gets the number of active workers/threads that have executed at least one test.
    /// </summary>
    public int ActiveWorkerCount => _workerPositions.Count;

    /// <summary>
    /// Gets the position for a specific worker.
    /// </summary>
    /// <param name="workerId">Optional worker identifier. Falls back to thread ID if null.</param>
    /// <returns>Current position for the worker, or 0 if worker hasn't executed any tests.</returns>
    public int GetWorkerPosition(string? workerId = null)
    {
        var threadId = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        var workerKey = workerId ?? threadId;

        return _workerPositions.TryGetValue(workerKey, out var position) ? position : 0;
    }

    /// <summary>
    /// Gets the previous test information for a specific worker.
    /// </summary>
    /// <param name="workerId">Optional worker identifier. Falls back to thread ID if null.</param>
    /// <returns>Previous test information, or null if this is the first test for the worker.</returns>
    public TestExecutionInfo? GetPreviousTest(string? workerId = null)
    {
        var threadId = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        var workerKey = workerId ?? threadId;

        return _previousTests.TryGetValue(workerKey, out var previousTest) ? previousTest : null;
    }

    /// <summary>
    /// Clears all tracking state. Useful for cleanup after test suite completion.
    /// </summary>
    public void Clear()
    {
        _previousTests.Clear();
        _workerPositions.Clear();
        _globalPosition = 0;
    }
}

/// <summary>
/// Information about a test execution for tracking as previous test context.
/// </summary>
public sealed class TestExecutionInfo
{
    /// <summary>
    /// Gets or sets the stable test identifier.
    /// </summary>
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the test display name.
    /// </summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the test outcome.
    /// </summary>
    public TestOutcome Outcome { get; set; }
}
