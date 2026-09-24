/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report.Contract;

/// <summary>
/// Assembles the envelope both renderers consume.
/// </summary>
/// <remarks>
/// The single place values are rounded and strings resolved. Doing it here rather than at
/// serialisation is what lets the text and JSON output agree exactly: both read the same
/// already-rounded numbers, so neither can present a figure the other contradicts.
/// </remarks>
internal static class EnvelopeBuilder
{
    /// <summary>
    /// Builds the envelope for one report.
    /// </summary>
    /// <param name="context">The window and its derived indexes.</param>
    /// <param name="result">What the providers produced.</param>
    /// <param name="incompleteSessions">Sessions found but not finalised.</param>
    /// <param name="unreadableSessions">Session files that could not be read.</param>
    /// <param name="skewedSessions">Sessions stamped ahead of this machine's clock.</param>
    /// <param name="top">Findings to show, or <see langword="null"/> to show all of them.</param>
    /// <param name="latestRun">
    /// The newest session read against the rest, or <see langword="null"/> when the window holds
    /// no session.
    /// </param>
    /// <param name="id">
    /// The finding <c>--id</c> asked for, or <see langword="null"/> for the full report. When
    /// given, the findings shown are that one alone, or none; everything else is built exactly as
    /// the full report builds it.
    /// </param>
    /// <param name="scope">
    /// What the report was scoped to, which every command it prints repeats; by default the
    /// assembly the window covers and nothing the caller typed.
    /// </param>
    /// <returns>The envelope.</returns>
    public static ReportEnvelope Build(
        AnalysisContext context,
        AnalysisResult result,
        int incompleteSessions,
        int unreadableSessions,
        int skewedSessions,
        int? top,
        LatestRunAnalysis? latestRun,
        string? id = null,
        ReportScope? scope = null)
    {
        scope ??= new ReportScope(context.Revision?.Assembly);

        // Reordered before anything is cut. A sibling always follows the finding it points at, so a
        // limit either keeps both or drops the sibling — it can never leave a row saying "same test
        // as #4" in a report whose fourth row is something else.
        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(result.Findings);

        // Over this list and no other, so the row a selection reports is the row this envelope
        // numbers, and cannot come from an ordering computed somewhere else.
        FindingSelection? selection = id == null ? null : FindingSelector.Select(ordered, id);

        IReadOnlyList<Finding> shown = selection != null
            ? selection.Finding is { } selected ? [selected] : []
            : top is { } limit && limit < ordered.Count
                ? [.. ordered.Take(limit)]
                : ordered;

        Dictionary<string, string> annotations = Annotations(ordered);

        int tests = context.Tests.Fingerprints.Count;

        int high = 0;
        int medium = 0;
        int low = 0;

        // Tests named by any finding, deduplicated: one test can attract findings of several kinds,
        // and counting it as unhealthy more than once would make "healthy" go negative on a bad day.
        var flagged = new HashSet<string>(StringComparer.Ordinal);
        foreach (Finding finding in result.Findings)
        {
            foreach (TestReference test in finding.Subject.Tests)
                flagged.Add(test.TestFingerprint);

            // Counted over every finding produced, not over the truncated list: `--top` decides how
            // much is shown and must never decide what the report says it found.
            switch (finding.Severity)
            {
                case Severity.High:
                    high++;
                    break;
                case Severity.Medium:
                    medium++;
                    break;
                default:
                    low++;
                    break;
            }
        }

        return new ReportEnvelope(
            ReportEnvelope.CurrentSchemaVersion,
            BuildWindow(context.Window),
            BuildContext(context.Revision),
            new SummaryDto(
                tests,
                result.Findings.Count,
                new SeverityCountsDto(high, medium, low),
                flagged.Count,
                Math.Max(0, tests - flagged.Count),
                result.ExcludedLowEvidence,
                result.ExcludedNotSignificant,
                NotMeasured(result),

                context.EnvironmentalSessionCount,
                context.PartialSessionCount,
                incompleteSessions,
                unreadableSessions,
                skewedSessions,
                result.FailedProviders),
            latestRun == null ? null : BuildLatestRun(latestRun, ordered),
            selection == null ? null : BuildSelection(selection),
            [.. shown.Select(finding => BuildFinding(finding, annotations, scope))],
            new TruncationDto(shown.Count, result.Findings.Count, DrillDown.ForFullReport(scope)));
    }

