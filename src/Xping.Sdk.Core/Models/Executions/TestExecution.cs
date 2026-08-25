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
        TimeoutBudget = null;
        TimeoutBudgetSource = null;
        Site = null;
        FailureSiteMember = null;
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
        string? stackTraceHash,
        bool stackTraceOmitted,
        TimeSpan? timeoutBudget,
        TimeoutBudgetSource? timeoutBudgetSource,
        FailureSite? site,
        string? failureSiteMember)
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
        StackTraceOmitted = stackTraceOmitted;
        TimeoutBudget = timeoutBudget;
        TimeoutBudgetSource = timeoutBudgetSource;
        Site = site;
        FailureSiteMember = failureSiteMember;
        TestOrchestrationRecord = testOrchestrationRecord;
        Retry = retry;
    }

    /// <summary>
    /// Gets the unique identifier for this test execution instance.
    /// This changes with each execution of the test.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets the stable test identity that persists across runs.
    /// This is the primary identifier for tracking tests over time.
    /// </summary>
    /// <remarks>
    /// The TestIdentity contains a stable hash-based ID that remains constant
    /// for the same test across different environments, machines, and runs.
    /// Use this for historical analysis and tracking test reliability.
    /// </remarks>
    public TestIdentity Identity { get; init; }

    /// <summary>
    /// Gets the test metadata including categories, tags, and custom attributes.
    /// </summary>
    public TestMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the execution context tracking order, parallelization, and suite state.
    /// </summary>
    /// <remarks>
    /// Enables detection of order-dependent failures, parallel execution issues,
    /// and resource contention patterns. Provides insights into test execution sequence,
    /// previous test information, and parallelization state.
    /// </remarks>
    public TestOrchestrationRecord TestOrchestrationRecord { get; init; }

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
    public RetryMetadata? Retry { get; init; }

    /// <summary>
    /// Gets the display test name including parameters, mainly for debugging.
    /// </summary>
    public string TestName { get; init; }

    /// <summary>
    /// Gets the outcome of the test execution.
    /// </summary>
    public TestOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the duration of the test execution.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the start time of the test execution in UTC.
    /// </summary>
    public DateTime StartTimeUtc { get; init; }

    /// <summary>
    /// Gets the end time of the test execution in UTC.
    /// </summary>
    public DateTime EndTimeUtc { get; init; }

    /// <summary>
    /// Gets the exception type if the test failed due to an exception.
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// Gets the error message if the test failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the stack trace if the test failed.
    /// </summary>
    /// <remarks>
    /// Will be <see langword="null"/> when the test passed, when no stack trace was available,
    /// or when collection was intentionally disabled (see <see cref="StackTraceOmitted"/>).
    /// </remarks>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets a stable hash of the error message for grouping similar failures.
    /// </summary>
    /// <remarks>
    /// This hash enables grouping of test failures with identical or similar error messages,
    /// helping identify common failure patterns across test runs and environments.
    /// The hash is computed using SHA256 for stability and collision resistance.
    /// </remarks>
    public string? ErrorMessageHash { get; init; }

    /// <summary>
    /// Gets a stable hash of the stack trace for grouping similar failures.
    /// </summary>
    /// <remarks>
    /// This hash enables grouping of test failures with identical or similar stack traces,
    /// helping identify common failure locations and patterns in the codebase.
    /// The hash is computed using SHA256 for stability and collision resistance.
    /// Will be <see langword="null"/> when the test passed, when no stack trace was available,
    /// or when collection was intentionally disabled (see <see cref="StackTraceOmitted"/>).
    /// </remarks>
    public string? StackTraceHash { get; init; }

    /// <summary>
    /// Gets a value indicating whether stack trace capture was explicitly disabled
    /// via <see cref="Xping.Sdk.Core.Configuration.XpingConfiguration.CaptureStackTraces"/>.
    /// When <see langword="true"/>, <see cref="StackTrace"/> and <see cref="StackTraceHash"/> are
    /// <see langword="null"/> because the user opted out — not because no stack trace existed.
    /// </summary>
    public bool StackTraceOmitted { get; init; }

    /// <summary>
    /// Gets the timeout the test declared for itself, or <see langword="null"/> when it declared none
    /// or declared an unlimited one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the test framework's own attribute — MSTest's <c>[Timeout]</c>, NUnit's
    /// <c>[Timeout]</c> or <c>[CancelAfter]</c>, xUnit's <c>[Fact(Timeout = …)]</c>. Xping records
    /// what the author declared; it never enforces it. Enforcement belongs to the framework, which
    /// already does it.
    /// </para>
    /// <para>
    /// Its value is in the comparison with <see cref="Duration"/>. A test killed at its ceiling and
    /// a test that failed an assertion both used to look alike; the budget beside the duration is
    /// what turns "it failed" into "it was killed at the 5 s limit it set itself". Check
    /// <see cref="TimeoutBudgetSource"/> to tell a missing declaration from an unlimited one.
    /// </para>
    /// </remarks>
    public TimeSpan? TimeoutBudget { get; init; }

    /// <summary>
    /// Gets where <see cref="TimeoutBudget"/> came from, or <see langword="null"/> when the test
    /// declared no timeout at all.
    /// </summary>
    public TimeoutBudgetSource? TimeoutBudgetSource { get; init; }

    /// <summary>
    /// Gets where in the test lifecycle this execution failed, or <see langword="null"/> when it did
    /// not fail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null and <see cref="FailureSite.Unknown"/> say different things, and the difference
    /// matters to anyone reading the record: null means the question does not apply because nothing
    /// went wrong, while <c>Unknown</c> means it failed and the adapter could not tell where.
    /// </para>
    /// <para>
    /// Its value is in separating one broken lifecycle member from many broken tests. A <c>[SetUp]</c>
    /// that throws is reported once per test that tried to use it, and without this field those
    /// executions are indistinguishable from a class full of genuinely failing tests.
    /// </para>
    /// </remarks>
    public FailureSite? Site { get; init; }

    /// <summary>
    /// Gets the lifecycle member that failed, such as <c>OrdersFixture.OneTimeSetUp</c>, or
    /// <see langword="null"/> when the framework does not name one.
    /// </summary>
    /// <remarks>
    /// Names the member inside the class;
    /// <see cref="TestOrchestrationRecord.CollectionName"/> already carries the class
    /// itself. The pair is what lets a finding say which member to go and fix rather than only that
    /// some shared code broke.
    /// </remarks>
    public string? FailureSiteMember { get; init; }
}
