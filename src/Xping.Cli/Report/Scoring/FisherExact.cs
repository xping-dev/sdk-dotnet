/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// Whether two arms failed at different rates, or divided the same failures the way chance would.
/// </summary>
/// <remarks>
/// <para>
/// Fisher's exact test on the 2×2 table of successes and trials — the same table
/// <see cref="WilsonInterval.DifferenceBoundNearestZero(int, int, int, int)"/> places an interval
/// around. The interval says how large a gap the evidence supports; this says whether there is a gap
/// at all. A caller thresholding a split needs both, and they answer different questions: an arm
/// pair can support a gap of 0.2 and still be the commonest thing chance produces.
/// </para>
/// <para>
/// <b>Exact rather than chi-squared.</b> The arms this serves are five to twenty observations a
/// side, with rates that sit at the ends — a quarter of the day in which a test failed every time
/// against one in which it never did. The chi-squared approximation is calibrated for neither, and
/// its error at these sizes is in the direction that invents findings.
/// </para>
/// <para>
/// <b>Conditioning on both margins is what "exact" means here, and what it costs.</b> The null
/// distribution is hypergeometric because both the arm sizes and the total number of failures are
/// held at what was observed. That makes the level a guarantee rather than an approximation, at the
/// price of some conservatism: the attainable p-values are a coarse ladder on small tables, and the
/// test can only ever land on one of its rungs. Barnard's unconditional test is more powerful and
/// has no closed form; the conservatism is the direction to err in for a report that a developer is
/// meant to trust.
/// </para>
/// <para>
/// <b>Two-sided unless the direction was fixed before the table was.</b> Most callers discover the
/// direction from the data rather than pre-registering it — a test that fails only at night and one
/// that fails only when it does not are each a finding — and a one-sided p taken after looking at
/// which arm was worse is half the p the comparison earned. <c>Vanished</c> is the exception and
/// never has to look: it forms a table only for fingerprints already known to be absent from the
/// current slice, and no finding exists for a test that started running, so the alternative is
/// one-sided before a single count is made.
/// </para>
/// <para>
/// This says whether the two differ. It says nothing about by how much, and a caller wanting that
/// needs <see cref="WilsonInterval"/>, whose bound is the gap the same two arms support.
/// </para>
/// </remarks>
internal static class FisherExact
{
    /// <summary>
    /// Relative tolerance for calling two tables equally probable.
    /// </summary>
    /// <remarks>
    /// The two-sided p sums every table no more probable than the observed one, so a table that ties
    /// with it must be counted. Ties are the normal case rather than an edge one: a table and its
    /// mirror image have equal probability whenever the margins are symmetric, and the two are
    /// computed by different sequences of multiplications that agree in exact arithmetic and can
    /// differ in the last bit. A bare <c>&lt;=</c> would then include or drop the opposite tail on
    /// the strength of rounding, which on a small table moves the p-value by a factor of two.
    /// </remarks>
    private const double TieTolerance = 1e-9;

    /// <summary>
    /// Probability of a division of the failures at least as uneven as the one observed.
    /// </summary>
    /// <param name="successes">Observations of the behaviour in the first arm.</param>
    /// <param name="trials">Size of the first arm.</param>
    /// <param name="otherSuccesses">Observations of the behaviour in the second arm.</param>
    /// <param name="otherTrials">Size of the second arm.</param>
    /// <returns>
    /// The two-sided p-value, in (0,1]; 1 where either arm is empty, where every observation fell
    /// the same way, or where nothing tells the two arms apart. The observed table is always counted,
    /// so the answer is its own probability at the least and cannot be zero for any window a store
    /// holds. It underflows to zero only where that probability is below the smallest positive
    /// double, which a perfectly separated split needs about a thousand runs a side to reach — and a
    /// claim resting on two thousand runs is one nothing downstream needs to tell from certainty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Both arms are counted in whatever unit is independent for the caller. Handing over correlated
    /// observations — several attempts of one test within one run — would understate the p-value by
    /// claiming more denominators than the data holds.
    /// </para>
    /// <para>
    /// Degenerate inputs claim nothing rather than throwing. An empty arm, a table with no failures
    /// at all, and a table with no successes at all each have exactly one arrangement consistent
    /// with their margins, so the observed one is certain and the p-value is 1.
    /// </para>
    /// </remarks>
    public static double TwoSidedPValue(
        int successes, int trials, int otherSuccesses, int otherTrials)
    {
        if (trials <= 0 || otherTrials <= 0)
            return 1.0;

        successes = Math.Clamp(successes, 0, trials);
        otherSuccesses = Math.Clamp(otherSuccesses, 0, otherTrials);

        // One row order for both ways of naming the same table. The answer is the same either way in
        // exact arithmetic, but the two orders enumerate the same tables by different sequences of
        // multiplications and can differ in the last bit. A caller that compares p-values exactly to
        // choose between two descriptions of one division — which is what TimeSensitiveProvider's
        // tie-break does — would then decide on rounding.
        if (otherTrials < trials || (otherTrials == trials && otherSuccesses < successes))
        {
            (successes, trials, otherSuccesses, otherTrials) =
                (otherSuccesses, otherTrials, successes, trials);
        }

        int observedFailures = successes + otherSuccesses;
        int total = trials + otherTrials;

        // Both margins are held at what was observed, so a table with none of the behaviour or all
        // of it has one arrangement and the observed one is it.
        if (observedFailures == 0 || observedFailures == total)
            return 1.0;

        // How many of the behaviour's occurrences could have landed in the first arm, given that
        // neither arm can hold more than its size and that the second arm has to hold the rest.
        int lowest = Math.Max(0, observedFailures - otherTrials);
        int highest = Math.Min(trials, observedFailures);

        double[] weights = Weights(lowest, highest, trials, otherTrials, observedFailures);

        double totalWeight = 0;
        foreach (double weight in weights)
            totalWeight += weight;

        double observed = weights[successes - lowest];
        double extreme = 0;

        foreach (double weight in weights)
        {
            if (weight <= observed * (1 + TieTolerance))
                extreme += weight;
        }

        return Math.Clamp(extreme / totalWeight, 0.0, 1.0);
    }

