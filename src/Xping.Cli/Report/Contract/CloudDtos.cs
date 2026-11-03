/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Contract;

// The Cloud layer of the envelope (cloud-data-in-cli-cli-spec §6). Every enum is the string Cloud
// sent, written verbatim: the CLI must print values it does not know (contract §4). These mirror the
// wire records in Cloud/Digest but are their own types, because the envelope is a documented
// contract that must not move when the wire does.

/// <summary>
/// What the report asked Xping Cloud, with which credential, and how it went.
/// </summary>
/// <remarks>
/// Present on every enriched report, including one without a credential (<c>not-attempted</c>): an
/// agent always finds the object and reads <paramref name="Status"/> first, <paramref name="Reason"/>
/// second (contract §8.1, R5).
/// </remarks>
/// <param name="Status">
/// <c>ok</c>, <c>partial</c>, <c>unavailable</c>, <c>login-required</c>, <c>forbidden</c>,
/// <c>no-project</c> or <c>not-attempted</c>.
/// </param>
/// <param name="Reason">
/// <c>none</c>, <c>credential-timeout</c>, <c>budget</c>, <c>timeout</c>, <c>network</c>,
/// <c>server</c>, <c>rate-limited</c>, <c>invalid-response</c>, <c>not-found</c> or
/// <c>signature-algorithm</c>.
/// </param>
/// <param name="ElapsedMs">How long the Cloud layer took; 0 when nothing was asked.</param>
/// <param name="CloudUrl">The normalized Xping Cloud URL, or null when the settings could not be read.</param>
/// <param name="Credential"><c>login</c> or <c>api-key</c>: the credential last used; null without one.</param>
/// <param name="WorkspaceId">The signed-in workspace, or null for an API key.</param>
/// <param name="Workspace">
/// That workspace's name, from the access token; null for an API key or a token without the claim.
/// </param>
/// <param name="FetchedAt">When the answers were read, by this machine's clock; null when none came.</param>
/// <param name="SignatureAlgorithm">The signature algorithm Cloud answered with, or null.</param>
/// <param name="RetentionDays">The workspace plan's retention, or null.</param>
/// <param name="RequestedTests">Tests asked about.</param>
/// <param name="EnrichedTests">Of those, the tests Cloud answered for, known to it or not.</param>
/// <param name="Projects">One row per assembly asked about, in target order.</param>
/// <param name="Sessions">What Cloud knows of this machine's newest sessions.</param>
internal sealed record CloudContextDto(
    string Status,
    string Reason,
    long ElapsedMs,
    string? CloudUrl,
    string? Credential,
    string? WorkspaceId,
    string? Workspace,
    DateTimeOffset? FetchedAt,
    string? SignatureAlgorithm,
    int? RetentionDays,
    int RequestedTests,
    int EnrichedTests,
    IReadOnlyList<CloudProjectDto> Projects,
    IReadOnlyList<CloudSessionDto> Sessions);

/// <summary>
/// The Cloud project one assembly was bound to, and how reading it went.
/// </summary>
/// <param name="Assembly">The test assembly.</param>
/// <param name="ProjectKey">The Cloud's project key, or null when none was found.</param>
/// <param name="Source">
/// How the key was found: <c>flag</c>, <c>env</c>, <c>config</c>, <c>session</c> or
/// <c>name-match</c>; null when none was found.
/// </param>
/// <param name="Status">The same vocabulary as <see cref="CloudContextDto.Status"/>.</param>
/// <param name="Reason">The same vocabulary as <see cref="CloudContextDto.Reason"/>.</param>
internal sealed record CloudProjectDto(string Assembly, string? ProjectKey, string? Source, string Status, string Reason);

