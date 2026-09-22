/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class LatestRunAnalyzerTests
{
    private static readonly IReadOnlyList<Finding> NoFindings = [];

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a window from oldest to newest, one entry per session, each entry naming the tests
    /// that session ran and how each ended. The last entry is the newest session.
    /// </summary>
    private static AnalysisContext Context(params (string Name, TestOutcome Outcome)[][] history)
    {
        var sessions = new List<TestSession>();

        for (int ordinal = 0; ordinal < history.Length; ordinal++)
        {
            sessions.Add(TestSessionFactory.Session(
                ordinal,
                history[ordinal]
                    .Select(t => TestSessionFactory.Execution(
                        t.Name, t.Outcome, errorMessage: t.Outcome.IsFailure() ? "boom" : null))
                    .ToList()));
        }

        return TestSessionFactory.Context([.. sessions]);
    }

    private static (string, TestOutcome) Pass(string name) => (name, TestOutcome.Passed);

    private static (string, TestOutcome) Fail(string name) => (name, TestOutcome.Failed);

    private static (string, TestOutcome) Skip(string name) => (name, TestOutcome.Skipped);

    private static Finding FindingOn(string id, params string[] names) =>
        new(
            id,
            FindingKind.Flaky,
            Severity.Medium,
            EvidenceLevel.Moderate,
            10,
            names.Length == 1
                ? new FindingSubject.SingleTest(Reference(names[0]))
                : new FindingSubject.Group(id, [.. names.Select(Reference)]),
            new StubEvidence(),
            "xping report",
            0.5);

    private static TestReference Reference(string name) =>
        new($"fp-{name}", $"MyApp.Tests.SampleTests.{name}", name, "SampleTests.cs", 10, "MyApp.Tests");

    private static LatestRunAnalysis Analyze(AnalysisContext context, bool showAll = false) =>
        LatestRunAnalyzer.Analyze(context, NoFindings, showAll)!;

    private static LatestRunFailure Single(LatestRunAnalysis analysis) =>
        Assert.Single(analysis.Failures);

    // ---------------------------------------------------------------------------------------
    // D3 — classification
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatHadAlwaysPassedIsNew()
    {
        LatestRunAnalysis analysis = Analyze(Context(
            [Pass("Alpha")],
            [Pass("Alpha")],
            [Pass("Alpha")],
            [Fail("Alpha")]));

        LatestRunFailure row = Single(analysis);
        Assert.Equal(LatestRunStatus.New, row.Status);
        Assert.Equal(3, row.PriorSessions);
        Assert.Equal(0, row.PriorFailures);
        Assert.Equal("fp-Alpha", row.Test.TestFingerprint);
        Assert.True(row.Execution.Outcome.IsFailure());
    }

    [Fact]
    public void ATestSeenInNoPriorSessionIsANewTest()
    {
        LatestRunAnalysis analysis = Analyze(Context(
            [Pass("Stable")],
            [Pass("Stable"), Fail("Alpha")]));

        LatestRunFailure row = Single(analysis);
        Assert.Equal(LatestRunStatus.NewTest, row.Status);
        Assert.Equal(0, row.PriorSessions);
        Assert.Equal(0, row.PriorFailures);
    }

    [Fact]
    public void ATestThatHadFailedBeforeIsSeenBefore()
    {
        // Four sessions: below every finding gate, so nothing else in the report can say this.
        LatestRunAnalysis analysis = Analyze(Context(
            [Fail("Alpha")],
            [Pass("Alpha")],
            [Fail("Alpha")],
            [Fail("Alpha")]));

        LatestRunFailure row = Single(analysis);
        Assert.Equal(LatestRunStatus.SeenBefore, row.Status);
        Assert.Equal(3, row.PriorSessions);
        Assert.Equal(2, row.PriorFailures);
    }

    [Fact]
    public void ATestThatPassedTheNewestSessionIsNotARow()
    {
        LatestRunAnalysis analysis = Analyze(Context(
            [Fail("Alpha")],
            [Pass("Alpha")]));

        Assert.Empty(analysis.Failures);
        Assert.Equal(0, analysis.FailuresTotal);
        Assert.Equal(1, analysis.TestsExecuted);
        Assert.Equal(0, analysis.TestsFailed);
    }

    [Fact]
    public void ATimedOutTestIsAFailureLikeAnyOther()
    {
        LatestRunAnalysis analysis = Analyze(Context(
            [Pass("Alpha")],
            [("Alpha", TestOutcome.Timeout)]));

        Assert.Equal(LatestRunStatus.New, Single(analysis).Status);
    }

    // ---------------------------------------------------------------------------------------
    // D3 — skipped tests are not appearances (Amendment 2A)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestSkippedInTheNewestSessionIsNeitherARowNorCounted()
    {
        LatestRunAnalysis analysis = Analyze(Context(
            [Pass("Alpha"), Pass("Beta")],
            [Skip("Alpha"), Fail("Beta")]));

        Assert.Equal("fp-Beta", Single(analysis).Test.TestFingerprint);
        Assert.Equal(1, analysis.TestsExecuted);
        Assert.Equal(1, analysis.TestsFailed);
    }

    [Fact]
    public void SessionsInWhichTheTestWasSkippedAreNotPriorAppearances()
    {
        // Skipped three times, passed twice, failed now: "passed the previous 2 runs", not 5.
        LatestRunAnalysis analysis = Analyze(Context(
            [Skip("Alpha")],
            [Pass("Alpha")],
            [Skip("Alpha")],
            [Pass("Alpha")],
            [Skip("Alpha")],
            [Fail("Alpha")]));

        LatestRunFailure row = Single(analysis);
        Assert.Equal(LatestRunStatus.New, row.Status);
        Assert.Equal(2, row.PriorSessions);
    }

    [Fact]
    public void ATestOnlyEverSkippedBeforeIsANewTest()
    {
        LatestRunAnalysis analysis = Analyze(Context(
            [Skip("Alpha")],
            [Skip("Alpha")],
            [Fail("Alpha")]));

        Assert.Equal(LatestRunStatus.NewTest, Single(analysis).Status);
    }

    // ---------------------------------------------------------------------------------------
    // D14 — retries settle on the final outcome
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestRescuedByARetryIsNotARow()
    {
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, "Alpha"),
            TestSessionFactory.Session(
                1,
                [
                    TestSessionFactory.Execution(
                        "Alpha", TestOutcome.Failed, attempt: 1, maxRetries: 2, errorMessage: "boom"),
                    TestSessionFactory.Execution(
                        "Alpha", TestOutcome.Passed, attempt: 2, maxRetries: 2, passedOnRetry: true)
                ]));

        LatestRunAnalysis analysis = Analyze(context);

        Assert.Empty(analysis.Failures);
        Assert.Equal(0, analysis.TestsFailed);
    }

    [Fact]
    public void ATestThatFailedEveryAttemptIsOneRowCarryingItsLastAttempt()
    {
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, "Alpha"),
            TestSessionFactory.Session(
                1,
                [
                    TestSessionFactory.Execution(
                        "Alpha", TestOutcome.Failed, attempt: 1, maxRetries: 1, errorMessage: "first"),
                    TestSessionFactory.Execution(
                        "Alpha", TestOutcome.Failed, attempt: 2, maxRetries: 1, errorMessage: "second")
                ]));

        LatestRunFailure row = Single(Analyze(context));

        Assert.Equal(LatestRunStatus.New, row.Status);
        Assert.Equal(2, row.Execution.Retry?.AttemptNumber);
    }

    [Fact]
    public void APriorSessionRescuedByARetryCountsAsAPass()
    {
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(
                0,
                [
                    TestSessionFactory.Execution(
                        "Alpha", TestOutcome.Failed, attempt: 1, maxRetries: 1, errorMessage: "boom"),
                    TestSessionFactory.Execution(
                        "Alpha", TestOutcome.Passed, attempt: 2, maxRetries: 1, passedOnRetry: true)
                ]),
            TestSessionFactory.Session(
                1, [TestSessionFactory.Execution("Alpha", TestOutcome.Failed, errorMessage: "boom")]));

        LatestRunFailure row = Single(Analyze(context));

        // The session ended green for this test, so it is a session it did not fail in — the same
        // answer SessionOutcomes gives about the session as a whole.
        Assert.Equal(LatestRunStatus.New, row.Status);
        Assert.Equal(1, row.PriorSessions);
        Assert.Equal(0, row.PriorFailures);
    }

    // ---------------------------------------------------------------------------------------
    // D4 — tests carrying a finding are suppressed and counted
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestNamedByAFindingIsCountedInsteadOfListed()
    {
        AnalysisContext context = Context(
            [Pass("Alpha"), Pass("Beta")],
            [Fail("Alpha"), Fail("Beta")]);

        LatestRunAnalysis analysis = LatestRunAnalyzer.Analyze(
            context, [FindingOn("f-1", "Alpha")])!;

        Assert.Equal("fp-Beta", Single(analysis).Test.TestFingerprint);
        Assert.Equal(1, analysis.ExplainedByFindings);
        Assert.Equal(["f-1"], analysis.ExplainingFindings.Select(f => f.Id));
        Assert.Equal(1, analysis.FailuresTotal);
        Assert.Equal(2, analysis.TestsFailed);
    }

    [Fact]
    public void AGroupFindingExplainsEveryMemberItNames()
    {
        AnalysisContext context = Context(
            [Pass("Alpha"), Pass("Beta"), Pass("Gamma")],
            [Fail("Alpha"), Fail("Beta"), Fail("Gamma")]);

        LatestRunAnalysis analysis = LatestRunAnalyzer.Analyze(
            context, [FindingOn("g-1", "Alpha", "Beta")])!;

        Assert.Equal("fp-Gamma", Single(analysis).Test.TestFingerprint);
        Assert.Equal(2, analysis.ExplainedByFindings);
        Assert.Equal(["g-1"], analysis.ExplainingFindings.Select(f => f.Id));
    }

    [Fact]
    public void ExplainingFindingsAreListedOnceEachInTheOrderGiven()
    {
        AnalysisContext context = Context(
            [Pass("Alpha"), Pass("Beta")],
            [Fail("Alpha"), Fail("Beta")]);

        // Two findings on Alpha, one of them shared with Beta: each finding appears once, in
        // ranked order, whatever order the tests were walked in.
        LatestRunAnalysis analysis = LatestRunAnalyzer.Analyze(
            context,
            [FindingOn("f-3", "Beta", "Alpha"), FindingOn("f-1", "Alpha"), FindingOn("f-2", "Beta")])!;

        Assert.Empty(analysis.Failures);
        Assert.Equal(2, analysis.ExplainedByFindings);
        Assert.Equal(["f-3", "f-1", "f-2"], analysis.ExplainingFindings.Select(f => f.Id));
    }

    [Fact]
    public void AFindingOnATestThatPassedExplainsNothing()
    {
        AnalysisContext context = Context(
            [Pass("Alpha"), Pass("Beta")],
            [Pass("Alpha"), Fail("Beta")]);

        LatestRunAnalysis analysis = LatestRunAnalyzer.Analyze(
            context, [FindingOn("f-1", "Alpha")])!;

        Assert.Equal("fp-Beta", Single(analysis).Test.TestFingerprint);
        Assert.Equal(0, analysis.ExplainedByFindings);
        Assert.Empty(analysis.ExplainingFindings);
    }

    // ---------------------------------------------------------------------------------------
    // D5 — single-session suppression
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AWindowOfOneSessionIsSuppressedButStillCounted()
    {
        LatestRunAnalysis analysis = Analyze(Context([Pass("Alpha"), Fail("Beta"), Fail("Gamma")]));

        Assert.True(analysis.Suppressed);
        Assert.Empty(analysis.Failures);
        Assert.Equal(0, analysis.FailuresTotal);
        Assert.Equal(3, analysis.TestsExecuted);
        Assert.Equal(2, analysis.TestsFailed);
    }

    [Fact]
    public void AWindowOfTwoSessionsIsNotSuppressed()
    {
        LatestRunAnalysis analysis = Analyze(Context([Pass("Alpha")], [Fail("Alpha")]));

        Assert.False(analysis.Suppressed);
        Assert.Single(analysis.Failures);
    }

    [Fact]
    public void AnEmptyWindowProducesNothing()
    {
        var window = AnalysisWindow.Create(
            [], DateTime.UnixEpoch, DateTime.UnixEpoch, WindowResolution.Default, null);

        Assert.Null(LatestRunAnalyzer.Analyze(new AnalysisContext(window, null), NoFindings));
    }

    // ---------------------------------------------------------------------------------------
    // D6 — environmental collapse
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEnvironmentalNewestSessionIsCountedAndNotItemised()
    {
        // Over both thresholds: 12 of 30 fail. Every one of them is a genuine "new" failure, and
        // none of them is listed.
        string[] names = [.. Enumerable.Range(0, 30).Select(i => $"T{i:00}")];
        AnalysisContext context = Context(
            [.. names.Select(Pass)],
            [.. names.Select((n, i) => i < 12 ? Fail(n) : Pass(n))]);

        LatestRunAnalysis analysis = Analyze(context);

        Assert.True(analysis.Session.IsLikelyEnvironmental);
        Assert.False(analysis.Suppressed);
        Assert.Empty(analysis.Failures);
        Assert.Equal(0, analysis.FailuresTotal);
        Assert.Equal(0, analysis.ExplainedByFindings);
        Assert.Equal(30, analysis.TestsExecuted);
        Assert.Equal(12, analysis.TestsFailed);
    }

    [Fact]
    public void AnEnvironmentalPriorSessionDoesNotCollapseTheNewestOne()
    {
        string[] names = [.. Enumerable.Range(0, 30).Select(i => $"T{i:00}")];
        AnalysisContext context = Context(
            [.. names.Select((n, i) => i < 12 ? Fail(n) : Pass(n))],
            [.. names.Select((n, i) => i == 0 ? Fail(n) : Pass(n))]);

        LatestRunAnalysis analysis = Analyze(context);

        Assert.False(analysis.Session.IsLikelyEnvironmental);
        LatestRunFailure row = Single(analysis);
        Assert.Equal(LatestRunStatus.SeenBefore, row.Status);
    }

    // ---------------------------------------------------------------------------------------
    // D7 — cap and --all
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void RowsBeyondTheCapAreWithheldAndCounted()
    {
        int many = LocalAnalysisConstants.LatestRunMaxRows + 3;
        string[] names = [.. Enumerable.Range(0, many).Select(i => $"T{i:00}")];

        // Thirteen failures clear the environmental count arm, so passing tests pad the session to
        // keep the rate arm under its threshold: the run has to be itemisable for the cap to show.
        string[] padding = [.. Enumerable.Range(0, 60).Select(i => $"P{i:00}")];
        AnalysisContext context = Context(
            [.. names.Concat(padding).Select(Pass)],
            [.. names.Select(Fail).Concat(padding.Select(Pass))]);

        LatestRunAnalysis analysis = Analyze(context);

        Assert.False(analysis.Session.IsLikelyEnvironmental);
        Assert.Equal(LocalAnalysisConstants.LatestRunMaxRows, analysis.Failures.Count);
        Assert.Equal(many, analysis.FailuresTotal);
        Assert.Equal(many, analysis.TestsFailed);
    }

    [Fact]
    public void NewFailuresAreCountedBeforeTheCap()
    {
        int many = LocalAnalysisConstants.LatestRunMaxRows + 3;
        string[] names = [.. Enumerable.Range(0, many).Select(i => $"T{i:00}")];
        string[] padding = [.. Enumerable.Range(0, 60).Select(i => $"P{i:00}")];
        AnalysisContext context = Context(
            [.. names.Concat(padding).Select(Pass)],
            [.. names.Select(Fail).Concat(padding.Select(Pass))]);

        LatestRunAnalysis analysis = Analyze(context);

        // Ten rows shown, thirteen new: the heading has to say thirteen.
        Assert.Equal(many, analysis.NewFailures);
        Assert.Equal(LocalAnalysisConstants.LatestRunMaxRows, analysis.Failures.Count);
    }

    [Fact]
    public void ASeenBeforeRowIsNotANewFailure()
    {
        AnalysisContext context = Context(
            [Fail("Known"), Pass("Regressed")],
            [Pass("Known"), Pass("Regressed")],
            [Fail("Known"), Fail("Regressed"), Fail("Fresh")]);

        LatestRunAnalysis analysis = Analyze(context);

        Assert.Equal(3, analysis.FailuresTotal);
        Assert.Equal(2, analysis.NewFailures);
    }

    [Fact]
    public void ShowAllLiftsTheCap()
    {
        int many = LocalAnalysisConstants.LatestRunMaxRows + 3;
        string[] names = [.. Enumerable.Range(0, many).Select(i => $"T{i:00}")];
        string[] padding = [.. Enumerable.Range(0, 60).Select(i => $"P{i:00}")];
        AnalysisContext context = Context(
            [.. names.Concat(padding).Select(Pass)],
            [.. names.Select(Fail).Concat(padding.Select(Pass))]);

        LatestRunAnalysis analysis = Analyze(context, showAll: true);

        Assert.Equal(many, analysis.Failures.Count);
        Assert.Equal(many, analysis.FailuresTotal);
    }

    [Fact]
    public void ExactlyTheCapIsNotTruncated()
    {
        int exactly = LocalAnalysisConstants.LatestRunMaxRows;
        string[] names = [.. Enumerable.Range(0, exactly).Select(i => $"T{i:00}")];
        string[] padding = [.. Enumerable.Range(0, 60).Select(i => $"P{i:00}")];
        AnalysisContext context = Context(
            [.. names.Concat(padding).Select(Pass)],
            [.. names.Select(Fail).Concat(padding.Select(Pass))]);

        LatestRunAnalysis analysis = Analyze(context);

        Assert.Equal(exactly, analysis.Failures.Count);
        Assert.Equal(exactly, analysis.FailuresTotal);
    }

    // ---------------------------------------------------------------------------------------
    // D11 — ordering
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void StatusesAreGroupedNewThenNewTestThenSeenBefore()
    {
        AnalysisContext context = Context(
            [Fail("Known"), Pass("Regressed")],
            [Pass("Known"), Pass("Regressed")],
            [Fail("Known"), Fail("Regressed"), Fail("Fresh")]);

        LatestRunAnalysis analysis = Analyze(context);

        Assert.Equal(
            [LatestRunStatus.New, LatestRunStatus.NewTest, LatestRunStatus.SeenBefore],
            analysis.Failures.Select(f => f.Status));
        Assert.Equal(
            ["fp-Regressed", "fp-Fresh", "fp-Known"],
            analysis.Failures.Select(f => f.Test.TestFingerprint));
    }

    [Fact]
    public void NewRowsRankTheLongestPassingHistoryFirst()
    {
        // Short passed once, Long passed three times, Mid passed twice.
        AnalysisContext context = Context(
            [Pass("Long")],
            [Pass("Long"), Pass("Mid")],
            [Pass("Long"), Pass("Mid"), Pass("Short")],
            [Fail("Short"), Fail("Long"), Fail("Mid")]);

        Assert.Equal(
            ["fp-Long", "fp-Mid", "fp-Short"],
            Analyze(context).Failures.Select(f => f.Test.TestFingerprint));
    }

    [Fact]
    public void NewTestRowsSortByShortName()
    {
        AnalysisContext context = Context(
            [Pass("Stable")],
            [Pass("Stable"), Fail("Zulu"), Fail("Alpha"), Fail("Mike")]);

        Assert.Equal(
            ["fp-Alpha", "fp-Mike", "fp-Zulu"],
            Analyze(context).Failures.Select(f => f.Test.TestFingerprint));
    }

    [Fact]
    public void SeenBeforeRowsRankTheMostFailuresFirst()
    {
        // Often failed 3 of 3 prior, Sometimes 1 of 3.
        AnalysisContext context = Context(
            [Fail("Often"), Fail("Sometimes")],
            [Fail("Often"), Pass("Sometimes")],
            [Fail("Often"), Pass("Sometimes")],
            [Fail("Sometimes"), Fail("Often")]);

        Assert.Equal(
            ["fp-Often", "fp-Sometimes"],
            Analyze(context).Failures.Select(f => f.Test.TestFingerprint));
    }

    [Fact]
    public void TiesBreakOnFingerprint()
    {
        // Same history for all three, so only the fingerprint separates them.
        AnalysisContext context = Context(
            [Pass("Charlie"), Pass("Alpha"), Pass("Bravo")],
            [Fail("Charlie"), Fail("Alpha"), Fail("Bravo")]);

        Assert.Equal(
            ["fp-Alpha", "fp-Bravo", "fp-Charlie"],
            Analyze(context).Failures.Select(f => f.Test.TestFingerprint));
    }

    [Fact]
    public void TheOrderDoesNotDependOnTheOrderSessionsWereRead()
    {
        TestSession older = TestSessionFactory.Session(
            0, [TestSessionFactory.Execution("Alpha"), TestSessionFactory.Execution("Beta")]);
        TestSession newer = TestSessionFactory.Session(
            1,
            [
                TestSessionFactory.Execution("Beta", TestOutcome.Failed, errorMessage: "boom"),
                TestSessionFactory.Execution("Alpha", TestOutcome.Failed, errorMessage: "boom")
            ]);

        IEnumerable<string> forward = Analyze(TestSessionFactory.Context(older, newer))
            .Failures.Select(f => f.Test.TestFingerprint);
        IEnumerable<string> reversed = Analyze(TestSessionFactory.Context(newer, older))
            .Failures.Select(f => f.Test.TestFingerprint);

        Assert.Equal(forward, reversed);
    }

    // ---------------------------------------------------------------------------------------
    // Consistency with the session view
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheFailureCountAgreesWithTheSessionView()
    {
        AnalysisContext context = Context(
            [Pass("Alpha"), Pass("Beta"), Pass("Gamma"), Pass("Delta")],
            [Fail("Alpha"), Skip("Beta"), Fail("Gamma"), Pass("Delta")]);

        LatestRunAnalysis analysis = Analyze(context);

        // A failing deciding attempt always has a verdict, so the two counts cannot differ; the
        // executed count can, because the view counts the skipped test and this does not.
        Assert.Equal(analysis.Session.Failures, analysis.TestsFailed);
        Assert.Equal(4, analysis.Session.Tests);
        Assert.Equal(3, analysis.TestsExecuted);
    }

    private sealed record StubEvidence : FindingEvidence;
}
