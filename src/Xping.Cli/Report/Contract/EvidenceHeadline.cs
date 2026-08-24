/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Shared;

namespace Xping.Cli.Report.Contract;

/// <summary>
/// Turns a finding's evidence into the one sentence a reader is shown, and the labelled pairs
/// behind it.
/// </summary>
/// <remarks>
/// <para>
/// Resolved here, once, rather than in each renderer. A renderer that reached into the evidence
/// payload to phrase its own summary would be the second place a measurement is described, and the
/// two would eventually disagree about the same run — which is precisely what the renderer contract
/// in <c>IReportRenderer</c> forbids.
/// </para>
/// <para>
/// <b>Every headline is ASCII.</b> It is read by a JSON consumer, by a terminal on a legacy code
/// page, and out of a chat client that may or may not have the font — so <c>-&gt;</c>, never an
/// arrow glyph. The glyph set is chosen after this runs and never applies to it.
/// </para>
/// <para>
/// Observations only, per the output contract's evidence rules: a headline states what was counted
/// and never why it happened. No arithmetic happens here either — every figure was rounded by its
/// provider to the precision the report publishes, and this only formats it.
/// </para>
/// </remarks>
internal static class EvidenceHeadline
{
    /// <summary>
    /// Resolves the headline and metrics for one finding.
    /// </summary>
    /// <param name="kind">What the finding claims.</param>
    /// <param name="evidence">The kind-specific payload.</param>
    /// <returns>The sentence and the pairs behind it.</returns>
    public static (string Headline, IReadOnlyList<MetricDto> Metrics) For(
        FindingKind kind, FindingEvidence evidence) => evidence switch
    {
        RetryMaskedEvidence retry => RetryMasked(retry),
        FlakyEvidence flaky => Flaky(flaky),
        AlwaysFailingEvidence always => AlwaysFailing(always),
        TimingOutEvidence timingOut => TimingOut(timingOut),
        BrokenFixtureEvidence fixture => BrokenFixture(fixture),
        SharedFailureEvidence shared => SharedFailure(shared),
        DurationRegressionEvidence regression => DurationRegression(regression),
        DurationUnstableEvidence unstable => DurationUnstable(unstable),
        ParallelSensitiveEvidence parallel => ParallelSensitive(parallel),
        VanishedEvidence vanished => Vanished(vanished),

        // A kind whose provider ships later. Naming the kind is honest and useless in equal measure,
        // which is better than a renderer printing an empty line where a number belongs.
        _ => ($"see evidence for details ({kind})", [])
    };

    private static (string, IReadOnlyList<MetricDto>) RetryMasked(RetryMaskedEvidence e)
    {
        string headline =
            $"passed on retry {Times(e.MaskedOccurrences)} in " +
            $"{e.SessionsWithMasking} of {Runs(e.Sessions)}, up to attempt {e.MaxAttemptObserved}";

        if (e.RetryWallClockMs > 0)
            headline += $", {Duration(e.RetryWallClockMs)} spent retrying";

        List<MetricDto> metrics =
        [
            new("masked", $"{e.MaskedOccurrences} of {e.Executions} executions ({Percent(e.MaskedRate)})"),
            new("runs affected", $"{e.SessionsWithMasking} of {e.Sessions}"),
            new("deepest attempt", e.MaxAttemptObserved.ToString(CultureInfo.InvariantCulture))
        ];

        if (e.RetryAttributeName is { Length: > 0 } attribute)
            metrics.Add(new MetricDto("mechanism", attribute));

        return (headline, metrics);
    }

    private static (string, IReadOnlyList<MetricDto>) Flaky(FlakyEvidence e)
    {
        string modes = e.DistinctSignatureCount == 1
            ? "1 failure mode"
            : $"{e.DistinctSignatureCount} failure modes";

        return (
            $"failed {e.Failures} of {e.Executions} executions ({Percent(e.FailureRate)}) " +
            $"in {e.SessionsWithFailures} of {Runs(e.Sessions)}, {modes}",
            [
                new("failed", $"{e.Failures} of {e.Executions} executions ({Percent(e.FailureRate)})"),
                new("runs affected", $"{e.SessionsWithFailures} of {e.Sessions}"),
                new("failure modes", e.DistinctSignatureCount.ToString(CultureInfo.InvariantCulture))
            ]);
    }