/// <summary>
/// What Cloud knows of one session this machine recorded (contract §6.1, §9.1).
/// </summary>
/// <param name="SessionKey">The session's key.</param>
/// <param name="ProjectKey">The project that answered for it.</param>
/// <param name="State"><c>unknown</c>, <c>pending</c>, <c>processing</c>, <c>complete</c>, <c>abandoned</c> or <c>failed</c>.</param>
/// <param name="Scored">Whether every upload of it has been scored.</param>
/// <param name="FlakyExamined">Whether every upload of it has passed flaky detection.</param>
internal sealed record CloudSessionDto(string SessionKey, string ProjectKey, string State, bool Scored, bool FlakyExamined);

/// <summary>
/// Xping Cloud's view of one test (contract §6.2), with what the CLI adds to it.
/// </summary>
/// <remarks>
/// A test Cloud does not know in the project is <paramref name="State"/> <c>unknown</c> with every
/// block null, so a reader can tell it from a test that was never asked about (<c>cloud: null</c>).
/// </remarks>
/// <param name="State"><c>ok</c> or <c>unknown</c>.</param>
/// <param name="FetchedAt">When this machine read it (contract §4.5).</param>
/// <param name="DisagreesWithLocal">
/// Whether a local <c>Flaky</c> or <c>AlwaysFailing</c> finding reads differently in Cloud
/// (contract L3). Only ever true on a finding.
/// </param>
/// <param name="Identity">
/// Null, <c>name-mismatch</c> when Cloud names the fingerprint differently, or <c>shared</c> when
/// several local tests carry it (contract §9.3).
/// </param>
/// <param name="Verdict">
/// <c>known-flaky</c>, <c>broken</c>, <c>unstable</c>, <c>passing</c> or <c>no-history</c>.
/// </param>
/// <param name="TestName">The name Cloud has for the fingerprint.</param>
/// <param name="LastExecutedAt">Cloud's newest execution of the test.</param>
/// <param name="TotalRuns">Cloud's lifetime run count: the one number not bound to a window.</param>
/// <param name="Confidence">The newest overall score, or null.</param>
/// <param name="Flaky">The flaky ledger's state, or null.</param>
/// <param name="Canonical">The newest canonical runs, or null without a canonical history.</param>
/// <param name="LastTransition">The newest state transition, or null.</param>
/// <param name="Signatures">The requested signatures and the most frequent ones, as Cloud sent them.</param>
internal sealed record CloudDigestDto(
    string State,
    DateTimeOffset FetchedAt,
    bool DisagreesWithLocal,
    string? Identity,
    string? Verdict,
    string? TestName,
    DateTimeOffset? LastExecutedAt,
    int? TotalRuns,
    CloudConfidenceDto? Confidence,
    CloudFlakyDto? Flaky,
    CloudCanonicalDto? Canonical,
    CloudTransitionDto? LastTransition,
    IReadOnlyList<CloudSignatureDto> Signatures);

/// <summary>
/// Cloud's confidence score for a test and the evidence behind it.
/// </summary>
/// <param name="State"><c>scored</c>, <c>insufficient-evidence</c> or <c>not-scored</c>.</param>
/// <param name="Score">The score, 0 to 1.</param>
/// <param name="LowerBound">Its lower bound.</param>
/// <param name="UpperBound">Its upper bound.</param>
/// <param name="Category">Its category, such as <c>moderately-reliable</c>.</param>
/// <param name="Trend">Its trend, such as <c>minor-degradation</c>.</param>
/// <param name="Delta">Its recent change.</param>
/// <param name="EffectiveSampleSize">The effective sample size the evidence level is banded from.</param>
/// <param name="EvidenceLevel">
/// Cloud's evidence scale (<c>sparse</c>, <c>adequate</c>, <c>robust</c>, <c>no-data</c>); never the
/// local one (contract §4.8).
/// </param>
/// <param name="CanonicalRuns">Canonical runs in the evidence window.</param>
/// <param name="SpeculativeRuns">Speculative runs in the evidence window.</param>
/// <param name="UntrackedRuns">Untracked runs in the evidence window.</param>
/// <param name="Window">The window the score covers.</param>
/// <param name="EvidenceWindow">The window the evidence covers.</param>
/// <param name="ComputedAt">When Cloud computed it.</param>
/// <param name="Freshness"><c>current</c>, <c>pending</c> or <c>none</c>.</param>
internal sealed record CloudConfidenceDto(
    string? State,
    double? Score,
    double? LowerBound,
    double? UpperBound,
    string? Category,
    string? Trend,
    double? Delta,
    double? EffectiveSampleSize,
    string? EvidenceLevel,
    int? CanonicalRuns,
    int? SpeculativeRuns,
    int? UntrackedRuns,
    CloudWindowDto? Window,
    CloudWindowDto? EvidenceWindow,
    DateTimeOffset? ComputedAt,
    string? Freshness);

