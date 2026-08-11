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
    /// Formats a rate for display, at the published precision and in a fixed culture.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted value.</returns>
    public static string FormatRate(double value) =>
        Round(value).ToString("0.###", CultureInfo.InvariantCulture);
}
