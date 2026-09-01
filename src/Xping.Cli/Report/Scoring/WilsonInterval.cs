/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// Confidence bounds on the rates local analysis thresholds and ranks on.
/// </summary>
/// <remarks>
/// <para>
/// Every rate a provider computes is a ratio of two small counts, and a point estimate says nothing
/// about how many observations are behind it: <c>2/2</c>, <c>5/5</c> and <c>50/50</c> are all 1.00.
/// Thresholding and ranking on the raw ratio therefore put the findings resting on the least data at
/// the top of the report, which is the opposite of what a reader needs. A lower bound is the same
/// number as the sample grows and a smaller one when it does not, so a claim only gets made when the
/// evidence carries it.
/// </para>
/// <para>
/// Wilson rather than Wald. The normal approximation has zero width at <c>p̂ = 0</c> and
/// <c>p̂ = 1</c> — it would report <c>2 of 2</c> as a bound of exactly 1.00 — and those two ends are
/// precisely where these thresholds sit. Wilson is derived by inverting the score test instead, so
/// it stays inside [0,1] and keeps a sensible width at both extremes.
/// </para>
/// <para>
/// This bounds a rate; it does not correct for how many rates were tested. A window with three
/// hundred tests in it performs three hundred of these comparisons per provider, and nothing here
/// charges for that.
/// </para>
/// </remarks>
internal static class WilsonInterval
{
    /// <summary>
    /// Standard normal deviate for a two-sided 95% interval (1.96).
    /// </summary>
    /// <remarks>
    /// Not in <see cref="LocalAnalysisConstants"/>: that class holds the lines measurements are
    /// compared against, and this is the confidence the comparison is made at. 95% is the convention
    /// a reader will assume when the report says a rate is "at least" something.
    /// </remarks>
    private const double ConfidenceZ = 1.96;

    /// <summary>
    /// Lower bound of the Wilson score interval for <paramref name="successes"/> in
    /// <paramref name="trials"/>.
    /// </summary>
    /// <param name="successes">Observations of the behaviour.</param>
    /// <param name="trials">Opportunities to observe it.</param>
    /// <param name="z">Standard normal deviate; defaults to a two-sided 95% interval.</param>
    /// <returns>The bound, in [0,1]; 0 when there were no trials.</returns>
    /// <remarks>
    /// The quantity to threshold and to rank on. <c>2/2</c> bounds at 0.34 and <c>19/20</c> at 0.76,
    /// so the second outranks the first despite the first's higher point estimate.
    /// </remarks>
    public static double LowerBound(int successes, int trials, double z = ConfidenceZ)
    {
        if (trials <= 0)
            return 0;

        double p = Proportion(successes, trials);
        double z2 = z * z;
        double centre = p + (z2 / (2.0 * trials));
        double margin = z * Math.Sqrt((p * (1 - p) / trials) + (z2 / (4.0 * trials * trials)));

        return Math.Clamp((centre - margin) / (1 + (z2 / trials)), 0.0, 1.0);
    }

    /// <summary>
    /// Upper bound of the Wilson score interval for <paramref name="successes"/> in
    /// <paramref name="trials"/>.
    /// </summary>
    /// <param name="successes">Observations of the behaviour.</param>
    /// <param name="trials">Opportunities to observe it.</param>
    /// <param name="z">Standard normal deviate; defaults to a two-sided 95% interval.</param>
    /// <returns>The bound, in [0,1]; 0 when there were no trials.</returns>
    /// <remarks>
    /// Exists to feed <see cref="DifferenceBoundNearestZero"/>. Nothing thresholds on it: an upper
    /// bound is the most the data could support, and a report that ranked on it would promote the
    /// findings with the least evidence.
    /// </remarks>
    public static double UpperBound(int successes, int trials, double z = ConfidenceZ)
    {
        if (trials <= 0)
            return 0;

        double p = Proportion(successes, trials);
        double z2 = z * z;
        double centre = p + (z2 / (2.0 * trials));
        double margin = z * Math.Sqrt((p * (1 - p) / trials) + (z2 / (4.0 * trials * trials)));

        return Math.Clamp((centre + margin) / (1 + (z2 / trials)), 0.0, 1.0);
    }

    /// <summary>
    /// How large a difference between two proportions the data supports, as a magnitude.
    /// </summary>
    /// <param name="successes">Observations in the first arm.</param>
    /// <param name="trials">Size of the first arm.</param>
    /// <param name="otherSuccesses">Observations in the second arm.</param>
    /// <param name="otherTrials">Size of the second arm.</param>
    /// <returns>
    /// The interval endpoint nearest zero, as a non-negative magnitude; 0 when the interval contains
    /// zero, or when either arm is empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Newcombe's hybrid score interval, built from the two arms' Wilson intervals rather than from a
    /// pooled normal approximation, for the same reason <see cref="LowerBound"/> is Wilson: the arms
    /// are small and their rates sit near the ends.
    /// </para>
    /// <para>
    /// Reduced to the endpoint nearest zero because that is the difference the evidence actually
    /// supports — the least the gap can be while still agreeing with what was observed. An interval
    /// straddling zero supports no difference at all and collapses to 0, which is what a five-a-side
    /// split that happened to break unevenly deserves.
    /// </para>
    /// <para>
    /// Returned as a magnitude. Both callers threshold and rank on an absolute gap and have already
    /// resolved which arm is the worse one; a signed bound would only be re-absolved at the call
    /// site.
    /// </para>
    /// </remarks>
    public static double DifferenceBoundNearestZero(
        int successes, int trials, int otherSuccesses, int otherTrials)
    {
        if (trials <= 0 || otherTrials <= 0)
            return 0;

        double p1 = Proportion(successes, trials);
        double p2 = Proportion(otherSuccesses, otherTrials);

        double l1 = LowerBound(successes, trials);
        double u1 = UpperBound(successes, trials);
        double l2 = LowerBound(otherSuccesses, otherTrials);
        double u2 = UpperBound(otherSuccesses, otherTrials);

        double difference = p1 - p2;
        double lower = difference - Math.Sqrt(Square(p1 - l1) + Square(u2 - p2));
        double upper = difference + Math.Sqrt(Square(u1 - p1) + Square(p2 - l2));

        if (lower <= 0 && upper >= 0)
            return 0;

        return Math.Min(Math.Abs(lower), Math.Abs(upper));
    }

    /// <summary>
    /// Divides one count by another, keeping the result a proportion.
    /// </summary>
    /// <remarks>
    /// The clamp is a guard, not an expectation: every caller today counts a subset of its own
    /// denominator. Without it a count that exceeded its denominator would make <c>p * (1 - p)</c>
    /// negative and the square root NaN, and NaN survives <see cref="Math.Clamp(double, double,
    /// double)"/> and every comparison in <see cref="ImpactScorer"/>, so the finding would silently
    /// score zero rather than fail. A statistic that cannot be computed should be visibly wrong or
    /// safely bounded, and bounded is the cheaper of the two here.
    /// </remarks>
    private static double Proportion(int successes, int trials) =>
        Math.Clamp((double)successes / trials, 0.0, 1.0);

    private static double Square(double value) => value * value;
}
