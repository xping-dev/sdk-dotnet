/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Cli.Report.Indexes;

/// <summary>
/// One analysed session, with the counts every provider would otherwise recompute.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsLikelyEnvironmental"/> is the flag that keeps one bad afternoon out of every test's
/// history. When a third of a suite fails at once, the tests did not all break simultaneously —
/// something underneath them did, and counting that run against each of them individually turns the
/// whole report into noise for as long as it stays in the window.
/// </para>
/// <para>
/// It is a classification against two thresholds, not a claim about what went wrong. The report says
/// a session looks environmental; it never says why, and it never says so about a test.
/// </para>
/// <para>
/// <see cref="IsPartial"/> is the other flag, and it answers a different question: not whether the
/// session's outcomes can be trusted, but whether its silences can. Where the test framework said
/// what it discovered and what it ran, that is <see cref="ReportedPartial"/> and <see cref="For"/>
/// knows it. Where it did not, the flag is a guess made against the rest of the window, so
/// <see cref="For"/> cannot decide it — see <see cref="AnalysisContext.PartialSessionCount"/>.
/// </para>
/// </remarks>
/// <param name="Session">The session itself.</param>
/// <param name="Index">Its position in the window; 0 is the newest.</param>
/// <param name="Tests">Distinct tests it ran.</param>
/// <param name="Failures">How many of them ended it as a failure, judged on their last attempt.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Tests"/>.</param>
/// <param name="IsLikelyEnvironmental">
/// Whether the session failed widely enough to be suspected instead of the tests in it.
/// </param>
/// <param name="ReportedPartial">
/// Whether the session ran less than its test framework discovered, or <see langword="null"/> when
/// the framework did not report counts that can be compared. A fact where present, which is why it
/// wins over the count <see cref="AnalysisContext"/> otherwise falls back to.
/// </param>
internal sealed record SessionView(
    TestSession Session,
    int Index,
    int Tests,
    int Failures,
    double FailureRate,
    bool IsLikelyEnvironmental,
    bool? ReportedPartial)
{
    /// <summary>
    /// Gets a value indicating whether the session covered only part of the suite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run under a <c>dotnet test --filter</c> is not a run in which the tests it excluded failed
    /// to appear — it is a run that never asked about them. A kind reading absence has to set such a
    /// session aside, or every unselected test looks deleted.
    /// </para>
    /// <para>
    /// Decided in two ways, and <see cref="AnalysisContext"/> sets it either way. Where the session
    /// carries <see cref="ReportedPartial"/> the flag is that fact: a filtered run discovered the
    /// tests it did not select, a run cut short recorded fewer tests than it selected, and a run
    /// after a deletion did neither. Only xUnit reports it today.
    /// </para>
    /// <para>
    /// Everywhere else it is <see cref="Tests"/> against
    /// <see cref="LocalAnalysisConstants.PartialSessionShare"/> of the largest run in the window,
    /// which one session cannot measure on its own. That is a classification against a threshold
    /// and not a claim about what happened, in the same way <see cref="IsLikelyEnvironmental"/> is:
    /// the report says the session covered part of the suite, and never says a filter was the
    /// reason, because a deletion produces the same count.
    /// </para>
    /// </remarks>
    public bool IsPartial { get; init; }

    /// <summary>
    /// Measures one session.
    /// </summary>
    /// <param name="session">The session to measure.</param>
    /// <param name="index">Its position in the window; 0 is the newest.</param>
    /// <returns>The view.</returns>
    public static SessionView For(TestSession session, int index)
    {
        (int tests, int failures) = SessionOutcomes.Tally(session);

        double rate = tests == 0 ? 0 : (double)failures / tests;

        // Both bounds are load-bearing. The rate alone would condemn a five-test suite with two
        // failures, which is not an outage; the count alone would condemn a thousand-test suite with
        // ten unrelated failures, which is a Tuesday.
        bool environmental =
            rate >= LocalAnalysisConstants.EnvironmentalSessionFailureRate &&
            failures >= LocalAnalysisConstants.EnvironmentalSessionMinFailures;

        return new SessionView(session, index, tests, failures, rate, environmental, ReportedPartialOf(session, tests));
    }

    /// <summary>
    /// Reads whether the session ran less than its test framework discovered.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="tests">The distinct tests it recorded.</param>
    /// <remarks>
    /// <para>
    /// Two ways to run less, and each is a fact. Fewer cases selected than discovered is a filter.
    /// Fewer tests recorded than cases selected is a run that stopped before it finished — cancelled,
    /// or killed by a hang timeout — and its silences are no more evidence than a filtered run's.
    /// A case never runs as fewer than one test: a theory whose rows could not be enumerated at
    /// discovery is one case that runs as several.
    /// </para>
    /// <para>
    /// More cases selected than discovered is not a run of anything: the two counts came from
    /// different discoveries, as when a runner runs cases it discovered earlier. Nothing can be
    /// read from them, so the session falls back to the threshold like one that reported nothing.
    /// </para>
    /// <para>
    /// A window is scoped to one assembly and the store projects each session onto it, so the
    /// breakdown holds that assembly's entry and no other. More than one entry is a session nobody
    /// projected, and which of them the question is about is not something to guess at here.
    /// </para>
    /// </remarks>
    private static bool? ReportedPartialOf(TestSession session, int tests)
    {
        if (session.StatisticsByAssembly is not { Count: 1 } breakdown)
            return null;

        AssemblyStatistics statistics = breakdown.Values.First();

        if (statistics is not { DiscoveredTestCases: int discovered, SelectedTestCases: int selected } ||
            selected > discovered)
        {
            return null;
        }

        return selected < discovered || tests < selected;
    }
}
