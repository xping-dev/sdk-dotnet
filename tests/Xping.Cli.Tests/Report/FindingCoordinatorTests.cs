/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
    public void EvidenceLevelFollowsTheSubjectsSessionCount()
    {
        var coordinator = new FindingCoordinator(
            [new StubProvider("stub", FindingKind.Flaky, "Test0")]);

        using var warnings = new StringWriter();

        // The subject runs once in every session, so its session count is the window's.
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
    /// </remarks>
    private sealed class StubProvider(
        string name,
        FindingKind kind,
        string test,
        double unreliability = 0.5,
        double? pValue = null,
        int hypothesesTested = 0)
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

            TestReference? reference = context.Tests.ReferenceFor($"fp-{test}");
            if (reference == null)
                return new ProviderReport([], family);

            return new ProviderReport(
                [
                    new FindingCandidate(
                        kind,
                        new FindingSubject.SingleTest(reference),
                        new StubEvidence(1),
                        unreliability,
                        SessionsSinceLastOccurrence: 0,
                        DrillDownCommand: "xping report",
                        PValue: pValue)
                ],
                family);
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
                        SessionsSinceLastOccurrence: 0,
                        DrillDownCommand: "xping report",
                        PValue: pValue,
                        Instead: new FindingCandidate(
                            alternative,
                            subject,
                            new StubEvidence(2),
                            0.4,
                            SessionsSinceLastOccurrence: 0,
                            DrillDownCommand: "xping report"))
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
            ProviderReport.Observations([.. Candidates()]);

        private static IEnumerable<FindingCandidate> Candidates()
        {
            yield return Explode();
        }

        private static FindingCandidate Explode() =>
            throw new InvalidOperationException("metric exploded while enumerating");
    }

    private sealed record StubEvidence(int Occurrences) : FindingEvidence;
}
