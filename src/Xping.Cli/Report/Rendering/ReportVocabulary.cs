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
        nameof(FindingKind.RetryDeepening) => "deeper retries",
        nameof(FindingKind.RetryExhausted) => "out of retries",
        nameof(FindingKind.Flaky) => "flaky",
        nameof(FindingKind.AlwaysFailing) => "always failing",
        nameof(FindingKind.TimingOut) => "timing out",
        nameof(FindingKind.BrokenFixture) => "broken fixture",
        nameof(FindingKind.SharedFailure) => "shared failure",
        nameof(FindingKind.DurationRegression) => "slower",
        nameof(FindingKind.DurationUnstable) => "unstable timing",
        nameof(FindingKind.ParallelSensitive) => "concurrency",
        nameof(FindingKind.TimeSensitive) => "time sensitive",
        nameof(FindingKind.Vanished) => "stopped running",

        // A kind added to the enum without a label. Printing what the envelope said is worse than a
        // word and far better than a blank column.
        _ => kind
    };

    /// <summary>
    /// Gets the short marker for the population a finding's counts were taken over.
    /// </summary>
    /// <param name="population">The rule, as the envelope spells it.</param>
    /// <returns>The marker.</returns>
    /// <remarks>
    /// <para>
    /// Every finding carries one, including the ones that discount nothing. A marker printed only
    /// where something was set aside would leave a reader unable to tell "counted everything" from
    /// "this build did not say", and the whole point of the column is that the answer is always on
    /// the page.
    /// </para>
    /// <para>
    /// Terse because it shares a line with the evidence level, the finding id and the source path,
    /// inside a fence that must survive a phone. <see cref="PopulationLegend"/> is what makes the
    /// markers readable, and it is printed once for the report rather than once per finding.
    /// </para>
    /// </remarks>
    public static string PopulationTokenFor(string population) => population switch
    {
        "allExecutions" => "all runs",
        "excludesEnvironmental" => "-env",
        "excludesEnvironmentalAndClustered" => "-env-cluster",

        // Subtractive, like its two neighbours, and for the same reason: the marker's job is to say
        // what was taken out. The headline of the one kind that carries this says what was left in
        // — "full runs" — and a marker reading `full runs` beside `all runs` would differ by one
        // letter in the column whose whole purpose is discrimination at a glance.
        "excludesPartialRuns" => "-partial",

        // A rule added to the enum without a marker. Printing what the envelope said is worse than
        // a word and far better than a blank segment.
        _ => population
    };

    /// <summary>
    /// The lines that say what the population markers mean.
    /// </summary>
    /// <remarks>
    /// <para>
    /// States the rule and links the definitions, rather than trying to define each marker in the
    /// space available. Defining them here is what the previous wording attempted and it could not
    /// be done in two lines: it explained <c>-cluster</c>, which is never printed on its own, and
    /// left <c>all runs</c> unexplained altogether. The one thing a reader has to do with a marker
    /// is decide whether two percentages can be compared, and that needs no definitions at all.
    /// </para>
    /// <para>
    /// Short lines rather than long ones, so the whole legend survives the same paste the fence
    /// above it was shaped for. The URL is on a line of its own so that a terminal which turns it
    /// into a link has a whole line to make one out of, and so a reader copying it does not take
    /// half a sentence with it.
    /// </para>
    /// <para>
    /// The middle sentence is the reason the markers exist at all: the report ranks kinds against
    /// each other, and a reader who compares two rates taken over different populations gets a
    /// wrong answer from two correct numbers.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> PopulationLegend { get; } =
    [
        "rates: the marker on each finding says which runs its percentage was",
        "counted out of; compare two only where the markers match.",
        "https://docs.xping.io/cli/command-reference.html#the-population-marker"
    ];

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
