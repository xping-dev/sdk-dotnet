/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// One run, published so the bin it landed in can be checked.
/// </summary>
/// <remarks>
/// <see cref="StartedAt"/> is the start of the run the execution belongs to, not of the execution
/// itself, for the reason the duration provider gives: the per-execution timestamp is reused across
/// retry attempts on the xUnit adapter. <see cref="LocalStartedAt"/> is that same instant shifted by
/// the offset the session recorded, formatted rather than typed because a
/// <see cref="DateTime"/> cannot carry an offset and a reader needs to see the one that was applied.
/// </remarks>
/// <param name="SessionId">The run it happened in.</param>
/// <param name="StartedAt">When that run started, in UTC.</param>
/// <param name="LocalStartedAt">The same instant on the machine's own clock.</param>
/// <param name="Sha">The commit it ran at, or <see langword="null"/> when none was recorded.</param>
/// <param name="Outcome">How it ended.</param>
internal sealed record TimeExemplar(
    string SessionId,
    DateTime StartedAt,
    string LocalStartedAt,
    string? Sha,
    string Outcome);

/// <summary>
/// One side of a temporal split, always carrying the counts its rate was computed from.
/// </summary>
/// <remarks>
/// Denominated in runs, not executions. A session is read on one clock, so every attempt of a test
/// within it falls in the same arm at the same local hour; the arm is built from one observation per
/// session and there is no second, execution-denominated count to publish alongside this one.
/// </remarks>
/// <param name="Failures">Runs in this arm that ended with the test failing.</param>
/// <param name="Sessions">Runs in this arm, one per session.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Sessions"/>.</param>
/// <param name="DistinctFailureDates">
/// Separate local calendar days this arm's <b>failures</b> fell on — the quantity the three-day gate
/// is applied to, published so a reader can check it. It is the difference between a pattern and an
/// incident, and no other figure here would show it: five failures across five days and five
/// failures in one afternoon produce identical rates. Counted over failures rather than over the
/// arm because an arm can span a fortnight while every failure in it sits in one evening.
/// </param>
/// <param name="Label">What this arm is called in the report, such as <c>12:00-18:00 local</c>.</param>
internal sealed record TimeArm(
    int Failures,
    int Sessions,
    double FailureRate,
    int DistinctFailureDates,
    string Label);

/// <summary>
/// The gap between the two arms, signed so the direction is read rather than derived.
/// </summary>
/// <remarks>
/// Positive means the test failed more in the arm the finding names, which is the usual case: the
/// worse arm is chosen first and the label follows it. The sign is still published, because the
/// arithmetic that produced it is the arithmetic the threshold was applied to and a reader checking
/// the claim should not have to reconstruct which way round it went.
/// </remarks>
/// <param name="FailureRate">Worse-arm rate minus other-arm rate.</param>
/// <param name="FailureRatePct">The same difference in percentage points.</param>
internal sealed record TimeDelta(double FailureRate, double FailureRatePct);

/// <summary>
/// Evidence that a test's failure rate moves with when it ran.
/// </summary>
/// <param name="Axis">
/// Which split fired: <c>LocalTimeOfDay</c>, <c>LocalDayGroup</c> or <c>UtcOffsetShift</c>. Named
/// rather than inferred from the labels, because a consumer filtering on the axis should not have to
/// parse prose to find it.
/// </param>
/// <param name="Worse">The arm holding the failures.</param>
/// <param name="Other">Everything else.</param>
/// <param name="Delta">The signed difference in failure rate, which the threshold was applied to.</param>
/// <param name="TimeZoneId">
/// The zone every considered run agreed on. Load-bearing rather than decorative: a local hour
/// means nothing without it, and the offset axis is only a daylight-saving shift because the zone
/// did not change.
/// </param>
/// <param name="Exemplars">Up to three runs from the worse arm, newest first.</param>
/// <param name="Contrast">One run typical of the other arm.</param>
internal sealed record TimeSensitiveEvidence(
    string Axis,
    TimeArm Worse,
    TimeArm Other,
    TimeDelta Delta,
    string TimeZoneId,
    IReadOnlyList<TimeExemplar> Exemplars,
    TimeExemplar? Contrast) : FindingEvidence;

