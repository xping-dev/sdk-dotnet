/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// Turns a provider's observation into the single score every finding is ranked by.
/// </summary>
/// <remarks>
/// <para>
/// One formula for every kind, applied in one place. The weights encode a judgement about what makes
/// a problem worth a developer's morning: how unreliable the test is dominates, but a test that runs
/// constantly, blocks builds, or misbehaved this morning all move ahead of one that does not.
/// </para>
/// <para>
/// This is deliberately not a confidence score. It ranks findings against each other inside one
/// report; it does not claim how likely the finding is to be real. The unreliability term is a
/// confidence bound — <see cref="WilsonInterval"/> — but only so that the ranking accounts for the
/// data behind each finding. A finding on five runs sorts below the same finding on forty; neither
/// score says what the odds are that either is a genuine defect, and nothing here corrects for the
/// number of tests the providers compared to produce them.
/// </para>
/// </remarks>
internal static class ImpactScorer
{
    private const double UnreliabilityWeight = 0.40;
    private const double RunFrequencyWeight = 0.25;
    private const double BlockingRateWeight = 0.20;
    private const double RecencyWeight = 0.15;

    /// <summary>
    /// Scores one candidate.
    /// </summary>
    /// <param name="candidate">What the provider observed.</param>
    /// <param name="index">The shared index the remaining terms are derived from.</param>
    /// <returns>A score in [0,1].</returns>
    /// <remarks>
    /// A group scores as its strongest member rather than its average: one badly affected test makes
    /// the cluster worth looking at, and averaging would let a wide cluster of mild failures bury it.
    /// </remarks>
    public static double Score(FindingCandidateInputs candidate, TestIndex index)
    {
        double recency = TestIndex.Recency(candidate.SessionsSinceLastOccurrence);
        double best = 0;

        foreach (string fingerprint in candidate.Fingerprints)
        {
            double impact =
                (UnreliabilityWeight * Clamp(candidate.Unreliability)) +
                (RunFrequencyWeight * Clamp(index.RunFrequencyOf(fingerprint))) +
                (BlockingRateWeight * Clamp(index.BlockingRateOf(fingerprint))) +
                (RecencyWeight * Clamp(recency));

            if (impact > best)
                best = impact;
        }

        return Clamp(best);
    }

    /// <summary>
    /// Bands a score into a severity.
    /// </summary>
    /// <param name="impact">The score to band.</param>
    /// <returns>The severity.</returns>
    public static Severity Band(double impact) => impact switch
    {
        >= LocalAnalysisConstants.SeverityHighThreshold => Severity.High,
        >= LocalAnalysisConstants.SeverityMediumThreshold => Severity.Medium,
        _ => Severity.Low
    };

    private static double Clamp(double value) => Math.Clamp(value, 0.0, 1.0);
}

/// <summary>
/// The parts of a candidate the scorer reads.
/// </summary>
/// <param name="Fingerprints">The tests the finding covers.</param>
/// <param name="Unreliability">The kind-specific unreliability term, in [0,1].</param>
/// <param name="SessionsSinceLastOccurrence">Sessions back the behaviour was last seen.</param>
internal sealed record FindingCandidateInputs(
    IReadOnlyList<string> Fingerprints,
    double Unreliability,
    int SessionsSinceLastOccurrence)
{
    /// <summary>
    /// Extracts the scorer's inputs from a candidate.
    /// </summary>
    /// <param name="candidate">The candidate to read.</param>
    /// <returns>The inputs.</returns>
    public static FindingCandidateInputs From(FindingCandidate candidate) =>
        new(
            [.. candidate.Subject.Tests.Select(t => t.TestFingerprint)],
            candidate.Unreliability,
            candidate.SessionsSinceLastOccurrence);
}
