/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Renders a report and takes it apart again.
/// </summary>
/// <remarks>
/// Shared rather than private to one test class because two suites now read the same output: the
/// tests that pin one rule each, and the golden sweep that pins the whole block. Two copies of
/// "which lines are inside the fence" would be two answers the moment the fence moved.
/// </remarks>
internal static class ReportText
{
    /// <summary>The widest line the fence may contain.</summary>
    public const int FenceWidth = 72;

    /// <summary>
    /// The column everything below a row's header line starts at — the name, the headline and the
    /// trailer alike.
    /// </summary>
    public const int Indent = 4;

    /// <summary>The fence itself.</summary>
    public const string Fence = "```";

    /// <summary>
    /// Capabilities drawn with the ASCII glyph set, as a pipe receives them.
    /// </summary>
    /// <param name="redirected">Whether stdout is redirected.</param>
    /// <returns>The capabilities.</returns>
    public static OutputCapabilities Capabilities(bool redirected) =>
        OutputCapabilities.Resolve(forceAscii: true, noColor: false, redirected, _ => null);

    /// <summary>
    /// Capabilities for an explicit glyph set.
    /// </summary>
    /// <param name="glyphs">The set to draw with.</param>
    /// <param name="color">Whether ANSI colour may be emitted.</param>
    /// <returns>The capabilities.</returns>
    /// <remarks>
    /// Constructed rather than resolved. <see cref="OutputCapabilities.Resolve"/> reaches
    /// <see cref="ReportGlyphs.Detect"/> — and therefore the real console encoding — for any caller
    /// that did not force ASCII, so a test asking for Unicode through it would pass or fail on the
    /// machine's console encoding rather than on the renderer.
    /// </remarks>
    public static OutputCapabilities Drawn(ReportGlyphs glyphs, bool color) =>
        new(glyphs, color, Decorate: false);

    /// <summary>
    /// Renders an envelope to text.
    /// </summary>
    /// <param name="envelope">What to render.</param>
    /// <param name="capabilities">What the stream can carry; ASCII to a pipe by default.</param>
    /// <returns>The report.</returns>
    public static string Render(ReportEnvelope envelope, OutputCapabilities? capabilities = null)
    {
        using var writer = new StringWriter();
        new TextReportRenderer(capabilities ?? Capabilities(redirected: true)).Render(envelope, writer);

        return writer.ToString();
    }

    /// <summary>
    /// Splits a report into lines, whatever the platform wrote between them.
    /// </summary>
    /// <param name="report">The rendered report.</param>
    /// <returns>The lines.</returns>
    public static string[] Lines(string report) =>
        report.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    /// <summary>
    /// Returns the lines between the fences — the part that has to survive a paste.
    /// </summary>
    /// <param name="report">The rendered report.</param>
    /// <returns>The fenced lines.</returns>
    public static string[] Fenced(string report)
    {
        string[] lines = Lines(report);
        int open = Array.FindIndex(lines, l => l.Trim() == Fence);
        int close = Array.FindLastIndex(lines, l => l.Trim() == Fence);

        Assert.True(open >= 0 && close > open, "the report is not fenced");

        return lines[(open + 1)..close];
    }

    /// <summary>
    /// Returns the finding rows — the fenced lines beneath the section heading and its rule.
    /// </summary>
    /// <param name="report">The rendered report.</param>
    /// <returns>The rows.</returns>
    /// <remarks>
    /// The heading is part of the fenced block and is width-asserted with the rest of it, but a test
    /// about a row's shape should not have to count the lines above the first row.
    /// </remarks>
    public static string[] Rows(string report)
    {
        string[] fenced = Fenced(report);
        int heading = Array.FindIndex(
            fenced, l => l.StartsWith("NEEDS ATTENTION", StringComparison.Ordinal));

        // The heading, the rule under it, and the blank line before the first row.
        return heading < 0 ? fenced : fenced[(heading + 3)..];
    }

    /// <summary>
    /// Removes every ANSI escape sequence, leaving what a pipe would have received.
    /// </summary>
    /// <param name="value">The decorated text.</param>
    /// <returns>The text.</returns>
    public static string Strip(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '\u001b')
            {
                builder.Append(value[i]);
                continue;
            }

            while (i < value.Length && value[i] != 'm')
                i++;
        }

        return builder.ToString();
    }

    /// <summary>Returns the dim trailer line of a single-finding report.</summary>
    /// <param name="report">The rendered report.</param>
    /// <returns>The trailer.</returns>
    public static string Trailer(string report) =>
        Fenced(report).Single(l => l.Contains("evidence", StringComparison.Ordinal)).TrimEnd();
}
