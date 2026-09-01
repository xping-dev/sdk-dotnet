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
/// One execution, published so the arm it landed in can be checked.
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
/// One side of the split, always carrying the counts its rate was computed from.
/// </summary>
/// <param name="Failures">Executions in this arm that failed.</param>
/// <param name="Executions">Executions in this arm.</param>
/// <param name="Sessions">Distinct runs those executions came from.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Executions"/>.</param>
/// <param name="MinConcurrency">Lowest concurrency observed in this arm.</param>
/// <param name="MaxConcurrency">Highest concurrency observed in this arm.</param>
internal sealed record ConcurrencyArm(
    int Failures,
    int Executions,
    int Sessions,
    double FailureRate,
    int MinConcurrency,
    int MaxConcurrency);

/// <summary>
/// The gap between the two arms, signed so the direction is read rather than derived.
/// </summary>
/// <remarks>
/// Positive means the test failed more in the crowded arm, negative more in the quiet one. Both
/// qualify: a test that only fails when it runs nearly alone is as real a defect as one that only
/// fails under contention, and it would otherwise be reported by nothing.
/// </remarks>
/// <param name="FailureRate">High-arm rate minus low-arm rate.</param>
/// <param name="FailureRatePct">The same difference in percentage points.</param>
internal sealed record ConcurrencyDelta(double FailureRate, double FailureRatePct);

/// <summary>
/// The concurrency the test was actually observed at, across both arms.
/// </summary>
/// <remarks>
/// Published so a reader can see how much room the split had. Two arms either side of a median of 12
/// on levels spanning 11 to 13 is a far weaker statement than the same split on levels spanning 1 to
/// 14, and nothing else in the evidence would show the difference.
/// </remarks>
/// <param name="Min">Lowest concurrency observed.</param>
/// <param name="Max">Highest concurrency observed.</param>
/// <param name="DistinctLevels">How many distinct concurrency values were seen.</param>
internal sealed record ConcurrencyRange(int Min, int Max, int DistinctLevels);

/// <summary>
/// Evidence that a test's failure rate moves with how many tests ran alongside it.
/// </summary>
/// <param name="High">Executions above the test's own median concurrency.</param>
/// <param name="Low">Executions at or below it.</param>
/// <param name="Delta">The signed difference in failure rate, which the threshold was applied to.</param>
/// <param name="SplitAtConcurrency">The median the arms were formed at.</param>
/// <param name="Observed">The concurrency range the split was made within.</param>
/// <param name="Exemplars">Up to three executions from the worse arm, newest first.</param>
/// <param name="Contrast">One execution typical of the other arm.</param>
internal sealed record ParallelSensitiveEvidence(
    ConcurrencyArm High,
    ConcurrencyArm Low,
    ConcurrencyDelta Delta,
    int SplitAtConcurrency,
    ConcurrencyRange Observed,
    IReadOnlyList<ConcurrencyExemplar> Exemplars,
    ConcurrencyExemplar? Contrast) : FindingEvidence;

