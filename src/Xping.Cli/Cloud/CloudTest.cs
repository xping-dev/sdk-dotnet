/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// Xping Cloud's view of one test (<c>TestResponse</c>), with only the members the CLI reads
/// (cli-auth-cli-spec §10.1).
/// </summary>
/// <param name="TestFingerprint">The test's fingerprint, as the SDK computes it.</param>
/// <param name="ConfidenceScore">The confidence score from 0 to 1, when there is enough data.</param>
/// <param name="ScoreCategory">The score's category, such as <c>ModeratelyReliable</c>.</param>
/// <param name="EvidenceLevel">How much data the score rests on.</param>
/// <param name="TotalExecutions">How many runs Xping Cloud has recorded.</param>
/// <param name="IsFlaky">Whether Xping Cloud considers the test flaky.</param>
/// <param name="ScoreTrend">Which way the score is moving.</param>
/// <param name="ScoreDelta">The last change of the score.</param>
/// <param name="LastExecutedAtUtc">When the test last ran.</param>
internal sealed record CloudTest(
    string TestFingerprint,
    double? ConfidenceScore,
    string? ScoreCategory,
    string EvidenceLevel,
    int TotalExecutions,
    bool? IsFlaky,
    string? ScoreTrend,
    double? ScoreDelta,
    DateTimeOffset? LastExecutedAtUtc);
