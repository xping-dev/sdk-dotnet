/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Cli.Report.Contract;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// Writes the report for a person — in the terminal, and in whatever they paste it into next.
/// </summary>
/// <remarks>
/// <para>
/// <b>The finding list is always fenced.</b> A report is shared far more often than it is merely
/// read, and every destination worth sharing into — Slack, a pull request, a ticket — renders
/// pasted text in a proportional font that destroys column alignment, while rendering a fenced
/// block verbatim in monospace. Emitting the fence unconditionally means a mouse selection and a
/// pipe both produce something that survives the paste; the cost is two lines of backticks in a
/// terminal, which is the cheaper half of the trade.
/// </para>
/// <para>
/// <b>Nothing inside the fence exceeds <see cref="FenceWidth"/> columns.</b> Chat clients wrap a
/// code block rather than scrolling it on a phone, and a wrapped line loses the alignment the fence
/// existed to preserve.
/// </para>
/// <para>
/// Leads with what was analysed rather than with the findings. On a developer machine the window is
/// usually small and the finding list often empty, and a reader who cannot see how much history the
/// report rests on will read an empty list as "everything is fine" rather than "there is not much
/// to go on yet".
/// </para>
/// </remarks>
internal sealed class TextReportRenderer(OutputCapabilities capabilities) : IReportRenderer
{
    /// <summary>Widest line the fence may contain.</summary>
    /// <remarks>
    /// Narrow enough to survive a phone, and to leave room for the <c>&gt;</c> a reader adds when
    /// quoting the block back at someone.
    /// </remarks>
    private const int FenceWidth = 72;

    private const string Fence = "```";

    // Width of the " | " the trailer's segments are joined with.
    private const int SeparatorWidth = 3;

    // Severity marker, two spaces, kind label, two spaces. Everything after this is the subject.
    private const int MarkerWidth = 4;
    private const int Indent = MarkerWidth + 2;
    private const int SubjectColumn = Indent + ReportVocabulary.LongestLabel + 2;

    /// <inheritdoc/>
    public void Render(ReportEnvelope envelope, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(output);

        var builder = new StringBuilder();

        WriteHeader(builder, envelope);

        // Outside the fence, so that a reader who copies only the block still gets the findings and
        // a chat client that collapses a long block still shows where the report came from.
        builder.AppendLine();
        builder.AppendLine(Fence);

        WriteFindings(builder, envelope);

        builder.AppendLine(Fence);
        WriteFooter(builder, envelope);

        output.Write(builder.ToString());
    }

    private void WriteHeader(StringBuilder builder, ReportEnvelope envelope)
    {
        WindowDto window = envelope.Window;
        string separator = $" {capabilities.Glyphs.Separator} ";

        var provenance = new List<string> { "Xping" };

        if (envelope.Context?.Assembly is { Length: > 0 } assembly)
            provenance.Add(assembly);

        provenance.Add(Runs(window.SessionCount));
        provenance.Add(Period(window));

        if (Revision(envelope.Context) is { Length: > 0 } revision)
            provenance.Add(revision);

        builder.AppendLine(string.Join(separator, provenance));

        SummaryDto summary = envelope.Summary;

        var counts = new List<string>
        {
            ReportVocabulary.FindingsPhrase(summary),
            $"{summary.Tests.ToString(CultureInfo.InvariantCulture)} tests",
            $"{summary.Healthy.ToString(CultureInfo.InvariantCulture)} healthy"
        };

        if (summary.ExcludedLowEvidence > 0)
            counts.Add($"{summary.ExcludedLowEvidence} awaiting more runs");

        builder.AppendLine(string.Join(separator, counts));

        WriteCaveats(builder, envelope, separator);
    }

