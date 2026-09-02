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
        RetryDeepeningEvidence deepening => RetryDeepening(deepening),
        RetryExhaustedEvidence exhausted => RetryExhausted(exhausted),
        FlakyEvidence flaky => Flaky(flaky),
        AlwaysFailingEvidence always => AlwaysFailing(always),
        TimingOutEvidence timingOut => TimingOut(timingOut),
        BrokenFixtureEvidence fixture => BrokenFixture(fixture),
        SharedFailureEvidence shared => SharedFailure(shared),
        DurationRegressionEvidence regression => DurationRegression(regression),
        DurationUnstableEvidence unstable => DurationUnstable(unstable),
        ParallelSensitiveEvidence parallel => ParallelSensitive(parallel),
        TimeSensitiveEvidence time => TimeSensitive(time),
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
            new("deepest attempt", e.MaxAttemptObserved.ToString(CultureInfo.InvariantCulture)),
            new("time retrying", Duration(e.RetryWallClockMs))
        ];

        AppendConfiguration(metrics, e.Configuration, e.ConfiguredDelayTotalMs);

        return (headline, metrics);
    }

    /// <summary>
    /// Phrases a test that now needs more attempts to pass than it used to.
    /// </summary>
    /// <remarks>
    /// Leads with the pair of counts rather than with a percentage: "1 -&gt; 3" is the whole finding,
    /// and "+200%" is the same fact said in a unit nobody retries in. The count of runs behind each
    /// side is in the sentence, because it is what separates a trend from a fortnight ago's noise.
    /// Those two counts share one trailing unit, as every other headline here does with "3 of 12
    /// runs": naming it on the first number and not the second reads as though the two were counting
    /// different things, and naming it twice reads as though they might not be. The word is literal
    /// rather than pluralised, because it has to follow "earlier" rather than the number it counts,
    /// and because the provider's floors put both counts at two or more.
    /// </remarks>
    private static (string, IReadOnlyList<MetricDto>) RetryDeepening(RetryDeepeningEvidence e)
    {
        string headline =
            $"attempts to pass {e.Baseline.TypicalAttempts} -> {e.Current.TypicalAttempts} " +
            $"({Signed(e.Delta.Attempts)}) across {e.Current.RunsSettledGreen} recent and " +
            $"{e.Baseline.RunsSettledGreen} earlier runs";

        if (e.RetryWallClockMs > 0)
            headline += $", {Duration(e.RetryWallClockMs)} spent retrying";

        List<MetricDto> metrics =
        [
            new(
                "attempts to pass",
                $"{e.Baseline.TypicalAttempts} -> {e.Current.TypicalAttempts} ({Signed(e.Delta.Attempts)})"),
            new("recent runs", $"{e.Current.RunsSettledGreen} of {e.Current.Runs} passed"),
            new("earlier runs", $"{e.Baseline.RunsSettledGreen} of {e.Baseline.Runs} passed"),
            new("deepest attempt", e.Current.MaxAttempts.ToString(CultureInfo.InvariantCulture)),
            new("time retrying", Duration(e.RetryWallClockMs))
        ];

        AppendConfiguration(metrics, e.Configuration, e.ConfiguredDelayTotalMs);

        return (headline, metrics);
    }

    /// <summary>
    /// Phrases a test whose retries ran out.
    /// </summary>
    /// <remarks>
    /// The declared limit is a metric and never the headline. It is the number the attribute wrote
    /// down, the frameworks disagree about what it counts, and putting it in the sentence would
    /// invite a reader to do the subtraction this report deliberately refuses to do.
    /// </remarks>
    private static (string, IReadOnlyList<MetricDto>) RetryExhausted(RetryExhaustedEvidence e)
    {
        string headline =
            $"gave up after {Attempts(e.MaxAttemptObserved)} in {e.ExhaustedRuns} of " +
            $"{e.RetriedRuns} retried runs ({Percent(e.ExhaustedRate)})";

        if (e.RetryWallClockMs > 0)
            headline += $", {Duration(e.RetryWallClockMs)} spent retrying";

        List<MetricDto> metrics =
        [
            new(
                "gave up",
                $"{e.ExhaustedRuns} of {e.RetriedRuns} retried runs ({Percent(e.ExhaustedRate)})"),
            new("rescued", $"{e.RescuedRuns} of {e.RetriedRuns}"),
            new("runs affected", $"{e.ExhaustedRuns} of {e.RunsConsidered}"),
            new("deepest attempt", e.MaxAttemptObserved.ToString(CultureInfo.InvariantCulture)),
            new("retries spent", e.RetryAttemptsSpent.ToString(CultureInfo.InvariantCulture)),
            new("time retrying", Duration(e.RetryWallClockMs))
        ];

        AppendConfiguration(metrics, e.Configuration, e.ConfiguredDelayTotalMs);

        return (headline, metrics);
    }

    /// <summary>
    /// Appends the pairs a retry attribute declared, each only when it declared one.
    /// </summary>
    /// <remarks>
    /// The declared limit is always stated, with its provenance in the value rather than in the
    /// label: a reader who sees "3" beside an observed fourth attempt must be able to see that the
    /// two numbers were written by different parties and are not in contradiction. Configured waiting
    /// is a separate pair from measured attempt time and is never added to it — whether the framework
    /// actually waited is not in the session.
    /// </remarks>
    private static void AppendConfiguration(
        List<MetricDto> metrics, RetryConfiguration configuration, long configuredDelayTotalMs)
    {
        metrics.Add(new MetricDto(
            "declared limit",
            configuration.MaxRetriesAsDeclared > 0
                ? $"{configuration.MaxRetriesAsDeclared.ToString(CultureInfo.InvariantCulture)} " +
                  "(as the attribute declared it)"
                : "none recorded by the adapter"));

        if (configuration.AttributeName is { Length: > 0 } attribute)
            metrics.Add(new MetricDto("mechanism", attribute));

        if (configuration.Reason is { Length: > 0 } reason)
            metrics.Add(new MetricDto("declared reason", reason));

        if (configuredDelayTotalMs > 0)
            metrics.Add(new MetricDto("configured wait", Duration(configuredDelayTotalMs)));
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
        // "One failure mode" is a claim about every failure, and the classification no longer
        // requires that. Where the failures were not identical the share says so, so a reader who
        // opens two exemplars and finds different messages is not left thinking the report misread
        // them. Counted rather than read off the published share, which is rounded to three places
        // and would round a share of 1999 failures in 2000 up into that claim.
        bool sole = e.Signature.Occurrences == e.Failures;

        string mode = sole
            ? "one failure mode"
            : $"one dominant failure mode ({Percent(e.ModalSignatureShare)} of failures)";

        string headline =
            $"failed {e.Failures} of {e.Executions} executions ({Percent(e.FailureRate)}), {mode}";

        // Named only when the adapter recorded a type. An adapter that captures no failure detail is
        // not the same as a failure that had none, and inventing a name here would hide the gap.
        if (e.Signature.ExceptionType is { Length: > 0 } type)
            headline += $": {type}";

        List<MetricDto> metrics =
        [
            new("failed", $"{e.Failures} of {e.Executions} executions ({Percent(e.FailureRate)})"),
            new("runs affected", $"{e.SessionsWithFailures} of {e.Sessions}"),
            new("failure mode", e.Signature.ExceptionType ?? "not recorded by the adapter")
        ];

        if (!sole)
            metrics.Add(new MetricDto("dominant mode", $"{Percent(e.ModalSignatureShare)} of failures"));

        return (headline, metrics);
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
        // Leads with the normalised figure because it is the claim: the raw pair can fall while the
        // test slows, when the recent runs happened on a faster machine, and a finding labelled
        // "slower" whose sentence opens by saying the test got faster reads as a bug.
        //
        // And leads with the interval rather than with the estimate alone. "1.8x slower" invites
        // the reader to treat the number as settled; "1.8x slower (95% CI 1.1-3.4x)" tells them how
        // much of it the runs behind it actually establish, which on three recent runs is the more
        // useful half of the sentence.
        $"{Multiple(e.Shift.Ratio)} slower " +
        $"(95% CI {Rate(e.Shift.RatioLow)}-{Multiple(e.Shift.RatioHigh)}), " +
        $"{Duration(e.Baseline.P50Ms)} -> {Duration(e.Current.P50Ms)} on the clock",
        [
            new("baseline p50", $"{Duration(e.Baseline.P50Ms)} over {e.Baseline.Executions} executions"),
            new("current p50", $"{Duration(e.Current.P50Ms)} over {e.Current.Executions} executions"),
            new("change", $"{Signed(e.Delta.P50Pct)} ({Signed(e.Delta.P50Ms)}ms)"),
            new(
                "slowdown",
                $"{Multiple(e.Shift.Ratio)} (95% CI {Rate(e.Shift.RatioLow)}-" +
                $"{Multiple(e.Shift.RatioHigh)}), {Signed(e.Shift.Ms)}ms at reference speed"),

            // The run counts belong to the p-value, not to the percentiles above: they are what the
            // comparison read, one reading each, and they are what bounds how small the p-value
            // could possibly have been.
            new(
                "significance",
                $"p {Probability(e.Shift.PValue)} one-sided, " +
                $"{e.Current.ComparedSessions} recent runs against {e.Baseline.ComparedSessions}")
        ]);

    private static (string, IReadOnlyList<MetricDto>) DurationUnstable(DurationUnstableEvidence e) =>
    (
        // The count belongs to the dispersion, which is what it was computed over. Attached to
        // the range, as it used to be, a reader carries it across to the dispersion beside it and
        // reads a spread over more executions than it was measured on.
        $"p50 {Duration(e.P50Ms)}, ranging {Duration(e.MinMs)} to {Duration(e.MaxMs)}, " +
        $"dispersion {Rate(e.Dispersion)} over {e.NormalisedExecutions} executions",
        [
            new("p50", Duration(e.P50Ms)),
            new("p95", Duration(e.P95Ms)),
            new("range", $"{Duration(e.MinMs)} to {Duration(e.MaxMs)}"),
            new(
                "dispersion",
                $"{Rate(e.Dispersion)} over {e.NormalisedExecutions} executions")
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

    /// <summary>
    /// Phrases a split of a test's executions by when they ran.
    /// </summary>
    /// <remarks>
    /// The distinct-day count is in the headline rather than only in the metrics, because it is what
    /// separates this finding from a coincidence and a reader skimming a fence will not open the
    /// evidence to look for it. "failed 80% in 18:00-24:00 local" invites belief on its own; the same
    /// sentence ending "across 4 days" says how much belief it has earned.
    /// </remarks>
    private static (string, IReadOnlyList<MetricDto>) TimeSensitive(TimeSensitiveEvidence e) =>
    (
        $"failed {Percent(e.Worse.FailureRate)} in {e.Worse.Label} against " +
        $"{Percent(e.Other.FailureRate)} in {e.Other.Label}, gap {Points(e.Delta.FailureRatePct)} " +
        $"across {Days(e.Worse.DistinctFailureDates)}",
        [
            new(
                e.Worse.Label,
                $"{e.Worse.Failures} of {Runs(e.Worse.Sessions)} ({Percent(e.Worse.FailureRate)})"),
            new(
                e.Other.Label,
                $"{e.Other.Failures} of {Runs(e.Other.Sessions)} ({Percent(e.Other.FailureRate)})"),
            new("gap", Points(e.Delta.FailureRatePct)),
            new("spread", $"failures on {Days(e.Worse.DistinctFailureDates)}"),
            new("time zone", e.TimeZoneId)
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

    private static string Days(int count) =>
        count == 1 ? "1 day" : $"{count.ToString(CultureInfo.InvariantCulture)} days";

    private static string Attempts(int count) =>
        count == 1 ? "1 attempt" : $"{count.ToString(CultureInfo.InvariantCulture)} attempts";

    /// <summary>
    /// Formats a whole-number change, keeping the plus that a bare number would drop.
    /// </summary>
    private static string Signed(int value) =>
        (value >= 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture);

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
    /// Formats a multiplier, keeping the "x" that says it is one.
    /// </summary>
    /// <remarks>
    /// An "x" and not a multiplication sign: the shareable output is asserted to be printable ASCII,
    /// so that a finding pasted into a terminal, a commit message or a chat window arrives as it
    /// left.
    /// </remarks>
    private static string Multiple(double ratio) =>
        ratio.ToString("0.##", CultureInfo.InvariantCulture) + "x";

    /// <summary>
    /// Formats a probability small enough to be worth reporting.
    /// </summary>
    /// <remarks>
    /// Three decimals, which is the precision the provider publishes and enough to separate the
    /// floors these p-values sit on — 1/1140 is 0.001 and 1/56 is 0.018. Anything below the last
    /// decimal is reported as a bound rather than as a zero, which would claim an impossible
    /// certainty.
    /// </remarks>
    private static string Probability(double value) =>
        value < 0.001
            ? "<0.001"
            : value.ToString("0.###", CultureInfo.InvariantCulture);

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
