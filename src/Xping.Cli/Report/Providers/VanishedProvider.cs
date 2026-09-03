/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// Evidence that a test which used to run has stopped running.
/// </summary>
/// <param name="BaselineSessions">Sessions in the baseline slice the test appeared in.</param>
/// <param name="BaselineSessionCount">Sessions in the baseline slice.</param>
/// <param name="CurrentSessionCount">Sessions in the current slice it is absent from.</param>
/// <param name="BaselineRunRate">Share of the baseline sessions it appeared in.</param>
/// <param name="ChanceOfAbsence">
/// How often a test with that habit would miss the current slice anyway. It is what separates a
/// habit that stopped from a test that was mostly absent already, and a reader cannot weigh the
/// claim without it: "ran in 3 of 17 earlier runs" and "ran in 17 of 17" are the same sentence
/// until this number is beside them.
/// </param>
/// <param name="Executions">Executions of the test across the whole window.</param>
/// <param name="LastSeenAt">When the test last ran.</param>
/// <param name="LastSeenSha">The commit it last ran at, when known.</param>
internal sealed record VanishedEvidence(
    int BaselineSessions,
    int BaselineSessionCount,
    int CurrentSessionCount,
    double BaselineRunRate,
    double ChanceOfAbsence,
    int Executions,
    DateTime LastSeenAt,
    string? LastSeenSha) : FindingEvidence;

/// <summary>
/// Reports tests that appeared consistently and then stopped.
/// </summary>
/// <remarks>
/// <para>
/// Usually a deliberate deletion, which is why it is reported quietly. It earns its place because
/// the other explanations are ones nobody notices: a filter that silently stopped matching, a
/// fixture that now throws during discovery, or a parameterised case whose arguments changed shape
/// so its fingerprint moved. In every one of those the suite goes green by running less.
/// </para>
/// <para>
/// Absence is only meaningful against a habit, and a count of appearances cannot establish one. A
/// test that ran in three of seventeen baseline sessions is more likely than not to miss the next
/// three whatever anyone does to it, so the gate is the probability of that: Fisher's exact test on
/// the baseline and current sessions against present and absent, one-sided because the direction is
/// fixed before any table is formed — this kind only ever looks at a test already known to be
/// absent, and there is no finding for one that started running.
/// </para>
/// </remarks>
internal sealed class VanishedProvider : IFindingProvider
{
    /// <inheritdoc/>
    public string Name => "vanished";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds => [FindingKind.Vanished];

    /// <inheritdoc/>
    public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
    {
        AnalysisWindowSlices slices = AnalysisWindowSlices.From(context);

        // Nothing to compare against: with no baseline every test looks new, and with no current
        // slice every test looks vanished.
        if (slices.BaselineCount == 0 || slices.CurrentCount == 0)
            yield break;

        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            if (slices.Current.Contains(fingerprint))
                continue;

            if (!slices.BaselineAppearances.TryGetValue(fingerprint, out int appearances) ||
                appearances < LocalAnalysisConstants.VanishedMinBaselineSessions)
            {
                continue;
            }

            // How often a test that ran this share of the baseline would miss the current slice with
            // nothing having changed. The current arm holds no appearances by construction — that is
            // what put the fingerprint here — so the table is the test's habit against its absence.
            double chanceOfAbsence = FisherExact.OneSidedPValue(
                appearances, slices.BaselineCount, 0, slices.CurrentCount);

            if (chanceOfAbsence > LocalAnalysisConstants.VanishedMaxChanceOfAbsence)
                continue;

            TestReference? reference = context.Tests.ReferenceFor(fingerprint);
            if (reference == null)
                continue;

            IReadOnlyList<ExecutionRef> executions = context.Tests.ExecutionsOf(fingerprint);

            // Executions arrive newest-session-first, so the head is the last time it ran.
            ExecutionRef mostRecent = executions[0];

            yield return new FindingCandidate(
                FindingKind.Vanished,
                new FindingSubject.SingleTest(reference),
                new VanishedEvidence(
                    appearances,
                    slices.BaselineCount,
                    slices.CurrentCount,
                    FindingOrder.Round((double)appearances / slices.BaselineCount),
                    FindingOrder.RoundProbability(chanceOfAbsence),
                    executions.Count,
                    mostRecent.Session.StartedAt,
                    RevisionContext.ReadSha(mostRecent.Session)),

                // A test that ran in every baseline session and then stopped is a starker change
                // than one that ran in three quarters of them, and the ratio says so without
                // inventing a reason for the disappearance.
                //
                // Whether the absence means anything at all is settled by the gate above, before
                // this is read, so the bound is left to do the one job it is good at: saying how
                // established the habit was, on a scale that grows with the runs behind it rather
                // than ranking five of five alongside fifty of fifty.
                Unreliability: WilsonInterval.LowerBound(appearances, slices.BaselineCount),

                // Measured from the current slice, which by definition is where it is absent.
                SessionsSinceLastOccurrence: mostRecent.SessionIndex,

                DrillDownCommand: DrillDown.ForTest(FindingKind.Vanished, reference),

                // Unrounded: this is the number #160's Benjamini-Hochberg pass sorts on, and the
                // rounded copy in the evidence is only what gets written down.
                PValue: chanceOfAbsence,

                // Reported quietly by default. A test that silently stopped running is worth
                // knowing about, but it is usually a deliberate deletion, and ranking it alongside
                // a failing test would train people to ignore the top of the report.
                SeverityCeiling: Severity.Low);
        }
    }
}

/// <summary>
/// The fingerprints present on each side of a window's split.
/// </summary>
/// <param name="Current">Fingerprints seen anywhere in the current slice.</param>
/// <param name="BaselineAppearances">Baseline sessions each fingerprint appeared in.</param>
/// <param name="CurrentCount">Sessions in the current slice.</param>
/// <param name="BaselineCount">Sessions in the baseline slice.</param>
internal sealed record AnalysisWindowSlices(
    IReadOnlySet<string> Current,
    IReadOnlyDictionary<string, int> BaselineAppearances,
    int CurrentCount,
    int BaselineCount)
{
    /// <summary>
    /// Derives the split for a window.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <returns>The fingerprints on each side.</returns>
    public static AnalysisWindowSlices From(AnalysisContext context)
    {
        var current = new HashSet<string>(StringComparer.Ordinal);
        foreach (TestSession session in context.Window.CurrentSlice)
        {
            foreach (string fingerprint in TestIndex.FingerprintsIn(session))
                current.Add(fingerprint);
        }

        var baseline = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (TestSession session in context.Window.BaselineSlice)
        {
            foreach (string fingerprint in TestIndex.FingerprintsIn(session))
            {
                baseline.TryGetValue(fingerprint, out int seen);
                baseline[fingerprint] = seen + 1;
            }
        }

        return new AnalysisWindowSlices(
            current,
            baseline,
            context.Window.CurrentSlice.Count,
            context.Window.BaselineSlice.Count);
    }
}
