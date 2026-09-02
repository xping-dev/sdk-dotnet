/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// One execution, published so the numbers computed from it can be checked.
/// </summary>
/// <remarks>
/// <see cref="StartedAt"/> is the start of the run the execution belongs to, not of the execution
/// itself. The per-execution timestamp is unreliable under retry on the xUnit adapter — the first
/// attempt's start is reused for every later attempt — while the duration is per-attempt everywhere,
/// so the report publishes the duration and dates it by its session.
/// </remarks>
/// <param name="SessionId">The run it happened in.</param>
/// <param name="StartedAt">When that run started.</param>
/// <param name="Sha">The commit it ran at, or <see langword="null"/> when none was recorded.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="DurationMs">How long it took.</param>
internal sealed record DurationExemplar(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    string Outcome,
    long DurationMs);

/// <summary>
/// One slice's duration profile, always carrying the counts it was computed from.
/// </summary>
/// <remarks>
/// Two kinds of count, because there are two samples. The percentiles here are raw milliseconds over
/// every execution; the comparison that decides a regression reads one normalised reading per run,
/// and a run can be missing from it — its own median was not positive, so nothing in it can be
/// normalised, or the test itself took no measurable time in it. Publishing one count for both would
/// let the evidence claim five runs behind a comparison made on one.
/// </remarks>
/// <param name="P50Ms">Median duration, in milliseconds.</param>
/// <param name="P95Ms">95th percentile duration, in milliseconds.</param>
/// <param name="Executions">Executions the percentiles were computed over.</param>
/// <param name="Sessions">Distinct runs those executions came from.</param>
/// <param name="ComparedSessions">
/// Runs the two-sample comparison actually read, one reading each. Never larger than
/// <paramref name="Sessions"/>, and the count both the arm floors and the test itself were applied
/// to.
/// </param>
internal sealed record DurationProfile(
    long P50Ms,
    long P95Ms,
    int Executions,
    int Sessions,
    int ComparedSessions);

/// <summary>
/// The change in raw wall-clock terms — what a developer would notice on the clock.
/// </summary>
/// <param name="P50Pct">Relative increase in median duration, as a percentage.</param>
/// <param name="P50Ms">Absolute increase in median duration, in milliseconds.</param>
internal sealed record DurationDelta(double P50Pct, long P50Ms);

/// <summary>
/// How much slower the test now is, after per-session normalisation — the figures every threshold
/// is actually applied to, and the test that admitted them.
/// </summary>
/// <remarks>
/// Reported separately from <see cref="DurationDelta"/> because the two routinely disagree, and the
/// disagreement is the point: a machine that was busy for the whole of the recent runs moves the raw
/// figure and leaves this one alone.
/// </remarks>
/// <param name="Ratio">
/// How many times slower, per <see cref="HodgesLehmann"/> — the median of every pairwise ratio
/// between a recent run and a baseline run. 1 would be no change.
/// </param>
/// <param name="RatioLow">Lower end of the 95% interval on <paramref name="Ratio"/>.</param>
/// <param name="RatioHigh">Upper end of that interval.</param>
/// <param name="Pct">
/// The same estimate as a percentage increase, for a reader who thinks in percentages. Exactly
/// <c>(<paramref name="Ratio"/> − 1) × 100</c>, carried rather than derived so that nothing
/// downstream has to do arithmetic on a published number.
/// </param>
/// <param name="Ms">
/// The estimate in milliseconds at the test's reference speed — the ratio applied to the baseline's
/// normalised level, multiplied back by the median of the medians of the runs the test appeared in.
/// The figure the absolute floor is applied to, and the honest millisecond answer to "how much
/// slower": a developer reads milliseconds, and these are the ones that do not move when the machine
/// does.
/// </param>
/// <param name="PValue">
/// How probable an ordering this favourable to the recent runs would be if the test had not slowed,
/// per <see cref="BrunnerMunzel"/>. One-sided. Published so the claim can be checked, and floored by
/// the number of ways the runs could have been arranged: three recent runs against seventeen cannot
/// produce anything below 1/1140 however large the slowdown.
/// </param>
internal sealed record DurationShift(
    double Ratio,
    double RatioLow,
    double RatioHigh,
    double Pct,
    long Ms,
    double PValue);

