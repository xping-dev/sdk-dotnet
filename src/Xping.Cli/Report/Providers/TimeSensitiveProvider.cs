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
/// How probable the reported gap would be if the test did not care when it ran, and how many
/// comparisons were made before this one was chosen.
/// </summary>
/// <remarks>
/// Both figures or neither. A p-value read without knowing how many splits were tried is not a
/// probability a reader can act on, and the correction that turns the one into the other is only
/// checkable if the multiplier is published beside its result.
/// </remarks>
/// <param name="PValue">
/// Fisher's exact test on the two arms, two-sided, already multiplied by
/// <paramref name="ComparisonsTried"/>. Two-sided because the direction was discovered from the
/// data rather than pre-registered: the failing side is resolved after the comparison, and a
/// one-sided p taken afterwards would be half the p the comparison earned.
/// </param>
/// <param name="ComparisonsTried">
/// Distinct divisions of this test's runs the axis search actually performed — the number the
/// p-value was multiplied by. Distinct rather than offered: where a window's runs fall in only two
/// local quarters, "this quarter against the rest" and "that quarter against the rest" divide the
/// same sessions the same way and are one comparison, not two.
/// </param>
internal sealed record TimeSignificance(double PValue, int ComparisonsTried);

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
/// <param name="Significance">
/// What the gap is worth once the breadth of the axis search has been charged for.
/// </param>
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
    TimeSignificance Significance,
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
/// <b>The search is charged for.</b> Trying three axes and keeping the best of up to six splits is a
/// multiple comparison, and the maximum of six dependent comparisons is systematically wider than
/// any one of them. Each split that has two arms to compare therefore carries a two-sided Fisher
/// exact p-value, Bonferroni-corrected by the number of <i>distinct</i> divisions this test's runs
/// actually admitted — a window whose runs fall in two quarters performed one comparison and is
/// charged for one. The winner is the split with the smallest corrected p-value, and it is reported
/// only if that clears <see cref="Alpha"/>. At most one finding is emitted per test, so the three
/// axes cannot each claim the same one.
/// </para>
/// <para>
/// Beside the test, two practical bars it cannot supply. A gap must reach
/// <see cref="LocalAnalysisConstants.TimeSensitivityDelta"/>, because a reliable five-point
/// difference is not worth a developer's morning; and the failing arm must span three distinct local
/// dates, because five failures inside one afternoon are significant and are still an incident
/// rather than a pattern. Neither is redundant with the test and neither replaces it.
/// </para>
/// <para>
/// <b>What the bar amounts to.</b> Take the clearest shape there is — an arm that failed every time
/// against one that never did — and count the failures needed to report it. Against a single
/// comparison it is five, from six runs a side to sixteen; from seventeen up it is six, because five
/// no longer reaches a gap of 0.30 and the delta takes over from the p-value as the binding bar.
/// Against two comparisons it is six almost throughout, and against four, six to seven. Five
/// failures is therefore the floor of what this kind asks and not a promise: how many it actually
/// asks for depends on how wide the search that found the split was.
/// </para>
/// <para>
/// This kind is consequently quiet on a short history, which is the honest answer: four red evenings
/// out of six against a clean morning is among the commonest things chance produces, and it used to
/// be reported as a pattern.
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

    // The corrected p-value a split has to clear. Local rather than shared, for the reason the
    // duration provider gives about its own: the specification's constant table does not name it,
    // and adding an entry there would be a threshold this session invented.
    //
    // The conventional level, which is what #161 asks for: "emits a finding in under five percent
    // of runs" on a test with no time effect in it. It is affordable here in a way it was not for a
    // duration regression, because Fisher conditions on both margins and the resulting ladder of
    // attainable p-values is coarse -- the measured null emission rate through the whole gate chain
    // is well under the level rather than at it. Tightening to 0.01 would cost the shapes a default
    // window can actually show: six failing evenings against one failing morning is p = 0.0152, and
    // a fortnight of runs rarely offers better.
    //
    // A pre-filter, not the final word. This charges one test for the axes it searched; it cannot
    // charge a suite for the three hundred tests it searched them on, because a provider by contract
    // cannot see the others. #160 applies a Benjamini-Hochberg pass across every fingerprint each
    // kind was tested on and will supersede this bar.
    private const double Alpha = 0.05;

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

        // Every division this test's runs admit, before any of them is judged. The multiplicity the
        // search has to be charged for has to be known before the first p-value is computed, which
        // is why the arm gate is separated from every gate after it.
        List<Partition> partitions = [.. Offered(considered)];

        if (partitions.Count == 0)
            return null;

        int comparisons = Comparisons(partitions);

        Split? best = null;

        // A fixed order, so that two axes producing the same evidence resolve the same way on every
        // run.
        foreach (Partition partition in partitions)
        {
            Split? split = Judge(partition, comparisons);

            if (split != null && (best == null || Beats(split, best)))
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
                new TimeSignificance(Probability(best.PAdjusted), comparisons),
                zone,
                Exemplars(failures),
                Contrast(best.Other)),

            // The least gap the two arms support, as the concurrency provider does and for the same
            // reason: five sessions a side is the smallest split allowed and produces the largest
            // observed deltas, so ranking on the observation put the thinnest evidence at the top.
            //
            // Whether a gap is real is now settled before this is read, so the bound is left to do
            // the one job it is good at: saying how large the gap is, on a scale that grows with the
            // runs behind it. It is also the first tie-break in the selection, and Fisher's ladder
            // of attainable p-values is coarse enough on small tables that ties are the normal case
            // — so the axis that gets published is the best evidenced of those the test could not
            // separate, and the published axis and the rank cannot disagree.
            Unreliability: best.Support,

            // Dated by the failures that drove it rather than by the test's last run. A test
            // that failed overnight a fortnight ago and has run cleanly since should decay, and
            // dating it by its newest passing run would hold it at full recency forever.
            //
            // The worse arm always holds at least one failure: its rate exceeds the other arm's by
            // the threshold, so it cannot be zero.
            SessionsSinceLastOccurrence: failures.Min(m => m.Reference.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.TimeSensitive, test),

            // Unrounded, unlike the copy in the evidence. This is the number #160 sorts on, and
            // rounding two neighbouring p-values onto each other would reorder the ranked list a
            // Benjamini-Hochberg pass walks down.
            PValue: best.PAdjusted,

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
    /// The least probable division wins. Every split offered for one test is charged the same
    /// multiplier, so ordering by the corrected p-value and by the raw one come to the same thing;
    /// the corrected figure is compared because it is the one that was gated and published.
    /// </para>
    /// <para>
    /// The better supported gap breaks the tie, then the wider observed one, then naming. Ties are
    /// the normal case rather than an edge one: Fisher conditions on both margins, so the attainable
    /// p-values on a table of five or six a side are a short ladder and several splits routinely
    /// land on the same rung. This is also what <see cref="TimeOfDaySplits"/> describes when it says
    /// a quarter with a huge rate over four runs must lose to a quarter with a smaller one over
    /// twelve, and it keeps the axis search agreeing with the score — the finding is ranked on the
    /// winner's supported gap, so choosing between equally probable splits on anything else would
    /// publish the thinnest and then rank it at nearly zero.
    /// </para>
    /// <para>
    /// The equality comparisons are exact on purpose. A tie here arises when two splits divided the
    /// same runs the same way, so every quantity was computed from identical counts and the doubles
    /// are bit-identical; a tolerance would only let genuinely different gaps tie. That holds for
    /// the p-value too, and not by luck: <see cref="Judge"/> orients each division towards its
    /// failing arm <i>before</i> testing it, so two orientations of one division call
    /// <see cref="FisherExact"/> with the same four numbers rather than with swapped rows. Were the
    /// test handed the arms unoriented, the two calls would enumerate the same tables in different
    /// orders, differ in the last bit, and quietly stop the naming tie-break below from ever firing.
    /// </para>
    /// </remarks>
    private static bool Beats(Split candidate, Split incumbent)
    {
        if (candidate.PAdjusted != incumbent.PAdjusted)
            return candidate.PAdjusted < incumbent.PAdjusted;

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
    /// Counts the distinct divisions among those offered — the multiplicity to charge for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything past the arm gate is deliberately not consulted. A division that goes on to fail
    /// the delta or the distinct-day bar was still a comparison this test performed, and counting
    /// only the divisions that survived would price the search after seeing how it came out, which
    /// is the same error in miniature that the search itself commits.
    /// </para>
    /// <para>
    /// Distinct by the division rather than by the axis that proposed it. Where a window's runs fall
    /// in only two quarters, "the evening against the rest of the day" and "the morning against the
    /// rest of the day" put the same sessions on the same two sides and differ only in which side is
    /// named — one comparison offered twice. Charging two for it would halve the level for the
    /// commonest shape a real window takes. It also catches the cross-axis coincidence, where a
    /// window's weekend runs happen to be exactly its evening runs.
    /// </para>
    /// <para>
    /// Only the count is reduced. Every offered division stays a candidate, because which of two
    /// orientations gets reported is a question about labels rather than about evidence, and it is
    /// the one <see cref="Beats"/>'s last tie-break exists to settle: keeping an arbitrary
    /// representative would publish "the rest of the day" as the failing side half the time.
    /// </para>
    /// </remarks>
    private static int Comparisons(List<Partition> partitions)
    {
        int distinct = 0;

        for (int i = 0; i < partitions.Count; i++)
        {
            bool seen = false;

            for (int j = 0; j < i; j++)
            {
                if (Same(partitions[j], partitions[i]))
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
                distinct++;
        }

        return distinct;
    }

    /// <summary>
    /// Decides whether two divisions put the same runs on the same two sides.
    /// </summary>
    /// <remarks>
    /// A division and its complement are the same division, so the sides are compared both ways
    /// round. Sizes first, which settles the overwhelming majority without looking at a session id.
    /// </remarks>
    private static bool Same(Partition first, Partition second) =>
        (first.Inside.Count == second.Inside.Count && Holds(first.Inside, second.Inside)) ||
        (first.Inside.Count == second.Outside.Count && Holds(first.Inside, second.Outside));

    /// <summary>
    /// Decides whether two arms hold the same sessions.
    /// </summary>
    /// <remarks>
    /// Both arms are built by filtering one stably ordered list, so equal arms hold their sessions
    /// in the same order and a positional walk answers this without allocating a set. Ordering is a
    /// property of <see cref="Considered"/> rather than of the order sessions were read in, which is
    /// what keeps two reports over one unchanged store byte-identical.
    /// </remarks>
    private static bool Holds(List<Measured> arm, List<Measured> other)
    {
        for (int i = 0; i < arm.Count; i++)
        {
            if (arm[i].Reference.Session.SessionId != other[i].Reference.Session.SessionId)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Produces every division the three axes offer, in a fixed order.
    /// </summary>
    private static IEnumerable<Partition> Offered(List<Measured> considered)
    {
        foreach (Partition partition in TimeOfDaySplits(considered))
            yield return partition;

        Partition? dayGroup = DayGroupSplit(considered);
        if (dayGroup != null)
            yield return dayGroup;

        Partition? offset = OffsetSplit(considered);
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
    private static IEnumerable<Partition> TimeOfDaySplits(List<Measured> considered)
    {
        for (int start = 0; start < 24; start += QuarterHours)
        {
            int lower = start;

            Partition? split = Divide(
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
    private static Partition? DayGroupSplit(List<Measured> considered) =>
        Divide(
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
    private static Partition? OffsetSplit(List<Measured> considered)
    {
        var offsets = new HashSet<TimeSpan>();
        foreach (Measured measured in considered)
            offsets.Add(measured.Clock.UtcOffset);

        if (offsets.Count != 2)
            return null;

        List<TimeSpan> ordered = [.. offsets.OrderBy(o => o)];
        TimeSpan lower = ordered[0];

        return Divide(
            considered,
            m => m.Clock.UtcOffset == lower,
            nameof(TimeAxis.UtcOffsetShift),
            OffsetLabel(lower),
            OffsetLabel(ordered[1]));
    }

    /// <summary>
    /// Divides the runs in two, keeping the division only if both sides can carry a rate.
    /// </summary>
    /// <param name="considered">The runs to divide.</param>
    /// <param name="predicate">What puts a run in the first arm.</param>
    /// <param name="axis">The axis this division belongs to.</param>
    /// <param name="insideLabel">What the first arm is called.</param>
    /// <param name="outsideLabel">What the second arm is called.</param>
    /// <returns>The division, or <see langword="null"/> when either side is too thin.</returns>
    /// <remarks>
    /// The arm gate and nothing else. It is the one condition that decides whether a comparison was
    /// available to make, and therefore the only one that can be applied before the comparisons are
    /// counted — every bar after it reads the outcome, and applying those first would price the
    /// search on the strength of how it turned out. <see cref="Judge"/> holds the rest.
    /// </remarks>
    private static Partition? Divide(
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

        return new Partition(axis, inside, insideLabel, outside, outsideLabel);
    }

    /// <summary>
    /// Applies every remaining gate to one division and orients it towards the failing side.
    /// </summary>
    /// <param name="partition">The division to judge.</param>
    /// <param name="comparisons">How many distinct divisions this test's runs admitted.</param>
    /// <returns>The split, or <see langword="null"/> when any gate declines it.</returns>
    /// <remarks>
    /// Ordered so the exact test is asked last, of the few divisions that could still produce a
    /// finding: enumerating every table the margins permit costs more than the two rates and the
    /// date count above it, and in a real window nearly every division has already been declined.
    /// </remarks>
    private static Split? Judge(Partition partition, int comparisons)
    {
        List<Measured> inside = partition.Inside;
        List<Measured> outside = partition.Outside;

        double delta = FailureRate(inside) - FailureRate(outside);

        if (Math.Abs(delta) < LocalAnalysisConstants.TimeSensitivityDelta)
            return null;

        // The threshold above is absolute, so this is the only place the direction is resolved: a
        // test that fails only when it runs at night and one that fails only when it does not are
        // each a finding, and each is reported against the arm that holds its failures.
        (List<Measured> worse, string worseLabel, List<Measured> other, string otherLabel) = delta >= 0
            ? (inside, partition.InsideLabel, outside, partition.OutsideLabel)
            : (outside, partition.OutsideLabel, inside, partition.InsideLabel);

        // The guard that separates a pattern from an incident, applied to the failures rather than to
        // the arm: an arm can span a fortnight while every one of its failures sits in one afternoon.
        if (DistinctDates(worse.Where(m => m.Reference.Failed)) <
            LocalAnalysisConstants.TimeSensitiveMinArmDays)
        {
            return null;
        }

        // And then the question none of the bars above asks: is this how the failures would have
        // fallen anyway? A gap says how far apart two rates are and nothing about how often five
        // runs and six divide a handful of failures that unevenly by themselves.
        double raw = FisherExact.TwoSidedPValue(
            FailureCount(worse), worse.Count, FailureCount(other), other.Count);

        // Bonferroni over the divisions this test's runs admitted. Crude beside Benjamini-Hochberg,
        // and the right instrument here: there are at most six of them, they are heavily dependent —
        // every one divides the same runs — and the guarantee wanted at this scale is that the
        // reported split is real, not that a bounded share of a long list is.
        double adjusted = Math.Min(1.0, raw * comparisons);

        if (adjusted > Alpha)
            return null;

        return new Split(
            partition.Axis,
            worse,
            worseLabel,
            other,
            otherLabel,
            Math.Abs(delta),
            WilsonInterval.DifferenceBoundNearestZero(
                FailureCount(worse), worse.Count, FailureCount(other), other.Count),
            delta >= 0,
            adjusted);
    }

    /// <summary>
    /// Rounds a p-value for publication, to three significant digits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Significant digits rather than the decimal places <see cref="FindingOrder.Round"/> gives
    /// every other published figure, and rather than the six decimals the duration provider gives
    /// its own. That figure is safe there because the comparison behind it is a fixed three recent
    /// runs against at most forty, which floors its p-value at 1/12341 and cannot go lower however
    /// long the history is. Nothing floors this one: it is the probability of the observed table,
    /// and that falls off a cliff as the window grows — a perfectly separated split of thirteen runs
    /// against thirteen is 1.9e-7, which six decimals publish as zero. Twenty-six runs is an
    /// ordinary window, and a probability of zero is a claim of certainty this measurement never
    /// makes.
    /// </para>
    /// <para>
    /// Three digits because that is the precision the figure has to a reader deciding whether to
    /// believe a finding. The unrounded value is what reaches the coordinator; this is only what
    /// gets written down.
    /// </para>
    /// <para>
    /// Through a round-trip rather than <see cref="Math.Round(double, int)"/>, which takes a count of
    /// decimal places and refuses more than fifteen. Deriving that count from the magnitude
    /// reintroduces the defect this method exists to avoid, one window size further out: a perfectly
    /// separated split of twenty-eight runs against twenty-eight is 2.6e-16, needs eighteen places,
    /// and is clamped to fifteen — which rounds it to zero. Scaling by a power of ten instead fails
    /// at the other end, where the scale factor itself overflows to infinity. A significant-digit
    /// format has neither limit and is what "three significant digits" actually means.
    /// </para>
    /// </remarks>
    private static double Probability(double value) =>
        value > 0
            ? double.Parse(
                value.ToString("G3", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : value;

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
    /// A two-arm division with enough runs each side to compare, before anything is asked of it.
    /// </summary>
    /// <remarks>
    /// Unoriented, unlike <see cref="Split"/>: which side holds the failures is not known until the
    /// rates are computed, and this exists to be counted before any of them are. One of these is one
    /// comparison the axis search performed.
    /// </remarks>
    /// <param name="Axis">Which axis produced it.</param>
    /// <param name="Inside">The runs the axis's predicate selected.</param>
    /// <param name="InsideLabel">What that side is called.</param>
    /// <param name="Outside">Everything else.</param>
    /// <param name="OutsideLabel">What that side is called.</param>
    private sealed record Partition(
        string Axis,
        List<Measured> Inside,
        string InsideLabel,
        List<Measured> Outside,
        string OutsideLabel);

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
    /// interval still admits no difference at all. What the split is ranked on, and the first thing
    /// it is chosen on once the test cannot separate two candidates.
    /// </param>
    /// <param name="WorseIsNamed">
    /// Whether <paramref name="Worse"/> is the side the predicate selected rather than its
    /// complement. Only a named side carries a time a reader can act on.
    /// </param>
    /// <param name="PAdjusted">
    /// How probable a division of the failures this uneven would be if the test did not care when it
    /// ran, already multiplied by the number of divisions its runs admitted. What the split is
    /// chosen on.
    /// </param>
    private sealed record Split(
        string Axis,
        List<Measured> Worse,
        string WorseLabel,
        List<Measured> Other,
        string OtherLabel,
        double Delta,
        double Support,
        bool WorseIsNamed,
        double PAdjusted);
}
