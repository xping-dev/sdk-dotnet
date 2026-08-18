/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;

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
/// </remarks>
/// <param name="Session">The session itself.</param>
/// <param name="Index">Its position in the window; 0 is the newest.</param>
/// <param name="Tests">Distinct tests it ran.</param>
/// <param name="Failures">How many of them ended it as a failure, judged on their last attempt.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Tests"/>.</param>
/// <param name="IsLikelyEnvironmental">
/// Whether the session failed widely enough to be suspected instead of the tests in it.
/// </param>
internal sealed record SessionView(
    TestSession Session,
    int Index,
    int Tests,
    int Failures,
    double FailureRate,
    bool IsLikelyEnvironmental)
{
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

        return new SessionView(session, index, tests, failures, rate, environmental);
    }
}
