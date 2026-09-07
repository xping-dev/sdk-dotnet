/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Tests.Report;

public sealed class VanishedProviderTests
{
    /// <summary>
    /// Builds sessions in which the newest ones stop running a test.
    /// </summary>
    /// <param name="total">Sessions to build.</param>
    /// <param name="presentIn">How many of the oldest sessions include the vanishing test.</param>
    private static AnalysisContext Context(int total, int presentIn)
    {
        var sessions = new List<TestSession>();

        for (int i = 0; i < total; i++)
        {
            sessions.Add(i < presentIn
                ? TestSessionFactory.Session(i, "Stable", "Removed")
                : TestSessionFactory.Session(i, "Stable"));
        }

        return TestSessionFactory.Context([.. sessions]);
    }

    private static IReadOnlyList<FindingCandidate> Analyze(AnalysisContext context) =>
        new VanishedProvider().Analyze(context).Candidates;

    private static int Family(AnalysisContext context) =>
        new VanishedProvider().Analyze(context).HypothesesTested
            .GetValueOrDefault(FindingKind.Vanished);

    private static NotMeasuredCount NotMeasured(AnalysisContext context) =>
        new VanishedProvider().Analyze(context).NotMeasured
            .GetValueOrDefault(FindingKind.Vanished);

    /// <summary>
    /// A test the baseline barely saw is waiting for history, and never unreadable.
    /// </summary>
    /// <remarks>
    /// A session appearance is the one observation every adapter records by existing: a test either
    /// ran in a run or it did not. So this kind has no unreadable half at all, and the published
    /// zero says so rather than leaving a reader to infer it from an absence.
    /// </remarks>
    [Fact]
    public void ATestTheBaselineBarelySawIsCountedAsAwaitingRunsAndNeverAsUnreadable()
    {
        // `Removed` appears in one baseline run, under the floor the absence is measured against;
        // `Stable` appears throughout and is measured.
        AnalysisContext context = Context(total: 8, presentIn: 1);

        NotMeasuredCount count = NotMeasured(context);

        Assert.Equal(1, count.AwaitingRuns);
        Assert.Equal(0, count.Unreadable);
    }

    /// <summary>
    /// A window with no baseline counts every test, not none of them.
    /// </summary>
    /// <remarks>
    /// The whole-window decline has to come out as the same number the per-test gate would have
    /// produced one test at a time. A tally that collapsed the moment a second full run arrived
    /// would be describing the store's shape rather than the suite's.
    /// </remarks>
    [Fact]
    public void AWindowWithNoBaselineCountsEveryTestRatherThanNone()
    {
        AnalysisContext context = Context(total: 1, presentIn: 1);

        NotMeasuredCount count = NotMeasured(context);

        Assert.Equal(context.Tests.Fingerprints.Count, count.AwaitingRuns);
        Assert.Equal(0, count.Unreadable);
    }

    /// <summary>
    /// A test that is still running is an asking that answered no, not an asking that never happened.
    /// </summary>
    /// <remarks>
    /// The multiplicity this kind has to be charged for is every established test in the window, and
    /// counting only the absent ones would describe a family in which every member is a discovery:
    /// a suite of three hundred stable tests holding one absence would report a family of one and
    /// pass that absence through the coordinator uncorrected. `Stable` runs throughout and `Removed`
    /// stops, so the family is two.
    /// </remarks>
    [Fact]
    public void TheFamilyIsEveryEstablishedTestAndNotOnlyTheAbsentOnes()
    {
        AnalysisContext context = Context(total: 8, presentIn: 5);

        Assert.Single(Analyze(context));
        Assert.Equal(2, Family(context));
    }

