/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;

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
/// <param name="P50Ms">Median duration, in milliseconds.</param>
/// <param name="P95Ms">95th percentile duration, in milliseconds.</param>
/// <param name="Executions">Executions the percentiles were computed over.</param>
/// <param name="Sessions">Distinct runs those executions came from.</param>
internal sealed record DurationProfile(long P50Ms, long P95Ms, int Executions, int Sessions);

/// <summary>
/// The change in raw wall-clock terms — what a developer would notice on the clock.
/// </summary>
/// <param name="P50Pct">Relative increase in median duration, as a percentage.</param>
/// <param name="P50Ms">Absolute increase in median duration, in milliseconds.</param>
internal sealed record DurationDelta(double P50Pct, long P50Ms);

/// <summary>
/// The change after per-session normalisation — the figure the threshold is actually applied to.
/// </summary>
/// <remarks>
/// Reported separately from <see cref="DurationDelta"/> because the two routinely disagree, and the
/// disagreement is the point: a machine that was busy for the whole of the recent runs moves the raw
/// figure and leaves this one alone.
/// </remarks>
/// <param name="P50Pct">Relative increase in normalised median duration, as a percentage.</param>
internal sealed record NormalisedDurationDelta(double P50Pct);

/// <summary>
/// Evidence that a test's median duration has increased against its own baseline.
/// </summary>
/// <param name="Current">The recent runs.</param>
/// <param name="Baseline">The runs before them.</param>
/// <param name="Delta">The change in raw milliseconds.</param>
/// <param name="NormalisedDelta">The change after normalisation, which the threshold was applied to.</param>
/// <param name="BaselineCv">
/// Dispersion of the baseline, as a coefficient of variation, computed on <b>normalised</b>
/// durations. The delta above it is measured on the normalised scale, and dispersion has to be
/// measured in the same units as the quantity it gates: a baseline that looks steady in raw
/// milliseconds can be swinging once machine speed is divided out, and gating on the raw figure
/// would wave through a shift that sits entirely inside its own noise.
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
    NormalisedDurationDelta NormalisedDelta,
    double BaselineCv,
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
/// <param name="Cv">
/// Dispersion as a coefficient of variation, computed on <b>normalised</b> durations — so this is a
/// claim about the test varying relative to the suite around it, not about the machine having had a
/// bad afternoon, which is the only claim the data supports.
/// </param>
/// <param name="Exemplars">Up to three executions chosen to span the observed spread.</param>
internal sealed record DurationUnstableEvidence(
    int Executions,
    int Sessions,
    long P50Ms,
    long P95Ms,
    long MinMs,
    long MaxMs,
    double Cv,
    IReadOnlyList<DurationExemplar> Exemplars) : FindingEvidence;