/// <summary>
/// What Cloud's flaky ledger says about a test.
/// </summary>
/// <param name="State">
/// <c>flaky</c>, <c>not-flaky</c>, <c>inconclusive</c> or <c>not-examined</c>; only <c>not-flaky</c>
/// means not flaky (contract §4.3).
/// </param>
/// <param name="Category">The flakiness category.</param>
/// <param name="Severity">The flakiness severity.</param>
/// <param name="Since">When it became flaky.</param>
/// <param name="LastDetectionAt">When it was last examined.</param>
/// <param name="Window">The window the state covers.</param>
/// <param name="ComputedAt">When Cloud computed it.</param>
/// <param name="Freshness"><c>current</c>, <c>pending</c> or <c>none</c>.</param>
internal sealed record CloudFlakyDto(
    string? State,
    string? Category,
    string? Severity,
    DateTimeOffset? Since,
    DateTimeOffset? LastDetectionAt,
    CloudWindowDto? Window,
    DateTimeOffset? ComputedAt,
    string? Freshness);

/// <summary>
/// The test's newest runs on the canonical branch.
/// </summary>
/// <param name="Runs">Runs in the window.</param>
/// <param name="Failed">Of those, the failures.</param>
/// <param name="ConsecutiveFailures">The current run of failures, looking back through retention.</param>
/// <param name="FailingSince">When that run of failures began.</param>
/// <param name="LatestOutcome">The newest run's outcome.</param>
/// <param name="LatestAt">When it ran.</param>
/// <param name="Window">The runs window.</param>
internal sealed record CloudCanonicalDto(
    int? Runs,
    int? Failed,
    int? ConsecutiveFailures,
    DateTimeOffset? FailingSince,
    string? LatestOutcome,
    DateTimeOffset? LatestAt,
    CloudWindowDto? Window);

/// <summary>
/// The test's newest state transition in Cloud.
/// </summary>
/// <param name="Type">What changed, such as <c>reliability-degraded</c>.</param>
/// <param name="From">The category or severity before.</param>
/// <param name="To">The category or severity after.</param>
/// <param name="OccurredAt">When.</param>
internal sealed record CloudTransitionDto(string? Type, string? From, string? To, DateTimeOffset? OccurredAt);

/// <summary>
/// One failure signature of a test, as Cloud saw it across machines.
/// </summary>
/// <param name="ErrorMessageHash">The signature: the SDK's hash of the normalised message.</param>
/// <param name="ExceptionType">The most frequent exception type under it.</param>
/// <param name="Requested">Whether this machine asked about it.</param>
/// <param name="Runs">Runs that failed with it in the window.</param>
/// <param name="CanonicalRuns">Of those, on the canonical branch.</param>
/// <param name="CiRuns">Of those, in CI.</param>
/// <param name="LocalRuns">Of those, outside CI.</param>
/// <param name="Machines">Distinct machines it failed on.</param>
/// <param name="FirstSeenAt">When it was first seen in the window.</param>
/// <param name="LastSeenAt">When it was last seen.</param>
/// <param name="StackTraceHashes">The stack trace hashes seen with it.</param>
/// <param name="Window">The window the counts cover.</param>
internal sealed record CloudSignatureDto(
    string? ErrorMessageHash,
    string? ExceptionType,
    bool? Requested,
    int? Runs,
    int? CanonicalRuns,
    int? CiRuns,
    int? LocalRuns,
    int? Machines,
    DateTimeOffset? FirstSeenAt,
    DateTimeOffset? LastSeenAt,
    IReadOnlyList<string> StackTraceHashes,
    CloudWindowDto? Window);