    /// <summary>
    /// A test with no habit behind it is a question that could not be asked.
    /// </summary>
    /// <remarks>
    /// The other half of the boundary: the family is what the data could answer, so a fingerprint
    /// the baseline barely saw is outside it whichever slice it is in now. Padding the family with
    /// those would tighten every bar for nothing.
    /// </remarks>
    [Fact]
    public void ATestWithoutABaselineHabitIsNotInTheFamily()
    {
        var sessions = new List<TestSession>();

        for (int i = 0; i < 8; i++)
        {
            // `Stable` throughout, `Occasional` in two baseline sessions only — one short of the
            // habit this kind needs before an absence could mean anything.
            sessions.Add(i < 2
                ? TestSessionFactory.Session(i, "Stable", "Occasional")
                : TestSessionFactory.Session(i, "Stable"));
        }

        Assert.Equal(1, Family(TestSessionFactory.Context([.. sessions])));
    }

    /// <summary>
    /// A window with no baseline or no current slice asked nothing of anyone.
    /// </summary>
    [Fact]
    public void AWindowWithNothingToCompareReportsNoFamily()
    {
        Assert.Equal(0, Family(TestSessionFactory.Context(TestSessionFactory.Session(0, "Stable"))));
    }

    [Fact]
    public void ATestThatStopsRunningIsReported()
    {
        // Eight sessions gives a current slice of three; the test ran in the oldest five.
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 5));

        FindingCandidate candidate = Assert.Single(candidates);
        Assert.Equal(FindingKind.Vanished, candidate.Kind);

        var subject = Assert.IsType<FindingSubject.SingleTest>(candidate.Subject);
        Assert.Equal("fp-Removed", subject.Test.TestFingerprint);
    }

    [Fact]
    public void EvidenceCarriesTheDenominatorsBehindTheClaim()
    {
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 5));

        var evidence = Assert.IsType<VanishedEvidence>(Assert.Single(candidates).Evidence);

        Assert.Equal(5, evidence.BaselineSessions);
        Assert.Equal(5, evidence.BaselineSessionCount);
        Assert.Equal(3, evidence.CurrentSessionCount);
        Assert.Equal(5, evidence.ExecutionsInWindow);
    }

    [Fact]
    public void EvidenceCarriesTheRunRateAndThePValueBehindTheClaim()
    {
        // Both, and not just the counts. "ran in 12 of 17 earlier runs" is the same sentence whether
        // the test was a habit or an occasional visitor, and the p-value is what tells them apart.
        // Five baseline appearances of five, none in the current three: one deal in fifty-six.
        var evidence = Assert.IsType<VanishedEvidence>(
            Assert.Single(Analyze(Context(total: 8, presentIn: 5))).Evidence);

        Assert.Equal(1.0, evidence.BaselineRunRate);
        Assert.Equal(0.0179, evidence.PValue);

        // Twelve of seventeen on a default window, which is where the bar actually sits.
        var wider = Assert.IsType<VanishedEvidence>(
            Assert.Single(Analyze(Context(total: 20, presentIn: 12))).Evidence);

        Assert.Equal(0.706, wider.BaselineRunRate);
        Assert.Equal(0.0491, wider.PValue);
    }

    [Theory]
    [InlineData(3, false)]      // 3 of 17: more likely than not to miss the last three anyway
    [InlineData(8, false)]      // 8 of 17: p 0.19, still the sort of thing that happens
    [InlineData(12, true)]      // 12 of 17: p 0.049, the first table on this window that carries
    [InlineData(17, true)]      // ran in every one of them and then stopped
    public void AbsenceIsReportedOnlyWhereItWasNotTheLikelyThingToHappen(int presentIn, bool reported)
    {
        // The defect this gate replaced was a count: three appearances qualified whether they were
        // three sessions out of three or three out of seventeen. On a default twenty-session window
        // the second is absent from the current slice 56% of the time with nothing having changed.
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 20, presentIn));

        Assert.Equal(reported ? 1 : 0, candidates.Count);
    }

    [Fact]
    public void TheCandidateHandsThePValueToTheCoordinatorUnrounded()
    {
        // The number #160's Benjamini-Hochberg pass sorts on. The evidence publishes a copy rounded
        // to three significant digits; this one must not be it.
        FindingCandidate candidate = Assert.Single(Analyze(Context(total: 20, presentIn: 12)));

        Assert.NotNull(candidate.PValue);
        Assert.Equal(0.0491228, candidate.PValue!.Value, 7);
    }

    [Fact]
    public void AWindowTooShortForAThreeSessionSliceReportsNothingOfThisKind()
    {
        // Below SmallWindowSessionCount the current slice narrows to one session, and one session's
        // absence is not evidence that anything stopped: six baseline appearances of six missing a
        // single run is one deal in seven. Deliberate, and the reason the kind is silent on a short
        // history rather than loud on it.
        Assert.Empty(Analyze(Context(total: 7, presentIn: 6)));
    }

    [Fact]
    public void ATestStillRunningIsNotReported()
    {
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 8));

        Assert.Empty(candidates);
    }

    [Fact]
    public void ATestSeenTooFewTimesToHaveBeenEstablishedIsNotReported()
    {
        // Two baseline appearances is below the minimum: a test seen once or twice and never again
        // was probably never really there, and calling that a change would be noise. The p-value gate
        // refuses it as well — two appearances split that way is better than one deal in three —
        // which is why the minimum is a guard rather than a decision.
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 2));

        Assert.Empty(candidates);
    }

    [Fact]
    public void AWindowWithNoBaselineProducesNothing()
    {
        // With one session everything is in the current slice, so no test can be absent from it.
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, "Stable", "Removed"));

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void UnreliabilityRisesWithHowEstablishedTheTestWas()
    {
        // Ran in every baseline session, then stopped — a starker change than one that missed a run
        // here and there before it went. Both clear the gate; this is only the ranking between them.
        FindingCandidate everySession = Assert.Single(Analyze(Context(total: 8, presentIn: 5)));
        FindingCandidate someSessions = Assert.Single(Analyze(Context(total: 10, presentIn: 6)));

        // Bounded rather than taken raw, so five of five is 0.57 and not the certainty a ratio of
        // one would claim. The comparison the finding rests on is unaffected.
        Assert.Equal(0.566, everySession.Unreliability, 3);
        Assert.Equal(0.487, someSessions.Unreliability, 3);
    }

    [Fact]
    public void CandidatesComeOutInFingerprintOrder()
    {
        var sessions = new List<TestSession>();
        for (int i = 0; i < 8; i++)
        {
            sessions.Add(i < 5
                ? TestSessionFactory.Session(i, "Stable", "Zulu", "Alpha", "Mike")
                : TestSessionFactory.Session(i, "Stable"));
        }

        IReadOnlyList<FindingCandidate> candidates =
            Analyze(TestSessionFactory.Context([.. sessions]));

        string[] fingerprints =
            [.. candidates.Select(c => ((FindingSubject.SingleTest)c.Subject).Test.TestFingerprint)];

        Assert.Equal(fingerprints.OrderBy(f => f, StringComparer.Ordinal), fingerprints);
    }

    [Fact]
    public void VanishedIsReportedQuietlyEvenWhenItScoresHighly()
    {
        // A test that ran in every session and then stopped scores highly on every term the generic
        // impact formula measures. It is still usually a deliberate deletion, so it must not sort
        // above a genuinely failing test.
        var coordinator = new FindingCoordinator([new VanishedProvider()]);

        using var warnings = new StringWriter();
        Finding finding = Assert.Single(
            coordinator.Run(Context(total: 8, presentIn: 5), null, warnings).Findings);

        Assert.Equal(Severity.Low, finding.Severity);
        Assert.True(finding.Impact > LocalAnalysisConstants.SeverityMediumThreshold);
    }

    [Theory]
    [InlineData(5, false)]      // p 0.083 — declined here, though the coordinator would have taken it
    [InlineData(6, true)]       // p 0.033
    public void ThePValueGateBindsBeforeEitherSessionFloor(int presentIn, bool reported)
    {
        // Which of the three gates actually decides, pinned so that the constants' remarks cannot
        // quietly stop being true. Five baseline appearances of seven is exactly
        // MinimumSessionsPerTestToReport and comfortably above VanishedMinBaselineSessions, so both
        // floors would admit it; the provider declines it anyway, because five appearances landing in
        // seven baseline runs and none in the current three is one deal in twelve. No appearance
        // count below five clears the p-value gate at any baseline size, so neither floor can ever
        // be the binding one.
        var coordinator = new FindingCoordinator([new VanishedProvider()]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(total: 10, presentIn), null, warnings);

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);

        // And it is the provider that decided, not the floor applied after it.
        Assert.Equal(reported ? 1 : 0, Analyze(Context(total: 10, presentIn)).Count);
    }

    [Fact]
    public void TheProviderReachesTheReportEndToEnd()
    {
        var coordinator = new FindingCoordinator([new VanishedProvider()]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(total: 8, presentIn: 5), null, warnings);

        Finding finding = Assert.Single(result.Findings);

        Assert.Equal(FindingKind.Vanished, finding.Kind);
        Assert.Empty(result.FailedProviders);
        Assert.StartsWith("f_", finding.Id, StringComparison.Ordinal);
        Assert.Contains("--kind Vanished", finding.DrillDownCommand, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds a suite of <paramref name="suiteSize"/> tests, the newest sessions running only some.
    /// </summary>
    /// <param name="total">Sessions to build.</param>
    /// <param name="filtered">How many of the newest sessions run a reduced set.</param>
    /// <param name="suiteSize">Tests the full runs execute.</param>
    /// <param name="selected">Tests the reduced runs execute.</param>
    private static AnalysisContext Suite(int total, int filtered, int suiteSize, int selected)
    {
        string[] suite = [.. Enumerable.Range(0, suiteSize).Select(i => $"T{i:00}")];

        var sessions = new List<TestSession>();
        for (int i = 0; i < total; i++)
        {
            // Ordinal 0 is the oldest, so the filtered runs are the last ones built.
            bool reduced = i >= total - filtered;
            sessions.Add(TestSessionFactory.Session(i, reduced ? suite[..selected] : suite));
        }

        return TestSessionFactory.Context([.. sessions]);
    }

    [Fact]
    public void AFilteredRunIsNotASessionEveryUnselectedTestVanishedFrom()
    {
        // The defect, exactly as reported: seventeen full runs of a suite, then three runs under a
        // `dotnet test --filter` naming one test. Every other test is absent from all three, ran in
        // all seventeen, and scores p = 8.8e-4 — the best any twenty-run window can do. The absence
        // is real and the conclusion is false: those runs never asked about the other sixteen.
        Assert.Empty(Analyze(Suite(total: 20, filtered: 3, suiteSize: 17, selected: 1)));
    }

    [Fact]
    public void TheFamilyIsTheQuestionsTheFullRunsCouldAnswer()
    {
        // Setting the filtered runs aside does not empty the family, and should not. Seventeen full
        // runs remain, every test in the suite is still asked whether it stopped, and seventeen
        // askings that answered no is what the Benjamini-Hochberg pass has to be charged for — the
        // multiplicity is real even though none of it became a finding.
        Assert.Equal(17, Family(Suite(total: 20, filtered: 3, suiteSize: 17, selected: 1)));
    }

    [Fact]
    public void AWindowWithTooFewFullRunsToSplitAsksNothingAtAll()
    {
        // Where the filtered runs leave a single run covering the suite there is a "now" and no
        // "before", so the kind returns an empty family rather than no candidates. The difference
        // matters to the coordinator: a family of seventeen would tighten the bar for a comparison
        // that was never actually made.
        AnalysisContext context = Suite(total: 20, filtered: 19, suiteSize: 17, selected: 1);

        Assert.Empty(Analyze(context));
        Assert.Equal(0, Family(context));
    }

    [Fact]
    public void ARunThatStillCoversTheSuiteIsNotSetAside()
    {
        // The control for the two above. Same shape, same denominators, but the last three runs
        // execute the whole suite bar the one test that genuinely stopped — so the absence stands.
        IReadOnlyList<FindingCandidate> candidates =
            Analyze(Suite(total: 20, filtered: 3, suiteSize: 17, selected: 16));

        FindingCandidate candidate = Assert.Single(candidates);
        var evidence = Assert.IsType<VanishedEvidence>(candidate.Evidence);

        Assert.Equal(0, evidence.PartialSessionsSetAside);
        Assert.Equal(17, evidence.BaselineSessionCount);
        Assert.Equal(3, evidence.CurrentSessionCount);
    }

    [Fact]
    public void TheNowIsTheMostRecentRunsThatCoveredTheSuiteAndNotTheMostRecentRuns()
    {
        // A re-split, not a filter of the window's own slices. Three filtered runs sit at the head
        // of this window; dropping them from a current slice of three would leave nothing to ask
        // about and the kind would go silent. Taking the three most recent runs that covered the
        // suite instead still finds the test that stopped before them.
        string[] suite = ["A", "B", "C", "D"];

        var sessions = new List<TestSession>();
        for (int i = 0; i < 17; i++)
            sessions.Add(TestSessionFactory.Session(i, suite));          // 0-16: the whole suite
        for (int i = 17; i < 20; i++)
            sessions.Add(TestSessionFactory.Session(i, "A", "B", "C"));  // 17-19: D has stopped
        for (int i = 20; i < 23; i++)
            sessions.Add(TestSessionFactory.Session(i, "A"));            // 20-22: under a filter

        FindingCandidate candidate = Assert.Single(
            Analyze(TestSessionFactory.Context([.. sessions])));

        var evidence = Assert.IsType<VanishedEvidence>(candidate.Evidence);

        Assert.Equal(3, evidence.PartialSessionsSetAside);
        Assert.Equal(3, evidence.CurrentSessionCount);
        Assert.Equal(17, evidence.BaselineSessionCount);
        Assert.Equal(17, evidence.BaselineSessions);
    }

    [Fact]
    public void AFilteredBaselineIsNotAHabitTheTestFailedToKeep()
    {
        // The other half. Interleave the filtered runs through the baseline and a test that ran in
        // every run that asked for it reads as a 5-of-17 occasional visitor, which is the one shape
        // the p-value gate exists to decline. Counted over the runs that covered the suite it is
        // 5 of 5, and the absence carries.
        //
        // Eight runs cover the suite, at every second ordinal; the five oldest of them run "F" and
        // the three newest do not. The twelve between them name one test and are set aside, so the
        // table is 5 of 5 against 3 — one deal in fifty-six — rather than 5 of 17 against 3.
        var sessions = new List<TestSession>();
        for (int i = 0; i < 20; i++)
        {
            if (i % 2 != 0 || i > 14)
                sessions.Add(TestSessionFactory.Session(i, "A"));
            else if (i <= 8)
                sessions.Add(TestSessionFactory.Session(i, "A", "B", "C", "D", "E", "F"));
            else
                sessions.Add(TestSessionFactory.Session(i, "A", "B", "C", "D", "E"));
        }

        FindingCandidate candidate = Assert.Single(
            Analyze(TestSessionFactory.Context([.. sessions])));

        Assert.Equal("fp-F", Assert.IsType<FindingSubject.SingleTest>(candidate.Subject).Test.TestFingerprint);

        var evidence = Assert.IsType<VanishedEvidence>(candidate.Evidence);

        Assert.Equal(5, evidence.BaselineSessions);
        Assert.Equal(5, evidence.BaselineSessionCount);
        Assert.Equal(3, evidence.CurrentSessionCount);
        Assert.Equal(12, evidence.PartialSessionsSetAside);
        Assert.Equal(1.0, evidence.BaselineRunRate);
    }

    [Fact]
    public void TheEvidenceCountsTheHabitRatherThanEveryRunTheTestAppearedIn()
    {
        // #182. D ran in twenty runs of this window: seventeen that covered the suite, and three
        // filtered ones that named only D. The absence is a change from the seventeen — the
        // filtered three are set aside from both slices before anything is counted, and the current
        // slice holds none of D's appearances by construction.
        var sessions = new List<TestSession>();
        for (int i = 0; i < 17; i++)
            sessions.Add(TestSessionFactory.Session(i, "A", "B", "C", "D"));
        for (int i = 17; i < 20; i++)
            sessions.Add(TestSessionFactory.Session(i, "D"));
        for (int i = 20; i < 23; i++)
            sessions.Add(TestSessionFactory.Session(i, "A", "B", "C"));

        AnalysisContext context = TestSessionFactory.Context([.. sessions]);
        FindingCandidate candidate = Assert.Single(Analyze(context));

        var evidence = Assert.IsType<VanishedEvidence>(candidate.Evidence);

        Assert.Equal(20, context.Tests.SessionsRunIn("fp-D"));
        Assert.Equal(3, evidence.PartialSessionsSetAside);
        Assert.Equal(17, candidate.EvidenceSessions);
        Assert.Equal(evidence.BaselineSessions, candidate.EvidenceSessions);
    }

    [Fact]
    public void TheEvidenceSaysHowManyRunsCoveredOnlyPartOfTheSuite()
    {
        // Otherwise the denominators are unexplainable: a reader who asked for twenty-three runs is
        // being shown a claim about twenty, and nothing on the finding says which twenty or why.
        string[] suite = ["A", "B", "C", "D"];

        var sessions = new List<TestSession>();
        for (int i = 0; i < 17; i++)
            sessions.Add(TestSessionFactory.Session(i, suite));
        for (int i = 17; i < 20; i++)
            sessions.Add(TestSessionFactory.Session(i, "A", "B", "C"));
        for (int i = 20; i < 23; i++)
            sessions.Add(TestSessionFactory.Session(i, "A"));

        FindingCandidate candidate = Assert.Single(
            Analyze(TestSessionFactory.Context([.. sessions])));

        (string headline, IReadOnlyList<MetricDto> metrics) =
            EvidenceHeadline.For(FindingKind.Vanished, candidate.Evidence);

        // Both clauses, not just the first. "the last 3" and "the last 3 full runs" are different
        // runs once anything has been set aside, and a headline is read a clause at a time.
        Assert.Equal(
            "ran in 17 of 17 earlier full runs, absent from the last 3 full runs",
            headline);

        Assert.Contains(
            metrics,
            m => m.Label == "set aside" && m.Value == "3 runs that covered part of the suite");
    }

    [Fact]
    public void AnOrdinaryStoreIsNotToldAboutRunsItDoesNotHave()
    {
        // The qualification is earned, not standing. With nothing set aside there is nothing for
        // the word "full" to distinguish the runs from, and the shorter sentence is the true one.
        FindingCandidate candidate = Assert.Single(Analyze(Context(total: 20, presentIn: 17)));

        (string headline, IReadOnlyList<MetricDto> metrics) =
            EvidenceHeadline.For(FindingKind.Vanished, candidate.Evidence);

        // Byte for byte the sentence this kind has always printed: with nothing set aside, "the
        // last 3" can mean nothing but the last three runs.
        Assert.Equal("ran in 17 of 17 earlier runs, absent from the last 3", headline);
        Assert.DoesNotContain(metrics, m => m.Label == "set aside");
    }

    [Fact]
    public void ADeletionOfMostOfASuiteIsNotReported()
    {
        // The cost of deciding this on counts, pinned rather than left to be discovered. Sixteen of
        // seventeen tests removed and one kept is arithmetically indistinguishable from a filter
        // selecting that one, so the report says nothing. Deliberate: the finding is capped at
        // Severity.Low because a disappearance is usually something the developer just did, and the
        // false positive it trades against arrives once per unselected test on every filtered run.
        Assert.Empty(Analyze(Suite(total: 20, filtered: 3, suiteSize: 17, selected: 1)));

        // A deletion that leaves most of the suite standing still reports, which is the case the
        // kind is actually for.
        Assert.Single(Analyze(Suite(total: 20, filtered: 3, suiteSize: 17, selected: 16)));
    }
}