    private static (string, IReadOnlyList<MetricDto>) AlwaysFailing(AlwaysFailingEvidence e)
    {
        string headline =
            $"failed {e.Failures} of {e.Executions} executions ({Percent(e.FailureRate)}), " +
            "one failure mode";

        // Named only when the adapter recorded a type. An adapter that captures no failure detail is
        // not the same as a failure that had none, and inventing a name here would hide the gap.
        if (e.Signature.ExceptionType is { Length: > 0 } type)
            headline += $": {type}";

        return (
            headline,
            [
                new("failed", $"{e.Failures} of {e.Executions} executions ({Percent(e.FailureRate)})"),
                new("runs affected", $"{e.SessionsWithFailures} of {e.Sessions}"),
                new("failure mode", e.Signature.ExceptionType ?? "not recorded by the adapter")
            ]);
    }

    private static (string, IReadOnlyList<MetricDto>) TimingOut(TimingOutEvidence e)
    {
        string headline =
            $"timed out {e.Timeouts} of {e.Executions} executions ({Percent(e.TimeoutRate)}) " +
            $"in {e.SessionsWithTimeouts} of {Runs(e.Sessions)}";

        // The budget beside the observed run is the whole reading. Stated only when the test declared
        // one: a limit that came from a suite-wide or runner-level setting is not in the session, and
        // naming a number the report cannot see would be an invention.
        if (e.DeclaredBudgetMs is { } budget)
        {
            headline += $", killed at its {Duration(budget)} limit";
        }

        List<MetricDto> metrics =
        [
            new("timed out", $"{e.Timeouts} of {e.Executions} executions ({Percent(e.TimeoutRate)})"),
            new("runs affected", $"{e.SessionsWithTimeouts} of {e.Sessions}"),
            new("declared limit", e.DeclaredBudgetMs is { } ms ? Duration(ms) : "none declared by the test")
        ];

        // Only when the test also failed some other way. "7 of 7 failures were timeouts" is noise;
        // "5 of 9" is the reason this was not reported as an ordinary failure.
        if (e.Timeouts != e.Failures)
        {
            metrics.Add(new MetricDto(
                "share of failures",
                $"{e.Timeouts} of {e.Failures} ({Percent(e.TimeoutShareOfFailures)})"));
        }

        return (headline, metrics);
    }

    /// <summary>
    /// Phrases a cluster whose cause is a named lifecycle member.
    /// </summary>
    /// <remarks>
    /// Leads with the member, because it is the only part a reader acts on: the counts say how much
    /// it costs, and the name says where to go. Falls back to naming the site alone when the framework
    /// named no member — still more than "these tests fail alike", and still nothing invented.
    /// </remarks>
    private static (string, IReadOnlyList<MetricDto>) BrokenFixture(BrokenFixtureEvidence e)
    {
        string subject = e.Member ?? Phrase(e.Site);

        return
        (
            $"{subject} failed, blocking {e.TestsBlocked} tests in {e.SessionsAffected} of {Runs(e.Sessions)}",
            [
                new("failing member", subject),
                new("where", Phrase(e.Site)),
                new("tests blocked", e.TestsBlocked.ToString(CultureInfo.InvariantCulture)),
                new("failures", e.Failures.ToString(CultureInfo.InvariantCulture)),
                new("runs affected", $"{e.SessionsAffected} of {e.Sessions}"),
                new("worst run", $"{e.MaxTestsInOneSession} tests")
            ]);
    }

    /// <summary>
    /// Turns a site's enum name into the words a person reads.
    /// </summary>
    /// <remarks>
    /// Enum names are the JSON contract; a report pasted into a chat is read by people who have never
    /// seen the enum. An unrecognised value prints as it came rather than as a blank.
    /// </remarks>
    private static string Phrase(string site) => site switch
    {
        nameof(FailureSite.TestSetup) => "per-test setup",
        nameof(FailureSite.TestTeardown) => "per-test teardown",
        nameof(FailureSite.FixtureSetup) => "fixture setup",
        nameof(FailureSite.FixtureTeardown) => "fixture teardown",
        nameof(FailureSite.AssemblySetup) => "assembly setup",
        nameof(FailureSite.AssemblyTeardown) => "assembly teardown",
        _ => site
    };

    private static (string, IReadOnlyList<MetricDto>) SharedFailure(SharedFailureEvidence e) =>
    (
        $"{e.MemberCount} tests failed alike in {e.SessionsAffected} of {Runs(e.Sessions)}, " +
        $"worst run hit {e.MaxTestsInOneSession}",
        [
            new("tests affected", e.MemberCount.ToString(CultureInfo.InvariantCulture)),
            new("failures", e.Failures.ToString(CultureInfo.InvariantCulture)),
            new("runs affected", $"{e.SessionsAffected} of {e.Sessions}"),
            new("worst run", $"{e.MaxTestsInOneSession} tests")
        ]);