/// <summary>
/// The window a Cloud value was computed over (contract §4.4).
/// </summary>
/// <param name="Kind"><c>days</c> or <c>runs</c>.</param>
/// <param name="Length">Days or runs.</param>
/// <param name="End">A <c>days</c> window's upper edge: when the value was computed.</param>
/// <param name="OldestAt">A <c>runs</c> window's oldest run.</param>
internal sealed record CloudWindowDto(string? Kind, int? Length, DateTimeOffset? End, DateTimeOffset? OldestAt);

/// <summary>
/// Whether Cloud has seen one local failure's signature for its test (contract §4.7).
/// </summary>
/// <param name="Match"><c>matched</c>, <c>unseen</c>, <c>unsignable</c> or <c>unknown</c>.</param>
/// <param name="Signature">The matching signature when <paramref name="Match"/> is <c>matched</c>; else null.</param>
internal sealed record CloudMatchDto(string Match, CloudSignatureDto? Signature);

/// <summary>
/// Xping Cloud's view of a test that failed in the newest session, and of that failure.
/// </summary>
/// <param name="Digest">The test's digest.</param>
/// <param name="Match">Whether Cloud has seen the failure's signature for the test.</param>
/// <param name="Signature">The matching signature when <paramref name="Match"/> is <c>matched</c>; else null.</param>
internal sealed record CloudFailureDto(CloudDigestDto Digest, string Match, CloudSignatureDto? Signature);

/// <summary>
/// Xping Cloud's view of the test <c>report --test</c> resolved to, with its run history.
/// </summary>
/// <param name="Digest">The test's digest, or null when Cloud did not answer for it.</param>
/// <param name="Runs">The newest runs in Cloud, newest first; empty unless <paramref name="RunsState"/> is <c>ok</c>.</param>
/// <param name="RunsState"><c>ok</c>, <c>budget</c>, <c>unavailable</c> or <c>not-attempted</c>.</param>
/// <param name="RetentionDays">The workspace plan's retention, or null.</param>
/// <param name="HasMore">Whether older runs exist than <paramref name="Runs"/> holds.</param>
internal sealed record CloudTestHistoryDto(
    CloudDigestDto? Digest,
    IReadOnlyList<CloudRunDto> Runs,
    string RunsState,
    int? RetentionDays,
    bool HasMore);

/// <summary>
/// One session's result for the test in Cloud (contract §6.3).
/// </summary>
/// <param name="SessionKey">The session.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="Outcome">The final attempt's outcome: <c>passed</c>, <c>failed</c> or <c>timeout</c>.</param>
/// <param name="Attempts">How many attempts it took.</param>
/// <param name="FirstAttemptOutcome">The first attempt's outcome.</param>
/// <param name="DurationMs">The final attempt's duration.</param>
/// <param name="Tier"><c>canonical</c>, <c>speculative</c> or <c>untracked</c>.</param>
/// <param name="IsCi">Whether it ran in CI.</param>
/// <param name="MachineName">The machine it ran on.</param>
/// <param name="Branch">The branch it ran on.</param>
/// <param name="CommitSha">The commit it ran at.</param>
/// <param name="PullRequestNumber">The pull request it ran for.</param>
/// <param name="ExceptionType">The failure's exception type.</param>
/// <param name="ErrorMessageHash">The failure's signature.</param>
internal sealed record CloudRunDto(
    string? SessionKey,
    DateTimeOffset? StartedAt,
    string? Outcome,
    int? Attempts,
    string? FirstAttemptOutcome,
    long? DurationMs,
    string? Tier,
    bool? IsCi,
    string? MachineName,
    string? Branch,
    string? CommitSha,
    int? PullRequestNumber,
    string? ExceptionType,
    string? ErrorMessageHash);
