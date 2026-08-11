/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// Decides how much data a finding rests on, and whether it rests on enough to report at all.
/// </summary>
/// <remarks>
/// Applied centrally rather than per provider. Two providers disagreeing about what counts as enough
/// evidence would produce a report where a test is confidently flagged by one metric and silently
/// dropped by another, with nothing on screen to explain the difference.
/// </remarks>
internal static class EvidenceLevelResolver
{
    /// <summary>
    /// Bands executions into an evidence level.
    /// </summary>
    /// <param name="executions">Executions of the subject within the window.</param>
    /// <returns>The level.</returns>
    public static EvidenceLevel Resolve(int executions) => executions switch
    {
        > LocalAnalysisConstants.EvidenceHighExecutions => EvidenceLevel.High,
        >= LocalAnalysisConstants.EvidenceModerateExecutions => EvidenceLevel.Moderate,
        _ => EvidenceLevel.Low
    };

    /// <summary>
    /// Counts the executions a finding rests on.
    /// </summary>
    /// <param name="subject">The test or group the finding is about.</param>
    /// <param name="index">The shared index.</param>
    /// <returns>The execution count.</returns>
    /// <remarks>
    /// A group is measured by its best-evidenced member, matching how it is scored: the cluster is
    /// worth reporting if any one member is well enough evidenced to stand behind.
    /// </remarks>
    public static int CountExecutions(FindingSubject subject, TestIndex index)
    {
        int best = 0;

        foreach (TestReference test in subject.Tests)
        {
            int count = index.ExecutionsOf(test.TestFingerprint).Count;
            if (count > best)
                best = count;
        }

        return best;
    }

    /// <summary>
    /// Returns whether a finding has enough behind it to be emitted.
    /// </summary>
    /// <param name="executions">Executions of the subject within the window.</param>
    /// <param name="sessionCount">Sessions in the window.</param>
    /// <returns><see langword="true"/> when the finding may be reported.</returns>
    /// <remarks>
    /// Both bounds matter. Enough sessions stops one unlucky run from defining a rate; enough
    /// executions of the subject stops a test added yesterday from being judged on the history of
    /// the tests around it.
    /// </remarks>
    public static bool MeetsReportingFloor(int executions, int sessionCount) =>
        sessionCount >= LocalAnalysisConstants.MinimumSessionsToReport &&
        executions >= LocalAnalysisConstants.MinimumExecutionsToReport;
}
