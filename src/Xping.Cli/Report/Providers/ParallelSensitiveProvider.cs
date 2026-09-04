/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// One execution, published so the level it was counted at can be checked.
/// </summary>
/// <param name="SessionId">The run it happened in.</param>
/// <param name="StartedAt">When that run started.</param>
/// <param name="Sha">The commit it ran at, or <see langword="null"/> when none was recorded.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Concurrency">Tests in flight alongside it, itself included.</param>
internal sealed record ConcurrencyExemplar(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    string Outcome,
    int Concurrency);

/// <summary>
/// Which way a trend points, named so that neither the evidence nor a renderer has to read a sign.
/// </summary>
internal enum ConcurrencyDirection
{
    /// <summary>The test fails more as the suite gets busier.</summary>
    WithConcurrency,

    /// <summary>The test fails more as the suite empties out.</summary>
    AgainstConcurrency
}

/// <summary>
/// One concurrency level, with everything the rate at it was computed from.
/// </summary>
/// <remarks>
/// Both denominators are published because they answer different questions and this finding needs
/// both. <paramref name="Executions"/> is what the rate is over — concurrency varies between attempts
/// within a run, so every attempt is a real, distinct reading. <paramref name="Sessions"/> is how many
/// independent occasions those readings came from, and it is the number that says whether a rate over
/// twelve executions is twelve builds or one afternoon of retries.
/// </remarks>
/// <param name="Concurrency">Tests in flight, itself included.</param>
/// <param name="Executions">Executions observed at this level.</param>
/// <param name="Sessions">Distinct runs those executions came from.</param>
/// <param name="Failures">How many of them failed.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Executions"/>.</param>
internal sealed record ConcurrencyLevel(
    int Concurrency,
    int Executions,
    int Sessions,
    int Failures,
    double FailureRate);

/// <summary>
/// What the trend test found, and what it was computed over.
/// </summary>
/// <remarks>
/// <para>
/// Two numbers rather than one, because they answer questions a reader asks separately.
/// <paramref name="Tau"/> is how strongly the failures track the concurrency; <paramref name="Z"/>
/// and <paramref name="PValue"/> are whether that tracking is more than the failures would have done
/// on their own. A test can move a long way on very little and a small way on a great deal, and
/// neither figure implies the other.
/// </para>
/// <para>
/// <paramref name="Sessions"/> is the divisor of the correction rather than a count of readings: the
/// probability was computed over occasions, not over attempts, so it is the number that bounds how
/// small the probability could have been.
/// </para>
/// </remarks>
/// <param name="Z">The standardised trend, signed towards higher concurrency.</param>
/// <param name="PValue">Two-sided probability of a trend this strong with no trend present.</param>
/// <param name="Tau">Kendall's τ_b between the concurrency level and failure, signed the same way.</param>
/// <param name="Sessions">Distinct runs the trend was measured over.</param>
/// <param name="Direction">Which way it points, named rather than left to the sign.</param>
internal sealed record ConcurrencyTrend(
    double Z,
    double PValue,
    double Tau,
    int Sessions,
    string Direction);

/// <summary>
/// The concurrency the test was actually observed at.
/// </summary>
/// <remarks>
/// Published so a reader can see how much room the trend had. A rise measured across levels 11 to 13
/// is a far weaker statement than the same rise measured across 1 to 14, and nothing else in the
/// evidence would show the difference — least of all τ_b, which discounts by exactly this and so
/// cannot also report it.
/// </remarks>
/// <param name="Min">Lowest concurrency observed.</param>
/// <param name="Max">Highest concurrency observed.</param>
/// <param name="DistinctLevels">How many distinct concurrency values were seen.</param>
internal sealed record ConcurrencyRange(int Min, int Max, int DistinctLevels);

