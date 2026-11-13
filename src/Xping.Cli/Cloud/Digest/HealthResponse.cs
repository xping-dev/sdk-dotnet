/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud.Digest;

/// <summary>
/// One page of a project's suite health (contract §6.5): every test Cloud calls <c>known-flaky</c>,
/// <c>broken</c> or <c>unstable</c>, ordered by fingerprint.
/// </summary>
/// <param name="ComputedAt">When Cloud answered.</param>
/// <param name="SignatureAlgorithm">How Cloud's signatures were computed.</param>
/// <param name="RetentionDays">The workspace plan's data retention.</param>
/// <param name="TotalTests">The tests Cloud classified: those that ran inside the retention.</param>
/// <param name="Tests">This page's tests, each the digest of contract §6.2.</param>
/// <param name="Sessions">One state per requested session key, as the digest answers them.</param>
/// <param name="Next">The cursor of the next page; null on the last.</param>
internal sealed record HealthResponse(
    DateTimeOffset ComputedAt,
    string? SignatureAlgorithm,
    int? RetentionDays,
    int? TotalTests,
    IReadOnlyList<TestDigest> Tests,
    IReadOnlyList<DigestSession>? Sessions,
    string? Next);
