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
/// <param name="LatestRun">
/// The newest session read against the ones before it; null only when the window holds no session.
/// Positioned before the findings because that is where it renders: the reader ran the command
/// because something just happened.
/// </param>
/// <param name="Selection">
/// What <c>--id</c> resolved to; null unless one was given. Positioned before the findings, which it
/// qualifies: under <c>--id</c> they hold the selected finding alone, or nothing when it is not
/// reported.
/// </param>
/// <param name="Findings">The findings, most severe first, after any truncation.</param>
/// <param name="Truncated">How much of the finding list is shown.</param>
internal sealed record ReportEnvelope(
    string SchemaVersion,
    WindowDto Window,
    ContextDto? Context,
    SummaryDto Summary,
    LatestRunDto? LatestRun,
    SelectionDto? Selection,
    IReadOnlyList<FindingDto> Findings,
    TruncationDto Truncated)
{
    /// <summary>The shape this build emits.</summary>
    /// <remarks>
    /// Moves whenever anything a consumer reads changes shape, and the per-kind evidence payloads
    /// are part of that even though this document describes them as opaque: a script that reached
    /// into <c>evidence</c> for a field this build no longer emits is reading a contract, and
    /// leaving the number still would tell it nothing had moved. 1.14 is where every finding gained
    /// <c>evidenceSessions</c> — the denominator <c>evidenceLevel</c> is banded from, which is the
    /// runs the finding was computed over rather than the runs its test appeared in. 1.16 is where
    /// both arms of a <c>DurationRegression</c> gained <c>comparedDispersion</c>, the spread of the
    /// runs that side of the comparison read — published because the p-value beside it is not
    /// calibrated against a difference in spread between the two. 1.17 is where the summary gained
    /// <c>skewedSessions</c>, the runs stamped ahead of this machine's clock and therefore left out
    /// of the window rather than allowed to date it. 1.18 is where <c>population</c> gained
    /// <c>excludesPartialRuns</c>, the rule <c>Vanished</c> had been applying since 1.13 while
    /// still publishing <c>allExecutions</c> — a consumer switching on the value sees a fourth one.
    /// 1.19 is where a subject gained <c>shortName</c>, the identity a reader greps for and pastes
    /// into <c>dotnet test --filter</c>, and <c>causeLabel</c>, what the members of a cluster have in
    /// common — both resolved here rather than by a renderer, which is what lets the rendered report
    /// name a test the way its author would; and where the summary gained <c>flagged</c>, the count
    /// <c>healthy</c> had been the complement of without ever saying so. 1.19 also reorders
    /// <c>findings</c> so that two findings about one test are adjacent, and gives the second of
    /// them an <c>annotation</c> saying which row the first is. 1.20 is where the envelope gained
    /// <c>latestRun</c>: the newest session read against the sessions before it, which is the one
    /// question the evidence-gated findings cannot answer and the first thing a reader who just
    /// watched a build go red wants answered. 1.21 is where the envelope gained <c>selection</c>,
    /// what <c>xping report --id</c> resolved to; where <c>drillDown</c> became that command for the
    /// finding's own id; and where a <c>Flaky</c> finding's <c>metrics</c> gained one
    /// <c>failure mode N</c> pair per distinct signature, naming the ways it failed.
    /// </remarks>
    public const string CurrentSchemaVersion = "1.21";
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
/// The newest session, read against the sessions before it.
/// </summary>
/// <remarks>
/// <para>
/// Not a finding and not graded like one. A row here is one session's outcome beside a count of
/// prior ones: no rate, no threshold, no correction for how many tests were looked at, and
/// therefore no severity and no population marker. It sits beside the findings so the report can
/// say what just broke as well as what is chronically unreliable, without lowering the gates that
/// keep the second claim honest.
/// </para>
/// <para>
/// Always present when the window holds a session, so that a consumer can tell <i>nothing
/// failed</i> from <i>not itemised</i>: <see cref="Suppressed"/> says the window held one session
/// and had no history to contrast, <see cref="IsLikelyEnvironmental"/> says the session failed too
/// widely to list test by test, and <see cref="FailuresTotal"/> against <see cref="FailuresShown"/>
/// says how many rows the cap withheld.
/// </para>
/// </remarks>
/// <param name="SessionId">The newest analysed session.</param>
/// <param name="StartedAt">When it started. Rendered, so a stale store is visible.</param>
/// <param name="Sha">Commit it ran at, when one was recorded. Never invented.</param>
/// <param name="IsLikelyEnvironmental">Whether the session itself is suspect.</param>
/// <param name="Suppressed">True when the window holds this session alone.</param>
/// <param name="TestsExecuted">Tests that recorded a verdict in this session.</param>
/// <param name="TestsFailed">Tests whose final outcome was a failure.</param>
/// <param name="NewFailures">
/// Rows that are <c>new</c> or <c>newTest</c>, counted before the cap — what the heading says and
/// what a one-line summary reads. Not <paramref name="FailuresTotal"/>: a test that had failed
/// before is listed, but it is not news.
/// </param>
/// <param name="ExplainedByFindings">Failing tests that a finding already accounts for.</param>
/// <param name="ExplainedByFindingIds">Those findings' ids, in envelope order.</param>
/// <param name="Failures">The rows, ordered for reading, after the cap.</param>
/// <param name="FailuresShown">Rows in <paramref name="Failures"/>.</param>
/// <param name="FailuresTotal">
/// Rows before the cap. Where it exceeds <paramref name="FailuresShown"/>, the envelope's
/// <c>truncated.command</c> is what shows the rest: <c>--all</c> lifts this cap and the findings'
/// together, and the report prints the one command once.
/// </param>
internal sealed record LatestRunDto(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    bool IsLikelyEnvironmental,
    bool Suppressed,
    int TestsExecuted,
    int TestsFailed,
    int NewFailures,
    int ExplainedByFindings,
    IReadOnlyList<string> ExplainedByFindingIds,
    IReadOnlyList<LatestRunFailureDto> Failures,
    int FailuresShown,
    int FailuresTotal);

/// <summary>
/// One test that ended the newest session red and carries no finding.
/// </summary>
/// <param name="Status"><c>new</c>, <c>newTest</c> or <c>seenBefore</c>.</param>
/// <param name="Subject">
/// The test. Always a single-test subject with its <c>shortName</c> resolved and no cause: a row
/// is one test's outcome in one session, and there is no group it could be about.
/// </param>
/// <param name="Contrast">
/// The history beside the outcome, as one already-resolved sentence. This is the row's whole
/// value — the outcome alone is what the test runner printed — and it is composed here and
/// nowhere else, so that a renderer never phrases it and a stronger statement of the same history
/// can replace it without a renderer changing.
/// </param>
/// <param name="FailureSummary">
/// What went wrong, in the fewest words that identify it: the exception type with its namespace
/// stripped, or null when the adapter recorded none. The findings' headline keeps the namespace
/// because there the type is a failure <i>mode</i> a reader groups on; here it is a label beside a
/// location, and the namespace is the part that pushes the location off the line.
/// </param>
/// <param name="PriorSessions">Sessions before this one in which the test recorded a verdict.</param>
/// <param name="PriorFailures">Of those, how many it failed in.</param>
internal sealed record LatestRunFailureDto(
    string Status,
    SubjectDto Subject,
    string Contrast,
    string? FailureSummary,
    int PriorSessions,
    int PriorFailures);

/// <summary>
/// Counts describing the run as a whole.
/// </summary>
/// <param name="Tests">Distinct tests seen in the window.</param>
/// <param name="Findings">Findings produced, before truncation.</param>
/// <param name="Counts">Those findings broken down by severity.</param>
/// <param name="Flagged">
/// Tests named by at least one finding, counted once each. Not <paramref name="Findings"/> and not
/// derivable from it in either direction: one test attracts findings of several kinds, and one
/// finding about a cluster covers every test in it. Published because it is the number
/// <paramref name="Healthy"/> is the complement of — without it, a reader given three findings and
/// four unhealthy tests out of sixteen has to reconstruct a deduplication they cannot see, and the
/// two counts read as an arithmetic error.
/// </param>
/// <param name="Healthy">
/// Tests no finding was raised about. Not "tests that were checked and are fine": a test a metric
/// could not be computed for is in here too, and <paramref name="NotMeasured"/> is what says so.
/// The unmeasured are deliberately not subtracted — which of them there are depends on which kinds
/// were asked for, and a headline count that moves with <c>--kind</c> would be worse than one that
/// needs a second number read beside it.
/// </param>
/// <param name="ExcludedLowEvidence">Candidates dropped for resting on too little data.</param>
/// <param name="ExcludedNotSignificant">
/// Candidates dropped because their kind's comparison, charged for every fingerprint it ran on,
/// no longer said anything. A large suite over a short window silences most of what it tests, and a
/// reader given only an empty block cannot tell that from a suite with nothing to report.
/// </param>
/// <param name="NotMeasured">
/// Per kind, the tests that kind could not be measured on at all, keyed by the kind as
/// <c>findings[].kind</c> and <c>--kind</c> spell it.
/// <para>
/// Not a total and cannot be made into one. Adding the values counts a test once per question its
/// data could not answer; a reader wanting "how much of my suite could this metric read" has to
/// read one entry. The four kinds absent from the map keep no such tally — <c>SharedFailure</c> and
/// <c>BrokenFixture</c> are counted in signature groups rather than tests, and the retry kinds are
/// decided by a chain in which a later kind is never attempted once an earlier one fires. A present
/// zero is a real statement: the kind was offered tests and read every one.
/// </para>
/// <para>
/// Distinct from <paramref name="ExcludedLowEvidence"/>, which counts candidates a provider offered
/// and the report then withheld. These are tests no candidate ever existed for.
/// </para>
/// </param>
/// <param name="EnvironmentalSessions">Sessions discounted as environment failures.</param>
/// <param name="PartialSessions">
/// Sessions that covered only part of the suite — a <c>dotnet test --filter</c> run, or anything
/// else that ran a fraction of the tests the window's largest run did. An observation and not a
/// discount: only the kinds that read absence set such a session aside, because a filtered run's
/// outcomes are as true as any other run's and it is only its silences that mean nothing.
/// </param>
/// <param name="IncompleteSessions">Sessions found but not finalised.</param>
/// <param name="UnreadableSessions">Session files that could not be read.</param>
/// <param name="SkewedSessions">
/// Sessions left out because they are stamped ahead of this machine's clock — a run recorded by a
/// clock that disagrees with this one, which the window cannot date and must not be bounded by.
/// <para>
/// Zero does not mean no clock disagrees. Where <em>every</em> recorded run is ahead of this
/// machine, one clock wrote the whole store, the spacing between its sessions is intact and none is
/// excluded; the report is dated against that clock and says so on standard error.
/// </para>
/// </param>
/// <param name="FailedProviders">Metrics that threw and produced nothing.</param>
internal sealed record SummaryDto(
    int Tests,
    int Findings,
    SeverityCountsDto Counts,
    int Flagged,
    int Healthy,
    int ExcludedLowEvidence,
    int ExcludedNotSignificant,
    IReadOnlyDictionary<string, NotMeasuredDto> NotMeasured,
    int EnvironmentalSessions,
    int PartialSessions,
    int IncompleteSessions,
    int UnreadableSessions,
    int SkewedSessions,
    IReadOnlyList<string> FailedProviders);

/// <summary>
/// Why one kind could not be measured on some of the tests it was offered.
/// </summary>
/// <remarks>
/// Two numbers because only one of them is answered by waiting. A test the comparison needs seven
/// runs of and has four will have seven; a test whose every run recorded a zero median normalises
/// nothing, and another such run normalises nothing either. A script deciding whether to keep
/// collecting reads the first; a developer deciding whether their adapter is recording what the
/// metric needs reads the second.
/// </remarks>
/// <param name="AwaitingRuns">Tests the kind needs more runs of.</param>
/// <param name="Unreadable">Tests whose recorded data cannot answer this kind's question.</param>
internal sealed record NotMeasuredDto(int AwaitingRuns, int Unreadable);

/// <summary>
/// One finding, with every value already at its published precision.
/// </summary>
/// <param name="Id">Stable short identity.</param>
/// <param name="Kind">What the finding claims.</param>
/// <param name="Severity">How much attention it deserves.</param>
/// <param name="EvidenceLevel">How much data it rests on.</param>
/// <param name="EvidenceSessions">
/// Independent runs this finding was computed from — what <paramref name="EvidenceLevel"/> was
/// banded from. Not the window's run count, and not the runs the test appeared in: a kind that
/// discounts environmental runs, or that cannot read a run at all, measured over fewer.
/// </param>
/// <param name="Population">
/// Which executions — or, where a kind counts appearances rather than outcomes, which runs — the
/// rate below was counted out of. The report ranks findings of different kinds against each other
/// and the kinds do not all count the same population, so two rates on one screen are comparable
/// only where this agrees.
/// <para>
/// It qualifies the denominator the rate is taken over, and not every figure in
/// <paramref name="Evidence"/> beside it. A <c>Vanished</c> finding counts its appearances over the
/// runs that covered the suite while its <c>executionsInWindow</c> stays a whole-window count with
/// nothing set aside, because the two answer different questions and folding them under one marker
/// would make it mean less rather than more.
/// </para>
/// </param>
/// <param name="Subject">The test or group it is about.</param>
/// <param name="Annotation">
/// What connects this finding to the one above it, or null where nothing does. Today that is only
/// <c>same test as #4</c>: one test can carry findings of several kinds, and the ranking used to
/// leave them four rows apart with nothing saying they were the same test. Resolved here rather
/// than by a renderer, like every other display string on this envelope, and carried in the JSON so
/// a consumer reading the list in order sees the same relationship.
/// </param>
/// <param name="Headline">The observations in one already-resolved sentence.</param>
/// <param name="Metrics">The same observations as labelled pairs, for a caller laying out its own.</param>
/// <param name="Evidence">The kind-specific observations.</param>
/// <param name="DrillDown">The command that expands it.</param>
internal sealed record FindingDto(
    string Id,
    string Kind,
    string Severity,
    string EvidenceLevel,
    int EvidenceSessions,
    string Population,
    SubjectDto Subject,
    string? Annotation,
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
/// <param name="ShortName">
/// The identity a reader is shown: <c>Class.Method</c>, with the adapter's own argument list where
/// the case is parameterised. Resolved here, once, so that no renderer performs string surgery on a
/// name — and taken from <paramref name="FullyQualifiedName"/> rather than from
/// <paramref name="DisplayName"/>, because it has to be greppable and has to survive being pasted
/// after <c>dotnet test --filter FullyQualifiedName~</c>. Null for a group subject.
/// </param>
/// <param name="CauseLabel">
/// What the members of a cluster have in common — the broken lifecycle member, the site it failed
/// at, or the exception type they share. Null for a single-test subject. Never
/// <paramref name="GroupId"/>: that is a signature hash, and a hash presented as a cause is worse
/// than saying the cause was not recorded.
/// </param>
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
    string? ShortName,
    string? CauseLabel,
    string? SourceFile,
    int? SourceLineNumber,
    string? Assembly,
    string? GroupId,
    int? MemberCount,
    IReadOnlyList<SubjectDto>? Members);

/// <summary>
/// What <c>xping report --id</c> resolved to.
/// </summary>
/// <remarks>
/// Present whether or not the finding is reported, so that a consumer keyed on
/// <see cref="Reported"/> never has to read standard error to learn the answer.
/// </remarks>
/// <param name="Id">The id as requested, normalised to lower case.</param>
/// <param name="Reported">Whether a finding with this id is in the report.</param>
/// <param name="Kind">
/// The kind the id names: the finding's own when reported; the kind the reverse lookup established
/// when not; null when the lookup found nothing.
/// </param>
/// <param name="Row">
/// The finding's position, from 1, in the full ordered list — the number the full report prints
/// for it. Null when not reported.
/// </param>
/// <param name="SameSubject">
/// Every other finding about the subject this id names, in envelope order. When reported, its
/// siblings; when not, where the claim went. Empty when there are none or the subject is unknown.
/// </param>
internal sealed record SelectionDto(
    string Id,
    bool Reported,
    string? Kind,
    int? Row,
    IReadOnlyList<SameSubjectDto> SameSubject);

/// <summary>
/// Another finding about the subject a selection names.
/// </summary>
/// <param name="Id">The finding's id.</param>
/// <param name="Kind">Its kind, spelled as <c>findings[].kind</c>.</param>
/// <param name="Row">Its position in the full ordered list, from 1.</param>
internal sealed record SameSubjectDto(string Id, string Kind, int Row);

/// <summary>
/// How much of the finding list is shown.
/// </summary>
/// <param name="Shown">Findings in <c>findings</c>.</param>
/// <param name="Total">Findings produced.</param>
/// <param name="Command">The invocation that shows all of them.</param>
internal sealed record TruncationDto(int Shown, int Total, string Command);
