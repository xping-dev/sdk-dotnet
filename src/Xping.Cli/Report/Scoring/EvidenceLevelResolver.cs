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
/// <para>
/// The two questions are answered from different numbers, and the split is the point.
/// </para>
/// <para>
/// <b>Whether to report</b> is decided centrally, on the subject's own history —
/// <see cref="CountSessions"/> and <see cref="MeetsReportingFloor"/>. Letting each provider decide
/// that would produce a report where a test is confidently flagged by one metric and silently
/// dropped by another, with nothing on screen to explain the difference. Emission stays one rule,
/// applied once, to every kind alike.
/// </para>
/// <para>
/// <b>How much it rests on</b> is banded on the candidate's own denominator, which only the provider
/// knows. Every provider measures over a subset of the runs its subject appeared in: environmental
/// runs are discounted, a session that recorded no UTC offset cannot be placed on a clock, a run of
/// zero-duration executions cannot be normalised. A split computed from ten runs and labelled with
/// the twenty the test appeared in claims evidence it has not got, and under bands that fit a
/// twenty-run window it claims the top one.
/// </para>
/// <para>
/// So two findings about one test may now carry different levels. That is the same thing the report
/// already says with its population marker — see <c>docs/internals/finding-populations.md</c> — and
/// it is a description of a claim rather than a gate on it, so it costs none of what the central
/// floor above is there to protect.
/// </para>
/// </remarks>
internal static class EvidenceLevelResolver
{
    /// <summary>
    /// Bands a finding's own denominator into an evidence level.
    /// </summary>
    /// <param name="sessions">
    /// Independent runs the claim was computed from — <c>FindingCandidate.EvidenceSessions</c>, not
    /// the runs the subject appeared in.
    /// </param>
    /// <returns>The level.</returns>
    public static EvidenceLevel Resolve(int sessions) => sessions switch
    {
        > LocalAnalysisConstants.EvidenceHighSessions => EvidenceLevel.High,
        >= LocalAnalysisConstants.EvidenceModerateSessions => EvidenceLevel.Moderate,
        _ => EvidenceLevel.Low
    };

    /// <summary>
    /// Counts the sessions the subject ran in — the reporting floor's denominator.
    /// </summary>
    /// <param name="subject">The test or group the finding is about.</param>
    /// <param name="index">The shared index.</param>
    /// <returns>The distinct session count.</returns>
    /// <remarks>
    /// <para>
    /// The subject's whole history in the window, whatever any one provider then measured over.
    /// That is what the floor wants to know — has this test been around long enough to be judged —
    /// and it is deliberately not what <see cref="Resolve"/> bands. A kind that does publish this
    /// figure as its own denominator, because it sets nothing aside, may hand it over as such.
    /// </para>
    /// <para>
    /// A group is measured by its best-evidenced member, matching how it is scored: the cluster is
    /// worth reporting if any one member is well enough evidenced to stand behind.
    /// </para>
    /// <para>
    /// Sessions rather than executions, because that is the unit of independence. Attempts of one
    /// test inside one session are correlated — an assertion that fails at 14:02 usually fails again
    /// five seconds later — so counting them separately claims a sample size the data has not got.
    /// </para>
    /// </remarks>
    public static int CountSessions(FindingSubject subject, TestIndex index)
    {
        int best = 0;

        foreach (TestReference test in subject.Tests)
        {
            int count = index.SessionsRunIn(test.TestFingerprint);
            if (count > best)
                best = count;
        }

        return best;
    }

    /// <summary>
    /// Returns whether a finding has enough behind it to be emitted.
    /// </summary>
    /// <param name="subjectSessions">Sessions the subject ran in, within the window.</param>
    /// <param name="windowSessions">Sessions in the window.</param>
    /// <returns><see langword="true"/> when the finding may be reported.</returns>
    /// <remarks>
    /// Both bounds matter. Enough sessions in the window stops one unlucky run from defining a rate;
    /// enough sessions of the subject stops a test added yesterday from being judged on the history
    /// of the tests around it. Counting the second bound in executions never achieved that — a test
    /// that retried five times in a single session cleared it without having any history at all.
    /// </remarks>
    public static bool MeetsReportingFloor(int subjectSessions, int windowSessions) =>
        windowSessions >= LocalAnalysisConstants.MinimumSessionsToReport &&
        subjectSessions >= LocalAnalysisConstants.MinimumSessionsPerTestToReport;
}