/// <summary>
/// Reports tests whose failure rate depends on when they ran.
/// </summary>
/// <remarks>
/// <para>
/// Three axes are tried, each a two-arm split of the same shape the concurrency provider uses:
/// the local six-hour quarter of the day, weekend against weekday, and the UTC offset the machine
/// was on. The last is what makes a daylight-saving shift visible without a time zone database — a
/// window that straddles a transition simply contains two offsets for one zone, and those are the
/// arms.
/// </para>
/// <para>
/// <b>Every reading is local, and local is not derivable from the stored instant.</b> Timestamps are
/// UTC; "fails overnight" is a claim about the developer's clock. The offset that turns one into the
/// other is recorded per session, and a run whose session recorded none is <b>excluded</b>
/// rather than assumed to be on UTC — the same rule, for the same reason, that the concurrency
/// provider applies to an execution with no orchestration record. It follows that a store written
/// before that field existed produces nothing here, which is correct: the question was never asked
/// of those runs.
/// </para>
/// <para>
/// <b>This is a screening heuristic, not a hypothesis test.</b> Trying three axes and reporting the
/// widest gap is a multiple comparison, and nothing here corrects for one. Three things contain it:
/// an absolute threshold rather than a computed significance, a floor of five sessions each side,
/// and — the one that does the real work — a requirement that the failing arm span three distinct
/// local dates. Without the last, five failures in a single afternoon clear every other gate and get
/// reported as an afternoon pattern. At most one finding is emitted per test, for the widest
/// qualifying gap, so the three axes cannot each claim the same test.
/// </para>
/// <para>
/// <b>Every arm is one observation per session.</b> A test contributes the verdict of its deciding
/// attempt and nothing else, taken from <see cref="TestIndex.RunsOf"/>. This is not a refinement of
/// the arm gate but a condition of the split existing at all: <see cref="ClocksIn"/> resolves one
/// clock per session, so a retried test's attempts share an arm, a local hour, a day group and an
/// offset. Counted separately they were never separate observations of anything these axes ask
/// about — an arm of five could be two sessions with retries, and its failure rate was correlated
/// for the same reason its denominator was inflated. Aggregating first is what makes the arm counts
/// and the rate refer to the same units.
/// </para>
/// <para>
/// The kind is capped at <see cref="Severity.Medium"/>. A test that fails more at one time of day
/// has told you where to look and nothing about what to fix, and after three screened axes it has
/// told you that with less confidence than a concurrency split does.
/// </para>
/// </remarks>
internal sealed class TimeSensitiveProvider : IFindingProvider
{
    // Three, per the output contract's exemplar budget. A per-provider constant rather than a shared
    // threshold, matching the duration and concurrency providers: the specification's constant table
    // does not list it, and adding an entry would be a threshold this session invented.
    private const int MaxExemplars = 3;

    /// <summary>Hours in each local bin of the time-of-day axis.</summary>
    /// <remarks>
    /// Six, giving four bins. Twenty-four hourly bins over a default window of twenty sessions would
    /// leave under one run in each, and seven weekday bins little better — a split has to leave
    /// enough mass either side for a rate to mean anything, and coarse bins that occasionally fire
    /// are worth more than fine ones that never can.
    /// </remarks>
    private const int QuarterHours = 6;

    /// <inheritdoc/>
    public string Name => "time";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds => [FindingKind.TimeSensitive];

    /// <inheritdoc/>
    public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built once for the window rather than once per test: every test in a session shares its
        // clock reading, and resolving it per fingerprint would repeat the same arithmetic for each
        // of a suite's several hundred tests.
        Dictionary<Guid, SessionClock> clocks = ClocksIn(context);

        if (clocks.Count == 0)
            yield break;

        // Fingerprints are ordinal-sorted by the index, so findings come out in the same sequence on
        // every run whatever order the sessions were read in.
        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            FindingCandidate? candidate = Examine(context, clocks, fingerprint);

