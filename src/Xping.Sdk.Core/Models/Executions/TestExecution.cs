/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Immutable test execution record with all associated metadata and results.
/// Use <see cref="Builders.TestExecutionBuilder"/> to create instances.
/// </summary>
public sealed class TestExecution
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// For creating instances in code, use <see cref="Builders.TestExecutionBuilder"/>.
    /// </summary>
    public TestExecution()
    {
        ExecutionId = Guid.NewGuid();
        Identity = new TestIdentity();
        Metadata = new TestMetadata();
        TestOrchestrationRecord = new TestOrchestrationRecord();
        Retry = new RetryMetadata();
        Outcome = TestOutcome.NotExecuted;
        Duration = TimeSpan.Zero;
        TestName = string.Empty;
        StartTimeUtc = DateTime.UtcNow;
        EndTimeUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Internal constructor for builder.
    /// </summary>
    internal TestExecution(
        Guid executionId,
        TestIdentity identity,
        TestMetadata metadata,
        TestOrchestrationRecord testOrchestrationRecord,
        string testName,
        TestOutcome outcome,
        TimeSpan duration,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        RetryMetadata? retry,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        string? errorMessageHash,
        string? stackTraceHash)
    {
        ExecutionId = executionId;
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        TestName = testName ?? throw new ArgumentNullException(nameof(testName));
        Metadata = metadata;
        Outcome = outcome;
        Duration = duration;
        StartTimeUtc = startTimeUtc;
        EndTimeUtc = endTimeUtc;
        ExceptionType = exceptionType;
        ErrorMessage = errorMessage;
        StackTrace = stackTrace;
        ErrorMessageHash = errorMessageHash;
        StackTraceHash = stackTraceHash;
        TestOrchestrationRecord = testOrchestrationRecord;
        Retry = retry;
    }

    /// <summary>
    /// Gets the unique identifier for this test execution instance.
    /// This changes with each execution of the test.
    /// </summary>
    public Guid ExecutionId { get; private set; }

    /// <summary>
    /// Gets the stable test identity that persists across runs.
    /// This is the primary identifier for tracking tests over time.
    /// </summary>
    /// <remarks>
    /// The TestIdentity contains a stable hash-based ID that remains constant
    /// for the same test across different environments, machines, and runs.
    /// Use this for historical analysis and tracking test reliability.
    /// </remarks>
    public TestIdentity Identity { get; private set; }

    /// <summary>
    /// Gets the test metadata including categories, tags, and custom attributes.
    /// </summary>
    public TestMetadata Metadata { get; private set; }

    /// <summary>
    /// Gets the execution context tracking order, parallelization, and suite state.
    /// </summary>
    /// <remarks>
    /// Enables detection of order-dependent failures, parallel execution issues,
    /// and resource contention patterns. Provides insights into test execution sequence,
    /// previous test information, and parallelization state.
    /// </remarks>
    public TestOrchestrationRecord TestOrchestrationRecord { get; private set; }

    /// <summary>
    /// Gets retry metadata if the test is configured for retry.
    /// </summary>
    /// <remarks>
    /// Null if the test does not have retry configuration.
    /// Contains retry attempt information, max retries, and retry strategy details
    /// when the test is executed with a retry mechanism.
    /// Helps identify flaky tests that pass only after retry attempts and
    /// enables analysis of retry patterns and test reliability issues.
    /// </remarks>
    public RetryMetadata? Retry { get; private set; }

    /// <summary>
    /// Gets the display test name including parameters, mainly for debugging.
    /// </summary>
    public string TestName { get; private set; }

    /// <summary>
    /// Gets the outcome of the test execution.
    /// </summary>
    public TestOutcome Outcome { get; private set; }

    /// <summary>
    /// Gets the duration of the test execution.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Gets the start time of the test execution in UTC.
    /// </summary>
    public DateTime StartTimeUtc { get; private set; }

    /// <summary>
    /// Gets the end time of the test execution in UTC.
    /// </summary>
    public DateTime EndTimeUtc { get; private set; }

    /// <summary>
    /// Gets the exception type if the test failed due to an exception.
    /// </summary>
    public string? ExceptionType { get; private set; }

    /// <summary>
    /// Gets the error message if the test failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the stack trace if the test failed.
    /// </summary>
    public string? StackTrace { get; private set; }

    /// <summary>
    /// Gets a stable hash of the error message for grouping similar failures.
    /// </summary>
    /// <remarks>
    /// This hash enables grouping of test failures with identical or similar error messages,
    /// helping identify common failure patterns across test runs and environments.
    /// The hash is computed using SHA256 for stability and collision resistance.
    /// </remarks>
    public string? ErrorMessageHash { get; private set; }

    /// <summary>
    /// Gets a stable hash of the stack trace for grouping similar failures.
    /// </summary>
    /// <remarks>
    /// This hash enables grouping of test failures with identical or similar stack traces,
    /// helping identify common failure locations and patterns in the codebase.
    /// The hash is computed using SHA256 for stability and collision resistance.
    /// </remarks>
    public string? StackTraceHash { get; private set; }
}