/// <summary>
/// Evidence that a test's failure rate moves with how many tests ran alongside it.
/// </summary>
/// <param name="Trend">What the trend test found.</param>
/// <param name="Observed">The concurrency range it was found across.</param>
/// <param name="Levels">
/// Every level observed, ascending. Ascending is a contract rather than an incidental ordering: the
/// dose-response is the finding, and a reader — or a renderer picking out the two ends — has to be
/// able to read it off in order.
/// </param>
/// <param name="Exemplars">
/// Up to three of the failures that drove the trend — those above the mean concurrency where it
/// rises, below it where it falls — furthest along the trend first. Not simply the newest three
/// failures, and not the failures at the extreme level: a failure on the other side of the mean is
/// the observation the statistic subtracted rather than added, and showing it as an exemplar would
/// illustrate the finding with the run that argues against it.
/// </param>
/// <param name="Contrast">One execution typical of the far end of the observed range.</param>
internal sealed record ParallelSensitiveEvidence(
    ConcurrencyTrend Trend,
    ConcurrencyRange Observed,
    IReadOnlyList<ConcurrencyLevel> Levels,
    IReadOnlyList<ConcurrencyExemplar> Exemplars,
    ConcurrencyExemplar? Contrast) : FindingEvidence;

/// <summary>
/// Reports tests whose failure rate moves with how many other tests were running at the time.
/// </summary>
/// <remarks>
/// <para>
/// Every distinct concurrency level the test was observed at is one point on a dose-response curve,
/// and <see cref="CochranArmitage"/> asks whether the failures track it. There is no split point and
/// no boundary to choose, which is what makes the finding reachable at all on the commonest .NET
/// configuration: a suite pinned at a fixed <c>maxParallelThreads</c> puts almost every execution on
/// one level, and any dichotomy of that distribution starves one of its two halves. Splitting at the
/// test's own median used to, and reported nothing for such a suite however concurrency-sensitive its
/// tests really were.
/// </para>
/// <para>
/// <b>Reading the ordering is the point, not a side benefit.</b> A two-arm comparison cannot tell
/// concurrency 2 from concurrency 14 once both are "high", so the monotone rise a genuinely
/// contention-sensitive test produces is the shape it is worst at seeing. Scoring the levels by value
/// rather than by rank additionally reads how far apart they sit relative to each other: across
/// levels of 1, 2 and 14 the jump to 14 counts for twelve times the step to 2. It does not read the
/// absolute width of the range — two levels are two levels however far apart — and
/// <see cref="ConcurrencyRange"/> is published so that a reader can see what the statistic could not.
/// </para>
/// <para>
/// <b>The boolean is deliberately unused.</b> <see cref="TestOrchestrationRecord.WasParallelized"/>
/// reports concurrency correctly today, but it is the wrong measurement here. Concurrency level
/// varies freely between runs while the flag derived from it does not, because the variation happens
/// among values that are all greater than one. Verified against a parallel assembly over three runs:
/// 360 of 770 tests ran at more than one concurrency level, with spreads as wide as eight to
/// fourteen, and yet not one test was ever on both sides of the flag — 646 were parallel every run
/// and 123 serial every run. Splitting on the flag would have nothing to compare. The trend subsumes
/// the parallel-versus-serial comparison rather than replacing it: a suite whose parallelisation
/// setting changed inside the window contributes its concurrency-one executions as one level and its
/// concurrency-<i>n</i> executions as another, with no special case.
/// </para>
/// <para>
/// <b>Two-sided, so both directions qualify.</b> A test that fails only when it runs nearly alone is
/// as real a defect as one that fails only under contention, and it would otherwise be reported by
/// nothing.
/// </para>
/// <para>
/// <b>Known limitation: concurrency is observed, not randomised.</b> A slow test overlaps more
/// neighbours than a fast one by construction, and a test's position in the run determines how
/// crowded the suite was around it. A rising trend is therefore confounded with the test's own
/// duration and with where it was scheduled: a test that got slower, and so came to run alongside
/// more tests, reads exactly like one that fails under contention. Stratifying by duration decile is
/// the correction, and it needs more data than a twenty-session window holds. Nothing here corrects
/// it. <see cref="ConcurrencyRange"/> and the per-level table are published so that a reader can at
/// least see how much room the trend had and judge it for themselves.
/// </para>
/// <para>
/// <b>Known limitation: τ_b is attempt-weighted.</b> The clustered variance behind the p-value
/// charges a heavily retried run for the repetition, and the rank the finding is given is that same
/// charge applied to the effect size — so neither the decision to report nor the position in the
/// report can be bought with retries. The published effect size still
/// weighs attempts rather than occasions, and a reader comparing two τ_b values across tests that
/// retry differently is not comparing like with like.
/// </para>
/// <para>
/// This kind overlaps <see cref="FindingKind.Flaky"/> by design. A concurrency-sensitive test both
/// passes and fails, so the failure-mode provider will usually claim it too; that is additive, not
/// duplicative. `Flaky` says the test is unreliable, this says what it is unreliable about.
/// </para>
/// </remarks>
internal sealed class ParallelSensitiveProvider : IFindingProvider
{
    // Three, per the output contract's exemplar budget. A per-provider constant rather than a shared
    // threshold, for the same reason the duration provider keeps its own: the specification's
    // constant table does not list it, and adding an entry would be a threshold this session invented.
    private const int MaxExemplars = 3;

