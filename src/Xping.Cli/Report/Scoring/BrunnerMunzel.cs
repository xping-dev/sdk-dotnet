/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// Whether one set of measurements is drawn from a slower distribution than another, or is doing
/// what it always did.
/// </summary>
/// <remarks>
/// <para>
/// Answers the stochastic-superiority question — is <c>P(baseline &lt; current) + ½P(baseline =
/// current)</c> greater than one half — by the Brunner–Munzel statistic, referred to the exact
/// permutation distribution of that statistic over every way the readings could have been split
/// between the two arms.
/// </para>
/// <para>
/// <b>Brunner–Munzel rather than Mann–Whitney.</b> The two arms have unequal variances by
/// construction: the recent arm is three runs, possibly on different hardware, against a fortnight
/// of history. Mann–Whitney's null is that the two distributions are <i>identical</i>, so it rejects
/// on a difference in spread alone and calls that a shift in location. Brunner–Munzel studentises by
/// each arm's own rank variance and tests the ordering directly, which is the hypothesis a reader of
/// "this test got slower" actually has in mind.
/// </para>
/// <para>
/// <b>The permutation distribution rather than a t approximation.</b> Neubert and Brunner (2007) is
/// the small-sample answer for this statistic, and small samples are all this ever sees: the recent
/// arm is at most three readings. The asymptotic form is also undefined on the commonest shape here.
/// Where both arms are internally tied — a fast test quantised to the same millisecond in every run
/// — both rank variances are exactly zero, and the statistic is <c>x/0</c> when the arms separate
/// and <c>0/0</c> when they do not, so the Welch–Satterthwaite degrees of freedom come out
/// <c>NaN</c>. The permutation form needs no such rescue: it counts arrangements, and the observed
/// one is always among them.
/// </para>
/// <para>
/// <b>What that calibration is exact for, and what it is not.</b> Relabelling the pooled readings is
/// exact under <i>exchangeability</i> — the two arms drawn from one distribution — which is the null
/// most windows sit under and the one the level is quoted at. The null this statistic is named for
/// is weaker: it asks only that neither arm be stochastically larger and permits the two to differ
/// in spread, which is the reason it was chosen over Mann–Whitney. Studentising is what makes a
/// permutation calibration valid under that weaker null, and it is valid there asymptotically;
/// three readings are not asymptotic. Measured with both arms centred so superiority is exactly one
/// half, seventeen readings against three, read at 0.01: the rejection rate is 0.0002 where the
/// baseline is four times the more variable arm and 0.0386 and 0.0729 where the recent arm is twice
/// and four times the more variable. The conservative direction is the common one — a fortnight of
/// history spreads wider than three runs — but the liberal one is real. The nonparametric
/// Behrens–Fisher problem has no exact finite-sample solution, so this is a residual to bound rather
/// than a bug to fix, and #187 owns it.
/// </para>
/// <para>
/// <b>Ranks, so the scale does not matter.</b> Every conclusion here is invariant under any strictly
/// increasing transform of the readings, which is why the caller may hand over durations, ratios or
/// logarithms of either and read the same p-value. What the caller must not do is hand over readings
/// that are not independent — several attempts of one test within one run are one occasion, and
/// passing them as several would understate the p-value.
/// </para>
/// <para>
/// This says whether the two differ and in which direction. It says nothing about by how much, and a
/// caller wanting that needs an effect size — <see cref="HodgesLehmann"/> is the one whose ordering
/// agrees with this test's.
/// </para>
/// </remarks>
internal static class BrunnerMunzel
{
    /// <summary>
    /// Relative tolerance for calling two arrangements' statistics equal.
    /// </summary>
    /// <remarks>
    /// A permutation p-value counts the arrangements at least as extreme as the observed one, so an
    /// arrangement that ties with it must be counted. Rank arithmetic over identical readings
    /// produces values equal in exact arithmetic that can differ in the last bit or two, and a bare
    /// <c>&gt;=</c> would then include or drop such an arrangement on the strength of rounding.
    /// </remarks>
    private const double TieTolerance = 1e-9;

