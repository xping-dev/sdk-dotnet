/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Contract;

/// <summary>
/// One assembly as the overview receives it: its finished report, or why there is none.
/// </summary>
/// <param name="Assembly">The assembly.</param>
/// <param name="Envelope">Its report; null when its window could not be opened.</param>
/// <param name="Unavailable">Why the window could not be opened; null when it was.</param>
internal sealed record OverviewRow(string Assembly, ReportEnvelope? Envelope, string? Unavailable);

/// <summary>
/// Builds the overview from every assembly's finished report.
/// </summary>
/// <remarks>
/// Takes reports rather than analyses, and copies their window and summary objects as they are.
/// The overview has no numbers of its own, which is what keeps a row equal to the header of the
/// report its command opens (report-format Amendment 6B).
/// </remarks>
internal static class OverviewBuilder
{
    /// <summary>
    /// Builds the overview envelope.
    /// </summary>
    /// <param name="rows">One per assembly, in any order.</param>
    /// <param name="scopeFor">The scope each row's command repeats, given its assembly.</param>
    /// <returns>The envelope, rows most severe first.</returns>
    public static OverviewEnvelope Build(IReadOnlyList<OverviewRow> rows, Func<string, ReportScope> scopeFor)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(scopeFor);

        return new OverviewEnvelope(
            ReportEnvelope.CurrentSchemaVersion,
            ReportScopes.Overview,
            [.. Order(rows, row => row.Assembly, row => row.Envelope?.Summary).Select(row => new AssemblyOverviewDto(
                row.Assembly,
                row.Envelope?.Window,
                row.Envelope?.Summary,
                row.Envelope == null ? row.Unavailable : null,
                DrillDown.ForAssembly(scopeFor(row.Assembly))))]);
    }

    /// <summary>
    /// Orders assemblies most severe first, then by name.
    /// </summary>
    /// <typeparam name="T">What is being ordered.</typeparam>
    /// <param name="items">The assemblies.</param>
    /// <param name="assembly">An item's assembly name.</param>
    /// <param name="summary">An item's report summary; null when its window did not open.</param>
    /// <returns>The items in overview order.</returns>
    /// <remarks>
    /// By the most severe band any finding reaches rather than by a count: one high finding is the
    /// row to read first, whatever another suite's tally of medium ones. An assembly with no window
    /// goes last, since there is nothing in it to act on. The name breaks ties ordinally, so two
    /// overviews of one store agree.
    /// </remarks>
    public static IEnumerable<T> Order<T>(IEnumerable<T> items, Func<T, string> assembly, Func<T, SummaryDto?> summary) =>
        items
            .OrderBy(item => Rank(summary(item)))
            .ThenBy(assembly, StringComparer.Ordinal);

    private static int Rank(SummaryDto? summary) => summary switch
    {
        null => 4,
        { Counts.High: > 0 } => 0,
        { Counts.Medium: > 0 } => 1,
        { Counts.Low: > 0 } => 2,
        _ => 3
    };
}
