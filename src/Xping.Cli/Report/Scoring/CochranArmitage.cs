/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// A trend statistic and how improbable it is.
/// </summary>
/// <param name="Z">
/// The standardised trend, signed: positive where the behaviour grows more common as the level
/// rises, negative where it grows rarer.
/// </param>
/// <param name="PValue">Two-sided probability of a trend at least this strong under no trend.</param>
internal readonly record struct TrendStatistic(double Z, double PValue);

/// <summary>
/// Whether a binary outcome moves with an ordered exposure, or falls across its levels the way
/// chance would.
/// </summary>
/// <remarks>
/// <para>
/// The Cochran–Armitage test for trend. It is the standard test for exactly this shape — a binary
/// outcome against an exposure that has an order — and its advantage over dichotomising the exposure
/// and running a two-arm comparison is twofold: it needs no split point, so it has something to say
/// about an exposure concentrated on one value, and it reads the ordering, so a rise spread evenly
/// across a dozen levels is the strongest thing it can see rather than the weakest.
/// </para>
/// <para>
/// <b>Levels are scored by value, not by rank.</b> The statistic is a covariance between the level
/// and the outcome, so a suite that ran a test at 1 and at 8 gets credit for the width of that gap
/// where one that ran it at 7 and 8 does not. That is the right reading for a concurrency axis, where
/// the numbers are counts of things and the distance between them means something. It is also what
/// separates this from <see cref="KendallTau"/>, which is deliberately rank-only and is reported
/// beside it as the effect size.
/// </para>
/// <para>
/// <b>The variance is taken over clusters, and never below the model's.</b> Observations sharing a
/// <see cref="TrendPoint.Cluster"/> are correlated, and the textbook variance
/// <c>p̄(1−p̄) Σ nᵢ(xᵢ − x̄)²</c> assumes they are not — which would let a caller buy significance by
/// repeating one occasion. Since the statistic decomposes exactly into a sum over clusters, the
/// sandwich estimator <c>Σ gₛ²</c> is available for nothing, and it charges a repeated occasion for
/// the repetition. What it cannot do is stand alone: at ten or twenty clusters that sum lands below
/// the model variance often enough to invent findings of its own, so the two are combined by taking
/// the larger. Choosing the conservative end rather than estimating which is right is the same move
/// the rest of this pipeline makes wherever a correction would be noisier than the quantity it
/// corrects.
/// </para>
/// <para>
/// <b>Continuity-corrected.</b> Conditional on the level counts and the total number of occurrences,
/// the statistic moves on a lattice whose step is the greatest common divisor of the gaps between
/// levels, and referring a lattice-valued statistic to a continuous distribution without the
/// half-step correction is anti-conservative. The correction scales itself to the data in the way
/// that is wanted: on a suite pinned at one level with occasional serial runs the step is the whole
/// distance between them and the correction bites hard, which is exactly where the normal
/// approximation is least trustworthy; across levels one apart it is a half and vanishes into the
/// noise.
/// </para>
/// <para>
/// <b>Normal rather than exact.</b> The exact conditional null of <c>Σ rᵢ xᵢ</c> is reachable by
/// dynamic programming over (occurrences placed, weighted sum), and was rejected on two counts. It
/// costs on the order of fourteen million operations per subject on a default window and grows
/// roughly with the cube of it, against a <c>--runs</c> that has no upper bound — seconds added to a
/// command that finishes in well under one. More decisively, it would be exact for the wrong null:
/// conditioning that way assumes the individual observations are exchangeable, which is precisely
/// what the clustering above says they are not. Sharpening a null that does not hold is not an
/// improvement. A permutation over clusters would hold, but it is Monte Carlo, and a report that must
/// be byte-identical between runs would have to carry a seed that silently decides findings.
/// </para>
/// <para>
/// This says whether the outcome moves with the level. It says nothing about how far, and a caller
/// wanting that needs <see cref="KendallTau"/>.
/// </para>
/// </remarks>
internal static class CochranArmitage
{
    /// <summary>What every degenerate input answers: no trend, and nothing improbable about it.</summary>
    private static readonly TrendStatistic Nothing = new(0.0, 1.0);

