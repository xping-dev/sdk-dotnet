/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// How many times slower one set of measurements is than another, and how well that is pinned down.
/// </summary>
/// <param name="Ratio">The estimate itself; 1 means the two are the same size.</param>
/// <param name="Low">Lower end of the 95% interval.</param>
/// <param name="High">Upper end of the 95% interval.</param>
internal readonly record struct RatioEstimate(double Ratio, double Low, double High);

/// <summary>
/// The Hodges–Lehmann estimate of how much larger one sample is than another, on a ratio scale.
/// </summary>
/// <remarks>
/// <para>
/// The median of every pairwise ratio <c>current / baseline</c>, with a distribution-free interval
/// read off the same sorted list. "1.8 times slower, 95% CI 1.1 to 3.4" tells a developer something
/// that "+80%" does not: the first says how confident the report is, and the second invites the
/// reader to assume it is certain.
/// </para>
/// <para>
/// <b>Ratios rather than differences of logarithms.</b> Durations are right-skewed and perturbed
/// multiplicatively, so the shift that is worth estimating is a factor and not a number of
/// milliseconds — which is the usual argument for working in log space. The median of the pairwise
/// ratios is exactly the exponential of the median of the pairwise log differences, because the
/// logarithm is monotone and a median only depends on ordering. Taking it directly avoids a
/// round trip that loses the last bits: a fourfold slowdown reads exactly 4 here where
/// <c>exp(log 8 − log 2)</c> reads 3.999999999999999, and a published "600ms" then truncates to 599.
/// At an even number of pairs the midpoint is the geometric mean of the two central ratios, which is
/// the arithmetic midpoint in log space and keeps the identity exact.
/// </para>
/// <para>
/// <b>The Hodges–Lehmann estimate rather than a ratio of the two medians.</b> It is the location
/// estimator consistent with the rank ordering <see cref="BrunnerMunzel"/> tests, so the number
/// reported and the test that admitted it are answering one question. It is also robust — half the
/// pairs have to move before it does — where a quotient of two medians over three and seventeen
/// readings rests on two of them.
/// </para>
/// <para>
/// <b>The interval is the Moses interval</b>, the order statistics of the same pairwise list at the
/// exact Wilcoxon rank-sum critical value. Exact rather than normal-approximated because these arms
/// are small: at five readings against three the approximation asks for an order statistic below the
/// first and yields no interval at all, where the exact rule gives the full spread of the pairwise
/// ratios at 96.4% coverage; at seven against three the two still disagree by a whole rank, the
/// approximation reaching one where the exact value is two. It assumes the two arms differ only in location, which is more than
/// Brunner–Munzel beside it assumes — the interval is therefore the weaker of the two statements,
/// and it is the one published because a reader needs a magnitude.
/// </para>
/// <para>
/// Two-sided at 95%, so the lower end is a 97.5% one-sided claim — deliberately stricter than the
/// one-sided test that admits the finding, and safe to rank on for that reason.
/// </para>
/// </remarks>
internal static class HodgesLehmann
{
    /// <summary>
    /// Half the mass an exact two-sided 95% interval may leave in each tail (0.025).
    /// </summary>
    /// <remarks>
    /// Not in <see cref="LocalAnalysisConstants"/>, for the reason
    /// <see cref="WilsonInterval"/> gives about its own: that class holds the lines measurements are
    /// compared against, and this is the confidence a measurement is stated at.
    /// </remarks>
    private const double TailMass = 0.025;

    /// <summary>
    /// Estimates how many times larger <paramref name="current"/> is than
    /// <paramref name="baseline"/>, without the interval around it.
    /// </summary>
    /// <param name="baseline">The readings to compare against; all strictly positive.</param>
    /// <param name="current">The readings under suspicion.</param>
    /// <returns>The ratio, or 1 when either arm is empty — the answer that claims nothing.</returns>
    /// <remarks>
    /// Separate from <see cref="Of"/> because the estimate costs a sorted list of at most a few
    /// hundred ratios and the interval costs the exact Mann–Whitney null distribution behind it. A
    /// caller gating on the estimate — deciding whether a slowdown is large enough to be worth a
    /// developer's morning before deciding whether it is real — asks this question of every test in
    /// the window and the other one only of the few that get past it.
    /// </remarks>
    public static double Ratio(IReadOnlyList<double> baseline, IReadOnlyList<double> current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        if (baseline.Count == 0 || current.Count == 0)
            return 1;

        return Median(Pairs(baseline, current));
    }