    // The p-value a trend has to clear. Local rather than shared, for the reason the duration and
    // temporal providers give about their own: the specification's constant table does not name it,
    // and adding an entry there would be a threshold this session invented.
    //
    // The conventional level, and it is affordable because two things ahead of it already spend most
    // of the budget. The variance is taken over runs rather than over attempts, and the statistic is
    // continuity-corrected against the lattice its own levels put it on -- which on a pinned suite,
    // where the gap between levels is the whole distance from 1 to 8, is a large correction. Measured
    // end to end through the whole gate chain, forty thousand windows a cell, with failures drawn
    // independently of the level: the worst cell of twenty distributions is 0.029, at a uniform 1..14
    // exposure over twenty runs with a true failure rate of one half, and the same shape reads 0.024
    // at a rate of 0.30. Ten-run windows top out at 0.017 and six-run windows at 0.003. The delta
    // gate this replaces measured 0.296 on the comparable shape.
    //
    // A pre-filter, and no longer the final word. This charges one test for one comparison; it
    // cannot charge a suite for the three hundred tests it made that comparison on, because a
    // provider by contract cannot see the others. The coordinator now does, applying a
    // Benjamini-Hochberg pass at `LocalAnalysisConstants.FalseDiscoveryRate` across every
    // fingerprint reported in `ProviderReport.HypothesesTested`.
    //
    // Kept rather than removed, deliberately. It is cheap, it keeps the effect size and the evidence
    // record off tests that were never going to qualify, and it cannot change what the pass decides:
    // the pass's own bar reaches 0.05 only where half the family are discoveries. A suite in that
    // state is not one a threshold is deciding anything for.
    private const double Alpha = 0.05;

    /// <inheritdoc/>
    public string Name => "concurrency";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds => [FindingKind.ParallelSensitive];

    /// <inheritdoc/>
    public ProviderReport Analyze(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = new List<FindingCandidate>();
        int tested = 0;

        // Fingerprints are ordinal-sorted by the index, so findings come out in the same sequence on
        // every run whatever order the sessions were read in.
        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            Examination examination = Examine(context, fingerprint);

            if (examination.Tested)
                tested++;

            if (examination.Candidate is { } candidate)
                candidates.Add(candidate);
        }