/// <summary>
/// Evidence that a test's median duration has increased against its own baseline.
/// </summary>
/// <param name="Current">The recent runs.</param>
/// <param name="Baseline">The runs before them.</param>
/// <param name="Delta">The change in raw milliseconds.</param>
/// <param name="Shift">
/// The change after normalisation, with its interval and the test that admitted it — what every
/// threshold was applied to.
/// </param>
/// <param name="FirstSeenAt">
/// The commit of the oldest recent run this test appeared in — where the change crosses from the
/// baseline into "now". Null when that run recorded no commit, never fabricated.
/// </param>
/// <param name="Exemplars">Up to three recent executions, newest first.</param>
/// <param name="Contrast">One execution typical of the prior behaviour.</param>
internal sealed record DurationRegressionEvidence(
    DurationProfile Current,
    DurationProfile Baseline,
    DurationDelta Delta,
    DurationShift Shift,
    string? FirstSeenAt,
    IReadOnlyList<DurationExemplar> Exemplars,
    DurationExemplar? Contrast) : FindingEvidence;

/// <summary>
/// Evidence that a test's duration varies too much for a regression to be measurable.
/// </summary>
/// <param name="Executions">Executions across the whole window.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="P50Ms">Median duration, in milliseconds.</param>
/// <param name="P95Ms">95th percentile duration, in milliseconds.</param>
/// <param name="MinMs">The fastest observed run, in milliseconds.</param>
/// <param name="MaxMs">The slowest observed run, in milliseconds.</param>
/// <param name="NormalisedExecutions">
/// Executions that could be normalised — what <paramref name="Dispersion"/> was computed over,
/// which is not always what the four raw figures above were.
/// </param>
/// <param name="NormalisedP50Ms">
/// The median the trivial-duration floor read, in milliseconds at the test's reference speed:
/// what it would take on the machine its own runs typically ran on. Not always over the same
/// executions as <paramref name="P50Ms"/> beside it — the floor reads the baseline where the test
/// has one and the window where it does not, so that a test cannot be measured against a size the
/// slowdown being reported gave it — but always the figure the decision was made on, so this
/// number and that decision cannot disagree.
/// </param>
/// <param name="Dispersion">
/// Spread per <see cref="RobustDispersion"/>, computed on <b>normalised</b> durations — so this is a
/// claim about the test varying relative to the suite around it, not about the machine having had a
/// bad afternoon, which is the only claim the data supports. Robust rather than a coefficient of
/// variation, so it describes what the test does most runs rather than what it did on its worst one.
/// </param>
/// <param name="Exemplars">Up to three executions chosen to span the observed spread.</param>
internal sealed record DurationUnstableEvidence(
    int Executions,
    int Sessions,
    long P50Ms,
    long P95Ms,
    long MinMs,
    long MaxMs,
    int NormalisedExecutions,
    long NormalisedP50Ms,
    double Dispersion,
    IReadOnlyList<DurationExemplar> Exemplars) : FindingEvidence;

/// <summary>
/// Reports how long tests take: whether one has slowed against its own history, and whether its
/// timing is steady enough for that question to have an answer.
/// </summary>
/// <remarks>
/// <para>
/// One provider owns both kinds because they are two readings of one thing. Whether a test has
/// slowed cannot be decided without accounting for how much its duration ordinarily moves, and a
/// test whose duration moves so much that nobody can predict what it will cost is itself the
/// finding. Splitting the provider would let both claim the same test off opposite readings of the
/// same runs, and the ordering in <see cref="Analyze"/> is what stops that.
/// </para>
/// <para>
/// <b>Whether a test has slowed is a two-sample question and is asked as one.</b> The recent runs
/// and the runs before them are compared by <see cref="BrunnerMunzel"/>, which asks whether the
/// recent slice is drawn from a slower distribution or is doing what the test always did, and the
/// size of the change is <see cref="HodgesLehmann"/>'s ratio with the interval it was measured to.
/// Both are required: a statistically solid three percent is not worth a developer's morning, and a
/// twofold gap over three runs that the test's own history contains is not a finding. There is no
/// longer a dispersion gate on the baseline, because the only job it had was to stand in for the
/// spread that the test now reads directly, and it read it on one arm only.
/// </para>
/// <para>
/// Instability is a different claim and keeps its own threshold. A test can be reported as unstable
/// without any question of a regression arising, and a test whose recent runs separate cleanly from
/// a wide baseline is now reported as slower where it used to be silenced for having a wide
/// baseline.
/// </para>
/// <para>
/// <b>The comparison reads one duration per run and the dispersion reads every execution.</b> A
/// two-sample test assumes its readings are independent, and three attempts of one test in one run
/// are one occasion of evidence rather than three — the unit #179 put on every arm gate, carried
/// here into the sample the gates are computed from. Instability makes no such assumption: it asks
/// how much a test's timing moves, and an attempt that took longer than its neighbour is part of
/// the answer however correlated the two are. So the two kinds count differently on purpose, and
/// each publishes the count it used.
/// </para>
/// <para>
/// <b>Every comparison is normalised per session.</b> Each execution's duration is divided by the
/// median duration of the run it belongs to before any cross-run comparison. On a developer machine
/// the dominant signal in raw milliseconds is the machine — a background build, thermal throttling,
/// a CI runner under load — and it moves every test in a run together, which is exactly what
/// dividing by the run's own median removes. Verified against the local store, where run medians for
/// one assembly move by a factor of two to sixteen across a fortnight and reorder the findings.
/// </para>
/// <para>
/// <b>Where a threshold is in milliseconds, they are milliseconds at the test's reference
/// speed</b> — a normalised figure multiplied back by the median of the medians of the runs the
/// test appeared in.
/// Both kinds have such a floor, and both exist for the same reason: below a few tens of
/// milliseconds a duration is measuring the scheduler. Reading that floor off raw milliseconds
/// while deciding everything else on normalised ones puts the two gates on different scales, and
/// they then disagree in exactly the case the normalisation exists for — a recent slice on a
/// faster machine, where the raw median falls while the normalised one rises. Anchoring instead
/// of dropping the floor keeps the number a developer recognises.
/// </para>
/// <para>
/// The limit of that technique is worth knowing: a run median describes the run's <i>test set</i> as
/// much as its speed, so runs executed under different filters normalise against different
/// populations. Nothing here can distinguish the two, and no claim is made that it does.
/// </para>
/// </remarks>
internal sealed class DurationProvider : IFindingProvider
{
    // Three, per the output contract's exemplar budget. A per-provider constant rather than a shared
    // threshold: the specification's constant table does not list it, and adding an entry there
    // would be a threshold this session invented.
    private const int MaxExemplars = 3;

