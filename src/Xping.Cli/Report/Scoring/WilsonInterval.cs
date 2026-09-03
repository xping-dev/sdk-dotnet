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
    /// <para>
    /// Shared rather than private because the concurrency provider, which has no interval to place
    /// and ranks on an effect discounted by its own trend statistic, discounts against this same
    /// deviate. Two different 95%s in one report would be a difference a reader could not discover.
    /// </para>
    /// </remarks>
    public const double ConfidenceZ = 1.96;

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
    public static double LowerBound(int successes, int trials, double z = ConfidenceZ) =>
        LowerBound((double)successes, trials, z);

    /// <summary>
    /// Lower bound of the Wilson score interval on an effective sample size.
    /// </summary>
    /// <param name="successes">Observations of the behaviour, deflated for clustering.</param>
    /// <param name="trials">Opportunities to observe it, deflated for clustering.</param>
    /// <param name="z">Standard normal deviate; defaults to a two-sided 95% interval.</param>
    /// <returns>The bound, in [0,1]; 0 when there were no trials.</returns>
    /// <remarks>
    /// Fractional rather than whole counts, because a caller whose observations are correlated has to
    /// divide both by a design effect and the result is not an integer. Rounding them back would put
    /// an error of up to half an observation into a denominator that is often only five, which is
    /// large enough to reorder findings and to do it non-monotonically. The formula never needed the
    /// counts to be whole.
    /// </remarks>
    public static double LowerBound(double successes, double trials, double z = ConfidenceZ)
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
    /// <returns>The bound, in [0,1]; 1 when there were no trials.</returns>
    /// <remarks>
    /// <para>
    /// Exists to feed <see cref="DifferenceBoundNearestZero(int, int, int, int)"/>. Nothing thresholds on it: an upper
    /// bound is the most the data could support, and a report that ranked on it would promote the
    /// findings with the least evidence.
    /// </para>
    /// <para>
    /// The empty case is 1 rather than 0, which is the opposite of <see cref="LowerBound(int, int, double)"/> and the
    /// only value that is not a claim. Having observed nothing, the least the proportion can be is
    /// zero and the most it can be is one; returning 0 here would assert that a behaviour never
    /// observed also cannot happen.
    /// </para>
    /// </remarks>
    public static double UpperBound(int successes, int trials, double z = ConfidenceZ) =>
        UpperBound((double)successes, trials, z);

    /// <summary>
    /// Upper bound of the Wilson score interval on an effective sample size.
    /// </summary>
    /// <param name="successes">Observations of the behaviour, deflated for clustering.</param>
    /// <param name="trials">Opportunities to observe it, deflated for clustering.</param>
    /// <param name="z">Standard normal deviate; defaults to a two-sided 95% interval.</param>
    /// <returns>The bound, in [0,1]; 1 when there were no trials.</returns>
    public static double UpperBound(double successes, double trials, double z = ConfidenceZ)
    {
        if (trials <= 0)
            return 1;

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
    /// pooled normal approximation, for the same reason <see cref="LowerBound(int, int, double)"/> is Wilson: the arms
    /// are small and their rates sit near the ends.
    /// </para>
    /// <para>
    /// Reduced to the endpoint nearest zero because that is the difference the evidence actually
    /// supports — the least the gap can be while still agreeing with what was observed. An interval
    /// straddling zero supports no difference at all and collapses to 0, which is what a five-a-side
    /// split that happened to break unevenly deserves.
    /// </para>
    /// <para>
    /// Returned as a magnitude. The caller thresholds and ranks on an absolute gap and has already
    /// resolved which arm is the worse one; a signed bound would only be re-absolved at the call
    /// site.
    /// </para>
    /// </remarks>
    public static double DifferenceBoundNearestZero(
        int successes, int trials, int otherSuccesses, int otherTrials) =>
        DifferenceBoundNearestZero((double)successes, trials, otherSuccesses, otherTrials);

    /// <summary>
    /// How large a difference between two proportions the data supports, on effective sample sizes.
    /// </summary>
    /// <param name="successes">Observations in the first arm, deflated for clustering.</param>
    /// <param name="trials">Effective size of the first arm.</param>
    /// <param name="otherSuccesses">Observations in the second arm, deflated for clustering.</param>
    /// <param name="otherTrials">Effective size of the second arm.</param>
    /// <returns>The interval endpoint nearest zero, as a non-negative magnitude.</returns>
    /// <remarks>
    /// The overload a caller reaches for when its arms hold correlated observations. Deflating both
    /// counts by the design effect leaves the point estimate exactly where it was and widens the
    /// interval to what the number of independent units supports, which is the whole intent: the rate
    /// stays as observed, the confidence stops outrunning the evidence.
    /// </remarks>
    public static double DifferenceBoundNearestZero(
        double successes, double trials, double otherSuccesses, double otherTrials)
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
    private static double Proportion(double successes, double trials) =>
        Math.Clamp(successes / trials, 0.0, 1.0);

    private static double Square(double value) => value * value;
}
