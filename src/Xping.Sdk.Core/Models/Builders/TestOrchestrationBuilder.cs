/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="TestOrchestrationRecord"/> instances.
/// </summary>
public sealed class TestOrchestrationBuilder
{
    private int _positionInSuite;
    private int? _globalPosition;
    private string? _previousTestId;
    private string? _previousTestName;
    private TestOutcome? _previousTestOutcome;
    private bool _wasParallelized;
    private int _concurrentTestCount;
    private string _threadId;
    private string _workerId;
    private TimeSpan _suiteElapsedTime;
    private string? _collectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestOrchestrationBuilder"/> class.
    /// </summary>
    public TestOrchestrationBuilder()
    {
        _positionInSuite = 0;
        _concurrentTestCount = 1;
        _threadId = string.Empty;
        _workerId = string.Empty;
        _suiteElapsedTime = TimeSpan.Zero;
        _collectionName = null;
    }

    /// <summary>
    /// Sets the position in a suite.
    /// </summary>
    public TestOrchestrationBuilder WithPositionInSuite(int positionInSuite)
    {
        _positionInSuite = positionInSuite;
        return this;
    }

    /// <summary>
    /// Sets the global position.
    /// </summary>
    public TestOrchestrationBuilder WithGlobalPosition(int? globalPosition)
    {
        _globalPosition = globalPosition;
        return this;
    }

    /// <summary>
    /// Sets information about the previous test.
    /// </summary>
    public TestOrchestrationBuilder WithPreviousTest(string? testId, string? testName, TestOutcome? outcome)
    {
        _previousTestId = testId;
        _previousTestName = testName;
        _previousTestOutcome = outcome;
        return this;
    }

    /// <summary>
    /// Sets the previous test ID.
    /// </summary>
    public TestOrchestrationBuilder WithPreviousTestId(string? previousTestId)
    {
        _previousTestId = previousTestId;
        return this;
    }

    /// <summary>
    /// Sets the previous test name.
    /// </summary>
    public TestOrchestrationBuilder WithPreviousTestName(string? previousTestName)
    {
        _previousTestName = previousTestName;
        return this;
    }

    /// <summary>
    /// Sets the previous test outcome.
    /// </summary>
    public TestOrchestrationBuilder WithPreviousTestOutcome(TestOutcome? previousTestOutcome)
    {
        _previousTestOutcome = previousTestOutcome;
        return this;
    }

    /// <summary>
    /// Sets parallelization information.
    /// </summary>
    public TestOrchestrationBuilder WithParallelization(bool wasParallelized, int concurrentTestCount)
    {
        _wasParallelized = wasParallelized;
        _concurrentTestCount = concurrentTestCount;
        return this;
    }

    /// <summary>
    /// Sets whether the test was parallelized.
    /// </summary>
    public TestOrchestrationBuilder WithWasParallelized(bool wasParallelized)
    {
        _wasParallelized = wasParallelized;
        return this;
    }

    /// <summary>
    /// Sets the concurrent test count.
    /// </summary>
    public TestOrchestrationBuilder WithConcurrentTestCount(int concurrentTestCount)
    {
        _concurrentTestCount = concurrentTestCount;
        return this;
    }

    /// <summary>
    /// Sets the thread ID.
    /// </summary>
    public TestOrchestrationBuilder WithThreadId(string threadId)
    {
        _threadId = threadId;
        return this;
    }

    /// <summary>
    /// Sets the worker ID.
    /// </summary>
    public TestOrchestrationBuilder WithWorkerId(string workerId)
    {
        _workerId = workerId;
        return this;
    }

    /// <summary>
    /// Sets the suite elapsed time.
    /// </summary>
    public TestOrchestrationBuilder WithSuiteElapsedTime(TimeSpan suiteElapsedTime)
    {
        _suiteElapsedTime = suiteElapsedTime;
        return this;
    }

    /// <summary>
    /// Sets the collection name.
    /// </summary>
    public TestOrchestrationBuilder WithCollectionName(string? collectionName)
    {
        _collectionName = collectionName;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="TestOrchestrationRecord"/> instance.
    /// </summary>
    public TestOrchestrationRecord Build()
    {
        return new TestOrchestrationRecord(
            positionInSuite: _positionInSuite,
            globalPosition: _globalPosition,
            previousTestId: _previousTestId,
            previousTestName: _previousTestName,
            previousTestOutcome: _previousTestOutcome,
            wasParallelized: _wasParallelized,
            concurrentTestCount: _concurrentTestCount,
            threadId: _threadId,
            workerId: _workerId,
            suiteElapsedTime: _suiteElapsedTime,
            collectionName: _collectionName);
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    public TestOrchestrationBuilder Reset()
    {
        _positionInSuite = 0;
        _globalPosition = null;
        _previousTestId = null;
        _previousTestName = null;
        _previousTestOutcome = null;
        _wasParallelized = false;
        _concurrentTestCount = 1;
        _threadId = string.Empty;
        _workerId = string.Empty;
        _suiteElapsedTime = TimeSpan.Zero;
        _collectionName = string.Empty;
        return this;
    }
}