    /// <summary>
    /// Writes the ways in which the report saw less than it wanted to.
    /// </summary>
    /// <remarks>
    /// Above the fence rather than below it, because a partial report that looks complete is worse
    /// than no report — and the reader of a pasted block reads the top of it.
    /// </remarks>
    private void WriteCaveats(StringBuilder builder, ReportEnvelope envelope, string separator)
    {
        SummaryDto summary = envelope.Summary;
        var caveats = new List<string>();

        if (summary.UnreadableSessions > 0)
            caveats.Add($"{summary.UnreadableSessions} unreadable {RunWord(summary.UnreadableSessions)} skipped");

        if (summary.IncompleteSessions > 0)
            caveats.Add($"{summary.IncompleteSessions} incomplete {RunWord(summary.IncompleteSessions)} skipped");

        if (summary.EnvironmentalSessions > 0)
            caveats.Add(
                $"{summary.EnvironmentalSessions} {RunWord(summary.EnvironmentalSessions)} " +
                "discounted as environmental");

        if (summary.FailedProviders.Count > 0)
            caveats.Add($"metrics unavailable: {string.Join(", ", summary.FailedProviders)}");

        if (caveats.Count > 0)
            builder.Append(capabilities.Glyphs.Warning).Append(' ')
                   .AppendLine(string.Join(separator, caveats));
    }

    private void WriteFindings(StringBuilder builder, ReportEnvelope envelope)
    {
        if (envelope.Findings.Count == 0)
        {
            // Still fenced. A clean report and a full one should paste as the same shape, or a
            // reader learns to read the presence of a block as bad news.
            builder.AppendLine(envelope.Summary.ExcludedLowEvidence > 0
                ? $"{capabilities.Glyphs.Pending} Nothing reportable yet. " +
                  $"{envelope.Summary.ExcludedLowEvidence} candidate(s) need more runs."
                : $"{capabilities.Glyphs.Pass} No findings.");

            return;
        }

        // Findings arrive ranked. The severity column carries what the old grouping by kind carried,
        // and preserving the ranking top to bottom is worth more than the grouping was.
        for (int index = 0; index < envelope.Findings.Count; index++)
        {
            if (index > 0)
                builder.AppendLine();

            WriteFinding(builder, envelope.Findings[index]);
        }
    }

    private void WriteFinding(StringBuilder builder, FindingDto finding)
    {
        string label = ReportVocabulary.LabelFor(finding.Kind).PadRight(ReportVocabulary.LongestLabel);

        builder.Append(capabilities.Colorize(finding.Severity, ReportVocabulary.MarkerFor(finding.Severity)))
               .Append("  ")
               .Append(label)
               .Append("  ")
               .AppendLine(Fit(Subject(finding.Subject), FenceWidth - SubjectColumn));

        foreach (string line in Wrap(finding.Headline, FenceWidth - Indent))
            builder.Append(' ', Indent).AppendLine(line);

        var trailer = new List<string>
        {
            $"evidence {finding.EvidenceLevel}",
            finding.Id
        };

        const int budget = FenceWidth - Indent;

        // The source location is what makes a finding actionable, so it is printed whenever the SDK
        // captured one rather than being reserved for a verbose mode.
        if (finding.Subject.SourceFile is { Length: > 0 } file)
        {
            string location = finding.Subject.SourceLineNumber is { } line
                ? $"{file}:{line.ToString(CultureInfo.InvariantCulture)}"
                : file;

            // The path absorbs the truncation rather than the line as a whole. Fit() cuts from the
            // left, so applied to the joined trailer it would eat "evidence high" and leave the
            // path — the one segment that can afford to lose its head — untouched.
            int spent = 0;
            foreach (string part in trailer)
                spent += part.Length + SeparatorWidth;

            // spent counts one separator per segment already in the list, which is exactly the
            // number string.Join adds once the location makes a third — two separators for three
            // segments — so the joined line lands on the budget rather than three columns over it.
            if (FitPath(location, budget - spent) is { } fitted)
                trailer.Add(fitted);
        }

        builder.Append(' ', Indent)
               .AppendLine(capabilities.Dim(Fit(string.Join(" | ", trailer), budget)));
    }

    /// <summary>
    /// Writes the line that says the list was cut short, when it was.
    /// </summary>
    /// <remarks>
    /// Nothing at all when every finding is shown: the command that would show them all is the one
    /// the reader just ran, and a report that ends by suggesting itself trains people to skip the
    /// last line. One line for the whole report rather than one per finding either way — ten
    /// near-identical commands are ten lines of noise in anything the report is pasted into.
    /// </remarks>
    private void WriteFooter(StringBuilder builder, ReportEnvelope envelope)
    {
        TruncationDto truncated = envelope.Truncated;

        if (truncated.Shown >= truncated.Total)
            return;

        builder.AppendLine();
        builder.AppendLine(capabilities.Dim(
            $"Showing {truncated.Shown} of {truncated.Total} " +
            $"{capabilities.Glyphs.Separator} all: {truncated.Command}"));
    }