            if (candidate != null)
                yield return candidate;
        }
    }

    /// <summary>
    /// Reads the local clock of every session that recorded one.
    /// </summary>
    /// <remarks>
    /// Environmental sessions are dropped here rather than per test, for the reason §6 gives: an
    /// outage lands in whichever bin its sessions occupy and manufactures a gap out of a bad
    /// afternoon. Dropping them once also keeps every axis measuring the same population.
    /// </remarks>
    private static Dictionary<Guid, SessionClock> ClocksIn(AnalysisContext context)
    {
        var clocks = new Dictionary<Guid, SessionClock>();

        foreach (SessionView view in context.SessionViews)
        {
            if (view.IsLikelyEnvironmental)
                continue;

            TestSession session = view.Session;

            // Null is not zero. A session that recorded no offset is one the question cannot be
            // asked of, and treating it as UTC would file every such machine alongside the machines
            // that genuinely are on UTC.
            if (session.EnvironmentInfo is not { UtcOffset: { } offset } environment)
                continue;

            clocks[session.SessionId] = new SessionClock(
                session.StartedAt + offset,
                offset,
                environment.TimeZoneId ?? string.Empty);
        }

        return clocks;
    }

    /// <summary>
    /// Examines one test, returning <see langword="null"/> when every axis declines it.
    /// </summary>
    private static FindingCandidate? Examine(
        AnalysisContext context, Dictionary<Guid, SessionClock> clocks, string fingerprint)
    {
        List<Measured> considered = Considered(context, clocks, fingerprint);

        // Two arms' worth is the least that can be split at all, and checking here saves the axis
        // work for the overwhelming majority of tests.
        if (considered.Count < LocalAnalysisConstants.TimeSensitiveMinArmSessions * 2)
            return null;

        // One zone for the whole comparison. A machine that moved between zones has two populations
        // in it, and a local hour drawn from both describes neither; the offset axis in particular
        // would read the move as a daylight-saving shift.
        string? zone = SingleZone(considered);
        if (zone == null)
            return null;

        TestReference? test = context.Tests.ReferenceFor(fingerprint);
        if (test == null)
            return null;

        Split? best = null;

        // A fixed order, so that two axes producing the same gap resolve the same way on every run.
        foreach (Split split in Splits(considered))
        {
            if (best == null || Beats(split, best))
                best = split;
        }

        if (best == null)
            return null;

        List<Measured> failures = [.. best.Worse.Where(m => m.Reference.Failed)];

        return new FindingCandidate(
            FindingKind.TimeSensitive,
            new FindingSubject.SingleTest(test),
            new TimeSensitiveEvidence(
                best.Axis,
                Summarise(best.Worse, best.WorseLabel),
                Summarise(best.Other, best.OtherLabel),
                new TimeDelta(
                    FindingOrder.Round(best.Delta),
                    FindingOrder.RoundPercent(best.Delta * 100)),
                zone,
                Exemplars(failures),
                Contrast(best.Other)),

            // The least gap the two arms support, as the concurrency provider does and for the same
            // reason: five sessions a side is the smallest split allowed and produces the largest
            // observed deltas, so ranking on the observation put the thinnest evidence at the top.
            // The condition still thresholds the observed delta, so which tests are reported has not
            // changed — though which axis is reported about them can, because Beats now selects on
            // this quantity too rather than leaving the choice and the score to disagree.
            //
            // Compounded here by the axis search: the best of up to six splits is kept, and the
            // largest of six noisy gaps is larger still. The bound is not a multiplicity correction
            // and does not pretend to be one, but it does stop the winner of that search from
            // outranking a finding measured once — and it is the quantity that search now picks the
            // winner on, so the published axis and the rank cannot disagree.
            Unreliability: best.Support,

            // Dated by the failures that drove it rather than by the test's last run. A test
            // that failed overnight a fortnight ago and has run cleanly since should decay, and
            // dating it by its newest passing run would hold it at full recency forever.
            //
            // The worse arm always holds at least one failure: its rate exceeds the other arm's by
            // the threshold, so it cannot be zero.
            SessionsSinceLastOccurrence: failures.Min(m => m.Reference.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.TimeSensitive, test),

            // A clock reading locates a problem; it never names one. Left uncapped, the generic
            // impact formula would rank a frequently-run test that fails more in the evening above a
            // test that fails outright, on evidence that is a correlation over three screened axes.
            SeverityCeiling: Severity.Medium);
    }

    /// <summary>
    /// Decides whether one split describes the test better than another.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The better supported gap wins, not the wider one. This is what <see cref="TimeOfDaySplits"/>
    /// describes when it says a quarter with a huge rate over four runs must lose to a quarter
    /// with a smaller one over twelve; comparing the observed gaps never implemented it. It also
    /// stops the axis search disagreeing with the score: the finding is ranked on the winner's
    /// supported gap, so choosing the winner on anything else would publish the thinnest split and
    /// then rank it at nearly zero, discarding a better evidenced axis that was available.
    /// </para>
    /// <para>
    /// The observed gap breaks ties, and only then does naming. Several thin splits can support
    /// nothing at all and tie at zero, and between two splits neither of which the evidence
    /// separates, the wider observation is the more useful thing to show.
    /// </para>
    /// <para>
    /// The equality comparisons are exact on purpose. A tie here arises when two splits divided the
    /// same runs the same way, so both quantities were computed from identical counts and the
    /// doubles are bit-identical; a tolerance would only let genuinely different gaps tie.
    /// </para>
    /// </remarks>
    private static bool Beats(Split candidate, Split incumbent)
    {
        if (candidate.Support != incumbent.Support)
            return candidate.Support > incumbent.Support;

        if (candidate.Delta != incumbent.Delta)
            return candidate.Delta > incumbent.Delta;

        return candidate.WorseIsNamed && !incumbent.WorseIsNamed;
    }

    /// <summary>
    /// Collects the runs this test can be placed on a clock, in a stable order.
    /// </summary>
    /// <remarks>
    /// One entry per session, the deciding attempt, so <c>Reference.Failed</c> reads as "the test
    /// ended this session red" — the same verdict <see cref="SessionOutcomes"/> gives. A session
    /// whose clock is unknown, or which was discounted as environmental, has no entry at all.
    /// </remarks>
    private static List<Measured> Considered(
        AnalysisContext context, Dictionary<Guid, SessionClock> clocks, string fingerprint)
    {
        List<Measured> considered = [];

        foreach (ExecutionRef reference in context.Tests.RunsOf(fingerprint))
        {
            if (clocks.TryGetValue(reference.Session.SessionId, out SessionClock? clock))
                considered.Add(new Measured(reference, clock));
        }

        return considered;
    }

    /// <summary>
    /// Gets the zone every considered run agrees on, or <see langword="null"/> when they do not.
    /// </summary>
    /// <remarks>
    /// An empty identifier counts as disagreement rather than as its own zone. It means the SDK
    /// recorded an offset but could not name the zone it came from, and a finding that leaned on
    /// "the zone did not change" would be asserting something it never established.
    /// </remarks>
    private static string? SingleZone(List<Measured> considered)
    {
        string zone = considered[0].Clock.TimeZoneId;

        if (zone.Length == 0)
            return null;

        foreach (Measured measured in considered)
        {
            if (!string.Equals(measured.Clock.TimeZoneId, zone, StringComparison.Ordinal))
                return null;
        }

        return zone;
    }

    /// <summary>
    /// Produces every split that qualifies, across all three axes.
    /// </summary>
    private static IEnumerable<Split> Splits(List<Measured> considered)
    {
        foreach (Split split in TimeOfDaySplits(considered))
            yield return split;

        Split? dayGroup = DayGroupSplit(considered);
        if (dayGroup != null)
            yield return dayGroup;

        Split? offset = OffsetSplit(considered);
        if (offset != null)
            yield return offset;
    }

    /// <summary>
    /// Splits each local six-hour quarter against the other three.
    /// </summary>
    /// <remarks>
    /// Each quarter is offered separately rather than only the worst one, because "worst" is not
    /// known until the gap is computed and the arm gates have been applied — a quarter with a huge
    /// rate over four runs must lose to a quarter with a smaller one over twelve.
    /// </remarks>
    private static IEnumerable<Split> TimeOfDaySplits(List<Measured> considered)
    {
        for (int start = 0; start < 24; start += QuarterHours)
        {
            int lower = start;

            Split? split = Qualify(
                considered,
                m => m.Clock.LocalStartedAt.Hour >= lower &&
                     m.Clock.LocalStartedAt.Hour < lower + QuarterHours,
                nameof(TimeAxis.LocalTimeOfDay),
                QuarterLabel(lower),
                "the rest of the day");

            if (split != null)
                yield return split;
        }
    }

    /// <summary>
    /// Splits local weekend runs against local weekday ones.
    /// </summary>
    /// <remarks>
    /// Two groups rather than seven days. A fortnight holds at most two of any given weekday, so a
    /// per-day split can never reach the three distinct dates a claim needs; grouping is the finest
    /// division the default window can actually support.
    /// </remarks>
    private static Split? DayGroupSplit(List<Measured> considered) =>
        Qualify(
            considered,
            m => m.Clock.LocalStartedAt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            nameof(TimeAxis.LocalDayGroup),
            "weekend",
            "weekday");

    /// <summary>
    /// Splits the window at a change in the machine's UTC offset.
    /// </summary>
    /// <remarks>
    /// Fires only when exactly two offsets were observed, which — the zone having been held constant
    /// already — is what a daylight-saving transition inside the window looks like. Three or more
    /// offsets is not a transition and is left alone rather than forced into two arms.
    /// </remarks>
    private static Split? OffsetSplit(List<Measured> considered)
    {
        var offsets = new HashSet<TimeSpan>();
        foreach (Measured measured in considered)
            offsets.Add(measured.Clock.UtcOffset);

        if (offsets.Count != 2)
            return null;

        List<TimeSpan> ordered = [.. offsets.OrderBy(o => o)];
        TimeSpan lower = ordered[0];

        return Qualify(
            considered,
            m => m.Clock.UtcOffset == lower,
            nameof(TimeAxis.UtcOffsetShift),
            OffsetLabel(lower),
            OffsetLabel(ordered[1]));
    }

    /// <summary>
    /// Applies every gate to one candidate split and orients it towards the failing side.
    /// </summary>
    /// <param name="considered">The runs to divide.</param>
    /// <param name="predicate">What puts a run in the first arm.</param>
    /// <param name="axis">The axis this split belongs to.</param>
    /// <param name="insideLabel">What the first arm is called.</param>
    /// <param name="outsideLabel">What the second arm is called.</param>
    /// <returns>The split, or <see langword="null"/> when any gate declines it.</returns>
    private static Split? Qualify(
        List<Measured> considered,
        Func<Measured, bool> predicate,
        string axis,
        string insideLabel,
        string outsideLabel)
    {
        List<Measured> inside = [.. considered.Where(predicate)];
        List<Measured> outside = [.. considered.Where(m => !predicate(m))];

        if (inside.Count < LocalAnalysisConstants.TimeSensitiveMinArmSessions ||
            outside.Count < LocalAnalysisConstants.TimeSensitiveMinArmSessions)
        {
            return null;
        }

        double delta = FailureRate(inside) - FailureRate(outside);

        if (Math.Abs(delta) < LocalAnalysisConstants.TimeSensitivityDelta)
            return null;

        // The threshold above is absolute, so this is the only place the direction is resolved: a
        // test that fails only when it runs at night and one that fails only when it does not are
        // each a finding, and each is reported against the arm that holds its failures.
        (List<Measured> worse, string worseLabel, List<Measured> other, string otherLabel) = delta >= 0
            ? (inside, insideLabel, outside, outsideLabel)
            : (outside, outsideLabel, inside, insideLabel);

        // The guard that separates a pattern from an incident, applied to the failures rather than to
        // the arm: an arm can span a fortnight while every one of its failures sits in one afternoon.
        if (DistinctDates(worse.Where(m => m.Reference.Failed)) <
            LocalAnalysisConstants.TimeSensitiveMinArmDays)
        {
            return null;
        }

        return new Split(
            axis,
            worse,
            worseLabel,
            other,
            otherLabel,
            Math.Abs(delta),
            WilsonInterval.DifferenceBoundNearestZero(
                FailureCount(worse), worse.Count, FailureCount(other), other.Count),
            delta >= 0);
    }

    private static int DistinctDates(IEnumerable<Measured> runs)
    {
        var dates = new HashSet<DateTime>();

        foreach (Measured measured in runs)
            dates.Add(measured.Clock.LocalStartedAt.Date);

        return dates.Count;
    }

    private static double FailureRate(List<Measured> arm) => (double)FailureCount(arm) / arm.Count;

    private static int FailureCount(List<Measured> arm)
    {
        int failures = 0;
        foreach (Measured measured in arm)
        {
            if (measured.Reference.Failed)
                failures++;
        }

        return failures;
    }

    /// <remarks>
    /// No session set is built here. <see cref="Considered"/> already emits one entry per session, so
    /// <c>arm.Count</c> is the session count; deriving it a second way would only create somewhere
    /// for the two to disagree.
    /// </remarks>
    private static TimeArm Summarise(List<Measured> arm, string label)
    {
        int failures = FailureCount(arm);

        return new TimeArm(
            failures,
            arm.Count,
            FindingOrder.Round((double)failures / arm.Count),
            DistinctDates(arm.Where(m => m.Reference.Failed)),
            label);
    }

    /// <summary>
    /// Picks up to three of the failures that drove the finding, newest first.
    /// </summary>
    private static List<TimeExemplar> Exemplars(List<Measured> failures) =>
        [.. Ordered(failures).Take(MaxExemplars).Select(ToExemplar)];

    /// <summary>
    /// Picks one run typical of the other arm.
    /// </summary>
    /// <remarks>
    /// A passing run where the arm has one, because the pair only makes the difference
    /// reasonable about if the other side shows the behaviour the finding claims is absent there.
    /// Falls back to the newest run rather than to nothing when the other arm also failed — it
    /// failed less, which is the whole claim.
    /// </remarks>
    private static TimeExemplar? Contrast(List<Measured> other)
    {
        List<Measured> passing = [.. other.Where(m => !m.Reference.Failed)];

        Measured? typical = Ordered(passing.Count > 0 ? passing : other).FirstOrDefault();

        return typical == null ? null : ToExemplar(typical);
    }

    /// <summary>
    /// Orders runs newest first, breaking every tie totally.
    /// </summary>
    /// <remarks>
    /// One run per session makes SessionIndex alone a total order; the attempt and identifier
    /// tie-breaks are kept because the ordering must stay total if that ever stops being true.
    /// </remarks>
    private static IOrderedEnumerable<Measured> Ordered(List<Measured> runs) =>
        runs
            .OrderBy(m => m.Reference.SessionIndex)
            .ThenBy(m => m.Reference.Execution.Retry?.AttemptNumber ?? 1)
            .ThenBy(m => m.Reference.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

    private static TimeExemplar ToExemplar(Measured measured) =>
        new(
            measured.Reference.Session.SessionId.ToString("D", CultureInfo.InvariantCulture),
            measured.Reference.Session.StartedAt,
            measured.Clock.LocalStartedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            RevisionContext.ReadSha(measured.Reference.Session),
            measured.Reference.Execution.Outcome.ToString());

    private static string QuarterLabel(int startHour) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{startHour:D2}:00-{startHour + QuarterHours:D2}:00 local");

    /// <summary>
    /// Formats an offset the way a reader writes one, always signed and always to the minute.
    /// </summary>
    /// <remarks>
    /// ASCII throughout, per the output contract: this reaches a headline, and a headline is read out
    /// of a terminal on a code page nobody chose.
    /// </remarks>
    private static string OffsetLabel(TimeSpan offset) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"UTC{(offset < TimeSpan.Zero ? '-' : '+')}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}");

    /// <summary>
    /// The axes a split can belong to.
    /// </summary>
    /// <remarks>
    /// A private enum used only through <c>nameof</c>. The axis reaches JSON as a string, and naming
    /// the three in one place is what stops the provider and its tests spelling them differently.
    /// </remarks>
    private enum TimeAxis
    {
        /// <summary>A local six-hour quarter against the rest of the day.</summary>
        LocalTimeOfDay,

        /// <summary>Local weekend against local weekday.</summary>
        LocalDayGroup,

        /// <summary>One recorded UTC offset against the other.</summary>
        UtcOffsetShift
    }

    /// <summary>
    /// One session's clock, resolved once.
    /// </summary>
    /// <param name="LocalStartedAt">When the run started on the machine's own clock.</param>
    /// <param name="UtcOffset">The offset that produced it.</param>
    /// <param name="TimeZoneId">The zone it came from, empty when the SDK could not name one.</param>
    private sealed record SessionClock(DateTime LocalStartedAt, TimeSpan UtcOffset, string TimeZoneId);

    /// <summary>
    /// One run together with the clock it was read on.
    /// </summary>
    /// <param name="Reference">The run's deciding attempt and the session it belongs to.</param>
    /// <param name="Clock">That session's local clock.</param>
    private sealed record Measured(ExecutionRef Reference, SessionClock Clock);

    /// <summary>
    /// A two-arm division that cleared every gate, oriented towards the failing side.
    /// </summary>
    /// <param name="Axis">Which axis produced it.</param>
    /// <param name="Worse">The arm holding the failures.</param>
    /// <param name="WorseLabel">What that arm is called.</param>
    /// <param name="Other">Everything else.</param>
    /// <param name="OtherLabel">What that arm is called.</param>
    /// <param name="Delta">The absolute gap in failure rate between them.</param>
    /// <param name="Support">
    /// The least gap the two arms support — the Newcombe bound of the same difference, 0 when the
    /// interval still admits no difference at all. What the split is chosen and ranked on.
    /// </param>
    /// <param name="WorseIsNamed">
    /// Whether <paramref name="Worse"/> is the side the predicate selected rather than its
    /// complement. Only a named side carries a time a reader can act on.
    /// </param>
    private sealed record Split(
        string Axis,
        List<Measured> Worse,
        string WorseLabel,
        List<Measured> Other,
        string OtherLabel,
        double Delta,
        double Support,
        bool WorseIsNamed);
}
