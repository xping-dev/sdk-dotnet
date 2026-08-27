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
        Assemblies = [];
        SessionState = TestSessionState.Initial;
        PullRequestContext = null;
        QuickStatistics = null;
        StatisticsByAssembly = null;
        SdkVersion = XpingVersion.Current;
    }

    /// <summary>
    /// Internal constructor for builder.
    /// </summary>
    internal TestSession(
        Guid sessionId,
        DateTime startedAt,
        EnvironmentInfo environmentInfo,
        IReadOnlyCollection<TestExecution> executions,
        IReadOnlyCollection<string> assemblies,
        DateTime? endedAt,
        int? totalTestsExpected,
        TestSessionState sessionState,
        PullRequestContext? pullRequestContext,
        QuickStatistics? quickStatistics,
        IReadOnlyDictionary<string, AssemblyStatistics>? statisticsByAssembly)
    {
        SessionId = sessionId.RequireCondition(arg => arg != Guid.Empty, "Session ID cannot be empty.");
        StartedAt = startedAt;
        EnvironmentInfo = environmentInfo.RequireNotNull();
        Executions = executions.RequireNotNull();
        Assemblies = [.. assemblies.RequireNotNull()];
        EndedAt = endedAt;
        TotalTestsExpected = totalTestsExpected;
        SessionState = sessionState;
        PullRequestContext = pullRequestContext;
        QuickStatistics = quickStatistics;
        StatisticsByAssembly = statisticsByAssembly;
        SdkVersion = XpingVersion.Current;
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
    /// Gets the distinct test assemblies this session covers, in ordinal order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A session records one test host process, not one test assembly: VSTest batches test projects
    /// sharing a target framework and architecture into a single host. This is the session-level
    /// projection of each execution's <c>Identity.Assembly</c>, and it is what the platform splits a
    /// session into projects by when no project is pinned via <c>ProjectId</c>.
    /// </para>
    /// <para>
    /// Cumulative across the uploads of one session rather than describing a single batch. The
    /// finalizing upload is assembled after the collector has been drained and therefore carries no
    /// executions of its own, so a per-batch value would be empty on exactly the upload that closes
    /// the run — the one that also carries <see cref="QuickStatistics"/>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Assemblies { get; init; }

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

    /// <summary>
    /// Gets <see cref="QuickStatistics"/> broken down by the test assembly each execution belongs
    /// to, keyed by assembly name in ordinal order.
    /// Only populated on the <c>TestSessionState.Finalized</c> upload; <c>null</c> otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="QuickStatistics"/> counts the whole test host process, and a solution-wide
    /// <c>dotnet test</c> batches several test projects into one host — so it is the one part of the
    /// payload that cannot be attributed to any of the <see cref="Assemblies"/>. This is the reading
    /// that can.
    /// </para>
    /// <para>
    /// Only the counters that decompose appear per assembly; see <see cref="AssemblyStatistics"/> for
    /// what is deliberately absent from it and stays on <see cref="QuickStatistics"/> alone. An
    /// execution naming no assembly is counted host-wide only, so the entries can sum to less than
    /// <see cref="QuickStatistics"/> but never to more.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, AssemblyStatistics>? StatisticsByAssembly { get; init; }

    /// <summary>
    /// Gets the version of the Xping SDK that produced this session (e.g. <c>"1.2.3"</c>).
    /// Automatically stamped from assembly metadata — no manual input required.
    /// </summary>
    public string SdkVersion { get; init; }
}
