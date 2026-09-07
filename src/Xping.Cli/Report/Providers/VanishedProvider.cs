/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// Evidence that a test which used to run has stopped running.
/// </summary>
/// <param name="BaselineSessions">Sessions in the baseline slice the test appeared in.</param>
/// <param name="BaselineSessionCount">Sessions in the baseline slice.</param>
/// <param name="CurrentSessionCount">Sessions in the current slice it is absent from.</param>
/// <param name="PartialSessionsSetAside">
/// Sessions in the window that covered only part of the suite and were counted on neither side. A
/// run under a filter never asked about this test, so its silence is not an absence. Published
/// because the two denominators above are otherwise unexplainable to a reader who asked for twenty
/// runs and is being shown a claim about four: the sentence is true of the runs that ran the suite,
/// and this is how many did not.
/// </param>
/// <param name="BaselineRunRate">
/// Share of the baseline sessions it appeared in — the habit itself, as a point estimate.
/// </param>
/// <param name="PValue">
/// One-sided, against the null that appearing was independent of which slice a session fell in, and
/// conditioned on both margins: how often every session this test appeared in would land in the
/// baseline and none in the current slice, given how many sessions it appeared in anywhere in the
/// window. Not the predictive chance that a test with this run rate misses the next few sessions,
/// which is a different and slightly smaller number — 0.596 against 0.559 for three appearances of
/// seventeen. It is what separates a habit that stopped from a test that was mostly absent already,
/// and a reader cannot weigh the claim without it: "ran in 3 of 17 earlier runs" and "ran in 17 of
/// 17" are the same sentence until it is beside them.
/// </param>
/// <param name="ExecutionsInWindow">
/// Executions of the test across the whole window, with nothing set aside. This kind counts session
/// appearances, and an environmental run is still a run the test either was or was not in;
/// discounting one would shorten the very history the absence is measured against.
/// </param>
/// <param name="LastSeenAt">When the test last ran.</param>
/// <param name="LastSeenSha">The commit it last ran at, when known.</param>
internal sealed record VanishedEvidence(
    int BaselineSessions,
    int BaselineSessionCount,
    int CurrentSessionCount,
    int PartialSessionsSetAside,
    double BaselineRunRate,
    double PValue,
    int ExecutionsInWindow,
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
/// Absence is only meaningful against a habit, and a count of appearances cannot establish one: a
/// test that ran in three of seventeen baseline sessions is more likely than not to miss the next
/// three whatever anyone does to it. So the gate is a test rather than a count — Fisher's exact test
/// on the baseline and current sessions against present and absent, asking how often every one of a
/// test's appearances would fall in the baseline and none in the current slice if appearing had
/// nothing to do with when the session ran. One-sided, because the direction is fixed before any
/// table is formed: this kind only ever looks at a test already known to be absent, and there is no
/// finding for one that started running.
/// </para>
/// <para>
/// And absence is only meaningful in a session that asked. A run under a <c>dotnet test --filter</c>
/// did not fail to see the tests it excluded; it never looked for them, and counting its silence
/// makes every unselected test look deleted. So this kind reads the window as the sequence of
/// sessions that covered the suite — <see cref="Indexes.SessionView.IsPartial"/> — and re-splits
/// that sequence into its own "now" and "before". A re-split rather than a filter of the window's
/// own slices, because the two differ on a store that interleaves full and filtered runs: dropping
/// the partial sessions out of a current slice of three filtered runs leaves nothing to ask about,
/// where taking the three most recent full runs still finds a test that genuinely stopped.
/// </para>
/// </remarks>
internal sealed class VanishedProvider : IFindingProvider
{
    /// <inheritdoc/>
    public string Name => "vanished";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds => [FindingKind.Vanished];

    /// <inheritdoc/>
    public ProviderReport Analyze(AnalysisContext context)
    {
        var candidates = new List<FindingCandidate>();
        int tested = 0;

        AnalysisWindowSlices slices = AnalysisWindowSlices.From(context);

        // Nothing to compare against: with no baseline every test looks new, and with no current
        // slice every test looks vanished. This is also where a window holding too few full runs
        // stops — and it stops with an empty family rather than merely with no candidates, so the
        // coordinator's Benjamini-Hochberg pass is handed a kind that asked nothing instead of a
        // kind that asked three hundred questions and liked none of the answers.
        if (slices.BaselineCount == 0 || slices.CurrentCount == 0)
            return Report(candidates, tested);

        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            // The habit the absence would be a change from. A test the baseline barely saw is one
            // this question cannot be asked of at all, whichever slice it is in now.
            if (!slices.BaselineAppearances.TryGetValue(fingerprint, out int appearances) ||
                appearances < LocalAnalysisConstants.VanishedMinBaselineSessions)
            {
                continue;
            }

            // Counted before the presence check below and not after it, which is the whole
            // difference between a family and a shortlist. Being absent from the current slice is
            // this kind's finding, not its precondition: every established test in the window is a
            // test asked whether it stopped running, and the ones still running are the askings that
            // answered no. Counting only the absences would describe a family in which every member
            // is a discovery and correct for nothing — a suite of three hundred stable tests holding
            // one absence would report m = 1 and pass it through untouched.
            tested++;

            if (slices.Current.Contains(fingerprint))
                continue;

            // How lopsided the split of this test's appearances is, against the null that appearing
            // had nothing to do with which slice a session fell in. The current arm holds none of
            // them by construction — that is what put the fingerprint here — so the table is the
            // test's habit against its absence.
            double pValue = FisherExact.OneSidedPValue(
                appearances, slices.BaselineCount, 0, slices.CurrentCount);

            if (pValue > LocalAnalysisConstants.VanishedAlpha)
                continue;

            TestReference? reference = context.Tests.ReferenceFor(fingerprint);
            if (reference == null)
                continue;

            IReadOnlyList<ExecutionRef> executions = context.Tests.ExecutionsOf(fingerprint);

            // Executions arrive newest-session-first, so the head is the last time it ran.
            ExecutionRef mostRecent = executions[0];

            candidates.Add(new FindingCandidate(
                FindingKind.Vanished,
                new FindingSubject.SingleTest(reference),
                new VanishedEvidence(
                    appearances,
                    slices.BaselineCount,
                    slices.CurrentCount,
                    slices.PartialSessionsSetAside,
                    FindingOrder.Round((double)appearances / slices.BaselineCount),
                    FindingOrder.RoundProbability(pValue),
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
                LastOccurrenceIn: mostRecent.Session,

                DrillDownCommand: DrillDown.ForTest(FindingKind.Vanished, reference),

                // Unrounded: this is the number the coordinator's Benjamini-Hochberg pass sorts on,
                // and the rounded copy in the evidence is only what gets written down.
                PValue: pValue,

                // Reported quietly by default. A test that silently stopped running is worth
                // knowing about, but it is usually a deliberate deletion, and ranking it alongside
                // a failing test would train people to ignore the top of the report.
                SeverityCeiling: Severity.Low));
        }

        return Report(candidates, tested);
    }

    /// <summary>
    /// Pairs the candidates with the size of the family they were drawn from.
    /// </summary>
    /// <param name="candidates">Absences the gate let through.</param>
    /// <param name="tested">Fingerprints the absence was measured on.</param>
    /// <returns>The provider's report.</returns>
    private static ProviderReport Report(IReadOnlyList<FindingCandidate> candidates, int tested) =>
        new(candidates, new Dictionary<FindingKind, int> { [FindingKind.Vanished] = tested });
}

/// <summary>
/// The fingerprints present on each side of a split, taken over the runs that covered the suite.
/// </summary>
/// <remarks>
/// Not the window's own <see cref="AnalysisWindow.CurrentSlice"/> and
/// <see cref="AnalysisWindow.BaselineSlice"/>. Those are every session in order, and a session that
/// ran a tenth of the suite belongs in neither side of a question about absence — it did not fail to
/// see the tests it excluded, it never looked. So the sessions that covered the suite are taken in
/// order and split again, on <see cref="AnalysisWindow.SliceSizeFor(int)"/>, which is the same rule
/// the window itself narrows by.
/// </remarks>
/// <param name="Current">Fingerprints seen anywhere in the current slice.</param>
/// <param name="BaselineAppearances">Baseline sessions each fingerprint appeared in.</param>
/// <param name="CurrentCount">Sessions in the current slice.</param>
/// <param name="BaselineCount">Sessions in the baseline slice.</param>
/// <param name="PartialSessionsSetAside">Sessions left out of both for covering part of the suite.</param>
internal sealed record AnalysisWindowSlices(
    IReadOnlySet<string> Current,
    IReadOnlyDictionary<string, int> BaselineAppearances,
    int CurrentCount,
    int BaselineCount,
    int PartialSessionsSetAside)
{
    /// <summary>
    /// Derives the split for a window.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <returns>The fingerprints on each side.</returns>
    public static AnalysisWindowSlices From(AnalysisContext context)
    {
        // Newest first, because AnalysisContext builds its views in window order and the window is
        // ordered newest first. Dropping the partial ones preserves that, so the head of what is
        // left is still the most recent thing that ran the suite.
        var covering = new List<TestSession>(context.SessionViews.Count);
        foreach (SessionView view in context.SessionViews)
        {
            if (!view.IsPartial)
                covering.Add(view.Session);
        }

        int sliceSize = AnalysisWindow.SliceSizeFor(covering.Count);

        var current = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < sliceSize; position++)
        {
            foreach (string fingerprint in TestIndex.FingerprintsIn(covering[position]))
                current.Add(fingerprint);
        }

        var baseline = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int position = sliceSize; position < covering.Count; position++)
        {
            foreach (string fingerprint in TestIndex.FingerprintsIn(covering[position]))
            {
                baseline.TryGetValue(fingerprint, out int seen);
                baseline[fingerprint] = seen + 1;
            }
        }

        return new AnalysisWindowSlices(
            current,
            baseline,
            sliceSize,
            covering.Count - sliceSize,
            context.SessionViews.Count - covering.Count);
    }
}