    /// <summary>
    /// Probability that the first arm would hold at least as many occurrences as it did.
    /// </summary>
    /// <param name="successes">Observations of the behaviour in the first arm.</param>
    /// <param name="trials">Size of the first arm.</param>
    /// <param name="otherSuccesses">Observations of the behaviour in the second arm.</param>
    /// <param name="otherTrials">Size of the second arm.</param>
    /// <returns>
    /// The one-sided p-value, in (0,1]; 1 on the same degenerate tables as
    /// <see cref="TwoSidedPValue(int, int, int, int)"/>. The observed table is always counted, so
    /// this is at least its own probability.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Only for a caller whose direction was decided before the counts were: this returns the upper
    /// tail on the first arm, and choosing which arm to name first after seeing which one was
    /// heavier halves a p-value the comparison did not earn. Everything the class remark says about
    /// exactness, conditioning and the coarseness of the attainable ladder applies here unchanged.
    /// </para>
    /// <para>
    /// The arms are not reordered, unlike in the two-sided test, where the swap exists to make two
    /// namings of one table agree in their last bits. Here the naming is the hypothesis, so a swap
    /// would answer a different question.
    /// </para>
    /// </remarks>
    public static double OneSidedPValue(
        int successes, int trials, int otherSuccesses, int otherTrials)
    {
        if (trials <= 0 || otherTrials <= 0)
            return 1.0;

        successes = Math.Clamp(successes, 0, trials);
        otherSuccesses = Math.Clamp(otherSuccesses, 0, otherTrials);

        int occurrences = successes + otherSuccesses;
        int total = trials + otherTrials;

        // Both margins are held at what was observed, so a table with none of the behaviour or all
        // of it has one arrangement and the observed one is it.
        if (occurrences == 0 || occurrences == total)
            return 1.0;

        int lowest = Math.Max(0, occurrences - otherTrials);
        int highest = Math.Min(trials, occurrences);

        double[] weights = Weights(lowest, highest, trials, otherTrials, occurrences);

        double totalWeight = 0;
        foreach (double weight in weights)
            totalWeight += weight;

        double extreme = 0;
        for (int k = successes - lowest; k < weights.Length; k++)
            extreme += weights[k];

        return Math.Clamp(extreme / totalWeight, 0.0, 1.0);
    }

    /// <summary>
    /// Relative probability of each table the observed margins permit.
    /// </summary>
    /// <param name="lowest">Fewest occurrences the first arm could have held.</param>
    /// <param name="highest">Most it could have held.</param>
    /// <param name="trials">Size of the first arm.</param>
    /// <param name="otherTrials">Size of the second arm.</param>
    /// <param name="occurrences">Occurrences across both arms.</param>
    /// <returns>
    /// Weights proportional to the hypergeometric probabilities, the largest of them exactly 1.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Built from the ratio between neighbouring tables rather than from the hypergeometric formula,
    /// so no binomial coefficient is ever formed:
    /// <c>w(k+1) / w(k) = ((n₁ − k) / (k + 1)) × ((r − k) / (n₂ − r + k + 1))</c>.
    /// </para>
    /// <para>
    /// Accumulated as logarithms and rescaled so the largest weight is one. The direct product
    /// overflows: <c>--runs</c> has no upper bound, and the central coefficient of a window of a
    /// thousand runs is around <c>10³⁰⁰</c>, which is inside a double only until the two coefficients
    /// a hypergeometric probability multiplies are formed. Working in logs and subtracting the
    /// maximum before exponentiating cannot overflow at any window size, and the caller only ever
    /// compares these weights against each other and against their sum.
    /// </para>
    /// </remarks>
    private static double[] Weights(
        int lowest, int highest, int trials, int otherTrials, int occurrences)
    {
        double[] logs = new double[highest - lowest + 1];
        double running = 0;
        double largest = 0;

        for (int k = lowest; k < highest; k++)
        {
            running +=
                Math.Log(trials - k) - Math.Log(k + 1) +
                Math.Log(occurrences - k) - Math.Log(otherTrials - occurrences + k + 1);

            logs[k - lowest + 1] = running;

            if (running > largest)
                largest = running;
        }

        double[] weights = new double[logs.Length];
        for (int i = 0; i < weights.Length; i++)
            weights[i] = Math.Exp(logs[i] - largest);

        return weights;
    }
}
