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
/// <para>
/// The new-failure count is appended only when it is non-zero. A CI step title that says
/// <c>2 new failures</c> is the reason to open the report; one that says <c>0 new failures</c> on
/// every green build is a phrase a reader learns to skip, and the findings count beside it would
/// go with it.
/// </para>
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

        string newFailures = envelope.LatestRun is { Suppressed: false, IsLikelyEnvironmental: false, NewFailures: > 0 } latest
            ? latest.NewFailures == 1
                ? ", 1 new failure"
                : $", {latest.NewFailures.ToString(CultureInfo.InvariantCulture)} new failures"
            : string.Empty;

        output.WriteLine($"Xping: {ReportVocabulary.FindingsPhrase(summary)} in {runs}{scope}{newFailures}");
    }
}
