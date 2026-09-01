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
    private sealed class StubProvider(string name, FindingKind kind, string test, double unreliability = 0.5)
        : IFindingProvider
    {
        public string Name { get; } = name;

        public IReadOnlyList<FindingKind> Kinds { get; } = [kind];

        public bool WasRun { get; private set; }

        public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
        {
            WasRun = true;

            TestReference? reference = context.Tests.ReferenceFor($"fp-{test}");
            if (reference == null)
                yield break;

            yield return new FindingCandidate(
                kind,
                new FindingSubject.SingleTest(reference),
                new StubEvidence(1),
                unreliability,
                SessionsSinceLastOccurrence: 0,
                DrillDownCommand: "xping report");
        }
    }

    private sealed class ThrowingProvider : IFindingProvider
    {
        public string Name => "broken";

        public IReadOnlyList<FindingKind> Kinds => [FindingKind.DurationRegression];

        public IEnumerable<FindingCandidate> Analyze(AnalysisContext context) =>
            throw new InvalidOperationException("metric exploded");
    }

    private sealed class ThrowingLazyProvider : IFindingProvider
    {
        public string Name => "lazy-broken";

        public IReadOnlyList<FindingKind> Kinds => [FindingKind.ParallelSensitive];

        public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
        {
            yield return Explode();
        }

        private static FindingCandidate Explode() =>
            throw new InvalidOperationException("metric exploded while enumerating");
    }

    private sealed record StubEvidence(int Occurrences) : FindingEvidence;
}
