/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud.Digest;

/// <summary>
/// The answer of <c>GET /v1/projects/{projectKey}/tests/{fingerprint}/runs</c> (contract §6.3).
/// </summary>
/// <param name="RetentionDays">The workspace plan's data retention.</param>
/// <param name="HasMore">Whether older runs exist past the last one returned.</param>
/// <param name="Runs">The newest runs, newest first, across every machine and branch.</param>
internal sealed record RunsResponse(int? RetentionDays, bool HasMore, IReadOnlyList<CloudRun> Runs);

/// <summary>
/// One session's result for one test, its attempts collapsed to the final one (contract §4.6).
/// </summary>
/// <param name="SessionKey">The session's key.</param>
/// <param name="StartedAt">When the session started.</param>
/// <param name="Outcome"><c>passed</c>, <c>failed</c> or <c>timeout</c>.</param>
/// <param name="Attempts">How many attempts the run took.</param>
/// <param name="FirstAttemptOutcome">The first attempt's outcome, the retry signal.</param>
/// <param name="DurationMs">How long the final attempt took.</param>
/// <param name="Tier"><c>canonical</c>, <c>speculative</c> or <c>untracked</c>.</param>
/// <param name="IsCi">Whether the session ran in CI.</param>
/// <param name="MachineName">The machine it ran on.</param>
/// <param name="Branch">The branch it ran on.</param>
/// <param name="CommitSha">The commit it ran at.</param>
/// <param name="PullRequestNumber">The pull request it ran for.</param>
/// <param name="ExceptionType">The failure's exception type.</param>
/// <param name="ErrorMessageHash">The failure's signature.</param>
internal sealed record CloudRun(
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
