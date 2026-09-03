/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json.Nodes;

namespace Xping.Cli.Report.Contract;

/// <summary>
/// The complete output of one report, in the shape both renderers consume.
/// </summary>
/// <remarks>
/// <para>
/// Every display string is already resolved by the time an envelope exists. Renderers choose layout
/// and nothing else — no thresholds, no formatting decisions, no arithmetic. That is what keeps the
/// human report and the JSON output from ever disagreeing about the same run.
/// </para>
/// <para>
/// Property declaration order is the serialised property order, so it is part of the contract.
/// </para>
/// </remarks>
/// <param name="SchemaVersion">Version of this envelope's shape.</param>
/// <param name="Window">Which sessions were analysed, and how they were chosen.</param>
/// <param name="Context">Where those sessions came from; null when unknown, never invented.</param>
/// <param name="Summary">Counts describing the run as a whole.</param>
/// <param name="Findings">The findings, most severe first, after any truncation.</param>
/// <param name="Truncated">How much of the finding list is shown.</param>
internal sealed record ReportEnvelope(
    string SchemaVersion,
    WindowDto Window,
    ContextDto? Context,
    SummaryDto Summary,
    IReadOnlyList<FindingDto> Findings,
    TruncationDto Truncated)
{
    /// <summary>The shape this build emits.</summary>
    /// <remarks>
    /// Moves whenever anything a consumer reads changes shape, and the per-kind evidence payloads
    /// are part of that even though this document describes them as opaque: a script that reached
    /// into <c>evidence</c> for a field this build no longer emits is reading a contract, and
    /// leaving the number still would tell it nothing had moved. 1.10 is where the summary gained
    /// <c>excludedNotSignificant</c>, the candidates a kind's multiplicity correction silenced.
    /// </remarks>
    public const string CurrentSchemaVersion = "1.10";
}

/// <summary>
/// Which sessions were analysed, and how they were chosen.
/// </summary>
/// <param name="From">Start of the oldest analysed session.</param>
/// <param name="To">Start of the newest analysed session.</param>
/// <param name="SessionCount">Sessions analysed.</param>
/// <param name="Resolution">Which selection rule applied.</param>
/// <param name="ResolutionArgument">The flag value that produced it, when there was one.</param>
/// <param name="CurrentSliceSize">Sessions forming the "now" side of a delta.</param>
/// <param name="SessionIds">The analysed sessions, newest first.</param>
internal sealed record WindowDto(
    DateTime From,
    DateTime To,
    int SessionCount,
    string Resolution,
    string? ResolutionArgument,
    int CurrentSliceSize,
    IReadOnlyList<string> SessionIds);

/// <summary>
/// Where the analysed sessions came from.
/// </summary>
/// <remarks>
/// There is no <c>dirty</c> field. The SDK records whether changes are <i>staged</i>, which answers
/// a different question, and a field that is wrong whenever someone has unstaged edits is worse than
/// one that is absent.
/// </remarks>
/// <param name="Sha">Commit the newest analysed session ran at.</param>
/// <param name="Branch">Branch it ran on.</param>
/// <param name="Assembly">Test assembly the report covers.</param>
internal sealed record ContextDto(string? Sha, string? Branch, string? Assembly);

/// <summary>
/// Counts describing the run as a whole.
/// </summary>
/// <param name="Tests">Distinct tests seen in the window.</param>
/// <param name="Findings">Findings produced, before truncation.</param>
/// <param name="Counts">Those findings broken down by severity.</param>
/// <param name="Healthy">Tests no finding was raised about.</param>
/// <param name="ExcludedLowEvidence">Candidates dropped for resting on too little data.</param>
/// <param name="ExcludedNotSignificant">
/// Candidates dropped because their kind's comparison, charged for every fingerprint it ran on,
/// no longer said anything. A large suite over a short window silences most of what it tests, and a
/// reader given only an empty block cannot tell that from a suite with nothing to report.
/// </param>
/// <param name="EnvironmentalSessions">Sessions discounted as environment failures.</param>
/// <param name="IncompleteSessions">Sessions found but not finalised.</param>
/// <param name="UnreadableSessions">Session files that could not be read.</param>
/// <param name="FailedProviders">Metrics that threw and produced nothing.</param>
internal sealed record SummaryDto(
    int Tests,
    int Findings,
    SeverityCountsDto Counts,
    int Healthy,
    int ExcludedLowEvidence,
    int ExcludedNotSignificant,
    int EnvironmentalSessions,
    int IncompleteSessions,
    int UnreadableSessions,
    IReadOnlyList<string> FailedProviders);