    // Named in the condition for a duration regression but absent from the shared constant table,
    // so they stay local for the same reason as the exemplar budget above.
    //
    // Runs rather than executions, per #179: attempts of one test within a run are correlated, so
    // a test that retried five times in one afternoon has one occasion of evidence and not five.
    //
    // The baseline figure is derived rather than chosen. Three recent runs against `n` baseline
    // ones can be dealt C(n + 3, 3) ways, and the strongest thing the data can say -- every recent
    // run slower than every run before it -- is one of them. At six that is 1/84 = 0.0119, which
    // does not reach `RegressionAlpha`, so no arrangement of six baseline runs can produce a
    // finding; at seven it is 1/120 = 0.0083, which does. Stating seven is the difference between
    // declining and appearing to test. It was five while the comparison was a ratio of two medians,
    // which could be computed from any two numbers and said nothing about how many were behind it.
    private const int MinimumBaselineSessions = 7;
    private const int MinimumCurrentSessions = 3;

    // The p-value a slowdown has to clear. Local for the same reason as the counts above: the
    // specification's constant table does not name it, and adding an entry there would be a
    // threshold this session invented.
    //
    // Not the conventional 0.05, because this comparison is not made once. Every fingerprint in the
    // window is tested, and #168 asks for a per-test false-positive rate under one in a hundred at
    // every dispersion test durations take -- which an exact test delivers by construction, since
    // it cannot report more often than the level it is read at. Measured end to end over the whole
    // gate chain, forty thousand windows per cell: 0.0004 at a true dispersion of 0.20 and 0.0096
    // at 0.70, against 0.05 and 0.047 at the conventional level. The practical floor beside it only
    // ever lowers this.
    //
    // What it costs is the shape that needs the most evidence anyway. A true doubling on a steady
    // test is still reported 97% of the time against seventeen baseline runs and 89% against seven.
    //
    // A pre-filter, not the final word. #160 applies a Benjamini-Hochberg pass across every
    // fingerprint each kind was tested on, which is the only place the multiplicity can properly be
    // charged for -- a provider cannot see the other tests. This holds the rate to something
    // defensible in the meantime, and that pass will supersede it.
    private const double RegressionAlpha = 0.01;

    // Baseline runs the comparison reads, most recent first. The exact test enumerates every way
    // the pooled runs could have been split, which is C(n + 3, 3) and grows as a cube; `--runs` is
    // unbounded, so something has to stop it. Forty holds the enumeration under twelve and a half
    // thousand arrangements and never binds on a default twenty-run window. What it costs is the
    // difference between the fortieth-oldest run and the hundredth as evidence about a test's
    // baseline, which is nothing: the comparison's power is capped by the three runs on the other
    // side. The published `Baseline.P50Ms` still covers the whole baseline, and `ComparedSessions`
    // states the truncation where it happened.
    private const int MaxComparedBaselineSessions = 40;

