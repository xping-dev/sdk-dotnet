/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report.Contract;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// Turns a finished envelope into output.
/// </summary>
/// <remarks>
/// A renderer chooses layout and nothing else. It contains no analysis, no thresholds and no
/// arithmetic — every value it prints was resolved before it was called. Two renderers that each
/// did their own formatting would eventually disagree about the same run, and the disagreement
/// would surface as a bug report nobody could reproduce.
/// </remarks>
internal interface IReportRenderer
{
    /// <summary>
    /// Writes one report.
    /// </summary>
    /// <param name="envelope">The finished report.</param>
    /// <param name="output">Where to write it.</param>
    void Render(ReportEnvelope envelope, TextWriter output);
}

/// <summary>
/// Writes the report as the machine-readable envelope.
/// </summary>
internal sealed class JsonReportRenderer : IReportRenderer
{
    /// <inheritdoc/>
    public void Render(ReportEnvelope envelope, TextWriter output)
    {
        output.WriteLine(JsonSerializer.Serialize(envelope, ReportJsonOptions.Default));
    }
}
