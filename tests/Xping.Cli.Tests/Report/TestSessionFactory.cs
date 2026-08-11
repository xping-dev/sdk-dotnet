/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Store;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Builds sessions for analysis tests.
/// </summary>
/// <remarks>
/// Everything is explicit and deterministic — fixed timestamps, fixed ids, no clock reads. Analysis
/// is required to produce byte-identical output over unchanged input, so a fixture that varied per
/// run would make that property untestable.
/// </remarks>
internal static class TestSessionFactory
{
    /// <summary>The instant the oldest fixture session starts at.</summary>
    public static readonly DateTime Epoch = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>The assembly fixture tests belong to unless told otherwise.</summary>
    public const string DefaultAssembly = "MyApp.Tests";

    /// <summary>
    /// Builds a session id that is stable for a given ordinal.
    /// </summary>
    /// <param name="ordinal">The session's position; 0 is the oldest.</param>
    /// <returns>The id.</returns>
    /// <remarks>
    /// Offset by one so that ordinal zero is not the empty GUID, which the session builder rejects.
    /// </remarks>
    public static Guid SessionIdFor(int ordinal) =>
        new($"00000000-0000-0000-0000-{ordinal + 1:D12}");

    /// <summary>
    /// Builds one execution.
    /// </summary>
    /// <param name="name">Method name; also seeds the fingerprint.</param>
    /// <param name="outcome">How it ended.</param>
    /// <param name="assembly">Owning assembly.</param>
    /// <param name="durationMs">How long it took.</param>
    /// <param name="attempt">Retry attempt number.</param>
    /// <returns>The execution.</returns>
    public static TestExecution Execution(
        string name,
        TestOutcome outcome = TestOutcome.Passed,
        string assembly = DefaultAssembly,
        int durationMs = 100,
        int attempt = 1)
    {
        TestIdentity identity = new TestIdentityBuilder()
            .WithTestFingerprint($"fp-{name}")
            .WithFullyQualifiedName($"MyApp.Tests.SampleTests.{name}")
            .WithAssembly(assembly)
            .WithNamespace("MyApp.Tests")
            .WithClassName("SampleTests")
            .WithMethodName(name)
            .WithDisplayName(name)
            .WithSourceFile("SampleTests.cs")
            .WithSourceLineNumber(10)
            .Build();

        RetryMetadata retry = new RetryMetadataBuilder()
            .WithAttemptNumber(attempt)
            .WithMaxRetries(0)
            .WithPassedOnRetry(false)
            .Build();

        return new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(name)
            .WithOutcome(outcome)
            .WithDuration(TimeSpan.FromMilliseconds(durationMs))
            .WithStartTime(Epoch)
            .WithEndTime(Epoch.AddMilliseconds(durationMs))
            .WithRetry(retry)
            .Build();
    }

    /// <summary>
    /// Builds a session containing the named tests, all passing.
    /// </summary>
    /// <param name="ordinal">The session's position; 0 is the oldest.</param>
    /// <param name="testNames">Tests the session executed.</param>
    /// <returns>The session.</returns>
    public static TestSession Session(int ordinal, params string[] testNames) =>
        Session(ordinal, testNames.Select(n => Execution(n)).ToList());

    /// <summary>
    /// Builds a session containing the given executions.
    /// </summary>
    /// <param name="ordinal">The session's position; 0 is the oldest.</param>
    /// <param name="executions">The executions it recorded.</param>
    /// <param name="sha">Commit it ran at, or <see langword="null"/> for none.</param>
    /// <param name="branch">Branch it ran on, or <see langword="null"/> for none.</param>
    /// <param name="startedAt">Explicit start time; defaults to one minute per ordinal.</param>
    /// <returns>The session.</returns>
    public static TestSession Session(
        int ordinal,
        IReadOnlyList<TestExecution> executions,
        string? sha = null,
        string? branch = null,
        DateTime? startedAt = null)
    {
        var properties = new Dictionary<string, string>();
        if (sha != null)
            properties["Git.SHA"] = sha;
        if (branch != null)
            properties["Git.Branch"] = branch;

        EnvironmentInfo environment = new EnvironmentInfoBuilder()
            .WithMachineName("dev-box")
            .WithEnvironmentName("Local")
            .AddCustomProperties(properties)
            .Build();

        return new TestSessionBuilder()
            .WithSessionId(SessionIdFor(ordinal))
            .WithStartedAt(startedAt ?? Epoch.AddMinutes(ordinal))
            .WithEndedAt((startedAt ?? Epoch.AddMinutes(ordinal)).AddSeconds(30))
            .WithEnvironmentInfo(environment)
            .AddExecutions(executions)
            .WithSessionState(TestSessionState.Finalized)
            .Build();
    }

    /// <summary>
    /// Builds a window over the given sessions, ordered as the store would return them.
    /// </summary>
    /// <param name="sessions">The sessions, in any order.</param>
    /// <returns>The window.</returns>
    public static AnalysisWindow Window(params TestSession[] sessions)
    {
        var ordered = sessions
            .OrderByDescending(s => s.StartedAt)
            .ThenBy(s => s.SessionId.ToString("N"), StringComparer.Ordinal)
            .ToList();

        return AnalysisWindow.Create(
            ordered,
            ordered[^1].StartedAt,
            ordered[0].StartedAt,
            WindowResolution.Default,
            null);
    }

    /// <summary>
    /// Builds an analysis context over the given sessions.
    /// </summary>
    /// <param name="sessions">The sessions, in any order.</param>
    /// <returns>The context.</returns>
    public static AnalysisContext Context(params TestSession[] sessions)
    {
        AnalysisWindow window = Window(sessions);
        return new AnalysisContext(window, RevisionContext.FromNewest(window.Sessions));
    }
}

/// <summary>
/// An in-memory <see cref="ISessionSource"/>.
/// </summary>
/// <remarks>
/// Lets window resolution be tested without a filesystem, which is the point of keeping disk access
/// behind this interface in the first place.
/// </remarks>
internal sealed class FakeSessionSource(params TestSession[] sessions) : ISessionSource
{
    private readonly List<TestSession> _sessions = sessions
        .OrderByDescending(s => s.StartedAt)
        .ThenBy(s => s.SessionId.ToString("N"), StringComparer.Ordinal)
        .ToList();

    /// <summary>Gets or sets a value indicating whether a store was found.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Gets the store location.</summary>
    public string? StorePath => "/fake/store";

    /// <summary>Gets or sets the unreadable-file count every read reports.</summary>
    public int UnreadableCount { get; set; }

    /// <inheritdoc/>
    public SessionReadResult Read(int maxSessions, string? assembly)
    {
        IEnumerable<TestSession> matching = _sessions;

        if (assembly != null)
        {
            matching = matching.Where(s =>
                string.Equals(LocalSessionSource.AssemblyOf(s), assembly, StringComparison.Ordinal));
        }

        return new SessionReadResult([.. matching.Take(maxSessions)], UnreadableCount);
    }

    /// <inheritdoc/>
    public string? NewestAssembly() =>
        _sessions.Count == 0 ? null : LocalSessionSource.AssemblyOf(_sessions[0]);

    /// <inheritdoc/>
    public IReadOnlyList<string> KnownAssemblies() =>
        _sessions
            .Select(LocalSessionSource.AssemblyOf)
            .Where(a => a is { Length: > 0 })
            .Select(a => a!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();
}
