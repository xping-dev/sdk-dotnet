/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Model;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Indexes;

/// <summary>
/// One execution, together with the session it belongs to.
/// </summary>
/// <param name="Session">The session the execution ran in.</param>
/// <param name="SessionIndex">Position of that session in the window; 0 is the newest.</param>
/// <param name="Execution">The execution itself.</param>
internal sealed record ExecutionRef(TestSession Session, int SessionIndex, TestExecution Execution)
{
    /// <summary>Gets a value indicating whether this execution failed.</summary>
    public bool Failed => Execution.Outcome.IsFailure();
}

/// <summary>
/// Everything derived from the window that more than one provider needs.
/// </summary>
/// <remarks>
/// <para>
/// Built once and shared. Providers are forbidden from calling each other, so without a shared index
/// each would rebuild the same fingerprint-to-executions map — the work would multiply by the number
/// of providers, and worse, two providers could disagree about what "this test's executions" means.
/// </para>
/// <para>
/// Every enumeration this exposes is ordered. Dictionaries back the lookups, but their enumeration
/// order is not stable across processes, and the report has to be byte-identical between runs.
/// </para>
/// </remarks>
internal sealed class TestIndex
{
    private readonly Dictionary<string, List<ExecutionRef>> _byFingerprint;
    private readonly Dictionary<string, List<ExecutionRef>> _runsByFingerprint;
    private readonly Dictionary<string, int> _sessionsRunIn;
    private readonly Dictionary<string, TestReference> _references;
    private readonly Dictionary<Guid, int> _sessionPositions;
    private readonly HashSet<Guid> _sessionsWithFinalFailures;

    private TestIndex(
        AnalysisWindow window,
        Dictionary<string, List<ExecutionRef>> byFingerprint,
        Dictionary<string, List<ExecutionRef>> runsByFingerprint,
        Dictionary<string, int> sessionsRunIn,
        Dictionary<string, TestReference> references,
        Dictionary<Guid, int> sessionPositions,
        HashSet<Guid> sessionsWithFinalFailures,
        IReadOnlyList<string> fingerprints)
    {
        Window = window;
        _byFingerprint = byFingerprint;
        _runsByFingerprint = runsByFingerprint;
        _sessionsRunIn = sessionsRunIn;
        _references = references;
        _sessionPositions = sessionPositions;
        _sessionsWithFinalFailures = sessionsWithFinalFailures;
        Fingerprints = fingerprints;
    }

    /// <summary>Gets the window this index was built over.</summary>
    public AnalysisWindow Window { get; }

    /// <summary>
    /// Gets every test fingerprint in the window, in ordinal order.
    /// </summary>
    /// <remarks>
    /// Sorted rather than insertion-ordered so that a provider iterating tests emits findings in the
    /// same sequence on every run, whatever order the sessions happened to be read in.
    /// </remarks>
    public IReadOnlyList<string> Fingerprints { get; }

    /// <summary>
    /// Gets the executions of one test, newest session first.
    /// </summary>
    /// <param name="fingerprint">The test to look up.</param>
    /// <returns>Its executions, or an empty list when the test is not in the window.</returns>
    public IReadOnlyList<ExecutionRef> ExecutionsOf(string fingerprint) =>
        _byFingerprint.TryGetValue(fingerprint, out List<ExecutionRef>? executions) ? executions : [];

    /// <summary>
    /// Gets the runs of one test, newest session first — one entry per session.
    /// </summary>
    /// <param name="fingerprint">The test to look up.</param>
    /// <returns>Its runs, or an empty list when the test is not in the window.</returns>
    /// <remarks>
    /// <para>
    /// The session is the unit of independence in this data; the attempt is not. A test that failed,
    /// retried and passed produces several <see cref="ExecutionRef"/> entries that are neither
    /// independent of each other nor independently informative, and any gate or rate that counts them
    /// separately is claiming a sample size it does not have.
    /// </para>
    /// <para>
    /// Each run is represented by its deciding attempt — the highest attempt number recorded for the
    /// fingerprint in that session — so <see cref="ExecutionRef.Failed"/> on a run answers the same
    /// question <see cref="SessionOutcomes"/> answers about the session: did this test end it red.
    /// The two must never disagree, or the report would call a session green while flagging a test
    /// inside it as having blocked the build.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ExecutionRef> RunsOf(string fingerprint) =>
        _runsByFingerprint.TryGetValue(fingerprint, out List<ExecutionRef>? runs) ? runs : [];

