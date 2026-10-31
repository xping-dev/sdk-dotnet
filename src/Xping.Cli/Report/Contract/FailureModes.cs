/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;

namespace Xping.Cli.Report.Contract;

/// <summary>
/// Resolves the failure modes a finding or a test history publishes.
/// </summary>
/// <remarks>
/// Both views map through here, so a mode reads the same under <c>--id</c> and <c>--test</c> and
/// only the population it was counted over differs.
/// </remarks>
internal static class FailureModes
{
    /// <summary>
    /// Gets the modes a finding's evidence names.
    /// </summary>
    /// <param name="evidence">The kind-specific payload.</param>
    /// <returns>Its modes, most frequent first, or an empty list for a kind that names none.</returns>
    public static IReadOnlyList<FailureModeDto> Of(FindingEvidence evidence) => evidence switch
    {
        FlakyEvidence flaky => [.. flaky.DistinctSignatures.Select(From)],

        // The modal mode only: an always-failing test's other modes are not in its evidence, and
        // its dominant-mode share already says when the modal one does not cover every failure.
        AlwaysFailingEvidence always => [From(always.Signature)],
        BrokenFixtureEvidence fixture => [From(fixture.Signature)],
        SharedFailureEvidence shared => [From(shared.Signature)],
        _ => []
    };

    /// <summary>
    /// Gets the modes a test failed with over every failed attempt in the window.
    /// </summary>
    /// <param name="context">The window the history was resolved over.</param>
    /// <param name="fingerprint">The test.</param>
    /// <returns>Its modes in the index's order: count, then recency, then hash.</returns>
    public static IReadOnlyList<FailureModeDto> Of(AnalysisContext context, string fingerprint) =>
        [.. context.Signatures.SignaturesOf(fingerprint).Select(o => From(FailureModeProvider.ViewOf(context, o)))];

    private static FailureModeDto From(SignatureView view) =>
        new(
            view.Hash,
            view.ExceptionType,
            view.Message,
            view.Frames,
            view.Degraded,
            view.Unavailable,
            view.TimedOut,
            view.Occurrences,
            view.FirstSeenAt,
            view.FirstSeenSha,
            view.FirstSeenSessionsAgo,
            view.FirstSeenInLatestSession,
            view.FirstSeenAfterWindowStart);
}
