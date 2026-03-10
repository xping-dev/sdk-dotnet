/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="TestSession"/> instances.
/// </summary>
public sealed class TestSessionBuilder
{
    private Guid _sessionId;
    private DateTime _startedAt;
    private DateTime? _endedAt;
    private EnvironmentInfo _environmentInfo;
    private readonly List<TestExecution> _executions;
    private int? _totalTestsExpected;
    private TestSessionState _sessionState;
    private PullRequestContext? _pullRequestContext;
    private QuickStatistics? _quickStatistics;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestSessionBuilder"/> class.
    /// </summary>
    public TestSessionBuilder()
    {
        _sessionId = Guid.NewGuid();
        _startedAt = DateTime.UtcNow;
        _environmentInfo = new EnvironmentInfo();
        _executions = [];
        _sessionState = TestSessionState.Initial;
        _pullRequestContext = null;
        _quickStatistics = null;
    }

    /// <summary>
    /// Sets the session ID.
    /// </summary>
    /// <param name="sessionId">The unique session identifier.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithSessionId(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        _sessionId = sessionId;
        return this;
    }

    /// <summary>
    /// Sets the session start time.
    /// </summary>
    /// <param name="startedAt">The start time in UTC.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithStartedAt(DateTime startedAt)
    {
        _startedAt = startedAt;
        return this;
    }

    /// <summary>
    /// Sets the session end time.
    /// </summary>
    /// <param name="endedAt">The end time in UTC.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithEndedAt(DateTime? endedAt)
    {
        _endedAt = endedAt;
        return this;
    }

    /// <summary>
    /// Sets the environment information.
    /// </summary>
    /// <param name="environmentInfo">The environment information.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithEnvironmentInfo(EnvironmentInfo environmentInfo)
    {
        _environmentInfo = environmentInfo ?? throw new ArgumentNullException(nameof(environmentInfo));
        return this;
    }

    /// <summary>
    /// Adds a single test execution to the session.
    /// </summary>
    /// <param name="execution">The test execution to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder AddExecution(TestExecution execution)
    {
        if (execution != null)
        {
            _executions.Add(execution);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple test executions to the session.
    /// </summary>
    /// <param name="executions">The test executions to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder AddExecutions(IEnumerable<TestExecution> executions)
    {
        if (executions != null)
        {
            _executions.AddRange(executions.Where(e => e != null));
        }
        return this;
    }

    /// <summary>
    /// Sets the total number of tests expected in this session.
    /// </summary>
    /// <param name="totalTests">The total test count.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithTotalTestsExpected(int? totalTests)
    {
        _totalTestsExpected = totalTests;
        return this;
    }

    /// <summary>
    /// Sets the upload state of the session batch.
    /// </summary>
    /// <param name="state">The session state to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithSessionState(TestSessionState state)
    {
        _sessionState = state;
        return this;
    }

    /// <summary>
    /// Sets the pull request context detected from the CI/CD environment.
    /// Pass <c>null</c> when not running in a PR context.
    /// </summary>
    /// <param name="context">The pull request context, or <c>null</c>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithPullRequestContext(PullRequestContext? context)
    {
        _pullRequestContext = context;
        return this;
    }

    /// <summary>
    /// Sets the pre-calculated quick statistics for the finalized session.
    /// Should only be set on the <see cref="TestSessionState.Finalized"/> upload.
    /// </summary>
    /// <param name="statistics">The accumulated statistics, or <c>null</c> for partial uploads.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder WithQuickStatistics(QuickStatistics? statistics)
    {
        _quickStatistics = statistics;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="TestSession"/> instance.
    /// </summary>
    /// <returns>A new immutable test session.</returns>
    public TestSession Build()
    {
        return new TestSession(
            sessionId: _sessionId,
            startedAt: _startedAt,
            environmentInfo: _environmentInfo,
            executions: _executions.AsReadOnly(),
            endedAt: _endedAt,
            totalTestsExpected: _totalTestsExpected,
            sessionState: _sessionState,
            pullRequestContext: _pullRequestContext,
            quickStatistics: _quickStatistics);
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder Reset()
    {
        _sessionId = Guid.NewGuid();
        _startedAt = DateTime.UtcNow;
        _endedAt = null;
        _environmentInfo = new EnvironmentInfo();
        _executions.Clear();
        _totalTestsExpected = null;
        _sessionState = TestSessionState.Initial;
        _pullRequestContext = null;
        _quickStatistics = null;
        return this;
    }
}