    /// <summary>
    /// Gets how many distinct sessions a test ran in.
    /// </summary>
    /// <param name="fingerprint">The test to look up.</param>
    /// <returns>The session count, or zero when the test is not in the window.</returns>
    /// <remarks>
    /// The denominator every per-test floor is applied to. Counted in sessions rather than in
    /// executions because a single session that retried five times would otherwise clear a floor of
    /// five on its own, which is the opposite of what a floor is for.
    /// </remarks>
    public int SessionsRunIn(string fingerprint) => _sessionsRunIn.GetValueOrDefault(fingerprint);

    /// <summary>
    /// Gets the display identity of one test.
    /// </summary>
    /// <param name="fingerprint">The test to look up.</param>
    /// <returns>Its reference, or <see langword="null"/> when the test is not in the window.</returns>
    public TestReference? ReferenceFor(string fingerprint) =>
        _references.TryGetValue(fingerprint, out TestReference? reference) ? reference : null;

    /// <summary>
    /// Gets the position of a session in the window; 0 is the newest.
    /// </summary>
    /// <param name="sessionId">The session to look up.</param>
    /// <returns>Its position, or -1 when it is not in the window.</returns>
    public int PositionOf(Guid sessionId) =>
        _sessionPositions.TryGetValue(sessionId, out int position) ? position : -1;

    /// <summary>
    /// Gets the fingerprints present in a given session, in ordinal order.
    /// </summary>
    /// <param name="session">The session to inspect.</param>
    /// <returns>The distinct fingerprints it executed.</returns>
    public static IReadOnlyList<string> FingerprintsIn(TestSession session) =>
        session.Executions
            .Select(e => e.Identity.TestFingerprint)
            .Where(f => f is { Length: > 0 })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Gets how often a test ran relative to how many sessions it could have run in.
    /// </summary>
    /// <param name="fingerprint">The test to measure.</param>
    /// <returns>A value in [0,1].</returns>
    /// <remarks>
    /// <para>
    /// A test that runs in every session is worth more attention than one that runs occasionally,
    /// because it costs the whole team on every build.
    /// </para>
    /// <para>
    /// Counted in sessions, not executions. A retried test records an execution per attempt, so
    /// dividing attempts by sessions would read a test that runs in a quarter of builds and retries
    /// four times as one that runs in every build — and would do it worst for exactly the tests the
    /// retry findings already report.
    /// </para>
    /// </remarks>
    public double RunFrequencyOf(string fingerprint)
    {
        if (Window.SessionCount == 0)
            return 0;

        return Math.Min(1.0, (double)_sessionsRunIn.GetValueOrDefault(fingerprint) / Window.SessionCount);
    }

    /// <summary>
    /// Gets the fraction of a test's failures that landed in a session that ended up failing.
    /// </summary>
    /// <param name="fingerprint">The test to measure.</param>
    /// <returns>A value in [0,1]; zero when the test never failed.</returns>
    /// <remarks>
    /// Separates a test that fails and blocks the build from one whose failures are always masked by
    /// a retry. Both are worth fixing; only the first is stopping anyone today.
    /// </remarks>
    public double BlockingRateOf(string fingerprint)
    {
        int failures = 0;
        int blocking = 0;

        foreach (ExecutionRef reference in ExecutionsOf(fingerprint))
        {
            if (!reference.Failed)
                continue;

            failures++;
            if (_sessionsWithFinalFailures.Contains(reference.Session.SessionId))
                blocking++;
        }

        return failures == 0 ? 0 : (double)blocking / failures;
    }

    /// <summary>
    /// Gets how recently a test last did the thing a provider cares about.
    /// </summary>
    /// <param name="sessionsSinceLastOccurrence">Sessions elapsed since the last occurrence.</param>
    /// <returns>A value in (0,1], halving every few sessions.</returns>
    /// <remarks>
    /// Decays rather than cuts off, so a test that misbehaved eight sessions ago still registers —
    /// quietly — instead of vanishing from the report the moment it drops out of an arbitrary
    /// recent slice.
    /// </remarks>
    public static double Recency(int sessionsSinceLastOccurrence) =>
        Math.Pow(0.5, sessionsSinceLastOccurrence / LocalAnalysisConstants.RecencyHalfLifeSessions);

