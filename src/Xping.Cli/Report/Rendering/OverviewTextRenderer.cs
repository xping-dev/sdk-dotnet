/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Contract;
using Xping.Cli.Reporting;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// Writes the overview for a person: two lines per assembly and the command that opens one.
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
            output.WriteLine(capabilities.Dim(indent + string.Join(separator,
                $"{summary.Tests.ToString(CultureInfo.InvariantCulture)} tests",
                $"{summary.Flagged.ToString(CultureInfo.InvariantCulture)} flagged",
                TextReportRenderer.Runs(window.SessionCount),
                "last " + window.To.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))));
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
