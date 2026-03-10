/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Models;

/// <summary>
/// Immutable test session containing environment information and test executions.
/// Use <see cref="Builders.TestSessionBuilder"/> to create instances.
/// </summary>
public sealed class TestSession
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// For creating instances in code, use <see cref="Builders.TestSessionBuilder"/>.
    /// </summary>
    public TestSession()
    {
        SessionId = Guid.Empty;
        StartedAt = DateTime.UtcNow;
        EnvironmentInfo = new EnvironmentInfo();
        Executions = [];
        SessionState = TestSessionState.Initial;
        PullRequestContext = null;
        QuickStatistics = null;
    }

    /// <summary>
    /// Internal constructor for builder.
    /// </summary>
    internal TestSession(
        Guid sessionId,
        DateTime startedAt,
        EnvironmentInfo environmentInfo,
        IReadOnlyCollection<TestExecution> executions,
        DateTime? endedAt,
        int? totalTestsExpected,
        TestSessionState sessionState,
        PullRequestContext? pullRequestContext,
        QuickStatistics? quickStatistics)
    {
        SessionId = sessionId.RequireCondition(arg => arg != Guid.Empty, "Session ID cannot be empty.");
        StartedAt = startedAt;
        EnvironmentInfo = environmentInfo.RequireNotNull();
        Executions = executions.RequireNotNull();
        EndedAt = endedAt;
        TotalTestsExpected = totalTestsExpected;
        SessionState = sessionState;
        PullRequestContext = pullRequestContext;
        QuickStatistics = quickStatistics;
    }

    /// <summary>
    /// Gets the unique identifier for this test session.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Gets when the test session started (UTC).
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets when the test session ended (UTC). Null if still running.
    /// </summary>
    public DateTime? EndedAt { get; init; }

    /// <summary>
    /// Gets the environment information for this test session.
    /// This is shared across all test executions in the session.
    /// </summary>
    public EnvironmentInfo EnvironmentInfo { get; init; }

    /// <summary>
    /// Gets the test executions in this session.
    /// </summary>
    public IReadOnlyCollection<TestExecution> Executions { get; init; }

    /// <summary>
    /// Gets the total number of tests expected in this session.
    /// Useful for tracking session completion progress.
    /// </summary>
    public int? TotalTestsExpected { get; init; }

    /// <summary>
    /// Gets the upload state of this session batch.
    /// The cloud uses this to decide whether to post a PR comment:
    /// only <see cref="TestSessionState.Finalized"/> triggers a comment.
    /// </summary>
    public TestSessionState SessionState { get; init; }

    /// <summary>
    /// Gets the pull request or merge request context detected from the CI/CD environment,
    /// or <c>null</c> when not running inside a PR build or when detection is disabled.
    /// </summary>
    public PullRequestContext? PullRequestContext { get; init; }

    /// <summary>
    /// Gets the pre-calculated test statistics accumulated across all batch uploads.
    /// Only populated on the <c>TestSessionState.Finalized</c> upload; <c>null</c> otherwise.
    /// </summary>
    public QuickStatistics? QuickStatistics { get; init; }
}