    /// <summary>
    /// Estimates <c>P(baseline &lt; current) + ½P(baseline = current)</c>.
    /// </summary>
    /// <param name="baseline">The readings to compare against.</param>
    /// <param name="current">The readings under suspicion.</param>
    /// <returns>
    /// The estimate, in [0,1]; one half when either arm is empty, which is the answer that claims
    /// nothing.
    /// </returns>
    /// <remarks>
    /// One half is "the two are interchangeable"; one is "every recent reading is above every
    /// baseline reading". Computed from midranks over the pooled sample rather than by counting
    /// pairs, which is the same number and is what the statistic is built from.
    /// </remarks>
    public static double Superiority(IReadOnlyList<double> baseline, IReadOnlyList<double> current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        if (baseline.Count == 0 || current.Count == 0)
            return 0.5;

        Sample sample = Sample.Pool(baseline, current);
        double mean = 0;

        foreach (int index in sample.Observed)
            mean += sample.Ranks[index];

        mean /= current.Count;

        return (mean - ((current.Count + 1) / 2.0)) / baseline.Count;
    }

    /// <summary>
    /// Probability of seeing an ordering at least this favourable to <paramref name="current"/> when
    /// neither arm is stochastically larger than the other.
    /// </summary>
    /// <param name="baseline">The readings to compare against.</param>
    /// <param name="current">The readings under suspicion.</param>
    /// <returns>
    /// The one-sided p-value, in (0,1]; 1 when either arm is empty or nothing tells the two apart.
    /// Never zero — the observed arrangement is always counted, so the smallest value attainable is
    /// the reciprocal of the number of arrangements. Exact under exchangeability; see the remarks on
    /// this class for where it is not.
    /// </returns>
    /// <remarks>
    /// <para>
    /// One-sided, in the direction of <paramref name="current"/> being the slower arm. The kind this
    /// serves only ever claims a slowdown, and a two-sided p would spend half its power on a
    /// direction nobody asked about.
    /// </para>
    /// <para>
    /// That floor of <c>1 / C(n, k)</c> is the honest statement of how little a handful of readings
    /// can establish: three recent runs against seven baseline ones can reach 1/120 and no lower,
    /// and only when every recent run was slower than every run before it. It is why a caller with
    /// a short baseline has to decide what level it will read this at before deciding how much
    /// history to require — below seven, three recent runs cannot reach 0.01 however extreme the
    /// durations are.
    /// </para>
    /// </remarks>
    public static double OneSidedPValue(
        IReadOnlyList<double> baseline, IReadOnlyList<double> current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        if (baseline.Count == 0 || current.Count == 0)
            return 1.0;

        Sample sample = Sample.Pool(baseline, current);
        var scratch = new Scratch(baseline.Count, current.Count);

        double observed = sample.Statistic(sample.Observed, scratch);

        long extreme = 0;
        long total = 0;

        // Every way of choosing which readings the recent arm holds, in one fixed lexicographic
        // order over the ascending pooled sample. The enumeration is therefore a property of the
        // readings and not of the order they arrived in, which is what keeps two reports over one
        // unchanged store byte-identical.
        int[] arm = new int[current.Count];
        for (int i = 0; i < arm.Length; i++)
            arm[i] = i;

        while (true)
        {
            total++;
            if (AtLeastAsExtreme(sample.Statistic(arm, scratch), observed))
                extreme++;

            int cursor = arm.Length - 1;
            while (cursor >= 0 && arm[cursor] == sample.Values.Length - arm.Length + cursor)
                cursor--;

            if (cursor < 0)
                break;

            arm[cursor]++;
            for (int i = cursor + 1; i < arm.Length; i++)
                arm[i] = arm[i - 1] + 1;
        }

        return (double)extreme / total;
    }