    /// <summary>
    /// Projects the latest-run analysis into the envelope, with every sentence resolved.
    /// </summary>
    /// <param name="analysis">What the analyzer read from the newest session.</param>
    /// <param name="ordered">Every finding produced, in the order the envelope lists them.</param>
    /// <returns>The section, as both renderers will read it.</returns>
    private static LatestRunDto BuildLatestRun(LatestRunAnalysis analysis, IReadOnlyList<Finding> ordered)
    {
        TestSession session = analysis.Session.Session;

        // In envelope order and not in the order the analyzer met them, which was the order of
        // the tests they explain. The "also failing" line names them by row number, and a reader
        // expects "#1, #2, #4" rather than "#2, #1, #4".
        var position = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < ordered.Count; index++)
            position[ordered[index].Id] = index;

        List<string> explainedBy =
        [
            .. analysis.ExplainingFindings
                .Select(finding => finding.Id)
                .OrderBy(id => position.GetValueOrDefault(id, int.MaxValue))
                .ThenBy(id => id, StringComparer.Ordinal)
        ];

        return new LatestRunDto(
            session.SessionId.ToString("D", CultureInfo.InvariantCulture),
            session.StartedAt,
            RevisionContext.ReadSha(session),
            analysis.Session.IsLikelyEnvironmental,
            analysis.Suppressed,
            analysis.TestsExecuted,
            analysis.TestsFailed,
            analysis.NewFailures,
            analysis.ExplainedByFindings,
            explainedBy,
            [.. analysis.Failures.Select(BuildLatestRunFailure)],
            analysis.Failures.Count,
            analysis.FailuresTotal);
    }

    private static SelectionDto BuildSelection(FindingSelection selection) =>
        new(
            selection.Id,
            selection.Finding != null,
            selection.Kind?.ToString(),
            selection.Row,
            [.. selection.SameSubject.Select(same => new SameSubjectDto(same.Finding.Id, same.Finding.Kind.ToString(), same.Row))]);

    private static LatestRunFailureDto BuildLatestRunFailure(LatestRunFailure failure) =>
        new(
            ToCamelCase(failure.Status.ToString()),
            ForTest(failure.Test),
            Contrast(failure),
            FailureSummary(failure.Execution.ExceptionType),
            failure.PriorSessions,
            failure.PriorFailures);

    /// <summary>
    /// Names a failure in the fewest characters that identify it.
    /// </summary>
    /// <param name="exceptionType">The recorded type, or null.</param>
    /// <returns>The type without its namespace, or null when none was recorded.</returns>
    /// <remarks>
    /// The last segment after a dot, looked for in front of any generic argument list so that
    /// <c>Foo.Bar`1[System.String]</c> keeps its arguments and loses only its own namespace. A
    /// nested type keeps its <c>Outer+Inner</c> form, which is the type's own spelling.
    /// </remarks>
    private static string? FailureSummary(string? exceptionType)
    {
        if (exceptionType is not { Length: > 0 } type)
            return null;

        int generic = type.IndexOfAny(['`', '[']);
        int end = generic < 0 ? type.Length : generic;
        int dot = end == 0 ? -1 : type.LastIndexOf('.', end - 1);

        return dot < 0 ? type : type.Substring(dot + 1);
    }

    /// <summary>
    /// Phrases a row's history, which is the whole reason the row exists.
    /// </summary>
    /// <param name="failure">The row.</param>
    /// <returns>One sentence, in ASCII.</returns>
    /// <remarks>
    /// <para>
    /// ASCII for the reason <see cref="EvidenceHeadline"/> is: the glyph set is chosen after this
    /// runs and never reaches it, and piped output is asserted to carry nothing above 0x7F.
    /// </para>
    /// <para>
    /// A test seen before states its count and nothing else. An earlier draft added "too few to
    /// classify yet", which names an evidence gate — and a test with twenty runs and eight failures
    /// whose hypothesis lost the multiplicity correction, or whose provider threw, has cleared
    /// every gate there is. The row cannot know why no finding exists and must not guess.
    /// </para>
    /// <para>
    /// Its runs are counted inclusive of this one, because that is the history being handed to the
    /// reader. A new failure is counted over the runs before this one, because "passed the previous
    /// N runs" is a statement about those runs alone. Neither counts a run the environmental
    /// heuristic flagged.
    /// </para>
    /// </remarks>
    private static string Contrast(LatestRunFailure failure) => failure.Status switch
    {
        LatestRunStatus.New => failure.PriorSessions == 1
            ? "passed the previous run, failed just now"
            : $"passed the previous {failure.PriorSessions.ToString(CultureInfo.InvariantCulture)} runs, failed just now",

        LatestRunStatus.NewTest => "first seen this run, failed",

        LatestRunStatus.SeenBefore =>
            $"failed {(failure.PriorFailures + 1).ToString(CultureInfo.InvariantCulture)} of " +
            $"{(failure.PriorSessions + 1).ToString(CultureInfo.InvariantCulture)} runs",

        _ => throw new NotSupportedException($"Unknown status '{failure.Status}'.")
    };

    /// <summary>
    /// Projects the per-kind not-measured tally into the envelope's spelling.
    /// </summary>
    /// <param name="result">What the providers produced.</param>
    /// <returns>One entry per kind that keeps such a tally, in <see cref="FindingKind"/> order.</returns>
    /// <remarks>
    /// Enumerated over the enum rather than over the dictionary, because a dictionary's order is
    /// its insertion order and that is provider execution order — which the coordinator already
    /// sorts, but only by provider name. Two runs over an unchanged store have to serialise
    /// byte-identically, and taking the order from the declaration is what makes that a property of
    /// the code rather than of a hash.
    /// <para>
    /// Keyed by <c>nameof</c> rather than by the enum. The registered converter camel-cases enum
    /// values while <c>JsonSerializerOptions.DictionaryKeyPolicy</c> is unset, so a
    /// <see cref="FindingKind"/> key would serialise as <c>durationRegression</c> and disagree with
    /// the <c>DurationRegression</c> that every finding's <c>kind</c> and every <c>--kind</c>
    /// argument spell.
    /// </para>
    /// </remarks>
    private static Dictionary<string, NotMeasuredDto> NotMeasured(AnalysisResult result)
    {
        var published = new Dictionary<string, NotMeasuredDto>(StringComparer.Ordinal);

        foreach (FindingKind kind in Enum.GetValues<FindingKind>())
        {
            if (result.NotMeasured.TryGetValue(kind, out NotMeasuredCount count))
                published[kind.ToString()] = new NotMeasuredDto(count.AwaitingRuns, count.Unreadable);
        }

        return published;
    }

    private static WindowDto BuildWindow(AnalysisWindow window) =>
        new(
            window.From,
            window.To,
            window.SessionCount,
            ToCamelCase(window.Resolution.ToString()),
            window.ResolutionArgument,
            window.CurrentSliceSize,
            [.. window.Sessions.Select(s => s.SessionId.ToString("D", CultureInfo.InvariantCulture))]);

    private static ContextDto? BuildContext(RevisionContext? revision) =>
        revision == null ? null : new ContextDto(revision.Sha, revision.Branch, revision.Assembly);

    /// <summary>
    /// Says, of each finding that is not the first about its test, which row the first one is.
    /// </summary>
    /// <param name="ordered">Every finding produced, already adjacent.</param>
    /// <returns>The annotation for each finding that has one, keyed by finding id.</returns>
    /// <remarks>
    /// Rows are numbered from 1 over the whole ordered list, which is what the renderer prints, so
    /// the number is right whether or not the report was truncated — the reordering guarantees an
    /// anchor is shown wherever its sibling is. Phrased here for the reason every other display
    /// string is: a renderer that worded it would be the second place the relationship is described.
    /// </remarks>
    internal static Dictionary<string, string> Annotations(IReadOnlyList<Finding> ordered)
    {
        var anchors = new Dictionary<string, int>(StringComparer.Ordinal);
        var annotations = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int index = 0; index < ordered.Count; index++)
        {
            if (FindingOrder.Test(ordered[index]) is not { } test)
                continue;

            if (anchors.TryGetValue(test, out int anchor))
                annotations[ordered[index].Id] =
                    $"same test as #{anchor.ToString(CultureInfo.InvariantCulture)}";
            else
                anchors[test] = index + 1;
        }

        return annotations;
    }

    private static FindingDto BuildFinding(
        Finding finding, Dictionary<string, string> annotations, ReportScope scope)
    {
        (string headline, IReadOnlyList<MetricDto> metrics) =
            EvidenceHeadline.For(finding.Kind, finding.Evidence);

        return new FindingDto(
            finding.Id,
            finding.Kind.ToString(),
            ToCamelCase(finding.Severity.ToString()),
            ToCamelCase(finding.EvidenceLevel.ToString()),
            finding.EvidenceSessions,
            ToCamelCase(PopulationRules.For(finding.Kind).ToString()),
            BuildSubject(finding.Subject, finding.Evidence),
            annotations.GetValueOrDefault(finding.Id),
            headline,
            metrics,
            BuildEvidence(finding.Evidence),
            DrillDown.ForFinding(finding.Id, scope));
    }

    /// <summary>
    /// Projects a finding's subject into the envelope.
    /// </summary>
    /// <param name="subject">The test or group the finding is about.</param>
    /// <param name="evidence">
    /// That finding's evidence, which is where a cluster's cause is recorded. Passed in because a
    /// subject does not carry one: a group knows which tests it holds and not what they share.
    /// </param>
    /// <returns>The subject, with every name it is presented under already resolved.</returns>
    /// <remarks>
    /// No subject carries both names. A single test has a <c>shortName</c> and no cause; a group has
    /// a cause and no name of its own, and its members each carry theirs.
    /// </remarks>
    private static SubjectDto BuildSubject(FindingSubject subject, FindingEvidence evidence) =>
        subject switch
        {
            FindingSubject.SingleTest single => ForTest(single.Test),

            FindingSubject.Group group => new SubjectDto(
                "group",
                null, null, null, null,
                SubjectNames.CauseLabel(evidence),
                null, null, null,
                group.GroupId,
                group.Members.Count,
                [.. group.Members.Select(ForTest)]),

            _ => throw new NotSupportedException($"Unknown subject type '{subject.GetType().Name}'.")
        };

    private static SubjectDto ForTest(TestReference test) =>
        new(
            "test",
            test.TestFingerprint,
            test.FullyQualifiedName,
            test.DisplayName,

            // The name the report shows, resolved here so that no renderer chooses between the two
            // above it. Both are kept beside it: this one is deliberately lossy, and a consumer that
            // wants the whole identity must not have to reassemble it.
            SubjectNames.ShortName(test.FullyQualifiedName, test.DisplayName),
            null,

            // Never stripped for brevity. These two are what let an agent open the file rather than
            // go searching for a name.
            test.SourceFile,
            test.SourceLineNumber,
            test.Assembly,
            null, null, null);

    /// <summary>
    /// Serialises a kind-specific evidence payload into the envelope.
    /// </summary>
    /// <remarks>
    /// Serialised against its runtime type so each kind contributes its own fields with no type
    /// discriminator, matching the output contract. Property order follows the evidence record's
    /// declaration order, which keeps the bytes stable between runs.
    /// </remarks>
    private static JsonNode? BuildEvidence(FindingEvidence evidence) =>
        JsonSerializer.SerializeToNode(
            evidence, evidence.GetType(), ReportJsonOptions.Default);

    /// <summary>
    /// Lower-cases the first character of an enum name for the JSON contract.
    /// </summary>
    private static string ToCamelCase(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);
}
