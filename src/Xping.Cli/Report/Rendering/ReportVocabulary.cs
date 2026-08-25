/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// The words a person is shown for the things the model names.
/// </summary>
/// <remarks>
/// <para>
/// Enum names are for the JSON contract and for <c>--kind</c>; <c>RetryMasked</c> and
/// <c>AlwaysFailing</c> are precise and read like identifiers, which is what they are. A report
/// pasted into a chat is read by people who have never seen the enum.
/// </para>
/// <para>
/// One table, shared by every human-facing renderer. Two renderers each inventing a label is how a
/// report and its one-line summary end up calling the same finding two different things.
/// </para>
/// </remarks>
internal static class ReportVocabulary
{
    /// <summary>Longest label here, so a column can be sized without measuring at runtime.</summary>
    public const int LongestLabel = 15;

    /// <summary>
    /// Gets the human label for a finding kind.
    /// </summary>
    /// <param name="kind">The kind, as the envelope spells it.</param>
    /// <returns>The label.</returns>
    /// <remarks>
    /// Takes the serialized string rather than the enum: renderers read an envelope, which is text
    /// by then, and reparsing it back into an enum only to look up a word would be a second place
    /// an unknown kind could throw.
    /// </remarks>
    public static string LabelFor(string kind) => kind switch
    {
        nameof(FindingKind.RetryMasked) => "masked by retry",
        nameof(FindingKind.Flaky) => "flaky",
        nameof(FindingKind.AlwaysFailing) => "always failing",
        nameof(FindingKind.TimingOut) => "timing out",
        nameof(FindingKind.BrokenFixture) => "broken fixture",
        nameof(FindingKind.SharedFailure) => "shared failure",
        nameof(FindingKind.DurationRegression) => "slower",
        nameof(FindingKind.DurationUnstable) => "unstable timing",
        nameof(FindingKind.OrderDependent) => "order dependent",
        nameof(FindingKind.ParallelSensitive) => "concurrency",
        nameof(FindingKind.TimeSensitive) => "time sensitive",
        nameof(FindingKind.NetworkDependent) => "network",
        nameof(FindingKind.Vanished) => "stopped running",
        nameof(FindingKind.NeverRun) => "never run",

        // A kind added to the enum without a label. Printing what the envelope said is worse than a
        // word and far better than a blank column.
        _ => kind
    };

    /// <summary>
    /// Gets the fixed-width marker for severity.
    /// </summary>
    /// <param name="severity">The severity, as the envelope spells it.</param>
    /// <returns>The marker, padded to a common width.</returns>
    public static string MarkerFor(string severity) => severity switch
    {
        "high" => "HIGH",
        "medium" => "MED ",
        "low" => "LOW ",
        _ => "    "
    };

    /// <summary>
    /// Phrases the finding count and its severity breakdown.
    /// </summary>
    /// <param name="summary">The run summary.</param>
    /// <returns>The phrase, without a trailing full stop.</returns>
    /// <remarks>
    /// Shared by the fenced report and the one-line summary, so the two cannot describe the same run
    /// with two different sentences. Bands that are empty are omitted rather than printed as zero:
    /// "3 findings (1 high, 2 medium)" is read at a glance, and "3 findings (1 high, 2 medium, 0 low)"
    /// is not.
    /// </remarks>
    public static string FindingsPhrase(SummaryDto summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.Findings == 0)
            return "no findings";

        var bands = new List<string>();

        if (summary.Counts.High > 0)
            bands.Add($"{summary.Counts.High.ToString(CultureInfo.InvariantCulture)} high");
        if (summary.Counts.Medium > 0)
            bands.Add($"{summary.Counts.Medium.ToString(CultureInfo.InvariantCulture)} medium");
        if (summary.Counts.Low > 0)
            bands.Add($"{summary.Counts.Low.ToString(CultureInfo.InvariantCulture)} low");

        string count = summary.Findings == 1
            ? "1 finding"
            : $"{summary.Findings.ToString(CultureInfo.InvariantCulture)} findings";

        return $"{count} ({string.Join(", ", bands)})";
    }
}
