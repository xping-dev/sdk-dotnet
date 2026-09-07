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
    /// <param name="top">Findings to show, or <see langword="null"/> to show all of them.</param>
    /// <returns>The envelope.</returns>
    public static ReportEnvelope Build(
        AnalysisContext context,
        AnalysisResult result,
        int incompleteSessions,
        int unreadableSessions,
        int? top)
    {
        IReadOnlyList<Finding> shown = top is { } limit && limit < result.Findings.Count
            ? [.. result.Findings.Take(limit)]
            : result.Findings;

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
                Math.Max(0, tests - flagged.Count),
                result.ExcludedLowEvidence,
                result.ExcludedNotSignificant,
                NotMeasured(result),

                context.EnvironmentalSessionCount,
                context.PartialSessionCount,
                incompleteSessions,
                unreadableSessions,
                result.FailedProviders),
            [.. shown.Select(BuildFinding)],
            new TruncationDto(shown.Count, result.Findings.Count, DrillDown.ForFullReport()));
    }

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

    private static FindingDto BuildFinding(Finding finding)
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
            BuildSubject(finding.Subject),
            headline,
            metrics,
            BuildEvidence(finding.Evidence),
            finding.DrillDownCommand);
    }

    private static SubjectDto BuildSubject(FindingSubject subject) => subject switch
    {
        FindingSubject.SingleTest single => ForTest(single.Test),

        FindingSubject.Group group => new SubjectDto(
            "group",
            null, null, null, null, null, null,
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