/// <summary>
/// Reports tests whose failure rate depends on how many other tests were running at the time.
/// </summary>
/// <remarks>
/// <para>
/// Each test is split at <b>its own median</b> observed concurrency and the two halves' failure rates
/// compared. A per-test median rather than a fixed boundary is what makes this a property of the test
/// instead of a property of the suite it happens to live in: a class that always runs in a crowded
/// collection and one that always runs in a quiet corner are each measured against their own normal.
/// </para>
/// <para>
/// <b>The boolean is deliberately unused.</b> <see cref="TestOrchestrationRecord.WasParallelized"/>
/// reports concurrency correctly today, but it is the wrong measurement here. Concurrency level
/// varies freely between runs while the flag derived from it does not, because the variation happens
/// among values that are all greater than one. Verified against a parallel assembly over three runs:
/// 360 of 770 tests ran at more than one concurrency level, with spreads as wide as eight to
/// fourteen, and yet not one test was ever on both sides of the flag — 646 were parallel every run
/// and 123 serial every run. Splitting on the flag would have nothing to compare.
/// </para>
/// <para>
/// The median split subsumes the parallel-versus-serial comparison rather than replacing it. A suite
/// whose parallelisation setting changed inside the window puts its concurrency-one executions and
/// its concurrency-<i>n</i> executions either side of the median on its own, with no special case.
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

    /// <inheritdoc/>
    public string Name => "concurrency";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds => [FindingKind.ParallelSensitive];

    /// <inheritdoc/>
    public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Fingerprints are ordinal-sorted by the index, so findings come out in the same sequence on
        // every run whatever order the sessions were read in.
        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            FindingCandidate? candidate = Examine(context, fingerprint);

            if (candidate != null)
                yield return candidate;
        }
    }

    /// <summary>
    /// Examines one test, returning <see langword="null"/> when any gate declines it.
    /// </summary>
    private static FindingCandidate? Examine(AnalysisContext context, string fingerprint)
    {
        List<Measured> considered = Considered(context, fingerprint);

        // Fewer than two arms' worth cannot be split at all, and checking here saves sorting the
        // overwhelming majority of tests, which never vary their concurrency. An execution count is a
        // conservative stand-in for the session gate below — a test cannot have run in more sessions
        // than it has executions — so this rejects only tests that gate would reject anyway.
        if (considered.Count < LocalAnalysisConstants.ParallelSensitiveMinArmSessions * 2)
            return null;

        int median = MedianConcurrency(considered);

        List<Measured> low = [.. considered.Where(m => m.Concurrency <= median)];
        List<Measured> high = [.. considered.Where(m => m.Concurrency > median)];

        // An empty high arm is a test whose concurrency never varied. That is the common case and it
        // is not a finding: the question was asked and the data answered it.
        //
        // Gated on distinct sessions rather than on executions, while the rates below stay over
        // executions. The asymmetry is deliberate and is what separates this provider from the
        // temporal one: concurrency genuinely differs between attempts within a session, so those
        // executions are real, distinct readings and collapsing them would discard the signal. What
        // they are not is independent occasions — ten attempts across two sessions are one afternoon,
        // and a gate counting them would let it pass for ten.
        if (DistinctSessions(low) < LocalAnalysisConstants.ParallelSensitiveMinArmSessions ||
            DistinctSessions(high) < LocalAnalysisConstants.ParallelSensitiveMinArmSessions)
        {
            return null;
        }

        double highRate = FailureRate(high);
        double lowRate = FailureRate(low);
        double delta = highRate - lowRate;

        if (Math.Abs(delta) < LocalAnalysisConstants.ParallelSensitivityDelta)
            return null;

        TestReference? test = context.Tests.ReferenceFor(fingerprint);
        if (test == null)
            return null;

        // The arm holding the failures, whichever side of the median it fell on. The threshold above
        // is absolute, so this is the only place the direction is resolved.
        (List<Measured> worse, List<Measured> quieter) = delta >= 0 ? (high, low) : (low, high);

        List<Measured> failures = [.. worse.Where(m => m.Reference.Failed)];

        EffectiveCounts highEffective = Deflate(high);
        EffectiveCounts lowEffective = Deflate(low);

        return new FindingCandidate(
            FindingKind.ParallelSensitive,
            new FindingSubject.SingleTest(test),
            new ParallelSensitiveEvidence(
                Summarise(high, highRate),
                Summarise(low, lowRate),
                new ConcurrencyDelta(
                    FindingOrder.Round(delta),
                    FindingOrder.RoundPercent(delta * 100)),
                median,
                Range(considered),
                Exemplars(failures),
                Contrast(quieter)),

            // The least gap the two arms support, rather than the gap they happened to show. The
            // condition still thresholds the observed delta, so what is emitted has not changed;
            // what changed is where it ranks. Five sessions a side is the smallest split allowed
            // and produces the largest observed deltas in the report, so ranking on the observation
            // put the thinnest evidence at the top. A five-a-side split whose interval still admits
            // no difference at all scores zero here and falls to the bottom of the list, where the
            // reader can still find it.
            //
            // Computed on effective sample sizes rather than on the execution counts the rates come
            // from. The arms hold attempts, and attempts within a session are correlated; handing
            // their raw count to an interval that assumes independent trials would let a test buy
            // confidence with retries. A test that retried its way to thirty executions across five
            // sessions would have outranked one measured thirty times over thirty builds, and by the
            // same margin for the same wrong reason the execution-denominated arm gate did.
            Unreliability: WilsonInterval.DifferenceBoundNearestZero(
                highEffective.Failures, highEffective.Sessions,
                lowEffective.Failures, lowEffective.Sessions),

            // Dated by the failures that drove it rather than by the test's last execution. A test
            // that failed under load a fortnight ago and has run cleanly since should decay, and
            // dating it by its newest passing run would hold it at full recency forever.
            //
            // The worse arm always holds at least one failure: its rate exceeds the other arm's by
            // the threshold, so it cannot be zero.
            SessionsSinceLastOccurrence: failures.Min(m => m.Reference.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.ParallelSensitive, test));
    }

    /// <summary>
    /// Collects the executions this test can be measured on, in a stable order.
    /// </summary>
    /// <remarks>
    /// Two exclusions, both of which drop the execution entirely rather than substituting a value.
    /// An execution whose adapter recorded no orchestration data cannot be placed in an arm, and
    /// defaulting it to "ran alone" would invent the very measurement the finding is about.
    /// Environmental sessions go for the reason §6 gives: an outage lands in whichever arm its
    /// sessions occupy and manufactures a delta out of a bad afternoon.
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
    /// Reads the median concurrency by nearest rank.
    /// </summary>
    /// <remarks>
    /// Nearest rank rather than an interpolating variant, matching how the duration provider reads a
    /// percentile: it returns a level the test was actually observed at, so the published split point
    /// is a real number of concurrent tests rather than a fractional one no run could have produced.
    /// </remarks>
    private static int MedianConcurrency(List<Measured> considered)
    {
        var levels = new List<int>(considered.Count);
        foreach (Measured measured in considered)
            levels.Add(measured.Concurrency);

        levels.Sort();

        int rank = (int)Math.Ceiling(0.50 * levels.Count) - 1;

        return levels[Math.Clamp(rank, 0, levels.Count - 1)];
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

    /// <summary>
    /// Counts the distinct sessions an arm's executions came from.
    /// </summary>
    /// <remarks>
    /// The arm gate's denominator, and the figure the evidence publishes beside the execution count,
    /// so a reader can see how many independent occasions a rate over twelve executions rests on.
    /// </remarks>
    private static int DistinctSessions(List<Measured> arm)
    {
        var sessions = new HashSet<Guid>();

        foreach (Measured measured in arm)
            sessions.Add(measured.Reference.Session.SessionId);

        return sessions.Count;
    }

    /// <summary>
    /// An arm's counts scaled down to the independent observations behind them.
    /// </summary>
    /// <param name="Failures">Failures, scaled to <paramref name="Sessions"/>.</param>
    /// <param name="Sessions">Distinct sessions the arm's executions came from.</param>
    private readonly record struct EffectiveCounts(double Failures, double Sessions);

    /// <summary>
    /// Deflates an arm to the sample size its correlated executions are actually worth.
    /// </summary>
    /// <param name="arm">The arm to deflate.</param>
    /// <returns>The effective counts.</returns>
    /// <remarks>
    /// <para>
    /// The design effect for a clustered sample is <c>1 + (m̄ - 1)ρ</c>, for a mean cluster size m̄
    /// and an intra-cluster correlation ρ. This takes ρ = 1, which reduces it to m̄ and the effective
    /// size to the session count. That is the conservative end of the range and it is chosen rather
    /// than estimated: at five to twenty sessions an ICC estimate is noisier than the quantity it
    /// would be correcting, and a bound that moves with the noise in its own correction is worse than
    /// one that is merely cautious.
    /// </para>
    /// <para>
    /// Both counts are scaled, not just the denominator, so the point estimate the interval is
    /// centred on is exactly the observed rate. The arm keeps the concurrency readings its executions
    /// supplied; what it loses is the claim that they were independent trials.
    /// </para>
    /// <para>
    /// One correlation is left uncorrected and is worth naming: a single session can contribute
    /// attempts to both arms, so the two are not independent samples either, and Newcombe's interval
    /// assumes they are. That inflates confidence in the same direction, by less, and correcting it
    /// needs a paired analysis this provider does not have.
    /// </para>
    /// </remarks>
    private static EffectiveCounts Deflate(List<Measured> arm)
    {
        int sessions = DistinctSessions(arm);

        return new EffectiveCounts(FailureRate(arm) * sessions, sessions);
    }

    private static ConcurrencyArm Summarise(List<Measured> arm, double rate)
    {
        int failures = 0;
        int min = int.MaxValue;
        int max = int.MinValue;

        foreach (Measured measured in arm)
        {
            if (measured.Reference.Failed)
                failures++;

            min = Math.Min(min, measured.Concurrency);
            max = Math.Max(max, measured.Concurrency);
        }

        return new ConcurrencyArm(
            failures, arm.Count, DistinctSessions(arm), FindingOrder.Round(rate), min, max);
    }

    private static ConcurrencyRange Range(List<Measured> considered)
    {
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
    /// Picks up to three of the failures that drove the finding, newest first.
    /// </summary>
    private static List<ConcurrencyExemplar> Exemplars(List<Measured> failures) =>
        [.. Ordered(failures).Take(MaxExemplars).Select(ToExemplar)];

    /// <summary>
    /// Picks one execution typical of the other arm.
    /// </summary>
    /// <remarks>
    /// A passing execution where the arm has one, because the pair only makes the difference
    /// reasonable about if the other side shows the behaviour the finding claims is absent there.
    /// Falls back to the newest execution rather than to nothing when the quieter arm also failed —
    /// it failed less, which is the whole claim, and showing it is more honest than showing an empty
    /// field a reader cannot tell from analysis that never looked.
    /// </remarks>
    private static ConcurrencyExemplar? Contrast(List<Measured> quieter)
    {
        List<Measured> passing = [.. quieter.Where(m => !m.Reference.Failed)];

        Measured? typical = Ordered(passing.Count > 0 ? passing : quieter).FirstOrDefault();

        return typical == null ? null : ToExemplar(typical);
    }

    /// <summary>
    /// Orders executions newest first, breaking every tie totally.
    /// </summary>
    private static IOrderedEnumerable<Measured> Ordered(List<Measured> executions) =>
        executions
            .OrderBy(m => m.Reference.SessionIndex)
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
    /// One execution together with the concurrency it ran at.
    /// </summary>
    /// <param name="Reference">The execution and the session it belongs to.</param>
    /// <param name="Concurrency">Tests in flight alongside it, itself included.</param>
    private sealed record Measured(ExecutionRef Reference, int Concurrency);
}