    /// <inheritdoc/>
    public string Name => "duration";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds =>
        [FindingKind.DurationRegression, FindingKind.DurationUnstable];

    /// <inheritdoc/>
    public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Computed once for the whole window and shared by every test in a run, which is what makes
        // the normalisation a property of the run rather than something each test re-derives.
        Dictionary<Guid, double> medians = SessionMedians(context);

        var currentSessions = new HashSet<Guid>(
            context.Window.CurrentSlice.Select(s => s.SessionId));

        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            TestReference? test = context.Tests.ReferenceFor(fingerprint);
            if (test == null)
                continue;

            IReadOnlyList<ExecutionRef> all = context.Tests.ExecutionsOf(fingerprint);

            List<ExecutionRef> current = [];
            List<ExecutionRef> baseline = [];

            foreach (ExecutionRef reference in all)
            {
                if (currentSessions.Contains(reference.Session.SessionId))
                    current.Add(reference);
                else
                    baseline.Add(reference);
            }

            // A test that has stopped running is not a duration finding. Its absence is what is
            // interesting about it, and that belongs to `Vanished` — claiming it here as well would
            // report one disappearance twice under two names.
            if (current.Count == 0)
                continue;

            // The scale this test's own figures are expressed in. Per test rather than per window,
            // because a run median is only a machine-speed reading among runs that ran the same
            // tests: a window holding eight `--filter`ed runs of three fast tests and eight full
            // ones has a window median of the filtered runs' speed, and multiplying by that would
            // put every millisecond gate a factor of fifty out for tests that never ran in them.
            double referenceMs = ReferenceMedian(all, medians);

            Profile whole = Build(all, medians);
            Profile currentProfile = Build(current, medians);
            Profile baselineProfile = Build(baseline, medians);

            // A regression suppresses the instability finding for the same test, and the two now
            // overlap more than they used to: a test whose baseline swings and whose recent runs
            // then step clear of all of it earns both, where the retired stability gate used to
            // decline the first. The step is what lifted the whole window past the instability
            // threshold, so reporting that as instability would state the regression a second time
            // under another name. This ordering is what stops it, and
            // ARegressingTestIsNotAlsoReportedAsUnstable builds the sample that needs it.
            FindingCandidate? candidate =
                Regression(test, current, currentProfile, baselineProfile, referenceMs) ??
                Unstable(context, test, all, whole, baselineProfile, referenceMs);

