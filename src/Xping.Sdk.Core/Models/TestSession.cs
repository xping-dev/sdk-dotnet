/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;

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
        SessionId = Guid.Empty.ToString();
        StartedAt = DateTime.UtcNow;
        EnvironmentInfo = new EnvironmentInfo();
        Executions = [];
    }

    /// <summary>
    /// Internal constructor for builder.
    /// </summary>
    internal TestSession(
        string sessionId,
        DateTime startedAt,
        EnvironmentInfo environmentInfo,
        IReadOnlyCollection<TestExecution> executions,
        DateTime? endedAt,
        int? totalTestsExpected)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        StartedAt = startedAt;
        EnvironmentInfo = environmentInfo ?? throw new ArgumentNullException(nameof(environmentInfo));
        Executions = executions ?? throw new ArgumentNullException(nameof(executions));
        EndedAt = endedAt;
        TotalTestsExpected = totalTestsExpected;
    }

    /// <summary>
    /// Gets the unique identifier for this test session.
    /// </summary>
    public string SessionId { get; private set; }

    /// <summary>
    /// Gets when the test session started (UTC).
    /// </summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>
    /// Gets when the test session ended (UTC). Null if still running.
    /// </summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>
    /// Gets the environment information for this test session.
    /// This is shared across all test executions in the session.
    /// </summary>
    public EnvironmentInfo EnvironmentInfo { get; private set; }

    /// <summary>
    /// Gets the test executions in this session.
    /// </summary>
    public IReadOnlyCollection<TestExecution> Executions { get; private set; }

    /// <summary>
    /// Gets the total number of tests expected in this session.
    /// Useful for tracking session completion progress.
    /// </summary>
    public int? TotalTestsExpected { get; private set; }
}
