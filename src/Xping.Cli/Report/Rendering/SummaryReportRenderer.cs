/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;

using Xping.Cli.Report.Contract;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// Writes the report as a single line.
/// </summary>
/// <remarks>
/// For the places a report has to fit into one: the body of a chat message, a commit trailer, the
/// title of a CI step. It states the counts and nothing else — a line that tried to name a test
/// would be the one place the report's phrasing is not the fenced block's phrasing.
/// </remarks>
internal sealed class SummaryReportRenderer : IReportRenderer
{
    /// <inheritdoc/>
    public void Render(ReportEnvelope envelope, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(output);

        SummaryDto summary = envelope.Summary;
        int sessions = envelope.Window.SessionCount;

        string runs = sessions == 1
            ? "1 run"
            : $"{sessions.ToString(CultureInfo.InvariantCulture)} runs";

        string scope = envelope.Context?.Assembly is { Length: > 0 } assembly
            ? $" of {assembly}"
            : string.Empty;

        output.WriteLine($"Xping: {ReportVocabulary.FindingsPhrase(summary)} in {runs}{scope}");
    }
}
