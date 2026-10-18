/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Contract;

/// <summary>
/// Builds what <c>xping report --test</c> publishes about one test.
/// </summary>
/// <remarks>
/// Reads the same index the providers read, so its runs and deciding attempts are theirs. Its counts
/// are not the retry findings' counts: a run passed on retry here is any run decided by a passing
/// attempt after the first (D9), where <c>RetryMasked</c> also needs the adapter to have recorded
/// that the retry is what passed. The findings' rows and the floor's reason are left for
/// <see cref="EnvelopeBuilder"/>, which holds the ordered findings they are read from.
/// </remarks>
internal static class TestHistoryBuilder
{
    /// <summary>
    /// Builds the history of a test the name resolved to.
    /// </summary>
    /// <param name="context">The window resolved for the test, scoped to its assembly.</param>
    /// <param name="query">The name the reader typed.</param>
    /// <param name="match">The test it resolved to, which must have run in the window.</param>
    /// <returns>The history, with no finding rows yet.</returns>
    public static TestHistoryDto Found(AnalysisContext context, string query, TestMatch match)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(match);

        string fingerprint = match.Fingerprint;
        TestIndex tests = context.Tests;

        TestReference reference = tests.ReferenceFor(fingerprint)
            ?? throw new ArgumentException($"Test '{fingerprint}' did not run in the window.", nameof(match));

        Dictionary<int, ExecutionRef> runs = tests.RunsOf(fingerprint).ToDictionary(run => run.SessionIndex);

        Dictionary<int, List<TestAttemptDto>> attempts = tests.ExecutionsOf(fingerprint)
            .GroupBy(e => e.SessionIndex)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(e => TestIndex.AttemptOf(e.Execution))
                    .Select(e => new TestAttemptDto(
                        TestIndex.AttemptOf(e.Execution), e.Execution.Outcome, Milliseconds(e.Execution)))
                    .ToList());

        List<TestSessionDto> sessions = [];
        for (int position = 0; position < context.SessionViews.Count; position++)
        {
            SessionView view = context.SessionViews[position];
            TestSession session = view.Session;

            sessions.Add(runs.TryGetValue(position, out ExecutionRef? run)
                ? new TestSessionDto(
                    session.SessionId,
                    session.StartedAt,
                    RevisionContext.ReadSha(session),
                    Appeared: true,
                    view.IsPartial,
                    run.Execution.Outcome,
                    TestIndex.AttemptOf(run.Execution),
                    Milliseconds(run.Execution),
                    attempts[position])
                : new TestSessionDto(
                    session.SessionId,
                    session.StartedAt,
                    RevisionContext.ReadSha(session),
                    Appeared: false,
                    view.IsPartial,
                    Outcome: null,
                    DecidingAttempt: null,
                    DurationMs: null,
                    []));
        }

        return new TestHistoryDto(
            query,
            EnvelopeBuilder.ForTest(reference),
            match.ShortFingerprint,
            sessions,
            Counts(runs.Values, context.Window.SessionCount),
            Duration(tests.VerdictRunsOf(fingerprint)),
            FindingsWithheld: null,
            FindingRows: [],
            Candidates: []);
    }

    /// <summary>
    /// Builds the answer to a name that matched several tests.
    /// </summary>
    /// <param name="resolution">The ambiguous resolution.</param>
    /// <param name="scope">
    /// What the command was scoped to. Each candidate's command takes the candidate's own assembly.
    /// </param>
    /// <returns>The candidates, newest run first, and nothing else.</returns>
    public static TestHistoryDto Ambiguous(TestResolution resolution, ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(scope);

        // Found and ambiguous differ only in the candidate list and the exit code, so a resolution
        // of the wrong kind would build an answer that looks valid and contradicts the exit code.
        if (resolution.Kind != TestResolutionKind.Ambiguous)
            throw new ArgumentException($"Expected an ambiguous resolution, got {resolution.Kind}.", nameof(resolution));

        return Empty(resolution.Query) with
        {
            Candidates =
            [
                .. resolution.Candidates.Select(c => new TestCandidateDto(
                    c.ShortFingerprint,
                    c.Assembly,
                    c.FullyQualifiedName,
                    c.LastSeen,
                    DrillDown.ForTest(c.ShortFingerprint, scope with { Assembly = c.Assembly })))
            ]
        };
    }

    /// <summary>
    /// Builds the answer to a name that matched nothing, so a script still sees what was asked for.
    /// </summary>
    /// <param name="query">The name the reader typed.</param>
    /// <returns>The query, and nothing else.</returns>
    public static TestHistoryDto NotFound(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Empty(query);
    }

    private static TestHistoryDto Empty(string query) =>
        new(query, null, null, [], null, null, null, [], []);

    /// <summary>
    /// Counts the runs by how their deciding attempt ended.
    /// </summary>
    /// <remarks>
    /// A run that ended skipped, not executed or inconclusive is counted as ran and as other, never as
    /// an absence: the framework saw the test, and calling it absent would hide an ignore someone left
    /// on (OQ-1).
    /// </remarks>
    private static TestRunCountsDto Counts(IEnumerable<ExecutionRef> runs, int windowRuns)
    {
        int ran = 0, passed = 0, passedOnRetry = 0, failed = 0, other = 0;

        foreach (ExecutionRef run in runs)
        {
            ran++;

            switch (run.Execution.Outcome)
            {
                case TestOutcome.Passed when TestIndex.AttemptOf(run.Execution) > 1:
                    passedOnRetry++;
                    break;
                case TestOutcome.Passed:
                    passed++;
                    break;
                case TestOutcome.Failed or TestOutcome.Timeout:
                    failed++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        return new TestRunCountsDto(ran, windowRuns, passed, passedOnRetry, failed, other);
    }

    /// <summary>
    /// Summarises the durations of the runs that reached a verdict.
    /// </summary>
    /// <remarks>
    /// Raw milliseconds and not <see cref="Providers.DurationProvider"/>'s normalised profile, which
    /// answers "slow relative to its session" — a finding's question, not a history's.
    /// </remarks>
    private static TestDurationDto? Duration(IReadOnlyList<ExecutionRef> verdictRuns)
    {
        if (verdictRuns.Count == 0)
            return null;

        List<double> sorted = [.. verdictRuns.Select(run => run.Execution.Duration.TotalMilliseconds).Order()];

        return new TestDurationDto(
            Round(Quantile.Interpolated(sorted, 0.50)),
            Round(Quantile.Interpolated(sorted, 0.95)),
            Round(sorted[0]),
            Round(sorted[^1]),
            FindingOrder.Round(RobustDispersion.Of(sorted)),
            sorted.Count);
    }

    private static long Milliseconds(TestExecution execution) => Round(execution.Duration.TotalMilliseconds);

    private static long Round(double milliseconds) =>
        (long)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
}
