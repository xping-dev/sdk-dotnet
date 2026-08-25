/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Security.Cryptography;
using System.Text;
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
    /// Builds an execution id that is stable for a given execution's inputs.
    /// </summary>
    /// <param name="name">Method name.</param>
    /// <param name="attempt">Retry attempt number.</param>
    /// <param name="outcome">How it ended.</param>
    /// <returns>The id.</returns>
    public static Guid ExecutionIdFor(string name, int attempt, TestOutcome outcome)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{name}|{attempt}|{outcome}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// Builds one execution.
    /// </summary>
    /// <param name="name">Method name; also seeds the fingerprint.</param>
    /// <param name="outcome">How it ended.</param>
    /// <param name="assembly">Owning assembly.</param>
    /// <param name="durationMs">How long it took.</param>
    /// <param name="attempt">Retry attempt number.</param>
    /// <param name="passedOnRetry">Whether this attempt is a pass that followed a failure.</param>
    /// <param name="maxRetries">Retries the attribute allows.</param>
    /// <param name="retryAttributeName">The retry mechanism the SDK identified.</param>
    /// <param name="retryReason">
    /// The reason the retry attribute declared, or <see langword="null"/> when it declared none.
    /// An empty string is reachable on purpose: it is what the MSTest adapter writes where the other
    /// two leave null, and the report has to read it as an absence rather than as a blank reason.
    /// </param>
    /// <param name="retryDelayMs">
    /// The delay the retry attribute declared between attempts. Zero means the adapter recorded
    /// none, which is what every NUnit execution looks like.
    /// </param>
    /// <param name="errorMessage">Failure text, when the execution failed.</param>
    /// <param name="executionId">Explicit execution id; defaults to one derived from the inputs.</param>
    /// <param name="retry">
    /// Whether to attach retry metadata at all. Passing <see langword="false"/> reproduces an
    /// adapter that never detected a retry attribute, which is the common case in a real store.
    /// </param>
    /// <param name="exceptionType">The type the adapter recorded, when it recorded one.</param>
    /// <param name="stackTrace">The stack trace the adapter recorded, when it recorded one.</param>
    /// <param name="stackTraceOmitted">
    /// Whether stack trace capture was switched off. Distinct from having no trace: it means the
    /// developer opted out, not that none existed.
    /// </param>
    /// <param name="concurrency">
    /// Tests in flight alongside this one, itself included, or <see langword="null"/> to record no
    /// orchestration data at all. Null is the default because it is what an execution written before
    /// the field existed looks like, and because every fixture that predates concurrency analysis
    /// must keep meaning exactly what it did.
    /// </param>
    /// <param name="timeoutBudgetMs">
    /// The timeout the test declared for itself, or <see langword="null"/> when it declared none.
    /// </param>
    /// <param name="failureSite">
    /// Where in the lifecycle the failure happened, or <see langword="null"/> for an execution
    /// recorded by an adapter that resolved no site — which is what every stored execution from
    /// before this field existed looks like.
    /// </param>
    /// <param name="failureSiteMember">The lifecycle member that failed, when one was named.</param>
    /// <returns>The execution.</returns>
    public static TestExecution Execution(
        string name,
        TestOutcome outcome = TestOutcome.Passed,
        string assembly = DefaultAssembly,
        int durationMs = 100,
        int attempt = 1,
        bool passedOnRetry = false,
        int maxRetries = 0,
        string retryAttributeName = "",
        string? retryReason = null,
        int retryDelayMs = 0,
        string? errorMessage = null,
        Guid? executionId = null,
        bool retry = true,
        string? exceptionType = null,
        string? stackTrace = null,
        bool stackTraceOmitted = false,
        int? concurrency = null,
        int? timeoutBudgetMs = null,
        FailureSite? failureSite = null,
        string? failureSiteMember = null)
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

        RetryMetadata? retryMetadata = null;

        if (retry)
        {
            RetryMetadataBuilder retryBuilder = new RetryMetadataBuilder()
                .WithAttemptNumber(attempt)
                .WithMaxRetries(maxRetries)
                .WithPassedOnRetry(passedOnRetry)
                .WithRetryAttributeName(retryAttributeName);

            // Applied only when set, so every fixture written before these existed keeps meaning
            // exactly what it did — an unset reason stays null rather than becoming an empty string.
            if (retryReason != null)
                retryBuilder.WithRetryReason(retryReason);

            if (retryDelayMs > 0)
                retryBuilder.WithDelayBetweenRetries(TimeSpan.FromMilliseconds(retryDelayMs));

            retryMetadata = retryBuilder.Build();
        }

        TestExecutionBuilder builder = new TestExecutionBuilder()

            // Derived rather than random: the exemplar order in a finding falls back to the
            // execution id, and a fixture that produced a new one per call would make the
            // byte-identical-output requirement untestable.
            .WithExecutionId(executionId ?? ExecutionIdFor(name, attempt, outcome))
            .WithIdentity(identity)
            .WithTestName(name)
            .WithOutcome(outcome)
            .WithDuration(TimeSpan.FromMilliseconds(durationMs))
            .WithStartTime(Epoch)
            .WithEndTime(Epoch.AddMilliseconds(durationMs))
            .WithRetry(retryMetadata)
            .WithStackTraceOmitted(stackTraceOmitted)
            .WithTimeoutBudget(
                timeoutBudgetMs is { } budgetMs ? TimeSpan.FromMilliseconds(budgetMs) : null,
                timeoutBudgetMs is null ? null : TimeoutBudgetSource.Declared)
            .WithFailureSite(failureSite, failureSiteMember);

        // Attached only when asked for. The execution builder already defaults the record, and that
        // default reports a concurrency of zero — which is precisely how concurrency analysis
        // recognises an execution it cannot place in an arm.
        if (concurrency is { } concurrent)
        {
            builder = builder.WithTestOrchestrationRecord(
                new TestOrchestrationBuilder()
                    .WithParallelization(concurrent > 1, concurrent)
                    .Build());
        }

        // A caller that named a type or supplied a trace is exercising signature composition and
        // gets exactly what it passed — including a null type, which is what NUnit records for an
        // assertion failure. A caller that passed a message alone gets a plausible type with it,
        // which is what the retry fixtures have always assumed.
        if (exceptionType != null || stackTrace != null)
            builder = builder.WithException(exceptionType, errorMessage, stackTrace);
        else if (errorMessage != null)
            builder = builder.WithException("System.Exception", errorMessage);

        return builder.Build();
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
    /// <param name="customProperties">Extra environment properties, such as the recording mode.</param>
    /// <param name="utcOffset">
    /// The machine's offset from UTC, or <see langword="null"/> to record none at all. Null is the
    /// default because it is what a session written before the field existed looks like, and because
    /// every fixture that predates temporal analysis must keep meaning exactly what it did.
    /// </param>
    /// <param name="timeZoneId">
    /// The zone the offset came from. Only meaningful alongside <paramref name="utcOffset"/>.
    /// </param>
    /// <returns>The session.</returns>
    public static TestSession Session(
        int ordinal,
        IReadOnlyList<TestExecution> executions,
        string? sha = null,
        string? branch = null,
        DateTime? startedAt = null,
        IReadOnlyDictionary<string, string>? customProperties = null,
        TimeSpan? utcOffset = null,
        string? timeZoneId = "Europe/Berlin")
    {
        var properties = new Dictionary<string, string>();
        if (sha != null)
            properties["Git.SHA"] = sha;
        if (branch != null)
            properties["Git.Branch"] = branch;

        if (customProperties != null)
        {
            foreach (KeyValuePair<string, string> property in customProperties)
                properties[property.Key] = property.Value;
        }

        EnvironmentInfo environment = new EnvironmentInfoBuilder()
            .WithMachineName("dev-box")
            .WithEnvironmentName("Local")
            .WithLocalTimeZone(utcOffset, utcOffset is null ? null : timeZoneId)
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
