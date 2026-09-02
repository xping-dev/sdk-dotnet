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
/// The larger of two robust scale estimates, divided by the median and corrected for sample size:
/// <c>c(n) · max(1.4826 · median(|x − median(x)|), (p75 − p25) / 1.349) / median(x)</c>.
/// </para>
/// <para>
/// <b>Robust rather than a coefficient of variation.</b> Durations are right-skewed and one GC pause
/// or one cold JIT dominates both the mean and the sum of squares, so a standard deviation over a
/// mean describes the outlier rather than the test. Against the store this repository records, one
/// anomalous run put the coefficient of variation above 1.5 for every one of nine hundred
/// fingerprints at once, and a test with nineteen steady runs and one five times slower is called
/// unstable by it 99% of the time.
/// </para>
/// <para>
/// <b>Two estimates rather than one, because the median absolute deviation alone is blind to a test
/// with two speeds.</b> It measures how far the typical reading sits from the middle, and when one
/// mode holds a majority of the runs that mode <i>is</i> the middle — so a test that takes 500ms in
/// twelve runs and 3s in eight reads a deviation of nearly zero. That is not a knife-edge at one
/// split: it is every split except an exact tie. Left alone it would put a false-positive channel
/// straight back into the regression gate, which reads a low dispersion as "steady enough to
/// measure against", and it would hide the bimodal test from the instability gate that exists to
/// report it. Bimodal durations are ordinary — a cache hit against a miss, a connection served from
/// the pool or opened, a call that sometimes retries.
/// </para>
/// <para>
/// The interquartile range has no such blind spot: the two quartiles sit in different modes as soon
/// as the minority holds a quarter of the runs. It is the weaker of the two against outright
/// corruption, breaking down at 25% where the median absolute deviation survives 50%, which is why
/// the answer is the larger of them rather than either alone. Where the sample is symmetric and
/// unimodal the two agree — at the normal both estimate the same standard deviation — so the maximum
/// costs nothing there and only speaks up on the shapes the other one cannot see.
/// </para>
/// <para>
/// Rousseeuw and Croux's Qn is the alternative usually reached for and does not help here: its
/// quantile of pairwise distances stays inside the within-mode mass for the same splits, reading
/// 0.04 to 0.11 across every one of them where the interquartile range reads 0.5 to 3.5.
/// </para>
/// <para>
/// <b><see cref="Correction"/> makes it mean the same thing at five readings as at twenty.</b> Both
/// estimates are badly biased low on a handful of points, so a statistic thresholded at a fixed line
/// without this waves small samples through and holds large ones to a stricter bar than the constant
/// states. The gates this feeds sit at five readings.
/// </para>
/// <para>
/// The correction factors are in the spirit of Croux and Rousseeuw (1992), derived by simulation for
/// this exact combination and for the median rather than the mean: these make the estimate exceed
/// the true dispersion half the time, which is the property a threshold needs, where a mean-unbiasing
/// factor makes its average correct instead. Derivation is in <c>RobustDispersionTests</c>, which
/// re-runs it.
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
    /// Not in <see cref="LocalAnalysisConstants"/>, and neither is <see cref="QuartileConsistency"/>.
    /// That class holds the lines measurements are compared against; these are part of the definition
    /// of the measurement, and changing one would not move a threshold but rename the statistic.
    /// </remarks>
    private const double DeviationConsistency = 1.4826;

    /// <summary>
    /// Interquartile range of a standard normal, in standard deviations (2 × 0.6745).
    /// </summary>
    private const double QuartileConsistency = 1.349;

    /// <summary>
    /// Median-unbiasing factors for sample sizes 2 to 40, indexed by count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived at the standard normal over 200,000 samples per size, as the reciprocal of the median
    /// of the uncorrected statistic. Two independent seeds agree to within 0.005 at every size. Past
    /// forty the factor is under 1.005 and <see cref="Correction"/> stops applying one.
    /// </para>
    /// <para>
    /// The sequence falls unevenly rather than smoothly, alternating slightly between odd and even
    /// counts. That is the two estimates trading places: which of them is the larger depends on where
    /// the quartiles land between readings, and that moves with the parity of the count.
    /// </para>
    /// </remarks>
    private static readonly double[] Corrections =
    [
        0,       0,       1.4137,  1.4136,  1.3786,  1.1893,  1.1805,  1.1121,
        1.1222,  1.0700,  1.0778,  1.0540,  1.0610,  1.0346,  1.0437,  1.0324,
        1.0382,  1.0211,  1.0281,  1.0209,  1.0262,  1.0136,  1.0192,  1.0138,
        1.0173,  1.0082,  1.0141,  1.0101,  1.0123,  1.0056,  1.0101,  1.0077,
        1.0097,  1.0033,  1.0069,  1.0047,  1.0074,  1.0016,  1.0051,  1.0032,
        1.0046
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
    /// of variation of 0.20, 0.48 at 0.50 and 0.65 at 0.70. The two are close but not the same
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

        double spread = Math.Max(
            DeviationConsistency * Median(deviations),
            (Quantile(sorted, 0.75) - Quantile(sorted, 0.25)) / QuartileConsistency);

        return Correction(sorted.Length) * spread / median;
    }

    /// <summary>
    /// Reads the median of a sorted sample, averaging the two central values at an even count.
    /// </summary>
    /// <remarks>
    /// Interpolating, unlike the nearest-rank percentile the duration provider publishes. That one
    /// returns an observed value on purpose; this one is never shown to anybody, and reading a
    /// single observed value here would make <see cref="Corrections"/> swing between odd and even
    /// counts — 2.5 at four readings against 1.3 at five, and a statistic identically zero at two.
    /// </remarks>
    private static double Median(double[] sorted) =>
        sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;

    /// <summary>
    /// Reads a quantile of a sorted sample by linear interpolation between the two nearest readings.
    /// </summary>
    /// <remarks>
    /// Interpolating for the same reason as <see cref="Median"/>, and here it matters more: a
    /// quartile read by nearest rank jumps a whole reading as the count crosses a multiple of four,
    /// which put a four-long cycle into the correction factors and left them not converging on one.
    /// </remarks>
    private static double Quantile(double[] sorted, double quantile)
    {
        double position = (sorted.Length - 1) * quantile;
        int lower = (int)Math.Floor(position);
        int upper = Math.Min(lower + 1, sorted.Length - 1);

        return sorted[lower] + ((position - lower) * (sorted[upper] - sorted[lower]));
    }

    /// <summary>
    /// Reads the median-unbiasing factor for a sample of <paramref name="count"/> readings.
    /// </summary>
    /// <remarks>
    /// Past the table the correction is under half a percent and none is applied. A local window
    /// holds twenty runs by default, so counts beyond forty are what a heavily retried test reaches
    /// rather than the common case.
    /// </remarks>
    private static double Correction(int count) =>
        count < Corrections.Length ? Corrections[count] : 1.0;
}