        return new ProviderReport(
            candidates,
            new Dictionary<FindingKind, int> { [FindingKind.ParallelSensitive] = tested });
    }

    /// <summary>
    /// Examines one test, saying both whether the trend test was run on it and what survived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no gate on how many runs the test appeared in, and that is deliberate rather than an
    /// omission. The gate the median split needed — five sessions a side — existed because a rate
    /// difference says nothing about how many observations are behind it, and something had to. The
    /// statistic now says it directly: measured over perfectly separated windows, four runs a side is
    /// the smallest that reaches the bar at all, and a single quiet run against any number of crowded
    /// ones never does, because the clustered variance charges that lone run for carrying the whole
    /// statistic. A separate session floor would only duplicate that, less well and with a number
    /// nobody could derive. The coordinator's own reporting floor still applies.
    /// </para>
    /// <para>
    /// The gates run cheapest first: most tests in a window never vary their concurrency and are
    /// rejected by counting distinct levels, before any statistic is computed.
    /// </para>
    /// </remarks>
    private static Examination Examine(AnalysisContext context, string fingerprint)
    {
        List<Measured> considered = Considered(context, fingerprint);
        ConcurrencyRange range = Range(considered);

        // A test whose concurrency never varied. There is no trend to test for and so nothing to
        // charge the correction with: a fingerprint that never ran at two levels is not a
        // comparison this provider made and lost, it is one it could not make.
        if (range.DistinctLevels < 2)
            return Examination.NotPosed;

        List<TrendPoint> points = [.. considered.Select(m =>
            new TrendPoint(m.Concurrency, m.Reference.Failed, m.Reference.SessionIndex))];

        // From here the fingerprint has been tested, whatever the gates below say. Every return
        // past this line counts towards the family the coordinator corrects against.
        TrendStatistic statistic = CochranArmitage.Of(points);

        if (statistic.PValue > Alpha)
            return Examination.Of(null);

        double tau = KendallTau.TauB(points);

        // The two disagree only where a level value is an outlier — the statistic scores levels by
        // value and τ_b by rank, so failures at 2 among levels of 1, 2 and 100 can pull one up while
        // the other goes down. Publishing a direction that the published effect size contradicts is
        // not defensible, and declining costs one comparison.
        if (Math.Sign(statistic.Z) != Math.Sign(tau))
            return Examination.Of(null);

        if (Math.Abs(tau) < LocalAnalysisConstants.ParallelSensitivityTau)
            return Examination.Of(null);

        TestReference? test = context.Tests.ReferenceFor(fingerprint);
        if (test == null)
            return Examination.Of(null);

        ConcurrencyDirection direction = tau >= 0
            ? ConcurrencyDirection.WithConcurrency
            : ConcurrencyDirection.AgainstConcurrency;

        // The end of the range the trend points at, and the end it points away from. The threshold
        // above is absolute, so this is the only place the direction is resolved.
        int loud = direction == ConcurrencyDirection.WithConcurrency ? range.Max : range.Min;
        int quiet = direction == ConcurrencyDirection.WithConcurrency ? range.Min : range.Max;

        List<Measured> driving = Driving(considered, direction);

        return Examination.Of(new FindingCandidate(
            FindingKind.ParallelSensitive,
            new FindingSubject.SingleTest(test),
            new ParallelSensitiveEvidence(
                new ConcurrencyTrend(
                    FindingOrder.Round(statistic.Z),
                    FindingOrder.RoundProbability(statistic.PValue),
                    FindingOrder.Round(tau),
                    DistinctSessions(considered),
                    direction.ToString()),
                range,
                Levels(considered),
                Exemplars(driving, direction),
                Contrast(considered, quiet)),

            // The correlation discounted by how precisely it was measured, rather than the
            // correlation itself. The emission decision is made on the estimate above, so what is
            // reported has not changed; what this changes is where it ranks. A perfectly separated
            // window of four runs a side and one of twenty a side both read τ_b 1.00, and ranking on
            // that would put the thinnest evidence at the top of the report — which is what #159
            // exists to prevent. It is a rank and not a confidence bound; `Support` says why.
            Unreliability: Support(tau, statistic.Z),

            // Dated by the failures that drove the trend rather than by the test's last execution, or
            // by its last failure of any kind. A test that failed under load a fortnight ago and has
            // run cleanly since should decay; dating it by its newest passing run would hold it at
            // full recency forever, and dating it by a recent failure at the quiet end of the range
            // would hold it there on the strength of a counterexample.
            LastOccurrenceIn: TestIndex.NewestSession(driving.Select(m => m.Reference)),

            DrillDownCommand: DrillDown.ForTest(FindingKind.ParallelSensitive, test),

            PValue: statistic.PValue));
    }

    /// <summary>
    /// Collects the executions this test can be measured on, in a stable order.
    /// </summary>
    /// <remarks>
    /// Two exclusions, both of which drop the execution entirely rather than substituting a value.
    /// An execution whose adapter recorded no orchestration data cannot be placed at a level, and
    /// defaulting it to "ran alone" would invent the very measurement the finding is about.
    /// Environmental sessions go for the reason §6 gives: an outage lands at whichever levels its
    /// sessions occupy and manufactures a trend out of a bad afternoon.
    /// </remarks>
    private static List<Measured> Considered(AnalysisContext context, string fingerprint)
    {
        List<Measured> considered = [];

        foreach (ExecutionRef reference in context.Tests.ExecutionsOf(fingerprint))
        {
            if (context.SessionViewFor(reference.Session.SessionId)?.IsLikelyEnvironmental == true)
                continue;

            TestOrchestrationRecord? orchestration = reference.Execution.TestOrchestrationRecord;

            // A default-constructed record reads zero, which is how an execution that predates the
            // field — or an adapter that never filled it — arrives here. One is the lowest count a
            // measurement can honestly report, since a running test always counts itself.
            if (orchestration is not { ConcurrentTestCount: >= 1 })
                continue;

            considered.Add(new Measured(reference, orchestration.ConcurrentTestCount));
        }

        return considered;
    }

    /// <summary>
    /// The failures that produced the trend, as opposed to the ones that argued against it.
    /// </summary>
    /// <param name="considered">Every execution the test can be measured on.</param>
    /// <param name="direction">Which way the trend was found to point.</param>
    /// <returns>The failures on the trend's side of the mean level; never empty.</returns>
    /// <remarks>
    /// <para>
    /// The statistic reduces exactly to the sum, over the failures alone, of how far each one sat
    /// from the mean level — every passing execution's contribution cancels against the failures'
    /// once the two are written out. So a failure counts towards a rising trend if and only if it
    /// happened above the mean, and against it otherwise, and this is the set the statistic was
    /// actually built from rather than an approximation of it.
    /// </para>
    /// <para>
    /// It cannot come back empty. A positive statistic <i>is</i> a positive sum over these failures,
    /// so at least one of them must sit on that side, and a statistic of zero was declined long
    /// before this is reached.
    /// </para>
    /// <para>
    /// Both the exemplars and the finding's date are drawn from here, and they have to agree: a
    /// finding whose three exemplars are crowded failures but whose recency comes from last night's
    /// solitary failure at concurrency 1 would sit at the top of the report on the strength of the
    /// one observation that contradicts it.
    /// </para>
    /// </remarks>
    private static List<Measured> Driving(List<Measured> considered, ConcurrencyDirection direction)
    {
        double mean = 0;
        foreach (Measured measured in considered)
            mean += measured.Concurrency;

        mean /= considered.Count;

        return
        [
            .. considered.Where(m => m.Reference.Failed && (direction == ConcurrencyDirection.WithConcurrency
                ? m.Concurrency > mean
                : m.Concurrency < mean))
        ];
    }

    /// <summary>
    /// Counts the distinct sessions a set of executions came from.
    /// </summary>
    private static int DistinctSessions(IEnumerable<Measured> executions)
    {
        var sessions = new HashSet<Guid>();

        foreach (Measured measured in executions)
            sessions.Add(measured.Reference.Session.SessionId);

        return sessions.Count;
    }

    /// <summary>
    /// Summarises every level observed, ascending.
    /// </summary>
    /// <remarks>
    /// The whole table rather than the two ends. The dose-response is the claim this finding makes,
    /// and a reader who can only see the endpoints cannot tell a monotone rise across six levels from
    /// two extremes with noise between them.
    /// </remarks>
    private static List<ConcurrencyLevel> Levels(List<Measured> considered)
    {
        var grouped = new SortedDictionary<int, List<Measured>>();

        foreach (Measured measured in considered)
        {
            if (!grouped.TryGetValue(measured.Concurrency, out List<Measured>? level))
                grouped[measured.Concurrency] = level = [];

            level.Add(measured);
        }

        var levels = new List<ConcurrencyLevel>(grouped.Count);

        foreach ((int concurrency, List<Measured> level) in grouped)
        {
            int failures = level.Count(m => m.Reference.Failed);

            levels.Add(new ConcurrencyLevel(
                concurrency,
                level.Count,
                DistinctSessions(level),
                failures,
                FindingOrder.Round((double)failures / level.Count)));
        }

        return levels;
    }

    private static ConcurrencyRange Range(List<Measured> considered)
    {
        if (considered.Count == 0)
            return new ConcurrencyRange(0, 0, 0);

        int min = int.MaxValue;
        int max = int.MinValue;
        var levels = new HashSet<int>();

        foreach (Measured measured in considered)
        {
            min = Math.Min(min, measured.Concurrency);
            max = Math.Max(max, measured.Concurrency);
            levels.Add(measured.Concurrency);
        }

        return new ConcurrencyRange(min, max, levels.Count);
    }

    /// <summary>
    /// Picks up to three of the failures that drove the trend.
    /// </summary>
    /// <param name="driving">The failures on the trend's side of the mean level.</param>
    /// <param name="direction">Which way the trend points.</param>
    /// <remarks>
    /// Ordered by concurrency towards the trend's direction before anything else, which is a
    /// departure from the newest-first rule the rest of the report follows and is deliberate: inside
    /// a rising trend a failure at concurrency 2 is a counterexample, not an exemplar, and showing
    /// the reader the newest three failures would as often as not show them the ones that argue
    /// against the finding. Recency still breaks the ties, so the choice stays stable between runs.
    /// </remarks>
    private static List<ConcurrencyExemplar> Exemplars(
        List<Measured> driving, ConcurrencyDirection direction) =>
        [.. Ordered(driving, direction).Take(MaxExemplars).Select(ToExemplar)];

    /// <summary>
    /// Picks one execution typical of the other end of the range.
    /// </summary>
    /// <remarks>
    /// A passing execution where that end has one, because the pair only makes the trend reasonable
    /// about if the other end shows the behaviour the finding claims is absent there. Falls back to
    /// the newest execution at that level rather than to nothing when it also failed — it failed
    /// less, which is the whole claim, and showing it is more honest than showing an empty field a
    /// reader cannot tell from analysis that never looked.
    /// </remarks>
    private static ConcurrencyExemplar? Contrast(List<Measured> considered, int level)
    {
        List<Measured> quiet = [.. considered.Where(m => m.Concurrency == level)];
        List<Measured> passing = [.. quiet.Where(m => !m.Reference.Failed)];

        Measured? typical = Ordered(passing.Count > 0 ? passing : quiet, null).FirstOrDefault();

        return typical == null ? null : ToExemplar(typical);
    }

    /// <summary>
    /// Orders executions newest first, breaking every tie totally.
    /// </summary>
    /// <param name="executions">The executions to order.</param>
    /// <param name="towards">
    /// The direction to sort concurrency in first, or <see langword="null"/> to order by recency
    /// alone.
    /// </param>
    private static IOrderedEnumerable<Measured> Ordered(
        List<Measured> executions, ConcurrencyDirection? towards) =>
        executions
            .OrderBy(m => towards switch
            {
                ConcurrencyDirection.WithConcurrency => -m.Concurrency,
                ConcurrencyDirection.AgainstConcurrency => m.Concurrency,
                _ => 0
            })
            .ThenBy(m => m.Reference.SessionIndex)
            .ThenBy(m => m.Reference.Execution.Retry?.AttemptNumber ?? 1)
            .ThenBy(m => m.Reference.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

    private static ConcurrencyExemplar ToExemplar(Measured measured) =>
        new(
            measured.Reference.Session.SessionId.ToString("D", CultureInfo.InvariantCulture),
            measured.Reference.Session.StartedAt,
            RevisionContext.ReadSha(measured.Reference.Session),
            measured.Reference.Execution.Outcome.ToString(),
            measured.Concurrency);

    /// <summary>
    /// The correlation, discounted by how precisely the runs behind it measured a trend.
    /// </summary>
    /// <param name="tau">The estimated correlation.</param>
    /// <param name="z">The standardised trend the same observations produced.</param>
    /// <returns>A rank in [0,1]; 0 wherever the trend barely cleared its own threshold.</returns>
    /// <remarks>
    /// <para>
    /// <b>Not a confidence bound, and the difference matters.</b> Every other kind's
    /// <c>Unreliability</c> is a genuine interval endpoint on the quantity the kind measures — see
    /// <see cref="WilsonInterval"/>. This one is not, because neither way of putting an interval
    /// round a rank correlation survives contact with this data: τ_b's asymptotic variance assumes
    /// the executions are independent, which retries make false, and a delete-one-run jackknife
    /// returns a standard error of exactly zero whenever the runs agree with each other, which would
    /// rank five identical runs alongside fifty.
    /// </para>
    /// <para>
    /// What it is instead is the effect size scaled by <c>1 − z₉₅ / |Z|</c>, the share of the trend
    /// statistic that is in excess of the threshold it had to clear. That is the shape a Wald bound
    /// takes when a standard error is recovered from a statistic, and it is deliberately not called
    /// one: <see cref="CochranArmitage"/> scores the levels by value while
    /// <see cref="KendallTau"/> scores them by rank, so the two are measuring the same association
    /// with different functionals and a monotone remapping of the levels moves this without moving
    /// τ_b at all.
    /// </para>
    /// <para>
    /// What it does do is the job #159 asks of a rank, which is that a claim resting on more runs
    /// outranks the same claim resting on fewer. The statistic grows with the runs behind a trend
    /// and the effect size does not, so a perfect dose-response over four runs a side scores 0.01
    /// and the same one over twenty scores 0.67. A trend that barely cleared its threshold falls to
    /// the bottom of the report, where a reader can still find it.
    /// </para>
    /// <para>
    /// Conservative on two further counts, both deliberate: the statistic it divides by has already
    /// been shrunk by the continuity correction, and the correction is largest exactly where the
    /// evidence is thinnest. Both push the rank down, which is the direction to err in for a number
    /// whose only job is to decide what a reader is shown first.
    /// </para>
    /// <para>
    /// <b>The scale is not the other kinds' scale, and that is worth knowing before this is compared
    /// across kinds.</b> The statistic grows about as fast as the square root of the runs behind it,
    /// so this is capped near <c>1 − z₉₅/√(G−1)</c> — about 0.53 on a default twenty-run window,
    /// where a Wilson bound on a proportion can approach 1.00. Concurrency findings therefore band
    /// lower than comparable findings of other kinds at the same strength of evidence.
    /// <see cref="Scoring.ImpactScorer"/> weights every kind's figure alike, so the effect is real
    /// rather than notional, and it is the price of ranking a rank correlation on the same [0,1]
    /// axis as a rate.
    /// </para>
    /// </remarks>
    private static double Support(double tau, double z) =>
        Math.Abs(z) <= WilsonInterval.ConfidenceZ
            ? 0
            : Math.Clamp(Math.Abs(tau) * (1 - (WilsonInterval.ConfidenceZ / Math.Abs(z))), 0, 1);

    /// <summary>
    /// One execution together with the concurrency it ran at.
    /// </summary>
    /// <param name="Reference">The execution and the session it belongs to.</param>
    /// <param name="Concurrency">Tests in flight alongside it, itself included.</param>
    private sealed record Measured(ExecutionRef Reference, int Concurrency);
}
