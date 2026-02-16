/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="TestSession"/> instances.
/// </summary>
public sealed class TestSessionBuilder
{
    private string _sessionId;
    private DateTime _startedAt;
    private DateTime? _endedAt;
    private EnvironmentInfo _environmentInfo;
    private readonly List<TestExecution> _executions;
    private int? _totalTestsExpected;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestSessionBuilder"/> class.
    /// </summary>
    public TestSessionBuilder()
    {
        _sessionId = Guid.NewGuid().ToString();
        _startedAt = DateTime.UtcNow;
        _environmentInfo = new EnvironmentInfo();
        _executions = [];
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

        _sessionId = sessionId.ToString();
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
            totalTestsExpected: _totalTestsExpected);
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public TestSessionBuilder Reset()
    {
        _sessionId = Guid.NewGuid().ToString();
        _startedAt = DateTime.UtcNow;
        _endedAt = null;
        _environmentInfo = new EnvironmentInfo();
        _executions.Clear();
        _totalTestsExpected = null;
        return this;
    }
}