    /// <summary>
    /// Estimates how many times larger <paramref name="current"/> is than
    /// <paramref name="baseline"/>, with the interval that says how well it is pinned down.
    /// </summary>
    /// <param name="baseline">The readings to compare against; all strictly positive.</param>
    /// <param name="current">The readings under suspicion.</param>
    /// <returns>
    /// The ratio and its interval, or a ratio of 1 with an interval of 1 to 1 when either arm is
    /// empty — the answer that claims nothing.
    /// </returns>
    /// <remarks>
    /// A baseline reading that is not positive would make its pairwise ratios infinite and is the
    /// caller's to exclude: nothing can be measured against a test that took no measurable time.
    /// <para>
    /// <see cref="Ratio"/> is the same number without the interval, and is what a caller wanting
    /// only the size of the change should ask for; this one builds a distribution to place the
    /// bounds.
    /// </para>
    /// </remarks>
    public static RatioEstimate Of(IReadOnlyList<double> baseline, IReadOnlyList<double> current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        if (baseline.Count == 0 || current.Count == 0)
            return new RatioEstimate(1, 1, 1);

        double[] pairs = Pairs(baseline, current);
        int rank = CriticalRank(baseline.Count, current.Count);

        return new RatioEstimate(
            Median(pairs), pairs[rank - 1], pairs[pairs.Length - rank]);
    }

    /// <summary>
    /// Every ratio a recent reading makes with a baseline one, ascending.
    /// </summary>
    private static double[] Pairs(IReadOnlyList<double> baseline, IReadOnlyList<double> current)
    {
        double[] pairs = new double[baseline.Count * current.Count];

        int next = 0;
        foreach (double numerator in current)
        {
            foreach (double denominator in baseline)
                pairs[next++] = numerator / denominator;
        }

        Array.Sort(pairs);

        return pairs;
    }

    /// <summary>
    /// Reads the median of a sorted sample, taking the geometric mean of the two central values at
    /// an even count.
    /// </summary>
    /// <remarks>
    /// Geometric rather than arithmetic because these are ratios: the midpoint between twice as slow
    /// and eight times as slow is four times, not five.
    /// </remarks>
    private static double Median(double[] sorted) =>
        sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : Math.Sqrt(sorted[(sorted.Length / 2) - 1] * sorted[sorted.Length / 2]);

    /// <summary>
    /// The order statistic, counted from each end, that bounds the interval.
    /// </summary>
    /// <param name="baselineCount">Readings in the baseline arm.</param>
    /// <param name="currentCount">Readings in the recent arm.</param>
    /// <returns>A rank of at least 1, where 1 means the extreme pairwise ratios themselves.</returns>
    /// <remarks>
    /// The largest <c>k</c> whose tail <c>P(U ≤ k − 1)</c> stays within
    /// <see cref="TailMass"/> under the exact Mann–Whitney null. Against three recent readings it is
    /// 1 at five baseline ones (96.4% achieved coverage), 2 at seven (96.7%), 3 at eight (95.2%),
    /// 4 at ten (95.1%) and 7 at seventeen (95.9%) — seven being the shortest baseline the duration
    /// provider will make a claim from.
    /// <para>
    /// Clamped at 1 rather than reporting no interval. That only binds on arms shorter than any
    /// caller here supplies: fewer than forty arrangements cannot put 2.5% in a tail at all, and
    /// there the returned bounds are the extremes of the pairwise ratios at whatever coverage the
    /// sample can carry.
    /// </para>
    /// </remarks>
    private static int CriticalRank(int baselineCount, int currentCount)
    {
        double[] distribution = MannWhitneyCounts(baselineCount, currentCount);

        double total = 0;
        foreach (double ways in distribution)
            total += ways;

        double tail = 0;
        int rank = 0;

        for (int u = 0; u < distribution.Length; u++)
        {
            tail += distribution[u];
            if (tail / total > TailMass)
                break;

            rank = u + 1;
        }

        return Math.Max(1, rank);
    }

    /// <summary>
    /// Arrangements producing each value of the Mann–Whitney statistic, from 0 upwards.
    /// </summary>
    /// <remarks>
    /// The textbook recurrence <c>f(m, n, u) = f(m − 1, n, u − n) + f(m, n − 1, u)</c>, which counts
    /// the ways <c>u</c> baseline-over-current inversions can arise. Exact integer counts carried as
    /// doubles: the largest this ever reaches is the number of ways to choose three runs from
    /// forty-three, which is five digits.
    /// </remarks>
    private static double[] MannWhitneyCounts(int baselineCount, int currentCount)
    {
        int largest = baselineCount * currentCount;

        // f(m, 0, ·), the plane the recurrence starts from: with no recent readings there are no
        // inversions and exactly one arrangement.
        double[][] previous = new double[baselineCount + 1][];
        for (int m = 0; m <= baselineCount; m++)
        {
            previous[m] = new double[largest + 1];
            previous[m][0] = 1;
        }

        for (int n = 1; n <= currentCount; n++)
        {
            double[][] plane = new double[baselineCount + 1][];
            plane[0] = new double[largest + 1];
            plane[0][0] = 1;

            for (int m = 1; m <= baselineCount; m++)
            {
                plane[m] = new double[largest + 1];

                for (int u = 0; u <= m * n; u++)
                {
                    plane[m][u] =
                        (u >= n ? plane[m - 1][u - n] : 0) +
                        previous[m][u];
                }
            }

            previous = plane;
        }

        return previous[baselineCount];
    }
}