    /// <summary>
    /// Whether one arrangement speaks at least as strongly for the recent arm as the observed one.
    /// </summary>
    /// <remarks>
    /// The tolerance is skipped where either value is infinite, because subtracting one from an
    /// infinity gives <c>NaN</c> and every comparison against that is false — which would count no
    /// arrangements at all and report a p-value of zero for the one shape, complete separation with
    /// no spread inside either arm, where the answer should be <c>1 / C(n, k)</c>.
    /// </remarks>
    private static bool AtLeastAsExtreme(double statistic, double observed) =>
        double.IsInfinity(statistic) || double.IsInfinity(observed)
            ? statistic >= observed
            : statistic >= observed - (TieTolerance * (1 + Math.Abs(observed)));

    /// <summary>
    /// The pooled readings, their midranks, and where the observed recent arm sits among them.
    /// </summary>
    /// <param name="Values">Every reading, ascending.</param>
    /// <param name="Ranks">Midrank of each, which no rearrangement of the arms changes.</param>
    /// <param name="Observed">
    /// Indices the recent arm actually occupies, ascending. Among readings tied with each other the
    /// choice of index is arbitrary and cannot move the statistic, since the two are the same number.
    /// </param>
    private sealed record Sample(double[] Values, double[] Ranks, int[] Observed)
    {
        /// <summary>
        /// Pools two arms into one ascending sample.
        /// </summary>
        public static Sample Pool(IReadOnlyList<double> baseline, IReadOnlyList<double> current)
        {
            int total = baseline.Count + current.Count;

            double[] values = new double[total];
            bool[] recent = new bool[total];

            for (int i = 0; i < baseline.Count; i++)
                values[i] = baseline[i];

            for (int i = 0; i < current.Count; i++)
            {
                values[baseline.Count + i] = current[i];
                recent[baseline.Count + i] = true;
            }

            double[] keys = [.. values];
            Array.Sort(keys, recent);
            Array.Sort(values);

            int[] observed = new int[current.Count];
            int taken = 0;
            for (int i = 0; i < total; i++)
            {
                if (recent[i])
                    observed[taken++] = i;
            }

            return new Sample(values, Midranks(values), observed);
        }

        /// <summary>
        /// The studentised Brunner–Munzel statistic for one arrangement of the pooled readings.
        /// </summary>
        /// <param name="arm">Indices the recent arm holds, ascending.</param>
        /// <param name="scratch">Buffers to work in, so an enumeration allocates nothing.</param>
        /// <returns>
        /// The statistic, an infinity where the arms separate with no spread inside either, or zero
        /// where nothing distinguishes them at all.
        /// </returns>
        /// <remarks>
        /// The degenerate cases are not an edge: a test taking the same number of milliseconds in
        /// every run produces them every time. Treating a positive difference over no spread as
        /// <c>+∞</c> and no difference over no spread as <c>0</c> is what the limits say, and it is
        /// what makes a wholly tied sample return a p-value of exactly 1 — every arrangement ties
        /// with the observed one — rather than the 0 a comparison against <c>NaN</c> would give.
        /// </remarks>
        public double Statistic(int[] arm, Scratch scratch)
        {
            Split(arm, scratch);

            // Both arms come out ascending because the pooled sample is, so the within-arm midranks
            // need no further sorting.
            FillMidranks(scratch.CurrentValues, scratch.CurrentWithin);
            FillMidranks(scratch.BaselineValues, scratch.BaselineWithin);

            double currentMean = Average(scratch.CurrentRanks);
            double baselineMean = Average(scratch.BaselineRanks);

            int baselineCount = scratch.BaselineValues.Length;
            int currentCount = scratch.CurrentValues.Length;

            double numerator =
                baselineCount * currentCount * (currentMean - baselineMean) /
                (baselineCount + currentCount);

            double denominator = Math.Sqrt(
                (baselineCount * RankVariance(scratch.BaselineRanks, scratch.BaselineWithin, baselineMean)) +
                (currentCount * RankVariance(scratch.CurrentRanks, scratch.CurrentWithin, currentMean)));

            if (denominator > 0)
                return numerator / denominator;

            return numerator > 0 ? double.PositiveInfinity
                : numerator < 0 ? double.NegativeInfinity
                : 0;
        }

