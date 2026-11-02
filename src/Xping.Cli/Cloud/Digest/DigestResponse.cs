/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud.Digest;

// The wire shapes of contract §6.1 and §6.2. Every enum is kept as the string Cloud sent, because
// the CLI must accept and print values it does not know (contract §4). Deserialization leaves a
// missing member null whatever its declared type, so CloudApiClient checks the ones it relies on.

/// <summary>
/// The answer to a <see cref="DigestRequest"/>.
/// </summary>
/// <param name="ComputedAt">When Cloud answered; the verdict's computation time (contract §4.5).</param>
/// <param name="SignatureAlgorithm">How Cloud's signatures were computed; compared with <c>emh-1</c>.</param>
/// <param name="RetentionDays">The workspace plan's data retention.</param>
/// <param name="Tests">One digest per requested test, in request order.</param>
/// <param name="Sessions">One state per requested session key, in request order.</param>
internal sealed record DigestResponse(
    DateTimeOffset ComputedAt,
    string? SignatureAlgorithm,
    int? RetentionDays,
    IReadOnlyList<TestDigest> Tests,
    IReadOnlyList<DigestSession>? Sessions);

/// <summary>
/// Cloud's view of one test (contract §6.2). Blocks are null when Cloud has nothing for them.
/// </summary>
/// <param name="Fingerprint">The test's fingerprint, as requested.</param>
/// <param name="State"><c>ok</c>, or <c>unknown</c> when the project has no such test.</param>
/// <param name="TestName">The name Cloud recorded, for the identity check.</param>
/// <param name="LastExecutedAt">Cloud's newest run of the test.</param>
/// <param name="TotalRuns">Every run Cloud holds; the one lifetime number.</param>
/// <param name="Verdict"><c>known-flaky</c>, <c>broken</c>, <c>unstable</c>, <c>passing</c> or <c>no-history</c>.</param>
/// <param name="Confidence">The newest overall score.</param>
/// <param name="Flaky">The flaky ledger's state.</param>
/// <param name="Canonical">The newest canonical runs; null with <c>no-history</c>.</param>
/// <param name="LastTransition">The newest state transition, of either dimension.</param>
/// <param name="Signatures">Every requested signature and the most frequent others.</param>
internal sealed record TestDigest(
    string Fingerprint,
    string State,
    string? TestName,
    DateTimeOffset? LastExecutedAt,
    int? TotalRuns,
    string? Verdict,
    DigestConfidence? Confidence,
    DigestFlaky? Flaky,
    DigestCanonical? Canonical,
    DigestTransition? LastTransition,
    IReadOnlyList<DigestSignature>? Signatures);

/// <summary>
/// The confidence block of a <see cref="TestDigest"/>.
/// </summary>
/// <param name="State"><c>scored</c>, <c>insufficient-evidence</c> or <c>not-scored</c>.</param>
/// <param name="Score">The score, 0 to 1; null unless scored.</param>
/// <param name="LowerBound">The score's lower bound.</param>
/// <param name="UpperBound">The score's upper bound.</param>
/// <param name="Category">The score's category, such as <c>highly-unreliable</c>.</param>
/// <param name="Trend">Which way the score is moving.</param>
/// <param name="Delta">The score's last change.</param>
/// <param name="EffectiveSampleSize">The effective sample size behind the score.</param>
/// <param name="EvidenceLevel"><c>sparse</c>, <c>adequate</c>, <c>robust</c> or <c>no-data</c>.</param>
/// <param name="CanonicalRuns">Canonical runs in the evidence window.</param>
/// <param name="SpeculativeRuns">Speculative runs in the evidence window.</param>
/// <param name="UntrackedRuns">Untracked runs in the evidence window.</param>
/// <param name="Window">The scoring window.</param>
/// <param name="EvidenceWindow">The evidence window.</param>
/// <param name="ComputedAt">When Cloud scored the test.</param>
/// <param name="Freshness"><c>current</c>, <c>pending</c> or <c>none</c>.</param>
internal sealed record DigestConfidence(
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
    DigestWindow? Window,
    DigestWindow? EvidenceWindow,
    DateTimeOffset? ComputedAt,
    string? Freshness);