/// <summary>
/// Reports how long tests take: whether one has slowed against its own history, and whether its
/// timing is steady enough for that question to have an answer.
/// </summary>
/// <remarks>
/// <para>
/// One provider owns both kinds because they are one judgement about one statistic. Whether a test
/// has regressed cannot be decided without knowing how much its duration ordinarily moves, and a
/// test whose duration moves too much for the question to be answerable is itself the finding.
/// Splitting them would let both claim the same test on opposite sides of the same threshold.
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
    // so they stay local for the same reason as the exemplar budget above. Below these counts a
    // percentile is describing two or three data points and a "median" is a coin toss.
    private const int MinimumBaselineExecutions = 5;
    private const int MinimumCurrentExecutions = 3;

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

            Profile whole = Build(all, medians);
            Profile currentProfile = Build(current, medians);
            Profile baselineProfile = Build(baseline, medians);

            // A regression suppresses the instability finding for the same test. The two are nearly
            // disjoint by construction — one needs a steady baseline, the other an unsteady window —
            // but a large enough shift lifts the whole-window dispersion on its own, and reporting
            // that as instability would state the regression a second time under another name.
            FindingCandidate? candidate =
                Regression(context, test, current, currentProfile, baselineProfile) ??
                Unstable(context, test, all, whole, baselineProfile);

            if (candidate != null)
                yield return candidate;
        }
    }

    /// <summary>
    /// Attempts the regression finding, returning <see langword="null"/> when any gate declines it.
    /// </summary>
    private static FindingCandidate? Regression(
        AnalysisContext context,
        TestReference test,
        List<ExecutionRef> current,
        Profile currentProfile,
        Profile baselineProfile)
    {
        // Nothing to compare against is not a slowdown. A test added this week has history in the
        // window but no history of its own, and calling that a regression would report every new
        // test as one.
        if (baselineProfile.Executions < MinimumBaselineExecutions ||
            currentProfile.Executions < MinimumCurrentExecutions)
        {
            return null;
        }

        double baselineNormalised = baselineProfile.NormalisedP50;

        // An instantaneous or unnormalisable baseline has no meaningful relative increase; the
        // division would produce an infinity that then compares greater than every threshold.
        if (baselineNormalised <= 0)
            return null;

        double increase =
            (currentProfile.NormalisedP50 - baselineNormalised) / baselineNormalised;

        if (increase < LocalAnalysisConstants.DurationRegressionPct)
            return null;

        // Guards the relative test on the scale a developer actually experiences: two milliseconds
        // becoming four is a hundred-percent regression and is not worth anyone's morning.
        double rawIncrease = currentProfile.RawP50 - baselineProfile.RawP50;
        if (rawIncrease < LocalAnalysisConstants.DurationRegressionMinMs)
            return null;

        double baselineCv = CoefficientOfVariation(baselineProfile.Normalised);
        if (baselineCv > LocalAnalysisConstants.DurationStableCvMax)
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
                    (long)rawIncrease),
                new NormalisedDurationDelta(FindingOrder.RoundPercent(increase * 100)),
                FindingOrder.Round(baselineCv),
                FirstSeenAt(current),
                Recent(current),
                Contrast(baselineProfile)),

            // Doubling is as unreliable as this measure gets. Beyond that the test is simply slow,
            // and ranking a tenfold slowdown above a twofold one would crowd out every other kind
            // on the strength of one arithmetic accident.
            Unreliability: Math.Min(1.0, increase / 2),

            SessionsSinceLastOccurrence: current.Min(e => e.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.DurationRegression, test));
    }

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
        Profile baselineProfile)
    {
        double cv = CoefficientOfVariation(whole.Normalised);
        if (cv < LocalAnalysisConstants.DurationUnstableCvMin)
            return null;

        // The test's own prior behaviour where it has any, and the window where it does not. A test
        // first seen this week has no baseline, and refusing to measure it at all would make its
        // instability invisible for as long as the window takes to fill.
        double floor = baselineProfile.Executions > 0 ? baselineProfile.RawP50 : whole.RawP50;

        // Below a few tens of milliseconds the coefficient of variation is measuring the scheduler,
        // not the test.
        if (floor < LocalAnalysisConstants.DurationTrivialMs)
            return null;

        return new FindingCandidate(
            FindingKind.DurationUnstable,
            new FindingSubject.SingleTest(test),
            new DurationUnstableEvidence(
                whole.Executions,
                context.Window.SessionCount,
                (long)whole.RawP50,
                (long)whole.RawP95,
                (long)whole.RawMin,
                (long)whole.RawMax,
                FindingOrder.Round(cv),
                Spanning(all, whole.RawP50)),

            // The dispersion itself, capped. A test whose duration varies by more than its own mean
            // is as unpredictable as the measure can express.
            Unreliability: Math.Min(1.0, cv),

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
            (long)Milliseconds(reference));

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

    private static Profile Build(
        IReadOnlyList<ExecutionRef> executions, Dictionary<Guid, double> medians)
    {
        var raw = new List<double>(executions.Count);
        var normalised = new List<double>(executions.Count);
        var sessions = new HashSet<Guid>();

        foreach (ExecutionRef reference in executions)
        {
            raw.Add(Milliseconds(reference));
            sessions.Add(reference.Session.SessionId);

            if (medians.TryGetValue(reference.Session.SessionId, out double median))
                normalised.Add(Milliseconds(reference) / median);
        }

        // Sorted before anything is computed from them, so every percentile reads the same index and
        // every sum accumulates in the same order on every run.
        raw.Sort();
        normalised.Sort();

        return new Profile(executions, raw, normalised, sessions.Count);
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
    /// Measures dispersion as standard deviation over mean.
    /// </summary>
    /// <returns>
    /// The coefficient, or zero when it cannot be computed. Zero is the conservative answer in both
    /// directions: it passes the stability gate a regression needs and fails the instability gate,
    /// so absent data never produces a finding on its own.
    /// </returns>
    private static double CoefficientOfVariation(List<double> values)
    {
        if (values.Count < 2)
            return 0;

        double total = 0;
        foreach (double value in values)
            total += value;

        double mean = total / values.Count;
        if (mean <= 0)
            return 0;

        double squares = 0;
        foreach (double value in values)
        {
            double deviation = value - mean;
            squares += deviation * deviation;
        }

        return Math.Sqrt(squares / values.Count) / mean;
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
    /// <param name="Normalised">Durations over their own run's median, ascending.</param>
    /// <param name="Sessions">Distinct runs the executions came from.</param>
    private sealed record Profile(
        IReadOnlyList<ExecutionRef> Source,
        List<double> Raw,
        List<double> Normalised,
        int Sessions)
    {
        public int Executions => Raw.Count;

        public double RawP50 => Percentile(Raw, 0.50);

        public double RawP95 => Percentile(Raw, 0.95);

        public double RawMin => Raw.Count == 0 ? 0 : Raw[0];

        public double RawMax => Raw.Count == 0 ? 0 : Raw[^1];

        public double NormalisedP50 => Percentile(Normalised, 0.50);

        public DurationProfile ToPublished() =>
            new((long)RawP50, (long)RawP95, Executions, Sessions);
    }
}
