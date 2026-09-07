/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;

using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class FindingCoordinatorTests
{
    private static AnalysisContext Context(int sessionCount = 6, int testsPerSession = 1)
    {
        string[] names = [.. Enumerable.Range(0, testsPerSession).Select(i => $"Test{i}")];

        return TestSessionFactory.Context(
            [.. Enumerable.Range(0, sessionCount).Select(i => TestSessionFactory.Session(i, names))]);
    }

    [Fact]
    public void AProviderThatThrowsIsRecordedAndTheReportStillRenders()
    {
        var coordinator = new FindingCoordinator(
        [
            new ThrowingProvider(),
            new StubProvider("healthy", FindingKind.Flaky, "Test0")
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        // The working provider's finding survives; only the broken metric is lost.
        Assert.Single(result.Findings);
        Assert.Equal("broken", Assert.Single(result.FailedProviders));
        Assert.Contains("broken", warnings.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AProviderThatThrowsMidEnumerationIsStillContained()
    {
        // Providers are iterators, so the throw happens while their results are being drained rather
        // than when Analyze is called. Catching only the call would miss this entirely.
        var coordinator = new FindingCoordinator([new ThrowingLazyProvider()]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Empty(result.Findings);
        Assert.Equal("lazy-broken", Assert.Single(result.FailedProviders));
    }

    [Fact]
    public void FindingsBelowTheEvidenceFloorAreExcludedAndCounted()
    {
        // Four sessions is below the session floor, so nothing may be reported however confident the
        // provider is.
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0")]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(sessionCount: 4), null, warnings);

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.ExcludedLowEvidence);
    }

    [Fact]
    public void FindingsAtTheEvidenceFloorAreReported()
    {
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0")]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(sessionCount: 5), null, warnings);

        Assert.Single(result.Findings);
        Assert.Equal(0, result.ExcludedLowEvidence);
    }

    // -------------------------------------------------------------------------------------------
    // Multiplicity
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// A finding that tested nothing is corrected for nothing, however large the suite.
    /// </summary>
    /// <remarks>
    /// The bypass `RetryMasked`, `SharedFailure` and `BrokenFixture` rest on. They count things that
    /// demonstrably happened; there is no null hypothesis under which a retry that masked a failure
    /// did not happen, and a false discovery rate over observations is not a question.
    /// </remarks>
    [Fact]
    public void ACandidateThatCarriesNoPValueIsNeverSilencedByMultiplicity()
    {
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.RetryMasked, "Test0", hypothesesTested: 300)]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Single(result.Findings);
        Assert.Equal(0, result.ExcludedNotSignificant);
    }

    /// <summary>
    /// The same p-value is a finding out of one comparison and noise out of three hundred.
    /// </summary>
    /// <remarks>
    /// The whole of the issue in one pair of assertions. A p of 0.04 is the conventional level twice
    /// over on its own, and it is also what one comparison in twenty-five produces with nothing at
    /// all going on — so a suite that ran the comparison three hundred times has seen about twelve
    /// of them and has learnt nothing from any.
    /// </remarks>
    [Theory]
    [InlineData(1, true)]
    [InlineData(300, false)]
    public void APValueIsJudgedAgainstTheNumberOfFingerprintsItsKindWasTestedOn(
        int tested, bool reported)
    {
        var coordinator = new FindingCoordinator(
        [
            new StubProvider(
                "stub", FindingKind.TimeSensitive, "Test0", pValue: 0.04, hypothesesTested: tested)
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);
        Assert.Equal(reported ? 0 : 1, result.ExcludedNotSignificant);
    }

    /// <summary>
    /// A candidate is charged to one reason for not being reported, not to both.
    /// </summary>
    /// <remarks>
    /// The floor runs first, so a candidate resting on four runs is one that needs more runs — which
    /// is what a reader can act on — rather than one that failed a significance bar it was never
    /// really measured against.
    /// </remarks>
    [Fact]
    public void ACandidateBelowTheEvidenceFloorIsCountedThereAndNowhereElse()
    {
        var coordinator = new FindingCoordinator(
        [
            new StubProvider(
                "stub", FindingKind.TimeSensitive, "Test0", pValue: 0.04, hypothesesTested: 300)
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(sessionCount: 4), null, warnings);

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.ExcludedLowEvidence);
        Assert.Equal(0, result.ExcludedNotSignificant);
    }

    /// <summary>
    /// Asking for one kind must not change what that kind reports.
    /// </summary>
    /// <remarks>
    /// The correction is within a kind, and `--kind` narrows a family and its members together, so a
    /// finding cannot appear or vanish according to what else was asked for in the same run. A
    /// correction pooled across kinds would fail this: dropping the concurrency comparisons would
    /// shorten the list the clock findings were ranked in and loosen their bar.
    /// </remarks>
    [Fact]
    public void RestrictingTheReportToOneKindDoesNotChangeWhatThatKindReports()
    {
        StubProvider[] Providers() =>
        [
            new("time", FindingKind.TimeSensitive, "Test0", pValue: 0.001, hypothesesTested: 50),
            new("concurrency", FindingKind.ParallelSensitive, "Test0",
                pValue: 0.0001, hypothesesTested: 50)
        ];

        using var warnings = new StringWriter();

        AnalysisResult everything = new FindingCoordinator(Providers()).Run(Context(), null, warnings);

        AnalysisResult narrowed = new FindingCoordinator(Providers()).Run(
            Context(), new HashSet<FindingKind> { FindingKind.TimeSensitive }, warnings);

        Assert.Contains(everything.Findings, f => f.Kind == FindingKind.TimeSensitive);
        Assert.Contains(narrowed.Findings, f => f.Kind == FindingKind.TimeSensitive);
        Assert.Equal(0, narrowed.ExcludedNotSignificant);
    }

    /// <summary>
    /// Narrowing the report to one kind does not change what that kind could not measure.
    /// </summary>
    /// <remarks>
    /// The third of #185's criteria, and the reason the tally is per kind rather than a total. A
    /// figure that shrank whenever `--kind` was passed would be describing the invocation instead of
    /// the store, and a reader comparing yesterday's full report with today's narrowed one would
    /// read the difference as the suite improving. The filter is applied to the tally with the same
    /// condition it is applied to the family and the candidates, which is what makes this hold.
    /// </remarks>
    [Fact]
    public void TheNotMeasuredTallyForAKindIsUnchangedWhenTheReportIsNarrowedToIt()
    {
        StubProvider[] Providers() =>
        [
            new("time", FindingKind.TimeSensitive, "Test0", awaitingRuns: 11, unreadable: 4),
            new("concurrency", FindingKind.ParallelSensitive, "Test0", unreadable: 97)
        ];

        using var warnings = new StringWriter();

        AnalysisResult everything = new FindingCoordinator(Providers()).Run(Context(), null, warnings);

        AnalysisResult narrowed = new FindingCoordinator(Providers()).Run(
            Context(), new HashSet<FindingKind> { FindingKind.TimeSensitive }, warnings);

        Assert.Equal(
            new NotMeasuredCount(11, 4),
            everything.NotMeasured[FindingKind.TimeSensitive]);

        Assert.Equal(
            new NotMeasuredCount(11, 4),
            narrowed.NotMeasured[FindingKind.TimeSensitive]);

        // And the kind that was filtered out contributes nothing rather than a zero, so a reader
        // cannot mistake "not asked about" for "asked, and every test was readable".
        Assert.True(everything.NotMeasured.ContainsKey(FindingKind.ParallelSensitive));
        Assert.False(narrowed.NotMeasured.ContainsKey(FindingKind.ParallelSensitive));
    }

    /// <summary>
    /// A candidate the floor dropped is not also counted as one nothing could be measured about.
    /// </summary>
    /// <remarks>
    /// The two are opposite statements and #185 exists because they were being told apart nowhere. A
    /// candidate at the floor is a claim the provider computed and this pass withheld for resting on
    /// too little of the test's history; the tally counts tests no claim was ever computed for. A
    /// candidate that reaches the coordinator at all has been measured by definition.
    /// </remarks>
    [Fact]
    public void ACandidateDroppedAtTheFloorIsNotAlsoCountedAsUnmeasured()
    {
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.TimeSensitive, "Test0")]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(sessionCount: 4), null, warnings);

        Assert.Equal(1, result.ExcludedLowEvidence);
        Assert.True(result.NotMeasured[FindingKind.TimeSensitive].IsEmpty);
    }

    /// <summary>
    /// A provider that throws costs its own tally and nobody else's.
    /// </summary>
    /// <remarks>
    /// The same contract the candidates already have. A metric that fell over has said nothing about
    /// its coverage, and publishing a zero for it would say the opposite — that it looked at every
    /// test and could read them all. Absence is what the caveat line's "metrics unavailable" is for.
    /// </remarks>
    [Fact]
    public void AProviderThatThrowsContributesNoTallyAndDoesNotDisturbAnother()
    {
        var coordinator = new FindingCoordinator(
        [
            new ThrowingProvider(),
            new StubProvider("stub", FindingKind.TimeSensitive, "Test0", unreadable: 5)
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Equal(5, result.NotMeasured[FindingKind.TimeSensitive].Unreadable);
        Assert.False(result.NotMeasured.ContainsKey(FindingKind.DurationRegression));
    }

    /// <summary>
    /// A claim the pass silences hands over to the weaker one its provider was holding back.
    /// </summary>
    /// <remarks>
    /// The case a provider cannot decide for itself. `DurationProvider` suppresses the instability
    /// finding for a test it is already calling a regression, because the step that made the
    /// regression is what widened the spread and reporting both states one thing twice. That
    /// suppression is only right while the regression is reported, and whether it is turns on this
    /// pass — so a provider resolving it alone would leave a slow, wildly varying test unmentioned
    /// on the strength of a finding that never appeared.
    /// </remarks>
    [Fact]
    public void ASilencedCandidateHandsOverToTheAlternativeItsProviderOffered()
    {
        var coordinator = new FindingCoordinator(
        [
            new SupersedingProvider(
                FindingKind.DurationRegression,
                FindingKind.DurationUnstable,
                pValue: 0.04,
                hypothesesTested: 300)
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Equal(FindingKind.DurationUnstable, Assert.Single(result.Findings).Kind);

        // The handover is a substitution, not a reprieve: nothing was excluded, because something
        // is reported about the subject.
        Assert.Equal(0, result.ExcludedNotSignificant);
    }

    /// <summary>
    /// A claim that clears its bar keeps its provider's suppression.
    /// </summary>
    [Fact]
    public void ASurvivingCandidateKeepsTheAlternativeSuppressed()
    {
        var coordinator = new FindingCoordinator(
        [
            new SupersedingProvider(
                FindingKind.DurationRegression,
                FindingKind.DurationUnstable,
                pValue: 0.0001,
                hypothesesTested: 300)
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Equal(FindingKind.DurationRegression, Assert.Single(result.Findings).Kind);
    }

    /// <summary>
    /// A kind whose provider never reported a family is corrected against the results themselves.
    /// </summary>
    /// <remarks>
    /// A provider miscounting its own family must not make the correction weaker than the evidence.
    /// Falling back on the number of results is the least the family can honestly be, and it is what
    /// keeps a bug in one provider from quietly turning the pass off for its kind.
    /// </remarks>
    [Fact]
    public void AKindThatReportedNoFamilyIsStillCorrectedAgainstItsOwnResults()
    {
        var coordinator = new FindingCoordinator(
        [
            new StubProvider("stub", FindingKind.TimeSensitive, "Test0", pValue: 0.4)
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.ExcludedNotSignificant);
    }

    [Fact]
    public void TheKindFilterSkipsProvidersThatCannotContribute()
    {
        var flaky = new StubProvider("flaky", FindingKind.Flaky, "Test0");
        var vanished = new StubProvider("vanished", FindingKind.Vanished, "Test0");

        var coordinator = new FindingCoordinator([flaky, vanished]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(
            Context(), new HashSet<FindingKind> { FindingKind.Vanished }, warnings);

        Assert.Equal(FindingKind.Vanished, Assert.Single(result.Findings).Kind);
        Assert.False(flaky.WasRun);
    }

    [Fact]
    public void EvidenceLevelFollowsTheCandidatesOwnDenominator()
    {
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0")]);

        using var warnings = new StringWriter();

        // The stub measures over every run its subject appeared in, and the subject runs once in
        // every session, so all three numbers coincide here. The next test is where they part.
        Assert.Equal(
            EvidenceLevel.Low,
            coordinator.Run(Context(sessionCount: 7), null, warnings).Findings[0].EvidenceLevel);

        Assert.Equal(
            EvidenceLevel.Moderate,
            coordinator.Run(Context(sessionCount: 8), null, warnings).Findings[0].EvidenceLevel);

        Assert.Equal(
            EvidenceLevel.High,
            coordinator.Run(Context(sessionCount: 16), null, warnings).Findings[0].EvidenceLevel);
    }

    [Fact]
    public void AClaimMeasuredOverFewerRunsThanItsTestRanInIsBandedOnTheFewer()
    {
        // #182. A test present in all twenty runs of the window, whose finding was computed from
        // ten of them - the shape every provider produces, because every provider drops runs it
        // cannot read or has discounted. Banded on the subject this published as `high`, which is
        // evidence the split has not got and the top of a three-level scale besides.
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0", evidenceSessions: 10)]);

        using var warnings = new StringWriter();
        Finding finding = coordinator.Run(Context(sessionCount: 20), null, warnings).Findings[0];

        Assert.Equal(20, Context(sessionCount: 20).Tests.SessionsRunIn("fp-Test0"));
        Assert.Equal(EvidenceLevel.Moderate, finding.EvidenceLevel);

        // Published beside the band, so a reader can see which of the two numbers it came from.
        Assert.Equal(10, finding.EvidenceSessions);
    }

    [Fact]
    public void TheReportingFloorStillReadsTheSubjectRatherThanTheClaim()
    {
        // The half of #182 that deliberately did not move. Whether a test is worth reporting at all
        // has to be answered the same way for every kind, or one metric flags a test that another
        // silently drops with nothing on screen to explain it. So a claim resting on two runs is
        // still emitted where its subject has history - it is emitted saying `low`.
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0", evidenceSessions: 2)]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(sessionCount: 20), null, warnings);

        Assert.Equal(0, result.ExcludedLowEvidence);
        Assert.Equal(EvidenceLevel.Low, Assert.Single(result.Findings).EvidenceLevel);
    }

    [Fact]
    public void AnAlternativeIsBandedOnItsOwnDenominatorRatherThanTheClaimItReplaces()
    {
        // The superseding stub's alternative measures over half the runs the silenced claim did,
        // which is the normal case rather than a contrivance: DurationUnstable reads what could be
        // normalised where the DurationRegression it stands in for read what could be compared.
        // Banding the handover on the original would label the replacement with the evidence of a
        // finding that was never printed.
        var coordinator = new FindingCoordinator(
        [
            new SupersedingProvider(
                FindingKind.DurationRegression, FindingKind.DurationUnstable, 0.9, 100)
        ]);

        using var warnings = new StringWriter();
        Finding finding = Assert.Single(
            coordinator.Run(Context(sessionCount: 20), null, warnings).Findings);

        Assert.Equal(FindingKind.DurationUnstable, finding.Kind);
        Assert.Equal(10, finding.EvidenceSessions);
        Assert.Equal(EvidenceLevel.Moderate, finding.EvidenceLevel);
    }

    [Fact]
    public void FindingIdsAreStableAcrossRepeatedReports()
    {
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0")]);

        using var warnings = new StringWriter();

        string first = coordinator.Run(Context(), null, warnings).Findings[0].Id;
        string second = coordinator.Run(Context(), null, warnings).Findings[0].Id;

        Assert.Equal(first, second);
        Assert.StartsWith("f_", first, StringComparison.Ordinal);
    }

    [Fact]
    public void FindingIdsSurviveTheWindowGrowingAsRunsAccumulate()
    {
        // The point of the id: after another `dotnet test`, the same claim about the same test is
        // recognisably the same finding. Hashing the window would renumber it on every run, which
        // is exactly when a reader most needs to tell "seen it" from "that's new".
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0")]);

        using var warnings = new StringWriter();

        string before = coordinator.Run(Context(sessionCount: 6), null, warnings).Findings[0].Id;
        string after = coordinator.Run(Context(sessionCount: 9), null, warnings).Findings[0].Id;

        Assert.Equal(before, after);
    }

    [Fact]
    public void FindingIdsDifferBetweenKindsAboutTheSameTest()
    {
        // kind is the other half of the identity; without it a flaky test and a retry-masked one
        // would share an id and a consumer keyed on it would see one finding where there are two.
        var coordinator = new FindingCoordinator(
        [
            new StubProvider("flaky", FindingKind.Flaky, "Test0"),
            new StubProvider("masked", FindingKind.RetryMasked, "Test0")
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(), null, warnings);

        Assert.Equal(2, result.Findings.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void FindingIdsDifferBetweenSubjects()
    {
        var coordinator = new FindingCoordinator(
        [
            new StubProvider("a", FindingKind.Flaky, "Test0"),
            new StubProvider("b", FindingKind.Flaky, "Test1")
        ]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(testsPerSession: 2), null, warnings);

        Assert.Equal(2, result.Findings.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AFindingRestingOnFiveRunsNeverOutranksTheSameFindingOnForty()
    {
        // The invariant the whole ranking rests on. Both tests fail three runs in five; one has been
        // watched five times and the other forty. Every other term the scorer reads is identical -
        // the test ran in every session of its own window, every failure broke a build, and the
        // newest session is one of the failures - so the ordering here is the sample size and
        // nothing else. Before findings ranked on a bound, the two scored the same.
        Finding thin = OnlyFinding(FailingHalfOf(sessions: 5));
        Finding evidenced = OnlyFinding(FailingHalfOf(sessions: 40));

        Assert.True(evidenced.Impact > thin.Impact, $"{evidenced.Impact} > {thin.Impact}");
        // Published as well as ranked. The evidence level said this all along; until now it never
        // reached the sort.
        Assert.Equal(EvidenceLevel.Low, thin.EvidenceLevel);
        Assert.Equal(EvidenceLevel.High, evidenced.EvidenceLevel);
    }

    [Fact]
    public void TheSameFindingRanksLowerWhereTheWindowIsWeeksRatherThanMinutes()
    {
        // Two windows of twenty sessions, failing in the same ten and clean in the same ten. Every
        // term the scorer reads is identical, the session index of the last failure included: only
        // the wall clock separates them. One is an afternoon of `dotnet watch test`, where the last
        // failure is ten minutes old and nothing has gone cold; the other is three weeks of CI,
        // where the same failure is a fortnight old and genuinely has. Counted in sessions the two
        // scored alike, which is #172 in one assertion.
        Finding dense = OnlyFinding(FailingInTheOlderHalf(TimeSpan.FromMinutes(1)));
        Finding sparse = OnlyFinding(FailingInTheOlderHalf(TimeSpan.FromDays(1.5)));

        Assert.True(dense.Impact > sparse.Impact, $"{dense.Impact} > {sparse.Impact}");
    }

    /// <summary>
    /// Builds a window in which one test fails in the older half and runs clean in the newer.
    /// </summary>
    /// <param name="apart">The gap between one session's start and the next.</param>
    /// <returns>The context.</returns>
    /// <remarks>
    /// The spacing is the only thing a caller varies. Failing in the older half is what puts the
    /// last occurrence far enough back for the two spacings to disagree about how stale it is.
    /// </remarks>
    private static AnalysisContext FailingInTheOlderHalf(TimeSpan apart) =>
        TestSessionFactory.Context(
            [.. Enumerable.Range(0, 20).Select(ordinal =>
                TestSessionFactory.Session(
                    ordinal,
                    [
                        TestSessionFactory.Execution(
                            "Subject",
                            ordinal < 10 ? TestOutcome.Failed : TestOutcome.Passed,
                            errorMessage: ordinal < 10 ? "boom" : null)
                    ],
                    startedAt: TestSessionFactory.Epoch + (apart * ordinal)))]);

    /// <summary>
    /// Builds a window in which one test fails three runs in every five, ending on a failure.
    /// </summary>
    private static AnalysisContext FailingHalfOf(int sessions) =>
        TestSessionFactory.Context(
            [.. Enumerable.Range(0, sessions).Select(ordinal =>
                TestSessionFactory.Session(
                    ordinal,
                    [
                        TestSessionFactory.Execution(
                            "Subject",
                            ordinal % 5 >= 2 ? TestOutcome.Failed : TestOutcome.Passed,
                            errorMessage: ordinal % 5 >= 2 ? "boom" : null)
                    ]))]);

    private static Finding OnlyFinding(AnalysisContext context)
    {
        using var warnings = new StringWriter();

        return Assert.Single(
            new FindingCoordinator([new FailureModeProvider()]).Run(context, null, warnings).Findings);
    }

    /// <summary>
    /// Emits one finding about a named test, with a fixed unreliability.
    /// </summary>
    /// <remarks>
    /// <paramref name="pValue"/> and <paramref name="hypothesesTested"/> are what the multiplicity
    /// pass reads. Left at their defaults the stub is an observation of something that happened,
    /// which is the shape <c>RetryMasked</c> and <c>SharedFailure</c> have and the shape every test
    /// written before that pass existed assumed.
    /// <para>
    /// <paramref name="evidenceSessions"/> defaults to every session the subject ran in, which is
    /// the shape of a kind that sets nothing aside. A test that wants the mismatch this stub cannot
    /// otherwise produce — a claim measured over fewer runs than its subject appeared in — passes
    /// its own.
    /// </para>
    /// </remarks>
    private sealed class StubProvider(
        string name,
        FindingKind kind,
        string test,
        double unreliability = 0.5,
        double? pValue = null,
        int hypothesesTested = 0,
        int? evidenceSessions = null,
        int awaitingRuns = 0,
        int unreadable = 0)
        : IFindingProvider
    {
        public string Name { get; } = name;

        public IReadOnlyList<FindingKind> Kinds { get; } = [kind];

        public bool WasRun { get; private set; }

        public ProviderReport Analyze(AnalysisContext context)
        {
            WasRun = true;

            var family = new Dictionary<FindingKind, int>();

            if (hypothesesTested > 0)
                family[kind] = hypothesesTested;

            var notMeasured = new Dictionary<FindingKind, NotMeasuredCount>
            {
                [kind] = new NotMeasuredCount(awaitingRuns, unreadable)
            };

            TestReference? reference = context.Tests.ReferenceFor($"fp-{test}");
            if (reference == null)
                return new ProviderReport([], family, notMeasured);

            return new ProviderReport(
                [
                    new FindingCandidate(
                        kind,
                        new FindingSubject.SingleTest(reference),
                        new StubEvidence(1),
                        unreliability,
                        LastOccurrenceIn: context.Window.Sessions[0],
                        DrillDownCommand: "xping report",
                        EvidenceSessions:
                            evidenceSessions ?? context.Tests.SessionsRunIn($"fp-{test}"),
                        PValue: pValue)
                ],
                family,
                notMeasured);
        }
    }

    /// <summary>
    /// Emits one tested claim about a test, holding a second, untested one behind it.
    /// </summary>
    private sealed class SupersedingProvider(
        FindingKind kind, FindingKind alternative, double pValue, int hypothesesTested)
        : IFindingProvider
    {
        public string Name => "superseding";

        public IReadOnlyList<FindingKind> Kinds { get; } = [kind, alternative];

        public ProviderReport Analyze(AnalysisContext context)
        {
            var family = new Dictionary<FindingKind, int> { [kind] = hypothesesTested };

            TestReference? reference = context.Tests.ReferenceFor("fp-Test0");
            if (reference == null)
                return new ProviderReport([], family);

            var subject = new FindingSubject.SingleTest(reference);

            return new ProviderReport(
                [
                    new FindingCandidate(
                        kind,
                        subject,
                        new StubEvidence(1),
                        0.5,
                        LastOccurrenceIn: context.Window.Sessions[0],
                        DrillDownCommand: "xping report",
                        EvidenceSessions: context.Tests.SessionsRunIn("fp-Test0"),
                        PValue: pValue,
                        Instead: new FindingCandidate(
                            alternative,
                            subject,
                            new StubEvidence(2),
                            0.4,
                            LastOccurrenceIn: context.Window.Sessions[0],
                            DrillDownCommand: "xping report",

                            // Half the runs the claim it replaces was measured over, so the
                            // handover can be seen to band on its own denominator.
                            EvidenceSessions: context.Tests.SessionsRunIn("fp-Test0") / 2))
                ],
                family);
        }
    }

    private sealed class ThrowingProvider : IFindingProvider
    {
        public string Name => "broken";

        public IReadOnlyList<FindingKind> Kinds => [FindingKind.DurationRegression];

        public ProviderReport Analyze(AnalysisContext context) =>
            throw new InvalidOperationException("metric exploded");
    }

    private sealed class ThrowingLazyProvider : IFindingProvider
    {
        public string Name => "lazy-broken";

        public IReadOnlyList<FindingKind> Kinds => [FindingKind.ParallelSensitive];

        public ProviderReport Analyze(AnalysisContext context) =>
            ProviderReport.Observations(
                [.. Candidates()], ReadOnlyDictionary<FindingKind, NotMeasuredCount>.Empty);

        private static IEnumerable<FindingCandidate> Candidates()
        {
            yield return Explode();
        }

        private static FindingCandidate Explode() =>
            throw new InvalidOperationException("metric exploded while enumerating");
    }

    private sealed record StubEvidence(int Occurrences) : FindingEvidence;
}