        /// <summary>
        /// Deals the pooled readings and their midranks into the two arms.
        /// </summary>
        private void Split(int[] arm, Scratch scratch)
        {
            int next = 0;
            int left = 0;
            int taken = 0;

            for (int i = 0; i < Values.Length; i++)
            {
                if (next < arm.Length && arm[next] == i)
                {
                    scratch.CurrentValues[taken] = Values[i];
                    scratch.CurrentRanks[taken] = Ranks[i];
                    taken++;
                    next++;
                }
                else
                {
                    scratch.BaselineValues[left] = Values[i];
                    scratch.BaselineRanks[left] = Ranks[i];
                    left++;
                }
            }
        }
    }

    /// <summary>
    /// Reusable buffers for one comparison's enumeration.
    /// </summary>
    /// <remarks>
    /// A default window enumerates over a thousand arrangements and a long one twelve thousand, each
    /// needing six arrays of the arms' sizes. Allocating them once per comparison rather than once
    /// per arrangement is the difference between a report that runs and one that spends its time in
    /// the collector.
    /// </remarks>
    private sealed class Scratch
    {
        public Scratch(int baselineCount, int currentCount)
        {
            BaselineValues = new double[baselineCount];
            BaselineRanks = new double[baselineCount];
            BaselineWithin = new double[baselineCount];
            CurrentValues = new double[currentCount];
            CurrentRanks = new double[currentCount];
            CurrentWithin = new double[currentCount];
        }

        public double[] BaselineValues { get; }

        public double[] BaselineRanks { get; }

        public double[] BaselineWithin { get; }

        public double[] CurrentValues { get; }

        public double[] CurrentRanks { get; }

        public double[] CurrentWithin { get; }
    }

    /// <summary>
    /// One arm's rank variance, the quantity that studentises the statistic.
    /// </summary>
    /// <remarks>
    /// <c>1/(n−1) · Σ (R − R' − R̄ + (n+1)/2)²</c>, where <c>R</c> is the reading's midrank in the
    /// pooled sample and <c>R'</c> its midrank within its own arm. Zero at a single reading, and
    /// zero whenever an arm's readings are all equal.
    /// </remarks>
    private static double RankVariance(double[] pooledRanks, double[] withinRanks, double mean)
    {
        if (pooledRanks.Length < 2)
            return 0;

        double centre = (pooledRanks.Length + 1) / 2.0;
        double sum = 0;

        for (int i = 0; i < pooledRanks.Length; i++)
        {
            double term = pooledRanks[i] - withinRanks[i] - mean + centre;
            sum += term * term;
        }

        return sum / (pooledRanks.Length - 1);
    }

    /// <summary>
    /// Midranks of an ascending sample: the average of the ranks a run of tied readings spans.
    /// </summary>
    private static double[] Midranks(double[] ascending)
    {
        double[] ranks = new double[ascending.Length];
        FillMidranks(ascending, ranks);

        return ranks;
    }

    /// <summary>
    /// Writes the midranks of an ascending sample into a caller-supplied buffer.
    /// </summary>
    private static void FillMidranks(double[] ascending, double[] ranks)
    {
        int start = 0;
        while (start < ascending.Length)
        {
            int end = start;
            while (end + 1 < ascending.Length && ascending[end + 1] == ascending[start])
                end++;

            double midrank = (start + end + 2) / 2.0;
            for (int i = start; i <= end; i++)
                ranks[i] = midrank;

            start = end + 1;
        }
    }

    private static double Average(double[] values)
    {
        double sum = 0;
        foreach (double value in values)
            sum += value;

        return sum / values.Length;
    }
}
