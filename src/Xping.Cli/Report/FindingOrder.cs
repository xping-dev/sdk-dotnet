/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Report;

/// <summary>
/// The order findings appear in, everywhere.
/// </summary>
/// <remarks>
/// <para>
/// Total and stable. Every comparison falls through to a tiebreaker and ultimately to the finding
/// id, so no two distinct findings ever compare equal — which is what makes two runs over an
/// unchanged store produce byte-identical output, and what makes <c>--top N</c> mean the same thing
/// twice.
/// </para>
/// <para>
/// Impact is rounded before comparison. Two findings whose scores differ in the fifteenth decimal
/// place are the same finding as far as a reader is concerned, and letting that noise decide the
/// order would make the sort depend on floating-point accumulation order.
/// </para>
/// </remarks>
internal sealed class FindingOrder : IComparer<Finding>
{
    /// <summary>Gets the shared comparer.</summary>
    public static FindingOrder Instance { get; } = new();

    private FindingOrder()
    {
    }

    /// <inheritdoc/>
    public int Compare(Finding? x, Finding? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return 1;
        if (y is null)
            return -1;

        // Severity is declared most-severe-first, so ascending ordinal order is descending severity.
        int bySeverity = x.Severity.CompareTo(y.Severity);
        if (bySeverity != 0)
            return bySeverity;

        int byImpact = Round(y.Impact).CompareTo(Round(x.Impact));
        if (byImpact != 0)
            return byImpact;

        int byKind = x.Kind.CompareTo(y.Kind);
        if (byKind != 0)
            return byKind;

        int bySubject = string.CompareOrdinal(x.Subject.SortKey, y.Subject.SortKey);
        if (bySubject != 0)
            return bySubject;

        return string.CompareOrdinal(x.Id, y.Id);
    }

    /// <summary>
    /// Rounds a rate to the precision the report actually publishes.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <returns>The rounded value.</returns>
    public static double Round(double value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Rounds a percentage to the precision the report actually publishes.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <returns>The rounded value.</returns>
    public static double RoundPercent(double value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Rounds a p-value to the three significant digits the report publishes.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <returns>The rounded value; zero and negatives are returned unchanged.</returns>
    /// <remarks>
    /// <para>
    /// Significant digits rather than the decimal places <see cref="Round"/> gives every other
    /// published figure. Nothing floors most of these probabilities — the tail of a normal, or the
    /// probability of one table among the many a window's margins permit — and they fall off a cliff
    /// as the window grows, so a fixed count of decimals eventually publishes one as zero. A
    /// probability of zero is a claim of certainty, and no measurement in this report makes one.
    /// </para>
    /// <para>
    /// Three digits because that is the precision the figure has to a reader deciding whether to
    /// believe a finding. The unrounded value is what reaches the coordinator, where a multiplicity
    /// correction sorts on it; this is only what gets written down.
    /// </para>
    /// <para>
    /// Through a round-trip rather than <see cref="Math.Round(double, int)"/>, which takes a count of
    /// decimal places and refuses more than fifteen. Deriving that count from the magnitude
    /// reintroduces the defect this method exists to avoid, one window size further out: a perfectly
    /// separated split of twenty-eight runs against twenty-eight is 2.6e-16, needs eighteen places,
    /// and is clamped to fifteen — which rounds it to zero. Scaling by a power of ten instead fails
    /// at the other end, where the scale factor itself overflows to infinity. A significant-digit
    /// format has neither limit and is what "three significant digits" actually means.
    /// </para>
    /// </remarks>
    public static double RoundProbability(double value) =>
        value > 0
            ? double.Parse(
                value.ToString("G3", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : value;

    /// <summary>
    /// Formats a rate for display, at the published precision and in a fixed culture.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted value.</returns>
    public static string FormatRate(double value) =>
        Round(value).ToString("0.###", CultureInfo.InvariantCulture);
}
