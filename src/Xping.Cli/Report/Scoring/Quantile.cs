/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// Reads a quantile of a sorted sample, under whichever of the two definitions the quantity calls
/// for.
/// </summary>
/// <remarks>
/// <para>
/// The definition is a property of the thing being measured, not of the caller. A duration is
/// continuous: it can take any value between two readings, and the midpoint between 10ms and 20ms is
/// a duration a run could have taken. An attempt count and a concurrency level are not: there is no
/// run that made one and a half attempts, and a split point of 3.5 concurrent tests is a number no
/// scheduler ever produced.
/// </para>
/// <para>
/// So continuous quantities get <see cref="Interpolated"/> and discrete ones get
/// <see cref="NearestRank"/>, and the two live beside each other here so that the choice has to be
/// made deliberately rather than inherited from whatever the neighbouring provider happened to do.
/// </para>
/// <para>
/// Both definitions require an already-sorted sample. Sorting inside would hide an allocation and a
/// sort in what reads as an accessor — the duration provider reads three quantiles off one list —
/// and every caller here has a list it sorted for other reasons anyway.
/// </para>
/// </remarks>
internal static class Quantile
{
    /// <summary>
    /// Reads a quantile by linear interpolation between the two nearest ranks — the "R-7" definition
    /// that <c>PERCENTILE</c> and NumPy's default both use.
    /// </summary>
    /// <param name="sorted">Readings in ascending order.</param>
    /// <param name="quantile">The quantile to read, in [0,1].</param>
    /// <returns>The interpolated reading, or zero when there are none.</returns>
    /// <remarks>
    /// <para>
    /// For continuous quantities. Nearest rank returns the <i>lower</i> of the two central readings
    /// at every even count — index 1 of four, index 2 of six, index 4 of ten — so a median read that
    /// way is biased low on half of all sample sizes, and the bias does not cancel between two arms
    /// of different sizes. Interpolation has no such preference: it is the same estimator whatever
    /// the count is parity-wise, which is what makes a figure comparable against another figure.
    /// </para>
    /// <para>
    /// Deterministic, which is what the alternative is usually defended on: the same sorted readings
    /// give the same IEEE operations in the same order and therefore the same bits, on every run and
    /// every machine. What it gives up is that the answer is no longer a reading anybody observed,
    /// and that only costs something where the reader is meant to go and find the run it came from.
    /// </para>
    /// <para>
    /// Zero on an empty sample rather than an exception, because the callers ask for a quantile of a
    /// set that may legitimately be empty — a run that normalised nothing — and treat zero as
    /// "no reading", declining the gate behind it.
    /// </para>
    /// </remarks>
    internal static double Interpolated(IReadOnlyList<double> sorted, double quantile)
    {
        if (sorted.Count == 0)
            return 0;

        if (sorted.Count == 1)
            return sorted[0];

        double position = quantile * (sorted.Count - 1);
        int lower = (int)Math.Floor(position);
        int upper = Math.Min(lower + 1, sorted.Count - 1);

        return sorted[lower] + ((position - lower) * (sorted[upper] - sorted[lower]));
    }

    /// <summary>
    /// Reads a quantile by nearest rank: the reading at <c>⌈q · n⌉</c>, counting from one.
    /// </summary>
    /// <typeparam name="T">The reading type. Never operated on, only indexed.</typeparam>
    /// <param name="sorted">Readings in ascending order. Must not be empty.</param>
    /// <param name="quantile">The quantile to read, in [0,1].</param>
    /// <returns>One of the readings themselves.</returns>
    /// <remarks>
    /// <para>
    /// For discrete quantities, where the interpolated answer would be a value the thing being
    /// counted cannot take. It returns one of the readings unchanged, which is also why it is
    /// generic without a numeric constraint — it does arithmetic on the index and none on the
    /// reading.
    /// </para>
    /// <para>
    /// Its even-count preference for the lower middle is a real property and callers depend on it:
    /// from two runs, <c>[1, 3]</c> reads as 1, so a single expensive run cannot on its own move a
    /// figure that a finding then claims a cost increase from. Lower is the conservative direction
    /// for every such claim.
    /// </para>
    /// <para>
    /// Throws on an empty sample rather than inventing a reading, since there is no value of
    /// <typeparamref name="T"/> that means "none". Every caller gates the count first.
    /// </para>
    /// </remarks>
    internal static T NearestRank<T>(IReadOnlyList<T> sorted, double quantile)
    {
        ArgumentOutOfRangeException.ThrowIfZero(sorted.Count);

        int rank = (int)Math.Ceiling(quantile * sorted.Count) - 1;

        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }
}