    private static (string, IReadOnlyList<MetricDto>) DurationRegression(
        DurationRegressionEvidence e) =>
    (
        $"p50 {Duration(e.Baseline.P50Ms)} -> {Duration(e.Current.P50Ms)} " +
        $"({Signed(e.Delta.P50Pct)}), normalised {Signed(e.NormalisedDelta.P50Pct)}",
        [
            new("baseline p50", $"{Duration(e.Baseline.P50Ms)} over {e.Baseline.Executions} executions"),
            new("current p50", $"{Duration(e.Current.P50Ms)} over {e.Current.Executions} executions"),
            new("change", $"{Signed(e.Delta.P50Pct)} ({Signed(e.Delta.P50Ms)}ms)"),
            new("normalised change", Signed(e.NormalisedDelta.P50Pct))
        ]);

    private static (string, IReadOnlyList<MetricDto>) DurationUnstable(DurationUnstableEvidence e) =>
    (
        $"p50 {Duration(e.P50Ms)}, ranging {Duration(e.MinMs)} to {Duration(e.MaxMs)} " +
        $"over {e.Executions} executions, cv {Rate(e.Cv)}",
        [
            new("p50", Duration(e.P50Ms)),
            new("p95", Duration(e.P95Ms)),
            new("range", $"{Duration(e.MinMs)} to {Duration(e.MaxMs)}"),
            new("cv", $"{Rate(e.Cv)} over {e.Executions} executions")
        ]);

    private static (string, IReadOnlyList<MetricDto>) ParallelSensitive(
        ParallelSensitiveEvidence e) =>
    (
        $"failed {Percent(e.High.FailureRate)} above concurrency {e.SplitAtConcurrency} " +
        $"and {Percent(e.Low.FailureRate)} at or below, gap {Points(e.Delta.FailureRatePct)}",
        [
            new(
                $"above {e.SplitAtConcurrency}",
                $"{e.High.Failures} of {e.High.Executions} executions ({Percent(e.High.FailureRate)})"),
            new(
                $"at or below {e.SplitAtConcurrency}",
                $"{e.Low.Failures} of {e.Low.Executions} executions ({Percent(e.Low.FailureRate)})"),
            new("gap", Points(e.Delta.FailureRatePct)),
            new("concurrency seen", $"{e.Observed.Min} to {e.Observed.Max}")
        ]);

    private static (string, IReadOnlyList<MetricDto>) Vanished(VanishedEvidence e) =>
    (
        $"ran in {e.BaselineSessions} of {e.BaselineSessionCount} earlier runs, " +
        $"absent from the last {e.CurrentSessionCount}",
        [
            new("ran in", $"{e.BaselineSessions} of {e.BaselineSessionCount} earlier runs"),
            new("absent from", $"the last {e.CurrentSessionCount} runs"),
            new("executions", e.Executions.ToString(CultureInfo.InvariantCulture))
        ]);

    private static string Times(int count) =>
        count == 1 ? "once" : $"{count.ToString(CultureInfo.InvariantCulture)} times";

    private static string Runs(int count) =>
        count == 1 ? "1 run" : $"{count.ToString(CultureInfo.InvariantCulture)} runs";

    /// <summary>
    /// Formats a rate in [0,1] as a whole percentage.
    /// </summary>
    /// <remarks>
    /// The scaling is presentation, not analysis: the rate was already rounded to the precision the
    /// report publishes, and the unrounded figure it came from is not reachable from here. Reading
    /// "35%" is what makes the sentence quotable in a chat message; reading "0.35" is not.
    /// </remarks>
    private static string Percent(double rate) =>
        (rate * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Rate(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats an already-signed change, keeping the plus that a bare number would drop.
    /// </summary>
    private static string Signed(double percent) =>
        (percent >= 0 ? "+" : string.Empty) +
        percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Signed(long milliseconds) =>
        (milliseconds >= 0 ? "+" : string.Empty) +
        milliseconds.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a difference between two rates, in percentage points rather than percent.
    /// </summary>
    /// <remarks>
    /// The unit matters: 60% against 10% is a gap of 50 points, not of 500%, and the two readings
    /// differ by an order of magnitude.
    /// </remarks>
    private static string Points(double percentagePoints) =>
        Math.Abs(percentagePoints).ToString("0.#", CultureInfo.InvariantCulture) + " pts";

    /// <summary>
    /// Formats a duration at the scale a reader thinks in.
    /// </summary>
    private static string Duration(long milliseconds) => DurationFormatter.Format(milliseconds);
}
