/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Models.Statistics;

/// <summary>
/// The end-of-session summary line, reduced to display-ready pieces.
/// </summary>
/// <remarks>
/// Every test framework adapter prints the same summary once its session is finalized, so the
/// decisions behind that line — which outcome counters to read, how to word them, and at what
/// scale to render the durations — live here rather than three times over in the adapters.
/// The adapters keep only the logging call, so the wording stays identical across NUnit, xUnit
/// and MSTest.
/// </remarks>
public sealed class SessionSummary
{
    private SessionSummary(
        int total,
        bool retried,
        int distinctTests,
        int retries,
        string outcomes,
        string executionDuration,
        string wallClockDuration,
        string overhead)
    {
        Total = total;
        Retried = retried;
        DistinctTests = distinctTests;
        Retries = retries;
        Outcomes = outcomes;
        ExecutionDuration = executionDuration;
        WallClockDuration = wallClockDuration;
        Overhead = overhead;
    }

    /// <summary>
    /// Gets the number of executions recorded, counting every retry attempt separately.
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// Gets a value indicating whether the runner retried at least one test, in which case
    /// <see cref="Outcomes"/> reports the test-level result rather than the per-attempt one.
    /// </summary>
    public bool Retried { get; }

    /// <summary>
    /// Gets the number of distinct tests behind <see cref="Total"/>.
    /// </summary>
    public int DistinctTests { get; }

    /// <summary>
    /// Gets the number of retry attempts the runner performed, zero when nothing was retried.
    /// </summary>
    public int Retries { get; }

    /// <summary>
    /// Gets the outcome breakdown, for example <c>9 passed, 2 failed, 1 skipped</c>.
    /// </summary>
    public string Outcomes { get; }

    /// <summary>
    /// Gets the summed execution time of the tests themselves, rendered at human scale.
    /// </summary>
    public string ExecutionDuration { get; }

    /// <summary>
    /// Gets the elapsed time of the whole session, rendered at human scale.
    /// </summary>
    public string WallClockDuration { get; }

    /// <summary>
    /// Gets the framework overhead as a suffix to <see cref="WallClockDuration"/>, or an empty
    /// string when there is none to report.
    /// </summary>
    public string Overhead { get; }

    /// <summary>
    /// Gets the word to pair with <see cref="DistinctTests"/>.
    /// </summary>
    public string TestLabel => DistinctTests == 1 ? "test" : "tests";

    /// <summary>
    /// Gets the word to pair with <see cref="Retries"/>.
    /// </summary>
    public string RetryLabel => Retries == 1 ? "retry" : "retries";

    /// <summary>
    /// Reduces the session's statistics to the pieces the summary line is built from.
    /// </summary>
    /// <param name="statistics">The finalized statistics for the session.</param>
    /// <returns>The display-ready summary.</returns>
    public static SessionSummary From(QuickStatistics statistics)
    {
        statistics = statistics.RequireNotNull();

        // A retried test is recorded once per attempt, so the outcome counters count attempts.
        // When any retry happened, report the test-level outcomes instead — a suite that recovered
        // on retry is green — and keep the attempt count visible beside them.
        bool retried = statistics.DistinctTests > 0 && statistics.DistinctTests != statistics.Total;

        int passed = retried ? statistics.FinalPassed : statistics.Passed;
        int failed = retried ? statistics.FinalFailed : statistics.Failed;
        int skipped = retried ? statistics.FinalSkipped : statistics.Skipped;
        int inconclusive = retried ? statistics.FinalInconclusive : statistics.Inconclusive;
        int notExecuted = retried ? statistics.FinalNotExecuted : statistics.NotExecuted;
        int timedOut = retried ? statistics.FinalTimeout : statistics.Timeout;

        var outcomes = new StringBuilder();
        outcomes.Append($"{passed} passed");
        if (failed > 0)        outcomes.Append($", {failed} failed");
        if (timedOut > 0)      outcomes.Append($", {timedOut} timed out");
        if (skipped > 0)       outcomes.Append($", {skipped} skipped");
        if (inconclusive > 0)  outcomes.Append($", {inconclusive} inconclusive");
        if (notExecuted > 0)   outcomes.Append($", {notExecuted} not executed");

        // TotalDurationMs sums each test's own execution time; WallClockDurationMs is the elapsed
        // time for the whole session (includes fixture setup/teardown, discovery, etc.). The gap
        // between them is the framework overhead, which is what a slow-but-green suite needs to
        // look at.
        return new SessionSummary(
            total: statistics.Total,
            retried: retried,
            distinctTests: statistics.DistinctTests,
            retries: retried ? statistics.Total - statistics.DistinctTests : 0,
            outcomes: outcomes.ToString(),
            executionDuration: DurationFormatter.Format(statistics.TotalDurationMs),
            wallClockDuration: DurationFormatter.Format(statistics.WallClockDurationMs),
            overhead: DurationFormatter.FormatOverhead(
                statistics.TotalDurationMs, statistics.WallClockDurationMs));
    }
}
