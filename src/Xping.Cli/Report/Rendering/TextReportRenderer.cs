/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;

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

    // Kinds named on the unmeasured line before the rest become a count. Three is what fits beside
    // the glyph and the prefix at the widths `ReportVocabulary` labels take; a fourth pushes the
    // line past the fence it sits above, and a reader scanning four numbers for the largest is
    // being given a table one segment at a time.
    private const int UnmeasuredKindsShown = 3;

    private const string Fence = "```";

    // What the heading says about the order of the rows beneath it. Right-aligned to the fence,
    // opposite the heading, so it reads as a note on the list rather than as part of its name.
    //
    // It ranks tests, not rows. A second finding about a test already listed follows the first
    // whatever its own severity, and its header line says so; between distinct tests the order is
    // exactly what the note claims. The alternative wording -- "grouped by test" -- would describe
    // the exception and not the rule.
    private const string OrderingNote = "most severe first";

    // Width of the " | " the trailer's segments are joined with.
    private const int SeparatorWidth = 3;

    // The row number, right-padded so that single and double digits agree, plus one space. Rows are
    // numbered because a truncated list only reads as truncated when they are, and because a
    // finding has to be referrable to from the row beneath it.
    private const int RowLabelWidth = 4;

    // Everything below a row's header line is indented to here, which puts it under the severity
    // marker. The name, the headline and the trailer all get the same budget as a result: a name is
    // the one line in the report that must never be cut, and the old layout gave it the least room
    // of the three by spending a third of the fence on the marker and the kind label beside it.
    private const int ContinuationIndent = RowLabelWidth;

    // What every line below a row's header has to fit in.
    private const int ContinuationBudget = FenceWidth - ContinuationIndent;

    // A cluster's members are indented past the cause they share, so the two read as a list under a
    // heading rather than as four names of equal standing.
    private const int MemberIndent = ContinuationIndent + 2;

    // The column a wrapped empty-report line continues at: the width of the glyph and the space
    // after it, so the continuation aligns under the sentence and reads as more of it rather than
    // as a second piece of news. Not ContinuationIndent -- that is where a finding's lines sit, and
    // the empty report is not a finding.
    private const int EmptyReportIndent = 2;

    // The latest-run rows: a status word at this indent, in a field this wide, and the name, the
    // contrast and the trailer at the column after it. Wider than a severity marker's field because
    // "seen before" is eleven characters and the status is prose, not a code; the name still keeps
    // 55 columns, which the same Class.Method that fits a finding's 68 fits in practice.
    private const int LatestRunIndent = 4;

    private const int LatestRunStatusWidth = 13;

    private const int LatestRunRowIndent = LatestRunIndent + LatestRunStatusWidth;

    private const int LatestRunBudget = FenceWidth - LatestRunRowIndent;

    // What the latest-run heading is called. The time and sha follow it on the same line, two
    // spaces off, so the label reads as a label and the provenance as provenance.
    private const string LatestRunLabel = "LATEST RUN";

    // Members named before the rest become a count. Three is enough to recognise what the cluster
    // is -- one test suite, one fixture, one namespace -- and a forty-member cluster listed in full
    // is a finding nobody scrolls past. The whole list is in the JSON, where a caller reads it by
    // name rather than by eye.
    private const int MembersShown = 3;

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

        WriteLegend(builder, envelope);
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

        // What state the suite is in, and nothing about the findings: the line above answers what was
        // analysed, and the heading below the fence carries the severity breakdown beside the list
        // it describes. The three counts reconcile -- tests is healthy plus flagged -- which is what
        // the finding count standing in this position could never do, one test carrying two findings
        // and one finding covering a whole cluster.
        var counts = new List<string>
        {
            $"{summary.Tests.ToString(CultureInfo.InvariantCulture)} tests",
            $"{summary.Healthy.ToString(CultureInfo.InvariantCulture)} healthy",
            $"{summary.Flagged.ToString(CultureInfo.InvariantCulture)} flagged"
        };

        if (summary.ExcludedLowEvidence > 0)
            counts.Add($"{summary.ExcludedLowEvidence} awaiting more runs");

        // Beside the evidence count rather than among the caveats, because the two say the same sort
        // of thing — a candidate the report saw and did not print — and a reader comparing two runs
        // needs them in the same place. A caveat is something that went wrong; a candidate that did
        // not clear its kind's bar is the report working.
        if (summary.ExcludedNotSignificant > 0)
            counts.Add($"{summary.ExcludedNotSignificant} not significant");

        builder.AppendLine(string.Join(separator, counts));

        WriteUnmeasured(builder, summary, separator);
        WriteCaveats(builder, envelope, separator);
    }

    /// <summary>
    /// Writes the questions this window's data could not answer at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its own line, because it is neither of the things the two lines around it are. The counts
    /// line holds suite-wide totals of candidates the report saw and withheld; this is per kind and
    /// counts tests no candidate ever existed for. The caveat line holds things that went wrong;
    /// a question the recorded data cannot answer is the report working. And thirteen kinds cannot
    /// be appended to a line of totals in any case.
    /// </para>
    /// <para>
    /// <b>Only the unreadable half is printed.</b> The counts line already says "awaiting more
    /// runs", and a second waiting figure beside it in a different unit — tests here, candidates
    /// there — is the confusion this whole change exists to remove. The awaiting half is published
    /// in the JSON envelope, where a caller can read the two apart by name.
    /// </para>
    /// <para>
    /// Three kinds and then a count of the rest. At the widths these labels take, three segments
    /// and the glyph land around the fence's own width; four are reliably past it, and a reader who
    /// has to scan four numbers to find the big one would have been better served by the JSON.
    /// </para>
    /// </remarks>
    private void WriteUnmeasured(StringBuilder builder, SummaryDto summary, string separator)
    {
        // Count descending so the largest gap is read first, then by the enum's own order so that
        // two kinds with equal counts resolve the same way on every run. Without the tie-break the
        // line would depend on dictionary order and two reports over one store could differ.
        List<KeyValuePair<string, NotMeasuredDto>> unreadable =
        [
            .. summary.NotMeasured
                .Where(entry => entry.Value.Unreadable > 0)
                .OrderByDescending(entry => entry.Value.Unreadable)
                .ThenBy(entry => KindOrder(entry.Key))
        ];

        if (unreadable.Count == 0)
            return;

        var segments = new List<string>();

        foreach (KeyValuePair<string, NotMeasuredDto> entry in unreadable.Take(UnmeasuredKindsShown))
        {
            segments.Add(
                $"{ReportVocabulary.LabelFor(entry.Key)} " +
                entry.Value.Unreadable.ToString(CultureInfo.InvariantCulture));
        }

        if (unreadable.Count > UnmeasuredKindsShown)
            segments.Add($"+{unreadable.Count - UnmeasuredKindsShown} more");

        builder.Append(capabilities.Glyphs.Pending).Append(' ')
               .Append("nothing to measure: ")
               .AppendLine(string.Join(separator, segments));
    }

    /// <summary>
    /// Orders a kind by its declaration, for a tie-break that cannot vary between runs.
    /// </summary>
    /// <param name="kind">The kind, as the envelope spells it.</param>
    /// <returns>Its position, or one past the end for a kind this build does not know.</returns>
    private static int KindOrder(string kind) =>
        Enum.TryParse(kind, out FindingKind parsed) ? (int)parsed : int.MaxValue;

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

        // Named by what is wrong with them rather than by what was done about them: "skipped" alone
        // would read as another unreadable file, and the reader can only act on this one by fixing
        // the clock on the machine that wrote them.
        if (summary.SkewedSessions > 0)
            caveats.Add(
                $"{summary.SkewedSessions} {RunWord(summary.SkewedSessions)} " +
                "skipped, stamped ahead of this machine's clock");

        if (summary.IncompleteSessions > 0)
            caveats.Add($"{summary.IncompleteSessions} incomplete {RunWord(summary.IncompleteSessions)} skipped");

        if (summary.EnvironmentalSessions > 0)
            caveats.Add(
                $"{summary.EnvironmentalSessions} {RunWord(summary.EnvironmentalSessions)} " +
                "discounted as environmental");

        // Worded as what the window holds, not as what was done about it. Only the kinds that read
        // absence set these aside; every other kind still counts them in full, and "discounted"
        // beside the environmental line would claim a symmetry that does not exist.
        //
        // Against the window's own run count, because that is the number the header states and the
        // one this caveat qualifies. "3 runs covered part of the suite" says a filtered run
        // happened; "3 of 20" says what fraction of the window it is, which is the only thing a
        // reader can do anything with.
        //
        // Deliberately not "the denominator every rate is read against": the rates below are not
        // all taken over the same thing, which is what each finding's population marker exists to
        // say. This line scopes the window; the marker scopes the finding.
        if (summary.PartialSessions > 0)
            caveats.Add(
                $"{summary.PartialSessions} of {envelope.Window.SessionCount} " +
                $"{RunWord(envelope.Window.SessionCount)} covered part of the suite");

        if (summary.FailedProviders.Count > 0)
            caveats.Add($"metrics unavailable: {string.Join(", ", summary.FailedProviders)}");

        if (caveats.Count > 0)
            builder.Append(capabilities.Glyphs.Warning).Append(' ')
                   .AppendLine(string.Join(separator, caveats));
    }

    private void WriteFindings(StringBuilder builder, ReportEnvelope envelope)
    {
        // Above the findings, inside the same fence. The reader ran the command because something
        // just happened, and the section is absent when nothing did, so the top slot costs nothing
        // in the healthy case.
        if (WriteLatestRun(builder, envelope))
            builder.AppendLine();

        if (envelope.Findings.Count == 0)
        {
            // Still fenced. A clean report and a full one should paste as the same shape, or a
            // reader learns to read the presence of a block as bad news.
            //
            // Wrapped like every other line in the block. The sentence names one reason per thing
            // the report withheld, and a store that withheld all three ran past the fence -- which
            // stayed invisible until the width assertion swept every fixture rather than one.
            //
            // The budget is the indent's short of the fence for every line, including the first,
            // which does not carry it. Two budgets would buy the first line two columns it has
            // never needed, and would put the arithmetic somewhere a reader has to check.
            List<string> reason = Wrap(EmptyReport(envelope.Summary), FenceWidth - EmptyReportIndent);

            for (int index = 0; index < reason.Count; index++)
                builder.Append(' ', index == 0 ? 0 : EmptyReportIndent).AppendLine(reason[index]);

            return;
        }

        string bands = ReportVocabulary.SeverityBands(envelope.Summary);
        WriteHeading(
            builder,
            bands.Length > 0 ? $"NEEDS ATTENTION ({bands})" : "NEEDS ATTENTION",
            OrderingNote);

        // Findings arrive ranked. The severity column carries what the old grouping by kind carried,
        // and preserving the ranking top to bottom is worth more than the grouping was.
        for (int index = 0; index < envelope.Findings.Count; index++)
        {
            if (index > 0)
                builder.AppendLine();

            WriteFinding(builder, envelope.Findings[index], index + 1);
        }
    }

    /// <summary>
    /// Writes the heading the finding list sits under.
    /// </summary>
    /// <param name="builder">What the report is being written into.</param>
    /// <param name="label">The upper-case name of the section, with whatever qualifies it.</param>
    /// <param name="annotation">What the reader should know about the rows, right-aligned.</param>
    /// <remarks>
    /// <para>
    /// One shape for every section: the label at the left, the annotation at the fence, a rule
    /// beneath. What either says is the section's business — the findings heading carries the
    /// severity bands and the ordering note, the latest-run heading the session's time and its
    /// failure count — and the shape is what tells a reader they are looking at a section at all.
    /// </para>
    /// <para>
    /// A count in an internal vocabulary word — "5 findings (5 high)" — told a reader how many rows
    /// there were and nothing about whether the list was informational or actionable. The heading
    /// says which it is, in words that claim nothing the evidence does not: not "problems found",
    /// which asserts causality the evidence rules forbid, and not "reliability issues", which is
    /// jargon.
    /// </para>
    /// <para>
    /// The parenthesis is where the severity breakdown lives now that it has left the header, and it
    /// sits beside the list it describes rather than two lines above it. Nothing is printed twice:
    /// the heading carries the bands, the rows carry the ranking, the header carries the suite.
    /// </para>
    /// <para>
    /// Only where there is something under it. A clean report and a full one should not both require
    /// a reader to parse a heading before discovering which they are looking at.
    /// </para>
    /// </remarks>
    private void WriteHeading(StringBuilder builder, string label, string annotation)
    {
        builder.Append(label)
               .Append(' ', Math.Max(1, FenceWidth - label.Length - annotation.Length))
               .AppendLine(annotation);

        builder.Append(capabilities.Glyphs.HorizontalRule, FenceWidth).AppendLine();
        builder.AppendLine();
    }

    /// <summary>
    /// Writes what just broke, above the findings, when there is anything to say.
    /// </summary>
    /// <param name="builder">What the report is being written into.</param>
    /// <param name="envelope">The report.</param>
    /// <returns>Whether anything was written.</returns>
    /// <remarks>
    /// <para>
    /// The findings answer what is chronically unreliable and need five runs to answer it. This
    /// answers what the newest run did that the runs before it did not, which needs two, and it is
    /// the first thing a reader who just watched a build go red wants to know.
    /// </para>
    /// <para>
    /// Nothing at all on a clean run, and on a window of one session: every row would restate the
    /// test runner. A section that appears only when it has news is a section a reader learns to
    /// read. A run that failed too widely to itemise is news, and gets one line saying so.
    /// </para>
    /// <para>
    /// Every sentence here was composed in the envelope. The renderer lays the rows out and phrases
    /// nothing, so that a stronger statement of the same history — which is what Cloud will supply
    /// — is a change to one string and not to this file.
    /// </para>
    /// </remarks>
    private bool WriteLatestRun(StringBuilder builder, ReportEnvelope envelope)
    {
        if (envelope.LatestRun is not { Suppressed: false } latest)
            return false;

        if (!latest.IsLikelyEnvironmental && latest.Failures.Count == 0 && latest.ExplainedByFindings == 0)
            return false;

        WriteHeading(builder, LatestRunLabel + "  " + LatestRunProvenance(latest), LatestRunAnnotation(latest));

        // The environmental heuristic exists to stop one broken dependency from poisoning every
        // test's history, and a section that itemised the 187 tests it took down would defeat it.
        // The run is described in one line and not listed.
        if (latest.IsLikelyEnvironmental)
        {
            builder.Append(' ', LatestRunIndent)
                   .Append("Looks environmental: ")
                   .Append(latest.TestsFailed.ToString(CultureInfo.InvariantCulture))
                   .Append(" of ")
                   .Append(Tests(latest.TestsExecuted))
                   .AppendLine(" failed. Not itemised.");

            return true;
        }

        for (int index = 0; index < latest.Failures.Count; index++)
        {
            if (index > 0)
                builder.AppendLine();

            WriteLatestRunFailure(builder, latest.Failures[index]);
        }

        // The two closing lines sit together under the rows, the cap first because it is about the
        // rows and the "also failing" line because it is about the run. The cap line is dim like
        // the footer that says the same thing about the findings; it is navigation, not news.
        bool capped = latest.FailuresShown < latest.FailuresTotal;

        if (capped || latest.ExplainedByFindings > 0)
        {
            if (latest.Failures.Count > 0)
                builder.AppendLine();
        }

        if (capped)
        {
            builder.Append(' ', LatestRunIndent).AppendLine(capabilities.Dim(
                $"Showing {latest.FailuresShown.ToString(CultureInfo.InvariantCulture)} of " +
                $"{latest.FailuresTotal.ToString(CultureInfo.InvariantCulture)} " +
                $"{capabilities.Glyphs.Separator} all: {latest.OverflowCommand}"));
        }

        if (latest.ExplainedByFindings > 0)
            builder.Append(' ', LatestRunIndent).AppendLine(AlsoFailing(latest, envelope.Findings));

        return true;
    }

    /// <summary>
    /// Says which run the section is about: its start time and, when recorded, its commit.
    /// </summary>
    /// <remarks>
    /// The time and not the date, because the header two lines up already places the window, and
    /// on the one store where the date would matter — one nobody has run in a week — the header
    /// says so too. The sha is shortened the way the header shortens it, and never invented.
    /// </remarks>
    private string LatestRunProvenance(LatestRunDto latest)
    {
        string time = Format(latest.StartedAt, "HH:mm");

        if (latest.Sha is not { Length: > 0 } sha)
            return time;

        return $"{time} {capabilities.Glyphs.Separator} {(sha.Length > 7 ? sha.Substring(0, 7) : sha)}";
    }

    /// <summary>
    /// Says what the rows add up to, at the far end of the heading line.
    /// </summary>
    /// <remarks>
    /// Counts what is new, not what is listed. A test that had failed before is a row, because the
    /// section is a complete account of the run, but it is not news — and the number here is what a
    /// reader glancing at the heading takes away.
    /// </remarks>
    private static string LatestRunAnnotation(LatestRunDto latest) => latest.NewFailures switch
    {
        _ when latest.IsLikelyEnvironmental => "looks environmental",
        0 => "no new failures",
        1 => "1 new failure",
        int count => $"{count.ToString(CultureInfo.InvariantCulture)} new failures"
    };

    /// <summary>
    /// Writes one latest-run row: the status and name, the contrast, and the dim trailer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The status word is the only decorated element, and only <c>new</c> is decorated: it is the
    /// regression signal and the reason the section exists. Bold rather than a colour, because
    /// colour is what the report spends on severity and these rows have none.
    /// </para>
    /// <para>
    /// The trailer is the failure and its location, and nothing else. No evidence level, because
    /// nothing was estimated; no id, because nothing is addressable; no population marker, because
    /// no rate was counted and a marker would assert a discounting decision that was never made.
    /// The location stays last so its left-truncated form never opens a line.
    /// </para>
    /// </remarks>
    private void WriteLatestRunFailure(StringBuilder builder, LatestRunFailureDto failure)
    {
        string status = ReportVocabulary.StatusWordsFor(failure.Status);
        string decorated = failure.Status == "new" ? capabilities.Emphasis(status) : status;

        // Padded against the plain word: escape codes occupy no columns, and padding to a length
        // that counted them would put the name short of its column on a terminal.
        builder.Append(' ', LatestRunIndent)
               .Append(decorated)
               .Append(' ', Math.Max(1, LatestRunStatusWidth - status.Length))
               .AppendLine(FitName(Name(failure.Subject), LatestRunBudget));

        foreach (string line in Wrap(failure.Contrast, LatestRunBudget))
            builder.Append(' ', LatestRunRowIndent).AppendLine(line);

        var trailer = new List<string>();

        if (failure.FailureSummary is { Length: > 0 } summary)
            trailer.Add(summary);

        if (failure.Subject.SourceFile is { Length: > 0 } file)
        {
            string location = failure.Subject.SourceLineNumber is { } line
                ? $"{file}:{line.ToString(CultureInfo.InvariantCulture)}"
                : file;

            // The path absorbs the truncation, for the reason the findings' trailer gives: Fit()
            // cuts from the left, and applied to the joined line it would eat the failure and leave
            // the one segment that can afford to lose its head untouched.
            int spent = trailer.Count == 0 ? 0 : trailer[0].Length + SeparatorWidth;

            if (FitPath(location, LatestRunBudget - spent) is { } fitted)
                trailer.Add(fitted);
        }

        if (trailer.Count == 0)
            return;

        builder.Append(' ', LatestRunRowIndent)
               .AppendLine(capabilities.Dim(Fit(string.Join(" | ", trailer), LatestRunBudget)));
    }

    /// <summary>
    /// Closes the section with the failures a finding already accounts for.
    /// </summary>
    /// <param name="latest">The section.</param>
    /// <param name="findings">The findings as rendered, whose positions are the row numbers.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// One line and not one row per test, so the section is a complete account of the run without
    /// listing the same test twice. The findings are named by the row numbers they hold below; a
    /// finding <c>--top</c> cut has no row and is not named, and where every one was cut the line
    /// says so — the truncation footer already names the command that shows them.
    /// </remarks>
    private static string AlsoFailing(LatestRunDto latest, IReadOnlyList<FindingDto> findings)
    {
        var rows = new List<string>();

        foreach (string id in latest.ExplainedByFindingIds)
        {
            for (int index = 0; index < findings.Count; index++)
            {
                if (string.Equals(findings[index].Id, id, StringComparison.Ordinal))
                {
                    rows.Add("#" + (index + 1).ToString(CultureInfo.InvariantCulture));
                    break;
                }
            }
        }

        string tests = Tests(latest.ExplainedByFindings);

        return rows.Count == 0
            ? $"Also failing: {tests} explained by findings not shown."
            : $"Also failing: {tests} explained by {(rows.Count == 1 ? "finding" : "findings")} below ({string.Join(", ", rows)}).";
    }

    /// <summary>
    /// Says why a report found nothing, which is three different things.
    /// </summary>
    /// <param name="summary">Counts describing the run as a whole.</param>
    /// <returns>The one line an empty report prints.</returns>
    /// <remarks>
    /// A suite with nothing wrong with it, a suite too young to say, and a suite whose candidates
    /// were all indistinguishable from chance are three different pieces of news, and printing "no
    /// findings" for the second and third teaches a reader that the report has looked when it has
    /// only declined to answer.
    /// </remarks>
    private string EmptyReport(SummaryDto summary)
    {
        var reasons = new List<string>();

        if (summary.ExcludedLowEvidence > 0)
            reasons.Add($"{summary.ExcludedLowEvidence} {NeedWord(summary.ExcludedLowEvidence)} more runs");

        if (summary.ExcludedNotSignificant > 0)
            reasons.Add($"{summary.ExcludedNotSignificant} could be chance");

        // Counted in kinds, not tests. The sentence is answering "why is this block empty", and the
        // answer is which questions went unasked; how many tests each of them covers is on the line
        // above, in the unit that line uses. A suite where every question was unanswerable used to
        // reach here and print a green "No findings.", which is the reading #185 was filed about.
        int silent = summary.NotMeasured.Count(entry => entry.Value.Unreadable > 0);

        if (silent > 0)
            reasons.Add($"{silent} {KindWord(silent)} had nothing to measure");

        return reasons.Count == 0
            ? $"{capabilities.Glyphs.Pass} No findings."
            : $"{capabilities.Glyphs.Pending} Nothing reportable yet: " +
              $"{string.Join(", ", reasons)}.";
    }

    /// <summary>
    /// Writes one finding: a header line, the name, the headline, and the dim trailer.
    /// </summary>
    /// <param name="builder">What the report is being written into.</param>
    /// <param name="finding">The finding.</param>
    /// <param name="row">Its position in the list, from 1.</param>
    /// <remarks>
    /// <para>
    /// The name has a line to itself and is never competed with for horizontal space. With the
    /// marker and the kind label in front of it the name had 49 of the fence's 72 columns, on the
    /// one line in the report that must not be truncated; it now has 68, which no
    /// <c>Class.Method</c> in practice reaches. The cost is one line per finding, and that line is
    /// the fix.
    /// </para>
    /// <para>
    /// The severity marker is the coloured element and the only one. It is what the colour encodes:
    /// a row number carries no severity, and a coloured name would say the identity was the thing
    /// worth ranking.
    /// </para>
    /// <para>
    /// The annotation is right-aligned to the fence, at the far end of the line the eye is already
    /// on. Measured against the plain text: the marker beside it may be wrapped in escape codes,
    /// which occupy no columns, and padding to a length that counted them would put the annotation
    /// short of the edge on a terminal and at it in a pipe.
    /// </para>
    /// </remarks>
    private void WriteFinding(StringBuilder builder, FindingDto finding, int row)
    {
        string number = row.ToString(CultureInfo.InvariantCulture) + ".";
        string marker = ReportVocabulary.MarkerFor(finding.Severity);
        string label = ReportVocabulary.LabelFor(finding.Kind);
        int spacing = Math.Max(1, RowLabelWidth - number.Length);

        builder.Append(number)
               .Append(' ', spacing)
               .Append(capabilities.Colorize(finding.Severity, marker))
               .Append("  ")
               .Append(label);

        if (finding.Annotation is { Length: > 0 } annotation)
        {
            int written = number.Length + spacing + marker.Length + 2 + label.Length;

            builder.Append(' ', Math.Max(1, FenceWidth - written - annotation.Length))
                   .Append(annotation);
        }

        builder.AppendLine();

        builder.Append(' ', ContinuationIndent)
               .AppendLine(FitName(Name(finding.Subject), ContinuationBudget));

        WriteMembers(builder, finding.Subject);

        foreach (string line in Wrap(finding.Headline, ContinuationBudget))
            builder.Append(' ', ContinuationIndent).AppendLine(line);

        // Between the evidence level and the id: the two say how much to believe the finding and
        // which finding it is, and which executions its rate was taken over belongs with the first.
        var trailer = new List<string>
        {
            $"evidence {finding.EvidenceLevel}",
            ReportVocabulary.PopulationTokenFor(finding.Population),
            finding.Id
        };

        const int budget = ContinuationBudget;

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
            // number string.Join adds once the location joins them: three segments here — evidence,
            // population, id — is three separators for the four the join sees. So the joined line
            // lands on the budget rather than three columns over it, whatever the segment count.
            if (FitPath(location, budget - spent) is { } fitted)
                trailer.Add(fitted);
        }

        builder.Append(' ', ContinuationIndent)
               .AppendLine(capabilities.Dim(Fit(string.Join(" | ", trailer), budget)));
    }

    /// <summary>
    /// Writes the lines that say what the population markers on each finding mean.
    /// </summary>
    /// <remarks>
    /// Below the fence, for the reason the truncation line is: it is about the report rather than
    /// about any finding in it, and inside the fence it would spend three of the seventy-two columns
    /// the findings need. Once for the whole report rather than once per finding — thirteen
    /// footnotes saying the same thing is how a reader learns to skip the end of the block.
    /// </remarks>
    private void WriteLegend(StringBuilder builder, ReportEnvelope envelope)
    {
        // A report with nothing in it printed no markers, so there is nothing to explain. The empty
        // report already says why it is empty, and a legend under it would be the only line there.
        if (envelope.Findings.Count == 0)
            return;

        builder.AppendLine();

        foreach (string line in ReportVocabulary.PopulationLegend)
            builder.AppendLine(capabilities.Dim(line));
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

    /// <summary>
    /// Lists the tests a cluster covers, beneath the cause they share.
    /// </summary>
    /// <param name="builder">What the report is being written into.</param>
    /// <param name="subject">The subject, which lists members only when it is a group.</param>
    /// <remarks>
    /// <para>
    /// The members are what makes a cluster navigable, and they are listed in every case — including
    /// the one where the evidence named no cause at all. A finding a reader cannot name and cannot
    /// open is a finding they skip.
    /// </para>
    /// <para>
    /// Three and then a count. The cause above is the line that is acted on; these say what it took
    /// down, and three of them establish that as well as forty do.
    /// </para>
    /// </remarks>
    private static void WriteMembers(StringBuilder builder, SubjectDto subject)
    {
        if (subject.Members is not { Count: > 0 } members)
            return;

        foreach (SubjectDto member in members.Take(MembersShown))
        {
            builder.Append(' ', MemberIndent)
                   .AppendLine(FitName(Name(member), FenceWidth - MemberIndent));
        }

        if (members.Count > MembersShown)
        {
            builder.Append(' ', MemberIndent)
                   .Append('+')
                   .Append((members.Count - MembersShown).ToString(CultureInfo.InvariantCulture))
                   .AppendLine(" more");
        }
    }

    /// <summary>
    /// Reads the line that says what a finding is about.
    /// </summary>
    /// <param name="subject">The subject, with its names already resolved.</param>
    /// <returns>The name, or the shared cause and how many tests carry it.</returns>
    /// <remarks>
    /// Read and not resolved. Every choice between a subject's recorded names was made in
    /// <c>EnvelopeBuilder</c>, which is what stopped this renderer preferring a prose display name
    /// to an identity and naming a cluster after whichever member happened to sort first.
    /// <para>
    /// Parentheses rather than a dash: they are ASCII in both glyph sets and need no
    /// <c>ReportGlyphs</c> pair of their own, and piped output is asserted to carry nothing
    /// above <c>0x7f</c>.
    /// </para>
    /// </remarks>
    private static string Name(SubjectDto subject)
    {
        if (subject.ShortName is { Length: > 0 } name)
            return name;

        string cause = subject.CauseLabel is { Length: > 0 } label ? label : "(cause not recorded)";

        return subject.MemberCount is { } members ? $"{cause} ({Tests(members)})" : cause;
    }

    private static string Tests(int count) =>
        count == 1 ? "1 test" : $"{count.ToString(CultureInfo.InvariantCulture)} tests";

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

    private static string NeedWord(int count) => count == 1 ? "needs" : "need";

    private static string KindWord(int count) => count == 1 ? "kind" : "kinds";

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
    /// Truncates a name from the left, never inside the method segment.
    /// </summary>
    /// <param name="value">The resolved name, which may carry an argument list.</param>
    /// <param name="width">Columns available.</param>
    /// <returns>The name, elided from the left where it did not fit.</returns>
    /// <remarks>
    /// <para>
    /// The method segment is the half a reader greps for and the half that identifies the test, so
    /// the class in front of it is what pays: <c>...Tests.VeryLongMethodName</c>. Where the method
    /// alone cannot fit either, it is emitted whole and the line overflows — half an identifier is
    /// unsearchable, and a few columns past the fence is the smaller harm. This is
    /// <see cref="Wrap"/>'s policy for an over-long word and <see cref="FitPath"/>'s for a single
    /// path segment, applied to the one line in the report that must stay usable.
    /// </para>
    /// <para>
    /// The dot is looked for in front of the argument list, never inside it. A parameterised NUnit
    /// case is recorded as <c>SampleTests.Add(1.5, 2)</c>, and the last dot in that string belongs
    /// to an argument.
    /// </para>
    /// </remarks>
    private static string FitName(string value, int width)
    {
        if (value.Length <= width)
            return value;

        int paren = value.IndexOf('(', StringComparison.Ordinal);
        int end = paren < 0 ? value.Length : paren;
        int dot = end == 0 ? -1 : value.LastIndexOf('.', end - 1);

        if (dot < 0)
            return value;

        // The ellipsis, at least one character of the class, the dot and the whole method segment.
        // Below that, Fit() would start eating the method, so the class goes entirely instead.
        return width >= value.Length - dot + 4
            ? Fit(value, width)
            : string.Concat("...", value.AsSpan(dot + 1));
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
