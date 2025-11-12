namespace Xping.Sdk.Core.Models;

/// <summary>
/// Execution context tracking test order, parallelization, and suite state.
/// Enables detection of order-dependent failures, parallel execution issues, and resource contention patterns.
/// </summary>
public class ExecutionContext
{
    // Test Ordering

    /// <summary>
    /// Position of this test in the execution order (1-based).
    /// In parallel execution, this is the position within the thread/worker.
    /// </summary>
    public int PositionInSuite { get; set; }

    /// <summary>
    /// Global position across all threads (best effort in parallel scenarios).
    /// May be approximate when tests run in parallel across multiple workers.
    /// </summary>
    public int? GlobalPosition { get; set; }

    /// <summary>
    /// Stable Test ID of the test that executed immediately before this one.
    /// Null for the first test. In parallel execution, this is per-worker.
    /// </summary>
    public string? PreviousTestId { get; set; }

    /// <summary>
    /// Display name of the previous test for debugging.
    /// </summary>
    public string? PreviousTestName { get; set; }

    /// <summary>
    /// Outcome of the previous test (Passed, Failed, Skipped, etc.).
    /// </summary>
    public TestOutcome? PreviousTestOutcome { get; set; }

    // Parallelization

    /// <summary>
    /// Whether this test was executed in parallel with other tests.
    /// </summary>
    public bool WasParallelized { get; set; }

    /// <summary>
    /// Number of tests executing concurrently at the time this test started.
    /// 1 for sequential execution.
    /// </summary>
    public int ConcurrentTestCount { get; set; }

    /// <summary>
    /// Thread ID or Worker ID executing this test.
    /// Useful for correlating tests that share the same execution thread.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Framework-specific worker identifier (e.g., NUnit WorkerId, XUnit Collection).
    /// </summary>
    public string? WorkerId { get; set; }

    // Test Suite Context

    /// <summary>
    /// Unique identifier for this test run/session.
    /// All tests in the same run share this ID.
    /// </summary>
    public string TestSuiteId { get; set; } = string.Empty;

    /// <summary>
    /// Total number of tests in this suite (if known).
    /// May be null if framework doesn't expose this information.
    /// </summary>
    public int? TotalTestsInSuite { get; set; }

    /// <summary>
    /// Time elapsed since the test suite started executing.
    /// Useful for detecting "late in suite" failures.
    /// </summary>
    public TimeSpan SuiteElapsedTime { get; set; }

    /// <summary>
    /// Framework-specific collection or fixture name.
    /// XUnit: Collection name, NUnit: Fixture name, MSTest: Test class name.
    /// </summary>
    public string? CollectionName { get; set; }
}