/// <summary>
/// One finding, with every value already at its published precision.
/// </summary>
/// <param name="Id">Stable short identity.</param>
/// <param name="Kind">What the finding claims.</param>
/// <param name="Severity">How much attention it deserves.</param>
/// <param name="EvidenceLevel">How much data it rests on.</param>
/// <param name="Subject">The test or group it is about.</param>
/// <param name="Headline">The observations in one already-resolved sentence.</param>
/// <param name="Metrics">The same observations as labelled pairs, for a caller laying out its own.</param>
/// <param name="Evidence">The kind-specific observations.</param>
/// <param name="DrillDown">The command that expands it.</param>
internal sealed record FindingDto(
    string Id,
    string Kind,
    string Severity,
    string EvidenceLevel,
    SubjectDto Subject,
    string Headline,
    IReadOnlyList<MetricDto> Metrics,
    JsonNode? Evidence,
    string DrillDown);

/// <summary>
/// One labelled observation from a finding's evidence, already formatted.
/// </summary>
/// <remarks>
/// The pair exists so a caller can lay the numbers out itself — a table, a chat card — without
/// re-deriving them from <see cref="FindingDto.Evidence"/> and inventing a second phrasing for the
/// same measurement. <see cref="Value"/> is a string, not a number: it is presentation, and the
/// figure it was rounded from is in the evidence payload for anyone doing arithmetic.
/// </remarks>
/// <param name="Label">What was measured, in lower case.</param>
/// <param name="Value">The measurement, at published precision.</param>
internal sealed record MetricDto(string Label, string Value);

/// <summary>
/// Findings broken down by severity.
/// </summary>
/// <remarks>
/// Resolved here rather than tallied by each renderer, for the same reason every other value is:
/// two renderers counting the same list are two places the count can be wrong.
/// </remarks>
/// <param name="High">Findings at high severity.</param>
/// <param name="Medium">Findings at medium severity.</param>
/// <param name="Low">Findings at low severity.</param>
internal sealed record SeverityCountsDto(int High, int Medium, int Low);

/// <summary>
/// The test or group a finding is about.
/// </summary>
/// <param name="Type">Either <c>test</c> or <c>group</c>.</param>
/// <param name="Fingerprint">Stable identity, for a single-test subject.</param>
/// <param name="FullyQualifiedName">Namespace, class and method, for a single-test subject.</param>
/// <param name="DisplayName">Runner-facing name, for a single-test subject.</param>
/// <param name="SourceFile">Source path, when the SDK captured one.</param>
/// <param name="SourceLineNumber">Line the test begins on, when the SDK captured one.</param>
/// <param name="Assembly">Owning test assembly.</param>
/// <param name="GroupId">Cluster identity, for a group subject.</param>
/// <param name="MemberCount">Members in the cluster, for a group subject.</param>
/// <param name="Members">The member tests, for a group subject.</param>
internal sealed record SubjectDto(
    string Type,
    string? Fingerprint,
    string? FullyQualifiedName,
    string? DisplayName,
    string? SourceFile,
    int? SourceLineNumber,
    string? Assembly,
    string? GroupId,
    int? MemberCount,
    IReadOnlyList<SubjectDto>? Members);

/// <summary>
/// How much of the finding list is shown.
/// </summary>
/// <param name="Shown">Findings in <c>findings</c>.</param>
/// <param name="Total">Findings produced.</param>
/// <param name="Command">The invocation that shows all of them.</param>
internal sealed record TruncationDto(int Shown, int Total, string Command);