    private static string Subject(SubjectDto subject)
    {
        if (subject.MemberCount is not { } members)
            return subject.DisplayName ?? subject.FullyQualifiedName ?? subject.GroupId ?? "(unnamed)";

        SubjectDto? first = subject.Members?.Count > 0 ? subject.Members[0] : null;
        string name = first?.DisplayName ?? first?.FullyQualifiedName ?? subject.GroupId ?? "(cluster)";

        return members > 1 ? $"{name} +{members - 1} more" : name;
    }

    /// <summary>
    /// Formats the analysed period, keeping the times when they are what distinguishes the runs.
    /// </summary>
    private string Period(WindowDto window)
    {
        string arrow = $" {capabilities.Glyphs.Arrow} ";

        return window.From.Date == window.To.Date
            ? Format(window.From, "yyyy-MM-dd HH:mm") + arrow + Format(window.To, "HH:mm")
            : Format(window.From, "yyyy-MM-dd") + arrow + Format(window.To, "yyyy-MM-dd");
    }

    private static string Revision(ContextDto? context)
    {
        if (context == null)
            return string.Empty;

        string sha = context.Sha is { Length: > 7 } full ? full.Substring(0, 7) : context.Sha ?? string.Empty;

        if (context.Branch is not { Length: > 0 } branch)
            return sha;

        return sha.Length == 0 ? branch : $"{branch}@{sha}";
    }

    private static string Runs(int count) => count == 1 ? "1 run" : $"{count} runs";

    private static string RunWord(int count) => count == 1 ? "run" : "runs";

    private static string Format(DateTime value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Truncates from the left, keeping the end of the value.
    /// </summary>
    /// <remarks>
    /// The end is the half worth keeping: a test's identity is its method name, and a cluster's is
    /// its member count. Truncating from the right would leave a column of identical namespaces.
    /// </remarks>
    private static string Fit(string value, int width)
    {
        if (value.Length <= width || width <= 3)
            return value;

        return string.Concat("...", value.AsSpan(value.Length - (width - 3)));
    }

    /// <summary>
    /// Truncates a path from the left, cutting at a directory boundary.
    /// </summary>
    /// <remarks>
    /// The tail is the half worth keeping — the file and line are what a reader opens, while the
    /// leading directories are the part that varies with where the repository sits. Cutting at a
    /// separator rather than mid-segment keeps the result reading as a path: <c>.../CartTests.cs:42</c>
    /// rather than <c>...t/Cart/CartTests.cs:42</c>.
    /// </remarks>
    private static string? FitPath(string value, int width)
    {
        if (value.Length <= width)
            return value;

        // Under four columns not even an ellipsis and one character fit. Returning the value
        // untouched — as Fit() does at this width — would hand back a segment wider than its budget
        // and put the whole trailer back under left truncation, which is the one thing this method
        // exists to prevent. Nothing is the honest answer.
        if (width <= 3)
            return null;

        // Left to right, so the first separator that fits keeps the longest tail.
        for (int slash = 0; slash < value.Length; slash++)
        {
            if (value[slash] != '/')
                continue;

            if (value.Length - slash + 3 <= width)
                return string.Concat("...", value.AsSpan(slash));
        }

        // A single segment longer than the budget: no boundary to cut at, so cut mid-name. Safe to
        // delegate here because the width <= 3 case Fit() passes through was ruled out above.
        return Fit(value, width);
    }

    /// <summary>
    /// Breaks text onto lines no wider than a limit, at spaces.
    /// </summary>
    /// <remarks>
    /// A word longer than the limit is emitted whole rather than split. It is a type name, a path or
    /// an assertion fragment, and half of one is unsearchable — the fence overflowing by a few
    /// columns is the smaller harm.
    /// </remarks>
    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
                line.Append(' ');

            line.Append(word);
        }

        if (line.Length > 0)
            lines.Add(line.ToString());

        return lines.Count == 0 ? [string.Empty] : lines;
    }
}
