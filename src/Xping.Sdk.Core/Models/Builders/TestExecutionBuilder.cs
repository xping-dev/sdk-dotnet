/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="TestExecution"/> instances.
/// </summary>
public sealed class TestExecutionBuilder
{
    private Guid _executionId;
    private TestIdentity _identity;
    private string _testName;
    private TestOutcome _outcome;
    private TimeSpan _duration;
    private DateTime _startTimeUtc;
    private DateTime _endTimeUtc;
    private TestMetadata _metadata;
    private TestOrchestrationRecord _testOrchestrationRecord;
    private RetryMetadata? _retry;
    private string? _exceptionType;
    private string? _errorMessage;
    private string? _stackTrace;
    private string? _errorMessageHash;
    private string? _stackTraceHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestExecutionBuilder"/> class.
    /// </summary>
    public TestExecutionBuilder()
    {
        _executionId = Guid.NewGuid();
        _identity = new TestIdentity();
        _metadata = new TestMetadata();
        _testOrchestrationRecord = new TestOrchestrationRecord();
        _retry = new RetryMetadata();
        _testName = string.Empty;
        _outcome = TestOutcome.NotExecuted;
        _duration = TimeSpan.Zero;
        _startTimeUtc = DateTime.UtcNow;
        _endTimeUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the execution ID.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithExecutionId(Guid executionId)
    {
        _executionId = executionId;
        return this;
    }

    /// <summary>
    /// Sets the test identity.
    /// </summary>
    /// <param name="identity">The test identity.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithIdentity(TestIdentity identity)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        return this;
    }

    /// <summary>
    /// Sets the test name.
    /// </summary>
    /// <param name="testName">The test name.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithTestName(string testName)
    {
        _testName = testName ?? throw new ArgumentNullException(nameof(testName));
        return this;
    }

    /// <summary>
    /// Sets the test outcome.
    /// </summary>
    /// <param name="outcome">The test outcome.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithOutcome(TestOutcome outcome)
    {
        _outcome = outcome;
        return this;
    }

    /// <summary>
    /// Sets the test duration.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithDuration(TimeSpan duration)
    {
        _duration = duration;
        return this;
    }

    /// <summary>
    /// Sets the start time.
    /// </summary>
    /// <param name="startTimeUtc">The start time in UTC.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithStartTime(DateTime startTimeUtc)
    {
        _startTimeUtc = startTimeUtc;
        return this;
    }

    /// <summary>
    /// Sets the end time.
    /// </summary>
    /// <param name="endTimeUtc">The end time in UTC.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithEndTime(DateTime endTimeUtc)
    {
        _endTimeUtc = endTimeUtc;
        return this;
    }

    /// <summary>
    /// Sets the start and end times and calculates duration automatically.
    /// </summary>
    /// <param name="startTimeUtc">The start time in UTC.</param>
    /// <param name="endTimeUtc">The end time in UTC.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithTimeRange(DateTime startTimeUtc, DateTime endTimeUtc)
    {
        _startTimeUtc = startTimeUtc;
        _endTimeUtc = endTimeUtc;
        _duration = endTimeUtc - startTimeUtc;
        return this;
    }

    /// <summary>
    /// Sets the test metadata.
    /// </summary>
    /// <param name="metadata">The test metadata.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithMetadata(TestMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>
    /// Sets exception information for a failed test.
    /// </summary>
    /// <param name="exceptionType">The exception type.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="stackTrace">The stack trace.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithException(string? exceptionType, string? errorMessage, string? stackTrace = null)
    {
        _exceptionType = exceptionType;
        _errorMessage = errorMessage;
        _stackTrace = stackTrace;
        return this;
    }

    /// <summary>
    /// Sets the error message hash.
    /// </summary>
    /// <param name="errorMessageHash">The error message hash.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithErrorMessageHash(string? errorMessageHash)
    {
        _errorMessageHash = errorMessageHash;
        return this;
    }

    /// <summary>
    /// Sets the stack trace hash.
    /// </summary>
    /// <param name="stackTraceHash">The stack trace hash.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithStackTraceHash(string? stackTraceHash)
    {
        _stackTraceHash = stackTraceHash;
        return this;
    }

    /// <summary>
    /// Sets the test orchestration.
    /// </summary>
    /// <param name="orchestrationRecord">The orchestration record.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithTestOrchestrationRecord(TestOrchestrationRecord orchestrationRecord)
    {
        _testOrchestrationRecord = orchestrationRecord;
        return this;
    }

    /// <summary>
    /// Sets the retry metadata.
    /// </summary>
    /// <param name="retry">The retry metadata.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder WithRetry(RetryMetadata? retry)
    {
        _retry = retry;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="TestExecution"/> instance.
    /// </summary>
    /// <returns>A new immutable test execution.</returns>
    public TestExecution Build()
    {
        return new TestExecution(
            executionId: _executionId,
            identity: _identity,
            testName: _testName,
            outcome: _outcome,
            duration: _duration,
            startTimeUtc: _startTimeUtc,
            endTimeUtc: _endTimeUtc,
            metadata: _metadata,
            exceptionType: _exceptionType,
            errorMessage: _errorMessage,
            stackTrace: _stackTrace,
            errorMessageHash: _errorMessageHash,
            stackTraceHash: _stackTraceHash,
            testOrchestrationRecord: _testOrchestrationRecord,
            retry: _retry);
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public TestExecutionBuilder Reset()
    {
        _executionId = Guid.NewGuid();
        _identity = new TestIdentity();
        _testName = string.Empty;
        _outcome = TestOutcome.NotExecuted;
        _duration = TimeSpan.Zero;
        _startTimeUtc = DateTime.UtcNow;
        _endTimeUtc = DateTime.UtcNow;
        _metadata = new TestMetadata();
        _testOrchestrationRecord = new TestOrchestrationRecord();
        _retry = new RetryMetadata();
        _exceptionType = null;
        _errorMessage = null;
        _stackTrace = null;
        _errorMessageHash = null;
        _stackTraceHash = null;
        return this;
    }
}
