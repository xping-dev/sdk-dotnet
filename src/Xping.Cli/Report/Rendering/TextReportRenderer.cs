/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Cli.Report.Contract;
using Xping.Cli.Reporting;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// Writes the report for a person reading a terminal.
/// </summary>
/// <remarks>
/// Leads with what was analysed rather than with the findings. On a developer machine the window is
/// usually small and the finding list often empty, and a reader who cannot see how much history the
/// report rests on will read an empty list as "everything is fine" rather than "there is not much to
/// go on yet".
/// </remarks>
internal sealed class TextReportRenderer(ReportGlyphs glyphs) : IReportRenderer
{
    private const int RuleWidth = 64;

    /// <inheritdoc/>
    public void Render(ReportEnvelope envelope, TextWriter output)
    {
        var builder = new StringBuilder();

        WriteHeader(builder, envelope);
        WriteFindings(builder, envelope);
        WriteSummary(builder, envelope);

        output.Write(builder.ToString());
    }

    private void WriteHeader(StringBuilder builder, ReportEnvelope envelope)
    {
        WindowDto window = envelope.Window;

        builder.Append("Xping report ")
               .Append(glyphs.Separator)
               .Append(' ')
               .Append(window.SessionCount.ToString(CultureInfo.InvariantCulture))
               .Append(window.SessionCount == 1 ? " run" : " runs")
               .Append(' ')
               .Append(glyphs.Separator)
               .Append(' ')
               .Append(Format(window.From))
               .Append(' ')
               .Append(glyphs.Arrow)
               .Append(' ')
               .Append(Format(window.To))
               .AppendLine();

        if (envelope.Context is { } context)
        {
            var parts = new List<string>();

            if (context.Assembly is { Length: > 0 })
                parts.Add(context.Assembly);
            if (context.Branch is { Length: > 0 })
                parts.Add(context.Branch);
            if (context.Sha is { Length: > 0 })
                parts.Add(Abbreviate(context.Sha));

            if (parts.Count > 0)
                builder.AppendLine(string.Join($" {glyphs.Separator} ", parts));
        }

        builder.Append(new string(glyphs.HorizontalRule, RuleWidth)).AppendLine();
    }

    private void WriteFindings(StringBuilder builder, ReportEnvelope envelope)
    {
        builder.AppendLine();

        if (envelope.Findings.Count == 0)
        {
            builder.AppendLine(envelope.Summary.ExcludedLowEvidence > 0
                ? $"{glyphs.Pending} Nothing reportable yet. " +
                  $"{envelope.Summary.ExcludedLowEvidence} candidate(s) need more runs before they " +
                  "can be judged."
                : $"{glyphs.Pass} No findings.");

            return;
        }

        // Grouping by kind is the only decision this renderer makes about a finding, and it is a
        // layout decision: findings arrive already ranked, and the groups preserve that ranking.
        string? currentKind = null;

        foreach (FindingDto finding in envelope.Findings)
        {
            if (!string.Equals(currentKind, finding.Kind, StringComparison.Ordinal))
            {
                if (currentKind != null)
                    builder.AppendLine();

                builder.Append(finding.Kind).AppendLine();
                currentKind = finding.Kind;
            }

            WriteFinding(builder, finding);
        }
    }

    private void WriteFinding(StringBuilder builder, FindingDto finding)
    {
        builder.Append("  ")
               .Append(Marker(finding.Severity))
               .Append(' ')
               .Append(finding.Subject.DisplayName ?? finding.Subject.GroupId ?? finding.Id)
               .AppendLine();

        builder.Append("      severity ")
               .Append(finding.Severity)
               .Append(' ')
               .Append(glyphs.Separator)
               .Append(" evidence ")
               .Append(finding.EvidenceLevel)
               .Append(' ')
               .Append(glyphs.Separator)
               .Append(' ')
               .Append(finding.Id)
               .AppendLine();

        // The source location is what makes a finding actionable, so it is printed whenever the SDK
        // captured one rather than being reserved for a verbose mode.
        if (finding.Subject.SourceFile is { Length: > 0 } file)
        {
            builder.Append("      ").Append(file);

            if (finding.Subject.SourceLineNumber is { } line)
                builder.Append(':').Append(line.ToString(CultureInfo.InvariantCulture));

            builder.AppendLine();
        }

        if (finding.Subject.MemberCount is { } members)
            builder.Append("      ").Append(members).AppendLine(" tests affected");

        builder.Append("      ").AppendLine(finding.DrillDown);
    }

    private void WriteSummary(StringBuilder builder, ReportEnvelope envelope)
    {
        SummaryDto summary = envelope.Summary;

        builder.AppendLine();
        builder.Append(new string(glyphs.HorizontalRule, RuleWidth)).AppendLine();

        builder.Append(summary.Tests.ToString(CultureInfo.InvariantCulture))
               .Append(" tests ")
               .Append(glyphs.Separator)
               .Append(' ')
               .Append(summary.Findings.ToString(CultureInfo.InvariantCulture))
               .Append(" findings ")
               .Append(glyphs.Separator)
               .Append(' ')
               .Append(summary.Healthy.ToString(CultureInfo.InvariantCulture))
               .AppendLine(" healthy");

        // Every one of these says the report saw less than it wanted to. Silence about them would
        // make a partial report look like a complete one.
        if (summary.ExcludedLowEvidence > 0)
            builder.Append(summary.ExcludedLowEvidence).AppendLine(" excluded for low evidence");

        if (summary.UnreadableSessions > 0)
            builder.Append(glyphs.Warning).Append(' ')
                   .Append(summary.UnreadableSessions).AppendLine(" unreadable run(s) skipped");

        if (summary.IncompleteSessions > 0)
            builder.Append(glyphs.Warning).Append(' ')
                   .Append(summary.IncompleteSessions).AppendLine(" incomplete run(s) skipped");

        if (summary.FailedProviders.Count > 0)
            builder.Append(glyphs.Warning).Append(" metrics unavailable: ")
                   .AppendLine(string.Join(", ", summary.FailedProviders));

        if (envelope.Truncated.Shown < envelope.Truncated.Total)
            builder.Append("Showing ")
                   .Append(envelope.Truncated.Shown)
                   .Append(" of ")
                   .Append(envelope.Truncated.Total)
                   .Append(". All: ")
                   .AppendLine(envelope.Truncated.Command);
    }

    private string Marker(string severity) => severity switch
    {
        "high" => glyphs.Fail,
        "medium" => glyphs.Warning,
        _ => glyphs.Skip
    };

    private static string Format(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Abbreviate(string sha) =>
        sha.Length <= 7 ? sha : sha.Substring(0, 7);
}
