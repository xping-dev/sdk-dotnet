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
    /// <remarks>
    /// Parallelization is measured from the test start and end notifications reported through
    /// <see cref="RecordTestStart"/> and <see cref="RecordTestEnd"/>. Callers that never report a
    /// test start always get <see cref="TestOrchestrationRecord.ConcurrentTestCount"/> of <c>1</c>.
    /// </remarks>
    TestOrchestrationRecord CreateExecutionContext(string? workerId = null, string? collectionName = null, int attemptNumber = 1);

    /// <summary>
    /// Signals that a test has started executing on the specified worker, so that the tests it
    /// overlaps with can be measured.
    /// </summary>
    /// <param name="workerId">
    /// Optional worker identifier (e.g., NUnit WorkerId, xUnit collection name, MSTest thread ID).
    /// Falls back to the current managed thread ID if <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The number of tests in flight after this test was registered, including this test.
    /// Always at least <c>1</c>.
    /// </returns>
    /// <remarks>
    /// Each worker acts as a single in-flight slot, because every supported test framework runs at
    /// most one test per worker at a time. Calling this method twice for the same worker without an
    /// intervening <see cref="RecordTestEnd"/> therefore replaces the previous slot instead of
    /// counting the test twice, which also self-heals a missing end notification.
    /// </remarks>
    int RecordTestStart(string? workerId = null);

    /// <summary>
    /// Signals that the test running on the specified worker has finished, releasing its in-flight slot.
    /// </summary>
    /// <param name="workerId">
    /// Optional worker identifier. Falls back to the current managed thread ID if <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// Call this only after the test's <see cref="CreateExecutionContext"/> record has been built, so
    /// that the test is counted in its own <see cref="TestOrchestrationRecord.ConcurrentTestCount"/>.
    /// Calling it for a worker with no test in flight is a no-op, so it is safe to invoke from a
    /// <c>finally</c> block.
    /// </remarks>
    void RecordTestEnd(string? workerId = null);

    /// <summary>
    /// Records a completed test so it can be referenced as the previous test in subsequent executions
    /// on the same worker.
    /// </summary>
    /// <param name="workerId">
    /// Optional worker identifier. Falls back to the current managed thread ID if <see langword="null"/>.
    /// </param>
    /// <param name="testFingerprint">Stable test identifier from <c>TestIdentity</c>.</param>
    /// <param name="testName">Display the name of the completed test.</param>
    /// <param name="outcome">Outcome of the completed test.</param>
    void RecordTestCompletion(string? workerId, string testFingerprint, string testName, TestOutcome outcome);

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
