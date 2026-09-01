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
/// <param name="Executions">Executions of the test across the whole window.</param>
/// <param name="LastSeenAt">When the test last ran.</param>
/// <param name="LastSeenSha">The commit it last ran at, when known.</param>
internal sealed record VanishedEvidence(
    int BaselineSessions,
    int BaselineSessionCount,
    int CurrentSessionCount,
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
/// Absence is only meaningful against a habit. A test seen once and never again was probably never
/// really established, so a minimum number of baseline appearances is required before its
/// disappearance counts as a change.
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
                    executions.Count,
                    mostRecent.Session.StartedAt,
                    RevisionContext.ReadSha(mostRecent.Session)),

                // A test that ran in every baseline session and then stopped is a starker change
                // than one that ran in the minimum three, and the ratio says so without inventing a
                // reason for the disappearance.
                //
                // Bounded below, because the ratio alone cannot tell three appearances in three
                // from three in seventeen: the first is a habit that stopped, the second is a test
                // that was mostly absent already and whose absence is the likeliest thing it could
                // do next. The bound puts them at 0.44 and 0.06.
                Unreliability: WilsonInterval.LowerBound(appearances, slices.BaselineCount),

                // Measured from the current slice, which by definition is where it is absent.
                SessionsSinceLastOccurrence: mostRecent.SessionIndex,

                DrillDownCommand: DrillDown.ForTest(FindingKind.Vanished, reference),

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