    /// <summary>
    /// Builds the index for a window.
    /// </summary>
    /// <param name="window">The sessions under analysis.</param>
    /// <returns>The index.</returns>
    public static TestIndex Build(AnalysisWindow window)
    {
        var byFingerprint = new Dictionary<string, List<ExecutionRef>>(StringComparer.Ordinal);
        var runsByFingerprint = new Dictionary<string, List<ExecutionRef>>(StringComparer.Ordinal);
        var sessionsRunIn = new Dictionary<string, int>(StringComparer.Ordinal);
        var references = new Dictionary<string, TestReference>(StringComparer.Ordinal);
        var sessionPositions = new Dictionary<Guid, int>();
        var sessionsWithFinalFailures = new HashSet<Guid>();

        for (int position = 0; position < window.Sessions.Count; position++)
        {
            TestSession session = window.Sessions[position];
            sessionPositions[session.SessionId] = position;

            foreach (TestExecution execution in session.Executions)
            {
                string fingerprint = execution.Identity.TestFingerprint;
                if (string.IsNullOrEmpty(fingerprint))
                    continue;

                if (!byFingerprint.TryGetValue(fingerprint, out List<ExecutionRef>? executions))
                {
                    executions = [];
                    byFingerprint[fingerprint] = executions;
                }

                if (!runsByFingerprint.TryGetValue(fingerprint, out List<ExecutionRef>? runs))
                {
                    runs = [];
                    runsByFingerprint[fingerprint] = runs;
                }

                var reference = new ExecutionRef(session, position, execution);

                // Sessions are walked one at a time, so a fingerprint's executions arrive grouped by
                // session: the session count only advances when the last one recorded came from a
                // different session, and never counts a retry twice.
                if (executions.Count == 0 || executions[^1].SessionIndex != position)
                {
                    sessionsRunIn[fingerprint] = sessionsRunIn.GetValueOrDefault(fingerprint) + 1;
                    runs.Add(reference);
                }
                else if (AttemptOf(execution) >= AttemptOf(runs[^1].Execution))
                {
                    // Same session, so this attempt replaces the run's representative when it is at
                    // least as late. Attempts are not guaranteed to arrive in order, and the
                    // comparison is `>=` rather than `>` for the reason SessionOutcomes gives: on
                    // equal attempt numbers the last one recorded is the one that session ended on.
                    runs[^1] = reference;
                }

                executions.Add(reference);

                // Sessions are walked newest first, so the first identity seen for a fingerprint is
                // the most recent one. That matters after a rename: the report should show what the
                // test is called now, not what it was called a fortnight ago.
                if (!references.ContainsKey(fingerprint))
                    references[fingerprint] = ToReference(execution);
            }

            if (SessionOutcomes.HasFinalFailure(session))
                sessionsWithFinalFailures.Add(session.SessionId);
        }

        var fingerprints = byFingerprint.Keys.OrderBy(f => f, StringComparer.Ordinal).ToList();

        return new TestIndex(
            window,
            byFingerprint,
            runsByFingerprint,
            sessionsRunIn,
            references,
            sessionPositions,
            sessionsWithFinalFailures,
            fingerprints);
    }

    /// <summary>
    /// Reads the attempt number an execution recorded, defaulting an unretried run to its first.
    /// </summary>
    /// <remarks>
    /// The same reading <see cref="SessionOutcomes"/> takes. An adapter that never retried records no
    /// retry block at all, and that run is its own deciding attempt.
    /// </remarks>
    private static int AttemptOf(TestExecution execution) => execution.Retry?.AttemptNumber ?? 1;

    private static TestReference ToReference(TestExecution execution)
    {
        TestIdentity identity = execution.Identity;

        return new TestReference(
            identity.TestFingerprint,
            identity.FullyQualifiedName,
            string.IsNullOrEmpty(identity.DisplayName) ? execution.TestName : identity.DisplayName,
            identity.SourceFile,
            identity.SourceLineNumber,
            identity.Assembly);
    }
}
