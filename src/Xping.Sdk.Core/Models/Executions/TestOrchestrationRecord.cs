/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Immutable execution context tracking test order, parallelization, and suite state.
/// Enables detection of order-dependent failures, parallel execution issues, and resource contention patterns.
/// </summary>
public sealed class TestOrchestrationRecord
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// </summary>
    public TestOrchestrationRecord()
    {
        ThreadId = string.Empty;
        WorkerId = string.Empty;
        CollectionName = string.Empty;
    }

    /// <summary>
    /// Internal constructor for manual construction.
    /// </summary>
    internal TestOrchestrationRecord(
        int positionInSuite,
        int? globalPosition,
        string? previousTestId,
        string? previousTestName,
        TestOutcome? previousTestOutcome,
        bool wasParallelized,
        int concurrentTestCount,
        string threadId,
        string workerId,
        TimeSpan suiteElapsedTime,
        string? collectionName)
    {
        PositionInSuite = positionInSuite;
        GlobalPosition = globalPosition;
        PreviousTestId = previousTestId;
        PreviousTestName = previousTestName;
        PreviousTestOutcome = previousTestOutcome;
        WasParallelized = wasParallelized;
        ConcurrentTestCount = concurrentTestCount;
        ThreadId = threadId;
        WorkerId = workerId;
        SuiteElapsedTime = suiteElapsedTime;
        CollectionName = collectionName;
    }

    // Test Ordering

    /// <summary>
    /// Gets the position of this test in the execution order (1-based).
    /// In parallel execution, this is the position within the thread/worker.
    /// </summary>
    public int PositionInSuite { get; private set; }

    /// <summary>
    /// Gets the global position across all threads (the best effort in parallel scenarios).
    /// May be approximate when tests run in parallel across multiple workers.
    /// </summary>
    public int? GlobalPosition { get; private set; }

    /// <summary>
    /// Gets the stable Test ID of the test that executed immediately before this one.
    /// Null for the first test. In parallel execution, this is per-worker.
    /// </summary>
    public string? PreviousTestId { get; private set; }

    /// <summary>
    /// Gets the display name of the previous test for debugging.
    /// Null for the first test.
    /// </summary>
    public string? PreviousTestName { get; private set; }

    /// <summary>
    /// Gets the outcome of the previous test (Passed, Failed, Skipped, etc.).
    /// Null for the first test.
    /// </summary>
    public TestOutcome? PreviousTestOutcome { get; private set; }

    // Parallelization

    /// <summary>
    /// Gets a value indicating whether this test was executed in parallel with other tests.
    /// </summary>
    public bool WasParallelized { get; private set; }

    /// <summary>
    /// Gets the number of tests executing concurrently at the time this test started.
    /// 1 for sequential execution.
    /// </summary>
    public int ConcurrentTestCount { get; private set; }

    /// <summary>
    /// Gets the thread ID or Worker ID executing this test.
    /// Useful for correlating tests that share the same execution thread.
    /// </summary>
    public string ThreadId { get; private set; }

    /// <summary>
    /// Gets the framework-specific worker identifier (e.g., NUnit WorkerId, XUnit Collection).
    /// </summary>
    public string WorkerId { get; private set; }

    // Test Suite Context

    /// <summary>
    /// Gets the time elapsed since the test suite started executing.
    /// Useful for detecting "late in suite" failures.
    /// </summary>
    public TimeSpan SuiteElapsedTime { get; private set; }

    /// <summary>
    /// Gets the optional framework-specific collection or fixture name.
    /// XUnit: Collection name, NUnit: Fixture name, MSTest: Test class name.
    /// </summary>
    public string? CollectionName { get; private set; }
}