/// <summary>
/// The flaky block of a <see cref="TestDigest"/>.
/// </summary>
/// <param name="State"><c>flaky</c>, <c>not-flaky</c>, <c>inconclusive</c> or <c>not-examined</c>.</param>
/// <param name="Category">The kind of flakiness, when flaky.</param>
/// <param name="Severity">How bad it is, when flaky.</param>
/// <param name="Since">When the test became flaky, when it is.</param>
/// <param name="LastDetectionAt">The newest detection.</param>
/// <param name="Window">The detection window.</param>
/// <param name="ComputedAt">When Cloud examined the test.</param>
/// <param name="Freshness"><c>current</c>, <c>pending</c> or <c>none</c>.</param>
internal sealed record DigestFlaky(
    string? State,
    string? Category,
    string? Severity,
    DateTimeOffset? Since,
    DateTimeOffset? LastDetectionAt,
    DigestWindow? Window,
    DateTimeOffset? ComputedAt,
    string? Freshness);

/// <summary>
/// The canonical block of a <see cref="TestDigest"/>.
/// </summary>
/// <param name="Runs">Canonical runs in the window.</param>
/// <param name="Failed">Of those, how many failed.</param>
/// <param name="ConsecutiveFailures">Failures in a row, looking back through retention.</param>
/// <param name="FailingSince">When the current run of failures began.</param>
/// <param name="LatestOutcome">The newest canonical run's outcome.</param>
/// <param name="LatestAt">When the newest canonical run ran.</param>
/// <param name="Window">The run-count window.</param>
internal sealed record DigestCanonical(
    int? Runs,
    int? Failed,
    int? ConsecutiveFailures,
    DateTimeOffset? FailingSince,
    string? LatestOutcome,
    DateTimeOffset? LatestAt,
    DigestWindow? Window);

/// <summary>
/// The newest state transition of a test.
/// </summary>
/// <param name="Type">Such as <c>reliability-degraded</c>.</param>
/// <param name="From">The category or severity before.</param>
/// <param name="To">The category or severity after.</param>
/// <param name="OccurredAt">When it happened.</param>
internal sealed record DigestTransition(string? Type, string? From, string? To, DateTimeOffset? OccurredAt);

/// <summary>
/// One failure signature of a test, spread across machines and environments.
/// </summary>
/// <param name="ErrorMessageHash">The signature: the SDK's hash of the normalised message.</param>
/// <param name="ExceptionType">The most frequent exception type with this signature.</param>
/// <param name="Requested">Whether this machine asked about it.</param>
/// <param name="Runs">Runs that failed with it.</param>
/// <param name="CanonicalRuns">Of those, canonical.</param>
/// <param name="CiRuns">Of those, in CI.</param>
/// <param name="LocalRuns">Of those, outside CI.</param>
/// <param name="Machines">Distinct machines it failed on.</param>
/// <param name="FirstSeenAt">Its oldest failure in the window.</param>
/// <param name="LastSeenAt">Its newest failure in the window.</param>
/// <param name="StackTraceHashes">The call sites it failed at.</param>
/// <param name="Window">The signature window.</param>
internal sealed record DigestSignature(
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
    IReadOnlyList<string>? StackTraceHashes,
    DigestWindow? Window);

/// <summary>
/// The window a value was computed in (contract §4.4).
/// </summary>
/// <param name="Kind"><c>days</c> or <c>runs</c>.</param>
/// <param name="Length">Days or runs.</param>
/// <param name="End">For days: when the value was computed, the window's upper edge.</param>
/// <param name="OldestAt">For runs: when the oldest run in the window ran.</param>
internal sealed record DigestWindow(string? Kind, int? Length, DateTimeOffset? End, DateTimeOffset? OldestAt);

/// <summary>
/// What Cloud knows of one session this machine recorded.
/// </summary>
/// <param name="SessionKey">The session's key, as requested.</param>
/// <param name="State"><c>unknown</c>, <c>pending</c>, <c>processing</c>, <c>complete</c>, <c>abandoned</c> or <c>failed</c>.</param>
/// <param name="Scored">Whether every upload of it has been scored.</param>
/// <param name="FlakyExamined">Whether every upload of it has passed flaky detection.</param>
internal sealed record DigestSession(string SessionKey, string State, bool? Scored, bool? FlakyExamined);