            if (candidate != null)
                yield return candidate;
        }
    }

    /// <summary>
    /// Attempts the regression finding, returning <see langword="null"/> when any gate declines it.
    /// </summary>
    /// <param name="test">The test under consideration.</param>
    /// <param name="current">Its executions in the recent slice.</param>
    /// <param name="currentProfile">Those executions reduced to statistics.</param>
    /// <param name="baselineProfile">The same for everything before them.</param>
    /// <param name="referenceMs">The test's reference speed, in milliseconds.</param>
    private static FindingCandidate? Regression(
        TestReference test,
        List<ExecutionRef> current,
        Profile currentProfile,
        Profile baselineProfile,
        double referenceMs)
    {
        // Nothing to compare against is not a slowdown. A test added this week has history in the
        // window but no history of its own, and calling that a regression would report every new
        // test as one.
        //
        // Counted over the runs the comparison actually reads, which is the only count that cannot
        // disagree with the claim. A run whose own median was not positive normalises nothing, and a
        // run where the test itself took no measurable time contributes a reading no ratio can be
        // taken against; either way the arm is thinner than its session count says.
        if (baselineProfile.Compared.Count < MinimumBaselineSessions ||
            currentProfile.Compared.Count < MinimumCurrentSessions)
        {
            return null;
        }

        // The cheap, exact gates first. Both are decided from a sorted list of pairwise ratios,
        // where the test below enumerates every way the runs could have been split between the two
        // arms — so ordering them this way keeps that enumeration off every test that could not
        // qualify anyway, which in a real window is nearly all of them.
        RatioEstimate shift = HodgesLehmann.Of(baselineProfile.Compared, currentProfile.Compared);

        if (shift.Ratio - 1 < LocalAnalysisConstants.DurationRegressionPct)
            return null;

        // Guards the relative test on the scale a developer actually experiences: two milliseconds
        // becoming four is a hundred-percent regression and is not worth anyone's morning. Measured
        // through the same normalisation the relative test used, and multiplied back into
        // milliseconds by the test's reference speed. In raw milliseconds this gate disagrees with
        // the one above it precisely when the normalisation was doing its job: a recent slice on a
        // faster machine can raise the normalised level while lowering the raw one, and a negative
        // raw increase declines every real regression measured that way.
        double baselineLevel = Percentile(baselineProfile.Compared, 0.50);
        double increaseMs = (shift.Ratio - 1) * baselineLevel * referenceMs;

        if (increaseMs < LocalAnalysisConstants.DurationRegressionMinMs)
            return null;

        // And then the question itself: is the recent slice drawn from a slower distribution, or is
        // this the variation the test already had? Nothing above asks it — a ratio of two medians
        // says how far apart two numbers are and nothing about how far apart they routinely fall.
        double pValue = BrunnerMunzel.OneSidedPValue(
            baselineProfile.Compared, currentProfile.Compared);

        if (pValue > RegressionAlpha)
            return null;

        return new FindingCandidate(
            FindingKind.DurationRegression,
            new FindingSubject.SingleTest(test),
            new DurationRegressionEvidence(
                currentProfile.ToPublished(),
                baselineProfile.ToPublished(),
                new DurationDelta(
                    FindingOrder.RoundPercent(PercentIncrease(
                        baselineProfile.RawP50, currentProfile.RawP50)),
                    RoundMs(currentProfile.RawP50 - baselineProfile.RawP50)),
                new DurationShift(
                    FindingOrder.Round(shift.Ratio),
                    FindingOrder.Round(shift.Low),
                    FindingOrder.Round(shift.High),
                    FindingOrder.RoundPercent((shift.Ratio - 1) * 100),
                    RoundMs(increaseMs),
                    Probability(pValue)),
                FirstSeenAt(current),
                Recent(current),
                Contrast(baselineProfile)),

            // The lower end of the interval rather than the estimate itself, for the reason #178
            // gave every kind that ranks on a proportion: a fourfold slowdown measured over three
            // runs against seven and one measured over three against seventeen are not the same
            // finding, and only the interval knows it. Doubling is as unreliable as this measure
            // gets — beyond that the test is simply slow, and ranking a tenfold slowdown above a
            // twofold one would crowd out every other kind on one arithmetic accident.
            Unreliability: Math.Clamp((shift.Low - 1) / 2, 0, 1.0),

            SessionsSinceLastOccurrence: current.Min(e => e.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.DurationRegression, test),

            PValue: pValue);
    }

    /// <summary>
    /// Rounds a millisecond figure for publication.
    /// </summary>
    /// <remarks>
    /// Rounded rather than truncated. Every published millisecond here is the product of a ratio and
    /// a scale, and a cast would report a six-hundred-millisecond slowdown as 599 whenever that
    /// product landed a bit under — an off-by-one a reader cannot explain and cannot check against
    /// the exemplars beside it.
    /// </remarks>
    private static long RoundMs(double value) =>
        (long)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Rounds a p-value for publication.
    /// </summary>
    /// <remarks>
    /// Six decimals rather than the three <see cref="FindingOrder.Round"/> gives every other
    /// published figure. A p-value here is bounded below by one over the number of ways the runs
    /// could have been dealt, and a long baseline makes that number small: forty runs against three
    /// floors it at 1/12341, which three decimals would publish as zero. A probability of zero is a
    /// claim of certainty, and this measurement never makes one.
    /// </remarks>
    private static double Probability(double value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Attempts the instability finding, returning <see langword="null"/> when a gate declines it.
    /// </summary>
    /// <remarks>
    /// Measured over the whole window rather than either slice. Instability is a standing property
    /// of a test, not a change between two halves of its history, and splitting the window would
    /// only halve the evidence behind it.
    /// </remarks>
    private static FindingCandidate? Unstable(
        AnalysisContext context,
        TestReference test,
        IReadOnlyList<ExecutionRef> all,
        Profile whole,
        Profile baselineProfile,
        double referenceMs)
    {
        double dispersion = RobustDispersion.Of(whole.Normalised);
        if (dispersion < LocalAnalysisConstants.DurationUnstableDispersionMin)
            return null;

        // The test's own prior behaviour where it has any, and the window where it does not. A test
        // first seen this week has no baseline, and refusing to measure it at all would make its
        // instability invisible for as long as the window takes to fill.
        Profile against = baselineProfile.NormalisedExecutions > 0 ? baselineProfile : whole;

        // In milliseconds at the test's reference speed, not raw ones. The dispersion this floor
        // qualifies is measured on normalised durations, and a floor read off the clock answers a
        // different question from the statistic it is guarding: a test that takes 30ms on the fast
        // machine its runs happened on is not a trivial test, and one reading 200ms on a machine
        // labouring at a fifth of the window's speed is.
        double floor = against.NormalisedP50 * referenceMs;

        // Below a few tens of milliseconds the dispersion is measuring the scheduler, not the test.
        if (floor < LocalAnalysisConstants.DurationTrivialMs)
            return null;

        return new FindingCandidate(
            FindingKind.DurationUnstable,
            new FindingSubject.SingleTest(test),
            new DurationUnstableEvidence(
                whole.Executions,
                context.Window.SessionCount,
                RoundMs(whole.RawP50),
                RoundMs(whole.RawP95),
                RoundMs(whole.RawMin),
                RoundMs(whole.RawMax),
                whole.NormalisedExecutions,
                RoundMs(floor),
                FindingOrder.Round(dispersion),
                Spanning(all, whole.RawP50)),

            // The dispersion itself, capped. A test whose typical run sits two thirds of its own
            // median away from it is as unpredictable as this measure can express, and everything
            // past that is the same finding with a larger number on it.
            Unreliability: Math.Min(1.0, dispersion),

            SessionsSinceLastOccurrence: all.Min(e => e.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.DurationUnstable, test));
    }

    /// <summary>
    /// Reads the commit the change crossed into "now" at.
    /// </summary>
    /// <remarks>
    /// Taken from the oldest recent run <i>this test appeared in</i>, not simply the oldest recent
    /// run. A test need not execute in every run, and naming a commit that never ran it would
    /// attribute the change to the wrong place.
    /// </remarks>
    private static string? FirstSeenAt(List<ExecutionRef> current)
    {
        ExecutionRef oldest = current[0];

        foreach (ExecutionRef reference in current)
        {
            if (reference.SessionIndex > oldest.SessionIndex)
                oldest = reference;
        }

        return RevisionContext.ReadSha(oldest.Session);
    }

    /// <summary>
    /// Picks up to three recent executions, newest first.
    /// </summary>
    private static List<DurationExemplar> Recent(List<ExecutionRef> current) =>
        [.. Ordered(current).Take(MaxExemplars).Select(ToExemplar)];

    /// <summary>
    /// Picks up to three executions chosen to make the observed spread legible.
    /// </summary>
    /// <remarks>
    /// The fastest, the most typical and the slowest, rather than the three most recent. Three
    /// exemplars that all sit near the median describe a test that looks perfectly steady, which is
    /// the opposite of what this finding claims.
    /// </remarks>
    private static List<DurationExemplar> Spanning(IReadOnlyList<ExecutionRef> all, double p50)
    {
        ExecutionRef?[] chosen =
        [
            Nearest(all, e => -Milliseconds(e)),
            Nearest(all, e => Math.Abs(Milliseconds(e) - p50)),
            Nearest(all, e => Milliseconds(e))
        ];

        var ids = new HashSet<Guid>();
        List<ExecutionRef> spread = [];

        foreach (ExecutionRef? reference in chosen)
        {
            if (reference != null && ids.Add(reference.Execution.ExecutionId))
                spread.Add(reference);
        }

        // Presented newest first, as every other kind's exemplars are, so a reader comparing two
        // findings is not silently reading two different orderings.
        return [.. Ordered(spread).Select(ToExemplar)];
    }

    /// <summary>
    /// Picks the execution scoring lowest on a key, breaking every tie totally.
    /// </summary>
    private static ExecutionRef? Nearest(
        IReadOnlyList<ExecutionRef> executions, Func<ExecutionRef, double> key) =>
        executions
            .OrderBy(key)
            .ThenBy(e => e.SessionIndex)
            .ThenBy(e => e.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Picks one baseline execution typical of the prior behaviour.
    /// </summary>
    /// <remarks>
    /// The one nearest the baseline median rather than the newest, because the pair only makes the
    /// change reasonable about if the "before" half is representative of before — an unluckily slow
    /// baseline run next to a regressed one understates the difference it exists to show.
    /// </remarks>
    private static DurationExemplar? Contrast(Profile baseline)
    {
        ExecutionRef? typical = Nearest(
            baseline.Source, e => Math.Abs(Milliseconds(e) - baseline.RawP50));

        // Absent rather than null-filled when there is nothing to contrast against: a consumer
        // cannot tell an empty field apart from analysis that looked and found nothing.
        return typical == null ? null : ToExemplar(typical);
    }

    private static IOrderedEnumerable<ExecutionRef> Ordered(IReadOnlyList<ExecutionRef> executions) =>
        executions
            .OrderBy(e => e.SessionIndex)
            .ThenBy(e => e.Execution.Retry?.AttemptNumber ?? 1)
            .ThenBy(e => e.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

    private static DurationExemplar ToExemplar(ExecutionRef reference) =>
        new(
            reference.Session.SessionId.ToString("D", CultureInfo.InvariantCulture),
            reference.Session.StartedAt,
            RevisionContext.ReadSha(reference.Session),
            reference.Execution.Outcome.ToString(),
            RoundMs(Milliseconds(reference)));

    private static double Milliseconds(ExecutionRef reference) =>
        reference.Execution.Duration.TotalMilliseconds;

    /// <summary>
    /// Computes the median duration of every run in the window.
    /// </summary>
    /// <remarks>
    /// A run whose median is not positive is left out entirely rather than recorded as zero: it
    /// would divide every duration in that run to infinity, and one such run would dominate every
    /// statistic computed from the window.
    /// </remarks>
    private static Dictionary<Guid, double> SessionMedians(AnalysisContext context)
    {
        var medians = new Dictionary<Guid, double>(context.Window.SessionCount);

        foreach (var session in context.Window.Sessions)
        {
            var durations = new List<double>(session.Executions.Count);
            foreach (var execution in session.Executions)
                durations.Add(execution.Duration.TotalMilliseconds);

            durations.Sort();

            double median = Percentile(durations, 0.50);
            if (median > 0)
                medians[session.SessionId] = median;
        }

        return medians;
    }

    /// <summary>
    /// Reads the reference speed for one test: the median of the medians of the runs it appeared
    /// in, in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The anchor a normalised figure is multiplied by to become milliseconds again — the same
    /// divisors that produced the ratios, reduced to one number, so the product reconstructs
    /// milliseconds on a machine speed the test was actually measured at.
    /// </para>
    /// <para>
    /// One value across both slices rather than one each, which is what makes the two sides of a
    /// comparison expressible in the same units: anchoring each slice to its own machine would
    /// reintroduce the difference the normalisation just removed.
    /// </para>
    /// <para>
    /// Over the runs the test appeared in rather than the whole window, because a run median is a
    /// machine-speed reading only among runs that ran the same tests. Runs under a
    /// <c>--filter</c> are ordinary in a local store and hold a handful of fast tests, so a window
    /// with more of them than full runs has a window-wide median of their speed — a number about
    /// the store rather than about any machine, and one that would put every millisecond gate
    /// orders out for every test absent from them.
    /// </para>
    /// <para>
    /// A median rather than a mean, so one unusual run moves it as little as it moves anything
    /// else here, and zero when no run the test appeared in had a usable median, which declines
    /// every millisecond gate.
    /// </para>
    /// </remarks>
    private static double ReferenceMedian(
        IReadOnlyList<ExecutionRef> executions, Dictionary<Guid, double> medians)
    {
        var seen = new HashSet<Guid>();
        var values = new List<double>();

        // One reading per run, not per execution: a run that retried three times describes one
        // machine once.
        foreach (ExecutionRef reference in executions)
        {
            if (seen.Add(reference.Session.SessionId) &&
                medians.TryGetValue(reference.Session.SessionId, out double median))
            {
                values.Add(median);
            }
        }

        values.Sort();

        return Percentile(values, 0.50);
    }

    private static Profile Build(
        IReadOnlyList<ExecutionRef> executions, Dictionary<Guid, double> medians)
    {
        var raw = new List<double>(executions.Count);
        var normalised = new List<double>(executions.Count);
        var sessions = new HashSet<Guid>();

        // One entry per run the test appeared in, holding every attempt it made there. Kept in the
        // order the runs are reached so the truncation below can take the most recent ones.
        var perSession = new List<(int Index, Guid Session, List<double> Attempts)>();
        var positions = new Dictionary<Guid, int>();

        foreach (ExecutionRef reference in executions)
        {
            raw.Add(Milliseconds(reference));
            sessions.Add(reference.Session.SessionId);

            if (medians.TryGetValue(reference.Session.SessionId, out double median))
                normalised.Add(Milliseconds(reference) / median);

            if (!positions.TryGetValue(reference.Session.SessionId, out int position))
            {
                position = perSession.Count;
                positions[reference.Session.SessionId] = position;
                perSession.Add((reference.SessionIndex, reference.Session.SessionId, []));
            }

            perSession[position].Attempts.Add(Milliseconds(reference));
        }

        // Sorted before anything is computed from them, so every percentile reads the same index and
        // every sum accumulates in the same order on every run.
        raw.Sort();
        normalised.Sort();

        return new Profile(
            executions, raw, normalised, sessions.Count, Compared(perSession, medians));
    }

    /// <summary>
    /// Reduces a test's runs to the sample the two-sample comparison reads: one normalised reading
    /// each, most recent runs first, capped, ascending.
    /// </summary>
    /// <param name="perSession">Every run the test appeared in, with the attempts it made there.</param>
    /// <param name="medians">Median duration of every run in the window.</param>
    /// <remarks>
    /// <para>
    /// <b>One reading per run rather than one per execution.</b> Attempts of a test within a run are
    /// correlated — the same machine, the same minute, often the same cause — and the test this
    /// feeds assumes its readings are independent. Handing it three attempts as three observations
    /// would let one bad afternoon count three times and understate the p-value accordingly. This is
    /// the unit #179 put on every arm gate, carried into the sample the gates are computed from.
    /// </para>
    /// <para>
    /// The run's own reading is the nearest-rank median of its attempts, by the same definition
    /// every other percentile here uses, so it is a duration the test was actually observed to take.
    /// </para>
    /// <para>
    /// Readings that are not strictly positive are left out. They cannot carry a ratio — a test that
    /// took no measurable time has no factor by which it later became slower — and the xUnit adapter
    /// records exactly that for a failure raised outside the timed invocation. Dropping them here
    /// rather than guarding a division later is what keeps the arm floors counting the same runs the
    /// comparison reads.
    /// </para>
    /// </remarks>
    private static List<double> Compared(
        List<(int Index, Guid Session, List<double> Attempts)> perSession,
        Dictionary<Guid, double> medians)
    {
        // Newest first, so a truncated baseline keeps its most recent runs. Ties cannot occur —
        // a session index identifies a run — but the identifier breaks them anyway, because two
        // reports over one store have to agree down to the byte.
        perSession.Sort((left, right) =>
            left.Index != right.Index
                ? left.Index.CompareTo(right.Index)
                : left.Session.CompareTo(right.Session));

        var compared = new List<double>(perSession.Count);

        foreach ((_, Guid session, List<double> attempts) in perSession)
        {
            if (compared.Count == MaxComparedBaselineSessions)
                break;

            if (!medians.TryGetValue(session, out double median))
                continue;

            attempts.Sort();

            double reading = Percentile(attempts, 0.50) / median;
            if (reading > 0)
                compared.Add(reading);
        }

        compared.Sort();

        return compared;
    }

    /// <summary>
    /// Reads a percentile by nearest rank.
    /// </summary>
    /// <param name="sorted">Values in ascending order.</param>
    /// <param name="percentile">The percentile to read, in [0,1].</param>
    /// <returns>The value at that rank, or zero when there are none.</returns>
    /// <remarks>
    /// One definition, used for the run median and for every published percentile alike. Nearest
    /// rank rather than an interpolating variant because it returns an observed value and involves
    /// no arithmetic that could reorder two runs in the last decimal place — and on the handful of
    /// executions a local window holds, an interpolated percentile invents a precision the data
    /// does not have.
    /// </remarks>
    private static double Percentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
            return 0;

        int rank = (int)Math.Ceiling(percentile * sorted.Count) - 1;

        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }

    /// <summary>
    /// Computes a relative increase, guarding the division.
    /// </summary>
    private static double PercentIncrease(double before, double after) =>
        before <= 0 ? 0 : (after - before) / before * 100;

    /// <summary>
    /// One set of executions reduced to the statistics both kinds are decided on.
    /// </summary>
    /// <param name="Source">The executions themselves, for exemplar selection.</param>
    /// <param name="Raw">Durations in milliseconds, ascending.</param>
    /// <param name="Normalised">
    /// Every execution's duration over its own run's median, ascending. What the dispersion behind
    /// the instability finding is measured on: instability is a claim about how much a test's
    /// timing moves, and an attempt that took a different length of time from its neighbour is part
    /// of that however correlated the two are.
    /// </param>
    /// <param name="Sessions">Distinct runs the executions came from.</param>
    /// <param name="Compared">
    /// One normalised reading per run, ascending. What the two-sample comparison reads, and a
    /// strict subset of the runs behind <paramref name="Normalised"/> — see <c>Compared</c>.
    /// </param>
    private sealed record Profile(
        IReadOnlyList<ExecutionRef> Source,
        List<double> Raw,
        List<double> Normalised,
        int Sessions,
        List<double> Compared)
    {
        public int Executions => Raw.Count;

        public int NormalisedExecutions => Normalised.Count;

        public double RawP50 => Percentile(Raw, 0.50);

        public double RawP95 => Percentile(Raw, 0.95);

        public double RawMin => Raw.Count == 0 ? 0 : Raw[0];

        public double RawMax => Raw.Count == 0 ? 0 : Raw[^1];

        public double NormalisedP50 => Percentile(Normalised, 0.50);

        public DurationProfile ToPublished() =>
            new(
                RoundMs(RawP50),
                RoundMs(RawP95),
                Executions,
                Sessions,
                Compared.Count);
    }
}
