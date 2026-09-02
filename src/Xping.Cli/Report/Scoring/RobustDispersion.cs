/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// How much a set of measurements varies, relative to its own middle, without one reading owning
/// the answer.
/// </summary>
/// <remarks>
/// <para>
/// The scaled median absolute deviation over the median:
/// <c>c(n) · 1.4826 · median(|x − median(x)|) / median(x)</c>.
/// </para>
/// <para>
/// <b>Robust rather than a coefficient of variation.</b> Durations are right-skewed and one GC pause
/// or one cold JIT dominates both the mean and the sum of squares, so a standard deviation over a
/// mean describes the outlier rather than the test. The median absolute deviation has a 50% breakdown
/// point against a coefficient of variation's 0%: half the readings would have to move before it
/// does. Against the store this repository records, one anomalous run put the coefficient of
/// variation above 1.5 for every one of nine hundred fingerprints at once, and a test with nineteen
/// steady runs and one five times slower is called unstable by it 99% of the time.
/// </para>
/// <para>
/// <b>1.4826 makes it a standard deviation.</b> At the normal the median absolute deviation is
/// 0.6745σ, so the reciprocal puts the statistic on the scale a reader already has intuitions for.
/// </para>
/// <para>
/// <b><see cref="Correction"/> makes it mean the same thing at five readings as at twenty.</b> This
/// is the half that the naive formula gets wrong and the reason this class exists rather than four
/// lines inside a provider. A median absolute deviation read off a handful of points is badly biased
/// low — at five its median is a quarter under the value it converges to — so an uncorrected
/// statistic thresholded at a fixed line waves through small samples and holds large ones to a
/// stricter bar than the constant states. The gates this feeds sit at five readings.
/// </para>
/// <para>
/// The correction factors are the finite-sample constants of Croux and Rousseeuw (1992), re-derived
/// for this exact implementation and for the median rather than the mean: these make the estimate
/// exceed the true dispersion half the time, which is the property a threshold needs, where theirs
/// make its average correct. Derivation is in <c>RobustDispersionTests</c>, which re-runs it.
/// </para>
/// <para>
/// This measures spread. It says nothing about whether two sets of measurements differ, and a caller
/// wanting that question answered needs a two-sample test rather than a comparison of two of these.
/// </para>
/// </remarks>
internal static class RobustDispersion
{
    /// <summary>
    /// Reciprocal of the median absolute deviation of a standard normal (1 / 0.6745).
    /// </summary>
    /// <remarks>
    /// Not in <see cref="LocalAnalysisConstants"/>. That class holds the lines measurements are
    /// compared against; this is part of the definition of the measurement, and changing it would
    /// not move a threshold but rename the statistic.
    /// </remarks>
    private const double NormalConsistency = 1.4826;

    /// <summary>
    /// Median-unbiasing factors for sample sizes 2 to 20, indexed by count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived at the standard normal over 200,000 samples per size, as the reciprocal of the median
    /// of <c>1.4826 · median(|x − median(x)|)</c>. Two independent seeds agree to within 0.005 at
    /// every size. Beyond twenty the sequence is smooth and <see cref="Correction"/> continues it in
    /// closed form.
    /// </para>
    /// <para>
    /// Three is the largest correction rather than two, which looks wrong and is not. At an odd
    /// count the deviation set contains an exact zero — the middle reading's distance from itself —
    /// and at three that zero is one of only two values below the median deviation, so the statistic
    /// reads the smaller of the two remaining gaps. At an even count the two central deviations are
    /// averaged and the zero is diluted.
    /// </para>
    /// </remarks>
    private static readonly double[] Corrections =
    [
        0,       0,       1.4137,  1.8722,  1.4898,  1.3395,  1.2632,
        1.2069,  1.1775,  1.1512,  1.1350,  1.1172,  1.1060,  1.0962,
        1.0901,  1.0808,  1.0771,  1.0704,  1.0667,  1.0622,  1.0602
    ];

    /// <summary>
    /// Measures how much <paramref name="values"/> vary relative to their own median.
    /// </summary>
    /// <param name="values">The readings, in any order.</param>
    /// <returns>
    /// The dispersion, or zero when it cannot be computed — fewer than two readings, or a median
    /// that is not positive. Zero is the conservative answer in both directions: it passes a
    /// stability gate and fails an instability gate, so absent data never produces a finding on its
    /// own.
    /// </returns>
    /// <remarks>
    /// A lognormal sample, which is the shape test durations take, reads 0.20 at a true coefficient
    /// of variation of 0.20, 0.46 at 0.50 and 0.60 at 0.70. The two are close but not the same
    /// number, and thresholds carried across from one to the other would not mean what they said.
    /// </remarks>
    public static double Of(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count < 2)
            return 0;

        double[] sorted = [.. values];
        Array.Sort(sorted);

        double median = Median(sorted);
        if (median <= 0)
            return 0;

        var deviations = new double[sorted.Length];
        for (int i = 0; i < sorted.Length; i++)
            deviations[i] = Math.Abs(sorted[i] - median);

        Array.Sort(deviations);

        return Correction(sorted.Length) * NormalConsistency * Median(deviations) / median;
    }

    /// <summary>
    /// Reads the median of a sorted sample, averaging the two central values at an even count.
    /// </summary>
    /// <remarks>
    /// Interpolating, unlike the nearest-rank percentile the duration provider publishes. That one
    /// returns an observed value on purpose; this one is never shown to anybody, and taking the
    /// lower of two central values here would make <see cref="Corrections"/> swing between odd and
    /// even counts — 2.5 at four readings against 1.3 at five, and a statistic identically zero at
    /// two — where averaging leaves it smooth and monotone.
    /// </remarks>
    private static double Median(double[] sorted) =>
        sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;

    /// <summary>
    /// Reads the median-unbiasing factor for a sample of <paramref name="count"/> readings.
    /// </summary>
    /// <remarks>
    /// <c>1 + 1.2 / n</c> past the table, which is within 0.002 of the simulated value at every size
    /// from fifteen upwards and joins the table continuously at twenty. A local window holds twenty
    /// runs by default, so the closed form is what a retried test or a longer window reaches rather
    /// than the common case.
    /// </remarks>
    private static double Correction(int count) =>
        count < Corrections.Length ? Corrections[count] : 1 + (1.2 / count);
}
