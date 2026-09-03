/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// How small a p-value has to be to be worth reporting, once the number of tests that produced it
/// is accounted for.
/// </summary>
/// <remarks>
/// <para>
/// The Benjamini-Hochberg step-up procedure. Given the p-values a family of hypothesis tests
/// produced, it returns the largest one that can be called a discovery while holding the expected
/// proportion of false discoveries among everything it accepts to a stated rate.
/// </para>
/// <para>
/// <b>Why a false discovery rate and not a family-wise one.</b> The output is a ranked list a
/// developer reads from the top and stops reading when the entries stop being useful. What such a
/// reader needs bounded is the share of the list that is noise, which is what this bounds.
/// Bonferroni bounds instead the chance that the list contains a single false entry anywhere, and
/// buying that guarantee over three hundred fingerprints costs a factor of three hundred on every
/// threshold — a bar nothing a twenty-run window can show would ever clear. A report that says
/// nothing is as useless as one that says eight wrong things.
/// </para>
/// <para>
/// <b>Independence.</b> The guarantee proved in Benjamini and Hochberg (1995) assumes the tests are
/// independent, and holds under positive regression dependence besides. The families here are one
/// comparison per fingerprint, and fingerprints in a suite are not fully independent — tests
/// sharing a fixture fail together, and a bad afternoon lands in every test's evening arm at once.
/// The Benjamini-Yekutieli correction is the price of dropping the assumption entirely, and it is a
/// factor of <c>ln(m)</c> — about five and a half at three hundred, most of Bonferroni's cost for
/// most of Bonferroni's silence. The dependence here is positive rather than adversarial, which is
/// the case the 1995 bound survives, so the plain procedure is what runs.
/// </para>
/// <para>
/// This says which p-values are worth reporting. It says nothing about which are interesting: a
/// cutoff is a floor under a ranked list and never a ranking, and the impact score remains what
/// orders whatever clears it.
/// </para>
/// </remarks>
internal static class BenjaminiHochberg
{
    /// <summary>
    /// The largest p-value a family admits at a given false discovery rate.
    /// </summary>
    /// <param name="pValues">
    /// The p-values observed, in any order. May be fewer than <paramref name="hypotheses"/>: a
    /// caller that dropped a test's result on some ground unrelated to its p-value simply omits it,
    /// and the omission costs power rather than validity, because every rank the remaining values
    /// take is then at most the rank they would have had.
    /// </param>
    /// <param name="hypotheses">
    /// How many tests the family performed — every fingerprint the question was asked of, including
    /// those whose answer never became a candidate. This is the figure the whole correction turns
    /// on, and it is not the length of <paramref name="pValues"/>: a caller that has already
    /// screened its results at some level and passes only the survivors, while claiming their count
    /// as the family, has described a family in which every member is a discovery and corrected for
    /// nothing.
    /// </param>
    /// <param name="rate">The share of accepted discoveries allowed to be false, in (0,1].</param>
    /// <returns>
    /// The cutoff: every p-value at or below it is a discovery. <see langword="null"/> where none
    /// is, where no p-values were supplied, or where the rate is not a proportion.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A cutoff rather than the accepted subset, because that is what the step-up rule actually
    /// produces and because it settles ties without a rule of its own. Fisher's exact test on small
    /// tables lands on a coarse ladder of attainable values, so two fingerprints holding the same
    /// p-value is the normal case rather than an edge one; accepting "every p at or below this
    /// value" takes them together, where walking a sorted list and cutting at an index would keep
    /// one and drop its twin on the strength of list position.
    /// </para>
    /// <para>
    /// A family smaller than the values in it is treated as being their number. That is a caller
    /// bug rather than an input, and the two ways of answering it are to throw — which costs the
    /// whole report for one provider's miscount — or to fall back on the least the family can
    /// honestly be, which is what was observed. Silently trusting the smaller number is the one
    /// option that is not available: it would make the correction weaker than the data warrants,
    /// which is the direction that invents findings.
    /// </para>
    /// </remarks>
    public static double? Cutoff(IReadOnlyCollection<double> pValues, int hypotheses, double rate)
    {
        ArgumentNullException.ThrowIfNull(pValues);

        if (pValues.Count == 0 || rate <= 0.0 || rate > 1.0)
            return null;

        double[] sorted = [.. pValues.Order()];
        int m = Math.Max(hypotheses, sorted.Length);

        double? cutoff = null;

        // Step-up: the largest rank that clears its own bar carries every smaller p-value with it,
        // including any that failed a bar of their own. Walking upwards and keeping the last
        // acceptance is that rule, and it is why the loop does not stop at the first failure.
        for (int k = 1; k <= sorted.Length; k++)
        {
            if (sorted[k - 1] <= k / (double)m * rate)
                cutoff = sorted[k - 1];
        }

        return cutoff;
    }
}
