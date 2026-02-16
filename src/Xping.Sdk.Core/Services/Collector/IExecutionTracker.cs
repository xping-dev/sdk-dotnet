/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Collector;

/// <summary>
/// Defines a contract for tracking test execution order, parallelization, and suite state across test runs.
/// </summary>
public interface IExecutionTracker
{
    /// <summary>
    /// Gets the current global position counter across all workers.
    /// The value is approximate in parallel execution scenarios.
    /// </summary>
    int GlobalPosition { get; }

    /// <summary>
    /// Gets the number of workers or threads that have executed at least one test.
    /// </summary>
    int ActiveWorkerCount { get; }

    /// <summary>
    /// Creates an execution context for a test, capturing its global and per-worker position,
    /// the previously executed test on the same worker, and the current parallelization state.
    /// </summary>
    /// <param name="workerId">
    /// Optional worker identifier (e.g., NUnit WorkerId, xUnit collection name).
    /// Falls back to the current managed thread ID if <see langword="null"/>.
    /// </param>
    /// <param name="collectionName">Optional collection or fixture name for framework-specific grouping.</param>
    /// <param name="attemptNumber">
    /// The 1-based attempt number for this test execution. When greater than 1 (i.e., a retry),
    /// position counters are not incremented — the record reuses the position claimed by the
    /// first attempt so that retried tests do not shift the positions of subsequent tests.
    /// </param>
    /// <returns>
    /// An <see cref="TestOrchestrationRecord"/> containing order, parallelization, and suite state information.
    /// </returns>
    TestOrchestrationRecord CreateExecutionContext(string? workerId = null, string? collectionName = null, int attemptNumber = 1);

    /// <summary>
    /// Records a completed test so it can be referenced as the previous test in subsequent executions
    /// on the same worker.
    /// </summary>
    /// <param name="workerId">
    /// Optional worker identifier. Falls back to the current managed thread ID if <see langword="null"/>.
    /// </param>
    /// <param name="testId">Stable test identifier from <c>TestIdentity</c>.</param>
    /// <param name="testName">Display the name of the completed test.</param>
    /// <param name="outcome">Outcome of the completed test.</param>
    void RecordTestCompletion(string? workerId, string testId, string testName, TestOutcome outcome);

    /// <summary>
    /// Returns the current execution position for the specified worker.
    /// </summary>
    /// <param name="workerId">
    /// Optional worker identifier. Falls back to the current managed thread ID if <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The number of tests the worker has executed, or <c>0</c> if the worker has not executed any tests.
    /// </returns>
    int GetWorkerPosition(string? workerId = null);

    /// <summary>
    /// Returns information about the most recently completed test on the specified worker.
    /// </summary>
    /// <param name="workerId">
    /// Optional worker identifier. Falls back to the current managed thread ID if <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="PrecedingTestRecord"/> for the previous test, or <see langword="null"/> if no test
    /// has completed on this worker yet.
    /// </returns>
    PrecedingTestRecord? GetPreviousTest(string? workerId = null);

    /// <summary>
    /// Resets all tracking state. Should be called after test suite completion to release accumulated state.
    /// </summary>
    void Clear();
}