    /// <summary>
    /// Tests whether <paramref name="points"/> show a trend across their levels.
    /// </summary>
    /// <param name="points">The observations, in any order.</param>
    /// <returns>
    /// The signed standardised trend and its two-sided probability. <see cref="Nothing"/> — a Z of
    /// zero and a probability of one — wherever the question cannot be asked: fewer than two
    /// observations, fewer than two distinct levels, fewer than two clusters, or an outcome that
    /// happened every time or never. None of those are errors; they are the data answering.
    /// </returns>
    /// <remarks>
    /// Deterministic in the face of any input order. Both sums that carry rounding — the statistic
    /// itself and the sandwich variance — are accumulated over clusters in ascending cluster order
    /// rather than in the order the observations arrived, so two runs over one window agree bit for
    /// bit.
    /// </remarks>
    public static TrendStatistic Of(IReadOnlyList<TrendPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
            return Nothing;

        var levels = new SortedDictionary<int, int>();
        long levelTotal = 0;
        int occurrences = 0;

        foreach (TrendPoint point in points)
        {
            levels[point.Level] = levels.GetValueOrDefault(point.Level) + 1;
            levelTotal += point.Level;

            if (point.Occurred)
                occurrences++;
        }

        // An outcome that never happened, or one that always did, has no covariance with anything:
        // the model variance below is exactly zero and the statistic is undefined rather than small.
        if (levels.Count < 2 || occurrences == 0 || occurrences == points.Count)
            return Nothing;

        double mean = (double)levelTotal / points.Count;
        double rate = (double)occurrences / points.Count;

        double spread = 0;
        foreach ((int level, int trials) in levels)
            spread += trials * Square(level - mean);

        double model = rate * (1 - rate) * spread;

        if (model <= 0)
            return Nothing;

        var clusters = new SortedDictionary<int, double>();

        foreach (TrendPoint point in points)
        {
            double contribution = (point.Level - mean) * ((point.Occurred ? 1.0 : 0.0) - rate);
            clusters[point.Cluster] = clusters.GetValueOrDefault(point.Cluster) + contribution;
        }

        // One cluster is one occasion, and one occasion cannot tell a trend from an afternoon.
        if (clusters.Count < 2)
            return Nothing;

        double statistic = 0;
        double squares = 0;

        foreach (double contribution in clusters.Values)
        {
            statistic += contribution;
            squares += contribution * contribution;
        }

        // The finite-sample scaling of the sandwich estimator, for the same reason a sample variance
        // divides by n-1: the cluster contributions are measured against a mean estimated from them.
        double clustered = squares * clusters.Count / (clusters.Count - 1.0);
        double variance = Math.Max(model, clustered);

        double corrected = Math.Max(0, Math.Abs(statistic) - (Step(levels.Keys) / 2.0));
        double z = Math.Sign(statistic) * corrected / Math.Sqrt(variance);

        return new TrendStatistic(z, NormalTail.TwoSidedPValue(z));
    }

    /// <summary>
    /// The smallest amount the statistic can move.
    /// </summary>
    /// <param name="levels">The distinct levels observed, ascending.</param>
    /// <returns>The greatest common divisor of the gaps between them.</returns>
    /// <remarks>
    /// Holding the level counts and the total number of occurrences fixed, the only thing that can
    /// vary is which observations carried the outcome, and moving one occurrence from a level to
    /// another shifts the statistic by the distance between them. The lattice those shifts generate
    /// is therefore spaced by the greatest common divisor of every pairwise gap — which is the
    /// greatest common divisor of the consecutive gaps, and takes one pass to read.
    /// </remarks>
    private static int Step(IEnumerable<int> levels)
    {
        int step = 0;
        int? previous = null;

        foreach (int level in levels)
        {
            if (previous is { } earlier)
                step = GreatestCommonDivisor(step, level - earlier);

            previous = level;
        }

        return Math.Max(step, 1);
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
            (left, right) = (right, left % right);

        return Math.Abs(left);
    }

    private static double Square(double value) => value * value;
}
