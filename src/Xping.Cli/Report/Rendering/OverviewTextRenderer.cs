/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Contract;
using Xping.Cli.Reporting;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// Writes the overview for a person: two lines per assembly, a third for Cloud, and the command
/// that opens one.
/// </summary>
/// <remarks>
/// <para>
/// Every number on a row is one the assembly's own report prints in its header, in the words that
/// header uses: tests, flagged, the findings phrase of <c>--summary</c>, runs, and the start of the
/// newest one. A row that said "unreliable" where the report says "flagged" would read as a second
/// opinion (report-format Amendment 6B).
/// </para>
/// <para>
/// Two lines rather than one: the name and its verdict are what the reader scans, and the counts
/// beside them on one line would push past the fence width on any real assembly name.
/// </para>
/// </remarks>
/// <param name="capabilities">What the output stream can show.</param>
internal sealed class OverviewTextRenderer(OutputCapabilities capabilities)
{
    private const int DetailIndent = 2;

    private readonly CloudText cloudText = new(capabilities.Glyphs);

    /// <summary>
    /// Writes the overview.
    /// </summary>
    /// <param name="envelope">The finished overview.</param>
    /// <param name="output">Where to write it.</param>
    public void Render(OverviewEnvelope envelope, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(output);

        string separator = $" {capabilities.Glyphs.Separator} ";
        string indent = new(' ', DetailIndent);

        output.WriteLine($"Xping{separator}{envelope.Assemblies.Count.ToString(CultureInfo.InvariantCulture)} assemblies");

        // A credential refused or timed out fails every row the same way: said once, under the
        // header, instead of as many times as there are assemblies (Amendment 8F).
        List<AssemblyOverviewDto> asked = [.. envelope.Assemblies.Where(a => a.Cloud is not null)];
        string? shared = asked.Count > 1
            && asked.All(a => cloudText.HealthToken(a.Cloud!.Health.State, a.Cloud.Health.Verdicts) is null)
            && asked.Select(CloudLine).Distinct(StringComparer.Ordinal).ToList() is [{ } only]
                ? only
                : null;

        if (shared is not null)
            output.WriteLine(capabilities.Dim("cloud: " + shared));

        output.WriteLine();

        foreach (AssemblyOverviewDto row in envelope.Assemblies)
        {
            string name = Recorded(row.Assembly);

            if (row.Summary is not { } summary || row.Window is not { } window)
            {
                output.WriteLine(Line(name, "no window", capabilities.Dim));
                output.WriteLine(capabilities.Dim(indent + Recorded(row.Unavailable ?? string.Empty)));
                continue;
            }

            string verdict = ReportVocabulary.FindingsPhrase(summary);
            string severity = summary.Counts switch
            {
                { High: > 0 } => "high",
                { Medium: > 0 } => "medium",
                { Low: > 0 } => "low",
                _ => string.Empty
            };

            output.WriteLine(Line(name, verdict, text => capabilities.Colorize(severity, text)));
            var counts = new List<string>
            {
                TextReportRenderer.Tests(summary.Tests),
                $"{summary.Flagged.ToString(CultureInfo.InvariantCulture)} flagged"
            };

            if (summary.FailedLatestRun > 0)
                counts.Add(TextReportRenderer.FailedLatestRun(summary));

            counts.Add(TextReportRenderer.Runs(window.SessionCount));
            counts.Add("last " + window.To.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            output.WriteLine(capabilities.Dim(indent + string.Join(separator, counts)));

            if (shared is null && CloudLine(row) is { } line)
                output.WriteLine(capabilities.Dim(indent + "cloud: " + line));
        }

        // The first row's command, because the first row is the one most worth opening. A line of its
        // own, ending on the command, so it can be copied whole and run.
        if (envelope.Assemblies.Count > 0)
        {
            output.WriteLine();
            output.WriteLine($"Details: {TerminalText.Printable(envelope.Assemblies[0].Command, TerminalCharset.Command)}");
        }
    }

    /// <summary>
    /// The header's Cloud line, in its words (Amendment 8F): the row repeats what its report says of
    /// Cloud, and says nothing when the report would say nothing.
    /// </summary>
    private string? CloudLine(AssemblyOverviewDto row) =>
        row.Cloud is { } cloud
            ? cloudText.HealthToken(cloud.Health.State, cloud.Health.Verdicts) ?? cloudText.RowReason(row.Assembly, cloud)
            : null;

    /// <summary>
    /// The name on the left and the verdict against the fence's right edge, or one space after a
    /// name too long to leave room.
    /// </summary>
    private string Line(string name, string verdict, Func<string, string> paint)
    {
        int gap = Math.Max(1, TextReportRenderer.FenceWidth - name.Length - verdict.Length);

        return capabilities.Emphasis(name) + new string(' ', gap) + paint(verdict);
    }

    /// <summary>Reads a value the runs recorded, made safe for the terminal (D7).</summary>
    private string Recorded(string value) => TerminalText.Printable(value, capabilities.Glyphs.Charset);
}
